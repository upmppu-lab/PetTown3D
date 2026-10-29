using System.Collections.Generic;

public partial class PetTownGame
{
    void OpenCafeRecipes(string id)
    {
        var room=FacilityRoom(id);if(!room||room.Kind!=3){OpenFacilities();return;}
        var state=Progression.Facilities;var business=state.Get(id,3);var entries=new List<PetTownCatalogEntry>();
        for(int i=0;i<PetTownCafeRecipes.Count;i++)
        {
            int menu=i;bool open=PetTownCafeRecipes.Unlocked(menu,Progression.playerLevel);var row=OperationsEntry(menu,PetTownCafeRecipes.Name(menu),open?(business.cafeMenu==menu?"판매 중 · ":"")+state.CafeStock(menu)+"인분 보유":"플레이어 Lv."+PetTownCafeRecipes.UnlockLevel(menu)+"에 해금",PetTownCafeRecipes.Price(menu)+" 골드"+(menu>0?" · 보호자용":" · 기본 메뉴"),!open?"잠김":business.cafeMenu==menu?"선택됨":"판매 선택","이 카페의 새 주문부터 적용합니다. 이미 받은 주문의 메뉴·가격은 바뀌지 않습니다. 샌드위치·푸딩·수프·그라탱은 보호자용 메뉴이고, 수프는 Lv.3, 그라탱은 Lv.5부터 열립니다.",3);
            if(open)row.PriceProvider=()=>(business.cafeMenu==menu?"판매 중 · ":"")+state.CafeStock(menu)+"인분 · 예약 "+(state.CafeStock(menu)-state.AvailableCafe(menu));entries.Add(row);
        }
        for(int i=1;i<PetTownCafeRecipes.Count;i++)
        {
            int menu=i;if(!PetTownCafeRecipes.Unlocked(menu,Progression.playerLevel))continue;entries.Add(OperationsEntry(10+menu,PetTownCafeRecipes.Short(menu)+" 재료 준비",PetTownCafeRecipes.Needs(menu).Replace(" → "," · "),PetTownCafeRecipes.Portions(menu)+"인분 · 도착 후 조리",PetTownCafeRecipes.Portions(menu)+"인분 준비",PetTownCafeRecipes.Needs(menu)+"\n재료를 미리 묶어 보관합니다. 실제 요리는 주민·펫·담당 요리사가 카페에 도착한 후 진행합니다. 기본 주문 예약분은 사용하지 않습니다.",2));
        }
        entries.Add(OperationsEntry(20,"기본 재료 보충",state.cafeIngredients+"개 보유","5개 / 100 골드","5개 구매","기본 메뉴와 레시피 준비에 공통으로 사용합니다.",2));
        entries.Add(OperationsEntry(21,"현재 보유 생산물","양배추 "+Progression.Farm.produce[0]+" · 토마토 "+Progression.Farm.produce[1]+" · 당근 "+Progression.Farm.produce[2]+" · 옥수수 "+Progression.Farm.produce[3]+" · 감자 "+Progression.Farm.produce[6],"우유 "+Progression.Ranch.products[0]+"개","새로고침","농장과 목장에서 직접 생산하거나 직원이 수집한 재고입니다. 자동 구매나 자동 재료 준비는 하지 않습니다.",3));
        entries.Add(OperationsEntry(22,"카페 운영으로","메뉴는 카페별로 선택","준비한 재료는 모든 카페가 공유","돌아가기","기존 방문과 직원 배치를 확인합니다.",3));
        catalogWindow.Show("카페 메뉴·레시피",entries,e=>
        {
            if(!OperationsReady())return;var current=FacilityRoom(id);if(!current||current.Kind!=3){OpenFacilities();return;}
            if(e.Key==22){OpenFacilityBusiness(id);return;}if(e.Key==21){OpenCafeRecipes(id);return;}
            if(e.Key>=0&&e.Key<PetTownCafeRecipes.Count){if(!PetTownCafeRecipes.Unlocked(e.Key,Progression.playerLevel))Message(PetTownCafeRecipes.Name(e.Key)+" 메뉴는 플레이어 Lv."+PetTownCafeRecipes.UnlockLevel(e.Key)+"에 열려요.");else{state.SelectCafeMenu(business,e.Key);Message("새 주문부터 "+PetTownCafeRecipes.Name(e.Key)+" 메뉴를 판매해요.");}}
            else if(e.Key==20){Message(state.BuySupplies(3,ref money)?"기본 재료5개를 보충했어요.":"골드와 재료 보관 한도를 확인해 주세요.");}
            else if(e.Key>=11&&e.Key<10+PetTownCafeRecipes.Count){string reason;if(!PetTownCafeRecipes.Unlocked(e.Key-10,Progression.playerLevel))reason="아직 열리지 않은 메뉴예요.";else state.PrepareCafeRecipe(e.Key-10,Progression.Farm,Progression.Ranch,out reason);Message(reason);}
            SaveFacilityStaffProgress();OpenCafeRecipes(id);
        },()=>{},()=>{});
    }
}
