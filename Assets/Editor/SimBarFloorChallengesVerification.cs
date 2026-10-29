using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Spontaneous Floor Challenge Rules")]
    public static void CheckFloorChallengesMenu() { Debug.Log(CheckFloorChallenges()); }

    public static string CheckFloorChallenges()
    {
        var report = new Report();
        try
        {
            var signatures = new HashSet<string>(); bool paperAbsent = false, guestAbsent = false, bothPresent = false;
            bool validSchedules = true;
            for (int seed = 1; seed <= 256; seed++)
            {
                var candidate = SimBarFloorChallenges.NewState(seed);
                signatures.Add(candidate.paperCount + "/" + candidate.paperAt0 + "/" + candidate.paperKind0 + "/" + candidate.guestScheduled + "/" + candidate.guestAt);
                paperAbsent |= candidate.paperCount == 0; guestAbsent |= !candidate.guestScheduled;
                bothPresent |= candidate.paperCount > 0 && candidate.guestScheduled;
                int confetti = 0, tissue = 0; float previous = 20f;
                for (int i = 0; i < candidate.paperCount; i++)
                {
                    float at = i == 0 ? candidate.paperAt0 : i == 1 ? candidate.paperAt1 : candidate.paperAt2;
                    int kind = i == 0 ? candidate.paperKind0 : i == 1 ? candidate.paperKind1 : candidate.paperKind2;
                    if (kind == 0) confetti++; else if (kind == 1) tissue++;
                    validSchedules &= at >= previous && at <= 135f && (kind == 0 || kind == 1); previous = at;
                }
                validSchedules &= candidate.paperCount <= 3 && confetti <= 2 && tissue <= 2
                    && (!candidate.guestScheduled || candidate.guestAt >= 40f && candidate.guestAt <= 140f);
            }
            report.Check(signatures.Count > 100 && paperAbsent && guestAbsent && bothPresent,
                "Different seeds vary event times and occurrence, including nights without each event and nights with both.");
            report.Check(validSchedules, "Generated random schedules remain ordered, within show time, and bounded to two throws of either paper kind.");
            report.Check(JsonUtility.ToJson(SimBarFloorChallenges.NewState(91)) == JsonUtility.ToJson(SimBarFloorChallenges.NewState(91)),
                "One saved seed generates a stable schedule rather than rerolling on each frame.");
            var late = SimBarFloorChallenges.NewState(91, 120f);
            report.Check((late.paperCount == 0 || late.paperAt0 >= 128f) && (!late.guestScheduled || late.guestAt >= 128f)
                && SimBarFloorChallenges.NewState(91, 166f).paperCount == 0 && !SimBarFloorChallenges.NewState(91, 166f).guestScheduled,
                "Legacy mid-show migration schedules only future events; a nearly ended show gains no sudden backlog.");

            var state = new SimBarFloorChallenges.State { seed = 3, paperCount = 3, paperKind0 = 0, paperKind1 = 1, paperKind2 = 0,
                paperAt0 = 22f, paperAt1 = 60f, paperAt2 = 110f, guestScheduled = true, guestAt = 81f };
            string original = JsonUtility.ToJson(state);
            var scheduled = SimBarFloorChallenges.AdvanceSchedule(state, 180f, false);
            report.Check(scheduled.confetti == 0 && scheduled.tissue == 0 && !scheduled.guest && JsonUtility.ToJson(state) == original,
                "A paused shift cannot fire scheduled paper or the assistance guest.");
            scheduled = SimBarFloorChallenges.AdvanceSchedule(state, 59f, true);
            report.Check(scheduled.confetti == 1 && scheduled.tissue == 0 && !scheduled.guest && state.paperFired == 1,
                "Advancing elapsed time fires only the events that are due.");
            state = JsonUtility.FromJson<SimBarFloorChallenges.State>(JsonUtility.ToJson(state));
            scheduled = SimBarFloorChallenges.AdvanceSchedule(state, 180f, true);
            report.Check(scheduled.confetti == 1 && scheduled.tissue == 1 && scheduled.guest && state.paperFired == 3 && state.guestFired,
                "Save/reload preserves fired flags and delivers each remaining event once.");
            original = JsonUtility.ToJson(state); scheduled = SimBarFloorChallenges.AdvanceSchedule(state, 180f, true);
            report.Check(scheduled.confetti == 0 && scheduled.tissue == 0 && !scheduled.guest && JsonUtility.ToJson(state) == original,
                "Revisiting the same elapsed time cannot repeat paper, guests or associated rewards.");

            state = SimBarFloorChallenges.NewState(1); state.paperCount = 0; state.guestScheduled = false;
            report.Check(SimBarFloorChallenges.Start(state, SimBarFloorChallenges.Cleanup, 0)
                && !SimBarFloorChallenges.Start(state, SimBarFloorChallenges.Guest, 0),
                "Only one reaction round can own the input at a time.");
            original = JsonUtility.ToJson(state);
            report.Check(!SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Guest, true).accepted
                && !SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Cleanup, false).accepted
                && !SimBarFloorChallenges.Tick(state, 3f, false).timedOut && JsonUtility.ToJson(state) == original,
                "Wrong keys and paused input neither count taps nor consume the round timer.");
            var press = SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Cleanup, true);
            report.Check(press.accepted && !press.completed && state.presses == 1
                && !SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Cleanup, true).accepted && state.presses == 1,
                "An initial edge press counts once; same-frame repeats or a held key cannot synthesize rapid taps.");
            SimBarFloorChallenges.Tick(state, .03f, true);
            report.Check(!SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Cleanup, true).accepted && state.presses == 1,
                "Input faster than the debounce interval cannot inflate progress.");
            var timeout = SimBarFloorChallenges.Tick(state, 8f, true);
            report.Check(timeout.timedOut && timeout.kind == SimBarFloorChallenges.Cleanup && state.activeKind == SimBarFloorChallenges.None
                && state.timeouts == 1 && state.cleanupWorkBudget == 0f && !SimBarFloorChallenges.Start(state, SimBarFloorChallenges.Cleanup, 0),
                "A missed cleanup window grants no labor and enters a brief retry cooldown.");
            SimBarFloorChallenges.Tick(state, 1.5f, true);
            report.Check(SimBarFloorChallenges.Start(state, SimBarFloorChallenges.Cleanup, 0), "A timed-out cleanup can retry without losing or inventing completed work.");
            for (int i = 0; i < SimBarFloorChallenges.CleanupPresses; i++)
            { if (i > 0) SimBarFloorChallenges.Tick(state, .1f, true); press = SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Cleanup, true); }
            report.Check(press.completed && press.kind == SimBarFloorChallenges.Cleanup && state.cleanupWorkBudget == SimBarFloorChallenges.CleanupPulseWork
                && state.completedRounds == 1 && state.activeKind == SimBarFloorChallenges.None,
                "Ten distinct timely cleanup presses earn one bounded work budget, rather than instantly removing litter.");
            report.Check(!SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Cleanup, true).accepted
                && state.cleanupWorkBudget == SimBarFloorChallenges.CleanupPulseWork,
                "Repeating the completed callback cannot award a second labor pulse.");

            SimBarFloorChallenges.Tick(state, .5f, true); SimBarFloorChallenges.Start(state, SimBarFloorChallenges.Guest, 1);
            for (int i = 0; i < 3; i++)
            { if (i > 0) SimBarFloorChallenges.Tick(state, .1f, true); SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Guest, true); }
            original = JsonUtility.ToJson(state);
            state = JsonUtility.FromJson<SimBarFloorChallenges.State>(original); SimBarFloorChallenges.Sanitize(state);
            report.Check(JsonUtility.ToJson(state) == original && state.activeKind == SimBarFloorChallenges.Guest && state.activeStage == 1 && state.presses == 3,
                "An active dialogue reaction saves its stage, partial taps, exact remaining time and input debounce age.");
            for (int i = 3; i < SimBarFloorChallenges.GuestPresses; i++)
            { SimBarFloorChallenges.Tick(state, .1f, true); press = SimBarFloorChallenges.Press(state, SimBarFloorChallenges.Guest, true); }
            report.Check(press.completed && press.kind == SimBarFloorChallenges.Guest && press.stage == 1
                && state.cleanupWorkBudget == SimBarFloorChallenges.CleanupPulseWork && state.completedRounds == 2,
                "Eight guest presses finish only their saved dialogue stage and cannot create cleanup labor.");
            SimBarFloorChallenges.Tick(state, .5f, true); SimBarFloorChallenges.Start(state, SimBarFloorChallenges.Guest, 2);
            original = JsonUtility.ToJson(state);
            SimBarFloorChallenges.Tick(state, float.NaN, true); SimBarFloorChallenges.Tick(state, -1f, true);
            report.Check(JsonUtility.ToJson(state) == original, "Invalid deltas cannot expire or advance a reaction round.");
            timeout = SimBarFloorChallenges.Tick(state, SimBarFloorChallenges.GuestSeconds, true);
            report.Check(timeout.timedOut && timeout.kind == SimBarFloorChallenges.Guest && timeout.stage == 2 && state.timeouts == 2,
                "A missed guest reaction reports the original courteous stage for a retry instead of advancing the guest.");
            var corrupt = new SimBarFloorChallenges.State { paperCount = 999, paperFired = -1, paperAt0 = float.NaN,
                guestScheduled = true, guestAt = float.PositiveInfinity, activeKind = 99, presses = 999, cleanupWorkBudget = float.NaN,
                cooldown = -1f, pressAge = float.NaN, timeouts = -1, completedRounds = -1 };
            SimBarFloorChallenges.Sanitize(corrupt);
            report.Check(corrupt.paperCount == 0 && corrupt.paperFired == 0 && !corrupt.guestScheduled
                && corrupt.activeKind == SimBarFloorChallenges.None && corrupt.cleanupWorkBudget == 0f && corrupt.completedRounds == 0,
                "Corrupt saves cancel invalid schedules and rounds without manufacturing events, work or successful reactions.");
            report.measurements.Add("256 schedule seeds; one-shot saved events; paused/wrong/held/debounced input; cleanup timeout/retry; budget-only labor pulse; active-stage JSON recovery; guest timeout and corrupt-save repair.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        return report.ToString();
    }
}
