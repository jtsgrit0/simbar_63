using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (FindFirstObjectByType<GameManager>() == null)
        {
            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
        }

        if (FindFirstObjectByType<AudioManager>() == null)
        {
            var am = new GameObject("AudioManager");
            am.AddComponent<AudioManager>();
        }

        if (FindFirstObjectByType<UIManager>() == null)
        {
            var ui = new GameObject("UIManager");
            ui.AddComponent<UIManager>();
        }

        // Scene-building objects are intentionally omitted here because the cozy room layout already creates the environment.
        // UI generation remains separate and is handled by the UI builders.
#endif
    }
}
