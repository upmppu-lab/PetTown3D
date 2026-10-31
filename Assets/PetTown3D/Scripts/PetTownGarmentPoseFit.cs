using System;
using UnityEngine;

// Experimental pose fitting for unrigged clothing. This runs BEFORE weight transfer.
// Uses the reference skinned garment as a pose guide, without modifying the master rig.
public static class PetTownGarmentPoseFit
{
    static Vector3 Mid(Bounds b) { return b.center; }
    static Bounds BoundsOf(Renderer[] renderers)
    {
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in renderers) { if (!r || !r.enabled) continue; if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        return b;
    }
    static Transform Bone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name || t.name.EndsWith(":" + name, StringComparison.Ordinal)) return t;
        return null;
    }
    static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a; float d = ab.sqrMagnitude;
        return d < 1e-9f ? a : a + Mathf.Clamp01(Vector3.Dot(p - a, ab) / d) * ab;
    }
    static float Influence(Vector3 p, Vector3 a, Vector3 b, float radius)
    {
        float distance = Vector3.Distance(p, Closest(p, a, b));
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / Mathf.Max(radius, .001f)));
    }
    static Vector3 RemapLimb(Vector3 point, Vector3 garmentA, Vector3 garmentB, Vector3 bodyA, Vector3 bodyB)
    {
        Vector3 from = garmentB - garmentA, to = bodyB - bodyA;
        if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return point;
        float lengthRatio = Mathf.Clamp(to.magnitude / from.magnitude, .6f, 1.5f);
        Quaternion rotation = Quaternion.FromToRotation(from, to);
        Vector3 offset = point - garmentA;
        return bodyA + rotation * (offset * lengthRatio);
    }
    public static bool TryFit(GameObject garment, Transform master, Transform reference, bool top, out string status)
    {
        status = "";
        if (!garment || !master || !reference) { status = "대상 없음"; return false; }
        var refSkin = reference.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (!refSkin) { status = "기준 스킨 메시 없음"; return false; }
        string[] starts = top ? new[] { "LeftUpperArm", "RightUpperArm" } : new[] { "LeftUpperLeg", "RightUpperLeg" };
        string[] ends = top ? new[] { "LeftHand", "RightHand" } : new[] { "LeftLowerLeg", "RightLowerLeg" };
        Transform[] a = new Transform[2], b = new Transform[2];
        for (int i = 0; i < 2; i++) { a[i] = Bone(master, starts[i]); b[i] = Bone(master, ends[i]); if (!a[i] || !b[i]) { status = "본 탐색 실패: " + starts[i]; return false; } }
        // Estimate the garment's symmetric sleeve/leg axes in its current world bounds.
        // Arms: from upper side of torso toward lower outer edge. Legs: from hip to hem.
        var rs = garment.GetComponentsInChildren<Renderer>(true);
        Bounds gb = BoundsOf(rs);
        if (gb.size.sqrMagnitude < 1e-8f) { status = "의상 범위 없음"; return false; }
        Vector3[] ga = new Vector3[2], ge = new Vector3[2];
        float side = top ? gb.extents.x * .33f : gb.extents.x * .24f;
        for (int i = 0; i < 2; i++)
        {
            // Determine left/right using actual master bone positions, not name assumptions.
            float sign = Mathf.Sign(a[i].position.x - master.position.x);
            if (Mathf.Abs(sign) < .01f) sign = i == 0 ? -1 : 1;
            ga[i] = gb.center + new Vector3(sign * side, top ? gb.extents.y * .20f : gb.extents.y * .88f, 0);
            ge[i] = gb.center + new Vector3(sign * (top ? gb.extents.x * .90f : gb.extents.x * .30f), -gb.extents.y * (top ? .50f : .94f), 0);
        }
        int count = 0;
        foreach (var filter in garment.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter || !filter.sharedMesh || !filter.GetComponent<MeshRenderer>()) continue;
            Mesh mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = filter.sharedMesh.name + "_PoseFitted";
            Vector3[] vertices = mesh.vertices;
            Matrix4x4 toWorld = filter.transform.localToWorldMatrix, toLocal = filter.transform.worldToLocalMatrix;
            float radius = Mathf.Max(gb.extents.x * (top ? .75f : .70f), .02f);
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = toWorld.MultiplyPoint3x4(vertices[v]);
                Vector3 result = p; float best = 0f;
                for (int i = 0; i < 2; i++)
                {
                    float weight = Influence(p, ga[i], ge[i], radius);
                    if (top) weight *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(p.x - gb.center.x) - gb.extents.x * .20f) / (gb.extents.x * .35f)));
                    else weight *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((gb.center.y + gb.extents.y * .65f - p.y) / (gb.extents.y * .60f)));
                    if (weight > best) { best = weight; result = Vector3.Lerp(p, RemapLimb(p, ga[i], ge[i], a[i].position, b[i].position), weight); }
                }
                vertices[v] = toLocal.MultiplyPoint3x4(result);
            }
            mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            filter.sharedMesh = mesh; count++;
        }
        status = count > 0 ? "팔/다리 자세 보정 메시 " + count + "개" : "보정할 메시 없음";
        return count > 0;
    }
}
