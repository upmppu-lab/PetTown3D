using UnityEngine;
[System.Serializable] public sealed class PetTownExhibitModel {public GameObject prefab;public AnimationClip idle,walk;public float height=1;}
public sealed class PetTownCommunityLibrary : ScriptableObject
{
    public GameObject grass,path,bench,fountain,lamp,fence,flowers,rock;
    public Material stonePathMaterial;
    public Texture2D stonePathHeight;
    public GameObject[] meadowPatches;
    public GameObject zooShelter,zooHayRack,zooEnrichment,zooRail,zooCliff;
    public PetTownExhibitModel[] animals=new PetTownExhibitModel[3];
    public Texture2D[] thumbnails=new Texture2D[3];
    // Playground Low Poly pieces for the park corner (0 slide, 1 swings, 2 seesaw, 3 sandbox) and the lake fishing rod.
    public GameObject[] playground;
    public Material playgroundMaterial,playgroundSandMaterial;
    public GameObject fishingRod;
    static PetTownCommunityLibrary cached;
    public static PetTownCommunityLibrary Current=>cached?cached:(cached=Resources.Load<PetTownCommunityLibrary>("Environment/Community/Library"));
}
