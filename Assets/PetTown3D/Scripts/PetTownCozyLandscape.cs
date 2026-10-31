using System.Collections.Generic;
using UnityEngine;

// Small walkable flowers/shore stones only. Terrain, obstacles and road data remain authoritative.
public sealed class PetTownCozyLandscape : MonoBehaviour
{
    PetTownGame game;GameObject root;float next;int signature=int.MinValue;
    readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
    PetTownInteriorState[] rooms;
    public int DecorationCount=>root?root.transform.childCount:0;
    public void Initialize(PetTownGame source){game=source;Refresh();}
    void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;Refresh();}
    void Refresh()
    {
        if(!game)return;rooms=FindObjectsByType<PetTownInteriorState>(FindObjectsSortMode.None);
        int hash=PetTownRoadPath.Revision;foreach(var room in rooms)hash=unchecked(hash+room.transform.position.GetHashCode()*17+room.LocalBounds.GetHashCode()*31+room.transform.rotation.GetHashCode());
        if(hash==signature&&root)return;signature=hash;Clear();root=new GameObject("Cozy_Walkable_Garden_Accents");root.transform.SetParent(transform,false);root.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild=true;
        foreach(var room in rooms)
        {
            var b=room.LocalBounds;float z=b.yMin-.95f;
            for(float x=b.xMin-.3f;x<=b.xMax+.3f;x+=1.5f)
            {
                if(Mathf.Abs(x-room.EntranceLocal.x)<2.7f)continue;
                var p=room.transform.TransformPoint(new Vector3(x,0,z));
                Place(((int)(x*10)&1)==0?"Bush_Common_Flowers":"Flower_3_Group",p,.50f);
            }
            foreach(float x in new[]{b.xMin-.9f,b.xMax+.9f})for(float zz=b.yMin+1.7f;zz<b.yMax;zz+=3)
                Place("Flower_3_Group",room.transform.TransformPoint(new Vector3(x,0,zz)),.38f);
        }
        for(float z=-96;z<=96;z+=3.6f)for(int side=-1;side<=1;side+=2)
        {
            float center=Mathf.Sin(z*.031f)*12+Mathf.Sin(z*.09f)*3,slope=.372f*Mathf.Cos(z*.031f)+.27f*Mathf.Cos(z*.09f);
            int index=Mathf.RoundToInt((z+96)/3.6f);float jitter=Mathf.Sin(index*17.37f+side*3.7f)*.55f;
            float d=6.5f+jitter;var p=new Vector3(center+side*d*Mathf.Sqrt(1+slope*slope),0,z);
            p.z+=Mathf.Sin(index*8.19f+side)*.9f;
            if(index%3!=1)Place("Rock_Medium_1",p,.24f+(index%3)*.10f);
            if(index%7==2){var group=p;group.x+=side*.65f;group.z+=.8f;Place("Rock_Medium_1",group,.58f);group.z-=1.5f;group.x+=side*.25f;Place("Rock_Medium_1",group,.25f);}
            p.x+=side*(.9f+jitter);p.z+=.6f;
            Place(index%4==0?"Bush_Common_Flowers":"Flower_3_Group",p,index%4==0?.53f:.34f);
        }
    }
    bool ClearGround(Vector3 p)
    {
        if(!game.VegetationAllowed(p))return false;
        foreach(var room in rooms){var l=room.transform.InverseTransformPoint(p);var b=room.LocalBounds;b.xMin-=.5f;b.xMax+=.5f;b.yMin-=.5f;b.yMax+=.5f;if(b.Contains(new Vector2(l.x,l.z)))return false;}
        if(PetTownRoadPath.TryNearestHit(p,4,out var road)&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(road.Point.x,road.Point.z))<road.Width*.5f+.75f)return false;
        return true;
    }
    void Place(string key,Vector3 p,float height)
    {
        if(!ClearGround(p))return;if(!prefabs.TryGetValue(key,out var prefab)){prefab=PetTownForestLibrary.Current?PetTownForestLibrary.Current.Find(key):null;if(!prefab)prefab=Resources.Load<GameObject>("Environment/"+key);prefabs[key]=prefab;}if(!prefab)return;
        p.y=game.GetGroundHeight(p.x,p.z);var obj=Instantiate(prefab,root.transform);obj.name=key;obj.transform.position=p;obj.transform.rotation=Quaternion.Euler(0,Mathf.Repeat(p.x*37+p.z*17,360),0);
        Bounds bounds=default;bool found=false;
        foreach(var mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            if(!mf.sharedMesh)continue;var b=mf.sharedMesh.bounds;for(int k=0;k<8;k++){var q=mf.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1)));if(!found){bounds=new Bounds(q,Vector3.zero);found=true;}else bounds.Encapsulate(q);}
        }
        if(!found||bounds.size.y<.001f){Destroy(obj);return;}float factor=height/bounds.size.y;obj.transform.localScale*=factor;obj.transform.position+=new Vector3((p.x-bounds.center.x)*factor,(p.y-bounds.min.y)*factor,(p.z-bounds.center.z)*factor);
        foreach(var c in obj.GetComponentsInChildren<Collider>()){c.enabled=false;Destroy(c);}
        foreach(var n in obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()){n.enabled=false;Destroy(n);}
    }
    public void Clear(){if(root){root.SetActive(false);Destroy(root);root=null;}}
    void OnDestroy(){Clear();}
}
