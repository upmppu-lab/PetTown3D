using System.Collections.Generic;
using UnityEngine;

// Road types and the separate "road" button (no longer part of the construction list).
public partial class PetTownGame
{
    public const int RoadButtonRoute = 120;
    public const int BuyPetButtonRoute = 121;
    int roadBuildType;
    readonly Dictionary<int, Material> roadTypeMaterials = new Dictionary<int, Material>();
    readonly List<Texture2D> roadSwatches = new List<Texture2D>();

    Material RoadMaterialFor(int type)
    {
        type = Mathf.Clamp(type, 0, PetTownRoadTypes.Count - 1);
        Material material;
        if (roadTypeMaterials.TryGetValue(type, out material) && material) return material;
        var baseMaterial = RoadSurfaceMaterial();
        material = new Material(baseMaterial) { name = "RoadType" + type };
        var def = PetTownRoadTypes.Get(type);
        if (material.HasProperty("_Tint"))
        {
            material.SetColor("_Tint", def.tint);
            material.SetColor("_Alt", def.alt);
            material.SetFloat("_Pattern", def.pattern);
            material.SetFloat("_Cell", def.cell);
        }
        roadTypeMaterials[type] = material;
        return material;
    }

    bool RoadTypeUnlocked(int type)
    {
        return Progression == null || Progression.playerLevel >= PetTownRoadTypes.Get(type).unlockLevel;
    }

    // Opens the road list. Choosing one switches to road-drawing mode with that road type.
    void OpenRoadChooser()
    {
        if (catalogWindow == null || !catalogWindow.Ready)
        {
            roadBuildType = 0;
            SetMode(Mode.Build);
            selected = 8;
            uiShopOpen = false;
            return;
        }
        SetMode(Mode.Build);
        uiShopOpen = false;
        selected = -1;
        ClearGhost();
        if (constructionGridOverlay != null) constructionGridOverlay.Hide();
        foreach (var old in roadSwatches) if (old) Destroy(old);
        roadSwatches.Clear();
        var entries = new List<PetTownCatalogEntry>();
        for (int i = 0; i < PetTownRoadTypes.Count; i++)
        {
            int type = i;
            var def = PetTownRoadTypes.Get(i);
            bool open = RoadTypeUnlocked(i);
            var swatch = PetTownRoadTypes.Swatch(i);
            roadSwatches.Add(swatch);
            entries.Add(new PetTownCatalogEntry
            {
                Key = i,
                Name = def.name,
                Category = PetTownRoadTypes.Paved(i) ? "포장길" : "흙길",
                Price = def.cost,
                PriceUnit = "/m",
                Thumbnail = swatch,
                Footprint = Vector2.zero,
                Description = def.description + "\n폭 " + def.width.ToString("0.0") + "m" + (open ? "" : "\n플레이어 Lv." + def.unlockLevel + "에 열립니다.") + "\n드래그로 그리고, 자유 곡선과 격자 직선 모드를 고를 수 있어요."
            });
        }
        catalogWindow.Show("길 종류", entries, entry =>
        {
            if (!RoadTypeUnlocked(entry.Key))
            {
                Message("이 길은 플레이어 Lv." + PetTownRoadTypes.Get(entry.Key).unlockLevel + "에 열려요.");
                OpenRoadChooser();
                return;
            }
            roadBuildType = entry.Key;
            selected = 8;
            uiShopOpen = false;
            uiActionBuiltFor = -999;
            ghostBuildFor = -1;
            Message(PetTownRoadTypes.Get(roadBuildType).name + "을(를) 그릴 수 있어요. 드래그해서 길을 만들어 보세요.");
        }, () => SetMode(Mode.Play), () => { useNewConstructionCatalog = false; uiActionBuiltFor = -999; });
        uiActionBuiltFor = -999;
    }
}
