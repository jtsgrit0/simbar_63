#if UNITY_EDITOR_WIN
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class SimBarFastGamePlayDefault
{
    private const string Reference = "Assets/Scenes/SimBar_Reference.unity";
    private const string Menu = "SimBar/Use Latest FastGame in Standard Play";
    private static string PrefKey => "SimBar.StandardPlay.FastGame." + Application.dataPath;

    static SimBarFastGamePlayDefault()
    {
        EditorApplication.playModeStateChanged += PlayState;
        EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Configure();
        EditorApplication.delayCall += Configure;
    }

    [MenuItem(Menu)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, true));
        Configure();
    }

    [MenuItem(Menu, true)]
    private static bool Validate()
    {
        UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(PrefKey, true));
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool Enabled() => !Application.isBatchMode
        && EditorPrefs.GetBool(PrefKey, true)
        && SceneManager.GetActiveScene().path == Reference;

    private static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Enabled())
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SimBarFastGamePlay.ScenePath);
            if (scene != null) EditorSceneManager.playModeStartScene = scene;
        }
        else if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == SimBarFastGamePlay.ScenePath)
        {
            EditorSceneManager.playModeStartScene = null;
        }
    }

    private static void PlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            bool armed = Enabled();
            SessionState.SetBool(SimBarFastGamePlay.ArmedKey, armed);
            if (armed) SimBarFastGamePreview.CloseOpenWindows();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(SimBarFastGamePlay.ArmedKey);
            Configure();
        }
    }
}
#endif
