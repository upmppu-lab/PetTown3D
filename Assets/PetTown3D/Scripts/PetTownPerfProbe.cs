#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

// 개발용 성능 측정 도구: 켜 두면 모든 프로파일러 마커의 평균 시간(밀리초)과 프레임당 GC 할당량을 모은다.
// PetTownPerfProbe.Report() 로 상위 항목을 문자열로 받는다. 릴리스 빌드에는 들어가지 않는다.
public sealed class PetTownPerfProbe : MonoBehaviour
{
    sealed class Entry { public string name; public ProfilerRecorder rec; public double sum; public int frames; public double max; }
    static readonly List<Entry> entries = new List<Entry>();
    static ProfilerRecorder gcRec;
    static double gcSum; static int gcFrames; static double frameSum; static double frameMax; static int frameCount;
    static readonly int[] slowBuckets = new int[4];
    static PetTownPerfProbe instance;

    public static void Begin()
    {
        if (instance) return;
        var go = new GameObject("PetTownPerfProbe") { hideFlags = HideFlags.HideAndDontSave };
        instance = go.AddComponent<PetTownPerfProbe>();
        DontDestroyOnLoad(go);
    }

    public static void End()
    {
        if (instance) Destroy(instance.gameObject);
        instance = null;
    }

    void OnEnable()
    {
        entries.Clear();
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
            if (d.Category != ProfilerCategory.Scripts && d.Category != ProfilerCategory.Animation && d.Category != ProfilerCategory.Ai
                && d.Category != ProfilerCategory.Physics && d.Category != ProfilerCategory.Internal && d.Category != ProfilerCategory.Gui
                && d.Category != ProfilerCategory.Render) continue;
            var e = new Entry { name = d.Name };
            e.rec = ProfilerRecorder.StartNew(d.Category, d.Name, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            entries.Add(e);
        }
        gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        Reset();
    }

    void OnDisable()
    {
        foreach (var e in entries) e.rec.Dispose();
        if (gcRec.Valid) gcRec.Dispose();
    }

    void LateUpdate()
    {
        foreach (var e in entries)
        {
            if (!e.rec.Valid) continue;
            double v = e.rec.LastValue;
            e.sum += v; e.frames++;
            if (v > e.max) e.max = v;
        }
        if (gcRec.Valid) { gcSum += gcRec.LastValue; gcFrames++; }
        double dt = Time.unscaledDeltaTime * 1000.0;
        frameSum += dt; frameCount++;
        if (dt > frameMax) frameMax = dt;
        if (dt > 16.8) slowBuckets[0]++;
        if (dt > 33.4) slowBuckets[1]++;
        if (dt > 50) slowBuckets[2]++;
    }

    public static string Report(int top)
    {
        var sb = new StringBuilder();
        double avg = frameCount > 0 ? frameSum / frameCount : 0;
        sb.AppendLine("frames=" + frameCount + " avgFrame=" + avg.ToString("F1") + "ms (" + (avg > 0 ? 1000.0 / avg : 0).ToString("F0") + "fps) maxFrame=" + frameMax.ToString("F1") + "ms");
        sb.AppendLine("frames>16.7ms=" + slowBuckets[0] + " >33ms=" + slowBuckets[1] + " >50ms=" + slowBuckets[2]);
        sb.AppendLine("GC alloc/frame=" + (gcFrames > 0 ? gcSum / gcFrames / 1024.0 : 0).ToString("F1") + "KB");
        var list = new List<Entry>(entries);
        list.RemoveAll(x => x.frames == 0);
        list.Sort((a, b) => (b.sum / b.frames).CompareTo(a.sum / a.frames));
        for (int i = 0; i < list.Count && i < top; i++)
        {
            var e = list[i];
            sb.AppendLine((e.sum / e.frames / 1e6).ToString("F2") + "ms avg, max " + (e.max / 1e6).ToString("F1") + "  " + e.name);
        }
        return sb.ToString();
    }

    public static void Reset()
    {
        foreach (var e in entries) { e.sum = 0; e.frames = 0; e.max = 0; }
        gcSum = 0; gcFrames = 0; frameSum = 0; frameMax = 0; frameCount = 0;
        for (int i = 0; i < slowBuckets.Length; i++) slowBuckets[i] = 0;
    }
}
#endif
