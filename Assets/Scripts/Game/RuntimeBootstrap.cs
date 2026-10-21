using UnityEngine;

public static class RuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (GameManager.Instance == null)
        {
            var gameManagerGO = new GameObject("GameManager");
            gameManagerGO.AddComponent<GameManager>();
        }

        if (AudioManager.Instance == null)
        {
            var audioGO = new GameObject("AudioManager");
            audioGO.AddComponent<AudioManager>();
        }

        // SimBarExperience owns the playable presentation.  The older builders are
        // kept in the project as reusable prototypes, but must not create a second
        // floor/canvas on top of the authored scene at runtime.
        if (Object.FindFirstObjectByType<SimBarExperience>() == null)
        {
            var experienceGO = new GameObject("SimBarExperience");
            experienceGO.AddComponent<SimBarExperience>();
        }
    }

}
