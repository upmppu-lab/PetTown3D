using UnityEngine;
using UnityEngine.AI;

public sealed partial class PetTownCommunityView
{
    void BuildDetailedZoo()
    {
        var art=PetTownSceneryLibrary.Current;
        for(int h=0;h<PetTownZooLayout.Habitats.Length;h++)
        {
            var habitat=PetTownZooLayout.Habitats[h];HabitatFloor(habitat,h==0||h==3?meadowSurface:pathSurface);
            HabitatBarrier(habitat,h==1?1.25f:.85f,h==1);
            // Fill the exclusion with short strips matching the curved outline, avoiding rectangular over-carving.
            int rows=Mathf.CeilToInt(habitat.radius.y*2.2f/.38f);
            for(int row=0;row<rows;row++)
            {
                float z=habitat.center.y-habitat.radius.y*1.1f+(row+.5f)*.38f;
                float lo=100,hi=-100;
                for(float x=habitat.center.x-habitat.radius.x*1.1f;x<=habitat.center.x+habitat.radius.x*1.1f;x+=.12f)
                    if(habitat.Outside(new Vector2(x,z))<=.02f){lo=Mathf.Min(lo,x);hi=Mathf.Max(hi,x);}
                if(hi>=lo)Block("ZooHabitatSafety_"+h,new Vector3((lo+hi)*.5f,.6f,z),new Vector3(hi-lo+.16f,1.2f,.4f));
            }
            for(int i=0;i<3;i++)
            {
                var p=habitat.Edge((i*1.4f+.45f),.75f);var forest=PetTownForestLibrary.Current;
                Spawn(forest&&forest.rocks.Length>0?forest.rocks[i%forest.rocks.Length]:library.rock,"Zoo_NaturalRock",p,new Vector3(1.1f,.6f,1),i*63,true);
            }
        }
        // A rocky island and water, rather than the savanna's dry ground, distinguish the penguin habitat.
        Disc("PenguinSand",new Vector3(4.6f,.06f,6.5f),3.25f,2.32f,new Color(.72f,.69f,.56f),false);
        Disc("PenguinLagoonWater",new Vector3(5.25f,.073f,6.15f),1.95f,1.48f,Color.white,true);
        Spawn(library.rock,"PenguinRockIsland",new Vector3(3.6f,.063f,7.1f),new Vector3(1.7f,.45f,1.1f),25);
        Spawn(library.zooShelter,"Savanna_ShadeShelter",new Vector3(6.7f,.055f,-.8f),new Vector3(2.2f,1.8f,1.7f),65,true);
        Spawn(library.zooHayRack,"Savanna_HayRack",new Vector3(3.4f,.055f,.6f),new Vector3(1,.75f,.8f),-30,true);
        Spawn(library.zooEnrichment,"Savanna_EnrichmentBall",new Vector3(4.1f,.055f,-3),Vector3.one*.45f,0,true);
        Spawn(library.zooShelter,"Tiger_ShadeShelter",new Vector3(-6.5f,.055f,6),new Vector3(2.6f,1.7f,2),20,true);
        Tree(new Vector3(-4.6f,.055f,-2.7f),3.1f);Tree(new Vector3(-3.9f,.055f,6.4f),3.7f);
        Tree(new Vector3(2.3f,.055f,8),3.3f);Tree(new Vector3(3.3f,.055f,-3.8f),3.3f);
        Tree(new Vector3(-8,.055f,-9.4f),3);Tree(new Vector3(8.9f,.055f,-9.1f),3.6f);
        if(art)
        {
            Spawn(art.kiosk,"Zoo_VisitorGazebo",new Vector3(5.1f,.05f,-8.8f),new Vector3(3.2f,2.8f,3),0,true);
            Block("ZooGazeboSafety",new Vector3(5.1f,1,-8.8f),new Vector3(2.8f,2,2.6f));
            Spawn(art.picnic,"Zoo_Picnic",new Vector3(-4.3f,.05f,-9.1f),new Vector3(2.6f,1.1f,1.6f),0,true);
            Block("ZooPicnicSafety",new Vector3(-4.3f,.5f,-9.1f),new Vector3(2.6f,1,1.6f));
            Spawn(art.trash,"Zoo_Recycling",new Vector3(2.2f,.05f,-8.9f),new Vector3(.5f,.85f,.5f),0,true);
            FlowerPatch(new Vector3(-2.8f,.05f,-9.2f),1.1f,.6f);FlowerPatch(new Vector3(8,.05f,-8.9f),1.2f,.8f);
        }
        Spawn(library.fountain,"Zoo_WelcomeFountain",new Vector3(2.25f,.06f,-4.9f),new Vector3(1.35f,.9f,1.35f));
        Block("ZooFountainSafety",new Vector3(2.25f,.5f,-4.9f),new Vector3(1.4f,1,1.4f));
        foreach(float x in new[]{-8.3f,8.3f}){Spawn(library.bench,"Zoo_ObservationBench",new Vector3(x,.05f,1),new Vector3(1.7f,.85f,.65f),x<0?90:-90);Block("ZooBenchSafety",new Vector3(x,.4f,1),new Vector3(.65f,.8f,1.7f));}
        ZooPerimeter();
    }
    void HabitatFloor(PetTownZooLayout.Habitat habitat,Material material)
    {
        const int n=64;var v=new Vector3[n+1];var uv=new Vector2[n+1];var colors=new Color[n+1];var t=new int[n*3];v[0]=new Vector3(habitat.center.x,.05f,habitat.center.y);
        for(int i=0;i<n;i++){v[i+1]=habitat.Edge(i*Mathf.PI*2/n,.05f);t[i*3]=0;t[i*3+1]=(i+1)%n+1;t[i*3+2]=i+1;}
        for(int i=0;i<v.Length;i++){uv[i]=new Vector2(v[i].x,v[i].z)/4;colors[i]=material==pathSurface?new Color(.8f,0,0,1):Color.black;}
        var mesh=new Mesh{name=habitat.name+"_Surface"};mesh.vertices=v;mesh.triangles=t;mesh.uv=uv;mesh.colors=colors;mesh.RecalculateNormals();ownedMeshes.Add(mesh);
        var g=new GameObject(habitat.name+"_Ground");g.transform.SetParent(models,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void HabitatBarrier(PetTownZooLayout.Habitat habitat,float height,bool rocky)
    {
        const int n=32;
        for(int i=0;i<n;i++)
        {
            var a=habitat.Edge(i*Mathf.PI*2/n);var b=habitat.Edge((i+1)*Mathf.PI*2/n);var d=b-a;
            Spawn(library.zooRail?library.zooRail:library.fence,"Zoo_ObservationRailing",(a+b)*.5f,new Vector3(d.magnitude+.025f,height,.10f),-Mathf.Atan2(d.z,d.x)*Mathf.Rad2Deg);
            if(rocky&&i%2==0)Spawn(library.zooCliff?library.zooCliff:library.rock,"Tiger_RockEscarpment",habitat.Edge((i+.5f)*Mathf.PI*2/n,.15f),new Vector3(1.55f,.8f,.65f),i*360f/n);
        }
    }
    void ZooPerimeter()
    {
        for(int side=0;side<4;side++)for(int i=0;i<16;i++)
        {
            float u=-11+(i+.5f)*22/16;if(side==0&&Mathf.Abs(u-EntranceLocal.x)<1.6f)continue;
            bool horizontal=side<2;var p=horizontal?new Vector3(u,.05f,side==0?-10.9f:10.9f):new Vector3(side==2?-10.9f:10.9f,.05f,u);
            Spawn(library.zooRail?library.zooRail:library.fence,"Zoo_PerimeterRailing",p,new Vector3(22/16f,1.15f,.12f),horizontal?0:90);
            Block("ZooPerimeterSafety",p+Vector3.up*.55f,horizontal?new Vector3(22/16f,1.1f,.12f):new Vector3(.12f,1.1f,22/16f));
        }
        Spawn(library.zooShelter,"Zoo_EntranceCanopy",new Vector3(EntranceLocal.x,.05f,-10.2f),new Vector3(3.1f,2.4f,1.3f),0,true);
    }
}
