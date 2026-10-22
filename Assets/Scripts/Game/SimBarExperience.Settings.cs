using UnityEngine;
using UnityEngine.UI;

public sealed partial class SimBarExperience
{
    private GameObject settingsOverlay;
    private bool settingsWasPaused;
    private Button settingsKorean;
    private Button settingsEnglish;
    private Text settingsTitle;
    private Button settingsCloseButton;
    private RectTransform settingsContent;
    private readonly Button[] settingsTextSizes = new Button[3];
    private readonly Text[] drinkButtonLabels = new Text[3];

    private void InitializeLanguageSettings()
    {
        SimBarLocalization.Changed += RefreshLanguageUI;
        SimBarReadability.Changed += RefreshTextSizeChoices;
        Transform panel;
        settingsOverlay = JourneyOverlay("Language Settings", new Vector2(780f, 660f), out panel);
        settingsContent = CreateReadableModalContent("Language Settings", panel);
        Text title = CreateText("Settings Title", panel, "SETTINGS", 31, TextAnchor.MiddleCenter, Color.white);
        settingsTitle = title;
        SetRect(title.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 270f), new Vector2(718f, 82f));
        Text heading = CreateText("Language Heading", settingsContent, "LANGUAGE", 22, TextAnchor.MiddleCenter, JuiGold);
        SetRect(heading.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 196f), new Vector2(718f, 64f));
        settingsKorean = OpButton("한국어", settingsContent, Vector2.one * 0.5f, new Vector2(-159f, 122f), new Vector2(302f, 80f), 25);
        settingsKorean.name = "Language Korean Button";
        settingsKorean.onClick.AddListener(() => SimBarLocalization.SetLanguage(true));
        settingsEnglish = OpButton("English", settingsContent, Vector2.one * 0.5f, new Vector2(159f, 122f), new Vector2(302f, 80f), 25);
        settingsEnglish.name = "Language English Button";
        settingsEnglish.onClick.AddListener(() => SimBarLocalization.SetLanguage(false));
        Text textSizeHeading = CreateText("Text Size Heading", settingsContent, "TEXT SIZE", 22, TextAnchor.MiddleCenter, JuiGold);
        SimBarLocalization.Bind(textSizeHeading, "TEXT SIZE", "글자 크기");
        SetRect(textSizeHeading.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 36f), new Vector2(718f, 64f));
        string[] sizeNames = { "Normal", "Large", "Extra large" };
        string[] koreanSizeNames = { "기본", "크게", "아주 크게" };
        string[] sizeButtonNames = { "Text Size Normal Button", "Text Size Large Button", "Text Size Extra Large Button" };
        for (int i = 0; i < settingsTextSizes.Length; i++)
        {
            int mode = i;
            Button choice = OpButton(sizeNames[i], settingsContent, Vector2.one * 0.5f, new Vector2(-242f + i * 242f, -50f), new Vector2(226f, 80f), 22);
            choice.name = sizeButtonNames[i];
            SimBarLocalization.Bind(choice.GetComponentInChildren<Text>(true), sizeNames[i], koreanSizeNames[i]);
            choice.onClick.AddListener(() => SimBarReadability.SetSizeMode(mode));
            settingsTextSizes[i] = choice;
        }
        Text note = CreateText("Language Save Note", settingsContent, "Changes apply immediately and are saved for next time.", 17, TextAnchor.MiddleCenter, JuiMuted);
        SetRect(note.rectTransform, Vector2.one * 0.5f, new Vector2(0f, -147f), new Vector2(718f, 68f));
        Button close = OpButton("CLOSE", panel, Vector2.one * 0.5f, new Vector2(0f, -245f), new Vector2(400f, 72f), 22);
        settingsCloseButton = close;
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
        RefreshTextSizeChoices();
        LayoutReadableSettingsModal();
        RefreshLanguageUI();
    }

    private void LayoutReadableSettingsModal()
    {
        if (settingsContent == null || settingsTitle == null || settingsCloseButton == null) return;
        RectTransform frame = (RectTransform)settingsTitle.transform.parent;
        float innerWidth = Mathf.Max(160f, frame.rect.width - 32f);
        float titleHeight = ResponsiveModalTextHeight(settingsTitle, innerWidth, 42f);
        ReadablePlace(settingsTitle.transform, 0f, 10f, innerWidth, titleHeight);
        float closeWidth = Mathf.Min(400f, innerWidth);
        float closeHeight = ResponsiveModalButtonHeight(settingsCloseButton, closeWidth, 46f);
        SetRect(settingsCloseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 12f + closeHeight * 0.5f), new Vector2(closeWidth, closeHeight));
        ResponsiveModalViewport(settingsContent, frame, titleHeight + 24f, closeHeight + 28f);

        float width = settingsContent.rect.width - 8f;
        float top = 4f;
        ResponsiveModalTextRow(settingsContent, "Language Heading", ref top, width, 32f, 8f);
        float languageWidth = (width - 10f) * 0.5f;
        float languageHeight = Mathf.Max(ResponsiveModalButtonHeight(settingsKorean, languageWidth, 46f), ResponsiveModalButtonHeight(settingsEnglish, languageWidth, 46f));
        ReadablePlace(settingsKorean.transform, -(languageWidth + 10f) * 0.5f, top, languageWidth, languageHeight);
        ReadablePlace(settingsEnglish.transform, (languageWidth + 10f) * 0.5f, top, languageWidth, languageHeight);
        top += languageHeight + 24f;
        ResponsiveModalTextRow(settingsContent, "Text Size Heading", ref top, width, 32f, 8f);
        bool stacked = width < 440f;
        float sizeWidth = stacked ? width : (width - 20f) / 3f;
        float sizeHeight = 46f;
        foreach (Button choice in settingsTextSizes) sizeHeight = Mathf.Max(sizeHeight, ResponsiveModalButtonHeight(choice, sizeWidth, 46f));
        for (int i = 0; i < settingsTextSizes.Length; i++)
            ReadablePlace(settingsTextSizes[i].transform, stacked ? 0f : (i - 1) * (sizeWidth + 10f), top + (stacked ? i * (sizeHeight + 8f) : 0f), sizeWidth, sizeHeight);
        top += stacked ? settingsTextSizes.Length * (sizeHeight + 8f) + 12f : sizeHeight + 20f;
        ResponsiveModalTextRow(settingsContent, "Language Save Note", ref top, width, 44f, 12f);
        ReadableContentHeight(settingsContent, top);
        settingsCloseButton.transform.SetAsLastSibling();
    }

    private void RefreshTextSizeChoices()
    {
        for (int i = 0; i < settingsTextSizes.Length; i++)
        {
            Button choice = settingsTextSizes[i];
            if (choice == null) continue;
            bool selected = SimBarReadability.SizeMode == i;
            choice.GetComponent<Image>().color = selected ? new Color(0.12f, 0.48f, 0.34f) : new Color(0.16f, 0.20f, 0.27f);
            Text label = choice.GetComponentInChildren<Text>(true);
            if (label != null) label.color = selected ? new Color(0.94f, 1f, 0.95f) : Color.white;
        }
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
        settingsWasPaused = paused;
        paused = true;
        settingsOverlay.SetActive(true);
        settingsOverlay.transform.SetAsLastSibling();
        LayoutReadableSettingsModal();
        ScrollRect scroll = settingsContent != null ? settingsContent.parent.GetComponent<ScrollRect>() : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
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
        for (int i = 0; i < drinkButtonLabels.Length; i++)
            if (drinkButtonLabels[i] != null) drinkButtonLabels[i].text = (i + 1) + "  " + DrinkNames[i] + "  $" + DrinkPrices[i];
        if (settingsKorean != null) settingsKorean.GetComponent<Image>().color = SimBarLocalization.IsKorean ? new Color(0.12f, 0.48f, 0.34f) : new Color(0.16f, 0.20f, 0.27f);
        if (settingsEnglish != null) settingsEnglish.GetComponent<Image>().color = !SimBarLocalization.IsKorean ? new Color(0.12f, 0.48f, 0.34f) : new Color(0.16f, 0.20f, 0.27f);
        RefreshTextSizeChoices();
        RefreshOperationsLanguage();
        RefreshPourLocalization();
        RefreshUI();
        if (toastText != null) toastText.transform.parent.gameObject.SetActive(false);
        if (settingsOverlay != null && settingsOverlay.activeSelf) settingsOverlay.transform.SetAsLastSibling();
    }

    private void OnDestroy()
    {
        SimBarLocalization.Changed -= RefreshLanguageUI;
        SimBarReadability.Changed -= RefreshTextSizeChoices;
    }
}
