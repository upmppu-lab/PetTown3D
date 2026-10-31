#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-click try-out of free-asset models for the new civic buildings (ids 405-416).
// Candidates live in Assets/PetTown3D/_CivicCandidates (already converted to URP Lit). 'Apply' copies them to
// Resources/Decor/<res>.prefab (the game picks them up automatically); 'Remove' deletes those copies so the
// simple placeholder buildings come back. Fronts/orientation could not be checked visually - look in Play mode.
public static class PetTownCivicModels
{
    static readonly string[][] Map = new string[][]
    {
        new[]{"Bakery", "Bakery_market_with_food"},
        new[]{"FleaMarket", "Vegetable_market_with_objects"},
        new[]{"PetPlayground", "SM_Playground_01"},
        new[]{"Lookout", "CastleTower"},
        new[]{"TownClinic", "small_house_1"},
        new[]{"PoliceStation", "mid_house_2"},
        new[]{"FireStation", "mid_house_4"},
        new[]{"Library", "office_building_1_with_base"},
        new[]{"PetAcademy", "mid_house_5"},
        new[]{"PetHotel", "office_building_2_with_base"},
    };
    const string Candidates = "Assets/PetTown3D/_CivicCandidates/", Target = "Assets/PetTown3D/Resources/Decor/";

    [MenuItem("PetTown3D/Civic models/Apply free-asset models")]
    public static void Apply()
    {
        int n = 0;
        foreach (var m in Map)
        {
            string from = Candidates + m[1] + ".prefab", to = Target + m[0] + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(from) == null) { Debug.LogWarning("missing candidate " + from); continue; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(to) != null) AssetDatabase.DeleteAsset(to);
            if (AssetDatabase.CopyAsset(from, to)) n++;
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("PetTown civic models applied: " + n);
    }

    [MenuItem("PetTown3D/Civic models/Remove (back to placeholders)")]
    public static void Remove()
    {
        int n = 0;
        foreach (var m in Map) if (AssetDatabase.DeleteAsset(Target + m[0] + ".prefab")) n++;
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("PetTown civic models removed: " + n);
    }
}
#endif
