#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class ForceLoadingBootstrapScene
{
    private const string LoadingScenePath = "Assets/Scenes/Bootstarp.unity";

    static ForceLoadingBootstrapScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (EditorSceneManager.GetActiveScene().path != LoadingScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(LoadingScenePath);
                }
                else
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }
    }
}
#endif