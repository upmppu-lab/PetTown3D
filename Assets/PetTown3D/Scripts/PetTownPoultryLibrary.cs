using UnityEngine;
public sealed class PetTownPoultryLibrary : ScriptableObject
{
    public GameObject house,chicken,hay,bucket,egg,sack,gate;public AnimationClip idle,walk;public Texture2D thumbnail,birdIcon,feedIcon,waterIcon,eggIcon,cleanIcon;
    public bool Ready=>house&&chicken&&hay&&bucket&&egg&&sack&&gate&&idle&&walk;
    static PetTownPoultryLibrary cached;
    public static PetTownPoultryLibrary Current=>cached?cached:(cached=Resources.Load<PetTownPoultryLibrary>("Environment/Poultry/Library"));
}
