using System.Collections.Generic;
using UnityEngine;

// 2D game parity: free-roaming shop pets leave messes, the hero / cleaning staff sweep them, shy pets get stressed by crowds.
public partial class PetTownGame
{
    sealed class PetMess { public string id; public GameObject root; }
    readonly List<PetMess> petMess = new List<PetMess>();
    int petMessSeq;
    float messClock;
    Material messMaterial;
    const int MaxPetMess = 6;

    bool MessExists(string id)
    {
        if (id != null && id.StartsWith("hab:")) return HabDirt(id.Substring(4)) >= 30f;
        return petMess.Exists(m => m.id == id && m.root);
    }
    bool RemoveMess(string id)
    {
        if (id != null && id.StartsWith("hab:")) { SetHabDirt(id.Substring(4), 0f); return true; }
        int i = petMess.FindIndex(x => x.id == id); if (i < 0) return false;
        if (petMess[i].root) Destroy(petMess[i].root);
        petMess.RemoveAt(i); return true;
    }
    void SpawnMess(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "PetMess";
        var col = go.GetComponent<Collider>(); if (col) Destroy(col);
        go.transform.localScale = new Vector3(.24f, .12f, .24f);
        at.y += .04f; go.transform.position = at;
        if (messMaterial == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            messMaterial = new Material(sh); messMaterial.color = new Color(.36f, .22f, .1f);
        }
        go.GetComponent<Renderer>().sharedMaterial = messMaterial;
        MarkNavMeshIgnore(go);
        petMess.Add(new PetMess { id = "mess" + (++petMessSeq), root = go });
    }
    void TickPetMess(float seconds)
    {
        if (Progression == null || Progression.care == null || shop == null) return;
        for (int i = petMess.Count - 1; i >= 0; i--) if (!petMess[i].root) petMess.RemoveAt(i);
        if (hero) for (int i = petMess.Count - 1; i >= 0; i--)
            if ((petMess[i].root.transform.position - hero.transform.position).sqrMagnitude < 2.4f)
            { RemoveMess(petMess[i].id); money += Progression.AddExperience(1); progressionDirty = true; Message("오물을 치웠어요. +1 XP"); }
        messClock += seconds; if (messClock < 1f) return;
        float dt = messClock; messClock = 0;
        bool night = TimeWeather.Night;
        var pets = Progression.care.pets;
        if (!night && petMess.Count < MaxPetMess)
            for (int i = 0; i < livePets.Count; i++)
            {
                var lp = livePets[i]; if (lp == null || !lp.root || !lp.root.activeInHierarchy) continue;
                string cid = string.IsNullOrEmpty(lp.careId) ? lp.root.name + "_" + i : lp.careId;
                var rec = pets.Find(x => x.id == cid); if (rec == null || rec.displayOnly || rec.fullness < 15) continue;
                if (Random.value < dt / 90f) { SpawnMess(lp.root.transform.position); if (petMess.Count >= MaxPetMess) break; }
            }
        if (petMess.Count > 0)
        {
            foreach (var rec in pets) if (!rec.displayOnly) rec.cleanliness = Mathf.Max(0, rec.cleanliness - petMess.Count * .1f * dt / 60f);
            if (petMess.Count >= 3) Progression.serviceHappiness = Mathf.Max(-10f, Progression.serviceHappiness - .01f * dt);
        }
        // 펫 운동장(id 408)이 있으면 낮에 매장 펫의 심심함·스트레스가 천천히 풀림 (2D 놀이울타리 회복, 건물 레벨 배율)
        var playground = decorTags.Find(x => x && x.defId == 408);
        if (playground != null && !night)
        {
            float rk = DecorLvMul(playground.record) * dt / 60f;
            foreach (var rec in pets) if (!rec.displayOnly) { rec.boredom = Mathf.Max(0f, rec.boredom - 2f * rk); rec.stress = Mathf.Max(0f, rec.stress - .8f * rk); }
        }
        int near = 0; foreach (var v in villagers) if (v && (v.transform.position - shopPosition).sqrMagnitude < 196f) near++;
        if (!night && near >= 3) foreach (var rec in pets) if (!rec.displayOnly && rec.TraitIndex == 1) rec.stress = Mathf.Min(100, rec.stress + 2f * dt / 60f * .35f);
    }
}
