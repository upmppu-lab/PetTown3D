using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class PetTownGame
{
    const int ShopActorLimit=24;
    readonly Dictionary<string,LivePet> shopPetActors=new Dictionary<string,LivePet>();float nextShopActors;
    static PetTownPetVisual PetVisual(int species,int appearance)=>PetTownPetVisualLibrary.Current?PetTownPetVisualLibrary.Current.Find(0,0):null;
    static AnimationClip[] PetVisualClipsFor(PetTownResidentPetRecord pet)
    {
        var visual=PetVisual(pet.species,pet.appearance);if(visual==null)return Resources.LoadAll<AnimationClip>(CompanionResource(pet));
        var clips=new List<AnimationClip>();foreach(var clip in new[]{visual.idle,visual.walk,visual.run})if(clip&&!clips.Contains(clip))clips.Add(clip);return clips.ToArray();
    }
    void RefreshShopPetActors(bool force=false)
    {
        if(loadingTown||expansionPending||!shop||(!force&&Time.unscaledTime<nextShopActors))return;nextShopActors=Time.unscaledTime+.25f;
        var data=Progression.care.pets;var removed=new List<string>();foreach(var pair in shopPetActors)if(!pair.Value.root||!data.Exists(p=>p.id==pair.Key&&!p.displayOnly)){DisposeShopPet(pair.Value);removed.Add(pair.Key);}foreach(var id in removed)shopPetActors.Remove(id);
        for(int i=0;i<livePets.Count;i++)
        {
            var live=livePets[i];if(!live.root||live.inventory||Time.frameCount<live.growthReadyFrame)continue;string id=live.careId??live.root.name+"_"+i;var pet=Progression.care.Get(id,live.root.name.Contains("ferret")?"전시 페럿":"전시 강아지 "+(i+1));pet.displayOnly=true;pet.speciesKind=live.root.name.Contains("ferret")?3:0;pet.appearance=i%2;
            var growth=live.root.GetComponent<PetTownPetGrowthVisual>();if(!growth)growth=live.root.AddComponent<PetTownPetGrowthVisual>();growth.Initialize(live.model.transform,pet);
        }
        int spawned=0;foreach(var pet in data)
        {
            if(pet.displayOnly)continue;
            if(shopPetActors.TryGetValue(pet.id,out var current)){var growth=current.root?current.root.GetComponent<PetTownPetGrowthVisual>():null;if(growth)growth.Initialize(current.model.transform,pet);continue;}
            if(shopPetActors.Count>=ShopActorLimit||spawned>=1)continue;var actor=CreateShopPetActor(pet);if(actor==null)continue;shopPetActors[pet.id]=actor;livePets.Add(actor);spawned++;
        }
    }
    // Asset sample input/controllers must never compete with our agent or pet pose owner.
    static void PreparePetSource(GameObject model)
    {
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        foreach(var collider in model.GetComponentsInChildren<Collider>(true)){collider.enabled=false;if(collider is CharacterController controller)controller.stepOffset=0;}
        foreach(var agent in model.GetComponentsInChildren<NavMeshAgent>(true))agent.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
    }
    static bool PetVisualModelHeight(GameObject model,out float min,out float max)
    {
        min=float.PositiveInfinity;max=float.NegativeInfinity;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(!skin.sharedMesh)continue;var mesh=new Mesh();skin.BakeMesh(mesh,true);
            foreach(var v in mesh.vertices){float y=skin.transform.TransformPoint(v).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);}if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
        }
        return min<float.PositiveInfinity&&max>min||CompanionModelHeight(model,out min,out max);
    }
    bool ShopPetSpawnPoint(string id,out Vector3 point)
    {
        point=Vector3.zero;var room=shop.GetComponent<PetTownInteriorState>();if(!room)return false;var bounds=room.LocalBounds;uint seed=2166136261;foreach(char c in id)seed=unchecked((seed^c)*16777619);var random=new System.Random((int)(seed&0x7fffffff));
        for(int attempt=0;attempt<32;attempt++)
        {
            var local=new Vector3(Mathf.Lerp(bounds.xMin+1,bounds.xMax-1,(float)random.NextDouble()),0,Mathf.Lerp(bounds.yMin+1,bounds.yMax-1,(float)random.NextDouble()));var candidate=room.transform.TransformPoint(local);candidate.y=Ground(candidate.x,candidate.z);NavMeshHit hit;
            if(!NavMesh.SamplePosition(candidate,out hit,.65f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-candidate.y)>.5f)continue;var inside=room.transform.InverseTransformPoint(hit.position);if(!bounds.Contains(new Vector2(inside.x,inside.z)))continue;bool busy=false;foreach(var live in livePets)if(live.root&&Vector3.Distance(live.root.transform.position,hit.position)<.65f){busy=true;break;}if(busy)continue;point=hit.position;return true;
        }
        return false;
    }
    LivePet CreateShopPetActor(PetTownPetCareRecord pet)
    {
        var visual=PetVisual(pet.speciesKind,pet.appearance);if(visual==null||!visual.prefab||!ShopPetSpawnPoint(pet.id,out var point))return null;
        var root=new GameObject("SHOP_PET_"+pet.id);root.transform.position=point;MarkNavMeshIgnore(root);var wrapper=new GameObject("PetVisual");wrapper.transform.SetParent(root.transform,false);var model=Instantiate(visual.prefab,wrapper.transform);model.name="SourceModel";model.transform.localPosition=Vector3.zero;wrapper.transform.localRotation=Quaternion.Euler(0,visual.yaw,0);
        PreparePetSource(model);
        if(visual.idle)visual.idle.SampleAnimation(model,0);
        if(!PetVisualModelHeight(wrapper,out float min,out float max)||max-min<.00001f){Destroy(root);return null;}float scale=Mathf.Clamp(visual.height/(max-min),.001f,1000);float pivot=model.transform.position.y;model.transform.localScale*=scale;model.transform.position+=Vector3.up*(point.y-(pivot+(min-pivot)*scale));
        var animator=model.GetComponentInChildren<Animator>(true);if(animator){animator.enabled=false;animator.applyRootMotion=false;}var legacy=model.GetComponentInChildren<Animation>(true);if(!legacy)legacy=(animator?animator.gameObject:model).AddComponent<Animation>();legacy.Stop();legacy.playAutomatically=false;legacy.enabled=true;
        var owner=root.AddComponent<PetTownPetClipOwner>();string idle=null,walk=null,run=null;var sources=new[]{visual.idle,visual.walk,visual.run};var keys=new[]{"PetIdle","PetWalk","PetRun"};
        for(int i=0;i<sources.Length;i++)if(sources[i]){var clip=Instantiate(sources[i]);clip.legacy=true;clip.wrapMode=WrapMode.Loop;clip.name=keys[i];legacy.AddClip(clip,keys[i]);owner.clips.Add(clip);if(i==0)idle=keys[i];else if(i==1)walk=keys[i];else run=keys[i];}
        var bones=model.GetComponentsInChildren<Transform>(true);var poses=new Vector3[bones.Length];var rotations=new Quaternion[bones.Length];var scales=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++){poses[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;scales[i]=bones[i].localScale;}
        var anim=new VillagerAnim{anim=legacy,idleClip=idle,walkClip=walk??run,runClip=run??walk,model=wrapper.transform,poseBones=bones,restPositions=poses,restRotations=rotations,restScales=scales};villagerAnimInfo[root]=anim;if(idle!=null)legacy.Play(idle);
        root.AddComponent<PetTownCompanionPose>().Initialize(bones,poses);PetTownVisualGrounding.Attach(this,root.transform,wrapper.transform);
        var agent=root.AddComponent<NavMeshAgent>();agent.radius=pet.speciesKind==2||pet.speciesKind>=3?.18f:.25f;agent.height=Mathf.Max(.4f,visual.height+.05f);agent.speed=pet.speciesKind>=3?1.25f:.9f;agent.acceleration=8;agent.angularSpeed=400;agent.stoppingDistance=.22f;agent.autoBraking=true;agent.Warp(point);
        root.AddComponent<PetTownPetGrowthVisual>().Initialize(wrapper.transform,pet);
        return new LivePet{root=root,model=wrapper,animation=anim,agent=agent,home=point,target=point,lastPosition=point,nextPick=Time.time+2,speed=agent.speed,inventory=true,careId=pet.id};
    }
    void DisposeShopPet(LivePet pet){if(pet.root){villagerAnimInfo.Remove(pet.root);pet.root.SetActive(false);Destroy(pet.root);}livePets.Remove(pet);}
    void ClearShopPetActors(){foreach(var pet in shopPetActors.Values)DisposeShopPet(pet);shopPetActors.Clear();}
}
