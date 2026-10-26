using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent staff, facilities and timed live-house events.</summary>
public sealed partial class SimBarExperience
{
    public static string OperationsSaveKey { get { return "SimBar.Operations.v1"; } }

    private static readonly string[] OpStaffNames = { "BARBACK", "FLOOR HOST", "PROMOTER" };
    private static readonly string[] OpStaffNamesKorean = { "바 보조", "홀 매니저", "홍보 담당" };
    private static readonly int[] OpHireCosts = { 450, 600, 750 };
    private static readonly int[] OpWages = { 45, 35, 50 };
    private static readonly string[] OpStaffDescriptions =
    {
        "Automatically serves the oldest order every 12 seconds.\nDraught upgrades make service faster.",
        "Guests wait 18 seconds longer.\nSatisfaction slowly recovers while the host is on duty.",
        "+1 fan every 20 seconds and 10% more frequent orders.\nBuild your audience while you work the bar."
    };
    private static readonly string[] OpStaffDescriptionsKorean =
    {
        "12초마다 가장 오래된 주문을 자동으로 서빙합니다.\n생맥주 설비를 업그레이드하면 더 빠르게 서빙합니다.",
        "손님의 대기 시간이 18초 늘어납니다.\n근무 중에는 만족도가 서서히 회복됩니다.",
        "20초마다 팬이 1명 늘고 주문이 10% 더 자주 들어옵니다.\n바를 운영하면서 팬을 모아 보세요."
    };
    private static readonly string[] OpUpgradeNames = { "DRAUGHT SYSTEM", "SOUND & LIGHTS", "VIP LOUNGE" };
    private static readonly string[] OpUpgradeNamesKorean = { "생맥주 설비", "음향 및 조명", "VIP 라운지" };
    private static readonly int[,] OpUpgradeCosts = { { 650, 1050, 1550 }, { 750, 1250, 1800 }, { 550, 950, 1450 } };
    private static readonly string[] OpUpgradeDescriptions =
    {
        "+8% drink revenue per level.\nYour hired barback also serves 12% faster per level.",
        "+10% tips per level.\nReduces the band's energy loss while the show runs.",
        "+6 seconds of guest patience per level.\nAdds one more open tab to the queue per level."
    };
    private static readonly string[] OpUpgradeDescriptionsKorean =
    {
        "레벨마다 음료 매출이 8% 늘어납니다.\n고용한 바 보조의 서빙 속도도 레벨마다 12% 빨라집니다.",
        "레벨마다 팁이 10% 늘어납니다.\n공연 중 밴드의 에너지 소모를 줄입니다.",
        "레벨마다 손님의 대기 시간이 6초 늘어납니다.\n주문 대기열도 레벨마다 1칸 늘어납니다."
    };
    private static readonly string[] OpEventNames = { "OPEN MIC NIGHT", "ROCK SHOWCASE", "FAN FEST" };
    private static readonly string[] OpEventNamesKorean = { "오픈 마이크 나이트", "록 쇼케이스", "팬 페스티벌" };
    private static readonly int[] OpEventCosts = { 180, 420, 650 };
    private static readonly float[] OpEventDurations = { 60f, 90f, 120f };
    private static readonly float[] OpEventCooldowns = { 90f, 150f, 210f };
    private static readonly int[] OpEventTargets = { 6, 9, 12 };
    private static readonly int[] OpEventRewards = { 300, 650, 1050 };
    private static readonly int[] OpEventFanRewards = { 12, 25, 40 };
    private static readonly string[] OpEventDescriptions =
    {
        "60 sec: +10% drink revenue, +8 sec guest patience.\nServe 6 orders for $300 + 12 fans. Cooldown: 90 sec.",
        "90 sec: +40% tips and band energy recovery.\nServe 9 orders for $650 + 25 fans. Cooldown: 150 sec.",
        "120 sec: +15% revenue, +20% tips, faster orders.\n+2 fans / 10 sec. Serve 12 orders: $1,050 + 40 fans."
    };
    private static readonly string[] OpEventDescriptionsKorean =
    {
        "60초: 음료 매출 +10%, 손님 대기 시간 +8초.\n주문 6건 서빙 시 $300와 팬 12명 획득. 재개최 대기: 90초.",
        "90초: 팁 +40%, 밴드 에너지 회복.\n주문 9건 서빙 시 $650와 팬 25명 획득. 재개최 대기: 150초.",
        "120초: 매출 +15%, 팁 +20%, 주문 빈도 증가.\n10초마다 팬 +2명. 주문 12건 서빙 시 $1,050와 팬 40명 획득."
    };

    private static string OpStaffName(int index) { return SimBarLocalization.Text(OpStaffNames[index], OpStaffNamesKorean[index]); }
    private static string OpUpgradeName(int index) { return SimBarLocalization.Text(OpUpgradeNames[index], OpUpgradeNamesKorean[index]); }
    private static string OpEventName(int index) { return SimBarLocalization.Text(OpEventNames[index], OpEventNamesKorean[index]); }
    private static string OpManagementHint { get { return SimBarLocalization.Text("Simulation pauses while this menu is open. Purchases are saved immediately.", "이 메뉴를 열면 게임이 일시 정지됩니다. 구매 내역은 즉시 저장됩니다."); } }

    [Serializable]
    private sealed class OperationsSaveData
    {
        public int version = 1;
        public float money;
        public int fans;
        public float satisfaction;
        public float performance;
        public float clock;
        public int playlist;
        public int pours;
        public int[] staff;
        public int[] upgrades;
        public float wageTimer;
        public int wageDebt;
        public int activeEvent = -1;
        public float eventRemaining;
        public int eventStartPours;
        public float eventFanTimer;
        public float[] cooldowns;
        public int completedEvents;
        public float autoServeTimer;
        public float promoterTimer;
        public string journeyJson;
    }

    private readonly int[] opStaff = new int[3];
    private readonly int[] opUpgrades = new int[3];
    private readonly float[] opCooldowns = new float[3];
    private readonly GameObject[] opStaffVisuals = new GameObject[3];
    private readonly GameObject[] opUpgradeVisuals = new GameObject[3];
    private bool opInitialized;
    private bool opMenuOpen;
    private bool opWasPaused;
    private bool opPausePanelWasVisible;
    private int opPage;
    private int opWageDebt;
    private int opActiveEvent = -1;
    private int opEventStartPours;
    private int opCompletedEvents;
    private float opWageTimer = 60f;
    private float opAutoServeTimer = 12f;
    private float opPromoterTimer;
    private float opEventRemaining;
    private float opEventFanTimer;
    private float opAutosaveTimer;
    private GameObject opOverlay;
    private Transform opBody;
    private Text opWallet;
    private Text opStatus;
    private Text opFeedback;
    private Text opMapCaption;
    private Text opStaffCaption;
    private Sprite opRounded;

    private bool OpStaffOnDuty(int index) { return opStaff[index] > 0 && opWageDebt == 0; }
    private int OpHireCount { get { return opStaff[0] + opStaff[1] + opStaff[2]; } }
    private int OpPayroll { get { return opStaff[0] * OpWages[0] + opStaff[1] * OpWages[1] + opStaff[2] * OpWages[2]; } }
    private int OpEventPours { get { return Mathf.Max(0, servedTonight - opEventStartPours); } }

    private float OperationsRevenueMultiplier
    {
        get { return 1f + opUpgrades[0] * 0.08f + (opActiveEvent == 0 ? 0.10f : opActiveEvent == 2 ? 0.15f : 0f); }
    }
    private float OperationsTipMultiplier
    {
        get { return 1f + opUpgrades[1] * 0.10f + (opActiveEvent == 1 ? 0.40f : opActiveEvent == 2 ? 0.20f : 0f); }
    }
    private float OperationsPatienceBonus
    {
        get { return (OpStaffOnDuty(1) ? 18f : 0f) + opUpgrades[2] * 6f + (opActiveEvent == 0 ? 8f : 0f); }
    }
    private float OperationsOrderIntervalMultiplier
    {
        get { return (OpStaffOnDuty(2) ? 0.9f : 1f) * (opActiveEvent == 2 ? 0.7f : 1f); }
    }
    private int OperationsQueueBonus { get { return opUpgrades[2]; } }

    private void InitializeOperations()
    {
        if (opInitialized || canvas == null) return;
        LoadOperations();
        foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (node.name == "Map Caption") opMapCaption = node.GetComponent<Text>();
            if (node.name == "Staff Management") opStaffCaption = node.GetComponent<Text>();
            if (node.name == "Staff Management Card")
            {
                Button staffButton = node.GetComponent<Button>();
                if (staffButton == null) staffButton = node.gameObject.AddComponent<Button>();
                staffButton.targetGraphic = node.GetComponent<Image>();
                staffButton.targetGraphic.raycastTarget = true;
                staffButton.onClick.AddListener(() => OpenOperations(0));
            }
        }

        // Keep the playable service panel visible below the management tabs.
        string[] labels = { "UPGRADES", "GUEST INFO", "EVENTS" };
        int[] pages = { 1, 3, 2 };
        for (int i = 0; i < labels.Length; i++)
        {
            int page = pages[i];
            Button nav = OpButton(labels[i], canvas.transform, new Vector2(0.5f, 0f), new Vector2(-220f + i * 220f, 185f), new Vector2(208f, 42f), 18);
            nav.gameObject.name = "Operations " + labels[i];
            nav.onClick.AddListener(() => OpenOperations(page));
        }
        GameObject chip = OpPanel("Operations Live Status", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -83f), new Vector2(650f, 34f), new Color(0.035f, 0.045f, 0.075f, 0.90f), false);
        opStatus = OpText("Operations Status", chip.transform, string.Empty, 15, Vector2.zero, new Vector2(632f, 30f), new Color(0.80f, 0.91f, 0.78f), TextAnchor.MiddleCenter);
        BuildOperationsModal();
        opInitialized = true;
        UpdateOperationsWorldVisuals();
        RefreshOperationsHUD();
    }

    private void TickOperations(float delta)
    {
        if (!opInitialized || paused || delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        delta = Mathf.Min(delta, 600f);
        for (int i = 0; i < opCooldowns.Length; i++) opCooldowns[i] = Mathf.Max(0f, opCooldowns[i] - delta);
        opWageTimer -= delta;
        if (opWageTimer <= 0f) ProcessOperationsPayroll();

        if (OpStaffOnDuty(0))
        {
            opAutoServeTimer -= delta;
            if (opAutoServeTimer <= 0f && !IsPourInProgress)
            {
                opAutoServeTimer = Mathf.Max(6f, 12f * (1f - opUpgrades[0] * 0.12f));
                if (orderQueue.Count > 0) ServeDrink(orderQueue.Peek().drinkIndex);
            }
        }
        if (OpStaffOnDuty(1)) satisfaction = Mathf.Clamp01(satisfaction + delta * 0.0007f);
        if (OpStaffOnDuty(2))
        {
            opPromoterTimer += delta;
            if (opPromoterTimer >= 20f)
            {
                int fanGain = Mathf.FloorToInt(opPromoterTimer / 20f);
                opPromoterTimer -= fanGain * 20f;
                fans = Mathf.Min(10000000, fans + fanGain);
                SaveOperations();
            }
        }
        performance = Mathf.Clamp01(performance + opUpgrades[1] * delta * 0.0004f);
        if (opActiveEvent >= 0)
        {
            float eventDelta = Mathf.Min(delta, opEventRemaining);
            opEventRemaining = Mathf.Max(0f, opEventRemaining - delta);
            if (opActiveEvent == 1) performance = Mathf.Clamp01(performance + eventDelta * 0.002f);
            if (opActiveEvent == 2)
            {
                opEventFanTimer += eventDelta;
                if (opEventFanTimer >= 10f)
                {
                    int fanGain = Mathf.FloorToInt(opEventFanTimer / 10f) * 2;
                    opEventFanTimer %= 10f;
                    fans = Mathf.Min(10000000, fans + fanGain);
                }
            }
            if (opEventRemaining <= 0f) CompleteOperationsEvent();
        }
        opAutosaveTimer += delta;
        if (opAutosaveTimer >= 10f) { opAutosaveTimer = 0f; SaveOperations(); }
    }

    private void ProcessOperationsPayroll()
    {
        if (OpPayroll <= 0) { opWageDebt = 0; opWageTimer = 60f; return; }
        if (opWageDebt == 0) opWageDebt = OpPayroll;
        if (cash >= opWageDebt)
        {
            int payment = opWageDebt;
            cash -= payment;
            opWageDebt = 0;
            opWageTimer = 60f;
            ShowToast(SimBarLocalization.Format("STAFF PAYROLL PAID  -${0}", "직원 급여 지급  -${0}", payment), 2.8f);
        }
        else
        {
            opWageTimer = 10f;
            ShowToast(SimBarLocalization.Format("WAGES DUE: ${0} — hired staff are off duty until paid.", "미지급 급여: ${0} — 급여를 지급할 때까지 고용한 직원이 근무를 중단합니다.", opWageDebt), 4f);
        }
        UpdateOperationsWorldVisuals();
        SaveOperations();
    }

    public bool TryHireStaff(int index)
    {
        if (!opInitialized || index < 0 || index >= 3) return false;
        if (opStaff[index] != 0) return OpFailure(SimBarLocalization.Text("This employee is already hired.", "이미 고용한 직원입니다."));
        if (opWageDebt > 0) return OpFailure(SimBarLocalization.Text("Pay outstanding wages before hiring another employee.", "다른 직원을 고용하려면 먼저 미지급 급여를 지급하세요."));
        if (cash < OpHireCosts[index]) return OpFailure(SimBarLocalization.Format("Not enough cash to hire {0}.", "{0} 직원을 고용할 현금이 부족합니다.", OpStaffName(index)));
        cash -= OpHireCosts[index];
        opStaff[index] = 1;
        if (index == 0) opAutoServeTimer = 12f;
        UpdateOperationsWorldVisuals();
        FinishOperationsAction(SimBarLocalization.Format("{0} hired. Payroll: ${1} per 60 seconds.", "{0} 고용 완료. 60초마다 급여 ${1}를 지급합니다.", OpStaffName(index), OpPayroll));
        return true;
    }

    public bool TryPurchaseUpgrade(int index)
    {
        if (!opInitialized || index < 0 || index >= 3) return false;
        int level = opUpgrades[index];
        if (level >= 3) return OpFailure(SimBarLocalization.Text("This facility is already at its maximum level.", "이미 최고 레벨인 시설입니다."));
        int price = OpUpgradeCosts[index, level];
        if (cash < price) return OpFailure(SimBarLocalization.Text("Not enough cash for this facility upgrade.", "시설을 업그레이드할 현금이 부족합니다."));
        cash -= price;
        opUpgrades[index] = level + 1;
        UpdateOperationsWorldVisuals();
        FinishOperationsAction(SimBarLocalization.Format("{0} upgraded to level {1}.", "{0} 업그레이드 완료. 현재 레벨: {1}.", OpUpgradeName(index), level + 1));
        return true;
    }

    public bool TryStartEvent(int index)
    {
        if (!opInitialized || index < 0 || index >= 3) return false;
        if (opActiveEvent >= 0) return OpFailure(SimBarLocalization.Text("Finish the current event before starting another.", "다른 이벤트를 시작하려면 현재 이벤트를 마쳐야 합니다."));
        if (opCooldowns[index] > 0f) return OpFailure(SimBarLocalization.Format("This event is cooling down for {0}.", "이 이벤트는 {0} 후에 다시 개최할 수 있습니다.", OpTime(opCooldowns[index])));
        if (cash < OpEventCosts[index]) return OpFailure(SimBarLocalization.Text("Not enough cash to book this event.", "이벤트를 개최할 현금이 부족합니다."));
        cash -= OpEventCosts[index];
        opActiveEvent = index;
        opEventRemaining = OpEventDurations[index];
        opEventStartPours = servedTonight;
        opEventFanTimer = 0f;
        opCooldowns[index] = OpEventDurations[index] + OpEventCooldowns[index];
        performance = Mathf.Clamp01(performance + 0.05f);
        requestTimer = Mathf.Min(requestTimer, 1f);
        FinishOperationsAction(SimBarLocalization.Format("{0} booked! Close this menu to start the show.", "{0} 개최 준비 완료! 메뉴를 닫으면 공연이 시작됩니다.", OpEventName(index)));
        return true;
    }

    private void CompleteOperationsEvent()
    {
        if (opActiveEvent < 0) return;
        int index = opActiveEvent;
        bool success = OpEventPours >= OpEventTargets[index];
        int reward = success ? OpEventRewards[index] : 50;
        int newFans = success ? OpEventFanRewards[index] : 3;
        cash += reward;
        fans = Mathf.Min(10000000, fans + newFans);
        if (success) { opCompletedEvents++; satisfaction = Mathf.Clamp01(satisfaction + 0.04f); }
        opActiveEvent = -1;
        opEventRemaining = 0f;
        // Last call can settle an event early; its post-show cooldown still
        // starts at the normal duration rather than including unplayed minutes.
        opCooldowns[index] = Mathf.Min(opCooldowns[index], OpEventCooldowns[index]);
        JourneyRecordEvent(success);
        ShowToast(SimBarLocalization.Format("{0}  +${1}  +{2} FANS", "{0}  +${1}  팬 +{2}명", success ? SimBarLocalization.Text("EVENT SUCCESS!", "이벤트 성공!") : SimBarLocalization.Text("EVENT ENDED — target missed.", "이벤트 종료 — 목표 미달"), reward, newFans), 5f);
        SaveOperations();
    }

    private void PayOperationsWages()
    {
        if (opWageDebt <= 0) return;
        if (cash < opWageDebt) { OpFailure(SimBarLocalization.Text("Not enough cash to settle the wage balance.", "미지급 급여를 지급할 현금이 부족합니다.")); return; }
        int payment = opWageDebt;
        cash -= payment;
        opWageDebt = 0;
        opWageTimer = 60f;
        UpdateOperationsWorldVisuals();
        FinishOperationsAction(SimBarLocalization.Format("Wages paid: ${0}. All hired staff are back on duty.", "급여 ${0} 지급 완료. 고용한 직원 모두가 근무를 재개합니다.", payment));
    }

    private bool OpFailure(string message)
    {
        if (opFeedback != null) { opFeedback.text = message; opFeedback.color = new Color(1f, 0.60f, 0.48f); }
        ShowToast(message, 3f);
        return false;
    }

    private void FinishOperationsAction(string message)
    {
        RefreshUI();
        SaveOperations();
        if (opMenuOpen) DrawOperationsPage();
        if (opFeedback != null) { opFeedback.text = message; opFeedback.color = new Color(0.57f, 0.94f, 0.60f); }
        ShowToast(message, 3.5f);
    }

    private void BuildOperationsModal()
    {
        opOverlay = CreatePanel("Operations Modal Overlay", canvas.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, new Color(0.005f, 0.008f, 0.02f, 0.74f));
        RectTransform shade = opOverlay.GetComponent<RectTransform>();
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
        shade.offsetMin = Vector2.zero; shade.offsetMax = Vector2.zero;
        GameObject frame = OpPanel("Operations Modal", opOverlay.transform, Vector2.one * 0.5f, Vector2.zero, new Vector2(1080f, 720f), new Color(0.055f, 0.065f, 0.105f, 1f), true);
        OpText("Operations Title", frame.transform, "SIMBAR  /  HOUSE MANAGEMENT", 29, new Vector2(-33f, 316f), new Vector2(938f, 46f), new Color(0.99f, 0.90f, 0.65f), TextAnchor.MiddleLeft);
        opWallet = OpText("Operations Budget", frame.transform, string.Empty, 19, new Vector2(-55f, 276f), new Vector2(922f, 32f), Color.white, TextAnchor.MiddleLeft);
        Button close = OpButton("CLOSE  X", frame.transform, Vector2.one * 0.5f, new Vector2(449f, 315f), new Vector2(138f, 42f), 17);
        close.onClick.AddListener(CloseOperations);
        string[] names = { "STAFF", "FACILITIES", "EVENTS", "GUEST INFO" };
        for (int i = 0; i < names.Length; i++)
        {
            int page = i;
            Button tab = OpButton(names[i], frame.transform, Vector2.one * 0.5f, new Vector2(-363f + i * 242f, 225f), new Vector2(226f, 45f), 18);
            tab.onClick.AddListener(() => { opPage = page; DrawOperationsPage(); });
        }
        GameObject body = CreatePanel("Operations Body", frame.transform, Vector2.one * 0.5f, new Vector2(0f, -12f), new Vector2(998f, 428f), Color.clear);
        body.GetComponent<Image>().raycastTarget = false;
        opBody = body.transform;
        opFeedback = OpText("Operations Feedback", frame.transform, OpManagementHint, 17, new Vector2(0f, -286f), new Vector2(1016f, 40f), new Color(0.71f, 0.78f, 0.89f), TextAnchor.MiddleCenter);
        OpText("Operations Save Hint", frame.transform, "AUTO-SAVED  /  P OR CLOSE TO RETURN TO THE FLOOR", 13, new Vector2(0f, -332f), new Vector2(1010f, 24f), new Color(0.49f, 0.59f, 0.72f), TextAnchor.MiddleCenter);
        opOverlay.SetActive(false);
    }

    private void OpenOperations(int page)
    {
        if (!opInitialized || opOverlay == null || !JourneyCanManage) return;
        if (!opMenuOpen)
        {
            opWasPaused = paused;
            opPausePanelWasVisible = pausePanel != null && pausePanel.activeSelf;
        }
        opMenuOpen = true;
        paused = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        opPage = Mathf.Clamp(page, 0, 3);
        opOverlay.SetActive(true);
        opOverlay.transform.SetAsLastSibling();
        if (opFeedback != null)
        {
            opFeedback.text = OpManagementHint;
            opFeedback.color = new Color(0.71f, 0.78f, 0.89f);
        }
        DrawOperationsPage();
        RefreshOperationsHUD();
    }

    private void CloseOperations()
    {
        if (!opMenuOpen) return;
        opMenuOpen = false;
        if (opOverlay != null) opOverlay.SetActive(false);
        paused = opWasPaused;
        if (pausePanel != null) pausePanel.SetActive(opPausePanelWasVisible && paused);
        SaveOperations();
        RefreshUI();
    }

    private bool TryHandleOperationsPause()
    {
        if (!opMenuOpen) return false;
        CloseOperations();
        return true;
    }

    private void DrawOperationsPage()
    {
        if (opBody == null) return;
        for (int i = opBody.childCount - 1; i >= 0; i--)
        {
            GameObject child = opBody.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        if (opPage == 3) { DrawOperationsGuests(); return; }
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            GameObject card = OpPanel("Operations Item " + i, opBody, Vector2.one * 0.5f, new Vector2(0f, 147f - i * 142f), new Vector2(980f, 130f), new Color(0.09f, 0.105f, 0.16f, 1f), false);
            string title;
            string description;
            string action;
            bool enabled = true;
            if (opPage == 0)
            {
                title = SimBarLocalization.Format("{0}  /  ${1} WAGE EVERY 60 SEC", "{0}  /  60초마다 급여 ${1}", OpStaffName(i), OpWages[i]);
                description = SimBarLocalization.Text(OpStaffDescriptions[i], OpStaffDescriptionsKorean[i]);
                action = opStaff[i] > 0 ? (opWageDebt > 0 ? SimBarLocalization.Text("OFF DUTY / WAGES DUE", "근무 중단 / 급여 미지급") : SimBarLocalization.Text("HIRED / ON DUTY", "고용 완료 / 근무 중")) : SimBarLocalization.Format("HIRE  ${0}", "고용  ${0}", OpHireCosts[i]);
                enabled = opStaff[i] == 0;
            }
            else if (opPage == 1)
            {
                title = SimBarLocalization.Format("{0}  /  LEVEL {1} OF 3", "{0}  /  레벨 {1} / 3", OpUpgradeName(i), opUpgrades[i]);
                description = SimBarLocalization.Text(OpUpgradeDescriptions[i], OpUpgradeDescriptionsKorean[i]);
                action = opUpgrades[i] >= 3 ? SimBarLocalization.Text("MAX LEVEL", "최고 레벨") : SimBarLocalization.Format("UPGRADE  ${0}", "업그레이드  ${0}", OpUpgradeCosts[i, opUpgrades[i]]);
                enabled = opUpgrades[i] < 3;
            }
            else
            {
                title = OpEventName(i); description = SimBarLocalization.Text(OpEventDescriptions[i], OpEventDescriptionsKorean[i]);
                action = SimBarLocalization.Format("BOOK  ${0}", "개최  ${0}", OpEventCosts[i]);
                if (opActiveEvent == i) { action = SimBarLocalization.Format("LIVE  {0}", "진행 중  {0}", OpTime(opEventRemaining)); enabled = false; }
                else if (opActiveEvent >= 0) { action = SimBarLocalization.Text("AN EVENT IS RUNNING", "다른 이벤트 진행 중"); enabled = false; }
                else if (opCooldowns[i] > 0f) { action = SimBarLocalization.Format("COOLDOWN  {0}", "재개최 대기  {0}", OpTime(opCooldowns[i])); enabled = false; }
            }
            OpText("Item Title", card.transform, title, 22, new Vector2(-112f, 38f), new Vector2(708f, 34f), new Color(0.97f, 0.91f, 0.73f), TextAnchor.MiddleLeft);
            OpText("Item Description", card.transform, description, 17, new Vector2(-112f, -12f), new Vector2(708f, 65f), new Color(0.79f, 0.84f, 0.93f), TextAnchor.MiddleLeft);
            Button buy = OpButton(action, card.transform, Vector2.one * 0.5f, new Vector2(357f, 0f), new Vector2(224f, 54f), 17);
            buy.interactable = enabled;
            buy.onClick.AddListener(() =>
            {
                if (opPage == 0) TryHireStaff(index);
                else if (opPage == 1) TryPurchaseUpgrade(index);
                else TryStartEvent(index);
            });
        }
        if (opPage == 0 && opWageDebt > 0)
        {
            Button pay = OpButton(SimBarLocalization.Format("PAY OVERDUE WAGES  ${0}", "미지급 급여 지급  ${0}", opWageDebt), opBody, Vector2.one * 0.5f, new Vector2(0f, -231f), new Vector2(410f, 36f), 16);
            pay.onClick.AddListener(PayOperationsWages);
        }
    }

    private void DrawOperationsGuests()
    {
        string overview = SimBarLocalization.Format("FANS  {0}      SATISFACTION  {1}%      BAND ENERGY  {2}%", "팬  {0}명      만족도  {1}%      밴드 에너지  {2}%", fans.ToString("N0"), Mathf.RoundToInt(satisfaction * 100f), Mathf.RoundToInt(performance * 100f));
        OpText("Guest Overview", opBody, overview, 24, new Vector2(0f, 174f), new Vector2(976f, 50f), new Color(0.88f, 0.94f, 0.80f), TextAnchor.MiddleCenter);
        string summary = SimBarLocalization.Format("TONIGHT'S POURS  {0}     OPEN TABS  {1}/{2}     SUCCESSFUL EVENTS  {3}", "오늘 서빙  {0}잔     대기 주문  {1}/{2}     성공한 이벤트  {3}회", servedTonight, orderQueue.Count, 4 + OperationsQueueBonus, opCompletedEvents);
        OpText("Guest Summary", opBody, summary, 19, new Vector2(0f, 121f), new Vector2(976f, 40f), Color.white, TextAnchor.MiddleCenter);
        Patron[] waiting = orderQueue.ToArray();
        if (waiting.Length == 0)
            OpText("Guest Empty", opBody, SimBarLocalization.Text("No open tabs. Keep the music playing and enjoy the crowd.", "대기 중인 주문이 없습니다. 음악과 함께 손님들의 분위기를 즐겨 보세요."), 23, Vector2.zero, new Vector2(950f, 60f), new Color(0.76f, 0.83f, 0.93f), TextAnchor.MiddleCenter);
        int rows = Mathf.Min(waiting.Length, 7);
        for (int i = 0; i < rows; i++)
        {
            Patron guest = waiting[i];
            int drink = Mathf.Clamp(guest.drinkIndex, 0, DrinkNames.Length - 1);
            string line = SimBarLocalization.Format("{0}.  {1}     {2}     PATIENCE {3} SEC", "{0}.  {1}     {2}     남은 대기 {3}초", i + 1, guest.name.ToUpperInvariant(), DrinkNames[drink], Mathf.CeilToInt(Mathf.Max(0f, guest.patience)));
            OpText("Guest Tab " + i, opBody, line, 20, new Vector2(0f, 62f - i * 38f), new Vector2(930f, 36f), i == 0 ? new Color(0.99f, 0.83f, 0.42f) : new Color(0.78f, 0.84f, 0.94f), TextAnchor.MiddleLeft);
        }
    }

    private void RefreshOperationsHUD()
    {
        if (!opInitialized) return;
        string staffState = opWageDebt > 0 ? SimBarLocalization.Format("WAGES DUE ${0}", "미지급 급여 ${0}", opWageDebt) : SimBarLocalization.Format("STAFF {0}", "직원 {0}명", 2 + OpHireCount);
        if (opStatus != null)
        {
            opStatus.text = opActiveEvent >= 0
                ? SimBarLocalization.Format("{0}  {1}  |  POURS {2}/{3}  |  {4}", "{0}  {1}  |  서빙 {2}/{3}  |  {4}", OpEventName(opActiveEvent), OpTime(opEventRemaining), OpEventPours, OpEventTargets[opActiveEvent], staffState)
                : SimBarLocalization.Format("{0}  |  FACILITIES {1}/9  |  REVENUE x{2}  |  SAVED AUTOMATICALLY", "{0}  |  시설 {1}/9  |  매출 x{2}  |  자동 저장", staffState, opUpgrades[0] + opUpgrades[1] + opUpgrades[2], OperationsRevenueMultiplier.ToString("0.00"));
            opStatus.color = opWageDebt > 0 ? new Color(1f, 0.65f, 0.48f) : opActiveEvent >= 0 ? new Color(0.96f, 0.82f, 0.36f) : new Color(0.80f, 0.91f, 0.78f);
        }
        if (opWallet != null)
            opWallet.text = SimBarLocalization.Format("CASH ${0}   /   {1}   /   PAUSED WHILE MANAGING", "현금 ${0}   /   {1}   /   관리 중 일시 정지", cash.ToString("N0"), opWageDebt > 0 ? SimBarLocalization.Format("OVERDUE WAGES ${0}", "미지급 급여 ${0}", opWageDebt) : SimBarLocalization.Format("PAYROLL ${0} IN {1}", "{1} 후 급여 ${0}", OpPayroll, OpTime(opWageTimer)));
        if (opMapCaption != null) opMapCaption.text = opWageDebt > 0 ? SimBarLocalization.Text("HIRED STAFF OFF DUTY / WAGES DUE", "고용 직원 근무 중단 / 급여 미지급") : SimBarLocalization.Format("{0} STAFF  /  HOUSE OPEN", "직원 {0}명  /  영업 중", 2 + OpHireCount);
        if (opStaffCaption != null) opStaffCaption.text = SimBarLocalization.Format("Staff Management  {0}", "직원 관리  {0}명", 2 + OpHireCount);
    }

    private void RefreshOperationsLanguage()
    {
        if (!opInitialized) return;
        RefreshOperationsHUD();
        if (opMenuOpen) DrawOperationsPage();
        if (opFeedback != null)
        {
            opFeedback.text = OpManagementHint;
            opFeedback.color = new Color(0.71f, 0.78f, 0.89f);
        }
    }

    private void UpdateOperationsWorldVisuals()
    {
        if (worldRoot == null) return;
        Vector3[] positions = { new Vector3(-5.8f, 0f, -0.565f), new Vector3(-0.4f, 0f, -4.9f), new Vector3(8.75f, 0f, -3.5f) };
        for (int i = 0; i < 3; i++)
        {
            if (opStaff[i] > 0 && opStaffVisuals[i] == null)
            {
                Patron employee = CreatePerson("Hired " + OpStaffNames[i], positions[i], i == 0 ? Color.white : new Color(0.2f, 0.65f, 0.65f), Color.black, false, worldRoot, i == 0 ? "bartender" : "guest", 15f);
                if (employee != null && employee.root != null) opStaffVisuals[i] = employee.root.gameObject;
            }
            if (opStaffVisuals[i] != null) opStaffVisuals[i].SetActive(OpStaffOnDuty(i));
            if (opUpgradeVisuals[i] != null) { opUpgradeVisuals[i].SetActive(false); Destroy(opUpgradeVisuals[i]); opUpgradeVisuals[i] = null; }
            if (opUpgrades[i] <= 0) continue;
            Transform decorations = new GameObject("Facility " + OpUpgradeNames[i] + " Level " + opUpgrades[i]).transform;
            decorations.SetParent(worldRoot, false);
            opUpgradeVisuals[i] = decorations.gameObject;
            for (int level = 0; level < opUpgrades[i]; level++)
            {
                GameObject strip;
                if (i == 0) strip = CreateBox("Upgraded Draught Trim", new Vector3(-7.8f + level * 1.2f, 0.72f, -2.35f), new Vector3(0.9f, 0.035f, 0.035f), amberGlowMat, decorations);
                else if (i == 1) strip = CreateBox("Sound Upgrade LED", new Vector3(2f + level * 2.5f, 0.69f, 2.25f), new Vector3(0.85f, 0.04f, 0.08f), cyanGlowMat, decorations);
                else strip = CreateBox("VIP Seat Trim", new Vector3(-7.8f + level * 1.4f, 0.82f, -5.05f), new Vector3(0.85f, 0.035f, 0.035f), pinkGlowMat, decorations);
                Collider collider = strip.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }
        }
    }

    public void SaveOperations()
    {
        if (!opInitialized) return;
        OperationsSaveData state = new OperationsSaveData
        {
            money = cash, fans = fans, satisfaction = satisfaction, performance = performance, clock = gameClock,
            playlist = playlistIndex, pours = servedTonight, staff = (int[])opStaff.Clone(), upgrades = (int[])opUpgrades.Clone(),
            wageTimer = opWageTimer, wageDebt = opWageDebt, activeEvent = opActiveEvent, eventRemaining = opEventRemaining,
            eventStartPours = opEventStartPours, eventFanTimer = opEventFanTimer, cooldowns = (float[])opCooldowns.Clone(),
            completedEvents = opCompletedEvents, autoServeTimer = opAutoServeTimer, promoterTimer = opPromoterTimer,
            journeyJson = ExportJourneyState()
        };
        try { PlayerPrefs.SetString(OperationsSaveKey, JsonUtility.ToJson(state)); PlayerPrefs.Save(); }
        catch (Exception error) { Debug.LogWarning("SimBar could not save operations: " + error.Message); }
    }

    private void LoadOperations()
    {
        if (!PlayerPrefs.HasKey(OperationsSaveKey)) return;
        try
        {
            OperationsSaveData state = JsonUtility.FromJson<OperationsSaveData>(PlayerPrefs.GetString(OperationsSaveKey));
            if (state == null || state.version != 1) return;
            cash = OpFinite(state.money, StartingCash, 0f, 1000000000f);
            fans = Mathf.Clamp(state.fans, 0, 10000000);
            satisfaction = OpFinite(state.satisfaction, 0.94f, 0.3f, 1f);
            performance = OpFinite(state.performance, 0.98f, 0.35f, 1f);
            gameClock = OpFinite(state.clock, 21.5f, 18f, 23.999f);
            playlistIndex = Mathf.Clamp(state.playlist, 0, Playlist.Length - 1);
            servedTonight = Mathf.Clamp(state.pours, 0, 1000000);
            for (int i = 0; i < 3; i++)
            {
                opStaff[i] = state.staff != null && i < state.staff.Length ? Mathf.Clamp(state.staff[i], 0, 1) : 0;
                opUpgrades[i] = state.upgrades != null && i < state.upgrades.Length ? Mathf.Clamp(state.upgrades[i], 0, 3) : 0;
                opCooldowns[i] = state.cooldowns != null && i < state.cooldowns.Length ? OpFinite(state.cooldowns[i], 0f, 0f, 600f) : 0f;
            }
            opWageDebt = Mathf.Clamp(state.wageDebt, 0, OpPayroll);
            opWageTimer = OpFinite(state.wageTimer, 60f, 0.1f, 60f);
            opActiveEvent = state.activeEvent >= 0 && state.activeEvent < 3 ? state.activeEvent : -1;
            opEventRemaining = opActiveEvent >= 0 ? OpFinite(state.eventRemaining, 0f, 0f, OpEventDurations[opActiveEvent]) : 0f;
            if (opEventRemaining <= 0f) opActiveEvent = -1;
            opEventStartPours = Mathf.Clamp(state.eventStartPours, 0, servedTonight);
            opEventFanTimer = OpFinite(state.eventFanTimer, 0f, 0f, 10f);
            opCompletedEvents = Mathf.Clamp(state.completedEvents, 0, 1000000);
            opAutoServeTimer = OpFinite(state.autoServeTimer, 12f, 0.1f, 12f);
            opPromoterTimer = OpFinite(state.promoterTimer, 0f, 0f, 20f);
            ImportJourneyState(state.journeyJson);
            PlaySelectedMusic();
            for (int i = 0; i < playlistRows.Count; i++)
                playlistRows[i].color = i == playlistIndex ? new Color(0.06f, 0.36f, 0.30f, 0.96f) : new Color(0.10f, 0.12f, 0.19f, 0.88f);
        }
        catch (Exception error) { Debug.LogWarning("SimBar kept its defaults because the operations save could not be loaded: " + error.Message); }
    }

    private void OnApplicationPause(bool applicationPaused) { if (applicationPaused) SaveOperations(); }
    private void OnApplicationQuit() { SaveOperations(); }

    private static float OpFinite(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    private static string OpTime(float seconds)
    {
        int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
    }

    private Text OpText(string name, Transform parent, string value, int size, Vector2 position, Vector2 bounds, Color color, TextAnchor alignment)
    {
        Text text = CreateText(name, parent, value, size, alignment, color);
        SetRect(text.rectTransform, Vector2.one * 0.5f, position, bounds);
        text.fontStyle = FontStyle.Normal;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button OpButton(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
    {
        Button button = CreateButton(label, parent, anchor, position, size, new Color(0.16f, 0.20f, 0.27f, 1f), fontSize);
        OpRound(button.GetComponent<Image>(), true);
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.50f, 0.54f, 0.59f, 0.9f);
        colors.highlightedColor = new Color(1.20f, 1.20f, 1.15f, 1f);
        button.colors = colors;
        Text text = button.GetComponentInChildren<Text>();
        if (text != null) { text.fontStyle = FontStyle.Normal; text.raycastTarget = false; }
        return button;
    }

    private GameObject OpPanel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, bool prominent)
    {
        GameObject panel = CreatePanel(name, parent, anchor, position, size, color);
        OpRound(panel.GetComponent<Image>(), prominent);
        return panel;
    }

    private void OpRound(Image image, bool prominent)
    {
        if (opRounded == null)
        {
            const int edge = 48;
            Texture2D texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false);
            texture.name = "SimBar Operations Panel"; texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[edge * edge];
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 23.5f) - 12f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 23.5f) - 12f, 0f);
                pixels[y * edge + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(12f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            opRounded = Sprite.Create(texture, new Rect(0, 0, edge, edge), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, new Vector4(13f, 13f, 13f, 13f));
        }
        image.sprite = opRounded; image.type = Image.Type.Sliced;
        Outline line = image.gameObject.AddComponent<Outline>();
        line.effectColor = prominent ? new Color(0.77f, 0.65f, 0.35f, 1f) : new Color(0.28f, 0.33f, 0.42f, 1f);
        line.effectDistance = prominent ? new Vector2(1.7f, -1.7f) : new Vector2(0.9f, -0.9f);
    }
}
