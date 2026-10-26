using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Signals web performance checks only after the playable presentation has rendered.</summary>
public sealed class SimBarWebReady : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SimBar_ReportGameReady();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        new GameObject("SimBar Web Ready").AddComponent<SimBarWebReady>();
    }

    private IEnumerator Start()
    {
        while (FindAnyObjectByType<SimBarExperience>() == null || !SplashScreen.isFinished)
            yield return null;
        yield return new WaitForEndOfFrame();
        SimBar_ReportGameReady();
        Destroy(gameObject);
    }
#endif
}
