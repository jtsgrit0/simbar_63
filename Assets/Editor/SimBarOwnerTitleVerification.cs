using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises actual owner-card callbacks, preference reload and title pointer targets.</summary>
public static class SimBarOwnerTitleVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SimBar/Validation/Check Running Owner Title")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress || Read<bool>(host, "opMenuOpen"))
        {
            report.failures.Add("Enter Play Mode and close management/pour controls before checking the owner title.");
            return report.ToString();
        }
        bool hadOwner = PlayerPrefs.HasKey(SimBarExperience.OwnerPreferenceKey);
        string savedOwner = PlayerPrefs.GetString(SimBarExperience.OwnerPreferenceKey, "");
        bool hadLanguage = PlayerPrefs.HasKey(SimBarLocalization.LanguageKey);
        string savedLanguage = PlayerPrefs.GetString(SimBarLocalization.LanguageKey, "");
        bool korean = SimBarLocalization.IsKorean;
        bool hadOperations = PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey);
        string savedOperations = PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey, "");
        int owner = host.SelectedOwnerIndex;
        object phase = Read<object>(host, "journeyPhase");
        bool paused = Read<bool>(host, "paused");
        bool validation = Read<bool>(host, "journeyValidationMode");
        bool challenge = Read<bool>(host, "juiChallenge");
        try
        {
            Write(host, "journeyValidationMode", false);
            Write(host, "journeyPhase", Enum.Parse(phase.GetType(), "Lobby"));
            Write(host, "paused", true);
            Call(host, "RefreshUI");
            GameObject lobby = Read<GameObject>(host, "juiLobby");
            Button[] cards = Read<Button[]>(host, "ownerCards");
            report.Check(lobby != null && lobby.activeInHierarchy && cards.Length == 3,
                "The paused title offers exactly three selectable owners.");
            CheckVisibleTitle(report, host, lobby);
            var portraits = lobby.GetComponentsInChildren<RawImage>(true);
            int portraitCount = 0;
            var distinctPortraits = new HashSet<Texture>();
            foreach (RawImage image in portraits)
            {
                if (!image.name.StartsWith("Journey Owner Portrait ", StringComparison.Ordinal)) continue;
                portraitCount++;
                report.Check(image.texture != null, image.name + " loads its bundled illustrated portrait.");
                if (image.texture != null) distinctPortraits.Add(image.texture);
                float visibleAspect = image.texture == null ? 0f : image.texture.width * image.uvRect.width / (image.texture.height * image.uvRect.height);
                report.Check(!image.raycastTarget && Mathf.Abs(visibleAspect - 400f / 350f) < 0.001f,
                    image.name + " preserves its image aspect and lets clicks reach the card.");
            }
            report.Check(portraitCount == 3 && distinctPortraits.Count == 3,
                "Three different portrait textures are shown on the title.");
            string[] ids = { "female", "ff", "pet-sounds" };
            Canvas canvas = Read<Canvas>(host, "canvas");
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < cards.Length; i++)
            {
                Button card = cards[i];
                report.Check(card != null && card.interactable && card.isActiveAndEnabled && PointerReachable(canvas, card),
                    "Owner card " + i + " is pointer-accessible at the current screen size.");
                card.onClick.Invoke();
                report.Check(host.SelectedOwnerIndex == i && host.SelectedOwnerId == ids[i]
                    && PlayerPrefs.GetString(SimBarExperience.OwnerPreferenceKey) == ids[i],
                    "Owner card " + i + " selects and persists the player identity.");
                Write(host, "selectedOwner", (i + 1) % 3);
                Call(host, "LoadOwnerSelection"); Call(host, "RefreshOwnerSelection");
                report.Check(host.SelectedOwnerIndex == i,
                    "Owner " + i + " reloads from its saved preference.");
                Text[] badges = Read<Text[]>(host, "ownerBadges");
                int selectedBadges = 0;
                foreach (Text badge in badges)
                    if (badge.text == "SELECTED" || badge.text == "선택됨") selectedBadges++;
                report.Check(selectedBadges == 1 && Read<Text>(host, "ownerSelectionLabel").text.Contains(host.SelectedOwnerName),
                    "Exactly one portrait is selected and the title identifies its owner.");
            }
            for (int i = -1; i <= 3; i += 4)
            {
                Call(host, "SelectOwner", i);
                report.Check(host.SelectedOwnerIndex == 2, "Out-of-range owner " + i + " is ignored.");
            }
            PlayerPrefs.SetString(SimBarExperience.OwnerPreferenceKey, "unknown-owner");
            Call(host, "LoadOwnerSelection");
            report.Check(host.SelectedOwnerIndex == 0, "An unknown preference safely defaults to the first owner.");
            foreach (bool useKorean in new[] { false, true })
            {
                SimBarLocalization.SetLanguage(useKorean);
                Call(host, "RefreshUI");
                Text[] names = Read<Text[]>(host, "ownerNames");
                report.Check(names[0].text == (useKorean ? "여사장" : "THE OWNER")
                    && Read<Text>(host, "ownerSelectionLabel").text.StartsWith(useKorean ? "선택한 사장" : "YOUR OWNER", StringComparison.Ordinal),
                    "Owner names and selected identity refresh in " + (useKorean ? "Korean." : "English."));
            }
            Button begin = Read<Button>(host, "juiBegin");
            Button resume = Read<Button>(host, "juiContinue");
            report.Check((!begin.gameObject.activeSelf || PointerReachable(canvas, begin))
                && (!resume.gameObject.activeSelf || PointerReachable(canvas, resume)),
                "The existing begin/continue paths remain pointer-accessible below owner selection.");
            report.Check(PlayerPrefs.HasKey(SimBarExperience.OperationsSaveKey) == hadOperations
                && PlayerPrefs.GetString(SimBarExperience.OperationsSaveKey, "") == savedOperations,
                "Changing owners never rewrites the existing venue/night save.");
            report.measurements.Add("Three actual card callbacks; distinct bundled portraits; preference reload/defaults; KO/EN identity; begin/continue targets; venue save unchanged.");
            report.measurements.Add("Screen " + Screen.width + "x" + Screen.height + "; title 1480x900 logical pixels.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            if (hadOwner) PlayerPrefs.SetString(SimBarExperience.OwnerPreferenceKey, savedOwner);
            else PlayerPrefs.DeleteKey(SimBarExperience.OwnerPreferenceKey);
            SimBarLocalization.SetLanguage(korean);
            if (hadLanguage) PlayerPrefs.SetString(SimBarLocalization.LanguageKey, savedLanguage);
            else PlayerPrefs.DeleteKey(SimBarLocalization.LanguageKey);
            Write(host, "selectedOwner", owner);
            Write(host, "journeyPhase", phase); Write(host, "paused", paused);
            Write(host, "journeyValidationMode", validation); Write(host, "juiChallenge", challenge);
            Call(host, "RefreshUI");
            PlayerPrefs.Save();
        }
        return report.ToString();
    }

    /// <summary>Checks viewport fit independently while the portrait artwork is being prepared.</summary>
    public static string VerifyLayout()
    {
        var report = new SimBarRuntimeVerification.Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress || Read<bool>(host, "opMenuOpen"))
        {
            report.failures.Add("Enter Play Mode and close management/pour controls before checking the title layout.");
            return report.ToString();
        }
        object phase = Read<object>(host, "journeyPhase");
        bool paused = Read<bool>(host, "paused");
        bool validation = Read<bool>(host, "journeyValidationMode");
        try
        {
            Write(host, "journeyValidationMode", false);
            Write(host, "journeyPhase", Enum.Parse(phase.GetType(), "Lobby"));
            Write(host, "paused", true);
            Call(host, "RefreshUI");
            CheckVisibleTitle(report, host, Read<GameObject>(host, "juiLobby"));
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            Write(host, "journeyPhase", phase); Write(host, "paused", paused);
            Write(host, "journeyValidationMode", validation);
            Call(host, "RefreshUI");
        }
        return report.ToString();
    }

    private static void CheckVisibleTitle(SimBarRuntimeVerification.Report report, SimBarExperience host, GameObject lobby)
    {
        Canvas.ForceUpdateCanvases();
        var layout = lobby.GetComponentInChildren<SimBarOwnerTitleLayout>(true);
        report.Check(layout != null, "Only the illustrated title has a viewport-fit component.");
        if (layout == null) return;
        layout.RefreshFit();
        RectTransform panel = layout.GetComponent<RectTransform>();
        Vector3 scale = panel.localScale;
        report.Check(scale.x > 0f && scale.x <= 1f && Mathf.Abs(scale.x - scale.y) < 0.0001f,
            "The title scales uniformly without stretching the portraits or enlarging the original layout.");
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        report.Check(CornersOnScreen(corners), "The complete title panel fits inside the current viewport.");
        Canvas canvas = Read<Canvas>(host, "canvas");
        foreach (Button card in Read<Button[]>(host, "ownerCards"))
        {
            card.GetComponent<RectTransform>().GetWorldCorners(corners);
            report.Check(CornersOnScreen(corners), card.name + " is fully visible, including its face and name.");
            report.Check(PointerReachable(canvas, card), card.name + " remains clickable after viewport fitting.");
        }
        foreach (Button button in new[] { Read<Button>(host, "juiBegin"), Read<Button>(host, "juiContinue") })
        {
            if (!button.gameObject.activeSelf) continue;
            button.GetComponent<RectTransform>().GetWorldCorners(corners);
            report.Check(CornersOnScreen(corners) && PointerReachable(canvas, button),
                button.name + " stays fully visible and clickable after viewport fitting.");
        }
        report.measurements.Add("Screen " + Screen.width + "x" + Screen.height + "; title uniform scale " + scale.x.ToString("F3") + ".");
    }

    private static bool CornersOnScreen(Vector3[] corners)
    {
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
            if (point.x < -0.5f || point.y < -0.5f || point.x > Screen.width + 0.5f || point.y > Screen.height + 0.5f) return false;
        }
        return true;
    }

    private static bool PointerReachable(Canvas canvas, Button button)
    {
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        if (!new Rect(0f, 0f, Screen.width, Screen.height).Contains(point)) return false;
        Image graphic = button.GetComponent<Image>();
        // A title reactivated by this verifier can receive its render depth on the next frame.
        if (graphic.depth < 0)
            return button.isActiveAndEnabled && button.interactable && graphic.raycastTarget && graphic.Raycast(point, null);
        var hits = new List<RaycastResult>();
        canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) { position = point }, hits);
        foreach (RaycastResult hit in hits)
        {
            Button target = hit.gameObject.GetComponentInParent<Button>();
            if (target != null) return target == button;
            if (hit.gameObject.GetComponent<Graphic>() != null) return false;
        }
        return false;
    }

    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
}
