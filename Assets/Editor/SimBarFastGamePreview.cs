#if UNITY_EDITOR_WIN
using UnityEditor;
using UnityEngine;

public sealed class SimBarFastGamePreview : EditorWindow
{
    private SimBarFastGameSession session;

    [MenuItem("SimBar/Play Latest FastGame in Unity")]
    public static void Open()
    {
        var window = GetWindow<SimBarFastGamePreview>();
        window.titleContent = new GUIContent("SIMBAR FastGame");
        window.minSize = new Vector2(480f, 300f);
        window.Show();
        if (!window.session.IsRunning) window.session.StartPreview();
    }

    [MenuItem("SimBar/Play Latest FastGame in Unity", true)]
    private static bool Validate() => !EditorApplication.isPlayingOrWillChangePlaymode;

    internal static void CloseOpenWindows()
    {
        foreach (var preview in Resources.FindObjectsOfTypeAll<SimBarFastGamePreview>()) preview.Close();
    }

    private void OnEnable()
    {
        session = new SimBarFastGameSession();
        wantsMouseMove = true;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        session?.ClosePreview();
    }

    private void OnEditorUpdate()
    {
        if (session != null && session.Tick()) Repaint();
    }

    private void OnGUI()
    {
        if (session == null) return;
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label(session.Version == null ? "FastGame release" : "FastGame " + session.Version, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(session.IsRunning ? "Restart" : "Start", EditorStyles.toolbarButton)) session.StartPreview();
        EditorGUILayout.EndHorizontal();
        Rect area = new Rect(0, 22, position.width, Mathf.Max(1, position.height - 22));
        session.Draw(area);
        session.HandleInput(Event.current, SimBarFastGameSession.Fit(area));
    }

    private void OnLostFocus() => session?.LoseFocus();
}
#endif
