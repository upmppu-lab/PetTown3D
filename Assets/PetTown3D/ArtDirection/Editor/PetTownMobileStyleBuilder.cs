using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// Author a separate warm, chunky mobile-game skin. Prior review artwork remains available.
public static class PetTownMobileStyleBuilder
{
    const string Root="Assets/PetTown3D/ArtDirection/MobileStyle";
    [MenuItem("PetTown/Art Direction/Build Rounded Mobile Style")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring UI.");
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        string path="Assets/PetTown3D/Resources/Fonts/JuaSDF.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(!font)
        {
            font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Jua-Regular"),64,12,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name="Jua Rounded Korean";AssetDatabase.CreateAsset(font,path);
            foreach(var tex in font.atlasTextures)AssetDatabase.AddObjectToAsset(tex,font);
            AssetDatabase.AddObjectToAsset(font.material,font);
        }
        string characters="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /.,:;!?+-−×%()[]↑↓←→★☆♥";
        foreach(var f in Directory.GetFiles("Assets/PetTown3D","*.cs",SearchOption.AllDirectories))characters+=File.ReadAllText(f);
        font.TryAddCharacters(new string(characters.Where(c=>!char.IsControl(c)).Distinct().ToArray()),out _);EditorUtility.SetDirty(font);
        var data=AssetDatabase.LoadAssetAtPath<PetTownArtPreviewData>("Assets/PetTown3D/ArtDirection/ReviewData.asset");data.font=font;
        data.paper=Chrome("WarmCreamFrame",new Color(1,.985f,.93f),new Color(.97f,.94f,.85f),0);
        data.header=Chrome("ToffeeHeader",new Color(.74f,.46f,.23f),new Color(.57f,.31f,.12f),1);
        data.card=Chrome("IvoryCard",new Color(1,.99f,.95f),new Color(.98f,.95f,.87f),2);
        data.selectedCard=Chrome("SelectedCard",new Color(.91f,.98f,.79f),new Color(.70f,.86f,.45f),2);
        data.greenButton=Chrome("LeafButton",new Color(.50f,.76f,.25f),new Color(.20f,.46f,.07f),3);
        data.lightButton=Chrome("HoneyButton",new Color(1,.91f,.65f),new Color(.89f,.71f,.38f),3);
        data.coralButton=Chrome("CloseButton",new Color(1,.91f,.61f),new Color(.91f,.70f,.36f),3);
        data.barTrack=Capsule("NeedTrack",false);data.barFill=Capsule("NeedFill",true);
        data.hudCircle=HudSprite("GlassCircle",true,false);data.hudChip=HudSprite("DarkHudChip",false,false);data.hudMessage=HudSprite("AdaptiveMessage",false,true);
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
    }
    static Sprite HudSprite(string name,bool circle,bool message)
    {
        int w=circle?256:512,h=circle?256:96;var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);float radius=circle?121:message?16:43;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            Vector2 p=new Vector2(x-(w-1)*.5f,y-(h-1)*.5f);float d;
            if(circle)d=p.magnitude-radius;else{Vector2 q=new Vector2(Mathf.Abs(p.x),Mathf.Abs(p.y))-new Vector2(w*.5f-5-radius,h*.5f-5-radius);d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;}
            Color c=circle?new Color(1,.96f,.80f,.16f):message?new Color(.13f,.085f,.05f,.91f):new Color(.24f,.14f,.075f,.95f);
            if(d>-3)c=new Color(1,.88f,.56f,circle ? .65f : .95f);else if(d>-5&&!circle)c=new Color(.49f,.30f,.13f,.98f);
            else if(circle)c.a+=Mathf.Pow(Mathf.Clamp01((y/(float)h-.35f)/.65f),3)*.09f;
            c.a*=Mathf.Clamp01(.5f-d);tex.SetPixel(x,y,c);
        }
        tex.Apply();string path=Root+"/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteBorder=circle?Vector4.zero:new Vector4(radius+6,radius+6,radius+6,radius+6);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite Capsule(string name,bool fill)
    {
        var tex=new Texture2D(256,64,TextureFormat.RGBA32,false);for(int y=0;y<64;y++)for(int x=0;x<256;x++){float d=new Vector2(x-Mathf.Clamp(x,32,223),y-31.5f).magnitude-30;Color c=fill?Color.Lerp(new Color(.86f,.86f,.86f),Color.white,y/63f):new Color(.88f,.83f,.70f);if(!fill&&d>-2)c=new Color(.65f,.50f,.29f);c.a=Mathf.Clamp01(.5f-d);tex.SetPixel(x,y,c);}tex.Apply();string path=Root+"/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteBorder=new Vector4(32,24,32,24);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite Chrome(string name,Color top,Color bottom,int style)
    {
        const int n=512;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);var pixels=new Color32[n*n];
        Color bark=new Color(.34f,.18f,.075f),wood=new Color(.52f,.29f,.11f),honey=new Color(.77f,.52f,.23f),rim=new Color(.92f,.76f,.46f);
        float radius=style==0?44:style==1?25:48;
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            Vector2 q=new Vector2(Mathf.Abs(x-255.5f),Mathf.Abs(y-255.5f))-Vector2.one*(247-radius);
            float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
            Color c=Color.Lerp(bottom,top,y/(n-1f));float thickness=style==0?25:style==1?6:style==2?8:14;
            if(distance>-thickness)
            {
                float d=-distance/thickness;
                c=d<.22f?bark:d<.48f?wood:d<.78f?honey:rim;
            }
            else if(style!=1&&distance>-thickness-4)c=Color.Lerp(c,Color.white,.45f);
            c.a=Mathf.Clamp01(.5f-distance);pixels[y*n+x]=c;
        }
        tex.SetPixels32(pixels);tex.Apply();string path=Root+"/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteBorder=new Vector4(64,64,64,64);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
