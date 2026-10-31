using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownAdoptionRecord
{
    public bool groomed;public int sex,generation;public string parentA,parentB;public string petId,name,adopterId,adopterName;public int species,level,day,income;public float condition;
}
[Serializable]
public sealed class PetTownProgressionState
{
    public int version=1,playerLevel=1,playerExperience,sequence;
    public PetTownExplorationState exploration;
    public int petGrowthVersion;
    public int environmentVersion;
    public PetTownTimeWeatherState environment;
    public PetTownTimeWeatherState Environment(int day){if(environmentVersion<1||environment==null){environment=new PetTownTimeWeatherState{weatherDay=day};environmentVersion=1;}return environment;}
    public PetTownCommunityState community;
    public PetTownCommunityState Community{get{if(community==null)community=new PetTownCommunityState();return community;}}
    public PetTownRanchState ranch;
    public PetTownRanchState Ranch{get{if(ranch==null)ranch=new PetTownRanchState();return ranch;}}
    public PetTownFarmState farm;
    public PetTownFarmState Farm{get{if(farm==null)farm=new PetTownFarmState();return farm;}}
    public int[] food={10,10,6,6};
    public int shelfStock=-1;
    public float serviceHappiness;public bool autoFeeder,autoCleaner;public List<PetTownSlotDirt> slotDirt=new List<PetTownSlotDirt>();public List<PetTownHouseLevel> houseLevels=new List<PetTownHouseLevel>();public List<PetTownVillageRequest> requests=new List<PetTownVillageRequest>();public int requestDay=-1;
    public bool stockInitialized;
    public int facilityVersion;public PetTownFacilityState facilities;
    public PetTownFacilityState Facilities{get{if(facilityVersion<1||facilities==null){facilities=new PetTownFacilityState();facilityVersion=1;}return facilities;}}
    public int breedingVersion;public PetTownBreedingState breeding;
    public PetTownBreedingState Breeding{get{if(breedingVersion<1||breeding==null){breeding=new PetTownBreedingState();breedingVersion=1;}return breeding;}}
    public int operationsVersion;
    public PetTownShopOperationsState operations;
    public PetTownShopOperationsState Operations(int day){if(operationsVersion<1||operations==null){operations=new PetTownShopOperationsState{lastPayrollDay=day};operationsVersion=1;}return operations;}
    public int residentVersion;
    public PetTownResidentState residents;
    public PetTownPetCareState care=new PetTownPetCareState();
    public List<PetTownAdoptionRecord> adoptions=new List<PetTownAdoptionRecord>();
    public string playerName="",townName="";
    public static readonly string[] Species={"강아지","고양이","토끼","페럿","기니피그","햄스터","쥐","친칠라","물고기"};
    public static readonly string[] FoodNames={"강아지","고양이","토끼·설치류","페럿"};
    public static int FoodLineOf(int species){return species<0?0:species<4?species:2;}
    public static readonly string[] Notes={"산책과 놀이를 좋아하는 강아지예요.","느긋한 휴식과 깨끗한 공간을 좋아해요.","조용한 공간에서 편안히 쉬게 해 주세요.","호기심이 많고 활발한 페럿이에요.","순하고 먹는 걸 좋아하는 기니피그예요.","쳇바퀴를 좋아하는 작고 부지런한 햄스터예요.","똑똑하고 사람을 잘 따르는 쥐예요.","보드라운 털을 가진 조용한 친칠라예요.","수족관에서 느긋하게 헤엄치는 알록달록한 물고기예요."};
    public static readonly int[] FoodPrice={40,40,25,35};
    public int ExperienceNeeded=>RequiredExperience(playerLevel);
    public static int RequiredExperience(int level)=>level<=30?Mathf.RoundToInt(50*Mathf.Pow(level,1.6f)):Mathf.RoundToInt(50*Mathf.Pow(30,1.6f)+280*(level-30));
    public int AddExperience(int amount)
    {
        if(amount<=0||playerLevel>=100)return 0;playerExperience+=amount;int reward=0;
        while(playerLevel<100&&playerExperience>=ExperienceNeeded){playerExperience-=ExperienceNeeded;playerLevel++;reward+=50*playerLevel;}
        if(playerLevel>=100)playerExperience=0;return reward;
    }
    public void Normalize()
    {
        if(environmentVersion>=1&&environment!=null)environment.Normalize();
        if(community!=null)community.Normalize();
        if(ranch!=null)ranch.Normalize();
        if(farm!=null)farm.Normalize();
        if(facilityVersion>=1&&facilities!=null)facilities.Normalize();
        if(operationsVersion>=1&&operations!=null)operations.Normalize();
        if(residentVersion>=1&&residents!=null)residents.Normalize();
        if(petGrowthVersion<1){if(residents!=null&&residents.people!=null)foreach(var owner in residents.people)if(owner.pets!=null)foreach(var pet in owner.pets)pet.level=Mathf.Max(PetTownPetCareRecord.AdultLevel,pet.level);petGrowthVersion=1;}
        playerLevel=Mathf.Clamp(playerLevel,1,100);playerExperience=Mathf.Max(0,playerExperience);serviceHappiness=Mathf.Clamp(serviceHappiness,-20,15);
        if(care==null)care=new PetTownPetCareState();if(care.pets==null)care.pets=new List<PetTownPetCareRecord>();care.pets.RemoveAll(p=>p==null);
        foreach(var p in care.pets)p.Normalize();
        { var usedPets=PetNamesInUse(true);int k=0;foreach(var p in care.pets)if(!p.displayOnly&&PetTownNames.IsDefaultPetName(p.nickname,Species))p.nickname=PetTownNames.Pet(++k+care.pets.Count,usedPets); }
        if(breedingVersion>=1&&breeding!=null)breeding.Normalize(care.pets);else foreach(var p in care.pets)p.breedingId=null;
        if(adoptions==null)adoptions=new List<PetTownAdoptionRecord>();
        if(food==null||food.Length!=4){var old=food;food=new int[4];if(old!=null)Array.Copy(old,food,Math.Min(4,old.Length));}for(int i=0;i<food.Length;i++)food[i]=Mathf.Max(0,food[i]);
    }
    HashSet<string> PetNamesInUse(bool skipDefault=false){var set=new HashSet<string>();if(care!=null&&care.pets!=null)foreach(var q in care.pets)if(q!=null&&!string.IsNullOrEmpty(q.nickname)&&!(skipDefault&&PetTownNames.IsDefaultPetName(q.nickname,Species)))set.Add(q.nickname);return set;}
    public void MigrateStock(int dogs,int cats,int rabbits)
    {
        if(stockInitialized)return;foreach(var p in care.pets)p.displayOnly=true;
        AddPets(0,Mathf.Max(0,dogs),true);AddPets(1,Mathf.Max(0,cats),true);AddPets(2,Mathf.Max(0,rabbits),true);stockInitialized=true;
    }
    public void AddPets(int species,int count,bool mature)
    {
        for(int i=0;i<count;i++){sequence++;care.pets.Add(new PetTownPetCareRecord{id=Guid.NewGuid().ToString("N"),nickname=PetTownNames.Pet(sequence,PetNamesInUse()),speciesKind=species,appearance=(sequence-1)%2,growthRulesVersion=1,petLevel=mature?PetTownPetCareRecord.AdultLevel:1,growth=mature?1:0});care.pets[care.pets.Count-1].EnsureSex();}
    }
    public PetTownPetCareRecord AddChosenPet(int species,int sex,int appearance)
    {
        sequence++;var pet=new PetTownPetCareRecord{id=Guid.NewGuid().ToString("N"),nickname=PetTownNames.Pet(sequence,PetNamesInUse()),speciesKind=species,appearance=appearance,growthRulesVersion=1,petLevel=1,growth=0,sex=sex};
        care.pets.Add(pet);return pet;
    }
    public int Stock(int species)=>care.pets.FindAll(p=>!p.displayOnly&&p.speciesKind==species).Count;
    public int Quote(PetTownPetCareRecord pet,int basePrice)=>Mathf.Max(1,Mathf.RoundToInt(basePrice*(.6f+.6f*pet.Condition/100)*(1+.18f*(pet.Level-1))*(pet.groomed?1.2f:1)*pet.GradeMultiplier*.8f));
    public bool Adopt(string id,int requestedSpecies,int basePrice,int day,out int income,out int reward)
    {
        income=reward=0;var p=care.pets.Find(v=>v.id==id);
        if(p==null||!p.Adoptable||p.speciesKind<0||p.speciesKind>=Species.Length||p.speciesKind!=requestedSpecies)return false;
        income=Quote(p,basePrice);care.pets.Remove(p);adoptions.Insert(0,new PetTownAdoptionRecord{petId=p.id,name=p.nickname,species=p.speciesKind,level=p.Level,day=day,income=income,condition=p.Condition,groomed=p.groomed,sex=p.sex,generation=p.generation,parentA=p.parentA,parentB=p.parentB});
        if(adoptions.Count>50)adoptions.RemoveRange(50,adoptions.Count-50);
        Operations(day).Record("adopt");Operations(day).discovered[p.speciesKind]=true;
        serviceHappiness=Mathf.Clamp(serviceHappiness+.5f,-20,15);reward=AddExperience(Mathf.RoundToInt(basePrice/8f));return true;
    }
    public bool AdoptToResident(string id,string residentId,int basePrice,int day,out int income,out int reward)
    {
        income=reward=0;var owner=residents!=null?residents.people.Find(p=>p.id==residentId):null;
        var pet=care.pets.Find(p=>p.id==id);
        if(owner==null||owner.pets==null||owner.pets.Count>=PetTownResidentState.PetLimit||pet==null)return false;
        foreach(var person in residents.people)if(person.pets!=null&&person.pets.Exists(p=>p.id==id))return false;
        if(!Adopt(id,pet.speciesKind,basePrice,day,out income,out reward))return false;
        owner.pets.Add(new PetTownResidentPetRecord{id=pet.id,name=pet.nickname,species=pet.speciesKind,level=pet.Level,experience=pet.experience,appearance=pet.appearance,adoptedDay=day,groomed=pet.groomed,sex=pet.sex,generation=pet.generation,parentA=pet.parentA,parentB=pet.parentB});
        owner.happiness=Mathf.Min(100,owner.happiness+8);adoptions[0].adopterId=owner.id;adoptions[0].adopterName=owner.name;return true;
    }
    public void Tick(float seconds,bool night=false){foreach(var p in care.pets){p.Tick(seconds,night);if(p.displayOnly)continue;if(autoFeeder&&p.fullness<35&&p.speciesKind>=0&&p.speciesKind<Species.Length){int line=FoodLineOf(p.speciesKind);if(food[line]>0){food[line]--;p.fullness=Mathf.Min(100,p.fullness+55);}}if(autoCleaner&&p.cleanliness<35)p.cleanliness=100;}}
}