#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// 새 일일 의뢰(fish/chick/slot) 순수 상태 검사. 장면/PlayerPrefs/실제 저장을 건드리지 않습니다.
public static class PetTownNewQuestsQA
{
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(k,v)=>{checks[k]=v;if(!v)throw new Exception(k);};
        foreach(var key in new[]{"fish","chick","slot"}){var d=PetTownDailyQuestDefinition.Find(key);test("def_"+key,d!=null&&d.target>=1&&d.gold>0&&d.xp>0&&!string.IsNullOrEmpty(d.name));}
        var keys=new HashSet<string>();bool unique=true;foreach(var d in PetTownDailyQuestDefinition.All)if(!keys.Add(d.key))unique=false;test("def_keys_unique",unique&&PetTownDailyQuestDefinition.All.Length==16);

        var counters=new Dictionary<string,int>{{"fish",5},{"chick",0},{"slot",2}};Func<string,int> activity=k=>counters.ContainsKey(k)?counters[k]:0;
        var board=new PetTownDailyQuestState();test("ensure_day_creates",board.EnsureDay(10,3,new[]{"fish","chick","slot"},activity)&&board.batches.Count==1&&board.batches[0].quests.Count==3);
        var q=board.batches[0].quests;var fish=q.Find(x=>x.key=="fish");var chick=q.Find(x=>x.key=="chick");var slot=q.Find(x=>x.key=="slot");
        test("baselines_snapshot_activity",fish.baseline==5&&chick.baseline==0&&slot.baseline==2&&fish.target==2);
        test("progress_counts_only_after_posting",fish.Progress(activity)==0&&slot.Progress(activity)==0);
        int g,xp,bonus;
        test("claim_before_target_rejected",!board.Claim(fish.id,10,activity,out g,out xp,out bonus));
        counters["fish"]=6;test("partial_progress_not_claimable",fish.Progress(activity)==1&&!board.Claim(fish.id,10,activity,out g,out xp,out bonus));
        counters["fish"]=9;test("progress_clamped_to_target",fish.Progress(activity)==2);
        test("claim_fish",board.Claim(fish.id,10,activity,out g,out xp,out bonus)&&g==110&&xp==4&&bonus==0);
        test("claim_twice_rejected",!board.Claim(fish.id,10,activity,out g,out xp,out bonus));
        counters["chick"]=1;test("claim_chick_next_day",board.Claim(chick.id,11,activity,out g,out xp,out bonus)&&g==130&&xp==5);
        counters["slot"]=3;test("claim_after_expiry_rejected",!board.Claim(slot.id,12,activity,out g,out xp,out bonus));

        var copy=JsonUtility.FromJson<PetTownDailyQuestState>(JsonUtility.ToJson(board));copy.Normalize();
        test("roundtrip_keeps_new_quests",copy.batches.Count==1&&copy.batches[0].quests.Exists(x=>x.key=="fish"&&x.claimed)&&copy.batches[0].quests.Exists(x=>x.key=="chick"));
        var legacy=JsonUtility.FromJson<PetTownDailyQuestState>("{\"version\":1,\"lastDay\":4,\"batches\":[{\"day\":4,\"bonusGold\":260,\"quests\":[{\"id\":\"4_feed\",\"key\":\"feed\",\"baseline\":1,\"target\":3,\"gold\":80,\"xp\":3}]}]}");legacy.Normalize();
        test("legacy_board_unchanged",legacy.batches.Count==1&&legacy.batches[0].quests.Count==1&&legacy.batches[0].quests[0].key=="feed"&&legacy.batches[0].quests[0].baseline==1);
        var odd=new PetTownDailyQuestState();odd.batches.Add(new PetTownDailyQuestBatch{day=20,quests=new List<PetTownDailyQuestRecord>{new PetTownDailyQuestRecord{id="20_x",key="nope",target=1},new PetTownDailyQuestRecord{id="20_slot",key="slot",target=1,gold=90,xp=3}}});odd.Normalize();
        test("unknown_removed_new_kept",odd.batches[0].quests.Count==1&&odd.batches[0].quests[0].key=="slot");
        // 활동 카운터: 한 번에 여러 마리도 기록, 0 이하는 무시
        var ops=new PetTownShopOperationsState();ops.Record("fish",2);ops.Record("fish");ops.Record("fish",0);ops.Record("fish",-3);test("activity_counter_accumulates",ops.Activity("fish")==3&&ops.Activity("chick")==0);
        return checks;
    }
}
#endif
