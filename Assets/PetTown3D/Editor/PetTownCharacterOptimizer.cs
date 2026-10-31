using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityMeshSimplifier;

// Converts a heavy Meshy .glb character (4096px textures, ~40k tris) into a light stand-alone prefab:
// simplified mesh, one 1024px albedo texture, URP material and its own AnimatorController + clips.
// The resulting prefab has no dependency on the original .glb, so the .glb can be moved out of Assets.
public static class PetTownCharacterOptimizer
{
    public const string OutRoot = "Assets/PetTown3D/Resources/Characters";
    // 얼굴/텍스처 이음새를 보존해서 줄이면 얼굴이 깨지지 않는다(더 많은 삼각형이 남는다).
    public static bool SeamSafe = true;
    // 게임이 쓰는 걷기/달리기/서기 동작만 남겨서 용량을 줄인다.
    public static bool LocomotionOnly = false;
    static bool IsLocomotion(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (var bad in new[] { "sit", "lie", "lay", "sleep", "jump", "leap", "penguin", "push", "kettlebell", "hip_hop", "dance", "happy", "wave", "transition", "turn", "rotate", "pivot" }) if (n.Contains(bad)) return false;
        return n.Contains("walk") || n.Contains("run") || n.Contains("idle") || n.Contains("jog");
    }

    public static string Convert(string glbPath, string outName, int texSize, float quality)
    {
        var report = new StringBuilder();
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (src == null) return outName + ": cannot load " + glbPath;
        string leaf = Path.GetFileName(outName);
        string folder = OutRoot + "/" + outName;
        if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        EnsureFolder(folder);
        outName = leaf;

        var inst = Object.Instantiate(src);
        inst.name = outName;
        int trisBefore = 0, trisAfter = 0, meshIndex = 0;

        var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null) continue;
            trisBefore += smr.sharedMesh.triangles.Length / 3;
            var mesh = Simplify(smr.sharedMesh, quality, outName + "_mesh" + meshIndex);
            AssetDatabase.CreateAsset(mesh, folder + "/" + outName + "_mesh" + meshIndex + ".asset");
            trisAfter += mesh.triangles.Length / 3;
            smr.sharedMesh = mesh;
            smr.sharedMaterials = ConvertMaterials(smr.sharedMaterials, folder, outName, texSize, meshIndex);
            smr.updateWhenOffscreen = false;
            smr.localBounds = mesh.bounds;
            meshIndex++;
        }
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            trisBefore += mf.sharedMesh.triangles.Length / 3;
            var mesh = Simplify(mf.sharedMesh, quality, outName + "_mesh" + meshIndex);
            AssetDatabase.CreateAsset(mesh, folder + "/" + outName + "_mesh" + meshIndex + ".asset");
            trisAfter += mesh.triangles.Length / 3;
            mf.sharedMesh = mesh;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr) mr.sharedMaterials = ConvertMaterials(mr.sharedMaterials, folder, outName, texSize, meshIndex);
            meshIndex++;
        }

        // Animator: build our own controller (one state per clip, state name == clip name) with copied clips.
        var animator = inst.GetComponentInChildren<Animator>(true);
        int clipCount = 0; var clipNames = new List<string>();
        if (animator != null)
        {
            var srcList = new List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(glbPath)) { var c = o as AnimationClip; if (c != null && !c.name.StartsWith("__preview__")) srcList.Add(c); }
            var srcClips = srcList.ToArray();
            var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/" + outName + ".controller");
            var sm = controller.layers[0].stateMachine;
            foreach (var clip in srcClips)
            {
                if (clip == null) continue;
                if (LocomotionOnly && !IsLocomotion(clip.name)) continue;
                var copy = Object.Instantiate(clip); copy.name = clip.name; CompressClip(copy);
                AssetDatabase.CreateAsset(copy, folder + "/" + outName + "_" + Safe(clip.name) + ".anim");
                var state = sm.AddState(outName + "_" + Safe(clip.name)); // 게임은 클립 에셋 이름으로 상태를 찾는다
                state.motion = copy;
                if (clipCount == 0) sm.defaultState = state;
                clipCount++; clipNames.Add(clip.name);
            }
            animator.runtimeAnimatorController = controller;
            if (animator.avatar != null && AssetDatabase.GetAssetPath(animator.avatar) == glbPath) animator.avatar = null;
            animator.applyRootMotion = false;
        }

        string prefabPath = folder + "/" + outName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();

        bool dependsOnGlb = false;
        foreach (var dep in AssetDatabase.GetDependencies(prefabPath, true)) if (dep == glbPath) dependsOnGlb = true;
        long bytes = 0; foreach (var f in Directory.GetFiles(folder)) if (!f.EndsWith(".meta")) bytes += new FileInfo(f).Length;
        report.Append(outName + ": tris " + trisBefore + " -> " + trisAfter + ", clips=" + clipCount + " [" + string.Join(",", clipNames) + "], " + (bytes / 1024) + " KB" + (dependsOnGlb ? ", STILL DEPENDS ON GLB" : ""));
        return report.ToString();
    }

    // Ramer-Douglas-Peucker keyframe reduction on every curve; linear tangents reproduce the kept segments exactly.
    public static void CompressClip(AnimationClip clip)
    {
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null || curve.length < 6) continue;
            string prop = b.propertyName;
            float tol = prop.Contains("Rotation") || prop.Contains("rotation") ? 0.0008f : prop.Contains("Scale") || prop.Contains("scale") ? 0.002f : 0.002f;
            var keys = curve.keys; int n = keys.Length;
            var keep = new bool[n]; keep[0] = true; keep[n - 1] = true;
            var stack = new Stack<int[]>(); stack.Push(new[] { 0, n - 1 });
            while (stack.Count > 0)
            {
                var seg = stack.Pop(); int lo = seg[0], hi = seg[1]; if (hi - lo < 2) continue;
                float worst = 0; int at = -1;
                for (int i = lo + 1; i < hi; i++)
                {
                    float u = (keys[i].time - keys[lo].time) / Mathf.Max(1e-6f, keys[hi].time - keys[lo].time);
                    float expect = Mathf.Lerp(keys[lo].value, keys[hi].value, u);
                    float dev = Mathf.Abs(keys[i].value - expect);
                    if (dev > worst) { worst = dev; at = i; }
                }
                if (worst > tol && at > 0) { keep[at] = true; stack.Push(new[] { lo, at }); stack.Push(new[] { at, hi }); }
            }
            var kept = new List<Keyframe>(); for (int i = 0; i < n; i++) if (keep[i]) kept.Add(keys[i]);
            for (int i = 0; i < kept.Count; i++)
            {
                var k = kept[i];
                float inSlope = i > 0 ? (k.value - kept[i - 1].value) / Mathf.Max(1e-6f, k.time - kept[i - 1].time) : 0f;
                float outSlope = i < kept.Count - 1 ? (kept[i + 1].value - k.value) / Mathf.Max(1e-6f, kept[i + 1].time - k.time) : 0f;
                if (i == 0) inSlope = outSlope; if (i == kept.Count - 1) outSlope = inSlope;
                k.inTangent = inSlope; k.outTangent = outSlope; kept[i] = k;
            }
            AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(kept.ToArray()));
        }
    }

    public static string CompressFolder(string folder)
    {
        long before = 0, after = 0; int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid); if (!p.EndsWith(".anim")) continue;
            before += new FileInfo(p).Length;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
            CompressClip(clip); EditorUtility.SetDirty(clip); AssetDatabase.SaveAssets();
            after += new FileInfo(p).Length; count++;
        }
        return folder + ": " + count + " clips " + (before / 1024) + " KB -> " + (after / 1024) + " KB";
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
    }

    static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        return sb.ToString();
    }

    // ---- background-ish queue: one item per editor update, progress written to a log file ----------------------
    public const string LogPath = "C:\\UnityProjects\\PetTown3D_convert_log.txt";
    static readonly Queue<string[]> queue = new Queue<string[]>();
    static bool running;
    public static void Enqueue(string glbPath, string outName, int texSize, float quality)
    {
        queue.Enqueue(new[] { glbPath, outName, texSize.ToString(), quality.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        if (!running) { running = true; EditorApplication.update += Step; }
    }
    static void Step()
    {
        if (queue.Count == 0) { running = false; EditorApplication.update -= Step; File.AppendAllText(LogPath, "QUEUE DONE\n"); return; }
        var job = queue.Dequeue();
        var started = System.DateTime.Now;
        string line;
        if (job[0] == "COMPRESS") { try { line = CompressFolder(job[1]); } catch (System.Exception e) { line = job[1] + ": COMPRESS ERROR " + e.Message; } File.AppendAllText(LogPath, line + " (" + (System.DateTime.Now - started).TotalSeconds.ToString("0") + "s)\n"); return; }
        try { line = Convert(job[0], job[1], int.Parse(job[2]), float.Parse(job[3], System.Globalization.CultureInfo.InvariantCulture)); }
        catch (System.Exception e) { line = job[1] + ": ERROR " + e.Message; }
        File.AppendAllText(LogPath, line + " (" + (System.DateTime.Now - started).TotalSeconds.ToString("0") + "s)\n");
    }

    // 'quality' is the target triangle count (e.g. 6000). Several passes are needed: one pass stalls around 1/3.
    static Mesh Simplify(Mesh source, float quality, string name)
    {
        int target = Mathf.Max(500, (int)quality);
        float extent = Mathf.Max(source.bounds.size.x, Mathf.Max(source.bounds.size.y, source.bounds.size.z));
        float factor = extent > 0f && extent < .5f ? 1.7f / extent : 1f;
        Mesh work = source;
        if (factor != 1f) { work = Object.Instantiate(source); var vs = work.vertices; for (int i = 0; i < vs.Length; i++) vs[i] *= factor; work.vertices = vs; work.RecalculateBounds(); }
        Mesh current = work;
        for (int pass = 0; pass < 6; pass++)
        {
            int tris = current.triangles.Length / 3;
            if (tris <= target * 1.1f) break;
            var simplifier = new MeshSimplifier();
            simplifier.Initialize(current);
            var opts = SimplificationOptions.Default;
            opts.PreserveBorderEdges = SeamSafe;
            opts.PreserveUVSeamEdges = SeamSafe;
            opts.PreserveUVFoldoverEdges = SeamSafe;
            opts.EnableSmartLink = true;
            opts.Agressiveness = SeamSafe ? 6 : 10;
            opts.MaxIterationCount = 1000;
            simplifier.SimplificationOptions = opts;
            simplifier.SimplifyMesh(Mathf.Max(0.15f, (float)target / tris));
            var next = simplifier.ToMesh();
            if (current != work) Object.DestroyImmediate(current);
            current = next;
            if (next.triangles.Length / 3 >= tris * 0.95f) break;
        }
        if (current == work) current = Object.Instantiate(work);
        if (factor != 1f) { var vs2 = current.vertices; for (int i = 0; i < vs2.Length; i++) vs2[i] /= factor; current.vertices = vs2; if (work != source) Object.DestroyImmediate(work); }
        current.name = name;
        current.RecalculateBounds();
        current.UploadMeshData(false);
        return current;
    }

    static Material[] ConvertMaterials(Material[] sources, string folder, string outName, int texSize, int meshIndex)
    {
        var result = new Material[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            var s = sources[i];
            Texture baseTex = null; Color tint = Color.white;
            if (s != null)
            {
                foreach (var prop in new[] { "baseColorTexture", "_BaseMap", "_MainTex" }) if (s.HasProperty(prop)) { baseTex = s.GetTexture(prop); if (baseTex) break; }
                foreach (var prop in new[] { "baseColorFactor", "_BaseColor", "_Color" }) if (s.HasProperty(prop)) { tint = s.GetColor(prop); break; }
            }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = outName + "_mat" + meshIndex + "_" + i };
            mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.2f);
            if (baseTex != null)
            {
                string texPath = folder + "/" + outName + "_albedo" + meshIndex + "_" + i + ".png";
                SaveResized(baseTex, texPath, texSize);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
            }
            else mat.SetColor("_BaseColor", tint);
            AssetDatabase.CreateAsset(mat, folder + "/" + mat.name + ".mat");
            result[i] = mat;
        }
        return result;
    }

    static void SaveResized(Texture source, string path, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.maxTextureSize = size;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.mipmapEnabled = true;
        importer.sRGBTexture = true;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }
}
