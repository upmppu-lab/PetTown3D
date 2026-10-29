using System;
using System.Collections.Generic;
using UnityEngine;

// Transfers reference garment skinning to an aligned, independently imported clothing mesh.
// Works in Unity at runtime; original model and animation assets remain untouched.
public static class PetTownAutoSkin
{
    struct Sample { public Vector3 position; public BoneWeight weight; }

    // Recompute bind poses whenever the fitted garment is moved/scaled in STYLE.
    // A bind pose is expressed relative to the mesh renderer transform, not the parent rig.
    public static void RefreshBindposes(GameObject garment)
    {
        if (!garment) return;
        foreach (var skin in garment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!skin.sharedMesh || skin.bones == null || skin.bones.Length == 0) continue;
            var mesh = skin.sharedMesh;
            var poses = new Matrix4x4[skin.bones.Length];
            bool valid = true;
            for (int i = 0; i < poses.Length; i++)
            {
                if (!skin.bones[i]) { valid = false; break; }
                poses[i] = skin.bones[i].worldToLocalMatrix * skin.transform.localToWorldMatrix;
            }
            if (!valid) continue;
            mesh.bindposes = poses;
            skin.sharedMesh = mesh;
        }
    }

    public static bool TryBind(GameObject garment, Transform reference, out string message)
    {
        message = "";
        if (garment == null || reference == null) { message = "기준 의상 없음"; return false; }
        var source = reference.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (source == null || source.sharedMesh == null || source.bones == null || source.bones.Length == 0)
        { message = "기준 의상에 SkinnedMeshRenderer/본 정보 없음"; return false; }
        Mesh sourceMesh = source.sharedMesh;
        Vector3[] sv;
        BoneWeight[] sw;
        try
        {
            sw = sourceMesh.boneWeights;
            // BakeMesh samples the animated reference in the CURRENT pose.
            // Raw sourceMesh.vertices are bind-pose coordinates and must not be
            // compared to a visible, posed garment in world space.
            var baked = new Mesh();
            source.BakeMesh(baked);
            sv = baked.vertices;
            UnityEngine.Object.Destroy(baked);
        }
        catch (Exception ex) { message = "기준 메시 읽기 실패: " + ex.Message; return false; }
        if (sv.Length == 0 || sw.Length != sv.Length) { message = "기준 웨이트 정보 없음"; return false; }
        var samples = new Sample[sv.Length];
        Matrix4x4 sourceToWorld = source.transform.localToWorldMatrix;
        for (int i = 0; i < sv.Length; i++) samples[i] = new Sample { position = sourceToWorld.MultiplyPoint3x4(sv[i]), weight = sw[i] };

        var filters = garment.GetComponentsInChildren<MeshFilter>(true);
        
        int converted = 0;
        foreach (var filter in filters)
        {
            if (filter == null || filter.sharedMesh == null) continue;
            var mr = filter.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            Mesh original = filter.sharedMesh;
            Vector3[] vertices;
            try { vertices = original.vertices; }
            catch (Exception ex) { message = "새 의상 메시 읽기 실패: " + ex.Message; return false; }
            if (vertices.Length == 0) continue;
            Mesh mesh = UnityEngine.Object.Instantiate(original);
            mesh.name = original.name + "_PetTownAutoSkinned";
            var weights = new BoneWeight[vertices.Length];
            Matrix4x4 garmentToWorld = filter.transform.localToWorldMatrix;
            // Nearest-reference-vertex transfer: automatic starting weights; difficult garments may need cleanup.
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = garmentToWorld.MultiplyPoint3x4(vertices[i]);
                int nearest = 0; float best = float.PositiveInfinity;
                for (int j = 0; j < samples.Length; j++)
                {
                    float d = (world - samples[j].position).sqrMagnitude;
                    if (d < best) { best = d; nearest = j; }
                }
                weights[i] = samples[nearest].weight;
            }
            mesh.boneWeights = weights;
            var bindposes = new Matrix4x4[source.bones.Length];
            for (int b = 0; b < bindposes.Length; b++)
            {
                if (source.bones[b] == null) { message = "기준 본 누락"; UnityEngine.Object.Destroy(mesh); return false; }
                bindposes[b] = source.bones[b].worldToLocalMatrix * filter.transform.localToWorldMatrix;
            }
            mesh.bindposes = bindposes;
            // Cache materials BEFORE adding another renderer. Adding a component may
            // trigger garment setup callbacks that destroy the original MeshRenderer.
            Material[] originalMaterials = mr.sharedMaterials;
            GameObject targetObject = filter.gameObject;
            var skin = targetObject.GetComponent<SkinnedMeshRenderer>();
            if (skin == null) skin = targetObject.AddComponent<SkinnedMeshRenderer>();
            skin.bones = source.bones;
            skin.rootBone = source.rootBone != null ? source.rootBone : source.bones[0];
            skin.sharedMesh = mesh;
            skin.sharedMaterials = originalMaterials;
            skin.updateWhenOffscreen = true;
            skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            // Unity's destroyed-object comparison is required here: AddComponent
            // can invoke other scripts which remove the previous renderer.
            if (mr != null) mr.enabled = false;

            converted++;
        }
        if (converted == 0) { message = "변환할 MeshRenderer가 없음"; return false; }
        message = "자동 웨이트 적용: 메시 " + converted + "개";
        return true;
    }
}
