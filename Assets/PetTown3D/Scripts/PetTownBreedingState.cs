using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownBreedingRecord
{
    public string id,parentA,parentB,nameA,nameB;public int species,litter,generation;public float remaining,duration;
}
[Serializable]
public sealed class PetTownBirthRecord
{
    public string parentA,parentB;public int day,species,count;public List<string> children=new List<string>();
}
// Additive save data. Existing animals are never removed to enforce a breeding-space limit.
[Serializable]
public sealed class PetTownBreedingState
{
    public List<PetTownBreedingRecord> nests=new List<PetTownBreedingRecord>();
    public List<PetTownBirthRecord> births=new List<PetTownBirthRecord>();
    public bool auto;public float autoClock;
    public const int AutoLevel=5,AutoMaxNests=2;
    public static void Inherit(PetTownPetCareRecord pa,PetTownPetCareRecord pb,PetTownPetCareRecord child,int day)
    {
        uint r=PetTownClinic.Roll(child.id,day);int bonusRoll=(int)(r%100),fromA=(int)(r/100%100),mutate=(int)(r/10000%100),which=(int)(r/1000000%3);
        int baseStars=Mathf.RoundToInt((Mathf.Max(1,pa.stars)+Mathf.Max(1,pb.stars))*.5f);int bonus=bonusRoll<22?1:bonusRoll>=90?-1:0;
        int gene=0;if(pa.gene>0&&fromA<40)gene=pa.gene;else if(pb.gene>0&&fromA>=60)gene=pb.gene;
        if(gene==0&&mutate<(pa.gene>0||pb.gene>0?12:6))gene=1+which;
        child.gene=gene;child.stars=Mathf.Clamp(baseStars+bonus+(gene>0?1:0),1,5);
    }
    public bool TryAuto(PetTownProgressionState state,int capacity,out string reason)
    {
        reason="";if(nests.Count>=AutoMaxNests){reason="교배 중인 쌍이 가득해요.";return false;}
        PetTownPetCareRecord bestA=null,bestB=null;int best=-1;string why;
        foreach(var a in state.care.pets)
        {
            if(a.sex!=1||!CanBreed(a,out why))continue;
            foreach(var b in state.care.pets)
            {
                if(b.sex!=2||b.speciesKind!=a.speciesKind||!CanBreed(b,out why))continue;
                int score=Mathf.Max(1,a.stars)+Mathf.Max(1,b.stars)+(a.gene>0?2:0)+(b.gene>0?2:0);if(score>best){best=score;bestA=a;bestB=b;}
            }
        }
        if(bestA==null){reason="교배할 수 있는 짝이 없어요.";return false;}
        return Start(state,bestA.id,bestB.id,capacity,bestA.speciesKind==2?2:1,out reason);
    }
    public int Reserved{get{int n=0;foreach(var p in nests)n+=p.litter;return n;}}
    public static float Duration(int kind)=>kind==1?175:kind==3?200:150;
    public static bool CanBreed(PetTownPetCareRecord p,out string reason)
    {
        reason="";
        if(p==null){reason="펫을 다시 선택해 주세요.";return false;}
        if(p.displayOnly||p.speciesKind<0||p.speciesKind>=PetTownProgressionState.Species.Length){reason="전시 동물은 교배 대상이 아닙니다.";return false;}
        if(p.growth<1){reason="성체가 된 뒤 함께할 수 있어요.";return false;}
        if(p.busy||p.IsBreeding){reason="진행 중인 놀이 또는 교배가 있어요.";return false;}
        if(p.breedingCooldown>0){reason="휴식이 "+Mathf.CeilToInt(p.breedingCooldown)+"초 더 필요해요.";return false;}
        if(p.fullness<40||p.Condition<50){reason="배부름40%와 컨디션50% 이상이 필요해요.";return false;}
        if(p.sex<1||p.sex>2){reason="성별 정보를 확인해 주세요.";return false;}
        return true;
    }
    public bool Start(PetTownProgressionState state,string first,string second,int capacity,int requestedLitter,out string reason)
    {
        reason="";var a=state.care.pets.Find(p=>p.id==first);var b=state.care.pets.Find(p=>p.id==second);
        if(first==second||a==null||b==null||a.speciesKind!=b.speciesKind||a.sex==b.sex){reason="같은 종류의 서로 다른 암수를 선택해 주세요.";return false;}
        if(!CanBreed(a,out reason)||!CanBreed(b,out reason))return false;
        if(nests.Exists(p=>p.parentA==first||p.parentB==first||p.parentA==second||p.parentB==second)){reason="이미 교배 중인 펫이에요.";return false;}
        int stock=state.care.pets.FindAll(p=>!p.displayOnly).Count,free=Mathf.Max(0,capacity-stock-Reserved);
        if(free==0){reason="새끼를 돌볼 공간이 부족해요. 분양하거나 매장을 넓혀 주세요.";return false;}
        int count=Mathf.Min(free,Mathf.Clamp(requestedLitter,1,a.speciesKind==2?3:2));
        var nest=new PetTownBreedingRecord{id=Guid.NewGuid().ToString("N"),parentA=a.id,parentB=b.id,nameA=a.nickname,nameB=b.nickname,species=a.speciesKind,litter=count,generation=Mathf.Max(a.generation,b.generation)+1,duration=Duration(a.speciesKind),remaining=Duration(a.speciesKind)};
        nests.Add(nest);a.breedingId=b.breedingId=nest.id;a.breedingCooldown=b.breedingCooldown=600;return true;
    }
    public bool Cancel(string id,List<PetTownPetCareRecord> pets)
    {
        var n=nests.Find(p=>p.id==id);if(n==null)return false;nests.Remove(n);Release(n,pets);return true;
    }
    static void Release(PetTownBreedingRecord n,List<PetTownPetCareRecord> pets){foreach(var p in pets)if(p.breedingId==n.id)p.breedingId=null;}
    public int Tick(float seconds,PetTownProgressionState state,int day)
    {
        if(seconds<=0)return 0;int born=0;
        for(int i=nests.Count-1;i>=0;i--)
        {
            var n=nests[i];if(!state.care.pets.Exists(p=>p.id==n.parentA)||!state.care.pets.Exists(p=>p.id==n.parentB)){Release(n,state.care.pets);nests.RemoveAt(i);continue;}
            n.remaining=Mathf.Max(0,n.remaining-seconds);if(n.remaining>0)continue;
            int first=state.care.pets.Count;state.AddPets(n.species,n.litter,false);var record=new PetTownBirthRecord{day=day,species=n.species,count=n.litter,parentA=n.nameA,parentB=n.nameB};
            for(int k=first;k<state.care.pets.Count;k++){var p=state.care.pets[k];p.parentA=n.parentA;p.parentB=n.parentB;p.generation=n.generation;p.fullness=80;p.cleanliness=100;record.children.Add(p.id);
                var pa=state.care.pets.Find(x=>x.id==n.parentA);var pb=state.care.pets.Find(x=>x.id==n.parentB);if(pa!=null&&pb!=null)Inherit(pa,pb,p,day);}
            born+=n.litter;births.Insert(0,record);if(births.Count>30)births.RemoveRange(30,births.Count-30);Release(n,state.care.pets);nests.RemoveAt(i);
        }
        return born;
    }
    public void Normalize(List<PetTownPetCareRecord> pets)
    {
        if(nests==null)nests=new List<PetTownBreedingRecord>();if(births==null)births=new List<PetTownBirthRecord>();foreach(var p in pets)p.breedingId=null;
        var used=new HashSet<string>();var ids=new HashSet<string>();
        for(int i=0;i<nests.Count;)
        {
            var n=nests[i];var a=n==null?null:pets.Find(p=>p.id==n.parentA);var b=n==null?null:pets.Find(p=>p.id==n.parentB);
            if(n==null||string.IsNullOrEmpty(n.id)||ids.Contains(n.id)||a==null||b==null||a==b||a.displayOnly||b.displayOnly||n.species<0||n.species>=PetTownProgressionState.Species.Length||a.speciesKind!=n.species||b.speciesKind!=n.species||used.Contains(a.id)||used.Contains(b.id)){nests.RemoveAt(i);continue;}
            ids.Add(n.id);used.Add(a.id);used.Add(b.id);n.litter=Mathf.Clamp(n.litter,1,n.species==2?3:2);n.generation=Mathf.Max(1,n.generation);n.duration=Mathf.Max(1,n.duration);n.remaining=Mathf.Clamp(n.remaining,0,n.duration);a.breedingId=b.breedingId=n.id;i++;
        }
        births.RemoveAll(p=>p==null);if(births.Count>30)births.RemoveRange(30,births.Count-30);autoClock=Mathf.Max(0,autoClock);
    }
}
