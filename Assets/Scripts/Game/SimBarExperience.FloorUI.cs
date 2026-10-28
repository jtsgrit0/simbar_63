using UnityEngine;
using UnityEngine.UI;

/// <summary>CSS-pixel mission HUD and a scrollable staff / hospitality drawer.</summary>
public sealed partial class SimBarExperience
{
    private bool floorMissionDrawerOpen;
    private bool floorMissionWasPaused, floorMissionUiVisible, floorMissionUiKorean;
    private Canvas floorMissionCanvas;
    private SimBarBookingLayout floorMissionLayout;
    private RectTransform floorMissionUiRoot, floorMissionBackdrop, floorMissionDrawer;
    private RectTransform floorMissionHeader, floorMissionFooter, floorMissionViewport, floorMissionContent;
    private RectTransform floorPerformanceCard, floorCleanupCard, floorGuestCard;
    private ScrollRect floorMissionScroll;
    private Button floorMissionHudButton, floorMissionCloseButton, floorConfettiButton, floorTissueButton, floorCleanupButton;
    private readonly Button[] floorGuestButtons = new Button[3];
    private Text floorMissionTitle, floorMissionSubtitle, floorMissionFeedbackLabel;
    private Text floorPerformanceTitle, floorPerformanceIntro, floorPerformanceDebt;
    private Text floorCleanupTitle, floorCleanupStatus, floorCleanupWorkers, floorCleanupReward;
    private Text floorGuestTitle, floorGuestStatus, floorGuestHint;
    private RectTransform floorCleanupTrack, floorCleanupFill;

    private static readonly Color FloorUiBackground = new Color(0.045f, 0.060f, 0.085f, 0.99f);
    private static readonly Color FloorUiCard = new Color(0.075f, 0.095f, 0.125f, 1f);
    private static readonly Color FloorUiLine = new Color(0.16f, 0.20f, 0.25f, 1f);
    private static readonly Color FloorUiGold = new Color(0.95f, 0.78f, 0.43f, 1f);
    private static readonly Color FloorUiGreen = new Color(0.16f, 0.40f, 0.34f, 1f);
    private static readonly Color FloorUiMuted = new Color(0.72f, 0.77f, 0.78f, 1f);
    private static readonly string[] FloorGuestActionsEnglish = { "1 / PAUSE ORDERS / OFFER WATER", "2 / CALMLY EXPLAIN / REQUEST EXIT", "3 / BELONGINGS / STAFF ESCORT" };
    private static readonly string[] FloorGuestActionsKorean = { "1 / 주문 잠시 중단·물 안내", "2 / 차분히 설명·정중한 퇴점 요청", "3 / 소지품 확인·직원 동행" };

    private void BuildFloorMissionUI()
    {
        if (floorMissionCanvas != null) return;
        var host = new GameObject("Performance and floor missions", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        host.transform.SetParent(transform, false);
        floorMissionCanvas = host.GetComponent<Canvas>();
        floorMissionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        floorMissionCanvas.sortingOrder = 620;
        floorMissionUiRoot = BookingPanel("Floor mission HUD root", host.transform, Color.clear);
        BookingStretch(floorMissionUiRoot);
        // A transparent full-screen Graphic must never intercept the normal bar controls.
        floorMissionUiRoot.GetComponent<Image>().raycastTarget = false;
        floorMissionHudButton = BookingButton("Performance and floor mission HUD button", floorMissionUiRoot, "", "", 13, FloorUiBackground, FloorUiGold);
        floorMissionHudButton.onClick.AddListener(ToggleFloorMissionDrawer);

        floorMissionBackdrop = BookingPanel("Floor mission outside close target", floorMissionUiRoot, new Color(0f, 0f, 0f, 0.12f));
        BookingStretch(floorMissionBackdrop);
        var outside = floorMissionBackdrop.gameObject.AddComponent<Button>();
        outside.targetGraphic = floorMissionBackdrop.GetComponent<Image>();
        outside.onClick.AddListener(CloseFloorMissionDrawer);
        floorMissionDrawer = BookingPanel("Performance and floor mission drawer", floorMissionUiRoot, FloorUiBackground);
        floorMissionHeader = BookingPanel("Floor mission fixed header", floorMissionDrawer, FloorUiBackground);
        floorMissionTitle = BookingText("Floor mission title", floorMissionHeader, "SHOW & FLOOR MISSIONS", "공연·홀 미션", 22, FloorUiGold, true);
        floorMissionSubtitle = BookingText("Floor mission phase instructions", floorMissionHeader, "", "", 12, FloorUiMuted);

        floorMissionViewport = BookingPanel("Floor mission scrolling viewport", floorMissionDrawer, new Color(1f, 1f, 1f, 0.001f));
        floorMissionViewport.gameObject.AddComponent<RectMask2D>();
        floorMissionContent = BookingPanel("Floor mission scrolling content", floorMissionViewport, Color.clear);
        floorMissionContent.GetComponent<Image>().raycastTarget = false;
        floorMissionScroll = floorMissionViewport.gameObject.AddComponent<ScrollRect>();
        floorMissionScroll.viewport = floorMissionViewport;
        floorMissionScroll.content = floorMissionContent;
        floorMissionScroll.horizontal = false;
        floorMissionScroll.vertical = true;
        floorMissionScroll.movementType = ScrollRect.MovementType.Clamped;
        floorMissionScroll.scrollSensitivity = 32f;
        floorMissionScroll.decelerationRate = 0.1f;
        BuildFloorPerformanceCard();
        BuildFloorCleanupCard();
        BuildFloorGuestCard();

        floorMissionFooter = BookingPanel("Floor mission fixed feedback and close", floorMissionDrawer, FloorUiBackground);
        floorMissionFeedbackLabel = BookingText("Floor mission action feedback", floorMissionFooter, "", "", 12, FloorUiGold);
        floorMissionFeedbackLabel.resizeTextForBestFit = true;
        floorMissionFeedbackLabel.resizeTextMinSize = 10;
        floorMissionFeedbackLabel.resizeTextMaxSize = 12;
        floorMissionCloseButton = BookingButton("Continue watching the floor", floorMissionFooter, "", "", 15, FloorUiGreen, Color.white);
        floorMissionCloseButton.onClick.AddListener(CloseFloorMissionDrawer);
        floorMissionBackdrop.gameObject.SetActive(false);
        floorMissionDrawer.gameObject.SetActive(false);
        floorMissionUiKorean = SimBarLocalization.IsKorean;
        floorMissionLayout = SimBarBookingLayout.Attach(floorMissionCanvas, LayoutFloorMissionUI);
        RefreshFloorMissionUI();
    }

    private void BuildFloorPerformanceCard()
    {
        floorPerformanceCard = BookingPanel("Stage confetti and tissue performance", floorMissionContent, FloorUiCard);
        floorPerformanceTitle = BookingText("Stage performance card title", floorPerformanceCard, "1 / STAGE PERFORMANCE", "1 / 꽃가루·휴지 퍼포먼스", 18, Color.white, true);
        floorPerformanceIntro = BookingText("Stage performance tradeoff", floorPerformanceCard,
            "Lift the crowd during the show. Each effect can be used twice. Your staff will clean the floor after the performance.",
            "공연 중 분위기를 올려 보세요. 연출마다 두 번 사용할 수 있고, 공연 후에는 직원들이 바닥을 정리합니다.", 13, FloorUiMuted);
        floorConfettiButton = BookingButton("Throw stage confetti", floorPerformanceCard, "", "", 15, new Color(0.34f, 0.20f, 0.40f), Color.white);
        floorTissueButton = BookingButton("Throw tissue streamers", floorPerformanceCard, "", "", 15, new Color(0.18f, 0.32f, 0.43f), Color.white);
        floorConfettiButton.onClick.AddListener(() => FloorThrow(SimBarFloorMissions.Confetti));
        floorTissueButton.onClick.AddListener(() => FloorThrow(SimBarFloorMissions.Tissue));
        floorPerformanceDebt = BookingText("Floor litter obligation", floorPerformanceCard, "", "", 12, FloorUiGold);
    }

    private void BuildFloorCleanupCard()
    {
        floorCleanupCard = BookingPanel("Post-performance employee cleanup", floorMissionContent, FloorUiCard);
        floorCleanupTitle = BookingText("Cleanup card title", floorCleanupCard, "2 / AFTER-SHOW CLEANUP", "2 / 공연 후 직원 청소", 18, Color.white, true);
        floorCleanupStatus = BookingText("Cleanup remaining work", floorCleanupCard, "", "", 14, FloorUiGold, true);
        floorCleanupWorkers = BookingText("Cleanup available and arrived employees", floorCleanupCard, "", "", 13, FloorUiMuted);
        floorCleanupTrack = BookingPanel("Cleanup progress track", floorCleanupCard, FloorUiLine);
        floorCleanupTrack.GetComponent<Image>().raycastTarget = false;
        floorCleanupFill = BookingPanel("Cleanup completed work", floorCleanupTrack, FloorUiGreen);
        floorCleanupFill.GetComponent<Image>().raycastTarget = false;
        floorCleanupButton = BookingButton("Assign employees after the show", floorCleanupCard, "", "", 15, FloorUiGreen, Color.white);
        floorCleanupButton.onClick.AddListener(FloorAssignCleanup);
        floorCleanupReward = BookingText("Cleanup settlement receipt", floorCleanupCard, "", "", 12, FloorUiMuted);
    }

    private void BuildFloorGuestCard()
    {
        floorGuestCard = BookingPanel("Courteous intoxicated guest exit", floorMissionContent, FloorUiCard);
        floorGuestTitle = BookingText("Guest care card title", floorGuestCard, "3 / INTOXICATED GUEST EXIT", "3 / 만취 손님 퇴점 안내", 18, Color.white, true);
        floorGuestStatus = BookingText("Selected guest and conversation progress", floorGuestCard, "", "", 14, FloorUiGold, true);
        floorGuestHint = BookingText("Guest care sequence explanation", floorGuestCard, "", "", 13, FloorUiMuted);
        for (int i = 0; i < floorGuestButtons.Length; i++)
        {
            int choice = i;
            floorGuestButtons[i] = BookingButton("Courteous guest action " + i, floorGuestCard, "", "", 14, FloorUiLine, Color.white);
            floorGuestButtons[i].onClick.AddListener(() => FloorGuestChoice(choice));
        }
    }

    private void OpenFloorMissionDrawer()
    {
        if (floorMissionCanvas == null || floorMissionDrawerOpen || JourneyValidationMode
            || (!JourneyIsPlaying && !JourneyIsFloorClosing) || IsPourInProgress) return;
        // A deliberate settings pause remains in charge; menu pauses can be exchanged.
        if (paused && !serviceMenuOpen && !opMenuOpen) return;
        if (serviceMenuOpen) CloseServiceMenu();
        if (opMenuOpen) CloseOperations();
        // Closing management can restore a deliberate pause and its visible panel.
        if (paused) return;
        floorMissionWasPaused = JourneyIsFloorClosing ? false : paused;
        floorMissionDrawerOpen = true;
        paused = !JourneyIsFloorClosing;
        if (pausePanel != null) pausePanel.SetActive(false);
        floorMissionBackdrop.gameObject.SetActive(true);
        floorMissionDrawer.gameObject.SetActive(true);
        floorMissionLayout.RefreshLayout();
        floorMissionScroll.StopMovement();
        float focus = 0f;
        if (FloorState != null)
        {
            if (JourneyIsFloorClosing && SimBarFloorMissions.Remaining(FloorState) > 0) focus = -floorCleanupCard.anchoredPosition.y;
            else if (FloorState.guestActive || (FloorState.guestComplete && !FloorGuestExitFinished)) focus = -floorGuestCard.anchoredPosition.y;
        }
        float travel = Mathf.Max(1f, floorMissionContent.rect.height - floorMissionViewport.rect.height);
        floorMissionScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(focus / travel);
        RefreshFloorMissionUI();
    }

    private void CloseFloorMissionDrawer()
    {
        if (!floorMissionDrawerOpen) return;
        floorMissionDrawerOpen = false;
        if (floorMissionBackdrop != null) floorMissionBackdrop.gameObject.SetActive(false);
        if (floorMissionDrawer != null) floorMissionDrawer.gameObject.SetActive(false);
        if (!JourneyValidationMode)
        {
            if (JourneyIsFloorClosing) paused = false;
            else if (JourneyIsPlaying) paused = floorMissionWasPaused;
        }
        RefreshFloorMissionUI();
    }

    private void ToggleFloorMissionDrawer()
    {
        if (floorMissionDrawerOpen) CloseFloorMissionDrawer();
        else OpenFloorMissionDrawer();
    }

    private void RefreshFloorMissionUI()
    {
        if (floorMissionCanvas == null) return;
        bool visible = !JourneyValidationMode && (JourneyIsPlaying || JourneyIsFloorClosing);
        if (!visible && floorMissionDrawerOpen) CloseFloorMissionDrawer();
        if (floorMissionCanvas.gameObject.activeSelf != visible) floorMissionCanvas.gameObject.SetActive(visible);
        if (!visible) { floorMissionUiVisible = false; return; }
        if (!floorMissionUiVisible) { floorMissionUiVisible = true; floorMissionLayout.RefreshLayout(); }
        if (floorMissionUiKorean != SimBarLocalization.IsKorean)
        {
            floorMissionUiKorean = SimBarLocalization.IsKorean;
            floorMissionFeedback = "";
            foreach (Text label in floorMissionCanvas.GetComponentsInChildren<Text>(true)) label.font = BookingFont;
        }
        var state = FloorState;
        bool hasState = state != null;
        if (!hasState) state = SimBarFloorMissions.NewState();
        int remaining = SimBarFloorMissions.Remaining(state);
        string guestSummary = state.guestComplete ? SimBarLocalization.Text("DONE", "완료")
            : state.guestActive ? (state.guestStep + 1) + "/3" : SimBarLocalization.Text("WAIT", "대기");
        floorMissionHudButton.GetComponentInChildren<Text>().text = SimBarLocalization.Format(
            "SHOW & FLOOR / H\nLitter {0} / Guest {1}", "공연·홀 미션 / H\n바닥 {0} / 안내 {1}", remaining, guestSummary);
        floorMissionHudButton.interactable = !IsPourInProgress && (!paused || floorMissionDrawerOpen || serviceMenuOpen || opMenuOpen);
        floorMissionBackdrop.gameObject.SetActive(floorMissionDrawerOpen);
        floorMissionDrawer.gameObject.SetActive(floorMissionDrawerOpen);
        if (!floorMissionDrawerOpen) return;

        floorMissionSubtitle.text = JourneyIsFloorClosing
            ? SimBarLocalization.Text("SHOW ENDED / Orders have stopped.\nFinish the floor missions to settle tonight.", "공연 종료 · 주문은 멈췄습니다.\n청소와 퇴점 안내를 마치면 마감합니다.")
            : SimBarLocalization.Text("The show pauses while you choose.\nClose this drawer to return to the floor.", "선택하는 동안 영업 시간이 멈춥니다.\n창을 닫으면 공연과 영업이 이어집니다.");
        floorConfettiButton.GetComponentInChildren<Text>().text = SimBarLocalization.Format("CONFETTI {0}/2", "꽃가루 뿌리기 {0}/2", state.confettiThrows);
        floorTissueButton.GetComponentInChildren<Text>().text = SimBarLocalization.Format("TISSUE {0}/2", "휴지 퍼포먼스 {0}/2", state.tissueThrows);
        bool performing = hasState && JourneyIsPlaying && !JourneyIsFloorClosing && !state.cleanupStarted && !IsPourInProgress;
        floorConfettiButton.interactable = performing && state.confettiThrows < SimBarFloorMissions.ThrowsPerEffect;
        floorTissueButton.interactable = performing && state.tissueThrows < SimBarFloorMissions.ThrowsPerEffect;
        floorPerformanceDebt.text = SimBarLocalization.Format("FLOOR TO CLEAN  {0}  /  Staff work follows the show.", "바닥 정리량 {0} / 공연이 끝나면 직원 청소가 필요합니다.", remaining);
        int assigned = Mathf.Clamp(state.cleanupWorkers, 0, SimBarFloorMissions.MaxCleanupWorkers);
        int ready = Mathf.Clamp(CleanupReadyWorkers, 0, assigned);
        float progress = SimBarFloorMissions.CleanupProgress(state);
        floorCleanupStatus.text = state.cleanupComplete ? SimBarLocalization.Text("FLOOR CLEAN / 100%", "바닥 청소 완료 / 100%")
            : state.cleanupActive ? SimBarLocalization.Format("CLEANUP {0}% / {1} LEFT", "청소 {0}% / 남은 정리량 {1}", Mathf.RoundToInt(progress * 100f), remaining)
            : remaining > 0 ? SimBarLocalization.Format("{0} LITTER TO CLEAN", "남은 바닥 정리량 {0}", remaining) : SimBarLocalization.Text("NO LITTER ON THE FLOOR", "현재 치울 꽃가루·휴지가 없습니다.");
        floorCleanupWorkers.text = state.cleanupComplete ? SimBarLocalization.Text("The assigned floor cleanup is complete.", "직원들의 바닥 정리 작업이 완료됐습니다.")
            : remaining == 0 ? SimBarLocalization.Text("No cleanup assignment is needed yet.", "현재 필요한 청소 배치가 없습니다.")
            : state.cleanupActive
            ? SimBarLocalization.Format("Assigned {0} / Arrived {1}\n{2}", "배치 {0}명 / 현장 도착 {1}명\n{2}", assigned, ready,
                ready < assigned ? SimBarLocalization.Text("Staff are moving to the cleanup spots.", "직원들이 청소 위치로 이동하고 있습니다.") : SimBarLocalization.Text("Assigned staff are cleaning the floor.", "배치한 직원들이 바닥을 정리하고 있습니다."))
            : JourneyIsFloorClosing ? SimBarLocalization.Format("Available staff: {0}\nAssign employees to begin cleanup.", "배치 가능한 직원 {0}명\n직원을 배치하면 청소를 시작합니다.", FloorAvailableWorkers)
            : SimBarLocalization.Format("Available staff: {0}\nCleanup starts after the performance.", "배치 가능한 직원 {0}명\n청소는 공연이 끝난 뒤 시작할 수 있습니다.", FloorAvailableWorkers);
        floorCleanupButton.interactable = hasState && JourneyIsFloorClosing && remaining > 0 && !state.cleanupActive && !state.cleanupComplete && FloorAvailableWorkers > 0;
        floorCleanupButton.GetComponentInChildren<Text>().text = state.cleanupComplete ? SimBarLocalization.Text("CLEANUP COMPLETE", "청소 완료")
            : state.cleanupActive ? SimBarLocalization.Text("STAFF ASSIGNED / WORKING", "직원 배치 완료 / 청소 중")
            : JourneyIsFloorClosing ? SimBarLocalization.Text("ASSIGN STAFF TO CLEAN", "직원 청소 배치") : SimBarLocalization.Text("AVAILABLE AFTER THE SHOW", "공연 후 직원 배치 가능");
        floorCleanupReward.text = state.cleanupRewardClaimed ? SimBarLocalization.Text("Cleanup bonus settled.", "청소 보너스 정산 완료")
            : SimBarLocalization.Format("Cleanup bonus {0} / paid after completed work.", "청소 보너스 {0} / 작업 완료 후 정산", SimBarMoney.Format(SimBarFloorMissions.CleanupReward));
        BookingPlace(floorCleanupFill, 0f, 0f, Mathf.Max(0f, floorCleanupTrack.rect.width) * progress, 7f);
        RefreshFloorGuestCard(state, hasState);
        floorMissionFeedbackLabel.text = string.IsNullOrEmpty(floorMissionFeedback)
            ? JourneyIsFloorClosing ? SimBarLocalization.Text("Assigned staff keep working while you view the scene.", "현장을 보는 동안에도 배치한 직원들은 계속 작업합니다.")
                : SimBarLocalization.Text("Choose a stage effect or check the floor missions.", "공연 연출을 선택하거나 홀 미션의 진행 상황을 확인하세요.")
            : floorMissionFeedback;
        floorMissionCloseButton.GetComponentInChildren<Text>().text = JourneyIsFloorClosing
            ? SimBarLocalization.Text("WATCH THE STAFF / CLOSE", "현장 보기 / 직원 작업 계속")
            : SimBarLocalization.Text("CONTINUE THE SHOW / CLOSE", "공연·영업 계속 / 닫기");
    }

    private void RefreshFloorGuestCard(SimBarFloorMissions.State state, bool hasState)
    {
        string name = SimBarLocalization.Text("Guest needing assistance", "도움이 필요한 손님");
        floorGuestStatus.text = state.guestComplete ? FloorGuestExitFinished ? SimBarLocalization.Text("COURTEOUS EXIT COMPLETE", "정중한 퇴점 안내 완료")
                : SimBarLocalization.Text("STAFF ARE ACCOMPANYING THE GUEST", "직원이 출입구까지 동행 중입니다.")
            : state.guestActive ? SimBarLocalization.Format("{0} / STEP {1} OF 3", "{0} / 안내 {1} / 3", name, state.guestStep + 1)
            : SimBarLocalization.Text("WATCH FOR A GUEST WHO NEEDS HELP", "도움이 필요한 손님이 나타나면 안내하세요.");
        floorGuestHint.text = state.guestComplete ? SimBarLocalization.Text("The guest was treated respectfully.\nThe staff member checks belongings and accompanies them to the exit.", "손님을 정중하게 안내했습니다.\n직원이 소지품을 확인하고 출입구까지 동행합니다.")
            : SimBarLocalization.Text("Pause additional orders, explain calmly, then offer a staff escort. Follow the highlighted step.", "추가 주문을 잠시 멈추고, 차분하게 설명한 뒤 직원 동행을 안내하세요. 강조된 순서부터 진행합니다.");
        for (int i = 0; i < floorGuestButtons.Length; i++)
        {
            bool done = state.guestComplete || (state.guestActive && i < state.guestStep);
            bool current = state.guestActive && i == state.guestStep;
            floorGuestButtons[i].GetComponentInChildren<Text>().text = SimBarLocalization.Text(FloorGuestActionsEnglish[i], FloorGuestActionsKorean[i]) + (done ? SimBarLocalization.Text(" / DONE", " / 완료") : "");
            floorGuestButtons[i].interactable = hasState && state.guestActive && !state.guestComplete && i >= state.guestStep;
            BookingButtonColor(floorGuestButtons[i], done ? FloorUiGreen : current ? new Color(0.43f, 0.34f, 0.16f) : FloorUiLine, current ? FloorUiGold : Color.white);
        }
    }

    private void LayoutFloorMissionUI(Vector2 size)
    {
        if (floorMissionDrawer == null || size.x <= 0f || size.y <= 0f) return;
        float hudWidth = Mathf.Min(230f, size.x - 24f);
        BookingPlace(floorMissionHudButton.GetComponent<RectTransform>(), size.x - hudWidth - 12f, Mathf.Min(112f, Mathf.Max(12f, size.y - 68f)), hudWidth, 54f);
        float width = Mathf.Min(420f, size.x - 24f);
        float height = Mathf.Min(720f, size.y - 24f);
        float margin = 14f;
        float inner = width - margin * 2f;
        float headerHeight = width < 300f ? 120f : 106f;
        const float footerHeight = 104f;
        float viewportHeight = Mathf.Max(1f, height - headerHeight - footerHeight);
        BookingPlace(floorMissionDrawer, size.x - width - 12f, 12f, width, height);
        BookingPlace(floorMissionHeader, 0f, 0f, width, headerHeight);
        BookingPlace(floorMissionTitle.rectTransform, margin, 10f, inner, 39f);
        floorMissionTitle.fontSize = width < 300f ? 19 : 22;
        BookingPlace(floorMissionSubtitle.rectTransform, margin, 52f, inner, headerHeight - 56f);
        BookingPlace(floorMissionViewport, margin, headerHeight, inner, viewportHeight);
        BookingPlace(floorMissionFooter, 0f, height - footerHeight, width, footerHeight);
        BookingPlace(floorMissionFeedbackLabel.rectTransform, margin, 5f, inner, 42f);
        BookingPlace(floorMissionCloseButton.GetComponent<RectTransform>(), margin, 56f, inner, 40f);

        float cardInner = inner - 24f;
        const float performanceHeight = 286f, cleanupHeight = 328f, guestHeight = 430f, gap = 12f;
        BookingPlace(floorPerformanceCard, 0f, 0f, inner, performanceHeight);
        BookingPlace(floorPerformanceTitle.rectTransform, 12f, 12f, cardInner, 31f);
        BookingPlace(floorPerformanceIntro.rectTransform, 12f, 50f, cardInner, 76f);
        BookingPlace(floorConfettiButton.GetComponent<RectTransform>(), 12f, 136f, cardInner, 43f);
        BookingPlace(floorTissueButton.GetComponent<RectTransform>(), 12f, 187f, cardInner, 43f);
        BookingPlace(floorPerformanceDebt.rectTransform, 12f, 240f, cardInner, 42f);
        float cleanupY = performanceHeight + gap;
        BookingPlace(floorCleanupCard, 0f, cleanupY, inner, cleanupHeight);
        BookingPlace(floorCleanupTitle.rectTransform, 12f, 12f, cardInner, 31f);
        BookingPlace(floorCleanupStatus.rectTransform, 12f, 53f, cardInner, 52f);
        BookingPlace(floorCleanupWorkers.rectTransform, 12f, 111f, cardInner, 73f);
        BookingPlace(floorCleanupTrack, 12f, 192f, cardInner, 7f);
        BookingPlace(floorCleanupButton.GetComponent<RectTransform>(), 12f, 214f, cardInner, 45f);
        BookingPlace(floorCleanupReward.rectTransform, 12f, 271f, cardInner, 48f);
        float guestY = cleanupY + cleanupHeight + gap;
        BookingPlace(floorGuestCard, 0f, guestY, inner, guestHeight);
        BookingPlace(floorGuestTitle.rectTransform, 12f, 12f, cardInner, 31f);
        BookingPlace(floorGuestStatus.rectTransform, 12f, 53f, cardInner, 59f);
        BookingPlace(floorGuestHint.rectTransform, 12f, 120f, cardInner, 90f);
        for (int i = 0; i < floorGuestButtons.Length; i++) BookingPlace(floorGuestButtons[i].GetComponent<RectTransform>(), 12f, 220f + i * 64f, cardInner, 58f);
        float contentHeight = guestY + guestHeight;
        BookingPlace(floorMissionContent, 0f, 0f, inner, Mathf.Max(contentHeight, viewportHeight));
        if (FloorState != null) BookingPlace(floorCleanupFill, 0f, 0f, cardInner * SimBarFloorMissions.CleanupProgress(FloorState), 7f);
        int guestFont = cardInner < 285f ? 12 : 14;
        foreach (var button in floorGuestButtons) button.GetComponentInChildren<Text>().fontSize = guestFont;
    }

    private void DestroyFloorMissionUI()
    {
        floorMissionDrawerOpen = false;
        if (floorMissionCanvas != null) Destroy(floorMissionCanvas.gameObject);
        floorMissionCanvas = null;
        floorMissionLayout = null;
    }
}
