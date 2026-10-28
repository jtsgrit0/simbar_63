using System;

/// <summary>
/// Pure rules for stage effects, the staff's post-show cleanup and a courteous guest exit.
/// The caller applies Result rewards and saves them together with this state. No money,
/// scene objects, clocks, random numbers or external communication are owned here.
/// </summary>
public static class SimBarFloorMissions
{
    public const int Confetti = 0;
    public const int Tissue = 1;
    public const int ThrowsPerEffect = 2;
    public const int ConfettiLitter = 24;
    public const int TissueLitter = 32;
    public const int MaxLitter = ThrowsPerEffect * (ConfettiLitter + TissueLitter);
    public const int MaxCleanupWorkers = 5;
    public const float CleanupRatePerWorker = 3f;
    public const float CleanupReward = 70f;
    public const int CleanupFans = 3;
    public const int GuestFans = 4;
    public const int MaxGuestIndex = 127;

    // Guest action ids: 0 pause service / offer water; 1 courteous request and escort offer;
    // 2 accompany the guest to the exit; 3 impatient dialogue (one-step setback).
    public const int OfferWater = 0;
    public const int PoliteRequest = 1;
    public const int EscortToExit = 2;
    public const int Impatient = 3;

    [Serializable]
    public sealed class State
    {
        public int version = 1;
        public int confettiThrows;
        public int tissueThrows;
        // Total litter produced, not remaining litter. Remaining() subtracts cleaned.
        public int litter;
        public float cleaned;
        public bool cleanupStarted;
        public bool cleanupActive;
        public bool cleanupComplete;
        public int cleanupWorkers;
        public bool cleanupRewardClaimed;
        public bool guestSpawned;
        public bool guestActive;
        public bool guestComplete;
        public int guestStep;
        public int guestIndex = -1;
        public int guestSetbacks;
    }

    public struct Result
    {
        public bool success;
        public string code;
        public int fanGain;
        public float cashDelta;
        public float energyGain;
        public int litterAdded;
        public bool cleanupFinished;
        public bool guestCompleted;
    }

    public static State NewState() { return new State(); }

    /// <summary>Repairs saved fields without issuing rewards or completing unstarted work.</summary>
    public static void Sanitize(State state)
    {
        if (state == null) return;
        state.version = 1;
        state.confettiThrows = Clamp(state.confettiThrows, 0, ThrowsPerEffect);
        state.tissueThrows = Clamp(state.tissueThrows, 0, ThrowsPerEffect);
        state.litter = state.confettiThrows * ConfettiLitter + state.tissueThrows * TissueLitter;
        state.cleanupWorkers = Clamp(state.cleanupWorkers, 0, MaxCleanupWorkers);
        state.cleaned = Finite(state.cleaned) ? Math.Max(0f, Math.Min(state.litter, state.cleaned)) : 0f;
        if (state.cleanupRewardClaimed && state.litter > 0)
        {
            // A committed receipt is never made claimable again by a partial/corrupt load.
            state.cleanupStarted = true;
            state.cleaned = state.litter;
        }
        if (!state.cleanupStarted || state.litter == 0)
        {
            state.cleanupStarted = false;
            state.cleaned = 0f;
            state.cleanupComplete = false;
            state.cleanupActive = false;
            state.cleanupWorkers = 0;
        }
        else
        {
            state.cleanupComplete = state.cleaned >= state.litter;
            state.cleanupActive = !state.cleanupComplete && state.cleanupWorkers > 0;
        }
        state.guestSetbacks = Clamp(state.guestSetbacks, 0, 1000);
        if (state.guestComplete)
        {
            state.guestSpawned = true;
            state.guestActive = false;
            state.guestStep = 3;
            state.guestIndex = Clamp(state.guestIndex, 0, MaxGuestIndex);
        }
        else if (!state.guestSpawned)
        {
            state.guestActive = false;
            state.guestStep = 0;
            state.guestIndex = -1;
        }
        else
        {
            state.guestActive = true;
            state.guestStep = Clamp(state.guestStep, 0, 2);
            state.guestIndex = Clamp(state.guestIndex, 0, MaxGuestIndex);
        }
    }

    public static int ThrowCount(State state)
    {
        return state == null ? 0 : Clamp(state.confettiThrows, 0, ThrowsPerEffect) + Clamp(state.tissueThrows, 0, ThrowsPerEffect);
    }

    public static int Remaining(State state)
    {
        if (state == null || !Finite(state.cleaned)) return 0;
        return Clamp((int)Math.Ceiling(state.litter - state.cleaned), 0, MaxLitter);
    }

    public static float CleanupProgress(State state)
    {
        return state == null || state.litter <= 0 || !Finite(state.cleaned) ? 0f
            : Math.Max(0f, Math.Min(1f, state.cleaned / state.litter));
    }

    /// <summary>
    /// Each effect may be thrown twice per night. performanceActive must be false in the
    /// lobby, pause, guide, post-show cleanup or results. Successful calls create both the
    /// crowd response and the litter obligation; replaying a capped effect creates neither.
    /// </summary>
    public static Result Throw(State state, int effect, bool performanceActive)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (!performanceActive) return Fail("not_performing");
        if (effect != Confetti && effect != Tissue) return Fail("invalid_effect");
        if (state.cleanupStarted || state.cleanupComplete || state.cleanupRewardClaimed) return Fail("cleanup_started");
        int count = effect == Confetti ? state.confettiThrows : state.tissueThrows;
        if (count >= ThrowsPerEffect) return Fail("effect_limit");
        int added = effect == Confetti ? ConfettiLitter : TissueLitter;
        if (effect == Confetti) state.confettiThrows++; else state.tissueThrows++;
        state.litter += added;
        var result = Ok(effect == Confetti ? "confetti" : "tissue");
        result.fanGain = 2;
        result.energyGain = 0.04f;
        result.litterAdded = added;
        return result;
    }

    /// <summary>The caller opens performanceOver in its final-set or post-show phase.</summary>
    public static Result StartCleanup(State state, int workers, bool performanceOver)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (!performanceOver) return Fail("performance_running");
        if (state.cleanupRewardClaimed || state.cleanupComplete) return Fail("already_clean");
        if (state.litter == 0) return Fail("no_litter");
        if (workers < 1 || workers > MaxCleanupWorkers) return Fail("no_workers");
        if (state.cleanupActive) return Fail("already_cleaning");
        state.cleanupStarted = true;
        state.cleanupActive = true;
        state.cleanupWorkers = workers;
        return Ok("cleanup_started");
    }

    public static Result AssignCleanupWorkers(State state, int workers)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (!state.cleanupStarted || state.cleanupComplete) return Fail("not_cleaning");
        if (workers < 0 || workers > MaxCleanupWorkers) return Fail("invalid_workers");
        state.cleanupWorkers = workers;
        state.cleanupActive = workers > 0;
        return Ok(workers > 0 ? "workers_assigned" : "cleanup_waiting");
    }

    /// <summary>
    /// Call only for real simulation time; simulationRunning=false freezes all work.
    /// Available workers bound the assignment. Completion itself never pays out: Settle
    /// returns the unique receipt to commit atomically with the root wallet and fan count.
    /// </summary>
    public static Result Tick(State state, float delta, int availableWorkers, bool simulationRunning)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (!simulationRunning) return Fail("paused");
        if (!Finite(delta) || delta <= 0f) return Fail("invalid_delta");
        if (!state.cleanupActive || state.cleanupComplete) return Fail("not_cleaning");
        int workers = Math.Min(state.cleanupWorkers, Clamp(availableWorkers, 0, MaxCleanupWorkers));
        if (workers <= 0) return Fail("no_workers");
        // Bound a malformed delta without allowing float overflow or negative work.
        delta = Math.Min(delta, 180f);
        state.cleaned = Math.Min(state.litter, state.cleaned + delta * workers * CleanupRatePerWorker);
        var result = Ok("cleaning");
        if (state.cleaned >= state.litter)
        {
            state.cleaned = state.litter;
            state.cleanupComplete = true;
            state.cleanupActive = false;
            result.code = "cleanup_complete";
            result.cleanupFinished = true;
        }
        return result;
    }

    /// <summary>Claim only after actual assigned cleanup work has completed.</summary>
    public static Result Settle(State state)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (state.cleanupRewardClaimed) return Fail("already_settled");
        if (!state.cleanupStarted || state.litter <= 0 || !state.cleanupComplete || state.cleaned < state.litter)
            return Fail("incomplete_cleanup");
        state.cleanupRewardClaimed = true;
        var result = Ok("cleanup_reward");
        result.cashDelta = CleanupReward;
        result.fanGain = CleanupFans;
        return result;
    }

    /// <summary>The root chooses an eligible non-reserved patron once per night.</summary>
    public static Result SpawnGuest(State state, int guestIndex, bool eligible)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (!eligible) return Fail("guest_not_eligible");
        if (state.guestSpawned) return Fail("guest_already_spawned");
        if (guestIndex < 0 || guestIndex > MaxGuestIndex) return Fail("invalid_guest");
        state.guestSpawned = true;
        state.guestActive = true;
        state.guestStep = 0;
        state.guestIndex = guestIndex;
        return Ok("guest_arrived");
    }

    /// <summary>
    /// The three courteous dialogue actions have a deliberate order. Impatient dialogue
    /// steps back without violence or a monetary penalty. Offering water is a fictional
    /// hospitality action, never a claim that intoxication is medically reversed.
    /// </summary>
    public static Result ResolveGuest(State state, int choice)
    {
        if (!Valid(state)) return Fail("invalid_state");
        if (state.guestComplete) return Fail("guest_already_complete");
        if (!state.guestActive) return Fail("no_guest");
        if (choice < OfferWater || choice > Impatient) return Fail("invalid_choice");
        if (choice == Impatient)
        {
            state.guestStep = Math.Max(0, state.guestStep - 1);
            state.guestSetbacks = Math.Min(1000, state.guestSetbacks + 1);
            return Ok("deescalate");
        }
        if (choice != state.guestStep) return Fail("sequence_needed");
        state.guestStep++;
        if (state.guestStep < 3) return Ok(state.guestStep == 1 ? "water_offered" : "escort_offered");
        state.guestComplete = true;
        state.guestActive = false;
        var result = Ok("guest_complete");
        result.guestCompleted = true;
        result.fanGain = GuestFans;
        return result;
    }

    private static bool Valid(State state)
    {
        return state != null && state.version == 1
            && state.confettiThrows >= 0 && state.confettiThrows <= ThrowsPerEffect
            && state.tissueThrows >= 0 && state.tissueThrows <= ThrowsPerEffect
            && state.litter == state.confettiThrows * ConfettiLitter + state.tissueThrows * TissueLitter
            && Finite(state.cleaned) && state.cleaned >= 0f && state.cleaned <= state.litter
            && state.cleanupWorkers >= 0 && state.cleanupWorkers <= MaxCleanupWorkers
            && (!state.cleanupComplete || (state.cleanupStarted && state.litter > 0 && state.cleaned >= state.litter))
            && (!state.cleanupActive || (state.cleanupStarted && !state.cleanupComplete && state.cleanupWorkers > 0))
            && (state.cleanupStarted || state.cleaned == 0f)
            && state.guestStep >= 0 && state.guestStep <= 3
            && (!state.guestComplete || (state.guestSpawned && !state.guestActive && state.guestStep == 3))
            && (!state.guestActive || (state.guestSpawned && !state.guestComplete && state.guestStep < 3
                && state.guestIndex >= 0 && state.guestIndex <= MaxGuestIndex))
            && state.guestSetbacks >= 0 && state.guestSetbacks <= 1000;
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static int Clamp(int value, int min, int max) { return Math.Min(max, Math.Max(min, value)); }
    private static Result Fail(string code) { return new Result { code = code }; }
    private static Result Ok(string code) { return new Result { success = true, code = code }; }
}
