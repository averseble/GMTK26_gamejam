#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// В Play Mode всегда стартуем с BootScene, даже если открыт Menu/Battle.
/// </summary>
[InitializeOnLoad]
public static class PlayFromBoot
{
    const string BootScenePath = "Assets/HighNoonGame/Scenes/BootScene.unity";

    static PlayFromBoot()
    {
        var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        if (boot == null)
        {
            Debug.LogWarning($"PlayFromBoot: scene not found at {BootScenePath}");
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        EditorSceneManager.playModeStartScene = boot;
    }
}
#endif
