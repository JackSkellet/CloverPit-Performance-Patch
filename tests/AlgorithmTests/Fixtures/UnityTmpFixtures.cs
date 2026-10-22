// Minimal authored fixtures for this test suite, not Unity or TMP implementation code.
using System;

namespace UnityEngine
{
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1f){this.r=r;this.g=g;this.b=b;this.a=a;} }
    public struct Color32
    {
        public byte r,g,b,a;
        public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}
        private static byte Channel(float value)=>(byte)MathF.Round(Math.Clamp(value,0f,1f)*255f);
        public static implicit operator Color32(Color c)=>new Color32(Channel(c.r),Channel(c.g),Channel(c.b),Channel(c.a));
    }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Vector4 { public float x,y,z,w; public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;} }
    public static class Debug { public static void Log(object x)=>Console.WriteLine(x); public static void LogWarning(object x)=>Console.WriteLine(x); }
}
namespace UnityEngine.Events { public delegate void UnityAction(); }
namespace UnityEngine.UI
{
    public class Graphic
    {
        protected UnityEngine.Events.UnityAction m_OnDirtyVertsCallback;
        public void RegisterDirtyVerticesCallback(UnityEngine.Events.UnityAction callback){m_OnDirtyVertsCallback+=callback;}
        public void UnregisterDirtyVerticesCallback(UnityEngine.Events.UnityAction callback){m_OnDirtyVertsCallback-=callback;}
        protected void FireDirty()=>m_OnDirtyVertsCallback?.Invoke();
    }
}
namespace TMPro
{
    using UnityEngine;
    [Flags] public enum FontStyles { Normal=0,Bold=1,Italic=2,Underline=4,Strikethrough=0x40,Highlight=0x200 }
    public enum VertexSortingOrder { Normal,Reverse }
    public enum TextOverflowModes { Overflow,Page }
    public enum TMP_TextElementType { Character,Sprite }
    [Flags] public enum TMP_VertexDataUpdateFlags { Colors32=16 }
    public interface ITextPreprocessor { }
    public class TMP_ColorGradient { }
    public class TMP_Style { public static readonly TMP_Style NormalStyle=new TMP_Style(); public string styleOpeningDefinition="",styleClosingDefinition=""; public uint[] styleOpeningTagArray=new uint[0],styleClosingTagArray=new uint[0]; }
    public struct TMP_Vertex { public Color32 color; }
    public struct TMP_CharacterInfo
    {
        public TMP_TextElementType elementType; public FontStyles style; public bool isVisible;
        public int materialReferenceIndex,vertexIndex; public Color32 color;
        public TMP_Vertex vertex_BL,vertex_TL,vertex_TR,vertex_BR;
    }
    public struct TMP_MeshInfo { public Color32[] colors32; public Vector3[] vertices; public Vector4[] uvs0; public int vertexCount; }
    public class TMP_TextInfo { public int characterCount,materialCount,wordCount,lineCount; public TMP_CharacterInfo[] characterInfo; public TMP_MeshInfo[] meshInfo; }
    public class TMP_Text : UnityEngine.UI.Graphic
    {
        // Exact declared field owner/type used by the production DynamicMethod.
        protected Color m_fontColor=new Color(.2f,.3f,.4f,1f);
        public bool havePropertiesChanged; public int StockAlphaWrites;
        public float alpha { get=>m_fontColor.a; set { if(m_fontColor.a!=value){m_fontColor.a=value;StockAlphaWrites++;havePropertiesChanged=true;FireDirty();} } }
        public Color color { get=>m_fontColor; set {m_fontColor=value;havePropertiesChanged=true;FireDirty();} }
    }
    public class TextMeshProUGUI : TMP_Text
    {
        // Real production event creates this exact Action<TMP_TextInfo> backing field.
        public event Action<TMP_TextInfo> OnPreRenderText;
        public bool isActiveAndEnabled=true,enableVertexGradient,richText=true,isVolumetricText;
        public TMP_ColorGradient colorGradientPreset; public ITextPreprocessor textPreprocessor;
        public TMP_Style textStyle=TMP_Style.NormalStyle; public FontStyles fontStyle=FontStyles.Normal;
        public VertexSortingOrder geometrySortingOrder=VertexSortingOrder.Normal;
        public int firstVisibleCharacter,maxVisibleCharacters=int.MaxValue,maxVisibleWords=int.MaxValue,maxVisibleLines=int.MaxValue;
        public TextOverflowModes overflowMode=TextOverflowModes.Overflow;
        private string source="AB "; public string text {get=>source;set{source=value;havePropertiesChanged=true;}}
        public TMP_TextInfo textInfo; public int Uploads,Rebuilds; public TMP_VertexDataUpdateFlags LastUpload;
        public void UpdateVertexData(TMP_VertexDataUpdateFlags flags){Uploads++;LastUpload=flags;}
        // This is a synthetic plain-text rebuild, not Unity's glyph generator.
        public void ForceMeshUpdate()
        {
            Rebuilds++;Color32 c=color;
            if(textInfo?.characterInfo!=null && textInfo.meshInfo!=null)
                for(int i=0;i<textInfo.characterCount;i++)
                {
                    var ch=textInfo.characterInfo[i];if(!ch.isVisible)continue;
                    for(int j=0;j<4;j++)textInfo.meshInfo[ch.materialReferenceIndex].colors32[ch.vertexIndex+j]=c;
                    ch.color=c;ch.vertex_BL.color=c;ch.vertex_TL.color=c;ch.vertex_TR.color=c;ch.vertex_BR.color=c;textInfo.characterInfo[i]=ch;
                }
            havePropertiesChanged=false;OnPreRenderText?.Invoke(textInfo);
        }
    }
}
