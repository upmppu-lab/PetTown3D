using UnityEngine;

// Plays the downloaded fountain's original blend-shape water clip.
public sealed class PetTownFountainFlow : MonoBehaviour
{
    AnimationClip ownedClip;
    public void Initialize(GameObject model, AnimationClip source)
    {
        if(!model||!source)return;
        foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
        var animation=model.GetComponent<Animation>();if(!animation)animation=model.AddComponent<Animation>();
        animation.Stop();animation.playAutomatically=false;
        ownedClip=Instantiate(source);ownedClip.legacy=true;ownedClip.wrapMode=WrapMode.Loop;
        animation.AddClip(ownedClip,"OriginalWaterFlow");animation.Play("OriginalWaterFlow");
    }
    void OnDestroy(){if(ownedClip){if(Application.isPlaying)Destroy(ownedClip);else DestroyImmediate(ownedClip);}}
}
