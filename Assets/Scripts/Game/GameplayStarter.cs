using UnityEngine;

public class GameplayStarter : MonoBehaviour
{
    [SerializeField] private bool autoStartGame = true;

    private void Awake()
    {
        // The SIMBAR live-house controller owns this scene's presentation and
        // EventSystem. Keep this legacy bootstrap for the original prototype
        // scenes, but never let it compete with the authored SIMBAR scene.
        if (FindAnyObjectByType<SimBarExperience>() != null)
            return;

        BarUIBuilder.EnsureEventSystem();
    }

    private void Start()
    {
        if (FindAnyObjectByType<SimBarExperience>() != null)
            return;

        if (!autoStartGame)
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.isOpen = true;
        }

        var uiBuilder = FindFirstObjectByType<BarUIBuilder>();
        if (uiBuilder == null)
        {
            var newUiBuilder = new GameObject("BarUIBuilder");
            uiBuilder = newUiBuilder.AddComponent<BarUIBuilder>();
        }

        uiBuilder.BuildUI();

        var sceneBuilder = FindFirstObjectByType<BarSceneBuilder>();
        if (sceneBuilder == null)
        {
            var newSceneBuilder = new GameObject("BarSceneBuilder");
            sceneBuilder = newSceneBuilder.AddComponent<BarSceneBuilder>();
        }

        sceneBuilder.BuildScene();
    }
}
