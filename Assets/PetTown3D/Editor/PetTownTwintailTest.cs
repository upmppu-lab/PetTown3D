#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PetTownTwintailTest
{
    const string Path = "Assets/PetTown3D/Resources/HairStyles/Hair_04_Twintail_Test.glb";
    const string TestName = "PetTown_Twintail_Visual_Test";

    [MenuItem("PetTown/헤어 테스트/새 트윈테일 장착 (Play 모드)")]
    static void TryHair()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("먼저 Play 모드를 실행하세요."); return; }
        var master = GameObject.Find("PetTown_MasterCharacter");
        if (!master) { Debug.LogError("PetTown_MasterCharacter를 찾지 못했습니다."); return; }
        var hair = Find(master.transform, "Hair_01");
        if (!hair) { Debug.LogError("Hair_01을 찾지 못했습니다."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (!prefab) { Debug.LogError("GLB가 Unity 모델로 임포트되지 않았습니다. Console에서 임포트 오류를 확인하세요."); return; }
        var existing = GameObject.Find(TestName);
        if (existing) Object.DestroyImmediate(existing);
        var skin = hair.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Transform bone = hair.parent;
        if (skin)
        {
            foreach (var b in skin.bones) if (b && b.name.ToLowerInvariant().Contains("head")) { bone = b; break; }
            if (bone == hair.parent && skin.rootBone) bone = skin.rootBone;
        }
        var test = (GameObject)Object.Instantiate(prefab, bone);
        test.name = TestName;
        test.transform.localPosition = Vector3.zero;
        test.transform.localRotation = Quaternion.identity;
        test.transform.localScale = Vector3.one;
        var original = hair.GetComponentsInChildren<Renderer>(true);
        var incoming = test.GetComponentsInChildren<Renderer>(true);
        if (!BoundsOf(original, out var target) || !BoundsOf(incoming, out var source) || source.size.x < 0.00001f)
        {
            Object.DestroyImmediate(test);
            Debug.LogError("헤어 메시 바운드를 읽지 못했습니다."); return;
        }
        float ratio = target.size.x / source.size.x;
        test.transform.localScale = Vector3.one * ratio;
        if (BoundsOf(incoming, out source)) test.transform.position += target.center - source.center;
        foreach (var r in original) if (r) r.enabled = false;
        Debug.Log($"[Twintail test] 폭 기준 배율 {ratio:F5}; 정면/옆면을 확인하세요. 이 테스트는 자동 관통 보정이 아닙니다.");
        Selection.activeGameObject = test;
    }

    [MenuItem("PetTown/헤어 테스트/원본 헤어 복원 (Play 모드)")]
    static void Restore()
    {
        if (!EditorApplication.isPlaying) return;
        var existing = GameObject.Find(TestName);
        if (existing) Object.DestroyImmediate(existing);
        var master = GameObject.Find("PetTown_MasterCharacter");
        if (!master) return;
        var hair = Find(master.transform, "Hair_01");
        if (hair) foreach (var r in hair.GetComponentsInChildren<Renderer>(true)) if (r) r.enabled = true;
    }
    static bool BoundsOf(Renderer[] rs, out Bounds bounds)
    {
        bounds = new Bounds(); bool any = false;
        foreach (var r in rs)
        {
            if (!r || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }
    static Transform Find(Transform p, string n)
    {
        if (p.name == n) return p;
        foreach (Transform c in p) { var v = Find(c,n); if (v) return v; }
        return null;
    }
}
#endif
