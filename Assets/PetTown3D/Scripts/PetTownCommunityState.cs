using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownCommunityReward {public string residentId;public int day;}
[Serializable] public sealed class PetTownCommunityLot
{
    public string id;public int kind,lastDay,totalVisits;public bool open=true,automatic=true;public int fishStock=PetTownCommunityState.FishCap/2;
    public int[] animals=new int[PetTownCommunityState.AnimalNames.Length];public float fullness=100,cleanliness=100;
    public int expansion,breedBorn;public bool keeper,sick;public int[] breedDays=new int[PetTownCommunityState.AnimalNames.Length];
    public List<PetTownCommunityReward> rewarded=new List<PetTownCommunityReward>();
    public int AnimalCount{get{int total=0;if(animals!=null)foreach(int n in animals)total+=n;return total;}}
    public bool Usable=>open&&(kind!=7||AnimalCount>0&&!sick&&fullness>=35&&cleanliness>=35);
}
[Serializable] public sealed class PetTownCommunityVisit
{
    public string id,buildingId,residentId;public int stage,slot;public float elapsed,rest;
}
[Serializable] public sealed class PetTownCommunityState
{
    public List<PetTownCommunityLot> lots=new List<PetTownCommunityLot>();
    public List<PetTownCommunityVisit> visits=new List<PetTownCommunityVisit>();
    public int food,revision,fish;
    public const int FishCap=12,FishRegenPerDay=3,FishPrice=40,FishingSlots=2;
    public static readonly string[] AnimalNames={"사슴","호랑이","펭귄","코끼리","기린","얼룩말"};
    public static readonly int[] AnimalPrices={500,800,450,1500,1100,700};
    public static readonly int[] AnimalLimits={2,1,3,1,2,3};
    public const int MaxExpansion=2,KeeperCost=800,KeeperLevel=4,TreatCost=300,BreedDaysNeeded=4;
    static readonly int[] ExpandCost={1200,2400};
    public static int ExpandPrice(int level)=>level>=0&&level<ExpandCost.Length?ExpandCost[level]:0;
    public static int Limit(PetTownCommunityLot lot,int species)=>AnimalLimits[species]+(lot==null?0:Mathf.Clamp(lot.expansion,0,MaxExpansion));
    public static string Name(int kind)=>kind==5?"공원":kind==6?"호수":"동물원";
    public void Normalize()
    {
        if(lots==null)lots=new List<PetTownCommunityLot>();if(visits==null)visits=new List<PetTownCommunityVisit>();
        lots.RemoveAll(x=>x==null||string.IsNullOrEmpty(x.id)||x.kind<5||x.kind>7);var ids=new HashSet<string>();lots.RemoveAll(x=>!ids.Add(x.id));
        foreach(var lot in lots){lot.expansion=Mathf.Clamp(lot.expansion,0,MaxExpansion);lot.breedBorn=Mathf.Max(0,lot.breedBorn);if(lot.kind!=7){lot.sick=false;lot.keeper=false;}if(lot.animals==null)lot.animals=new int[AnimalNames.Length];else if(lot.animals.Length!=AnimalNames.Length)Array.Resize(ref lot.animals,AnimalNames.Length);if(lot.breedDays==null)lot.breedDays=new int[AnimalNames.Length];else if(lot.breedDays.Length!=AnimalNames.Length)Array.Resize(ref lot.breedDays,AnimalNames.Length);for(int i=0;i<AnimalNames.Length;i++){lot.animals[i]=Mathf.Clamp(lot.animals[i],0,Limit(lot,i));lot.breedDays[i]=Mathf.Clamp(lot.breedDays[i],0,BreedDaysNeeded);}lot.fullness=float.IsFinite(lot.fullness)?Mathf.Clamp(lot.fullness,0,100):100;lot.cleanliness=float.IsFinite(lot.cleanliness)?Mathf.Clamp(lot.cleanliness,0,100):100;if(lot.rewarded==null)lot.rewarded=new List<PetTownCommunityReward>();lot.rewarded.RemoveAll(x=>x==null);if(lot.rewarded.Count>200)lot.rewarded.RemoveRange(200,lot.rewarded.Count-200);lot.totalVisits=Mathf.Max(0,lot.totalVisits);lot.fishStock=Mathf.Clamp(lot.fishStock,0,FishCap);}
        ids.Clear();visits.RemoveAll(x=>x==null||string.IsNullOrEmpty(x.residentId)||!lots.Exists(l=>l.id==x.buildingId)||!ids.Add(x.residentId));foreach(var v in visits){v.stage=Mathf.Clamp(v.stage,0,3);v.slot=Mathf.Clamp(v.slot,0,PetTownPlaygroundRig.SlotCount-1);v.elapsed=0;v.rest=float.IsFinite(v.rest)?Mathf.Clamp(v.rest,0,15):0;}food=Mathf.Clamp(food,0,9999);fish=Mathf.Clamp(fish,0,9999);
    }
    public PetTownCommunityLot Get(string id,int kind,int day)
    {
        var lot=lots.Find(x=>x.id==id);if(lot==null){lot=new PetTownCommunityLot{id=id,kind=kind,lastDay=day};lots.Add(lot);revision++;}return lot;
    }
    public bool BuyAnimal(PetTownCommunityLot lot,int species,ref int money)
    {
        if(lot==null||!lots.Contains(lot)||lot.kind!=7||species<0||species>=AnimalNames.Length||lot.animals[species]>=Limit(lot,species)||money<AnimalPrices[species])return false;
        money-=AnimalPrices[species];lot.animals[species]++;revision++;return true;
    }
    public bool ExpandZoo(PetTownCommunityLot lot,ref int money){if(lot==null||!lots.Contains(lot)||lot.kind!=7||lot.expansion>=MaxExpansion||money<ExpandCost[lot.expansion])return false;money-=ExpandCost[lot.expansion];lot.expansion++;revision++;return true;}
    public bool HireKeeper(PetTownCommunityLot lot,int level,ref int money){if(lot==null||!lots.Contains(lot)||lot.kind!=7||lot.keeper||level<KeeperLevel||money<KeeperCost)return false;money-=KeeperCost;lot.keeper=true;revision++;return true;}
    public bool TreatZoo(PetTownCommunityLot lot,ref int money){if(lot==null||!lots.Contains(lot)||lot.kind!=7||!lot.sick||money<TreatCost)return false;money-=TreatCost;lot.sick=false;lot.cleanliness=Mathf.Max(lot.cleanliness,60);revision++;return true;}
    // Daily zoo upkeep: a keeper tends the animals with stored food, neglect can make the enclosure sick, and healthy pairs may have young.
    public void ZooDaily(PetTownCommunityLot lot,int day)
    {
        if(lot==null||lot.kind!=7||lot.AnimalCount==0)return;
        if(lot.keeper){int need=lot.AnimalCount;if(food>=need){food-=need;lot.fullness=100;}lot.cleanliness=100;}
        if(!lot.sick&&(lot.cleanliness<35||lot.fullness<30)&&PetTownClinic.Roll(lot.id,day)%100<35)lot.sick=true;
        for(int s=0;s<AnimalNames.Length;s++)
        {
            if(!lot.sick&&lot.fullness>=60&&lot.cleanliness>=60&&lot.animals[s]>=2&&lot.animals[s]<Limit(lot,s)){lot.breedDays[s]++;if(lot.breedDays[s]>=BreedDaysNeeded){lot.breedDays[s]=0;lot.animals[s]++;lot.breedBorn++;}}
            else lot.breedDays[s]=0;
        }
    }
    public bool SellFish(int amount,ref int money){if(amount<=0||amount>fish||(long)money+(long)amount*FishPrice>int.MaxValue)return false;fish-=amount;money+=amount*FishPrice;revision++;return true;}
    public bool FishToIngredients(PetTownFacilityState cafe){if(cafe==null||fish<2||cafe.cafeIngredients>9998)return false;fish-=2;cafe.cafeIngredients+=2;revision++;return true;}
    public bool BuyFood(ref int money){if(money<100||food>9989)return false;money-=100;food+=10;revision++;return true;}
    public bool Feed(PetTownCommunityLot lot){if(lot==null||!lots.Contains(lot)||lot.kind!=7||lot.AnimalCount==0||lot.fullness>=90||food<lot.AnimalCount)return false;food-=lot.AnimalCount;lot.fullness=100;revision++;return true;}
    public bool Clean(PetTownCommunityLot lot){if(lot==null||!lots.Contains(lot)||lot.kind!=7||lot.AnimalCount==0||lot.cleanliness>=90)return false;lot.cleanliness=100;revision++;return true;}
    public void AdvanceDay(int day){foreach(var lot in lots){if(day<=lot.lastDay)continue;int days=Mathf.Min(30,day-lot.lastDay);if(lot.kind==7&&lot.AnimalCount>0){lot.fullness=Mathf.Max(0,lot.fullness-18*days);lot.cleanliness=Mathf.Max(0,lot.cleanliness-12*days);ZooDaily(lot,day);}if(lot.kind==6)lot.fishStock=Mathf.Min(FishCap,lot.fishStock+FishRegenPerDay*days);lot.lastDay=day;lot.rewarded.RemoveAll(x=>x.day<day-1);}revision++;}
    public bool Complete(PetTownCommunityVisit visit,PetTownResidentRecord person,int day)
    {
        if(visit==null||person==null||!visits.Contains(visit)||visit.residentId!=person.id||visit.stage!=2||visit.rest<15)return false;var lot=lots.Find(x=>x.id==visit.buildingId);if(lot==null||!lot.Usable)return false;
        visit.stage=3;visit.elapsed=0;if(lot.rewarded.Exists(x=>x.residentId==person.id&&x.day==day))return false;
        int bonus=lot.kind==7?3:2;if(lot.kind==6&&visit.slot<FishingSlots&&lot.fishStock>0&&fish<9999){lot.fishStock--;fish++;bonus=3;}person.happiness=Mathf.Min(100,person.happiness+bonus);lot.totalVisits++;lot.rewarded.Insert(0,new PetTownCommunityReward{residentId=person.id,day=day});if(lot.rewarded.Count>200)lot.rewarded.RemoveRange(200,lot.rewarded.Count-200);revision++;return true;
    }
}
