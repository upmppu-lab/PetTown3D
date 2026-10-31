using TMPro;
using UnityEngine;

// Name label for buildings / decorations in the same style as the top-bar chips: a dark rounded pill with a gold rim and cream text.
// It always faces the camera (PetTownSignBoard) and stays close to the thing it names.
public sealed class PetTownPillLabel : MonoBehaviour
{
    static Sprite pillSprite;
    SpriteRenderer bg;
    TextMeshPro text;

    public static Sprite PillSprite()
    {
        if (pillSprite != null) return pillSprite;
        const int S = 64; const float half = 32f, radius = 28f, rim = 3.5f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "PillLabel", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[S * S];
        Color fill = new Color(.16f, .12f, .08f, .86f), gold = new Color(.93f, .72f, .30f, 1f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - half) - (half - radius), 0f), dy = Mathf.Max(Mathf.Abs(y + .5f - half) - (half - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;          // < 0 inside the shape
                float cover = Mathf.Clamp01(.5f - d);                        // 1px anti-aliased edge
                float rimK = Mathf.Clamp01((d + rim) + .5f);                 // 0 inside, 1 on the rim
                Color c = Color.Lerp(fill, gold, rimK);
                c.a *= cover; px[y * S + x] = c;
            }
        tex.SetPixels(px); tex.Apply();
        pillSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(28, 28, 28, 28));
        return pillSprite;
    }

    public static PetTownPillLabel Create(Transform parent, string label, Vector3 localPosition, float fontSize)
    {
        var go = new GameObject("NameBoard");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.AddComponent<PetTownSignBoard>();
        var p = go.AddComponent<PetTownPillLabel>();

        var bgObject = new GameObject("Pill");
        bgObject.transform.SetParent(go.transform, false);
        bgObject.transform.localPosition = new Vector3(0, 0, .02f);
        p.bg = bgObject.AddComponent<SpriteRenderer>();
        p.bg.sprite = PillSprite(); p.bg.drawMode = SpriteDrawMode.Sliced; p.bg.sortingOrder = 0;
        p.bg.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; p.bg.receiveShadows = false;

        var textObject = new GameObject("Label");
        textObject.transform.SetParent(go.transform, false);
        p.text = textObject.AddComponent<TextMeshPro>();
        textObject = p.text.gameObject;
        var library = PetTownCozyLibrary.Current;
        if (library != null && library.style != null && library.style.font != null) p.text.font = library.style.font;
        p.text.fontSize = fontSize; p.text.fontStyle = FontStyles.Bold;
        p.text.alignment = TextAlignmentOptions.Center;
        p.text.color = new Color(1f, .94f, .78f);
        p.text.textWrappingMode = TextWrappingModes.NoWrap;
        p.text.raycastTarget = false; p.text.sortingOrder = 2;
        p.text.rectTransform.sizeDelta = new Vector2(30f, 3f);
        p.SetText(label);
        return p;
    }

    public void SetText(string label)
    {
        text.text = label;
        text.rectTransform.sizeDelta = new Vector2(30f, 3f);
        text.ForceMeshUpdate();
        Vector2 pv = text.GetPreferredValues(label);
        float w = Mathf.Max(1.6f, pv.x + .7f), h = Mathf.Max(.8f, pv.y + .36f);
        bg.size = new Vector2(w, h);
        text.rectTransform.sizeDelta = new Vector2(w, h);
    }
}
