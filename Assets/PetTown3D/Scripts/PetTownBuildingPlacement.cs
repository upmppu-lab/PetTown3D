using UnityEngine;

// Metadata only: never moves an existing building or drives an agent.
[DisallowMultipleComponent]
public sealed class PetTownBuildingPlacement : MonoBehaviour
{
    [SerializeField] PetTownBuildingSaveRecord record;
    public PetTownBuildingSaveRecord Record => record;
    public Vector3 EntranceWorld { get {var interior=GetComponent<PetTownInteriorState>();return interior!=null?transform.TransformPoint(interior.EntranceLocal):transform.position+record.Rotation*record.entranceLocal;} }
    public Vector3 EntranceForward => record.EntranceForward;
    public void Initialize(PetTownBuildingSaveRecord value){record=value.Copy(-1);}
}