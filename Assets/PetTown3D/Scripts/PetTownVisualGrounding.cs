using System.Collections.Generic;
using UnityEngine;

// Only the visual wrapper moves. Navigation, colliders and animation clips retain their existing coordinates.
[DefaultExecutionOrder(200)]
public sealed class PetTownVisualGrounding : MonoBehaviour
{
    struct Influence
    {
        public Transform bone; public Vector3 point; public float weight;
        public Vector3 World => bone ? bone.TransformPoint(point)*weight : Vector3.zero;
    }
    struct Anchor
    {
        public Influence a,b,c,d;
        public Vector3 World => a.World+b.World+c.World+d.World;
    }
    sealed class Skin
    {
        public SkinnedMeshRenderer renderer; public Vector3[] world;
        public Vector3[] source; public BoneWeight[] weights; public Matrix4x4[] bind; public Transform[] bones;
    }
    static int nextCalibrationSlot;
    readonly List<Anchor> anchors=new List<Anchor>();
    PetTownGame game;Transform model,actor;int readyFrame;bool calibrated;float velocity;
    public float LastGap {get;private set;}
    public int AnchorCount=>anchors.Count;
    public float VisualOffset=>transform.localPosition.y;
    public static void Attach(PetTownGame source,Transform root,Transform visual)
    {
        if(!source||!root||!visual||visual.GetComponentInParent<PetTownVisualGrounding>())return;
        var wrapper=new GameObject("GroundedVisual").transform;wrapper.SetParent(root,false);visual.SetParent(wrapper,true);
        var component=wrapper.gameObject.AddComponent<PetTownVisualGrounding>();component.game=source;component.actor=root;component.model=visual;
        component.readyFrame=Time.frameCount+3+(nextCalibrationSlot++&7);
    }
    static Influence Weighted(Skin skin,int index,float weight,Vector3 vertex)
    {
        if(weight<=0||index<0||index>=skin.bones.Length||index>=skin.bind.Length)return default;
        return new Influence{bone=skin.bones[index],point=skin.bind[index].MultiplyPoint3x4(vertex),weight=weight};
    }
    void Calibrate()
    {
        var feet=new List<Transform>();
        foreach(var bone in model.GetComponentsInChildren<Transform>(true))
        {
            string name=bone.name.ToLowerInvariant();if(name.Contains("foot")||name.Contains("toe")||name.Contains("ankle")||name.EndsWith("leg2")||name.Contains("paw"))feet.Add(bone);
        }
        // Prefer the actual foot over its toe/end descendants, retaining four feet on quadrupeds.
        bool hasFoot=feet.Exists(t=>t.name.ToLowerInvariant().Contains("foot"));
        if(hasFoot)feet.RemoveAll(t=>!t.name.ToLowerInvariant().Contains("foot"));
        if(feet.Count==0)feet.Add(model);
        var positions=new Vector3[feet.Count];
        for(int i=0;i<feet.Count;i++)positions[i]=feet[i].position;
        var skins=new List<Skin>();var scratch=new Mesh();float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(!renderer.sharedMesh)continue;renderer.BakeMesh(scratch,false);var world=scratch.vertices;
            // BakeMesh already includes the renderer scale; applying localToWorldMatrix would scale the feet twice.
            for(int i=0;i<world.Length;i++){world[i]=renderer.transform.position+renderer.transform.rotation*world[i];bottom=Mathf.Min(bottom,world[i].y);top=Mathf.Max(top,world[i].y);}
            var skin=new Skin{renderer=renderer,world=world};var shared=renderer.sharedMesh;
            if(shared.isReadable){skin.source=shared.vertices;skin.weights=shared.boneWeights;skin.bind=shared.bindposes;skin.bones=renderer.bones;}
            skins.Add(skin);
        }
        Destroy(scratch);if(skins.Count==0)return;
        float band=Mathf.Clamp((top-bottom)*.22f,.06f,.45f);
        float cutoff=bottom+band;
        // An initially raised foot must also be represented, otherwise it can become the untracked support on the next step.
        foreach(var position in positions)cutoff=Mathf.Max(cutoff,position.y+(top-bottom)*.1f);
        // Cache a small support hull for each foot. The underside changes as the ankle rolls,
        // so sample all 26 cube directions rather than just one flat-pose sole point.
        var directions=new List<Vector3>();
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(x!=0||y!=0||z!=0)directions.Add(new Vector3(x,y,z).normalized);
        int perFoot=directions.Count,samples=feet.Count*perFoot;var selectedSkin=new Skin[samples];var selectedIndex=new int[samples];var scores=new float[samples];
        for(int i=0;i<samples;i++)scores[i]=float.PositiveInfinity;
        foreach(var skin in skins)for(int v=0;v<skin.world.Length;v++)
        {
            var point=skin.world[v];if(point.y>cutoff)continue;int nearest=0;float best=float.PositiveInfinity;
            for(int i=0;i<feet.Count;i++){float distance=(point-positions[i]).sqrMagnitude;if(distance<best){nearest=i;best=distance;}}
            var local=feet[nearest].InverseTransformPoint(point);
            for(int edge=0;edge<perFoot;edge++)
            {
                float score=Vector3.Dot(local,directions[edge]);
                int slot=nearest*perFoot+edge;if(score<scores[slot]){scores[slot]=score;selectedSkin[slot]=skin;selectedIndex[slot]=v;}
            }
        }
        for(int i=0;i<samples;i++)
        {
            var skin=selectedSkin[i];if(skin==null)continue;int v=selectedIndex[i];bool duplicate=false;
            for(int j=0;j<i;j++)if(selectedSkin[j]==skin&&selectedIndex[j]==v){duplicate=true;break;}
            if(duplicate)continue;
            if(skin.source!=null&&v<skin.weights.Length&&v<skin.source.Length)
            {
                var w=skin.weights[v];var vertex=skin.source[v];
                anchors.Add(new Anchor{a=Weighted(skin,w.boneIndex0,w.weight0,vertex),b=Weighted(skin,w.boneIndex1,w.weight1,vertex),c=Weighted(skin,w.boneIndex2,w.weight2,vertex),d=Weighted(skin,w.boneIndex3,w.weight3,vertex)});
            }
            else {var foot=feet[i/perFoot];anchors.Add(new Anchor{a=new Influence{bone=foot,point=foot.InverseTransformPoint(skin.world[v]),weight=1}});}
        }
        // Large source vertex/weight arrays are released after calibration; only a few weighted sole points remain.
        calibrated=anchors.Count>0;
    }
    void LateUpdate()
    {
        if(!game||!actor||!model)return;
        if(!calibrated){if(Time.frameCount<readyFrame)return;Calibrate();if(!calibrated){readyFrame=Time.frameCount+30;return;}}
        float centerGround=game.GetGroundHeight(actor.position.x,actor.position.z),gap=float.PositiveInfinity;
        foreach(var anchor in anchors)
        {
            var point=anchor.World;float ground=game.GetGroundHeight(point.x,point.z);
            if(Mathf.Abs(ground-centerGround)>.3f)ground=centerGround;
            gap=Mathf.Min(gap,point.y-ground-.005f);
        }
        if(float.IsInfinity(gap))return;LastGap=gap;
        var local=transform.localPosition;float target=Mathf.Clamp(local.y-gap,-.8f,.8f);
        local.y=Time.deltaTime<=0?target:Mathf.SmoothDamp(local.y,target,ref velocity,.025f,16,Time.deltaTime);
        // Do not let smoothing leave the feet visibly suspended during a fast step.
        local.y=Mathf.Clamp(local.y,target-.008f,target+.008f);
        transform.localPosition=local;
    }
}
