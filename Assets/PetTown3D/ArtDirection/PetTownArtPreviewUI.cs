using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// New, independent design-review UI. No PetTownGame or PlayerPrefs mutations.
public sealed class PetTownArtPreviewUI : MonoBehaviour
{
    public PetTownArtPreviewData data;
    public Camera placementCamera;
    RectTransform canvasRoot, window, gridRoot, detail;
    TMP_InputField search;
    TextMeshProUGUI selectedTitle, selectedPrice, selectedSize, selectedDescription, footer, selectionHint, sortTitle;
    RawImage detailImage;
    readonly List<Sprite> icons = new List<Sprite>();
    readonly List<GameObject> cards = new List<GameObject>();
    readonly List<Button> tabs = new List<Button>();
    readonly string[] categories = { "전체", "주거", "생활", "야외", "도로" };
    string category = "전체";
    int selected = 2, sort;
    bool showCatalog = true;
    GameObject preview;
    int quarter;
    static readonly Color Ink = new Color(.20f,.19f,.17f), Muted = new Color(.48f,.43f,.35f);

    void Awake() { Build(); }
    void Start() { foreach(var old in FindObjectsByType<PetTownCustomizationUI>(FindObjectsSortMode.None))old.enabled=false; }
    void OnDestroy() { foreach(var s in icons) if(s) Destroy(s); }
    public void Build()
    {
        if(data == null || data.font == null) return;
        var old=transform.Find("DesignCanvas"); if(old) { old.gameObject.SetActive(false); if(Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
        canvasRoot=Rect("DesignCanvas", transform,0,0,1600,1000);
        var canvas=canvasRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=50;
        var scaler=canvasRoot.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1600,1000); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
        while(icons.Count<12)
        {
            int i=icons.Count, col=i%4, row=i/4;
            icons.Add(Sprite.Create(data.iconAtlas,new Rect(col*data.iconAtlas.width/4f,(2-row)*data.iconAtlas.height/3f,data.iconAtlas.width/4f,data.iconAtlas.height/3f),new Vector2(.5f,.5f),100));
        }
        BuildHud(); BuildCatalog();
        if(Application.isPlaying && FindFirstObjectByType<EventSystem>() == null) { var es=new GameObject("DesignReviewEventSystem",typeof(EventSystem),typeof(StandaloneInputModule)); }
    }
    void BuildHud()
    {
        Surface("Profile",canvasRoot,20,20,340,90,data.paper);
        var profile=canvasRoot.Find("Profile");
        var disk=Image("PortraitMedallion",profile,8,-8,98,98,data.circle);disk.color=new Color(1,.94f,.75f);
        if(data.portrait)Raw("ActualPlayer",profile,18,3,76,76,data.portrait);else Icon(profile,13,-3,87,87,4);
        Label("Name",profile,116,12,210,26,"PET TOWN 3D",22,Ink,FontStyles.Bold);
        Label("Population",profile,116,44,200,24,"주민 "+data.residents+"명  ·  명성 "+data.reputation,16,Muted);
        var level=Image("LevelPill",profile,15,77,88,25,data.lightButton);Label("Level",level.transform,0,0,88,25,"매장 Lv. "+data.level,13,Ink,FontStyles.Bold,TextAlignmentOptions.Center);
        Resource(385,20,285,70,8,data.gold.ToString("N0"),"골드");
        Resource(681,20,190,70,9,data.pets.ToString("N0"),"펫 재고");
        Surface("Calendar",canvasRoot,1178,20,390,70,data.paper);Icon(canvasRoot.Find("Calendar"),10,7,56,56,10);Label("Day",canvasRoot.Find("Calendar"),78,13,288,24,"Day "+data.day,22,Ink,FontStyles.Bold);Label("Season",canvasRoot.Find("Calendar"),78,40,288,18,"우리 마을의 새로운 하루",12,Muted);
        var ribbon=Surface("MenuShelf",canvasRoot,270,190,1060,105,data.paper);
        DockBottom(ribbon.rectTransform,0,16);
        DockBottom((RectTransform)canvasRoot.Find("MenuShelf_SoftShadow"),0,10);
        string[] names={"건설","꾸미기","가방","펫샵 관리","캐릭터","주민","펫","설정"};int[] ii={0,1,2,3,4,5,6,7};
        for(int i=0;i<names.Length;i++)
        {
            int k=i;var b=Button("Menu_"+i,ribbon.transform,8+i*130,40,126,59,names[i],()=>Navigate(k),i==0?data.greenButton:data.lightButton,i==0?Color.white:Ink,18);
            Icon(b.transform,28,-43,72,72,ii[i]);var label=b.GetComponentInChildren<TextMeshProUGUI>();label.rectTransform.anchoredPosition=new Vector2(0,-29);label.rectTransform.sizeDelta=new Vector2(124,27);
        }
        selectionHint=Label("Feedback",canvasRoot,1120,800,450,70,"건물 카드를 눌러\n새 외형을 크게 확인해 보세요.",20,Color.white,FontStyles.Bold,TextAlignmentOptions.Center);
        selectionHint.gameObject.AddComponent<Shadow>().effectColor=new Color(.15f,.22f,.1f,.85f);
    }
    void Resource(float x,float y,float w,float h,int icon,string value,string caption)
    {
        var p=Surface(caption,canvasRoot,x,y,w,h,data.paper);Icon(p.transform,8,4,60,60,icon);Label("Value",p.transform,76,9,w-86,30,value,25,Ink,FontStyles.Bold);Label("Caption",p.transform,77,43,w-86,18,caption,12,Muted);
    }
    void BuildCatalog()
    {
        window=Surface("BuildingCatalog",canvasRoot,24,164,1066,652,data.paper).rectTransform;
        var head=Image("WalnutHeader",window,6,6,1054,51,data.header);Icon(head.transform,12,-11,61,61,11);Label("Title",head.transform,82,5,500,39,"건물 목록",27,new Color(1,.97f,.87f),FontStyles.Bold);
        Button("Close",head.transform,1009,7,34,34,"×",()=>{showCatalog=false;window.gameObject.SetActive(false);},data.coralButton,Color.white,29);
        for(int i=0;i<categories.Length;i++) { int idx=i;var b=Button("Tab_"+i,window,19+i*137,69,130,35,categories[i],()=>SetCategory(idx),i==0?data.greenButton:data.lightButton,i==0?Color.white:Ink,17);tabs.Add(b); }
        var sf=Image("SearchField",window,20,115,466,35,data.card); search=sf.gameObject.AddComponent<TMP_InputField>();
        var v=Rect("Viewport",sf.transform,31,3,421,29);var l=Label("Input",v,0,0,421,29,"",16,Ink);var hint=Label("Placeholder",v,0,0,421,29,"이름으로 검색...",16,new Color(.58f,.58f,.56f));search.textViewport=v;search.textComponent=l;search.placeholder=hint;search.lineType=TMP_InputField.LineType.SingleLine;search.onValueChanged.AddListener(_=>RebuildCards());
        var searchRing=Image("SearchRing",sf.transform,10,8,16,16,data.circle);searchRing.type=UnityEngine.UI.Image.Type.Simple;searchRing.color=Muted;var inside=Image("SearchLens",sf.transform,13,11,10,10,data.circle);inside.type=UnityEngine.UI.Image.Type.Simple;inside.color=new Color(.98f,.97f,.91f);var handle=Image("SearchHandle",sf.transform,23,21,3,8,null);handle.color=Muted;handle.rectTransform.localRotation=Quaternion.Euler(0,0,42);
        var sortButton=Button("Sort",window,496,115,187,35,"기본순",()=>{sort=(sort+1)%3;sortTitle.text=sort==1?"가격순":sort==2?"이름순":"기본순";RebuildCards();},data.lightButton,Ink,16);sortTitle=sortButton.GetComponentInChildren<TextMeshProUGUI>();
        gridRoot=Rect("Cards",window,20,164,664,432);
        detail=Image("DetailPaper",window,701,69,343,558,data.card).rectTransform;
        selectedTitle=Label("SelectedName",detail,18,12,306,37,"",26,Ink,FontStyles.Bold);
        Image("ImageWell",detail,13,58,317,250,data.lightButton).color=new Color(.91f,.96f,.94f);
        detailImage=Raw("ActualModelRender",detail,17,62,309,242,null);
        selectedDescription=Label("Description",detail,21,323,300,65,"",16,Muted);
        Icon(detail,18,403,25,25,8);selectedPrice=Label("Price",detail,54,403,268,28,"",19,Ink,FontStyles.Bold);
        selectedSize=Label("Size",detail,22,441,300,25,"",17,Muted);
        Button("Place",detail,17,491,309,49,"배치 미리보기",PreviewSelected,data.greenButton,Color.white,22);
        footer=Label("Footer",window,21,605,655,28,"",15,Muted,FontStyles.Normal,TextAlignmentOptions.Center);
        RebuildCards();
    }
    void SetCategory(int idx)
    {
        category=categories[idx];for(int i=0;i<tabs.Count;i++){tabs[i].GetComponent<Image>().sprite=i==idx?data.greenButton:data.lightButton;tabs[i].GetComponentInChildren<TextMeshProUGUI>().color=i==idx?Color.white:Ink;}RebuildCards();
    }
    public void SelectBuilding(int key)
    {
        for(int i=0;i<data.buildings.Length;i++)if(data.buildings[i].key==key){selected=i;break;}RebuildCards();
    }
    void RebuildCards()
    {
        foreach(var c in cards){c.SetActive(false);if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);}cards.Clear();
        var found=new List<int>();for(int i=0;i<data.buildings.Length;i++)if((category=="전체"||data.buildings[i].category==category)&&(string.IsNullOrEmpty(search.text)||data.buildings[i].title.Contains(search.text)))found.Add(i);
        if(sort==1)found.Sort((a,b)=>data.buildings[a].price.CompareTo(data.buildings[b].price));else if(sort==2)found.Sort((a,b)=>string.Compare(data.buildings[a].title,data.buildings[b].title,StringComparison.Ordinal));
        for(int n=0;n<found.Count;n++)
        {
            int i=found[n];var e=data.buildings[i];var b=Button("Building_"+e.key,gridRoot,(n%4)*168,(n/4)*216,160,207,"",()=>{selected=i;RebuildCards();},i==selected?data.selectedCard:data.card,Ink);
            Raw("ModelThumbnail",b.transform,5,5,150,139,e.thumbnail);
            Label("Name",b.transform,4,144,152,27,e.title,18,Ink,FontStyles.Bold,TextAlignmentOptions.Center);
            Icon(b.transform,25,176,22,22,8);Label("Price",b.transform,49,174,107,27,e.price.ToString("N0")+(e.key==8?" /m":""),17,Ink,FontStyles.Bold);
            cards.Add(b.gameObject);
        }
        var s=data.buildings[selected];selectedTitle.text=s.title;detailImage.texture=s.thumbnail;selectedPrice.text=s.price.ToString("N0")+" 골드"+(s.key==8?" /m":"");selectedSize.text="크기   "+s.size;selectedDescription.text=s.description;
        footer.text=found.Count+"개 항목   ·   "+(sort==1?"가격순":sort==2?"이름순":"기본순")+"   ·   카드를 눌러 선택하세요";
    }
    void Navigate(int k)
    {
        if(k==0){showCatalog=!showCatalog;window.gameObject.SetActive(showCatalog);return;}
        ShowHint(k==1?"가구 카탈로그는 이 시안 확인 후 제작합니다.":k==2?"가방 화면은 이 시안 확인 후 제작합니다.":"첫 시안은 HUD와 건물 목록입니다.\n기능 연결은 시안 확인 후 진행합니다.");
    }
    void ShowHint(string message) { if(selectionHint)selectionHint.text=message; }
    void PreviewSelected()
    {
        if(preview)Destroy(preview);var e=data.buildings[selected];if(!e.model)return;preview=Instantiate(e.model);preview.name="DesignPlacementPreview";preview.transform.position=new Vector3(7,0,-7);quarter=0;
        foreach(var c in preview.GetComponentsInChildren<Collider>())Destroy(c);ShowHint(e.title+" 배치 미리보기\nR : 90° 회전   ESC : 닫기\n골드·저장 데이터 변경 없음");
        showCatalog=false;window.gameObject.SetActive(false);
    }
    void Update()
    {
        if(!preview)return;if(Input.GetKeyDown(KeyCode.R)){quarter=(quarter+1)%4;preview.transform.rotation=Quaternion.Euler(0,quarter*90,0);}if(Input.GetKeyDown(KeyCode.Escape)){Destroy(preview);window.gameObject.SetActive(true);showCatalog=true;}
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static void DockBottom(RectTransform rect,float x,float y)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(x,y);
    }
    Image Surface(string name,Transform parent,float x,float y,float w,float h,Sprite sprite)
    {
        var shadow=Image(name+"_SoftShadow",parent,x-4,y+6,w+8,h+8,data.shadow);shadow.raycastTarget=false;
        return Image(name,parent,x,y,w,h,sprite);
    }
    Image Image(string name,Transform parent,float x,float y,float w,float h,Sprite sprite)
    {
        var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=sprite==data.paper?1.4f:sprite==data.header?1.6f:sprite==data.shadow||sprite==data.circle?1:2.3f;return image;
    }
    RawImage Raw(string name,Transform parent,float x,float y,float w,float h,Texture texture)
    {
        var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;return image;
    }
    Image Icon(Transform parent,float x,float y,float w,float h,int index)
    {
        var image=Image("Icon_"+index,parent,x,y,w,h,icons[index]);image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.raycastTarget=false;return image;
    }
    TextMeshProUGUI Label(string name,Transform parent,float x,float y,float w,float h,string text,float size,Color color,FontStyles style=FontStyles.Normal,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
    {
        var r=Rect(name,parent,x,y,w,h);var l=r.gameObject.AddComponent<TextMeshProUGUI>();l.font=data.font;l.fontSize=size;l.text=text;l.color=color;l.fontStyle=style;l.alignment=alignment;l.raycastTarget=false;l.textWrappingMode=TextWrappingModes.Normal;return l;
    }
    Button Button(string name,Transform parent,float x,float y,float w,float h,string text,Action callback,Sprite sprite,Color textColor,float size=18)
    {
        var image=Image(name,parent,x,y,w,h,sprite);var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;var cs=b.colors;cs.highlightedColor=new Color(1,1,.94f);cs.pressedColor=new Color(.84f,.88f,.78f);cs.disabledColor=new Color(.7f,.7f,.7f);b.colors=cs;b.onClick.AddListener(()=>callback());Label("Label",image.transform,0,0,w,h,text,size,textColor,FontStyles.Bold,TextAlignmentOptions.Center);return b;
    }
}
