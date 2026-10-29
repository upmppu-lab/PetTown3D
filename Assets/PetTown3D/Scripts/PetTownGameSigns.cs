using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Entrance signs for every building: a post, a name board that faces the camera, and a doormat at the entrance.
public partial class PetTownGame
{
    readonly List<PetTownSignTag> signTags = new List<PetTownSignTag>();
    Material signBoardMaterial, signMatMaterial;

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
        Vector3 pos = entrance + forward * 1.5f + side * 2.1f;
        pos.y = Ground(pos.x, pos.z);

        if (signBoardMaterial == null) { signBoardMaterial = new Material(wall) { name = "SignBoard" }; signBoardMaterial.color = new Color(.97f, .91f, .75f); }
        if (signMatMaterial == null) { signMatMaterial = new Material(wall) { name = "SignMat" }; signMatMaterial.color = new Color(.62f, .27f, .2f); }

        var sign = new GameObject("EntranceSign");
        sign.transform.SetParent(b.transform, true);
        sign.transform.position = pos;
        SignPart(PrimitiveType.Cube, "Post", sign.transform, new Vector3(0, .85f, 0), new Vector3(.14f, 1.7f, .14f), wood);

        var board = new GameObject("Board");
        board.transform.SetParent(sign.transform, false);
        board.transform.localPosition = new Vector3(0, 2.15f, 0);
        board.AddComponent<PetTownSignBoard>();
        float width = Mathf.Max(1.7f, label.Length * .42f + .6f);
        SignPart(PrimitiveType.Cube, "Frame", board.transform, new Vector3(0, 0, .03f), new Vector3(width + .16f, .96f, .06f), wood);
        SignPart(PrimitiveType.Cube, "Plate", board.transform, new Vector3(0, 0, -.01f), new Vector3(width, .8f, .06f), signBoardMaterial);

        var textObject = new GameObject("Label");
        textObject.transform.SetParent(board.transform, false);
        textObject.transform.localPosition = new Vector3(0, .02f, -.06f);
        var text = textObject.AddComponent<TextMeshPro>();
        var library = PetTownCozyLibrary.Current;
        if (library != null && library.style != null && library.style.font != null) text.font = library.style.font;
        text.text = label + "\n<size=55%>입구</size>";
        text.fontSize = 3.4f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(.28f, .17f, .1f);
        text.rectTransform.sizeDelta = new Vector2(width, .8f);
        text.raycastTarget = false;

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
