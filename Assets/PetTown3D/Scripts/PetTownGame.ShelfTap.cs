using UnityEngine;

public partial class PetTownGame
{
    // 사료 선반(9)·장난감 진열대(1)를 누르면 물품 구매 목록을 바로 연다.
    bool TryTouchShelf(Vector2 screen)
    {
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(screen);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 300f, ~0, QueryTriggerInteraction.Ignore)) return false;
        var furniture = hit.collider.GetComponentInParent<PetTownFurniturePlacement>();
        if (furniture == null || !furniture.Detail) return false;
        if (furniture.Kind != 9 && furniture.Kind != 1) return false;
        OpenShelfStock();
        return true;
    }
}
