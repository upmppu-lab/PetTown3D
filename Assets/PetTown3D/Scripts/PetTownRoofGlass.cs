using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Every building keeps a roof that is always visible but see-through, so the inside can be seen from the camera.
// The real Toggle_Roof keeps its old show/hide behaviour; a ghost copy stands in while the real roof is hidden.
public sealed class PetTownRoofGlass : MonoBehaviour
{
    const float Opacity = .22f, Interval = 1.2f;
    PetTownGame game;
    float next;
    readonly Dictionary<Transform, Transform> ghosts = new Dictionary<Transform, Transform>();
    readonly Dictionary<Material, Material> glass = new Dictionary<Material, Material>();

    public void Initialize(PetTownGame source) { game = source; }

    void Update()
    {
        if (!game || Time.unscaledTime < next) return;
        next = Time.unscaledTime + Interval;
        var dead = new List<Transform>();
        foreach (var pair in ghosts) if (!pair.Key || !pair.Value) dead.Add(pair.Key);
        foreach (var key in dead)
        {
            Transform ghost;
            if (key != null && ghosts.TryGetValue(key, out ghost) && ghost) Destroy(ghost.gameObject);
            ghosts.Remove(key);
        }
        foreach (var building in game.BuildingObjects)
        {
            if (!building) continue;
            foreach (var t in building.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Toggle_Roof") continue;
                Transform ghost;
                if (!ghosts.TryGetValue(t, out ghost) || !ghost)
                {
                    ghost = Make(t);
                    if (!ghost) continue;
                    ghosts[t] = ghost;
                }
                // The solid roof never draws; the see-through copy is always shown instead (roof exists but the inside stays visible).
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
                if (ghost.gameObject.activeSelf) ghost.gameObject.SetActive(false); // 지붕은 완전히 투명(안 그림)
            }
        }
    }

    Transform Make(Transform original)
    {
        if (!original.parent) return null;
        var copy = Instantiate(original.gameObject, original.parent);
        copy.name = "GhostRoof";
        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var n in copy.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) Destroy(n);
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
        {
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = Ghost(materials[i]);
            r.sharedMaterials = materials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        copy.SetActive(false);
        return copy.transform;
    }

    Material Ghost(Material source)
    {
        if (!source) return source;
        Material result;
        if (glass.TryGetValue(source, out result) && result) return result;
        result = new Material(source) { name = source.name + "_Glass" };
        result.SetFloat("_Surface", 1);
        result.SetFloat("_Blend", 0);
        result.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        result.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        result.SetFloat("_ZWrite", 0);
        result.SetOverrideTag("RenderType", "Transparent");
        result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        result.DisableKeyword("_ALPHATEST_ON");
        result.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        result.renderQueue = (int)RenderQueue.Transparent;
        result.SetShaderPassEnabled("ShadowCaster", false);
        var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
        color.a = Opacity;
        if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
        glass[source] = result;
        return result;
    }

    void OnDestroy()
    {
        foreach (var g in ghosts.Values) if (g) Destroy(g.gameObject);
        foreach (var m in glass.Values) if (m) Destroy(m);
    }
}
