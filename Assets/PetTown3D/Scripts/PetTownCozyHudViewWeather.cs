using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 날씨를 글자 대신 작은 그림으로 보여 줍니다 (해/달/구름/비/눈).
public sealed partial class PetTownCozyHudView
{
    Image weatherIcon;
    int weatherIconKind = -1;
    readonly Dictionary<int, Sprite> weatherSprites = new Dictionary<int, Sprite>();
    readonly List<Texture2D> weatherTextures = new List<Texture2D>();

    Sprite sunSprite;

    void BuildWeatherIcon()
    {
        // 날짜 칩의 해 그림을 날씨 칩으로 옮기고, 날짜 칩은 글자만 남긴다.
        var sun = calendar.Find("Sun");
        if (sun)
        {
            sun.SetParent(weather, false); weatherIcon = sun.GetComponent<Image>(); sunSprite = weatherIcon.sprite; weatherIcon.preserveAspect = true;
            Fixed(sun as RectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(6, 0), new Vector2(23, 23));
        }
        PetTownUITheme.Place(dayLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
        PetTownUITheme.Place(weatherLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(32, 0), new Vector2(-6, 0));
        weatherLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void UpdateWeatherIcon(PetTownGame game)
    {
        if (!weatherIcon) return;
        int kind = 0; // 0 해, 1 달, 2 구름, 3 비, 4 눈
        if (game != null)
        {
            string name = game.TimeWeather.WeatherName;
            kind = name == "비" ? 3 : name == "눈" ? 4 : name == "흐림" ? 2 : game.TimeWeather.Night ? 1 : 0;
        }
        if (kind == weatherIconKind) return;
        weatherIconKind = kind; weatherIcon.sprite = kind == 0 && sunSprite ? sunSprite : WeatherSprite(kind);
    }

    // 칩 길이를 안의 글자에 딱 맞춘다.
    void FitChips()
    {
        float day = dayLabel.GetPreferredValues(dayLabel.text).x + 22;
        float wea = weatherLabel.GetPreferredValues(weatherLabel.text).x + 32 + 10;
        float peo = population.GetPreferredValues(population.text).x + 30 + 12;
        float rk = reputationLabel.GetPreferredValues(reputationLabel.text).x + 24;
        calendar.sizeDelta = new Vector2(day, calendar.sizeDelta.y);
        weather.sizeDelta = new Vector2(wea, weather.sizeDelta.y);
        peopleChip.sizeDelta = new Vector2(peo, peopleChip.sizeDelta.y);
        peopleChip.anchoredPosition = new Vector2(weather.anchoredPosition.x + wea + 6, peopleChip.anchoredPosition.y);
        rank.sizeDelta = new Vector2(rk, rank.sizeDelta.y);
    }

    void DisposeWeatherIcons()
    {
        foreach (var s in weatherSprites.Values) if (s) Destroy(s);
        foreach (var t in weatherTextures) if (t) Destroy(t);
        weatherSprites.Clear(); weatherTextures.Clear();
    }

    static float Cover(float dist, float edge) { return Mathf.Clamp01(.5f - dist / edge); }
    static float SegDist(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return Vector2.Distance(p, a + ab * t); }

    Sprite WeatherSprite(int kind)
    {
        Sprite cached; if (weatherSprites.TryGetValue(kind, out cached) && cached) return cached;
        const int N = 96; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        Color sun = new Color32(0xFF, 0xC8, 0x2E, 255), moon = new Color32(0xFF, 0xE9, 0x9A, 255), cloud = new Color32(0xF4, 0xF7, 0xFB, 255), cloudDark = new Color32(0xC9, 0xD3, 0xE0, 255), rain = new Color32(0x4C, 0xA8, 0xF0, 255);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            var p = new Vector2(x + .5f, y + .5f); Color c = new Color(0, 0, 0, 0);
            if (kind == 0)
            {
                float a = Cover(Vector2.Distance(p, new Vector2(48, 48)) - 21, 1.6f);
                for (int r = 0; r < 8; r++) { float ang = r * Mathf.PI / 4; var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); a = Mathf.Max(a, Cover(SegDist(p, new Vector2(48, 48) + dir * 30, new Vector2(48, 48) + dir * 40) - 3.2f, 1.6f)); }
                c = sun; c.a = a;
            }
            else if (kind == 1)
            {
                float a = Cover(Vector2.Distance(p, new Vector2(46, 48)) - 30, 1.6f) * (1f - Cover(Vector2.Distance(p, new Vector2(62, 56)) - 26, 1.6f));
                c = moon; c.a = a;
            }
            else
            {
                float a = Mathf.Max(Cover(Vector2.Distance(p, new Vector2(36, 54)) - 16, 1.6f), Cover(Vector2.Distance(p, new Vector2(54, 62)) - 21, 1.6f));
                a = Mathf.Max(a, Cover(Vector2.Distance(p, new Vector2(70, 52)) - 14, 1.6f));
                float body = (x > 22 && x < 84 && y > 38 && y < 54) ? Cover(Mathf.Abs(y - 46) - 8, 1.6f) : 0f; a = Mathf.Max(a, body);
                c = Color.Lerp(cloud, cloudDark, Mathf.Clamp01((54 - y) / 30f)); c.a = a;
                if (kind == 3) for (int d = 0; d < 3; d++) { float ra = Cover(SegDist(p, new Vector2(34 + d * 16, 32), new Vector2(30 + d * 16, 16)) - 2.8f, 1.6f); if (ra > c.a) { c = rain; c.a = ra; } }
                if (kind == 4) for (int d = 0; d < 3; d++) { float ra = Cover(Vector2.Distance(p, new Vector2(34 + d * 16, 28 - (d % 2) * 10)) - 4.2f, 1.6f); if (ra > c.a) { c = Color.white; c.a = ra; var edge = Cover(Vector2.Distance(p, new Vector2(34 + d * 16, 28 - (d % 2) * 10)) - 4.2f, 1.6f); } }
            }
            px[y * N + x] = c;
        }
        tex.SetPixels(px); tex.Apply(); weatherTextures.Add(tex);
        var sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100f); weatherSprites[kind] = sprite; return sprite;
    }
}
