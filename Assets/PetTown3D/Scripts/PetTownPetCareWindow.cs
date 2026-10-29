using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PetTownPetCareWindow : MonoBehaviour
{
    public static bool TextEntryFocused;
    public bool IsOpen=>layer&&layer.gameObject.activeSelf;
    public PetTownPetCareRecord Selected=>targets.Count>0?state.Get(targets[index].id,targets[index].name):null;
    PetTownUITheme theme;PetTownMobileWindow window;RectTransform layer,panel,content;float layoutWidth=-1;RawImage portrait;TMP_InputField nickname;
    TextMeshProUGUI summary,level,availability,feedback;readonly List<Image> fills=new List<Image>();readonly List<TextMeshProUGUI> values=new List<TextMeshProUGUI>();
    readonly List<Sprite> icons=new List<Sprite>();List<PetTownPetCareTarget> targets=new List<PetTownPetCareTarget>();PetTownPetCareState state;Action<string> notify;
    int index;float nextSave,nextRefresh;bool dirty;
    Material levelMaterial;
    PetTownPetCareContext context;TextMeshProUGUI foodText;Button favoriteButton,groomButton;
    readonly Dictionary<string,float> cooldowns=new Dictionary<string,float>();
    public void Show(Transform parent,List<PetTownPetCareTarget> data,Action<string> message,PetTownPetCareContext shared=null)
    {
        targets=data;notify=message;context=shared;state=context?.state??state??PetTownPetCareState.Load();index=Mathf.Clamp(index,0,Mathf.Max(0,data.Count-1));
        foreach(var target in data)state.Get(target.id,target.name);
        if(!layer)Build(parent);window.Show();LayoutControls();Refresh();
    }
    void Build(Transform parent)
    {
        theme=new PetTownUITheme();window=new PetTownMobileWindow("PetTownMobilePetCare",theme,Close);window.SetHeader("펫 정보",9,false);window.SetHeight(720);layer=window.Layer;panel=window.Panel;
        var scroll=PetTownMobileWindow.Scroll("PetCareScroll",window.Body,out content);PetTownMobileWindow.Fill((RectTransform)scroll.transform);
        theme.Button("PreviousPet",content,"‹",()=>Change(-1),Color.white);theme.Button("NextPet",content,"›",()=>Change(1),Color.white);
        var field=theme.Panel("PetName",content,Color.white,"Border");nickname=field.gameObject.AddComponent<TMP_InputField>();var input=theme.Text("Name",field.transform,"",19,theme.Ink,TextAlignmentOptions.Center);input.fontWeight=FontWeight.Bold;input.enableAutoSizing=true;input.fontSizeMin=13;input.fontSizeMax=19;input.textWrappingMode=TextWrappingModes.NoWrap;PetTownUITheme.Place(input.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,0),new Vector2(-8,0));nickname.textViewport=input.rectTransform;nickname.textComponent=input;nickname.characterLimit=14;nickname.onEndEdit.AddListener(Rename);
        var imageFrame=theme.Panel("Portrait",content,Color.white,"Border");
        var portraitViewport=PetTownUITheme.Rect("PortraitViewport",imageFrame.transform);PetTownUITheme.Place(portraitViewport,Vector2.zero,Vector2.one,new Vector2(9,9),new Vector2(-9,-9));
        portrait=PetTownUITheme.Rect("ActualPet",portraitViewport).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;portrait.gameObject.AddComponent<AspectRatioFitter>().aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        level=theme.Text("Level",imageFrame.transform,"",16,Color.black,TextAlignmentOptions.Center);
        var bold=theme.Font.fontWeightTable[7].regularTypeface;if(bold)level.font=bold;
        levelMaterial=new Material(level.font.material);levelMaterial.EnableKeyword("OUTLINE_ON");levelMaterial.SetColor("_OutlineColor",new Color(1,.82f,.16f,1));levelMaterial.SetFloat("_OutlineWidth",.20f);level.fontSharedMaterial=levelMaterial;level.UpdateMeshPadding();level.textWrappingMode=TextWrappingModes.NoWrap;
        At(level.rectTransform,-4,-6,56,24);
        summary=theme.Text("PetDetails",content,"",14,theme.Muted,TextAlignmentOptions.MidlineLeft);summary.enableAutoSizing=true;summary.fontSizeMin=12;summary.fontSizeMax=14;availability=theme.Text("Availability",content,"",14);
        favoriteButton=theme.Button("ProtectPet",content,"보호하기",()=>{if(Selected!=null){context?.toggleFavorite?.Invoke(Selected);Refresh();}},Color.white);
        string[] labels={"성장","배부름","청결","스트레스","심심함"};Color[] colors={new Color(1,.73f,.22f),new Color(.99f,.56f,.21f),new Color(.27f,.67f,.83f),new Color(.91f,.39f,.33f),new Color(.63f,.50f,.80f)};
        for(int i=0;i<5;i++)
        {
            var need=theme.Text("Need_"+i,content,labels[i],14);need.fontWeight=FontWeight.Bold;var track=theme.Panel("NeedTrack_"+i,content,Color.white,"BarTrack");var fill=theme.Panel("NeedFill",track.transform,Color.white,"BarFill");fill.color=colors[i];fill.raycastTarget=false;PetTownUITheme.Place(fill.rectTransform,Vector2.zero,Vector2.one,new Vector2(2,2),new Vector2(-2,-2));fills.Add(fill);var amount=theme.Text("Value",track.transform,"",12,theme.Ink,TextAlignmentOptions.Center);amount.fontWeight=FontWeight.Bold;PetTownMobileWindow.Fill(amount.rectTransform);values.Add(amount);
        }
        var food=theme.Panel("CareFood",content,Color.white,"Border");foodText=theme.Text("FoodLabel",food.transform,"",13,theme.Muted);PetTownUITheme.Place(foodText.rectTransform,new Vector2(0,.5f),Vector2.one,new Vector2(10,0),new Vector2(-10,-4));
        var buy=theme.Button("BuyFood",food.transform,"사료 5개 구매",()=>{if(Selected!=null){context?.buyFood?.Invoke(Selected);Refresh();}},Color.white);PetTownUITheme.Place((RectTransform)buy.transform,Vector2.zero,new Vector2(1,.5f),new Vector2(10,4),new Vector2(-10,0));
        string[] names={"먹이 주기","목욕","빗질","놀아주기","쓰다듬기","사진","안기","미용"};var atlas=PetTownCozyLibrary.Current.careIcons;
        for(int i=0;i<8;i++)
        {
            int action=i;var button=theme.Button("Care_"+i,content,names[i],()=>Care(action),Color.white);var label=button.GetComponentInChildren<TextMeshProUGUI>();label.fontSize=13;label.enableAutoSizing=true;label.fontSizeMin=11;label.fontSizeMax=13;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
            if(atlas){var icon=PetTownUITheme.Rect("CareIcon",button.transform).gameObject.AddComponent<Image>();var sprite=Sprite.Create(atlas,new Rect(i%4*atlas.width/4f,(2-i/4)*atlas.height/3f,atlas.width/4f,atlas.height/3f),new Vector2(.5f,.5f),100);icons.Add(sprite);icon.sprite=sprite;icon.preserveAspect=true;icon.raycastTarget=false;PetTownUITheme.Place(icon.rectTransform,new Vector2(0,.33f),Vector2.one,new Vector2(8,0),new Vector2(-8,-5));}
            if(i==7){groomButton=button;button.interactable=context!=null;label.text="미용";}
        }
        feedback=theme.Text("CareFeedback",content,"",14,theme.Muted,TextAlignmentOptions.Center);LayoutControls();
    }
    void LayoutControls()
    {
        float w=window.Body.rect.width;if(Mathf.Abs(w-layoutWidth)<.5f)return;layoutWidth=w;
        At((RectTransform)content.Find("PreviousPet"),0,0,44,44);At((RectTransform)content.Find("PetName"),52,0,w-104,44);At((RectTransform)content.Find("NextPet"),w-44,0,44,44);
        At((RectTransform)content.Find("Portrait"),6,60,104,104);At(summary.rectTransform,124,60,w-128,104);
        At(availability.rectTransform,4,174,w-116,34);At((RectTransform)favoriteButton.transform,w-104,169,104,44);
        for(int i=0;i<5;i++){float y=222+i*27;At((RectTransform)content.Find("Need_"+i),4,y,72,24);At((RectTransform)content.Find("NeedTrack_"+i),80,y,w-80,24);}
        At((RectTransform)content.Find("CareFood"),0,366,w,84);At(foodText.rectTransform,18,9,w-36,22);At((RectTransform)content.Find("CareFood/BuyFood"),10,34,w-20,44);
        float cw=(w-18)/4;
        for(int i=0;i<8;i++)
        {
            var cell=(RectTransform)content.Find("Care_"+i);At(cell,i%4*(cw+6),462+i/4*88,cw,80);
            var label=cell.GetComponentInChildren<TextMeshProUGUI>();At(label.rectTransform,4,48,cw-8,22);
            var icon=cell.Find("CareIcon") as RectTransform;if(icon)At(icon,(cw-38)*.5f,8,38,38);
        }
        At(feedback.rectTransform,0,638,w,36);content.sizeDelta=new Vector2(0,678);
    }
    void LateUpdate(){if(IsOpen){window.Layout();LayoutControls();}}
    void Rename(string value){if(Selected==null||string.IsNullOrWhiteSpace(value))return;Selected.nickname=value.Trim();dirty=true;Save();}
    public void SelectPet(string id){int found=targets.FindIndex(t=>t.id==id);if(found>=0){index=found;Refresh();}}
    void Change(int step){if(targets.Count==0)return;index=(index+step+targets.Count)%targets.Count;Refresh();}
    public void Care(int action)
    {
        if(Selected==null)return;
        if(action==3&&context?.play!=null){context.play(Selected);return;}
        if(action==5){var target=targets[index];if(target.portraitUv!=new Rect(0,0,1,1)){feedback.text="이 펫의 개별 사진은 아직 준비되지 않았어요.";return;}if(target.portrait){string folder=System.IO.Path.Combine(Application.persistentDataPath,"PetPhotos");System.IO.Directory.CreateDirectory(folder);var rt=RenderTexture.GetTemporary(target.portrait.width,target.portrait.height);Graphics.Blit(target.portrait,rt);var old=RenderTexture.active;RenderTexture.active=rt;var copy=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);copy.Apply();RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,target.id+".png"),copy.EncodeToPNG());Destroy(copy);feedback.text="펫 사진을 저장했어요.";notify?.Invoke(feedback.text);}return;}
        if(action<0||action>7||(action==7&&context==null))return;string key=Selected.id+":"+action;
        if(cooldowns.TryGetValue(key,out float until)&&Time.unscaledTime<until){feedback.text="잠시 뒤에 다시 돌봐 주세요.";return;}
        cooldowns[key]=Time.unscaledTime+8;
        if(context!=null)feedback.text=context.care(Selected,action);else{Selected.Care(action);feedback.text="돌봐 주었어요.";}
        dirty=true;Save();notify?.Invoke(feedback.text);Refresh();
    }
    string SlotSuffix(PetTownPetCareRecord pet){var text=context?.slot?.Invoke(pet);return string.IsNullOrEmpty(text)?"":"  ·  "+text;}
    void Refresh()
    {
        if(!IsOpen)return;var pet=Selected;if(pet==null){summary.text="펫샵에 전시 중인 펫이 없습니다.";return;}var target=targets[index];nickname.SetTextWithoutNotify(pet.nickname);portrait.texture=target.portrait;portrait.uvRect=target.portrait?PetTownUIArtwork.ContentUV(target.portrait,target.portraitUv):target.portraitUv;portrait.gameObject.SetActive(target.portrait!=null);if(target.portrait)portrait.GetComponent<AspectRatioFitter>().aspectRatio=target.portrait.width*portrait.uvRect.width/(target.portrait.height*portrait.uvRect.height);level.text="Lv. "+pet.Level;
        summary.text=target.species+"  ·  "+pet.SexLabel+SlotSuffix(pet)+"\n기분: "+(pet.stress>55?"휴식이 필요해요":pet.boredom>55?"함께 놀고 싶어요":"평온해요")+"\n"+pet.LifeStage+"  ·  XP "+pet.experience+" / "+pet.ExperienceNeeded+(pet.groomed?(pet.displayOnly?"\n미용 완료":"\n미용 완료 · 분양가 +20%"):"");
        availability.text=pet.IsBreeding?"교배 중 · 분양 대기":pet.displayOnly?"전시 동물 · 분양 제외":pet.favorite?"보호 중 · 분양 제외":pet.Adoptable?"분양 가능":"성장 중 · Lv.3부터 분양 가능";
        favoriteButton.interactable=!pet.displayOnly&&context!=null;favoriteButton.GetComponentInChildren<TextMeshProUGUI>().text=pet.favorite?"보호 해제":"보호하기";
        if(groomButton){groomButton.interactable=context!=null&&!pet.groomed;groomButton.GetComponentInChildren<TextMeshProUGUI>().text=pet.groomed?"미용 완료":"미용";}
        int foodLine=PetTownProgressionState.FoodLineOf(Mathf.Clamp(pet.speciesKind,0,PetTownProgressionState.Species.Length-1));foodText.text=(context?.food?.Invoke(pet)??"기본 사료")+" · 5개 "+PetTownProgressionState.FoodPrice[foodLine]*5+"G";
        float[] needs={pet.growth*100,pet.fullness,pet.cleanliness,pet.stress,pet.boredom};for(int i=0;i<5;i++){fills[i].gameObject.SetActive(needs[i]>0);fills[i].rectTransform.anchorMax=new Vector2(Mathf.Max(.03f,needs[i]/100f),1);values[i].text=Mathf.RoundToInt(needs[i])+"%";}
    }
    void Update()
    {
        if(state==null)return;if(context==null&&Application.isFocused)foreach(var target in targets){state.Get(target.id,target.name).Tick(Mathf.Min(Time.deltaTime,.25f));dirty=true;}
        if(Time.unscaledTime>=nextSave){nextSave=Time.unscaledTime+30;Save();}TextEntryFocused=IsOpen&&nickname.isFocused;
        if(IsOpen){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.5f;if(!nickname.isFocused)Refresh();}if(Input.GetKeyDown(KeyCode.Escape))Close();}
    }
    void Save(){if(dirty){if(context!=null)context.save?.Invoke();else state.Save();dirty=false;}}
    public void Close(){if(layer)layer.gameObject.SetActive(false);if(nickname)nickname.DeactivateInputField();TextEntryFocused=false;Save();}
    void OnApplicationPause(bool pause){if(pause)Save();}
    void OnDisable(){Close();}
    void OnDestroy(){Save();TextEntryFocused=false;foreach(var s in icons)if(s)Destroy(s);if(levelMaterial)Destroy(levelMaterial);window?.Dispose();theme?.Dispose();}
    static void At(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
}
