using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Running Day Booking")]
    public static void CheckDayBookingMenu() { Debug.Log(CheckDayBooking()); }

    public static string CheckDayBooking()
    {
        var report = new Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress)
        {
            report.failures.Add("Enter Play Mode and finish any pour before checking daytime booking.");
            return report.ToString();
        }
        var snapshot = new JourneyCheckSnapshot(host);
        try
        {
            Write(host, "journeyValidationMode", false);
            Call(host, "ImportJourneyState", "");
            Write(host, "cash", 20000f); Write(host, "opActiveEvent", -1);
            SetJourneyPhase(host, "Lobby"); Write(host, "paused", true);
            Call(host, "JourneyStartNight", false);
            var plan = Property<SimBarBooking.Plan>(host, "DayPlan");
            report.Check(Property<bool>(host, "JourneyIsPlanning") && Read<bool>(host, "paused") && plan.blocks == 0,
                "Start opens paused 10:00 planning, not a running night.");
            report.Check(!Read<Transform>(host, "worldRoot").gameObject.activeSelf,
                "The venue is hidden behind the planning desk.");
            Call(host, "TickJourney", 60f);
            report.Check(plan.blocks == 0 && !Property<bool>(host, "JourneyIsPlaying"), "Reading planning does not run the shift.");
            Call(host, "BookingSelectVenue", 2);
            report.Check(plan.venue == 2, "All four venues can be selected before the first action.");
            Call(host, "BookingListen", 0);
            report.Check(plan.blocks == 1 && plan.listenedMask == 1 && Read<float>(host, "cash") == 20000f,
                "Demo review consumes exactly two hours and no money.");
            Call(host, "BookingSelectVenue", 3);
            report.Check(plan.venue == 2, "The venue cannot change after consuming planning time.");
            int accepted = -1;
            for (int i = 0; i < 12; i++)
            {
                var trial = JsonUtility.FromJson<SimBarBooking.Plan>(JsonUtility.ToJson(plan));
                if (SimBarBooking.Offer(trial, Property<SimBarBooking.Career>(host, "BookingCareer"), i, 1, 20000f).success)
                { accepted = i; break; }
            }
            if (accepted < 0) throw new Exception("No deterministic accepted offer in the fixture.");
            Call(host, "BookingOffer", accepted, 1);
            float afterFee = Read<float>(host, "cash");
            report.Check(plan.blocks == 2 && plan.bookedMask == (1 << accepted)
                && afterFee == 20000f - SimBarBooking.Fee(accepted, 1), "Accepted booking charges the full fee exactly once.");
            Call(host, "BookingOffer", accepted, 1);
            report.Check(Read<float>(host, "cash") == afterFee && plan.blocks == 2, "Repeating an offer cannot charge or consume time twice.");
            Call(host, "BookingPromote");
            Call(host, "BookingSetTicket", 2);
            float planningCash = Read<float>(host, "cash");
            host.SaveOperations(); Call(host, "ImportJourneyState", ""); Call(host, "LoadOperations");
            Call(host, "JourneyReturnToLobby"); Call(host, "ContinueJourney");
            plan = Property<SimBarBooking.Plan>(host, "DayPlan");
            report.Check(Property<bool>(host, "JourneyIsPlanning") && plan.blocks == 3 && plan.promotions == 1
                && plan.ticketTier == 2 && plan.bookedMask == (1 << accepted) && Read<float>(host, "cash") == planningCash,
                "Reload resumes the same daytime actions, lineup, ticket and wallet without repayment.");
            var forecast = SimBarBooking.Forecast(plan, Property<SimBarBooking.Career>(host, "BookingCareer"));
            Call(host, "BookingLaunchNight");
            for (int i = 0; i < 3; i++) Call(host, "JourneyAdvanceGuide");
            report.Check(Property<bool>(host, "JourneyIsPlaying") && plan.locked && Read<float>(host, "gameClock") == 18f,
                "Launch freezes the lineup and starts the playable night at 18:00.");
            report.Check(Property<float>(host, "BookingOrderIntervalMultiplier") < 1f
                && Property<float>(host, "BookingEnergyBonus") > 0f && !string.IsNullOrEmpty(Property<string>(host, "BookingLiveLabel")),
                "The booked show affects order frequency, energy and the live lineup label.");
            object data = Read<object>(host, "journey");
            report.Check((float)data.GetType().GetField("openingCash").GetValue(data) == 20000f,
                "Daily profit starts before daytime fees and promotion costs.");
            float beforeClose = Read<float>(host, "cash");
            Call(host, "TickJourney", 180f);
            int stars = Property<int>(host, "JourneyStars");
            float ticketIncome = (float)Math.Floor(forecast.ticketIncome * (70 + stars * 10) / 100d);
            float closedCash = Read<float>(host, "cash");
            report.Check(plan.completed && closedCash == beforeClose + Property<int>(host, "JourneyResultBonus") + ticketIncome,
                "Closing pays the frozen, star-scaled ticket receipt together with the night bonus.");
            report.Check(Property<float>(host, "JourneyResultProfit") == closedCash - 20000f,
                "The result includes both daytime expenses and nighttime income.");
            int growth = Property<SimBarBooking.Career>(host, "BookingCareer").growth[accepted];
            Call(host, "SettleJourneyNight"); host.SaveOperations(); Call(host, "LoadOperations"); Call(host, "ContinueJourney");
            report.Check(Read<float>(host, "cash") == closedCash && Property<SimBarBooking.Career>(host, "BookingCareer").growth[accepted] == growth,
                "Reloading or reviewing results never duplicates ticket money or band growth.");
            Call(host, "JourneyNextNight");
            report.Check(Property<bool>(host, "JourneyIsPlanning") && Property<int>(host, "JourneyDay") == 2
                && Property<SimBarBooking.Plan>(host, "DayPlan").blocks == 0
                && Property<SimBarBooking.Career>(host, "BookingCareer").growth[accepted] == growth,
                "The next morning resets four blocks and keeps artist relationships and growth.");
            report.measurements.Add("Daytime wallet/save transaction; fixed venue; resume; locked forecast; day costs in profit; ticket/growth receipt idempotency; next morning.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally { snapshot.Restore(host); }
        return report.ToString();
    }
}
