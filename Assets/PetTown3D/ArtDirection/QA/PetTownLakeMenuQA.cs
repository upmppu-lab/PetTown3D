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

// 호수 낚시 메뉴 검사. 가짜 매장 + 비활성 가짜 게임(저장 카메라 없음)만 사용하며 실제 게임/저장을 실행하지 않습니다.
public sealed class PetTownLakeMenuQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Evidence="Assets/PetTown3D/ArtDirection/LakeFishing/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host,uiObject,camObject,shopObject,lakeObject,parkObject;PetTownGame game;PetTownProgressionState data;PetTownCatalogWindow window;Camera cam;RenderTexture rt;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    static string SaveHash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PlayerPrefs.GetString("PetTown3D_Save","")+"|"+PlayerPrefs.GetString("PetTown3D_PetCare_v1",""))));}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownLakeMenuQA","running");string before=SaveHash();bool failed=false;var run=Run();
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        try{Cleanup();}catch(Exception e){failed=true;trace.Add("Cleanup "+e);}
        bool same=before==SaveHash();checks["real_save_untouched"]=same;if(!same)failed=true;
        File.WriteAllText(Evidence+"menu-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"menu-trace.txt",trace);
        SessionState.SetString("PetTownLakeMenuQA",failed?"failed":"complete");enabled=false;
    }
    GameObject Lot(string id,int kind,Vector3 local)
    {
        var root=new GameObject("QA_LOT_"+id);root.transform.SetParent(fixture.transform,false);root.transform.localPosition=local;((IList)Get(game,"buildings")).Add(root);
        var b=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic),true);Set(b,"owner",root);Set(b,"center",root.transform.position);Set(b,"halfW",7.5f);Set(b,"halfD",7.5f);Set(b,"floorY",300.045f);((IList)Get(game,"buildingFloors")).Add(b);
        data.Community.Get(id,kind,500);root.AddComponent<PetTownCommunityView>().Initialize(game,id,kind,new Vector3(0,0,-7.5f));return root;
    }
    void Setup()
    {
        fixture=new GameObject("QA_LAKE_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.identity);
        host=new GameObject("QA_LAKE_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",5000);
        shopObject=new GameObject("QA_SHOP");shopObject.transform.SetParent(fixture.transform,false);shopObject.AddComponent<PetTownInteriorState>();Set(game,"shop",shopObject);((IList)Get(game,"buildings")).Add(shopObject);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",40f);Set(flat,"halfD",40f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        lakeObject=Lot("qa-lake",6,new Vector3(-12,0,0));parkObject=Lot("qa-park",5,new Vector3(12,0,0));
        camObject=new GameObject("QA_CAM");cam=camObject.AddComponent<Camera>();rt=new RenderTexture(1080,1920,24);cam.targetTexture=rt;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.55f,.7f,.85f);
        uiObject=new GameObject("QA_UI");var canvas=uiObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;
        var scaler=uiObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;uiObject.AddComponent<GraphicRaycaster>();
        window=uiObject.AddComponent<PetTownCatalogWindow>();window.Initialize(uiObject.transform);Set(game,"catalogWindow",window);
    }
    void AdoptCanvases()
    {
        foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if(!c.isRootCanvas||c.renderMode!=RenderMode.ScreenSpaceOverlay)continue;c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=1;
            var sc=c.GetComponent<CanvasScaler>();if(sc){sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1080,1920);sc.matchWidthOrHeight=.5f;}
        }
    }
    bool HasText(string value){foreach(var t in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))if(t.gameObject.activeInHierarchy&&t.text!=null&&t.text.Contains(value))return true;return false;}
    void Capture(string name)
    {
        AdoptCanvases();Canvas.ForceUpdateCanvases();cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Evidence+name+".png",tex.EncodeToPNG());RenderTexture.active=previous;Destroy(tex);
    }
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
    HashSet<string> BoardKeys()
    {
        var keys=new HashSet<string>();int original=(int)Get(game,"day");
        for(int d=original+10;d<original+22;d++){Set(game,"day",d);var ops=new PetTownShopOperationsState();Call(game,"EnsureDailyBoard",ops);if(ops.dailyQuests!=null)foreach(var b in ops.dailyQuests.batches)foreach(var qq in b.quests)keys.Add(qq.key);}
        Set(game,"day",original);return keys;
    }
    void Click(int key){window.Choose(key);window.ConfirmSelected();}
    IEnumerator Shot(string name){yield return null;Canvas.ForceUpdateCanvases();yield return null;Measure(name);Capture(name);}
    IEnumerator Run()
    {
        Setup();yield return null;yield return null;Assert("no_save_camera",Get(game,"cam")==null);
        var state=data.Community;var lot=state.lots.Find(x=>x.id=="qa-lake");Assert("lake_view_built",lakeObject.GetComponent<PetTownCommunityView>()!=null&&lot.kind==6);
        // 호수 관리 메뉴
        state.fish=5;lot.fishStock=6;Call(game,"OpenCommunityLot","qa-lake");yield return Shot("lake_menu");
        Assert("lake_menu_has_fish_rows",HasText("호수 물고기 6 / 12")&&HasText("물고기로 카페 재료 만들기"));
        int money0=(int)Get(game,"money");
        Click(41);yield return null;Assert("ingredients_blocked_without_cafe",state.fish==5&&data.Facilities.cafeIngredients==0);
        Click(40);yield return null;Assert("sell_all_fish_by_menu",state.fish==0&&(int)Get(game,"money")==money0+5*PetTownCommunityState.FishPrice);
        Click(40);yield return null;Assert("sell_with_no_fish_rejected",(int)Get(game,"money")==money0+5*PetTownCommunityState.FishPrice);
        // 카페 건물이 생기면 가능
        state.fish=4;var cafeObject=new GameObject("QA_CAFE");cafeObject.transform.SetParent(fixture.transform,false);var cafeRoom=cafeObject.AddComponent<PetTownInteriorState>();typeof(PetTownInteriorState).GetField("kind",F).SetValue(cafeRoom,3);((IList)Get(game,"buildings")).Add(cafeObject);
        int ing0=data.Facilities.cafeIngredients;Call(game,"OpenCommunityLot","qa-lake");yield return null;Click(41);yield return null;
        Assert("fish_to_ingredients_with_cafe",state.fish==2&&data.Facilities.cafeIngredients==ing0+2);
        Click(41);yield return null;Assert("second_conversion_uses_remaining_fish",state.fish==0&&data.Facilities.cafeIngredients==ing0+4);
        Click(41);yield return null;Assert("conversion_rejected_when_no_fish",state.fish==0&&data.Facilities.cafeIngredients==ing0+4);
        yield return Shot("lake_menu_after");
        // 공원에는 물고기 항목이 없음
        window.Hide();yield return null;Call(game,"OpenCommunityLot","qa-park");yield return Shot("park_menu");Assert("park_menu_has_no_fish_rows",!HasText("호수 물고기"));
        var boardKeys=BoardKeys();trace.Add("board keys (lake): "+string.Join(",",new List<string>(boardKeys).ToArray()));
        Assert("fish_quest_offered_with_lake",boardKeys.Contains("fish"));Assert("no_slot_or_chick_quest_here",!boardKeys.Contains("slot")&&!boardKeys.Contains("chick"));
        Cleanup();yield return null;
    }
    public void Cleanup()
    {
        if(uiObject)DestroyImmediate(uiObject);if(camObject)DestroyImmediate(camObject);if(rt){rt.Release();DestroyImmediate(rt);}if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);
        uiObject=camObject=host=fixture=shopObject=lakeObject=parkObject=null;rt=null;window=null;
    }
}
#endif
