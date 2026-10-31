using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Compact mobile HUD. All numbers and button callbacks still come from the existing game.
public sealed partial class PetTownCozyHudView : MonoBehaviour
{
    PetTownUITheme theme;
    RectTransform safe,layout,profile,gold,calendar,weather,rank,nav,orderPanel,noticePanel;
    TextMeshProUGUI status,population,versionLabel,coins,dayLabel,weatherLabel,levelLabel,experienceLabel,reputationLabel,order,notice;
    Image experienceFill;
    readonly Button[] buttons=new Button[10];
    readonly List<Material> labelMaterials=new List<Material>();
    public bool Ready=>theme!=null&&theme.Ready;
    public void SetVisible(bool visible){if(safe)safe.gameObject.SetActive(visible);}
    public void Initialize(Transform parent,Action<int> navigate,Action save,Action roof,Action serve)
    {
        theme=new PetTownUITheme();safe=PetTownUITheme.Rect("PetTown_CozyHUD",parent);PetTownUITheme.Place(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);safe.SetAsFirstSibling();
        layout=PetTownUITheme.Rect("HudReference",safe);
        profile=PetTownUITheme.Rect("CompactProfile",layout);
        var face=theme.Panel("Avatar",profile,Color.white,"Icon_4");Fixed(face.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-4),new Vector2(84,84));
        status=theme.Text("TownStatus",profile,"",14,Color.white);PetTownUITheme.Place(status.rectTransform,Vector2.zero,Vector2.one,new Vector2(63,20),new Vector2(0,-3));LabelOutline(status);
        var xpTrack=theme.Panel("PlayerExperience",profile,Color.white,"BarTrack");Fixed(xpTrack.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(96,-32),new Vector2(156,20));experienceFill=theme.Panel("ExperienceFill",xpTrack.transform,new Color(.39f,.75f,.94f),"BarFill");PetTownUITheme.Place(experienceFill.rectTransform,Vector2.zero,Vector2.one,new Vector2(1,1),new Vector2(-1,-1));
        experienceLabel=theme.Text("ExperienceAmount",xpTrack.transform,"",10,Color.black,TextAlignmentOptions.Center);PetTownUITheme.Place(experienceLabel.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);experienceLabel.fontStyle=FontStyles.Bold;
        var badgeRoot=PetTownUITheme.Rect("PlayerLevelBadge",profile);var badge=badgeRoot.gameObject.AddComponent<Image>();badge.sprite=theme.Style.hudChip;badge.type=Image.Type.Sliced;badge.raycastTarget=false;Fixed(badge.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(-1,-1),new Vector2(23,19));levelLabel=theme.Text("PlayerLevel",badge.transform,"1",12,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(levelLabel.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);levelLabel.fontStyle=FontStyles.Bold;levelLabel.enableAutoSizing=true;levelLabel.fontSizeMin=9;levelLabel.fontSizeMax=12;
        face.gameObject.AddComponent<Button>().onClick.AddListener(()=>OpenProfilePanel());face.raycastTarget=true;
        status.gameObject.SetActive(false);
        BuildProfileTexts();
        BuildPopulationChip(navigate);
        versionLabel=theme.Text("VersionLabel",layout,"PET TOWN 3D update ver.001",10,new Color(1,1,1,.8f),TextAlignmentOptions.Right);versionLabel.raycastTarget=false;Fixed(versionLabel.rectTransform,new Vector2(1,0),new Vector2(1,0),new Vector2(-8,4),new Vector2(220,16));versionLabel.textWrappingMode=TextWrappingModes.NoWrap;LabelOutline(versionLabel);
        gold=Chip("Gold",layout,theme.Style.hudChip);var coin=theme.Panel("Coin",gold,Color.white,"Icon_8");Fixed(coin.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(7,0),new Vector2(29,29));
        coins=theme.Text("GoldAmount",gold,"",20,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(coins.rectTransform,Vector2.zero,Vector2.one,new Vector2(38,2),new Vector2(-8,-2));coins.textWrappingMode=TextWrappingModes.NoWrap;coins.enableAutoSizing=true;coins.fontSizeMin=13;coins.fontSizeMax=20;
        calendar=Chip("Calendar",layout,theme.Style.hudChip);var sun=theme.Panel("Sun",calendar,Color.white,"Icon_10");Fixed(sun.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(6,0),new Vector2(23,23));
        dayLabel=theme.Text("Day",calendar,"",14,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(dayLabel.rectTransform,Vector2.zero,Vector2.one,new Vector2(29,0),new Vector2(-7,0));
        dayLabel.enableAutoSizing=true;dayLabel.fontSizeMin=11;dayLabel.fontSizeMax=14;dayLabel.textWrappingMode=TextWrappingModes.NoWrap;
        weather=Chip("Weather",layout,theme.Style.hudChip);weatherLabel=theme.Text("WeatherValue",weather,"",12,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(weatherLabel.rectTransform,Vector2.zero,Vector2.one,new Vector2(5,0),new Vector2(-5,0));
        rank=Chip("Reputation",layout,theme.Style.hudChip);rank.gameObject.AddComponent<Button>().onClick.AddListener(()=>{var gi=PetTownGame.Instance;if(gi!=null)gi.OpenReputationInfo();});reputationLabel=theme.Text("ReputationValue",rank,"",12,new Color(1,.85f,.43f),TextAlignmentOptions.Center);PetTownUITheme.Place(reputationLabel.rectTransform,Vector2.zero,Vector2.one,new Vector2(6,0),new Vector2(-6,0));
        nav=PetTownUITheme.Rect("IndependentMenus",layout);PetTownUITheme.Place(nav,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        string[] names={"건설","꾸미기","가방","펫샵 관리","캐릭터","주민","펫","설정"};int[] routes={1,2,5,4,3,6,7,8,PetTownGame.RoadButtonRoute,PetTownGame.BuyPetButtonRoute};
        for(int i=0;i<10;i++)
        {
            int action=routes[i];var r=PetTownUITheme.Rect("Navigation_"+i,nav);var image=r.gameObject.AddComponent<Image>();image.color=new Color(0,0,0,0);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>navigate(action));buttons[i]=button;
            button.transition=Selectable.Transition.None;NavVisual(r,i);
        }
        BuildShortcuts(navigate);
        buttons[4].gameObject.SetActive(false);buttons[5].gameObject.SetActive(false);
        var settingsRoot=(RectTransform)buttons[7].transform;for(int c=settingsRoot.childCount-1;c>=0;c--)Destroy(settingsRoot.GetChild(c).gameObject);navVisual[7]=MakeCompact(settingsRoot,PetTownGlyphs.Glyph(NavGlyphs[7]),NavColors[7]);
        orderPanel=Chip("CustomerOrder",layout,theme.Style.hudMessage);order=ToastLabel("Order",orderPanel);orderPanel.gameObject.AddComponent<Button>().onClick.AddListener(()=>serve());
        noticePanel=Chip("TownNotice",layout,theme.Style.hudMessage);notice=ToastLabel("Notice",noticePanel);noticePanel.GetComponent<Image>().raycastTarget=false;
        BuildProfilePanel(navigate);
        Layout();
    }
    RectTransform Chip(string name,Transform parent,Sprite sprite)
    {
        var r=PetTownUITheme.Rect(name,parent);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=2;return r;
    }
    TextMeshProUGUI ToastLabel(string name,Transform parent)
    {
        var label=theme.Text(name,parent,"",14,Color.white,TextAlignmentOptions.Center);PetTownUITheme.Place(label.rectTransform,Vector2.zero,Vector2.one,new Vector2(12,5),new Vector2(-12,-5));return label;
    }
    void LabelOutline(TextMeshProUGUI label,bool light=false)
    {
        var material=new Material(label.font.material);material.EnableKeyword("OUTLINE_ON");material.SetColor("_OutlineColor",light?new Color(1,1,.94f):new Color(.18f,.11f,.045f));material.SetFloat("_OutlineWidth",light?.16f:.11f);label.fontSharedMaterial=material;label.UpdateMeshPadding();labelMaterials.Add(material);
    }
    void Layout()
    {
        var area=Screen.safeArea;safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
        // Keep physical menu and text sizes readable in portrait without changing the parent Canvas.
        float parentScale=Mathf.Max(.0001f,safe.lossyScale.x);
        float desiredScale=Screen.height>=Screen.width?area.width/520f:area.height/640f;
        float factor=desiredScale/parentScale;
        layout.anchorMin=layout.anchorMax=layout.pivot=Vector2.zero;layout.anchoredPosition=Vector2.zero;
        layout.localScale=Vector3.one*factor;layout.sizeDelta=safe.rect.size/factor;
        Fixed(profile,new Vector2(0,1),new Vector2(0,1),new Vector2(10,-10),new Vector2(262,92));Fixed(gold,Vector2.one,Vector2.one,new Vector2(-10,-10),new Vector2(186,37));Fixed(rank,Vector2.one,Vector2.one,new Vector2(-10,-52),new Vector2(148,24));Fixed(calendar,new Vector2(0,1),new Vector2(0,1),new Vector2(10,-108),new Vector2(190,30));Fixed(weather,new Vector2(0,1),new Vector2(0,1),new Vector2(10,-144),new Vector2(48,30));Fixed(peopleChip,new Vector2(0,1),new Vector2(0,1),new Vector2(64,-144),new Vector2(140,30));
        // 아래 줄 순서: 펫 사기, 건설, 꾸미기, 가방, 펫샵 관리, 길
        int[] slotOf={1,2,3,4,-1,-1,5,-1,6,0};
        foreach(int bi in new[]{0,1,2,3,6,8,9})Fixed((RectTransform)buttons[bi].transform,Vector2.zero,Vector2.zero,new Vector2(10+slotOf[bi]*72,6),new Vector2(ButtonWidth,ButtonHeight));
        LayoutShortcuts();
        Fixed((RectTransform)buttons[7].transform,Vector2.one,Vector2.one,new Vector2(-202,-8),new Vector2(CompactSize,CompactSize));
    }
    void SizeMessage(RectTransform box,TextMeshProUGUI label)
    {
        float maximum=Mathf.Min(760,layout.rect.width-110);Vector2 preferred=label.GetPreferredValues(label.text,maximum-26,0);
        Fixed(box,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-Mathf.Min(180,layout.rect.height*.26f)),new Vector2(Mathf.Clamp(preferred.x+26,48,maximum),Mathf.Clamp(preferred.y+12,30,68)));
    }
    public void SetState(int day,int people,int reputation,int money,Texture face,string message,bool showMessage,string request,bool hasOrder,int active)
    {
        if(!Ready)return;Layout();var game=PetTownGame.Instance;var progress=game!=null?game.Progression:null;
        population.text=people+" / "+(game!=null?game.ResidentCapacity:people)+"명";if(game!=null){townLabel.text=game.TownName;nameLabel.text=game.PlayerName;}coins.text=money.ToString("N0");dayLabel.text=game!=null?game.GameClockText:"Day "+day;weatherLabel.text=game!=null?game.TimeWeather.WeatherName+" · "+(game.TimeWeather.Night?"밤":"낮"):"맑음 · 낮";reputationLabel.text="행복 "+(game!=null?game.TownHappiness:45)+" · 명성 "+reputation+" "+PetTownReputation.Grade(reputation);
        if(progress!=null){levelLabel.text=progress.playerLevel.ToString();experienceLabel.text=progress.playerLevel>=100?"MAX":progress.playerExperience+" / "+progress.ExperienceNeeded;float fraction=progress.playerLevel>=100?1:Mathf.Clamp01(progress.playerExperience/(float)progress.ExperienceNeeded);experienceFill.gameObject.SetActive(fraction>0);experienceFill.rectTransform.anchorMax=new Vector2(Mathf.Max(.02f,fraction),1);}
        bool modal=PetTownGame.Instance!=null&&PetTownGame.Instance.IsPresentationMenuOpen;
        bool duplicate=hasOrder&&message!=null&&message.StartsWith("Customer arrived!");bool showNotice=showMessage&&!string.IsNullOrEmpty(message)&&!duplicate;
        order.text=Localize(request);notice.text=Localize(message);SizeMessage(orderPanel,order);SizeMessage(noticePanel,notice);
        orderPanel.gameObject.SetActive(hasOrder&&!modal&&!showNotice);noticePanel.gameObject.SetActive(showNotice&&!modal);
        int[] routes={1,2,5,4,3,6,7,8,PetTownGame.RoadButtonRoute,PetTownGame.BuyPetButtonRoute};ApplyNavState(routes,active);
        UpdateWeatherIcon(game);FitChips();RefreshShortcuts();
    }
    static string Localize(string text){return (text??"").Replace("Puppy","강아지").Replace("Kitten","고양이").Replace("Bunny","토끼").Replace("Game saved","마을을 저장했어요").Replace("Saved town loaded","저장한 마을을 불러왔어요").Replace("Customer left without a pet. -1 fame","손님이 돌아갔어요. 명성 -1");}
    static void Fixed(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size){r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;}
    void OnDestroy(){DisposeWeatherIcons();PetTownGlyphs.Release();if(safe)Destroy(safe.gameObject);foreach(var m in labelMaterials)if(m)Destroy(m);theme?.Dispose();}
}