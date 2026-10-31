using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
public sealed class PetTownRanchAnimalVisual : MonoBehaviour
{
    PetTownRanchAnimal record;Transform visual;Animation animation;System.Random random;Vector3 destination;float nextPick,current=1;bool moving,canWalk;
    Transform marker;Material markerMaterial;bool wasReady;float cheerStart=-10,phase,modelHeight=1;
    readonly List<AnimationClip> clips=new List<AnimationClip>();
    public float CurrentScale=>current;
    public void Initialize(PetTownGame game,PetTownRanchAnimal bird,PetTownRanchModel source)
    {
        record=bird;if(visual)return;canWalk=source.walk;modelHeight=source.height;uint seed=2166136261;foreach(char c in bird.id)seed=unchecked((seed^c)*16777619);random=new System.Random((int)(seed&0x7fffffff));phase=(seed&255)/40f;
        visual=new GameObject("RanchVisual").transform;visual.SetParent(transform,false);var model=Instantiate(source.prefab,visual);model.transform.localPosition=Vector3.zero;foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;foreach(var c in model.GetComponentsInChildren<Collider>(true)){c.enabled=false;if(c is CharacterController controller)controller.stepOffset=0;}foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;source.idle.SampleAnimation(model,0);
        float min=float.PositiveInfinity,max=float.NegativeInfinity;var scratch=new Mesh();foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.BakeMesh(scratch,true);foreach(var vertex in scratch.vertices){float y=skin.transform.TransformPoint(vertex).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);}}Destroy(scratch);if(max>min){float k=source.height/(max-min),pivot=model.transform.position.y;model.transform.localScale*=k;model.transform.position+=Vector3.up*(transform.position.y-(pivot+(min-pivot)*k));}
        var bones=model.GetComponentsInChildren<Transform>(true);var positions=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)positions[i]=bones[i].localPosition;gameObject.AddComponent<PetTownCompanionPose>().Initialize(bones,positions);
        animation=model.GetComponentInChildren<Animation>(true);if(!animation)animation=model.AddComponent<Animation>();animation.Stop();animation.playAutomatically=false;foreach(var pair in new[]{new KeyValuePair<string,AnimationClip>("RanchIdle",source.idle),new KeyValuePair<string,AnimationClip>("RanchWalk",source.walk)}){if(!pair.Value)continue;var copy=Instantiate(pair.Value);copy.legacy=true;copy.wrapMode=WrapMode.Loop;animation.AddClip(copy,pair.Key);clips.Add(copy);}animation.Play("RanchIdle");PetTownVisualGrounding.Attach(game,transform,visual);destination=transform.localPosition;current=bird.Size;ApplyGrowth(0);
    }
    public void ApplyGrowth(float seconds){if(!visual||record==null)return;current=Mathf.MoveTowards(current,record.Size,Mathf.Max(0,seconds)*.3f);visual.localScale=Vector3.one*current;}
    void Update(){if(Application.isFocused)Tick(Mathf.Min(Time.deltaTime,.25f));}
    public void Tick(float seconds)
    {
        if(!visual||seconds<=0||!canWalk)return;if(Time.time>=nextPick){nextPick=Time.time+4+(float)random.NextDouble()*4;destination=PetTownRanchView.RoamPoint(record.species,random);}
        var old=transform.localPosition;var point=Vector3.MoveTowards(old,destination,seconds*(record.Adult?.55f:.35f));var direction=point-old;transform.localPosition=point;bool walking=direction.sqrMagnitude>.0000001f;if(walking)transform.localRotation=Quaternion.Slerp(transform.localRotation,Quaternion.LookRotation(direction),seconds*5);if(walking!=moving){moving=walking;animation.CrossFade(moving?"RanchWalk":"RanchIdle",.15f);}
    }
    void LateUpdate(){ApplyGrowth(Time.deltaTime);ShowProduct();}
    // Production cue: a slowly turning milk bottle / wool puff floats above a cow or sheep whose product is ready,
    // the animal stretches gently while it waits, and springs once when the product is collected.
    void ShowProduct()
    {
        if(!visual||record==null)return;
        bool ready=record.ready>0&&record.Adult&&record.species<2;
        if(wasReady&&!ready)cheerStart=Time.time;wasReady=ready;
        if(ready&&!marker)MakeMarker();
        if(marker)
        {
            if(marker.gameObject.activeSelf!=ready)marker.gameObject.SetActive(ready);
            if(ready){marker.position=transform.position+Vector3.up*(modelHeight*current+.4f+Mathf.Sin(Time.time*2.4f+phase)*.06f);marker.Rotate(0,100*Time.deltaTime,0,Space.World);}
        }
        float stretch=ready?Mathf.Max(0,Mathf.Sin(Time.time*1.3f+phase))*.03f:0;
        float c=Time.time-cheerStart;if(c>=0&&c<.7f)stretch+=Mathf.Sin(c/.7f*Mathf.PI*3)*.12f*(1-c/.7f);
        if(Mathf.Abs(stretch)>.0001f)visual.localScale=new Vector3(1-stretch*.5f,1+stretch,1-stretch*.5f)*current;
    }
    void MakeMarker()
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)return;markerMaterial=new Material(shader){name="RanchProductMarker"};
        var root=new GameObject(record.species==0?"MilkMarker":"WoolMarker").transform;marker=root;
        if(record.species==0)
        {
            markerMaterial.SetColor("_BaseColor",new Color(.94f,.97f,1f));markerMaterial.SetFloat("_Smoothness",.55f);
            var bottle=GameObject.CreatePrimitive(PrimitiveType.Capsule);bottle.transform.SetParent(root,false);bottle.transform.localScale=new Vector3(.15f,.17f,.15f);
            var cap=GameObject.CreatePrimitive(PrimitiveType.Sphere);cap.transform.SetParent(root,false);cap.transform.localPosition=new Vector3(0,.2f,0);cap.transform.localScale=Vector3.one*.1f;
            Style(bottle,markerMaterial);var capMaterial=new Material(shader){name="RanchProductCap"};capMaterial.SetColor("_BaseColor",new Color(.35f,.6f,.95f));Style(cap,capMaterial);extra=capMaterial;
        }
        else
        {
            markerMaterial.SetColor("_BaseColor",new Color(.99f,.96f,.9f));markerMaterial.SetFloat("_Smoothness",.05f);
            for(int i=0;i<4;i++){var puff=GameObject.CreatePrimitive(PrimitiveType.Sphere);puff.transform.SetParent(root,false);var a=i*Mathf.PI*.5f;puff.transform.localPosition=new Vector3(Mathf.Cos(a)*.07f,i%2*.04f,Mathf.Sin(a)*.07f);puff.transform.localScale=Vector3.one*.15f;Style(puff,markerMaterial);}
        }
    }
    Material extra;
    static void Style(GameObject node,Material material){var c=node.GetComponent<Collider>();if(c)Destroy(c);var r=node.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
    void OnDestroy(){foreach(var clip in clips)if(clip)Destroy(clip);if(marker)Destroy(marker.gameObject);if(markerMaterial)Destroy(markerMaterial);if(extra)Destroy(extra);}
}
