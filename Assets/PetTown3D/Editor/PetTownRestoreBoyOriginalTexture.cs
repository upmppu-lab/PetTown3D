using UnityEngine;
using UnityEditor;
using System.IO;

public static class PetTownRestoreBoyOriginalTexture
{
    private const string Root = "Assets/PetTown3D/Characters/Master";
    [MenuItem("Pet Town 3D/Restore Original Boy Texture (Safe)")]
    public static void Restore()
    {
        var go = GameObject.Find("PetTown_MasterCharacter");
        if (go == null) { EditorUtility.DisplayDialog("Pet Town", "Open the CharacterTest scene and select the existing PetTown_MasterCharacter.", "OK"); return; }
        string texPath = Root + "/OriginalTextures/Meshy_Boy_Original_BaseColor.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { EditorUtility.DisplayDialog("Pet Town", "Original boy texture missing: " + texPath, "OK"); return; }
        string matPath = Root + "/OriginalTextures/Meshy_Boy_Original_Restored.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = "Meshy_Boy_Original_Restored";
            AssetDatabase.CreateAsset(mat, matPath);
        }
        Undo.RecordObject(mat, "Assign original boy texture");
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(mat);
        int count = 0;
        foreach (string part in new[] { "Hair_01", "Face_01", "Top_01", "Bottom_01", "Mesh_0" })
        {
            Transform child = go.transform.Find(part);
            if (child == null) continue;
            var renderer = child.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null) continue;
            Undo.RecordObject(renderer, "Restore original boy material");
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0) materials = new Material[1];
            for (int i=0; i<materials.Length; i++) materials[i] = mat;
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
            count++;
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Pet Town", "Assigned the original boy GLB texture to " + count + " parts. Check appearance BEFORE saving the scene. If distorted, Ctrl+Z and report it; UV may differ.", "OK");
    }
}
