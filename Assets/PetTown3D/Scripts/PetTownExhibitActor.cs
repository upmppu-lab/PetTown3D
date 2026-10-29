using UnityEngine;
// Enclosures are carved out of the visitor NavMesh. Display animals use their original idle motion.
public sealed class PetTownExhibitActor : MonoBehaviour
{
    AnimationClip copy,walkCopy;int species;bool roam,moving;Vector3 target;float wait;
    Animation anim;Transform visualRoot;bool staticModel;float gaitPhase,gaitBlend,modelHeight=1;string currentClip="";
    static readonly float[] Speeds={.55f,.65f,.30f,.38f,.42f,.55f};
    // Slow wandering inside the animal's own enclosure (the exhibit keeps its idle animation while it glides).
    public void EnableRoam(int speciesIndex){species=Mathf.Clamp(speciesIndex,0,Speeds.Length-1);roam=true;wait=Random.Range(1f,5f);}
    void Update()
    {
        if(!roam)return;float dt=Mathf.Min(Time.deltaTime,.1f);
        if(!moving){wait-=dt;if(wait<=0)PickTarget();Gait(false,0,dt);return;}
        var pos=transform.localPosition;var to=target-pos;to.y=0;float d=to.magnitude;if(d<.08f){moving=false;wait=Random.Range(2.5f,7f);Gait(false,0,dt);return;}
        var want=Quaternion.LookRotation(to/d,Vector3.up);transform.localRotation=Quaternion.RotateTowards(transform.localRotation,want,95*dt);
        float facing=Mathf.Clamp01(Vector3.Dot(transform.localRotation*Vector3.forward,to/d));
        float step=Speeds[species]*dt*facing;
        transform.localPosition=pos+transform.localRotation*Vector3.forward*step;
        Gait(facing>.5f,step,dt);
    }
    // Walk clip for rigged animals (deer, tiger, penguin); static-mesh animals (elephant, giraffe, zebra) get a
    // procedural plodding gait (body bob + sway) because they have no skeleton to animate.
    void Gait(bool walking,float step,float dt)
    {
        if(anim&&walkCopy)
        {
            string want=walking?"ExhibitWalk":"ExhibitIdle";
            if(want!=currentClip&&anim.GetClip(want)){anim.CrossFade(want,.25f);currentClip=want;}
            if(walking){var state=anim["ExhibitWalk"];if(state!=null)state.speed=Mathf.Clamp(Speeds[species]/.8f,.5f,1.3f);}
        }
        if(staticModel&&visualRoot)
        {
            gaitBlend=Mathf.MoveTowards(gaitBlend,walking?1f:0f,dt*3f);
            if(walking)gaitPhase+=step/Mathf.Max(.5f,modelHeight*.55f)*Mathf.PI*2f;
            float s=Mathf.Sin(gaitPhase);
            visualRoot.localPosition=new Vector3(0,Mathf.Abs(s)*modelHeight*.014f*gaitBlend,0);
            visualRoot.localRotation=Quaternion.Euler(Mathf.Sin(gaitPhase*2f+1f)*1.2f*gaitBlend,0,s*2.4f*gaitBlend);
        }
    }
    void PickTarget()
    {
        int h=species<=2?species:3;var habitat=PetTownZooLayout.Habitats[h];
        for(int i=0;i<12;i++)
        {
            var c=transform.localPosition+new Vector3(Random.Range(-2.4f,2.4f),0,Random.Range(-2.4f,2.4f));
            if(habitat.Outside(new Vector2(c.x,c.z))>-1.0f||Crowded(c))continue;
            target=new Vector3(c.x,transform.localPosition.y,c.z);moving=true;return;
        }
        wait=2;
    }
    bool Crowded(Vector3 c)
    {
        var parent=transform.parent;if(!parent)return false;
        foreach(Transform other in parent){if(other==transform)continue;var delta=other.localPosition-c;delta.y=0;if(delta.sqrMagnitude<1.4f)return true;}
        return false;
    }
    public void Initialize(PetTownGame game,PetTownExhibitModel source)
    {
        var visual=new GameObject("ExhibitVisual").transform;visual.SetParent(transform,false);var model=Instantiate(source.prefab,visual);foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;foreach(var c in model.GetComponentsInChildren<Collider>(true)){c.enabled=false;if(c is CharacterController controller)controller.stepOffset=0;}foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;if(source.idle)source.idle.SampleAnimation(model,0);
        float min=float.PositiveInfinity,max=float.NegativeInfinity;var scratch=new Mesh();foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.BakeMesh(scratch,true);foreach(var v in scratch.vertices){float y=skin.transform.TransformPoint(v).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);}}if(Application.isPlaying)Destroy(scratch);else DestroyImmediate(scratch);if(max>min){float k=source.height/(max-min);float pivot=model.transform.position.y;model.transform.localScale*=k;model.transform.position+=Vector3.up*(transform.position.y-(pivot+(min-pivot)*k));}
        var bones=model.GetComponentsInChildren<Transform>(true);var positions=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)positions[i]=bones[i].localPosition;gameObject.AddComponent<PetTownCompanionPose>().Initialize(bones,positions);
        var animation=model.GetComponentInChildren<Animation>(true);if(!animation)animation=model.AddComponent<Animation>();animation.Stop();animation.playAutomatically=false;if(source.idle){copy=Instantiate(source.idle);copy.legacy=true;copy.wrapMode=WrapMode.Loop;animation.AddClip(copy,"ExhibitIdle");animation.Play("ExhibitIdle");currentClip="ExhibitIdle";}
        if(source.walk){walkCopy=Instantiate(source.walk);walkCopy.legacy=true;walkCopy.wrapMode=WrapMode.Loop;animation.AddClip(walkCopy,"ExhibitWalk");}
        anim=animation;visualRoot=visual;modelHeight=source.height;staticModel=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0;
       if(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0)PetTownVisualGrounding.Attach(game,transform,visual);
    }
    void OnDestroy(){if(copy)Destroy(copy);if(walkCopy)Destroy(walkCopy);}
}
