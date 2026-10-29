using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mobile catalogs share the game's existing data and confirm callbacks.
public sealed class PetTownCozyCatalogView : MonoBehaviour
{
    public static bool TextEntryFocused;
    public bool Ready=>theme!=null&&theme.Ready;
    public bool IsOpen=>window!=null&&window.IsOpen;
    public int VisibleEntryCount=>filtered.Count;
    public int SelectedKey=>selected?.Key??-1;
    public int SortMode{get;private set;}
    PetTownUITheme theme;PetTownMobileWindow window;
    RectTransform listPage,detailPage,tabs,searchRoot,sortRoot,listContent,detailContent,largeSlot;
    ScrollRect listScroll,detailScroll;TMP_InputField search;
    TextMeshProUGUI count,nameLabel,price,size,description,sortLabel;RawImage large;Image actionPortrait;Button confirm;
    readonly List<PetTownCatalogEntry> entries=new List<PetTownCatalogEntry>(),filtered=new List<PetTownCatalogEntry>();
    readonly List<GameObject> cards=new List<GameObject>(),tabObjects=new List<GameObject>();
    readonly List<string> categories=new List<string>();string category="전체",title;
    Action<PetTownCatalogEntry> choose;Action cancel;PetTownCatalogEntry selected;
    float nextDynamicRefresh;bool showingDetail;float shownWidth=-1,listPosition=1;
    public void Initialize(Transform parent)
    {
        theme=new PetTownUITheme();window=new PetTownMobileWindow("PetTownMobileCatalog",theme,Cancel,BackToList);
        listPage=PetTownUITheme.Rect("ListPage",window.Body);PetTownMobileWindow.Fill(listPage);
        tabs=PetTownUITheme.Rect("CategoryTabs",listPage);
        var field=theme.Panel("Search",listPage,Color.white,"Border");searchRoot=field.rectTransform;search=field.gameObject.AddComponent<TMP_InputField>();
        var viewport=PetTownUITheme.Rect("Viewport",field.transform);PetTownUITheme.Place(viewport,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4));viewport.gameObject.AddComponent<RectMask2D>();
        var input=theme.Text("Input",viewport,"",16);PetTownMobileWindow.Fill(input.rectTransform);var placeholder=theme.Text("Hint",viewport,"이름으로 검색",16,theme.Muted);PetTownMobileWindow.Fill(placeholder.rectTransform);
        search.textViewport=viewport;search.textComponent=input;search.placeholder=placeholder;search.characterLimit=48;search.lineType=TMP_InputField.LineType.SingleLine;search.onValueChanged.AddListener(_=>Rebuild(true));
        var sort=theme.Button("Sort",listPage,"기본순",CycleSort,Color.white);sortRoot=(RectTransform)sort.transform;sortLabel=sort.GetComponentInChildren<TextMeshProUGUI>();
        listScroll=PetTownMobileWindow.Scroll("VerticalList",listPage,out listContent);
        count=theme.Text("Count",listPage,"",14,theme.Muted,TextAlignmentOptions.Center);PetTownUITheme.Place(count.rectTransform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,24));
        detailPage=PetTownUITheme.Rect("DetailPage",window.Body);PetTownMobileWindow.Fill(detailPage);
        detailScroll=PetTownMobileWindow.Scroll("VerticalDetails",detailPage,out detailContent);PetTownUITheme.Place((RectTransform)detailScroll.transform,Vector2.zero,Vector2.one,new Vector2(0,60),Vector2.zero);
        nameLabel=theme.Text("Name",detailContent,"",22);largeSlot=PetTownUITheme.Rect("Artwork",detailContent);
        large=PetTownUITheme.Rect("LargeModel",largeSlot).gameObject.AddComponent<RawImage>();large.raycastTarget=false;var fit=large.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        actionPortrait=theme.Panel("KindIcon",largeSlot,Color.white,"Icon_3");PetTownMobileWindow.Fill(actionPortrait.rectTransform);
        description=theme.Text("Description",detailContent,"",17,theme.Muted,TextAlignmentOptions.TopLeft);price=theme.Text("Price",detailContent,"",21);size=theme.Text("Size",detailContent,"",16,theme.Muted);
        confirm=theme.Button("Confirm",detailPage,"선택하기",ConfirmSelected);PetTownUITheme.Place((RectTransform)confirm.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,52));
        window.Hide();
    }
    public void Show(string label,IList<PetTownCatalogEntry> data,Action<PetTownCatalogEntry> onChoose,Action onCancel)
    {
        title=label;entries.Clear();entries.AddRange(data);choose=onChoose;cancel=onCancel;category="전체";SortMode=0;sortLabel.text="기본순";selected=entries.Count>0?entries[0]:null;search.SetTextWithoutNotify("");categories.Clear();categories.Add("전체");
        foreach(var e in entries){if(!string.IsNullOrEmpty(e.Category)&&!categories.Contains(e.Category))categories.Add(e.Category);if(e.Thumbnail==null&&PetTownCozyLibrary.Current)e.Thumbnail=PetTownCozyLibrary.Current.Thumbnail(label,e.Key);}
        showingDetail=false;listPosition=1;window.Show();shownWidth=-1;BuildTabs();Rebuild(true);ShowPage();
    }
    int HeaderIcon=>title.Contains("가구")?1:title.Contains("건물")?11:title.Contains("가방")?2:title.Contains("설정")?7:title.Contains("주민")?5:3;
    public void Hide(){window?.Hide();TextEntryFocused=false;if(search)search.DeactivateInputField();}
    void Cancel(){Hide();cancel?.Invoke();}
    public void SetSearchText(string value){search.text=value;}
    public void SetCategory(string value){category=value;BuildTabs();Rebuild(true);}
    public void CycleSort(){SortMode=(SortMode+1)%3;sortLabel.text=SortMode==0?"기본순":SortMode==1?"가격순":"이름순";Rebuild(true);}
    public void Choose(int key)
    {
        var entry=entries.Find(e=>e.Key==key);if(entry==null)return;selected=entry;listPosition=listScroll.verticalNormalizedPosition;showingDetail=true;search.DeactivateInputField();UpdateDetail();ShowPage();detailScroll.verticalNormalizedPosition=1;
    }
    public void BackToList(){if(!showingDetail){Cancel();return;}showingDetail=false;Rebuild();ShowPage();listScroll.verticalNormalizedPosition=listPosition;}
    public void ConfirmSelected(){if(!IsOpen||selected==null)return;var e=selected;Hide();choose?.Invoke(e);}
    void ShowPage(){listPage.gameObject.SetActive(!showingDetail);detailPage.gameObject.SetActive(showingDetail);window.SetHeader(showingDetail?"상세 정보":title,HeaderIcon,showingDetail);window.Icon.sprite=PetTownGlyphs.Badge(PetTownGlyphs.Pick(title,PetTownGlyph.Shop));}
    PetTownGlyph EntryGlyph(PetTownCatalogEntry entry)=>PetTownGlyphs.Pick(entry.Name,PetTownGlyphs.Pick(title,PetTownGlyph.Shop));
    void BuildTabs()
    {
        foreach(var t in tabObjects){t.SetActive(false);Destroy(t);}tabObjects.Clear();
        if(categories.Count<=2)return;
        for(int i=0;i<categories.Count;i++){string value=categories[i];var b=theme.Button("Tab_"+i,tabs,value,()=>SetCategory(value),value==category?(Color?)null:Color.white);b.GetComponentInChildren<TextMeshProUGUI>().fontSize=14;tabObjects.Add(b.gameObject);}
    }
    void Layout()
    {
        window.Layout();float w=window.Body.rect.width;if(w<=0)return;
        int cols=Mathf.Max(2,Mathf.Min(4,Mathf.FloorToInt(w/72)));float tabHeight=tabObjects.Count==0?0:Mathf.CeilToInt(tabObjects.Count/(float)cols)*46;
        PetTownMobileWindow.At(tabs,0,0,w,tabHeight);for(int i=0;i<tabObjects.Count;i++)PetTownMobileWindow.At((RectTransform)tabObjects[i].transform,i%cols*(w/cols),i/cols*46,w/cols-4,44);
        PetTownMobileWindow.At(searchRoot,0,tabHeight+4,w-94,44);PetTownMobileWindow.At(sortRoot,w-86,tabHeight+4,86,44);
        PetTownUITheme.Place((RectTransform)listScroll.transform,Vector2.zero,Vector2.one,new Vector2(0,24),new Vector2(0,-tabHeight-56));
        if(Mathf.Abs(w-shownWidth)>.5f){shownWidth=w;ReflowCards();if(showingDetail)LayoutDetail();}
    }
    void Rebuild(bool reset=false)
    {
        if(!IsOpen)return;filtered.Clear();foreach(var e in entries)if((category=="전체"||e.Category==category)&&(string.IsNullOrWhiteSpace(search.text)||e.Name.IndexOf(search.text.Trim(),StringComparison.OrdinalIgnoreCase)>=0))filtered.Add(e);
        if(SortMode==1)filtered.Sort((a,b)=>{int c=a.Price.CompareTo(b.Price);return c!=0?c:a.Key.CompareTo(b.Key);});else if(SortMode==2)filtered.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.Ordinal));
        foreach(var c in cards){c.SetActive(false);Destroy(c);}cards.Clear();
        foreach(var entry in filtered)
        {
            var card=theme.Panel("Card_"+entry.Key,listContent,Color.white,"Border");card.gameObject.AddComponent<RectMask2D>();var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;button.onClick.AddListener(()=>Choose(entry.Key));
            var slot=PetTownUITheme.Rect("Artwork",card.transform);var artBack=theme.Panel("ArtBack",slot,Color.white,"Border");PetTownMobileWindow.Fill(artBack.rectTransform);artBack.raycastTarget=false;var image=PetTownUITheme.Rect("ModelThumbnail",slot).gameObject.AddComponent<RawImage>();image.texture=entry.Thumbnail;image.raycastTarget=false;var fitter=image.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=entry.Thumbnail?(float)entry.Thumbnail.width/entry.Thumbnail.height:1;
            if(!entry.Thumbnail){image.gameObject.SetActive(false);var icon=PetTownUITheme.Rect("KindIcon",slot).gameObject.AddComponent<Image>();icon.sprite=PetTownGlyphs.Badge(EntryGlyph(entry));icon.preserveAspect=true;icon.raycastTarget=false;PetTownUITheme.Place(icon.rectTransform,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4));}
            var label=theme.Text("Name",card.transform,entry.Name,18);label.enableAutoSizing=true;label.fontSizeMin=15;label.fontSizeMax=18;
            var amount=theme.Text("Price",card.transform,entry.PriceText,16,theme.Ink);amount.enableAutoSizing=true;amount.fontSizeMin=13;amount.fontSizeMax=16;amount.textWrappingMode=TextWrappingModes.NoWrap;
            var extra=theme.Text("Info",card.transform,entry.SizeText,13,theme.Muted);extra.textWrappingMode=TextWrappingModes.NoWrap;extra.overflowMode=TextOverflowModes.Ellipsis;
            // The whole row and its trailing button open the existing detail/confirmation page.
            // A price on this button is a preview, never an immediate purchase.
            bool priced=!entry.TextOnlyDetail&&entry.PriceLabel==null;
            var open=theme.Button("Open",card.transform,priced?entry.PriceText:"보기 ›",()=>Choose(entry.Key),null);
            var openText=open.GetComponentInChildren<TextMeshProUGUI>();openText.fontSize=15;openText.enableAutoSizing=true;openText.fontSizeMin=12;openText.fontSizeMax=15;openText.textWrappingMode=TextWrappingModes.NoWrap;
            amount.gameObject.SetActive(!priced);
            cards.Add(card.gameObject);
        }
        if(!showingDetail){float tabRows=categories.Count<=2?0:Mathf.CeilToInt(categories.Count/(float)Mathf.Max(2,Mathf.Min(4,Mathf.FloorToInt(window.Body.rect.width/72))))*46;window.SetHeight(96+tabRows+60+ListHeight()+28);}
        count.text=filtered.Count==0?"검색 결과가 없습니다":filtered.Count+"개 항목 · 위아래로 스크롤";
        Layout();ReflowCards();if(reset)listScroll.verticalNormalizedPosition=1;
    }
    static bool PricedRow(PetTownCatalogEntry e)=>!e.TextOnlyDetail&&e.PriceLabel==null;
    float RowHeight(PetTownCatalogEntry e)=>PricedRow(e)?70:82;
    float ListHeight(){float h=0;foreach(var e in filtered)h+=RowHeight(e)+6;return Mathf.Max(0,h-6);}
    void ReflowCards()
    {
        float w=window.Body.rect.width;if(w<=0)return;float y=0;
        for(int i=0;i<cards.Count;i++)
        {
            var entry=filtered[i];bool priced=PricedRow(entry);float h=RowHeight(entry);
            var rt=(RectTransform)cards[i].transform;PetTownMobileWindow.At(rt,0,y,w,h);y+=h+6;
            var art=(RectTransform)rt.Find("Artwork");var label=(RectTransform)rt.Find("Name");var amount=(RectTransform)rt.Find("Price");var extra=(RectTransform)rt.Find("Info");var open=(RectTransform)rt.Find("Open");
            float buttonWidth=priced?90:58,textWidth=Mathf.Max(30,w-80-buttonWidth-16);
            PetTownMobileWindow.At(art,10,(h-52)*.5f,52,52);
            PetTownMobileWindow.At(open,w-buttonWidth-10,(h-44)*.5f,buttonWidth,44);
            bool hasAmount=!priced&&!string.IsNullOrWhiteSpace(amount.GetComponent<TextMeshProUGUI>().text);
            bool hasInfo=!string.IsNullOrWhiteSpace(extra.GetComponent<TextMeshProUGUI>().text);
            amount.gameObject.SetActive(hasAmount);extra.gameObject.SetActive(hasInfo);
            float blockHeight=24+(hasAmount?22:0)+(hasInfo?24:0),textY=(h-blockHeight)*.5f;
            PetTownMobileWindow.At(label,72,textY,textWidth,24);textY+=24;
            if(hasAmount){PetTownMobileWindow.At(amount,72,textY,textWidth,20);textY+=22;}
            if(hasInfo)PetTownMobileWindow.At(extra,72,textY,textWidth,22);
            label.GetComponent<TextMeshProUGUI>().textWrappingMode=TextWrappingModes.NoWrap;
            label.GetComponent<TextMeshProUGUI>().overflowMode=TextOverflowModes.Ellipsis;
            amount.GetComponent<TextMeshProUGUI>().overflowMode=TextOverflowModes.Ellipsis;
        }
        listContent.sizeDelta=new Vector2(0,Mathf.Max(0,y-6));
    }
    void UpdateDetail()
    {
        nameLabel.text=selected?.Name??"선택한 항목 없음";large.texture=selected?.Thumbnail;large.gameObject.SetActive(large.texture);if(large.texture)large.GetComponent<AspectRatioFitter>().aspectRatio=(float)large.texture.width/large.texture.height;
        actionPortrait.sprite=PetTownGlyphs.Badge(selected!=null?EntryGlyph(selected):PetTownGlyphs.Pick(title));actionPortrait.gameObject.SetActive(!large.texture);
        price.text=selected?.PriceText??"";size.text=selected?.SizeText??"";description.text=selected?.Description??"";confirm.interactable=selected!=null;confirm.GetComponentInChildren<TextMeshProUGUI>().text=selected!=null?selected.ActionText:"선택하기";LayoutDetail();
    }
    void LayoutDetail()
    {
        float w=window.Body.rect.width,y=0;PetTownMobileWindow.At(nameLabel.rectTransform,2,y,w-4,32);y+=38;
        bool art=selected!=null&&!selected.TextOnlyDetail;largeSlot.gameObject.SetActive(art);if(art){PetTownMobileWindow.At(largeSlot,20,y,w-40,116);y+=126;}
        float priceHeight=Mathf.Max(28,price.GetPreferredValues(price.text,w-6,0).y+4);
        PetTownMobileWindow.At(price.rectTransform,3,y,w-6,priceHeight);y+=priceHeight+4;PetTownMobileWindow.At(size.rectTransform,3,y,w-6,30);y+=34;
        float h=Mathf.Max(48,description.GetPreferredValues(description.text,w-12,0).y+10);PetTownMobileWindow.At(description.rectTransform,6,y,w-12,h);detailContent.sizeDelta=new Vector2(0,y+h+12);window.SetHeight(y+h+184);
    }
    void Update(){TextEntryFocused=IsOpen&&search.isFocused;if(!IsOpen)return;if(Input.GetKeyDown(KeyCode.Escape)){if(search.isFocused)search.DeactivateInputField();else BackToList();}if(PetTownCustomizationUI.IsCharacterWindowOpen)Cancel();}
    void LateUpdate()
    {
        if(!IsOpen)return;Layout();
        if(Time.unscaledTime>=nextDynamicRefresh)
        {
            nextDynamicRefresh=Time.unscaledTime+.25f;
            for(int i=0;i<cards.Count&&i<filtered.Count;i++)
            {
                var entry=filtered[i];if(!entry.Dynamic)continue;
                var amount=cards[i].transform.Find("Price").GetComponent<TextMeshProUGUI>();var info=cards[i].transform.Find("Info").GetComponent<TextMeshProUGUI>();
                string priceValue=entry.PriceText,sizeValue=entry.SizeText;bool changed=amount.text!=priceValue||info.text!=sizeValue;
                if(amount.text!=priceValue)amount.text=priceValue;if(info.text!=sizeValue)info.text=sizeValue;
                if(changed)ReflowCards();
            }
            if(showingDetail&&selected!=null&&selected.Dynamic){price.text=selected.PriceText;size.text=selected.SizeText;confirm.GetComponentInChildren<TextMeshProUGUI>().text=selected.ActionText;}
        }
        foreach(var e in entries)if(e.Thumbnail==null&&e.ThumbnailProvider!=null){e.Thumbnail=e.ThumbnailProvider();if(e.Thumbnail!=null){float pos=listScroll.verticalNormalizedPosition;Rebuild();listScroll.verticalNormalizedPosition=pos;if(selected==e)UpdateDetail();break;}}
    }
    void OnDisable(){Hide();}
    void OnDestroy(){TextEntryFocused=false;window?.Dispose();theme?.Dispose();}
}
