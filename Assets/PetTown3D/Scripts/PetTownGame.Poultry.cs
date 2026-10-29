using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    void RefreshPoultry(string id){foreach(var farm in FarmRoots())if(farm.Id==id){var view=farm.GetComponent<PetTownCoopView>();if(view)view.RefreshNow();}}
    bool PoultryReady(string id,out PetTownCoopRecord coop)
    {
        coop=null;if(!FarmReady(id,out var farm))return false;coop=Progression.Farm.Poultry.Find(id);return coop!=null;
    }
    string PoultryStatus(PetTownCoopRecord coop){int r=Progression.Farm.Poultry.Reserved(coop);return "닭 "+coop.birds.Count+" /6마리"+(r>0?" (알 "+r+")":"")+" · 청결 "+Mathf.RoundToInt(coop.cleanliness)+"%";}
    bool BuildPoultry(string id)
    {
        if(!FarmReady(id,out var lot))return false;var farm=FarmRoots().Find(x=>x.Id==id);var view=farm?farm.GetComponent<PetTownCoopView>():null;string reason="";
        if(!view||!view.CanBuild(out reason)){Message(view?reason:"닭장 부지를 확인해 주세요.");return false;}
        if(!Progression.Farm.Poultry.Build(id,day,ref money)){Message("골드와 기존 닭장을 확인해 주세요.");return false;}
        constructionWorldRevision++;progressionDirty=true;RefreshPoultry(id);SaveProgressionSilent();Message("농장 뒤편에 닭장을 지었어요.");return true;
    }
    void OpenPoultry(string id)
    {
        if(!FarmReady(id,out var lot))return;var state=Progression.Farm.Poultry;var coop=state.Find(id);var entries=new List<PetTownCatalogEntry>();
        if(coop==null)
        {
            var build=OperationsEntry(0,"농장 닭장 짓기","700 골드 · 정원6마리","기존 밭9칸은 유지됩니다","닭장 짓기","농장 뒤편 오른쪽 빈 공간에 작은 집·울타리·먹이와 물통을 놓습니다.\n병아리는 별도로 맞이합니다. 공간이 비어 있어야 지을 수 있어요.",0);build.Thumbnail=PetTownPoultryLibrary.Current?.thumbnail;entries.Add(build);entries.Add(OperationsEntry(7,"밭 관리로 돌아가기","씨앗·작물 관리","농장 밭9칸","밭 보기","닭장 건설 없이도 기존 농장은 계속 이용할 수 있어요.",3));
            catalogWindow.Show("농장 · 닭장 건설",entries,e=>{if(e.Key==7)OpenFarmLot(id);else{BuildPoultry(id);OpenPoultry(id);}},()=>{},()=>{});return;
        }
        var buyBird=OperationsEntry(1,"암탉 병아리 맞이하기",PoultryStatus(coop),"1마리80 골드 · 정원6마리","1마리 맞이하기","산란계 병아리를 맞이합니다.\n먹이·물을 주고 깨끗하게 유지한2일 동안 자라 성체가 됩니다. 성체는 먹이와 물을 준 다음 날 달걀을 낳습니다.",9);buyBird.PriceProvider=()=>PoultryStatus(coop);entries.Add(buyBird);
        var buyRooster=OperationsEntry(10,"수탉 병아리 맞이하기",PoultryStatus(coop),"1마리80 골드 · 번식용","1마리 맞이하기","번식에 필요한 수탉 병아리를 맞이합니다.\n수탉은 달걀을 낳지 않지만 암탉과 짝을 지어 병아리를 얻을 수 있어요.",9);buyRooster.PriceProvider=()=>PoultryStatus(coop);entries.Add(buyRooster);
        var feedEntry=OperationsEntry(2,"닭 먹이주기","먹이 "+state.feed+"개 보유","오늘 먹이지 않은 닭당1개 사용","먹이주기","병아리를 나중에 추가하면 그 개체에도 따로 먹이를 줘야 합니다. 같은 날 이미 먹인 닭의 먹이는 다시 소모하지 않아요.",2);feedEntry.PriceProvider=()=>"먹이 "+state.feed+"개 · 오늘 "+coop.birds.FindAll(x=>x.fedDay==day).Count+" / "+coop.birds.Count+"마리 급식";entries.Add(feedEntry);
        var water=OperationsEntry(3,"깨끗한 물주기","오늘 "+coop.birds.FindAll(x=>x.wateredDay==day).Count+" / "+coop.birds.Count+"마리 급수","물은 무료로 보충합니다","물주기","먹이와 물을 모두 준 날만 성장하거나 산란합니다.",2);water.PriceProvider=()=>"오늘 "+coop.birds.FindAll(x=>x.wateredDay==day).Count+" / "+coop.birds.Count+"마리 급수";entries.Add(water);
        var clean=OperationsEntry(4,"닭장 청소","청결 "+Mathf.RoundToInt(coop.cleanliness)+"%","청결90% 미만일 때 청소","청소하기","닭이 있으면 매일 청결이8% 줄어듭니다.40% 미만이면 성장과 산란을 쉽니다.",1);clean.PriceProvider=()=>"청결 "+Mathf.RoundToInt(coop.cleanliness)+"%";entries.Add(clean);
        var eggs=OperationsEntry(5,"달걀 모으기","둥지 "+coop.nestEggs+" /12개","판매·카페 재료로 사용","달걀 모으기","성체1마리당 하루1개까지 낳습니다. 둥지가12개로 차면 더 쌓이지 않습니다. 날짜를 건너뛴 생산을 소급하지 않아요.",2);eggs.PriceProvider=()=>"둥지 "+coop.nestEggs+" /12개";entries.Add(eggs);
        entries.Add(OperationsEntry(6,"먹이와 달걀 상점","달걀 "+state.eggs+"개 보유","먹이 구매·만들기·달걀 판매","상점 보기","양배추를 먹이로 만들고 달걀을 판매하거나 카페 재료로 사용합니다.",2));
        entries.Add(OperationsEntry(8,"닭 번식 관리","알 품는 중 "+state.Reserved(coop)+"쌍","성체 암탉·수탉 짝짓기","번식 보기","성체 암탉과 수탉을 직접 골라 알을 품게 합니다.\n알 품기 2일 · 부모 휴식3일 · 부모·형제는 짝지을 수 없어요.",9));
        for(int i=0;i<coop.birds.Count;i++){var bird=coop.birds[i];var entry=OperationsEntry(100+i,bird.name,PoultrySex(bird)+" "+(bird.Adult?"성체":"병아리")+" · 성장 "+bird.grown+" /2일",(bird.fedDay==day?"먹이 완료":"먹이 필요")+" · "+(bird.wateredDay==day?"급수 완료":"물 필요"),"개체 보기","개체 ID를 유지한 채 자랍니다.\n농장의 실제 모델 크기도 성장에 맞춰 변합니다.",9);entry.PriceProvider=()=>PoultrySex(bird)+" "+(bird.Adult?"성체":"병아리")+" · 성장 "+bird.grown+" /2일";entry.SizeProvider=()=>(bird.fedDay==day?"먹이 완료":"먹이 필요")+" · "+(bird.wateredDay==day?"급수 완료":"물 필요");entries.Add(entry);}
        entries.Add(OperationsEntry(7,"밭 관리로 돌아가기","농장 밭9칸","작물·씨앗 관리","밭 보기","밭 관리로 돌아갑니다.",3));
        var art=PetTownPoultryLibrary.Current;if(art)foreach(var entry in entries)entry.Thumbnail=entry.Key==1||entry.Key==8||entry.Key==10||entry.Key>=100?art.birdIcon:entry.Key==2||entry.Key==6?art.feedIcon:entry.Key==3?art.waterIcon:entry.Key==4?art.cleanIcon:entry.Key==5?art.eggIcon:art.thumbnail;
        catalogWindow.Show("농장 · 닭장 관리",entries,e=>
        {
            if(e.Key==7){OpenFarmLot(id);return;}if(e.Key==6){OpenPoultryMarket(id);return;}if(!PoultryReady(id,out var current))return;bool done=false;int count=0;
            if(e.Key==1)done=state.BuyBird(current,day,ref money);else if(e.Key==2){count=state.Feed(current,day);done=count>0;}else if(e.Key==3){count=state.Water(current,day);done=count>0;}else if(e.Key==4)done=state.Clean(current);else if(e.Key==5){count=state.Collect(current);done=count>0;if(done){Operations.Record("eggs",count);money+=Progression.AddExperience(count*2);}}
            else if(e.Key==8){OpenPoultryBreeding(id);return;}
            else if(e.Key==10)done=state.BuyBirdOfSex(current,1,day,ref money);
            else if(e.Key>=100){int bi=e.Key-100;if(bi>=0&&bi<current.birds.Count){OpenPoultryBird(id,current.birds[bi].id);return;}}
            if(done){progressionDirty=true;RefreshPoultry(id);SaveProgressionSilent();Message(e.Key==1||e.Key==10?"병아리를 맞이했어요.":e.Key==2?count+"마리에게 먹이를 주었어요.":e.Key==3?count+"마리에게 물을 주었어요.":e.Key==4?"닭장을 청소했어요.":"달걀 "+count+"개를 모았어요.");}else Message("정원·먹이·오늘 돌봄·둥지 상태를 확인해 주세요.");OpenPoultry(id);
        },()=>{},()=>{});
    }
    static string PoultrySex(PetTownChickenRecord bird){bird.EnsureSex();return bird.sex==1?"수탉":"암탉";}
    static string PoultryName(PetTownCoopRecord coop,string id){var bird=string.IsNullOrEmpty(id)?null:coop.birds.Find(x=>x.id==id);return bird==null?"떠난 닭":bird.name;}
    void OpenPoultryBird(string id,string birdId)
    {
        if(!PoultryReady(id,out var coop))return;var state=Progression.Farm.Poultry;var bird=coop.birds.Find(x=>x.id==birdId);if(bird==null){OpenPoultry(id);return;}
        bool locked=state.BreedingLocked(coop,bird.id);int quote=PetTownPoultryState.SellQuote(bird);string family=string.IsNullOrEmpty(bird.parentA)?"처음 맞이한 개체":"부모 "+PoultryName(coop,bird.parentA)+" · "+PoultryName(coop,bird.parentB)+" · "+bird.generation+"세대";
        string role=!bird.Adult?"병아리 · 성장 "+bird.grown+" /2일":bird.Lays?"성체 · 산란 가능":"성체 · 달걀은 낳지 않아요";
        var entries=new List<PetTownCatalogEntry>{OperationsEntry(0,bird.name+" "+PoultrySex(bird),role,family+" · 판매 "+quote+" 골드","판매하기",(locked?"알을 품고 있는 부모는 판매할 수 없어요.":"판매하면 이 개체는 닭장에서 사라져요.")+"\n번식 휴식 "+bird.breedRestDays+"일 · 개체 ID는 유지됩니다.",9),OperationsEntry(1,"닭장으로 돌아가기",PoultryStatus(coop),"돌봄·성장·달걀 수집","닭장 보기","닭장 관리로 돌아갑니다.",9)};
        var art=PetTownPoultryLibrary.Current;if(art)foreach(var entry in entries)entry.Thumbnail=entry.Key==0?art.birdIcon:art.thumbnail;
        catalogWindow.Show("농장 · "+bird.name,entries,e=>
        {
            if(e.Key==1){OpenPoultry(id);return;}if(!PoultryReady(id,out var current))return;
            string reason;if(state.SellBird(current,birdId,ref money,out reason)){progressionDirty=true;RefreshPoultry(id);SaveProgressionSilent();Message(bird.name+"을(를) "+quote+" 골드에 보냈어요.");OpenPoultry(id);}else{Message(reason);OpenPoultryBird(id,birdId);}
        },()=>{},()=>{});
    }
    void OpenPoultryBreeding(string id)
    {
        if(!PoultryReady(id,out var coop))return;var state=Progression.Farm.Poultry;var entries=new List<PetTownCatalogEntry>();
        entries.Add(OperationsEntry(0,"번식 안내","암탉1·수탉1 짝짓기","알 품기 2일 · 부모 휴식3일","안내 보기","오늘 먹이와 물을 먹은 성체 암탉과 수탉을 한 마리씩 고르세요. 청결60 이상이어야 하고, 부모·형제는 짝지을 수 없어요.\n알을 품는 동안 매일 먹이·물을 주면 병아리가 태어납니다. 시작은 항상 직접 선택해요.",9));
        for(int i=0;i<coop.clutches.Count;i++){var c=coop.clutches[i];entries.Add(OperationsEntry(500+i,c.henName+" × "+c.roosterName,"알 품는 중 · 남은 "+c.remainingDays+"일",c.generation+"세대 병아리 예정","취소하기","취소해도 부모의 번식 휴식은 유지돼요.",9));}
        for(int i=0;i<coop.birds.Count;i++){var bird=coop.birds[i];bird.EnsureSex();if(!bird.Adult||bird.sex!=2)continue;string reason;bool ok=state.CanBreed(coop,bird,day,out reason);entries.Add(OperationsEntry(200+i,bird.name+" 암탉",ok?"짝 고르기 가능":reason,"성체 · "+bird.generation+"세대","수탉 고르기","이 암탉의 짝이 될 성체 수탉을 고릅니다.",9));}
        for(int i=0;i<Mathf.Min(3,coop.births.Count);i++){var b=coop.births[i];entries.Add(OperationsEntry(900+i,"최근 탄생 · "+b.childName,(b.sex==1?"수탉":"암탉")+" · "+b.generation+"세대",b.henName+" × "+b.roosterName+" · "+b.day+"일차","기록 보기","부모 정보가 기록으로 남습니다.",9));}
        entries.Add(OperationsEntry(7,"닭장으로 돌아가기",PoultryStatus(coop),"돌봄·성장·달걀 수집","닭장 보기","닭장 관리로 돌아갑니다.",9));
        var art=PetTownPoultryLibrary.Current;if(art)foreach(var entry in entries)entry.Thumbnail=entry.Key==7?art.thumbnail:art.birdIcon;
        catalogWindow.Show("농장 · 닭 번식",entries,e=>
        {
            if(e.Key==7){OpenPoultry(id);return;}if(!PoultryReady(id,out var current))return;
            if(e.Key>=500&&e.Key<600){int ci=e.Key-500;if(ci<current.clutches.Count&&state.CancelBreeding(current,current.clutches[ci].id)){progressionDirty=true;RefreshPoultry(id);SaveProgressionSilent();Message("알 품기를 취소했어요.");}}
            else if(e.Key>=200&&e.Key<300){int bi=e.Key-200;if(bi<current.birds.Count){OpenPoultryPartner(id,current.birds[bi].id);return;}}
            OpenPoultryBreeding(id);
        },()=>{},()=>{});
    }
    void OpenPoultryPartner(string id,string henId)
    {
        if(!PoultryReady(id,out var coop))return;var state=Progression.Farm.Poultry;var hen=coop.birds.Find(x=>x.id==henId);if(hen==null){OpenPoultryBreeding(id);return;}
        var entries=new List<PetTownCatalogEntry>();
        for(int i=0;i<coop.birds.Count;i++){var rooster=coop.birds[i];rooster.EnsureSex();if(!rooster.Adult||rooster.sex!=1)continue;string reason;bool ok=state.CanBreed(coop,rooster,day,out reason);if(ok&&PetTownPoultryState.Related(hen,rooster)){ok=false;reason="부모·형제 사이는 짝지을 수 없어요.";}entries.Add(OperationsEntry(300+i,rooster.name+" 수탉",ok?"짝짓기 가능":reason,"성체 · "+rooster.generation+"세대","짝짓기",hen.name+"와(과) 짝을 지어 알을 품게 합니다.",9));}
        if(entries.Count==0)entries.Add(OperationsEntry(0,"성체 수탉이 없어요","수탉 병아리를 맞이해 키워 주세요","1마리 80 골드","안내 보기","수탉은 달걀을 낳지 않지만 번식에 필요해요.",9));
        entries.Add(OperationsEntry(7,"번식 관리로 돌아가기","암탉 고르기","알 품기 현황","돌아가기","번식 관리로 돌아갑니다.",9));
        var art=PetTownPoultryLibrary.Current;if(art)foreach(var entry in entries)entry.Thumbnail=entry.Key==7?art.thumbnail:art.birdIcon;
        catalogWindow.Show("농장 · "+hen.name+"의 짝",entries,e=>
        {
            if(e.Key==7||e.Key==0){OpenPoultryBreeding(id);return;}if(!PoultryReady(id,out var current))return;int bi=e.Key-300;
            if(bi<0||bi>=current.birds.Count){OpenPoultryBreeding(id);return;}
            string reason;if(state.StartBreeding(current,henId,current.birds[bi].id,day,out reason)){progressionDirty=true;RefreshPoultry(id);SaveProgressionSilent();Message("알을 품기 시작했어요. 2일 동안 먹이와 물을 주세요.");OpenPoultryBreeding(id);}else{Message(reason);OpenPoultryPartner(id,henId);}
        },()=>{},()=>{});
    }
    void OpenPoultryMarket(string id)
    {
        if(!PoultryReady(id,out var coop))return;var state=Progression.Farm.Poultry;var entries=new List<PetTownCatalogEntry>{OperationsEntry(0,"닭 먹이10개 구매",state.feed+"개 보유","40 골드","10개 구매","먹이는 아직 먹지 않은 닭에게 하루1개씩 사용합니다.",2),OperationsEntry(1,"양배추로 먹이 만들기","양배추 "+Progression.Farm.produce[0]+"개 보유","양배추1개 → 먹이10개","먹이 만들기","양배추1개를 먹이10개로 바꿉니다.",3),OperationsEntry(2,"달걀 모두 판매",state.eggs+"개 보유","1개35 골드","모두 판매","둥지에서 모은 달걀만 판매합니다. 중복 판매되지 않아요.",2)};
        bool cafe=buildings.Exists(b=>b&&b.GetComponent<PetTownInteriorState>()&&b.GetComponent<PetTownInteriorState>().Kind==3);if(cafe)entries.Add(OperationsEntry(3,"달걀로 카페 재료 만들기","달걀 "+state.eggs+"개 보유","달걀2개 → 카페 재료2개","재료 만들기","실제 카페를 보유한 경우에만 만들 수 있어요.",3));entries.Add(OperationsEntry(4,"닭장으로 돌아가기",PoultryStatus(coop),"돌봄·성장·달걀 수집","닭장 보기","닭장 관리로 돌아갑니다.",9));
        var art=PetTownPoultryLibrary.Current;if(art)foreach(var entry in entries)entry.Thumbnail=entry.Key==0||entry.Key==1?art.feedIcon:entry.Key==2||entry.Key==3?art.eggIcon:art.thumbnail;
        catalogWindow.Show("농장 · 먹이와 달걀",entries,e=>{if(e.Key==4){OpenPoultry(id);return;}if(!PoultryReady(id,out var current))return;bool done=e.Key==0?state.BuyFeed(ref money):e.Key==1?state.MakeFeed(Progression.Farm):e.Key==2?state.SellEggs(state.eggs,ref money):e.Key==3&&cafe&&state.MakeIngredients(Progression.Facilities);if(done){progressionDirty=true;SaveProgressionSilent();Message(e.Key==0?"먹이10개를 구매했어요.":e.Key==1?"양배추로 먹이10개를 만들었어요.":e.Key==2?"달걀을 판매했어요.":"카페 재료2개를 만들었어요.");}else Message("골드·양배추·달걀 보유량을 확인해 주세요.");OpenPoultryMarket(id);},()=>{},()=>{});
    }
}
