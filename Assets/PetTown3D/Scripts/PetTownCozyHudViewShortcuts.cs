using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Simple, high-contrast HUD buttons: a coloured disc with a white glyph and a white-on-dark label pill.
// Covers the eight main menu buttons and the facility shortcut column (farm, ranch, park/zoo, cafe, hospital).
public sealed partial class PetTownCozyHudView
{
    public const int ShortcutRouteBase=101;
    static readonly string[] ShortcutNames={"농장","목장","공원·동물원","펫 카페","펫 병원"};
    static readonly string[] NavNames={"건설","꾸미기","가방","펫샵 관리","캐릭터","주민","펫","설정","길","펫 사기"};
    static readonly PetTownGlyph[] NavGlyphs={PetTownGlyph.Hammer,PetTownGlyph.Armchair,PetTownGlyph.Bag,PetTownGlyph.Shop,PetTownGlyph.Person,PetTownGlyph.People,PetTownGlyph.Paw,PetTownGlyph.Gear,PetTownGlyph.Road,PetTownGlyph.Paw};
    static readonly Color[] NavColors={Hex(0xE5,0x8B,0x2A),Hex(0xC8,0x69,0x4B),Hex(0x7A,0x6A,0xD8),Hex(0x3F,0x8F,0xD9),Hex(0x8E,0x7A,0xE0),Hex(0x5B,0x8D,0xEF),Hex(0xE7,0x7F,0x6B),Hex(0x6C,0x7A,0x89),Hex(0x8A,0x73,0x55),Hex(0xE0,0x5A,0x8A)};
    static readonly Color ShortcutLocked=Hex(0x8D,0x91,0x9C),HudInk=new Color32(0x1F,0x22,0x30,235),HudActiveRing=Hex(0xFF,0xD8,0x5A);
    const float ButtonWidth=68,ButtonHeight=70;
    const float DiscAlpha=.8f;
    static readonly Color PlateColor=new Color32(0x1F,0x22,0x30,178);
    static Color WithAlpha(Color c,float a){c.a=a;return c;}
    sealed class HudVisual{public Image disc,glyph,ring,badge;public TextMeshProUGUI badgeText;}
    readonly HudVisual[] navVisual=new HudVisual[10],shortcutVisual=new HudVisual[PetTownGlyphs.FacilityCount];
    readonly RectTransform[] shortcutRoots=new RectTransform[PetTownGlyphs.FacilityCount];
    float nextShortcutRefresh;
    static Color Hex(int r,int g,int b)=>new Color32((byte)r,(byte)g,(byte)b,255);

    HudVisual MakeVisual(RectTransform root,Sprite glyph,Color color,string text)
    {
        var bold=theme.Font.fontWeightTable[7].regularTypeface;var v=new HudVisual();
        root.gameObject.AddComponent<PressScale>();
        v.ring=Disc("Ring",root,new Color(1,.85f,.35f,0),54,Vector2.zero);
        v.disc=Disc("Disc",root,WithAlpha(color,DiscAlpha),48,Vector2.zero);
        v.glyph=PetTownUITheme.Rect("Glyph",root).gameObject.AddComponent<Image>();v.glyph.sprite=glyph;v.glyph.preserveAspect=true;v.glyph.raycastTarget=false;
        Anchor(v.glyph.rectTransform,new Vector2(.5f,1),new Vector2(0,-26),new Vector2(32,32));
        v.badge=Disc("Badge",root,HudInk,20,Vector2.zero);Anchor(v.badge.rectTransform,new Vector2(.5f,1),new Vector2(19,-8),new Vector2(20,20));
        v.badgeText=theme.Text("BadgeText",v.badge.transform,"+",12,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(v.badgeText.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);v.badgeText.fontStyle=FontStyles.Bold;v.badgeText.textWrappingMode=TextWrappingModes.NoWrap;if(bold)v.badgeText.font=bold;
        v.badge.gameObject.SetActive(false);
        // White bold text on a dark pill keeps contrast over grass, water and buildings.
        var pill=PetTownUITheme.Rect("LabelPill",root).gameObject.AddComponent<Image>();pill.sprite=PetTownGlyphs.Plate();pill.type=Image.Type.Sliced;pill.color=PlateColor;pill.raycastTarget=false;
        var label=theme.Text("Label",pill.transform,text,11.5f,Color.white,TextAlignmentOptions.Center);label.fontStyle=FontStyles.Bold;label.textWrappingMode=TextWrappingModes.NoWrap;if(bold)label.font=bold;
        // Plate width follows the text, and the plate overlaps the bottom of the icon slightly.
        float plateWidth=Mathf.Ceil(label.GetPreferredValues(text,0,0).x)+12;Anchor(pill.rectTransform,new Vector2(.5f,0),new Vector2(0,9),new Vector2(plateWidth,19));PetTownUITheme.Place(label.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        return v;
    }
    Image Disc(string name,Transform parent,Color color,float size,Vector2 offset)
    {
        var image=PetTownUITheme.Rect(name,parent).gameObject.AddComponent<Image>();image.sprite=PetTownGlyphs.Disc();image.color=color;image.raycastTarget=false;
        Anchor(image.rectTransform,new Vector2(.5f,1),new Vector2(offset.x,-size*.5f-2+offset.y),new Vector2(size,size));return image;
    }
    static void Anchor(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor.y>.5f?new Vector2(.5f,.5f):new Vector2(.5f,0);rect.anchoredPosition=position;rect.sizeDelta=size;
    }

    // Replaces the old circle-sprite + atlas icon + outlined label of main button i.
    void NavVisual(RectTransform root,int i){navVisual[i]=MakeVisual(root,PetTownGlyphs.Glyph(NavGlyphs[i]),NavColors[i],NavNames[i]);}
    void ApplyNavState(int[] routes,int active)
    {
        for(int i=0;i<navVisual.Length;i++){if(navVisual[i]==null)continue;bool on=routes[i]==active;navVisual[i].ring.color=on?HudActiveRing:new Color(1,.85f,.35f,0);navVisual[i].disc.color=WithAlpha(navVisual[i].disc.color,on?1f:DiscAlpha);}
    }


    const float CompactSize=42;
    // Small round icon without a label: coloured disc, white glyph, tiny count or "+" badge.
    HudVisual MakeCompact(RectTransform root,Sprite glyph,Color color)
    {
        var bold=theme.Font.fontWeightTable[7].regularTypeface;var v=new HudVisual();root.gameObject.AddComponent<PressScale>();
        v.ring=CenterDisc("Ring",root,new Color(1,.85f,.35f,0),CompactSize+2,Vector2.zero);
        v.disc=CenterDisc("Disc",root,WithAlpha(color,DiscAlpha),CompactSize-2,Vector2.zero);
        v.glyph=PetTownUITheme.Rect("Glyph",root).gameObject.AddComponent<Image>();v.glyph.sprite=glyph;v.glyph.preserveAspect=true;v.glyph.raycastTarget=false;
        v.glyph.rectTransform.anchorMin=v.glyph.rectTransform.anchorMax=new Vector2(.5f,.5f);v.glyph.rectTransform.sizeDelta=new Vector2(25,25);v.glyph.rectTransform.anchoredPosition=Vector2.zero;
        v.badge=CenterDisc("Badge",root,HudInk,16,new Vector2(CompactSize*.5f-5,CompactSize*.5f-5));
        v.badgeText=theme.Text("BadgeText",v.badge.transform,"+",10.5f,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(v.badgeText.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        v.badgeText.fontStyle=FontStyles.Bold;v.badgeText.textWrappingMode=TextWrappingModes.NoWrap;if(bold)v.badgeText.font=bold;v.badge.gameObject.SetActive(false);
        return v;
    }
    Image CenterDisc(string name,Transform parent,Color color,float size,Vector2 offset)
    {
        var image=PetTownUITheme.Rect(name,parent).gameObject.AddComponent<Image>();image.sprite=PetTownGlyphs.Disc();image.color=color;image.raycastTarget=false;
        image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,.5f);image.rectTransform.pivot=new Vector2(.5f,.5f);image.rectTransform.sizeDelta=new Vector2(size,size);image.rectTransform.anchoredPosition=offset;return image;
    }

    void BuildShortcuts(Action<int> navigate)
    {
        for(int i=0;i<PetTownGlyphs.FacilityCount;i++)
        {
            int route=ShortcutRouteBase+i;
            var root=PetTownUITheme.Rect("FacilityShortcut_"+i,nav);shortcutRoots[i]=root;
            var hit=root.gameObject.AddComponent<Image>();hit.color=new Color(0,0,0,0);hit.raycastTarget=true;
            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>navigate(route));
            shortcutVisual[i]=MakeCompact(root,PetTownGlyphs.Glyph(i),PetTownGlyphs.Tint((PetTownGlyph)i));
        }
        ApplyShortcutState(null);
    }
    void LayoutShortcuts()
    {
        // Small icons in a horizontal row at the top right, under the reputation chip.
        int count=shortcutRoots.Length;for(int i=0;i<count;i++)if(shortcutRoots[i])Fixed(shortcutRoots[i],Vector2.one,Vector2.one,new Vector2(-10-(count-1-i)*(CompactSize+4),-82),new Vector2(CompactSize,CompactSize));
    }
    void RefreshShortcuts()
    {
        if(Time.unscaledTime<nextShortcutRefresh)return;nextShortcutRefresh=Time.unscaledTime+.5f;
        var game=PetTownGame.Instance;if(game!=null)ApplyShortcutState(game);
    }
    void ApplyShortcutState(PetTownGame game)
    {
        for(int i=0;i<PetTownGlyphs.FacilityCount;i++)
        {
            var v=shortcutVisual[i];if(v==null)continue;
            int count=game!=null?game.FacilityShortcutCount(ShortcutRouteBase+i):0;bool built=count>0;
            v.disc.color=WithAlpha(built?PetTownGlyphs.Tint((PetTownGlyph)i):ShortcutLocked,DiscAlpha);v.glyph.color=built?Color.white:new Color(1,1,1,.8f);
            v.badge.gameObject.SetActive(!built||count>1);v.badgeText.text=built?count.ToString():"+";
        }
    }

    // Small press feedback so taps feel responsive.
    sealed class PressScale:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e){transform.localScale=Vector3.one*.93f;}
        public void OnPointerUp(PointerEventData e){transform.localScale=Vector3.one;}
        public void OnPointerExit(PointerEventData e){transform.localScale=Vector3.one;}
        void OnDisable(){transform.localScale=Vector3.one;}
    }
}
