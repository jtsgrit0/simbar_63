using System;
using UnityEngine;

/// <summary>
/// A resumable three-minute service night. This state is embedded in the same
/// operations save as the wallet, so settlement receipts and cash commit together.
/// </summary>
public sealed partial class SimBarExperience
{
    private const float JourneyNightDuration = 180f;
    private enum JourneyPhase { Lobby, Guide, Playing, Results, Planning, FloorClosing }

    [Serializable]
    private sealed class JourneyData
    {
        public int version = 1;
        public int day = 1;
        public bool challenge;
        public bool tutorialComplete;
        public int guideStep;
        public bool guidePending;
        public bool inProgress;
        public int session;
        public int settledSession;
        public bool settlementPending;
        public float secondsLeft = JourneyNightDuration;
        public int score;
        public int combo;
        public int bestCombo;
        public int manualPerfects;
        public int orders;
        public int mistakes;
        public int successfulEvents;
        public int manualStreak;
        public float feverSeconds;
        public float feverCharge;
        public float openingCash;
        public float drinkIncome;
        public int xp;
        public int bestScore;
        public int bestStars;
        public int completedNights;
        public int resultStars;
        public int resultServed;
        public int resultXP;
        public int resultBonus;
        public float resultProfit;
        public bool resultNewRecord;
        public SimBarBooking.Plan booking;
        public SimBarBooking.Career bookingCareer = SimBarBooking.NewCareer();
        public float dayOpeningCash;
        public bool hasDayOpeningCash;
        public float resultTicketIncome;
        public int resultAttendance;
        public SimBarFloorMissions.State floorMissions = SimBarFloorMissions.NewState();
        public bool floorClosing;
        public float resultFloorBonus;
        public int resultFloorTasks;
    }

    private JourneyData journey = new JourneyData();
    private JourneyPhase journeyPhase;
    private bool journeyInitialized;
    private bool journeyClosing;
    // Used only by the project's legacy operation diagnostics. It never saves.
    private bool journeyValidationMode;
    private float journeyAutosaveTimer;

    public bool JourneyValidationMode { get { return journeyValidationMode; } }
    public float JourneyProfitForHUD
    {
        get
        {
            if (journey == null) return 0f;
            return journey.inProgress ? JourneyClamp(cash - journey.openingCash, 0f, -1000000000f, 1000000000f)
                : journey.settledSession > 0 ? journey.resultProfit : 0f;
        }
    }
    private bool JourneyIsPlaying { get { return journeyValidationMode || (journeyInitialized && journeyPhase == JourneyPhase.Playing && journey.inProgress); } }
    private bool JourneyCanManage { get { return journeyValidationMode || JourneyIsPlaying; } }
    private bool JourneyIsLobby { get { return journeyPhase == JourneyPhase.Lobby; } }
    private bool JourneyIsGuide { get { return journeyPhase == JourneyPhase.Guide; } }
    private bool JourneyIsResults { get { return journeyPhase == JourneyPhase.Results; } }
    private bool JourneyHasSave { get { return JourneyHasActivePlan || journey.inProgress || journey.settledSession > 0; } }
    private bool JourneyHasActiveNight { get { return journey.inProgress; } }
    private int JourneyGuideStep { get { return journey.guideStep; } }
    private int JourneyDay { get { return journey.day; } }
    private float JourneySecondsLeft { get { return journey.secondsLeft; } }
    private float JourneyDuration { get { return JourneyNightDuration; } }
    private int JourneyScore { get { return journey.score; } }
    private int JourneyCombo { get { return journey.combo; } }
    private int JourneyBestCombo { get { return journey.bestCombo; } }
    private int JourneyPerfects { get { return journey.manualPerfects; } }
    private int JourneyOrders { get { return journey.orders; } }
    private int JourneyStars { get { return journey.resultStars; } }
    private int JourneyXP { get { return journey.xp; } }
    private int JourneyBestScore { get { return journey.bestScore; } }
    private float JourneyFeverSeconds { get { return journey.feverSeconds; } }
    private float JourneyFever { get { return journey.feverSeconds > 0f ? journey.feverSeconds / 12f : journey.feverCharge; } }
    private float JourneyResultProfit { get { return journey.resultProfit; } }
    private int JourneyResultServed { get { return journey.resultServed; } }
    private int JourneyResultXP { get { return journey.resultXP; } }
    private bool JourneyResultNewRecord { get { return journey.resultNewRecord; } }
    private int JourneyResultBonus { get { return journey.resultBonus; } }
    private bool JourneyChallenge { get { return journey.challenge; } }
    private string JourneyModeLabel { get { return journey.challenge ? SimBarLocalization.Text("CHALLENGE", "도전") : SimBarLocalization.Text("COZY", "여유"); } }
    private string JourneyPhaseLabel
    {
        get
        {
            if (JourneyIsGuide) return SimBarLocalization.Text("YOUR FIRST SOUND CHECK", "첫 영업 준비");
            if (JourneyIsResults) return SimBarLocalization.Text("NIGHT COMPLETE", "오늘의 영업 완료");
            if (JourneyIsFloorClosing) return SimBarLocalization.Text("AFTER-SHOW FLOOR MISSIONS", "공연 후 홀 정리");
            return JourneyIsPlaying ? SimBarLocalization.Format("DAY {0} / {1}", "{0}일 차 / {1}", journey.day, JourneyModeLabel) : SimBarLocalization.Text("WELCOME TO SIMBAR", "심바에 오신 것을 환영합니다");
        }
    }
    private string JourneyRankLabel
    {
        get
        {
            int xp = journey.xp;
            if (xp >= 3000) return SimBarLocalization.Text("HOUSE LEGEND", "바의 전설");
            if (xp >= 1600) return SimBarLocalization.Text("HEADLINER", "최고의 스타");
            if (xp >= 850) return SimBarLocalization.Text("CROWD FAVORITE", "손님들의 인기 스타");
            if (xp >= 400) return SimBarLocalization.Text("LOCAL HERO", "동네의 영웅");
            if (xp >= 150) return SimBarLocalization.Text("RISING STAR", "떠오르는 스타");
            return SimBarLocalization.Text("OPENING ACT", "새내기 바텐더");
        }
    }
    private float JourneyOrderIntervalMultiplier { get { return journeyValidationMode ? 1f : journey.challenge ? 0.72f : 1.18f; } }
    private float JourneyPatienceBonus { get { return journeyValidationMode ? 0f : journey.challenge ? -12f : 18f; } }
    private float JourneyTipMultiplier { get { return !journeyValidationMode && JourneyIsPlaying && journey.feverSeconds > 0f ? 1.75f : 1f; } }

    private int JourneyGoalTarget(int index)
    {
        if (index == 0) return journey.challenge ? 20 : 12;
        if (index == 1) return journey.challenge ? 7 : 4;
        return journey.challenge ? 8 : 5;
    }

    private int JourneyGoalValue(int index)
    {
        if (index == 0) return journey.orders;
        if (index == 1) return journey.manualPerfects;
        return journey.bestCombo;
    }

    private float JourneyGoalProgress(int index)
    {
        if (index < 0 || index > 2) return 0f;
        return Mathf.Clamp01(JourneyGoalValue(index) / (float)JourneyGoalTarget(index));
    }

    private string JourneyGoalLabel(int index)
    {
        if (index < 0 || index > 2) return string.Empty;
        string label = index == 0 ? SimBarLocalization.Text("SERVE ORDERS", "주문 제공") : index == 1 ? SimBarLocalization.Text("MANUAL PERFECTS", "직접 따르기 퍼펙트") : SimBarLocalization.Text("BEST COMBO", "최고 콤보");
        return label + "  " + JourneyGoalValue(index) + " / " + JourneyGoalTarget(index);
    }

    private void InitializeJourney()
    {
        if (journeyInitialized) return;
        SanitizeJourney();
        journeyInitialized = true;
        // A saved night never starts running behind the title screen. Recovery
        // only finishes an already-committed closing receipt, then shows lobby.
        if (journey.settlementPending) SettleJourneyNight();
        journeyPhase = JourneyPhase.Lobby;
        paused = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        BuildJourneyUI();
        RefreshJourneyHUD();
    }

    private void TickJourney(float delta)
    {
        if (journeyValidationMode || !JourneyIsPlaying || paused || !JourneyFinite(delta) || delta <= 0f) return;
        delta = Mathf.Min(delta, JourneyNightDuration);
        journey.secondsLeft = Mathf.Max(0f, journey.secondsLeft - delta);
        journey.feverSeconds = Mathf.Max(0f, journey.feverSeconds - delta);
        journeyAutosaveTimer += delta;
        if (journey.secondsLeft <= 0f)
        {
            FinishJourneyNight();
            return;
        }
        if (journeyAutosaveTimer >= 5f)
        {
            journeyAutosaveTimer = 0f;
            SaveOperations();
        }
        RefreshJourneyHUD();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus || !journeyInitialized || journeyValidationMode || (!JourneyIsPlaying && !JourneyIsFloorClosing) || paused) return;
        // A real-time shift should not expire while the player uses another tab.
        CloseFloorMissionDrawer();
        paused = true;
        if (pausePanel != null) pausePanel.SetActive(true);
        TickPourGame(0f);
        SaveOperations();
    }

    private void JourneyRecordServe(bool automatic, float quality, float income)
    {
        if (journeyValidationMode || !JourneyIsPlaying || paused) return;
        quality = JourneyClamp(quality, 0f, 0f, 1f);
        income = JourneyClamp(income, 0f, 0f, 100000f);
        journey.orders = Mathf.Min(100000, journey.orders + 1);
        journey.drinkIncome = Mathf.Min(100000000f, journey.drinkIncome + income);
        if (!automatic)
        {
            if (quality < 0.5f)
            {
                JourneyRecordMistake();
                return;
            }
            journey.combo = Mathf.Min(100000, journey.combo + 1);
            journey.bestCombo = Mathf.Max(journey.bestCombo, journey.combo);
            bool perfect = quality >= 0.90f;
            if (perfect) journey.manualPerfects = Mathf.Min(100000, journey.manualPerfects + 1);
            journey.manualStreak = Mathf.Min(100000, journey.manualStreak + 1);
            float multiplier = 1f + Mathf.Min(20, journey.combo - 1) * 0.05f;
            if (journey.feverSeconds > 0f) multiplier *= 1.30f;
            journey.score = Mathf.Min(100000000, journey.score + Mathf.RoundToInt((70f + 130f * quality) * multiplier));
            journey.feverCharge = Mathf.Clamp01(journey.manualStreak / 5f);
            if (journey.feverSeconds <= 0f && journey.manualStreak >= 5 && performance >= 0.72f)
            {
                journey.feverSeconds = 12f;
                journey.manualStreak = 0;
                journey.feverCharge = 0f;
                ShowToast(SimBarLocalization.Text("FEVER!  12 SEC / +75% TIPS / SCORE x1.3", "피버!  12초 / 팁 +75% / 점수 x1.3"), 3f);
            }
        }
        RefreshJourneyHUD();
        // The service transaction saves wallet and this record together.
    }

    private void JourneyRecordMistake()
    {
        if (journeyValidationMode || !JourneyIsPlaying || paused) return;
        journey.mistakes = Mathf.Min(100000, journey.mistakes + 1);
        journey.combo = 0;
        journey.manualStreak = 0;
        journey.feverCharge = 0f;
        journey.feverSeconds = 0f;
        RefreshJourneyHUD();
        SaveOperations();
    }

    private void JourneyRecordEvent(bool success)
    {
        if (journeyValidationMode || (!journey.inProgress && !journey.settlementPending)) return;
        if (success)
        {
            journey.successfulEvents = Mathf.Min(100000, journey.successfulEvents + 1);
            journey.score = Mathf.Min(100000000, journey.score + 250);
        }
        RefreshJourneyHUD();
        // CompleteOperationsEvent commits this record with its event payout.
    }

    private bool TryHandleJourneyPause()
    {
        if (journeyValidationMode) return false;
        if (floorMissionDrawerOpen) { CloseFloorMissionDrawer(); return true; }
        if (JourneyIsPlaying || JourneyIsFloorClosing) return false;
        if (JourneyIsGuide) JourneyReturnToLobby();
        paused = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshJourneyHUD();
        return true;
    }

    private void JourneyStartNight(bool challenge)
    {
        if (journeyValidationMode) return;
        if (journey.inProgress || JourneyHasActivePlan) { ContinueJourney(); return; }
        if (journey.settlementPending) SettleJourneyNight();
        if (journey.settledSession > 0 && journey.session == journey.settledSession)
            journey.day = Mathf.Min(9999, journey.day + 1);
        BeginDayPlanning(challenge);
    }

    private void StartFreshJourneyNight(bool challenge)
    {
        if (journeyValidationMode || journey.inProgress) return;
        CancelPourGame(false);
        if (opMenuOpen) CloseOperations();
        ResetFloorForNewNight();
        journey.session = journey.session >= 1000000000 ? 1 : journey.session + 1;
        // At rollover the monotonically unique receipt is reset alongside the
        // session, never leaving an old settled id equal to a fresh new night.
        if (journey.session == 1) journey.settledSession = 0;
        journey.challenge = challenge;
        journey.inProgress = true;
        journey.settlementPending = false;
        journey.secondsLeft = JourneyNightDuration;
        journey.score = journey.combo = journey.bestCombo = journey.manualPerfects = 0;
        journey.orders = journey.mistakes = journey.successfulEvents = journey.manualStreak = 0;
        journey.feverSeconds = journey.feverCharge = journey.drinkIncome = 0f;
        journey.openingCash = JourneyClamp(BookingHasNight && journey.hasDayOpeningCash ? journey.dayOpeningCash : cash, StartingCash, 0f, 1000000000f);
        journey.resultStars = journey.resultServed = journey.resultXP = journey.resultBonus = 0;
        journey.resultProfit = journey.resultTicketIncome = 0f;
        journey.resultAttendance = 0;
        journey.resultNewRecord = false;
        journey.guideStep = 0;
        journey.guidePending = !journey.tutorialComplete;
        journeyAutosaveTimer = 0f;
        gameClock = 18f;
        satisfaction = Mathf.Max(satisfaction, challenge ? 0.85f : 0.94f);
        performance = Mathf.Max(performance, 0.92f);
        ResetJourneyOrders();
        journeyPhase = journey.guidePending ? JourneyPhase.Guide : JourneyPhase.Playing;
        paused = journey.guidePending;
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshUI();
        RefreshJourneyHUD();
        SaveOperations();
        if (!paused) ShowToast(SimBarLocalization.Format("DAY {0} / THREE MINUTES. MAKE IT A GREAT NIGHT!", "{0}일 차 / 3분 동안 멋진 밤을 만들어 보세요!", journey.day), 4f);
    }

    private void ContinueJourney()
    {
        if (journeyValidationMode) return;
        if (journey.settlementPending) SettleJourneyNight();
        if (JourneyHasActivePlan) { BeginDayPlanning(journey.challenge); return; }
        if (!journey.inProgress)
        {
            if (journey.settledSession > 0)
            {
                journeyPhase = JourneyPhase.Results;
                paused = true;
                if (pausePanel != null) pausePanel.SetActive(false);
                RefreshJourneyHUD();
            }
            else JourneyStartNight(journey.challenge);
            return;
        }
        if (opMenuOpen) CloseOperations();
        if (journey.floorClosing)
        {
            if (!TryBeginFloorClosing()) FinishJourneyNight();
            return;
        }
        if (journey.secondsLeft <= 0f) { FinishJourneyNight(); return; }
        // The queue is a transient scene object. Reconnect safely supplies fresh
        // orders without erasing night counters, earnings, XP or event progress.
        ResetJourneyOrders();
        journeyPhase = journey.guidePending ? JourneyPhase.Guide : JourneyPhase.Playing;
        paused = journey.guidePending;
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshUI();
        RefreshJourneyHUD();
        SaveOperations();
    }

    private void JourneyAdvanceGuide()
    {
        if (journeyValidationMode || !JourneyIsGuide) return;
        if (journey.guideStep < 2) journey.guideStep++;
        else
        {
            journey.tutorialComplete = true;
            journey.guidePending = false;
            journeyPhase = JourneyPhase.Playing;
            paused = false;
            ShowToast(SimBarLocalization.Text("THE FLOOR IS YOURS! SELECT A DRINK, THEN TIME YOUR POUR.", "영업 시작! 음료를 고르고 타이밍에 맞춰 따라 보세요."), 4f);
        }
        RefreshJourneyHUD();
        SaveOperations();
    }

    private void JourneyNextNight()
    {
        if (journeyValidationMode || journey.inProgress || !JourneyIsResults) return;
        journey.day = Mathf.Min(9999, journey.day + 1);
        BeginDayPlanning(journey.challenge);
    }

    private void JourneyRetryNight()
    {
        if (journeyValidationMode || journey.inProgress || !JourneyIsResults) return;
        BeginDayPlanning(journey.challenge);
    }

    private void JourneyReturnToLobby()
    {
        if (journeyValidationMode) return;
        CancelPourGame(false);
        if (opMenuOpen) CloseOperations();
        CloseFloorMissionDrawer();
        journeyPhase = JourneyPhase.Lobby;
        paused = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshJourneyHUD();
        SaveOperations();
    }

    private void ResetJourneyOrders()
    {
        CancelPourGame(false);
        foreach (Patron patron in patrons) if (patron != null) ClearOrder(patron);
        orderQueue.Clear();
        if (patrons.Count >= 9) SeedOrders();
        else
        {
            for (int i = 0; i < Mathf.Min(3, patrons.Count); i++) AssignOrder(patrons[i], i % DrinkNames.Length);
        }
        requestTimer = 3f * JourneyOrderIntervalMultiplier * BookingOrderIntervalMultiplier;
        // servedTonight is deliberately lifetime-compatible for active events.
    }

    private void FinishJourneyNight()
    {
        if (journeyValidationMode || !journey.inProgress || journeyClosing) return;
        if (TryBeginFloorClosing()) return;
        CancelPourGame(false);
        CloseFloorMissionDrawer();
        journey.floorClosing = false;
        journey.secondsLeft = 0f;
        gameClock = 24f;
        journey.settlementPending = true;
        journey.inProgress = false;
        journeyPhase = JourneyPhase.Results;
        paused = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        SettleJourneyNight();
        RefreshUI();
        RefreshJourneyHUD();
    }

    private void SettleJourneyNight()
    {
        if (journeyValidationMode || journeyClosing || !journey.settlementPending) return;
        journeyClosing = true;
        try
        {
            if (journey.session > 0 && journey.settledSession != journey.session)
            {
                // Event settlement may save in its own method. The pending
                // receipt survives that intermediate save and finishes on load;
                // an inactive event will never be awarded a second time.
                if (opActiveEvent >= 0) CompleteOperationsEvent();
                int goals = 0;
                for (int i = 0; i < 3; i++) if (JourneyGoalProgress(i) >= 1f) goals++;
                int stars = goals == 3 ? 3 : goals >= 2 ? 2 : 1;
                int bonus = (journey.challenge ? 130 : 75) + goals * (journey.challenge ? 100 : 65) + stars * 40;
                int earnedXP = 25 + goals * 25 + stars * 15 + (journey.challenge ? 35 : 0);
                journey.resultStars = stars;
                journey.resultServed = journey.orders;
                journey.resultXP = earnedXP;
                journey.resultBonus = bonus;
                journey.resultNewRecord = journey.score > journey.bestScore;
                journey.bestScore = Mathf.Max(journey.bestScore, journey.score);
                journey.bestStars = Mathf.Max(journey.bestStars, stars);
                journey.xp = Mathf.Min(100000000, journey.xp + earnedXP);
                journey.completedNights = Mathf.Min(100000000, journey.completedNights + 1);
                cash = JourneyClamp(cash + bonus, cash, 0f, 1000000000f);
                CompleteBookingNight(stars);
                CompleteFloorNight();
                journey.resultProfit = JourneyClamp(cash - journey.openingCash, 0f, -1000000000f, 1000000000f);
                journey.settledSession = journey.session;
            }
            journey.settlementPending = false;
            journey.inProgress = false;
            journey.floorClosing = false;
            journey.guidePending = false;
            journey.feverSeconds = 0f;
            journey.combo = 0;
            foreach (Patron patron in patrons) if (patron != null) ClearOrder(patron);
            orderQueue.Clear();
            SaveOperations();
        }
        finally { journeyClosing = false; }
    }

    private string ExportJourneyState()
    {
        if (journey == null) journey = new JourneyData();
        return JsonUtility.ToJson(journey);
    }

    private void ImportJourneyState(string json)
    {
        PrepareFloorStateImport();
        journey = new JourneyData();
        if (!string.IsNullOrWhiteSpace(json) && json.Length <= 16000)
        {
            try
            {
                JourneyData loaded = JsonUtility.FromJson<JourneyData>(json);
                if (loaded != null && loaded.version == 1) journey = loaded;
            }
            catch (Exception error) { Debug.LogWarning("SimBar kept journey defaults: " + error.Message); }
        }
        SanitizeJourney();
    }

    private void SanitizeJourney()
    {
        if (journey == null) journey = new JourneyData();
        journey.version = 1;
        journey.day = Mathf.Clamp(journey.day, 1, 9999);
        journey.guideStep = Mathf.Clamp(journey.guideStep, 0, 2);
        journey.session = Mathf.Clamp(journey.session, 0, 1000000000);
        journey.settledSession = Mathf.Clamp(journey.settledSession, 0, journey.session);
        journey.secondsLeft = JourneyClamp(journey.secondsLeft, JourneyNightDuration, 0f, JourneyNightDuration);
        journey.score = Mathf.Clamp(journey.score, 0, 100000000);
        journey.combo = Mathf.Clamp(journey.combo, 0, 100000);
        journey.bestCombo = Mathf.Clamp(Mathf.Max(journey.bestCombo, journey.combo), 0, 100000);
        journey.manualPerfects = Mathf.Clamp(journey.manualPerfects, 0, 100000);
        journey.orders = Mathf.Clamp(Mathf.Max(journey.orders, journey.manualPerfects), 0, 100000);
        journey.mistakes = Mathf.Clamp(journey.mistakes, 0, 100000);
        journey.successfulEvents = Mathf.Clamp(journey.successfulEvents, 0, 100000);
        journey.manualStreak = Mathf.Clamp(journey.manualStreak, 0, 100000);
        journey.feverSeconds = JourneyClamp(journey.feverSeconds, 0f, 0f, 12f);
        journey.feverCharge = JourneyClamp(journey.feverCharge, 0f, 0f, 1f);
        journey.openingCash = JourneyClamp(journey.openingCash, cash, 0f, 1000000000f);
        journey.drinkIncome = JourneyClamp(journey.drinkIncome, 0f, 0f, 100000000f);
        journey.xp = Mathf.Clamp(journey.xp, 0, 100000000);
        journey.bestScore = Mathf.Clamp(journey.bestScore, 0, 100000000);
        journey.bestStars = Mathf.Clamp(journey.bestStars, 0, 3);
        journey.completedNights = Mathf.Clamp(journey.completedNights, 0, 100000000);
        journey.resultStars = Mathf.Clamp(journey.resultStars, 0, 3);
        journey.resultServed = Mathf.Clamp(journey.resultServed, 0, 100000);
        journey.resultXP = Mathf.Clamp(journey.resultXP, 0, 10000);
        journey.resultBonus = Mathf.Clamp(journey.resultBonus, 0, 100000);
        journey.resultProfit = JourneyClamp(journey.resultProfit, 0f, -1000000000f, 1000000000f);
        if (journey.floorMissions == null) journey.floorMissions = SimBarFloorMissions.NewState();
        SimBarFloorMissions.Sanitize(journey.floorMissions);
        journey.resultFloorBonus = JourneyClamp(journey.resultFloorBonus, 0f, 0f, SimBarFloorMissions.CleanupReward);
        journey.resultFloorTasks = Mathf.Clamp(journey.resultFloorTasks, 0, 2);
        if (journey.session == 0)
        {
            journey.inProgress = false;
            journey.settlementPending = false;
        }
        if (journey.settledSession > 0 && journey.settledSession == journey.session)
        {
            journey.inProgress = false;
            journey.settlementPending = false;
        }
        // Recover an unfinished floor shift before accepting a pending receipt.
        // Older saves have an empty floor state and retain their original settlement.
        if (journey.session > 0 && journey.settledSession != journey.session
            && journey.settlementPending
            && (SimBarFloorMissions.Remaining(journey.floorMissions) > 0 || journey.floorMissions.guestActive))
        {
            journey.inProgress = true;
            journey.floorClosing = true;
            journey.settlementPending = false;
        }
        if (!journey.inProgress || journey.settledSession == journey.session) journey.floorClosing = false;
        if (journey.floorClosing)
        {
            journey.secondsLeft = 0f;
            journey.settlementPending = false;
            journey.guidePending = false;
        }
        if (journey.bookingCareer == null) journey.bookingCareer = SimBarBooking.NewCareer();
        SimBarBooking.Sanitize(journey.booking, journey.bookingCareer);
        journey.dayOpeningCash = JourneyClamp(journey.dayOpeningCash, cash, 0f, 1000000000f);
        journey.resultTicketIncome = JourneyClamp(journey.resultTicketIncome, 0f, 0f, 1000000f);
        journey.resultAttendance = Mathf.Clamp(journey.resultAttendance, 0, 120);
        if (journey.booking != null && journey.booking.day != journey.day) journey.booking = null;
        // Older night saves have no booking plan and continue without fabricated fees or receipts.
        if (journey.inProgress && JourneyHasActivePlan) SimBarBooking.Lock(journey.booking);
        if (journey.tutorialComplete) journey.guidePending = false;
    }

    private static bool JourneyFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static float JourneyClamp(float value, float fallback, float min, float max)
    {
        return JourneyFinite(value) ? Mathf.Clamp(value, min, max)
            : JourneyFinite(fallback) ? Mathf.Clamp(fallback, min, max) : min;
    }
}
