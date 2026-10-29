#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;

// Editor-only isolated simulation; never uses the user's game instance or save camera.
public sealed class PetTownClinicPhysicalQA : MonoBehaviour
{
    public int[] scenarios={0,1,2,3};
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Evidence="Assets/PetTown3D/ArtDirection/ClinicHealth/";
    readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
    readonly List<string> trace=new List<string>();
    GameObject fixture,host;PetTownGame game;PetTownProgressionState data;PetTownInteriorState room;
    PetTownFurniturePlacement counter,table;NavMeshSurface surface;GameObject visitor,pet;NavMeshAgent visitorAgent,petAgent;
    object companion;IDictionary staffActors,visitorRuns;PetTownEmployeeRecord worker,cashier;object workActor,cashActor;
    static object Get(object o,string name)=>o.GetType().GetField(name,F|BindingFlags.Public).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,F|BindingFlags.Public).SetValue(o,value);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
    void Assert(string key,bool ok){checks[key]=ok;if(!ok)throw new Exception(key);}
    IEnumerator Start()
    {
        Directory.CreateDirectory(Evidence);SessionState.SetString("PetTownClinicPhysicalQA","running");float originalScale=Time.timeScale;Time.timeScale=3;
        var run=Run();bool failed=false;
        while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){failed=true;trace.Add("FAIL "+e);break;}if(!more)break;yield return run.Current;}
        try{Cleanup();}catch(Exception e){failed=true;trace.Add("Cleanup "+e);}
        Time.timeScale=originalScale;File.WriteAllText(Evidence+"physical-tests.json",Newtonsoft.Json.JsonConvert.SerializeObject(checks,Newtonsoft.Json.Formatting.Indented));File.WriteAllLines(Evidence+"physical-trace.txt",trace);
        SessionState.SetString("PetTownClinicPhysicalQA",failed?"failed":"complete");enabled=false;
    }
    GameObject Box(string name,Vector3 p,Vector3 size)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(fixture.transform,false);o.transform.localPosition=p;o.transform.localScale=size;return o;
    }
    NavMeshAgent Agent(string name,Vector3 local,float radius)
    {
        var o=new GameObject(name);o.transform.SetParent(fixture.transform,false);NavMeshHit hit;NavMesh.SamplePosition(fixture.transform.TransformPoint(local),out hit,2,NavMesh.AllAreas);o.transform.position=hit.position;
        var a=o.AddComponent<NavMeshAgent>();a.radius=radius;a.height=1.6f;a.baseOffset=0;a.stoppingDistance=.12f;a.speed=2.8f;a.acceleration=20;a.angularSpeed=720;a.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;return a;
    }
    object Staff(PetTownEmployeeRecord p,NavMeshAgent agent)
    {
        var a=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("EmployeeActor",BindingFlags.NonPublic),true);
        Set(a,"root",agent.gameObject);Set(a,"agent",agent);Set(a,"employeeId",p.id);staffActors.Add(p.id,a);return a;
    }
    void Setup(int kind,int menu)
    {
        fixture=new GameObject("QA_STAFF_FIXTURE");fixture.transform.SetPositionAndRotation(new Vector3(1000,300,1000),Quaternion.Euler(0,menu%2==1?90:0,0));
        Box("QA_Floor",new Vector3(0,-.1f,0),new Vector3(24,.2f,24));
        var furnitureCounter=Box("QA_Counter",new Vector3(-1,.5f,-2),new Vector3(2,1,.8f));var furnitureTable=Box("QA_Table",new Vector3(3,.6f,1.7f),new Vector3(2,1.2f,1.2f));
        var roomObject=new GameObject("QA_Room");roomObject.transform.SetParent(fixture.transform,false);room=roomObject.AddComponent<PetTownInteriorState>();room.Initialize("qa-staff-room-"+kind,new Vector2(14,14),.045f,new Vector3(0,.045f,-7),kind);
        counter=furnitureCounter.AddComponent<PetTownFurniturePlacement>();counter.Initialize(room,0,false,new Vector2(2,.8f),1,"qa-counter");table=furnitureTable.AddComponent<PetTownFurniturePlacement>();table.Initialize(room,4,false,new Vector2(2,1.2f),1.2f,"qa-table");
        // Metadata belongs to the room; collision geometry is unchanged by parenting.
        furnitureCounter.transform.SetParent(roomObject.transform,true);furnitureTable.transform.SetParent(roomObject.transform,true);
        surface=fixture.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.overrideVoxelSize=true;surface.voxelSize=.08f;Physics.SyncTransforms();surface.BuildNavMesh();
        host=new GameObject("QA_STAFF_GAME");host.SetActive(false);game=host.AddComponent<PetTownGame>();
        data=new PetTownProgressionState{stockInitialized=true,residentVersion=1,residents=new PetTownResidentState()};
        var person=new PetTownResidentRecord{id="qa-staff-owner",name="검사 주민",happiness=50};person.pets.Add(new PetTownResidentPetRecord{id="qa-staff-pet",name="검사 강아지",species=0,level=3,lastCafeDay=-1,lastCheckupDay=-1});data.residents.people.Add(person);
        Set(game,"progression",data);Set(game,"shop",roomObject);Set(game,"day",500);Set(game,"money",1000);((IList)Get(game,"buildings")).Add(roomObject);
        var floorType=typeof(PetTownGame).GetNestedType("BuildingFloor",BindingFlags.NonPublic);var floor=Activator.CreateInstance(floorType,true);Set(floor,"owner",roomObject);Set(floor,"center",roomObject.transform.position);Set(floor,"halfW",12f);Set(floor,"halfD",12f);Set(floor,"floorY",room.FloorY);((IList)Get(game,"buildingFloors")).Add(floor);
        var flat=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("FlatZone",BindingFlags.NonPublic),true);Set(flat,"center",fixture.transform.position);Set(flat,"halfW",12f);Set(flat,"halfD",12f);Set(flat,"y",300f);((IList)Get(game,"flatZones")).Add(flat);
        visitorAgent=Agent("QA_Visitor",new Vector3(0,0,-9),.4f);visitor=visitorAgent.gameObject;((IDictionary)Get(game,"residentActors")).Add(person.id,visitor);
        petAgent=Agent("QA_Companion",new Vector3(-1,0,-9),.25f);petAgent.height=.8f;petAgent.avoidancePriority=70;pet=petAgent.gameObject;
        companion=Activator.CreateInstance(typeof(PetTownGame).GetNestedType("ResidentCompanion",BindingFlags.NonPublic),true);Set(companion,"owner",visitor.transform);Set(companion,"ownerId",person.id);Set(companion,"petId",person.pets[0].id);Set(companion,"root",pet);Set(companion,"agent",petAgent);((IDictionary)Get(game,"residentCompanions")).Add(person.id,companion);
        data.Facilities.cafeIngredients=3;data.Facilities.medicalSupplies=3;var b=data.Facilities.Get(room.Id,kind);b.open=true;b.staffService=true;
        if(kind==4)person.pets[0].health=new PetTownPetHealth{version=1,condition=menu==0?0:2,diagnosed=menu==2,episode=menu==0?0:1,lastDay=500,nextRiskDay=505};
        if(kind==4&&menu==2)
        {
            b.automatic=true;Call(game,"TickFacilities",.1f);Assert("auto_treatment_disabled_no_admission",data.Facilities.visits.Count==0);
            b.automaticTreatment=true;Set(game,"nextFacilityGuest",0f);Call(game,"TickFacilities",.1f);Assert("auto_treatment_enabled_admits_once",data.Facilities.visits.Count==1&&data.Facilities.visits[0].clinicService==1);
        }
        else{object[] admit={room,person.id,person.pets[0].id,null};Assert("native_admission_"+kind+"_"+menu,(bool)typeof(PetTownGame).GetMethod("TryAdmitFacility",F).Invoke(game,admit));}
        worker=new PetTownEmployeeRecord{id="qa-specialist",role=PetTownFacilityStaffRules.Specialist(kind),facilityId=room.Id};cashier=new PetTownEmployeeRecord{id="qa-cashier",role="cashier",facilityId=room.Id};data.Operations(500).employees.Add(worker);data.operations.employees.Add(cashier);
        staffActors=(IDictionary)Get(game,"employeeActors");visitorRuns=(IDictionary)Get(game,"facilityActors");workActor=Staff(worker,Agent("QA_Specialist",new Vector3(-8,0,-8),.4f));cashActor=Staff(cashier,Agent("QA_Cashier",new Vector3(8,0,-8),.4f));
    }
    void StepStaff(PetTownEmployeeRecord p,object a,float dt)
    {
        if(p.paused||p.unpaid>0){Call(game,"CancelEmployeeJob",a);return;}
        if(Get(a,"task").ToString()=="None")Call(game,"PickEmployeeJob",p,a);else Call(game,"UpdateFacilityEmployeeJob",p,a,dt);
    }
    IEnumerator Run()
    {
        foreach(int scenario in scenarios)
        {
            int kind=scenario==3?3:4,menu=scenario==3?0:scenario;Setup(kind,menu);yield return null;string prefix=kind==3?"cafe_":"clinic_"+menu+"_";
            Assert(prefix+"agents_on_native_navmesh",visitorAgent.isOnNavMesh&&petAgent.isOnNavMesh&&((NavMeshAgent)Get(workActor,"agent")).isOnNavMesh);
            int phase=0,lastStage=-1;float at=Time.realtimeSinceStartup,start=at,held=0;int stock=3,cost=kind==4&&menu==2?2:1;bool staffHeld=false,paymentHeld=false,resultChecked=false;Vector3 petBefore=Vector3.zero;
            while(data.Facilities.visits.Count>0&&Time.realtimeSinceStartup-start<80)
            {
                float dt=Mathf.Clamp(Time.deltaTime,.001f,.25f);var v=data.Facilities.visits[0];
                StepStaff(worker,workActor,dt);StepStaff(cashier,cashActor,dt);
                if(!staffHeld&&Get(workActor,"task").ToString()=="FacilityService"&&v.stage==PetTownFacilityStage.Waiting)
                {
                    ((NavMeshAgent)Get(workActor,"agent")).isStopped=true;staffHeld=true;at=Time.realtimeSinceStartup;phase=1;
                }
                if(phase==1&&Time.realtimeSinceStartup-at>.7f)
                {
                    Assert(prefix+"worker_not_arrived_no_service",v.stage==PetTownFacilityStage.Waiting&&data.Facilities.VisitStock(v)==stock);
                    ((NavMeshAgent)Get(workActor,"agent")).isStopped=false;phase=2;
                }
                if(phase==2&&v.stage==PetTownFacilityStage.Working)
                {
                    held=v.remaining;petBefore=pet.transform.position;NavMeshHit hit;NavMesh.SamplePosition(fixture.transform.TransformPoint(new Vector3(-6,0,-6)),out hit,2,-1);petAgent.Warp(hit.position);petAgent.isStopped=true;at=Time.realtimeSinceStartup;phase=3;
                }
                if(phase==3&&Time.realtimeSinceStartup-at>.7f)
                {
                    Assert(prefix+"pet_away_pauses_work",Mathf.Approximately(v.remaining,held));petAgent.isStopped=false;phase=4;
                }
                if(phase==4&&(bool)Call(game,"FacilityActionReady",v)&&(bool)Call(game,"FacilityWorkerPresent",v))
                {
                    worker.unpaid=5;held=v.remaining;at=Time.realtimeSinceStartup;phase=5;
                }
                if(phase==5&&Time.realtimeSinceStartup-at>.7f)
                {
                    Assert(prefix+"unpaid_worker_pauses_work",Mathf.Approximately(v.remaining,held));worker.unpaid=0;phase=6;
                }
                if(phase==6&&(bool)Call(game,"FacilityWorkerPresent",v)&&(bool)Call(game,"FacilityActionReady",v))
                {
                    Assert(prefix+"resume_reuses_one_supply",data.Facilities.VisitStock(v)==stock-cost&&v.remaining<=held);
                    var furniture=kind==3?counter:table;var before=furniture.transform.position;furniture.transform.position+=room.transform.right;
                    Assert(prefix+"moved_furniture_invalidates_worker",!(bool)Call(game,"FacilityWorkerPresent",v)&&!(bool)Call(game,"FacilityActionReady",v));
                    furniture.transform.position=before;Set(game,"constructionWorldRevision",(int)Get(game,"constructionWorldRevision")+1);phase=7;
                }
                if(!paymentHeld&&Get(cashActor,"task").ToString()=="FacilityPayment"&&v.stage==PetTownFacilityStage.Payment)
                {
                    ((NavMeshAgent)Get(cashActor,"agent")).isStopped=true;paymentHeld=true;at=Time.realtimeSinceStartup;phase=8;
                }
                if(phase==8&&Time.realtimeSinceStartup-at>.7f)
                {
                    Assert(prefix+"cashier_not_arrived_no_payment",data.Facilities.receipts.Count==0&&(int)Get(game,"money")==1000);
                    ((NavMeshAgent)Get(cashActor,"agent")).isStopped=false;phase=9;
                }
                Call(game,"UpdateFacilityVisit",visitor,visitorAgent,dt);Call(game,"TickResidentCompanion",companion,data.residents.people[0].pets[0],dt);
                if(kind==4&&v.clinicApplied&&!resultChecked)
                {
                    resultChecked=true;var h=data.residents.people[0].pets[0].health;
                    Assert(prefix+"result_before_payment",data.Facilities.receipts.Count==0&&(int)Get(game,"money")==1000);
                    Assert(prefix+"correct_health_result",menu==0?h.condition==0:menu==1?h.diagnosed&&!h.recovering:h.recovering&&h.restDays==2);
                }
                if(data.Facilities.visits.Count>0&&(int)v.stage!=lastStage){lastStage=(int)v.stage;trace.Add(prefix+v.stage+" owner="+visitor.transform.position+" pet="+pet.transform.position+" stock="+data.Facilities.VisitStock(v)+" remaining="+v.remaining);}
                yield return null;
            }
            if(data.Facilities.visits.Count>0){var stuck=data.Facilities.visits[0];trace.Add("END stage="+stuck.stage+" phase="+phase+" elapsed="+stuck.elapsed+" owner="+visitor.transform.position+" pet="+pet.transform.position+" navGoal="+visitorAgent.destination+" pending="+visitorAgent.pathPending+" path="+visitorAgent.pathStatus+" hasPath="+visitorAgent.hasPath+" stopped="+visitorAgent.isStopped);object run;if(visitorRuns.Contains(stuck.id)){run=visitorRuns[stuck.id];trace.Add("RUN valid="+Get(run,"valid")+" stage="+Get(run,"stage")+" goal="+Get(run,"goal")+" companionWait="+Get(run,"waitingForCompanion"));}}else trace.Add("END no visit phase="+phase);
            Assert(prefix+"complete_physical_visit",data.Facilities.visits.Count==0&&phase==9);
            Assert(prefix+"single_payment_and_receipt",data.Facilities.receipts.Count==1&&(int)Get(game,"money")==1000+(kind==3?70:menu==2?260:140));
            Assert(prefix+"one_stock_and_activity",data.Facilities.Stock(kind)==3-cost&&data.operations.Activity(kind==3?"cafe":"clinic")==1);
            Assert(prefix+"service_receipt",data.Facilities.receipts[0].clinicService==(kind==4&&menu==2?1:0));
            Assert(prefix+"other_supply_pool_unchanged",kind==4?data.Facilities.cafeIngredients==3:data.Facilities.medicalSupplies==3);
            Assert(prefix+"result_resolved",kind!=4||resultChecked);
            Assert(prefix+"worker_completion_counts",worker.completed==1&&cashier.completed==1);
            Cleanup();yield return null;
        }
    }
    void Cleanup()
    {
        if(surface)surface.RemoveData();if(host)DestroyImmediate(host);if(fixture)DestroyImmediate(fixture);host=null;fixture=null;surface=null;
    }
}
#endif
