using System;
using UnityEngine;

[Serializable] public sealed class PetTownPetVisual
{
    public int species,appearance;public string resourcePath,displayName;public GameObject prefab;public AnimationClip idle,walk,run;public float height=.75f,yaw;public Texture2D portrait;
}
public sealed class PetTownPetVisualLibrary : ScriptableObject
{
    public PetTownPetVisual[] animals=new PetTownPetVisual[0];
    public PetTownPetVisual Find(int species,int appearance=0){PetTownPetVisual fallback=null;foreach(var animal in animals)if(animal.species==species){if(animal.appearance==appearance)return animal;if(fallback==null)fallback=animal;}return fallback;}
    public int CountLooks(int species){int n=0;foreach(var a in animals)if(a!=null&&a.species==species)n++;return n;}
    public PetTownPetVisual Look(int species,int index){int n=0;foreach(var a in animals)if(a!=null&&a.species==species){if(n==index)return a;n++;}return null;}
    static PetTownPetVisualLibrary cached;
    public static PetTownPetVisualLibrary Current=>cached?cached:(cached=Resources.Load<PetTownPetVisualLibrary>("Environment/PetVisuals/Library"));
}
