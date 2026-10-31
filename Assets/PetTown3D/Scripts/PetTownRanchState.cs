using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownRanchAnimal
{
    public int sex,generation,breedRestDays;public string parentA,parentB;
    public string SexLabel=>sex==1?"수컷":"암컷";
    public void EnsureSex(){if(sex==1||sex==2)return;if(species==0){sex=2;return;}sex=StableSex(id);}
    public static int StableSex(string id){uint hash=2166136261;foreach(char c in id??"")hash=unchecked((hash^c)*16777619);return 1+(int)(hash&1);}
    public string id,name;public int species,grown,boughtDay,fedDay=-1,wateredDay=-1,productProgress,ready;
    public bool Adult=>grown>=2;public float Size=>Mathf.Lerp(.45f,1,grown/2f);
}
[Serializable] public sealed class PetTownRanchLot
{
    public string id;public int lastDay;public bool staffService;public float cleanliness=100;public List<PetTownRanchAnimal> animals=new List<PetTownRanchAnimal>();
}
[Serializable] public sealed partial class PetTownRanchState
{
    public const int Limit=10000;public int feed,revision;public int[] products=new int[3];public List<PetTownRanchLot> lots=new List<PetTownRanchLot>();
    public static readonly string[] Species={"젖소","양","돼지"},Products={"우유","양털","고기"};
    public static readonly int[] Prices={350,250,150},Capacity={3,4,3},SellPrices={45,70,75};
    public PetTownRanchLot Get(string id,int day){if(string.IsNullOrEmpty(id))return null;var lot=lots.Find(x=>x.id==id);if(lot==null){lot=new PetTownRanchLot{id=id,lastDay=day};lots.Add(lot);revision++;}return lot;}
    bool Owns(PetTownRanchLot lot)=>lot!=null&&lots.Contains(lot);
    public bool BuyAnimal(PetTownRanchLot lot,int species,int day,ref int gold)
    {
        if(!Owns(lot)||species<0||species>2||day<lot.lastDay||lot.animals.FindAll(x=>x.species==species).Count+Reserved(lot,species)>=Capacity[species]||gold<Prices[species])return false;
        gold-=Prices[species];lot.animals.Add(new PetTownRanchAnimal{id=Guid.NewGuid().ToString("N"),name=NextAnimalName(lot,species),species=species,boughtDay=day});lot.animals[lot.animals.Count-1].EnsureSex();revision++;return true;
    }
    public bool BuyFeed(ref int gold){if(gold<60||feed>Limit-10)return false;gold-=60;feed+=10;revision++;return true;}
    public bool MakeFeed(PetTownFarmState farm){if(farm==null||farm.produce[0]<1||feed>Limit-10)return false;farm.produce[0]--;farm.revision++;feed+=10;revision++;return true;}
    public int Feed(PetTownRanchLot lot,int day){if(!Owns(lot)||day<lot.lastDay)return 0;int count=lot.animals.FindAll(x=>x.fedDay<day).Count;if(count==0||feed<count)return 0;foreach(var animal in lot.animals)if(animal.fedDay<day)animal.fedDay=day;feed-=count;revision++;return count;}
    public int Water(PetTownRanchLot lot,int day){if(!Owns(lot)||day<lot.lastDay)return 0;int count=0;foreach(var animal in lot.animals)if(animal.wateredDay<day){animal.wateredDay=day;count++;}if(count>0)revision++;return count;}
    public bool Clean(PetTownRanchLot lot){if(!Owns(lot)||lot.animals.Count==0||lot.cleanliness>=90)return false;lot.cleanliness=100;revision++;return true;}
    public void AdvanceDay(int day,IEnumerable<string> activeIds)
    {
        if(activeIds==null)return;var active=new HashSet<string>(activeIds);foreach(var lot in lots){if(!active.Contains(lot.id)||day<=lot.lastDay)continue;int previous=lot.lastDay;lot.lastDay=day;if(day==previous+1&&lot.cleanliness>=40)foreach(var animal in lot.animals){if(animal.fedDay!=previous||animal.wateredDay!=previous||animal.boughtDay>previous)continue;if(!animal.Adult)animal.grown++;else if(animal.species<2&&!(animal.species==0&&animal.sex==1)&&animal.ready==0){animal.productProgress++;if(animal.productProgress>=(animal.species==0?1:2))animal.ready=1;}}if(day==previous+1)AdvanceBreedingDay(lot,day,previous);if(lot.animals.Count>0)lot.cleanliness=Mathf.Max(0,lot.cleanliness-8*Mathf.Min(30,day-previous));revision++;}
    }
    public int Collect(PetTownRanchLot lot,int species)
    {
        if(!Owns(lot)||species<0||species>1)return 0;int count=0;foreach(var animal in lot.animals)if(animal.species==species&&animal.ready>0&&products[species]<Limit){animal.ready=0;animal.productProgress=0;products[species]++;count++;}if(count>0)revision++;return count;
    }
    // Shipment consumes the selected adult pig. Meat is never a passive output from a retained pig.
    public bool ShipPig(PetTownRanchLot lot,string animalId)
    {
        if(!Owns(lot)||string.IsNullOrEmpty(animalId)||products[2]>Limit-3)return false;var pig=lot.animals.Find(x=>x.id==animalId&&x.species==2&&x.Adult);if(pig==null||BreedingLocked(lot,animalId))return false;
        lot.animals.Remove(pig);products[2]+=3;revision++;return true;
    }
    public bool Sell(int species,int amount,ref int gold){if(species<0||species>2||amount<=0||amount>products[species]||(long)gold+(long)amount*SellPrices[species]>int.MaxValue)return false;gold+=amount*SellPrices[species];products[species]-=amount;revision++;return true;}
    public bool MakeIngredients(PetTownFacilityState cafe){if(cafe==null||products[0]<2||cafe.cafeIngredients>Limit-3)return false;products[0]-=2;cafe.cafeIngredients+=3;revision++;return true;}
    public void Normalize()
    {
        feed=Mathf.Clamp(feed,0,Limit);var fixedProducts=new int[3];if(products!=null)for(int i=0;i<Math.Min(3,products.Length);i++)fixedProducts[i]=Mathf.Clamp(products[i],0,Limit);products=fixedProducts;if(lots==null)lots=new List<PetTownRanchLot>();var ids=new HashSet<string>();lots.RemoveAll(x=>x==null||string.IsNullOrEmpty(x.id)||!ids.Add(x.id));var animalIds=new HashSet<string>();
        foreach(var lot in lots){lot.lastDay=Mathf.Max(0,lot.lastDay);lot.cleanliness=float.IsFinite(lot.cleanliness)?Mathf.Clamp(lot.cleanliness,0,100):100;if(lot.animals==null)lot.animals=new List<PetTownRanchAnimal>();lot.animals.RemoveAll(x=>x==null||x.species<0||x.species>2);var counts=new int[3];lot.animals.RemoveAll(x=>++counts[x.species]>Capacity[x.species]);foreach(var a in lot.animals){if(string.IsNullOrEmpty(a.id)||!animalIds.Add(a.id)){a.id=Guid.NewGuid().ToString("N");animalIds.Add(a.id);}a.EnsureSex();a.generation=Mathf.Max(0,a.generation);a.breedRestDays=Mathf.Clamp(a.breedRestDays,0,3);a.grown=Mathf.Clamp(a.grown,0,2);a.boughtDay=Mathf.Max(0,a.boughtDay);a.ready=Mathf.Clamp(a.ready,0,1);a.productProgress=Mathf.Clamp(a.productProgress,0,a.species==0?1:2);if(string.IsNullOrEmpty(a.name))a.name=Species[a.species];}}
        NormalizeBreeding();
    }
}
