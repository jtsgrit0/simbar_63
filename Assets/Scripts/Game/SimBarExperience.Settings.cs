using UnityEngine;
using UnityEngine.UI;

public sealed partial class SimBarExperience
{
    private GameObject settingsOverlay;
    private bool settingsWasPaused;
    private Button settingsKorean;
    private Button settingsEnglish;
    private readonly Text[] drinkButtonLabels = new Text[DrinkPrices.Length];

    private void InitializeLanguageSettings()
    {
        SimBarLocalization.Changed += RefreshLanguageUI;
        Transform panel;
        settingsOverlay = JourneyOverlay("Language Settings", new Vector2(670f, 420f), out panel);
        Text title = CreateText("Settings Title", panel, "SETTINGS", 31, TextAnchor.MiddleCenter, Color.white);
        SetRect(title.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 151f), new Vector2(600f, 50f));
        Text heading = CreateText("Language Heading", panel, "LANGUAGE", 22, TextAnchor.MiddleCenter, JuiGold);
        SetRect(heading.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 79f), new Vector2(600f, 36f));
        settingsKorean = OpButton("한국어", panel, Vector2.one * 0.5f, new Vector2(-148f, 11f), new Vector2(278f, 66f), 25);
        settingsKorean.name = "Language Korean Button";
        settingsKorean.onClick.AddListener(() => SimBarLocalization.SetLanguage(true));
        settingsEnglish = OpButton("English", panel, Vector2.one * 0.5f, new Vector2(148f, 11f), new Vector2(278f, 66f), 25);
        settingsEnglish.name = "Language English Button";
        settingsEnglish.onClick.AddListener(() => SimBarLocalization.SetLanguage(false));
        Text note = CreateText("Language Save Note", panel, "Changes apply immediately and are saved for next time.", 17, TextAnchor.MiddleCenter, JuiMuted);
        SetRect(note.rectTransform, Vector2.one * 0.5f, new Vector2(0f, -66f), new Vector2(618f, 45f));
        Button close = OpButton("CLOSE", panel, Vector2.one * 0.5f, new Vector2(0f, -143f), new Vector2(400f, 53f), 22);
        close.name = "Language Settings Close Button";
        close.onClick.AddListener(CloseLanguageSettings);

        // Each journey screen covers the HUD gear, so give it its own entry.
        AddSettingsEntry(juiLobby);
        AddSettingsEntry(juiGuide);
        AddSettingsEntry(juiResults);
        if (pausePanel != null)
        {
            SetRect(pausePanel.GetComponent<RectTransform>(), Vector2.one * 0.5f, Vector2.zero, new Vector2(500f, 410f));
            foreach (Text label in pausePanel.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "Pause Title") SetRect(label.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 151f), new Vector2(454f, 50f));
                if (label.name == "Pause Text") SetRect(label.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 94f), new Vector2(452f, 45f));
            }
            Button settings = OpButton("SETTINGS", pausePanel.transform, Vector2.one * 0.5f, new Vector2(0f, -144f), new Vector2(380f, 46f), 19);
            settings.name = "Pause Settings Button";
            settings.onClick.AddListener(OpenLanguageSettings);
        }
        RefreshLanguageUI();
    }

    private void AddSettingsEntry(GameObject overlay)
    {
        if (overlay == null) return;
        Button button = OpButton("SETTINGS", overlay.transform, Vector2.one, new Vector2(-114f, -44f), new Vector2(190f, 47f), 19);
        button.name = "Journey Settings Button";
        button.onClick.AddListener(OpenLanguageSettings);
    }

    private void OpenLanguageSettings()
    {
        if (settingsOverlay == null || settingsOverlay.activeSelf) return;
        if (floorMissionDrawerOpen) CloseFloorMissionDrawer();
        settingsWasPaused = paused;
        paused = true;
        settingsOverlay.SetActive(true);
        settingsOverlay.transform.SetAsLastSibling();
    }

    private void CloseLanguageSettings()
    {
        if (settingsOverlay == null || !settingsOverlay.activeSelf) return;
        settingsOverlay.SetActive(false);
        paused = settingsWasPaused;
        RefreshUI();
    }

    private void RefreshLanguageUI()
    {
        if (canvas == null) return;
        foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.font = SimBarLocalization.Font;
        // The Korean choice must stay readable even when the interface is English.
        Font choiceFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        if (settingsKorean != null && choiceFont != null) settingsKorean.GetComponentInChildren<Text>(true).font = choiceFont;
        RefreshServiceMenuLabels();
        RefreshBookingUI();
        RefreshFloorMissionUI();
        if (settingsKorean != null) settingsKorean.GetComponent<Image>().color = SimBarLocalization.IsKorean ? new Color(0.12f, 0.48f, 0.34f) : new Color(0.16f, 0.20f, 0.27f);
        if (settingsEnglish != null) settingsEnglish.GetComponent<Image>().color = !SimBarLocalization.IsKorean ? new Color(0.12f, 0.48f, 0.34f) : new Color(0.16f, 0.20f, 0.27f);
        RefreshOperationsLanguage();
        RefreshPourLocalization();
        RefreshUI();
        if (toastText != null) toastText.transform.parent.gameObject.SetActive(false);
        if (settingsOverlay != null && settingsOverlay.activeSelf) settingsOverlay.transform.SetAsLastSibling();
    }

    private void OnDestroy()
    {
        SimBarLocalization.Changed -= RefreshLanguageUI;
        if (serviceMenuRoot != null) Destroy(serviceMenuRoot);
        DestroyBookingUI();
        DestroyFloorMissionUI();
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }
        for (int i = 0; i < generatedMusicClips.Length; i++)
        {
            if (generatedMusicClips[i] != null) Destroy(generatedMusicClips[i]);
            generatedMusicClips[i] = null;
        }
    }
}
