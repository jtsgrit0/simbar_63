using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks the f34743e interface after SimBarHudSkin has applied its final layout.</summary>
public static class SimBarOriginalUIVerification
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SimBar/Validation/Check Running Original UI")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null)
        {
            report.failures.Add("Enter Play Mode in SimBar_Reference before checking the original UI.");
            return report.ToString();
        }
        Canvas canvas = Read<Canvas>(host, "canvas");
        if (canvas == null)
        {
            report.failures.Add("The running experience has a UI canvas.");
            return report.ToString();
        }
        var nodes = new Dictionary<string, Transform>();
        foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true))
            if (!nodes.ContainsKey(node.name)) nodes.Add(node.name, node);
        var snapshot = new Snapshot(host, canvas);
        try
        {
            Canvas.ForceUpdateCanvases();
            report.Check(canvas.GetComponent<SimBarHudSkin>() != null, "The original runtime HUD skin is applied.");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            report.Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                && Near(scaler.referenceResolution, new Vector2(1920f, 1080f)) && Near(scaler.matchWidthOrHeight, 0.5f),
                "Canvas uses the original 1920x1080 reference and 0.5 width/height match.");
            Camera camera = Camera.main;
            report.Check(camera != null && Near(camera.rect.position, Vector2.zero) && Near(camera.rect.size, Vector2.one),
                "The main camera occupies the full screen.");
            report.Check(canvas.GetComponentsInChildren<SimBarReadability>(true).Length == 0,
                "No readability layout component is attached to the canvas.");
            bool addedControls = false;
            foreach (string name in nodes.Keys)
                if (name.IndexOf("Compact", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Text Size", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("TextSize", StringComparison.OrdinalIgnoreCase) >= 0) addedControls = true;
            report.Check(!addedControls, "Compact menu and text-size controls are absent.");

            CheckRect(report, nodes, "CASH Card", Vector2.one, new Vector2(-493f, -47f), new Vector2(208f, 80f));
            CheckRect(report, nodes, "FANS Card", Vector2.one, new Vector2(-284f, -47f), new Vector2(200f, 80f));
            CheckRect(report, nodes, "TIME Card", Vector2.one, new Vector2(-97f, -47f), new Vector2(174f, 80f));
            CheckRect(report, nodes, "Order Border", new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(848f, 150f));
            CheckRect(report, nodes, "Order Console", Vector2.one * 0.5f, Vector2.zero, new Vector2(838f, 140f));
            CheckRect(report, nodes, "Playlist Border", new Vector2(1f, 0f), new Vector2(-187f, 383f), new Vector2(358f, 308f));
            CheckRect(report, nodes, "Playlist", Vector2.one * 0.5f, Vector2.zero, new Vector2(346f, 296f));
            CheckRect(report, nodes, "Venue Panel", Vector2.zero, new Vector2(133f, 139f), new Vector2(242f, 252f));
            CheckRect(report, nodes, "Mini Map", Vector2.one * 0.5f, new Vector2(0f, -3f), new Vector2(216f, 200f));
            CheckRect(report, nodes, "Mood Panel", new Vector2(1f, 0f), new Vector2(-187f, 112f), new Vector2(358f, 210f));
            CheckRect(report, nodes, "Staff Management Card", Vector2.zero, new Vector2(133f, 292f), new Vector2(242f, 44f));
            CheckRect(report, nodes, "Journey Night HUD", new Vector2(0f, 1f), new Vector2(365f, -117f), new Vector2(510f, 210f));
            CheckFonts(report, nodes, "Stat heading/value fonts", new[] { "CASH", "CASH Value", "FANS", "FANS Value", "TIME", "TIME Value" }, new[] { 26, 28, 26, 28, 26, 28 });
            CheckFonts(report, nodes, "Playlist fonts", new[] { "Playlist Title", "Now Playing" }, new[] { 31, 12 });
            CheckFonts(report, nodes, "Order console fonts", new[] { "Order Header", "Current Order", "Queue", "Service Help" }, new[] { 13, 24, 13, 11 });
            CheckFonts(report, nodes, "Venue and mood fonts", new[] { "Venue Label", "Map Caption", "Staff Management", "GUEST SATISFACTION", "PERFORMANCE ENERGY" }, new[] { 15, 12, 19, 17, 17 });
            CheckFonts(report, nodes, "Journey HUD fonts", new[] { "Journey Night Title", "Journey Countdown", "Journey Score", "Journey Combo", "Journey Rank" }, new[] { 19, 31, 24, 20, 14 });

            Transform order = Node(nodes, "Order Console");
            Button[] drinks = order != null ? order.GetComponentsInChildren<Button>(true) : new Button[0];
            bool originalDrinks = drinks.Length == 3;
            for (int i = 0; i < drinks.Length; i++)
            {
                Text label = drinks[i].GetComponentInChildren<Text>(true);
                originalDrinks &= RectMatches(drinks[i].GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                    new Vector2(-269f + 269f * i, 36f), new Vector2(253f, 43f)) && label != null && label.fontSize == 17;
            }
            report.Check(originalDrinks, "Three original drink buttons retain their positions, dimensions and 17-point labels.");

            CheckRect(report, nodes, "Language Settings Panel", Vector2.one * 0.5f, Vector2.zero, new Vector2(670f, 420f));
            CheckLabel(report, nodes, "Settings Title", new Vector2(0f, 151f), new Vector2(600f, 50f), 31);
            CheckLabel(report, nodes, "Language Heading", new Vector2(0f, 79f), new Vector2(600f, 36f), 22);
            CheckLabel(report, nodes, "Language Save Note", new Vector2(0f, -66f), new Vector2(618f, 45f), 17);
            CheckButton(report, nodes, "Language Korean Button", new Vector2(-148f, 11f), new Vector2(278f, 66f), 25);
            CheckButton(report, nodes, "Language English Button", new Vector2(148f, 11f), new Vector2(278f, 66f), 25);
            CheckButton(report, nodes, "Language Settings Close Button", new Vector2(0f, -143f), new Vector2(400f, 53f), 22);

            VerifySettingsCallbacks(report, host, nodes);
            report.measurements.Add("Baseline: f34743e after SimBarHudSkin; screen " + Screen.width + "x" + Screen.height + ".");
        }
        catch (Exception exception)
        {
            report.failures.Add("Original UI verification threw: " + exception.GetBaseException().Message);
        }
        finally
        {
            try { snapshot.Restore(host); }
            catch (Exception exception) { report.failures.Add("Could not restore UI state: " + exception.GetBaseException().Message); }
        }
        report.Check(snapshot.Matches(host), "Language preference, pause state and pre-existing overlays are restored.");
        return report.ToString();
    }

    private static void VerifySettingsCallbacks(SimBarRuntimeVerification.Report report, SimBarExperience host, Dictionary<string, Transform> nodes)
    {
        Button korean = Read<Button>(host, "settingsKorean");
        Button english = Read<Button>(host, "settingsEnglish");
        Transform closeNode = Node(nodes, "Language Settings Close Button");
        Transform entryNode = Node(nodes, "Journey Settings Button");
        Button close = closeNode != null ? closeNode.GetComponent<Button>() : null;
        Button entry = entryNode != null ? entryNode.GetComponent<Button>() : null;
        GameObject overlay = Read<GameObject>(host, "settingsOverlay");
        report.Check(korean != null && english != null && close != null && entry != null && overlay != null,
            "Settings entries and original language/close callbacks exist.");
        if (korean == null || english == null || close == null || entry == null || overlay == null) return;
        overlay.SetActive(false);
        Write(host, "paused", false);
        entry.onClick.Invoke();
        report.Check(overlay.activeSelf && Read<bool>(host, "paused"), "The settings entry opens the language modal and pauses play.");
        korean.onClick.Invoke();
        report.Check(SimBarLocalization.IsKorean && PlayerPrefs.GetString(SimBarLocalization.LanguageKey) == "ko"
            && Node(nodes, "Language Heading").GetComponent<Text>().text == "언어", "The Korean button updates and saves Korean immediately.");
        english.onClick.Invoke();
        report.Check(!SimBarLocalization.IsKorean && PlayerPrefs.GetString(SimBarLocalization.LanguageKey) == "en"
            && Node(nodes, "Language Heading").GetComponent<Text>().text == "LANGUAGE", "The English button updates and saves English immediately.");
        close.onClick.Invoke();
        report.Check(!overlay.activeSelf && !Read<bool>(host, "paused"), "Closing settings resumes a previously running game.");
        Write(host, "paused", true);
        entry.onClick.Invoke();
        close.onClick.Invoke();
        report.Check(!overlay.activeSelf && Read<bool>(host, "paused"), "Closing settings preserves a pre-existing pause.");
        entry.onClick.Invoke();
        Call(host, "TogglePause");
        report.Check(!overlay.activeSelf && Read<bool>(host, "paused"), "P closes settings while preserving the earlier pause.");
    }

    private static void CheckRect(SimBarRuntimeVerification.Report report, Dictionary<string, Transform> nodes, string name, Vector2 anchor, Vector2 position, Vector2 size)
        => report.Check(RectMatches(Node(nodes, name) as RectTransform, anchor, position, size), name + " retains its original anchor, position, dimensions and scale.");

    private static void CheckLabel(SimBarRuntimeVerification.Report report, Dictionary<string, Transform> nodes, string name, Vector2 position, Vector2 size, int font)
    {
        Transform node = Node(nodes, name);
        Text label = node != null ? node.GetComponent<Text>() : null;
        report.Check(RectMatches(node as RectTransform, Vector2.one * 0.5f, position, size) && label != null && label.fontSize == font,
            name + " retains its original geometry and font.");
    }

    private static void CheckButton(SimBarRuntimeVerification.Report report, Dictionary<string, Transform> nodes, string name, Vector2 position, Vector2 size, int font)
    {
        Transform node = Node(nodes, name);
        Button button = node != null ? node.GetComponent<Button>() : null;
        Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        report.Check(RectMatches(node as RectTransform, Vector2.one * 0.5f, position, size) && label != null && label.fontSize == font,
            name + " retains its original button geometry and font.");
    }

    private static void CheckFonts(SimBarRuntimeVerification.Report report, Dictionary<string, Transform> nodes, string description, string[] names, int[] sizes)
    {
        bool matches = true;
        for (int i = 0; i < names.Length; i++)
        {
            Transform node = Node(nodes, names[i]);
            Text label = node != null ? node.GetComponent<Text>() : null;
            matches &= label != null && label.fontSize == sizes[i] && !label.resizeTextForBestFit;
        }
        report.Check(matches, description + " match the original final HUD values without automatic fitting.");
    }

    private static bool RectMatches(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        => rect != null && Near(rect.anchorMin, anchor) && Near(rect.anchorMax, anchor)
            && Near(rect.pivot, Vector2.one * 0.5f) && Near(rect.anchoredPosition, position) && Near(rect.sizeDelta, size)
            && Vector3.Distance(rect.localScale, Vector3.one) < 0.01f;
    private static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 0.1f;
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
    private static Transform Node(Dictionary<string, Transform> nodes, string name) => nodes.TryGetValue(name, out Transform node) ? node : null;
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Fields).Invoke(target, null);

    private sealed class Snapshot
    {
        private readonly bool language, hadPreference;
        private readonly string preference;
        private readonly Dictionary<string, object> fields = new Dictionary<string, object>();
        private readonly List<GameObject> overlays = new List<GameObject>();
        private readonly List<bool> active = new List<bool>();
        private readonly List<Transform> siblings = new List<Transform>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<string> contents = new List<string>();
        private readonly List<Color> colors = new List<Color>();

        public Snapshot(SimBarExperience host, Canvas canvas)
        {
            language = SimBarLocalization.IsKorean;
            hadPreference = PlayerPrefs.HasKey(SimBarLocalization.LanguageKey);
            preference = PlayerPrefs.GetString(SimBarLocalization.LanguageKey, "");
            foreach (string name in new[] { "paused", "settingsWasPaused", "juiGuideShown", "juiGuideShownKorean", "toastTimer" })
                fields[name] = Read<object>(host, name);
            foreach (Transform child in canvas.transform)
            {
                siblings.Add(child);
                overlays.Add(child.gameObject);
                active.Add(child.gameObject.activeSelf);
            }
            foreach (Text label in canvas.GetComponentsInChildren<Text>(true))
            {
                labels.Add(label);
                contents.Add(label.text);
                colors.Add(label.color);
            }
        }

        public void Restore(SimBarExperience host)
        {
            SimBarLocalization.SetLanguage(language);
            Call(host, "RefreshLanguageUI");
            foreach (var field in fields) Write(host, field.Key, field.Value);
            for (int i = 0; i < overlays.Count; i++) if (overlays[i] != null) overlays[i].SetActive(active[i]);
            for (int i = 0; i < siblings.Count; i++) if (siblings[i] != null) siblings[i].SetSiblingIndex(i);
            for (int i = 0; i < labels.Count; i++)
                if (labels[i] != null) { labels[i].text = contents[i]; labels[i].color = colors[i]; }
            if (hadPreference) PlayerPrefs.SetString(SimBarLocalization.LanguageKey, preference);
            else PlayerPrefs.DeleteKey(SimBarLocalization.LanguageKey);
            PlayerPrefs.Save();
        }

        public bool Matches(SimBarExperience host)
        {
            if (SimBarLocalization.IsKorean != language || PlayerPrefs.HasKey(SimBarLocalization.LanguageKey) != hadPreference
                || PlayerPrefs.GetString(SimBarLocalization.LanguageKey, "") != preference) return false;
            foreach (var field in fields) if (!Equals(field.Value, Read<object>(host, field.Key))) return false;
            for (int i = 0; i < overlays.Count; i++) if (overlays[i] != null && overlays[i].activeSelf != active[i]) return false;
            return true;
        }
    }
}
