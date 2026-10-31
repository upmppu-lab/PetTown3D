using UnityEngine;

// 2D parity (my home, scaled down): one 'our house' building. Pets marked as protected (favorite) are its family pets:
// every morning up to 2 x level of them rest at home and recover. Levels 1-5 (see DecorLevel* in PetTownGameDecor.cs).
public partial class PetTownGame
{
    public const int HomeId = 417;
    void RemoveHomeTags()
    {
        for (int i = decorTags.Count - 1; i >= 0; i--)
        {
            var t = decorTags[i];
            if (!t) { decorTags.RemoveAt(i); continue; }
            if (t.defId != HomeId) continue;
            decorSave.items.Remove(t.record); decorTags.RemoveAt(i); Destroy(t.gameObject);
        }
    }
    void HomeNewDay()
    {
        if (loadingTown || Progression == null || Progression.care == null) return;
        PetTownDecorTag home = null;
        foreach (var t in decorTags) if (t && t.defId == HomeId) { home = t; break; }
        if (home == null) return;
        int lv = DecorLv(home.record), cap = 2 * lv, n = 0;
        foreach (var p in Progression.care.pets)
        {
            if (n >= cap) break;
            if (p == null || p.displayOnly || !p.favorite) continue;
            p.fullness = Mathf.Min(100f, p.fullness + 15f * lv); p.cleanliness = Mathf.Min(100f, p.cleanliness + 15f * lv);
            p.stress = Mathf.Max(0f, p.stress - 10f * lv); p.boredom = Mathf.Max(0f, p.boredom - 10f * lv); n++;
        }
        if (n > 0) { progressionDirty = true; Message("우리 집 Lv." + lv + ": 가족 펫 " + n + "마리가 푹 쉬었어요."); }
    }
}
