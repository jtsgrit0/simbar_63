using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Day Booking Model")]
    public static void CheckBookingMenu() { Debug.Log(CheckBooking()); }

    /// <summary>Pure model checks: no scene, wallet, PlayerPrefs, network or real artist data.</summary>
    public static string CheckBooking()
    {
        var report = new Report();
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        try
        {
            var career = SimBarBooking.NewCareer();
            var plan = SimBarBooking.NewPlan(1, 0);
            var genres = new HashSet<string>();
            foreach (var band in SimBarBooking.Bands12) genres.Add(band.genre);
            report.Check(SimBarBooking.Venues4.Length == 4 && SimBarBooking.Bands12.Length == 12 && genres.Count == 12,
                "Four venues and twelve distinct fictional newcomer genres exist.");
            bool safeDefinitions = true;
            foreach (var venue in SimBarBooking.Venues4) safeDefinitions &= venue.capacity >= 60 && venue.capacity <= 120;
            foreach (var band in SimBarBooking.Bands12) safeDefinitions &= band.baseFee >= 80 && band.baseFee <= 220;
            report.Check(safeDefinitions, "GAME capacities and fictional newcomer fees stay inside the intended bounds.");
            report.Check(SimBarBooking.Hour(plan) == 10 && plan.ticketTier == 1 && SimBarBooking.CanAct(plan),
                "New plans start at 10:00 with the standard ticket and four action blocks.");

            int beforeChance = SimBarBooking.Chance(plan, career, 0, 0);
            report.Check(SimBarBooking.Listen(plan, career, 0).success
                && SimBarBooking.Chance(plan, career, 0, 0) == beforeChance + 20 && SimBarBooking.Hour(plan) == 12,
                "Listening is free, consumes one two-hour block and adds twenty acceptance points.");
            string state = BookingSnapshot(plan, career);
            report.Check(!SimBarBooking.Listen(plan, career, 0).success && BookingSnapshot(plan, career) == state,
                "Listening twice to one band has no effect.");
            for (int i = 1; i < 4; i++) SimBarBooking.Listen(plan, career, i);
            state = BookingSnapshot(plan, career);
            report.Check(SimBarBooking.Hour(plan) == 18 && !SimBarBooking.CanAct(plan)
                && SimBarBooking.Offer(plan, career, 4, 1, 1000f).code == "no_time"
                && BookingSnapshot(plan, career) == state, "Four blocks end at 18:00 and a fifth action cannot spend or alter state.");

            plan = SimBarBooking.NewPlan(2, 0);
            state = BookingSnapshot(plan, career);
            var tooPoor = SimBarBooking.Offer(plan, career, 0, 0, SimBarBooking.Fee(0, 0) - 1f);
            report.Check(tooPoor.code == "insufficient_cash" && tooPoor.cashDelta == 0f && BookingSnapshot(plan, career) == state,
                "An unaffordable offer has no cash or time side effects.");
            bool invalidSafe = !SimBarBooking.Offer(plan, career, -1, 0, 1000f).success
                && !SimBarBooking.Offer(plan, career, 0, 2, 1000f).success
                && !SimBarBooking.Offer(plan, career, 0, 0, float.NaN).success
                && !SimBarBooking.Promote(plan, career, float.PositiveInfinity).success
                && !SimBarBooking.Listen(plan, career, 12).success;
            SimBarBooking.SetTicket(plan, 3);
            report.Check(invalidSafe && BookingSnapshot(plan, career) == state, "Invalid artist, package, ticket and nonfinite budget inputs leave state unchanged.");
            report.Check(Math.Abs(SimBarBooking.Fee(0, 1) - SimBarBooking.Fee(0, 0) * 1.35f) < 0.001f
                && SimBarBooking.Chance(plan, career, 0, 1) == SimBarBooking.Chance(plan, career, 0, 0) + 15,
                "The generous offer costs 135 percent and adds fifteen acceptance points.");

            var twin = SimBarBooking.NewPlan(2, 0);
            var offer = SimBarBooking.Offer(plan, career, 0, 1, 1000f);
            var repeat = SimBarBooking.Offer(twin, SimBarBooking.NewCareer(), 0, 1, 1000f);
            report.Check(offer.code == repeat.code && offer.cashDelta == repeat.cashDelta
                && JsonUtility.ToJson(plan) == JsonUtility.ToJson(twin), "Identical saved inputs produce identical outcomes and plans.");
            state = BookingSnapshot(plan, career);
            report.Check(SimBarBooking.Offer(plan, career, 0, 1, 1000f).code == "already_attempted"
                && BookingSnapshot(plan, career) == state, "A band receives at most one offer per day, including rejected offers.");

            bool everyGenreBookable = true;
            bool foundRejection = false;
            for (int venue = 0; venue < 4; venue++)
            {
                for (int band = 0; band < 12; band++)
                {
                    bool accepted = false;
                    for (int day = 1; day <= 128 && !accepted; day++)
                    {
                        var candidate = SimBarBooking.NewPlan(day, venue);
                        var outcome = SimBarBooking.Offer(candidate, career, band, 0, 1000f);
                        if (outcome.success)
                        {
                            accepted = outcome.cashDelta == -SimBarBooking.Fee(band, 0) && candidate.blocks == 1;
                        }
                        else if (outcome.code == "rejected")
                        {
                            foundRejection |= outcome.cashDelta == 0f && candidate.blocks == 1 && candidate.attemptedMask == (1 << band);
                        }
                    }
                    everyGenreBookable &= accepted;
                }
            }
            report.Check(everyGenreBookable, "Every one of twelve genres can book every venue and acceptance charges the full fee.");
            report.Check(foundRejection, "A deterministic rejection costs one block and zero credits.");

            plan = BookingAcceptedPlan(career);
            SimBarBooking.Metrics regular = SimBarBooking.Forecast(plan, career);
            int blocks = plan.blocks;
            SimBarBooking.SetTicket(plan, 0);
            SimBarBooking.Metrics cheap = SimBarBooking.Forecast(plan, career);
            SimBarBooking.SetTicket(plan, 2);
            SimBarBooking.Metrics premium = SimBarBooking.Forecast(plan, career);
            report.Check(cheap.ticketPrice == 10f && regular.ticketPrice == 15f && premium.ticketPrice == 20f
                && cheap.attendance >= regular.attendance && regular.attendance >= premium.attendance && plan.blocks == blocks,
                "The free 10/15/20-credit ticket choice trades attendance against price.");
            state = BookingSnapshot(plan, career);
            report.Check(!SimBarBooking.Promote(plan, career, 59f).success && BookingSnapshot(plan, career) == state,
                "Unaffordable promotion spends neither money nor time.");
            var promoted = SimBarBooking.Promote(plan, career, 60f);
            var promotedForecast = SimBarBooking.Forecast(plan, career);
            report.Check(promoted.success && promoted.cashDelta == -60f && plan.blocks == blocks + 1
                && promotedForecast.attendance > premium.attendance, "A paid sixty-credit promotion consumes one block and raises attendance.");
            report.Check(promotedForecast.attendance <= promotedForecast.venueCapacity && promotedForecast.energyBonus <= 0.15f
                && promotedForecast.orderIntervalMultiplier >= 0.75f, "The crowd, energy and order-rate benefits remain bounded.");

            SimBarBooking.Lock(plan);
            string frozen = JsonUtility.ToJson(SimBarBooking.Forecast(plan, career));
            career.growth[0] = 100; career.relationships[0] = 100;
            SimBarBooking.SetTicket(plan, 0);
            state = BookingSnapshot(plan, career);
            report.Check(!SimBarBooking.Promote(plan, career, 1000f).success && !SimBarBooking.Listen(plan, career, 1).success
                && BookingSnapshot(plan, career) == state && JsonUtility.ToJson(SimBarBooking.Forecast(plan, career)) == frozen,
                "Launching freezes the forecast and closes planning regardless of later career changes.");
            var paid = SimBarBooking.Complete(plan, career, 3);
            report.Check(paid.success && paid.income == promotedForecast.ticketIncome && paid.cashDelta == paid.income
                && plan.completed && career.relationships[0] <= 100 && career.growth[0] <= 100,
                "Three-star settlement pays the frozen ticket forecast and bounds career progress.");
            state = BookingSnapshot(plan, career);
            var duplicate = SimBarBooking.Complete(plan, career, 3);
            report.Check(!duplicate.success && duplicate.cashDelta == 0f && duplicate.income == 0f && BookingSnapshot(plan, career) == state,
                "Completing twice never duplicates ticket income or career progress.");
            plan = JsonUtility.FromJson<SimBarBooking.Plan>(JsonUtility.ToJson(plan));
            career = JsonUtility.FromJson<SimBarBooking.Career>(JsonUtility.ToJson(career));
            SimBarBooking.Sanitize(plan, career);
            report.Check(SimBarBooking.Complete(plan, career, 3).code == "already_completed",
                "A completed save remains idempotent after serialization and reload.");
            var lowStars = BookingAcceptedPlan(SimBarBooking.NewCareer());
            float fullIncome = SimBarBooking.Forecast(lowStars, career).ticketIncome;
            var lowPaid = SimBarBooking.Complete(lowStars, SimBarBooking.NewCareer(), 0);
            report.Check(lowPaid.income == (float)Math.Floor(fullIncome * 70d / 100d), "Zero-star service pays seventy percent of the ticket forecast.");
            var openMic = SimBarBooking.NewPlan(1, 0);
            SimBarBooking.Promote(openMic, career, 60f);
            report.Check(SimBarBooking.Forecast(openMic, career).ticketIncome == 0f
                && SimBarBooking.Complete(openMic, career, 3).income == 0f, "An unbooked community open mic never creates ticket cash, even after promotion.");

            bool foundFull = false;
            for (int day = 1; day <= 128 && !foundFull; day++)
            {
                var full = SimBarBooking.NewPlan(day, 0);
                for (int i = 0; i < 3; i++) SimBarBooking.Offer(full, career, i, 1, 1000f);
                if (full.bookedMask != 7) continue;
                state = BookingSnapshot(full, career);
                foundFull = SimBarBooking.Offer(full, career, 3, 1, 1000f).code == "lineup_full" && BookingSnapshot(full, career) == state;
            }
            report.Check(foundFull, "A three-band lineup cannot accept or charge a fourth band.");

            var corrupt = new SimBarBooking.Plan
            {
                day = -9, venue = 99, blocks = 99, listenedMask = -1, attemptedMask = -1, bookedMask = -1,
                promotions = 99, ticketTier = 99, bookedGrowth = null, locked = true, hasLockedMetrics = true,
                lockedMetrics = new SimBarBooking.Metrics { attendance = int.MaxValue, ticketIncome = float.NaN,
                    ticketPrice = float.PositiveInfinity, energyBonus = float.NaN, fanBonus = int.MaxValue, orderIntervalMultiplier = float.NegativeInfinity }
            };
            career = new SimBarBooking.Career { relationships = new[] { -8, 1000 }, growth = null };
            SimBarBooking.Sanitize(corrupt, career);
            var repaired = SimBarBooking.Forecast(corrupt, career);
            report.Check(corrupt.day == 1 && corrupt.venue == 3 && corrupt.blocks == 4 && corrupt.bookedMask == 7
                && corrupt.ticketTier == 2 && corrupt.bookedGrowth.Length == 12 && career.relationships.Length == 12
                && career.growth.Length == 12 && career.relationships[0] == 0 && career.relationships[1] == 100
                && repaired.attendance <= repaired.venueCapacity && repaired.ticketIncome == 0f && repaired.energyBonus == 0f,
                "Corrupt masks, ranges, short or null arrays and nonfinite frozen metrics are repaired safely.");
            var legacy = JsonUtility.FromJson<SimBarBooking.Plan>("{}");
            SimBarBooking.Sanitize(legacy, new SimBarBooking.Career());
            report.Check(SimBarBooking.Hour(legacy) == 10 && SimBarBooking.Forecast(legacy, career).ticketIncome == 0f,
                "Missing/default-zero saved fields preserve a valid cash-free opening plan.");
            SimBarBooking.Sanitize(null, null);
            SimBarBooking.Lock(null);
            SimBarBooking.SetTicket(null, 1);
            report.Check(!SimBarBooking.CanAct(null) && SimBarBooking.Hour(null) == 10
                && SimBarBooking.Chance(null, null, -1, -1) == 0 && SimBarBooking.Fee(-1, -1) == 0f
                && !SimBarBooking.Listen(null, null, 0).success && !SimBarBooking.Offer(null, null, 0, 0, 0f).success
                && !SimBarBooking.Promote(null, null, 0f).success && !SimBarBooking.Complete(null, null, 0).success
                && SimBarBooking.Forecast(null, null).ticketIncome == 0f, "All public entry points handle missing or invalid inputs defensively.");

            report.Check(UnityEngine.Random.state.Equals(originalRandom), "Booking never consumes or resets UnityEngine.Random state.");
            report.measurements.Add("Deterministic System-only booking; 48 venue/genre pairs; four blocks; full upfront fees; frozen evening; idempotent save/reload settlement.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally { UnityEngine.Random.state = originalRandom; }
        return report.ToString();
    }

    private static string BookingSnapshot(SimBarBooking.Plan plan, SimBarBooking.Career career)
    {
        return JsonUtility.ToJson(plan) + JsonUtility.ToJson(career);
    }

    private static SimBarBooking.Plan BookingAcceptedPlan(SimBarBooking.Career career)
    {
        for (int day = 1; day <= 128; day++)
        {
            var plan = SimBarBooking.NewPlan(day, 0);
            if (SimBarBooking.Offer(plan, career, 0, 0, 1000f).success) return plan;
        }
        throw new InvalidOperationException("No deterministic accepted sample found.");
    }
}
