using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Always-visible "취소" button while a building is selected for placement (phones have no ESC / right click).
public sealed class PetTownPlacementCancelButton : MonoBehaviour
{
    PetTownGame game;
    PetTownUITheme theme;
    GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<PetTownPlacementCancelButton>() != null) return;
        var go = new GameObject("PetTownPlacementCancel");
        DontDestroyOnLoad(go);
        go.AddComponent<PetTownPlacementCancelButton>();
    }

    void Build()
    {
        theme = new PetTownUITheme();
        if (!theme.Ready) { theme = null; return; }
        root = new GameObject("PlacementCancelUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        var button = theme.Button("Cancel", root.transform, "건설 취소", () => { if (game != null) game.CancelPlacement(); }, new Color(1f, .82f, .8f));
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
        rt.pivot = new Vector2(.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 150);
        rt.sizeDelta = new Vector2(220, 70);
        root.SetActive(false);
    }

    void Update()
    {
        if (game == null) { game = FindFirstObjectByType<PetTownGame>(); if (game == null) return; }
        if (theme == null && root == null) Build();
        if (root == null) return;
        bool show = game.PlacementActive;
        if (root.activeSelf != show) root.SetActive(show);
        if (show && EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    void OnDestroy() { if (theme != null) theme.Dispose(); }
}
