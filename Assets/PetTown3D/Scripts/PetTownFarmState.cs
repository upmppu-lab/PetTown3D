using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownFarmPlot
{
    public int crop=-1,grown,wateredDay=-1;
}
[Serializable] public sealed class PetTownFarmRecord
{
    public string id;public int lastDay,expansion;public bool staffService;public List<PetTownFarmPlot> plots=new List<PetTownFarmPlot>();
}
[Serializable] public sealed class PetTownFarmState
{
    public const int Kind=9,PlotCount=9,MaxExpansion=2,Limit=10000;
    public PetTownPoultryState poultry;
    public PetTownPoultryState Poultry{get{if(poultry==null)poultry=new PetTownPoultryState();return poultry;}}
    public int version=2,revision;
    public List<PetTownFarmRecord> lots=new List<PetTownFarmRecord>();
    public int[] seeds=new int[CropCount],produce=new int[CropCount];
    public const int CropCount=7;
    // Extra beds: level 1 adds two in the front yard, level 2 adds one beside the barn.
    public static readonly int[] ExpandCost={1500,2500};
    public static int PlotsFor(int expansion)=>PlotCount+(expansion>=1?2:0)+(expansion>=2?1:0);
    // Seasons last seven days; a crop in season is cheaper to sow and sells for more.
    public static readonly string[] SeasonNames={"봄","여름","가을","겨울"};
    public static readonly int[] BestSeason={0,1,0,1,2,2,3};
    public static int SeasonOf(int day)=>((Mathf.Max(1,day)-1)/7)%4;
    public static bool InSeason(int crop,int day)=>ValidCrop(crop)&&BestSeason[crop]==SeasonOf(day);
    public static int SeedCost(int crop,int day)=>InSeason(crop,day)?Mathf.Max(1,Mathf.RoundToInt(SeedPrice[crop]*.8f)):SeedPrice[crop];
    public static int SellValue(int crop,int day)=>InSeason(crop,day)?Mathf.RoundToInt(SellPrice[crop]*1.3f):SellPrice[crop];
    public static string SeasonCrops(int day){var names=new List<string>();for(int i=0;i<CropCount;i++)if(InSeason(i,day))names.Add(Names[i]);return names.Count>0?string.Join("·",names):"없음";}
    public static bool ValidCrop(int crop)=>crop>=0&&crop<CropCount;
    public static readonly string[] Names={"양배추","토마토","당근","옥수수","밀","호박","감자"};
    public static readonly int[] SeedPrice={15,25,20,30,12,40,18},SellPrice={35,55,45,65,30,90,40},GrowDays={2,3,2,3,2,4,2};
    public PetTownFarmRecord Get(string id,int day)
    {
        if(string.IsNullOrEmpty(id))return null;var lot=lots.Find(l=>l.id==id);if(lot!=null)return lot;
        lot=new PetTownFarmRecord{id=id,lastDay=day};for(int i=0;i<PlotCount;i++)lot.plots.Add(new PetTownFarmPlot());lots.Add(lot);revision++;return lot;
    }
    public bool BuySeeds(int crop,int amount,ref int gold,int day=-1)
    {
        if(!ValidCrop(crop)||amount<=0||amount>100||seeds[crop]>Limit-amount)return false;
        int cost=(day<0?SeedPrice[crop]:SeedCost(crop,day))*amount;if(gold<cost)return false;
        gold-=cost;seeds[crop]+=amount;revision++;return true;
    }
    public bool Expand(PetTownFarmRecord lot,ref int gold)
    {
        if(lot==null||!lots.Contains(lot)||lot.expansion>=MaxExpansion||gold<ExpandCost[lot.expansion])return false;
        gold-=ExpandCost[lot.expansion];lot.expansion++;while(lot.plots.Count<PlotsFor(lot.expansion))lot.plots.Add(new PetTownFarmPlot());revision++;return true;
    }
    bool Owns(PetTownFarmRecord lot,int index)=>lot!=null&&lots.Contains(lot)&&index>=0&&index<lot.plots.Count;
    public bool Plant(PetTownFarmRecord lot,int index,int crop)
    {
        if(!Owns(lot,index)||!ValidCrop(crop)||lot.plots[index].crop>=0||seeds[crop]<=0)return false;
        seeds[crop]--;var p=lot.plots[index];p.crop=crop;p.grown=0;p.wateredDay=-1;revision++;return true;
    }
    public bool Water(PetTownFarmRecord lot,int index,int day)
    {
        if(!Owns(lot,index)||day<lot.lastDay)return false;var p=lot.plots[index];
        if(!ValidCrop(p.crop)||p.wateredDay>=day||p.grown>=GrowDays[p.crop])return false;p.wateredDay=day;revision++;return true;
    }
    public bool Harvest(PetTownFarmRecord lot,int index,out int crop)
    {
        crop=-1;if(!Owns(lot,index))return false;var p=lot.plots[index];
        if(!ValidCrop(p.crop)||p.grown<GrowDays[p.crop]||produce[p.crop]>=Limit)return false;
        crop=p.crop;produce[crop]++;p.crop=-1;p.grown=0;p.wateredDay=-1;revision++;return true;
    }
    public bool Sell(int crop,int amount,ref int gold,int day=-1)
    {
        if(!ValidCrop(crop)||amount<=0||amount>Limit||produce[crop]<amount)return false;
        long value=(long)(day<0?SellPrice[crop]:SellValue(crop,day))*amount;if((long)gold+value>int.MaxValue)return false;
        produce[crop]-=amount;gold+=(int)value;revision++;return true;
    }
    public bool MakeIngredients(PetTownFacilityState facilities,int amount)
    {
        if(facilities==null||amount<=0||amount>Limit-facilities.cafeIngredients||produce[0]+produce[1]<amount)return false;
        int first=Math.Min(amount,produce[0]);produce[0]-=first;produce[1]-=amount-first;facilities.cafeIngredients+=amount;revision++;return true;
    }
    public static int GrainFeedYield(int crop)=>crop==3?20:crop==4?10:0;
    bool SpendGrain(int crop,int currentFeed,out int amount)
    {
        amount=GrainFeedYield(crop);if(amount<=0||produce==null||produce.Length<=crop||produce[crop]<=0||currentFeed>Limit-amount)return false;
        produce[crop]--;revision++;return true;
    }
    public bool MakePoultryFeed(int crop,PetTownPoultryState target)
    {
        if(target==null||!SpendGrain(crop,target.feed,out int amount))return false;target.feed+=amount;target.revision++;return true;
    }
    public bool MakeRanchFeed(int crop,PetTownRanchState target)
    {
        if(target==null||!SpendGrain(crop,target.feed,out int amount))return false;target.feed+=amount;target.revision++;return true;
    }
    public bool AdvanceDay(int day)
    {
        bool changed=false;foreach(var lot in lots)
        {
            if(day<=lot.lastDay)continue;int previous=lot.lastDay;lot.lastDay=day;
            // Only a single consecutive played day counts. No growth backfill after a skipped date.
            if(day!=previous+1)continue;
            foreach(var p in lot.plots)if(ValidCrop(p.crop)&&p.grown<GrowDays[p.crop]&&p.wateredDay==previous){p.grown++;changed=true;}
        }
        if(changed)revision++;return changed;
    }
    public void Normalize()
    {
        version=Math.Max(2,version);if(poultry!=null)poultry.Normalize();
        seeds=Fix(seeds);produce=Fix(produce);if(lots==null)lots=new List<PetTownFarmRecord>();var ids=new HashSet<string>();lots.RemoveAll(l=>l==null||string.IsNullOrEmpty(l.id)||!ids.Add(l.id));
        foreach(var lot in lots)
        {
            lot.lastDay=Mathf.Max(0,lot.lastDay);if(lot.plots==null)lot.plots=new List<PetTownFarmPlot>();lot.expansion=Mathf.Clamp(lot.expansion,0,MaxExpansion);int want=PlotsFor(lot.expansion);while(lot.plots.Count<want)lot.plots.Add(new PetTownFarmPlot());if(lot.plots.Count>want)lot.plots.RemoveRange(want,lot.plots.Count-want);
            for(int i=0;i<want;i++){if(lot.plots[i]==null)lot.plots[i]=new PetTownFarmPlot();var p=lot.plots[i];if(!ValidCrop(p.crop)){p.crop=-1;p.grown=0;p.wateredDay=-1;}else p.grown=Mathf.Clamp(p.grown,0,GrowDays[p.crop]);}
        }
    }
    static int[] Fix(int[] values){var result=new int[CropCount];if(values!=null)for(int i=0;i<Math.Min(CropCount,values.Length);i++)result[i]=Mathf.Clamp(values[i],0,Limit);return result;}
}
