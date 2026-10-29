using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PetTownExpansionWindow : MonoBehaviour
{
    PetTownUITheme theme;PetTownMobileWindow window;RectTransform panel,content;float layoutWidth=-1;TextMeshProUGUI status;Button confirm;
    GameObject preview;Material previewMaterial;PetTownInteriorState owner;
    Func<PetTownExpansionPlan,PetTownExpansionCheck> validate;Action<PetTownExpansionPlan> commit;Action closed;
    Rect shownArea;
    PetTownExpansionSettings settings;PetTownExpansionDirection direction;float nextRefresh;
    TextMeshProUGUI selection,dimensions,cost;readonly Button[] directionButtons=new Button[4];
    public bool IsOpen=>panel!=null&&panel.gameObject.activeSelf;
    public void Show(Transform parent,PetTownInteriorState building,PetTownExpansionSettings config,Func<PetTownExpansionPlan,PetTownExpansionCheck> check,Action<PetTownExpansionPlan> apply,Action onClose)
    {
        Close();owner=building;settings=config;validate=check;commit=apply;closed=onClose;theme=new PetTownUITheme();
        window=new PetTownMobileWindow("PetTownMobileExpansion",theme,Close);panel=window.Panel;window.SetHeader("증축 방향 선택",3,false);window.SetHeight(610);
        var scroll=PetTownMobileWindow.Scroll("ExpansionScroll",window.Body,out content);PetTownUITheme.Place((RectTransform)scroll.transform,Vector2.zero,Vector2.one,new Vector2(0,66),Vector2.zero);
        var illustration=theme.Panel("ShopArtwork",content,Color.white,"Icon_3");
        string[] names={"← 왼쪽","오른쪽 →","↓ 앞쪽","↑ 뒤쪽"};
        for(int i=0;i<4;i++){int n=i;directionButtons[i]=theme.Button("Direction_"+i,content,names[i],()=>Select((PetTownExpansionDirection)n),Color.white);directionButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize=16;}
        selection=theme.Text("SelectedDirection",content,"",21);dimensions=theme.Text("Size",content,"",17,theme.Muted);cost=theme.Text("Cost",content,"",20);status=theme.Text("Availability",content,"",17,theme.Ink,TextAlignmentOptions.TopLeft);
        confirm=theme.Button("Confirm",window.Body,"증축하기",()=>{var plan=PetTownExpansionPlan.Create(owner,direction,settings);if(validate(plan).CanCommit)commit(plan);});PetTownUITheme.Place((RectTransform)confirm.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,52));
        window.Show();LayoutControls();Select(PetTownExpansionDirection.Right);
    }
    void LayoutControls()
    {
        window.Layout();float w=window.Body.rect.width;if(Mathf.Abs(w-layoutWidth)<.5f)return;layoutWidth=w;
        PetTownMobileWindow.At((RectTransform)content.Find("ShopArtwork"),w*.5f-38,54,76,76);
        PetTownMobileWindow.At((RectTransform)directionButtons[0].transform,0,66,90,44);PetTownMobileWindow.At((RectTransform)directionButtons[1].transform,w-90,66,90,44);
        PetTownMobileWindow.At((RectTransform)directionButtons[2].transform,w*.5f-54,140,108,44);PetTownMobileWindow.At((RectTransform)directionButtons[3].transform,w*.5f-54,0,108,44);
        PetTownMobileWindow.At(selection.rectTransform,6,202,w-12,32);PetTownMobileWindow.At(dimensions.rectTransform,6,246,w-12,54);PetTownMobileWindow.At(cost.rectTransform,6,312,w-12,32);PetTownMobileWindow.At(status.rectTransform,6,356,w-12,96);content.sizeDelta=new Vector2(0,464);
    }
    public void Select(PetTownExpansionDirection value){direction=value;nextRefresh=0;Refresh();}
    void Update(){if(!IsOpen)return;if(Input.GetKeyDown(KeyCode.Escape)){Close();return;}LayoutControls();if(Time.unscaledTime>=nextRefresh)Refresh();}
    void Refresh()
    {
        if(owner==null){Close();return;}nextRefresh=Time.unscaledTime+.35f;
        var plan=PetTownExpansionPlan.Create(owner,direction,settings);var result=validate(plan);
        string[] directions={"왼쪽","오른쪽","앞쪽","뒤쪽"};selection.text="선택한 방향: "+directions[(int)direction];dimensions.text="확장 크기\n"+plan.Before.width+" × "+plan.Before.height+"m → "+plan.After.width+" × "+plan.After.height+"m";cost.text="예상 비용  "+plan.Cost.ToString("N0")+" 골드";status.text=result.Reason;status.color=result.CanCommit?new Color(.10f,.43f,.27f):result.Status==PetTownExpansionStatus.NeedsClearing?new Color(.58f,.35f,.08f):new Color(.76f,.21f,.14f);
        for(int i=0;i<4;i++){bool active=i==(int)direction;if(theme.Style)directionButtons[i].GetComponent<Image>().sprite=theme.Surface(active?"Button":"Light");directionButtons[i].GetComponentInChildren<TextMeshProUGUI>().color=active?Color.white:theme.Ink;}
        confirm.interactable=result.CanCommit;
        Color color=result.Status==PetTownExpansionStatus.Ready?new Color(.15f,.85f,.35f,.38f):result.Status==PetTownExpansionStatus.NeedsClearing?new Color(1,.72f,.1f,.42f):new Color(1,.15f,.1f,.4f);
        if(preview==null){preview=new GameObject("PLACEMENT_PREVIEW_Expansion");preview.transform.SetParent(owner.transform,false);var modifier=preview.AddComponent<Unity.AI.Navigation.NavMeshModifier>();modifier.ignoreFromBuild=true;previewMaterial=new Material(Resources.Load<Shader>("Materials/PetTownPlacementPreview"));}
        previewMaterial.SetColor("_BaseColor",color);
        if(shownArea==plan.Added&&preview.transform.childCount>0)return;shownArea=plan.Added;
        while(preview.transform.childCount>0){var child=preview.transform.GetChild(0);child.SetParent(null);Destroy(child.gameObject);}
        Cube("Added_Floor",new Vector3(plan.Added.center.x,owner.FloorOffset+.06f,plan.Added.center.y),new Vector3(plan.Added.width,.08f,plan.Added.height));
        if(direction==PetTownExpansionDirection.Left||direction==PetTownExpansionDirection.Right)Cube("New_Wall",new Vector3(direction==PetTownExpansionDirection.Left?plan.After.xMin:plan.After.xMax,1.4f,plan.Added.center.y),new Vector3(.12f,direction==PetTownExpansionDirection.Left?2.8f:.12f,plan.Added.height));
        else Cube("New_Wall",new Vector3(plan.Added.center.x,1.4f,direction==PetTownExpansionDirection.Front?plan.After.yMin:plan.After.yMax),new Vector3(plan.Added.width,direction==PetTownExpansionDirection.Back?2.8f:.12f,.12f));
    }
    void Cube(string name,Vector3 point,Vector3 size){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(preview.transform,false);g.transform.localPosition=point;g.transform.localScale=size;var c=g.GetComponent<Collider>();c.enabled=false;Destroy(c);g.GetComponent<Renderer>().sharedMaterial=previewMaterial;}
    public void Close(){window?.Dispose();window=null;panel=null;layoutWidth=-1;if(preview!=null){preview.SetActive(false);Destroy(preview);}if(previewMaterial!=null)Destroy(previewMaterial);theme?.Dispose();theme=null;var callback=closed;closed=null;callback?.Invoke();}
    void OnDisable(){Close();}
    void OnDestroy(){Close();}
}
