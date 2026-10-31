using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Entrance signs for every building: a post, a name board that faces the camera, and a doormat at the entrance.
public partial class PetTownGame
{
    readonly List<PetTownSignTag> signTags = new List<PetTownSignTag>();
    Material signBoardMaterial, signFrameMaterial, signMatMaterial;

    // Procedural wood-grain texture so the sign reads as a wooden board without any imported asset.
    static Material WoodSignMaterial(Color baseColor, int seed)
    {
        const int W = 128, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "SignWood", wrapMode = TextureWrapMode.Repeat };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = Mathf.PerlinNoise(x * .03f + seed, y * .55f + seed) * .6f + Mathf.PerlinNoise(x * .11f + seed, y * 1.7f) * .4f;
                float ring = Mathf.Sin((y + n * 14f) * .55f) * .5f + .5f;
                float k = .78f + ring * .16f + (n - .5f) * .16f;
                if (y < 2 || y >= H - 2) k *= .85f;
                px[y * W + x] = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f);
            }
        tex.SetPixels(px); tex.Apply();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { name = "SignWoodMat" };
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", .05f);
        return m;
    }

    List<PetTownSignTag> FindSignTags()
    {
        signTags.RemoveAll(t => !t);
        return signTags;
    }

    static string SignName(int kind)
    {
        switch (kind)
        {
            case 0: return "펫샵";
            case 1: return "우리 집";
            case 2: return "주민 주택";
            case 3: return "펫 카페";
            case 4: return "동물병원";
            case 5: return "공원";
            case 6: return "호수";
            case 7: return "동물원";
            case 9: return "농장";
            case 10: return "목장";
        }
        return null;
    }

    static GameObject SignPart(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        var collider = part.GetComponent<Collider>();
        if (collider) Object.Destroy(collider);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        if (renderer && material) renderer.sharedMaterial = material;
        if (renderer) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part;
    }

    // Called every couple of seconds: gives any building that has no sign yet one at its entrance.
    public void SignTick()
    {
        if (loadingTown || expansionPending || !wood) return;
        ClearTreesOverlappingBuildings();
        foreach (var b in buildings)
        {
            if (!b || !b.activeInHierarchy || b.name.StartsWith("Dark_") || b.name.StartsWith("Road")) continue;
            if (b.transform.Find("EntranceSign")) continue;
            int kind = BuildingKindOf(b);
            string label = SignName(kind);
            if (label == null) continue;
            BuildSign(b, kind, label);
        }
    }

    void BuildSign(GameObject b, int kind, string label)
    {
        var placement = b.GetComponent<PetTownBuildingPlacement>();
        var interior = b.GetComponent<PetTownInteriorState>();
        Vector3 center = b.transform.position, entrance, forward;
        if (placement != null && placement.Record != null) { entrance = placement.EntranceWorld; forward = placement.EntranceForward; }
        else if (interior != null) { entrance = b.transform.TransformPoint(interior.EntranceLocal); forward = entrance - center; }
        else { entrance = center + Vector3.back * 3.5f; forward = Vector3.back; }
        Vector3 outward = entrance - center; outward.y = 0;
        if (outward.magnitude > 1.5f) forward = outward;
        forward.y = 0;
        if (forward.sqrMagnitude < .01f) forward = Vector3.back;
        forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 pos = entrance + forward * .8f;
        pos.y = Ground(pos.x, pos.z);

        if (signBoardMaterial == null) { signBoardMaterial = WoodSignMaterial(new Color(.86f, .66f, .40f), 11); }
        if (signFrameMaterial == null) { signFrameMaterial = WoodSignMaterial(new Color(.46f, .29f, .16f), 23); }
        if (signMatMaterial == null) { signMatMaterial = new Material(wall) { name = "SignMat" }; signMatMaterial.color = new Color(.62f, .27f, .2f); }

        var sign = new GameObject("EntranceSign");
        sign.transform.SetParent(b.transform, true);
        sign.transform.position = pos;
        var pill = PetTownPillLabel.Create(sign.transform, label, new Vector3(0, 3.0f, 0), 3.2f);
        var board = pill.gameObject;
        if (kind == 2) StartCoroutine(PlaceHouseLabelLater(b));

        // Doormat on the ground in front of the door shows where to walk in.
        var mat = SignPart(PrimitiveType.Cube, "Doormat", sign.transform, Vector3.zero, new Vector3(2.2f, .05f, 1.1f), signMatMaterial);
        Vector3 matWorld = entrance + forward * .7f;
        matWorld.y = Ground(matWorld.x, matWorld.z) + .04f;
        mat.transform.position = matWorld;
        mat.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        MarkNavMeshIgnore(sign);
        var tag = sign.AddComponent<PetTownSignTag>();
        tag.building = b;
        tag.kind = kind;
        tag.board = board.transform;
        signTags.Add(tag);
    }
}
