#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

// 닭 번식/판매 시각·메뉴 검사. 가짜 매장 + 비활성 가짜 게임(저장 카메라 없음)만 사용하며 실제 게임/저장을 실행하지 않습니다.
public sealed class PetTownPoultryVisualQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Evidence="Assets/PetTown3D/ArtDirection/PoultryBreeding/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host,uiObject,camObject,lotObject;PetTownGame game;PetTownProgressionState data;PetTownCoopView view;PetTownCatalogWindow window;Camera cam;RenderTexture rt;readonly string id="qa-poultry-farm";
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    static string SaveHash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PlayerPrefs.GetString("PetTown3D_Save",""))));}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownPoultryVisualQA","running");string before=SaveHash();bool failed=false;var run=Run();
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        try{Cleanup();}catch(Exception e){failed=true;trace.Add("Cleanup "+e);}
        bool same=before==SaveHash();checks["real_save_untouched"]=same;if(!same)failed=true;
        File.WriteAllText(Evidence+"visual-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"visual-trace.txt",trace);
        SessionState.SetString("PetTownPoultryVisualQA",failed?"failed":"complete");enabled=false;
    }
    void Setup(int rotation)
    {
        fixture=new GameObject("QA_POULTRY_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.Euler(0,rotation,0));
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="QA_Floor";ground.transform.SetParent(fixture.transform,false);ground.transform.localPosition=new Vector3(0,-.1f,0);ground.transform.localScale=new Vector3(29,.2f,29);
        host=new GameObject("QA_POULTRY_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",5000);
        lotObject=new GameObject("QA_NATIVE_FARM");lotObject.transform.SetParent(fixture.transform,false);Set(game,"shop",lotObject);((IList)Get(game,"buildings")).Add(lotObject);
        var b=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic),true);Set(b,"owner",lotObject);Set(b,"center",lotObject.transform.position);Set(b,"halfW",9f);Set(b,"halfD",10f);Set(b,"floorY",300.045f);((IList)Get(game,"buildingFloors")).Add(b);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",14f);Set(flat,"halfD",14f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        data.Farm.Get(id,500);var p=data.Farm.Poultry;int gold=5000;p.Build(id,500,ref gold);var coop=p.Find(id);p.BuyBird(coop,500,ref gold);p.BuyBirdOfSex(coop,1,500,ref gold);foreach(var bird in coop.birds)bird.grown=2;p.feed=30;p.Feed(coop,500);p.Water(coop,500);
        lotObject.AddComponent<PetTownFarmView>().Initialize(game,id);view=lotObject.GetComponent<PetTownCoopView>();if(!view){view=lotObject.AddComponent<PetTownCoopView>();view.Initialize(game,id);}
    }
    void SetupUI()
    {
        camObject=new GameObject("QA_CAM");cam=camObject.AddComponent<Camera>();rt=new RenderTexture(1080,1920,24);cam.targetTexture=rt;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.55f,.7f,.85f);cam.nearClipPlane=.1f;cam.farClipPlane=100;
        var lightObject=new GameObject("QA_LIGHT");lightObject.transform.SetParent(camObject.transform,false);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;lightObject.transform.rotation=Quaternion.Euler(50,-30,0);
        uiObject=new GameObject("QA_UI");var canvas=uiObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;
        var scaler=uiObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;uiObject.AddComponent<GraphicRaycaster>();
        window=uiObject.AddComponent<PetTownCatalogWindow>();window.Initialize(uiObject.transform);Set(game,"catalogWindow",window);
    }
    void PositionCamera(){cam.transform.position=lotObject.transform.TransformPoint(PetTownCoopView.Center+new Vector3(0,3.2f,-4.2f));cam.transform.LookAt(lotObject.transform.TransformPoint(PetTownCoopView.Center+Vector3.up*.3f));}
    PetTownPoultryBirdVisual Find(string birdId){foreach(var v in view.GetComponentsInChildren<PetTownPoultryBirdVisual>())if(v.name=="FARM_CHICKEN_"+birdId)return v;return null;}
    static float Height(PetTownPoultryBirdVisual v)
    {
        Bounds b=default(Bounds);bool first=true;foreach(var s in v.GetComponentsInChildren<SkinnedMeshRenderer>()){if(first){b=s.bounds;first=false;}else b.Encapsulate(s.bounds);}return first?0:b.size.y;
    }
    HashSet<string> BoardKeys()
    {
        var keys=new HashSet<string>();int original=(int)Get(game,"day");
        for(int d=original+10;d<original+22;d++){Set(game,"day",d);var ops=new PetTownShopOperationsState();Call(game,"EnsureDailyBoard",ops);if(ops.dailyQuests!=null)foreach(var b in ops.dailyQuests.batches)foreach(var qq in b.quests)keys.Add(qq.key);}
        Set(game,"day",original);return keys;
    }
    void Capture(string name)
    {
        AdoptCanvases();Canvas.ForceUpdateCanvases();cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Evidence+name+".png",tex.EncodeToPNG());RenderTexture.active=previous;Destroy(tex);
    }
    void AdoptCanvases()
    {
        foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if(!c.isRootCanvas||c.renderMode!=RenderMode.ScreenSpaceOverlay)continue;c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=1;
            var sc=c.GetComponent<CanvasScaler>();if(sc){sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1080,1920);sc.matchWidthOrHeight=.5f;}
        }
    }
    // 장면 전체의 TextMeshPro/버튼을 측정합니다. 글자 넘침/작은 버튼은 기록하고, UI를 하나도 못 찾으면 실패로 처리합니다(빈 측정이 통과로 보이지 않게).
    void Measure(string name)
    {
        AdoptCanvases();Canvas.ForceUpdateCanvases();int over=0,cut=0,small=0,texts=0,buttons=0;var detail=new List<string>();
        foreach(var t in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            if(!t.gameObject.activeInHierarchy||!t.enabled||string.IsNullOrEmpty(t.text))continue;var group=t.GetComponentInParent<CanvasGroup>();if(group&&group.alpha<=.01f)continue;texts++;
            var r=t.rectTransform.rect;if(r.width<=1||r.height<=1)continue;bool wrap=t.textWrappingMode!=TMPro.TextWrappingModes.NoWrap;var pref=t.GetPreferredValues(t.text,wrap?r.width:0,0);
            bool exceeds=(!wrap&&pref.x>r.width+1.5f)||pref.y>r.height+1.5f;if(!exceeds||t.enableAutoSizing)continue;
            string line=t.name+" ["+(t.text.Length>22?t.text.Substring(0,22):t.text)+"] pref="+Mathf.RoundToInt(pref.x)+"x"+Mathf.RoundToInt(pref.y)+" rect="+Mathf.RoundToInt(r.width)+"x"+Mathf.RoundToInt(r.height);
            if(t.overflowMode==TMPro.TextOverflowModes.Overflow){over++;if(detail.Count<10)detail.Add("OVER["+line+"]");}else{cut++;if(detail.Count<10)detail.Add("CUT["+line+"]");}
        }
        foreach(var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if(!button.gameObject.activeInHierarchy)continue;buttons++;var r=((RectTransform)button.transform).rect;if(r.width<44||r.height<44){small++;if(detail.Count<14)detail.Add("SMALL["+button.name+"] "+Mathf.RoundToInt(r.width)+"x"+Mathf.RoundToInt(r.height));}
        }
        trace.Add("MEASURE "+name+": texts="+texts+" buttons="+buttons+" overflow="+over+" ellipsisCut="+cut+" smallButtons="+small);foreach(var d in detail)trace.Add("   "+d);
        Assert("measure_"+name+"_found_ui",texts>0&&buttons>0);checks["measure_"+name+"_overflow0"]=over==0;checks["measure_"+name+"_ellipsis0"]=cut==0;checks["measure_"+name+"_small0"]=small==0;
    }
    void Click(int key){window.Choose(key);window.ConfirmSelected();}
    IEnumerator Show(string menu,string shot)
    {
        yield return null;Canvas.ForceUpdateCanvases();yield return null;Measure(shot);Capture(shot);
    }
    IEnumerator Run()
    {
        foreach(int rotation in new[]{0,90})
        {
            string key="rotation_"+rotation+"_";Setup(rotation);SetupUI();yield return null;yield return null;PositionCamera();
            var s=data.Farm.Poultry;var coop=s.Find(id);Assert(key+"no_save_camera",Get(game,"cam")==null);
            Assert(key+"two_native_models",view.GetComponentsInChildren<PetTownPoultryBirdVisual>().Length==2);
            var hen=coop.birds[0];var rooster=coop.birds[1];var hv=Find(hen.id);var rv=Find(rooster.id);Assert(key+"visuals_found",hv&&rv);
            float hh=Height(hv),rh=Height(rv);trace.Add(key+"height hen="+hh+" rooster="+rh+" ratio="+(hh>0?rh/hh:0));
            Assert(key+"rooster_visibly_larger",hh>.05f&&rh>hh*1.05f&&rh<hh*1.25f);
            window.Hide();yield return null;Capture(key+"coop_adults");
            int money0=(int)Get(game,"money");
            // 메뉴: 닭장 관리
            Call(game,"OpenPoultry",id);yield return Show("main",key+"menu_main");
            Click(8);yield return Show("breeding",key+"menu_breeding");
            Click(200);yield return Show("partner",key+"menu_partner");
            Click(301);yield return null;Assert(key+"menu_started_breeding",s.Reserved(coop)==1&&hen.breedRestDays==3&&rooster.breedRestDays==3);
            yield return Show("breeding_clutch",key+"menu_breeding_clutch");
            Click(7);yield return null;
            Assert(key+"back_to_coop_menu",true);
            Click(100);yield return Show("bird",key+"menu_bird_detail");
            Click(0);yield return null;Assert(key+"locked_parent_sale_blocked",coop.birds.Count==2&&(int)Get(game,"money")==money0);
            Click(1);yield return null;
            Click(10);yield return null;Call(game,"RefreshPoultry",id);yield return null;yield return null;
            Assert(key+"menu_bought_rooster_chick",coop.birds.Count==3&&coop.birds[2].sex==1&&!coop.birds[2].Adult&&(int)Get(game,"money")==money0-80);
            Assert(key+"three_models_after_buy",view.GetComponentsInChildren<PetTownPoultryBirdVisual>().Length==3);
            Click(102);yield return null;Click(0);yield return null;Call(game,"RefreshPoultry",id);yield return null;yield return null;
            Assert(key+"menu_sold_chick_exact",coop.birds.Count==2&&(int)Get(game,"money")==money0-80+PetTownPoultryState.SellQuote(new PetTownChickenRecord()));
            Assert(key+"two_models_after_sale",view.GetComponentsInChildren<PetTownPoultryBirdVisual>().Length==2);
            window.Hide();yield return null;
            // 부화
            Assert(key+"no_chick_quest_before_hatching_soon",!BoardKeys().Contains("chick"));
            s.Feed(coop,500);s.Water(coop,500);s.Clean(coop);s.AdvanceDay(501,new[]{id});Set(game,"day",501);
            Assert(key+"chick_quest_offered_when_hatching_soon",BoardKeys().Contains("chick"));
            s.Feed(coop,501);s.Water(coop,501);s.Clean(coop);Set(game,"day",502);Call(game,"FarmNewDay");
            Assert(key+"chick_activity_recorded_by_game_hook",coop.hatched==1&&data.Operations(502).Activity("chick")==1);
            Set(game,"day",502);Call(game,"RefreshPoultry",id);yield return null;yield return null;
            Assert(key+"hatched_three_models",coop.birds.Count==3&&view.GetComponentsInChildren<PetTownPoultryBirdVisual>().Length==3);
            var chick=coop.birds.Find(x=>x.parentA==hen.id&&x.parentB==rooster.id);Assert(key+"chick_parentage",chick!=null&&chick.generation==1);var cv=Find(chick.id);Assert(key+"chick_model",cv);
            Assert(key+"chick_small",Mathf.Approximately(cv.CurrentScale,.45f)&&Height(cv)<hh*.75f);
            window.Hide();Capture(key+"coop_chick");
            chick.grown=2;cv.ApplyGrowth(10);Assert(key+"chick_grows_to_adult_scale",Mathf.Approximately(cv.CurrentScale,1));
            Cleanup();yield return null;
        }
    }
    public void Cleanup()
    {
        if(uiObject)DestroyImmediate(uiObject);if(camObject)DestroyImmediate(camObject);if(rt){rt.Release();DestroyImmediate(rt);}if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);
        uiObject=camObject=host=fixture=lotObject=null;rt=null;view=null;window=null;
    }
}
#endif
