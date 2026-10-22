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
                    CheckSettingsFit(report, settings.transform, context);
                    CheckPrimaryPanels(report, canvas, context);
                    CheckStableRefresh(report, canvas, readability, context);
                }
            }
            FindButton(settings.transform, "Language Settings Close Button").onClick.Invoke();
            report.Check(!settings.activeSelf && Read<bool>(host, "paused") == snapshot.Paused,
                "Closing text-size settings restores the incoming pause state.");
            CheckCompactPourLayout(report, host, canvas, readability);
            report.Check(snapshot.GameplayMatches(host), "Text size and language changes preserve the live night and gameplay save.");
            report.measurements.Add("Game View " + Screen.width + "x" + Screen.height + "; compact=" + readability.IsCompact
                + "; canvas pixels per CSS unit=" + readability.PixelsPerCssUnit.ToString("F3")
                + ". Checked both languages and all three sizes at this actual viewport.");
            report.measurements.Add("Real size callbacks, keyboard focus, selected state, default and saved preference reload, CSS font minimums, settings text fit, HUD separation and repeated-layout stability. Original state and preferences restored.");
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
        float minimum = mode == 0 ? 16f : mode == 1 ? 18f : 20f;
        var tooSmall = new List<string>();
        var bestFit = new List<string>();
        int active = 0;
        foreach (Text text in canvas.GetComponentsInChildren<Text>(false))
        {
            if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || text.color.a <= 0.01f) continue;
            active++;
            float css = readability.EffectiveFontCssSize(text);
            if (float.IsNaN(css) || float.IsInfinity(css) || css + 0.25f < minimum) tooSmall.Add(text.name + "=" + css.ToString("F1"));
            if (text.resizeTextForBestFit) bestFit.Add(text.name);
        }
        report.Check(active > 0 && tooSmall.Count == 0, context + ": all " + active + " active labels reach " + minimum + " CSS px. " + Summarize(tooSmall));
        report.Check(bestFit.Count == 0, context + ": active labels do not shrink with automatic best-fit. " + Summarize(bestFit));
    }

    private static void CheckSettingsFit(SimBarRuntimeVerification.Report report, Transform settings, string context)
    {
        var overflow = new List<string>();
        foreach (Text text in settings.GetComponentsInChildren<Text>(false))
        {
            if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text)) continue;
            Rect rect = text.rectTransform.rect;
            bool widthFits = text.horizontalOverflow == HorizontalWrapMode.Wrap || text.preferredWidth <= rect.width + 2f;
            if (!widthFits || text.preferredHeight > rect.height + 2f) overflow.Add(text.transform.parent.name + "/" + text.name);
        }
        report.Check(overflow.Count == 0, context + ": settings labels fit their rectangles without shrinking. " + Summarize(overflow));
    }

    private static void CheckPrimaryPanels(SimBarRuntimeVerification.Report report, Canvas canvas, string context)
    {
        string[] names = { "Order Border", "Playlist Border", "Venue Panel", "Staff Management Card", "Day and Profit Card", "Mood Panel", "Journey Night HUD", "Operations Live Status", "CASH Card", "FANS Card", "TIME Card" };
        var panels = new List<RectTransform>();
        foreach (string name in names)
        {
            Transform node = FindNode(canvas.transform, name);
            // Compact layouts deliberately hide secondary panels.
            if (node != null && node.gameObject.activeInHierarchy && node is RectTransform) panels.Add((RectTransform)node);
        }
        var overlaps = new List<string>();
        for (int i = 0; i < panels.Count; i++)
        for (int j = i + 1; j < panels.Count; j++)
        {
            Rect a = ScreenRect(panels[i]), b = ScreenRect(panels[j]);
            if (Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 1f
                && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 1f) overlaps.Add(panels[i].name + "/" + panels[j].name);
        }
        report.Check(overlaps.Count == 0, context + ": visible primary HUD panels do not overlap. " + Summarize(overlaps));
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

    private static void CheckCompactPourLayout(SimBarRuntimeVerification.Report report, SimBarExperience host, Canvas canvas, SimBarReadability readability)
    {
        if (!readability.IsCompact)
        {
            report.measurements.Add("Compact timing-panel checks require a compact Game View and were skipped at this viewport.");
            return;
        }
        GameObject pour = Read<GameObject>(host, "pourPanel");
        bool wasActive = pour.activeSelf;
        try
        {
            // Exercise geometry only; no pour is started and no game timer is advanced.
            pour.SetActive(true);
            for (int mode = 0; mode < 3; mode++)
            {
                SimBarReadability.SetSizeMode(mode);
                readability.RefreshLayout();
                Canvas.ForceUpdateCanvases();
                string context = "Compact timing panel mode " + mode;
                Rect timing = ScreenRect(pour.GetComponent<RectTransform>());
                var overlaps = new List<string>();
                foreach (string name in new[] { "CASH Card", "FANS Card", "TIME Card", "Order Border" })
                {
                    Transform node = FindNode(canvas.transform, name);
                    if (node == null || !node.gameObject.activeInHierarchy) continue;
                    Rect other = ScreenRect(node.GetComponent<RectTransform>());
                    if (Mathf.Min(timing.xMax, other.xMax) - Mathf.Max(timing.xMin, other.xMin) > 1f
                        && Mathf.Min(timing.yMax, other.yMax) - Mathf.Max(timing.yMin, other.yMin) > 1f) overlaps.Add(name);
                }
                report.Check(overlaps.Count == 0, context + ": timing controls do not overlap visible headers or bar service. " + Summarize(overlaps));
                bool navigationHidden = true;
                foreach (string name in new[] { "Staff Management Card", "Operations UPGRADES", "Operations GUEST INFO", "Operations EVENTS" })
                {
                    Transform node = FindNode(canvas.transform, name);
                    navigationHidden &= node != null && !node.gameObject.activeSelf;
                }
                report.Check(navigationHidden, context + ": all four management navigation controls are hidden.");
                report.Check(!FindButton(canvas.transform, "Compact Music Button").interactable
                    && !FindButton(canvas.transform, "Compact Venue Button").interactable,
                    context + ": auxiliary music and venue controls are disabled during timing.");
            }
            report.measurements.Add("Compact timing-panel geometry, management navigation visibility and disabled auxiliary controls checked at all three text sizes without starting a pour.");
        }
        finally
        {
            pour.SetActive(wasActive);
            readability.RefreshLayout();
        }
    }

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
        private readonly bool korean, hadLanguage, hadSize, hadSave, toastVisible;
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
            foreach (string field in new[] { "paused", "settingsWasPaused", "juiGuideShown", "juiGuideShownKorean", "juiChallenge", "toastTimer" }) fields[field] = Read<object>(host, field);
            foreach (string field in new[] { "cash", "fans", "gameClock", "servedTonight", "satisfaction", "performance", "requestTimer" }) gameplay[field] = Read<object>(host, field);
            Text label = Read<Text>(host, "toastText"); toast = label.text; toastVisible = label.transform.parent.gameObject.activeSelf;
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
            readability.RefreshLayout();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus);
            if (hadLanguage) PlayerPrefs.SetString(SimBarLocalization.LanguageKey, languagePreference); else PlayerPrefs.DeleteKey(SimBarLocalization.LanguageKey);
            if (hadSize) PlayerPrefs.SetInt(SimBarReadability.TextSizeKey, sizePreference); else PlayerPrefs.DeleteKey(SimBarReadability.TextSizeKey);
            if (hadSave) PlayerPrefs.SetString(SimBarExperience.OperationsSaveKey, save); else PlayerPrefs.DeleteKey(SimBarExperience.OperationsSaveKey);
            PlayerPrefs.Save();
        }
    }
}
