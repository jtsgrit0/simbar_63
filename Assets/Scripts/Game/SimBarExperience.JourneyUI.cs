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

    private static readonly Color JuiGold = new Color(0.97f, 0.81f, 0.43f, 1f);
    private static readonly Color JuiGreen = new Color(0.55f, 0.93f, 0.58f, 1f);
    private static readonly Color JuiMuted = new Color(0.70f, 0.77f, 0.87f, 1f);
    private static readonly string[] JuiGuideTitles =
    {
        "READ THE ORDER",
        "TIME YOUR SERVICE",
        "MAKE IT A GREAT NIGHT"
    };
    private static readonly string[] JuiGuideBodies =
    {
        "The first guest's order appears in BAR SERVICE.\nPress M or click MENU to choose an item, or use keys 1–5.\nPress E or PREPARE ORDER to match the oldest order.",
        "Choosing a menu item starts the timing bar.\nPour cocktails or plate food on the beat: press SPACE or STOP at the target.\nAccurate service earns tips and builds your combo.\nBuild a clean streak to ignite FEVER for bonus tips.",
        "Complete tonight's three goals before last call.\nHire staff, upgrade your venue and book events for useful bonuses.\nManagement menus pause the timers. Earn stars and XP, then open the next night."
    };
    private static readonly string[] JuiGuideExamples =
    {
        "CHECK THE ORDER     >     MENU / 1–5     >     CHOOSE AN ITEM\n\nUse E / PREPARE ORDER to prepare the oldest guest's requested item.",
        "CHOOSE AN ITEM     >     WATCH THE MARKER     >     SPACE / STOP\n\nGood service keeps the crowd happy. Perfect service keeps the combo alive.",
        "SERVE YOUR GUESTS     +     HIT YOUR GOALS     +     GROW YOUR VENUE\n\nYour cash, hired staff and facility upgrades carry into the next night."
    };
    private static readonly string[] JuiGuideTitlesKorean =
    {
        "주문 확인하기",
        "타이밍에 맞춰 서빙하기",
        "멋진 밤 만들기"
    };
    private static readonly string[] JuiGuideBodiesKorean =
    {
        "첫 손님의 주문이 바 서비스에 표시됩니다.\nM 키나 메뉴 버튼을 눌러 메뉴를 고르거나 1–5 키를 누르세요.\nE 키나 주문 메뉴 준비 버튼으로 가장 오래된 주문을 준비하세요.",
        "메뉴를 고르면 타이밍 바가 시작됩니다.\n음료 따르기와 푸드 플레이팅 모두 목표 구간에서 SPACE 키나 멈춤 버튼을 누르세요.\n정확하게 서빙하면 팁과 콤보를 얻습니다.\n실수 없이 연속으로 성공하면 피버가 발동해 팁이 늘어납니다.",
        "영업이 끝나기 전에 오늘의 목표 세 가지를 달성하세요.\n직원 고용, 매장 업그레이드, 이벤트 예약으로 보너스를 얻을 수 있습니다.\n관리 메뉴에서는 시간이 멈춥니다. 별과 경험치를 모아 다음 날에도 영업하세요."
    };
    private static readonly string[] JuiGuideExamplesKorean =
    {
        "주문 확인     >     메뉴 / 1–5     >     메뉴 선택\n\nE 키나 주문 메뉴 준비 버튼으로 가장 오래된 손님의 주문을 준비하세요.",
        "메뉴 선택     >     표시 확인     >     SPACE / 멈춤\n\n잘 서빙하면 손님이 만족하고, 완벽하게 서빙하면 콤보가 이어집니다.",
        "손님 응대     +     목표 달성     +     매장 성장\n\n보유 현금, 고용한 직원, 시설 업그레이드는 다음 날에도 유지됩니다."
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
        BuildBookingUI();
        RefreshJourneyHUD();
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
        juiLobby = JourneyOverlay("Journey Lobby", new Vector2(1480f, 900f), out panel);
        BuildOwnerTitleBackdrop(panel);
        OpText("Journey Lobby Eyebrow", panel, SimBarLocalization.Text("SEOUL AFTER DARK  /  LIVE HOUSE STORIES", "서울의 밤  /  라이브하우스 스토리"), 17, new Vector2(0f, 417f), new Vector2(1340f, 26f), new Color(0.39f, 0.91f, 0.94f), TextAnchor.MiddleCenter);
        Text title = OpText("Journey Lobby Title", panel, "SIMBAR", 72, new Vector2(0f, 370f), new Vector2(1340f, 88f), Color.white, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        OpText("Journey Lobby Subtitle", panel, SimBarLocalization.Text("CHOOSE YOUR OWNER. TAKE THE NIGHT.", "사장을 선택하고, 오늘의 밤을 시작하세요."), 25, new Vector2(0f, 312f), new Vector2(1320f, 42f), JuiGold, TextAnchor.MiddleCenter);
        BuildOwnerSelection(panel);
        Button cozy = OpButton(string.Empty, panel, Vector2.one * 0.5f, new Vector2(-227f, -268f), new Vector2(440f, 76f), 20);
        cozy.name = "Journey Cozy Mode Button";
        juiCozyCard = cozy.GetComponent<Image>();
        OpText("Journey Cozy Title", cozy.transform, "COZY NIGHT", 23, new Vector2(0f, 16f), new Vector2(414f, 32f), JuiGold, TextAnchor.MiddleCenter);
        OpText("Journey Cozy Description", cozy.transform, SimBarLocalization.Text("3 MIN  /  RELAXED ORDERS", "3분  /  여유로운 주문과 대기 시간"), 16, new Vector2(0f, -17f), new Vector2(412f, 27f), Color.white, TextAnchor.MiddleCenter);
        cozy.onClick.AddListener(() => SelectJourneyMode(false));
        Button challenge = OpButton(string.Empty, panel, Vector2.one * 0.5f, new Vector2(227f, -268f), new Vector2(440f, 76f), 20);
        challenge.name = "Journey Challenge Mode Button";
        juiChallengeCard = challenge.GetComponent<Image>();
        OpText("Journey Challenge Title", challenge.transform, "CHALLENGE NIGHT", 23, new Vector2(0f, 16f), new Vector2(414f, 32f), JuiGold, TextAnchor.MiddleCenter);
        OpText("Journey Challenge Description", challenge.transform, SimBarLocalization.Text("3 MIN  /  FASTER ORDERS, HIGHER GOALS", "3분  /  빠른 주문과 더 높은 목표"), 16, new Vector2(0f, -17f), new Vector2(412f, 27f), Color.white, TextAnchor.MiddleCenter);
        challenge.onClick.AddListener(() => SelectJourneyMode(true));
        juiLobbyMode = OpText("Journey Selected Mode", panel, "COZY NIGHT SELECTED", 17, new Vector2(0f, -326f), new Vector2(1320f, 28f), JuiGreen, TextAnchor.MiddleCenter);
        juiContinue = OpButton("CONTINUE SAVED NIGHT", panel, Vector2.one * 0.5f, new Vector2(-283f, -369f), new Vector2(546f, 56f), 22);
        juiContinue.name = "Journey Continue Button";
        juiContinue.onClick.AddListener(ContinueJourney);
        juiBegin = OpButton("OPEN TONIGHT", panel, Vector2.one * 0.5f, new Vector2(283f, -369f), new Vector2(546f, 56f), 23);
        juiBegin.name = "Journey Begin Button";
        juiBegin.GetComponent<Image>().color = new Color(0.22f, 0.44f, 0.38f);
        juiBegin.onClick.AddListener(() => JourneyStartNight(juiChallenge));
        juiLobbyProgress = OpText("Journey Lobby Progress", panel, string.Empty, 17, new Vector2(0f, -422f), new Vector2(1360f, 29f), JuiMuted, TextAnchor.MiddleCenter);
        SimBarOwnerTitleLayout.Attach(panel.GetComponent<RectTransform>());
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
        juiGuide = JourneyOverlay("Journey Guide", new Vector2(1080f, 650f), out panel);
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
        juiGuideNextLabel = next.GetComponentInChildren<Text>();
        next.onClick.AddListener(JourneyAdvanceGuide);
        OpText("Journey Guide Footer", panel, "Take your time. The doors open after this guide.", 16, new Vector2(0f, -292f), new Vector2(980f, 30f), JuiMuted, TextAnchor.MiddleCenter);
    }

    private void BuildJourneyResults()
    {
        Transform panel;
        juiResults = JourneyOverlay("Journey Results", new Vector2(1080f, 780f), out panel);
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
        juiResultProfit.resizeTextForBestFit = true;
        juiResultProfit.resizeTextMinSize = 20;
        juiResultProfit.resizeTextMaxSize = 34;
        juiResultProfit.verticalOverflow = VerticalWrapMode.Truncate;
        juiResultServices = JourneyResultStat(panel, "GUESTS SERVED", 0f);
        juiResultXP = JourneyResultStat(panel, "XP EARNED", 315f);
        juiResultRecord = OpText("Journey New Record", panel, string.Empty, 24, new Vector2(0f, -131f), new Vector2(980f, 40f), JuiGreen, TextAnchor.MiddleCenter);
        juiResultProgress = OpText("Journey Result Progress", panel, string.Empty, 18, new Vector2(0f, -174f), new Vector2(980f, 34f), JuiMuted, TextAnchor.MiddleCenter);
        Button retry = OpButton(SimBarLocalization.Text("PLAN THIS DAY AGAIN", "이날 다시 기획"), panel, Vector2.one * 0.5f, new Vector2(-322f, -257f), new Vector2(304f, 60f), 20);
        retry.name = "Journey Retry Night Button";
        retry.onClick.AddListener(JourneyRetryNight);
        Button next = OpButton(SimBarLocalization.Text("PLAN NEXT DAY", "다음 날 낮 기획"), panel, Vector2.one * 0.5f, new Vector2(0f, -257f), new Vector2(304f, 60f), 23);
        next.name = "Journey Next Night Button";
        next.onClick.AddListener(JourneyNextNight);
        Button lobby = OpButton("BACK TO LOBBY", panel, Vector2.one * 0.5f, new Vector2(322f, -257f), new Vector2(304f, 60f), 20);
        lobby.name = "Journey Back To Lobby Button";
        lobby.onClick.AddListener(JourneyReturnToLobby);
        OpText("Journey Results Saved", panel, "PROGRESS SAVED  /  YOUR VENUE CARRIES INTO THE NEXT NIGHT", 15, new Vector2(0f, -337f), new Vector2(1000f, 32f), JuiMuted, TextAnchor.MiddleCenter);
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

    private void RefreshJourneyHUD()
    {
        if (!juiBuilt) return;
        RefreshBookingUI();
        bool visible = !JourneyValidationMode;
        // The opaque Seoul title covers the venue. Avoid drawing/animating the
        // hidden 3D scene (and compiling its shaders) on the first title frame.
        if (worldRoot != null)
        {
            bool showVenue = !visible || (!JourneyIsPlanning && (!JourneyIsLobby || !ownerTitleHasFullscreenBackdrop));
            if (worldRoot.gameObject.activeSelf != showVenue)
                worldRoot.gameObject.SetActive(showVenue);
        }
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
        juiFever.fontSize = fever ? 17 : 14;
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
            RefreshOwnerSelection();
            juiLobbyProgress.text = SimBarLocalization.Format("DAY {0}    /    RANK {1}    /    {2} XP    /    BEST {3}", "{0}일 차    /    등급 {1}    /    경험치 {2}    /    최고 점수 {3}", JourneyDay, JourneyRankLabel, JourneyXP.ToString("N0"), JourneyBestScore.ToString("N0"));
            juiContinue.interactable = JourneyHasSave;
            juiContinue.gameObject.SetActive(JourneyHasSave);
            juiBegin.gameObject.SetActive(!(JourneyHasActiveNight || JourneyHasActivePlan));
            juiCozyCard.GetComponent<Button>().interactable = !(JourneyHasActiveNight || JourneyHasActivePlan);
            juiChallengeCard.GetComponent<Button>().interactable = !(JourneyHasActiveNight || JourneyHasActivePlan);
            juiBegin.GetComponent<RectTransform>().anchoredPosition = new Vector2(JourneyHasSave ? 283f : 0f, -369f);
            juiContinue.GetComponent<RectTransform>().anchoredPosition = new Vector2((JourneyHasActiveNight || JourneyHasActivePlan) ? 0f : -283f, -369f);
            juiBegin.GetComponentInChildren<Text>().text = SimBarLocalization.Text("PLAN THE DAY / BOOK BANDS", "낮 공연 기획·섭외 시작");
            Text continueLabel = juiContinue.GetComponentInChildren<Text>();
            if (continueLabel != null) continueLabel.text = JourneyHasActivePlan
                ? SimBarLocalization.Text("CONTINUE DAY PLANNING", "낮 기획·섭외 이어하기")
                : JourneyHasActiveNight
                ? SimBarLocalization.Text("CONTINUE SAVED NIGHT  /  ", "저장한 영업 이어하기  /  ") + OpTime(JourneySecondsLeft)
                : SimBarLocalization.Text("REVIEW LAST NIGHT", "지난 영업 결과 보기");
            if (JourneyHasActiveNight || JourneyHasActivePlan)
            {
                SelectJourneyMode(JourneyChallenge);
                juiLobbyMode.text = SimBarLocalization.Format("SAVED {0} NIGHT  /  CONTINUE WHERE YOU LEFT OFF", "저장된 {0} 모드  /  중단한 지점부터 이어서 시작", JourneyModeLabel);
            }
            else SelectJourneyMode(juiChallenge);
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
        }
        if (JourneyIsResults)
        {
            juiResultsTitle.text = SimBarLocalization.Format("DAY {0}  /  LAST CALL", "{0}일 차  /  영업 마감", JourneyDay);
            juiResultsRank.text = SimBarLocalization.Format("{0} POINTS    /    BEST COMBO x{1}    /    {2} PERFECT POURS", "{0}점    /    최고 콤보 x{1}    /    퍼펙트 {2}회", JourneyScore.ToString("N0"), JourneyBestCombo, JourneyPerfects);
            juiResultProfit.text = SimBarMoney.Format(JourneyResultProfit, true);
            juiResultServices.text = JourneyResultServed.ToString("N0");
            juiResultXP.text = "+" + JourneyResultXP.ToString("N0");
            juiResultRecord.text = JourneyResultNewRecord ? SimBarLocalization.Text("NEW HIGH SCORE!", "최고 점수 경신!") : SimBarLocalization.Text("BEST SCORE  ", "최고 점수  ") + JourneyBestScore.ToString("N0");
            juiResultProgress.text = SimBarLocalization.Format("BONUS {0} / TICKETS {1} / {2} GUESTS / {3} XP", "보너스 {0} / 티켓 {1} / 관객 {2}명 / 경험치 {3}", SimBarMoney.Format(JourneyResultBonus, true), SimBarMoney.Format(journey.resultTicketIncome), journey.resultAttendance, JourneyXP.ToString("N0"));
        }
    }

    private static void JourneySetOverlay(GameObject overlay, bool active)
    {
        if (overlay.activeSelf != active) overlay.SetActive(active);
        if (active) overlay.transform.SetAsLastSibling();
    }
}
