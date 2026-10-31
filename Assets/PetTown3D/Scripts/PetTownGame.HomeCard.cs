using System.Collections.Generic;
using UnityEngine;

// Tapping the 'our house' sign: furniture can be placed inside the house the same way as in the cafe / clinic.
public partial class PetTownGame
{
    void OpenHomeCard(GameObject b)
    {
        var room = b ? b.GetComponent<PetTownInteriorState>() : null;
        SetMode(Mode.Play);
        var entries = new List<PetTownCatalogEntry>();
        entries.Add(OperationsEntry(0, "집 가구 배치", "집 안 꾸미기", "침대·의자·소품을 집 안에 놓아요", "가구 놓기",
            "집 안에 가구를 놓고 옮기고 치울 수 있어요.\n출입구 앞 통로는 비워 두어야 해요." + (room ? "" : "\n(이 집의 실내 정보를 찾지 못했어요.)"), 5));
        entries.Add(OperationsEntry(1, "내 정보 · 마을 현황", "레벨 · 마을", "진행 상황 보기", "보기", "마을 레벨과 진행 상황을 봐요.", 5));
        catalogWindow.Show("우리 집", entries, e =>
        {
            if (e.Key == 0 && room) { SetMode(Mode.Furniture); facilityFurnitureTarget = room; OpenFurnitureCatalog(); Message("집 안에 가구를 배치해 주세요."); }
            else OpenTownProgression();
        }, () => { }, () => { });
    }
}
