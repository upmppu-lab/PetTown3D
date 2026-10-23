using System.Collections.Generic;
using UnityEngine;

// Runtime-only color customization. Never changes imported models or material assets.
public sealed class PetTownCharacterAppearance : MonoBehaviour
{
    public static PetTownCharacterAppearance Instance { get; private set; }
    static readonly string[] Parts = {"Hair_01", "Top_01", "Bottom_01"};
    static readonly Color[][] Palettes = {
        new [] {Color.white, new Color(.43f,.23f,.12f),new Color(.14f,.12f,.12f),new Color(.78f,.54f,.26f),new Color(.66f,.29f,.27f),new Color(.45f,.36f,.65f)},
        new [] {Color.white, new Color(.25f,.55f,.87f),new Color(.92f,.43f,.51f),new Color(.34f,.69f,.49f),new Color(.95f,.79f,.33f),new Color(.35f,.34f,.38f)},
        new [] {Color.white, new Color(.24f,.37f,.64f),new Color(.18f,.2f,.25f),new Color(.64f,.53f,.38f),new Color(.54f,.38f,.62f),new Color(.86f,.82f,.76f)}
    };
    readonly Dictionary<Renderer, MaterialPropertyBlock> originals = new Dictionary<Renderer, MaterialPropertyBlock>();
    readonly int[] chosen = new int[3];
    Transform character;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<PetTownCharacterAppearance>() != null) return;
        var obj = new GameObject("PetTown_Character_Appearance");
        obj.AddComponent<PetTownCharacterAppearance>();
        DontDestroyOnLoad(obj);
    }
    void Awake() { Instance=this; }
    void OnDestroy() { if (Instance==this) Instance=null; }
    void Update()
    {
        if (character != null) return;
        var root=GameObject.Find("PetTown_MasterCharacter");
        if (root==null) return;
        character=root.transform;
        for(int i=0;i<3;i++) Apply(i,chosen[i]);
    }
    public bool IsReady { get { return character!=null; } }
    public int GetSelected(int part) { return part>=0&&part<3 ? chosen[part] : 0; }
    public Color GetSwatch(int part,int index) { return Palettes[Mathf.Clamp(part,0,2)][Mathf.Clamp(index,0,5)]; }
    public void Apply(int part,int index)
    {
        if(part<0||part>=3||index<0||index>=6)return;
        chosen[part]=index;
        if(character==null)return;
        var target=FindRecursive(character,Parts[part]);
        if(target==null)return;
        foreach(var r in target.GetComponentsInChildren<Renderer>(true))
        {
            if(!originals.ContainsKey(r))
            {
                var initial=new MaterialPropertyBlock();
                r.GetPropertyBlock(initial);
                originals[r]=initial;
            }
            if(index==0)
            {
                r.SetPropertyBlock(originals[r]);
                continue;
            }
            var block=new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color tint=Palettes[part][index];
            // Unity Standard and URP Lit both accept color properties on compatible shaders.
            block.SetColor("_BaseColor",tint);
            block.SetColor("_Color",tint);
            r.SetPropertyBlock(block);
        }
    }
    static Transform FindRecursive(Transform root,string name)
    {
        if(root.name==name)return root;
        foreach(Transform child in root)
        {
            var found=FindRecursive(child,name);
            if(found!=null)return found;
        }
        return null;
    }
}
