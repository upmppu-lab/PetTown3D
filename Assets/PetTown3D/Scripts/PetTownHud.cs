using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation adapter: all actions and values come from the existing game.
public sealed class PetTownHud : MonoBehaviour
{
    PetTownUITheme theme;RectTransform safe,main,nav,statusPanel,coinPanel,orderPanel,toastPanel,quick;
    TextMeshProUGUI status,coins,order,toast;RawImage portrait;Button[] navigation=new Button[5];Button switchButton;
    bool modern=true;Action<bool> switchLegacy;
    PetTownCozyHudView cozy;
    public bool Ready=>cozy!=null?cozy.Ready:theme!=null&&theme.Ready;
    public bool Modern=>modern&&Ready;
    public void Initialize(Transform parent,Action<int> navigate,Action save,Action roof,Action serve,Action<bool> setLegacy)
    {
        if(PetTownCozyLibrary.Current!=null){switchLegacy=setLegacy;cozy=gameObject.AddComponent<PetTownCozyHudView>();cozy.Initialize(parent,navigate,save,roof,serve);setLegacy(false);return;}
        theme=new PetTownUITheme();if(!Ready)return;switchLegacy=setLegacy;
        main=PetTownUITheme.Rect("PetTown_KenneyHUD",parent);PetTownUITheme.Place(main,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);main.SetAsFirstSibling();
        safe=PetTownUITheme.Rect("SafeArea",main);PetTownUITheme.Place(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        statusPanel=theme.Panel("Status",safe,theme.Cream).rectTransform;portrait=PetTownUITheme.Rect("Portrait",statusPanel).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;
        PetTownUITheme.Place(portrait.rectTransform,Vector2.zero,new Vector2(0,1),new Vector2(10,10),new Vector2(56,-10));
        status=theme.Text("StatusText",statusPanel,"",17);PetTownUITheme.Place(status.rectTransform,Vector2.zero,Vector2.one,new Vector2(66,8),new Vector2(-10,-8));
        coinPanel=theme.Panel("Gold",safe,theme.Cream).rectTransform;coins=theme.Text("GoldText",coinPanel,"",19,theme.Ink,TextAlignmentOptions.Center);PetTownUITheme.Place(coins.rectTransform,Vector2.zero,Vector2.one,new Vector2(10,0),new Vector2(-10,0));
        nav=theme.Panel("Navigation",safe,theme.Cream).rectTransform;string[] names={"탐험","건설","가구","캐릭터","펫샵"};
        for(int i=0;i<5;i++){int index=i;navigation[i]=theme.Button("Nav_"+i,nav,names[i],()=>navigate(index));PetTownUITheme.Place((RectTransform)navigation[i].transform,new Vector2(i*.2f,0),new Vector2((i+1)*.2f,1),new Vector2(5,6),new Vector2(-5,-6));}
        orderPanel=theme.Panel("Order",safe,new Color(1,.86f,.62f)).rectTransform;order=theme.Text("OrderText",orderPanel,"",17);PetTownUITheme.Place(order.rectTransform,Vector2.zero,Vector2.one,new Vector2(12,6),new Vector2(-12,-6));var sell=orderPanel.gameObject.AddComponent<Button>();sell.onClick.AddListener(()=>serve());
        toastPanel=theme.Panel("Notification",safe,theme.Cream).rectTransform;toast=theme.Text("NoticeText",toastPanel,"",16,theme.Ink,TextAlignmentOptions.Center);PetTownUITheme.Place(toast.rectTransform,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4));
        quick=PetTownUITheme.Rect("QuickActions",safe);var saveButton=theme.Button("Save",quick,"저장",save);PetTownUITheme.Place((RectTransform)saveButton.transform,Vector2.zero,new Vector2(.5f,1),new Vector2(0,0),new Vector2(-3,0));var roofButton=theme.Button("Roof",quick,"지붕",roof);PetTownUITheme.Place((RectTransform)roofButton.transform,new Vector2(.5f,0),Vector2.one,new Vector2(3,0),Vector2.zero);
        switchButton=theme.Button("HUD_Fallback",parent,"기존 UI",()=>SetModern(!modern),Color.white);Layout();SetModern(true);
    }
    public void SetModern(bool value){modern=value;if(cozy!=null)cozy.SetVisible(value);if(main!=null)main.gameObject.SetActive(value);if(switchButton!=null)switchButton.GetComponentInChildren<TextMeshProUGUI>().text=value?"기존 UI":"새 UI";switchLegacy?.Invoke(!value);}
    public void SetState(int day,int people,int reputation,int gold,Texture portraitTexture,string notification,bool showNotice,string request,bool showOrder,int active)
    {
        if(cozy!=null){cozy.SetState(day,people,reputation,gold,portraitTexture,notification,showNotice,request,showOrder,active);return;}
        if(!Modern)return;Layout();status.text="PET TOWN 3D\n"+"Day "+day+" · 주민 "+people+" · 명성 "+reputation;coins.text=gold.ToString("N0")+" 골드";portrait.texture=portraitTexture;
        toastPanel.gameObject.SetActive(showNotice);toast.text=notification;orderPanel.gameObject.SetActive(showOrder);order.text=request;
        for(int i=0;i<navigation.Length;i++)navigation[i].targetGraphic.color=i==active?new Color(.62f,.88f,.68f):Color.white;
    }
    void Layout()
    {
        var screen=Screen.safeArea;safe.anchorMin=new Vector2(screen.xMin/Screen.width,screen.yMin/Screen.height);safe.anchorMax=new Vector2(screen.xMax/Screen.width,screen.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
        float width=safe.rect.width;bool narrow=width<850;
        Fixed(statusPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-12),new Vector2(narrow?width-24:340,72));
        Fixed(coinPanel,narrow?new Vector2(0,1):new Vector2(1,1),narrow?new Vector2(0,1):new Vector2(1,1),narrow?new Vector2(12,-92):new Vector2(-12,-12),new Vector2(narrow?Mathf.Min(210,width*.48f):220,46));
        Fixed(quick,new Vector2(1,1),new Vector2(1,1),new Vector2(-12,narrow?-92:-66),new Vector2(narrow?Mathf.Min(190,width*.44f):220,38));
        Fixed(nav,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,12),new Vector2(Mathf.Min(620,width-24),62));
        Fixed(orderPanel,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,narrow?-146:-118),new Vector2(Mathf.Min(540,width-24),62));
        Fixed(toastPanel,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,82),new Vector2(Mathf.Min(620,width-24),58));
        Fixed((RectTransform)switchButton.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-12,150),new Vector2(88,32));
    }
    static void Fixed(RectTransform rt,Vector2 anchor,Vector2 pivot,Vector2 point,Vector2 size){rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=point;rt.sizeDelta=size;}
    void OnDestroy(){theme?.Dispose();}
}
