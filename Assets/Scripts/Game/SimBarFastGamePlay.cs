#if UNITY_EDITOR_WIN
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SimBarFastGamePlay : MonoBehaviour
{
    public const string ScenePath = "Assets/Scenes/SimBar_FastGamePlay.unity";
    public const string ArmedKey = "SimBar.FastGamePlay.Armed";
    private SimBarFastGameSession session;
    private bool hasFocus = true;

    public static bool IsSelected => SessionState.GetBool(ArmedKey, false)
        || SceneManager.GetActiveScene().path == ScenePath;

    public static void Boot()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        var root = new GameObject("SIMBAR FastGame Player");
        root.AddComponent<SimBarFastGamePlay>();
    }

    private void Awake()
    {
        var camera = gameObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
        camera.cullingMask = 0;
        camera.useOcclusionCulling = false;
        session = new SimBarFastGameSession();
        session.StartPreview();
    }

    private void Update() => session?.Tick();

    private void OnGUI()
    {
        if (session == null) return;
        GUI.depth = int.MinValue;
        session.Draw(new Rect(0, 0, Screen.width, Screen.height));
        if (hasFocus) session.HandleInput(Event.current, SimBarFastGameSession.Fit(new Rect(0, 0, Screen.width, Screen.height)));
    }

    private void OnApplicationFocus(bool value)
    {
        hasFocus = value;
        if (!value) session?.LoseFocus();
    }

    private void OnDestroy() => session?.ClosePreview();
}
#endif
