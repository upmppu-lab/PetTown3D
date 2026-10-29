using System.Collections.Generic;
using UnityEngine;

// Moving parts of the park playground. The pack ships every piece as one merged mesh, so the swing seats (with chains)
// and the seesaw plank are split off into their own meshes at build time and swing around a pivot while somebody uses them.
// Pieces and anchors are placed from the view; residents read the anchors through PetTownPlaygroundPlay.
public sealed class PetTownPlaygroundRig : MonoBehaviour
{
    public const int SlotCount=6;                       // 0 swing left, 1 swing right, 2 seesaw high end, 3 seesaw low end, 4 slide, 5 sandbox
    const float SwingAmplitude=24f,SwingSpeed=2.35f,SeesawRest=14f,SeesawSpeed=2.0f,Alive=.6f;
    Transform slide,sand;
    readonly Transform[] swingPivot=new Transform[2],swingSeat=new Transform[2],seesawSeat=new Transform[2];
    Transform seesawPivot,seesawRoot,swingRoot;
    readonly float[] swingAmp=new float[2],swingPhase=new float[2];float seesawAmp,seesawPhase;
    readonly float[] seen=new float[SlotCount];
    readonly List<Mesh> meshes=new List<Mesh>();

    public Transform Slide=>slide;public Transform SandBox=>sand;public Transform SwingRoot=>swingRoot;public Transform SeesawRoot=>seesawRoot;
    public bool HasSwings=>swingPivot[0]&&swingPivot[1];public bool HasSeesaw=>seesawPivot;
    public Transform SwingSeat(int i)=>swingSeat[Mathf.Clamp(i,0,1)];
    public Transform SeesawSeat(int i)=>seesawSeat[Mathf.Clamp(i,0,1)];
    public Transform SwingPivot(int i)=>swingPivot[Mathf.Clamp(i,0,1)];
    public float SwingPhase(int i)=>swingPhase[Mathf.Clamp(i,0,1)];public float SwingAmp(int i)=>swingAmp[Mathf.Clamp(i,0,1)];
    public float SeesawPhaseValue=>seesawPhase;public float SeesawAmpValue=>seesawAmp;
    public void Touch(int slot){if(slot>=0&&slot<SlotCount)seen[slot]=Time.unscaledTime;}
    bool Used(int slot)=>Time.unscaledTime-seen[slot]<Alive;

    public void AttachSlide(Transform root){slide=root;}
    public void AttachSand(Transform root){sand=root;}

    // Splits the swing frame into the static frame plus two hanging seats (chains and boards), pivoting at the top bar.
    public void AttachSwings(Transform root,Material material)
    {
        swingRoot=root;var filter=root.GetComponent<MeshFilter>();if(!filter)filter=root.GetComponentInChildren<MeshFilter>();if(!filter||!filter.sharedMesh||!filter.sharedMesh.isReadable)return;
        var source=filter.sharedMesh;
        var pivots=new[]{new Vector3(-.67f,2.44f,0),new Vector3(.67f,2.44f,0)};
        var parts=Split(source,2,(b)=>
        {
            if(b.min.y>=.59f&&b.max.y<=2.41f&&Mathf.Abs(b.center.x)>.3f&&Mathf.Abs(b.center.x)<1.05f)return b.center.x<0?1:2;
            return 0;
        },new[]{Vector3.zero,pivots[0],pivots[1]});
        if(parts==null||parts[1]==null||parts[2]==null)return;
        foreach(var m in parts)if(m)meshes.Add(m);
        filter.sharedMesh=parts[0];
        for(int i=0;i<2;i++)
        {
            var pivot=new GameObject(i==0?"SwingLeft":"SwingRight").transform;pivot.SetParent(root,false);pivot.localPosition=pivots[i];pivot.localRotation=Quaternion.identity;pivot.localScale=Vector3.one;
            var mesh=new GameObject("Seat").AddComponent<MeshFilter>();mesh.transform.SetParent(pivot,false);mesh.sharedMesh=parts[i+1];
            var r=mesh.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;
            var seat=new GameObject("SeatAnchor").transform;seat.SetParent(pivot,false);seat.localPosition=new Vector3(0,-1.81f,0);swingSeat[i]=seat;swingPivot[i]=pivot;
        }
    }

    // The plank, its handles and the end pads tilt around the fulcrum; the A-frame legs and the axle stay put.
    public void AttachSeesaw(Transform root,Material material)
    {
        seesawRoot=root;var filter=root.GetComponent<MeshFilter>();if(!filter)filter=root.GetComponentInChildren<MeshFilter>();if(!filter||!filter.sharedMesh||!filter.sharedMesh.isReadable)return;
        var source=filter.sharedMesh;var pivot=new Vector3(0,.40f,0);
        var parts=Split(source,1,(b)=>
        {
            if(Mathf.Abs(b.center.z)<.05f&&b.center.y<.38f)return 0;
            if(Mathf.Abs(b.center.x)>.1f&&b.center.y<.4f)return 0;
            return 1;
        },new[]{Vector3.zero,pivot});
        if(parts==null||parts[1]==null)return;
        foreach(var m in parts)if(m)meshes.Add(m);
        filter.sharedMesh=parts[0];
        var node=new GameObject("SeesawPlank").transform;node.SetParent(root,false);node.localPosition=pivot;node.localRotation=Quaternion.identity;node.localScale=Vector3.one;seesawPivot=node;
        var mf=new GameObject("Plank").AddComponent<MeshFilter>();mf.transform.SetParent(node,false);mf.sharedMesh=parts[1];
        var r=mf.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;
        for(int i=0;i<2;i++)
        {
            float z=i==0?1.05f:-1.05f;var seat=new GameObject(i==0?"SeatHigh":"SeatLow").transform;seat.SetParent(node,false);seat.localPosition=new Vector3(0,.25f*z+.07f,z);seesawSeat[i]=seat;
        }
    }

    void Update()
    {
        float dt=Mathf.Min(Time.deltaTime,.1f);
        for(int i=0;i<2;i++)
        {
            if(!swingPivot[i])continue;bool on=Used(i);
            swingAmp[i]=Mathf.MoveTowards(swingAmp[i],on?1:0,dt*(on?.45f:.3f));
            if(swingAmp[i]<=0.0005f){swingAmp[i]=0;swingPhase[i]=0;swingPivot[i].localRotation=Quaternion.identity;continue;}
            swingPhase[i]+=dt*SwingSpeed;
            // Positive sine = forward (+Z) travel of the seat.
            swingPivot[i].localRotation=Quaternion.Euler(-SwingAmplitude*swingAmp[i]*Mathf.Sin(swingPhase[i]),0,0);
        }
        if(seesawPivot)
        {
            bool on=Used(2)||Used(3);seesawAmp=Mathf.MoveTowards(seesawAmp,on?1:0,dt*(on?.5f:.35f));
            if(seesawAmp<=0.0005f){seesawAmp=0;seesawPhase=0;seesawPivot.localRotation=Quaternion.identity;}
            else
            {
                seesawPhase+=dt*SeesawSpeed;float tilt=Mathf.Lerp(SeesawRest,SeesawRest*Mathf.Cos(seesawPhase),seesawAmp);
                seesawPivot.localRotation=Quaternion.Euler(-(tilt-SeesawRest),0,0);
            }
        }
    }

    // Splits a mesh into connected pieces (vertices welded by position) and groups them; group 0 is the remainder.
    static Mesh[] Split(Mesh source,int movingGroups,System.Func<Bounds,int> classify,Vector3[] pivot)
    {
        var verts=source.vertices;var tris=source.triangles;if(verts.Length==0||tris.Length==0)return null;
        var weld=new Dictionary<long,int>();var id=new int[verts.Length];
        for(int i=0;i<verts.Length;i++)
        {
            long key=((long)Mathf.RoundToInt(verts[i].x*500)&0x1FFFFF)|(((long)Mathf.RoundToInt(verts[i].y*500)&0x1FFFFF)<<21)|(((long)Mathf.RoundToInt(verts[i].z*500)&0x1FFFFF)<<42);
            int g;if(!weld.TryGetValue(key,out g)){g=weld.Count;weld[key]=g;}id[i]=g;
        }
        var parent=new int[weld.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
        System.Func<int,int> find=null;find=x=>{while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;};
        for(int i=0;i<tris.Length;i+=3){int a=find(id[tris[i]]);int b=find(id[tris[i+1]]);int c=find(id[tris[i+2]]);parent[b]=a;parent[find(c)]=find(a);}
        var bounds=new Dictionary<int,Bounds>();
        for(int i=0;i<verts.Length;i++){int r=find(id[i]);Bounds b;if(!bounds.TryGetValue(r,out b))b=new Bounds(verts[i],Vector3.zero);else b.Encapsulate(verts[i]);bounds[r]=b;}
        var group=new Dictionary<int,int>();foreach(var kv in bounds)group[kv.Key]=Mathf.Clamp(classify(kv.Value),0,movingGroups);
        var normals=source.normals;var uvs=source.uv;var tangents=source.tangents;var colors=source.colors;
        var result=new Mesh[movingGroups+1];
        for(int g=0;g<=movingGroups;g++)
        {
            var remap=new Dictionary<int,int>();var v=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var t=new List<Vector4>();var c=new List<Color>();var idx=new List<int>();
            for(int i=0;i<tris.Length;i+=3)
            {
                if(group[find(id[tris[i]])]!=g)continue;
                for(int k=0;k<3;k++)
                {
                    int o=tris[i+k];int m;if(!remap.TryGetValue(o,out m)){m=v.Count;remap[o]=m;v.Add(verts[o]-pivot[g]);if(normals.Length==verts.Length)n.Add(normals[o]);if(uvs.Length==verts.Length)u.Add(uvs[o]);if(tangents.Length==verts.Length)t.Add(tangents[o]);if(colors.Length==verts.Length)c.Add(colors[o]);}
                    idx.Add(m);
                }
            }
            if(idx.Count==0){result[g]=null;continue;}
            var mesh=new Mesh{name=source.name+"_part"+g};if(v.Count>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);if(n.Count==v.Count)mesh.SetNormals(n);if(u.Count==v.Count)mesh.SetUVs(0,u);if(t.Count==v.Count)mesh.SetTangents(t);if(c.Count==v.Count)mesh.SetColors(c);
            mesh.SetTriangles(idx,0);mesh.RecalculateBounds();if(n.Count!=v.Count)mesh.RecalculateNormals();result[g]=mesh;
        }
        return result;
    }

    void OnDestroy(){foreach(var m in meshes)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}}
}
