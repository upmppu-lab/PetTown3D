using UnityEngine;
using UnityEngine.UI;

// Isolated graphics test. No PetTownGame, PlayerPrefs, roads or actor behaviours.
public sealed class PetTownEnvironmentComparison : MonoBehaviour
{
    PetTownEnvironmentStyle style;PetTownUITheme theme;bool natural;
    public void SetNatural(bool enabled)
    {
        natural=enabled;if(style==null)style=gameObject.AddComponent<PetTownEnvironmentStyle>();
        if(!enabled){style.Restore();return;}
        Renderer Get(string name){var child=transform.Find(name);return child!=null?child.GetComponent<Renderer>():null;}
        style.Apply(Get("EDITABLE_Terrain_Mesh"),Get("River_Smooth_Banked_Stream"),Get("River_Terrain_Conforming_Banks"),GetComponentsInChildren<PetTownNatureObstacle>());
    }
    void Start()
    {
        SetNatural(true);theme=new PetTownUITheme();var go=new GameObject("EnvironmentComparisonUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        var before=theme.Button("Original",go.transform,"기존 환경 A",()=>SetNatural(false));PetTownUITheme.Place((RectTransform)before.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(18,-64),new Vector2(180,-18));
        var after=theme.Button("Natural",go.transform,"새 환경 B",()=>SetNatural(true));PetTownUITheme.Place((RectTransform)after.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(192,-64),new Vector2(354,-18));
        if(UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()==null){var es=new GameObject("EventSystem");es.AddComponent<UnityEngine.EventSystems.EventSystem>();es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();}
    }
    void OnDestroy(){theme?.Dispose();}
}
