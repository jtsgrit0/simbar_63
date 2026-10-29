using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Floor Mission Save and Actors")]
    public static void CheckFloorIntegrationMenu() { Debug.Log(CheckFloorIntegration()); }

    public static string CheckFloorIntegration()
    {
        var report = new Report();
        var host = FloorVerificationHost(report);
        if (host == null) return report.ToString();
        var snapshot = new JourneyCheckSnapshot(host);
        var motion = new FloorVerificationMotionSnapshot(host);
        try
        {
            FloorStartVerificationNight(host); FloorForceSchedule(host, false, false);
            FloorJourneyWrite(host, "secondsLeft", 110f); Call(host, "TickFloorMissions", .1f);
            var state = Property<SimBarFloorMissions.State>(host, "FloorState");
            var challenge = FloorChallengeState(host);
            int fans = Read<int>(host, "fans"); float cash = Read<float>(host, "cash");
            Call(host, "FloorQuickPress", SimBarFloorChallenges.Cleanup); Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest);
            report.Check(!state.guestSpawned && state.litter == 0 && Read<int>(host, "fans") == fans && challenge.activeKind == SimBarFloorChallenges.None,
                "No fixed 70-second event or manual quick button can invent paper, a guest or a reward on an unscheduled night.");
            FloorForceSchedule(host, true, true, 72f, 75f);
            Call(host, "TickFloorMissions", .1f);
            report.Check(state.litter == 0 && !state.guestSpawned, "Future random events remain dormant until their saved due time.");
            FloorJourneyWrite(host, "secondsLeft", 107f); Call(host, "TickFloorMissions", .1f);
            var effects = Read<SimBarFloorPerformance>(host, "floorPerformance");
            report.Check(state.confettiThrows == 1 && state.tissueThrows == 1 && state.litter == 56 && Read<int>(host, "fans") == fans + 4
                && effects != null && effects.ActiveParticleCount > 0 && effects.VisibleLitterCount > 0 && !state.guestSpawned,
                "Due spontaneous paper events create their real burst, audience response and persistent litter debt before a later guest event.");
            Call(host, "TickFloorMissions", .1f);
            report.Check(state.litter == 56 && Read<int>(host, "fans") == fans + 4 && challenge.paperFired == 2,
                "Repeated frames at a due time cannot replay a paper performance or audience reward.");
            int patrons = Read<IList>(host, "patrons").Count, queue = FloorQueueCount(host);
            FloorJourneyWrite(host, "secondsLeft", 105f); Call(host, "TickFloorMissions", .1f);
            object guest = Read<object>(host, "floorMissionGuest");
            report.Check(state.guestActive && guest != null && !Read<IList>(host, "patrons").Contains(guest)
                && Read<IList>(host, "patrons").Count == patrons && FloorQueueCount(host) == queue,
                "A due guest event creates a dedicated visible assistance actor outside the ordinary service queue.");
            FloorWaitForKind(host, SimBarFloorChallenges.Guest);
            int taps = challenge.presses; Call(host, "FloorQuickPress", SimBarFloorChallenges.Cleanup);
            report.Check(challenge.presses == taps && state.guestStep == 0, "The cleanup key cannot progress a guest reaction.");
            Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest); Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest);
            report.Check(challenge.presses == taps + 1 && state.guestStep == 0, "Same-frame duplicate input counts once and cannot skip the dialogue stage.");
            for (int i = 0; i < 2; i++) { FloorAdvance(host, 1); Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest); }
            int partialTaps = challenge.presses; float remaining = challenge.secondsLeft;
            string savedSchedule = FloorScheduleSignature(challenge);
            host.SaveOperations(); Call(host, "ImportJourneyState", ""); Call(host, "LoadOperations");
            Call(host, "JourneyReturnToLobby"); Call(host, "ContinueJourney");
            state = Property<SimBarFloorMissions.State>(host, "FloorState"); challenge = FloorChallengeState(host);
            report.Check(challenge.activeKind == SimBarFloorChallenges.Guest && challenge.presses == partialTaps
                && Mathf.Abs(challenge.secondsLeft - remaining) < .001f && FloorScheduleSignature(challenge) == savedSchedule
                && state.litter == 56 && state.guestStep == 0 && Read<int>(host, "fans") == fans + 4 && effects.ActiveParticleCount == 0,
                "Real save/load preserves the random schedule and active reaction taps/time without rerolling, rebursting or repeating rewards.");
            Write(host, "paused", true); remaining = challenge.secondsLeft; taps = challenge.presses;
            FloorAdvance(host, 20); Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest);
            report.Check(challenge.secondsLeft == remaining && challenge.presses == taps && state.guestStep == 0,
                "Paused runtime time and taps cannot consume or progress a reaction window.");
            Write(host, "paused", false); FloorAdvance(host, 80);
            report.Check(challenge.timeouts > 0 && state.guestStep == 0 && !state.guestComplete && Read<int>(host, "fans") == fans + 4,
                "Missing the guest window retries the same courteous stage without removing the guest or rewarding failure.");
            FloorCompleteGuestReactions(host, false);
            report.Check(state.guestComplete && state.guestStep == 3 && !Property<bool>(host, "FloorGuestExitFinished")
                && Read<int>(host, "fans") == fans + 4 + SimBarFloorMissions.GuestFans,
                "Three successful rapid-press rounds complete the three courteous stages once and begin a real staff escort.");
            int escortTicks = 0;
            while (!Property<bool>(host, "FloorGuestExitFinished") && escortTicks++ < 400) FloorAdvance(host, 1);
            if (!Property<bool>(host, "FloorGuestExitFinished")) report.measurements.Add("Guest reaction escort timeout: " + FloorActorDiagnostic(host));
            report.Check(Property<bool>(host, "FloorGuestExitFinished") && Read<object>(host, "floorEscortWorker") == null,
                "The successful reaction still waits for the actual guest exit and employee return.");
            Call(host, "TickJourney", 180f); Call(host, "OpenFloorMissionDrawer");
            bool drawerWasOpen = Read<bool>(host, "floorMissionDrawerOpen"); Call(host, "OnApplicationFocus", false);
            report.Check(drawerWasOpen && Read<bool>(host, "paused") && !Read<bool>(host, "floorMissionDrawerOpen")
                && Read<GameObject>(host, "pausePanel").activeSelf, "Focus loss from the closing drawer leaves a visible effective pause.");
            Call(host, "TogglePause");
            report.Check(Property<bool>(host, "JourneyIsFloorClosing") && !Read<bool>(host, "paused") && state.cleanupActive
                && Read<IList>(host, "floorCleaningCrew").Count == 2 && Read<float>(host, "cash") == cash,
                "Last call automatically assigns real employees and defers settlement; the normal resume control remains usable.");
            int peakReady = 0;
            for (int i = 0; i < 300; i++) { FloorAdvance(host, 1); peakReady = Mathf.Max(peakReady, Property<int>(host, "CleanupReadyWorkers")); }
            report.Check(peakReady > 0 && state.cleaned == 0f && challenge.cleanupWorkBudget == 0f && !state.cleanupComplete,
                "Arrived employees cannot auto-clean while the player ignores the cleanup reaction; elapsed time alone earns no labor.");
            bool partialSaved = false, partialPreserved = false; int cleanupTicks = 0;
            while (!Property<bool>(host, "JourneyIsResults") && cleanupTicks++ < 1600)
            {
                if (challenge.activeKind == SimBarFloorChallenges.Cleanup) Call(host, "FloorQuickPress", SimBarFloorChallenges.Cleanup);
                FloorAdvance(host, 1); peakReady = Mathf.Max(peakReady, Property<int>(host, "CleanupReadyWorkers"));
                if (!partialSaved && state.cleaned >= 12f && !state.cleanupComplete)
                {
                    float work = state.cleaned, budget = challenge.cleanupWorkBudget;
                    host.SaveOperations(); Call(host, "JourneyReturnToLobby"); Call(host, "ImportJourneyState", ""); Call(host, "LoadOperations");
                    Call(host, "ContinueJourney"); Call(host, "CloseFloorMissionDrawer");
                    state = Property<SimBarFloorMissions.State>(host, "FloorState"); challenge = FloorChallengeState(host);
                    partialPreserved = Mathf.Abs(state.cleaned - work) < .001f && Mathf.Abs(challenge.cleanupWorkBudget - budget) < .001f
                        && state.cleanupActive && Property<bool>(host, "JourneyIsFloorClosing"); partialSaved = true;
                }
            }
            if (!Property<bool>(host, "JourneyIsResults")) report.measurements.Add("Reaction cleanup timeout: " + FloorActorDiagnostic(host));
            report.Check(partialSaved && partialPreserved, "A partly worked closing resumes exact employee work and earned reaction labor budget after real save/load.");
            report.Check(state.cleanupComplete && SimBarFloorMissions.Remaining(state) == 0 && effects.VisibleLitterCount == 0
                && peakReady > 0 && challenge.completedRounds >= 5 && Property<bool>(host, "JourneyIsResults"),
                "Earned cleanup pulses plus actual arrived employees remove all visible paper before results open.");
            report.Check(FloorJourneyRead<int>(host, "resultFloorTasks") == 2 && FloorJourneyRead<float>(host, "resultFloorBonus") == SimBarFloorMissions.CleanupReward
                && Mathf.Abs(Read<float>(host, "cash") - cash - Property<int>(host, "JourneyResultBonus") - SimBarFloorMissions.CleanupReward) < .001f,
                "Both reaction-driven floor missions produce one cleanup receipt alongside the ordinary night bonus.");
            cash = Read<float>(host, "cash"); fans = Read<int>(host, "fans"); int xp = Property<int>(host, "JourneyXP");
            host.SaveOperations(); Call(host, "LoadOperations"); Call(host, "ContinueJourney");
            Call(host, "FinishJourneyNight"); Call(host, "SettleJourneyNight"); Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest);
            Call(host, "FloorQuickPress", SimBarFloorChallenges.Cleanup);
            report.Check(Read<float>(host, "cash") == cash && Read<int>(host, "fans") == fans && Property<int>(host, "JourneyXP") == xp,
                "Completed random events, reaction callbacks and reloaded receipts cannot duplicate cash, audience or XP rewards.");
            report.measurements.Add("Spontaneous due times72/73/75; guest escort=" + escortTicks + " ticks; hands-off cleanup30sec; reaction cleanup=" + cleanupTicks
                + " ticks; peak actual staff=" + peakReady + "; active reaction and labor-budget save recovery; original preferences/motion restored.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally { FloorRestoreVerification(host, snapshot, motion, report); }
        return report.ToString();
    }

    [MenuItem("SimBar/Validation/Check Live and Concurrent Floor Routes")]
    public static void CheckFloorAlternateRoutesMenu() { Debug.Log(CheckFloorAlternateRoutes()); }
    public static string CheckFloorAlternateRoutes()
    {
        var report = new Report(); var host = FloorVerificationHost(report);
        if (host == null) return report.ToString();
        var snapshot = new JourneyCheckSnapshot(host); var motion = new FloorVerificationMotionSnapshot(host);
        try
        {
            FloorStartVerificationNight(host); FloorForceSchedule(host, false, true);
            FloorJourneyWrite(host, "secondsLeft", 140f); Call(host, "TickFloorMissions", .1f);
            object queue = Read<object>(host, "orderQueue"); Array orders = (Array)queue.GetType().GetMethod("ToArray").Invoke(queue, null);
            float clock = Property<float>(host, "JourneySecondsLeft"); int served = Property<int>(host, "JourneyOrders");
            FloorCompleteGuestReactions(host, true);
            var state = Property<SimBarFloorMissions.State>(host, "FloorState"); object worker = Read<object>(host, "floorEscortWorker");
            Transform employee = worker == null ? null : FloorWorkerRoot(worker);
            Vector3 home = worker == null ? Vector3.zero : FloorPublicRead<Vector3>(worker, "savedPosition");
            Array after = (Array)queue.GetType().GetMethod("ToArray").Invoke(queue, null);
            bool sameQueue = orders.Length == after.Length;
            for (int i = 0; sameQueue && i < orders.Length; i++) sameQueue &= ReferenceEquals(orders.GetValue(i), after.GetValue(i));
            report.Check(state.guestComplete && state.litter == 0 && worker != null && Property<bool>(host, "JourneyIsPlaying")
                && !Read<bool>(host, "paused") && Property<float>(host, "JourneySecondsLeft") < clock && sameQueue,
                "Guest reaction rounds keep the show clock running and preserve ordinary queue entries while assigning a real escort employee.");
            Call(host, "CommitPlayerPour", FrontDrink(host), 1f);
            report.Check(Property<int>(host, "JourneyOrders") == served + 1 && Read<object>(host, "floorEscortWorker") != null,
                "Normal player service remains available while the reaction-assisted guest is being escorted.");
            float serviceCash = Read<float>(host, "cash"); int liveTicks = 0;
            while (!Property<bool>(host, "FloorGuestExitFinished") && liveTicks++ < 400) FloorAdvanceLive(host);
            if (!Property<bool>(host, "FloorGuestExitFinished")) report.measurements.Add("Live reaction escort timeout: " + FloorActorDiagnostic(host));
            report.Check(Property<bool>(host, "FloorGuestExitFinished") && employee != null
                && Vector3.Distance(Read<Transform>(host, "worldRoot").InverseTransformPoint(employee.position), home) <= .22f,
                "The live employee accompanies the guest out and returns to the actual original bar position.");
            report.Check(Property<bool>(host, "JourneyIsPlaying") && !state.cleanupStarted && Read<float>(host, "cash") == serviceCash,
                "A completed guest-only random mission neither stops the show nor awards an unearned cleanup bonus.");
            Call(host, "TickJourney", 180f);
            report.Check(Property<bool>(host, "JourneyIsResults") && FloorJourneyRead<int>(host, "resultFloorTasks") == 1
                && FloorJourneyRead<float>(host, "resultFloorBonus") == 0f, "A clean-floor last call settles one guest mission with no paper-cleanup payment.");

            FloorStartVerificationNight(host); FloorForceSchedule(host, true, true);
            FloorJourneyWrite(host, "secondsLeft", 140f); Call(host, "TickFloorMissions", .1f);
            state = Property<SimBarFloorMissions.State>(host, "FloorState"); float openingCash = Read<float>(host, "cash");
            Call(host, "TickJourney", 180f); Call(host, "CloseFloorMissionDrawer");
            IList crew = Read<IList>(host, "floorCleaningCrew");
            report.Check(state.cleanupActive && crew.Count == 2 && state.cleaned == 0f,
                "A simultaneous random paper/guest closing automatically assigns real workers without granting cleanup labor.");
            FloorCompleteGuestReactions(host, false); worker = Read<object>(host, "floorEscortWorker");
            object otherWorker = null; foreach (object cleaner in crew) if (!ReferenceEquals(cleaner, worker)) { otherWorker = cleaner; break; }
            Vector3 otherStart = otherWorker == null ? Vector3.zero : FloorWorkerRoot(otherWorker).position;
            report.Check(worker != null && crew.Contains(worker) && Property<int>(host, "CleanupReadyWorkers") <= 1,
                "The final guest reaction borrows one real cleanup employee and excludes the escort from ready cleanup labor.");
            var challenge = FloorChallengeState(host);
            int beforeRounds = challenge.completedRounds;
            int ticks = 0, peakDuringEscort = 0; bool otherMoved = false, escortReturned = false, workerResumed = false, earnedWorkPulse = false;
            while (!Property<bool>(host, "JourneyIsResults") && ticks++ < 1600)
            {
                int ready = Property<int>(host, "CleanupReadyWorkers");
                bool escortActive = Read<object>(host, "floorEscortWorker") != null;
                if (challenge.activeKind == SimBarFloorChallenges.Cleanup) Call(host, "FloorQuickPress", SimBarFloorChallenges.Cleanup);
                FloorAdvance(host, 1);
                if (!earnedWorkPulse && challenge.completedRounds > beforeRounds)
                    earnedWorkPulse = challenge.cleanupWorkBudget > 0f && !state.cleanupComplete && state.cleaned < state.litter;
                if (escortActive)
                {
                    peakDuringEscort = Mathf.Max(peakDuringEscort, ready);
                    otherMoved |= otherWorker != null && Vector3.Distance(otherStart, FloorWorkerRoot(otherWorker).position) > .1f;
                }
                else escortReturned = true;
                if (escortReturned && worker != null && crew.Contains(worker))
                    workerResumed |= FloorPublicRead<bool>(FloorPublicRead<object>(worker, "route"), "ready") && state.cleaned > 0f;
            }
            if (!Property<bool>(host, "JourneyIsResults")) report.measurements.Add("Concurrent reaction closing timeout: " + FloorActorDiagnostic(host));
            report.Check(earnedWorkPulse, "A rapid cleanup round earns a work budget while physical employees still have to travel and work.");
            report.Check(peakDuringEscort <= 1 && otherMoved,
                "The reserved escort cannot count as ready cleanup labor, while the other employee keeps physically moving rather than freezing.");
            report.Check(escortReturned && workerResumed, "The borrowed employee returns to a cleanup target and resumes actual credited work.");
            report.Check(Property<bool>(host, "JourneyIsResults") && state.cleanupComplete && state.guestComplete
                && Property<bool>(host, "FloorGuestExitFinished") && SimBarFloorMissions.Remaining(state) == 0,
                "Concurrent reaction-driven jobs finish actual cleanup and escort before opening results.");
            report.Check(FloorJourneyRead<int>(host, "resultFloorTasks") == 2 && FloorJourneyRead<float>(host, "resultFloorBonus") == SimBarFloorMissions.CleanupReward
                && Mathf.Abs(Read<float>(host, "cash") - openingCash - Property<int>(host, "JourneyResultBonus") - SimBarFloorMissions.CleanupReward) < .001f,
                "Concurrent rapid reactions still settle exactly one completed cleanup receipt and two mission records.");
            report.measurements.Add("Guest-only live reaction+escort=" + liveTicks + " ticks; concurrent reaction jobs=" + ticks + " ticks; other ready during escort=" + peakDuringEscort + ".");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally { FloorRestoreVerification(host, snapshot, motion, report); }
        return report.ToString();
    }

    private static SimBarExperience FloorVerificationHost(Report report)
    {
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress || Read<bool>(host, "opMenuOpen"))
        { report.failures.Add("Enter stable Play Mode and close management/pour controls before floor reaction integration checks."); return null; }
        var state = Property<SimBarFloorMissions.State>(host, "FloorState");
        if (state != null && (state.litter > 0 || state.guestSpawned))
        { report.failures.Add("Run reaction checks from a fresh lobby before starting floor missions, so existing actors remain untouched."); return null; }
        return host;
    }
    private static SimBarFloorChallenges.State FloorChallengeState(SimBarExperience host)
    {
        object floor = Property<SimBarFloorMissions.State>(host, "FloorState");
        foreach (FieldInfo field in floor.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (field.FieldType == typeof(SimBarFloorChallenges.State))
            {
                var state = (SimBarFloorChallenges.State)field.GetValue(floor);
                if (state != null) return state;
            }
        throw new Exception("The running journey has no saved floor challenge state.");
    }
    private static void FloorForceSchedule(SimBarExperience host, bool paper, bool guest, float paperAt = 20f, float guestAt = 40f)
    {
        var state = FloorChallengeState(host);
        state.paperCount = paper ? 2 : 0; state.paperFired = 0; state.paperKind0 = 0; state.paperKind1 = 1;
        state.paperAt0 = paperAt; state.paperAt1 = paperAt + 1f; state.guestScheduled = guest; state.guestFired = false; state.guestAt = guestAt;
        state.activeKind = SimBarFloorChallenges.None; state.presses = 0; state.secondsLeft = 0f; state.cooldown = 0f;
        state.cleanupWorkBudget = 0f; state.pressAge = 0f; state.timeouts = 0; state.completedRounds = 0;
    }
    private static string FloorScheduleSignature(SimBarFloorChallenges.State state)
    { return state.seed + "/" + state.paperCount + "/" + state.paperFired + "/" + state.paperKind0 + "/" + state.paperKind1 + "/" + state.paperAt0 + "/" + state.paperAt1 + "/" + state.guestScheduled + "/" + state.guestFired + "/" + state.guestAt; }
    private static void FloorWaitForKind(SimBarExperience host, int kind)
    {
        int ticks = 0;
        while (FloorChallengeState(host).activeKind != kind && ticks++ < 100) FloorAdvance(host, 1);
        if (FloorChallengeState(host).activeKind != kind) throw new Exception("Expected reaction kind " + kind + " was not offered by the real runtime.");
    }
    private static void FloorCompleteGuestReactions(SimBarExperience host, bool liveClock)
    {
        int ticks = 0;
        while (Property<SimBarFloorMissions.State>(host, "FloorState").guestActive && ticks++ < 300)
        {
            if (FloorChallengeState(host).activeKind == SimBarFloorChallenges.Guest) Call(host, "FloorQuickPress", SimBarFloorChallenges.Guest);
            if (liveClock) FloorAdvanceLive(host); else FloorAdvance(host, 1);
        }
        if (!Property<SimBarFloorMissions.State>(host, "FloorState").guestComplete) throw new Exception("Three rapid guest reaction rounds did not complete within30seconds.");
    }
    private static void FloorStartVerificationNight(SimBarExperience host)
    {
        Write(host, "journeyValidationMode", false); Call(host, "CloseFloorMissionDrawer"); Call(host, "ImportJourneyState", "");
        Write(host, "cash", 20000f); Write(host, "fans", 256); Write(host, "opActiveEvent", -1); Write(host, "opWageDebt", 0);
        Array.Clear(Read<int[]>(host, "opStaff"), 0, 3); Call(host, "UpdateOperationsWorldVisuals");
        FloorJourneyWrite(host, "tutorialComplete", true); SetJourneyPhase(host, "Lobby"); Call(host, "StartFreshJourneyNight", false);
    }
    private static void FloorAdvanceLive(SimBarExperience host)
    { Call(host, "TickJourney", .1f); Call(host, "TickVenueMotion", .1f); Call(host, "TickFloorMissions", .1f); }
    private static void FloorRestoreVerification(SimBarExperience host, JourneyCheckSnapshot snapshot, FloorVerificationMotionSnapshot motion, Report report)
    {
        if (report.failures.Count > 0) report.measurements.Add("Reaction failure actor state: " + FloorActorDiagnostic(host));
        Call(host, "CloseFloorMissionDrawer"); snapshot.Restore(host); motion.Restore(); Call(host, "SyncFloorWorld"); Call(host, "RefreshFloorMissionUI");
    }

    private static void FloorAdvance(SimBarExperience host, int ticks)
    {
        for (int i = 0; i < ticks; i++) { Call(host, "TickVenueMotion", .1f); Call(host, "TickFloorMissions", .1f); }
    }
    private static int FloorQueueCount(SimBarExperience host) => (int)Read<object>(host, "orderQueue").GetType().GetProperty("Count").GetValue(Read<object>(host, "orderQueue"));
    private static T FloorPublicRead<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);
    private static Transform FloorWorkerRoot(object worker) => FloorPublicRead<Transform>(FloorPublicRead<object>(FloorPublicRead<object>(worker, "actor"), "person"), "root");
    private static T FloorJourneyRead<T>(SimBarExperience host, string name) => FloorPublicRead<T>(Read<object>(host, "journey"), name);
    private static void FloorJourneyWrite(SimBarExperience host, string name, object value) => Read<object>(host, "journey").GetType().GetField(name).SetValue(Read<object>(host, "journey"), value);

    private static string FloorActorDiagnostic(SimBarExperience host)
    {
        var text = new System.Text.StringBuilder();
        var state = Property<SimBarFloorMissions.State>(host, "FloorState");
        text.Append("phase=").Append(Read<object>(host, "journeyPhase")).Append(" paused=").Append(Read<bool>(host, "paused"));
        if (state != null) text.Append(" cleaned=").Append(state.cleaned.ToString("F2")).Append('/').Append(state.litter)
            .Append(" remaining=").Append(SimBarFloorMissions.Remaining(state)).Append(" ready=").Append(Property<int>(host, "CleanupReadyWorkers"))
            .Append(" guestStep=").Append(state.guestStep).Append(" guestComplete=").Append(state.guestComplete);
        if (state != null)
        {
            try
            {
                var reaction = FloorChallengeState(host);
                text.Append(" reaction=").Append(reaction.activeKind).Append('/').Append(reaction.activeStage)
                    .Append(" taps=").Append(reaction.presses).Append(" window=").Append(reaction.secondsLeft.ToString("F2"))
                    .Append(" budget=").Append(reaction.cleanupWorkBudget.ToString("F2")).Append(" timeouts=").Append(reaction.timeouts)
                    .Append(" rounds=").Append(reaction.completedRounds).Append(" paperFired=").Append(reaction.paperFired).Append('/').Append(reaction.paperCount)
                    .Append(" guestFired=").Append(reaction.guestFired);
            }
            catch (Exception error) { text.Append(" reactionUnavailable=").Append(error.Message); }
        }
        Transform world = Read<Transform>(host, "worldRoot"); int index = 0;
        foreach (object worker in Read<IList>(host, "floorCleaningCrew"))
        {
            Transform root = FloorWorkerRoot(worker);
            text.Append("; worker").Append(index++).Append('@').Append(root == null ? "null" : world.InverseTransformPoint(root.position).ToString("F2"));
            FloorRouteDiagnostic(text, FloorPublicRead<object>(worker, "route"));
            FloorBlockerDiagnostic(text, host, root, world);
        }
        object guest = Read<object>(host, "floorMissionGuest");
        Transform guestRoot = guest == null ? null : FloorPublicRead<Transform>(guest, "root");
        text.Append("; guest@").Append(guestRoot == null ? "null" : world.InverseTransformPoint(guestRoot.position).ToString("F2"));
        FloorRouteDiagnostic(text, Read<object>(host, "floorGuestRoute"));
        FloorBlockerDiagnostic(text, host, guestRoot, world);
        text.Append(" leaving=").Append(Read<bool>(host, "floorGuestLeaving")).Append(" exited=").Append(Read<bool>(host, "floorGuestExited"))
            .Append(" escortJoined=").Append(Read<bool>(host, "floorEscortJoined")).Append(" returning=").Append(Read<bool>(host, "floorEscortReturning"));
        object escort = Read<object>(host, "floorEscortWorker");
        Transform escortRoot = escort == null ? null : FloorWorkerRoot(escort);
        text.Append("; escort@").Append(escortRoot == null ? "null" : world.InverseTransformPoint(escortRoot.position).ToString("F2"));
        FloorRouteDiagnostic(text, Read<object>(host, "floorEscortRoute"));
        FloorBlockerDiagnostic(text, host, escortRoot, world);
        return text.ToString();
    }

    private static void FloorBlockerDiagnostic(System.Text.StringBuilder text, SimBarExperience host, Transform root, Transform world)
    {
        if (root == null || world == null) return;
        Vector3 position = world.InverseTransformPoint(root.position);
        foreach (object actor in Read<IList>(host, "motionActors"))
        {
            object person = FloorPublicRead<object>(actor, "person");
            Transform other = FloorPublicRead<Transform>(person, "root");
            if (other == null || other == root || !other.gameObject.activeInHierarchy) continue;
            Vector3 body = world.InverseTransformPoint(other.position);
            float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(body.x, body.z));
            if (distance > .8f || Mathf.Abs(position.y - body.y) > 1f) continue;
            text.Append(" nearby[").Append(FloorPublicRead<string>(person, "name")).Append('@').Append(body.ToString("F2"))
                .Append(" d=").Append(distance.ToString("F2")).Append(']');
        }
    }

    private static void FloorRouteDiagnostic(System.Text.StringBuilder text, object route)
    {
        if (route == null) return;
        IList points = FloorPublicRead<IList>(route, "points"); int cursor = FloorPublicRead<int>(route, "cursor");
        text.Append(" goal=").Append(FloorPublicRead<Vector3>(route, "goal").ToString("F2"))
            .Append(" route=").Append(cursor).Append('/').Append(points.Count)
            .Append(" next=").Append(cursor < points.Count ? ((Vector3)points[cursor]).ToString("F2") : "none")
            .Append(" bridge=").Append(FloorPublicRead<bool>(route, "bridge"))
            .Append(" ready=").Append(FloorPublicRead<bool>(route, "ready"))
            .Append(" blocked=").Append(FloorPublicRead<float>(route, "blocked").ToString("F2"));
    }

    private sealed class FloorVerificationMotionSnapshot
    {
        private sealed class FieldState { public object target; public FieldInfo field; public object value; public object[] list; }
        private sealed class Pose { public Transform target; public Vector3 position, scale; public Quaternion rotation; public bool active; }
        private readonly List<FieldState> fields = new List<FieldState>();
        private readonly List<Pose> poses = new List<Pose>();
        private readonly UnityEngine.Random.State random = UnityEngine.Random.state;
        public FloorVerificationMotionSnapshot(SimBarExperience host)
        {
            foreach (string name in new[] { "motionClock", "motionServicePulse", "motionOrdersDirty" })
            {
                FieldInfo field = typeof(SimBarExperience).GetField(name, PrivateInstance);
                fields.Add(new FieldState { target = host, field = field, value = field.GetValue(host) });
            }
            foreach (object actor in Read<IList>(host, "motionActors"))
                foreach (FieldInfo field in actor.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    object value = field.GetValue(actor); IList list = value as IList;
                    var state = new FieldState { target = actor, field = field, value = value };
                    if (list != null) { state.list = new object[list.Count]; list.CopyTo(state.list, 0); }
                    fields.Add(state);
                }
            foreach (Transform target in Read<Transform>(host, "worldRoot").GetComponentsInChildren<Transform>(true))
                poses.Add(new Pose { target = target, position = target.localPosition, rotation = target.localRotation,
                    scale = target.localScale, active = target.gameObject.activeSelf });
        }
        public void Restore()
        {
            foreach (FieldState state in fields)
            {
                if (state.list != null) { IList list = (IList)state.value; list.Clear(); foreach (object item in state.list) list.Add(item); }
                else if (!state.field.IsInitOnly) state.field.SetValue(state.target, state.value);
            }
            foreach (Pose pose in poses) if (pose.target != null)
            { pose.target.localPosition = pose.position; pose.target.localRotation = pose.rotation; pose.target.localScale = pose.scale; pose.target.gameObject.SetActive(pose.active); }
            UnityEngine.Random.state = random;
        }
    }
}
