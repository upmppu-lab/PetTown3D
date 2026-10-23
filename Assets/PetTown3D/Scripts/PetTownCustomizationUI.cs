using UnityEngine;

// Additive UI only. No edits to the player mesh, animator, movement, or game scripts.
public sealed class PetTownCustomizationUI : MonoBehaviour
{
    public static bool IsWindowOpen { get; private set; }
    public static bool IsScreenPointOverUI(Vector2 screenPoint)
    {
        if (IsWindowOpen) return true;
        if (Screen.width >= Screen.height * 1.15f)
        {
            float scale=Mathf.Clamp(Mathf.Min(Screen.width/1000f,Screen.height/600f),.65f,1.7f);
            float x=screenPoint.x/scale, y=(Screen.height-screenPoint.y)/scale;
            float w=Screen.width/scale,h=Screen.height/scale;
            if (x<480f && y<160f) return true;
            if (x<265f && y>h-75f) return true;
            if (x>w-330f && y<375f) return true;
            if (x>w-350f && y>155f && y<h-75f) return true;
            if (y>h-65f && x<800f) return true;
        }

        float screenW=Screen.width, screenH=Screen.height;
        // Legacy IMGUI HUD: top status bars, right utility stack, bottom navigation.
        float uiScale=Mathf.Min(screenW/390f,screenH/780f);
        float sx=screenPoint.x/uiScale, sy=(screenH-screenPoint.y)/uiScale;
        if (sy < 165f && sx < 390f) return true;
        if (sx > 300f && sy < 500f) return true;
        if (sy > 680f) return true;
        // STYLE launcher native screen rectangle (same calculation as OnGUI).
        float size=Mathf.Clamp(screenW*.115f,42f,66f);
        float launcherY=Mathf.Min(438f*uiScale,screenH-size-32f);
        return screenPoint.x >= screenW-size-10f && screenPoint.x <= screenW-10f
            && screenH-screenPoint.y >= launcherY && screenH-screenPoint.y <= launcherY+size;
    }
    void Update() { IsWindowOpen=open; if (open && preview==null) preview=GetComponent<PetTownCharacterPreview>() ?? gameObject.AddComponent<PetTownCharacterPreview>(); if (preview!=null) preview.SetVisible(open); }
    void OnDisable() { IsWindowOpen=false; }
    PetTownCharacterPreview preview;
    bool open;
    bool previewDragging;
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
        uiFont=Resources.Load<Font>("Fonts/NanumSquareB");
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
        if(Btn(new Rect(919,70,53,45),"X")) { open=false; previewDragging=false; IsWindowOpen=false; }
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
        else if (ev.type == EventType.ScrollWheel && previewArea.Contains(ev.mousePosition))
        { if(preview!=null) preview.Zoom(ev.delta.y*.12f); ev.Use(); }
        GUI.Label(new Rect(330,567,330,24),"드래그: 회전   |   휠: 확대·축소",small);
        if (preview.Texture == null) GUI.Label(new Rect(334,267,325,112),"3D 미리보기를 준비 중입니다.",body);
        
        Panel(new Rect(699,145,279,470));
        GUI.Label(new Rect(711,158,252,45),tabs[tab]+" 스타일",body);
        int part=tab==0?0:tab==2?1:tab==3?2:-1;
        var appearance=PetTownCharacterAppearance.Instance;
        if(part>=0)
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
