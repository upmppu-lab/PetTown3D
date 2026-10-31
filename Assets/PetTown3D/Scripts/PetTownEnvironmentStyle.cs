using System.Collections.Generic;
using UnityEngine;

// Renderer-only adapter. Never changes terrain vertices, water UVs, colliders or root positions.
public sealed class PetTownEnvironmentStyle : MonoBehaviour
{
    sealed class Surface {public Renderer renderer;public Material original,replacement;}
    readonly List<Surface> surfaces=new List<Surface>();
    readonly List<Renderer> hidden=new List<Renderer>();readonly List<GameObject> visuals=new List<GameObject>();
    public int StyledNatureCount=>visuals.Count;
    public void Apply(Renderer ground,Renderer water,Renderer banks,IEnumerable<PetTownNatureObstacle> nature)
    {
        Restore();bool cozy=PetTownCozyLibrary.Current!=null;var library=cozy?PetTownForestLibrary.Current:null;Replace(ground,cozy?"Environment/PetTownCozyMeadow":"Environment/PetTownGroundNatural",library?library.groundMaterial:null);Replace(water,cozy?"Environment/PetTownCozyStream":"Environment/PetTownRiverNatural");Replace(banks,cozy?"Environment/PetTownCozyStream":"Environment/PetTownRiverNatural");
        foreach(var item in nature)
        {
            if(item==null||!item.gameObject.activeInHierarchy)continue;
            string path;bool tree=item.name.Contains("Tree"),rock=item.name.Contains("Rock");
            int variant=Mathf.Abs(Mathf.RoundToInt(item.transform.position.x*31+item.transform.position.z*17));
            path=tree?(variant%2==0?"CommonTree_1":"CommonTree_3"):rock?"Rock_Medium_1":variant%9==0?"Bush_Common_Flowers":variant%7==0?"Bush_Common":variant%3==0?"Flower_3_Group":"Grass_Common_Short";
            var forest=cozy?PetTownForestLibrary.Current:null;
            var prefab=forest?(tree?forest.trees[variant%forest.trees.Length]:forest.Find(path)):Resources.Load<GameObject>("Environment/"+path);if(prefab==null)prefab=Resources.Load<GameObject>("Environment/"+path);if(prefab==null)continue;
            var old=item.GetComponentsInChildren<Renderer>();if(old.Length==0)continue;var bounds=old[0].bounds;foreach(var renderer in old)bounds.Encapsulate(renderer.bounds);
            var coverage=forest?forest.Coverage(item.transform.position.x,item.transform.position.z):Color.white;
            if(forest&&forest.approvedMeadow&&!tree&&!rock&&coverage.r>.55f){foreach(var renderer in old)if(renderer.enabled){renderer.enabled=false;hidden.Add(renderer);}continue;}
            float height=tree?(cozy?Mathf.Clamp(bounds.size.y*1.2f,4.5f,6.6f):bounds.size.y):rock?Mathf.Clamp(bounds.size.y,.4f,1.2f):path.StartsWith("Bush")?.65f:path.StartsWith("Flower")?.32f:.24f;
            if(forest&&forest.approvedMeadow&&!tree&&!rock)height*=Mathf.Lerp(.45f,1,coverage.b);
            var visual=Instantiate(prefab,item.transform);visual.name="PetTown_Natural_Visual";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            var rr=visual.GetComponentsInChildren<Renderer>();if(rr.Length==0){Destroy(visual);continue;}var b=rr[0].bounds;foreach(var renderer in rr)b.Encapsulate(renderer.bounds);
            if(b.size.y<.001f){Destroy(visual);continue;}
            visual.transform.localScale*=height/b.size.y;b=rr[0].bounds;foreach(var renderer in rr)b.Encapsulate(renderer.bounds);
            visual.transform.position+=new Vector3(item.transform.position.x-b.center.x,item.transform.position.y-b.min.y,item.transform.position.z-b.center.z);
            foreach(var c in visual.GetComponentsInChildren<Collider>()){c.enabled=false;Destroy(c);}
            var mod=visual.AddComponent<Unity.AI.Navigation.NavMeshModifier>();mod.ignoreFromBuild=true;
            foreach(var renderer in old)if(renderer.enabled){renderer.enabled=false;hidden.Add(renderer);}
            visuals.Add(visual);
        }
        if(cozy){var game=FindFirstObjectByType<PetTownGame>();var landscape=gameObject.AddComponent<PetTownCozyLandscape>();landscape.Initialize(game);gameObject.AddComponent<PetTownCozyLighting>().Apply();gameObject.AddComponent<PetTownGrassField>().Initialize(game);gameObject.AddComponent<PetTownTimeWeatherView>().Initialize(game);}
    }
    void Replace(Renderer renderer,string resource,Material template=null)
    {
        var shader=template?template.shader:Resources.Load<Shader>(resource);if(renderer==null||shader==null||!shader.isSupported)return;
        var original=renderer.sharedMaterial;if(original==null)return;var replacement=new Material(template?template:original);replacement.shader=shader;replacement.name=original.name+"_Natural_Runtime";
        if(replacement.HasProperty("_Tint"))replacement.SetColor("_Tint",new Color(1.12f,1.12f,1.03f));
        var library=PetTownForestLibrary.Current;
        if(library&&library.meadowMask&&replacement.HasProperty("_Coverage")){replacement.SetTexture("_Coverage",library.meadowMask);replacement.SetVector("_CoverageBounds",library.meadowBounds);replacement.SetFloat("_UseCoverage",1);}
        if(library&&library.meadowMask&&replacement.HasProperty("_MeadowCoverage")){replacement.SetTexture("_MeadowCoverage",library.meadowMask);replacement.SetVector("_CoverageBounds",library.meadowBounds);replacement.SetFloat("_UseCoverage",1);}
        renderer.sharedMaterial=replacement;surfaces.Add(new Surface{renderer=renderer,original=original,replacement=replacement});
    }
    public void Restore()
    {
        foreach(var landscape in GetComponents<PetTownCozyLandscape>()){landscape.Clear();Destroy(landscape);}
        foreach(var lighting in GetComponents<PetTownCozyLighting>()){lighting.Restore();Destroy(lighting);}
        foreach(var field in GetComponents<PetTownGrassField>()){field.enabled=false;Destroy(field);}
        foreach(var value in surfaces){if(value.renderer!=null)value.renderer.sharedMaterial=value.original;if(value.replacement!=null)Destroy(value.replacement);}surfaces.Clear();
        foreach(var renderer in hidden)if(renderer!=null)renderer.enabled=true;hidden.Clear();foreach(var visual in visuals)if(visual!=null){visual.SetActive(false);Destroy(visual);}visuals.Clear();
    }
    void OnDestroy(){Restore();}
}
