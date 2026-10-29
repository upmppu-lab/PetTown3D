using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 게임 시작 직후(타이틀 화면)에 캐릭터를 미리 준비해서, 마을에서 처음 등장할 때 끊기지 않게 한다.
//  1) 주민/직원 모델과 동작 클립을 메모리에 붙잡아 둔다(프레임당 하나씩 나눠서 로드).
//  2) 모델을 카메라 앞에 아주 작게 한 번 그려 셰이더·텍스처·메시를 GPU에 올려 둔다(워밍업).
// 이미 불러온 에셋은 Resources 캐시를 그대로 쓰므로 게임 로직은 바꾸지 않는다.
public partial class PetTownGame
{
    static readonly string[] PreloadStaffModels =
    {
        "MasterPlayer", "MasterPlayer", "MasterPlayer",
        "MasterPlayer", "MasterPlayer", "MasterPlayer"
    };
    static readonly string[] PreloadSitClips =
    {
        "Characters/Villager_12/Villager_12_Sit_Cross_Legged", "Characters/Villager_10/Villager_10_Sit_and_Doze_Off",
        "Characters/Villager_10/Villager_10_Seated_Fist_Pump", "Characters/Staff_Doctor/Staff_Doctor_Sitting_Clap"
    };

    // 메모리에서 내려가지 않도록 참조를 들고 있는다.
    static readonly List<Object> preloadPinned = new List<Object>();
    public static bool CharactersPreloaded { get; private set; }

    IEnumerator PreloadCharactersRoutine()
    {
        if (CharactersPreloaded) yield break;
        yield return null; // 첫 화면이 먼저 나오게 한 프레임 양보
        var models = new List<string>(VillagerResources);
        models.AddRange(PreloadStaffModels);
        // RAM 4GB 미만 폰에서는 메모리를 아끼려고 앞쪽 8명만 미리 올린다(나머지는 나올 때 불러옴).
        if (Application.isMobilePlatform && SystemInfo.systemMemorySize < 4096 && models.Count > 8) models.RemoveRange(8, models.Count - 8);
        var loaded = new List<GameObject>();
        for (int i = 0; i < models.Count; i++)
        {
            ResourceRequest req = null;
            try { req = Resources.LoadAsync<GameObject>(models[i]); } catch (System.Exception e) { Debug.LogWarning("Preload: " + e.Message); }
            if (req == null) continue;
            while (!req.isDone) yield return null;
            var go = req.asset as GameObject;
            if (go == null) continue;
            preloadPinned.Add(go);
            loaded.Add(go);
            try { foreach (var c in Resources.LoadAll<AnimationClip>(models[i])) preloadPinned.Add(c); } catch (System.Exception) { }
            yield return null;
        }
        foreach (var path in PreloadSitClips)
        {
            var req = Resources.LoadAsync<AnimationClip>(path);
            while (!req.isDone) yield return null;
            if (req.asset != null) preloadPinned.Add(req.asset);
        }

        // 워밍업: 카메라 앞에 아주 작게 그려서 셰이더/텍스처를 GPU에 올린다.
        var cam = Camera.main;
        for (int tries = 0; cam == null && tries < 120; tries++) { yield return null; cam = Camera.main; }
        if (cam != null)
        {
            foreach (var prefab in loaded)
            {
                if (prefab == null) continue;
                GameObject inst = null;
                try
                {
                    inst = Instantiate(prefab);
                    inst.name = "PRELOAD_WARMUP";
                    inst.transform.position = cam.transform.position + cam.transform.forward * 4f;
                    inst.transform.localScale = Vector3.one * .02f;
                    foreach (var a in inst.GetComponentsInChildren<Animator>()) a.enabled = false;
                    foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                catch (System.Exception e) { Debug.LogWarning("Preload warmup: " + e.Message); }
                yield return null;
                yield return null;
                if (inst) Destroy(inst);
            }
        }
        CharactersPreloaded = true;
    }
}
