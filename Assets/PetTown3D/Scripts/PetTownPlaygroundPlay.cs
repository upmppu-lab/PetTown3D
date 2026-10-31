using UnityEngine;

// Cosmetic only: while a resident is in the "use" stage of a park visit (15 seconds) the body is posed on one of the playground
// pieces: swing, seesaw, slide or sandbox. The pose is applied after the Animator (LateUpdate) with a body offset on the model root,
// a spine lean and two-bone leg/arm solves that work for any bone axes. Weight fades in and out, and the component removes itself as soon
// as the visit stops refreshing it, so cancelled or finished visits never leave a resident floating on a swing.
public sealed class PetTownPlaygroundPlay : MonoBehaviour
{
    const float HoldSeconds=.6f,VisitSeconds=15f,FadeIn=.8f,FadeOut=.8f,ReleaseFade=.4f,Ankle=.08f;
    // Slide cycle (seconds): walk round to the ladder, climb, sit, slide, stand up. The first cycle starts mid-walk where the resident stands.
    const float CycleSeconds=7.5f,WalkSeconds=3.4f,ClimbSeconds=1.8f,SitSeconds=.4f,SlideSeconds=1.2f;

    struct Pose
    {
        public Vector3 hips;public Quaternion facing;public float lean;
        public bool legs,arms;public Vector3 footL,footR,handL,handR,kneePole;
    }

    PetTownPlaygroundRig rig;int slot=-1;float time,lastHold,releaseAt,startedAt;bool releasing,ready;
    Transform model,hips,spine,lu,ll,lf,ru,rl,rf,aul,all_,ahl,aur,alr,ahr;
    Vector3 baseLocalPosition;Quaternion baseLocalRotation;float standHips=.85f;
    float skip;Quaternion facing;bool facingSet;Vector3 entry;float walked;Vector3 lastGroundPoint;bool lastPointSet;

    public static void Play(GameObject actor,PetTownCommunityView view,int slot,float time)
    {
        if(!actor||!view)return;var rig=view.Playground;if(!rig)return;
        var prop=actor.GetComponent<PetTownPlaygroundPlay>();if(!prop)prop=actor.AddComponent<PetTownPlaygroundPlay>();
        prop.Refresh(rig,slot,time);
    }
    public static void Drop(GameObject actor){if(!actor)return;var prop=actor.GetComponent<PetTownPlaygroundPlay>();if(prop)prop.BeginRelease();}

    void Refresh(PetTownPlaygroundRig source,int visitSlot,float visitTime)
    {
        lastHold=Time.unscaledTime;time=Mathf.Clamp(visitTime,0,VisitSeconds);
        if(source!=rig||visitSlot!=slot){rig=source;slot=visitSlot;ready=false;facingSet=false;lastPointSet=false;walked=0;}
        if(releasing){releasing=false;startedAt=Time.time;}
        if(!ready){Setup();startedAt=Time.time;if(ready&&slot==4)skip=SlideSkip();}
        if(ready)rig.Touch(slot);
    }
    void BeginRelease(){if(releasing||!ready)return;releasing=true;releaseAt=Time.time;}

    void Setup()
    {
        ready=false;hips=Find("Hips");if(!hips||!hips.parent)return;model=hips.parent;
        spine=Find("Spine");lu=Find("LeftUpperLeg");ll=Find("LeftLowerLeg");lf=Find("LeftFoot");ru=Find("RightUpperLeg");rl=Find("RightLowerLeg");rf=Find("RightFoot");
        aul=Find("LeftUpperArm");all_=Find("LeftLowerArm");ahl=Find("LeftHand");aur=Find("RightUpperArm");alr=Find("RightLowerArm");ahr=Find("RightHand");
        if(!lu||!ll||!lf||!ru||!rl||!rf)return;
        baseLocalPosition=model.localPosition;baseLocalRotation=model.localRotation;
        standHips=Mathf.Clamp(hips.position.y-transform.position.y,.55f,1.3f);
        entry=transform.position;ready=true;
    }
    Transform Find(string bone)
    {
        foreach(var t in GetComponentsInChildren<Transform>(true))if(string.Equals(t.name,bone,System.StringComparison.OrdinalIgnoreCase))return t;
        return null;
    }

    static float Smooth(float s){s=Mathf.Clamp01(s);return s*s*(3-2*s);}

    void LateUpdate()
    {
        if(!ready||!rig||!model){Finish();return;}
        if(!releasing&&Time.unscaledTime-lastHold>HoldSeconds)BeginRelease();
        float weight=Smooth(Mathf.Min(time/FadeIn,(VisitSeconds-time)/FadeOut));
        weight*=releasing?Smooth(1-Mathf.Clamp01((Time.time-releaseAt)/ReleaseFade)):Smooth((Time.time-startedAt)/.3f);
        model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;
        if(releasing&&weight<=0.001f){Finish();return;}
        if(weight<=0.001f)return;
        Pose pose;if(!Build(out pose))return;
        Apply(pose,weight);
    }

    void Finish()
    {
        if(model){model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;}
        Destroy(this);
    }
    void OnDestroy(){if(model&&ready){model.localPosition=baseLocalPosition;model.localRotation=baseLocalRotation;}}

    // ---------------------------------------------------------------- poses

    bool Build(out Pose pose)
    {
        pose=new Pose();
        switch(slot)
        {
            case 0:case 1:return BuildSwing(slot,ref pose);
            case 2:case 3:return BuildSeesaw(slot-2,ref pose);
            case 4:return BuildSlide(ref pose);
            case 5:return BuildSand(ref pose);
        }
        return false;
    }

    bool BuildSwing(int i,ref Pose pose)
    {
        var seat=rig.SwingSeat(i);var pivot=rig.SwingPivot(i);var root=rig.SwingRoot;if(!seat||!pivot||!root)return false;
        float s=root.lossyScale.x;
        var q=Quaternion.Slerp(root.rotation,pivot.rotation,.8f)*Quaternion.Euler(0,180,0);   // rider looks out of the frame, toward the park
        Vector3 f=q*Vector3.forward,u=q*Vector3.up;
        float velocity=-Mathf.Cos(rig.SwingPhase(i))*rig.SwingAmp(i);      // +1 moving the way the rider faces, -1 backward
        float k=Smooth(.5f+.5f*velocity);                                  // legs reach out while going forward, tuck when going back
        pose.facing=q;pose.hips=seat.position+u*.12f-f*.03f;
        pose.lean=-3+7*velocity;
        pose.legs=true;pose.footL=pose.hips+q*new Vector3(-.11f,-.50f+.20f*k,.20f+.32f*k);pose.footR=pose.hips+q*new Vector3(.11f,-.50f+.20f*k,.20f+.32f*k);
        pose.arms=true;pose.handL=seat.position+q*new Vector3(-.295f*s,.60f,.03f);pose.handR=seat.position+q*new Vector3(.295f*s,.60f,.03f);
        pose.kneePole=f+u*.2f;
        return true;
    }

    bool BuildSeesaw(int end,ref Pose pose)
    {
        var seat=rig.SeesawSeat(end);var root=rig.SeesawRoot;if(!seat||!root)return false;
        var yaw=Quaternion.Euler(0,root.eulerAngles.y,0);Vector3 f=yaw*(end==0?Vector3.back:Vector3.forward);
        var q=Quaternion.LookRotation(f,Vector3.up);Vector3 r=q*Vector3.right;float ground=transform.position.y;
        pose.facing=q;pose.hips=seat.position+Vector3.up*.15f;pose.lean=6;
        pose.legs=true;
        var foot=pose.hips+f*.36f;foot.y=ground+Ankle;
        pose.footL=foot-r*.12f;pose.footR=foot+r*.12f;
        pose.arms=true;var grip=seat.position+f*.32f+Vector3.up*.34f;pose.handL=grip-r*.15f;pose.handR=grip+r*.15f;
        pose.kneePole=f+Vector3.up*.15f;
        return true;
    }

    // Slide path in mesh space (x runs from the ladder to the exit, z = 0 is the lane, z > 0 is the walkway beside it).
    static readonly Vector3[] Walkway={new Vector3(-1.45f,0,0),new Vector3(-1.45f,0,.95f),new Vector3(1.85f,0,.95f),new Vector3(1.92f,0,0),new Vector3(1.55f,.03f,0)};
    static readonly Vector3[] Ladder={new Vector3(1.55f,.03f,0),new Vector3(1.37f,.20f,0),new Vector3(.57f,.95f,0),new Vector3(.33f,1.12f,0)};
    static readonly Vector3[] Chute={new Vector3(.12f,1.15f,0),new Vector3(-.30f,.97f,0),new Vector3(-.70f,.66f,0),new Vector3(-1.05f,.30f,0),new Vector3(-1.40f,.06f,0)};

    Vector3 FromMesh(Vector3 p)=>rig.Slide.TransformPoint(p);
    static Vector3 Along(Vector3[] pts,float t,out Vector3 tangent)
    {
        float total=Length(pts);
        float s=Mathf.Clamp01(t)*total;
        for(int i=1;i<pts.Length;i++)
        {
            float len=Vector3.Distance(pts[i-1],pts[i]);
            if(s<=len||i==pts.Length-1){float u=len>0?Mathf.Clamp01(s/len):0;tangent=(pts[i]-pts[i-1]).normalized;return Vector3.Lerp(pts[i-1],pts[i],u);}
            s-=len;
        }
        tangent=Vector3.left;return pts[pts.Length-1];
    }
    // Where on the walkway the resident already stands, so the first walk starts from the visit point instead of the exit.
    float SlideSkip()
    {
        if(!rig||!rig.Slide)return 0;var local=rig.Slide.InverseTransformPoint(transform.position);
        float total=Length(Walkway),best=float.MaxValue,bestS=0,run=0;
        for(int i=1;i<Walkway.Length;i++)
        {
            var a=Walkway[i-1];var b=Walkway[i];var ab=b-a;float len=ab.magnitude;float t=len>0?Mathf.Clamp01(Vector3.Dot(new Vector3(local.x,0,local.z)-new Vector3(a.x,0,a.z),new Vector3(ab.x,0,ab.z))/(len*len)):0;
            var p=a+ab*t;float d=(new Vector3(local.x,0,local.z)-new Vector3(p.x,0,p.z)).sqrMagnitude;if(d<best){best=d;bestS=run+t*len;}run+=len;
        }
        return total>0?bestS/total*WalkSeconds:0;
    }
    static float Length(Vector3[] pts){float total=0;for(int i=1;i<pts.Length;i++)total+=Vector3.Distance(pts[i-1],pts[i]);return total;}

    bool BuildSlide(ref Pose pose)
    {
        var slide=rig.Slide;if(!slide)return false;
        float u=(time+skip)%CycleSeconds;
        Vector3 groundPoint,dir,tangent;bool walking=false,climbing=false,sliding=false,sitting=false;
        float lean=4;float seatBlend=0;
        float standStart=WalkSeconds+ClimbSeconds+SitSeconds+SlideSeconds;
        if(u<WalkSeconds)
        {
            float t=u/WalkSeconds;groundPoint=FromMesh(Along(Walkway,t,out tangent));dir=slide.TransformDirection(tangent);walking=true;
        }
        else if(u<WalkSeconds+ClimbSeconds)
        {
            float t=(u-WalkSeconds)/ClimbSeconds;groundPoint=FromMesh(Along(Ladder,t,out tangent));dir=slide.TransformDirection(tangent);climbing=true;lean=10;
        }
        else if(u<WalkSeconds+ClimbSeconds+SitSeconds)
        {
            float t=Smooth((u-WalkSeconds-ClimbSeconds)/SitSeconds);var top=FromMesh(Ladder[Ladder.Length-1]);var seat=FromMesh(Chute[0]);
            groundPoint=Vector3.Lerp(top,seat,t);dir=slide.TransformDirection(Vector3.left);sitting=true;seatBlend=t;
        }
        else if(u<standStart)
        {
            float t=(u-WalkSeconds-ClimbSeconds-SitSeconds)/SlideSeconds;t=(t*t*(3-2*t))*.6f+t*.4f;
            groundPoint=FromMesh(Along(Chute,t,out tangent));dir=slide.TransformDirection(tangent);sliding=true;seatBlend=1;
        }
        else
        {
            float t=Smooth((u-standStart)/.5f);
            groundPoint=FromMesh(Chute[Chute.Length-1]);groundPoint.y=transform.position.y;dir=slide.TransformDirection(Vector3.left);seatBlend=1-t;sitting=t<1;
        }
        var flat=dir;flat.y=0;if(flat.sqrMagnitude<.0001f)flat=slide.TransformDirection(Vector3.left);flat.Normalize();
        var wanted=Quaternion.LookRotation(flat,Vector3.up);if(!facingSet){facing=wanted;facingSet=true;}
        facing=Quaternion.RotateTowards(facing,wanted,480f*Time.deltaTime);
        Vector3 f=facing*Vector3.forward,r=facing*Vector3.right;
        pose.facing=facing;pose.lean=lean;

        if(sliding||(sitting&&seatBlend>0))
        {
            // Seated on the chute, legs stretched along the slope, hands up.
            var along=sliding?dir:slide.TransformDirection((Chute[1]-Chute[0]).normalized);along.Normalize();
            pose.facing=Quaternion.LookRotation(along,Vector3.up);
            var seatPoint=groundPoint+Vector3.up*.10f;var standing=groundPoint+Vector3.up*standHips;
            pose.hips=sliding?seatPoint:Vector3.Lerp(standing,seatPoint,seatBlend);
            pose.lean=-14;pose.legs=true;
            var knee=pose.hips+along*.62f;
            pose.footL=knee-r*.11f;pose.footR=knee+r*.11f;
            pose.arms=true;var arm=pose.hips+Vector3.up*.78f+along*.10f;pose.handL=arm-r*.30f;pose.handR=arm+r*.30f;
            pose.kneePole=Vector3.up;
            return true;
        }
        float groundY=groundPoint.y;if(!lastPointSet){lastGroundPoint=groundPoint;lastPointSet=true;}
        walked+=Vector3.Distance(lastGroundPoint,groundPoint);lastGroundPoint=groundPoint;
        bool moving=walking||climbing;float phase=walked/(climbing?.30f:.42f)*Mathf.PI;
        float bob=moving?Mathf.Abs(Mathf.Sin(phase))*.025f:0;
        pose.hips=groundPoint+Vector3.up*(standHips-bob);
        pose.legs=true;
        float rise=climbing?dir.y/Mathf.Max(.2f,new Vector3(dir.x,0,dir.z).magnitude):0;
        float stride=moving?(climbing?.18f:.22f):0;
        for(int side=0;side<2;side++)
        {
            float p=phase+side*Mathf.PI;float off=Mathf.Sin(p)*stride;float raise=moving?Mathf.Max(0,Mathf.Cos(p))*(climbing?.20f:.10f):0;
            var foot=groundPoint+f*off+(side==0?-r:r)*.10f;foot.y=groundY+Ankle+raise+off*rise;
            if(side==0)pose.footL=foot;else pose.footR=foot;
        }
        pose.kneePole=f;
        if(climbing)
        {
            pose.arms=true;var grip=pose.hips+f*.30f+Vector3.up*.55f;pose.handL=grip-r*.22f;pose.handR=grip+r*.22f;
        }
        return true;
    }

    bool BuildSand(ref Pose pose)
    {
        var sand=rig.SandBox;if(!sand)return false;
        var center=sand.position;center.y=transform.position.y;
        var toward=entry-center;toward.y=0;if(toward.sqrMagnitude<.01f)toward=Vector3.back;toward.Normalize();
        Vector3 f=-toward;var q=Quaternion.LookRotation(f,Vector3.up);Vector3 r=q*Vector3.right;
        float ground=transform.position.y;
        pose.facing=q;pose.hips=center+toward*.48f;pose.hips.y=ground+.40f;pose.lean=34;
        pose.legs=true;var foot=pose.hips+f*.12f;foot.y=ground+.11f+Ankle;pose.footL=foot-r*.16f;pose.footR=foot+r*.16f;pose.kneePole=f+Vector3.up*.6f;
        pose.arms=true;
        float t=Time.time*3.4f;Vector3 hp=pose.hips;
        pose.handL=Reach(hp,f,r,ground,t,-1);pose.handR=Reach(hp,f,r,ground,t+Mathf.PI,1);
        return true;
    }
    static Vector3 Reach(Vector3 hipsPosition,Vector3 f,Vector3 r,float ground,float phase,float side)
    {
        float scoop=Mathf.Sin(phase);var hand=hipsPosition+f*(.46f+.12f*Mathf.Max(0,scoop))+r*side*.13f;
        hand.y=ground+.19f+Mathf.Max(0,-scoop)*.05f+Mathf.Max(0,scoop)*.14f;return hand;
    }

    // ---------------------------------------------------------------- application

    void Apply(Pose pose,float weight)
    {
        // Body: rotate the model about the hips, then slide it so the hips land on the target.
        model.rotation=Quaternion.Slerp(model.rotation,pose.facing*baseLocalRotation,weight);
        var current=hips.position;model.position+=Vector3.Lerp(current,pose.hips,weight)-current;
        if(spine&&Mathf.Abs(pose.lean)>.01f)spine.rotation=Quaternion.AngleAxis(pose.lean*weight,model.rotation*Vector3.right)*spine.rotation;
        Vector3 f=model.rotation*Vector3.forward,r=model.rotation*Vector3.right;
        if(pose.legs)
        {
            Solve(lu,ll,lf,pose.footL,pose.kneePole+(-r)*.15f,weight);
            Solve(ru,rl,rf,pose.footR,pose.kneePole+r*.15f,weight);
        }
        if(pose.arms)
        {
            Solve(aul,all_,ahl,pose.handL,-f*.35f-r*.7f-Vector3.up*.45f,weight);
            Solve(aur,alr,ahr,pose.handR,-f*.35f+r*.7f-Vector3.up*.45f,weight);
        }
    }

    // Two-bone solve (shoulder/hip, elbow/knee, hand/foot), independent of the bone axes. The middle joint bulges toward 'pole'.
    static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole,float weight)
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
