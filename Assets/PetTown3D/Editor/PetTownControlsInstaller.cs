#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PetTownControlsInstaller
{
    [MenuItem("Pet Town 3D/Install Master Player Controls")]
    static void Install()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Pet Town", "Stop Play mode first.", "OK"); return; }
        GameObject master = GameObject.Find("PetTown_MasterCharacter");
        if (master == null) { EditorUtility.DisplayDialog("Pet Town", "Open PetTown3D_CharacterTest scene first. PetTown_MasterCharacter not found.", "OK"); return; }
        if (master.GetComponent<PetTownPlayerControls>() == null)
            Undo.AddComponent<PetTownPlayerControls>(master);
        var animator = master.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
            Debug.LogWarning("Pet Town: Master Animator Controller is missing. Assign MasterCharacter_Test.");
        EditorUtility.SetDirty(master);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(master.scene);
        Selection.activeGameObject = master;
        EditorUtility.DisplayDialog("Pet Town", "Player controls added. Save the scene (Ctrl+S).", "OK");
    }
}
#endif
