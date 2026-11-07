using UnityEngine;

public sealed partial class SimBarExperience
{
    private int bookingSelectedBand, bookingPackage;
    private string bookingFeedback;
    private bool JourneyIsPlanning { get { return journeyInitialized && journeyPhase == JourneyPhase.Planning; } }
    private SimBarBooking.Plan DayPlan { get { return journey.booking; } }
    private SimBarBooking.Career BookingCareer { get { return journey.bookingCareer; } }
    private bool JourneyHasActivePlan { get { return DayPlan != null && !DayPlan.locked && !DayPlan.completed && DayPlan.day == journey.day; } }
    private bool BookingHasNight { get { return DayPlan != null && DayPlan.day == journey.day && DayPlan.locked && !DayPlan.completed; } }
    private float BookingOrderIntervalMultiplier { get { return BookingHasNight && !journeyValidationMode ? SimBarBooking.Forecast(DayPlan, BookingCareer).orderIntervalMultiplier : 1f; } }
    private float BookingEnergyBonus { get { return BookingHasNight && !journeyValidationMode ? SimBarBooking.Forecast(DayPlan, BookingCareer).energyBonus : 0f; } }
    private string BookingLiveLabel
    {
        get
        {
            if (!BookingHasNight) return "";
            var metrics = SimBarBooking.Forecast(DayPlan, BookingCareer);
            var venue = SimBarBooking.Venues4[DayPlan.venue];
            string name = SimBarLocalization.IsKorean ? venue.name : venue.nameEnglish;
            if (metrics.bookedCount == 0) return name + SimBarLocalization.Text(" / COMMUNITY OPEN MIC", " / 커뮤니티 오픈마이크");
            int slot = Mathf.Min(metrics.bookedCount - 1, Mathf.FloorToInt((1f - journey.secondsLeft / JourneyNightDuration) * metrics.bookedCount));
            for (int i = 0; i < SimBarBooking.Bands12.Length; i++)
            {
                if ((DayPlan.bookedMask & (1 << i)) == 0 || slot-- > 0) continue;
                var band = SimBarBooking.Bands12[i];
                return name + " / LIVE " + (SimBarLocalization.IsKorean ? band.name : band.nameEnglish) + " · " + metrics.attendance + SimBarLocalization.Text(" guests", "명");
            }
            return name;
        }
    }

    private void BeginDayPlanning(bool challenge)
    {
        if (journeyValidationMode || journey.inProgress) return;
        if (DayPlan != null && DayPlan.locked && !DayPlan.completed) return;
        CancelPourGame(false);
        if (serviceMenuOpen) CloseServiceMenu();
        if (opMenuOpen) CloseOperations();
        if (journey.bookingCareer == null) journey.bookingCareer = SimBarBooking.NewCareer();
        SimBarBooking.Sanitize(DayPlan, BookingCareer);
        bool migrate=JourneyHasActivePlan&&!SimBarBooking.IsDesk(DayPlan);
        if (migrate)
        {
            int previousVenue=DayPlan.venue;
            bool trusted=journey.hasDayOpeningCash&&JourneyFinite(journey.dayOpeningCash)&&journey.dayOpeningCash>=0f&&journey.dayOpeningCash<=1000000000f&&journey.dayOpeningCash>=cash;
            journey.legacyDeskPlanJson=JsonUtility.ToJson(DayPlan);
            journey.deskMigrationCredit=trusted?journey.dayOpeningCash-cash:0f;
            journey.deskMigrationDay=journey.day;
            journey.deskMigrationTrustedOpening=trusted;
            if(trusted)cash=journey.dayOpeningCash;
            journey.booking=SimBarBooking.NewDeskPlan(journey.day,previousVenue);
            journey.dayOpeningCash=cash;journey.hasDayOpeningCash=true;
        }
        bool freshPlan=!JourneyHasActivePlan;
        if (freshPlan)
        {
            int previousVenue = journey.booking != null
                ? Mathf.Clamp(journey.booking.venue, 0, SimBarBooking.Venues4.Length - 1)
                : (selectedOwner == 2 ? 1 : 0);
            journey.booking = SimBarBooking.NewDeskPlan(journey.day, previousVenue);
            journey.dayOpeningCash = cash;
            journey.hasDayOpeningCash = true;
        }
        SimBarBooking.Sanitize(DayPlan, BookingCareer);
        journey.challenge = challenge;
        journeyPhase = JourneyPhase.Planning;
        bookingShowingPosters=false;
        bookingShowingArchive=false;
        paused = true;
        gameClock = SimBarBooking.Hour(DayPlan);
        bookingFeedback = "";
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshUI();
        RefreshJourneyHUD();
        SaveOperations();
    }

    private void BookingListen(int band)
    {
        if (!JourneyIsPlanning || SimBarBooking.IsDesk(DayPlan)) return;
        BookingApply(SimBarBooking.Listen(DayPlan, BookingCareer, band));
    }

    private void BookingOffer(int band, int package)
    {
        if (!JourneyIsPlanning || SimBarBooking.IsDesk(DayPlan)) return;
        BookingApply(SimBarBooking.Offer(DayPlan, BookingCareer, band, package, cash));
    }

    private void BookingPromote()
    {
        if (!JourneyIsPlanning || SimBarBooking.IsDesk(DayPlan)) return;
        BookingApply(SimBarBooking.Promote(DayPlan, BookingCareer, cash));
    }

    private void BookingApply(SimBarBooking.Result result)
    {
        if (result.success) cash = JourneyClamp(cash + result.cashDelta, cash, 0f, 1000000000f);
        switch (result.code)
        {
            case "scouted": bookingFeedback=SimBarLocalization.Text("Lineup discovered. Select an artist card to continue booking.","출연진 발굴 완료. 팀 카드를 눌러 섭외 상세로 이동하세요."); break;
            case "not_discovered": bookingFeedback=SimBarLocalization.Text("Research the band's poster first.","먼저 포스터를 조사해 이 팀을 발굴해 주세요."); break;
            case "already_scouted":
            case "already_discovered": bookingFeedback=SimBarLocalization.Text("This lineup is already in your scouting network.","이미 발굴한 출연진입니다. 섭외 탭에서 제안할 수 있어요."); break;
            case "listened": bookingFeedback = SimBarLocalization.Text("Demo reviewed. This band's acceptance chance improved.", "데모 검토 완료. 이 팀의 제안 수락 확률이 올랐어요."); break;
            case "accepted": bookingFeedback = SimBarLocalization.Text("Booking confirmed! The artist fee has been paid.", "섭외 확정! 출연료를 지급했습니다."); break;
            case "rejected": bookingFeedback = SimBarLocalization.Text("The band declined today. Try another newcomer.", "오늘의 제안은 거절됐어요. 다른 신인에게 제안해 보세요."); break;
            case "promoted": bookingFeedback = SimBarLocalization.Text("Promotion complete. More guests are expected tonight.", "홍보 완료. 오늘 밤 예상 관객이 늘었어요."); break;
            case "desk_selected": bookingFeedback=SimBarLocalization.Text("Freewill is selected. Prepare the show on the computer.","프리윌을 선택했어요. 컴퓨터로 공연 계획을 준비해요."); break;
            case "desk_cleared": bookingFeedback=SimBarLocalization.Text("Choose Freewill to begin today's preparation.","프리윌을 체크하고 오늘 준비를 시작해요."); break;
            case "desk_planned": bookingFeedback=SimBarLocalization.Text("The plan is saved. Confirm the show by phone.","공연 계획을 저장했어요. 전화로 출연을 확인해요."); break;
            case "desk_confirmed": bookingFeedback=SimBarLocalization.Text("Freewill is confirmed. Tonight is ready to open!","프리윌 출연 확인 완료! 오늘 밤 공연을 열어요!"); break;
            case "insufficient_cash": bookingFeedback = SimBarLocalization.Text("There is not enough cash for this action.", "보유금이 부족합니다."); break;
            case "no_time": bookingFeedback = SimBarLocalization.Text("Planning is over. Open tonight's show.", "기획 시간이 끝났어요. 오늘의 밤을 열어 주세요."); break;
            default: bookingFeedback = SimBarLocalization.Text("This action is already finished or unavailable today.", "오늘 이미 완료했거나 진행할 수 없는 행동입니다."); break;
        }
        gameClock = SimBarBooking.Hour(DayPlan);
        RefreshUI();
        RefreshBookingUI();
        // Wallet, accepted lineup and consumed time are one save transaction.
        SaveOperations();
    }

    private void BookingSelectVenue(int index)
    {
        if (!JourneyIsPlanning || SimBarBooking.IsDesk(DayPlan) || index < 0 || index >= SimBarBooking.Venues4.Length) return;
        if (bookingShowingArchive)
        {
            bookingHistoryVenue = index;
            RefreshBookingUI();
            return;
        }
        if (DayPlan.blocks > 0)
        {
            bookingFeedback = SimBarLocalization.Text("Choose the venue before the first planning action.", "공연장은 첫 기획 행동 전에 선택할 수 있어요.");
            RefreshBookingUI();
            return;
        }
        DayPlan.venue = index;
        SelectLatestPosterForVenue(index);
        SaveOperations();
        RefreshBookingUI();
    }

    private void BookingSetTicket(int tier)
    {
        if (!JourneyIsPlanning || SimBarBooking.IsDesk(DayPlan)) return;
        SimBarBooking.SetTicket(DayPlan, tier);
        SaveOperations();
        RefreshBookingUI();
    }

    private void BookingLaunchNight()
    {
        if (!JourneyIsPlanning || !JourneyHasActivePlan) return;
        if(SimBarBooking.IsDesk(DayPlan))
        {
            var result=SimBarBooking.LockDesk(DayPlan,BookingCareer);
            if(!result.success){bookingFeedback=SimBarLocalization.Text("Select Freewill, save the computer plan, then confirm by phone.","프리윌 선택, 컴퓨터 공연 계획, 전화 확인을 차례로 마쳐요.");RefreshBookingUI();return;}
        }
        else SimBarBooking.Lock(DayPlan);
        StartFreshJourneyNight(journey.challenge);
    }

    private void BookingDeskSelect(bool selected)
    {
        if(!JourneyIsPlanning||!SimBarBooking.IsDesk(DayPlan))return;
        var result=SimBarBooking.DeskSelect(DayPlan,selected);BookingApply(result);
        if(result.success)SetDeskChannel(1);
    }
    private void BookingDeskWork(int task)
    {
        if(!JourneyIsPlanning||!SimBarBooking.IsDesk(DayPlan))return;
        var result=SimBarBooking.DeskWork(DayPlan,task);BookingApply(result);
        if(result.success)SetDeskChannel(task==0?2:0);
    }

    private void BookingBackToLobby() { if (JourneyIsPlanning) JourneyReturnToLobby(); }

    private void CompleteBookingNight(int stars)
    {
        if (!BookingHasNight) return;
        var forecast = SimBarBooking.Forecast(DayPlan, BookingCareer);
        var result = SimBarBooking.Complete(DayPlan, BookingCareer, stars);
        if (!result.success) return;
        cash = JourneyClamp(cash + result.cashDelta, cash, 0f, 1000000000f);
        fans = Mathf.Clamp(fans + forecast.fanBonus, 0, 10000000);
        journey.resultTicketIncome = result.income;
        journey.resultAttendance = result.attendance;
        // Caller commits this completed flag, career, wallet and night receipt together.
    }
}
