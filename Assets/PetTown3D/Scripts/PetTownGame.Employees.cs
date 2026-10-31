using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public partial class PetTownGame
{
    static string StaffModelFor(string role,int appearance)
    {
        switch(role)
        {
            case "chef": return "Characters/Staff_Chef/Staff_Chef";
            case "vet": return "Characters/Staff_Doctor/Staff_Doctor";
            case "care": return "Characters/Staff_Nurse/Staff_Nurse";
            case "cashier": return "Characters/Staff_Manager/Staff_Manager";
            case "groomer": case "clean": return "Characters/Staff_Woman/Staff_Woman";
            default: return "Characters/Staff_Man/Staff_Man";
        }
    }
    enum EmployeeTask {None,Feed,Wash,Groom,Parcel,Sale,FacilityService,FacilityPayment,Agrarian,Sweep}
    sealed class EmployeeActor
    {
        public GameObject root;public NavMeshAgent agent;public TextMeshPro label;
        public EmployeeTask task;public string target,employeeId,workplaceId;public int plotIndex;public PetTownAgrarianTask agrarianTask;public bool facilityStarted;public int facilityRevision;public int token,species;public float progress,assigned,nextPick;public Vector3 goal,previous,subject;public bool hasPrevious;public string status="출근 중";public Vector3 home;public bool leaving,offDuty;public float leaveStart,postCheck;
    }
    readonly Dictionary<string,EmployeeActor> employeeActors=new Dictionary<string,EmployeeActor>();
    float nextEmployeeSpawn;
    string EmployeeStatus(PetTownEmployeeRecord p)
    {
        if(p.paused)return "휴무";if(p.unpaid>0)return "급여 정산 대기";
        return employeeActors.TryGetValue(p.id,out var a)?a.status:"출근 준비";
    }
    void UpdateEmployees(float seconds)
    {
        var removed=new List<string>();foreach(var pair in employeeActors)if(!pair.Value.root||!Operations.employees.Exists(p=>p.id==pair.Key)){CancelEmployeeJob(pair.Value);RemoveResidentActor(pair.Value.root);removed.Add(pair.Key);}foreach(var id in removed)employeeActors.Remove(id);
        if(Operations.employees.Count==0)return;
        foreach(var p in Operations.employees)
        {
            if(!employeeActors.TryGetValue(p.id,out var actor))
            {
                if(Time.unscaledTime<nextEmployeeSpawn)continue;nextEmployeeSpawn=Time.unscaledTime+.2f;
                Vector3 point;if(!ResidentSpawnPoint(new PetTownResidentRecord{appearance=p.appearance},new List<PetTownResidentHome>(),out point))continue;
                int before=villagers.Count;staffModelOverride=StaffModelFor(p.role,p.appearance);SpawnVillager(point,p.appearance);staffModelOverride=null;if(villagers.Count<=before)continue;
                var root=villagers[villagers.Count-1];villagers.RemoveAt(villagers.Count-1);root.name="STAFF_"+p.id;MarkNavMeshIgnore(root);
                var labelObject=new GameObject("StaffRole");labelObject.transform.SetParent(root.transform,false);labelObject.transform.localPosition=Vector3.up*(GetHeroTargetHeight()+.3f);
                var label=labelObject.AddComponent<TextMeshPro>();label.font=PetTownCozyLibrary.Current.style.font;label.fontSize=2.2f;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.93f,.66f);label.rectTransform.sizeDelta=new Vector2(3,.6f);
                actor=new EmployeeActor{root=root,agent=root.GetComponent<NavMeshAgent>(),label=label,employeeId=p.id,home=root.transform.position};employeeActors[p.id]=actor;
            }
            var agent=actor.agent;if(!agent||!actor.root)continue;
            if(cam&&actor.label){actor.label.transform.rotation=cam.transform.rotation;actor.label.text=p.name+"\n"+EmployeeStatus(p);}
            bool staffNight=TimeWeather.Night;
            if(actor.offDuty){if(staffNight)continue;actor.offDuty=false;actor.leaving=false;actor.root.SetActive(true);if(agent)agent.Warp(actor.home);actor.hasPrevious=false;actor.status="출근 중";continue;}
            bool moving=actor.hasPrevious&&seconds>.00001f&&Vector3.Distance(actor.previous,actor.root.transform.position)/seconds>.12f;
            actor.previous=actor.root.transform.position;actor.hasPrevious=true;UpdateVillagerAnim(actor.root,moving,false);
            if(!agent.isOnNavMesh){actor.status="경로 확인 중";continue;}
            if(p.paused||p.unpaid>0){CancelEmployeeJob(actor);actor.status=p.paused?"휴무":"급여 정산 대기";continue;}
            agent.speed=1.6f+Mathf.Min(1,p.level*.1f);
            if(staffNight&&actor.task==EmployeeTask.None&&!IsFacilityEmployeeTask(actor.task))
            {
                if(!actor.leaving){actor.leaving=true;actor.leaveStart=Time.time;agent.SetDestination(actor.home);}
                actor.status="퇴근 중";
                if(!agent.pathPending&&(Vector3.Distance(actor.root.transform.position,actor.home)<2f||Time.time-actor.leaveStart>20f)){CancelEmployeeJob(actor);actor.offDuty=true;actor.status="퇴근";actor.root.SetActive(false);}
                continue;
            }
            if(!staffNight)actor.leaving=false;
            if(IsFacilityEmployeeTask(actor.task)){UpdateFacilityEmployeeJob(p,actor,seconds);continue;}
            if(actor.task==EmployeeTask.None)
            {
                if(Time.unscaledTime<actor.nextPick)continue;actor.nextPick=Time.unscaledTime+1;PickEmployeeJob(p,actor);continue;
            }
            if(Time.time-actor.assigned>(actor.task==EmployeeTask.Agrarian?120:40)){CancelEmployeeJob(actor);actor.status="경로 확인 중";continue;}
            if(!EmployeeTargetValid(actor)){CancelEmployeeJob(actor);continue;}
            if(actor.task==EmployeeTask.Feed||actor.task==EmployeeTask.Wash||actor.task==EmployeeTask.Groom)
            {
                var subject=Progression.care.pets.Find(v=>v.id==actor.target);var table=actor.task==EmployeeTask.Groom?GroomingStation():null;
                Vector3 current=table?table.transform.position:EmployeePetPoint(subject);
                if((current-actor.subject).sqrMagnitude>1f){float started=actor.assigned;AssignEmployeeJob(actor,actor.task,actor.target,current);actor.assigned=started;continue;}
            }
            if(agent.pathPending)continue;
            if(agent.hasPath&&agent.pathStatus!=NavMeshPathStatus.PathComplete){CancelEmployeeJob(actor);actor.status="통로가 필요해요";continue;}
            if(Vector3.Distance(actor.root.transform.position,actor.goal)>agent.stoppingDistance+.4f)continue;
            actor.progress+=seconds;actor.status=(actor.task==EmployeeTask.Agrarian?PetTownAgrarianWork.Label(actor.agrarianTask):"업무 중")+" "+Mathf.RoundToInt(Mathf.Clamp01(actor.progress/PetTownShopOperationsState.WorkDuration(p.level))*100)+"%";
            if(actor.progress<PetTownShopOperationsState.WorkDuration(p.level))continue;
            bool done=FinishEmployeeJob(actor);if(done){p.completed++;progressionDirty=true;SaveProgressionSilent();}CancelEmployeeJob(actor);actor.nextPick=Time.unscaledTime+1;
        }
    }
    void CancelEmployeeJob(EmployeeActor a){ReleaseFacilityEmployee(a);if(a.agent&&a.agent.isOnNavMesh)a.agent.ResetPath();a.task=EmployeeTask.None;a.target=null;a.progress=0;a.status="업무 대기";}
    Vector3 EmployeeServicePoint()
    {
        var room=shop?shop.GetComponent<PetTownInteriorState>():null;
        return room?room.transform.TransformPoint(new Vector3(room.LocalBounds.center.x,room.FloorOffset,room.LocalBounds.yMin+2)):shopPosition;
    }
    Vector3 EmployeePetPoint(PetTownPetCareRecord p)
    {
        for(int i=0;i<livePets.Count;i++)if(livePets[i].root&&livePets[i].root.name+"_"+i==p.id)return livePets[i].root.transform.position;
        return EmployeeServicePoint();
    }
    bool AssignEmployeeJob(EmployeeActor a,EmployeeTask task,string target,Vector3 origin)
    {
        var station=task==EmployeeTask.Groom?GroomingStation():null;float baseRadius=station?Mathf.Max(station.Footprint.x,station.Footprint.y)*.5f+.65f:.8f;
        for(int i=0;i<24;i++)
        {
            float angle=i*Mathf.PI/4;Vector3 candidate=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(baseRadius+i/8*.5f);
            NavMeshHit hit;if(!NavMesh.SamplePosition(candidate,out hit,.7f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-origin.y)>.6f||IsWater(hit.position.x,hit.position.z))continue;
            var path=new NavMeshPath();if(!a.agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            a.subject=origin;a.goal=hit.position;a.task=task;a.target=target;a.progress=0;a.assigned=Time.time;a.agent.SetPath(path);a.status="작업 지점으로 이동";return true;
        }
        a.status="통로가 필요해요";return false;
    }
    bool TargetReserved(string id)
    {
        foreach(var a in employeeActors.Values)if(a.task!=EmployeeTask.None&&a.target==id)return true;return false;
    }
    void PickEmployeeJob(PetTownEmployeeRecord p,EmployeeActor a)
    {
        a.status="업무 대기";
        if(PetTownAgrarianWork.IsRole(p.role)){PickAgrarianEmployeeJob(p,a);return;}
        if(p.role=="chef"||p.role=="vet"||!string.IsNullOrEmpty(p.facilityId)){PickFacilityEmployeeJob(p,a);return;}
        if(p.role=="porter")
        {
            var parcel=Operations.deliveries.Find(v=>v.Arrived&&!v.IsPet&&!TargetReserved(v.id));if(parcel!=null)AssignEmployeeJob(a,EmployeeTask.Parcel,parcel.id,EmployeeServicePoint());return;
        }
        if(p.role=="cashier")
        {
            if(orderPet<0||adoptionSelectionOpen)return;var pet=Progression.care.pets.Find(v=>v.Adoptable&&v.speciesKind==orderPet&&!TargetReserved(v.id));
            if(pet!=null&&AssignEmployeeJob(a,EmployeeTask.Sale,pet.id,EmployeeServicePoint())){a.token=adoptionGeneration;a.species=orderPet;}return;
        }
        if(p.role=="groomer"&&!GroomingReady()){a.status="미용대·통로 확인 필요";return;}
        if(p.role=="groomer"&&Operations.kits<=0){a.status="미용 키트 부족";return;}
        if(p.role=="clean"){var mess=petMess.Find(x=>x.root&&!TargetReserved(x.id));if(mess!=null&&AssignEmployeeJob(a,EmployeeTask.Sweep,mess.id,mess.root.transform.position))return;var dh=DirtyHabitat(50f);if(dh!=null&&AssignEmployeeJob(a,EmployeeTask.Sweep,"hab:"+dh.Id,dh.transform.position))return;}
        if(p.role=="care")
        {
            PetTownPetCareRecord urgent=null;bool wash=false;float bestU=0;
            foreach(var pet in Progression.care.pets)
            {
                if(pet.busy||pet.IsBreeding||TargetReserved(pet.id))continue;
                float uf=pet.fullness<60&&OperationsFood(pet)>0?(60-pet.fullness)/60f+.15f:0,uc=pet.cleanliness<55?(55-pet.cleanliness)/55f:0,u=Mathf.Max(uf,uc);
                if(u>bestU){bestU=u;urgent=pet;wash=uc>uf;}
            }
            if(urgent!=null)AssignEmployeeJob(a,wash?EmployeeTask.Wash:EmployeeTask.Feed,urgent.id,EmployeePetPoint(urgent));
            else{var dh2=DirtyHabitat(60f);if(dh2!=null)AssignEmployeeJob(a,EmployeeTask.Sweep,"hab:"+dh2.Id,dh2.transform.position);}
            return;
        }
        PetTownPetCareRecord best=null;
        foreach(var pet in Progression.care.pets)
        {
            if(pet.busy||pet.IsBreeding||TargetReserved(pet.id))continue;
            bool candidate=p.role=="clean"?pet.cleanliness<55:p.role=="care"?pet.fullness<60&&OperationsFood(pet)>0:p.role=="groomer"&&!pet.groomed&&pet.growth>=1&&pet.speciesKind>=0&&pet.speciesKind<PetTownProgressionState.Species.Length&&Operations.kits>0;
            if(candidate&&(best==null||(p.role=="clean"?pet.cleanliness<best.cleanliness:pet.fullness<best.fullness)))best=pet;
        }
        if(best==null)return;
        var station=p.role=="groomer"?GroomingStation():null;AssignEmployeeJob(a,p.role=="clean"?EmployeeTask.Wash:p.role=="care"?EmployeeTask.Feed:EmployeeTask.Groom,best.id,station?station.transform.position:EmployeePetPoint(best));
    }
    int OperationsFood(PetTownPetCareRecord pet)=>Progression.food[PetTownProgressionState.FoodLineOf(Mathf.Clamp(pet.speciesKind,0,PetTownProgressionState.Species.Length-1))];
    bool EmployeeTargetValid(EmployeeActor a)
    {
        if(a.task==EmployeeTask.Agrarian)return AgrarianEmployeeTargetValid(a);
        if(a.task==EmployeeTask.Parcel)return Operations.deliveries.Exists(p=>p.id==a.target&&p.Arrived);
        if(a.task==EmployeeTask.Sweep)return MessExists(a.target);
        var pet=Progression.care.pets.Find(p=>p.id==a.target);if(pet==null||pet.busy||pet.IsBreeding)return false;
        if(a.task==EmployeeTask.Sale)return !adoptionSelectionOpen&&orderPet==a.species&&a.token==adoptionGeneration&&Time.time<=orderDeadline&&pet.Adoptable;
        if(a.task==EmployeeTask.Groom)return GroomingReady()&&!pet.groomed&&pet.growth>=1&&Operations.kits>0;
        return a.task==EmployeeTask.Feed?pet.NeedsCare(0)&&OperationsFood(pet)>0:pet.NeedsCare(1);
    }
    bool FinishEmployeeJob(EmployeeActor a)
    {
        if(!EmployeeTargetValid(a))return false;
        if(a.task==EmployeeTask.Agrarian)return FinishAgrarianEmployeeJob(a);
        if(a.task==EmployeeTask.Parcel)return Operations.Collect(a.target,Progression);
        if(a.task==EmployeeTask.Sweep)return RemoveMess(a.target);
        if(a.task==EmployeeTask.Sale)return CompleteIndividualAdoption(a.target,a.species,a.token);
        var pet=Progression.care.pets.Find(p=>p.id==a.target);
        if(a.task==EmployeeTask.Groom){if(!Operations.Groom(pet,GroomingReady(),out string reason))return false;money+=Progression.AddExperience(4);return true;}
        int action=a.task==EmployeeTask.Feed?0:1;ApplyProgressionCare(pet,action);return true;
    }
}
