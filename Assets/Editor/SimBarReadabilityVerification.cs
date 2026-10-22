using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks the live text-size controls and current Game View without changing a night or save.</summary>
public static class SimBarReadabilityVerification
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("SimBar/Validation/Check Running Readability")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        SimBarExperience host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null)
        {
            report.failures.Add("Enter Play Mode before checking readability.");
            return report.ToString();
        }
        Canvas canvas = Read<Canvas>(host, "canvas");
        SimBarReadability readability = canvas != null ? canvas.GetComponent<SimBarReadability>() : null;
        GameObject settings = Read<GameObject>(host, "settingsOverlay");
        if (readability == null || settings == null)
        {
            report.failures.Add("The live canvas must have its readability component and settings controls.");
            return report.ToString();
        }
        if (host.IsPourInProgress || Read<bool>(host, "opMenuOpen") || settings.activeSelf)
        {
            report.failures.Add("Finish the active pour and close management/settings before checking readability.");
            return report.ToString();
        }
        var snapshot = new Snapshot(host);
        try
        {
            Call(host, "OpenLanguageSettings");
            report.Check(settings.activeSelf && Read<bool>(host, "paused"), "Settings opens and pauses the night while text size is changed.");
            string[] names = { "Text Size Normal Button", "Text Size Large Button", "Text Size Extra Large Button" };
            var buttons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++) buttons[i] = FindButton(settings.transform, names[i]);

            PlayerPrefs.DeleteKey(SimBarReadability.TextSizeKey);
            ResetPreferenceLoad();
            report.Check(SimBarReadability.SizeMode == 1, "A new preference defaults to the Large text size.");
            foreach (bool korean in new[] { false, true })
            {
                SimBarLocalization.SetLanguage(korean);
                for (int mode = 0; mode < buttons.Length; mode++)
                {
                    Button button = buttons[mode];
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
                    report.Check(button.isActiveAndEnabled && button.interactable && button.GetComponent<Image>().raycastTarget,
                        names[mode] + " remains an enabled pointer target.");
                    button.onClick.Invoke();
                    readability.RefreshLayout();
                    Canvas.ForceUpdateCanvases();
                    string context = (korean ? "Korean" : "English") + " mode " + mode;
                    report.Check(SimBarReadability.SizeMode == mode && PlayerPrefs.HasKey(SimBarReadability.TextSizeKey)
                        && PlayerPrefs.GetInt(SimBarReadability.TextSizeKey, -1) == mode,
                        context + ": the real size-button callback applies and persists the selected size.");
                    ResetPreferenceLoad();
                    report.Check(SimBarReadability.SizeMode == mode, context + ": the selected size reloads from PlayerPrefs.");
                    CheckSelection(report, buttons, mode, context);
                    report.Check(EventSystem.current == null || EventSystem.current.currentSelectedGameObject == button.gameObject,
                        context + ": changing text size preserves keyboard/controller focus.");
                    CheckSizeLabels(report, settings.transform, buttons, korean);
                    CheckActiveText(report, canvas, readability, mode, context);
                    CheckModalTextFit(report, settings.transform, context);
                    CheckStableRefresh(report, canvas, readability, context);
                }
            }
            FindButton(settings.transform, "Language Settings Close Button").onClick.Invoke();
            report.Check(!settings.activeSelf && Read<bool>(host, "paused") == snapshot.Paused,
                "Closing text-size settings restores the incoming pause state.");
            report.Check(snapshot.GameplayMatches(host), "Text size and language changes preserve the live night and gameplay save.");
            CheckPlayableLayouts(report, host, canvas, readability);
            report.measurements.Add("Game View " + Screen.width + "x" + Screen.height + "; compact=" + readability.IsCompact
                + "; canvas pixels per CSS unit=" + readability.PixelsPerCssUnit.ToString("F3")
                + ". Checked both languages and all three sizes at this actual viewport.");
            report.measurements.Add("Real size callbacks, keyboard focus, saved preference reload, separate HUD/modal font minimums, settings fit, and real playable HUD checks in both languages/all sizes. Gameplay checks measure camera-visible scene area, continuous clear space, HUD bounds/separation, 44px mobile touch targets and topmost pointer reachability. Original state and preferences restored.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            try { snapshot.Restore(host, readability); }
            catch (Exception error) { report.failures.Add("Restore failed: " + error.GetBaseException()); }
        }
        return report.ToString();
    }

    private static void CheckSelection(SimBarRuntimeVerification.Report report, Button[] buttons, int mode, string context)
    {
        Color selected = buttons[mode].GetComponent<Image>().color;
        bool distinct = true;
        foreach (Button button in buttons) if (button != buttons[mode]) distinct &= button.GetComponent<Image>().color != selected;
        report.Check(distinct, context + ": the current text size has a distinct selected highlight.");
    }

    private static void CheckSizeLabels(SimBarRuntimeVerification.Report report, Transform settings, Button[] buttons, bool korean)
    {
        string[] expected = korean ? new[] { "기본", "크게", "아주 크게" } : new[] { "Normal", "Large", "Extra large" };
        report.Check(FindText(settings, "Text Size Heading").text == (korean ? "글자 크기" : "TEXT SIZE"), "The text-size heading uses the selected language.");
        for (int i = 0; i < buttons.Length; i++)
            report.Check(buttons[i].GetComponentInChildren<Text>(true).text == expected[i], buttons[i].name + " uses the selected language.");
    }

    private static void CheckActiveText(SimBarRuntimeVerification.Report report, Canvas canvas, SimBarReadability readability, int mode, string context)
    {
        float minimum = mode == 0 ? 14f : mode == 1 ? 16f : 18f;
        var tooSmall = new List<string>();
        var bestFit = new List<string>();
        int active = 0;
        foreach (Text text in canvas.GetComponentsInChildren<Text>(false))
        {
            if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || text.color.a <= 0.01f) continue;
            active++;
            float css = readability.EffectiveFontCssSize(text);
            float required = minimum + (IsModalText(text) ? 2f : 0f);
            if (float.IsNaN(css) || float.IsInfinity(css) || css + 0.25f < required) tooSmall.Add(text.name + "=" + css.ToString("F1") + "<" + required);
            if (text.resizeTextForBestFit) bestFit.Add(text.name);
        }
        report.Check(active > 0 && tooSmall.Count == 0, context + ": all " + active + " active labels reach " + minimum + " HUD / " + (minimum + 2f) + " modal CSS px. " + Summarize(tooSmall));
        report.Check(bestFit.Count == 0, context + ": active labels do not shrink with automatic best-fit. " + Summarize(bestFit));
    }

    private static bool IsModalText(Text text)
    {
        for (Transform node = text.transform; node != null; node = node.parent)
            if (node.name == "Journey Lobby Panel" || node.name == "Journey Guide Panel" || node.name == "Journey Results Panel"
                || node.name == "Language Settings Panel" || node.name == "Operations Modal" || node.name == "Pause Panel") return true;
        return false;
    }

    private static void CheckModalTextFit(SimBarRuntimeVerification.Report report, Transform settings, string context)
    {
        var overflow = new List<string>();
        foreach (Text text in settings.GetComponentsInChildren<Text>(false))
        {
            if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text)) continue;
            Rect rect = text.rectTransform.rect;
            bool widthFits = text.horizontalOverflow == HorizontalWrapMode.Wrap || text.preferredWidth <= rect.width + 2f;
            if (!widthFits || text.preferredHeight > rect.height + 2f) overflow.Add(text.transform.parent.name + "/" + text.name);
        }
        report.Check(overflow.Count == 0, context + ": modal labels fit their rectangles without shrinking. " + Summarize(overflow));
    }

    private static void CheckPrimaryPanels(SimBarRuntimeVerification.Report report, Canvas canvas, string context)
    {
        string[] names = { "Order Border", "Timing Pour", "Playlist Border", "Venue Panel", "Staff Management Card", "Operations UPGRADES", "Operations GUEST INFO", "Operations EVENTS", "Day and Profit Card", "Mood Panel", "Journey Night HUD", "Compact Journey Summary", "Compact Menu Button", "Operations Live Status", "CASH Card", "FANS Card", "TIME Card" };
        var panels = new List<RectTransform>();
        foreach (string name in names)
        {
            Transform node = FindNode(canvas.transform, name);
            // Compact layouts deliberately hide secondary panels.
            if (node != null && node.gameObject.activeInHierarchy && node is RectTransform) panels.Add((RectTransform)node);
        }
        var overlaps = new List<string>();
        var offscreen = new List<string>();
        Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
        foreach (RectTransform panel in panels)
        {
            Rect rect = ScreenRect(panel);
            if (!Within(rect, screen, 2f)) offscreen.Add(panel.name);
        }
        for (int i = 0; i < panels.Count; i++)
        for (int j = i + 1; j < panels.Count; j++)
        {
            Rect a = ScreenRect(panels[i]), b = ScreenRect(panels[j]);
            if (Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 1f
                && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 1f) overlaps.Add(panels[i].name + "/" + panels[j].name);
        }
        report.Check(overlaps.Count == 0, context + ": visible primary HUD panels do not overlap. " + Summarize(overlaps));
        report.Check(offscreen.Count == 0, context + ": primary HUD panels stay entirely inside the actual viewport. " + Summarize(offscreen));
    }

    private static void CheckStableRefresh(SimBarRuntimeVerification.Report report, Canvas canvas, SimBarReadability readability, string context)
    {
        var sizes = new Dictionary<Text, int>();
        foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) sizes[text] = text.fontSize;
        for (int i = 0; i < 3; i++) readability.RefreshLayout();
        bool stable = true;
        foreach (var size in sizes) if (size.Key != null && size.Key.fontSize != size.Value) stable = false;
        report.Check(stable, context + ": repeated layout refreshes never inflate or shrink font sizes.");
    }

    private static void CheckPlayableLayouts(SimBarRuntimeVerification.Report report, SimBarExperience host, Canvas canvas, SimBarReadability readability)
    {
        GameObject pour = Read<GameObject>(host, "pourPanel");
        bool wasActive = pour.activeSelf;
        try
        {
            // Exercise the real Playing presentation, rather than hidden HUD behind the lobby/settings.
            // This synchronous check starts no pour, advances no timer and writes no gameplay save.
            FieldInfo phase = typeof(SimBarExperience).GetField("journeyPhase", InstanceFlags);
            phase.SetValue(host, Enum.Parse(phase.FieldType, "Playing"));
            Write(host, "journeyInitialized", true);
            Write(host, "journeyValidationMode", false);
            Write(Read<object>(host, "journey"), "inProgress", true);
            Write(host, "paused", false);
            Read<GameObject>(host, "pausePanel").SetActive(false);
            Read<Text>(host, "toastText").transform.parent.gameObject.SetActive(false);
            foreach (bool korean in new[] { false, true })
            {
                SimBarLocalization.SetLanguage(korean);
                for (int mode = 0; mode < 3; mode++)
                {
                    SimBarReadability.SetSizeMode(mode);
                    pour.SetActive(false);
                    Call(host, "RefreshUI");
                    readability.RefreshLayout();
                    Call(readability, "LateUpdate");
                    Canvas.ForceUpdateCanvases();
                    string context = "Playable " + (korean ? "Korean" : "English") + " mode " + mode;
                    report.Check(host.ReadabilityPlaying && !AnyBlockingOverlay(canvas), context + ": checks run on the visible playable floor, outside every modal.");
                    CheckActiveText(report, canvas, readability, mode, context);
                    CheckGameplayTextFit(report, canvas, context);
                    CheckPrimaryPanels(report, canvas, context);
                    CheckGameplayTargets(report, canvas, readability, context);
                    Rect normalScene = CheckSceneVisibility(report, canvas, readability, 0.45f, context);
                    CheckCompactMenu(report, canvas, readability, context);
                    CheckPauseLayout(report, host, canvas, readability, mode, context + " pause");
                    if (!readability.IsCompact) continue;
                    pour.SetActive(true);
                    readability.RefreshLayout();
                    Call(readability, "LateUpdate");
                    Canvas.ForceUpdateCanvases();
                    CheckGameplayTextFit(report, canvas, context + " timing");
                    CheckPrimaryPanels(report, canvas, context + " timing");
                    CheckGameplayTargets(report, canvas, readability, context + " timing");
                    Rect timingScene = CheckSceneVisibility(report, canvas, readability, 0.35f, context + " timing");
                    report.Check(timingScene.height + 2f >= normalScene.height, context + ": timing replaces the order dock without shrinking the camera's scene band.");
                    Transform order = FindNode(canvas.transform, "Order Border");
                    report.Check(order == null || !order.gameObject.activeInHierarchy, context + ": the ordinary order dock is hidden while the timing dock is visible.");
                }
            }
        }
        finally
        {
            pour.SetActive(wasActive);
            readability.RefreshLayout();
        }
    }

    private static bool AnyBlockingOverlay(Canvas canvas)
    {
        foreach (string name in new[] { "Journey Lobby", "Journey Guide", "Journey Results", "Language Settings", "Operations Modal Overlay", "Pause Panel", "Compact Menu Panel" })
        {
            Transform node = FindNode(canvas.transform, name);
            if (node != null && node.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    private static void CheckPauseLayout(SimBarRuntimeVerification.Report report, SimBarExperience host, Canvas canvas, SimBarReadability readability, int mode, string context)
    {
        GameObject pause = Read<GameObject>(host, "pausePanel");
        bool wasActive = pause.activeSelf, wasPaused = Read<bool>(host, "paused");
        try
        {
            // Inspect the pause UI directly; never invoke Resume, Save or Settings.
            Write(host, "paused", true);
            pause.SetActive(true);
            readability.RefreshLayout();
            Call(readability, "LateUpdate");
            Canvas.ForceUpdateCanvases();
            report.Check(Within(ScreenRect(pause.GetComponent<RectTransform>()), new Rect(0f, 0f, Screen.width, Screen.height), 2f),
                context + ": the pause panel fits the actual viewport.");
            CheckButtons(report, canvas, readability, pause.GetComponentsInChildren<Button>(false), context);
            CheckModalTextFit(report, pause.transform, context);
            CheckActiveText(report, canvas, readability, mode, context);
        }
        finally
        {
            pause.SetActive(wasActive);
            Write(host, "paused", wasPaused);
            readability.RefreshLayout();
            Call(readability, "LateUpdate");
        }
    }

    private static void CheckGameplayTextFit(SimBarRuntimeVerification.Report report, Canvas canvas, string context)
    {
        var clipped = new List<string>();
        foreach (Text text in canvas.GetComponentsInChildren<Text>(false))
        {
            if (!text.isActiveAndEnabled || text.color.a <= 0.01f || string.IsNullOrWhiteSpace(text.text) || IsModalText(text)) continue;
            Rect rect = text.rectTransform.rect;
            bool widthFits = text.horizontalOverflow == HorizontalWrapMode.Wrap || text.preferredWidth <= rect.width + 2f;
            if (!widthFits || text.preferredHeight > rect.height + 2f)
                clipped.Add(text.name + "=" + text.preferredHeight.ToString("F0") + "/" + rect.height.ToString("F0"));
        }
        report.Check(clipped.Count == 0, context + ": visible HUD text fits without clipping or overflowing its rectangle. " + Summarize(clipped));
    }

    private static void CheckCompactMenu(SimBarRuntimeVerification.Report report, Canvas canvas, SimBarReadability readability, string context)
    {
        if (!readability.IsCompact) return;
        Button toggle = FindButton(canvas.transform, "Compact Menu Button");
        Transform menu = FindNode(canvas.transform, "Compact Menu Panel");
        toggle.onClick.Invoke();
        readability.RefreshLayout();
        Canvas.ForceUpdateCanvases();
        report.Check(menu != null && menu.gameObject.activeInHierarchy, context + ": the real menu callback opens on-demand management controls.");
        if (menu != null && menu.gameObject.activeInHierarchy)
        {
            CheckButtons(report, canvas, readability, menu.GetComponentsInChildren<Button>(false), context + " menu");
            FindButton(canvas.transform, "Close Compact Menu Button").onClick.Invoke();
            readability.RefreshLayout();
            report.Check(!menu.gameObject.activeInHierarchy, context + ": the real close callback restores the unobstructed floor.");
        }
    }

    private static void CheckGameplayTargets(SimBarRuntimeVerification.Report report, Canvas canvas, SimBarReadability readability, string context)
        => CheckButtons(report, canvas, readability, canvas.GetComponentsInChildren<Button>(false), context);

    private static void CheckButtons(SimBarRuntimeVerification.Report report, Canvas canvas, SimBarReadability readability, Button[] buttons, string context)
    {
        bool mobile = readability.CssViewport.x <= 900f || readability.CssViewport.y <= 520f;
        float ratio = Screen.width / readability.CssViewport.x;
        Rect viewport = new Rect(0f, 0f, Screen.width, Screen.height);
        var undersized = new List<string>();
        var inaccessible = new List<string>();
        var offscreen = new List<string>();
        var overlaps = new List<string>();
        var active = new List<Button>();
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        foreach (Button button in buttons)
        {
            if (!button.isActiveAndEnabled || !button.interactable) continue;
            active.Add(button);
            Rect rect = ScreenRect(button.GetComponent<RectTransform>());
            if (!Within(rect, viewport, 2f)) offscreen.Add(button.name);
            if (mobile && (rect.width / ratio < 43.5f || rect.height / ratio < 43.5f)) undersized.Add(button.name + "=" + (rect.width / ratio).ToString("F0") + "x" + (rect.height / ratio).ToString("F0"));
            var hits = new List<RaycastResult>();
            if (EventSystem.current != null && raycaster != null)
                raycaster.Raycast(new PointerEventData(EventSystem.current) { position = rect.center }, hits);
            bool reachable = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
            Graphic graphic = button.targetGraphic;
            if (!reachable && graphic != null && graphic.depth < 0)
            {
                // Newly enabled drawers have no render depth until the next frame.
                reachable = Within(rect, viewport, 2f) && graphic.raycastTarget && graphic.Raycast(rect.center, null);
                foreach (Graphic blocker in canvas.GetComponentsInChildren<Graphic>(false))
                    if (reachable && blocker != graphic && blocker.depth >= 0 && blocker.raycastTarget && blocker.color.a > 0.1f
                        && !blocker.transform.IsChildOf(button.transform) && !button.transform.IsChildOf(blocker.transform)
                        && DrawnAfter(blocker.transform, button.transform)
                        && ScreenRect(blocker.rectTransform).Contains(rect.center) && blocker.Raycast(rect.center, null)) reachable = false;
            }
            if (!reachable) inaccessible.Add(button.name);
        }
        for (int i = 0; i < active.Count; i++)
        for (int j = i + 1; j < active.Count; j++)
            if (Overlaps(ScreenRect(active[i].GetComponent<RectTransform>()), ScreenRect(active[j].GetComponent<RectTransform>()), 1f)) overlaps.Add(active[i].name + "/" + active[j].name);
        report.Check(active.Count > 0 && inaccessible.Count == 0, context + ": every enabled control is the topmost pointer target at its center. " + Summarize(inaccessible));
        report.Check(offscreen.Count == 0 && overlaps.Count == 0, context + ": enabled touch controls stay on screen and do not overlap. " + Summarize(offscreen) + " " + Summarize(overlaps));
        if (mobile) report.Check(undersized.Count == 0, context + ": all enabled mobile controls are at least 44x44 CSS px. " + Summarize(undersized));
    }

    private static Rect CheckSceneVisibility(SimBarRuntimeVerification.Report report, Canvas canvas, SimBarReadability readability, float minimum, string context)
    {
        PropertyInfo property = typeof(SimBarReadability).GetProperty("SceneViewportCss");
        report.Check(property != null, context + ": the scene viewport is exposed in CSS pixels.");
        if (property == null) return Rect.zero;
        Rect scene = (Rect)property.GetValue(readability);
        Vector2 view = readability.CssViewport;
        Rect viewport = new Rect(Vector2.zero, view);
        float fraction = scene.width * scene.height / Mathf.Max(1f, view.x * view.y);
        report.Check(Within(scene, viewport, 1f) && fraction >= minimum && scene.height / view.y >= minimum,
            context + ": the actual scene keeps at least " + (minimum * 100f).ToString("F0") + "% of the viewport and a continuous clear band; got " + (fraction * 100f).ToString("F1") + "% / " + scene.height.ToString("F0") + "px.");
        float ratio = Screen.width / view.x;
        if (readability.IsCompact)
        {
            Rect expectedCamera = new Rect(scene.position * ratio, scene.size * ratio);
            Camera camera = Camera.main;
            bool cameraMatches = camera != null && Mathf.Abs(camera.pixelRect.x - expectedCamera.x) < 2f && Mathf.Abs(camera.pixelRect.y - expectedCamera.y) < 2f
                && Mathf.Abs(camera.pixelRect.width - expectedCamera.width) < 2f && Mathf.Abs(camera.pixelRect.height - expectedCamera.height) < 2f;
            report.Check(cameraMatches, context + ": the venue camera renders into the reserved scene rectangle.");
        }
        var blocking = new List<string>();
        foreach (Image image in canvas.GetComponentsInChildren<Image>(false))
        {
            if (!image.isActiveAndEnabled || image.color.a < 0.5f || image.canvasRenderer.cull) continue;
            Mask mask = image.GetComponent<Mask>();
            if (mask != null && !mask.showMaskGraphic) continue;
            Rect rect = ScreenRect(image.rectTransform);
            rect = new Rect(rect.position / ratio, rect.size / ratio);
            if (Overlaps(rect, scene, 2f)) blocking.Add(image.name);
        }
        report.Check(blocking.Count == 0, context + ": persistent opaque HUD images stay outside the camera's reserved scene area. " + Summarize(blocking));
        report.measurements.Add(context + ": viewport " + view.x.ToString("F0") + "x" + view.y.ToString("F0") + ", scene " + scene + ", visible area " + (fraction * 100f).ToString("F1") + "%.");
        return scene;
    }

    private static bool Within(Rect inner, Rect outer, float tolerance)
        => inner.width > 0f && inner.height > 0f && inner.xMin >= outer.xMin - tolerance && inner.yMin >= outer.yMin - tolerance
            && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;

    private static bool DrawnAfter(Transform candidate, Transform target)
    {
        // A newly enabled menu has no Graphic.depth yet, but its hierarchy already
        // defines draw order. Older controls behind the drawer must not block it.
        var candidatePath = new List<Transform>();
        var targetPath = new List<Transform>();
        for (Transform node = candidate; node != null; node = node.parent) candidatePath.Add(node);
        for (Transform node = target; node != null; node = node.parent) targetPath.Add(node);
        int a = candidatePath.Count - 1, b = targetPath.Count - 1;
        while (a >= 0 && b >= 0 && candidatePath[a] == targetPath[b]) { a--; b--; }
        return a >= 0 && b >= 0 && candidatePath[a].GetSiblingIndex() > targetPath[b].GetSiblingIndex();
    }

    private static bool Overlaps(Rect a, Rect b, float tolerance)
        => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > tolerance
            && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > tolerance;

    private static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = min;
        for (int i = 1; i < corners.Length; i++) { Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corners[i]); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static string Summarize(List<string> values) => values.Count == 0 ? "" : string.Join(", ", values.GetRange(0, Mathf.Min(values.Count, 8))) + (values.Count > 8 ? " ..." : "");
    private static void ResetPreferenceLoad() => typeof(SimBarReadability).GetField("initialized", StaticFlags).SetValue(null, false);
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, InstanceFlags).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, InstanceFlags).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, InstanceFlags).Invoke(target, args);
    private static Transform FindNode(Transform root, string name) { foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node; return null; }
    private static Text FindText(Transform root, string name) { Transform node = FindNode(root, name); if (node != null && node.GetComponent<Text>() != null) return node.GetComponent<Text>(); throw new InvalidOperationException("Missing text: " + name); }
    private static Button FindButton(Transform root, string name) { Transform node = FindNode(root, name); if (node != null && node.GetComponent<Button>() != null) return node.GetComponent<Button>(); throw new InvalidOperationException("Missing button: " + name); }

    private sealed class Snapshot
    {
        public readonly bool Paused;
        private readonly bool korean, hadLanguage, hadSize, hadSave, toastVisible, pauseVisible;
        private readonly int mode, sizePreference;
        private readonly string languagePreference, save, journeyJson, toast;
        private readonly GameObject focus;
        private readonly Dictionary<string, object> fields = new Dictionary<string, object>();
        private readonly Dictionary<string, object> gameplay = new Dictionary<string, object>();

        public Snapshot(SimBarExperience host)
        {
            Paused = Read<bool>(host, "paused"); korean = SimBarLocalization.IsKorean; mode = SimBarReadability.SizeMode;
            hadLanguage = PlayerPrefs.HasKey(SimBarLocalization.LanguageKey); languagePreference = PlayerPrefs.GetString(SimBarLocalization.LanguageKey, "");
            hadSize = PlayerPrefs.HasKey(SimBarReadability.TextSizeKey); sizePreference = PlayerPrefs.GetInt(SimBarReadability.TextSizeKey, 1);
            hadSave = PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey); save = PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey, "");
            journeyJson = (string)Call(host, "ExportJourneyState");
            foreach (string field in new[] { "paused", "settingsWasPaused", "juiGuideShown", "juiGuideShownKorean", "juiChallenge", "toastTimer", "journeyPhase", "journeyInitialized", "journeyValidationMode" }) fields[field] = Read<object>(host, field);
            foreach (string field in new[] { "cash", "fans", "gameClock", "servedTonight", "satisfaction", "performance", "requestTimer" }) gameplay[field] = Read<object>(host, field);
            Text label = Read<Text>(host, "toastText"); toast = label.text; toastVisible = label.transform.parent.gameObject.activeSelf;
            pauseVisible = Read<GameObject>(host, "pausePanel").activeSelf;
            focus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        public bool GameplayMatches(SimBarExperience host)
        {
            foreach (var field in gameplay) if (!Equals(field.Value, Read<object>(host, field.Key))) return false;
            return journeyJson == (string)Call(host, "ExportJourneyState") && hadSave == PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey)
                && save == PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey, "");
        }

        public void Restore(SimBarExperience host, SimBarReadability readability)
        {
            SimBarReadability.SetSizeMode(mode);
            SimBarLocalization.SetLanguage(korean);
            Read<GameObject>(host, "settingsOverlay").SetActive(false);
            foreach (var field in gameplay) Write(host, field.Key, field.Value);
            JsonUtility.FromJsonOverwrite(journeyJson, Read<object>(host, "journey"));
            foreach (var field in fields) Write(host, field.Key, field.Value);
            Write(host, "juiGuideShown", -1);
            Call(host, "RefreshLanguageUI");
            foreach (var field in fields) Write(host, field.Key, field.Value);
            Text label = Read<Text>(host, "toastText"); label.text = toast; label.transform.parent.gameObject.SetActive(toastVisible);
            Read<GameObject>(host, "pausePanel").SetActive(pauseVisible);
            readability.RefreshLayout();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus);
            if (hadLanguage) PlayerPrefs.SetString(SimBarLocalization.LanguageKey, languagePreference); else PlayerPrefs.DeleteKey(SimBarLocalization.LanguageKey);
            if (hadSize) PlayerPrefs.SetInt(SimBarReadability.TextSizeKey, sizePreference); else PlayerPrefs.DeleteKey(SimBarReadability.TextSizeKey);
            if (hadSave) PlayerPrefs.SetString(SimBarExperience.OperationsSaveKey, save); else PlayerPrefs.DeleteKey(SimBarExperience.OperationsSaveKey);
            PlayerPrefs.Save();
        }
    }
}
