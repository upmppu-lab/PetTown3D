using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Presentation adapter for the original hair/clothing/preview APIs.
public sealed class PetTownMobileCharacterWindow : IDisposable
{
    PetTownMobileWindow window;PetTownUITheme theme;RectTransform content;RawImage portrait;
    PetTownCharacterPreview preview;Action closed;int tab,precision;bool advanced;float width=-1;
    readonly List<GameObject> options=new List<GameObject>();readonly List<Button> tabs=new List<Button>();
    readonly string[] names={"헤어","얼굴","상의","하의","장신구"};
    public bool IsOpen=>window!=null&&window.IsOpen;
    public void Show(PetTownCharacterPreview source,Action close)
    {
        preview=source;closed=close;if(window==null||!window.Layer)Build();window.Show();Rebuild();
    }
    void Build()
    {
        theme=new PetTownUITheme();window=new PetTownMobileWindow("PetTownMobileCharacter",theme,Close);window.SetHeader("캐릭터 꾸미기",4,false);window.SetHeight(640);
        var scroll=PetTownMobileWindow.Scroll("CharacterScroll",window.Body,out content);PetTownMobileWindow.Fill((RectTransform)scroll.transform);
        for(int i=0;i<names.Length;i++){int index=i;var b=theme.Button("Category_"+i,content,names[i],()=>{tab=index;advanced=false;Rebuild();},Color.white);b.GetComponentInChildren<TextMeshProUGUI>().fontSize=15;tabs.Add(b);}
        var frame=theme.Panel("CharacterPreview",content,Color.white,"Border");var viewport=PetTownUITheme.Rect("PreviewViewport",frame.transform);PetTownUITheme.Place(viewport,Vector2.zero,Vector2.one,new Vector2(12,12),new Vector2(-12,-12));viewport.gameObject.AddComponent<RectMask2D>();portrait=PetTownUITheme.Rect("ActualCharacter",viewport).gameObject.AddComponent<RawImage>();portrait.raycastTarget=true;PetTownUITheme.Place(portrait.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,8),new Vector2(-8,-8));var fit=portrait.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=.8f;
        portrait.gameObject.AddComponent<PetTownMobilePreviewInput>().preview=preview;
        theme.Button("RotateLeft",content,"<",()=>preview.Rotate(25),Color.white);theme.Button("RotateRight",content,">",()=>preview.Rotate(-25),Color.white);theme.Button("ZoomIn",content,"+",()=>preview.Zoom(-.15f),Color.white);theme.Button("ZoomOut",content,"−",()=>preview.Zoom(.15f),Color.white);theme.Text("PreviewHint",content,"드래그로 회전",14,theme.Muted,TextAlignmentOptions.Center);
    }
    Button Option(string name,string label,float y,Action action)
    {
        var b=theme.Button(name,content,label,action,Color.white);PetTownMobileWindow.At((RectTransform)b.transform,0,y,window.Body.rect.width,44);options.Add(b.gameObject);return b;
    }
    void Rebuild()
    {
        foreach(var o in options){o.SetActive(false);UnityEngine.Object.Destroy(o);}options.Clear();
        window.Layout(true);float w=window.Body.rect.width;width=w;
        for(int i=0;i<tabs.Count;i++){PetTownMobileWindow.At((RectTransform)tabs[i].transform,i*w/5,0,w/5-4,44);tabs[i].GetComponent<Image>().sprite=theme.Surface(i==tab?"Button":"Light");tabs[i].GetComponentInChildren<TextMeshProUGUI>().color=i==tab?Color.white:theme.Ink;}
        PetTownMobileWindow.At((RectTransform)content.Find("CharacterPreview"),0,56,144,176);
        string[] control={"RotateLeft","RotateRight","ZoomIn","ZoomOut"};for(int i=0;i<4;i++)PetTownMobileWindow.At((RectTransform)content.Find(control[i]),156+i%2*((w-156)/2),64+i/2*52,(w-156)/2-6,44);
        PetTownMobileWindow.At((RectTransform)content.Find("PreviewHint"),154,174,w-154,44);
        float y=248;
        if(tab==0)
        {
            var hair=PetTownHairSwitcher.Instance;string[] styles={"원본 헤어","포니테일","단발 + 앞머리","트윈테일","롱헤어"};
            for(int i=0;i<styles.Length;i++){int n=i;Option("Hair_"+i,(hair!=null&&hair.Selected==i?"선택 · ":"")+styles[i],y,()=>{if(hair!=null){hair.Select(n);preview.RefreshHair();preview.RefreshColors();Rebuild();}});y+=52;}
            if(hair!=null&&hair.Selected>0){Option("Advanced","세부 조정 "+(advanced?"접기":"열기"),y,()=>{advanced=!advanced;Rebuild();});y+=52;if(advanced)y=Calibration(y,true,0);}
        }
        else if(tab==2||tab==3)
        {
            int part=tab==2?0:1;var cloth=PetTownClothingSwitcher.Instance;
            for(int i=0;i<2;i++){int n=i;Option("Clothing_"+i,(cloth!=null&&cloth.GetSelected(part)==i?"선택 · ":"")+(i==0?"원본":tab==2?"후드티":"데님 팬츠"),y,()=>{if(cloth!=null){cloth.Select(part,n);preview.RefreshHair();Rebuild();}});y+=52;}
            if(cloth!=null&&cloth.GetSelected(part)>0){Option("Advanced","세부 조정 "+(advanced?"접기":"열기"),y,()=>{advanced=!advanced;Rebuild();});y+=52;if(advanced)y=Calibration(y,false,part);}
        }
        else
        {
            var text=theme.Text("AvailableStyles",content,"추가 스타일을 준비하고 있어요.",17,theme.Muted,TextAlignmentOptions.Center);PetTownMobileWindow.At(text.rectTransform,0,y,w,70);options.Add(text.gameObject);y+=80;
        }
        content.sizeDelta=new Vector2(0,y+16);
    }
    float Calibration(float y,bool hair,int part)
    {
        var h=PetTownHairSwitcher.Instance;var c=PetTownClothingSwitcher.Instance;float w=window.Body.rect.width;
        Option("Precision",precision==0?"조절 간격: 기본":precision==1?"조절 간격: 미세":"조절 간격: 초미세",y,()=>{precision=(precision+1)%3;Rebuild();});y+=52;
        string[] labels={"좌우","높이","앞뒤","크기","회전 X","회전 Y","회전 Z"};
        for(int i=0;i<7;i++)
        {
            int axis=i;float value=hair?(axis==3?h.GetScale(h.Selected):axis>=4?h.GetRotation(h.Selected)[axis-4]:h.GetOffset(h.Selected)[axis]):(axis==3?c.GetSize(part):axis>=4?c.GetAngles(part)[axis-4]:c.GetOffset(part)[axis]);
            var label=theme.Text("Axis_"+i,content,labels[i]+"  "+value.ToString(precision==2?"F4":"F2"),15);PetTownMobileWindow.At(label.rectTransform,0,y,w-112,44);options.Add(label.gameObject);
            for(int sign=-1;sign<=1;sign+=2)
            {
                int direction=sign;var b=theme.Button("Adjust",content,sign<0?"−":"+",()=>{float step=axis>=4?(precision==0?5:precision==1 ? .1f : .01f):(precision==0 ? .1f:precision==1 ? .001f : .0001f);if(hair)h.Adjust(axis,direction*step);else c.Adjust(part,axis,direction*step);preview.RefreshHair();Rebuild();},Color.white);
                PetTownMobileWindow.At((RectTransform)b.transform,w-(sign<0?104:48),y,44,44);options.Add(b.gameObject);
            }
            y+=52;
        }
        Option("Reset","초기화",y,()=>{if(hair)h.ResetSelectedCalibration();else c.Reset(part);preview.RefreshHair();Rebuild();});y+=52;
        Option("Save","설정 저장",y,()=>{if(hair)h.SaveCurrentCalibration();else c.Save();});return y+52;
    }
    public void Refresh(){if(!IsOpen)return;window.Layout();portrait.texture=preview.Texture;if(Mathf.Abs(width-window.Body.rect.width)>.5f)Rebuild();}
    public void Close(){window?.Hide();closed?.Invoke();}
    public void Dispose(){window?.Dispose();theme?.Dispose();}
}

public sealed class PetTownMobilePreviewInput : MonoBehaviour,IDragHandler,IScrollHandler
{
    public PetTownCharacterPreview preview;
    public void OnDrag(PointerEventData data){var canvas=GetComponentInParent<Canvas>();if(preview)preview.Rotate(-data.delta.x/Mathf.Max(.01f,canvas.scaleFactor)*.7f);}
    public void OnScroll(PointerEventData data){if(preview)preview.Zoom(-data.scrollDelta.y*.12f);}
}
