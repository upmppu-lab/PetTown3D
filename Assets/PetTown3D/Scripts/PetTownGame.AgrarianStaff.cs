using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class PetTownGame
{
    Transform AgrarianWorkplace(string id,bool farm)
    {
        if(farm){foreach(var view in FarmRoots())if(view.Id==id)return view.transform;}
        else foreach(var view in RanchRoots())if(view.Id==id)return view.transform;
        return null;
    }
    void PickAgrarianEmployeeJob(PetTownEmployeeRecord p,EmployeeActor a)
    {
        a.status="농장 업무 대기";bool farm=p.role=="farmer";
        if(string.IsNullOrEmpty(p.facilityId)){a.status="근무지 지정 필요";return;}
        var root=AgrarianWorkplace(p.facilityId,farm);if(!root){a.status="근무지 확인 필요";return;}
        List<PetTownAgrarianOrder> orders;
        if(farm){var lot=Progression.Farm.lots.Find(x=>x.id==p.facilityId);if(lot==null||!lot.staffService){a.status="농장 자동 업무 꺼짐";return;}orders=PetTownAgrarianWork.FarmOrders(Progression.Farm,lot,day);}
        else{var lot=Progression.Ranch.lots.Find(x=>x.id==p.facilityId);if(lot==null||!lot.staffService){a.status="목장 자동 업무 꺼짐";return;}orders=PetTownAgrarianWork.RanchOrders(Progression.Ranch,lot,day);}
        foreach(var order in orders)
        {
            string key=p.facilityId+(farm?(order.plot>=0?":plot:"+order.plot:":coop"):"~herd");if(TargetReserved(key))continue;
            if(AssignAgrarianEmployeeJob(p,a,root,order,key))return;
        }
        if(orders.Count==0)a.status="작업 대기 · 먹이·창고 확인";
    }
    bool AssignAgrarianEmployeeJob(PetTownEmployeeRecord p,EmployeeActor a,Transform root,PetTownAgrarianOrder order,string key)
    {
        if(!a.agent||!a.agent.enabled||!a.agent.isOnNavMesh)return false;
        bool farm=p.role=="farmer";var origin=root.TransformPoint(PetTownAgrarianWork.ServicePoint(farm,order));
        for(int i=0;i<9;i++)
        {
            var candidate=origin+(i==0?Vector3.zero:Quaternion.Euler(0,(i-1)*45,0)*Vector3.forward*.25f);NavMeshHit hit;
            if(!NavMesh.SamplePosition(candidate,out hit,.3f,a.agent.areaMask)||Mathf.Abs(hit.position.y-origin.y)>.4f||IsWater(hit.position.x,hit.position.z))continue;
            var local=root.InverseTransformPoint(hit.position);if(Mathf.Abs(local.x)>(farm?8.7f:10.7f)||Mathf.Abs(local.z)>(farm?9.7f:10.7f))continue;
            var path=new NavMeshPath();if(!a.agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete||!CompanionDryPath(path))continue;
            a.agent.isStopped=false;if(!a.agent.SetPath(path))continue;
            a.employeeId=p.id;a.task=EmployeeTask.Agrarian;a.target=key;a.workplaceId=p.facilityId;a.plotIndex=order.plot;a.agrarianTask=order.task;a.facilityRevision=constructionWorldRevision;a.goal=hit.position;a.subject=origin;a.progress=0;a.assigned=Time.time;a.status=PetTownAgrarianWork.Label(order.task)+" 지점으로 이동";return true;
        }
        a.status="농장·목장 통로 확인 필요";return false;
    }
    bool AgrarianEmployeeTargetValid(EmployeeActor a)
    {
        var p=Operations.employees.Find(x=>x.id==a.employeeId);
        if(p==null||p.paused||p.unpaid>0||!PetTownAgrarianWork.IsRole(p.role)||p.facilityId!=a.workplaceId||a.facilityRevision!=constructionWorldRevision)return false;
        bool farm=p.role=="farmer";var root=AgrarianWorkplace(a.workplaceId,farm);if(!root)return false;
        var order=new PetTownAgrarianOrder(a.agrarianTask,a.plotIndex);var current=root.TransformPoint(PetTownAgrarianWork.ServicePoint(farm,order));if((current-a.subject).sqrMagnitude>.01f)return false;
        return farm?PetTownAgrarianWork.FarmValid(Progression.Farm,Progression.Farm.lots.Find(x=>x.id==a.workplaceId),order,day):PetTownAgrarianWork.RanchValid(Progression.Ranch,Progression.Ranch.lots.Find(x=>x.id==a.workplaceId),order,day);
    }
    bool FinishAgrarianEmployeeJob(EmployeeActor a)
    {
        if(!AgrarianEmployeeTargetValid(a))return false;var p=Operations.employees.Find(x=>x.id==a.employeeId);var order=new PetTownAgrarianOrder(a.agrarianTask,a.plotIndex);
        var result=p.role=="farmer"?PetTownAgrarianWork.FinishFarm(Progression.Farm,Progression.Farm.lots.Find(x=>x.id==a.workplaceId),order,day):PetTownAgrarianWork.FinishRanch(Progression.Ranch,Progression.Ranch.lots.Find(x=>x.id==a.workplaceId),order,day);
        if(!result.done)return false;
        if(result.xp>0)money+=Progression.AddExperience(result.xp);if(!string.IsNullOrEmpty(result.activity))Operations.Record(result.activity,result.amount);
        if(p.role=="farmer"){foreach(var v in FarmRoots())if(v.Id==a.workplaceId)v.Refresh();RefreshPoultry(a.workplaceId);}else RefreshRanch(a.workplaceId);
        return true;
    }
}
