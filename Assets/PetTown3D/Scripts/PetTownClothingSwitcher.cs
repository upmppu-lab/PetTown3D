using UnityEngine;
// Experimental rigid clothing try-on. Does NOT modify FBX, skeleton or animation.
// A standalone GLB has no matching skin weights: it follows one body bone, not each limb.
public sealed class PetTownClothingSwitcher : MonoBehaviour
{
    public static PetTownClothingSwitcher Instance { get; private set; }
    public int TopSelected { get; private set; }
    public int BottomSelected { get; private set; }
    public string Status { get; private set; } = "의상 로딩 대기 중";
    readonly GameObject[] worn = new GameObject[2];
    readonly Transform[] originals = new Transform[2];
    readonly Transform[] anchors = new Transform[2];
    readonly Vector3[] basePositions = new Vector3[2];
    readonly Vector3[] baseScales = new Vector3[2];
    readonly Quaternion[] baseRotations = new Quaternion[2];
    readonly Vector3[] offsets = new Vector3[2];
    readonly Vector3[] angles = new Vector3[2];
    readonly float[] sizes = {1f,1f};
    Transform master;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() { if (FindFirstObjectByType<PetTownClothingSwitcher>()==null) { var g=new GameObject("PetTown_Clothing_Switcher"); DontDestroyOnLoad(g); g.AddComponent<PetTownClothingSwitcher>(); } }
    void Awake() { Instance=this; for(int p=0;p<2;p++) { string k=Key(p); offsets[p]=new Vector3(PlayerPrefs.GetFloat(k+"X"),PlayerPrefs.GetFloat(k+"Y"),PlayerPrefs.GetFloat(k+"Z")); angles[p]=new Vector3(PlayerPrefs.GetFloat(k+"RX"),PlayerPrefs.GetFloat(k+"RY"),PlayerPrefs.GetFloat(k+"RZ")); sizes[p]=PlayerPrefs.GetFloat(k+"S",1f); } TopSelected=0; BottomSelected=0; }
    void OnDestroy() { if(Instance==this)Instance=null; }
    static string Key(int p) { return p==0?"PetTownTop02_":"PetTownBottom02_"; }
    static Transform Find(Transform root,string name) { if(root.name==name)return root; foreach(Transform t in root) { var f=Find(t,name);if(f!=null)return f; } return null; }
    void Update() { if(master!=null)return; var g=GameObject.Find("PetTown_MasterCharacter"); if(g==null)return; master=g.transform; originals[0]=Find(master,"Top_01"); originals[1]=Find(master,"Bottom_01"); var bone=Find(master,"target_character"); anchors[0]=Find(master,"Chest"); if(anchors[0]==null)anchors[0]=Find(master,"Spine"); anchors[1]=Find(master,"Hips"); for(int p=0;p<2;p++)if(anchors[p]==null)anchors[p]=bone!=null?bone:master; int t=TopSelected,b=BottomSelected;TopSelected=BottomSelected=0; Select(0,t);Select(1,b); }
    public int GetSelected(int p) { return p==0?TopSelected:BottomSelected; }
    public Vector3 GetOffset(int p) { return offsets[p]; }
    public Vector3 GetAngles(int p) { return angles[p]; }
    public float GetSize(int p) { return sizes[p]; }
    public void Select(int p,int index)
    {
        if(p<0||p>1||index<0||index>1)return;
        if(index != 0) { Status="경량 버전에는 기본 의상만 포함됩니다"; return; }
        if(master==null||originals[p]==null)return;
        if(worn[p]!=null) { worn[p].SetActive(false);Destroy(worn[p]);worn[p]=null; }
        foreach(var r in originals[p].GetComponentsInChildren<Renderer>(true))r.enabled=true;
        if(index==0) { SetSelected(p,0); Save(); return; }
        var prefab=Resources.Load<GameObject>(p==0?"ClothingStyles/Top_02_Hoodie":"ClothingStyles/Bottom_02_Denim");
        if(prefab==null) { Status="GLB 로드 실패: glTFast 임포트 확인";SetSelected(p,0);return; }
        var g=Instantiate(prefab,master);g.name=p==0?"PetTown_Selected_Top_02":"PetTown_Selected_Bottom_02";g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.identity;g.transform.localScale=Vector3.one;
        var renderers=g.GetComponentsInChildren<Renderer>(true);var old=originals[p].GetComponentsInChildren<Renderer>(true);
        Bounds target=new Bounds();bool found=false;
        foreach(var r in old) { if(!found){target=r.bounds;found=true;}else target.Encapsulate(r.bounds); }
        Bounds source=new Bounds();bool has=false;
        foreach(var r in renderers){if(!has){source=r.bounds;has=true;}else source.Encapsulate(r.bounds);}
        if(!found||!has||source.size.x<.00001f){Destroy(g);Status="의상 메시를 읽지 못했습니다";return;}
        // Keep uniform scaling and original geometry; never deform mesh vertices.
        float scale=Mathf.Clamp(target.size.x/source.size.x,.001f,100f);
        g.transform.localScale=Vector3.one*scale;
        has=false;foreach(var r in renderers){if(!has){source=r.bounds;has=true;}else source.Encapsulate(r.bounds);}
        g.transform.position+=target.center-source.center;
        worn[p]=g;basePositions[p]=g.transform.localPosition;baseScales[p]=g.transform.localScale;baseRotations[p]=g.transform.localRotation;
        foreach(var r in old)r.enabled=false;
        SetSelected(p,1);Apply(p);
        // Do not guess sleeve/leg axes from the bounding box: it severely
        // distorts garments that were generated in a different rest pose.
        string rigStatus;
        bool rigged=PetTownAutoSkin.TryBind(g,originals[p],out rigStatus);
        Save();
        Status=rigged ? "기존 본 연결: "+rigStatus : "리깅 실패: "+rigStatus;
    }
    void SetSelected(int p,int i){if(p==0)TopSelected=i;else BottomSelected=i;}
    void Apply(int p){if(worn[p]==null)return;var t=worn[p].transform;t.localPosition=basePositions[p]+offsets[p];t.localScale=baseScales[p]*sizes[p];t.localRotation=baseRotations[p]*Quaternion.Euler(angles[p]);}
    public void Adjust(int p,int axis,float delta){if(p<0||p>1||worn[p]==null)return;if(axis==3)sizes[p]=Mathf.Clamp(sizes[p]+delta,.01f,20f);else if(axis>=4&&axis<=6){var a=angles[p];a[axis-4]=Mathf.Clamp(a[axis-4]+delta,-180f,180f);angles[p]=a;}else if(axis>=0&&axis<=2){var a=offsets[p];a[axis]=Mathf.Clamp(a[axis]+delta,-2f,2f);offsets[p]=a;}Apply(p);Save();}
    public void Reset(int p){offsets[p]=Vector3.zero;angles[p]=Vector3.zero;sizes[p]=1f;Apply(p);Save();}
    public void Save(){PlayerPrefs.SetInt("PetTownOutfitTop",TopSelected);PlayerPrefs.SetInt("PetTownOutfitBottom",BottomSelected);for(int p=0;p<2;p++){string k=Key(p);PlayerPrefs.SetFloat(k+"X",offsets[p].x);PlayerPrefs.SetFloat(k+"Y",offsets[p].y);PlayerPrefs.SetFloat(k+"Z",offsets[p].z);PlayerPrefs.SetFloat(k+"RX",angles[p].x);PlayerPrefs.SetFloat(k+"RY",angles[p].y);PlayerPrefs.SetFloat(k+"RZ",angles[p].z);PlayerPrefs.SetFloat(k+"S",sizes[p]);}PlayerPrefs.Save();}
}
