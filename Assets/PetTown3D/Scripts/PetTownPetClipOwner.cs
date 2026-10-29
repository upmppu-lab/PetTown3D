using System.Collections.Generic;
using UnityEngine;

public sealed class PetTownPetClipOwner : MonoBehaviour
{
    public readonly List<AnimationClip> clips=new List<AnimationClip>();
    void OnDestroy(){foreach(var clip in clips)if(clip)Destroy(clip);clips.Clear();}
}
