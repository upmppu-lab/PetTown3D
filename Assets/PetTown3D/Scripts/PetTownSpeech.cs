using TMPro;
using UnityEngine;

// A short line of text floating over a character's head. No background panel: plain outlined text.
[DefaultExecutionOrder(300)]
public sealed class PetTownSpeech : MonoBehaviour
{
    public enum Tone { Normal, Happy, Angry, Sick, Sad }

    TextMeshPro label;
    Transform labelTransform;
    float startedAt, until, height;
    Camera cam;
    string lastText;

    public static PetTownSpeech For(GameObject actor)
    {
        if (!actor) return null;
        var speech = actor.GetComponent<PetTownSpeech>();
        if (!speech) speech = actor.AddComponent<PetTownSpeech>();
        return speech;
    }

    public static void Say(GameObject actor, string text, Tone tone = Tone.Normal, float seconds = 3.6f)
    {
        if (!actor || string.IsNullOrEmpty(text)) return;
        var speech = For(actor);
        if (speech) speech.Show(text, tone, seconds);
    }

    public bool Active { get { return label && label.gameObject.activeSelf && Time.time < until; } }

    float MeasureHeight()
    {
        float top = 0;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!r || r is ParticleSystemRenderer) continue;
            if (label && r.transform.IsChildOf(label.transform)) continue;
            top = Mathf.Max(top, r.bounds.max.y);
        }
        float h = top - transform.position.y;
        return Mathf.Clamp(h <= .1f ? 1.9f : h, .6f, 6f);
    }

    void Build()
    {
        var go = new GameObject("SpeechText");
        labelTransform = go.transform;
        labelTransform.SetParent(transform, false);
        label = go.AddComponent<TextMeshPro>();
        labelTransform = label.transform; // TextMeshPro swaps the Transform for a RectTransform
        var library = PetTownCozyLibrary.Current;
        if (library != null && library.style != null && library.style.font != null) label.font = library.style.font;
        label.fontSize = 2.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.rectTransform.sizeDelta = new Vector2(6f, 1.6f);
        label.raycastTarget = false;
        label.fontStyle = FontStyles.Bold;
        go.AddComponent<PetTownTextOutline>().Set(.28f, new Color32(20, 18, 28, 255));
        go.SetActive(false);
    }

    void Show(string text, Tone tone, float seconds)
    {
        if (!label) Build();
        if (!label) return;
        height = MeasureHeight();
        // Keep clear of the status labels other systems already hang above some heads.
        float extra = 0;
        foreach (Transform child in transform)
            if (child != labelTransform && child.gameObject.activeSelf && (child.name == "FacilityVisitStatus" || child.name == "StaffRole")) extra = .7f;
        label.text = text;
        lastText = text;
        label.color = ToneColor(tone);
        startedAt = Time.time;
        until = Time.time + Mathf.Max(1.5f, seconds + text.Length * .03f);
        labelTransform.localPosition = Vector3.up * (height + .45f + extra) / Mathf.Max(.01f, transform.lossyScale.y);
        label.gameObject.SetActive(true);
    }

    static Color ToneColor(Tone tone)
    {
        switch (tone)
        {
            case Tone.Happy: return new Color(1f, .94f, .55f);
            case Tone.Angry: return new Color(1f, .55f, .5f);
            case Tone.Sick: return new Color(.72f, .95f, .6f);
            case Tone.Sad: return new Color(.7f, .82f, 1f);
        }
        return Color.white;
    }

    void LateUpdate()
    {
        if (!label || !label.gameObject.activeSelf) return;
        if (Time.time >= until) { label.gameObject.SetActive(false); return; }
        if (!cam) cam = Camera.main;
        if (cam)
        {
            labelTransform.rotation = cam.transform.rotation;
            float s = 1f;
            if (cam.orthographic) s = Mathf.Clamp(cam.orthographicSize / 12f, .7f, 2.2f);
            else s = Mathf.Clamp(Vector3.Distance(cam.transform.position, transform.position) / 28f, .7f, 2.2f);
            Vector3 parent = transform.lossyScale;
            labelTransform.localScale = new Vector3(s / Mathf.Max(.01f, parent.x), s / Mathf.Max(.01f, parent.y), s / Mathf.Max(.01f, parent.z));
        }
        float age = Time.time - startedAt, left = until - Time.time;
        var c = label.color;
        c.a = Mathf.Clamp01(Mathf.Min(age / .2f, left / .5f));
        label.color = c;
    }

    void OnDisable() { if (label) label.gameObject.SetActive(false); }
}
