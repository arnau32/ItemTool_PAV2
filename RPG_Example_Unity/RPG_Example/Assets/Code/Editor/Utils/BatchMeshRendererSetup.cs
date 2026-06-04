using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BatchMeshRendererSetup
{
    // Set to true to skip MeshRenderers whose GameObject is not already static.
    private const bool ONLY_STATIC_OBJECTS = true;

    [MenuItem("Tools/Batch MeshRenderer Setup/Contribute GI + Cast Shadows (Scene)")]
    private static void ApplyToScene()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int count = 0;

        foreach (var r in renderers)
        {
            var go = r.gameObject;

            if (ONLY_STATIC_OBJECTS && !go.isStatic)
                continue;

            Undo.RecordObject(r, "Batch MeshRenderer Setup");
            Undo.RecordObject(go, "Batch MeshRenderer Setup");

            r.shadowCastingMode  = ShadowCastingMode.On;
            r.staticShadowCaster = true;
            r.receiveShadows     = true;

            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);

            EditorUtility.SetDirty(r);
            EditorUtility.SetDirty(go);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[BatchMeshRendererSetup] Applied to {count} MeshRenderers.");
    }

    [MenuItem("Tools/Batch MeshRenderer Setup/Contribute GI + Cast Shadows (Selection)")]
    private static void ApplyToSelection()
    {
        var selection = Selection.gameObjects;
        if (selection.Length == 0)
        {
            Debug.LogWarning("[BatchMeshRendererSetup] Nothing selected.");
            return;
        }

        int count = 0;

        foreach (var go in selection)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

            foreach (var r in renderers)
            {
                Undo.RecordObject(r, "Batch MeshRenderer Setup");
                Undo.RecordObject(r.gameObject, "Batch MeshRenderer Setup");

                r.shadowCastingMode  = ShadowCastingMode.On;
                r.staticShadowCaster = true;
                r.receiveShadows     = true;

                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ContributeGI);

                EditorUtility.SetDirty(r);
                EditorUtility.SetDirty(r.gameObject);
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[BatchMeshRendererSetup] Applied to {count} MeshRenderers in selection.");
    }
}
