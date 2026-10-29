using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PetTownPetPlayWindow : MonoBehaviour
{
    PetTownMobileWindow window;PetTownUITheme theme;RectTransform arena;Button target,finish;
    TextMeshProUGUI timer,help,result;Func<float,string> completed;Action closed;
    readonly System.Random random=new System.Random();float remaining;int hits;bool ended;
    public bool IsOpen=>window!=null&&window.IsOpen;
    public int Hits=>hits;
    public void Show(string petName,Func<float,string> onComplete,Action onClose)
    {
        completed=onComplete;closed=onClose;hits=0;remaining=12;ended=false;if(window==null)Build();window.SetHeader(petName+"와 놀기",9,false);window.Show();
        result.text="";help.text="나타나는 장난감을 터치해 주세요. 목표 10번!";target.gameObject.SetActive(true);finish.GetComponentInChildren<TextMeshProUGUI>().text="그만두기";Layout();MoveTarget();Refresh();
    }
    void Build()
    {
        theme=new PetTownUITheme();window=new PetTownMobileWindow("PetTownPlayGame",theme,Close);window.SetHeight(590);
        timer=theme.Text("Timer",window.Body,"",20,theme.Ink,TextAlignmentOptions.Center);help=theme.Text("Help",window.Body,"",15,theme.Muted,TextAlignmentOptions.Center);
        arena=theme.Panel("PlayArea",window.Body,Color.white,"Border").rectTransform;
        target=theme.Button("CatchToy",arena,"",Tap,Color.white);var icon=theme.Panel("Toy",target.transform,Color.white,"Icon_9");PetTownUITheme.Place(icon.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,8),new Vector2(-8,-8));
        result=theme.Text("Result",window.Body,"",17,theme.Ink,TextAlignmentOptions.TopLeft);finish=theme.Button("Finish",window.Body,"그만두기",Close,Color.white);
    }
    void Layout()
    {
        window.Layout();float w=window.Body.rect.width;PetTownMobileWindow.At(timer.rectTransform,0,0,w,30);PetTownMobileWindow.At(help.rectTransform,0,36,w,42);
        PetTownMobileWindow.At(arena,0,88,w,250);PetTownMobileWindow.At(result.rectTransform,4,348,w-8,70);PetTownUITheme.Place((RectTransform)finish.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,48));
    }
    void MoveTarget(){var r=(RectTransform)target.transform;PetTownMobileWindow.At(r,12+(float)random.NextDouble()*Mathf.Max(0,arena.rect.width-92),12+(float)random.NextDouble()*158,64,64);}
    public void Tap(){if(!IsOpen||ended)return;hits++;MoveTarget();Refresh();if(hits>=10)Complete();}
    void Refresh(){timer.text="남은 시간 "+Mathf.CeilToInt(remaining)+"초 · "+hits+" / 10";}
    void Complete(){if(ended)return;ended=true;target.gameObject.SetActive(false);result.text=completed?.Invoke(Mathf.Clamp01(hits/10f))??"함께 즐겁게 놀았어요!";finish.GetComponentInChildren<TextMeshProUGUI>().text="확인";}
    void Update(){if(!IsOpen)return;Layout();if(!ended&&Application.isFocused){remaining=Mathf.Max(0,remaining-Mathf.Min(Time.deltaTime,.1f));Refresh();if(remaining<=0)Complete();}if(Input.GetKeyDown(KeyCode.Escape))Close();}
    public void Close(){if(!IsOpen)return;window.Hide();var callback=closed;closed=null;callback?.Invoke();}
    void OnDisable(){Close();}void OnDestroy(){Close();window?.Dispose();theme?.Dispose();}
}
