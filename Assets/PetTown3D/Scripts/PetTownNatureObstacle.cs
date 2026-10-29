using System.Globalization;
using UnityEngine;

// Stable generated-world identity; no asset or source prefab is modified.
public sealed class PetTownNatureObstacle : MonoBehaviour
{
    public string Id=>gameObject.name+":"+transform.position.x.ToString("F3",CultureInfo.InvariantCulture)+":"+transform.position.z.ToString("F3",CultureInfo.InvariantCulture);
    public Rect WorldArea
    {
        get{var rr=GetComponentsInChildren<Renderer>();if(rr.Length==0)return new Rect(transform.position.x-.2f,transform.position.z-.2f,.4f,.4f);var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return new Rect(b.min.x,b.min.z,b.size.x,b.size.z);}
    }
    public static void RegisterGenerated()
    {
        foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if(go.name=="Replaceable_Tree"||go.name=="Replaceable_Rock"||go.name=="Grass_Clump"||go.name=="Grass_Patch")
                if(go.GetComponent<PetTownNatureObstacle>()==null)go.AddComponent<PetTownNatureObstacle>();
    }
}
