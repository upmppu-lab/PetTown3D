using UnityEngine;

// Additive UI only. No edits to the player mesh, animator, movement, or game scripts.
public sealed class PetTownCustomizationUI : MonoBehaviour
{
    static bool characterWindowOpen;
    public static bool IsCharacterWindowOpen=>characterWindowOpen;
    public static bool IsWindowOpen { get=>characterWindowOpen||PetTownCatalogWindow.IsTextEntryFocused||PetTownPetCareWindow.TextEntryFocused; private set=>characterWindowOpen=value; }
    public void OpenFromHud(){open=true;IsWindowOpen=true;}
    public static bool IsScreenPointOverUI(Vector2 screenPoint)
    {
        if (IsWindowOpen) return true;
        if(PetTownCozyLibrary.Current!=null)return UnityEngine.EventSystems.EventSystem.current!=null&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        float screenW=Screen.width, screenH=Screen.height;
        bool landscape = screenW >= screenH * 1.15f;

        if (landscape)
        {
            // 데스크톱/와이드 화면: 실제로 그려지는 패널 크기에 맞춘 좁은 범위만 UI로 취급한다.
            float scale=Mathf.Clamp(Mathf.Min(screenW/1000f,screenH/600f),.65f,1.7f);
            float x=screenPoint.x/scale, y=(screenH-screenPoint.y)/scale;
            float w=screenW/scale,h=screenH/scale;
            if (x<480f && y<160f) return true;
            if (x<265f && y>h-75f) return true;
            if (x>w-330f && y<375f) return true;
            if (x>w-350f && y>155f && y<h-75f) return true;
            if (y>h-65f && x<800f) return true;
        }
        else
        {
            // 레거시(세로형/모바일) HUD: top status bars, right utility stack, bottom navigation.
            // 이 블록은 390x780 세로 캔버스 기준으로 만들어져서, 가로(와이드) 화면에서는 절대 적용하면 안 된다.
            // (예전엔 항상 같이 적용돼서 가로 화면 위쪽 절반이 통째로 "UI"로 막혀 클릭 이동이 안 됐음.)
            float uiScale=Mathf.Min(screenW/390f,screenH/780f);
            float sx=screenPoint.x/uiScale, sy=(screenH-screenPoint.y)/uiScale;
            if (sy < 165f && sx < 390f) return true;
            if (sx > 300f && sy < 500f) return true;
            if (sy > 680f) return true;
        }

        // STYLE launcher native screen rectangle (same calculation as OnGUI). 두 레이아웃 공통.
        float launcherUiScale=Mathf.Min(screenW/390f,screenH/780f);
        float size=Mathf.Clamp(screenW*.115f,42f,66f);
        float launcherY=Mathf.Min(438f*launcherUiScale,screenH-size-32f);
        return screenPoint.x >= screenW-size-10f && screenPoint.x <= screenW-10f
            && screenH-screenPoint.y >= launcherY && screenH-screenPoint.y <= launcherY+size;
    }
    void Update()
    {
        IsWindowOpen=open;if(open&&preview==null)preview=GetComponent<PetTownCharacterPreview>()??gameObject.AddComponent<PetTownCharacterPreview>();if(preview!=null)preview.SetVisible(open);
        if(open&&PetTownCozyLibrary.Current!=null){if(mobile==null)mobile=new PetTownMobileCharacterWindow();if(!mobile.IsOpen)mobile.Show(preview,()=>{open=false;IsWindowOpen=false;});mobile.Refresh();if(Input.GetKeyDown(KeyCode.Escape))mobile.Close();}
    }
    void OnDestroy(){mobile?.Dispose();}
    void OnDisable() { mobile?.Close();if(preview!=null)preview.SetVisible(false);IsWindowOpen=false; }
    PetTownMobileCharacterWindow mobile;
    PetTownCharacterPreview preview;
    bool open;
    bool previewDragging;
    bool previewPanning;
    int hairAdjustmentMode; // 0 = fast, 1 = fine, 2 = ultra-fine
    Vector2 lastPreviewMouse;
    int tab;
    readonly int[] selected = new int[5];
    readonly string[] tabs = {"헤어", "얼굴", "상의", "하의", "액세서리"};
    readonly string[] icons = {"✦", "☺", "▣", "▤", "★"};
    Font uiFont;
    GUIStyle heading, body, small, button;
    bool ready;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<PetTownCustomizationUI>() != null) return;
        var go = new GameObject("PetTown_Customization_UI");
        go.AddComponent<PetTownCustomizationUI>();
        DontDestroyOnLoad(go);
    }
    void Init()
    {
        if(ready)return;
        uiFont=Resources.Load<Font>("Fonts/Jua-Regular") ?? Resources.Load<Font>("Fonts/NanumSquareB");
        heading=new GUIStyle(GUI.skin.label){font=uiFont,fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        heading.normal.textColor=Color.white;
        body=new GUIStyle(heading){fontSize=18};
        small=new GUIStyle(heading){fontSize=14,wordWrap=true};
        button=new GUIStyle(heading){fontSize=17};
        ready=true;
    }
    void Panel(Rect r,float alpha=.30f)
    {
        Color old=GUI.color;
        GUI.color=new Color(0,0,0,alpha);GUI.DrawTexture(r,Texture2D.whiteTexture);
        GUI.color=Color.black;
        GUI.DrawTexture(new Rect(r.x,r.y,r.width,3),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,r.yMax-3,r.width,3),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,r.y,3,r.height),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax-3,r.y,3,r.height),Texture2D.whiteTexture);
        GUI.color=old;
    }
    bool Btn(Rect r,string text)
    {
        Panel(r);
        GUI.Label(r,text,button);
        return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }
    void OnGUI()
    {
        if(PetTownCozyLibrary.Current!=null)return;
        Init();
        GUI.depth = -100; // Render launcher above gameplay IMGUI controls.
        float s = Mathf.Min(Screen.width/1100f,Screen.height/690f);
        s = Mathf.Clamp(s,.55f,1.5f);
        Matrix4x4 old=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1100*s)/2,(Screen.height-690*s)/2,0),Quaternion.identity,new Vector3(s,s,1));
        if (!open)
        {
            // Launcher uses native screen coordinates; visible in portrait and landscape.
            GUI.matrix=old;
            float launcherSize=Mathf.Clamp(Screen.width*0.115f,42f,66f);
            // Place STYLE below the right utility stack (SHOP/ROOF/SAVE/MORE), not behind ROOF.
            float hudScale=Mathf.Min(Screen.width/390f,Screen.height/780f);
            float hudHeight=Screen.height/hudScale;
            float preferredY=(430f+8f)*hudScale;
            Rect launcher=new Rect(Screen.width-launcherSize-10f,Mathf.Min(preferredY,Screen.height-launcherSize-32f),launcherSize,launcherSize);
            Panel(launcher);
            GUIStyle launcherLabel=new GUIStyle(GUI.skin.label){font=uiFont,alignment=TextAnchor.MiddleCenter,fontSize=14,fontStyle=FontStyle.Bold};
            launcherLabel.normal.textColor=Color.white;
            GUI.Label(launcher,"STYLE",launcherLabel);
            if(GUI.Button(launcher,GUIContent.none,GUIStyle.none)) { open=true; IsWindowOpen=true; }
            return;
        }
        Panel(new Rect(95,42,910,603),.85f);
        GUI.Label(new Rect(320,66,450,50),"캐릭터 꾸미기",heading);
        if(Btn(new Rect(919,70,53,45),"X")) { open=false; previewDragging=false; previewPanning=false; IsWindowOpen=false; }
        Panel(new Rect(117,145,172,470));
        for(int i=0;i<tabs.Length;i++)
        {
            int y=164+i*85;
            if(Btn(new Rect(130,y,145,69),tabs[i]))tab=i;
        }
        Panel(new Rect(305,145,380,470));
        GUI.Label(new Rect(320,166,350,45),"캐릭터 미리보기",body);
        if (preview==null) preview=GetComponent<PetTownCharacterPreview>() ?? gameObject.AddComponent<PetTownCharacterPreview>();
        Rect previewArea = new Rect(320,205,350,365);
        if (preview.Texture!=null) GUI.DrawTexture(previewArea,preview.Texture,ScaleMode.ScaleToFit);
        Event ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && previewArea.Contains(ev.mousePosition))
        { previewDragging = true; lastPreviewMouse = ev.mousePosition; ev.Use(); }
        else if (ev.type == EventType.MouseDrag && previewDragging && ev.button == 0)
        { if(preview!=null) preview.Rotate(-(ev.mousePosition.x-lastPreviewMouse.x)*.7f); lastPreviewMouse=ev.mousePosition; ev.Use(); }
        else if (ev.type == EventType.MouseUp && ev.button == 0) previewDragging = false;
        else if (ev.type == EventType.MouseDown && ev.button == 2 && previewArea.Contains(ev.mousePosition))
        { previewPanning = true; lastPreviewMouse = ev.mousePosition; ev.Use(); }
        else if (ev.type == EventType.MouseDrag && previewPanning && ev.button == 2)
        { if (preview != null) preview.PanVertical(lastPreviewMouse.y - ev.mousePosition.y); lastPreviewMouse = ev.mousePosition; ev.Use(); }
        else if (ev.type == EventType.MouseUp && ev.button == 2) { previewPanning = false; ev.Use(); }
        else if (ev.type == EventType.ScrollWheel && previewArea.Contains(ev.mousePosition))
        { if(preview!=null) preview.Zoom(ev.delta.y*.12f); ev.Use(); }
        GUI.Label(new Rect(330,567,330,24),"좌클릭: 회전  |  휠: 확대  |  휠버튼 드래그: 높이",small);
        if (preview.Texture == null) GUI.Label(new Rect(334,267,325,112),"3D 미리보기를 준비 중입니다.",body);
        
        Panel(new Rect(699,145,279,470));
        GUI.Label(new Rect(711,158,252,45),tabs[tab]+" 스타일",body);
        int part=tab==0?0:tab==2?1:tab==3?2:-1;
        var appearance=PetTownCharacterAppearance.Instance;
        if(tab==0)
        {
            var hair=PetTownHairSwitcher.Instance;
            string[] names={"01  원본 헤어", "02  포니테일", "03  단발 + 앞머리", "04  트윈테일", "05  롱헤어"};
            for(int i=0;i<names.Length;i++)
            {
                string label=(hair!=null && hair.Selected==i ? "✓  " : "")+names[i];
                if(Btn(new Rect(713,204+i*39,250,35),label) && hair!=null)
                {
                    hair.Select(i);
                    if (preview != null) preview.RefreshHair();
                    if(preview!=null) preview.RefreshColors();
                }
            }
            if(hair!=null && hair.Selected>0)
            {
                // Default large step, optional precision mode; no repeated 1000 clicks.
                if(Btn(new Rect(713,405,250,25),hairAdjustmentMode == 0 ? "조절 간격: 기본 (클릭하여 변경)" : hairAdjustmentMode == 1 ? "조절 간격: 미세 0.001" : "조절 간격: 초미세 0.0001"))
                    hairAdjustmentMode = (hairAdjustmentMode + 1) % 3;
                string[] axisNames={"좌우 X", "높이 Y", "앞뒤 Z", "크기", "회전 X", "회전 Y", "회전 Z"};
                for(int axis=0;axis<7;axis++)
                {
                    int yy=435+axis*20;
                    GUI.Label(new Rect(707,yy,80,22),axisNames[axis],small);
                    float step = axis>=4 ? (hairAdjustmentMode==0 ? 5f : hairAdjustmentMode==1 ? .1f : .01f)
                        : (hairAdjustmentMode==0 ? .1f : hairAdjustmentMode==1 ? .001f : .0001f);
                    if(Btn(new Rect(792,yy,35,21),"−")) { hair.Adjust(axis,-step); if(preview!=null)preview.RefreshHair(); }
                    if(Btn(new Rect(837,yy,35,21),"+")) { hair.Adjust(axis,step); if(preview!=null)preview.RefreshHair(); }
                    float v=axis==3?hair.GetScale(hair.Selected):axis>=4?hair.GetRotation(hair.Selected)[axis-4]:hair.GetOffset(hair.Selected)[axis];
                    GUI.Label(new Rect(875,yy,90,22),v.ToString(axis>=4?(hairAdjustmentMode==2?"F2":"F1"):(hairAdjustmentMode==2?"F4":"F3")),small);
                }
                if(Btn(new Rect(840,595,123,22),"설정 저장")) hair.SaveCurrentCalibration();
                if(Btn(new Rect(713,595,120,22),"초기화"))
                {
                    hair.ResetSelectedCalibration();
                    if(preview!=null)preview.RefreshHair();
                }
            }
            else GUI.Label(new Rect(708,552,260,49),hair==null?"헤어 시스템 준비 중":hair.Status,small);
        }
        else if(tab==2 || tab==3)
        {
            int p=tab==2?0:1;
            var cloth=PetTownClothingSwitcher.Instance;
            string name=tab==2?"02  후드티":"02  데님 팬츠";
            for(int i=0;i<2;i++)
            {
                string label=(cloth!=null && cloth.GetSelected(p)==i?"✓  ":"")+(i==0?"01  원본":name);
                if(Btn(new Rect(713,205+i*48,250,42),label)&&cloth!=null)
                { cloth.Select(p,i);if(preview!=null)preview.RefreshHair(); }
            }
            if(cloth!=null && cloth.GetSelected(p)>0)
            {
                if(Btn(new Rect(713,309,250,28),hairAdjustmentMode==0?"조절 간격: 기본":hairAdjustmentMode==1?"조절 간격: 미세 0.001":"조절 간격: 초미세 0.0001")) hairAdjustmentMode=(hairAdjustmentMode+1)%3;
                string[] labels={"좌우 X","높이 Y","앞뒤 Z","크기","회전 X","회전 Y","회전 Z"};
                for(int axis=0;axis<7;axis++)
                {
                    int yy=344+axis*29;
                    GUI.Label(new Rect(707,yy,80,25),labels[axis],small);
                    float step=axis>=4?(hairAdjustmentMode==0?5f:hairAdjustmentMode==1?.1f:.01f):(hairAdjustmentMode==0?.1f:hairAdjustmentMode==1?.001f:.0001f);
                    if(Btn(new Rect(792,yy,35,25),"−")){cloth.Adjust(p,axis,-step);if(preview!=null)preview.RefreshHair();}
                    if(Btn(new Rect(837,yy,35,25),"+")){cloth.Adjust(p,axis,step);if(preview!=null)preview.RefreshHair();}
                    float v=axis==3?cloth.GetSize(p):axis>=4?cloth.GetAngles(p)[axis-4]:cloth.GetOffset(p)[axis];
                    GUI.Label(new Rect(875,yy,90,25),v.ToString(axis>=4?"F2":hairAdjustmentMode==2?"F4":"F3"),small);
                }
                if(Btn(new Rect(713,556,250,25),"초기화")){cloth.Reset(p);if(preview!=null)preview.RefreshHair();}
                if(Btn(new Rect(713,586,250,25),"설정 저장"))cloth.Save();
            }
            else GUI.Label(new Rect(713,380,250,160),"의상 교체 테스트: 독립 GLB는 팔/다리 관절에 맞춰 변형되지 않습니다.",small);
        }
        else if(part>=0)

        {
            for(int i=0;i<6;i++)
            {
                int x=715+(i%2)*125,y=220+(i/2)*103;
                Rect cell=new Rect(x,y,114,88);
                if(appearance!=null && i>0)
                {
                    Color oldColor=GUI.color;
                    Color swatch=appearance.GetSwatch(part,i);
                    GUI.color=new Color(swatch.r,swatch.g,swatch.b,.85f);
                    GUI.DrawTexture(new Rect(x+8,y+8,98,20),Texture2D.whiteTexture);
                    GUI.color=oldColor;
                }
                string caption=(i==0?"원본": "색상 "+i);
                if(appearance!=null && appearance.GetSelected(part)==i)caption="✓ "+caption;
                if(Btn(cell,caption) && appearance!=null) { appearance.Apply(part,i); if(preview!=null)preview.RefreshColors(); }
            }
            GUI.Label(new Rect(702,552,273,49),appearance!=null&&appearance.IsReady?"색상 변경 적용 중":"캐릭터 모델을 찾는 중",small);
        }
        else
        {
            GUI.Label(new Rect(711,236,250,220),"얼굴·액세서리 교체는\n별도의 3D 파츠가 필요합니다.\n원본 캐릭터는 유지됩니다.",small);
        }
        GUI.matrix=old;
    }
}
