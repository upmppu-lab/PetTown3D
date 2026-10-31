using System.Collections.Generic;
using UnityEngine;

// Cosmetic sitting pose for residents: when a visit leaves a resident waiting, eating or resting next to a chair or bench,
// the body is lowered onto the seat with bent knees and hands in the lap (applied after the Animator, like the playground poses).
// It removes itself as soon as the visit stops refreshing it, so nobody stays seated after leaving.
public sealed class PetTownSeatPose : MonoBehaviour
{
    const float Radius=1.9f,HoldSeconds=.6f,FadeSeconds=.6f,Ankle=.08f;
    static readonly Dictionary<Transform,PetTownSeatPose> taken=new Dictionary<Transform,PetTownSeatPose>();
    static readonly List<Transform> seats=new List<Transform>();static float scanned=-99;

    Transform seat;Vector3 seatPoint;Quaternion facing;bool releasing;float lastHold,startedAt,releaseAt,ground;
    Transform model,hips,spine,lu,ll,lf,ru,rl,rf,aul,all_,ahl,aur,alr,ahr;Vector3 baseLocalPosition;Quaternion baseLocalRotation;

    // Call every frame while the resident should be sitting. Does nothing when no free seat is within reach.
    public static void Seat(GameObject actor)
    {
        if(!actor)return;var pose=actor.GetComponent<PetTownSeatPose>();
        if(!pose)
        {
            Transform found;Vector3 point;if(!FindSeat(actor.transform.position,out found,out point))return;
            pose=actor.AddComponent<PetTownSeatPose>();if(!pose.Begin(found,point,actor.transform.rotation)){Destroy(pose);return;}
        }
        pose.lastHold=Time.unscaledTime;if(pose.releasing){pose.releasing=false;pose.startedAt=Time.time;}
    }
    public static void Stand(GameObject actor){if(!actor)return;var pose=actor.GetComponent<PetTownSeatPose>();if(pose&&!pose.releasing){pose.releasing=true;pose.releaseAt=Time.time;}}

    static void Rescan()
    {
        scanned=Time.unscaledTime;seats.Clear();
        foreach(var f in Object.FindObjectsByType<PetTownFurniturePlacement>(FindObjectsSortMode.None))if(f&&f.gameObject.activeInHierarchy&&!f.Detail&&f.Kind==3)seats.Add(f.transform);
        foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if(!t||!t.gameObject.activeInHierarchy||t.name.IndexOf("bench",System.StringComparison.OrdinalIgnoreCase)<0)continue;
            if(t.parent&&t.parent.name.IndexOf("bench",System.StringComparison.OrdinalIgnoreCase)>=0)continue;
            if(t.GetComponentInChildren<MeshRenderer>()&&!seats.Contains(t))seats.Add(t);
        }
    }
    static bool SeatBounds(Transform t,out Bounds bounds)
    {
        bounds=new Bounds();bool any=false;
        foreach(var r in t.GetComponentsInChildren<MeshRenderer>()){if(!r.enabled)continue;if(!any){bounds=r.bounds;any=true;}else bounds.Encapsulate(r.bounds);}
        return any;
    }
    static bool FindSeat(Vector3 from,out Transform seat,out Vector3 point)
    {
        seat=null;point=Vector3.zero;if(Time.unscaledTime-scanned>10)Rescan();
        float best=Radius*Radius;
        foreach(var t in seats)
        {
            if(!t)continue;PetTownSeatPose owner;if(taken.TryGetValue(t,out owner)&&owner)continue;
            Bounds b;if(!SeatBounds(t,out b)||b.size.y<.25f||Mathf.Abs(b.min.y-from.y)>.7f)continue;
            float d=new Vector2(b.center.x-from.x,b.center.z-from.z).sqrMagnitude;
            if(d<best){best=d;seat=t;point=new Vector3(b.center.x,b.min.y+Mathf.Min(.46f,b.size.y*.5f),b.center.z);}
        }
        return seat;
    }

    bool Begin(Transform chosen,Vector3 point,Quaternion actorRotation)
    {
        hips=Find("Hips");if(!hips||!hips.parent)return false;model=hips.parent;
        spine=Find("Spine");lu=Find("LeftUpperLeg");ll=Find("LeftLowerLeg");lf=Find("LeftFoot");ru=Find("RightUpperLeg");rl=Find("RightLowerLeg");rf=Find("RightFoot");
        aul=Find("LeftUpperArm");all_=Find("LeftLowerArm");ahl=Find("LeftHand");aur=Find("RightUpperArm");alr=Find("RightLowerArm");ahr=Find("RightHand");
        if(!lu||!ll||!lf||!ru||!rl||!rf)return false;
        baseLocalPosition=model.localPosition;baseLocalRotation=model.localRotation;ground=transform.position.y;
        seat=chosen;seatPoint=point;facing=Quaternion.Euler(0,actorRotation.eulerAngles.y,0);startedAt=Time.time;lastHold=Time.unscaledTime;taken[seat]=this;return true;
    }
    Transform Find(string bone)
    {
        foreach(var t in GetComponentsInChildren<Transform>(true))if(string.Equals(t.name,bone,System.StringComparison.OrdinalIgnoreCase))return t;
        return null;
    }
    static float Smooth(float s){s=Mathf.Clamp01(s);return s*s*(3-2*s);}

    void LateUpdate()
    {
        if(!model||!seat){Finish();return;}
        if(!releasing&&Time.unscaledTime-lastHold>HoldSeconds){releasing=true;releaseAt=Time.time;}
        float weight=releasing?Smooth(1-(Time.time-releaseAt)/FadeSeconds):Smooth((Time.time-startedAt)/FadeSeconds);
        model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;
        if(releasing&&weight<=.001f){Finish();return;}
        if(weight<=.001f)return;
        Vector3 f=facing*Vector3.forward,r=facing*Vector3.right;
        var seated=seatPoint+Vector3.up*.10f-f*.02f;
        model.rotation=Quaternion.Slerp(model.rotation,facing*baseLocalRotation,weight);
        var current=hips.position;model.position+=Vector3.Lerp(current,seated,weight)-current;
        if(spine)spine.rotation=Quaternion.AngleAxis(-2*weight,model.rotation*Vector3.right)*spine.rotation;
        var foot=seated+f*.42f;foot.y=ground+Ankle;var pole=f+Vector3.up*.3f;
        Solve(lu,ll,lf,foot-r*.12f,pole-r*.15f,weight);Solve(ru,rl,rf,foot+r*.12f,pole+r*.15f,weight);
        var lap=seated+f*.26f+Vector3.up*.16f;
        Solve(aul,all_,ahl,lap-r*.14f,-f*.35f-r*.7f-Vector3.up*.45f,weight);Solve(aur,alr,ahr,lap+r*.14f,-f*.35f+r*.7f-Vector3.up*.45f,weight);
    }
    void Finish(){if(model){model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;}Destroy(this);}
    void OnDestroy()
    {
        if(seat){PetTownSeatPose owner;if(taken.TryGetValue(seat,out owner)&&owner==this)taken.Remove(seat);}
        if(model){model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;}
    }

    internal static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole,float weight)
    {
        if(!upper||!lower||!end||weight<=.001f)return;
        var originalUpper=upper.localRotation;var originalLower=lower.localRotation;
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,end.position),reach=a+b;if(reach<.05f||a<.01f||b<.01f)return;
        var root=upper.position;var toTarget=target-root;if(toTarget.sqrMagnitude<.0001f)return;
        float d=Mathf.Clamp(toTarget.magnitude,Mathf.Abs(a-b)+.02f,reach-.005f);var direction=toTarget.normalized;
        var side=Vector3.ProjectOnPlane(pole,direction);if(side.sqrMagnitude<.0001f)side=Vector3.ProjectOnPlane(Vector3.up,direction);side.Normalize();
        float cosA=Mathf.Clamp((a*a+d*d-b*b)/(2*a*d),-1f,1f);float sinA=Mathf.Sqrt(1-cosA*cosA);
        var middle=root+(direction*cosA+side*sinA)*a;var tip=root+direction*d;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,middle-root)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(end.position-lower.position,tip-lower.position)*lower.rotation;
        var solvedUpper=upper.localRotation;var solvedLower=lower.localRotation;
        upper.localRotation=Quaternion.Slerp(originalUpper,solvedUpper,weight);lower.localRotation=Quaternion.Slerp(originalLower,solvedLower,weight);
    }
}
