using System.Collections.Generic;

public partial class PetTownGame
{
    void OpenFarmGrainFeed(string id)
    {
        if(!FarmReady(id,out var lot))return;var farm=Progression.Farm;var entries=new List<PetTownCatalogEntry>();
        for(int target=0;target<2;target++)for(int grain=0;grain<2;grain++)
        {
            int crop=3+grain,key=target*2+grain;bool chicken=target==0;bool owned=chicken?farm.poultry?.Find(id)!=null:RanchRoots().Count>0;
            var row=OperationsEntry(key,PetTownFarmState.Names[crop]+" → "+(chicken?"닭 먹이":"가축 먹이"),"수확물 "+farm.produce[crop]+"개 보유",PetTownFarmState.Names[crop]+"1개 → 먹이"+PetTownFarmState.GrainFeedYield(crop)+"개",owned?"먹이 만들기":"시설 확인","선택한 곡물1개를 사용해 "+(chicken?"이 농장 닭장의":"목장용")+" 먹이를 만듭니다. 골드는 사용하지 않습니다. 실제 "+(chicken?"이 농장의 닭장":"목장")+"을 보유해야 만들 수 있어요. 닭과 가축 먹이는 별도 재고입니다.",2);entries.Add(row);
        }
        entries.Add(OperationsEntry(-1,"농장 상점으로","씨앗·수확물 관리","작물을 판매할 수도 있어요","돌아가기","농장 상점으로 돌아갑니다.",3));
        catalogWindow.Show("농장 · 곡물 먹이",entries,e=>
        {
            if(e.Key<0){OpenFarmMarket(id);return;}if(!FarmReady(id,out var current))return;int crop=3+e.Key%2;bool chicken=e.Key<2;bool owned=chicken?farm.poultry?.Find(id)!=null:RanchRoots().Count>0;
            if(!owned){Message(chicken?"이 농장에 닭장을 먼저 지어 주세요.":"목장을 먼저 지어 주세요.");OpenFarmGrainFeed(id);return;}
            bool done=chicken?farm.MakePoultryFeed(crop,farm.Poultry):farm.MakeRanchFeed(crop,Progression.Ranch);
            if(done){SaveFacilityStaffProgress();Message("먹이"+PetTownFarmState.GrainFeedYield(crop)+"개를 만들었어요.");}else Message("수확물과 먹이 보관 한도를 확인해 주세요.");OpenFarmGrainFeed(id);
        },()=>{},()=>{});
    }
}
