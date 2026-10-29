using UnityEngine;

// Cosmetic pet actions (eat, treatment, sit, groom, happy) that work for any pet model: the model child is pitched, wiggled
// and squashed a little, never moved, so grounding and navigation stay untouched. Weight fades in/out and the component
// removes itself when the action stops being refreshed.
[DefaultExecutionOrder(200)]
public sealed class PetTownPetAction : MonoBehaviour
{
    public const int Eat=1,Treat=2,Sit=3,Groom=4,Happy=5;
    int kind;float started,until,lastHold,end;bool timed;Transform model;Quaternion baseRotation;

    // One-shot: play for 'seconds'.
    public static void Do(GameObject root,int kind,float seconds)
    {
        var a=Prepare(root,kind);if(!a)return;a.timed=true;a.until=Time.time+seconds;
    }
    // Held: call every frame while the action should continue.
    public static void Hold(GameObject root,int kind)
    {
        var a=Prepare(root,kind);if(!a)return;a.timed=false;a.lastHold=Time.unscaledTime;
    }
    static PetTownPetAction Prepare(GameObject root,int kind)
    {
        if(!root)return null;var a=root.GetComponent<PetTownPetAction>();
        if(!a){a=root.AddComponent<PetTownPetAction>();a.model=FindModel(root.transform);if(!a.model){Destroy(a);return null;}a.baseRotation=a.model.localRotation;a.started=Time.time;}
        if(a.kind!=kind){a.kind=kind;a.started=Time.time;}a.end=0;return a;
    }
    static Transform FindModel(Transform root)
    {
        foreach(Transform child in root){if(child.name.StartsWith("FacilityVisit"))continue;if(child.GetComponentInChildren<Renderer>(true))return child;}
        return null;
    }
    static float Smooth(float s){s=Mathf.Clamp01(s);return s*s*(3-2*s);}
    void LateUpdate()
    {
        if(!model){Destroy(this);return;}
        bool active=timed?Time.time<until:Time.unscaledTime-lastHold<.6f;
        if(!active&&end<=0)end=Time.time;
        float weight=Smooth((Time.time-started)/.25f);if(!active)weight*=1-Smooth((Time.time-end)/.3f);
        if(!active&&Time.time-end>.3f){model.localRotation=baseRotation;Destroy(this);return;}
        float t=Time.time-started;float pitch=0,yaw=0;Vector3 squash=Vector3.one;
        switch(kind)
        {
            case Eat:pitch=18+9*Mathf.Sin(t*9);squash=new Vector3(1,1-.03f*Mathf.Abs(Mathf.Sin(t*4.5f)),1);break;
            case Treat:pitch=-3;yaw=2*Mathf.Sin(t*14);squash=new Vector3(1.07f,.84f+.02f*Mathf.Sin(t*2),1.07f);break;
            case Sit:pitch=-14;squash=new Vector3(1.04f,.87f,1.04f);break;
            case Groom:yaw=14*Mathf.Sin(t*8);squash=Vector3.one*(1+.04f*Mathf.Sin(t*8));break;
            case Happy:yaw=8*Mathf.Sin(t*7);{float s=Mathf.Sin(t*10);squash=new Vector3(1-.05f*s,1+.08f*s,1-.05f*s);}break;
        }
        model.localRotation=baseRotation*Quaternion.Euler(pitch*weight,yaw*weight,0);
        model.localScale=Vector3.Scale(model.localScale,Vector3.Lerp(Vector3.one,squash,weight));
    }
    void OnDestroy(){if(model)model.localRotation=baseRotation;}
}
