using UnityEngine;

// Additive hair selection. Does not edit the master character mesh, rig, movement or animation.
public sealed class PetTownHairSwitcher : MonoBehaviour
{
    public static PetTownHairSwitcher Instance { get; private set; }
    public int Selected { get; private set; }
    // Extra size correction for imported GLB hair. Adjustable in Inspector.
    [Range(0.1f, 1f)] public float importedHairSizeFactor = 0.25f;
    public string Status { get; private set; } = "헤어 모델 준비 중";
    Transform master, originalHair, headBone;
    GameObject replacement;
    public Transform ActiveHair { get { return replacement != null ? replacement.transform : null; } }
    readonly Vector3[] offsets = { Vector3.zero, new Vector3(0f,0.02f,0f), new Vector3(0f,0.02f,0f), new Vector3(0f,0.02f,0f), new Vector3(0f,0.02f,0f) };
    readonly float[] scales = { 1f, 1f, 1f, 1f, 1f };
    readonly Vector3[] rotations = { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
    public Vector3 GetRotation(int index) { return rotations[Mathf.Clamp(index,0,4)]; }
    Vector3 baseLocalPosition;
    Vector3 baseLocalScale;
    Quaternion baseLocalRotation;
    public Vector3 GetOffset(int index) { return offsets[Mathf.Clamp(index,0,4)]; }
    public float GetScale(int index) { return scales[Mathf.Clamp(index,0,4)]; }
    public void Adjust(int axis, float delta)
    {
        if (Selected == 0 || replacement == null) return;
        if (axis == 3) scales[Selected] = Mathf.Clamp(scales[Selected] + delta, .1f, 20f);
        else if (axis >= 4 && axis <= 6) { var r = rotations[Selected]; r[axis-4] = Mathf.Clamp(r[axis-4] + delta, -180f, 180f); rotations[Selected] = r; }
        else if (axis >= 0 && axis <= 2) { var o = offsets[Selected]; o[axis] = Mathf.Clamp(o[axis] + delta, -.5f, .5f); offsets[Selected] = o; }
        ApplyCalibration(); Save();
    }
    // Reset only the selected hairstyle calibration; preserve other game saves.
    public void ResetSelectedCalibration()
    {
        if (Selected <= 0) return;
        offsets[Selected] = new Vector3(0f, .02f, 0f);
        scales[Selected] = 1f;
        rotations[Selected] = Vector3.zero;
        ApplyCalibration();
        Save();
    }
    void ApplyCalibration()
    {
        if (replacement == null) return;
        replacement.transform.localPosition = baseLocalPosition + offsets[Selected];
        replacement.transform.localScale = baseLocalScale * scales[Selected];
        replacement.transform.localRotation = baseLocalRotation * Quaternion.Euler(rotations[Selected]);
    }
    void LoadCalibration()
    {
        for(int i=1;i<=4;i++)
        {
            offsets[i] = new Vector3(PlayerPrefs.GetFloat("PetTownHair_"+i+"_X",0f),PlayerPrefs.GetFloat("PetTownHair_"+i+"_Y",.02f),PlayerPrefs.GetFloat("PetTownHair_"+i+"_Z",0f));
            scales[i] = PlayerPrefs.GetFloat("PetTownHair_"+i+"_Scale",1f);
            rotations[i] = new Vector3(PlayerPrefs.GetFloat("PetTownHair_"+i+"_RX",0f),PlayerPrefs.GetFloat("PetTownHair_"+i+"_RY",0f),PlayerPrefs.GetFloat("PetTownHair_"+i+"_RZ",0f));
        }
    }
    Renderer[] originalRenderers;
    const string SaveKey = "PetTown3D_SelectedHair_v1";
    static readonly string[] Paths = { "", "HairStyles/Hair_02_Ponytail", "HairStyles/Hair_03_Bob", "HairStyles/Hair_04_Twintail_Test", "HairStyles/Hair_05_Long" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<PetTownHairSwitcher>() != null) return;
        var obj = new GameObject("PetTown_Hair_Switcher");
        DontDestroyOnLoad(obj);
        obj.AddComponent<PetTownHairSwitcher>();
    }
    void Awake() { Instance = this; Selected = 0; LoadCalibration(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Update()
    {
        if (master != null) return;
        var obj = GameObject.Find("PetTown_MasterCharacter");
        if (obj == null) return;
        master = obj.transform;
        originalHair = Find(master, "Hair_01");
        if (originalHair == null) { Status = "Hair_01을 찾지 못했습니다"; return; }
        originalRenderers = originalHair.GetComponentsInChildren<Renderer>(true);
        var skin = originalHair.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin != null)
        {
            foreach (var bone in skin.bones)
                if (bone != null && bone.name.ToLowerInvariant().Contains("head")) { headBone = bone; break; }
            if (headBone == null) headBone = skin.rootBone;
        }
        if (headBone == null) headBone = originalHair.parent;
        int requested = Selected;
        Selected = 0;
        Select(requested);
    }
    public void Select(int index)
    {
        if (index != 0) { Status = "경량 버전에는 기본 헤어만 포함됩니다"; return; }
        if (originalHair == null) { Selected = index; Status = "캐릭터 로딩 대기 중"; return; }
        if (replacement != null) { replacement.SetActive(false); Destroy(replacement); replacement = null; }
        foreach (var r in originalRenderers) if (r != null) r.enabled = true;
        if (index == 0) { Selected = 0; Status = "원본 헤어 적용"; Save(); return; }
        var prefab = Resources.Load<GameObject>(Paths[index]);
        if (prefab == null)
        {
            Status = "GLB 모델을 로드할 수 없습니다. glTFast 임포트를 확인하세요.";
            Selected = 0;
            return;
        }
        replacement = Instantiate(prefab, headBone);
        replacement.name = "PetTown_Selected_Hair_0" + (index + 1);
        replacement.transform.localPosition = Vector3.zero;
        replacement.transform.localRotation = Quaternion.identity;
        replacement.transform.localScale = Vector3.one;
        // Match the replacement's rendered bounds to the existing hair in world space.
        Bounds target = new Bounds(); bool targetSet = false;
        foreach (var r in originalRenderers)
        {
            if (r == null) continue;
            if (!targetSet) { target = r.bounds; targetSet = true; } else target.Encapsulate(r.bounds);
        }
        var newRenderers = replacement.GetComponentsInChildren<Renderer>(true);
        Bounds source = new Bounds(); bool sourceSet = false;
        foreach (var r in newRenderers)
        {
            if (!sourceSet) { source = r.bounds; sourceSet = true; } else source.Encapsulate(r.bounds);
        }
        if (!sourceSet || !targetSet || source.size.x < .0001f)
        {
            Destroy(replacement); replacement = null;
            Status = "헤어 메시를 읽을 수 없습니다";
            Selected = 0; return;
        }
        // Older standalone hairstyles were reduced to 25% of the reference width.
        // Keep the successfully fitted twintail (04) unchanged; give 02/03 a proper baseline.
        float perStyleFactor = index >= 3 ? importedHairSizeFactor : 1f;
        float scale = Mathf.Clamp(target.size.x / source.size.x, .01f, 100f) * perStyleFactor;
        replacement.transform.localScale *= scale;
        // Recompute bounds after scaling; move hair center to original hair center.
        sourceSet = false;
        foreach (var r in newRenderers)
        {
            if (!sourceSet) { source = r.bounds; sourceSet = true; } else source.Encapsulate(r.bounds);
        }
        replacement.transform.position += target.center - source.center;
        foreach (var r in originalRenderers) if (r != null) r.enabled = false;
        Selected = index;
        baseLocalPosition = replacement.transform.localPosition;
        baseLocalScale = replacement.transform.localScale;
        baseLocalRotation = replacement.transform.localRotation;
        ApplyCalibration();
        Status = "헤어 " + (index + 1) + " 적용됨 (위치 확인 필요)";
        Save();
    }
    public void SaveCurrentCalibration() { Save(); Status = "헤어 설정 저장 완료 (다음 실행에도 유지)"; }
    void Save()
    {
        PlayerPrefs.SetInt(SaveKey, Selected);
        for(int i=1;i<=4;i++)
        {
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_X", offsets[i].x);
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_Y", offsets[i].y);
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_Z", offsets[i].z);
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_Scale", scales[i]);
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_RX", rotations[i].x);
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_RY", rotations[i].y);
            PlayerPrefs.SetFloat("PetTownHair_"+i+"_RZ", rotations[i].z);
        }
        PlayerPrefs.Save();
    }
    static Transform Find(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
