#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;

public sealed class PetTownFarmCropsVisualQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Evidence="Assets/PetTown3D/ArtDirection/FarmCrops/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host;PetTownGame game;PetTownProgressionState data;PetTownFarmView view;PetTownFarmRecord lot;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownFarmCropsVisualQA","running");bool failed=false;var run=Run();
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        Cleanup();File.WriteAllText(Evidence+"native-visual-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"native-visual-trace.txt",trace);SessionState.SetString("PetTownFarmCropsVisualQA",failed?"failed":"complete");enabled=false;
    }
    public void Setup(int rotation=0)
    {
        fixture=new GameObject("QA_FARM_CROPS_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.Euler(0,rotation,0));
        host=new GameObject("QA_FARM_CROPS_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",5000);
        var root=new GameObject("QA_NATIVE_FARM");root.transform.SetParent(fixture.transform,false);Set(game,"shop",root);((IList)Get(game,"buildings")).Add(root);
        var floor=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic),true);Set(floor,"owner",root);Set(floor,"center",root.transform.position);Set(floor,"halfW",10f);Set(floor,"halfD",10f);Set(floor,"floorY",300.045f);((IList)Get(game,"buildingFloors")).Add(floor);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",14f);Set(flat,"halfD",14f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        lot=data.Farm.Get("qa-farm-crops",500);for(int crop=0;crop<7;crop++){lot.plots[crop].crop=crop;data.Farm.seeds[crop]=5;}
        view=root.AddComponent<PetTownFarmView>();view.Initialize(game,lot.id);
    }
    Transform Crops=>(Transform)typeof(PetTownFarmView).GetField("crops",F).GetValue(view);
    Transform Plant(int crop){foreach(Transform child in Crops)if(child.name==PetTownFarmState.Names[crop])return child;return null;}
    static Bounds BoundsOf(Transform root)
    {
        var bounds=new Bounds();bool first=true;foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){var b=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var point=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}}return bounds;
    }
    static float WorldHeight(Transform plant){var b=BoundsOf(plant);return b.size.y*plant.lossyScale.y;}
    int Draws(){int n=0;foreach(var r in Crops.GetComponentsInChildren<MeshRenderer>())if(r.enabled)n+=r.sharedMaterials.Length;return n;}
    IEnumerator Run()
    {
        foreach(int rotation in new[]{0,90})
        {
            Setup(rotation);yield return null;string key="rotation_"+rotation+"_";var small=new float[7];
            for(int crop=0;crop<7;crop++){var plant=Plant(crop);Assert(key+"native_model_"+crop,plant&&plant.GetComponentsInChildren<MeshFilter>().Length>0);small[crop]=WorldHeight(plant);Assert(key+"small_growth_"+crop,Mathf.Approximately(plant.localScale.x,.28f)&&small[crop]>.02f);Assert(key+"icon_"+crop,PetTownFarmLibrary.Current.CropIcon(crop)!=null);lot.plots[crop].grown=PetTownFarmState.GrowDays[crop];}
            view.Refresh();yield return null;for(int crop=0;crop<7;crop++){var plant=Plant(crop);float height=WorldHeight(plant);Assert(key+"mature_growth_"+crop,Mathf.Approximately(plant.localScale.x,1)&&height>small[crop]*3.4f&&height<=PetTownFarmLibrary.CropSize(crop).y+.03f);var origin=view.transform.InverseTransformPoint(plant.position);Assert(key+"ground_contact_"+crop,Mathf.Abs(origin.y-.16f)<.002f);trace.Add(key+PetTownFarmState.Names[crop]+" height "+small[crop]+" -> "+height);}
            int unbatched=0;foreach(var r in Crops.GetComponentsInChildren<MeshRenderer>())if(!r.name.StartsWith("CropBatch_"))unbatched+=r.sharedMaterials.Length;int draws=Draws();Assert(key+"material_batches_reduce_draws",draws>0&&draws<unbatched/2);trace.Add(key+"draws "+unbatched+" -> "+draws);
            var owned=(List<Mesh>)typeof(PetTownFarmView).GetField("cropMeshes",F).GetValue(view);int meshCount=owned.Count;var previous=owned.ToArray();view.Refresh();yield return null;Assert(key+"stable_refresh_keeps_meshes",owned.Count==meshCount&&ReferenceEquals(previous[0],owned[0]));
            lot.plots[0].grown=0;view.Refresh();yield return null;Assert(key+"old_runtime_meshes_released",Array.TrueForAll(previous,m=>m==null)&&owned.Count==meshCount&&Draws()==draws);
            previous=owned.ToArray();var soils=(List<Renderer>[])typeof(PetTownFarmView).GetField("soil",F).GetValue(view);var block=new MaterialPropertyBlock();soils[0][0].GetPropertyBlock(block,0);var dry=block.GetColor("_BaseColor");data.Farm.Water(lot,0,500);view.Refresh();yield return null;soils[0][0].GetPropertyBlock(block,0);var watered=block.GetColor("_BaseColor");Assert(key+"watering_keeps_crop_meshes",ReferenceEquals(previous[0],owned[0]));Assert(key+"watering_updates_soil",Mathf.Abs(watered.r-dry.r*.64f)<.001f&&Mathf.Abs(watered.g-dry.g*.64f)<.001f);
            var saved=JsonUtility.FromJson<PetTownFarmState>(JsonUtility.ToJson(data.Farm));saved.Normalize();data.farm=saved;lot=saved.lots[0];view.Refresh();yield return null;Assert(key+"saved_crops_keep_rendering",Draws()==draws&&lot.plots[6].crop==6);
            for(int i=0;i<7;i++){lot.plots[i].grown=PetTownFarmState.GrowDays[i];data.Farm.Harvest(lot,i,out _);}view.Refresh();yield return null;Assert(key+"harvest_removes_crop_geometry",Crops.GetComponentsInChildren<MeshRenderer>().Length==0);
            Cleanup();yield return null;
        }
    }
    public void Cleanup(){if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);host=null;fixture=null;view=null;}
}
#endif
