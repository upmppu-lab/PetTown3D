#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;

// Native imported assets, isolated data and no save camera; never runs the user's game.
public sealed class PetTownRanchBirthVisualQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Evidence="Assets/PetTown3D/ArtDirection/RanchBreeding/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host;PetTownGame game;PetTownProgressionState data;PetTownRanchView view;PetTownRanchLot lot;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownRanchBirthVisualQA","running");var run=Run();bool failed=false;
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        Cleanup();File.WriteAllText(Evidence+"native-visual-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"native-visual-trace.txt",trace);SessionState.SetString("PetTownRanchBirthVisualQA",failed?"failed":"complete");enabled=false;
    }
    public void Setup(int rotation=0)
    {
        fixture=new GameObject("QA_RANCH_BIRTH_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.Euler(0,rotation,0));
        host=new GameObject("QA_RANCH_BIRTH_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",5000);
        var root=new GameObject("QA_NATIVE_RANCH");root.transform.SetParent(fixture.transform,false);Set(game,"shop",root);((IList)Get(game,"buildings")).Add(root);
        var floor=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic),true);Set(floor,"owner",root);Set(floor,"center",root.transform.position);Set(floor,"halfW",11f);Set(floor,"halfD",11f);Set(floor,"floorY",300.045f);((IList)Get(game,"buildingFloors")).Add(floor);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",14f);Set(flat,"halfD",14f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        lot=data.Ranch.Get("qa-ranch-birth",500);data.Ranch.feed=100;int gold=10000;for(int species=0;species<3;species++){data.Ranch.BuyAnimalOfSex(lot,species,2,500,ref gold);data.Ranch.BuyAnimalOfSex(lot,species,1,500,ref gold);}foreach(var a in lot.animals)a.grown=2;data.Ranch.Feed(lot,500);data.Ranch.Water(lot,500);
        view=root.AddComponent<PetTownRanchView>();view.Initialize(game,lot.id);
    }
    PetTownRanchAnimalVisual Find(string id){foreach(var v in view.GetComponentsInChildren<PetTownRanchAnimalVisual>())if(v.name=="RANCH_ANIMAL_"+id)return v;return null;}
    static float Height(PetTownRanchAnimalVisual root)
    {
        float min=float.PositiveInfinity,max=float.NegativeInfinity;var scratch=new Mesh();foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(scratch,false);foreach(var vertex in scratch.vertices){float y=(skin.transform.position+skin.transform.rotation*vertex).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);}}Destroy(scratch);return max>min?max-min:0;
    }
    IEnumerator Run()
    {
        foreach(int rotation in new[]{0,90})
        {
            Setup(rotation);yield return null;var s=data.Ranch;string reason;string key="rotation_"+rotation+"_";Assert(key+"native_adult_models",view.GetComponentsInChildren<PetTownRanchAnimalVisual>().Length==6);
            for(int species=0;species<3;species++){var pair=lot.animals.FindAll(a=>a.species==species);Assert(key+"breeding_"+species,s.StartBreeding(lot,pair[0].id,pair[1].id,500,out reason));}
            s.AdvanceDay(501,new[]{lot.id});s.Feed(lot,501);s.Water(lot,501);s.AdvanceDay(502,new[]{lot.id});Set(game,"day",502);view.RefreshAnimals();yield return null;yield return null;
            Assert(key+"nine_native_models_after_birth",view.GetComponentsInChildren<PetTownRanchAnimalVisual>().Length==9&&s.births.Count==3);
            var heights=new Dictionary<string,float>();var babies=lot.animals.FindAll(a=>a.generation==1);foreach(var baby in babies){var visual=Find(baby.id);Assert(key+"small_native_baby_"+baby.species,visual&&Mathf.Approximately(visual.CurrentScale,.45f));float h=Height(visual);heights[baby.id]=h;Assert(key+"calibrated_baby_height_"+baby.species,Mathf.Abs(h-PetTownRanchLibrary.Current.animals[baby.species].height*.45f)<.12f);Assert(key+"rendered_geometry_"+baby.species,h>.1f&&visual.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0);var local=view.transform.InverseTransformPoint(visual.transform.position);var center=PetTownRanchView.PenCenter(baby.species);var half=PetTownRanchView.PenHalf(baby.species);Assert(key+"baby_inside_own_pen_"+baby.species,Mathf.Abs(local.x-center.x)<half.x&&Mathf.Abs(local.z-center.z)<half.y);}
            var clone=JsonUtility.FromJson<PetTownRanchState>(JsonUtility.ToJson(s));clone.Normalize();data.ranch=clone;s=clone;lot=clone.lots[0];view.RefreshAnimals();yield return null;Assert(key+"restart_no_duplicate_models",view.GetComponentsInChildren<PetTownRanchAnimalVisual>().Length==9);
            s.Feed(lot,502);s.Water(lot,502);s.Clean(lot);s.AdvanceDay(503,new[]{lot.id});s.Feed(lot,503);s.Water(lot,503);s.Clean(lot);s.AdvanceDay(504,new[]{lot.id});Set(game,"day",504);view.RefreshAnimals();foreach(var baby in lot.animals)if(baby.generation==1)Find(baby.id).ApplyGrowth(10);yield return null;
            foreach(var baby in lot.animals)if(baby.generation==1){var visual=Find(baby.id);float h=Height(visual);Assert(key+"adult_scale_"+baby.species,baby.Adult&&Mathf.Approximately(visual.CurrentScale,1));Assert(key+"visible_growth_"+baby.species,h>heights[baby.id]*1.5f);Assert(key+"calibrated_adult_height_"+baby.species,Mathf.Abs(h-PetTownRanchLibrary.Current.animals[baby.species].height)<.15f);trace.Add(key+PetTownRanchState.Species[baby.species]+" height "+heights[baby.id]+" -> "+h);}
            var sold=lot.animals.Find(a=>a.generation==1&&a.species==2);int goldBefore=(int)Get(game,"money"),gold=goldBefore;Assert(key+"sale_uses_state",s.SellAnimal(lot,sold.id,ref gold,out reason)&&gold==goldBefore+PetTownRanchState.AnimalSellQuote(sold));view.RefreshAnimals();yield return null;Assert(key+"sold_model_removed",!Find(sold.id)&&view.GetComponentsInChildren<PetTownRanchAnimalVisual>().Length==8);
            Cleanup();yield return null;
        }
    }
    public void Cleanup(){if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);host=null;fixture=null;view=null;}
}
#endif
