using UnityEngine;
[System.Serializable] public sealed class PetTownRanchModel{public GameObject prefab;public AnimationClip idle,walk;public float height;public Texture2D portrait;}
public sealed class PetTownRanchLibrary : ScriptableObject
{
    public PetTownRanchModel[] animals=new PetTownRanchModel[3];public Texture2D thumbnail;
    public bool Ready=>animals!=null&&animals.Length==3&&animals[0]!=null&&animals[1]!=null&&animals[2]!=null&&animals[0].prefab&&animals[1].prefab&&animals[2].prefab&&animals[0].idle&&animals[1].idle&&animals[2].idle&&PetTownFarmLibrary.Current&&PetTownFarmLibrary.Current.Ready&&PetTownPoultryLibrary.Current&&PetTownPoultryLibrary.Current.Ready;
    static PetTownRanchLibrary cached;public static PetTownRanchLibrary Current=>cached?cached:(cached=Resources.Load<PetTownRanchLibrary>("Environment/Ranch/Library"));
}
