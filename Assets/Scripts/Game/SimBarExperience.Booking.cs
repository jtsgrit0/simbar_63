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
        CancelPourGame(false);
        if (serviceMenuOpen) CloseServiceMenu();
        if (opMenuOpen) CloseOperations();
        if (!JourneyHasActivePlan)
        {
            journey.booking = SimBarBooking.NewPlan(journey.day, selectedOwner == 2 ? 1 : 0);
            journey.dayOpeningCash = cash;
            journey.hasDayOpeningCash = true;
        }
        if (journey.bookingCareer == null) journey.bookingCareer = SimBarBooking.NewCareer();
        SimBarBooking.Sanitize(DayPlan, BookingCareer);
        journey.challenge = challenge;
        journeyPhase = JourneyPhase.Planning;
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
        if (!JourneyIsPlanning) return;
        BookingApply(SimBarBooking.Listen(DayPlan, BookingCareer, band));
    }

    private void BookingOffer(int band, int package)
    {
        if (!JourneyIsPlanning) return;
        BookingApply(SimBarBooking.Offer(DayPlan, BookingCareer, band, package, cash));
    }

    private void BookingPromote()
    {
        if (!JourneyIsPlanning) return;
        BookingApply(SimBarBooking.Promote(DayPlan, BookingCareer, cash));
    }

    private void BookingApply(SimBarBooking.Result result)
    {
        if (result.success) cash = JourneyClamp(cash + result.cashDelta, cash, 0f, 1000000000f);
        switch (result.code)
        {
            case "listened": bookingFeedback = SimBarLocalization.Text("Demo reviewed. This band's acceptance chance improved.", "데모 검토 완료. 이 팀의 제안 수락 확률이 올랐어요."); break;
            case "accepted": bookingFeedback = SimBarLocalization.Text("Booking confirmed! The artist fee has been paid.", "섭외 확정! 출연료를 지급했습니다."); break;
            case "rejected": bookingFeedback = SimBarLocalization.Text("The band declined today. Try another newcomer.", "오늘의 제안은 거절됐어요. 다른 신인에게 제안해 보세요."); break;
            case "promoted": bookingFeedback = SimBarLocalization.Text("Promotion complete. More guests are expected tonight.", "홍보 완료. 오늘 밤 예상 관객이 늘었어요."); break;
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
        if (!JourneyIsPlanning || index < 0 || index >= SimBarBooking.Venues4.Length) return;
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
        SaveOperations();
        RefreshBookingUI();
    }

    private void BookingSetTicket(int tier)
    {
        if (!JourneyIsPlanning) return;
        SimBarBooking.SetTicket(DayPlan, tier);
        SaveOperations();
        RefreshBookingUI();
    }

    private void BookingLaunchNight()
    {
        if (!JourneyIsPlanning || !JourneyHasActivePlan) return;
        SimBarBooking.Lock(DayPlan);
        StartFreshJourneyNight(journey.challenge);
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
