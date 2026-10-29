using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
public sealed class PetTownPoultryBirdVisual : MonoBehaviour
{
    PetTownChickenRecord record;Transform visual;Animation animation;System.Random random;Vector3 destination;float nextPick,current=1;bool moving;
    readonly List<AnimationClip> clips=new List<AnimationClip>();
    public float CurrentScale=>current;
    public void Initialize(PetTownGame game,PetTownChickenRecord bird,PetTownPoultryLibrary source)
    {
        record=bird;if(visual)return;uint seed=2166136261;foreach(char c in bird.id)seed=unchecked((seed^c)*16777619);random=new System.Random((int)(seed&0x7fffffff));
        visual=new GameObject("ChickenVisual").transform;visual.SetParent(transform,false);var model=Instantiate(source.chicken,visual);model.transform.localPosition=Vector3.zero;foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;foreach(var c in model.GetComponentsInChildren<Collider>(true)){c.enabled=false;if(c is CharacterController controller)controller.stepOffset=0;}foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;source.idle.SampleAnimation(model,0);
        float min=float.PositiveInfinity,max=float.NegativeInfinity;var scratch=new Mesh();foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.BakeMesh(scratch,true);foreach(var vertex in scratch.vertices){float y=skin.transform.TransformPoint(vertex).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);}}Destroy(scratch);if(max>min){float k=.5f/(max-min),pivot=model.transform.position.y;model.transform.localScale*=k;model.transform.position+=Vector3.up*(transform.position.y-(pivot+(min-pivot)*k));}
        var bones=model.GetComponentsInChildren<Transform>(true);var positions=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)positions[i]=bones[i].localPosition;gameObject.AddComponent<PetTownCompanionPose>().Initialize(bones,positions);
        animation=model.GetComponentInChildren<Animation>(true);if(!animation)animation=model.AddComponent<Animation>();animation.Stop();animation.playAutomatically=false;foreach(var pair in new[]{new KeyValuePair<string,AnimationClip>("PoultryIdle",source.idle),new KeyValuePair<string,AnimationClip>("PoultryWalk",source.walk)}){var copy=Instantiate(pair.Value);copy.legacy=true;copy.wrapMode=WrapMode.Loop;animation.AddClip(copy,pair.Key);clips.Add(copy);}animation.Play("PoultryIdle");PetTownVisualGrounding.Attach(game,transform,visual);destination=transform.localPosition;current=bird.Size;ApplyGrowth(0);
    }
    public void ApplyGrowth(float seconds){if(!visual||record==null)return;current=Mathf.MoveTowards(current,record.Size,Mathf.Max(0,seconds)*.3f);visual.localScale=Vector3.one*current*(record.sex==1?1.12f:1f);}
    void Update(){if(Application.isFocused)Tick(Mathf.Min(Time.deltaTime,.25f));}
    public void Tick(float seconds)
    {
        if(!visual||seconds<=0)return;if(Time.time>=nextPick){nextPick=Time.time+4+(float)random.NextDouble()*4;destination=new Vector3(PetTownCoopView.Center.x-1.1f+(float)random.NextDouble()*2.2f,.05f,5.8f+(float)random.NextDouble()*1.25f);}
        var old=transform.localPosition;var point=Vector3.MoveTowards(old,destination,seconds*(record.Adult?.2f:.13f));var direction=point-old;transform.localPosition=point;bool walking=direction.sqrMagnitude>.0000001f;if(walking)transform.localRotation=Quaternion.Slerp(transform.localRotation,Quaternion.LookRotation(direction),seconds*5);if(walking!=moving){moving=walking;animation.CrossFade(moving?"PoultryWalk":"PoultryIdle",.15f);}
    }
    void LateUpdate(){ApplyGrowth(Time.deltaTime);}
    void OnDestroy(){foreach(var clip in clips)if(clip)Destroy(clip);}
}
