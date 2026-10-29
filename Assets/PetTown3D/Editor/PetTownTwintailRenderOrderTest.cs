#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PetTownTwintailRenderOrderTest
{
    const string TestName = "PetTown_Twintail_Visual_Test";
    static readonly Dictionary<Renderer, Material[]> saved = new Dictionary<Renderer, Material[]>();

    [MenuItem("PetTown/헤어 테스트/트윈테일 렌더 순서 테스트 (Play 모드)")]
    static void Apply()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Play 모드에서 실행하세요."); return; }
        var root = GameObject.Find(TestName);
        if (!root) { Debug.LogWarning("먼저 '새 트윈테일 장착 (Play 모드)'을 실행하세요."); return; }
        RestoreMaterials();
        int count = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var old = renderer.sharedMaterials;
            saved[renderer] = old;
            var copies = new Material[old.Length];
            for (int i = 0; i < old.Length; i++)
            {
                if (!old[i]) continue;
                var mat = new Material(old[i]);
                mat.name = old[i].name + " (Twintail Queue Test)";
                mat.renderQueue = 3100;
                copies[i] = mat;
                count++;
            }
            renderer.sharedMaterials = copies;
        }
        Debug.Log("[Twintail] 렌더 큐 3100 적용: " + count + "개 머티리얼. 깊이 검사는 유지됩니다. Play 종료 시 초기화됩니다.");
    }

    [MenuItem("PetTown/헤어 테스트/트윈테일 렌더 순서 원복 (Play 모드)")]
    static void RestoreMaterials()
    {
        foreach (var pair in saved)
        {
            if (!pair.Key) continue;
            var current = pair.Key.sharedMaterials;
            pair.Key.sharedMaterials = pair.Value;
            foreach (var mat in current)
                if (mat && mat.name.EndsWith(" (Twintail Queue Test)")) Object.DestroyImmediate(mat);
        }
        saved.Clear();
        Debug.Log("[Twintail] 렌더 순서 테스트 원복 완료.");
    }
}
#endif
