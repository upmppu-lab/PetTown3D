using UnityEngine;

// Park playground corner (north-west quadrant, view-local metres) built from the Playground Low Poly pack.
// Pieces are placed at runtime like the rest of the park, so existing saved parks pick them up without any save change.
public sealed partial class PetTownCommunityView
{
    public PetTownPlaygroundRig Playground{get;private set;}
    // Standing points beside each piece (slot order matches PetTownPlaygroundRig): swing left/right, seesaw high/low end, slide, sandbox.
    static readonly Vector3[] ParkVisit=
    {
        new Vector3(-3.47f,.045f,5.35f),new Vector3(-2.23f,.045f,5.35f),new Vector3(-3.0f,.045f,3.35f),new Vector3(-5.0f,.045f,3.35f),new Vector3(-5.0f,.045f,5.74f),new Vector3(-6.25f,.045f,3.3f)
    };
    internal int SlotCount=>Kind==5?PetTownPlaygroundRig.SlotCount:4;

    void BuildParkPlayground()
    {
        if(!library||library.playground==null||!library.playgroundMaterial)return;
        Playground=gameObject.GetComponent<PetTownPlaygroundRig>();if(!Playground)Playground=gameObject.AddComponent<PetTownPlaygroundRig>();
        // 0 slide structure, 1 two-seat swing frame, 2 seesaw, 3 sand ring. Clear of the stone walkways, benches, lamp and visit points.
        var slide=Equipment(0,"Playground_Slide",new Vector3(-5.75f,.045f,6.55f),180,.85f);if(slide)Playground.AttachSlide(slide);
        var swings=Equipment(1,"Playground_Swings",new Vector3(-2.85f,.045f,6.55f),0,.92f);if(swings)Playground.AttachSwings(swings,library.playgroundMaterial);
        var seesaw=Equipment(2,"Playground_Seesaw",new Vector3(-4.15f,.045f,4.2f),90,.85f);if(seesaw)Playground.AttachSeesaw(seesaw,library.playgroundMaterial);
        var sand=Equipment(3,"Playground_Sandbox",new Vector3(-6.25f,.045f,4.55f),0,1f);if(sand)Playground.AttachSand(sand);
    }

    Transform Equipment(int index,string label,Vector3 pos,float yaw,float scale)
    {
        if(index<0||index>=library.playground.Length||!library.playground[index])return null;
        var holder=new GameObject(label);holder.transform.SetParent(models,false);
        var fit=new GameObject("AlignedFit").transform;fit.SetParent(holder.transform,false);
        var instance=Instantiate(library.playground[index],fit);instance.transform.localPosition=Vector3.zero;
        foreach(var c in instance.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        // The pack ships Built-in "Standard" materials; every slot uses the project's URP copy instead.
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=index==3&&library.playgroundSandMaterial?library.playgroundSandMaterial:library.playgroundMaterial;renderer.sharedMaterials=slots;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        fit.localScale=Vector3.one*scale;
        var b=BoundsOf(holder.transform);fit.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);
        holder.transform.localPosition=pos;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var area=ViewBounds(holder.transform);
        if(area.size.x>.2f&&area.size.z>.2f)Block(label+"Collision",new Vector3(area.center.x,Mathf.Max(.3f,area.center.y),area.center.z),new Vector3(area.size.x,Mathf.Max(.6f,area.size.y),area.size.z));
        return instance.transform;
    }

    // Bounds of a placed model expressed in this view's local space (BoundsOf measures in the model's own space).
    Bounds ViewBounds(Transform target)
    {
        bool first=true;var bounds=new Bounds();
        foreach(var filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var point=transform.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
}
