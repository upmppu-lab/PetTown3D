using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small "sit down / stand up" button shown above the bottom menu while the hero is next to a chair or bench.
// Same pill look as the HUD chips and the name labels.
public static class PetTownHeroSitUI
{
    static GameObject root;
    static TextMeshProUGUI label;
    static System.Action click;
    public static bool Visible { get { return root && root.activeSelf; } }

    public static void Show(string text, System.Action onClick)
    {
        Ensure();
        label.text = text; click = onClick;
        if (!root.activeSelf) root.SetActive(true);
    }
    public static void Hide()
    {
        click = null;
        if (root && root.activeSelf) root.SetActive(false);
    }
    static void Ensure()
    {
        if (root) return;
        root = new GameObject("HeroSitButton");
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(520, 900); scaler.matchWidthOrHeight = Screen.height >= Screen.width ? 0f : 1f;
        root.AddComponent<GraphicRaycaster>();
        var button = new GameObject("Button", typeof(RectTransform)); button.transform.SetParent(root.transform, false);
        var rt = (RectTransform)button.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0); rt.pivot = new Vector2(.5f, 0);
        rt.anchoredPosition = new Vector2(0, 112); rt.sizeDelta = new Vector2(112, 40);
        var img = button.AddComponent<Image>(); img.sprite = PetTownPillLabel.PillSprite(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2.5f;
        var b = button.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(() => { if (click != null) click(); });
        var textObject = new GameObject("Label", typeof(RectTransform)); textObject.transform.SetParent(button.transform, false);
        var trt = (RectTransform)textObject.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(8, 2); trt.offsetMax = new Vector2(-8, -2);
        label = textObject.AddComponent<TextMeshProUGUI>();
        var library = PetTownCozyLibrary.Current;
        if (library != null && library.style != null && library.style.font != null) label.font = library.style.font;
        label.fontSize = 20; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, .94f, .78f); label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        root.SetActive(false);
    }
}
