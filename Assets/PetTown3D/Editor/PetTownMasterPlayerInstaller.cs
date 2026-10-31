#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

// "Pet Town 3D > Install Master Player" 메뉴를 한 번 실행하면
// Assets/PetTown3D/Characters/Master/PetTown_MasterCharacter.fbx 를
// Resources/MasterPlayer.prefab 으로 패키징한다.
// (텍스처 입힌 머티리얼 + MasterCharacter_Test 애니메이터를 붙인 상태로 저장)
// 이후 Play를 누르면 PetTownGame.RebuildHero()가 이 프리팹을 찾아서
// 옛날 Meshy 스쿨걸 대신 이 마스터 캐릭터를 실제 플레이어로 스폰한다.
public static class PetTownMasterPlayerInstaller
{
    const string FbxPath = "Assets/PetTown3D/Characters/Master/PetTown_MasterCharacter.fbx";
    const string MatPath = "Assets/PetTown3D/Characters/Master/PetTown_Master_Textured.mat";
    const string ControllerPath = "Assets/PetTown3D/Characters/Master/MasterCharacter_Test.controller";
    const string PrefabPath = "Assets/PetTown3D/Resources/MasterPlayer.prefab";

    [MenuItem("Pet Town 3D/Install Master Player")]
    public static void Install()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (source == null)
        {
            EditorUtility.DisplayDialog("Pet Town 3D", "PetTown_MasterCharacter.fbx를 찾을 수 없습니다:\n" + FbxPath, "OK");
            return;
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

        var obj = (GameObject)PrefabUtility.InstantiatePrefab(source);
        if (obj == null) obj = Object.Instantiate(source);
        obj.name = "MasterPlayer";

        int rendererCount = 0;
        if (mat != null)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                if (mats.Length == 0) mats = new[] { mat };
                renderer.sharedMaterials = mats;
                rendererCount++;
            }
        }

        // 혹시 FBX에 콜라이더가 딸려왔다면 제거 (PetTownPlayerControls가 자체 CapsuleCast로 충돌 처리함).
        foreach (var c in obj.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var animator = obj.GetComponent<Animator>();
        if (animator == null) animator = obj.AddComponent<Animator>();
        if (controller != null) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        bool prefabExisted = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        PrefabUtility.SaveAsPrefabAsset(obj, PrefabPath);
        Object.DestroyImmediate(obj);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Pet Town 3D",
            (prefabExisted ? "MasterPlayer.prefab 갱신 완료.\n" : "MasterPlayer.prefab 생성 완료.\n") +
            "렌더러: " + rendererCount + "개, 애니메이터 컨트롤러: " + (controller != null ? "연결됨" : "없음(수동 연결 필요)") +
            "\n\nPlay를 누르면 새 캐릭터가 자동으로 플레이어로 스폰됩니다.", "OK");
    }
}
#endif
