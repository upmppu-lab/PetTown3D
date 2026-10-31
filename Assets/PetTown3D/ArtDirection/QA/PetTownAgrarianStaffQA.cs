#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;

// Isolated native-asset simulation. The user's game and PlayerPrefs slot are never loaded.
public sealed class PetTownAgrarianStaffQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Evidence="Assets/PetTown3D/ArtDirection/AgrarianStaff/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();readonly List<string> trace=new List<string>();
    GameObject fixture,host;PetTownGame game;PetTownProgressionState data;NavMeshSurface surface;Transform workplace;bool farm;string id;
    readonly List<NavMeshAgent> agents=new List<NavMeshAgent>();readonly List<object> workers=new List<object>();
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownAgrarianStaffQA","running");float original=Time.timeScale;Time.timeScale=3;bool failed=false;var run=Run();
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){trace.Add("FAIL "+e);failed=true;break;}if(!more)break;yield return run.Current;}
        try{Cleanup();}catch(Exception e){failed=true;trace.Add("Cleanup "+e);}
        Time.timeScale=original;File.WriteAllText(Evidence+"physical-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"physical-trace.txt",trace);SessionState.SetString("PetTownAgrarianStaffQA",failed?"failed":"complete");enabled=false;
    }
    void Setup(bool isFarm,int rotation)
    {
        farm=isFarm;id="qa-agrarian-"+(farm?"farm":"ranch")+rotation;
        fixture=new GameObject("QA_AGRARIAN_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.Euler(0,rotation,0));
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="QA_Floor";floor.transform.SetParent(fixture.transform,false);floor.transform.localPosition=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(29,.2f,29);
        host=new GameObject("QA_AGRARIAN_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();data=new PetTownProgressionState{stockInitialized=true};Set(game,"progression",data);Set(game,"day",500);Set(game,"money",1000);
        var lotObject=new GameObject("QA_NATIVE_"+(farm?"FARM":"RANCH"));lotObject.transform.SetParent(fixture.transform,false);workplace=lotObject.transform;Set(game,"shop",lotObject);((IList)Get(game,"buildings")).Add(lotObject);
        var b=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic),true);Set(b,"owner",lotObject);Set(b,"center",lotObject.transform.position);Set(b,"halfW",farm?9f:11f);Set(b,"halfD",farm?10f:11f);Set(b,"floorY",300.045f);((IList)Get(game,"buildingFloors")).Add(b);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",14f);Set(flat,"halfD",14f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        if(farm)
        {
            var lot=data.Farm.Get(id,500);lot.staffService=true;lot.plots[0].crop=0;lot.plots[1].crop=1;lot.plots[1].grown=3;data.Farm.seeds=new[]{5,5};
            var birds=data.Farm.Poultry;int gold=5000;birds.Build(id,500,ref gold);var coop=birds.Find(id);birds.BuyBird(coop,500,ref gold);birds.BuyBird(coop,500,ref gold);foreach(var bird in coop.birds)bird.grown=2;coop.cleanliness=70;coop.nestEggs=3;birds.feed=6;
            lotObject.AddComponent<PetTownFarmView>().Initialize(game,id);
        }
        else
        {
            var lot=data.Ranch.Get(id,500);lot.staffService=true;lot.cleanliness=70;data.Ranch.feed=9;int gold=5000;for(int s=0;s<3;s++)data.Ranch.BuyAnimal(lot,s,500,ref gold);foreach(var a in lot.animals)a.grown=2;lot.animals[0].ready=1;lot.animals[1].ready=1;
            lotObject.AddComponent<PetTownRanchView>().Initialize(game,id);
        }
    }
    void BakeAndSpawn()
    {
        surface=fixture.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.08f;Physics.SyncTransforms();surface.BuildNavMesh();
        var staff=(IDictionary)Get(game,"employeeActors");
        for(int i=0;i<2;i++)
        {
            var p=new PetTownEmployeeRecord{id="qa-worker-"+i,name="검사 관리사 "+i,role=farm?"farmer":"rancher",facilityId=id};data.Operations(500).employees.Add(p);
            var actor=new GameObject("QA_AGRARIAN_WORKER_"+i);actor.transform.SetParent(fixture.transform,false);NavMeshHit hit;NavMesh.SamplePosition(workplace.TransformPoint(new Vector3((i==0?-.6f:.6f),0,-12)),out hit,2,-1);actor.transform.position=hit.position;
            var agent=actor.AddComponent<NavMeshAgent>();agent.radius=.4f;agent.height=1.6f;agent.stoppingDistance=.12f;agent.speed=2.8f;agent.acceleration=20;agent.angularSpeed=720;agents.Add(agent);
            var a=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("EmployeeActor",BindingFlags.NonPublic),true);Set(a,"root",actor);Set(a,"agent",agent);Set(a,"employeeId",p.id);staff.Add(p.id,a);workers.Add(a);
        }
    }
    string Stamp()=>farm?JsonUtility.ToJson(data.Farm):JsonUtility.ToJson(data.Ranch);
    int Remaining()=>farm?PetTownAgrarianWork.FarmOrders(data.Farm,data.Farm.lots[0],(int)Get(game,"day")).Count:PetTownAgrarianWork.RanchOrders(data.Ranch,data.Ranch.lots[0],(int)Get(game,"day")).Count;
    void Toggle(bool on){if(farm)data.Farm.lots[0].staffService=on;else data.Ranch.lots[0].staffService=on;}
    void Step(){Call(game,"UpdateEmployees",Mathf.Clamp(Time.deltaTime,.001f,.25f));}
    IEnumerator Run()
    {
        foreach(bool isFarm in new[]{true,false})foreach(int rotation in new[]{0,90})
        {
            Setup(isFarm,rotation);yield return null;BakeAndSpawn();yield return null;string key=(farm?"farm_":"ranch_")+rotation+"_";
            Assert(key+"native_agents_on_navmesh",agents.All(a=>a.isOnNavMesh));string initial=Stamp();float start=Time.realtimeSinceStartup;
            while(!workers.Any(a=>Get(a,"task").ToString()=="Agrarian")&&Time.realtimeSinceStartup-start<5){Step();yield return null;}
            foreach(var a in agents)a.isStopped=true;float until=Time.realtimeSinceStartup+.7f;
            while(Time.realtimeSinceStartup<until){Step();foreach(var a in agents)a.isStopped=true;yield return null;}
            Assert(key+"no_completion_before_arrival",Stamp()==initial);
            foreach(var p in data.operations.employees)p.unpaid=5;string held=Stamp();until=Time.realtimeSinceStartup+.7f;while(Time.realtimeSinceStartup<until){Step();yield return null;}
            Assert(key+"unpaid_workers_pause",Stamp()==held&&workers.All(a=>Get(a,"task").ToString()=="None"));foreach(var p in data.operations.employees)p.unpaid=0;
            Toggle(false);held=Stamp();until=Time.realtimeSinceStartup+.7f;while(Time.realtimeSinceStartup<until){Step();yield return null;}
            Assert(key+"automation_off_blocks_work",Stamp()==held);Toggle(true);foreach(var a in agents)a.isStopped=false;
            bool reservations=true;start=Time.realtimeSinceStartup;
            while(Remaining()>0&&Time.realtimeSinceStartup-start<80)
            {
                Step();var targets=workers.Where(a=>Get(a,"task").ToString()=="Agrarian").Select(a=>(string)Get(a,"target")).ToList();reservations&=targets.Distinct().Count()==targets.Count;yield return null;
            }
            Step();Assert(key+"all_jobs_complete",Remaining()==0);Assert(key+"no_duplicate_target_reservation",reservations);
            if(farm)
            {
                var lot=data.Farm.lots[0];var coop=data.Farm.poultry.coops[0];Assert(key+"farm_outputs_once",lot.plots[0].wateredDay==500&&lot.plots[1].crop==-1&&data.Farm.produce[1]==1&&data.Farm.poultry.eggs==3&&coop.nestEggs==0&&data.Farm.poultry.feed==4&&coop.cleanliness==100);
                Assert(key+"farm_rewards_and_decisions",data.operations.Activity("harvest")==1&&data.operations.Activity("eggs")==3&&data.Farm.seeds.SequenceEqual(new[]{5,5})&&(int)Get(game,"money")==1000&&data.operations.employees.Sum(x=>x.completed)==6);
                data.Farm.AdvanceDay(501);data.Farm.poultry.AdvanceDay(501,new[]{id});Assert(key+"next_day_crop_growth",lot.plots[0].grown==1);
            }
            else
            {
                var lot=data.Ranch.lots[0];Assert(key+"ranch_outputs_once",data.Ranch.products[0]==1&&data.Ranch.products[1]==1&&data.Ranch.feed==6&&lot.cleanliness==100&&lot.animals.All(a=>a.fedDay==500&&a.wateredDay==500));
                Assert(key+"ranch_rewards_and_pig_preserved",data.operations.Activity("milk")==1&&data.operations.Activity("wool")==1&&data.Ranch.products[2]==0&&lot.animals.Count==3&&(int)Get(game,"money")==1000&&data.operations.employees.Sum(x=>x.completed)==5);
                data.Ranch.AdvanceDay(501,new[]{id});
            }
            Set(game,"day",501);start=Time.realtimeSinceStartup;
            while(Remaining()>0&&Time.realtimeSinceStartup-start<60){Step();yield return null;}
            Assert(key+"next_day_automatic_care",Remaining()==0&&(farm?data.Farm.lots[0].plots[0].wateredDay==501:data.Ranch.lots[0].animals.All(a=>a.fedDay==501&&a.wateredDay==501)));
            trace.Add(key+"completed "+JsonUtility.ToJson(data));Cleanup();yield return null;yield return null;
        }
    }
    void Cleanup()
    {
        if(surface)surface.RemoveData();if(host)Destroy(host);if(fixture)Destroy(fixture);host=null;fixture=null;surface=null;agents.Clear();workers.Clear();
    }
}
#endif
