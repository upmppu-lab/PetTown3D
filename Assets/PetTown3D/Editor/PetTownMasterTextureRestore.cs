using UnityEngine;
using UnityEditor;
using System.IO;

public static class PetTownMasterTextureRestore
{
    private const string Root = "Assets/PetTown3D/Resources/";
    private const string Output = "Assets/PetTown3D/Characters/Master/PetTown_Master_Textured.mat";
    private static readonly string[] Parts = { "Bottom_01", "Face_01", "Hair_01", "Mesh_0", "Top_01" };

    [MenuItem("Pet Town 3D/Restore Master Character Textures")]
    public static void Restore()
    {
        GameObject character = GameObject.Find("PetTown_MasterCharacter");
        if (character == null)
        {
            EditorUtility.DisplayDialog("Pet Town", "Open the character test scene and select a scene containing PetTown_MasterCharacter.", "OK");
            return;
        }
        string basePath = Root + "Meshy_AI_Chibi_Schoolgirl_with_biped_texture_0";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + ".png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_normal.png");
        if (albedo == null)
        {
            EditorUtility.DisplayDialog("Pet Town", "Original Meshy color texture was not found at: " + basePath + ".png", "OK");
            return;
        }
        string folder = Path.GetDirectoryName(Output).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("Pet Town", "Master folder is missing: " + folder, "OK");
            return;
        }
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(Output);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Pet Town", "No supported Lit shader found.", "OK");
                return;
            }
            mat = new Material(shader) { name = "PetTown_Master_Textured" };
            AssetDatabase.CreateAsset(mat, Output);
        }
        Undo.RecordObject(mat, "Restore Master Textures");
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (normal != null)
        {
            string normalPath = AssetDatabase.GetAssetPath(normal);
            TextureImporter ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            }
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
        }
        // Leave metallic/roughness maps untouched: Meshy roughness is not a Unity smoothness map.
        EditorUtility.SetDirty(mat);
        int count = 0;
        foreach (string part in Parts)
        {
            Transform t = FindChild(character.transform, part);
            if (t == null) { Debug.LogWarning("Pet Town: missing part " + part); continue; }
            Renderer r = t.GetComponent<Renderer>();
            if (r == null) { Debug.LogWarning("Pet Town: no renderer on " + part); continue; }
            Undo.RecordObject(r, "Restore Master Textures");
            Material[] current = r.sharedMaterials;
            if (current == null || current.Length == 0) current = new Material[1];
            for (int i = 0; i < current.Length; i++) current[i] = mat;
            r.sharedMaterials = current;
            EditorUtility.SetDirty(r);
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            count++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManagerMarkDirty();
        Selection.activeGameObject = character;
        EditorUtility.DisplayDialog("Pet Town", "Texture assigned to " + count + "/5 parts. Check the Scene view in Shaded mode, then save the scene (Ctrl+S).", "OK");
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void EditorSceneManagerMarkDirty()
    {
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
