using System.Collections.Generic;
using UnityEngine;

// "Level the ground" feature: when a building is blocked only because the terrain is too
// steep / uneven, ask the player whether to pay gold to flatten it, then build.
public partial class PetTownGame
{
    const int GradeGoldPerCubicMeter = 5;
    const int GradeMinCost = 100;

    Vector2 GradeCore(PetTownBuildingSaveRecord rec)
    {
        Vector2 core = PetTownBuildingDefinition.Default(rec.kind).footprintMeters;
        if (rec.kind == 2) core = new Vector2(9.2f, 9.2f);
        else if (rec.kind == 0 || rec.kind == 1 || rec.kind == 3 || rec.kind == 4) core += Vector2.one * 5f;
        if (Mathf.RoundToInt(rec.BuildingYaw / 90f) % 2 != 0) core = new Vector2(core.y, core.x);
        return Vector2.Max(core, rec.WorldFootprint) + Vector2.one * 2f;
    }

    int GradeCost(Vector3 center, Vector2 core)
    {
        float volume = 0f;
        for (float x = center.x - core.x * .5f; x <= center.x + core.x * .5f; x += 1f)
            for (float z = center.z - core.y * .5f; z <= center.z + core.y * .5f; z += 1f)
                volume += Mathf.Abs(Ground(x, z) - center.y);
        return Mathf.Clamp(GradeMinCost + Mathf.RoundToInt(volume * GradeGoldPerCubicMeter), GradeMinCost, 50000);
    }

    // Returns true when the confirmation window was shown (the caller should stop).
    bool PromptTerrainGrade(Vector3 rawPoint, Vector3 p, PetTownPlacementValidation v)
    {
        if (catalogWindow == null || !catalogWindow.Ready) return false;
        if (v.Code != "Slope" && v.Code != "HeightRange" && v.Code != "GradingSlope") return false;
        var rec = placementPlanner.Snapshot();
        Vector2 core = GradeCore(rec);
        int cost = GradeCost(p, core + Vector2.one * 12f);
        int build = mode == Mode.Start ? 0 : costs[selected];
        var entries = new List<PetTownCatalogEntry>
        {
            OperationsEntry(0, "지형을 평면으로 만들기", cost.ToString("N0") + " 골드",
                "약 " + Mathf.RoundToInt(core.x) + " × " + Mathf.RoundToInt(core.y) + " m 땅을 고릅니다",
                "평면으로 만들고 짓기",
                v.Message + "\n땅을 평평하게 만들면 이 자리에 건물을 지을 수 있어요.\n평탄화 " + cost.ToString("N0") + " 골드 + 건설비 " + build.ToString("N0") + " 골드가 듭니다.", 0),
            OperationsEntry(1, "취소", "", "다른 자리를 골라 주세요", "취소", "아무것도 바뀌지 않아요.", 3)
        };
        catalogWindow.Show("지형을 평면으로 만드시겠습니까?", entries, e =>
        {
            if (e.Key == 0) ApplyTerrainGrade(rawPoint, cost);
        }, () => { }, () => { });
        return true;
    }

    void ApplyTerrainGrade(Vector3 rawPoint, int cost)
    {
        Vector3 p = BuildingPlacementPoint(rawPoint);
        var rec = placementPlanner.Snapshot();
        Vector2 core = GradeCore(rec);
        int build = mode == Mode.Start ? 0 : costs[selected];
        cost = GradeCost(p, core + Vector2.one * 12f);
        if (money < cost + build) { Message("코인이 부족해요. 필요한 금액: " + (cost + build).ToString("N0") + " 골드"); return; }

        GameObject terrainObj = GameObject.Find("EDITABLE_Terrain_Mesh");
        MeshFilter mf = terrainObj != null ? terrainObj.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) { Message("지형을 찾지 못했어요."); return; }
        Vector3[] backup = mf.sharedMesh.vertices;
        int zones = flatZones.Count;

        gradeMarginOverride = 6f;
        FlattenTerrain(p, core.x * .5f, core.y * .5f, p.y);
        gradeMarginOverride = 0f;
        var after = ValidateBuildingPlacement(BuildingPlacementPoint(rawPoint), true);
        if (!after.CanPlace)
        {
            // Roll back: leveling would not make this spot buildable, so nothing is charged.
            Mesh mesh = mf.sharedMesh;
            mesh.vertices = backup; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            while (flatZones.Count > zones) flatZones.RemoveAt(flatZones.Count - 1);
            MeshCollider mc = terrainObj.GetComponent<MeshCollider>();
            if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
            if (constructionGridOverlay != null) constructionGridOverlay.Invalidate();
            constructionWorldRevision++;
            if (placementValidator != null) placementValidator.Invalidate();
            Message("평탄화해도 지을 수 없어요: " + after.Message);
            return;
        }
        money -= cost;
        Message("땅을 평평하게 만들었어요 (-" + cost.ToString("N0") + " 골드)");
        Place(rawPoint);
    }
}
