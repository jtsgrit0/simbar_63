using System;
using UnityEditor;
using UnityEngine;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Floor Mission Rules")]
    public static void CheckFloorMissionsMenu() { Debug.Log(CheckFloorMissions()); }

    /// <summary>No scene, wallet, preferences, network or real guest data is mutated.</summary>
    public static string CheckFloorMissions()
    {
        var report = new Report();
        try
        {
            var state = SimBarFloorMissions.NewState();
            report.Check(state.guestIndex == -1 && state.litter == 0 && !state.cleanupActive && !state.guestSpawned,
                "A fresh shift has no litter, cleanup job or selected guest.");
            string original = JsonUtility.ToJson(state);
            report.Check(SimBarFloorMissions.Throw(state, 0, false).code == "not_performing"
                && SimBarFloorMissions.Throw(state, -1, true).code == "invalid_effect"
                && JsonUtility.ToJson(state) == original,
                "Off-show and invalid stage effects cannot produce litter or audience rewards.");
            var confetti = SimBarFloorMissions.Throw(state, SimBarFloorMissions.Confetti, true);
            report.Check(confetti.success && confetti.litterAdded == SimBarFloorMissions.ConfettiLitter
                && confetti.fanGain > 0 && confetti.energyGain > 0f && state.confettiThrows == 1,
                "Confetti creates its visible performance event, crowd response and matching litter debt together.");
            var tissue = SimBarFloorMissions.Throw(state, SimBarFloorMissions.Tissue, true);
            report.Check(tissue.success && tissue.litterAdded == SimBarFloorMissions.TissueLitter
                && state.tissueThrows == 1 && state.litter == SimBarFloorMissions.ConfettiLitter + SimBarFloorMissions.TissueLitter,
                "Tissue is a distinct effect with its own use count and litter contribution.");
            SimBarFloorMissions.Throw(state, 0, true); SimBarFloorMissions.Throw(state, 1, true);
            original = JsonUtility.ToJson(state);
            report.Check(state.litter == SimBarFloorMissions.MaxLitter && SimBarFloorMissions.ThrowCount(state) == 4
                && !SimBarFloorMissions.Throw(state, 0, true).success && !SimBarFloorMissions.Throw(state, 1, true).success
                && JsonUtility.ToJson(state) == original,
                "Two throws per effect bound litter and prevent repeated crowd rewards.");
            report.Check(SimBarFloorMissions.StartCleanup(state, 2, false).code == "performance_running"
                && SimBarFloorMissions.StartCleanup(state, 0, true).code == "no_workers"
                && !state.cleanupStarted && SimBarFloorMissions.Settle(state).cashDelta == 0f,
                "Cleanup cannot start during the show or without assigned workers; unworked litter never earns money.");
            report.Check(SimBarFloorMissions.StartCleanup(state, 2, true).success && state.cleanupWorkers == 2,
                "After the performance, base employees can be assigned to cleanup.");
            original = JsonUtility.ToJson(state);
            report.Check(SimBarFloorMissions.Tick(state, 5f, 2, false).code == "paused"
                && SimBarFloorMissions.Tick(state, float.NaN, 2, true).code == "invalid_delta"
                && SimBarFloorMissions.Tick(state, -1f, 2, true).code == "invalid_delta"
                && SimBarFloorMissions.Tick(state, 5f, 0, true).code == "no_workers"
                && JsonUtility.ToJson(state) == original,
                "Paused time, invalid time and unavailable workers cannot advance cleanup.");
            var tick = SimBarFloorMissions.Tick(state, 1f, 1, true);
            report.Check(tick.success && state.cleaned == SimBarFloorMissions.CleanupRatePerWorker
                && SimBarFloorMissions.Remaining(state) == SimBarFloorMissions.MaxLitter - 3
                && SimBarFloorMissions.Settle(state).cashDelta == 0f,
                "Only available assigned labor advances work; a partial job has no settlement award.");
            string saved = JsonUtility.ToJson(state);
            state = JsonUtility.FromJson<SimBarFloorMissions.State>(saved); SimBarFloorMissions.Sanitize(state);
            report.Check(state.cleaned == 3f && state.cleanupActive && state.cleanupWorkers == 2,
                "A saved cleanup resumes the exact remaining work and worker assignment.");
            report.Check(SimBarFloorMissions.AssignCleanupWorkers(state, 5).success
                && SimBarFloorMissions.Tick(state, 1f, 5, true).success && state.cleaned == 18f,
                "Additional available employees increase cleanup speed without bypassing work.");
            report.Check(SimBarFloorMissions.AssignCleanupWorkers(state, 0).success && !state.cleanupActive
                && SimBarFloorMissions.Tick(state, 5f, 5, true).code == "not_cleaning",
                "Withdrawing the assignment pauses work without resetting its progress.");
            SimBarFloorMissions.StartCleanup(state, 2, true);
            tick = SimBarFloorMissions.Tick(state, 30f, 2, true);
            report.Check(tick.cleanupFinished && state.cleanupComplete && SimBarFloorMissions.Remaining(state) == 0
                && SimBarFloorMissions.CleanupProgress(state) == 1f && tick.cashDelta == 0f,
                "Actual work completes and removes all litter; completion itself does not pay a duplicate receipt.");
            var receipt = SimBarFloorMissions.Settle(state);
            report.Check(receipt.success && receipt.cashDelta == SimBarFloorMissions.CleanupReward
                && receipt.fanGain == SimBarFloorMissions.CleanupFans && state.cleanupRewardClaimed,
                "Completed cleanup returns one wallet-and-fan receipt for the root transaction.");
            state = JsonUtility.FromJson<SimBarFloorMissions.State>(JsonUtility.ToJson(state)); SimBarFloorMissions.Sanitize(state);
            report.Check(SimBarFloorMissions.Settle(state).code == "already_settled"
                && SimBarFloorMissions.Throw(state, 0, true).code == "cleanup_started",
                "Reloaded cleanup receipts cannot pay again or reopen stage effects after cleanup.");

            state = SimBarFloorMissions.NewState(); original = JsonUtility.ToJson(state);
            report.Check(SimBarFloorMissions.SpawnGuest(state, 2, false).code == "guest_not_eligible"
                && SimBarFloorMissions.SpawnGuest(state, -1, true).code == "invalid_guest"
                && JsonUtility.ToJson(state) == original,
                "An ineligible or invalid patron is never selected for the courteous-exit mission.");
            report.Check(SimBarFloorMissions.SpawnGuest(state, 2, true).success && state.guestActive && state.guestIndex == 2
                && SimBarFloorMissions.SpawnGuest(state, 3, true).code == "guest_already_spawned",
                "One named patron is selected exactly once per shift.");
            original = JsonUtility.ToJson(state);
            report.Check(SimBarFloorMissions.ResolveGuest(state, 2).code == "sequence_needed"
                && SimBarFloorMissions.ResolveGuest(state, 7).code == "invalid_choice"
                && JsonUtility.ToJson(state) == original,
                "The guest cannot be instantly removed by skipping courteous dialogue steps.");
            report.Check(SimBarFloorMissions.ResolveGuest(state, 0).code == "water_offered" && state.guestStep == 1,
                "Pausing service and offering water advances the first hospitality step.");
            report.Check(SimBarFloorMissions.ResolveGuest(state, 3).code == "deescalate"
                && state.guestStep == 0 && state.guestSetbacks == 1,
                "Impatient dialogue steps back into calm conversation without violence or a reward.");
            SimBarFloorMissions.ResolveGuest(state, 0); SimBarFloorMissions.ResolveGuest(state, 1);
            state = JsonUtility.FromJson<SimBarFloorMissions.State>(JsonUtility.ToJson(state)); SimBarFloorMissions.Sanitize(state);
            report.Check(state.guestActive && state.guestStep == 2 && state.guestIndex == 2,
                "The selected guest and courteous dialogue progress survive save/reload.");
            var exited = SimBarFloorMissions.ResolveGuest(state, 2);
            report.Check(exited.guestCompleted && exited.fanGain == SimBarFloorMissions.GuestFans
                && state.guestComplete && !state.guestActive && state.guestStep == 3,
                "The final escort step completes the mission and returns its single audience reward.");
            original = JsonUtility.ToJson(state);
            report.Check(SimBarFloorMissions.ResolveGuest(state, 2).code == "guest_already_complete"
                && SimBarFloorMissions.SpawnGuest(state, 3, true).code == "guest_already_spawned"
                && JsonUtility.ToJson(state) == original,
                "Repeated escort or spawn callbacks cannot duplicate completion or rewards.");
            state = JsonUtility.FromJson<SimBarFloorMissions.State>(JsonUtility.ToJson(state)); SimBarFloorMissions.Sanitize(state);
            report.Check(SimBarFloorMissions.ResolveGuest(state, 2).fanGain == 0 && state.guestComplete,
                "A reloaded completed guest remains completed and earns nothing twice.");

            var corrupt = new SimBarFloorMissions.State { confettiThrows = 999, tissueThrows = -10, litter = 99999,
                cleaned = float.NaN, cleanupStarted = false, cleanupComplete = true, cleanupWorkers = 999,
                guestSpawned = false, guestActive = true, guestIndex = 999, guestStep = 99, guestSetbacks = -3 };
            SimBarFloorMissions.Sanitize(corrupt);
            report.Check(corrupt.litter == 2 * SimBarFloorMissions.ConfettiLitter && corrupt.cleaned == 0f
                && !corrupt.cleanupComplete && !corrupt.cleanupActive && corrupt.cleanupWorkers == 0
                && !corrupt.guestActive && corrupt.guestIndex == -1 && corrupt.guestStep == 0 && corrupt.guestSetbacks == 0,
                "Malformed counters, nonfinite work and unspawned guest selections are repaired without inventing completion.");
            report.Check(SimBarFloorMissions.Settle(corrupt).cashDelta == 0f,
                "Sanitizing an incomplete corrupt save cannot manufacture a cleanup payment.");
            var old = JsonUtility.FromJson<SimBarFloorMissions.State>("{}"); SimBarFloorMissions.Sanitize(old);
            report.Check(old.guestIndex == -1 && old.litter == 0 && !old.cleanupActive && !old.guestActive,
                "Missing legacy state defaults to no new mission, debt or automatic payout.");
            report.measurements.Add("Bounded confetti/tissue events; post-show staff work; pause/worker guards; partial and committed JSON recovery; unique cleanup receipt; ordered courteous guest exit.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        return report.ToString();
    }
}
