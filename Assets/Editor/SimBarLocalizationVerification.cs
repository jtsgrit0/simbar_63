using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises the live language controls without starting a night or changing a save.</summary>
public static class SimBarLocalizationVerification
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("SimBar/Validation/Check Running Localization")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null)
        {
            report.failures.Add("Enter Play Mode before checking the running localization.");
            return report.ToString();
        }
        if (host.IsPourInProgress || Read<bool>(host, "opMenuOpen") || Read<GameObject>(host, "settingsOverlay").activeSelf)
        {
            report.failures.Add("Finish the active pour and close management/settings before checking localization.");
            return report.ToString();
        }
        var snapshot = new Snapshot(host);
        try
        {
            Canvas canvas = Read<Canvas>(host, "canvas");
            GameObject settings = Read<GameObject>(host, "settingsOverlay");
            Button korean = Read<Button>(host, "settingsKorean");
            Button english = Read<Button>(host, "settingsEnglish");
            Button close = FindButton(settings.transform, "Language Settings Close Button");

            // Use the real lobby entry and real language button callbacks.
            SetPhase(host, "Lobby");
            Write(host, "paused", true);
            Call(host, "RefreshUI");
            Button entry = FindButton(Read<GameObject>(host, "juiLobby").transform, "Journey Settings Button");
            report.Check(PointerCanReach(canvas, entry), "The lobby settings entry is pointer-accessible.");
            entry.onClick.Invoke();
            report.Check(settings.activeSelf && Read<bool>(host, "paused"), "The lobby entry opens the settings overlay and keeps the game paused.");
            // Newly enabled graphics receive their raycast depth on the next rendered frame.
            // VerifySettingsPointerTargets performs the actual raycasts after that frame.
            report.Check(IsActivePointerTarget(korean) && IsActivePointerTarget(english) && IsActivePointerTarget(close),
                "Both language choices and the close button are active, enabled pointer targets.");
            english.onClick.Invoke();
            RefreshSkin(canvas);
            CheckPreference(report, false);
            CheckCommonUI(report, host, false);
            var staticLabels = CaptureStaticLabels();
            Color englishSelected = english.GetComponent<Image>().color;
            Color koreanUnselected = korean.GetComponent<Image>().color;

            korean.onClick.Invoke();
            RefreshSkin(canvas);
            CheckPreference(report, true);
            CheckCommonUI(report, host, true);
            CheckStaticLabels(report, staticLabels, true);
            report.Check(korean.GetComponent<Image>().color == englishSelected && english.GetComponent<Image>().color == koreanUnselected,
                "The selected language highlight moves to the Korean button.");
            report.Check(settings.activeSelf && settings.transform.GetSiblingIndex() == settings.transform.parent.childCount - 1,
                "Changing language keeps settings visible above the journey overlays.");
            CheckKoreanFont(report, canvas);
            close.onClick.Invoke();
            report.Check(!settings.activeSelf && Read<bool>(host, "paused"), "Closing lobby settings preserves the paused state.");

            // Both incoming pause states must survive an open/change/close cycle.
            foreach (bool wasPaused in new[] { false, true })
            {
                Write(host, "paused", wasPaused);
                Call(host, "OpenLanguageSettings");
                report.Check(Read<bool>(host, "paused") && Read<bool>(host, "settingsWasPaused") == wasPaused,
                    "Opening settings records the incoming pause state (" + wasPaused + ").");
                float remaining = ReadJourneyFloat(host, "secondsLeft");
                Call(host, "TickJourney", 5f);
                report.Check(ReadJourneyFloat(host, "secondsLeft") == remaining, "Settings never consumes the saved night countdown.");
                SimBarLocalization.SetLanguage(!SimBarLocalization.IsKorean);
                if (wasPaused) Call(host, "TogglePause");
                else close.onClick.Invoke();
                report.Check(Read<bool>(host, "paused") == wasPaused, "Closing settings restores the incoming pause state (" + wasPaused + ").");
                report.Check(!settings.activeSelf, wasPaused ? "P closes the language settings overlay." : "The close button closes the language settings overlay.");
            }
            Write(host, "paused", true);

            CheckJourneyScreens(report, host);
            CheckOperationsPages(report, host);
            CheckPourStates(report, host);

            // Check the complete stable binding set again on the return to English.
            SetPhase(host, "Lobby");
            SimBarLocalization.SetLanguage(false);
            Call(host, "RefreshLanguageUI");
            RefreshSkin(canvas);
            CheckStaticLabels(report, staticLabels, false, true);
            CheckCommonUI(report, host, false);
            report.Check(snapshot.GameplayMatches(host), "Language changes preserve cash, fans, clock, served count, career data, staff and upgrades.");
            report.Check(snapshot.SavedGameMatches(), "Language changes never create, erase or rewrite the gameplay save.");
            report.measurements.Add(staticLabels.Count + " live static bindings; HUD/drinks/order/queue; three guide steps; lobby/results; four management pages; running/good/perfect/spill/timeout pour states.");
            report.measurements.Add("Real settings callbacks and pointer targets; both pause states; ko/en PlayerPrefs and startup preference reload; bundled font glyphs. Original language, preferences and live state restored.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            try { snapshot.Restore(host); }
            catch (Exception error) { report.failures.Add("Restore failed: " + error.GetBaseException()); }
        }
        return report.ToString();
    }

    /// <summary>Call after opening settings and allowing one rendered frame for UI depths to update.</summary>
    public static string VerifySettingsPointerTargets()
    {
        var report = new SimBarRuntimeVerification.Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || !Read<GameObject>(host, "settingsOverlay").activeInHierarchy)
        {
            report.failures.Add("Open language settings in Play Mode and allow one rendered frame before checking its pointer targets.");
            return report.ToString();
        }
        Canvas canvas = Read<Canvas>(host, "canvas");
        foreach (Button button in new[] { Read<Button>(host, "settingsKorean"), Read<Button>(host, "settingsEnglish"),
            FindButton(Read<GameObject>(host, "settingsOverlay").transform, "Language Settings Close Button") })
        {
            report.Check(button.GetComponent<Image>().depth >= 0 && PointerCanReach(canvas, button),
                button.name + " is the first pointer raycast hit after rendering.");
        }
        return report.ToString();
    }

    private static void CheckPreference(SimBarRuntimeVerification.Report report, bool korean)
    {
        report.Check(SimBarLocalization.IsKorean == korean && PlayerPrefs.HasKey(SimBarLocalization.LanguageKey)
            && PlayerPrefs.GetString(SimBarLocalization.LanguageKey) == (korean ? "ko" : "en"), "The language button persists its selected language.");
        // Exercise the same lazy preference load used by a fresh runtime, retaining live event subscribers.
        typeof(SimBarLocalization).GetField("initialized", StaticFlags).SetValue(null, false);
        report.Check(SimBarLocalization.IsKorean == korean, "The selected language reloads from PlayerPrefs.");
    }

    private static void CheckCommonUI(SimBarRuntimeVerification.Report report, SimBarExperience host, bool korean)
    {
        foreach (string field in new[] { "orderText", "queueText", "playlistNowText", "opStatus", "opWallet", "opMapCaption", "opStaffCaption",
            "juiNightTitle", "juiScore", "juiCombo", "juiRank", "juiFever", "juiLobbyProgress", "juiLobbyMode", "juiPauseDescription" })
            CheckLanguage(report, Read<Text>(host, field), korean, field);
        Transform root = Read<Canvas>(host, "canvas").transform;
        CheckExact(report, FindText(root, "Settings Title"), korean ? "설정" : "SETTINGS");
        CheckExact(report, FindText(root, "Language Heading"), korean ? "언어" : "LANGUAGE");
        CheckExact(report, Read<Button>(host, "settingsKorean").GetComponentInChildren<Text>(true), "한국어");
        CheckExact(report, Read<Button>(host, "settingsEnglish").GetComponentInChildren<Text>(true), "English");
        CheckLanguage(report, FindText(root, "Daily Profit"), korean, "Daily Profit");
        foreach (Text label in Read<Text[]>(host, "drinkButtonLabels")) CheckLanguage(report, label, korean, "Drink button");
        foreach (Text label in Read<Text[]>(host, "juiGoals")) CheckLanguage(report, label, korean, "Night goal");
    }

    private static void CheckJourneyScreens(SimBarRuntimeVerification.Report report, SimBarExperience host)
    {
        object journey = Read<object>(host, "journey");
        int originalStep = Read<int>(journey, "guideStep");
        try
        {
            foreach (bool korean in new[] { false, true })
            {
                SimBarLocalization.SetLanguage(korean);
                SetPhase(host, "Lobby"); Call(host, "RefreshUI");
                CheckLanguage(report, Read<Text>(host, "juiLobbyProgress"), korean, "Lobby career summary");
                CheckLanguage(report, Read<Text>(host, "juiLobbyMode"), korean, "Lobby selected mode");
                foreach (string phase in new[] { "Lobby", "Guide", "Results" })
                {
                    SetPhase(host, phase); Call(host, "RefreshUI");
                    GameObject overlay = Read<GameObject>(host, phase == "Lobby" ? "juiLobby" : phase == "Guide" ? "juiGuide" : "juiResults");
                    CheckExact(report, FindButton(overlay.transform, "Journey Settings Button").GetComponentInChildren<Text>(true), korean ? "설정" : "SETTINGS");
                }
                SetPhase(host, "Guide");
                for (int step = 0; step < 3; step++)
                {
                    Write(journey, "guideStep", step); Call(host, "RefreshUI");
                    foreach (string field in new[] { "juiGuideTitle", "juiGuideBody", "juiGuideExample", "juiGuideNumber", "juiGuideNextLabel" })
                        CheckLanguage(report, Read<Text>(host, field), korean, "Guide step " + step + " " + field);
                }
                SetPhase(host, "Results"); Call(host, "RefreshUI");
                foreach (string field in new[] { "juiResultsTitle", "juiResultsRank", "juiResultRecord", "juiResultProgress" })
                    CheckLanguage(report, Read<Text>(host, field), korean, field);
                foreach (Text label in Read<Text[]>(host, "juiResultGoals")) CheckLanguage(report, label, korean, "Result goal");
            }
        }
        finally { Write(journey, "guideStep", originalStep); }
    }

    private static void CheckOperationsPages(SimBarRuntimeVerification.Report report, SimBarExperience host)
    {
        Write(host, "opMenuOpen", true);
        try
        {
            for (int page = 0; page < 4; page++)
            {
                Write(host, "opPage", page);
                foreach (bool korean in new[] { false, true })
                {
                    SimBarLocalization.SetLanguage(korean);
                    Call(host, "RefreshOperationsLanguage");
                    bool localizedContent = false;
                    foreach (Text text in Read<Transform>(host, "opBody").GetComponentsInChildren<Text>(true))
                    {
                        // Redraw destroys old rows at the end of this frame after deactivating them.
                        if (!LocallyActive(text.transform, Read<Transform>(host, "opBody"))) continue;
                        localizedContent |= HasHangul(text.text);
                        if (!korean) report.Check(!HasHangul(text.text), "Management page " + page + " redraws its rows in English.");
                    }
                    report.Check(korean ? localizedContent : !localizedContent, "Management page " + page + " redraws in the selected language.");
                }
            }
        }
        finally { Write(host, "opMenuOpen", false); }
    }

    private static void CheckPourStates(SimBarRuntimeVerification.Report report, SimBarExperience host)
    {
        object patron = null;
        foreach (object candidate in Read<IEnumerable>(host, "patrons")) { patron = candidate; break; }
        report.Check(patron != null, "A live guest exists for checking the running pour labels.");
        foreach (bool korean in new[] { false, true })
        {
            SimBarLocalization.SetLanguage(korean);
            Write(host, "pourRunning", patron != null); Write(host, "pourPatron", patron); Write(host, "pourDrinkIndex", 0);
            Write(host, "pourElapsed", 0.5f); Call(host, "RefreshPourLocalization");
            foreach (string field in new[] { "pourTitle", "pourSubtitle", "pourHint", "pourStopLabel", "pourClockText" })
                CheckLanguage(report, Read<Text>(host, field), korean, "Running " + field);
            Write(host, "pourRunning", false); Write(host, "pourPatron", null); Write(host, "pourFeedbackRemaining", 0.8f);
            foreach (float quality in new[] { 1f, 0.65f, 0.2f })
            {
                Write(host, "<LastPourQuality>k__BackingField", quality);
                foreach (bool timeout in quality < 0.5f ? new[] { false, true } : new[] { false })
                {
                    Write(host, "pourTimedOut", timeout); Call(host, "RefreshPourLocalization");
                    foreach (string field in new[] { "pourTitle", "pourSubtitle", "pourHint", "pourStopLabel" })
                        CheckLanguage(report, Read<Text>(host, field), korean, "Pour result " + quality + " " + field);
                }
            }
        }
    }

    private sealed class StaticLabel
    {
        public Text label;
        public string english, korean;
    }

    private static List<StaticLabel> CaptureStaticLabels()
    {
        var result = new List<StaticLabel>();
        foreach (object binding in (IEnumerable)typeof(SimBarLocalization).GetField("bindings", StaticFlags).GetValue(null))
        {
            Text label = Read<Text>(binding, "label");
            if (label != null && label.text == Read<string>(binding, "applied"))
                result.Add(new StaticLabel { label = label, english = Read<string>(binding, "english"), korean = Read<string>(binding, "korean") });
        }
        return result;
    }

    private static void CheckStaticLabels(SimBarRuntimeVerification.Report report, List<StaticLabel> labels, bool korean, bool skipDynamicGuide = false)
    {
        report.Check(labels.Count >= 25, "The verification covers the complete live static label registry.");
        foreach (StaticLabel item in labels)
        {
            if (item.label == null) continue;
            // NEXT is a bound initial label which legitimately changes to OPEN THE DOORS on guide step three.
            if (skipDynamicGuide && item.english == "NEXT") continue;
            CheckExact(report, item.label, korean ? item.korean : item.english);
        }
    }

    private static void CheckKoreanFont(SimBarRuntimeVerification.Report report, Canvas canvas)
    {
        Font bundled = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        report.Check(bundled != null && SimBarLocalization.Font == bundled && AssetDatabase.GetAssetPath(bundled).StartsWith("Assets/Resources/Fonts/"),
            "Korean mode uses the font bundled in the project.");
        if (bundled == null) return;
        var glyphs = new HashSet<char>("한글설정언어주문서빙완벽");
        foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
        {
            report.Check(text.font == bundled, "Korean text uses the bundled font: " + text.name);
            foreach (char c in text.text) if (IsHangul(c)) glyphs.Add(c);
        }
        foreach (char c in glyphs) report.Check(bundled.HasCharacter(c), "Bundled font contains Korean glyph " + c + ".");
    }

    private static void CheckLanguage(SimBarRuntimeVerification.Report report, Text label, bool korean, string context)
    {
        report.Check(label != null && !string.IsNullOrWhiteSpace(label.text) && HasHangul(label.text) == korean, context + " displays " + (korean ? "Korean." : "English."));
    }
    private static void CheckExact(SimBarRuntimeVerification.Report report, Text label, string expected) =>
        report.Check(label != null && label.text == expected, (label != null ? label.name : "Missing label") + " displays '" + expected + "'.");
    private static bool HasHangul(string value) { foreach (char c in value ?? "") if (IsHangul(c)) return true; return false; }
    private static bool IsHangul(char c) => (c >= '\uac00' && c <= '\ud7a3') || (c >= '\u3131' && c <= '\u318e');
    private static bool LocallyActive(Transform child, Transform root) { for (Transform current = child; current != root; current = current.parent) if (!current.gameObject.activeSelf) return false; return true; }
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, InstanceFlags).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, InstanceFlags).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, InstanceFlags).Invoke(target, args);
    private static float ReadJourneyFloat(SimBarExperience host, string name) => Read<float>(Read<object>(host, "journey"), name);
    private static void SetPhase(SimBarExperience host, string phase) { FieldInfo field = host.GetType().GetField("journeyPhase", InstanceFlags); field.SetValue(host, Enum.Parse(field.FieldType, phase)); }
    private static Text FindText(Transform root, string name) { foreach (Text text in root.GetComponentsInChildren<Text>(true)) if (text.name == name) return text; throw new InvalidOperationException("Missing text: " + name); }
    private static Button FindButton(Transform root, string name) { foreach (Button button in root.GetComponentsInChildren<Button>(true)) if (button.name == name) return button; throw new InvalidOperationException("Missing button: " + name); }
    private static void RefreshSkin(Canvas canvas) { SimBarHudSkin skin = canvas.GetComponent<SimBarHudSkin>(); if (skin != null) Call(skin, "LateUpdate"); }
    private static bool PointerCanReach(Canvas canvas, Button button)
    {
        if (button == null || EventSystem.current == null) return false;
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>();
        canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) }, hits);
        return hits.Count > 0 && hits[0].gameObject == button.gameObject;
    }
    private static bool IsActivePointerTarget(Button button) => button != null && button.isActiveAndEnabled && button.interactable
        && button.GetComponent<Image>() != null && button.GetComponent<Image>().raycastTarget;

    private sealed class Snapshot
    {
        private readonly bool language, hadLanguage, hadSave;
        private readonly string languagePreference, saved, journeyJson, toast;
        private readonly bool toastVisible, pauseVisible, pourVisible;
        private readonly Dictionary<string, object> fields = new Dictionary<string, object>();
        private readonly Dictionary<string, Array> arrays = new Dictionary<string, Array>();
        private readonly Dictionary<string, object> gameplay = new Dictionary<string, object>();
        public Snapshot(SimBarExperience host)
        {
            language = SimBarLocalization.IsKorean;
            hadLanguage = PlayerPrefs.HasKey(SimBarLocalization.LanguageKey); languagePreference = PlayerPrefs.GetString(SimBarLocalization.LanguageKey, "");
            hadSave = PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey); saved = PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey, "");
            journeyJson = (string)Call(host, "ExportJourneyState");
            foreach (string name in new[] { "paused", "settingsWasPaused", "journeyPhase", "juiChallenge", "juiGuideShown", "juiGuideShownKorean", "opPage", "opMenuOpen",
                "pourRunning", "pourPatron", "pourDrinkIndex", "pourElapsed", "pourFeedbackRemaining", "pourTimedOut", "<LastPourQuality>k__BackingField", "toastTimer" }) fields[name] = Read<object>(host, name);
            foreach (string name in new[] { "cash", "fans", "gameClock", "servedTonight", "requestTimer", "satisfaction", "performance" }) gameplay[name] = Read<object>(host, name);
            foreach (string name in new[] { "opStaff", "opUpgrades", "opCooldowns" }) arrays[name] = (Array)Read<Array>(host, name).Clone();
            Text label = Read<Text>(host, "toastText"); toast = label.text; toastVisible = label.transform.parent.gameObject.activeSelf;
            pauseVisible = Read<GameObject>(host, "pausePanel").activeSelf; pourVisible = Read<GameObject>(host, "pourPanel").activeSelf;
        }
        public bool GameplayMatches(SimBarExperience host)
        {
            if (journeyJson != (string)Call(host, "ExportJourneyState")) return false;
            foreach (var field in gameplay) if (!Equals(field.Value, Read<object>(host, field.Key))) return false;
            foreach (var field in arrays)
            {
                Array current = Read<Array>(host, field.Key);
                for (int i = 0; i < current.Length; i++) if (!Equals(current.GetValue(i), field.Value.GetValue(i))) return false;
            }
            return true;
        }
        public bool SavedGameMatches() => PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey) == hadSave && PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey, "") == saved;
        public void Restore(SimBarExperience host)
        {
            foreach (var field in gameplay) Write(host, field.Key, field.Value);
            foreach (var field in arrays) Array.Copy(field.Value, Read<Array>(host, field.Key), field.Value.Length);
            JsonUtility.FromJsonOverwrite(journeyJson, Read<object>(host, "journey"));
            foreach (var field in fields) Write(host, field.Key, field.Value);
            // Rebuild guide content even when the original language matches the last checked language.
            Write(host, "juiGuideShown", -1);
            SimBarLocalization.SetLanguage(language);
            Call(host, "RefreshLanguageUI");
            Read<GameObject>(host, "settingsOverlay").SetActive(false);
            Read<GameObject>(host, "opOverlay").SetActive(false);
            Read<GameObject>(host, "pausePanel").SetActive(pauseVisible);
            Read<GameObject>(host, "pourPanel").SetActive(pourVisible);
            Text label = Read<Text>(host, "toastText"); label.text = toast; label.transform.parent.gameObject.SetActive(toastVisible);
            RefreshSkin(Read<Canvas>(host, "canvas"));
            if (hadLanguage) PlayerPrefs.SetString(SimBarLocalization.LanguageKey, languagePreference); else PlayerPrefs.DeleteKey(SimBarLocalization.LanguageKey);
            if (hadSave) PlayerPrefs.SetString(SimBarExperience.OperationsSaveKey, saved); else PlayerPrefs.DeleteKey(SimBarExperience.OperationsSaveKey);
            PlayerPrefs.Save();
        }
    }
}
