using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    public int CurrentFarmDay=>day;
    void CreateFarmBuilding(GameObject root,Vector3 point)
    {
        var definition=PetTownBuildingDefinition.Default(9);FlattenBuildingFootprint(point,9,10,point.y);ClearBuildingFootprint(point,9,10);
        buildingFloors.Add(new BuildingFloor{owner=root,center=point,halfW=9,halfD=10,floorY=point.y+.045f});
        var record=activeBuildingPlacement;
        if(record==null)
        {
            record=new PetTownBuildingSaveRecord{kind=9,id=Guid.NewGuid().ToString("N"),groundY=point.y,footprintMeters=definition.footprintMeters,entranceLocal=definition.entranceLocal,facingQuarter=2};
            root.AddComponent<PetTownBuildingPlacement>().Initialize(record);
        }
        if(string.IsNullOrEmpty(record.id))record.id=Guid.NewGuid().ToString("N");
        Progression.Farm.Get(record.id,day);root.AddComponent<PetTownFarmView>().Initialize(this,record.id);
    }
    List<PetTownFarmView> FarmRoots()
    {
        var result=new List<PetTownFarmView>();foreach(var root in buildings){var farm=root?root.GetComponent<PetTownFarmView>():null;if(farm&&farm.gameObject.activeInHierarchy)result.Add(farm);}return result;
    }
    bool FarmReady(string id,out PetTownFarmRecord lot)
    {
        lot=null;if(!OperationsReady()||!FarmRoots().Exists(f=>f.Id==id))return false;
        lot=Progression.Farm.lots.Find(f=>f.id==id);if(lot==null)Message("농장 목록을 다시 열어 주세요.");return lot!=null;
    }
    void FarmNewDay(){if(Progression.farm!=null){Progression.farm.AdvanceDay(day);if(Progression.farm.poultry!=null){int hatchedBefore=Progression.farm.poultry.TotalHatched();Progression.farm.poultry.AdvanceDay(day,FarmRoots().ConvertAll(x=>x.Id));int hatchedNow=Progression.farm.poultry.TotalHatched()-hatchedBefore;if(hatchedNow>0&&shop)Operations.Record("chick",hatchedNow);}}}
    string FarmStatus(PetTownFarmPlot p)
    {
        if(!PetTownFarmState.ValidCrop(p.crop))return "비어 있는 밭";if(p.grown>=PetTownFarmState.GrowDays[p.crop])return PetTownFarmState.Names[p.crop]+" · 수확 가능";
        return PetTownFarmState.Names[p.crop]+" · 성장 "+p.grown+" / "+PetTownFarmState.GrowDays[p.crop]+"일";
    }
    void OpenFarmManagement()
    {
        SetMode(Mode.Play);var roots=FarmRoots();var entries=new List<PetTownCatalogEntry>();
        for(int i=0;i<roots.Count;i++)
        {
            var view=roots[i];var lot=Progression.Farm.Get(view.Id,day);int key=i;
            var e=OperationsEntry(key,"농장 "+(i+1),"밭 "+lot.plots.Count+"칸",lot.plots.FindAll(p=>p.crop>=0).Count+"칸 재배 중","밭 관리","씨앗을 심고 매일 물을 주면 자랍니다.\n양배추·당근·밀·감자2일, 토마토·옥수수3일, 호박4일. 물을 주지 않으면 그날의 성장이 멈춥니다.",3);e.Thumbnail=PetTownFarmLibrary.Current?.thumbnail;entries.Add(e);
        }
        entries.Add(OperationsEntry(-1,"농장 짓기","3,000 골드 · 18×20m","건설 목록에서 위치 선택","건설 목록","입구 방향을 돌려 배치하고 길을 연결할 수 있어요. 기존 도로 위에는 짓지 않습니다.",0));
        catalogWindow.Show("농장 관리",entries,e=>{if(e.Key==-1){SetMode(Mode.Build);OpenConstructionCatalog();}else if(e.Key<roots.Count&&roots[e.Key])OpenFarmLot(roots[e.Key].Id);},()=>{},()=>{});
    }
    void OpenFarmLot(string id)
    {
        if(!FarmReady(id,out var lot))return;var entries=new List<PetTownCatalogEntry>();
        entries.Add(OperationsEntry(100,"밭에 모두 물주기","오늘 물이 필요한 밭 관리","물을 준 날만 성장합니다","모두 물주기","하루에 한 번씩 줄 수 있어요. 빈 밭과 수확 가능한 밭은 제외합니다.",2));
        entries.Add(OperationsEntry(101,"씨앗과 수확물","씨앗 구매·작물 판매","수확물을 카페 재료로도 사용","농장 상점","일곱 종류의 씨앗을 사고 수확물을 정리합니다.",2));
        entries.Add(OperationsEntry(103,"닭장과 달걀",Progression.Farm.poultry?.Find(id)!=null?"병아리·먹이·물·달걀 관리":"농장 뒤편에 닭장 추가","닭장 700 골드 · 정원6마리","닭장 보기","기존 밭9칸을 유지하며 뒤편 빈 공간에 짓습니다.",9));
        entries.Add(OperationsEntry(104,lot.staffService?"직원 자동 업무 끄기":"직원 자동 업무 켜기",lot.staffService?"밭·닭장 관리 중":"직접 관리",FacilityStaffSummary(id),"전환하기","배치한 농장 관리사가 물주기·수확과 닭장 먹이·물·청소·달걀 수집을 합니다. 씨앗 구매·심기·판매는 직접 결정합니다.",5));
        entries.Add(OperationsEntry(105,"농장 직원 배치",FacilityStaffSummary(id),"농장 관리사 고용·배정","직원 선택","직원이 직접 작업 지점으로 이동합니다. 미지급 급여와 휴무 중에는 쉽니다.",5));
        entries.Add(lot.expansion<PetTownFarmState.MaxExpansion?OperationsEntry(106,"밭 넓히기","밭 "+lot.plots.Count+"칸 → "+PetTownFarmState.PlotsFor(lot.expansion+1)+"칸",PetTownFarmState.ExpandCost[lot.expansion]+" 골드","넓히기","농장 빈 터에 밭을 추가합니다. 심어 둔 작물과 직원 배정은 그대로 유지됩니다.",2):OperationsEntry(106,"밭 넓히기","최대 "+lot.plots.Count+"칸","더 넓힐 수 없어요","최대","이 농장의 밭은 모두 넓혔어요.",2));
        entries.Add(OperationsEntry(107,"지금은 "+PetTownFarmState.SeasonNames[PetTownFarmState.SeasonOf(day)],"제철: "+PetTownFarmState.SeasonCrops(day),"씨앗 20% 할인 · 판매가 30% 상승","계절 보기","7일마다 계절이 바뀌어요. 제철 작물은 싸게 사서 비싸게 팔 수 있어요.",3));
        for(int i=0;i<lot.plots.Count;i++)
        {
            int index=i;var plot=lot.plots[i];var e=OperationsEntry(i,"밭 "+(i+1),FarmStatus(plot),plot.crop<0?"씨앗을 심어 주세요":plot.grown>=PetTownFarmState.GrowDays[plot.crop]?"지금 수확할 수 있어요":plot.wateredDay==day?"오늘 물주기 완료":"물이 필요해요","밭 보기","이 밭에 심기·물주기·수확을 합니다.",3);
            e.PriceProvider=()=>FarmStatus(lot.plots[index]);e.SizeProvider=()=>lot.plots[index].crop<0?"씨앗을 심어 주세요":lot.plots[index].grown>=PetTownFarmState.GrowDays[lot.plots[index].crop]?"수확 준비 완료":lot.plots[index].wateredDay==day?"오늘 물주기 완료":"물이 필요해요";entries.Add(e);
        }
        entries.Add(OperationsEntry(102,"농장 목록","다른 농장 보기","모든 농장 관리","목록 보기","건설한 농장 목록으로 돌아갑니다.",3));
        catalogWindow.Show("농장 · 밭 관리",entries,e=>
        {
            if(e.Key==107){Message("지금은 "+PetTownFarmState.SeasonNames[PetTownFarmState.SeasonOf(day)]+" · 제철: "+PetTownFarmState.SeasonCrops(day));return;}if(e.Key==106){if(!FarmReady(id,out var grow))return;if(Progression.playerLevel<PetTownUnlocks.FarmExpandLevel(grow.expansion)){Message("밭 넓히기는 플레이어 Lv."+PetTownUnlocks.FarmExpandLevel(grow.expansion)+"에 열려요.");return;}if(Progression.Farm.Expand(grow,ref money)){progressionDirty=true;SaveProgressionSilent();Message("밭이 넓어졌어요! 모두 "+grow.plots.Count+"칸");}else Message("골드를 확인해 주세요. 최대로 넓힌 농장은 더 넓힐 수 없어요.");OpenFarmLot(id);return;}if(e.Key==105){OpenAgrarianWorkers(id,true);return;}if(e.Key==104){if(!FarmReady(id,out var active))return;active.staffService=!active.staffService;if(!active.staffService)CancelAgrarianWorkers(id);SaveFacilityStaffProgress();OpenFarmLot(id);return;}
            if(e.Key==100){if(!FarmReady(id,out var current))return;int count=0;for(int i=0;i<current.plots.Count;i++)if(Progression.Farm.Water(current,i,day))count++;if(count>0){progressionDirty=true;SaveProgressionSilent();}Message(count>0?count+"칸에 물을 주었어요.":"오늘 물이 필요한 밭이 없어요.");OpenFarmLot(id);}
            else if(e.Key==103)OpenPoultry(id);else if(e.Key==101)OpenFarmMarket(id);else if(e.Key==102)OpenFarmManagement();else OpenFarmPlot(id,e.Key);
        },()=>{},()=>{});
    }
    void OpenFarmPlot(string id,int index)
    {
        if(!FarmReady(id,out var lot)||index<0||index>=lot.plots.Count)return;var p=lot.plots[index];var entries=new List<PetTownCatalogEntry>();
        if(p.crop<0)for(int crop=0;crop<PetTownFarmState.CropCount;crop++)
        {
            int kind=crop;var e=OperationsEntry(kind,PetTownFarmState.Names[kind]+" 심기","씨앗 "+Progression.Farm.seeds[kind]+"개 보유","매일 물주기 · "+PetTownFarmState.GrowDays[kind]+"일 성장","심기","심을 때 씨앗1개를 사용합니다.\n심은 뒤 물주기를 해야 다음 날 자랍니다.",3);e.Thumbnail=PetTownFarmLibrary.Current?.CropIcon(kind);e.PriceProvider=()=>"씨앗 "+Progression.Farm.seeds[kind]+"개 보유";entries.Add(e);
        }
        else if(p.grown>=PetTownFarmState.GrowDays[p.crop])entries.Add(OperationsEntry(-2,"수확하기",FarmStatus(p),"수확물 1개를 받습니다","수확하기","수확한 뒤 빈 밭에 다시 씨앗을 심을 수 있어요.\n"+(p.crop<2?"양배추·토마토는 판매하거나 기본 카페 재료로 바꿉니다.":p.crop==3||p.crop==4?"옥수수·밀은 판매하거나 닭/가축 먹이로 바꿉니다.":"이 작물은 농장 상점에서 판매할 수 있어요."),3));
        else
        {
            var e=OperationsEntry(-1,"물주기",FarmStatus(p),p.wateredDay==day?"오늘 물주기 완료":"오늘 물이 필요해요","물주기","물을 준 날의 다음 날에1단계 자랍니다. 같은 날 여러 번 물을 줘도 더 빨리 자라지 않습니다.",2);e.PriceProvider=()=>FarmStatus(p);e.SizeProvider=()=>p.wateredDay==day?"오늘 물주기 완료":"오늘 물이 필요해요";entries.Add(e);
        }
        entries.Add(OperationsEntry(-3,"씨앗과 수확물","씨앗 구매·판매","농장 상점","상점 보기","씨앗이 부족하면 상점에서 구매해 주세요.",2));
        entries.Add(OperationsEntry(-4,"밭 목록","다른 밭 관리",lot.plots.Count+"칸의 밭","목록 보기","농장 밭 목록으로 돌아갑니다.",3));
        catalogWindow.Show("농장 · 밭 "+(index+1),entries,e=>
        {
            if(e.Key==-3){OpenFarmMarket(id);return;}if(e.Key==-4){OpenFarmLot(id);return;}if(!FarmReady(id,out var current))return;bool done=false;
            if(PetTownFarmState.ValidCrop(e.Key))done=Progression.Farm.Plant(current,index,e.Key);
            else if(e.Key==-1)done=Progression.Farm.Water(current,index,day);
            else if(e.Key==-2&&Progression.Farm.Harvest(current,index,out int crop)){done=true;money+=Progression.AddExperience(3);Operations.Record("harvest");}
            if(done){progressionDirty=true;SaveProgressionSilent();Message(PetTownFarmState.ValidCrop(e.Key)?"씨앗을 심었어요. 물을 주면 자라요.":e.Key==-1?"밭에 물을 주었어요.":"수확물을1개 받았어요!");}else Message("씨앗·성장 상태와 오늘 물주기 여부를 확인해 주세요.");OpenFarmPlot(id,index);
        },()=>{},()=>{});
    }
    void OpenFarmMarket(string id)
    {
        if(!FarmReady(id,out var lot))return;var farm=Progression.Farm;var entries=new List<PetTownCatalogEntry>();
        for(int crop=0;crop<PetTownFarmState.CropCount;crop++)
        {
            int kind=crop;var buy=OperationsEntry(kind,PetTownFarmState.Names[kind]+" 씨앗 5개",PetTownFarmState.SeedCost(kind,day)*5+" 골드"+(PetTownFarmState.InSeason(kind,day)?" · 제철 할인":""),"보유 "+farm.seeds[kind]+"개","5개 구매","씨앗은 빈 밭에 하나씩 심습니다. 매일 물을 주며 "+PetTownFarmState.GrowDays[kind]+"일 동안 키웁니다.",2);buy.Thumbnail=PetTownFarmLibrary.Current?.CropIcon(kind);buy.SizeProvider=()=>"보유 "+farm.seeds[kind]+"개";entries.Add(buy);
            var sell=OperationsEntry(10+kind,PetTownFarmState.Names[kind]+" 판매","보유 "+farm.produce[kind]+"개","1개 "+PetTownFarmState.SellValue(kind,day)+" 골드"+(PetTownFarmState.InSeason(kind,day)?" · 제철 +30%":""),"모두 판매","선택한 종류의 수확물을 모두 판매합니다.",2);sell.Thumbnail=PetTownFarmLibrary.Current?.CropIcon(kind);sell.PriceProvider=()=>"보유 "+farm.produce[kind]+"개";entries.Add(sell);
        }
        bool cafe=buildings.Exists(b=>b&&b.GetComponent<PetTownInteriorState>()&&b.GetComponent<PetTownInteriorState>().Kind==3);
        if(cafe)entries.Add(OperationsEntry(20,"카페 재료 만들기","양배추·토마토5개 사용","양배추부터 기본 재료5개로","5개 만들기","양배추와 토마토 수확물5개를 카페 공용 재료5개로 바꿉니다. 원하지 않으면 작물로 판매할 수 있어요.",3));
        entries.Add(OperationsEntry(40,"곡물로 먹이 만들기","옥수수·밀 수확물 사용","닭·가축 먹이를 따로 보관","먹이 만들기","옥수수1개로20개, 밀1개로10개의 먹이를 만듭니다. 실제 닭장 또는 목장이 필요해요.",2));
        entries.Add(OperationsEntry(30,"밭 목록","농장으로 돌아가기","심기·물주기·수확","밭 보기","농장 밭을 관리합니다.",3));
        catalogWindow.Show("농장 · 씨앗과 수확물",entries,e=>
        {
            if(e.Key==40){OpenFarmGrainFeed(id);return;}if(e.Key==30){OpenFarmLot(id);return;}if(!FarmReady(id,out var current))return;bool done=PetTownFarmState.ValidCrop(e.Key)?farm.BuySeeds(e.Key,5,ref money,day):PetTownFarmState.ValidCrop(e.Key-10)?farm.Sell(e.Key-10,farm.produce[e.Key-10],ref money,day):e.Key==20&&cafe&&farm.MakeIngredients(Progression.Facilities,5);
            if(done){progressionDirty=true;SaveProgressionSilent();Message(PetTownFarmState.ValidCrop(e.Key)?"씨앗5개를 구매했어요.":PetTownFarmState.ValidCrop(e.Key-10)?"수확물을 판매했어요.":"카페 재료5개를 만들었어요.");}else Message("골드와 보유 수량을 확인해 주세요.");OpenFarmMarket(id);
        },()=>{},()=>{});
    }
}
