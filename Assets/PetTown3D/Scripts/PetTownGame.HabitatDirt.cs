using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownSlotDirt { public string id; public float dirt; }

// 2D parity (v1.11/v1.25): pets living in a cage or tank dirty it; a filthy enclosure stresses them. The hero / carers / cleaners clean it.
public partial class PetTownGame
{
    float habClock;
    static bool DirtyKind(int kind) { return kind == 2 || kind == 3; }
    float HabDirt(string id)
    {
        if (Progression == null || Progression.slotDirt == null) return 0f;
        var r = Progression.slotDirt.Find(x => x != null && x.id == id); return r == null ? 0f : r.dirt;
    }
    void SetHabDirt(string id, float v)
    {
        if (Progression == null) return;
        if (Progression.slotDirt == null) Progression.slotDirt = new List<PetTownSlotDirt>();
        var r = Progression.slotDirt.Find(x => x != null && x.id == id);
        if (r == null) { if (v <= 0f) return; r = new PetTownSlotDirt { id = id }; Progression.slotDirt.Add(r); }
        r.dirt = Mathf.Clamp(v, 0f, 100f); progressionDirty = true;
    }
    PetTownFurniturePlacement DirtyHabitat(float threshold)
    {
        PetTownFurniturePlacement best = null; float bd = threshold;
        foreach (var f in ShopSlotFurniture())
        {
            if (!DirtyKind(f.Kind) || TargetReserved("hab:" + f.Id)) continue;
            float d = HabDirt(f.Id); if (d >= bd) { bd = d; best = f; }
        }
        return best;
    }
    void TickHabitatDirt(float seconds)
    {
        if (Progression == null || Progression.care == null || !shop) return;
        habClock += seconds; if (habClock < 1f) return;
        float dt = habClock; habClock = 0f;
        bool night = TimeWeather.Night;
        foreach (var f in ShopSlotFurniture())
        {
            if (!DirtyKind(f.Kind)) continue;
            var occ = PetTownShopSlots.Occupants(Progression.care.pets, f.Id);
            float d = HabDirt(f.Id);
            if (occ.Count > 0) { d = Mathf.Min(100f, d + occ.Count * .2f * dt * (night ? .4f : 1f)); SetHabDirt(f.Id, d); }
            if (d >= 60f) foreach (var p in occ) p.stress = Mathf.Min(100f, p.stress + 1.5f * dt / 60f * .35f);
            if (hero && d >= 30f && (f.transform.position - hero.transform.position).sqrMagnitude < 6.25f)
            { SetHabDirt(f.Id, 0f); money += Progression.AddExperience(1); Message("우리를 깨끗하게 치웠어요. +1 XP"); }
        }
    }
}
