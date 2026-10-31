using System.Collections.Generic;
using UnityEngine;

// Flat, warm window skin (brown frame, cream paper, orange buttons) generated procedurally as nine-sliced sprites.
// One canvas unit = Scale texels, so edges stay crisp at any screen density. Owned and released by PetTownUITheme.
public static class PetTownFlatSkin
{
    const int Scale=3;
    struct Ring{public float width;public Color color;public Ring(float w,Color c){width=w;color=c;}}
    static Color C(int r,int g,int b)=>new Color32((byte)r,(byte)g,(byte)b,255);

    public static bool Handles(string key)=>!string.IsNullOrEmpty(key)&&!key.StartsWith("Icon_")&&!key.StartsWith("Bar");

    // Returns an owned sprite (the caller keeps and destroys it with its texture).
    public static Sprite Create(string key,out Texture2D texture)
    {
        switch(key)
        {
            case "Header":
                return Box(key,out texture,96,56,16,22,new[]{new Ring(1.6f,C(0x58,0x34,0x18))},C(0xB9,0x80,0x4A),C(0x8E,0x5C,0x30),0,Color.clear,true);
            case "Border":
                return Box(key,out texture,64,64,16,22,new[]{new Ring(2.2f,C(0xE3,0xC9,0x94))},C(0xFF,0xFE,0xF9),C(0xFF,0xFA,0xEE),0,Color.clear,false);
            case "Selection":
                return Box(key,out texture,64,64,16,22,new[]{new Ring(2.6f,C(0x5C,0xAE,0x3E))},C(0xF1,0xFA,0xE6),C(0xE4,0xF4,0xD3),0,Color.clear,false);
            case "Button":
                return Box(key,out texture,64,44,14,18,new[]{new Ring(2.4f,C(0xA9,0x4C,0x0C))},C(0xFF,0xB5,0x5C),C(0xF4,0x82,0x1F),4f,C(0xDA,0x68,0x12),true);
            case "Light":
                return Box(key,out texture,64,44,14,18,new[]{new Ring(2.2f,C(0xC7,0x9C,0x60))},C(0xFF,0xFF,0xFF),C(0xF8,0xEC,0xD3),3.4f,C(0xE9,0xD4,0xA9),true);
            default: // "Panel" and anything else: window frame with a cream body
                return Box(key,out texture,96,96,28,36,new[]{new Ring(6.5f,C(0x6E,0x41,0x20)),new Ring(2.4f,C(0xDB,0xA7,0x5C))},C(0xFF,0xF9,0xEC),C(0xFF,0xF9,0xEC),0,Color.clear,false);
        }
    }

    // Rounded box with concentric rings, a vertical fill gradient, an optional darker bottom lip and a top gloss line.
    static Sprite Box(string key,out Texture2D texture,int wu,int hu,float radius,int border,Ring[] rings,Color top,Color bottom,float lip,Color lipColor,bool gloss)
    {
        int w=wu*Scale,h=hu*Scale;var pixels=new Color[w*h];float cx=wu*.5f,cy=hu*.5f;
        float inset=0;foreach(var r in rings)inset+=r.width;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float u=(x+.5f)/Scale,v=(y+.5f)/Scale;
            float qx=Mathf.Abs(u-cx)-(cx-radius),qy=Mathf.Abs(v-cy)-(cy-radius);
            float d=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
            Color pixel=Color.clear;float used=0;
            foreach(var r in rings){pixel=Over(pixel,r.color,Mathf.Clamp01(.5f-(d+used)*Scale));used+=r.width;}
            float cover=Mathf.Clamp01(.5f-(d+used)*Scale);
            if(cover>0)
            {
                float t=Mathf.Clamp01((v-used)/Mathf.Max(.01f,hu-used*2));Color fill=Color.Lerp(bottom,top,t);
                if(lip>0){float lipMix=Mathf.Clamp01((used+lip-v)*Scale+.5f);fill=Color.Lerp(fill,lipColor,lipMix);}
                if(gloss){float from=hu-used-1.1f,to=hu-used-2.7f;float strip=Mathf.Clamp01((from-v)*Scale+.5f)*Mathf.Clamp01((v-to)*Scale+.5f);fill=Color.Lerp(fill,Color.white,strip*.38f);}
                pixel=Over(pixel,fill,cover);
            }
            pixels[y*w+x]=pixel;
        }
        texture=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,name="PetTown_FlatSkin_"+key};texture.SetPixels(pixels);texture.Apply(false,false);
        var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100f*Scale,0,SpriteMeshType.FullRect,new Vector4(border*Scale,border*Scale,border*Scale,border*Scale));
        sprite.name="PetTown_FlatSkin_"+key;return sprite;
    }
    static Color Over(Color dst,Color src,float coverage)
    {
        float a=coverage*src.a;if(a<=0)return dst;float outA=a+dst.a*(1-a);
        return new Color((src.r*a+dst.r*dst.a*(1-a))/outA,(src.g*a+dst.g*dst.a*(1-a))/outA,(src.b*a+dst.b*dst.a*(1-a))/outA,outA);
    }
}
