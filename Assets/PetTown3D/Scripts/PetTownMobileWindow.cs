using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owned presentation canvas: a portrait-shaped modal in both device orientations.
public sealed class PetTownMobileWindow : IDisposable
{
    public RectTransform Layer,Safe,Panel,Header,Body;
    public TextMeshProUGUI Title;
    public Image Icon;
    public Button Back,Close;
    public Canvas Canvas;
    readonly PetTownUITheme theme;
    Material panelMaterial;
    Vector2 draggedOffset;
    int lastWidth,lastHeight;Rect lastSafe;float preferredHeight=720;
    static int order;
    public bool IsOpen=>Layer&&Layer.gameObject.activeSelf;
    public PetTownMobileWindow(string name,PetTownUITheme skin,Action close,Action back=null)
    {
        theme=skin;Layer=PetTownUITheme.Rect(name,null);Canvas=Layer.gameObject.AddComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=Layer.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(430,800);scaler.matchWidthOrHeight=1;
        Layer.gameObject.AddComponent<GraphicRaycaster>();
        var dim=theme.Panel("ModalDim",Layer,Color.white);dim.sprite=null;dim.material=null;dim.color=new Color(.12f,.075f,.035f,.35f);Fill(dim.rectTransform);dim.gameObject.AddComponent<Button>().onClick.AddListener(()=>close());
        Safe=PetTownUITheme.Rect("SafeArea",Layer);Panel=theme.Panel("MobilePortraitPanel",Safe,theme.Cream).rectTransform;
        var paper=Panel.GetComponent<Image>();
        if(paper.material&&paper.material.HasProperty("_CutoutEnabled")){panelMaterial=new Material(paper.material);panelMaterial.SetFloat("_CutoutEnabled",1);paper.material=panelMaterial;}
        Header=theme.Panel("Header",Panel,Color.white,"Header").rectTransform;
        Icon=theme.Panel("HeaderIcon",Header,Color.white,"Icon_3");At(Icon.rectTransform,10,10,36,36);
        Title=theme.Text("Title",Header,"",20,Color.white,TextAlignmentOptions.Center);Title.fontWeight=FontWeight.Bold;Title.gameObject.AddComponent<PetTownTextOutline>().Set(.2f,new Color32(66,36,14,255));Title.enableAutoSizing=true;Title.fontSizeMin=16;Title.fontSizeMax=20;Title.textWrappingMode=TextWrappingModes.NoWrap;
        PetTownUITheme.Place(Title.rectTransform,Vector2.zero,Vector2.one,new Vector2(62,8),new Vector2(-58,-8));
        Back=theme.Button("Back",Header,"‹",()=>{if(back!=null)back();else close();},Color.white);At((RectTransform)Back.transform,8,6,46,44);Back.gameObject.SetActive(false);
        Close=theme.Button("Close",Header,"×",close,Color.white);var cr=(RectTransform)Close.transform;cr.anchorMin=cr.anchorMax=new Vector2(1,1);cr.pivot=new Vector2(1,1);cr.anchoredPosition=new Vector2(-9,-6);cr.sizeDelta=new Vector2(44,44);
        Body=PetTownUITheme.Rect("Body",Panel);PetTownUITheme.Place(Body,Vector2.zero,Vector2.one,new Vector2(16,16),new Vector2(-16,-78));
        Header.gameObject.AddComponent<PetTownWindowDragHandle>().Initialize(Safe,()=>draggedOffset,MoveWindow);
        Layout(true);
    }
    public void SetHeader(string title,int icon,bool canGoBack)
    {
        Title.text=title;Icon.sprite=theme.Icon(icon);Icon.gameObject.SetActive(!canGoBack);Back.gameObject.SetActive(canGoBack);
    }
    public void Show(){Canvas.sortingOrder=200+(order++%1000);Layer.gameObject.SetActive(true);Layout(true);}
    public void Hide(){if(Layer)Layer.gameObject.SetActive(false);}
    public void SetHeight(float height){height=Mathf.Clamp(height,320,720);if(Mathf.Abs(preferredHeight-height)<.5f)return;preferredHeight=height;Layout(true);}
    public void Layout(bool force=false)
    {
        if(!Layer||Screen.width<=0||Screen.height<=0)return;
        if(!force&&lastWidth==Screen.width&&lastHeight==Screen.height&&lastSafe==Screen.safeArea)return;
        lastWidth=Screen.width;lastHeight=Screen.height;lastSafe=Screen.safeArea;
        Safe.anchorMin=new Vector2(lastSafe.xMin/lastWidth,lastSafe.yMin/lastHeight);Safe.anchorMax=new Vector2(lastSafe.xMax/lastWidth,lastSafe.yMax/lastHeight);Safe.offsetMin=Safe.offsetMax=Vector2.zero;
        UnityEngine.Canvas.ForceUpdateCanvases();float available=Mathf.Max(200,Safe.rect.height*.90f),w=Mathf.Min(360,Safe.rect.width*.86f,available*.64f),h=Mathf.Min(available,Mathf.Max(preferredHeight,w*.9f));
        Panel.anchorMin=Panel.anchorMax=new Vector2(.5f,.48f);Panel.pivot=new Vector2(.5f,.5f);Panel.anchoredPosition=draggedOffset;Panel.sizeDelta=new Vector2(w,h);
        At(Header,14,14,w-28,56);
        UnityEngine.Canvas.ForceUpdateCanvases();
        ClampPosition();UpdateHeaderCutout();
    }
    void MoveWindow(Vector2 offset){draggedOffset=offset;Panel.anchoredPosition=offset;ClampPosition();UpdateHeaderCutout();}
    void ClampPosition()
    {
        // Allow most of the body off the left/bottom edge, but retain the close button and a usable grip.
        var close=(RectTransform)Close.transform;
        var low=Safe.InverseTransformPoint(close.TransformPoint(close.rect.min));
        var high=Safe.InverseTransformPoint(close.TransformPoint(close.rect.max));
        var headLow=Safe.InverseTransformPoint(Header.TransformPoint(Header.rect.min));
        var headHigh=Safe.InverseTransformPoint(Header.TransformPoint(Header.rect.max));
        float grip=Mathf.Min(72,Mathf.Max(0,Header.rect.width-close.rect.width-20)),margin=6;
        float x=high.x>Safe.rect.xMax-margin?Safe.rect.xMax-margin-high.x:low.x-grip<Safe.rect.xMin+margin?Safe.rect.xMin+margin-low.x+grip:0;
        float y=headHigh.y>Safe.rect.yMax-margin?Safe.rect.yMax-margin-headHigh.y:headLow.y<Safe.rect.yMin+margin?Safe.rect.yMin+margin-headLow.y:0;
        draggedOffset+=new Vector2(x,y);Panel.anchoredPosition=draggedOffset;
    }
    void UpdateHeaderCutout()
    {
        if(panelMaterial)
        {
            // UI vertices are batched in the root Canvas coordinate system.
            var low=Canvas.transform.InverseTransformPoint(Header.TransformPoint(Header.rect.min));
            var high=Canvas.transform.InverseTransformPoint(Header.TransformPoint(Header.rect.max));
            panelMaterial.SetVector("_HeaderCutout",new Vector4(low.x,low.y,high.x,high.y));
        }
    }
    public static ScrollRect Scroll(string name,Transform parent,out RectTransform content)
    {
        var area=PetTownUITheme.Rect(name,parent);var scroll=area.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;scroll.decelerationRate=.135f;
        var viewport=PetTownUITheme.Rect("Viewport",area);Fill(viewport);var surface=viewport.gameObject.AddComponent<Image>();surface.color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        content=PetTownUITheme.Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        scroll.viewport=viewport;scroll.content=content;return scroll;
    }
    public static void Fill(RectTransform r)=>PetTownUITheme.Place(r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
    public static void At(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
    public void Dispose(){if(Layer){Layer.gameObject.SetActive(false);UnityEngine.Object.Destroy(Layer.gameObject);}if(panelMaterial)UnityEngine.Object.Destroy(panelMaterial);}
}
