#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;

// 양/돼지 걷기 검사(소의 걷기 클립 재사용). 가짜 목장 + 비활성 가짜 게임(저장 카메라 없음)만 사용합니다.
public sealed class PetTownRanchWalkQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Evidence="Assets/PetTown3D/ArtDirection/RanchWalk/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host;PetTownGame game;PetTownProgressionState data;PetTownRanchView view;PetTownRanchLot lot;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    static string SaveHash(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PlayerPrefs.GetString("PetTown3D_Save","")+"|"+PlayerPrefs.GetString("PetTown3D_PetCare_v1",""))));}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownRanchWalkQA","running");string before=SaveHash();float scale=Time.timeScale;bool failed=false;var run=Run();
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        Time.timeScale=scale;try{Cleanup();}catch(Exception e){failed=true;trace.Add("Cleanup "+e);}
        bool same=before==SaveHash();checks["real_save_untouched"]=same;if(!same)failed=true;
        File.WriteAllText(Evidence+"walk-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"walk-trace.txt",trace);
        SessionState.SetString("PetTownRanchWalkQA",failed?"failed":"complete");enabled=false;
    }
    void Setup()
    {
        fixture=new GameObject("QA_WALK_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.identity);
        host=new GameObject("QA_WALK_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",5000);
        var root=new GameObject("QA_NATIVE_RANCH");root.transform.SetParent(fixture.transform,false);Set(game,"shop",root);((IList)Get(game,"buildings")).Add(root);
        var floor=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic),true);Set(floor,"owner",root);Set(floor,"center",root.transform.position);Set(floor,"halfW",11f);Set(floor,"halfD",11f);Set(floor,"floorY",300.045f);((IList)Get(game,"buildingFloors")).Add(floor);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",14f);Set(flat,"halfD",14f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        lot=data.Ranch.Get("qa-ranch-walk",500);data.Ranch.feed=100;int gold=10000;for(int species=0;species<3;species++){data.Ranch.BuyAnimalOfSex(lot,species,2,500,ref gold);data.Ranch.BuyAnimalOfSex(lot,species,1,500,ref gold);}
        foreach(var a in lot.animals)a.grown=2;view=root.AddComponent<PetTownRanchView>();view.Initialize(game,lot.id);
    }
    PetTownRanchAnimalVisual Find(string id){foreach(var v in view.GetComponentsInChildren<PetTownRanchAnimalVisual>())if(v.name=="RANCH_ANIMAL_"+id)return v;return null;}
    static float Height(PetTownRanchAnimalVisual v)
    {
        Bounds b=default(Bounds);bool first=true;foreach(var s in v.GetComponentsInChildren<SkinnedMeshRenderer>()){if(first){b=s.bounds;first=false;}else b.Encapsulate(s.bounds);}return first?0:b.size.y;
    }
    static void Baked(PetTownRanchAnimalVisual v,out float low,out float high)
    {
        low=float.PositiveInfinity;high=float.NegativeInfinity;var scratch=new Mesh();foreach(var skin in v.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(scratch,false);foreach(var vertex in scratch.vertices){float y=(skin.transform.position+skin.transform.rotation*vertex).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}}Destroy(scratch);low-=v.transform.position.y;high-=v.transform.position.y;
    }
    static float LowestAboveRoot(PetTownRanchAnimalVisual v)
    {
        float min=float.PositiveInfinity;foreach(var s in v.GetComponentsInChildren<SkinnedMeshRenderer>())min=Mathf.Min(min,s.bounds.min.y);return min-v.transform.position.y;
    }
    IEnumerator Run()
    {
        Setup();yield return null;yield return null;Assert("no_save_camera",Get(game,"cam")==null);
        var lib=PetTownRanchLibrary.Current;Assert("library_walk_clips_set",lib.animals[0].walk&&lib.animals[1].walk&&lib.animals[2].walk);
        var visuals=new List<PetTownRanchAnimalVisual>();var ids=new List<string>();var species=new List<int>();foreach(var a in lot.animals){var v=Find(a.id);Assert("visual_"+PetTownRanchState.Species[a.species],v);visuals.Add(v);ids.Add(a.id);species.Add(a.species);}
        int n=visuals.Count;var start=new Vector3[n];var last=new Vector3[n];var travelled=new float[n];var sawWalk=new bool[n];var idleHeight=new float[n];var minHeight=new float[n];var maxHeight=new float[n];var lowest=new float[n];var lowestMax=new float[n];var overshoot=new float[n];var bakedLow=new float[n];var bakedLowMax=new float[n];var bakedHigh=new float[n];var bakedHighMin=new float[n];var bakedIdleHeight=new float[n];
        for(int i=0;i<n;i++){float lo,hi;Baked(visuals[i],out lo,out hi);bakedLow[i]=bakedLowMax[i]=lo;bakedHigh[i]=bakedHighMin[i]=hi;bakedIdleHeight[i]=hi-lo;}
        for(int i=0;i<n;i++){start[i]=last[i]=visuals[i].transform.localPosition;idleHeight[i]=Height(visuals[i]);minHeight[i]=maxHeight[i]=idleHeight[i];lowest[i]=lowestMax[i]=LowestAboveRoot(visuals[i]);}
        Time.timeScale=5f;float end=Time.realtimeSinceStartup+14f;int frames=0;
        while(Time.realtimeSinceStartup<end)
        {
            yield return null;frames++;
            for(int i=0;i<n;i++)
            {
                var v=visuals[i];if(!Application.isFocused)v.Tick(Mathf.Min(Time.deltaTime,.25f));
                var p=v.transform.localPosition;travelled[i]+=Vector3.Distance(p,last[i]);last[i]=p;var anim=v.GetComponentInChildren<Animation>();if(anim&&anim.IsPlaying("RanchWalk"))sawWalk[i]=true;
                float h=Height(v);minHeight[i]=Mathf.Min(minHeight[i],h);maxHeight[i]=Mathf.Max(maxHeight[i],h);float low=LowestAboveRoot(v);lowest[i]=Mathf.Min(lowest[i],low);lowestMax[i]=Mathf.Max(lowestMax[i],low);
                if(frames%12==0){float lo,hi;Baked(v,out lo,out hi);bakedLow[i]=Mathf.Min(bakedLow[i],lo);bakedLowMax[i]=Mathf.Max(bakedLowMax[i],lo);bakedHigh[i]=Mathf.Max(bakedHigh[i],hi);bakedHighMin[i]=Mathf.Min(bakedHighMin[i],hi);}
                var local=view.transform.InverseTransformPoint(v.transform.position);var center=PetTownRanchView.PenCenter(species[i]);var half=PetTownRanchView.PenHalf(species[i]);float over=Mathf.Max(Mathf.Abs(local.x-center.x)-half.x,Mathf.Abs(local.z-center.z)-half.y);overshoot[i]=Mathf.Max(overshoot[i],over);
            }
        }
        trace.Add("frames="+frames+" realtime=14s timeScale=5 focused="+Application.isFocused);
        for(int i=0;i<n;i++)
        {
            string name=PetTownRanchState.Species[species[i]]+(lot.animals[i].sex==2?"♀":"♂");
            trace.Add(name+": travelled="+travelled[i].ToString("F2")+"m sawWalk="+sawWalk[i]+" height idle="+idleHeight[i].ToString("F2")+" range="+minHeight[i].ToString("F2")+"~"+maxHeight[i].ToString("F2")+" feetBelowRoot="+lowest[i].ToString("F2")+" feetMax="+lowestMax[i].ToString("F2")+" penOvershoot="+overshoot[i].ToString("F2")+"m penHalf="+PetTownRanchView.PenHalf(species[i]).ToString("F1")+" penCenter="+PetTownRanchView.PenCenter(species[i]).ToString("F1"));
            Assert("walks_"+name,travelled[i]>.4f&&sawWalk[i]);
            Assert("height_stable_"+name,idleHeight[i]>.1f&&minHeight[i]>idleHeight[i]*.7f&&maxHeight[i]<idleHeight[i]*1.35f);
            Assert("feet_stay_near_ground_"+name,lowest[i]>-.3f&&lowestMax[i]<.3f);checks["stays_inside_own_pen_"+name]=overshoot[i]<.2f;
            trace.Add("   baked: idleHeight="+bakedIdleHeight[i].ToString("F2")+" lowestRange="+bakedLow[i].ToString("F3")+"~"+bakedLowMax[i].ToString("F3")+" topRange="+bakedHighMin[i].ToString("F2")+"~"+bakedHigh[i].ToString("F2")+" libraryHeight="+lib.animals[species[i]].height.ToString("F2"));
            Assert("baked_height_calibrated_"+name,Mathf.Abs(bakedIdleHeight[i]-lib.animals[species[i]].height)<.25f);
            Assert("baked_feet_stay_on_ground_"+name,bakedLow[i]>-.12f*bakedIdleHeight[i]&&bakedLowMax[i]<.12f*bakedIdleHeight[i]);
            Assert("baked_body_not_bobbing_"+name,bakedHighMin[i]>bakedIdleHeight[i]*.85f&&bakedHigh[i]<bakedIdleHeight[i]*1.15f);
        }
        Time.timeScale=1f;Cleanup();yield return null;
    }
    public void Cleanup(){if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);host=null;fixture=null;view=null;}
}
#endif
