using UnityEngine;

// Imported clips may contain translation keys. Preserve the bind translations while keeping
// animated joint rotations, so animation cannot move the rendered animal away from its agent.
public sealed class PetTownCompanionPose : MonoBehaviour
{
    Transform[] bones;Vector3[] positions;
    public void Initialize(Transform[] transforms,Vector3[] rest){bones=transforms;positions=rest;}
    void LateUpdate(){if(bones==null)return;for(int i=0;i<bones.Length;i++)if(bones[i])bones[i].localPosition=positions[i];}
}
