using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Daytime planning desk. Fictional booking and real source records stay separate.</summary>
public sealed partial class SimBarExperience
{
    private Canvas bookingCanvas;
    private SimBarBookingLayout bookingLayout;
    private RectTransform bookingRoot, bookingHeader, bookingFooter, bookingContent;
    private RectTransform bookingRoster, bookingDetail, bookingBusiness, bookingArchive;
    private RectTransform bookingVenueMenu;
    private ScrollRect bookingScroll;
    private Button bookingBack, bookingLanguage, bookingPlanTab, bookingArchiveTab, bookingVenueButton;
    private Button bookingListenButton, bookingOfferButton, bookingPromotionButton, bookingLaunchButton, bookingBrowseButton;
    private readonly Button[] bookingVenueButtons = new Button[4];
    private readonly Button[] bookingBandButtons = new Button[12];
    private readonly Text[] bookingBandNames = new Text[12];
    private readonly Text[] bookingBandGenres = new Text[12];
    private readonly Text[] bookingBandStates = new Text[12];
    private readonly Button[] bookingPackageButtons = new Button[2];
    private readonly Button[] bookingTicketButtons = new Button[3];
    private Text bookingTitle, bookingSubtitle, bookingClockLabel, bookingVenueLabel, bookingVenueStats;
    private Text bookingSelectedName, bookingSelectedGenre, bookingSelectedMetrics, bookingActionHint;
    private Text bookingLineupTitle, bookingLineupBody, bookingForecastLabel, bookingFeedbackLabel, bookingWalletLabel;
    private Text bookingLiveText, bookingArchiveTitle, bookingArchiveNote;
    private readonly List<RectTransform> bookingArchiveRows = new List<RectTransform>();
    private readonly List<Text> bookingArchiveArtistLabels = new List<Text>();
    private readonly List<Text> bookingArchiveEvidenceLabels = new List<Text>();
    private readonly List<Button> bookingArchiveSources = new List<Button>();
    private bool bookingShowingArchive, bookingVenueMenuOpen, bookingUiKorean, bookingUiWasPlanning;
    private int bookingArchiveVenue = -1, bookingHistoryVenue = -1;
    private Vector2 bookingUiSize;
    private float bookingRosterY;

    private static readonly Color BookingPaper = new Color(0.96f, 0.94f, 0.88f, 1f);
    private static readonly Color BookingCard = new Color(1f, 0.99f, 0.96f, 1f);
    private static readonly Color BookingInk = new Color(0.13f, 0.20f, 0.22f, 1f);
    private static readonly Color BookingMuted = new Color(0.38f, 0.43f, 0.42f, 1f);
    private static readonly Color BookingGreen = new Color(0.15f, 0.37f, 0.30f, 1f);
    private static readonly Color BookingPaleGreen = new Color(0.83f, 0.90f, 0.80f, 1f);
    private static readonly Color BookingLine = new Color(0.85f, 0.85f, 0.77f, 1f);
    // Real archive names and won amounts remain readable in either interface language.
    private static Font BookingFont { get { return Resources.Load<Font>("Fonts/NotoSansKR-Regular") ?? SimBarLocalization.Font; } }

    private void BuildBookingUI()
    {
        if (bookingCanvas != null) return;
        var host = new GameObject("Daytime Booking Desk", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        host.transform.SetParent(transform, false);
        bookingCanvas = host.GetComponent<Canvas>();
        bookingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        bookingCanvas.sortingOrder = 650;
        bookingRoot = BookingPanel("Warm daytime background", host.transform, BookingPaper);
        BookingStretch(bookingRoot);
        bookingHeader = BookingPanel("Planning header", bookingRoot, BookingPaper);
        bookingFooter = BookingPanel("Planning footer", bookingRoot, BookingCard);
        bookingTitle = BookingText("Planning title", bookingHeader, "", "", 27, BookingInk, true);
        bookingSubtitle = BookingText("Planning introduction", bookingHeader,
            "10:00–18:00  /  Four two-hour blocks, then a three-minute live night",
            "10:00–18:00  /  2시간씩 네 번 기획하고, 3분의 밤을 열어요", 14, BookingMuted);
        bookingBack = BookingButton("Save and lobby", bookingHeader, "SAVE / LOBBY", "저장 · 로비", 13, BookingLine, BookingInk);
        bookingBack.onClick.AddListener(BookingBackToLobby);
        bookingLanguage = BookingButton("Planning language", bookingHeader, "한국어", "ENGLISH", 12, BookingCard, BookingInk);
        bookingLanguage.onClick.AddListener(() => SimBarLocalization.SetLanguage(!SimBarLocalization.IsKorean));
        bookingPlanTab = BookingButton("Fictional booking tab", bookingHeader, "BOOK NEWCOMERS", "신인 섭외", 15, BookingGreen, Color.white);
        bookingPlanTab.onClick.AddListener(() => BookingShowArchive(false));
        bookingArchiveTab = BookingButton("Verified venue history tab", bookingHeader, "VENUE ARCHIVE", "공연 기록", 15, BookingLine, BookingInk);
        bookingArchiveTab.onClick.AddListener(() => BookingShowArchive(true));
        bookingClockLabel = BookingText("Planning time and remaining blocks", bookingHeader, "", "", 15, BookingGreen, true);
        bookingVenueButton = BookingButton("Choose one of four venues", bookingHeader, "", "", 16, BookingCard, BookingInk);
        bookingVenueButton.onClick.AddListener(() => { bookingVenueMenuOpen = !bookingVenueMenuOpen; bookingVenueMenu.gameObject.SetActive(bookingVenueMenuOpen); });
        bookingVenueLabel = bookingVenueButton.GetComponentInChildren<Text>();
        bookingVenueStats = BookingText("Venue game settings", bookingHeader, "", "", 12, BookingMuted);

        RectTransform viewport = BookingPanel("Planning scroll viewport", bookingRoot, new Color(1f, 1f, 1f, 0.001f));
        viewport.gameObject.AddComponent<RectMask2D>();
        bookingContent = BookingPanel("Planning scrolling content", viewport, new Color(1f, 1f, 1f, 0f));
        bookingContent.GetComponent<Image>().raycastTarget = false;
        bookingScroll = viewport.gameObject.AddComponent<ScrollRect>();
        bookingScroll.viewport = viewport;
        bookingScroll.content = bookingContent;
        bookingScroll.horizontal = false;
        bookingScroll.vertical = true;
        bookingScroll.movementType = ScrollRect.MovementType.Clamped;
        bookingScroll.scrollSensitivity = 32f;
        bookingScroll.decelerationRate = 0.1f;
        BuildBookingRoster();
        BuildBookingDetail();
        BuildBookingBusiness();
        bookingArchive = BookingPanel("Read only real venue archive", bookingContent, BookingCard);
        bookingArchiveTitle = BookingText("Archive title", bookingArchive, "", "", 22, BookingInk, true);
        bookingArchiveNote = BookingText("Archive scope and verification date", bookingArchive, "", "", 13, BookingMuted);

        bookingFeedbackLabel = BookingText("Planning action feedback", bookingFooter, "", "", 14, BookingGreen);
        bookingWalletLabel = BookingText("Game wallet", bookingFooter, "", "", 13, BookingMuted);
        bookingLaunchButton = BookingButton("Launch three minute night", bookingFooter, "", "", 19, BookingGreen, Color.white);
        bookingLaunchButton.onClick.AddListener(BookingLaunchNight);

        // The venue menu is a last sibling so its four choices overlay the scroll area.
        bookingVenueMenu = BookingPanel("Venue dropdown", bookingRoot, BookingCard);
        for (int i = 0; i < bookingVenueButtons.Length; i++)
        {
            int selected = i;
            bookingVenueButtons[i] = BookingButton("Venue " + i, bookingVenueMenu, "", "", 15, BookingCard, BookingInk);
            bookingVenueButtons[i].onClick.AddListener(() =>
            {
                BookingSelectVenue(selected);
                bookingVenueMenuOpen = false;
                bookingVenueMenu.gameObject.SetActive(false);
                RefreshBookingUI();
            });
        }
        bookingVenueMenu.gameObject.SetActive(false);

        if (canvas != null)
        {
            bookingLiveText = BookingText("Tonight fictional live lineup", canvas.transform, "", "", 15, new Color(0.93f, 0.88f, 0.67f), true);
            var rect = bookingLiveText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -122f);
            rect.sizeDelta = new Vector2(700f, 38f);
            bookingLiveText.alignment = TextAnchor.MiddleCenter;
        }
        bookingUiKorean = SimBarLocalization.IsKorean;
        bookingLayout = SimBarBookingLayout.Attach(bookingCanvas, LayoutBookingUI);
        RefreshBookingUI();
    }

    private void BuildBookingRoster()
    {
        bookingRoster = BookingPanel("Fictional newcomer list", bookingContent, BookingCard);
        BookingText("Roster heading", bookingRoster, "12 FICTIONAL NEWCOMERS", "가상 신인 12팀 · 모든 장르", 18, BookingInk, true);
        BookingText("Roster scope", bookingRoster, "Original artists created for this game", "게임을 위해 만든 아티스트입니다", 12, BookingMuted);
        for (int i = 0; i < bookingBandButtons.Length; i++)
        {
            int selected = i;
            var row = BookingPanel("Fictional band " + i, bookingRoster, BookingCard);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            bookingBandButtons[i] = button;
            bookingBandNames[i] = BookingText("Artist name", button.transform, "", "", 16, BookingInk, true);
            bookingBandGenres[i] = BookingText("Artist genre", button.transform, "", "", 12, BookingMuted);
            bookingBandStates[i] = BookingText("Artist status", button.transform, "", "", 12, BookingGreen);
            bookingBandStates[i].alignment = TextAnchor.MiddleRight;
            button.onClick.AddListener(() =>
            {
                bookingSelectedBand = selected;
                RefreshBookingUI();
                bookingScroll.StopMovement();
                bookingScroll.verticalNormalizedPosition = 1f;
            });
        }
    }

    private void BuildBookingDetail()
    {
        bookingDetail = BookingPanel("Selected fictional artist", bookingContent, BookingCard);
        BookingText("Fictional artist badge", bookingDetail, "FICTIONAL ARTIST  /  GAME VALUES", "가상 아티스트  /  모든 수치는 게임 설정", 12, BookingGreen, true);
        bookingSelectedName = BookingText("Selected artist name", bookingDetail, "", "", 26, BookingInk, true);
        bookingSelectedGenre = BookingText("Selected artist genre", bookingDetail, "", "", 16, BookingMuted);
        bookingSelectedMetrics = BookingText("Fictional offer metrics", bookingDetail, "", "", 16, BookingInk);
        bookingPackageButtons[0] = BookingButton("Standard offer package", bookingDetail, "STANDARD OFFER", "기본 제안", 14, BookingLine, BookingInk);
        bookingPackageButtons[1] = BookingButton("Generous offer package", bookingDetail, "GENEROUS  +35% FEE", "넉넉한 제안 · 출연료 +35%", 14, BookingLine, BookingInk);
        for (int i = 0; i < 2; i++)
        {
            int package = i;
            bookingPackageButtons[i].onClick.AddListener(() => { bookingPackage = package; RefreshBookingUI(); });
        }
        bookingListenButton = BookingButton("Listen to fictional demo", bookingDetail, "LISTEN TO DEMO", "데모 듣기", 16, BookingPaleGreen, BookingInk);
        bookingListenButton.onClick.AddListener(() => BookingListen(bookingSelectedBand));
        bookingOfferButton = BookingButton("Send fictional artist offer", bookingDetail, "SEND OFFER", "섭외 제안하기", 16, BookingGreen, Color.white);
        bookingOfferButton.onClick.AddListener(() => BookingOffer(bookingSelectedBand, bookingPackage));
        bookingActionHint = BookingText("Actions consume time", bookingDetail,
            "Each listen / offer uses 2 hours. Rejected offers also use time.\nFees are paid only after acceptance. Listening improves the chance.",
            "듣기·제안마다 2시간 소요. 거절되어도 시간은 흘러요.\n수락할 때만 출연료를 지불해요. 데모를 들으면 확률이 올라요.", 12, BookingMuted);
        bookingBrowseButton = BookingButton("Browse all twelve newcomers", bookingDetail, "BROWSE ALL 12 ARTISTS  ↓", "가상 신인 12팀 둘러보기  ↓", 14, BookingLine, BookingInk);
        bookingBrowseButton.onClick.AddListener(() =>
        {
            float travel = Mathf.Max(1f, bookingContent.rect.height - bookingScroll.viewport.rect.height);
            bookingScroll.StopMovement();
            bookingScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(bookingRosterY / travel);
        });
    }

    private void BuildBookingBusiness()
    {
        bookingBusiness = BookingPanel("Tonight lineup tickets and forecast", bookingContent, BookingCard);
        bookingLineupTitle = BookingText("Lineup count", bookingBusiness, "", "", 18, BookingInk, true);
        bookingLineupBody = BookingText("Tonight booked fictional artists", bookingBusiness, "", "", 15, BookingInk);
        BookingText("Game ticket tier heading", bookingBusiness, "TICKETS  /  GAME PRICE", "티켓 가격  /  게임 설정", 12, BookingMuted, true);
        for (int i = 0; i < bookingTicketButtons.Length; i++)
        {
            int tier = i;
            bookingTicketButtons[i] = BookingButton("Ticket tier " + i, bookingBusiness, SimBarMoney.Format(10f + i * 5f), SimBarMoney.Format(10f + i * 5f), 16, BookingLine, BookingInk);
            bookingTicketButtons[i].onClick.AddListener(() => BookingSetTicket(tier));
        }
        bookingPromotionButton = BookingButton("Promote fictional show", bookingBusiness, "", "", 15, BookingPaleGreen, BookingInk);
        bookingPromotionButton.onClick.AddListener(BookingPromote);
        bookingForecastLabel = BookingText("Game forecast only", bookingBusiness, "", "", 13, BookingMuted);
    }

    private void RefreshBookingUI()
    {
        if (bookingCanvas == null) return;
        bool planning = JourneyIsPlanning && !JourneyValidationMode;
        if (bookingCanvas.gameObject.activeSelf != planning) bookingCanvas.gameObject.SetActive(planning);
        if (bookingLiveText != null)
        {
            bookingLiveText.gameObject.SetActive(JourneyIsPlaying && !JourneyValidationMode);
            bookingLiveText.text = BookingLiveLabel;
        }
        if (!planning || DayPlan == null) { bookingUiWasPlanning = false; return; }
        if (!bookingUiWasPlanning)
        {
            bookingUiWasPlanning = true;
            bookingLayout.RefreshLayout();
            bookingScroll.verticalNormalizedPosition = 1f;
        }
        bool korean = SimBarLocalization.IsKorean;
        if (bookingUiKorean != korean)
        {
            bookingUiKorean = korean;
            foreach (var label in bookingCanvas.GetComponentsInChildren<Text>(true)) label.font = BookingFont;
            if (bookingLiveText != null) bookingLiveText.font = BookingFont;
            bookingArchiveVenue = -1;
        }
        var plan = DayPlan;
        int venueIndex = Mathf.Clamp(bookingShowingArchive && bookingHistoryVenue >= 0 ? bookingHistoryVenue : plan.venue, 0, SimBarBooking.Venues4.Length - 1);
        var venue = SimBarBooking.Venues4[venueIndex];
        bookingSelectedBand = Mathf.Clamp(bookingSelectedBand, 0, SimBarBooking.Bands12.Length - 1);
        bookingPackage = Mathf.Clamp(bookingPackage, 0, 1);
        var band = SimBarBooking.Bands12[bookingSelectedBand];
        bool canAct = SimBarBooking.CanAct(plan);
        int booked = BookingBitCount(plan.bookedMask);
        int bit = 1 << bookingSelectedBand;
        bool listened = (plan.listenedMask & bit) != 0;
        bool attempted = (plan.attemptedMask & bit) != 0;
        bool selectedBooked = (plan.bookedMask & bit) != 0;
        int relationship = BookingCareer != null && BookingCareer.relationships != null && BookingCareer.relationships.Length > bookingSelectedBand ? BookingCareer.relationships[bookingSelectedBand] : 0;
        int growth = BookingCareer != null && BookingCareer.growth != null && BookingCareer.growth.Length > bookingSelectedBand ? BookingCareer.growth[bookingSelectedBand] : 0;
        bookingTitle.text = SimBarLocalization.Format("DAY {0}  /  THE BOOKING DESK", "{0}일 차  /  낮의 기획실", plan.day);
        bookingClockLabel.text = SimBarLocalization.Format("{0}:00  ·  {1} / 4 BLOCKS LEFT", "{0}:00  ·  남은 시간 {1} / 4블록", SimBarBooking.Hour(plan), SimBarBooking.ActionLimit - plan.blocks);
        bookingVenueLabel.text = (korean ? venue.name : venue.nameEnglish) + "  v";
        bookingVenueStats.text = SimBarLocalization.Format("Game settings: capacity {0} / {1}", "게임 설정: 정원 {0}명 / 선호 {1}", venue.capacity, BookingGenre(venue.preferredGenre));
        for (int i = 0; i < bookingVenueButtons.Length; i++)
        {
            var candidate = SimBarBooking.Venues4[i];
            bookingVenueButtons[i].GetComponentInChildren<Text>().text = (i == venueIndex ? "•  " : "   ") + (korean ? candidate.name : candidate.nameEnglish);
            bookingVenueButtons[i].interactable = !plan.locked && !plan.completed && (plan.blocks == 0 || bookingShowingArchive);
            BookingButtonColor(bookingVenueButtons[i], i == venueIndex ? BookingPaleGreen : BookingCard, BookingInk);
        }
        bookingSelectedName.text = korean ? band.name : band.nameEnglish;
        bookingSelectedGenre.text = korean ? band.genreKorean : band.genre;
        bookingSelectedMetrics.text = SimBarLocalization.Format(
            "Offer fee     {0}\nAcceptance     {1}%\nRelationship     {2}/100    ·    Growth     {3}/100",
            "제안 출연료     {0}\n수락 확률     {1}%\n관계     {2}/100    ·    성장     {3}/100",
            SimBarMoney.Format(SimBarBooking.Fee(bookingSelectedBand, bookingPackage)),
            SimBarBooking.Chance(plan, BookingCareer, bookingSelectedBand, bookingPackage), relationship, growth);
        bookingListenButton.interactable = canAct && !listened && !attempted;
        bookingListenButton.GetComponentInChildren<Text>().text = listened ? SimBarLocalization.Text("DEMO HEARD ✓", "데모 감상 완료") : SimBarLocalization.Text("LISTEN · 2H · FREE", "데모 듣기 · 2시간 · 무료");
        bookingOfferButton.interactable = canAct && !attempted && booked < SimBarBooking.BookingLimit && cash >= SimBarBooking.Fee(bookingSelectedBand, bookingPackage);
        bookingOfferButton.GetComponentInChildren<Text>().text = selectedBooked ? SimBarLocalization.Text("BOOKED", "섭외 완료") : attempted ? SimBarLocalization.Text("OFFER CLOSED", "오늘 제안 완료") : SimBarLocalization.Text("OFFER · 2 HOURS", "섭외 제안 · 2시간");
        for (int i = 0; i < bookingPackageButtons.Length; i++)
            BookingButtonColor(bookingPackageButtons[i], bookingPackage == i ? BookingGreen : BookingLine, bookingPackage == i ? Color.white : BookingInk);
        for (int i = 0; i < bookingBandButtons.Length; i++)
        {
            var item = SimBarBooking.Bands12[i];
            bookingBandNames[i].text = korean ? item.name : item.nameEnglish;
            bookingBandGenres[i].text = (korean ? item.genreKorean : item.genre) + "  ·  " + SimBarMoney.Format(item.baseFee);
            bookingBandStates[i].text = (plan.bookedMask & (1 << i)) != 0 ? SimBarLocalization.Text("BOOKED", "섭외 완료")
                : (plan.attemptedMask & (1 << i)) != 0 ? SimBarLocalization.Text("CLOSED", "제안 완료")
                : (plan.listenedMask & (1 << i)) != 0 ? SimBarLocalization.Text("HEARD", "감상 완료") : SimBarLocalization.Text("FICTIONAL", "가상 신인");
            BookingButtonColor(bookingBandButtons[i], bookingSelectedBand == i ? BookingPaleGreen : BookingCard, BookingInk);
        }
        bookingLineupTitle.text = SimBarLocalization.Format("TONIGHT'S LINEUP  {0} / 3", "오늘의 라인업  {0} / 3팀", booked);
        string lineup = "";
        int slot = 0;
        for (int i = 0; i < SimBarBooking.Bands12.Length; i++)
        {
            if ((plan.bookedMask & (1 << i)) == 0) continue;
            lineup += (++slot) + ". " + (korean ? SimBarBooking.Bands12[i].name : SimBarBooking.Bands12[i].nameEnglish) + "\n";
        }
        bookingLineupBody.text = booked == 0 ? SimBarLocalization.Text("No bookings yet.\nYou can still open a free community open mic.", "아직 섭외한 팀이 없어요.\n무료 커뮤니티 오픈마이크로도 문을 열 수 있어요.") : lineup.TrimEnd();
        for (int i = 0; i < bookingTicketButtons.Length; i++)
        {
            bookingTicketButtons[i].interactable = !plan.locked && !plan.completed;
            BookingButtonColor(bookingTicketButtons[i], plan.ticketTier == i ? BookingGreen : BookingLine, plan.ticketTier == i ? Color.white : BookingInk);
        }
        bookingPromotionButton.GetComponentInChildren<Text>().text = SimBarLocalization.Format("PROMOTE  ·  2H  ·  {0}", "공연 홍보  ·  2시간  ·  {0}", SimBarMoney.Format(SimBarBooking.PromotionCost));
        bookingPromotionButton.interactable = canAct && cash >= SimBarBooking.PromotionCost;
        var forecast = SimBarBooking.Forecast(plan, BookingCareer);
        bookingForecastLabel.text = SimBarLocalization.Format(
            "GAME FORECAST  ·  {0}/{1} guests  ·  Promotions {2}\nTicket income up to {3} · Final income follows service results.\nVenue settings and all booking numbers are fictional game values.",
            "게임 예상  ·  관객 {0}/{1}명  ·  홍보 {2}회\n티켓 수입 최대 {3} · 최종 수입은 영업 성과에 따라 결정\n매장 수치·출연료·확률은 실제 자료가 아닌 게임 설정입니다.",
            forecast.attendance, forecast.venueCapacity, plan.promotions, SimBarMoney.Format(forecast.ticketIncome));
        bookingFeedbackLabel.text = string.IsNullOrEmpty(bookingFeedback)
            ? SimBarLocalization.Text("Listen, make an offer, or promote. Each action spends one block.", "데모 감상, 섭외 제안, 공연 홍보마다 한 블록이 흘러갑니다.") : bookingFeedback;
        bookingWalletLabel.text = SimBarLocalization.Text("GAME WALLET  ", "게임 보유금  ") + SimBarMoney.Format(cash);
        bookingLaunchButton.interactable = !plan.locked && !plan.completed;
        bookingLaunchButton.GetComponentInChildren<Text>().text = booked > 0 ? SimBarLocalization.Text("OPEN THE NIGHT  ·  3 MIN", "오늘의 밤 열기  ·  3분") : SimBarLocalization.Text("OPEN COMMUNITY MIC  ·  3 MIN", "커뮤니티 오픈마이크 열기  ·  3분");
        BookingButtonColor(bookingPlanTab, bookingShowingArchive ? BookingLine : BookingGreen, bookingShowingArchive ? BookingInk : Color.white);
        BookingButtonColor(bookingArchiveTab, bookingShowingArchive ? BookingGreen : BookingLine, bookingShowingArchive ? Color.white : BookingInk);
        if (bookingArchiveVenue != venueIndex) RebuildBookingArchive(venueIndex);
    }

    private void BookingShowArchive(bool show)
    {
        bookingShowingArchive = show;
        bookingVenueMenuOpen = false;
        bookingVenueMenu.gameObject.SetActive(false);
        LayoutBookingUI(bookingUiSize);
        bookingScroll.StopMovement();
        bookingScroll.verticalNormalizedPosition = 1f;
        RefreshBookingUI();
    }

    private void RebuildBookingArchive(int venueIndex)
    {
        bookingArchiveVenue = venueIndex;
        foreach (var row in bookingArchiveRows) if (row != null) Destroy(row.gameObject);
        bookingArchiveRows.Clear(); bookingArchiveArtistLabels.Clear(); bookingArchiveEvidenceLabels.Clear(); bookingArchiveSources.Clear();
        var venue = SimBarBooking.Venues4[venueIndex];
        bookingArchiveTitle.text = (SimBarLocalization.IsKorean ? venue.name : venue.nameEnglish) + SimBarLocalization.Text("  /  VERIFIED RECORDS", "  /  확인된 공연 기록");
        bookingArchiveNote.text = SimBarLocalization.Text("Selected verified records · Open source\nReal artists are archive-only and cannot be booked. Checked: ", "확인된 일부 기록 · 출처 열기\n실존 아티스트는 기록 열람 전용입니다. 확인일: ") + SimBarVenueArchive.VerifiedAt;
        foreach (var entry in SimBarVenueArchive.Entries)
        {
            if (entry.venueId != venue.id) continue;
            var row = BookingPanel("Historical performance source", bookingArchive, BookingPaper);
            bookingArchiveRows.Add(row);
            bookingArchiveArtistLabels.Add(BookingText("Historical artist and date", row, entry.artist + "  ·  " + entry.date, entry.artist + "  ·  " + entry.date, 17, BookingInk, true));
            bookingArchiveEvidenceLabels.Add(BookingText("Evidence for this real performance", row, BookingEvidence(entry.evidence), BookingEvidence(entry.evidence), 13, BookingMuted));
            var source = BookingButton("Open historical source", row, "OPEN SOURCE ↗", "출처 열기 ↗", 13, BookingGreen, Color.white);
            string sourceUrl = entry.url;
            source.onClick.AddListener(() => Application.OpenURL(sourceUrl));
            bookingArchiveSources.Add(source);
        }
        LayoutBookingUI(bookingUiSize);
    }

    private void LayoutBookingUI(Vector2 size)
    {
        if (bookingHeader == null || size.x <= 0f) return;
        bookingUiSize = size;
        bool wide = size.x >= 900f;
        bool compact = !wide && size.y < 600f;
        float margin = wide ? 24f : 12f;
        float width = size.x - margin * 2f;
        float headerHeight = compact ? 158f : wide ? 158f : 230f;
        float footerHeight = compact ? 96f : wide ? 90f : 132f;
        float bodyHeight = Mathf.Max(1f, size.y - headerHeight - footerHeight);
        BookingPlace(bookingHeader, 0, 0, size.x, headerHeight);
        BookingPlace(bookingTitle.rectTransform, margin, 17f, width - (wide ? 250f : 132f), wide ? 46f : 66f);
        bookingTitle.fontSize = wide ? 27 : 23;
        BookingPlace(bookingBack.GetComponent<RectTransform>(), size.x - margin - 116f, 15f, 116f, 34f);
        BookingPlace(bookingLanguage.GetComponent<RectTransform>(), size.x - margin - 116f, 53f, 116f, 28f);
        BookingPlace(bookingSubtitle.rectTransform, margin, wide ? 61f : 88f, width, wide ? 28f : 38f);
        BookingPlace(bookingPlanTab.GetComponent<RectTransform>(), margin, wide ? 101f : 132f, wide ? 160f : (width - 8f) * 0.5f, 40f);
        BookingPlace(bookingArchiveTab.GetComponent<RectTransform>(), margin + (wide ? 168f : (width + 8f) * 0.5f), wide ? 101f : 132f, wide ? 148f : (width - 8f) * 0.5f, 40f);
        BookingPlace(bookingClockLabel.rectTransform, wide ? margin + 332f : margin, wide ? 91f : 187f, wide ? width - 674f : width * 0.49f, wide ? 53f : 34f);
        bookingClockLabel.fontSize = wide ? 14 : 12;
        BookingPlace(bookingVenueButton.GetComponent<RectTransform>(), wide ? size.x - margin - 320f : margin + width * 0.51f, wide ? 93f : 180f, wide ? 320f : width * 0.49f, 36f);
        bookingVenueLabel.fontSize = wide ? 16 : 13;
        BookingPlace(bookingVenueStats.rectTransform, wide ? size.x - margin - 320f : margin, wide ? 131f : 215f, wide ? 320f : width, 18f);
        bookingVenueStats.fontSize = 11;
        bookingSubtitle.gameObject.SetActive(!compact);
        if (compact)
        {
            BookingPlace(bookingTitle.rectTransform, margin, 8f, width - 132f, 52f);
            bookingTitle.fontSize = 20;
            BookingPlace(bookingBack.GetComponent<RectTransform>(), size.x - margin - 116f, 7f, 116f, 28f);
            BookingPlace(bookingLanguage.GetComponent<RectTransform>(), size.x - margin - 116f, 39f, 116f, 25f);
            BookingPlace(bookingPlanTab.GetComponent<RectTransform>(), margin, 62f, (width - 8f) * 0.5f, 32f);
            BookingPlace(bookingArchiveTab.GetComponent<RectTransform>(), margin + (width + 8f) * 0.5f, 62f, (width - 8f) * 0.5f, 32f);
            BookingPlace(bookingClockLabel.rectTransform, margin, 101f, width * 0.49f, 31f);
            BookingPlace(bookingVenueButton.GetComponent<RectTransform>(), margin + width * 0.51f, 100f, width * 0.49f, 34f);
            BookingPlace(bookingVenueStats.rectTransform, margin, 137f, width, 18f);
        }
        BookingPlace(bookingScroll.viewport, margin, headerHeight, width, bodyHeight);
        BookingPlace(bookingFooter, 0, size.y - footerHeight, size.x, footerHeight);
        if (wide)
        {
            BookingPlace(bookingFeedbackLabel.rectTransform, margin, 12f, width - 390f, 34f);
            BookingPlace(bookingWalletLabel.rectTransform, margin, 50f, width - 390f, 26f);
            BookingPlace(bookingLaunchButton.GetComponent<RectTransform>(), size.x - margin - 370f, 19f, 370f, 52f);
        }
        else
        {
            BookingPlace(bookingFeedbackLabel.rectTransform, margin, 5f, width, 38f);
            bookingFeedbackLabel.fontSize = 12;
            BookingPlace(bookingWalletLabel.rectTransform, margin, 45f, width, 22f);
            BookingPlace(bookingLaunchButton.GetComponent<RectTransform>(), margin, 74f, width, 46f);
            bookingLaunchButton.GetComponentInChildren<Text>().fontSize = width < 350f ? 16 : 18;
        }
        if (compact)
        {
            BookingPlace(bookingFeedbackLabel.rectTransform, margin, 2f, width, 30f);
            bookingFeedbackLabel.fontSize = 11;
            BookingPlace(bookingWalletLabel.rectTransform, margin, 34f, width, 20f);
            BookingPlace(bookingLaunchButton.GetComponent<RectTransform>(), margin, 57f, width, 34f);
        }
        float menuWidth = wide ? 320f : Mathf.Min(width, 300f);
        BookingPlace(bookingVenueMenu, size.x - margin - menuWidth, compact ? 134f : wide ? 132f : 218f, menuWidth, 184f);
        for (int i = 0; i < 4; i++) BookingPlace(bookingVenueButtons[i].GetComponent<RectTransform>(), 6f, 6f + i * 43f, menuWidth - 12f, 41f);

        float gap = 16f;
        float rosterWidth = wide ? Mathf.Clamp(width * 0.37f, 300f, 440f) : width;
        float detailWidth = wide ? width - rosterWidth - gap : width;
        const float detailHeight = 398f;
        const float businessHeight = 364f;
        float rightX = wide ? rosterWidth + gap : 0f;
        bookingRosterY = wide ? 0f : detailHeight + businessHeight + gap * 2f;
        BookingPlace(bookingDetail, rightX, 0f, detailWidth, detailHeight);
        BookingPlace(bookingBusiness, rightX, detailHeight + gap, detailWidth, businessHeight);
        BookingPlace(bookingRoster, 0f, bookingRosterY, rosterWidth, 880f);
        var rosterHeadings = bookingRoster.GetComponentsInChildren<Text>(true);
        BookingPlace(rosterHeadings[0].rectTransform, 16f, 14f, rosterWidth - 32f, 28f);
        BookingPlace(rosterHeadings[1].rectTransform, 16f, 43f, rosterWidth - 32f, 23f);
        for (int i = 0; i < 12; i++)
        {
            BookingPlace(bookingBandButtons[i].GetComponent<RectTransform>(), 8f, 73f + i * 66f, rosterWidth - 16f, 62f);
            BookingPlace(bookingBandNames[i].rectTransform, 11f, 7f, rosterWidth - 38f, 25f);
            BookingPlace(bookingBandGenres[i].rectTransform, 11f, 34f, rosterWidth - 126f, 21f);
            BookingPlace(bookingBandStates[i].rectTransform, rosterWidth - 114f, 34f, 82f, 21f);
        }
        float inner = detailWidth - 32f;
        float half = (inner - 10f) * 0.5f;
        BookingPlace(bookingDetail.GetChild(0).GetComponent<RectTransform>(), 16f, 14f, inner, 22f);
        BookingPlace(bookingSelectedName.rectTransform, 16f, 41f, inner, 60f);
        BookingPlace(bookingSelectedGenre.rectTransform, 16f, 104f, inner, 27f);
        BookingPlace(bookingSelectedMetrics.rectTransform, 16f, 139f, inner, 88f);
        BookingPlace(bookingPackageButtons[0].GetComponent<RectTransform>(), 16f, 233f, half, 39f);
        BookingPlace(bookingPackageButtons[1].GetComponent<RectTransform>(), 26f + half, 233f, half, 39f);
        BookingPlace(bookingListenButton.GetComponent<RectTransform>(), 16f, 281f, half, 44f);
        BookingPlace(bookingOfferButton.GetComponent<RectTransform>(), 26f + half, 281f, half, 44f);
        BookingPlace(bookingActionHint.rectTransform, 16f, 333f, inner, 49f);
        BookingPlace(bookingBrowseButton.GetComponent<RectTransform>(), 16f, 388f, inner, wide ? 0f : 34f);
        bookingBrowseButton.gameObject.SetActive(!wide);
        if (!wide)
        {
            // Keep the browse control inside the detail card and move lower cards down.
            BookingPlace(bookingDetail, rightX, 0f, detailWidth, detailHeight + 38f);
            BookingPlace(bookingBusiness, rightX, detailHeight + 38f + gap, detailWidth, businessHeight);
            bookingRosterY += 38f;
            BookingPlace(bookingRoster, 0f, bookingRosterY, rosterWidth, 880f);
        }
        bookingSelectedName.fontSize = detailWidth < 390f ? 23 : 26;
        bookingSelectedMetrics.fontSize = detailWidth < 360f ? 14 : 16;
        for (int i = 0; i < 2; i++) bookingPackageButtons[i].GetComponentInChildren<Text>().fontSize = detailWidth < 420f ? 12 : 14;
        bookingListenButton.GetComponentInChildren<Text>().fontSize = detailWidth < 390f ? 13 : 16;
        bookingOfferButton.GetComponentInChildren<Text>().fontSize = detailWidth < 390f ? 14 : 16;
        BookingPlace(bookingLineupTitle.rectTransform, 16f, 14f, inner, 29f);
        BookingPlace(bookingLineupBody.rectTransform, 16f, 51f, inner, 83f);
        BookingPlace(bookingBusiness.GetChild(2).GetComponent<RectTransform>(), 16f, 138f, inner, 25f);
        float ticketWidth = (inner - 16f) / 3f;
        for (int i = 0; i < 3; i++) BookingPlace(bookingTicketButtons[i].GetComponent<RectTransform>(), 16f + i * (ticketWidth + 8f), 169f, ticketWidth, 42f);
        BookingPlace(bookingPromotionButton.GetComponent<RectTransform>(), 16f, 224f, inner, 43f);
        BookingPlace(bookingForecastLabel.rectTransform, 16f, 280f, inner, 76f);
        bookingForecastLabel.fontSize = detailWidth < 390f ? 12 : 13;

        bookingRoster.gameObject.SetActive(!bookingShowingArchive);
        bookingDetail.gameObject.SetActive(!bookingShowingArchive);
        bookingBusiness.gameObject.SetActive(!bookingShowingArchive);
        bookingArchive.gameObject.SetActive(bookingShowingArchive);
        float archiveRowHeight = wide ? 116f : 156f;
        float archiveHeight = 140f + bookingArchiveRows.Count * (archiveRowHeight + 10f);
        BookingPlace(bookingArchive, 0f, 0f, width, archiveHeight);
        BookingPlace(bookingArchiveTitle.rectTransform, 16f, 17f, width - 32f, wide ? 35f : 61f);
        bookingArchiveTitle.fontSize = wide ? 22 : 20;
        BookingPlace(bookingArchiveNote.rectTransform, 16f, wide ? 62f : 82f, width - 32f, 48f);
        for (int i = 0; i < bookingArchiveRows.Count; i++)
        {
            BookingPlace(bookingArchiveRows[i], 12f, 136f + i * (archiveRowHeight + 10f), width - 24f, archiveRowHeight);
            float textWidth = wide ? width - 190f : width - 48f;
            BookingPlace(bookingArchiveArtistLabels[i].rectTransform, 12f, 10f, textWidth, wide ? 32f : 45f);
            BookingPlace(bookingArchiveEvidenceLabels[i].rectTransform, 12f, wide ? 48f : 56f, textWidth, 53f);
            BookingPlace(bookingArchiveSources[i].GetComponent<RectTransform>(), wide ? width - 164f : 12f, wide ? 35f : 113f, 128f, 34f);
        }
        float contentHeight = bookingShowingArchive ? archiveHeight : wide ? 880f : bookingRosterY + 880f;
        BookingPlace(bookingContent, 0f, 0f, width, Mathf.Max(contentHeight, bodyHeight));
    }

    private void DestroyBookingUI()
    {
        if (bookingCanvas != null) Destroy(bookingCanvas.gameObject);
        if (bookingLiveText != null) Destroy(bookingLiveText.gameObject);
        bookingCanvas = null;
        bookingLayout = null;
    }

    private static RectTransform BookingPanel(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj.GetComponent<RectTransform>();
    }

    private static Text BookingText(string name, Transform parent, string english, string korean, int size, Color color, bool bold = false)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<Text>();
        text.font = SimBarLocalization.Font;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        if (!string.IsNullOrEmpty(english) || !string.IsNullOrEmpty(korean)) SimBarLocalization.Bind(text, english, korean);
        text.font = BookingFont;
        return text;
    }

    private static Button BookingButton(string name, Transform parent, string english, string korean, int size, Color background, Color foreground)
    {
        var rect = BookingPanel(name, parent, background);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.94f, 0.96f, 0.90f);
        colors.pressedColor = new Color(0.78f, 0.85f, 0.75f);
        colors.disabledColor = new Color(0.80f, 0.80f, 0.76f, 0.55f);
        button.colors = colors;
        var label = BookingText(name + " Label", rect, english, korean, size, foreground, true);
        BookingStretch(label.rectTransform, 7f);
        label.rectTransform.offsetMin = new Vector2(7f, 2f);
        label.rectTransform.offsetMax = new Vector2(-7f, -2f);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private static void BookingButtonColor(Button button, Color background, Color foreground)
    {
        button.GetComponent<Image>().color = background;
        var label = button.GetComponentInChildren<Text>();
        if (label != null) label.color = foreground;
    }

    private static void BookingPlace(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private static void BookingStretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static int BookingBitCount(int mask)
    {
        int count = 0;
        for (int i = 0; i < 12; i++) if ((mask & (1 << i)) != 0) count++;
        return count;
    }

    private static string BookingEvidence(string evidence)
    {
        if (evidence.Contains("retrospective") || evidence.Contains("firsthand"))
            return SimBarLocalization.Text("Performance report / on-site photos", "공연 후 보도·현장 사진 기록");
        if (evidence.Contains("announcement"))
            return SimBarLocalization.Text("Past performance announcement; completion not independently confirmed", "과거 공연 공지 · 실제 개최 여부는 별도 확인 필요");
        return SimBarLocalization.Text("Dated artist / cultural archive; selected record", "아티스트·문화기관의 날짜 있는 일부 공연 기록");
    }

    private static string BookingGenre(string english)
    {
        foreach (var band in SimBarBooking.Bands12)
            if (band.genre == english) return SimBarLocalization.IsKorean ? band.genreKorean : band.genre;
        return english;
    }
}
