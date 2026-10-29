using UnityEngine;

// Shared positions for paving, barriers, visitors, animals and planting.
public static class PetTownZooLayout
{
    public struct Habitat
    {
        public string name;public Vector2 center,radius;public float phase;
        public Habitat(string name,float x,float z,float rx,float rz,float phase){this.name=name;center=new Vector2(x,z);radius=new Vector2(rx,rz);this.phase=phase;}
        public Vector3 Edge(float angle,float inset=0){float w=1+.055f*Mathf.Sin(angle*3+phase)+.025f*Mathf.Cos(angle*5+phase);return new Vector3(center.x+Mathf.Cos(angle)*(radius.x*w-inset),.052f,center.y+Mathf.Sin(angle)*(radius.y*w-inset));}
        public float Outside(Vector2 p){var d=p-center;float a=Mathf.Atan2(d.y/radius.y,d.x/radius.x);float w=1+.055f*Mathf.Sin(a*3+phase)+.025f*Mathf.Cos(a*5+phase);return (new Vector2(d.x/radius.x,d.y/radius.y).magnitude-w)*Mathf.Min(radius.x,radius.y);}
    }
    public static readonly Habitat[] Habitats={
        new Habitat("WoodlandDeer",-5.8f,-4.1f,2.9f,2.5f,.3f),
        new Habitat("TigerRockGarden",-5.3f,4.25f,3.3f,3.65f,1.1f),
        new Habitat("PenguinLagoon",4.6f,6.5f,3.45f,2.55f,2.3f),
        new Habitat("Savanna",5,-1.25f,3.65f,3.8f,.7f)
    };
    public static float Outside(Vector2 p){float d=100;foreach(var h in Habitats)d=Mathf.Min(d,h.Outside(p));return d;}
    public static Vector3 Visit(int slot){return new[]{new Vector3(0,.045f,-6.5f),new Vector3(-.1f,.045f,.4f),new Vector3(-.2f,.045f,6.2f),new Vector3(9.6f,.045f,2.6f)}[Mathf.Clamp(slot,0,3)];}
    public static Vector3 Animal(int species,int index)
    {
        int baseLimit=PetTownCommunityState.AnimalLimits[Mathf.Clamp(species,0,PetTownCommunityState.AnimalLimits.Length-1)];
        if(index>=baseLimit){var b=Animal(species,0);float sx=(index%2==0?-1:1)*(1.1f+.4f*(index-baseLimit));float sz=((index/2)%2==0?1:-1)*.9f;return new Vector3(b.x+sx,b.y,b.z+sz);}
        if(species==0)return new Vector3(-6.3f+index*1.2f,.055f,-4.1f+index*.65f);
        if(species==1)return new Vector3(-5.6f,.055f,3.8f);
        if(species==2)return new Vector3(3.3f+index*.6f,.065f,7.2f-index*.3f);
        if(species==3)return new Vector3(5.5f,.055f,.55f);
        if(species==4)return new Vector3(3.3f+index*.9f,.055f,-1.2f-index*1.6f);
        return new Vector3(5.5f+index*.55f,.055f,-2.5f-index*.5f);
    }
}
