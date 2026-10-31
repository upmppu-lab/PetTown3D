using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Owns only runtime UI resources. Imported images and fonts are never modified.
public sealed class PetTownUITheme : IDisposable
{
    public readonly Color Ink=new Color(.30f,.20f,.12f),Muted=new Color(.50f,.42f,.32f),Cream=new Color(1,.97f,.89f),Mint=new Color(.27f,.58f,.15f);
    public PetTownArtPreviewData Style { get; private set; }
    bool ownsFont;
    public TMP_FontAsset Font { get; private set; }
    Material windowPaper,windowHeader;
    readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    readonly List<Texture2D> flatTextures=new List<Texture2D>();
    public bool Ready=>Font!=null;
    public PetTownUITheme()
    {
        Style=PetTownCozyLibrary.Current?.style;if(Style!=null&&Style.font!=null){Font=Style.font;return;}ownsFont=true;
        var source=Resources.Load<Font>("Fonts/NanumSquareB");
        if(source!=null)Font=TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
    }
    Sprite SpriteFor(string key)
    {
        if(sprites.TryGetValue(key,out var sprite))return sprite;
        if(PetTownFlatSkin.Handles(key)){Texture2D flatTexture;sprite=PetTownFlatSkin.Create(key,out flatTexture);flatTextures.Add(flatTexture);sprites.Add(key,sprite);return sprite;}
        if(Style!=null)
        {
            if(key.StartsWith("Icon_")){int index=int.Parse(key.Substring(5));var t=Style.iconAtlas;sprite=Sprite.Create(t,new Rect(index%4*t.width/4f,(2-index/4)*t.height/3f,t.width/4f,t.height/3f),new Vector2(.5f,.5f),100);sprites.Add(key,sprite);return sprite;}
            var source=key=="BarTrack"?Style.barTrack:key=="BarFill"?Style.barFill:key=="Header"?Style.header:key=="Selection"?Style.selectedCard:key=="Button"?Style.greenButton:key=="Light"?Style.lightButton:key=="Border"?Style.card:Style.paper;
            if(source==null)return null;
            // The imported sheets have zero borders. Slice owned runtime copies, never mutate the source sprites.
            float edge=key=="Panel"?80:key=="Header"?64:key.StartsWith("Bar")?22:72;
            edge=Mathf.Min(edge,Mathf.Min(source.rect.width,source.rect.height)*.25f);
            sprite=Sprite.Create(source.texture,source.rect,new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height),source.pixelsPerUnit,0,SpriteMeshType.FullRect,new Vector4(edge,edge,edge,edge));sprite.name="PetTown_Sliced_"+key;sprites.Add(key,sprite);return sprite;
        }
        var tex=Resources.Load<Texture2D>("UI/Kenney/"+key);
        if(tex==null)return null;
        sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,new Vector4(12,12,12,12));
        sprite.name="PetTown_Kenney_"+key;sprites.Add(key,sprite);return sprite;
    }
    public Sprite Icon(int index)=>SpriteFor("Icon_"+index);
    public Sprite Surface(string key)=>SpriteFor(key);
    public static RectTransform Rect(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;
    }
    public static void Place(RectTransform rt,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {
        rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=offsetMin;rt.offsetMax=offsetMax;
    }
    Material WindowSurface(bool header)
    {
        var current=header?windowHeader:windowPaper;if(current!=null)return current;
        var shader=Resources.Load<Shader>("UI/Cozy/WindowColors");if(shader==null)return null;
        current=new Material(shader){name=header?"PetTown_WindowHeader_Gray45":"PetTown_WindowPaper_Ivory",hideFlags=HideFlags.HideAndDontSave};
        current.SetFloat("_SurfaceMode",header?1:0);
        Color gray=new Color(.16f,.17f,.19f,1);
        if(QualitySettings.activeColorSpace==ColorSpace.Linear)gray=gray.linear;
        current.SetVector("_HeaderTint",new Vector4(gray.r,gray.g,gray.b,1));
        if(header)windowHeader=current;else windowPaper=current;return current;
    }
    public Image Panel(string name,Transform parent,Color color,string sprite="Panel")
    {
        var rt=Rect(name,parent);var image=rt.gameObject.AddComponent<Image>();image.color=Style!=null||PetTownFlatSkin.Handles(sprite)?Color.white:color;image.sprite=SpriteFor(sprite);image.type=Image.Type.Sliced;if(PetTownFlatSkin.Handles(sprite))image.pixelsPerUnitMultiplier=1;if(Style!=null){if(!PetTownFlatSkin.Handles(sprite))image.pixelsPerUnitMultiplier=2;if(sprite.StartsWith("Icon_")){image.type=Image.Type.Simple;image.preserveAspect=true;image.raycastTarget=false;}}
        if(!PetTownFlatSkin.Handles(sprite)&&(sprite=="Panel"||sprite=="Border"||sprite=="Header"))
        {
            image.material=WindowSurface(sprite=="Header");
            if(sprite=="Header")image.color=new Color(1,1,1,.45f);
        }
        return image;
    }
    public TextMeshProUGUI Text(string name,Transform parent,string value,float size=18,Color? color=null,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var rt=Rect(name,parent);var label=rt.gameObject.AddComponent<TextMeshProUGUI>();label.font=Font;label.fontSize=size;label.color=color??Ink;label.text=value;label.richText=false;label.alignment=align;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;return label;
    }
    public Button Button(string name,Transform parent,string value,Action action,Color? color=null)
    {
        var image=Panel(name,parent,color??Color.white,"Button");var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        if(color.HasValue)image.sprite=SpriteFor("Light");
        var colors=button.colors;colors.highlightedColor=new Color(1f,.98f,.94f);colors.pressedColor=new Color(.82f,.8f,.78f);colors.disabledColor=new Color(.66f,.66f,.66f,1f);button.colors=colors;
        var text=Text("Label",image.transform,value,17,color.HasValue?Ink:Color.white,TextAlignmentOptions.Center);Place(text.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,2),new Vector2(-8,-2));
        text.fontWeight=FontWeight.Bold;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;
        if(!color.HasValue)text.gameObject.AddComponent<PetTownTextOutline>().Set(.18f,new Color32(150,70,12,255));
        if(action!=null)button.onClick.AddListener(()=>action());return button;
    }
    public void Dispose()
    {
        if(windowPaper!=null)UnityEngine.Object.Destroy(windowPaper);if(windowHeader!=null)UnityEngine.Object.Destroy(windowHeader);
        foreach(var sprite in sprites.Values)if(sprite!=null)UnityEngine.Object.Destroy(sprite);sprites.Clear();
        foreach(var texture in flatTextures)if(texture!=null)UnityEngine.Object.Destroy(texture);flatTextures.Clear();
        if(Font==null||!ownsFont)return;
        foreach(var texture in Font.atlasTextures)if(texture!=null)UnityEngine.Object.Destroy(texture);
        if(Font.material!=null)UnityEngine.Object.Destroy(Font.material);UnityEngine.Object.Destroy(Font);Font=null;
    }
}
