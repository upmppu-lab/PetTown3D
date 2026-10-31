using System.Collections.Generic;
using UnityEngine;

// Lake activity: a resident stands at the shore and skips stones into the water (wind-up, throw, small splash).
// Cosmetic only; removes itself when the visit stops refreshing it.
public sealed class PetTownStoneThrow : MonoBehaviour
{
    const float Cycle=3.4f,WindUp=.75f,Throw=.2f,HoldSeconds=.6f,Fade=.5f;
    Transform model,hips,spine,aur,alr,ahr;Vector3 baseLocalPosition;Quaternion baseLocalRotation,facing;Material stoneMaterial;
    float startedAt,lastHold,releaseAt;bool releasing,thrown;int lastCycle=-1;
    readonly List<Stone> stones=new List<Stone>();
    sealed class Stone{public Transform body;public Vector3 from,to;public float t;public bool splash;}

    public static void Hold(GameObject actor)
    {
        if(!actor)return;var s=actor.GetComponent<PetTownStoneThrow>();
        if(!s){s=actor.AddComponent<PetTownStoneThrow>();if(!s.Begin()){Destroy(s);return;}}
        s.lastHold=Time.unscaledTime;if(s.releasing){s.releasing=false;s.startedAt=Time.time;}
    }
    public static void Drop(GameObject actor){if(!actor)return;var s=actor.GetComponent<PetTownStoneThrow>();if(s&&!s.releasing){s.releasing=true;s.releaseAt=Time.time;}}
    bool Begin()
    {
        hips=Find("Hips");if(!hips||!hips.parent)return false;model=hips.parent;spine=Find("Spine");aur=Find("RightUpperArm");alr=Find("RightLowerArm");ahr=Find("RightHand");
        if(!aur||!alr||!ahr)return false;
        baseLocalPosition=model.localPosition;baseLocalRotation=model.localRotation;facing=Quaternion.Euler(0,transform.eulerAngles.y,0);startedAt=Time.time;lastHold=Time.unscaledTime;
        var renderer=GetComponentInChildren<Renderer>();if(renderer&&renderer.sharedMaterial){stoneMaterial=new Material(renderer.sharedMaterial);if(stoneMaterial.HasProperty("_BaseColor"))stoneMaterial.SetColor("_BaseColor",new Color(.45f,.45f,.47f));if(stoneMaterial.HasProperty("_BaseMap"))stoneMaterial.SetTexture("_BaseMap",null);}
        return true;
    }
    Transform Find(string bone){foreach(var t in GetComponentsInChildren<Transform>(true))if(string.Equals(t.name,bone,System.StringComparison.OrdinalIgnoreCase))return t;return null;}
    static float Smooth(float s){s=Mathf.Clamp01(s);return s*s*(3-2*s);}

    void LateUpdate()
    {
        UpdateStones();
        if(!model){Finish();return;}
        if(!releasing&&Time.unscaledTime-lastHold>HoldSeconds){releasing=true;releaseAt=Time.time;}
        float weight=releasing?Smooth(1-(Time.time-releaseAt)/Fade):Smooth((Time.time-startedAt)/Fade);
        model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;
        if(releasing&&weight<=.001f){Finish();return;}
        if(weight<=.001f)return;
        float elapsed=Time.time-startedAt;int cycle=Mathf.FloorToInt(elapsed/Cycle);float u=elapsed%Cycle;
        Vector3 f=facing*Vector3.forward,r=facing*Vector3.right;var shoulder=aur.position;Vector3 target;float lean=0;
        var rest=shoulder+Vector3.down*.52f+f*.05f+r*.12f;
        if(u<WindUp){float k=Smooth(u/WindUp);target=Vector3.Lerp(rest,shoulder-f*.28f+Vector3.up*.28f+r*.08f,k);lean=-6*k;}
        else if(u<WindUp+Throw){float k=Smooth((u-WindUp)/Throw);target=Vector3.Lerp(shoulder-f*.28f+Vector3.up*.28f+r*.08f,shoulder+f*.48f+Vector3.up*.04f,k);lean=-6+16*k;
            if(lastCycle!=cycle){lastCycle=cycle;Launch(target,f);}}
        else{float k=Smooth((u-WindUp-Throw)/.6f);target=Vector3.Lerp(shoulder+f*.48f+Vector3.up*.04f,rest,k);lean=10*(1-k);}
        model.rotation=Quaternion.Slerp(model.rotation,facing*baseLocalRotation,weight);
        if(spine)spine.rotation=Quaternion.AngleAxis(lean*weight,model.rotation*Vector3.right)*spine.rotation;
        PetTownSeatPose.Solve(aur,alr,ahr,target,-f*.35f+r*.7f-Vector3.up*.45f,weight);
    }
    void Launch(Vector3 from,Vector3 f)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);var col=go.GetComponent<Collider>();if(col)Destroy(col);
        go.transform.localScale=Vector3.one*.1f;go.transform.position=from;if(stoneMaterial)go.GetComponent<Renderer>().sharedMaterial=stoneMaterial;
        var ground=transform.position;var land=new Vector3(from.x,ground.y+.02f,from.z)+f*Random.Range(3.2f,4.6f);
        stones.Add(new Stone{body=go.transform,from=from,to=land});
    }
    void UpdateStones()
    {
        for(int i=stones.Count-1;i>=0;i--)
        {
            var s=stones[i];if(!s.body){stones.RemoveAt(i);continue;}
            s.t+=Time.deltaTime;
            if(!s.splash)
            {
                float k=Mathf.Clamp01(s.t/.9f);var p=Vector3.Lerp(s.from,s.to,k);p.y+=Mathf.Sin(k*Mathf.PI)*.9f;s.body.position=p;
                if(k>=1){s.splash=true;s.t=0;s.body.localScale=new Vector3(.2f,.02f,.2f);s.body.position=s.to;if(stoneMaterial){var m=s.body.GetComponent<Renderer>();m.sharedMaterial=stoneMaterial;}}
            }
            else
            {
                float k=s.t/.6f;s.body.localScale=new Vector3(.2f+.9f*k,.02f,.2f+.9f*k);if(k>=1){Destroy(s.body.gameObject);stones.RemoveAt(i);}
            }
        }
    }
    void Finish(){if(model){model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;}if(stones.Count==0)Destroy(this);else if(!releasing){releasing=true;releaseAt=Time.time;}}
    void OnDestroy()
    {
        if(model){model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;}
        foreach(var s in stones)if(s!=null&&s.body)Destroy(s.body.gameObject);
        if(stoneMaterial)Destroy(stoneMaterial);
    }
}
