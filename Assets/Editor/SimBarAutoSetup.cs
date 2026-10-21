using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SimBarAutoSetup
{
    private const string ScenePath = "Assets/Scenes/SimBar_AutoScene.unity";

    static SimBarAutoSetup()
    {
        // The authored reference venue has its own startup. Never switch away
        // from the user's active scene on every script/domain reload.
        if (!SceneExists("Assets/Scenes/SimBar_Reference.unity"))
            EditorApplication.delayCall += EnsureSetup;
    }

    [MenuItem("SimBar/Setup Default Scene")]
    public static void SetupDefaultScene()
    {
        EnsureSetup();
    }

    private static void EnsureSetup()
    {
        EditorApplication.delayCall -= EnsureSetup;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!SceneExists(ScenePath))
        {
            CreateSceneIfMissing();
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            var gm = new GameObject("GameManager");
            gameManager = gm.AddComponent<GameManager>();
        }

        var audioManager = Object.FindFirstObjectByType<AudioManager>();
        if (audioManager == null)
        {
            var am = new GameObject("AudioManager");
            audioManager = am.AddComponent<AudioManager>();
        }

        var uiManager = Object.FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            var um = new GameObject("UIManager");
            uiManager = um.AddComponent<UIManager>();
        }

        // 자동으로 Main Camera 생성
        var mainCamera = Object.FindFirstObjectByType<Camera>();
        if (mainCamera == null)
        {
            var cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            mainCamera.transform.position = new Vector3(0, 10, 0);
            mainCamera.transform.eulerAngles = new Vector3(90, 0, 0);
            // Skybox 설정 추가
            mainCamera.clearFlags = CameraClearFlags.Skybox;
        }

        // 자동으로 Directional Light 생성
        // var dirLight = Object.FindObjectOfType<Light>();
        // if (dirLight == null || dirLight.type != LightType.Directional)
        // {
        //     var lightObj = new GameObject("Directional Light");
        //     dirLight = lightObj.AddComponent<Light>();
        //     dirLight.type = LightType.Directional;
        //     dirLight.transform.eulerAngles = new Vector3(50, -30, 0);
        //     dirLight.intensity = 1.0f;
        //     dirLight.color = Color.white;
        // }

        EditorSceneManager.SaveScene(scene);
        if (uiManager == null)
        {
            var ui = new GameObject("UIManager");
            uiManager = ui.AddComponent<UIManager>();
        }

        var barSceneBuilder = Object.FindFirstObjectByType<BarSceneBuilder>();
        if (barSceneBuilder == null)
        {
            var builder = new GameObject("BarSceneBuilder");
            barSceneBuilder = builder.AddComponent<BarSceneBuilder>();
        }

        var barUIBuilder = Object.FindFirstObjectByType<BarUIBuilder>();
        if (barUIBuilder == null)
        {
            var uiBuilder = new GameObject("BarUIBuilder");
            barUIBuilder = uiBuilder.AddComponent<BarUIBuilder>();
        }

        var gameplayStarter = Object.FindFirstObjectByType<GameplayStarter>();
        if (gameplayStarter == null)
        {
            var starter = new GameObject("GameplayStarter");
            gameplayStarter = starter.AddComponent<GameplayStarter>();
        }

        if (gameManager != null)
            gameManager.isOpen = true;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool SceneExists(string path)
    {
        return System.IO.File.Exists(path);
    }

    private static void CreateSceneIfMissing()
    {
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(newScene, ScenePath);
    }
}
