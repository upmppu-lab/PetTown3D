using UnityEngine;
// Curated references to the user's existing imported packs. No source pack is modified.
public sealed class PetTownSceneryLibrary : ScriptableObject
{
    public GameObject kiosk,picnic,trash,boat,woodenBridge,plaza,parkFlowerBed,parkBush;public GameObject[] parkTrees;
    public GameObject wheelbarrow,scarecrow,shovel,rake,pitchfork,produceCrate,basket,barrel,hayRound,hayThin,sign,cart,churn,wheat,corn,carrot,pumpkin,furrow;
    public GameObject[] detailedFlowers; public AnimationClip fountainFlow;
    static PetTownSceneryLibrary cached;public static PetTownSceneryLibrary Current=>cached?cached:(cached=Resources.Load<PetTownSceneryLibrary>("Environment/Scenery/Library"));
}
