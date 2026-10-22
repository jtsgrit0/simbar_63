using UnityEngine;
using UnityEngine.UI;

/// <summary>Lobby, first-night guide, nightly goals and end-of-night results.</summary>
public sealed partial class SimBarExperience
{
    private bool juiBuilt;
    private bool juiChallenge;
    private int juiGuideShown = -1;
    private bool juiGuideShownKorean;
    private GameObject juiHud;
    private GameObject juiLobby;
    private GameObject juiGuide;
    private GameObject juiResults;
    private Text juiDay;
    private Text juiNightTitle;
    private Text juiClock;
    private Text juiScore;
    private Text juiCombo;
    private Text juiRank;
    private Text juiFever;
    private Image juiFeverFill;
    private readonly Text[] juiGoals = new Text[3];
    private readonly Image[] juiGoalFills = new Image[3];
    private readonly Text[] juiResultGoals = new Text[3];
    private readonly Text[] juiStars = new Text[3];
    private Image juiCozyCard;
    private Image juiChallengeCard;
    private Text juiLobbyProgress;
    private Text juiLobbyMode;
    private Button juiContinue;
    private Button juiBegin;
    private Button juiSaveLobby;
    private Text juiPauseDescription;
    private Text juiGuideTitle;
    private Text juiGuideBody;
    private Text juiGuideExample;
    private Text juiGuideNumber;
    private Text juiGuideNextLabel;
    private readonly Image[] juiGuideSteps = new Image[3];
    private Text juiResultsTitle;
    private Text juiResultsRank;
    private Text juiResultProfit;
    private Text juiResultServices;
    private Text juiResultXP;
    private Text juiResultRecord;
    private Text juiResultProgress;
    private RectTransform juiLobbyContent;
    private RectTransform juiGuideContent;
    private RectTransform juiResultsContent;
    private ScrollRect juiGuideScroll;
    private RectTransform juiLobbyActions;
    private RectTransform juiGuideActions;
    private RectTransform juiResultsActions;
    private Button juiGuideNext;
    private readonly Button[] juiResultActions = new Button[3];

    private static readonly Color JuiGold = new Color(0.97f, 0.81f, 0.43f, 1f);
    private static readonly Color JuiGreen = new Color(0.55f, 0.93f, 0.58f, 1f);
    private static readonly Color JuiMuted = new Color(0.70f, 0.77f, 0.87f, 1f);
    private static readonly string[] JuiGuideTitles =
    {
        "READ THE ORDER",
        "TIME YOUR POUR",
        "MAKE IT A GREAT NIGHT"
    };
    private static readonly string[] JuiGuideBodies =
    {
        "Read the oldest order in BAR SERVICE.\nPick its drink with 1 / 2 / 3, or tap a drink.\nServe guests before their patience runs out.",
        "Pick a drink to start the timing bar.\nPress SPACE or STOP inside the target zone.\nGold earns perfect pours, tips and combos.\nFive clean pours can trigger FEVER for extra tips.",
        "Finish three goals before the 3-minute night ends.\nHire staff, improve facilities or book events.\nManagement menus pause every timer.\nEarn stars and XP, then open the next night."
    };
    private static readonly string[] JuiGuideExamples =
    {
        "MINA: AMBER ALE\n1 AMBER ALE  /  2 NEON FIZZ  /  3 WHISKEY SOUR",
        "CHOOSE DRINK  >  WATCH THE MARKER  >  SPACE / STOP\nAim for gold to keep your combo.",
        "SERVE GUESTS  +  COMPLETE GOALS  +  GROW YOUR BAR\nCash, staff and upgrades carry into the next night."
    };
    private static readonly string[] JuiGuideTitlesKorean =
    {
        "주문 확인하기",
        "타이밍에 맞춰 따르기",
        "멋진 밤 만들기"
    };
    private static readonly string[] JuiGuideBodiesKorean =
    {
        "바 서비스에서 가장 오래된 주문을 확인하세요.\n1 / 2 / 3 키나 음료 버튼으로 알맞은 음료를 고르세요.\n손님의 대기 시간이 끝나기 전에 제공하세요.",
        "음료를 고르면 타이밍 바가 시작됩니다.\n목표 구간에서 SPACE 키나 멈춤 버튼을 누르세요.\n금색 구간을 맞추면 퍼펙트, 팁, 콤보를 얻습니다.\n5회 연속 성공하면 피버로 추가 팁을 얻을 수 있습니다.",
        "3분 안에 오늘의 목표 세 가지를 달성하세요.\n직원 고용, 시설 개선, 이벤트로 보너스를 얻으세요.\n관리 메뉴에서는 모든 타이머가 멈춥니다.\n별과 경험치를 모아 다음 날에도 영업하세요."
    };
    private static readonly string[] JuiGuideExamplesKorean =
    {
        "미나의 주문: 앰버 에일\n1 앰버 에일  /  2 네온 피즈  /  3 위스키 사워",
        "음료 선택  >  표시 확인  >  SPACE / 멈춤\n금색 구간을 맞춰 콤보를 이어 가세요.",
        "손님 응대  +  목표 달성  +  매장 성장\n현금, 직원, 업그레이드는 다음 날에도 유지됩니다."
    };

    private void BuildJourneyUI()
    {
        if (juiBuilt || canvas == null) return;
        foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true))
            if (node.name == "Day 35") { juiDay = node.GetComponent<Text>(); break; }
        BuildJourneyPlayHUD();
        BuildJourneyLobby();
        BuildJourneyGuide();
        BuildJourneyResults();
        ConfigureJourneyPauseMenu();
        juiBuilt = true;
        RefreshJourneyHUD();
        ApplyReadableModalLayout();
    }

    private void BuildJourneyPlayHUD()
    {
        juiHud = OpPanel("Journey Night HUD", canvas.transform, new Vector2(0f, 1f), new Vector2(365f, -117f), new Vector2(510f, 210f), new Color(0.035f, 0.045f, 0.075f, 0.95f), true);
        juiHud.GetComponent<Image>().raycastTarget = false;
        juiNightTitle = OpText("Journey Night Title", juiHud.transform, string.Empty, 19, new Vector2(-75f, 78f), new Vector2(310f, 29f), JuiGold, TextAnchor.MiddleLeft);
        juiClock = OpText("Journey Countdown", juiHud.transform, "03:00", 31, new Vector2(176f, 74f), new Vector2(128f, 42f), Color.white, TextAnchor.MiddleRight);
        juiScore = OpText("Journey Score", juiHud.transform, "SCORE 0", 24, new Vector2(-128f, 43f), new Vector2(215f, 34f), Color.white, TextAnchor.MiddleLeft);
        juiCombo = OpText("Journey Combo", juiHud.transform, "COMBO x0", 20, new Vector2(126f, 44f), new Vector2(215f, 31f), JuiGreen, TextAnchor.MiddleRight);
        juiRank = OpText("Journey Rank", juiHud.transform, string.Empty, 14, new Vector2(-119f, 14f), new Vector2(230f, 25f), JuiMuted, TextAnchor.MiddleLeft);
        juiFever = OpText("Journey Fever", juiHud.transform, "FEVER --", 14, new Vector2(125f, 14f), new Vector2(215f, 25f), JuiGold, TextAnchor.MiddleRight);
        GameObject feverTrack = OpPanel("Journey Fever Track", juiHud.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -3f), new Vector2(466f, 5f), new Color(0.16f, 0.18f, 0.24f), false);
        feverTrack.GetComponent<Image>().raycastTarget = false;
        juiFeverFill = CreatePanel("Journey Fever Fill", feverTrack.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(466f, 5f), JuiGold).GetComponent<Image>();
        juiFeverFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        juiFeverFill.raycastTarget = false;
        for (int i = 0; i < 3; i++)
        {
            float y = -25f - i * 29f;
            juiGoals[i] = OpText("Journey Goal " + i, juiHud.transform, string.Empty, 16, new Vector2(-56f, y), new Vector2(358f, 25f), Color.white, TextAnchor.MiddleLeft);
            GameObject track = OpPanel("Journey Goal Track " + i, juiHud.transform, Vector2.one * 0.5f, new Vector2(182f, y), new Vector2(96f, 8f), new Color(0.16f, 0.18f, 0.24f), false);
            track.GetComponent<Image>().raycastTarget = false;
            juiGoalFills[i] = CreatePanel("Journey Goal Fill " + i, track.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(96f, 8f), JuiGreen).GetComponent<Image>();
            juiGoalFills[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            juiGoalFills[i].raycastTarget = false;
        }
    }

    private void BuildJourneyLobby()
    {
        Transform panel;
        juiLobby = ReadableJourneyOverlay("Journey Lobby", out juiLobbyContent);
        panel = juiLobbyContent;
        OpText("Journey Lobby Title", panel, "SIMBAR", 57, new Vector2(0f, 281f), new Vector2(960f, 78f), JuiGold, TextAnchor.MiddleCenter);
        OpText("Journey Lobby Subtitle", panel, "YOUR BAR. YOUR CROWD. ONE GREAT NIGHT.", 23, new Vector2(0f, 224f), new Vector2(980f, 40f), Color.white, TextAnchor.MiddleCenter);
        juiLobbyProgress = OpText("Journey Lobby Progress", panel, string.Empty, 20, new Vector2(0f, 173f), new Vector2(980f, 35f), JuiMuted, TextAnchor.MiddleCenter);
        Button cozy = OpButton(string.Empty, panel, Vector2.one * 0.5f, new Vector2(-238f, 47f), new Vector2(452f, 160f), 20);
        cozy.name = "Journey Cozy Mode Button";
        juiCozyCard = cozy.GetComponent<Image>();
        OpText("Journey Cozy Title", cozy.transform, "COZY NIGHT", 29, new Vector2(0f, 42f), new Vector2(414f, 42f), JuiGold, TextAnchor.MiddleCenter);
        OpText("Journey Cozy Description", cozy.transform, "3 min / 12 orders / 4 perfects / x5 combo\nLonger guest patience. A gentler pace.", 18, new Vector2(0f, -17f), new Vector2(412f, 66f), Color.white, TextAnchor.MiddleCenter);
        cozy.onClick.AddListener(() => SelectJourneyMode(false));
        Button challenge = OpButton(string.Empty, panel, Vector2.one * 0.5f, new Vector2(238f, 47f), new Vector2(452f, 160f), 20);
        challenge.name = "Journey Challenge Mode Button";
        juiChallengeCard = challenge.GetComponent<Image>();
        OpText("Journey Challenge Title", challenge.transform, "CHALLENGE NIGHT", 29, new Vector2(0f, 42f), new Vector2(414f, 42f), JuiGold, TextAnchor.MiddleCenter);
        OpText("Journey Challenge Description", challenge.transform, "3 min / 20 orders / 7 perfects / x8 combo\nFaster orders. Less time per guest.", 18, new Vector2(0f, -17f), new Vector2(412f, 66f), Color.white, TextAnchor.MiddleCenter);
        challenge.onClick.AddListener(() => SelectJourneyMode(true));
        juiLobbyMode = OpText("Journey Selected Mode", panel, "COZY NIGHT SELECTED", 18, new Vector2(0f, -64f), new Vector2(960f, 34f), JuiGreen, TextAnchor.MiddleCenter);
        juiContinue = OpButton("CONTINUE SAVED NIGHT", panel, Vector2.one * 0.5f, new Vector2(0f, -127f), new Vector2(620f, 55f), 23);
        juiContinue.name = "Journey Continue Button";
        juiContinue.onClick.AddListener(ContinueJourney);
        juiBegin = OpButton("OPEN TONIGHT", panel, Vector2.one * 0.5f, new Vector2(0f, -197f), new Vector2(620f, 59f), 24);
        juiBegin.name = "Journey Begin Button";
        juiBegin.onClick.AddListener(() => JourneyStartNight(juiChallenge));
        OpText("Journey Lobby Carryover", panel, "Your venue, hired staff and upgrades stay with you.", 18, new Vector2(0f, -266f), new Vector2(990f, 32f), JuiMuted, TextAnchor.MiddleCenter);
        OpText("Journey Lobby Controls", panel, SimBarLocalization.Text("1 / 2 / 3 DRINK   |   SPACE / STOP   |   P PAUSE", "1 / 2 / 3 음료 선택   |   SPACE / 멈춤   |   P 일시 정지"), 18, new Vector2(0f, -316f), new Vector2(980f, 42f), new Color(0.68f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
        juiLobbyActions = CreateReadableActionFooter("Journey Lobby", juiLobbyContent, new[] { juiContinue, juiBegin });
        SelectJourneyMode(false);
    }

    private void SelectJourneyMode(bool challenge)
    {
        juiChallenge = challenge;
        if (juiCozyCard != null) juiCozyCard.color = !challenge ? new Color(0.21f, 0.23f, 0.19f) : new Color(0.10f, 0.13f, 0.20f);
        if (juiChallengeCard != null) juiChallengeCard.color = challenge ? new Color(0.25f, 0.18f, 0.15f) : new Color(0.10f, 0.13f, 0.20f);
        if (juiLobbyMode != null) juiLobbyMode.text = challenge
            ? SimBarLocalization.Text("CHALLENGE NIGHT SELECTED  /  HIGHER GOALS", "도전의 밤 선택  /  더 높은 목표")
            : SimBarLocalization.Text("COZY NIGHT SELECTED  /  A GENTLER PACE", "여유로운 밤 선택  /  여유로운 진행");
    }

    private void BuildJourneyGuide()
    {
        Transform panel;
        juiGuide = ReadableJourneyOverlay("Journey Guide", out juiGuideContent);
        panel = juiGuideContent;
        juiGuideScroll = panel.parent.GetComponent<ScrollRect>();
        juiGuideNumber = OpText("Journey Guide Number", panel, "WELCOME / 1 OF 3", 18, new Vector2(0f, 275f), new Vector2(980f, 32f), JuiMuted, TextAnchor.MiddleCenter);
        juiGuideTitle = OpText("Journey Guide Title", panel, string.Empty, 36, new Vector2(0f, 220f), new Vector2(990f, 60f), JuiGold, TextAnchor.MiddleCenter);
        for (int i = 0; i < 3; i++)
        {
            GameObject step = OpPanel("Journey Guide Step " + i, panel, Vector2.one * 0.5f, new Vector2(-115f + i * 115f, 166f), new Vector2(92f, 6f), new Color(0.24f, 0.28f, 0.36f), false);
            juiGuideSteps[i] = step.GetComponent<Image>();
        }
        juiGuideBody = OpText("Journey Guide Body", panel, string.Empty, 23, new Vector2(0f, 67f), new Vector2(970f, 152f), Color.white, TextAnchor.MiddleCenter);
        GameObject example = OpPanel("Journey Guide Example Card", panel, Vector2.one * 0.5f, new Vector2(0f, -97f), new Vector2(954f, 132f), new Color(0.10f, 0.13f, 0.19f), false);
        juiGuideExample = OpText("Journey Guide Example", example.transform, string.Empty, 20, Vector2.zero, new Vector2(914f, 110f), new Color(0.70f, 0.94f, 0.84f), TextAnchor.MiddleCenter);
        Button next = OpButton("NEXT", panel, Vector2.one * 0.5f, new Vector2(0f, -235f), new Vector2(560f, 58f), 24);
        next.name = "Journey Guide Next Button";
        juiGuideNext = next;
        juiGuideNextLabel = next.GetComponentInChildren<Text>();
        next.onClick.AddListener(JourneyAdvanceGuide);
        OpText("Journey Guide Footer", panel, SimBarLocalization.Text("No rush. The night starts after this guide.", "천천히 읽으세요. 안내가 끝나면 영업을 시작합니다."), 18, new Vector2(0f, -292f), new Vector2(980f, 42f), JuiMuted, TextAnchor.MiddleCenter);
        juiGuideActions = CreateReadableActionFooter("Journey Guide", juiGuideContent, new[] { juiGuideNext });
    }

    private void BuildJourneyResults()
    {
        Transform panel;
        juiResults = ReadableJourneyOverlay("Journey Results", out juiResultsContent);
        panel = juiResultsContent;
        juiResultsTitle = OpText("Journey Results Title", panel, "LAST CALL", 39, new Vector2(0f, 333f), new Vector2(1000f, 62f), JuiGold, TextAnchor.MiddleCenter);
        juiResultsRank = OpText("Journey Results Rank", panel, string.Empty, 23, new Vector2(0f, 284f), new Vector2(1000f, 38f), Color.white, TextAnchor.MiddleCenter);
        bool starGlyph = SimBarLocalization.Font.HasCharacter('\u2605');
        for (int i = 0; i < 3; i++)
            juiStars[i] = OpText("Journey Result Star " + i, panel, starGlyph ? "\u2605" : "STAR", starGlyph ? 70 : 31, new Vector2(-115f + i * 115f, 217f), new Vector2(112f, 90f), JuiGold, TextAnchor.MiddleCenter);
        for (int i = 0; i < 3; i++)
        {
            GameObject goal = OpPanel("Journey Results Goal " + i, panel, Vector2.one * 0.5f, new Vector2(0f, 132f - i * 43f), new Vector2(930f, 37f), new Color(0.10f, 0.13f, 0.19f), false);
            juiResultGoals[i] = OpText("Journey Result Goal Label " + i, goal.transform, string.Empty, 20, Vector2.zero, new Vector2(898f, 34f), Color.white, TextAnchor.MiddleLeft);
        }
        juiResultProfit = JourneyResultStat(panel, "NIGHT PROFIT", -315f);
        juiResultServices = JourneyResultStat(panel, "GUESTS SERVED", 0f);
        juiResultXP = JourneyResultStat(panel, "XP EARNED", 315f);
        juiResultRecord = OpText("Journey New Record", panel, string.Empty, 24, new Vector2(0f, -131f), new Vector2(980f, 40f), JuiGreen, TextAnchor.MiddleCenter);
        juiResultProgress = OpText("Journey Result Progress", panel, string.Empty, 18, new Vector2(0f, -174f), new Vector2(980f, 34f), JuiMuted, TextAnchor.MiddleCenter);
        Button retry = OpButton("RETRY THIS NIGHT", panel, Vector2.one * 0.5f, new Vector2(-322f, -257f), new Vector2(304f, 60f), 20);
        retry.name = "Journey Retry Night Button";
        juiResultActions[0] = retry;
        retry.onClick.AddListener(JourneyRetryNight);
        Button next = OpButton("NEXT NIGHT", panel, Vector2.one * 0.5f, new Vector2(0f, -257f), new Vector2(304f, 60f), 23);
        next.name = "Journey Next Night Button";
        juiResultActions[1] = next;
        next.onClick.AddListener(JourneyNextNight);
        Button lobby = OpButton("BACK TO LOBBY", panel, Vector2.one * 0.5f, new Vector2(322f, -257f), new Vector2(304f, 60f), 20);
        lobby.name = "Journey Back To Lobby Button";
        juiResultActions[2] = lobby;
        lobby.onClick.AddListener(JourneyReturnToLobby);
        OpText("Journey Results Saved", panel, SimBarLocalization.Text("SAVED  /  CASH, STAFF AND UPGRADES CARRY OVER", "저장 완료  /  현금, 직원, 업그레이드 유지"), 18, new Vector2(0f, -337f), new Vector2(1000f, 42f), JuiMuted, TextAnchor.MiddleCenter);
        juiResultsActions = CreateReadableActionFooter("Journey Results", juiResultsContent, juiResultActions);
    }

    private Text JourneyResultStat(Transform parent, string title, float x)
    {
        GameObject card = OpPanel("Journey Result " + title, parent, Vector2.one * 0.5f, new Vector2(x, -45f), new Vector2(294f, 110f), new Color(0.10f, 0.13f, 0.19f), false);
        OpText("Journey Result Stat Title", card.transform, title, 17, new Vector2(0f, 28f), new Vector2(278f, 30f), JuiMuted, TextAnchor.MiddleCenter);
        return OpText("Journey Result Stat Value", card.transform, string.Empty, 34, new Vector2(0f, -15f), new Vector2(278f, 49f), JuiGold, TextAnchor.MiddleCenter);
    }

    private void ConfigureJourneyPauseMenu()
    {
        if (pausePanel == null) return;
        SetRect(pausePanel.GetComponent<RectTransform>(), Vector2.one * 0.5f, Vector2.zero, new Vector2(500f, 320f));
        foreach (Text text in pausePanel.GetComponentsInChildren<Text>(true))
        {
            if (text.name == "Pause Title")
                SetRect(text.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 110f), new Vector2(454f, 50f));
            else if (text.name == "Pause Text")
            {
                juiPauseDescription = text;
                text.text = SimBarLocalization.Text("Return to the floor, or save this night for later.", "영업을 계속하거나 저장 후 나중에 이어서 할 수 있습니다.");
                text.fontSize = 17;
                SetRect(text.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 60f), new Vector2(452f, 45f));
            }
        }
        foreach (Button button in pausePanel.GetComponentsInChildren<Button>(true))
            SetRect(button.GetComponent<RectTransform>(), Vector2.one * 0.5f, new Vector2(0f, -10f), new Vector2(380f, 46f));
        juiSaveLobby = OpButton("SAVE & LOBBY", pausePanel.transform, Vector2.one * 0.5f, new Vector2(0f, -78f), new Vector2(380f, 46f), 19);
        juiSaveLobby.name = "Journey Save And Lobby Button";
        juiSaveLobby.onClick.AddListener(JourneyReturnToLobby);
    }

    private GameObject JourneyOverlay(string name, Vector2 size, out Transform panel)
    {
        GameObject overlay = CreatePanel(name, canvas.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, new Color(0.005f, 0.008f, 0.02f, 0.76f));
        RectTransform shade = overlay.GetComponent<RectTransform>();
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = Vector2.zero; shade.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().raycastTarget = true;
        GameObject card = OpPanel(name + " Panel", overlay.transform, Vector2.one * 0.5f, Vector2.zero, size, new Color(0.045f, 0.055f, 0.095f, 1f), true);
        panel = card.transform;
        overlay.SetActive(false);
        return overlay;
    }

    private GameObject ReadableJourneyOverlay(string name, out RectTransform content)
    {
        Transform frame;
        GameObject overlay = JourneyOverlay(name, new Vector2(1540f, 1000f), out frame);
        content = CreateReadableModalContent(name, frame);
        return overlay;
    }

    // Keep modal text at the selected readable size. Content can grow vertically
    // instead of forcing a smaller font when the viewport or language changes.
    private RectTransform CreateReadableModalContent(string name, Transform frame)
    {
        RectTransform frameRect = (RectTransform)frame;
        GameObject viewport = CreatePanel(name + " Viewport", frame, Vector2.one * 0.5f, new Vector2(-8f, 0f),
            frameRect.sizeDelta - new Vector2(72f, 48f), Color.clear);
        viewport.AddComponent<RectMask2D>();
        viewport.GetComponent<Image>().raycastTarget = true;
        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 70f;
        GameObject body = new GameObject(name + " Content", typeof(RectTransform));
        RectTransform content = body.GetComponent<RectTransform>();
        content.SetParent(viewport.transform, false);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = scroll.viewport.sizeDelta - new Vector2(24f, 0f);
        scroll.content = content;

        GameObject track = CreatePanel(name + " Scroll Track", frame, new Vector2(1f, 0.5f), new Vector2(-21f, 0f),
            new Vector2(12f, frameRect.sizeDelta.y - 60f), new Color(0.14f, 0.17f, 0.23f));
        GameObject handle = CreatePanel(name + " Scroll Handle", track.transform, Vector2.one * 0.5f, Vector2.zero,
            new Vector2(12f, 100f), JuiMuted);
        Scrollbar bar = track.AddComponent<Scrollbar>();
        bar.handleRect = handle.GetComponent<RectTransform>();
        bar.targetGraphic = handle.GetComponent<Image>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalNormalizedPosition = 1f;
        return content;
    }

    private RectTransform CreateReadableActionFooter(string name, RectTransform content, Button[] buttons)
    {
        Transform frame = content.parent.parent;
        GameObject footer = CreatePanel(name + " Action Footer", frame, new Vector2(0.5f, 0f), new Vector2(0f, 76f),
            new Vector2(((RectTransform)frame).rect.width - 48f, 104f), new Color(0.045f, 0.055f, 0.095f, 1f));
        foreach (Button button in buttons)
            if (button != null) button.transform.SetParent(footer.transform, false);
        footer.transform.SetAsLastSibling();
        return footer.GetComponent<RectTransform>();
    }

    private void LayoutReadableActionFooter(RectTransform content, RectTransform footer, Button[] buttons)
    {
        if (content == null || footer == null) return;
        RectTransform frame = (RectTransform)footer.parent;
        int count = 0;
        foreach (Button button in buttons) if (button != null && button.gameObject.activeSelf) count++;
        float innerWidth = frame.rect.width - 88f;
        float buttonWidth = count > 1 ? (innerWidth - 20f * (count - 1)) / count : Mathf.Min(1160f, innerWidth);
        float buttonHeight = 82f;
        foreach (Button button in buttons)
            if (button != null && button.gameObject.activeSelf)
                buttonHeight = Mathf.Max(buttonHeight, ReadableButtonHeight(button, buttonWidth, 82f));
        float footerHeight = buttonHeight + 32f;
        SetRect(footer, new Vector2(0.5f, 0f), new Vector2(0f, 24f + footerHeight * 0.5f), new Vector2(frame.rect.width - 48f, footerHeight));
        int index = 0;
        foreach (Button button in buttons)
        {
            if (button == null || !button.gameObject.activeSelf) continue;
            float x = (index - (count - 1) * 0.5f) * (buttonWidth + 20f);
            ReadablePlace(button.transform, x, 16f, buttonWidth, buttonHeight);
            index++;
        }

        // The action panel is a sibling of the masked viewport, so its buttons
        // stay visible and receive pointer events at every scroll position.
        RectTransform viewport = (RectTransform)content.parent;
        float viewportHeight = Mathf.Max(120f, frame.rect.height - footerHeight - 64f);
        float viewportY = footerHeight * 0.5f + 8f;
        SetRect(viewport, Vector2.one * 0.5f, new Vector2(-8f, viewportY), new Vector2(frame.rect.width - 72f, viewportHeight));
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport.rect.width - 24f);
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        if (scroll != null && scroll.verticalScrollbar != null)
            SetRect(scroll.verticalScrollbar.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-21f, viewportY), new Vector2(12f, viewportHeight - 12f));
        footer.SetAsLastSibling();
    }

    /// <summary>Called after the shared readability controller sizes modal fonts.</summary>
    public void ApplyReadableModalLayout()
    {
        if (juiBuilt)
        {
            LayoutReadableJourneyLobby();
            LayoutReadableJourneyGuide();
            LayoutReadableJourneyResults();
        }
        LayoutReadableOperationsModal();
    }

    private static float ReadableTextHeight(Text label, float width, float minimum)
    {
        if (label == null) return minimum;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = false;
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        return Mathf.Max(minimum, Mathf.Ceil(label.preferredHeight) + 16f);
    }

    private void ReadablePlace(Transform node, float x, float top, float width, float height)
    {
        if (node == null) return;
        SetRect((RectTransform)node, new Vector2(0.5f, 1f), new Vector2(x, -top - height * 0.5f), new Vector2(width, height));
    }

    private float ReadableTextRow(Transform parent, string name, ref float top, float width, float minimum, float gap)
    {
        Transform node = parent.Find(name);
        if (node == null) return 0f;
        float height = ReadableTextHeight(node.GetComponent<Text>(), width, minimum);
        ReadablePlace(node, 0f, top, width, height);
        top += height + gap;
        return height;
    }

    private float ReadableButtonHeight(Button button, float width, float minimum)
    {
        if (button == null) return minimum;
        Text label = button.GetComponentInChildren<Text>(true);
        float height = ReadableTextHeight(label, width - 36f, minimum - 20f) + 20f;
        if (label != null) SetRect(label.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(width - 36f, height - 20f));
        return height;
    }

    private static void ReadableContentHeight(RectTransform content, float height)
    {
        float viewportHeight = ((RectTransform)content.parent).rect.height;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewportHeight, height));
    }

    private void LayoutReadableJourneyLobby()
    {
        if (juiLobbyContent == null) return;
        Transform panel = juiLobbyContent;
        float width = juiLobbyContent.rect.width - 24f;
        float top = 12f;
        ReadableTextRow(panel, "Journey Lobby Title", ref top, width, 84f, 4f);
        ReadableTextRow(panel, "Journey Lobby Subtitle", ref top, width, 48f, 12f);
        ReadableTextRow(panel, "Journey Lobby Progress", ref top, width, 58f, 24f);
        float cardWidth = (width - 28f) * 0.5f;
        Text cozyTitle = juiCozyCard.transform.Find("Journey Cozy Title").GetComponent<Text>();
        Text challengeTitle = juiChallengeCard.transform.Find("Journey Challenge Title").GetComponent<Text>();
        Text cozyDescription = juiCozyCard.transform.Find("Journey Cozy Description").GetComponent<Text>();
        Text challengeDescription = juiChallengeCard.transform.Find("Journey Challenge Description").GetComponent<Text>();
        float titleHeight = Mathf.Max(ReadableTextHeight(cozyTitle, cardWidth - 40f, 64f), ReadableTextHeight(challengeTitle, cardWidth - 40f, 64f));
        float descriptionHeight = Mathf.Max(ReadableTextHeight(cozyDescription, cardWidth - 40f, 100f), ReadableTextHeight(challengeDescription, cardWidth - 40f, 100f));
        float cardHeight = titleHeight + descriptionHeight + 44f;
        ReadablePlace(juiCozyCard.transform, -(cardWidth + 28f) * 0.5f, top, cardWidth, cardHeight);
        ReadablePlace(juiChallengeCard.transform, (cardWidth + 28f) * 0.5f, top, cardWidth, cardHeight);
        foreach (Image card in new[] { juiCozyCard, juiChallengeCard })
        {
            string prefix = card == juiCozyCard ? "Journey Cozy " : "Journey Challenge ";
            ReadablePlace(card.transform.Find(prefix + "Title"), 0f, 14f, cardWidth - 40f, titleHeight);
            ReadablePlace(card.transform.Find(prefix + "Description"), 0f, titleHeight + 24f, cardWidth - 40f, descriptionHeight);
        }
        top += cardHeight + 18f;
        ReadableTextRow(panel, "Journey Selected Mode", ref top, width, 48f, 18f);
        ReadableTextRow(panel, "Journey Lobby Carryover", ref top, width, 48f, 8f);
        ReadableTextRow(panel, "Journey Lobby Controls", ref top, width, 48f, 16f);
        LayoutReadableActionFooter(juiLobbyContent, juiLobbyActions, new[] { juiContinue, juiBegin });
        ReadableContentHeight(juiLobbyContent, top);
    }

    private void LayoutReadableJourneyGuide()
    {
        if (juiGuideContent == null) return;
        float width = juiGuideContent.rect.width - 24f;
        float top = 12f;
        ReadableTextRow(juiGuideContent, "Journey Guide Number", ref top, width, 48f, 8f);
        ReadableTextRow(juiGuideContent, "Journey Guide Title", ref top, width, 76f, 20f);
        for (int i = 0; i < juiGuideSteps.Length; i++)
            ReadablePlace(juiGuideSteps[i].transform, -130f + i * 130f, top, 104f, 7f);
        top += 35f;
        ReadableTextRow(juiGuideContent, "Journey Guide Body", ref top, width - 32f, 190f, 24f);
        Transform example = juiGuideContent.Find("Journey Guide Example Card");
        float exampleHeight = ReadableTextHeight(juiGuideExample, width - 80f, 116f) + 32f;
        ReadablePlace(example, 0f, top, width, exampleHeight);
        ReadablePlace(juiGuideExample.transform, 0f, 16f, width - 80f, exampleHeight - 32f);
        top += exampleHeight + 28f;
        ReadableTextRow(juiGuideContent, "Journey Guide Footer", ref top, width, 48f, 20f);
        LayoutReadableActionFooter(juiGuideContent, juiGuideActions, new[] { juiGuideNext });
        ReadableContentHeight(juiGuideContent, top);
    }

    private void LayoutReadableJourneyResults()
    {
        if (juiResultsContent == null) return;
        float width = juiResultsContent.rect.width - 24f;
        float top = 12f;
        ReadableTextRow(juiResultsContent, "Journey Results Title", ref top, width, 80f, 8f);
        ReadableTextRow(juiResultsContent, "Journey Results Rank", ref top, width, 60f, 16f);
        float starHeight = 100f;
        for (int i = 0; i < juiStars.Length; i++) starHeight = Mathf.Max(starHeight, ReadableTextHeight(juiStars[i], 160f, 100f));
        for (int i = 0; i < juiStars.Length; i++) ReadablePlace(juiStars[i].transform, -170f + i * 170f, top, 160f, starHeight);
        top += starHeight + 16f;
        for (int i = 0; i < juiResultGoals.Length; i++)
        {
            float height = ReadableTextHeight(juiResultGoals[i], width - 40f, 58f) + 12f;
            ReadablePlace(juiResultGoals[i].transform.parent, 0f, top, width, height);
            SetRect(juiResultGoals[i].rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(width - 40f, height - 12f));
            top += height + 10f;
        }
        float statWidth = (width - 40f) / 3f;
        float statHeight = 136f;
        Text[] values = { juiResultProfit, juiResultServices, juiResultXP };
        foreach (Text value in values)
        {
            Text title = value.transform.parent.Find("Journey Result Stat Title").GetComponent<Text>();
            statHeight = Mathf.Max(statHeight, ReadableTextHeight(title, statWidth - 28f, 48f) + ReadableTextHeight(value, statWidth - 28f, 60f) + 32f);
        }
        for (int i = 0; i < values.Length; i++)
        {
            Text title = values[i].transform.parent.Find("Journey Result Stat Title").GetComponent<Text>();
            float titleHeight = ReadableTextHeight(title, statWidth - 28f, 48f);
            ReadablePlace(values[i].transform.parent, (i - 1) * (statWidth + 20f), top, statWidth, statHeight);
            ReadablePlace(title.transform, 0f, 10f, statWidth - 28f, titleHeight);
            ReadablePlace(values[i].transform, 0f, titleHeight + 16f, statWidth - 28f, statHeight - titleHeight - 28f);
        }
        top += statHeight + 20f;
        ReadableTextRow(juiResultsContent, "Journey New Record", ref top, width, 56f, 8f);
        ReadableTextRow(juiResultsContent, "Journey Result Progress", ref top, width, 64f, 20f);
        ReadableTextRow(juiResultsContent, "Journey Results Saved", ref top, width, 52f, 20f);
        LayoutReadableActionFooter(juiResultsContent, juiResultsActions, juiResultActions);
        ReadableContentHeight(juiResultsContent, top);
    }

    private void RefreshJourneyHUD()
    {
        if (!juiBuilt) return;
        bool visible = !JourneyValidationMode;
        juiHud.SetActive(visible && JourneyIsPlaying);
        JourneySetOverlay(juiLobby, visible && JourneyIsLobby);
        JourneySetOverlay(juiGuide, visible && JourneyIsGuide);
        JourneySetOverlay(juiResults, visible && JourneyIsResults);
        if (juiSaveLobby != null) juiSaveLobby.gameObject.SetActive(visible);
        if (!visible) return;

        if (juiPauseDescription != null) juiPauseDescription.text = SimBarLocalization.Text("Return to the floor, or save this night for later.", "영업을 계속하거나 저장 후 나중에 이어서 할 수 있습니다.");
        if (juiDay != null) juiDay.text = SimBarLocalization.Format("Day {0}", "{0}일 차", JourneyDay);
        juiNightTitle.text = SimBarLocalization.Format("DAY {0}  /  {1}", "{0}일 차  /  {1}", JourneyDay, JourneyModeLabel.ToUpperInvariant());
        juiClock.text = OpTime(JourneySecondsLeft);
        juiClock.color = JourneySecondsLeft <= 30f ? new Color(1f, 0.55f, 0.40f) : Color.white;
        juiScore.text = SimBarLocalization.Text("SCORE ", "점수 ") + JourneyScore.ToString("N0");
        juiCombo.text = SimBarLocalization.Text("COMBO x", "콤보 x") + JourneyCombo;
        juiRank.text = SimBarLocalization.Format("RANK {0}  /  {1} XP", "등급 {0}  /  경험치 {1}", JourneyRankLabel, JourneyXP.ToString("N0"));
        bool fever = JourneyFeverSeconds > 0f;
        juiFever.text = fever
            ? SimBarLocalization.Format("FEVER  {0}s", "피버  {0}초", Mathf.CeilToInt(JourneyFeverSeconds))
            : SimBarLocalization.Format("FEVER CHARGE  {0}%", "피버 충전  {0}%", Mathf.RoundToInt(JourneyFever * 100f));
        juiFever.color = fever ? JuiGold : JuiMuted;
        juiFeverFill.rectTransform.sizeDelta = new Vector2(466f * Mathf.Clamp01(JourneyFever), 5f);
        for (int i = 0; i < 3; i++)
        {
            float progress = Mathf.Clamp01(JourneyGoalProgress(i));
            bool complete = progress >= 0.999f;
            juiGoals[i].text = (complete ? SimBarLocalization.Text("DONE  ", "완료  ") : (i + 1) + ".  ") + JourneyGoalLabel(i);
            juiGoals[i].color = complete ? JuiGreen : Color.white;
            juiGoalFills[i].rectTransform.sizeDelta = new Vector2(96f * progress, 8f);
            juiResultGoals[i].text = (complete ? SimBarLocalization.Text("DONE   ", "완료   ") : SimBarLocalization.Text("MISSED   ", "미달성   ")) + JourneyGoalLabel(i);
            juiResultGoals[i].color = complete ? JuiGreen : JuiMuted;
            juiStars[i].color = i < JourneyStars ? JuiGold : new Color(0.25f, 0.29f, 0.37f);
        }
        if (JourneyIsLobby)
        {
            juiLobbyProgress.text = SimBarLocalization.Format("DAY {0}    /    RANK {1}    /    {2} XP    /    BEST {3}", "{0}일 차    /    등급 {1}    /    경험치 {2}    /    최고 점수 {3}", JourneyDay, JourneyRankLabel, JourneyXP.ToString("N0"), JourneyBestScore.ToString("N0"));
            juiContinue.interactable = JourneyHasSave;
            juiContinue.gameObject.SetActive(JourneyHasSave);
            juiBegin.gameObject.SetActive(!JourneyHasActiveNight);
            juiCozyCard.GetComponent<Button>().interactable = !JourneyHasActiveNight;
            juiChallengeCard.GetComponent<Button>().interactable = !JourneyHasActiveNight;
            Text continueLabel = juiContinue.GetComponentInChildren<Text>();
            if (continueLabel != null) continueLabel.text = JourneyHasActiveNight
                ? SimBarLocalization.Text("CONTINUE SAVED NIGHT  /  ", "저장한 영업 이어하기  /  ") + OpTime(JourneySecondsLeft)
                : SimBarLocalization.Text("REVIEW LAST NIGHT", "지난 영업 결과 보기");
            if (JourneyHasActiveNight)
            {
                SelectJourneyMode(JourneyChallenge);
                juiLobbyMode.text = SimBarLocalization.Format("SAVED {0} NIGHT  /  CONTINUE WHERE YOU LEFT OFF", "저장된 {0} 모드  /  중단한 지점부터 이어서 시작", JourneyModeLabel);
            }
            else SelectJourneyMode(juiChallenge);
            LayoutReadableJourneyLobby();
        }
        if (JourneyIsGuide && (juiGuideShown != JourneyGuideStep || juiGuideShownKorean != SimBarLocalization.IsKorean))
        {
            int step = Mathf.Clamp(JourneyGuideStep, 0, 2);
            juiGuideShown = JourneyGuideStep;
            juiGuideShownKorean = SimBarLocalization.IsKorean;
            juiGuideNumber.text = SimBarLocalization.Format("WELCOME  /  {0} OF 3", "처음 오셨나요?  /  {0} / 3", step + 1);
            juiGuideTitle.text = SimBarLocalization.Text(JuiGuideTitles[step], JuiGuideTitlesKorean[step]);
            juiGuideBody.text = SimBarLocalization.Text(JuiGuideBodies[step], JuiGuideBodiesKorean[step]);
            juiGuideExample.text = SimBarLocalization.Text(JuiGuideExamples[step], JuiGuideExamplesKorean[step]);
            juiGuideNextLabel.text = step == 2 ? SimBarLocalization.Text("OPEN THE DOORS", "영업 시작") : SimBarLocalization.Text("NEXT", "다음");
            for (int i = 0; i < 3; i++) juiGuideSteps[i].color = i <= step ? JuiGold : new Color(0.24f, 0.28f, 0.36f);
            LayoutReadableJourneyGuide();
            if (juiGuideScroll != null) juiGuideScroll.verticalNormalizedPosition = 1f;
        }
        if (JourneyIsResults)
        {
            juiResultsTitle.text = SimBarLocalization.Format("DAY {0}  /  LAST CALL", "{0}일 차  /  영업 마감", JourneyDay);
            juiResultsRank.text = SimBarLocalization.Format("{0} POINTS    /    BEST COMBO x{1}    /    {2} PERFECT POURS", "{0}점    /    최고 콤보 x{1}    /    퍼펙트 {2}회", JourneyScore.ToString("N0"), JourneyBestCombo, JourneyPerfects);
            juiResultProfit.text = JourneyResultProfit.ToString("+$#,##0;-$#,##0;$0");
            juiResultServices.text = JourneyResultServed.ToString("N0");
            juiResultXP.text = "+" + JourneyResultXP.ToString("N0");
            juiResultRecord.text = JourneyResultNewRecord ? SimBarLocalization.Text("NEW HIGH SCORE!", "최고 점수 경신!") : SimBarLocalization.Text("BEST SCORE  ", "최고 점수  ") + JourneyBestScore.ToString("N0");
            juiResultProgress.text = SimBarLocalization.Format("NIGHT BONUS +${0}    /    RANK {1}    /    TOTAL XP {2}", "영업 보너스 +${0}    /    등급 {1}    /    총 경험치 {2}", JourneyResultBonus.ToString("N0"), JourneyRankLabel, JourneyXP.ToString("N0"));
            LayoutReadableJourneyResults();
        }
    }

    private static void JourneySetOverlay(GameObject overlay, bool active)
    {
        bool opening = active && !overlay.activeSelf;
        if (overlay.activeSelf != active) overlay.SetActive(active);
        if (opening)
            foreach (ScrollRect scroll in overlay.GetComponentsInChildren<ScrollRect>(true)) scroll.verticalNormalizedPosition = 1f;
        if (active) overlay.transform.SetAsLastSibling();
    }
}
