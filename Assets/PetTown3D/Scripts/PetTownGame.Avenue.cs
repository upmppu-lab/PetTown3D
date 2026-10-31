using UnityEngine;

// 2D parity (street trees): an avenue of six trees with lamp posts between them. Tree colour follows the season (7-day seasons).
public partial class PetTownGame
{
    public const int AvenueId = 418;
    static readonly Color[] AvenueTints = { new Color(1f, .78f, .84f), new Color(.72f, .95f, .55f), new Color(1f, .55f, .22f), new Color(.86f, .91f, 1f) };

    float BuildAvenue(Transform root, PetTownDecorDef def, PetTownDecorRecord rec)
    {
        var prefab = Resources.Load<GameObject>("Decor/ParkTree1");
        var pole = new Material(wall) { name = "AvenuePole" }; pole.color = new Color(.22f, .22f, .25f);
        var glow = new Material(wall) { name = "AvenueLamp" }; glow.color = new Color(1f, .9f, .6f);
        glow.EnableKeyword("_EMISSION"); if (glow.HasProperty("_EmissionColor")) glow.SetColor("_EmissionColor", new Color(1f, .8f, .4f) * 1.5f);
        var trunk = new Material(wall) { name = "AvenueTrunk" }; trunk.color = new Color(.4f, .28f, .18f);
        const int trees = 6; const float step = 4f; float start = -(trees - 1) * step * .5f;
        for (int i = 0; i < trees; i++)
        {
            float lx = start + i * step;
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab, root);
                inst.name = "AvenueTree_" + i;
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.Euler(0, (i * 67) % 360, 0) * prefab.transform.rotation;
                StripPhysics(inst);
                FitDecor(inst, root, def.height * (.9f + .1f * (i % 3)));
                Vector3 wp = inst.transform.position + root.TransformVector(new Vector3(lx, 0, 0));
                inst.transform.position = new Vector3(wp.x, Ground(wp.x, wp.z) + (inst.transform.position.y - root.position.y), wp.z);
            }
            else
            {
                SignPart(PrimitiveType.Cylinder, "AvenueTree_trunk" + i, root, new Vector3(lx, 1.4f, 0), new Vector3(.4f, 1.4f, .4f), trunk);
                SignPart(PrimitiveType.Sphere, "AvenueTree_crown" + i, root, new Vector3(lx, 3.6f, 0), new Vector3(2.6f, 2.6f, 2.6f), pole);
            }
            if (i < trees - 1)
            {
                float mx = lx + step * .5f;
                Vector3 lw = root.TransformPoint(new Vector3(mx, 0, 1.2f));
                float gy = Ground(lw.x, lw.z) - root.position.y;
                SignPart(PrimitiveType.Cylinder, "LampPole" + i, root, new Vector3(mx, gy + 1.6f, 1.2f), new Vector3(.12f, 1.6f, .12f), pole);
                SignPart(PrimitiveType.Sphere, "LampLight" + i, root, new Vector3(mx, gy + 3.3f, 1.2f), new Vector3(.45f, .45f, .45f), glow);
            }
        }
        ApplyAvenueSeason(root.gameObject);
        return def.height;
    }

    static bool UnderAvenueTree(Transform t, Transform stop)
    {
        for (; t != null && t != stop; t = t.parent) if (t.name.StartsWith("AvenueTree")) return true;
        return false;
    }

    void ApplyAvenueSeason(GameObject go)
    {
        if (!go) return;
        Color c = AvenueTints[Mathf.Clamp(PetTownFarmState.SeasonOf(day), 0, 3)];
        var mpb = new MaterialPropertyBlock();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is TMPro.TMP_Text || r.GetComponent<TMPro.TMP_Text>() != null || !UnderAvenueTree(r.transform, go.transform.parent)) continue;
            r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c); r.SetPropertyBlock(mpb);
        }
    }

    void RefreshAvenueSeasons()
    {
        foreach (var t in decorTags) if (t && t.defId == AvenueId) ApplyAvenueSeason(t.gameObject);
    }
}
