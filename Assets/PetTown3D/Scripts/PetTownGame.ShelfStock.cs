using System.Collections.Generic;
using UnityEngine;

// 선반 재고: 사료 선반·장난감 진열대가 있으면 분양 손님이 용품을 함께 사 간다. 재고가 없으면 아쉬워한다(서비스 점수 -0.4).
public partial class PetTownGame
{
    const int ShelfPerUnit=10,RestockAmount=5,RestockPrice=75;
    int ShelfCount()
    {
        int n=0;if(!shop)return 0;
        foreach(var f in shop.GetComponentsInChildren<PetTownFurniturePlacement>())if(f.Owner&&f.Owner.Kind==0&&f.Detail&&f.gameObject.activeInHierarchy&&(f.Kind==9||f.Kind==1))n++;
        return n;
    }
    int ShelfStockNow(){if(Progression==null)return 0;if(Progression.shelfStock<0)Progression.shelfStock=10;return Progression.shelfStock;}
    int SupplySale(out bool missing)
    {
        missing=false;if(Progression==null||ShelfCount()==0)return 0;
        if(ShelfStockNow()>0){Progression.shelfStock--;progressionDirty=true;return 25+shopLevel*3;}
        Progression.serviceHappiness=Mathf.Clamp(Progression.serviceHappiness-.4f,-20,15);missing=true;return 0;
    }
    void OpenShelfStock()
    {
        SetMode(Mode.Play);int shelves=ShelfCount(),cap=Mathf.Max(1,shelves)*ShelfPerUnit,now=ShelfStockNow();
        var entries=new List<PetTownCatalogEntry>();
        entries.Add(new PetTownCatalogEntry{Key=0,IconKey=9,TextOnlyDetail=true,Name="선반 용품 재입고",Category="선반",PriceLabel=now+" / "+cap+"개",FootprintLabel=RestockAmount+"개 / "+RestockPrice+" 골드",ActionLabel=now>=cap?"가득 찼어요":RestockAmount+"개 채우기",Description="분양 손님이 용품을 사 가요. 재고가 없으면 아쉬워해요."});
        entries.Add(new PetTownCatalogEntry{Key=1,IconKey=9,TextOnlyDetail=true,Name="사료·미용 키트",Category="아이템",PriceLabel="",FootprintLabel="",ActionLabel="구매하러 가기",Description="펫 먹이와 미용 키트를 삽니다."});
        catalogWindow.Show("선반 재고",entries,e=>{
            if(e.Key==1){OpenProgressionInventory();return;}
            if(ShelfStockNow()+RestockAmount>cap){Message("선반이 가득 찼어요.");OpenShelfStock();return;}
            if(money<RestockPrice){Message("골드가 부족해요.");return;}
            money-=RestockPrice;Progression.shelfStock=ShelfStockNow()+RestockAmount;progressionDirty=true;SaveProgressionSilent();Message("용품 "+RestockAmount+"개를 채웠어요. ("+Progression.shelfStock+" / "+cap+")");OpenShelfStock();
        },()=>{},()=>{});
    }
}
