// Synthetic tests for the authored opacity helper; no game assembly is loaded.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CloverPitPatch;

internal static class OpacityTests
{
    static int cases,assertions;
    static void Check(bool ok,string why){assertions++;if(!ok)throw new Exception(why);}
    static void Run(string name,Action body){CrtOpacity.FastCalls=0;CrtOpacity.FallbackCalls=0;CrtOpacity.ColorUploads=0;body();cases++;Console.WriteLine("PASS "+name);}
    static byte Byte(float a)=>(byte)MathF.Round(Math.Clamp(a,0f,1f)*255f);
    static TextMeshProUGUI Fixture(TextMeshProUGUI t=null)
    {
        t??=new TextMeshProUGUI();
        var colors=new Color32[12];var pos=new Vector3[12];var uv=new Vector4[12];
        for(int i=0;i<12;i++){colors[i]=new Color32((byte)(30+i),(byte)(80+i),(byte)(150+i),i<8?(byte)255:(byte)77);pos[i]=new Vector3(i+1,i+2,i+3);uv[i]=new Vector4(i*.1f,i*.2f,i*.3f,i*.4f);}
        t.textInfo=new TMP_TextInfo{characterCount=3,materialCount=1,wordCount=1,lineCount=1,characterInfo=new TMP_CharacterInfo[3],meshInfo=new[]{new TMP_MeshInfo{colors32=colors,vertices=pos,uvs0=uv,vertexCount=8}}};
        for(int i=0;i<3;i++){var c=new Color32(31,82,153,255);t.textInfo.characterInfo[i]=new TMP_CharacterInfo{isVisible=i<2,vertexIndex=i*4,elementType=TMP_TextElementType.Character,color=c,vertex_BL=new TMP_Vertex{color=c},vertex_TL=new TMP_Vertex{color=c},vertex_TR=new TMP_Vertex{color=c},vertex_BR=new TMP_Vertex{color=c}};}
        t.havePropertiesChanged=false;return t;
    }
    static void Fast(TextMeshProUGUI t,float a)
    {
        long before=CrtOpacity.FastCalls;int writes=t.StockAlphaWrites;CrtOpacity.Apply(t,a);
        Check(CrtOpacity.FastCalls==before+1,"not fast");Check(t.StockAlphaWrites==writes,"stock alpha setter used");Check(t.alpha==a,"public alpha mismatch");Check(!t.havePropertiesChanged,"unexpected geometry dirtiness");
    }
    static void Fallback(string name,Action<TextMeshProUGUI> mutate)
    {
        Run("fallback/"+name,()=>{var t=Fixture();mutate(t);CrtOpacity.Apply(t,.4f);Check(CrtOpacity.FallbackCalls==1,"not stock fallback");Check(CrtOpacity.FastCalls==0,"unexpected fast path");Check(t.StockAlphaWrites==1,"stock setter skipped");Check(t.alpha==.4f,"fallback alpha mismatch");Check(t.havePropertiesChanged,"fallback lost dirty flag");Check(t.Uploads==0,"fallback uploaded stale mesh");});
    }
    class Preprocessor:ITextPreprocessor{}
    class DerivedText:TextMeshProUGUI{}
    public static void RunAll()
    {
        cases=0; assertions=0;
        float[] values={0f,.001f,.12345f,.5f,.8f,.9f,.999f,1f,0f,.9f};
        var sequence=Fixture();var original=(Color32[])sequence.textInfo.meshInfo[0].colors32.Clone();var positions=(Vector3[])sequence.textInfo.meshInfo[0].vertices.Clone();var uvs=(Vector4[])sequence.textInfo.meshInfo[0].uvs0.Clone();
        for(int step=0;step<values.Length;step++)
        {
            float a=values[step];Run("alpha-sequence/"+step+"/"+a,()=>{
                Fast(sequence,a);var m=sequence.textInfo.meshInfo[0];
                for(int j=0;j<m.colors32.Length;j++){var c=m.colors32[j];Check(c.r==original[j].r&&c.g==original[j].g&&c.b==original[j].b,"RGB changed");Check(c.a==(j<8?Byte(a):original[j].a),"alpha or unused capacity changed");Check(m.vertices[j].Equals(positions[j]),"geometry changed");Check(m.uvs0[j].Equals(uvs[j]),"UV changed");}
                for(int j=0;j<2;j++){var c=sequence.textInfo.characterInfo[j];Check(c.color.a==Byte(a)&&c.vertex_BL.color.a==Byte(a)&&c.vertex_TL.color.a==Byte(a)&&c.vertex_TR.color.a==Byte(a)&&c.vertex_BR.color.a==Byte(a),"character alpha snapshot stale");}
                Check(sequence.LastUpload==TMP_VertexDataUpdateFlags.Colors32,"wrong upload flags");
            });
        }
        Run("same-byte-float-update",()=>{var t=Fixture();Fast(t,.5000f);int n=t.Uploads;Fast(t,.5001f);Check(t.Uploads==n,"uploaded identical byte");Check(t.alpha==.5001f,"precise float alpha lost");});
        Run("identical-alpha-noop",()=>{var t=Fixture();CrtOpacity.Apply(t,1f);Check(t.StockAlphaWrites==0&&t.Uploads==0&&CrtOpacity.FastCalls==0&&CrtOpacity.FallbackCalls==0,"same value not a noop");});
        Fallback("rich-tags",t=>{t.text="<color=red>AB</color>";t.havePropertiesChanged=false;});
        Run("literal-markup-richtext-disabled",()=>{var t=Fixture();t.text="A<B";t.richText=false;t.havePropertiesChanged=false;Fast(t,.6f);});
        Fallback("vertex-gradient",t=>t.enableVertexGradient=true);
        Fallback("gradient-preset",t=>t.colorGradientPreset=new TMP_ColorGradient());
        Fallback("sprite",t=>t.textInfo.characterInfo[0].elementType=TMP_TextElementType.Sprite);
        foreach(FontStyles flag in new[]{FontStyles.Underline,FontStyles.Strikethrough,FontStyles.Highlight})
        {var f=flag;Fallback("component-"+f,t=>t.fontStyle=f);Fallback("character-"+f,t=>t.textInfo.characterInfo[0].style=f);}
        Fallback("preprocessor",t=>t.textPreprocessor=new Preprocessor());
        Run("separate-empty-normal-style",()=>{var t=Fixture();t.textStyle=new TMP_Style();Fast(t,.6f);Check(CrtOpacity.FallbackCalls==0,"equivalent normal style rejected");});
        Fallback("style-has-stale-tags",t=>t.textStyle=new TMP_Style{styleOpeningTagArray=new uint[]{60}});
        Fallback("style-null-tag-arrays",t=>t.textStyle=new TMP_Style{styleOpeningTagArray=null});
        Fallback("custom-style",t=>t.textStyle=new TMP_Style { styleOpeningDefinition="<color=red>",styleOpeningTagArray=new uint[]{60} });
        Fallback("pre-render-callback",t=>t.OnPreRenderText+=_=>{});
        Fallback("dirty-callback",t=>t.RegisterDirtyVerticesCallback(()=>{}));
        Fallback("dirty-properties",t=>t.havePropertiesChanged=true);
        Fallback("null-textInfo",t=>t.textInfo=null);
        Fallback("null-mesh-array",t=>t.textInfo.meshInfo=null);
        Fallback("null-colors",t=>t.textInfo.meshInfo[0].colors32=null);
        Fallback("null-character-array",t=>t.textInfo.characterInfo=null);
        Fallback("vertex-buffer-short",t=>t.textInfo.meshInfo[0].vertexCount=3);
        Fallback("invalid-material-index",t=>t.textInfo.characterInfo[0].materialReferenceIndex=1);
        Fallback("negative-vertex-index",t=>t.textInfo.characterInfo[0].vertexIndex=-1);
        Fallback("character-count-overflow",t=>t.textInfo.characterCount=4);
        Fallback("empty-generated-text",t=>t.textInfo.characterCount=0);
        Fallback("null-source",t=>{t.text=null;t.havePropertiesChanged=false;});
        Fallback("disabled",t=>t.isActiveAndEnabled=false);
        Fallback("reversed",t=>t.geometrySortingOrder=VertexSortingOrder.Reverse);
        Fallback("volumetric",t=>t.isVolumetricText=true);
        Fallback("first-visible",t=>t.firstVisibleCharacter=1);
        Fallback("paged-overflow",t=>t.overflowMode=TextOverflowModes.Page);
        Fallback("max-characters",t=>t.maxVisibleCharacters=1);
        Fallback("max-words",t=>t.maxVisibleWords=0);
        Fallback("max-lines",t=>t.maxVisibleLines=0);
        Fallback("nonuniform-vertex-alpha",t=>t.textInfo.meshInfo[0].colors32[1].a=100);
        Run("derived-type-fallback",()=>{var t=Fixture(new DerivedText());CrtOpacity.Apply(t,.2f);Check(CrtOpacity.FallbackCalls==1&&t.StockAlphaWrites==1,"subclass did not fallback");});
        Run("content-change-dirty-then-fast-recovery",()=>{var t=Fixture();Fast(t,.8f);t.text="XY ";CrtOpacity.Apply(t,.5f);Check(t.havePropertiesChanged&&CrtOpacity.FallbackCalls==1,"content dirtiness lost");t.ForceMeshUpdate();Fast(t,.9f);Check(t.text=="XY ","content overwritten");});
        Run("external-RGB-change-then-fast-recovery",()=>{var t=Fixture();Fast(t,.8f);t.color=new Color(.9f,.1f,.2f,.7f);CrtOpacity.Apply(t,.5f);Check(CrtOpacity.FallbackCalls==1&&t.havePropertiesChanged,"RGB change lost");t.ForceMeshUpdate();Color32 expected=t.color;Fast(t,.9f);foreach(var c in t.textInfo.meshInfo[0].colors32[..8])Check(c.r==expected.r&&c.g==expected.g&&c.b==expected.b,"external RGB overwritten");});
        Run("content-change-at-zero-then-positive",()=>{var t=Fixture();Fast(t,0f);t.text="ZERO";t.ForceMeshUpdate();Fast(t,.9f);foreach(var c in t.textInfo.meshInfo[0].colors32[..8])Check(c.a==Byte(.9f),"zero recovery failed");});
        Run("callback-content-dirtiness-preserved",()=>{var t=Fixture();int fired=0;t.RegisterDirtyVerticesCallback(()=>{fired++;t.text="CALLBACK";});CrtOpacity.Apply(t,.4f);Check(fired==1&&t.text=="CALLBACK"&&t.havePropertiesChanged,"callback update was erased");});
        Run("callback-removal-fast-recovery",()=>{var t=Fixture();Action<TMP_TextInfo> cb=_=>{};t.OnPreRenderText+=cb;CrtOpacity.Apply(t,.4f);t.OnPreRenderText-=cb;t.ForceMeshUpdate();Fast(t,.6f);});
        Run("disable-enable-dirty-rebuild-recovery",()=>{var t=Fixture();t.isActiveAndEnabled=false;CrtOpacity.Apply(t,.3f);t.isActiveAndEnabled=true;t.havePropertiesChanged=true;CrtOpacity.Apply(t,.4f);Check(CrtOpacity.FallbackCalls==2,"enable dirty state not preserved");t.ForceMeshUpdate();Fast(t,.7f);});
        Console.WriteLine("RESULT cases="+cases+" assertions="+assertions+" failed=0");
        Console.WriteLine("Scope: production helper source unchanged; synthetic Unity/TMP fixtures. Not native Unity rendering proof.");
    }
}
