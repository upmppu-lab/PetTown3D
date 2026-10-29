using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownChickenRecord
{
    public string id,name,parentA,parentB;public int grown,purchasedDay,fedDay=-1,wateredDay=-1,sex,generation,breedRestDays;
    public bool Adult=>grown>=PetTownPoultryState.GrowDays;
    public bool Lays=>sex!=1;
    public void EnsureSex(){if(sex!=1&&sex!=2)sex=string.IsNullOrEmpty(parentA)?2:StableSex(id);}
    public static int StableSex(string id){int h=17;if(!string.IsNullOrEmpty(id))foreach(char c in id)h=h*31+c;return (h&1)==0?1:2;}
    public float Size=>Mathf.Lerp(.45f,1,grown/(float)PetTownPoultryState.GrowDays);
}
[Serializable] public sealed class PetTownChickenClutch
{
    public string id,henId,roosterId,henName,roosterName;public int generation,remainingDays=2,startedDay;
}
[Serializable] public sealed class PetTownChickenBirth
{
    public string id,childId,childName,henName,roosterName;public int generation,sex,day;
}
[Serializable] public sealed class PetTownCoopRecord
{
    public string farmId;public int lastDay,nestEggs;public float cleanliness=100;
    public int nameSequence,hatched;
    public List<PetTownChickenRecord> birds=new List<PetTownChickenRecord>();
    public List<PetTownChickenClutch> clutches=new List<PetTownChickenClutch>();
    public List<PetTownChickenBirth> births=new List<PetTownChickenBirth>();
}
[Serializable] public sealed class PetTownPoultryState
{
    public const int BuildPrice=700,BirdPrice=80,Capacity=6,GrowDays=2,NestCapacity=12,Limit=10000,EggPrice=35;
    public List<PetTownCoopRecord> coops=new List<PetTownCoopRecord>();public int feed,eggs,revision;
    public PetTownCoopRecord Find(string farmId)=>coops.Find(x=>x.farmId==farmId);
    bool Owns(PetTownCoopRecord coop)=>coop!=null&&coops.Contains(coop);
    public bool Build(string farmId,int day,ref int gold)
    {
        if(string.IsNullOrEmpty(farmId)||day<0||Find(farmId)!=null||gold<BuildPrice)return false;
        gold-=BuildPrice;coops.Add(new PetTownCoopRecord{farmId=farmId,lastDay=day});revision++;return true;
    }
    public bool BuyBird(PetTownCoopRecord coop,int day,ref int gold)=>BuyBirdOfSex(coop,2,day,ref gold);
    public bool BuyBirdOfSex(PetTownCoopRecord coop,int sex,int day,ref int gold)
    {
        if((sex!=1&&sex!=2)||!Owns(coop)||day<coop.lastDay||coop.birds.Count+Reserved(coop)>=Capacity||gold<BirdPrice)return false;
        gold-=BirdPrice;coop.birds.Add(new PetTownChickenRecord{id=Guid.NewGuid().ToString("N"),name=NextName(coop),purchasedDay=day,sex=sex});revision++;return true;
    }
    public bool BuyFeed(ref int gold){if(gold<40||feed>Limit-10)return false;gold-=40;feed+=10;revision++;return true;}
    public bool MakeFeed(PetTownFarmState farm){if(farm==null||farm.produce[0]<1||feed>Limit-10)return false;farm.produce[0]--;farm.revision++;feed+=10;revision++;return true;}
    public int Feed(PetTownCoopRecord coop,int day)
    {
        if(!Owns(coop)||day<coop.lastDay)return 0;int count=coop.birds.FindAll(x=>x.fedDay<day).Count;if(count==0||feed<count)return 0;
        foreach(var bird in coop.birds)if(bird.fedDay<day)bird.fedDay=day;feed-=count;revision++;return count;
    }
    public int Water(PetTownCoopRecord coop,int day)
    {
        if(!Owns(coop)||day<coop.lastDay)return 0;int count=0;foreach(var bird in coop.birds)if(bird.wateredDay<day){bird.wateredDay=day;count++;}if(count>0)revision++;return count;
    }
    public bool Clean(PetTownCoopRecord coop){if(!Owns(coop)||coop.birds.Count==0||coop.cleanliness>=90)return false;coop.cleanliness=100;revision++;return true;}
    public void AdvanceDay(int day,IEnumerable<string> activeFarmIds)
    {
        if(day<0||activeFarmIds==null)return;var active=new HashSet<string>(activeFarmIds);
        foreach(var coop in coops)
        {
            if(!active.Contains(coop.farmId)||day<=coop.lastDay)continue;int previous=coop.lastDay;coop.lastDay=day;
            if(day==previous+1&&coop.cleanliness>=40)foreach(var bird in coop.birds)
            {
                if(bird.fedDay!=previous||bird.wateredDay!=previous||bird.purchasedDay>previous)continue;
                if(bird.Adult){if(bird.Lays&&coop.nestEggs<NestCapacity)coop.nestEggs++;}else bird.grown++;
            }
            AdvanceBreeding(coop,day,previous);if(coop.birds.Count>0)coop.cleanliness=Mathf.Max(0,coop.cleanliness-8*Mathf.Min(30,day-previous));revision++;
        }
    }
    public int Collect(PetTownCoopRecord coop)
    {
        if(!Owns(coop))return 0;int count=Math.Min(coop.nestEggs,Limit-eggs);if(count<=0)return 0;eggs+=count;coop.nestEggs-=count;revision++;return count;
    }
    public bool SellEggs(int amount,ref int gold){if(amount<=0||amount>eggs||(long)gold+(long)amount*EggPrice>int.MaxValue)return false;eggs-=amount;gold+=amount*EggPrice;revision++;return true;}
    public bool MakeIngredients(PetTownFacilityState cafe){if(cafe==null||eggs<2||cafe.cafeIngredients>Limit-2)return false;eggs-=2;cafe.cafeIngredients+=2;revision++;return true;}
    public const int BreedDays=2,RestDays=3,BirthLog=20;
    public static int SellQuote(PetTownChickenRecord bird)=>bird==null?0:Mathf.Max(1,Mathf.RoundToInt(BirdPrice*(bird.Adult?.6f:.3f)));
    static string NextName(PetTownCoopRecord coop){string name;do{coop.nameSequence++;name="닭 "+coop.nameSequence;}while(coop.birds.Exists(x=>x.name==name));return name;}
    static void EnsureLists(PetTownCoopRecord coop){if(coop.clutches==null)coop.clutches=new List<PetTownChickenClutch>();if(coop.births==null)coop.births=new List<PetTownChickenBirth>();}
    public int TotalHatched(){int total=0;foreach(var coop in coops)if(coop!=null)total+=coop.hatched;return total;}
    public int Reserved(PetTownCoopRecord coop){if(coop==null)return 0;EnsureLists(coop);return coop.clutches.Count;}
    public bool BreedingLocked(PetTownCoopRecord coop,string id){if(coop==null)return false;EnsureLists(coop);return coop.clutches.Exists(x=>x.henId==id||x.roosterId==id);}
    static bool SameParent(string a,string b)=>!string.IsNullOrEmpty(a)&&a==b;
    public static bool Related(PetTownChickenRecord a,PetTownChickenRecord b)
    {
        if(a==null||b==null)return false;return a.id==b.parentA||a.id==b.parentB||b.id==a.parentA||b.id==a.parentB||SameParent(a.parentA,b.parentA)||SameParent(a.parentA,b.parentB)||SameParent(a.parentB,b.parentA)||SameParent(a.parentB,b.parentB);
    }
    public bool CanBreed(PetTownCoopRecord coop,PetTownChickenRecord bird,int day,out string reason)
    {
        reason="";if(!Owns(coop)||bird==null||!coop.birds.Contains(bird)||day<coop.lastDay){reason="닭장과 개체를 다시 확인해 주세요.";return false;}
        bird.EnsureSex();if(!bird.Adult){reason="성체가 된 뒤 함께할 수 있어요.";return false;}
        if(BreedingLocked(coop,bird.id)){reason="이미 알을 품고 있는 부모예요.";return false;}
        if(bird.breedRestDays>0){reason="번식 휴식 "+bird.breedRestDays+"일이 남았어요.";return false;}
        if(coop.cleanliness<60||bird.fedDay!=day||bird.wateredDay!=day){reason="오늘 먹이·물과 닭장 청결60 이상이 필요해요.";return false;}
        return true;
    }
    public bool StartBreeding(PetTownCoopRecord coop,string first,string second,int day,out string reason)
    {
        reason="";if(!Owns(coop)){reason="실제 닭장을 선택해 주세요.";return false;}EnsureLists(coop);var a=coop.birds.Find(x=>x.id==first);var b=coop.birds.Find(x=>x.id==second);
        if(a==null||b==null||a==b){reason="서로 다른 성체 두 마리를 선택해 주세요.";return false;}
        if(!CanBreed(coop,a,day,out reason)||!CanBreed(coop,b,day,out reason))return false;
        if(a.sex==b.sex){reason="암탉과 수탉을 한 마리씩 선택해 주세요.";return false;}
        if(Related(a,b)){reason="부모·자식 또는 같은 부모의 자식끼리는 짝지을 수 없어요.";return false;}
        if(coop.birds.Count+coop.clutches.Count>=Capacity){reason="병아리1마리를 위한 빈자리가 필요해요.";return false;}
        var hen=a.sex==2?a:b;var rooster=a.sex==1?a:b;
        coop.clutches.Add(new PetTownChickenClutch{id=Guid.NewGuid().ToString("N"),henId=hen.id,roosterId=rooster.id,henName=hen.name,roosterName=rooster.name,generation=Mathf.Max(a.generation,b.generation)+1,remainingDays=BreedDays,startedDay=day});
        a.breedRestDays=b.breedRestDays=RestDays;revision++;return true;
    }
    public bool CancelBreeding(PetTownCoopRecord coop,string id)
    {
        if(!Owns(coop))return false;EnsureLists(coop);var clutch=coop.clutches.Find(x=>x.id==id);if(clutch==null)return false;coop.clutches.Remove(clutch);revision++;return true;
    }
    public bool SellBird(PetTownCoopRecord coop,string id,ref int gold,out string reason)
    {
        reason="";if(!Owns(coop)){reason="닭장을 다시 선택해 주세요.";return false;}var bird=coop.birds.Find(x=>x.id==id);
        if(bird==null){reason="이미 닭장을 떠난 개체예요.";return false;}
        if(BreedingLocked(coop,id)){reason="알을 품고 있는 부모는 판매할 수 없어요.";return false;}
        int price=SellQuote(bird);if((long)gold+price>int.MaxValue){reason="골드 보관 한도에 도달했어요.";return false;}
        coop.birds.Remove(bird);gold+=price;revision++;return true;
    }
    void AdvanceBreeding(PetTownCoopRecord coop,int day,int previous)
    {
        EnsureLists(coop);int elapsed=Mathf.Clamp(day-previous,0,30);foreach(var bird in coop.birds)bird.breedRestDays=Mathf.Max(0,bird.breedRestDays-elapsed);
        for(int i=coop.clutches.Count-1;i>=0;i--)
        {
            var c=coop.clutches[i];var hen=coop.birds.Find(x=>x.id==c.henId);var rooster=coop.birds.Find(x=>x.id==c.roosterId);
            if(hen==null||rooster==null){coop.clutches.RemoveAt(i);continue;}
            if(day!=previous+1||coop.cleanliness<40||hen.fedDay!=previous||hen.wateredDay!=previous||rooster.fedDay!=previous||rooster.wateredDay!=previous||c.startedDay>previous)continue;
            c.remainingDays=Mathf.Max(0,c.remainingDays-1);if(c.remainingDays>0||coop.birds.Count>=Capacity)continue;
            var chick=new PetTownChickenRecord{id=Guid.NewGuid().ToString("N"),name=NextName(coop),purchasedDay=day,generation=c.generation,parentA=c.henId,parentB=c.roosterId};chick.sex=PetTownChickenRecord.StableSex(chick.id);coop.birds.Add(chick);coop.hatched++;
            coop.births.Insert(0,new PetTownChickenBirth{id=c.id,childId=chick.id,childName=chick.name,henName=c.henName,roosterName=c.roosterName,generation=c.generation,sex=chick.sex,day=day});
            if(coop.births.Count>BirthLog)coop.births.RemoveRange(BirthLog,coop.births.Count-BirthLog);coop.clutches.RemoveAt(i);
        }
    }
    void NormalizeBreeding(PetTownCoopRecord coop)
    {
        EnsureLists(coop);coop.nameSequence=Mathf.Clamp(coop.nameSequence,0,1000000);coop.hatched=Mathf.Clamp(coop.hatched,0,1000000);var ids=new HashSet<string>();var parents=new HashSet<string>();
        for(int i=0;i<coop.clutches.Count;)
        {
            var c=coop.clutches[i];var hen=c==null?null:coop.birds.Find(x=>x.id==c.henId);var rooster=c==null?null:coop.birds.Find(x=>x.id==c.roosterId);
            bool valid=c!=null&&!string.IsNullOrEmpty(c.id)&&hen!=null&&rooster!=null&&hen!=rooster&&hen.Adult&&rooster.Adult&&hen.sex==2&&rooster.sex==1&&!Related(hen,rooster)&&!ids.Contains(c.id)&&!parents.Contains(hen.id)&&!parents.Contains(rooster.id)&&coop.birds.Count+i<Capacity;
            if(!valid){coop.clutches.RemoveAt(i);continue;}
            ids.Add(c.id);parents.Add(hen.id);parents.Add(rooster.id);c.remainingDays=Mathf.Clamp(c.remainingDays,0,BreedDays);c.generation=Mathf.Max(1,c.generation);c.startedDay=Mathf.Max(0,c.startedDay);i++;
        }
        coop.births.RemoveAll(x=>x==null||string.IsNullOrEmpty(x.childId));if(coop.births.Count>BirthLog)coop.births.RemoveRange(BirthLog,coop.births.Count-BirthLog);
    }
    public void Normalize()
    {
        feed=Mathf.Clamp(feed,0,Limit);eggs=Mathf.Clamp(eggs,0,Limit);if(coops==null)coops=new List<PetTownCoopRecord>();var ids=new HashSet<string>();coops.RemoveAll(x=>x==null||string.IsNullOrEmpty(x.farmId)||!ids.Add(x.farmId));var birds=new HashSet<string>();
        foreach(var coop in coops){coop.lastDay=Mathf.Max(0,coop.lastDay);coop.nestEggs=Mathf.Clamp(coop.nestEggs,0,NestCapacity);coop.cleanliness=float.IsFinite(coop.cleanliness)?Mathf.Clamp(coop.cleanliness,0,100):100;if(coop.birds==null)coop.birds=new List<PetTownChickenRecord>();coop.birds.RemoveAll(x=>x==null);if(coop.birds.Count>Capacity)coop.birds.RemoveRange(Capacity,coop.birds.Count-Capacity);foreach(var bird in coop.birds){if(string.IsNullOrEmpty(bird.id)||!birds.Add(bird.id)){bird.id=Guid.NewGuid().ToString("N");birds.Add(bird.id);}bird.grown=Mathf.Clamp(bird.grown,0,GrowDays);bird.purchasedDay=Mathf.Max(0,bird.purchasedDay);if(string.IsNullOrEmpty(bird.name))bird.name="닭";bird.EnsureSex();bird.generation=Mathf.Clamp(bird.generation,0,1000);bird.breedRestDays=Mathf.Clamp(bird.breedRestDays,0,RestDays);}NormalizeBreeding(coop);}
    }
}
