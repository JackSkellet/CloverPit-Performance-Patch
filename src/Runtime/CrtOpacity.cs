using System;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloverPitPatch
{
    public static class CrtOpacity
    {
        private static readonly Action<TMP_Text, float> WriteAlpha;
        private static readonly Func<TextMeshProUGUI, bool> HasCallbacks;
        private static readonly bool VerifyRequested;
        private static bool verified, verificationRunning, disabled;
        public static long FastCalls, FallbackCalls, ColorUploads;
        public static int VerificationPassed;

        static CrtOpacity()
        {
            VerifyRequested = Array.IndexOf(Environment.GetCommandLineArgs(), "--clover-patch-verify") >= 0;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo color = typeof(TMP_Text).GetField("m_fontColor", flags);
                FieldInfo alpha = typeof(Color).GetField("a");
                FieldInfo preRender = typeof(TextMeshProUGUI).GetField("OnPreRenderText", flags);
                FieldInfo dirty = typeof(Graphic).GetField("m_OnDirtyVertsCallback", flags);
                if (color == null || color.FieldType != typeof(Color) || preRender == null || dirty == null)
                    throw new InvalidOperationException("Unsupported TMP field layout");
                var write = new DynamicMethod("CloverPit_WriteFontAlpha", typeof(void), new[] { typeof(TMP_Text), typeof(float) }, typeof(CrtOpacity).Module, true);
                ILGenerator il = write.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, color);
                il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, alpha); il.Emit(OpCodes.Ret);
                WriteAlpha = (Action<TMP_Text, float>)write.CreateDelegate(typeof(Action<TMP_Text, float>));
                var callbacks = new DynamicMethod("CloverPit_AlphaCallbacks", typeof(bool), new[] { typeof(TextMeshProUGUI) }, typeof(CrtOpacity).Module, true);
                il = callbacks.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, preRender); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Cgt_Un);
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, dirty); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Cgt_Un);
                il.Emit(OpCodes.Or); il.Emit(OpCodes.Ret);
                HasCallbacks = (Func<TextMeshProUGUI, bool>)callbacks.CreateDelegate(typeof(Func<TextMeshProUGUI, bool>));
                Debug.Log("[CloverPitPatch] 2.0 opacity fast path ready; complex text keeps stock behavior.");
            }
            catch (Exception e)
            {
                disabled = true;
                Debug.LogWarning("[CloverPitPatch] Opacity acceleration unavailable; stock rendering retained: " + e.GetType().Name);
            }
        }

        public static void Apply(TextMeshProUGUI text, float alpha)
        {
            if (text.alpha == alpha) return;
            if (!disabled)
            {
                if (VerifyRequested && !verified && !verificationRunning && Eligible(text)) VerifyInUnity(text);
                if (!disabled && TryFast(text, alpha)) { FastCalls++; return; }
            }
            FallbackCalls++;
            text.alpha = alpha;
        }

        private static bool IsPlainStyle(TMP_Style style)
        {
            return style != null && string.IsNullOrEmpty(style.styleOpeningDefinition) &&
                string.IsNullOrEmpty(style.styleClosingDefinition) &&
                style.styleOpeningTagArray != null && style.styleOpeningTagArray.Length == 0 &&
                style.styleClosingTagArray != null && style.styleClosingTagArray.Length == 0;
        }

        private static bool Eligible(TextMeshProUGUI text)
        {
            if (WriteAlpha == null || HasCallbacks == null || text.GetType() != typeof(TextMeshProUGUI) ||
                !text.isActiveAndEnabled || text.havePropertiesChanged || HasCallbacks(text) ||
                text.enableVertexGradient || text.colorGradientPreset != null || text.textPreprocessor != null ||
                !IsPlainStyle(text.textStyle) || text.fontStyle != FontStyles.Normal ||
                text.geometrySortingOrder != VertexSortingOrder.Normal || text.isVolumetricText ||
                text.firstVisibleCharacter != 0 || text.overflowMode == TextOverflowModes.Page)
                return false;
            string source = text.text;
            if (source == null || (text.richText && source.IndexOf('<') >= 0)) return false;
            TMP_TextInfo info = text.textInfo;
            if (info == null || info.characterCount == 0 || info.characterInfo == null || info.meshInfo == null ||
                info.characterCount > info.characterInfo.Length || text.maxVisibleCharacters < info.characterCount ||
                text.maxVisibleWords < info.wordCount || text.maxVisibleLines < info.lineCount)
                return false;
            byte expected = ((Color32)text.color).a;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (c.elementType != TMP_TextElementType.Character || c.style != FontStyles.Normal) return false;
                if (!c.isVisible) continue;
                int material = c.materialReferenceIndex, vertex = c.vertexIndex;
                if (material < 0 || material >= info.materialCount || material >= info.meshInfo.Length || vertex < 0) return false;
                Color32[] colors = info.meshInfo[material].colors32;
                if (colors == null || vertex + 3 >= colors.Length || vertex + 3 >= info.meshInfo[material].vertexCount) return false;
                for (int j = 0; j < 4; j++) if (colors[vertex + j].a != expected) return false;
            }
            return true;
        }

        private static bool TryFast(TextMeshProUGUI text, float alpha)
        {
            if (!Eligible(text)) return false;
            byte oldAlpha = ((Color32)text.color).a;
            Color color = text.color; color.a = alpha;
            byte newAlpha = ((Color32)color).a;
            WriteAlpha(text, alpha);
            if (oldAlpha == newAlpha) return true;
            TMP_TextInfo info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (!info.characterInfo[i].isVisible) continue;
                TMP_CharacterInfo c = info.characterInfo[i];
                Color32[] colors = info.meshInfo[c.materialReferenceIndex].colors32;
                for (int j = 0; j < 4; j++) colors[c.vertexIndex + j].a = newAlpha;
                c.color.a = newAlpha;
                c.vertex_BL.color.a = newAlpha; c.vertex_TL.color.a = newAlpha;
                c.vertex_TR.color.a = newAlpha; c.vertex_BR.color.a = newAlpha;
                info.characterInfo[i] = c;
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            ColorUploads++;
            return true;
        }

        // Explicit verification launch only. Runs synchronously before rendering,
        // restores the original alpha, and uses the real shipped TMP generator.
        private static void VerifyInUnity(TextMeshProUGUI text)
        {
            verificationRunning = true;
            float original = text.alpha;
            try
            {
                float[] values = { 0f, 0.001f, 0.12345f, 0.5f, 0.8f, 0.9f, 0.999f, 1f };
                foreach (float value in values)
                {
                    text.alpha = value; text.ForceMeshUpdate();
                    TMP_TextInfo info = text.textInfo;
                    Color32[][] expected = new Color32[info.materialCount][];
                    Vector3[][] vertices = new Vector3[info.materialCount][];
                    Vector4[][] uvs = new Vector4[info.materialCount][];
                    for (int m = 0; m < info.materialCount; m++)
                    {
                        expected[m] = (Color32[])info.meshInfo[m].colors32.Clone();
                        vertices[m] = (Vector3[])info.meshInfo[m].vertices.Clone();
                        uvs[m] = (Vector4[])info.meshInfo[m].uvs0.Clone();
                    }
                    text.alpha = value == 0f ? 1f : 0f; text.ForceMeshUpdate();
                    if (!TryFast(text, value)) throw new InvalidOperationException("Native test text was not eligible");
                    for (int m = 0; m < info.materialCount; m++)
                        for (int j = 0; j < info.meshInfo[m].vertexCount; j++)
                        {
                            Color32 a = expected[m][j], b = info.meshInfo[m].colors32[j];
                            Vector3 v = vertices[m][j], w = info.meshInfo[m].vertices[j];
                            Vector4 u = uvs[m][j], z = info.meshInfo[m].uvs0[j];
                            if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a ||
                                v.x != w.x || v.y != w.y || v.z != w.z ||
                                u.x != z.x || u.y != z.y || u.z != z.z || u.w != z.w)
                                throw new InvalidOperationException("Native TMP mesh differs at alpha " + value);
                        }
                }
                VerificationPassed = values.Length;
                Debug.Log("[CloverPitPatch] Native TMP parity PASS: 8 alpha cases, zero-opacity recovery, identical RGBA/vertices/UVs.");
            }
            catch (Exception e)
            {
                disabled = true;
                Debug.LogWarning("[CloverPitPatch] Native TMP parity failed; stock opacity retained: " + e.Message);
            }
            finally
            {
                text.alpha = original; text.ForceMeshUpdate();
                verified = true; verificationRunning = false;
            }
        }
    }
}
