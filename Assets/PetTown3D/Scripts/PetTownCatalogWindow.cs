using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PetTownCatalogEntry
{
    public int Key,Price;
    public bool TextOnlyDetail;
    public int IconKey=-1;
    public string Name,Category,Description,PriceUnit,PriceLabel,FootprintLabel,ActionLabel;
    public Func<string> PriceProvider,SizeProvider,ActionProvider;
    public string ActionText=>ActionProvider?.Invoke()??ActionLabel??"선택하기";
    public bool Dynamic=>PriceProvider!=null||SizeProvider!=null||ActionProvider!=null;
    public string PriceText=>PriceProvider?.Invoke()??PriceLabel??(Price.ToString("N0")+" 골드"+(PriceUnit??""));
    public string SizeText=>SizeProvider?.Invoke()??FootprintLabel??(Footprint==Vector2.zero?"드래그":Footprint.x+"×"+Footprint.y+"m");
    public Vector2 Footprint;
    public Texture2D Thumbnail;
    public Func<Texture2D> ThumbnailProvider;
}
// Reusable data-only catalog view. Selection calls the existing game's placement entry point.
public sealed class PetTownCatalogWindow : MonoBehaviour
{
    static PetTownCatalogWindow textOwner;
    PetTownCozyCatalogView cozy;
    int visibleEntryCount,sortMode;
    public static bool IsTextEntryFocused=>PetTownCozyCatalogView.TextEntryFocused||(textOwner!=null&&textOwner.IsOpen&&textOwner.search!=null&&textOwner.search.isFocused);
    public bool IsOpen=>cozy!=null?cozy.IsOpen:root!=null&&root.activeSelf;
    public bool Ready=>cozy!=null?cozy.Ready:theme!=null&&theme.Ready;
    public int VisibleEntryCount { get=>cozy!=null?cozy.VisibleEntryCount:visibleEntryCount; private set=>visibleEntryCount=value; }
    public int SelectedKey=>cozy!=null?cozy.SelectedKey:selected==null?-1:selected.Key;
    public int SortMode { get=>cozy!=null?cozy.SortMode:sortMode; private set=>sortMode=value; }
    PetTownUITheme theme;
    GameObject root;
    RectTransform safe,panel,listArea,viewport,content,detail,filters;
    GridLayoutGroup grid;
    TMP_InputField search;
    TextMeshProUGUI title,count,description,selectedName,price,footprint,sortLabel;
    RawImage portrait;
    Button confirm;
    readonly List<PetTownCatalogEntry> entries=new List<PetTownCatalogEntry>();
    readonly List<GameObject> cards=new List<GameObject>(),tabs=new List<GameObject>();
    readonly List<string> categories=new List<string>();
    PetTownCatalogEntry selected;
    string category="전체";
    Action<PetTownCatalogEntry> choose;
    Action cancel,legacy;
    int lastW,lastH;Rect lastSafe;float nextDynamicRefresh;
    public void Initialize(Transform parent)
    {
        if(PetTownCozyLibrary.Current!=null){cozy=gameObject.AddComponent<PetTownCozyCatalogView>();cozy.Initialize(parent);return;}
        theme=new PetTownUITheme();if(!theme.Ready){Debug.LogWarning("PetTown catalog: Korean TMP font could not be created; keeping the existing menu.");return;}
        textOwner=this;
        safe=PetTownUITheme.Rect("CatalogSafeArea",parent);PetTownUITheme.Place(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);root=safe.gameObject;
        var backdrop=theme.Panel("ModalBackdrop",safe,new Color(.08f,.16f,.12f,.35f));backdrop.sprite=null;PetTownUITheme.Place(backdrop.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var dismiss=backdrop.gameObject.AddComponent<Button>();dismiss.onClick.AddListener(Cancel);
        panel=theme.Panel("CatalogWindow",safe,theme.Cream).rectTransform;
        title=theme.Text("Title",panel,"건물 목록",26);Top(title.rectTransform,20,78,18,34);
        var close=theme.Button("Close",panel,"닫기",Cancel);PetTownUITheme.Place((RectTransform)close.transform,new Vector2(1,1),Vector2.one,new Vector2(-80,-52),new Vector2(-16,-12));
        var field=theme.Panel("Search",panel,Color.white,"Border");Top(field.rectTransform,18,130,62,40);search=field.gameObject.AddComponent<TMP_InputField>();
        var textRect=PetTownUITheme.Rect("TextViewport",field.transform);PetTownUITheme.Place(textRect,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4));textRect.gameObject.AddComponent<RectMask2D>();
        var inputText=theme.Text("Text",textRect,"",17);PetTownUITheme.Place(inputText.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var hint=theme.Text("Placeholder",textRect,"이름 검색...",17,theme.Muted);PetTownUITheme.Place(hint.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        search.textViewport=textRect;search.textComponent=inputText;search.placeholder=hint;search.characterLimit=48;search.lineType=TMP_InputField.LineType.SingleLine;search.onValueChanged.AddListener(_=>Rebuild());
        var sort=theme.Button("Sort",panel,"이름순",CycleSort,Color.white);PetTownUITheme.Place((RectTransform)sort.transform,new Vector2(1,1),Vector2.one,new Vector2(-122,-102),new Vector2(-18,-62));sortLabel=sort.GetComponentInChildren<TextMeshProUGUI>();
        filters=PetTownUITheme.Rect("CategoryTabs",panel);Top(filters,18,18,114,34);
        listArea=PetTownUITheme.Rect("List",panel);var scroll=listArea.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
        viewport=theme.Panel("Viewport",listArea,new Color(1,1,1,.24f)).rectTransform;PetTownUITheme.Place(viewport,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-14,0));viewport.gameObject.AddComponent<RectMask2D>();
        content=PetTownUITheme.Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.spacing=new Vector2(10,10);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.childAlignment=TextAnchor.UpperLeft;
        scroll.viewport=viewport;scroll.content=content;
        var bar=theme.Panel("Scrollbar",listArea,new Color(.25f,.45f,.3f,.13f));PetTownUITheme.Place(bar.rectTransform,new Vector2(1,0),Vector2.one,new Vector2(-8,0),Vector2.zero);var sb=bar.gameObject.AddComponent<Scrollbar>();var handle=theme.Panel("Handle",bar.transform,theme.Mint);PetTownUITheme.Place(handle.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);sb.handleRect=handle.rectTransform;sb.targetGraphic=handle;sb.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=sb;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        detail=theme.Panel("Detail",panel,Color.white).rectTransform;
        portrait=PetTownUITheme.Rect("Thumbnail",detail).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;PetTownUITheme.Place(portrait.rectTransform,new Vector2(0,.62f),Vector2.one,new Vector2(12,0),new Vector2(-12,-12));
        selectedName=theme.Text("Name",detail,"",23);price=theme.Text("Price",detail,"",18,theme.Mint);footprint=theme.Text("Footprint",detail,"",16,theme.Muted);description=theme.Text("Description",detail,"",16,theme.Muted,TextAlignmentOptions.TopLeft);
        confirm=theme.Button("Confirm",detail,"배치하기",ConfirmSelected);PetTownUITheme.Place((RectTransform)confirm.transform,Vector2.zero,new Vector2(1,0),new Vector2(12,12),new Vector2(-12,56));
        count=theme.Text("Count",panel,"",15,theme.Muted);PetTownUITheme.Place(count.rectTransform,Vector2.zero,new Vector2(1,0),new Vector2(168,10),new Vector2(-18,48));
        var fallback=theme.Button("LegacyMenu",panel,"기존 목록",()=>{Hide();legacy?.Invoke();},Color.white);PetTownUITheme.Place((RectTransform)fallback.transform,Vector2.zero,Vector2.zero,new Vector2(18,12),new Vector2(150,48));
        Layout();root.SetActive(false);
    }
    static void Top(RectTransform rt,float left,float right,float top,float height)=>PetTownUITheme.Place(rt,new Vector2(0,1),Vector2.one,new Vector2(left,-top-height),new Vector2(-right,-top));
    public void Show(string heading,IList<PetTownCatalogEntry> data,Action<PetTownCatalogEntry> onChoose,Action onCancel,Action onLegacy)
    {
        if(cozy!=null){cozy.Show(heading,data,onChoose,onCancel);return;}
        if(!Ready)return;entries.Clear();entries.AddRange(data);choose=onChoose;cancel=onCancel;legacy=onLegacy;title.text=heading;category="전체";SortMode=0;sortLabel.text="이름순";search.SetTextWithoutNotify("");selected=null;
        categories.Clear();categories.Add("전체");foreach(var entry in entries)if(!categories.Contains(entry.Category))categories.Add(entry.Category);
        root.SetActive(true);root.transform.SetAsLastSibling();BuildTabs();Rebuild();Layout();
    }
    public void Hide()
    {
        if(cozy!=null){cozy.Hide();return;}
        if(root==null)return;search.DeactivateInputField();if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.transform.IsChildOf(root.transform))EventSystem.current.SetSelectedGameObject(null);root.SetActive(false);
    }
    void Cancel(){Hide();cancel?.Invoke();}
    public void SetSearchText(string value){if(cozy!=null){cozy.SetSearchText(value);return;}search.text=value;}
    public void SetCategory(string value){if(cozy!=null){cozy.SetCategory(value);return;}category=value;BuildTabs();Rebuild();}
    public void CycleSort(){if(cozy!=null){cozy.CycleSort();return;}SortMode=(SortMode+1)%(entries.Exists(e=>e.Footprint!=Vector2.zero)?3:2);sortLabel.text=SortMode==0?"이름순":SortMode==1?"가격순":"면적순";Rebuild();}
    void BuildTabs()
    {
        foreach(var tab in tabs){tab.SetActive(false);Destroy(tab);}tabs.Clear();
        for(int i=0;i<categories.Count;i++){string value=categories[i];var button=theme.Button("Category_"+i,filters,value,()=>SetCategory(value),value==category?new Color(.71f,.88f,.72f):Color.white);PetTownUITheme.Place((RectTransform)button.transform,new Vector2(i/(float)categories.Count,0),new Vector2((i+1)/(float)categories.Count,1),new Vector2(0,0),new Vector2(-6,0));tabs.Add(button.gameObject);}
    }
    void Rebuild()
    {
        if(!Ready)return;foreach(var card in cards){card.SetActive(false);Destroy(card);}cards.Clear();var filtered=entries.FindAll(e=>(category=="전체"||e.Category==category)&&(string.IsNullOrWhiteSpace(search.text)||e.Name.IndexOf(search.text.Trim(),StringComparison.OrdinalIgnoreCase)>=0));
        filtered.Sort((a,b)=>{int c=SortMode==1?a.Price.CompareTo(b.Price):SortMode==2?(a.Footprint.x*a.Footprint.y).CompareTo(b.Footprint.x*b.Footprint.y):string.Compare(a.Name,b.Name,StringComparison.Ordinal);return c!=0?c:a.Key.CompareTo(b.Key);});
        VisibleEntryCount=filtered.Count;if(selected==null||!filtered.Contains(selected))selected=filtered.Count>0?filtered[0]:null;
        foreach(var entry in filtered)
        {
            var card=theme.Panel("Entry_"+entry.Key,content,entry==selected?new Color(.83f,.93f,.81f):Color.white,"Border");var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;button.onClick.AddListener(()=>Choose(entry.Key));
            var name=theme.Text("Name",card.transform,entry.Name,19);Top(name.rectTransform,14,12,8,29);
            var info=theme.Text("Info",card.transform,entry.PriceText+(string.IsNullOrEmpty(entry.SizeText)?"":"  ·  "+entry.SizeText),14,theme.Muted);Top(info.rectTransform,14,10,40,35);cards.Add(card.gameObject);
        }
        count.text=filtered.Count==0?"검색 결과가 없습니다.":filtered.Count+"개 항목  ·  목록을 스크롤할 수 있어요";UpdateDetail();LayoutGrid();
    }
    public void Choose(int key){if(cozy!=null){cozy.Choose(key);return;}selected=entries.Find(e=>e.Key==key);Rebuild();}
    void UpdateDetail()
    {
        confirm.interactable=selected!=null;selectedName.text=selected?.Name??"선택한 항목 없음";price.text=selected?.PriceText??"";footprint.text=selected?.SizeText??"";confirm.GetComponentInChildren<TextMeshProUGUI>().text=selected!=null?selected.ActionText:"배치하기";description.text=selected?.Description??"다른 검색어나 카테고리를 선택해 주세요.";portrait.texture=selected?.Thumbnail;portrait.gameObject.SetActive(portrait.texture!=null);
    }
    public void ConfirmSelected(){if(cozy!=null){cozy.ConfirmSelected();return;}if(selected==null)return;var value=selected;Hide();choose?.Invoke(value);}
    void Update()
    {
        if(cozy!=null)return;
        if(!IsOpen)return;
        if(PetTownCustomizationUI.IsCharacterWindowOpen){Cancel();return;}
        if(Input.GetKeyDown(KeyCode.Escape)){if(search.isFocused)search.DeactivateInputField();else Cancel();}
        if(lastW!=Screen.width||lastH!=Screen.height||lastSafe!=Screen.safeArea)Layout();
    }
    void Layout()
    {
        lastW=Screen.width;lastH=Screen.height;lastSafe=Screen.safeArea;if(lastW<=0||lastH<=0)return;
        safe.anchorMin=new Vector2(lastSafe.xMin/lastW,lastSafe.yMin/lastH);safe.anchorMax=new Vector2(lastSafe.xMax/lastW,lastSafe.yMax/lastH);safe.offsetMin=safe.offsetMax=Vector2.zero;
        bool portraitMode=lastW<lastH;
        PetTownUITheme.Place(panel,portraitMode?new Vector2(.035f,.12f):new Vector2(.1f,.15f),portraitMode?new Vector2(.965f,.9f):new Vector2(.9f,.87f),Vector2.zero,Vector2.zero);
        if(portraitMode)
        {
            PetTownUITheme.Place(listArea,new Vector2(0,.38f),Vector2.one,new Vector2(18,8),new Vector2(-18,-162));PetTownUITheme.Place(detail,Vector2.zero,new Vector2(1,.38f),new Vector2(18,58),new Vector2(-18,-4));
            portrait.gameObject.SetActive(false);
            PetTownUITheme.Place(selectedName.rectTransform,new Vector2(0,.67f),Vector2.one,new Vector2(12,0),new Vector2(-12,-6));PetTownUITheme.Place(price.rectTransform,new Vector2(0,.4f),new Vector2(.5f,.67f),new Vector2(12,0),Vector2.zero);PetTownUITheme.Place(footprint.rectTransform,new Vector2(.5f,.4f),new Vector2(1,.67f),Vector2.zero,new Vector2(-12,0));description.gameObject.SetActive(false);
        }
        else
        {
            PetTownUITheme.Place(listArea,Vector2.zero,new Vector2(.66f,1),new Vector2(18,58),new Vector2(-8,-162));PetTownUITheme.Place(detail,new Vector2(.66f,0),Vector2.one,new Vector2(8,58),new Vector2(-18,-162));
            portrait.gameObject.SetActive(portrait.texture!=null);description.gameObject.SetActive(true);
            PetTownUITheme.Place(selectedName.rectTransform,new Vector2(0,.54f),new Vector2(1,.69f),new Vector2(12,0),new Vector2(-12,0));PetTownUITheme.Place(price.rectTransform,new Vector2(0,.44f),new Vector2(1,.55f),new Vector2(12,0),new Vector2(-12,0));PetTownUITheme.Place(footprint.rectTransform,new Vector2(0,.34f),new Vector2(1,.44f),new Vector2(12,0),new Vector2(-12,0));PetTownUITheme.Place(description.rectTransform,new Vector2(0,.19f),new Vector2(1,.34f),new Vector2(12,0),new Vector2(-12,0));
        }
        LayoutGrid();
    }
    void LayoutGrid()
    {
        if(viewport==null)return;int columns=Screen.width<Screen.height?1:2;grid.constraintCount=columns;
        float width=Mathf.Max(160,viewport.rect.width);grid.cellSize=new Vector2((width-grid.spacing.x*(columns-1))/columns,88);
        content.sizeDelta=new Vector2(0,Mathf.Max(0,Mathf.CeilToInt(cards.Count/(float)columns)*98-10));
    }
    void LateUpdate()
    {
        if(cozy!=null)return;
        if(!IsOpen)return;LayoutGrid();
        if(Time.unscaledTime>=nextDynamicRefresh)
        {
            nextDynamicRefresh=Time.unscaledTime+.25f;
            foreach(var entry in entries)if(entry.Dynamic){var card=cards.Find(c=>c.name=="Entry_"+entry.Key);if(card){var info=card.transform.Find("Info").GetComponent<TextMeshProUGUI>();info.text=entry.PriceText+(string.IsNullOrEmpty(entry.SizeText)?"":"  ·  "+entry.SizeText);}}
            if(selected!=null&&selected.Dynamic)UpdateDetail();
        }
        if(selected!=null&&selected.Thumbnail==null&&selected.ThumbnailProvider!=null)
        {
            selected.Thumbnail=selected.ThumbnailProvider();if(selected.Thumbnail!=null){UpdateDetail();Layout();}
        }
    }
    void OnDestroy(){if(textOwner==this)textOwner=null;if(root!=null)Destroy(root);theme?.Dispose();}
}
