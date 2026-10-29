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

// 펫샵 전시 자리 메뉴/이동 목표 검사. 가짜 매장 + 비활성 가짜 게임(저장 카메라 없음)만 사용하며 실제 게임/저장을 실행하지 않습니다.
public sealed class PetTownShopSlotsMenuQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Evidence="Assets/PetTown3D/ArtDirection/ShopSlots/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host,uiObject,camObject,shopObject;PetTownGame game;PetTownProgressionState data;PetTownCatalogWindow window;Camera cam;RenderTexture rt;PetTownInteriorState room;
    PetTownFurniturePlacement cageA,cageB,cushion;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    static string SaveHash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PlayerPrefs.GetString("PetTown3D_Save","")+"|"+PlayerPrefs.GetString("PetTown3D_PetCare_v1",""))));}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownShopSlotsMenuQA","running");string before=SaveHash();bool failed=false;var run=Run();
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        try{Cleanup();}catch(Exception e){failed=true;trace.Add("Cleanup "+e);}
        bool same=before==SaveHash();checks["real_save_and_petcare_untouched"]=same;if(!same)failed=true;
        File.WriteAllText(Evidence+"menu-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"menu-trace.txt",trace);
        SessionState.SetString("PetTownShopSlotsMenuQA",failed?"failed":"complete");enabled=false;
    }
    PetTownFurniturePlacement Furn(string id,int kind,Vector3 local)
    {
        var go=new GameObject("QA_FURN_"+id);go.transform.SetParent(shopObject.transform,false);go.transform.localPosition=local;var p=go.AddComponent<PetTownFurniturePlacement>();p.Initialize(room,kind,true,new Vector2(.9f,.7f),.7f,id);return p;
    }
    PetTownPetCareRecord Pet(string id)=>data.care.pets.Find(x=>x.id==id);
    void Setup()
    {
        fixture=new GameObject("QA_SLOTS_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.identity);
        host=new GameObject("QA_SLOTS_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",5000);
        shopObject=new GameObject("QA_SHOP");shopObject.transform.SetParent(fixture.transform,false);room=shopObject.AddComponent<PetTownInteriorState>();Set(game,"shop",shopObject);((IList)Get(game,"buildings")).Add(shopObject);
        cageA=Furn("a-cage",2,new Vector3(-3,0,2));cageB=Furn("b-cage",2,new Vector3(-3,0,-2));cushion=Furn("c-cushion",4,new Vector3(3,0,2));Furn("d-birdcage",6,new Vector3(3,0,-2));Furn("e-toys",1,new Vector3(0,0,4));
        var pets=data.care.pets;string[] ids={"rabbit","ferret","rabbit2","dog","cat"};int[] species={2,3,2,0,1};string[] names={"토끼 모찌","페럿 콩이","토끼 보리","강아지 초코","고양이 나비"};
        for(int i=0;i<ids.Length;i++)pets.Add(new PetTownPetCareRecord{id=ids[i],nickname=names[i],speciesKind=species[i],petLevel=PetTownPetCareRecord.AdultLevel,growthRulesVersion=1});
        pets.Add(new PetTownPetCareRecord{id="show",nickname="전시 강아지",speciesKind=0,displayOnly=true,petLevel=PetTownPetCareRecord.AdultLevel,growthRulesVersion=1});
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
    // TextMeshPro/버튼 측정. UI를 하나도 못 찾으면 실패(빈 측정이 통과로 보이지 않게).
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
    bool Anchor(string careId,out Vector3 anchor)
    {
        var liveType=typeof(PetTownGame).GetNestedType("LivePet",BindingFlags.NonPublic|BindingFlags.Public);var live=Activator.CreateInstance(liveType);liveType.GetField("careId").SetValue(live,careId);
        var args=new object[]{live,null};bool ok=(bool)Call(game,"TryShopSlotAnchor",args);anchor=args[1] is Vector3?(Vector3)args[1]:Vector3.zero;return ok;
    }
    IEnumerator Run()
    {
        Setup();yield return null;Assert("no_save_camera",Get(game,"cam")==null);
        Assert("only_slot_furniture_listed",((IList)Call(game,"ShopSlotFurniture")).Count==3);
        // 목록
        Call(game,"OpenPetSlots");yield return Shot("slots_list");
        Click(0);yield return Shot("slot_cage_empty");
        // 케이지 A: 토끼, 페럿 배정
        Click(200);yield return null;Assert("assign_rabbit_by_menu",Pet("rabbit").slotId=="a-cage");
        Click(200);yield return null;Assert("assign_ferret_by_menu",Pet("ferret").slotId=="a-cage");
        yield return Shot("slot_cage_full");
        Click(200);yield return null;Assert("full_cage_rejects_third",string.IsNullOrEmpty(Pet("rabbit2").slotId));
        Click(201);yield return null;Assert("wrong_species_rejected_by_menu",string.IsNullOrEmpty(Pet("dog").slotId));
        Click(100);yield return null;Assert("release_by_menu",string.IsNullOrEmpty(Pet("rabbit").slotId)&&Pet("ferret").slotId=="a-cage");
        Click(201);yield return null;Assert("freed_seat_filled",Pet("rabbit2").slotId=="a-cage");
        // 쿠션: 강아지
        Click(7);yield return Shot("slots_list_after");
        Click(2);yield return null;Click(203);yield return null;Assert("dog_on_cushion",Pet("dog").slotId=="c-cushion");
        Assert("display_pet_never_assigned",string.IsNullOrEmpty(Pet("show").slotId));
        Assert("slot_activity_recorded_per_successful_assign",data.Operations(500).Activity("slot")==4);
        var slotBoardKeys=BoardKeys();trace.Add("board keys (slots): "+string.Join(",",new List<string>(slotBoardKeys).ToArray()));Assert("slot_quest_offered_with_slots",slotBoardKeys.Contains("slot")&&!slotBoardKeys.Contains("fish"));
        // 펫 정보 창에 현재 자리 표시
        Assert("slot_name_for_info_window",(string)Call(game,"ShopSlotName",Pet("dog"))=="강아지 쿠션 1"&&(string)Call(game,"ShopSlotName",Pet("rabbit2"))=="RAT 케이지 1"&&(string)Call(game,"ShopSlotName",Pet("cat"))==""&&(string)Call(game,"ShopSlotName",Pet("show"))=="");
        window.Hide();yield return null;
        var care=uiObject.AddComponent<PetTownPetCareWindow>();var careTargets=new List<PetTownPetCareTarget>{new PetTownPetCareTarget{id="dog",name="강아지 초코",species="강아지"},new PetTownPetCareTarget{id="cat",name="고양이 나비",species="고양이"}};
        care.Show(uiObject.transform,careTargets,m=>{},(PetTownPetCareContext)Call(game,"ProgressionCareContext"));yield return null;yield return null;
        Assert("care_window_shows_slot",HasText("강아지 쿠션 1"));
        TMPro.TextMeshProUGUI careSummary=null;foreach(var tx in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))if(tx.gameObject.activeInHierarchy&&tx.text!=null&&tx.text.Contains("강아지 쿠션 1"))careSummary=tx;
        Assert("care_summary_found",careSummary!=null);careSummary.ForceMeshUpdate();trace.Add("care summary fontSize="+careSummary.fontSize+" overflowing="+careSummary.isTextOverflowing+" text="+careSummary.text.Replace("\n","|"));
        Assert("care_summary_not_overflowing",!careSummary.isTextOverflowing&&careSummary.fontSize>=11.9f);
        yield return Shot("pet_care_window_slot");
        Call(care,"Change",1);yield return null;yield return null;Assert("care_window_cat_has_no_slot",!HasText("쿠션 1")&&!HasText("RAT 케이지"));
        Call(care,"Close");yield return null;Destroy(care);yield return null;
        // 이동 목표 중심
        Vector3 anchor;Assert("anchor_cushion_dog",Anchor("dog",out anchor)&&Vector3.Distance(anchor,cushion.transform.position)<.001f);
        Assert("anchor_cage_ferret",Anchor("ferret",out anchor)&&Vector3.Distance(anchor,cageA.transform.position)<.001f);
        Assert("no_anchor_when_unassigned",!Anchor("cat",out anchor)&&!Anchor("rabbit",out anchor)&&!Anchor("show",out anchor)&&!Anchor("missing",out anchor));
        var roam=PetTownShopSlots.RoamPoint(anchor=cushion.transform.position,.3f,.6f);Assert("roam_target_near_cushion",Vector2.Distance(new Vector2(roam.x,roam.z),new Vector2(anchor.x,anchor.z))<1.21f);
        // 가구 제거 → 배정 해제
        DestroyImmediate(cageA.gameObject);yield return null;
        Assert("anchor_gone_with_furniture",!Anchor("ferret",out anchor)&&!Anchor("rabbit2",out anchor));
        Call(game,"OpenPetSlots");yield return Shot("slots_list_after_remove");
        Assert("removed_furniture_frees_pets",string.IsNullOrEmpty(Pet("ferret").slotId)&&string.IsNullOrEmpty(Pet("rabbit2").slotId)&&Pet("dog").slotId=="c-cushion");
        // 자리가 하나도 없을 때 + 펫샵 관리 메뉴 진입
        DestroyImmediate(cageB.gameObject);DestroyImmediate(cushion.gameObject);yield return null;
        Call(game,"OpenPetSlots");yield return Shot("slots_empty");Assert("empty_state_message",HasText("배치된 전시 자리가 없어요"));
        Call(game,"OpenShopManagement");yield return null;Click(31);yield return null;yield return null;
        Assert("management_menu_opens_slots",HasText("배치된 전시 자리가 없어요")||HasText("펫 전시 자리"));
        var menu=new List<PetTownCatalogEntry>();Call(game,"AppendOperationsMenu",menu);bool has31=false;foreach(var e in menu)if(e.Key==31&&e.Name=="펫 전시 자리")has31=true;Assert("management_menu_has_entry",has31);
        Cleanup();yield return null;
    }
    public void Cleanup()
    {
        if(uiObject)DestroyImmediate(uiObject);if(camObject)DestroyImmediate(camObject);if(rt){rt.Release();DestroyImmediate(rt);}if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);
        uiObject=camObject=host=fixture=shopObject=null;rt=null;window=null;
    }
}
#endif
