using System.Collections.Generic;
using UnityEngine;

// Conservative safety rules matching PetTownGame's procedural water geometry.
public static class PetTownWorldSafety
{
    public static bool IsWater(float x, float z, float margin = 0f)
    {
        float river = Mathf.Sin(z * .031f) * 12f + Mathf.Sin(z * .09f) * 3f;
        return Mathf.Abs(x - river) < 3.5f + margin ||
               (x > 52f - margin && x < 72f + margin && z > 35f - margin && z < 66f + margin);
    }

    public static bool CanPlaceBuilding(Vector3 center, int kind, List<GameObject> buildings, Vector2? footprintOverride = null)
    {
        float width = kind == 0 ? 15f : kind == 2 ? 7f : kind == 7 ? 22f : (kind == 5 || kind == 6 ? 15f : 11f);
        float depth = kind == 0 ? 13f : kind == 2 ? 7f : kind == 7 ? 22f : (kind == 5 || kind == 6 ? 15f : 10f);
        if (kind == 8) { width = 5f; depth = 5f; }
        if(footprintOverride.HasValue){width=footprintOverride.Value.x;depth=footprintOverride.Value.y;}
        for (float x = -width * .5f - .5f; x <= width * .5f + .5f; x += 1f)
            for (float z = -depth * .5f - .5f; z <= depth * .5f + .5f; z += 1f)
                if (IsWater(center.x + x, center.z + z)) return false;
        if (buildings != null)
            foreach (GameObject b in buildings)
            {
                if (b == null) continue;
                if (Mathf.Abs(center.x - b.transform.position.x) < width * .5f + 5f &&
                    Mathf.Abs(center.z - b.transform.position.z) < depth * .5f + 5f) return false;
            }
        return true;
    }
}
