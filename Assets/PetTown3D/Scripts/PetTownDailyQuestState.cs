using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PetTownDailyQuestDefinition
{
    public string key,name;public int target,gold,xp;
    public static readonly PetTownDailyQuestDefinition[] All={
        new PetTownDailyQuestDefinition{key="feed",name="오늘의 식사 돌봄",target=3,gold=80,xp=3},
        new PetTownDailyQuestDefinition{key="wash",name="보송보송 목욕 시간",target=2,gold=80,xp=3},
        new PetTownDailyQuestDefinition{key="play",name="신나는 놀이 시간",target=2,gold=90,xp=3},
        new PetTownDailyQuestDefinition{key="adopt",name="새 가족을 만나요",target=1,gold=150,xp=5},
        new PetTownDailyQuestDefinition{key="delivery",name="도착한 물품 정리",target=1,gold=80,xp=3},
        new PetTownDailyQuestDefinition{key="groom",name="단정한 펫 친구",target=1,gold=120,xp=4},
        new PetTownDailyQuestDefinition{key="cafe",name="따뜻한 카페 한 상",target=1,gold=120,xp=4},
        new PetTownDailyQuestDefinition{key="clinic",name="건강한 하루 검진",target=1,gold=150,xp=5},
        new PetTownDailyQuestDefinition{key="harvest",name="농장의 신선한 수확",target=3,gold=120,xp=4},
        new PetTownDailyQuestDefinition{key="eggs",name="둥지의 신선한 달걀",target=1,gold=80,xp=3},
        new PetTownDailyQuestDefinition{key="milk",name="목장의 신선한 우유",target=1,gold=90,xp=3},
        new PetTownDailyQuestDefinition{key="wool",name="포근한 양털 모으기",target=1,gold=110,xp=4},
        new PetTownDailyQuestDefinition{key="meat",name="목장의 출하 준비",target=3,gold=100,xp=4},
        new PetTownDailyQuestDefinition{key="fish",name="호숫가 낚시 한 판",target=2,gold=110,xp=4},
        new PetTownDailyQuestDefinition{key="chick",name="새 병아리의 탄생",target=1,gold=130,xp=5},
        new PetTownDailyQuestDefinition{key="slot",name="펫 전시 자리 정리",target=1,gold=90,xp=3},
        new PetTownDailyQuestDefinition{key="zoo",name="동물원 식구 돌보기",target=2,gold=110,xp=4},
        new PetTownDailyQuestDefinition{key="born",name="새 생명의 소식",target=1,gold=160,xp=5}};
    public static PetTownDailyQuestDefinition Find(string key)=>Array.Find(All,d=>d.key==key);
}
[Serializable] public sealed class PetTownDailyQuestRecord
{
    public string id,key;public int baseline,target,gold,xp;public bool claimed;
    public int Progress(Func<string,int> activity)=>Mathf.Clamp(activity(key)-baseline,0,target);
}
[Serializable] public sealed class PetTownDailyQuestBatch
{
    public int day,bonusGold;public bool bonusClaimed;public List<PetTownDailyQuestRecord> quests=new List<PetTownDailyQuestRecord>();
}
[Serializable] public sealed class PetTownDailyQuestState
{
    public int version=1,lastDay=-1;
    public List<PetTownDailyQuestBatch> batches=new List<PetTownDailyQuestBatch>();
    public void Normalize()
    {
        if(batches==null)batches=new List<PetTownDailyQuestBatch>();var days=new HashSet<int>();var ids=new HashSet<string>();
        batches.RemoveAll(b=>b==null||b.day<0||!days.Add(b.day));
        foreach(var b in batches)
        {
            b.bonusGold=Mathf.Clamp(b.bonusGold,0,6200);if(b.quests==null)b.quests=new List<PetTownDailyQuestRecord>();
            b.quests.RemoveAll(q=>q==null||string.IsNullOrEmpty(q.id)||PetTownDailyQuestDefinition.Find(q.key)==null||!ids.Add(q.id));
            if(b.quests.Count>5)b.quests.RemoveRange(5,b.quests.Count-5);
            foreach(var q in b.quests){q.baseline=Mathf.Max(0,q.baseline);q.target=Mathf.Clamp(q.target,1,100);q.gold=Mathf.Clamp(q.gold,0,1000);q.xp=Mathf.Clamp(q.xp,0,100);}
            lastDay=Mathf.Max(lastDay,b.day);
        }
        batches.RemoveAll(b=>b.day<lastDay-1);
    }
    public bool EnsureDay(int day,int level,IList<string> eligible,Func<string,int> activity)
    {
        if(day<0||day<=lastDay||eligible==null||eligible.Count==0)return false;
        var unique=new List<string>();foreach(var key in eligible)if(PetTownDailyQuestDefinition.Find(key)!=null&&!unique.Contains(key))unique.Add(key);if(unique.Count==0)return false;
        lastDay=day;batches.RemoveAll(b=>b.day<day-1);
        var batch=new PetTownDailyQuestBatch{day=day,bonusGold=200+60*Mathf.Clamp(level,1,100)};
        int start=day%unique.Count;
        for(int i=0;i<Mathf.Min(5,unique.Count);i++)
        {
            var def=PetTownDailyQuestDefinition.Find(unique[(start+i)%unique.Count]);
            batch.quests.Add(new PetTownDailyQuestRecord{id=day+"_"+def.key,key=def.key,baseline=Mathf.Max(0,activity(def.key)),target=def.target,gold=def.gold,xp=def.xp});
        }
        batches.Add(batch);return true;
    }
    public bool Claim(string id,int day,Func<string,int> activity,out int gold,out int xp,out int bonus)
    {
        gold=xp=bonus=0;if(day<lastDay||string.IsNullOrEmpty(id))return false;
        foreach(var b in batches)
        {
            if(day<b.day||day>b.day+1)continue;
            var q=b.quests.Find(v=>v.id==id);if(q==null||q.claimed||q.Progress(activity)<q.target)continue;
            q.claimed=true;gold=q.gold;xp=q.xp;
            if(!b.bonusClaimed&&b.quests.Count==5&&b.quests.TrueForAll(v=>v.claimed)){b.bonusClaimed=true;bonus=b.bonusGold;}
            return true;
        }
        return false;
    }
}
