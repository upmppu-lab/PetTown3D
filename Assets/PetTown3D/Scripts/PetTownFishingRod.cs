using UnityEngine;

// Cosmetic only: while a resident is in the fishing stage of a lake visit (15 seconds), a rod is held in the right hand
// and the right arm plays a short cycle that follows the visit clock: wind up, cast, wait, nibbles, hook set, reel in.
// The arm is posed after the Animator runs, with a two-bone solve that works for any bone axes. Weight fades in and out,
// and everything removes itself when the visit stops refreshing it, so cancelled or finished visits never leave a rod behind.
public sealed class PetTownFishingRod : MonoBehaviour
{
    const float HoldSeconds=.6f,LineWidth=.008f,CycleSeconds=15f,FadeIn=.35f,FadeOut=.4f;
    const float CastFrom=1.1f,CastTo=1.8f,BiteFrom=10f,ReelFrom=11.3f,ReelTo=12.9f;
    // Hand target in the resident frame (x right, y up, z forward) in units of arm length; w is the rod pitch above horizontal.
    struct Key{public float t;public Vector4 pose;public Key(float time,float x,float y,float z,float pitch){t=time;pose=new Vector4(x,y,z,pitch);}}
    static readonly Key[] Keys=
    {
        new Key(0f,.15f,-.20f,.80f,35),new Key(.9f,.25f,.78f,-.22f,118),new Key(1.15f,.12f,.40f,.85f,60),new Key(1.55f,.15f,-.05f,.82f,30),
        new Key(2.3f,.15f,-.20f,.80f,35),new Key(9.8f,.15f,-.20f,.80f,35),
        new Key(10.1f,.15f,-.26f,.80f,27),new Key(10.35f,.15f,-.20f,.80f,36),new Key(10.6f,.15f,-.26f,.80f,26),new Key(10.85f,.15f,-.20f,.80f,35),
        new Key(11.05f,.15f,-.31f,.82f,19),new Key(11.35f,.20f,.55f,.45f,80),new Key(12.4f,.18f,.30f,.55f,62),new Key(13.3f,.20f,.40f,.50f,70),new Key(15f,.20f,.40f,.50f,70)
    };

    Transform hand,lower,upper,rod,bobber;LineRenderer line;Material lineMaterial,bobberMaterial;
    Vector3 water;float lastHold,seed,length=1.6f,time,startedAt,releaseAt;bool releasing;

    public static void Hold(GameObject actor,PetTownCommunityLibrary library,PetTownCommunityView view,int slot,float time)
    {
        if(!actor||!library||!library.fishingRod||!view)return;
        var local=view.VisitLocal(slot);var toLake=new Vector3(0,local.y,1)-local;toLake.y=0;if(toLake.sqrMagnitude<.01f)return;toLake.Normalize();
        var target=view.transform.TransformPoint(local+toLake*3.2f);target.y=view.transform.TransformPoint(new Vector3(0,.1f,0)).y;
        var prop=actor.GetComponent<PetTownFishingRod>();if(!prop)prop=actor.AddComponent<PetTownFishingRod>();
        prop.Refresh(library,target,time);
    }
    public static void Drop(GameObject actor){if(!actor)return;var prop=actor.GetComponent<PetTownFishingRod>();if(prop)prop.BeginRelease();}

    void Refresh(PetTownCommunityLibrary library,Vector3 target,float visitTime)
    {
        lastHold=Time.unscaledTime;water=target;time=Mathf.Clamp(visitTime,0,CycleSeconds);
        if(releasing&&rod){releasing=false;startedAt=Time.time;}
        if(rod)return;
        hand=FindHand(transform);if(!hand)return;seed=Random.value*10;startedAt=Time.time;releasing=false;
        lower=hand.parent;upper=lower?lower.parent:null;
        float height=0;foreach(var r in GetComponentsInChildren<Renderer>())if(r.enabled)height=Mathf.Max(height,r.bounds.size.y);
        length=Mathf.Clamp(height>.1f?height*.95f:1.6f,1.2f,2.2f);
        var instance=Instantiate(library.fishingRod,hand);rod=instance.transform;rod.localPosition=Vector3.zero;
        rod.localScale=Vector3.one*length/Mathf.Max(.0001f,hand.lossyScale.x);
        foreach(var c in instance.GetComponentsInChildren<Collider>(true))c.enabled=false;
        var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)shader=Shader.Find("Universal Render Pipeline/Lit");
        var lineNode=new GameObject("FishingLine");line=lineNode.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=10;line.startWidth=LineWidth;line.endWidth=LineWidth;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        lineMaterial=new Material(shader){name="FishingLine"};lineMaterial.SetColor("_BaseColor",new Color(.92f,.92f,.88f));line.sharedMaterial=lineMaterial;
        var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="FishingBobber";var col=ball.GetComponent<Collider>();if(col)Destroy(col);
        bobber=ball.transform;bobber.localScale=Vector3.one*.09f;bobberMaterial=new Material(shader){name="FishingBobber"};bobberMaterial.SetColor("_BaseColor",new Color(.9f,.18f,.14f));ball.GetComponent<Renderer>().sharedMaterial=bobberMaterial;
        ball.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    void BeginRelease(){if(releasing||!rod)return;releasing=true;releaseAt=Time.time;}

    static Vector4 Evaluate(float t)
    {
        t=Mathf.Clamp(t,0,CycleSeconds);int i=0;while(i<Keys.Length-2&&t>Keys[i+1].t)i++;
        var a=Keys[i];var b=Keys[i+1];float h=Mathf.Max(.0001f,b.t-a.t);float u=Mathf.Clamp01((t-a.t)/h);
        Vector4 ma=i>0?(b.pose-Keys[i-1].pose)/Mathf.Max(.0001f,b.t-Keys[i-1].t):Vector4.zero;
        Vector4 mb=i+2<Keys.Length?(Keys[i+2].pose-a.pose)/Mathf.Max(.0001f,Keys[i+2].t-a.t):Vector4.zero;
        float u2=u*u,u3=u2*u;
        return (2*u3-3*u2+1)*a.pose+(u3-2*u2+u)*h*ma+(-2*u3+3*u2)*b.pose+(u3-u2)*h*mb;
    }
    static float Pulse(float t,float start,float len){float u=(t-start)/len;return u<0||u>1?0:Mathf.Sin(u*Mathf.PI);}
    static float Dip(float t){if(t<BiteFrom||t>ReelFrom)return 0;return Pulse(t,10.05f,.25f)*.04f+Pulse(t,10.55f,.25f)*.04f+Pulse(t,11f,.35f)*.11f;}
    static float Smooth(float s){s=Mathf.Clamp01(s);return s*s*(3-2*s);}

    void LateUpdate()
    {
        if(!rod||!hand){Clear();return;}
        if(!releasing&&Time.unscaledTime-lastHold>HoldSeconds)BeginRelease();
        float weight=releasing?1-Mathf.Clamp01((Time.time-releaseAt)/FadeOut):Mathf.Clamp01((Time.time-startedAt)/FadeIn);weight=Smooth(weight);
        if(releasing&&weight<=0){Clear();return;}
        var toWater=water-transform.position;toWater.y=0;
        if(toWater.sqrMagnitude>.04f&&!releasing){var face=Quaternion.LookRotation(toWater.normalized);transform.rotation=Quaternion.RotateTowards(transform.rotation,face,360f*Time.deltaTime);}
        var frame=Quaternion.Euler(0,transform.eulerAngles.y,0);
        var rest=hand.rotation;var pose=Evaluate(time);
        SolveArm(frame,pose,weight);
        float pitch=pose.w*Mathf.Deg2Rad;var direction=frame*new Vector3(0,Mathf.Sin(pitch),Mathf.Cos(pitch));
        float sway=time>2.3f&&time<9.8f?Mathf.Sin(Time.time*1.3f+seed)*2.5f:0;
        // Roll 90 degrees so the rod's bent side (local -X) droops downward.
        rod.rotation=Quaternion.Slerp(rest,Quaternion.LookRotation(direction,Vector3.up)*Quaternion.Euler(sway,0,90),weight);
        var tip=rod.TransformPoint(new Vector3(-.1f,0,1));
        var surface=new Vector3(water.x,water.y+.02f,water.z);float bob=Mathf.Sin(Time.time*2.1f+seed)*.012f-Dip(time);
        Vector3 floatPosition;float sag=.22f;
        if(time<CastFrom){floatPosition=tip+Vector3.down*(.32f+Mathf.Sin(Time.time*5f)*.02f);sag=.03f;}
        else if(time<CastTo){float s=(time-CastFrom)/(CastTo-CastFrom);floatPosition=Vector3.Lerp(tip,surface,Smooth(s))+Vector3.up*Mathf.Sin(s*Mathf.PI)*1.1f;sag=.1f*(1-s)+.03f;}
        else if(time<ReelFrom){floatPosition=new Vector3(surface.x,surface.y+bob,surface.z);}
        else if(time<ReelTo){float s=Smooth((time-ReelFrom)/(ReelTo-ReelFrom));floatPosition=Vector3.Lerp(surface,tip+Vector3.down*.3f,s)+Vector3.up*Mathf.Sin(s*Mathf.PI)*.5f;sag=.14f*(1-s)+.03f;}
        else{floatPosition=tip+Vector3.down*(.3f+Mathf.Sin(Time.time*4f)*.015f);sag=.03f;}
        bobber.position=floatPosition;
        for(int i=0;i<line.positionCount;i++)
        {
            float t=i/(float)(line.positionCount-1);var p=Vector3.Lerp(tip,floatPosition,t);p.y-=Mathf.Sin(t*Mathf.PI)*sag;line.SetPosition(i,p);
        }
    }

    // Two-bone solve: upper arm and forearm are rotated in world space toward the shoulder-elbow-hand triangle, so bone axes do not matter.
    void SolveArm(Quaternion frame,Vector4 pose,float weight)
    {
        if(!upper||!lower)return;
        var originalUpper=upper.localRotation;var originalLower=lower.localRotation;
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position),reach=a+b;if(reach<.05f||a<.01f)return;
        var shoulder=upper.position;var target=shoulder+frame*new Vector3(pose.x,pose.y,pose.z)*reach;var toTarget=target-shoulder;
        float d=Mathf.Clamp(toTarget.magnitude,Mathf.Abs(a-b)+.02f,reach-.005f);var direction=toTarget.normalized;
        var side=Vector3.ProjectOnPlane(frame*new Vector3(.55f,-.75f,-.35f),direction);if(side.sqrMagnitude<.0001f)side=frame*Vector3.right;side.Normalize();
        float cosA=Mathf.Clamp((a*a+d*d-b*b)/(2*a*d),-1f,1f);float sinA=Mathf.Sqrt(1-cosA*cosA);
        var elbow=shoulder+(direction*cosA+side*sinA)*a;var wrist=shoulder+direction*d;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-shoulder)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,wrist-lower.position)*lower.rotation;
        var solvedUpper=upper.localRotation;var solvedLower=lower.localRotation;
        upper.localRotation=Quaternion.Slerp(originalUpper,solvedUpper,weight);lower.localRotation=Quaternion.Slerp(originalLower,solvedLower,weight);
    }

    void Clear()
    {
        if(rod)Destroy(rod.gameObject);if(line)Destroy(line.gameObject);if(bobber)Destroy(bobber.gameObject);
        if(lineMaterial)Destroy(lineMaterial);if(bobberMaterial)Destroy(bobberMaterial);
        rod=null;line=null;bobber=null;lineMaterial=null;bobberMaterial=null;Destroy(this);
    }
    void OnDestroy()
    {
        if(rod)Destroy(rod.gameObject);if(line)Destroy(line.gameObject);if(bobber)Destroy(bobber.gameObject);
        if(lineMaterial)Destroy(lineMaterial);if(bobberMaterial)Destroy(bobberMaterial);
    }

    static Transform FindHand(Transform root)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            var n=t.name.ToLowerInvariant();
            if(n.Contains("thumb")||n.Contains("index")||n.Contains("middle")||n.Contains("ring")||n.Contains("pinky")||n.Contains("finger"))continue;
            if(n.Contains("righthand")||n.EndsWith("hand_r")||n.EndsWith("hand.r")||n.EndsWith("r_hand"))return t;
        }
        return null;
    }
}
