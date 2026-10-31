using UnityEngine;

// The UI font has no male / female sign glyphs (they showed up as blank circles), so the two symbols are drawn here as small white sprites.
public static class PetTownGenderIcons
{
    static Sprite male, female;
    public static Sprite Get(string label)
    {
        if (label == "♂") return male != null ? male : (male = Make(true));
        if (label == "♀") return female != null ? female : (female = Make(false));
        return null;
    }
    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
    static Sprite Make(bool isMale)
    {
        const int S = 64; const float thick = 6f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = isMale ? "MaleIcon" : "FemaleIcon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f); float d;
                if (isMale)
                {
                    Vector2 c = new Vector2(26f, 26f); const float r = 15f;
                    d = Mathf.Abs(Vector2.Distance(p, c) - r);
                    Vector2 dir = new Vector2(1f, 1f).normalized, from = c + dir * r, tip = new Vector2(51f, 51f);
                    d = Mathf.Min(d, SegmentDistance(p, from, tip));
                    d = Mathf.Min(d, SegmentDistance(p, new Vector2(35f, 51f), tip));
                    d = Mathf.Min(d, SegmentDistance(p, new Vector2(51f, 35f), tip));
                }
                else
                {
                    Vector2 c = new Vector2(32f, 40f); const float r = 14f;
                    d = Mathf.Abs(Vector2.Distance(p, c) - r);
                    d = Mathf.Min(d, SegmentDistance(p, new Vector2(32f, 26f), new Vector2(32f, 8f)));
                    d = Mathf.Min(d, SegmentDistance(p, new Vector2(22f, 16f), new Vector2(42f, 16f)));
                }
                px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(thick * .5f - d + .5f));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100f);
    }
}
