using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownResidentPetRecord
{
    public PetTownPetHealth health;
    public bool groomed;public int sex,generation;public string parentA,parentB;
    public string id,name;
    public int species,level,experience,appearance,adoptedDay,lastCafeDay,lastCheckupDay,walkDay,walkXP;
    public void GainServiceExperience(int amount)
    {
        level=Mathf.Clamp(level,1,10);if(amount<=0||level>=10)return;experience+=amount;
        while(level<10&&experience>=20+15*level){experience-=20+15*level;level++;}if(level>=10)experience=0;
    }
}

[Serializable]
public sealed class PetTownResidentRecord
{
    public string id,name,homeId,followPetId,job;
    public int money=-1;
    public int joinedDay,appearance;
    public float happiness;
    public List<PetTownResidentPetRecord> pets=new List<PetTownResidentPetRecord>();
}

[Serializable]
public sealed class PetTownResidentDayRecord
{
    public int day,moved,left,happiness;
}

// A current house snapshot, supplied by the world adapter. No transforms or agents are saved here.
public sealed class PetTownResidentHome
{
    public string id,label;
    public int capacity=2;
    public Vector3 entrance,forward;
}

[Serializable]
public sealed class PetTownResidentState
{
    public int version=1,startDay,lastDay,lowDays,sequence;
    public List<PetTownResidentRecord> people=new List<PetTownResidentRecord>();
    public List<PetTownResidentDayRecord> history=new List<PetTownResidentDayRecord>();
    public int Population=>people.Count;
    public const int PetLimit=10;
    public static float Occupancy(float happiness)=>Mathf.Clamp(.35f+(happiness-20)*.013f,.35f,1f);
    public static int Capacity(List<PetTownResidentHome> homes)
    {
        int total=0;foreach(var h in homes)total+=Mathf.Max(0,h.capacity);return total;
    }
    public int Occupants(string homeId)=>people.FindAll(p=>p.homeId==homeId).Count;
    public void Normalize()
    {
        if(people==null)people=new List<PetTownResidentRecord>();
        if(history==null)history=new List<PetTownResidentDayRecord>();
        people.RemoveAll(p=>p==null);var ids=new HashSet<string>();
        foreach(var p in people)
        {
            if(string.IsNullOrEmpty(p.id)||!ids.Add(p.id)){p.id=Guid.NewGuid().ToString("N");ids.Add(p.id);}
            if(PetTownNames.IsDefaultResidentName(p.name)){var used=new HashSet<string>();foreach(var q in people)if(q!=null&&!PetTownNames.IsDefaultResidentName(q.name))used.Add(q.name);p.name=PetTownNames.Resident(p.appearance,Mathf.Abs(p.id.GetHashCode())%9973,used);}
            if(p.pets==null)p.pets=new List<PetTownResidentPetRecord>();p.pets.RemoveAll(v=>v==null);
            if(!string.IsNullOrEmpty(p.followPetId)&&!p.pets.Exists(v=>v.id==p.followPetId))p.followPetId=null;
            foreach(var pet in p.pets){pet.walkXP=Mathf.Clamp(pet.walkXP,0,5);pet.health?.Normalize();}
            p.happiness=Mathf.Clamp(float.IsNaN(p.happiness)?45:p.happiness,0,100);p.appearance=Mathf.Max(0,p.appearance);
        }
        history.RemoveAll(v=>v==null);if(history.Count>50)history.RemoveRange(50,history.Count-50);
        sequence=Mathf.Max(sequence,people.Count);lowDays=Mathf.Max(0,lowDays);
    }
    public void Migrate(int oldPopulation,int day,float happiness,List<PetTownResidentHome> homes)
    {
        people.Clear();history.Clear();sequence=0;lowDays=0;startDay=lastDay=day;
        for(int i=0;i<Mathf.Max(0,oldPopulation);i++)Add(day,happiness);
        Rehouse(homes);
    }
    HashSet<string> UsedNames(){var set=new HashSet<string>();foreach(var q in people)if(q!=null&&!string.IsNullOrEmpty(q.name))set.Add(q.name);return set;}
    PetTownResidentRecord Add(int day,float happiness)
    {
        sequence++;var p=new PetTownResidentRecord{id=Guid.NewGuid().ToString("N"),name=PetTownNames.Resident((sequence-1)%18,sequence,UsedNames()),joinedDay=day,happiness=Mathf.Clamp(happiness,0,100),appearance=(sequence-1)%18,homeId=""};people.Add(p);return p;
    }
    // Keep valid assignments; displaced/legacy residents wait for a real vacant home.
    public void Rehouse(List<PetTownResidentHome> homes)
    {
        var used=new Dictionary<string,int>();foreach(var h in homes)used[h.id]=0;
        foreach(var p in people)
        {
            var h=homes.Find(v=>v.id==p.homeId);
            if(h!=null&&used[h.id]<h.capacity)used[h.id]++;
            else p.homeId="";
        }
        foreach(var p in people)
        {
            if(!string.IsNullOrEmpty(p.homeId))continue;
            PetTownResidentHome best=null;
            foreach(var h in homes)if(h.capacity>0&&used[h.id]<h.capacity&&(best==null||used[h.id]/(float)h.capacity<used[best.id]/(float)best.capacity))best=h;
            if(best!=null){p.homeId=best.id;used[best.id]++;}
        }
    }
    public PetTownResidentDayRecord AdvanceDay(int day,int happiness,List<PetTownResidentHome> homes)
    {
        if(day<=lastDay)return null;lastDay=day;Rehouse(homes);
        int cap=Capacity(homes),target=Mathf.FloorToInt(cap*Occupancy(happiness));
        int moved=Mathf.Min(Mathf.Max(0,target-Population),Mathf.Max(1,Mathf.CeilToInt(cap*.15f)));
        for(int i=0;i<moved;i++)Add(day,happiness);
        Rehouse(homes);
        lowDays=happiness<35?lowDays+1:0;
        int left=0;
        if(day-startDay>7&&lowDays>=3&&Population>4)
        {
            int count=Mathf.Min(Population-4,Mathf.Max(1,Mathf.CeilToInt(Population*(35-happiness)/100f*.6f)));
            // Leave the oldest established residents last; IDs of everyone else stay unchanged.
            for(int i=0;i<count;i++){people.RemoveAt(people.Count-1);left++;}
        }
        foreach(var p in people)p.happiness=Mathf.Clamp(Mathf.Round(p.happiness*.85f+happiness*.15f),0,100);
        var record=new PetTownResidentDayRecord{day=day,moved=moved,left=left,happiness=happiness};history.Insert(0,record);
        if(history.Count>50)history.RemoveRange(50,history.Count-50);return record;
    }
}
