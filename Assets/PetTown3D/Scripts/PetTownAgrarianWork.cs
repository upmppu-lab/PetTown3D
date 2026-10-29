using System.Collections.Generic;
using UnityEngine;

public enum PetTownAgrarianTask { None,WaterPlot,HarvestPlot,FeedCoop,WaterCoop,CleanCoop,CollectEggs,FeedRanch,WaterRanch,CleanRanch,CollectMilk,CollectWool }
public struct PetTownAgrarianOrder
{
    public PetTownAgrarianTask task;public int plot;
    public PetTownAgrarianOrder(PetTownAgrarianTask task,int plot=-1){this.task=task;this.plot=plot;}
}
public struct PetTownAgrarianOutcome {public bool done;public int xp,amount;public string activity;}

// Existing state methods remain authoritative for resource use, daily care and collection limits.
public static class PetTownAgrarianWork
{
    public static bool IsRole(string role)=>role=="farmer"||role=="rancher";
    public static string Label(PetTownAgrarianTask task)
    {
        switch(task){case PetTownAgrarianTask.WaterPlot:return "밭 물주기";case PetTownAgrarianTask.HarvestPlot:return "작물 수확";case PetTownAgrarianTask.FeedCoop:return "닭 먹이주기";case PetTownAgrarianTask.WaterCoop:return "닭 물주기";case PetTownAgrarianTask.CleanCoop:return "닭장 청소";case PetTownAgrarianTask.CollectEggs:return "달걀 모으기";case PetTownAgrarianTask.FeedRanch:return "가축 먹이주기";case PetTownAgrarianTask.WaterRanch:return "가축 물주기";case PetTownAgrarianTask.CleanRanch:return "목장 청소";case PetTownAgrarianTask.CollectMilk:return "우유 모으기";case PetTownAgrarianTask.CollectWool:return "양털 모으기";default:return "업무 대기";}
    }
    public static bool FarmValid(PetTownFarmState farm,PetTownFarmRecord lot,PetTownAgrarianOrder order,int day)
    {
        if(farm==null||lot==null||!farm.lots.Contains(lot)||!lot.staffService||day<lot.lastDay)return false;
        if(order.task==PetTownAgrarianTask.WaterPlot||order.task==PetTownAgrarianTask.HarvestPlot)
        {
            if(order.plot<0||order.plot>=lot.plots.Count)return false;var p=lot.plots[order.plot];if(p.crop<0||p.crop>=PetTownFarmState.Names.Length)return false;
            return order.task==PetTownAgrarianTask.WaterPlot?p.grown<PetTownFarmState.GrowDays[p.crop]&&p.wateredDay<day:p.grown>=PetTownFarmState.GrowDays[p.crop]&&farm.produce[p.crop]<PetTownFarmState.Limit;
        }
        var state=farm.poultry;var coop=state?.Find(lot.id);if(coop==null||day<coop.lastDay)return false;
        if(order.task==PetTownAgrarianTask.WaterCoop)return coop.birds.Exists(b=>b.wateredDay<day);
        if(order.task==PetTownAgrarianTask.FeedCoop){int n=coop.birds.FindAll(b=>b.fedDay<day).Count;return n>0&&state.feed>=n;}
        if(order.task==PetTownAgrarianTask.CleanCoop)return coop.birds.Count>0&&coop.cleanliness<90;
        return order.task==PetTownAgrarianTask.CollectEggs&&coop.nestEggs>0&&state.eggs<PetTownPoultryState.Limit;
    }
    public static List<PetTownAgrarianOrder> FarmOrders(PetTownFarmState farm,PetTownFarmRecord lot,int day)
    {
        var result=new List<PetTownAgrarianOrder>();
        foreach(var task in new[]{PetTownAgrarianTask.WaterCoop,PetTownAgrarianTask.FeedCoop,PetTownAgrarianTask.CleanCoop}){var order=new PetTownAgrarianOrder(task);if(FarmValid(farm,lot,order,day))result.Add(order);}
        if(lot!=null)foreach(var task in new[]{PetTownAgrarianTask.HarvestPlot,PetTownAgrarianTask.WaterPlot})for(int i=0;i<lot.plots.Count;i++){var order=new PetTownAgrarianOrder(task,i);if(FarmValid(farm,lot,order,day))result.Add(order);}
        var eggs=new PetTownAgrarianOrder(PetTownAgrarianTask.CollectEggs);if(FarmValid(farm,lot,eggs,day))result.Add(eggs);return result;
    }
    public static bool RanchValid(PetTownRanchState ranch,PetTownRanchLot lot,PetTownAgrarianOrder order,int day)
    {
        if(ranch==null||lot==null||!ranch.lots.Contains(lot)||!lot.staffService||day<lot.lastDay)return false;
        if(order.task==PetTownAgrarianTask.WaterRanch)return lot.animals.Exists(a=>a.wateredDay<day);
        if(order.task==PetTownAgrarianTask.FeedRanch){int n=lot.animals.FindAll(a=>a.fedDay<day).Count;return n>0&&ranch.feed>=n;}
        if(order.task==PetTownAgrarianTask.CleanRanch)return lot.animals.Count>0&&lot.cleanliness<90;
        int species=order.task==PetTownAgrarianTask.CollectMilk?0:order.task==PetTownAgrarianTask.CollectWool?1:-1;
        return species>=0&&ranch.products[species]<PetTownRanchState.Limit&&lot.animals.Exists(a=>a.species==species&&a.ready>0);
    }
    public static List<PetTownAgrarianOrder> RanchOrders(PetTownRanchState ranch,PetTownRanchLot lot,int day)
    {
        var result=new List<PetTownAgrarianOrder>();foreach(var task in new[]{PetTownAgrarianTask.WaterRanch,PetTownAgrarianTask.FeedRanch,PetTownAgrarianTask.CleanRanch,PetTownAgrarianTask.CollectMilk,PetTownAgrarianTask.CollectWool}){var order=new PetTownAgrarianOrder(task);if(RanchValid(ranch,lot,order,day))result.Add(order);}return result;
    }
    public static PetTownAgrarianOutcome FinishFarm(PetTownFarmState farm,PetTownFarmRecord lot,PetTownAgrarianOrder order,int day)
    {
        var r=new PetTownAgrarianOutcome();if(!FarmValid(farm,lot,order,day))return r;var coop=farm.poultry?.Find(lot.id);
        switch(order.task)
        {
            case PetTownAgrarianTask.WaterPlot:r.done=farm.Water(lot,order.plot,day);break;
            case PetTownAgrarianTask.HarvestPlot:int crop;r.done=farm.Harvest(lot,order.plot,out crop);if(r.done){r.xp=3;r.activity="harvest";r.amount=1;}break;
            case PetTownAgrarianTask.FeedCoop:r.done=farm.poultry.Feed(coop,day)>0;break;
            case PetTownAgrarianTask.WaterCoop:r.done=farm.poultry.Water(coop,day)>0;break;
            case PetTownAgrarianTask.CleanCoop:r.done=farm.poultry.Clean(coop);break;
            case PetTownAgrarianTask.CollectEggs:r.amount=farm.poultry.Collect(coop);r.done=r.amount>0;r.xp=r.amount*2;r.activity="eggs";break;
        }
        return r;
    }
    public static PetTownAgrarianOutcome FinishRanch(PetTownRanchState ranch,PetTownRanchLot lot,PetTownAgrarianOrder order,int day)
    {
        var r=new PetTownAgrarianOutcome();if(!RanchValid(ranch,lot,order,day))return r;
        switch(order.task)
        {
            case PetTownAgrarianTask.FeedRanch:r.done=ranch.Feed(lot,day)>0;break;
            case PetTownAgrarianTask.WaterRanch:r.done=ranch.Water(lot,day)>0;break;
            case PetTownAgrarianTask.CleanRanch:r.done=ranch.Clean(lot);break;
            case PetTownAgrarianTask.CollectMilk:case PetTownAgrarianTask.CollectWool:int species=order.task==PetTownAgrarianTask.CollectMilk?0:1;r.amount=ranch.Collect(lot,species);r.done=r.amount>0;r.xp=r.amount*3;r.activity=species==0?"milk":"wool";break;
        }
        return r;
    }
    public static Vector3 ServicePoint(bool farm,PetTownAgrarianOrder order)
    {
        if(farm){if(order.task==PetTownAgrarianTask.WaterPlot||order.task==PetTownAgrarianTask.HarvestPlot){var p=PetTownFarmView.PlotPosition(order.plot)+(order.plot==9||order.plot==10?new Vector3(order.plot==9?2.4f:-2.4f,0,0):Vector3.back*1.95f);p.y=.045f;return p;}return PetTownCoopView.Center+Vector3.back*2.9f;}
        switch(order.task){case PetTownAgrarianTask.FeedRanch:return new Vector3(0,.045f,-6.6f);case PetTownAgrarianTask.WaterRanch:return new Vector3(0,.045f,-4.8f);case PetTownAgrarianTask.CleanRanch:return new Vector3(0,.045f,-1.5f);case PetTownAgrarianTask.CollectMilk:return new Vector3(-1.9f,.045f,-3.7f);default:return new Vector3(1.9f,.045f,3.8f);}
    }
}
