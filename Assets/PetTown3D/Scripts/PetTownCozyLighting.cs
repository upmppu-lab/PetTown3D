using UnityEngine;
using UnityEngine.Rendering;

// Presentation only: real-time directional shadows, with a restrained ambient fill.
public sealed class PetTownCozyLighting : MonoBehaviour
{
    Light sun;LightShadows previousShadows;float previousStrength,previousBias,previousNormalBias;
    AmbientMode previousMode;Color previousAmbient;
    bool applied;
    public void Apply()
    {
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){sun=light;break;}
        if(!sun)return;applied=true;previousShadows=sun.shadows;previousStrength=sun.shadowStrength;previousBias=sun.shadowBias;previousNormalBias=sun.shadowNormalBias;
        previousMode=RenderSettings.ambientMode;previousAmbient=RenderSettings.ambientLight;
        sun.shadows=LightShadows.Soft;sun.shadowStrength=.78f;sun.shadowBias=.025f;sun.shadowNormalBias=.18f;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.47f,.49f,.51f);
    }
    public void Restore(){if(!applied)return;applied=false;if(sun){sun.shadows=previousShadows;sun.shadowStrength=previousStrength;sun.shadowBias=previousBias;sun.shadowNormalBias=previousNormalBias;}RenderSettings.ambientMode=previousMode;RenderSettings.ambientLight=previousAmbient;}
    void OnDestroy(){Restore();}
}
