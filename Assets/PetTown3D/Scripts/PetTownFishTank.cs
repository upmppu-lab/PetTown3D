using System.Collections.Generic;
using UnityEngine;

// 수족관 안에서 헤엄치는 주문 물고기(종류 8). 물고기는 NavMesh 없이 가구에 붙어 움직입니다.
public sealed class PetTownTankSwimmer : MonoBehaviour
{
    public string petId, slotId;
    public Vector3 center = new Vector3(0, 1.1f, 0);
    public float phase, speed = .6f, ampX = .7f, ampY = .13f, ampZ = .14f;
    void Update()
    {
        phase += Time.deltaTime * speed;
        var pos = center + new Vector3(Mathf.Sin(phase) * ampX, Mathf.Sin(phase * 1.6f) * ampY, Mathf.Cos(phase * .9f) * ampZ);
        var dir = new Vector3(Mathf.Cos(phase) * ampX, Mathf.Cos(phase * 1.6f) * ampY * 1.6f, -Mathf.Sin(phase * .9f) * ampZ * .9f);
        transform.localPosition = pos;
        if (dir.sqrMagnitude > .0001f) transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
    }
}

public partial class PetTownGame
{
    const int FishSpecies = 8;
    readonly Dictionary<string, GameObject> tankSwimmers = new Dictionary<string, GameObject>();
    bool fishNoTankHinted;

    void RefreshFishTanks()
    {
        if (Progression == null || Progression.care == null || !shop) return;
        var pets = Progression.care.pets;
        var tanks = ShopSlotFurniture().FindAll(f => f.Kind == 3);
        var dead = new List<string>();
        foreach (var pair in tankSwimmers) if (!pair.Value || !pets.Exists(p => p != null && p.id == pair.Key && p.speciesKind == FishSpecies)) dead.Add(pair.Key);
        foreach (var id in dead) { if (tankSwimmers[id]) Destroy(tankSwimmers[id]); tankSwimmers.Remove(id); }
        bool waiting = false;
        foreach (var pet in pets)
        {
            if (pet == null || pet.displayOnly || pet.speciesKind != FishSpecies) continue;
            PetTownPetCareRecord pc = pet;
            var slot = string.IsNullOrEmpty(pc.slotId) ? null : tanks.Find(f => f.Id == pc.slotId);
            if (slot == null)
            {
                if (!string.IsNullOrEmpty(pc.slotId)) { pc.slotId = ""; progressionDirty = true; }
                PetTownShopSlots.Rule rule;
                foreach (var tank in tanks)
                {
                    PetTownShopSlots.TryRule(tank.Kind, out rule);
                    string reason;
                    if (PetTownShopSlots.Assign(pc, tank.Id, rule, pets, out reason)) { slot = tank; progressionDirty = true; break; }
                }
            }
            if (slot == null)
            {
                waiting = true;
                GameObject old;
                if (tankSwimmers.TryGetValue(pc.id, out old) && old) Destroy(old);
                tankSwimmers.Remove(pc.id);
                continue;
            }
            GameObject cur;
            if (tankSwimmers.TryGetValue(pc.id, out cur) && cur)
            {
                var sw = cur.GetComponent<PetTownTankSwimmer>();
                if (sw != null && sw.slotId == slot.Id && cur.transform.parent == slot.transform) continue;
                Destroy(cur); tankSwimmers.Remove(pc.id);
            }
            var made = CreateTankSwimmer(pc, slot);
            if (made != null) tankSwimmers[pc.id] = made;
        }
        if (waiting && !fishNoTankHinted) { fishNoTankHinted = true; Message("물고기가 지낼 수족관이 없어요. 펫샵 안에 수족관을 놓아 주세요!"); }
        if (!waiting) fishNoTankHinted = false;
    }

    GameObject CreateTankSwimmer(PetTownPetCareRecord pet, PetTownFurniturePlacement tank)
    {
        var visual = PetVisual(pet.speciesKind, pet.appearance);
        if (visual == null || !visual.prefab) return null;
        var root = new GameObject("TANK_FISH_" + pet.id);
        root.transform.position = tank.transform.position + Vector3.up * 1.1f;
        MarkNavMeshIgnore(root);
        var model = Instantiate(visual.prefab, root.transform);
        model.name = "SourceModel"; model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.Euler(0, visual.yaw, 0);
        PreparePetSource(model);
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        var animator = model.GetComponentInChildren<Animator>(true); if (animator) animator.enabled = false;
        float growth = Mathf.Clamp01(pet.growth);
        float min, max;
        if (PetVisualModelHeight(root, out min, out max) && max - min > .00001f)
        {
            float scale = Mathf.Clamp(visual.height * Mathf.Lerp(.65f, 1f, growth) / (max - min), .001f, 1000);
            float pivot = model.transform.position.y;
            model.transform.localScale *= scale;
            float mid = (min + max) * .5f;
            model.transform.position += Vector3.up * (root.transform.position.y - (pivot + (mid - pivot) * scale));
        }
        root.transform.SetParent(tank.transform, true);
        var swim = root.AddComponent<PetTownTankSwimmer>();
        uint seed = 2166136261; foreach (char ch in pet.id) seed = unchecked((seed ^ ch) * 16777619);
        var rnd = new System.Random((int)(seed & 0x7fffffff));
        swim.petId = pet.id; swim.slotId = tank.Id; swim.phase = (float)rnd.NextDouble() * 6.28f;
        swim.speed = .45f + (float)rnd.NextDouble() * .3f;
        swim.center = new Vector3(((float)rnd.NextDouble() - .5f) * .2f, 1.08f + (float)rnd.NextDouble() * .22f, ((float)rnd.NextDouble() - .5f) * .1f);
        return root;
    }
}
