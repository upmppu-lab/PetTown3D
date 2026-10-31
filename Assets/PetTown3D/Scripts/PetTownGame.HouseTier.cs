using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Resident houses grow when upgraded (small cottage -> house -> two-story), the name label sits on top of the house,
// trees that grew into a building are cleared, and furniture goes to the interior the hero is standing in
// (house furniture only in the house, pet-shop furniture only in the shop, shared pieces in both).
public partial class PetTownGame
{
    static int HouseTierVariant(int lv) { return lv >= 3 ? 2 : lv == 2 ? 1 : 0; }   // MedievalHouseResources: 0 small, 1 normal, 2 two-story
    static float HouseTierHeight(int lv) { return lv >= 3 ? 7.0f : lv == 2 ? 5.0f : 3.6f; }
    int NewHouseLevel() { return activeBuildingPlacement != null ? HouseLv(activeBuildingPlacement.id) : 1; }

    static string HouseIdOf(GameObject b)
    {
        var metadata = b.GetComponent<PetTownBuildingPlacement>(); var p = b.transform.position;
        string id = metadata != null && metadata.Record != null ? metadata.Record.id : null;
        if (string.IsNullOrEmpty(id)) id = "legacy-house:" + p.x.ToString("F3", CultureInfo.InvariantCulture) + ":" + p.z.ToString("F3", CultureInfo.InvariantCulture);
        return id;
    }

    void UpgradeHouseModelById(string homeId, int lv)
    {
        foreach (var b in buildings)
        {
            if (!b || !b.name.StartsWith("Resident_House", System.StringComparison.Ordinal) || HouseIdOf(b) != homeId) continue;
            UpgradeHouseModel(b, lv); return;
        }
    }

    void UpgradeHouseModel(GameObject b, int lv)
    {
        var prefab = MedievalHouseResources.Length > 0 ? Resources.Load<GameObject>(MedievalHouseResources[Mathf.Clamp(HouseTierVariant(lv), 0, MedievalHouseResources.Length - 1)]) : null;
        if (prefab == null) return;
        var old = b.transform.Find("Model"); Quaternion yaw = old ? old.localRotation : Quaternion.identity;
        if (old) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        foreach (var c in b.GetComponents<BoxCollider>()) Destroy(c);
        var instance = Instantiate(prefab, b.transform);
        instance.name = "Model"; instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = yaw; instance.transform.localScale = Vector3.one;
        foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
        MarkNavMeshIgnore(instance);
        StartCoroutine(FitPropWithColliderNextFrame(instance, b, b.transform.position.y, HouseTierHeight(lv), .8f));
        StartCoroutine(PlaceHouseLabelLater(b));
        QueueConstructionNavRefresh();
    }

    IEnumerator PlaceHouseLabelLater(GameObject b)
    {
        for (int i = 0; i < 4; i++) yield return null;
        if (b) PlaceHouseLabel(b);
    }

    void PlaceHouseLabel(GameObject b)
    {
        var pill = b.GetComponentInChildren<PetTownPillLabel>(); if (!pill) return;
        var model = b.transform.Find("Model"); if (!model) return;
        Bounds bb; if (!RendererBounds(model.gameObject, out bb)) return;
        pill.transform.position = new Vector3(bb.center.x, bb.max.y + .7f, bb.center.z);
        int lv = HouseLv(HouseIdOf(b));
        pill.SetText(lv > 1 ? "주민 주택 ★Lv." + lv : "주민 주택");
    }

    // ---- trees that stand inside / overhang a building --------------------------------------
    int lastTreeCheckBuildings = -1;
    void ClearTreesOverlappingBuildings()
    {
        if (buildings.Count == lastTreeCheckBuildings) return;
        lastTreeCheckBuildings = buildings.Count;
        var rects = new List<Rect>();
        foreach (var b in buildings)
        {
            if (!b || b.name.StartsWith("Road")) continue;
            Bounds bb = new Bounds(b.transform.position, Vector3.zero); bool any = false;
            foreach (var r in b.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponent<TMPro.TMP_Text>() != null || r.GetComponent<SpriteRenderer>() != null) continue;
                if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
            }
            if (any) rects.Add(new Rect(bb.min.x - 1.5f, bb.min.z - 1.5f, bb.size.x + 3f, bb.size.z + 3f));
        }
        if (rects.Count == 0) return;
        foreach (var tr in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!tr || !tr.name.StartsWith("Replaceable_Tree")) continue;
            Vector2 p = new Vector2(tr.position.x, tr.position.z);
            foreach (var r in rects) if (r.Contains(p)) { roadTrees.Remove(tr.gameObject); Destroy(tr.gameObject); break; }
        }
    }

    // ---- furniture scope ---------------------------------------------------------------------
    PetTownInteriorState furnitureOwnerCache; float furnitureOwnerCacheTime = -9f;
    PetTownInteriorState ResolveFurnitureOwner()
    {
        if (mode == Mode.Furniture && facilityFurnitureTarget) return facilityFurnitureTarget;
        var shopInterior = shop != null ? shop.GetComponent<PetTownInteriorState>() : null;
        if (mode != Mode.Furniture) return shopInterior;
        if (furnitureOwnerCache && Time.unscaledTime - furnitureOwnerCacheTime < .25f) return furnitureOwnerCache;
        furnitureOwnerCacheTime = Time.unscaledTime;
        PetTownInteriorState best = null; var body = HeroBody();
        if (body)
        {
            float bestDistance = float.MaxValue;
            foreach (var s in FindObjectsByType<PetTownInteriorState>(FindObjectsSortMode.None))
            {
                if (!s || !s.gameObject.activeInHierarchy) continue;
                Vector3 lp = s.transform.InverseTransformPoint(body.transform.position);
                Rect r = s.LocalBounds; var padded = new Rect(r.xMin - 1.2f, r.yMin - 1.2f, r.width + 2.4f, r.height + 2.4f);
                if (!padded.Contains(new Vector2(lp.x, lp.z)) || Mathf.Abs(body.transform.position.y - s.FloorY) > 2.5f) continue;
                float d = (new Vector2(lp.x, lp.z) - r.center).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = s; }
            }
        }
        furnitureOwnerCache = best != null ? best : shopInterior;
        return furnitureOwnerCache;
    }

    static bool ShopOnlyFurniture(PetTownCozyFurniture f)
    {
        if (!f.detail) return f.kind == 0 || f.kind == 1 || f.kind == 2;          // 계산대, 펫 쿠션, 진열대
        return f.kind <= 9 || f.kind == 16 || f.kind == 17;                        // 사료·장난감 선반, 케이지, 수족관, 쿠션, 새장, 펫타워, 컴퓨터, 진열 선반
    }
    static bool HomeOnlyFurniture(PetTownCozyFurniture f) { return !string.IsNullOrEmpty(f.category) && f.category.StartsWith("집"); }
    static bool FurnitureAllowedFor(PetTownCozyFurniture f, PetTownInteriorState owner)
    {
        bool house = owner != null && owner.Kind == 1;
        return house ? !ShopOnlyFurniture(f) : !HomeOnlyFurniture(f);
    }
}
