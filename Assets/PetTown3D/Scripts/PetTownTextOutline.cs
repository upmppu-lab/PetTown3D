using TMPro;
using UnityEngine;

// Applies a TextMeshPro outline once the text is enabled. Setting outlineWidth on a text created under an inactive
// parent throws a NullReferenceException because its material does not exist yet.
[DefaultExecutionOrder(100)]
public sealed class PetTownTextOutline : MonoBehaviour
{
    float width;Color32 color;bool ready;
    public void Set(float outlineWidth,Color32 outlineColor){width=outlineWidth;color=outlineColor;ready=true;if(isActiveAndEnabled)Apply();}
    void OnEnable(){if(ready)Apply();}
    void Apply(){var text=GetComponent<TMP_Text>();if(!text)return;text.outlineWidth=width;text.outlineColor=color;}
}
