using System;

/// <summary>
/// Saved spontaneous-event schedules and short reaction rounds. The caller owns
/// input, employees, dialogue, paper effects and rewards; this model owns no Unity state.
/// </summary>
public static class SimBarFloorChallenges
{
    public const int None = 0;
    public const int Cleanup = 1;
    public const int Guest = 2;
    public const int GuestPresses = 8;
    public const float GuestSeconds = 6f;
    public const int CleanupPresses = 10;
    public const float CleanupSeconds = 7f;
    public const float CleanupPulseWork = 32f;
    public const float MinimumPressInterval = .06f;
    private const int MaximumLitter = 112;

    [Serializable]
    public sealed class State
    {
        public int version = 1;
        public int seed = 1;
        public int paperCount;
        public int paperFired;
        public int paperKind0;
        public int paperKind1;
        public int paperKind2;
        public float paperAt0;
        public float paperAt1;
        public float paperAt2;
        public bool guestScheduled;
        public bool guestFired;
        public float guestAt;
        public int activeKind;
        public int activeStage;
        public int presses;
        public float secondsLeft;
        public float cooldown;
        public float cleanupWorkBudget;
        public float pressAge;
        public int timeouts;
        public int completedRounds;
    }

    public struct ScheduleResult
    {
        public int confetti;
        public int tissue;
        public bool guest;
    }

    public struct RoundResult
    {
        public bool accepted;
        public bool completed;
        public bool timedOut;
        public int kind;
        public int stage;
    }

    public static State NewState(int seed, float elapsed = 0f)
    {
        var state = new State { seed = NormalizeSeed(seed) };
        if (!Finite(elapsed)) return state;
        elapsed = Math.Max(0f, elapsed);
        if (elapsed >= 165f) return state;
        uint random = (uint)state.seed;
        float paperMinimum = Math.Max(20f, elapsed + 8f);
        if (paperMinimum <= 135f && Next(ref random) % 100u < 80u)
        {
            state.paperCount = 1 + (int)(Next(ref random) % 3u);
            int confetti = 0, tissue = 0;
            for (int i = 0; i < state.paperCount; i++)
            {
                int kind = (int)(Next(ref random) % 2u);
                if (confetti >= 2) kind = 1;
                else if (tissue >= 2) kind = 0;
                if (kind == 0) confetti++; else tissue++;
                SetPaper(state, i, kind, Between(ref random, paperMinimum, 135f));
            }
            // Fixed fields keep JsonUtility saves small; sort the at-most-three entries.
            for (int i = 0; i < state.paperCount; i++)
                for (int j = i + 1; j < state.paperCount; j++)
                    if (PaperAt(state, j) < PaperAt(state, i))
                    {
                        int kind = PaperKind(state, i); float at = PaperAt(state, i);
                        SetPaper(state, i, PaperKind(state, j), PaperAt(state, j));
                        SetPaper(state, j, kind, at);
                    }
        }
        float guestMinimum = Math.Max(40f, elapsed + 8f);
        if (guestMinimum <= 140f && Next(ref random) % 100u < 75u)
        {
            state.guestScheduled = true;
            state.guestAt = Between(ref random, guestMinimum, 140f);
        }
        return state;
    }

    public static ScheduleResult AdvanceSchedule(State state, float elapsed, bool running)
    {
        var result = new ScheduleResult();
        if (state == null) return result;
        Sanitize(state);
        if (!running || !Finite(elapsed) || elapsed < 0f) return result;
        while (state.paperFired < state.paperCount && PaperAt(state, state.paperFired) <= elapsed)
        {
            if (PaperKind(state, state.paperFired) == 0) result.confetti++;
            else result.tissue++;
            state.paperFired++;
        }
        if (state.guestScheduled && !state.guestFired && state.guestAt <= elapsed)
        {
            state.guestFired = true;
            result.guest = true;
        }
        return result;
    }

    public static bool Start(State state, int kind, int stage)
    {
        if (state == null || (kind != Cleanup && kind != Guest)) return false;
        Sanitize(state);
        if (state.activeKind != None || state.cooldown > 0f) return false;
        state.activeKind = kind;
        state.activeStage = Clamp(stage, 0, kind == Guest ? 2 : 1000);
        state.presses = 0;
        state.secondsLeft = Duration(kind);
        state.pressAge = MinimumPressInterval;
        return true;
    }

    public static RoundResult Tick(State state, float delta, bool running)
    {
        var result = new RoundResult();
        if (state == null) return result;
        Sanitize(state);
        if (!running || !Finite(delta) || delta <= 0f) return result;
        delta = Math.Min(delta, 180f);
        state.cooldown = Math.Max(0f, state.cooldown - delta);
        state.pressAge = Math.Min(180f, state.pressAge + delta);
        if (state.activeKind == None) return result;
        state.secondsLeft = Math.Max(0f, state.secondsLeft - delta);
        if (state.secondsLeft > 0f) return result;
        result.timedOut = true; result.kind = state.activeKind; result.stage = state.activeStage;
        ClearRound(state);
        state.cooldown = 1.5f;
        state.timeouts = Clamp(state.timeouts + 1, 0, 1000000000);
        return result;
    }

    public static RoundResult Press(State state, int kind, bool running)
    {
        var result = new RoundResult();
        if (state == null) return result;
        Sanitize(state);
        if (!running || kind == None || kind != state.activeKind || state.secondsLeft <= 0f
            || state.cooldown > 0f || state.pressAge < MinimumPressInterval) return result;
        result.accepted = true; result.kind = state.activeKind; result.stage = state.activeStage;
        state.pressAge = 0f;
        state.presses++;
        if (state.presses < RequiredPresses(kind)) return result;
        result.completed = true;
        if (kind == Cleanup)
            state.cleanupWorkBudget = Math.Min(MaximumLitter, state.cleanupWorkBudget + CleanupPulseWork);
        state.completedRounds = Clamp(state.completedRounds + 1, 0, 1000000000);
        ClearRound(state);
        state.cooldown = .5f;
        return result;
    }

    /// <summary>Repairs fields without firing events, finishing rounds or granting work.</summary>
    public static void Sanitize(State state)
    {
        if (state == null) return;
        state.version = 1; state.seed = NormalizeSeed(state.seed);
        state.paperCount = Clamp(state.paperCount, 0, 3);
        state.paperFired = Clamp(state.paperFired, 0, state.paperCount);
        int confetti = 0, tissue = 0;
        float previous = 20f;
        for (int i = 0; i < state.paperCount; i++)
        {
            float at = PaperAt(state, i); int kind = PaperKind(state, i);
            if (kind == 0) confetti++; else if (kind == 1) tissue++;
            bool valid = Finite(at) && at >= previous && at <= 135f
                && (kind == 0 || kind == 1) && confetti <= 2 && tissue <= 2;
            if (!valid)
            {
                // A corrupt future schedule is cancelled, never regenerated as a reward.
                state.paperCount = Math.Max(state.paperFired, i);
                if (i < state.paperFired)
                    for (int past = 0; past < state.paperFired; past++) SetPaper(state, past, past % 2, 20f);
                break;
            }
            previous = at;
        }
        for (int unused = state.paperCount; unused < 3; unused++) SetPaper(state, unused, 0, 0f);
        if (!Finite(state.guestAt) || state.guestAt < 40f || state.guestAt > 140f)
        { state.guestScheduled = false; state.guestAt = 0f; }
        state.cooldown = Finite(state.cooldown) ? Bound(state.cooldown, 0f, 1.5f) : 0f;
        state.cleanupWorkBudget = Finite(state.cleanupWorkBudget) ? Bound(state.cleanupWorkBudget, 0f, MaximumLitter) : 0f;
        state.pressAge = Finite(state.pressAge) ? Bound(state.pressAge, 0f, 180f) : 0f;
        state.timeouts = Clamp(state.timeouts, 0, 1000000000);
        state.completedRounds = Clamp(state.completedRounds, 0, 1000000000);
        if (state.activeKind != Cleanup && state.activeKind != Guest) ClearRound(state);
        else
        {
            state.activeStage = Clamp(state.activeStage, 0, state.activeKind == Guest ? 2 : 1000);
            state.presses = Clamp(state.presses, 0, RequiredPresses(state.activeKind) - 1);
            state.secondsLeft = Finite(state.secondsLeft) ? Bound(state.secondsLeft, 0f, Duration(state.activeKind)) : 0f;
        }
    }

    private static void ClearRound(State state)
    { state.activeKind = None; state.activeStage = 0; state.presses = 0; state.secondsLeft = 0f; state.pressAge = 0f; }
    private static int RequiredPresses(int kind) { return kind == Cleanup ? CleanupPresses : GuestPresses; }
    private static float Duration(int kind) { return kind == Cleanup ? CleanupSeconds : GuestSeconds; }
    private static int NormalizeSeed(int seed) { return (seed & int.MaxValue) == 0 ? 1 : seed & int.MaxValue; }
    private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
    private static float Bound(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static uint Next(ref uint value) { value = unchecked(value * 1664525u + 1013904223u); return value; }
    private static float Between(ref uint random, float min, float max)
    { return min + (max - min) * ((Next(ref random) & 0x00ffffffu) / 16777215f); }
    private static int PaperKind(State state, int index)
    { return index == 0 ? state.paperKind0 : index == 1 ? state.paperKind1 : state.paperKind2; }
    private static float PaperAt(State state, int index)
    { return index == 0 ? state.paperAt0 : index == 1 ? state.paperAt1 : state.paperAt2; }
    private static void SetPaper(State state, int index, int kind, float at)
    {
        if (index == 0) { state.paperKind0 = kind; state.paperAt0 = at; }
        else if (index == 1) { state.paperKind1 = kind; state.paperAt1 = at; }
        else { state.paperKind2 = kind; state.paperAt2 = at; }
    }
}
