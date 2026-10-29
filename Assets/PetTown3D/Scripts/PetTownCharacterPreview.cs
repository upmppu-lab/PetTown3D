using System.Collections.Generic;
using UnityEngine;

// Visual-only preview: copies Transform, MeshFilter, MeshRenderer and SkinnedMeshRenderer.
// NEVER instantiates the player GameObject or its gameplay scripts.
public sealed class PetTownCharacterPreview : MonoBehaviour
{
    const int PreviewLayer = 30;
    GameObject previewRoot;
    Camera previewCamera;
    RenderTexture texture;
    Light previewLight;
    Transform source;
    float orbitYaw = 180f;
    float zoom = 1.5f;
    Vector3 viewCenter;
    bool keepCameraViewOnRebuild;
    readonly Dictionary<Transform, Transform> mapping = new Dictionary<Transform, Transform>();
    public RenderTexture Texture { get { return texture; } }

    public void SetVisible(bool visible)
    {
        if (visible && previewRoot == null) Build();
        if (previewCamera != null) previewCamera.enabled = visible;
    }

    void Build()
    {
        var original = GameObject.Find("PetTown_MasterCharacter");
        if (original == null) return;
        source = original.transform;
        previewRoot = new GameObject("PetTown_Preview_Visuals_Only");
        previewRoot.layer = PreviewLayer;
        previewRoot.transform.position = new Vector3(0f, -1000f, 0f);
        mapping.Clear();
        CopyTransforms(source, previewRoot.transform, true);
        CopyRenderers();
        // Separate camera; never touches Camera.main or the gameplay camera.
        texture = new RenderTexture(512, 640, 16, RenderTextureFormat.ARGB32);
        texture.name = "PetTown_Preview_RT";
        texture.Create();
        var camObject = new GameObject("PetTown_Preview_Camera_Only");
        camObject.transform.SetParent(transform, false);
        previewCamera = camObject.AddComponent<Camera>();
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(.16f, .19f, .20f, 1f);
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = zoom;
        previewCamera.nearClipPlane = .05f;
        previewCamera.farClipPlane = 30f;
        previewCamera.targetTexture = texture;
        // The imported model faces the opposite direction from the previous assumption.
        if (!keepCameraViewOnRebuild) viewCenter = previewRoot.transform.position + Vector3.up * 1.15f;
        keepCameraViewOnRebuild = false;
        UpdateView();
        var lightObject = new GameObject("PetTown_Preview_Light_Only");
        lightObject.transform.SetParent(transform, false);
        previewLight = lightObject.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.cullingMask = 1 << PreviewLayer;
        previewLight.intensity = 1.6f;
        previewLight.transform.rotation = Quaternion.Euler(30f, -20f, 0f);
        RefreshColors();
    }

    void LateUpdate()
    {
        if (previewRoot == null || source == null) return;
        foreach (var pair in mapping)
        {
            if (pair.Key == null || pair.Value == null || pair.Key == source) continue;
            pair.Value.localPosition = pair.Key.localPosition;
            pair.Value.localRotation = pair.Key.localRotation;
            pair.Value.localScale = pair.Key.localScale;
        }
    }

    void UpdateView()
    {
        if (previewCamera == null) return;
        float angle = orbitYaw * Mathf.Deg2Rad;
        previewCamera.transform.position = viewCenter + new Vector3(Mathf.Sin(angle) * 4f, .1f, -Mathf.Cos(angle) * 4f);
        previewCamera.transform.LookAt(viewCenter);
        previewCamera.orthographicSize = zoom;
    }

    public void Rotate(float degrees)
    {
        orbitYaw = Mathf.Repeat(orbitYaw + degrees, 360f);
        UpdateView();
    }

    public void Zoom(float delta)
    {
        zoom = Mathf.Clamp(zoom + delta, .18f, 3.5f);
        UpdateView();
    }

    // Move the preview camera target upward/downward without moving the player or hairstyle.
    public void PanVertical(float screenDragPixels)
    {
        if (previewRoot == null) return;
        viewCenter.y = Mathf.Clamp(viewCenter.y + screenDragPixels * zoom * .004f,
            previewRoot.transform.position.y + .3f,
            previewRoot.transform.position.y + 2.8f);
        UpdateView();
    }

    void CopyTransforms(Transform src, Transform parent, bool root)
    {
        Transform dst = parent;
        if (!root)
        {
            var obj = new GameObject(src.name);
            obj.layer = PreviewLayer;
            dst = obj.transform;
            dst.SetParent(parent, false);
            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
        else
        {
            dst.localScale = src.localScale;
        }
        mapping[src] = dst;
        foreach (Transform child in src)
        {
            // Unity Destroy() is deferred: never clone a disabled previous hairstyle.
            if (!child.gameObject.activeInHierarchy || child.name.StartsWith("PetTown_Selected_Hair_") && !child.gameObject.activeSelf) continue;
            CopyTransforms(child, dst, false);
        }
    }

    void CopyRenderers()
    {
        foreach (var pair in mapping)
        {
            Transform src = pair.Key, dst = pair.Value;
            var filter = src.GetComponent<MeshFilter>();
            var meshRenderer = src.GetComponent<MeshRenderer>();
            if (filter != null && meshRenderer != null)
            {
                dst.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var r = dst.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterials = meshRenderer.sharedMaterials;
                r.enabled = meshRenderer.enabled;
            }
            var skin = src.GetComponent<SkinnedMeshRenderer>();
            if (skin != null)
            {
                var r = dst.gameObject.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = skin.sharedMesh;
                r.sharedMaterials = skin.sharedMaterials;
                r.enabled = skin.enabled;
                r.localBounds = skin.localBounds;
                r.updateWhenOffscreen = true;
                var bones = new Transform[skin.bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    if (skin.bones[i] != null && mapping.TryGetValue(skin.bones[i], out var bone)) bones[i] = bone;
                r.bones = bones;
                if (skin.rootBone != null && mapping.TryGetValue(skin.rootBone, out var rootBone)) r.rootBone = rootBone;
            }
        }
    }

    // Rebuild the visual-only clone after a hairstyle changes.
    public void RefreshHair()
    {
        // Keep the user's zoom, orbit and vertical camera pan when hair is adjusted.
        keepCameraViewOnRebuild = previewRoot != null;
        if (previewCamera != null) { previewCamera.targetTexture = null; Destroy(previewCamera.gameObject); previewCamera = null; }
        if (previewLight != null) { Destroy(previewLight.gameObject); previewLight = null; }
        if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
        if (previewRoot != null) { previewRoot.SetActive(false); Destroy(previewRoot); previewRoot = null; }
        mapping.Clear();
        source = null;
        Build();
    }

    public void RefreshColors()
    {
        if (source == null || previewRoot == null) return;
        foreach (var pair in mapping)
        {
            var srcRenderers = pair.Key.GetComponents<Renderer>();
            var dstRenderers = pair.Value.GetComponents<Renderer>();
            for (int i = 0; i < Mathf.Min(srcRenderers.Length, dstRenderers.Length); i++)
            {
                var block = new MaterialPropertyBlock();
                srcRenderers[i].GetPropertyBlock(block);
                dstRenderers[i].SetPropertyBlock(block);
            }
        }
    }

    void OnDestroy()
    {
        if (previewCamera != null) previewCamera.targetTexture = null;
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (previewRoot != null) Destroy(previewRoot);
    }
}
