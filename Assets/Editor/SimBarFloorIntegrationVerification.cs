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

    /// <summary>Runs the real callbacks and movement against a temporary night, then restores the live save.</summary>
    public static string CheckFloorIntegration()
    {
        var report = new Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress || Read<bool>(host, "opMenuOpen"))
        { report.failures.Add("Enter stable Play Mode and close management/pour controls before floor integration checks."); return report.ToString(); }
        var initialFloor = Property<SimBarFloorMissions.State>(host, "FloorState");
        if (initialFloor != null && (initialFloor.litter > 0 || initialFloor.guestSpawned))
        { report.failures.Add("Run this check from a fresh lobby before starting floor missions, so existing mission actors remain untouched."); return report.ToString(); }
        var snapshot = new JourneyCheckSnapshot(host);
        var motion = new FloorVerificationMotionSnapshot(host);
        try
        {
            Write(host, "journeyValidationMode", false);
            Call(host, "ImportJourneyState", "");
            Write(host, "cash", 20000f); Write(host, "fans", 256);
            Write(host, "opActiveEvent", -1); Write(host, "opWageDebt", 0);
            Array.Clear(Read<int[]>(host, "opStaff"), 0, 3);
            Call(host, "UpdateOperationsWorldVisuals");
            FloorJourneyWrite(host, "tutorialComplete", true);
            SetJourneyPhase(host, "Lobby");
            Call(host, "StartFreshJourneyNight", false);
            report.Check(Property<bool>(host, "JourneyIsPlaying") && !Read<bool>(host, "paused"), "A temporary night starts through the real fresh-night callback.");

            int fans = Read<int>(host, "fans"); float cash = Read<float>(host, "cash");
            Call(host, "FloorThrow", 0); Call(host, "FloorThrow", 1);
            Call(host, "FloorThrow", 0); Call(host, "FloorThrow", 1);
            var state = Property<SimBarFloorMissions.State>(host, "FloorState");
            var effects = Read<SimBarFloorPerformance>(host, "floorPerformance");
            report.Check(state.confettiThrows == 2 && state.tissueThrows == 2 && state.litter == SimBarFloorMissions.MaxLitter
                && Read<int>(host, "fans") == fans + 8 && Read<float>(host, "cash") == cash,
                "Both real performance buttons create bounded paper debt and audience rewards without charging money.");
            report.Check(effects != null && effects.VisibleLitterCount > 0 && effects.ActiveParticleCount > 0
                && effects.ActiveParticleCount <= SimBarFloorPerformance.ParticleLimit,
                "The performance callback creates visible pooled floor litter and airborne paper.");
            Call(host, "FloorThrow", 0); Call(host, "FloorThrow", 1);
            report.Check(state.litter == SimBarFloorMissions.MaxLitter && Read<int>(host, "fans") == fans + 8,
                "Repeated exhausted performance callbacks cannot repeat rewards or litter.");
            Call(host, "FloorAssignCleanup");
            report.Check(!state.cleanupStarted && state.cleaned == 0f, "Employees cannot start cleanup while the band is playing.");

            int patronCount = Read<IList>(host, "patrons").Count;
            int queueCount = FloorQueueCount(host);
            FloorJourneyWrite(host, "secondsLeft", 110f); Call(host, "TickFloorMissions", .1f);
            object guest = Read<object>(host, "floorMissionGuest");
            report.Check(state.guestActive && guest != null && Read<IList>(host, "patrons").Count == patronCount
                && FloorQueueCount(host) == queueCount && !Read<IList>(host, "patrons").Contains(guest),
                "The scheduled assistance guest is a dedicated visible actor outside the ordinary service queue.");
            Call(host, "FloorGuestChoice", 2);
            report.Check(state.guestStep == 0 && !state.guestComplete, "Skipping to escort does not remove the guest.");
            Call(host, "FloorGuestChoice", 0);
            report.Check(state.guestStep == 1 && state.guestActive, "The first courteous action pauses service and offers water.");
            host.SaveOperations(); Call(host, "ImportJourneyState", ""); Call(host, "LoadOperations");
            Call(host, "JourneyReturnToLobby"); Call(host, "ContinueJourney");
            state = Property<SimBarFloorMissions.State>(host, "FloorState");
            report.Check(state.litter == SimBarFloorMissions.MaxLitter && state.confettiThrows == 2 && state.tissueThrows == 2
                && state.guestStep == 1 && state.guestActive && Read<int>(host, "fans") == fans + 8
                && effects.ActiveParticleCount == 0,
                "Real save/load preserves paper debt and dialogue progress without replaying a paper burst or audience reward.");

            Call(host, "TickJourney", 180f);
            bool closingDrawerWasOpen = Read<bool>(host, "floorMissionDrawerOpen");
            Call(host, "OnApplicationFocus", false);
            report.Check(closingDrawerWasOpen && Read<bool>(host, "paused") && !Read<bool>(host, "floorMissionDrawerOpen")
                && Property<bool>(host, "JourneyIsFloorClosing") && Read<GameObject>(host, "pausePanel").activeSelf,
                "Losing focus with the closing mission drawer open closes the drawer and leaves an effective visible pause.");
            Call(host, "TogglePause");
            report.Check(!Read<bool>(host, "paused") && Property<bool>(host, "JourneyIsFloorClosing")
                && !Read<GameObject>(host, "pausePanel").activeSelf && Property<float>(host, "JourneySecondsLeft") == 0f,
                "The normal resume control works after focus loss during the closing drawer.");
            Call(host, "CloseFloorMissionDrawer");
            report.Check(Property<bool>(host, "JourneyIsFloorClosing") && Property<bool>(host, "JourneyHasActiveNight")
                && !Property<bool>(host, "JourneyIsResults") && Property<float>(host, "JourneySecondsLeft") == 0f
                && Read<float>(host, "cash") == cash,
                "Last call stops the show and defers the night receipt until pending floor missions finish.");
            Call(host, "FloorAssignCleanup");
            IList crew = Read<IList>(host, "floorCleaningCrew");
            var employeeStarts = new List<Vector3>();
            foreach (object worker in crew) employeeStarts.Add(FloorWorkerRoot(worker).position);
            report.Check(state.cleanupActive && state.cleanupWorkers == 2 && crew.Count == 2
                && Property<int>(host, "CleanupReadyWorkers") == 0 && state.cleaned == 0f,
                "Cleanup borrows the two existing base employees, with no work credited before arrival.");
            FloorAdvance(host, 1);
            bool employeeMoved = false;
            for (int i = 0; i < crew.Count; i++) employeeMoved |= Vector3.Distance(employeeStarts[i], FloorWorkerRoot(crew[i]).position) > .01f;
            report.Check(employeeMoved && state.cleaned == 0f, "Assigned employees visibly walk away from the bar before cleanup progresses.");
            Call(host, "TogglePause"); float cleaned = state.cleaned;
            Vector3 pausedPosition = crew.Count > 0 ? FloorWorkerRoot(crew[0]).position : Vector3.zero;
            FloorAdvance(host, 20);
            report.Check(Read<bool>(host, "paused") && state.cleaned == cleaned
                && (crew.Count == 0 || Vector3.Distance(pausedPosition, FloorWorkerRoot(crew[0]).position) < .0001f),
                "A normal pause freezes both the employees' movement and their cleanup work during closing.");
            Call(host, "TogglePause");

            int peakReady = 0, cleanupTicks = 0; bool resumedPartial = false, partialPreserved = false;
            while (!state.cleanupComplete && cleanupTicks++ < 1200)
            {
                FloorAdvance(host, 1);
                peakReady = Mathf.Max(peakReady, Property<int>(host, "CleanupReadyWorkers"));
                if (!resumedPartial && state.cleaned >= 12f && !state.cleanupComplete)
                {
                    cleaned = state.cleaned;
                    host.SaveOperations(); Call(host, "JourneyReturnToLobby");
                    Call(host, "ImportJourneyState", ""); Call(host, "LoadOperations"); Call(host, "ContinueJourney");
                    Call(host, "CloseFloorMissionDrawer");
                    state = Property<SimBarFloorMissions.State>(host, "FloorState");
                    partialPreserved = Mathf.Abs(state.cleaned - cleaned) < .001f && state.cleanupActive
                        && state.guestStep == 1 && Property<bool>(host, "JourneyIsFloorClosing");
                    resumedPartial = true;
                }
            }
            report.Check(resumedPartial && partialPreserved, "Leaving, saving and continuing a partly cleaned closing preserves exact work and guest dialogue.");
            report.Check(peakReady > 0 && state.cleanupComplete && SimBarFloorMissions.Remaining(state) == 0
                && effects.VisibleLitterCount == 0, "Actual arrived employees complete and visibly remove every paper piece within the simulated closing window.");
            if (!state.cleanupComplete) report.measurements.Add("Cleanup timeout: " + FloorActorDiagnostic(host));
            report.Check(Property<bool>(host, "JourneyIsFloorClosing") && state.guestActive
                && !state.cleanupRewardClaimed && Read<float>(host, "cash") == cash,
                "Finished cleanup alone cannot bypass the remaining guest mission or pay an early receipt.");

            Call(host, "FloorGuestChoice", 2);
            report.Check(state.guestStep == 1 && !state.guestComplete, "Reloaded dialogue still enforces courteous action order.");
            Call(host, "FloorGuestChoice", 1); Call(host, "FloorGuestChoice", 2);
            report.Check(state.guestComplete && !state.guestActive && !Property<bool>(host, "FloorGuestExitFinished")
                && Property<bool>(host, "JourneyIsFloorClosing"),
                "The final dialogue starts a real exit walk and keeps the receipt closed while the guest is leaving.");
            int postGuestFans = Read<int>(host, "fans"); Call(host, "FloorGuestChoice", 2);
            report.Check(Read<int>(host, "fans") == postGuestFans && postGuestFans == fans + 8 + SimBarFloorMissions.GuestFans,
                "A repeated guest callback cannot duplicate the courteous-exit reward.");
            guest = Read<object>(host, "floorMissionGuest"); Transform guestRoot = FloorPublicRead<Transform>(guest, "root");
            object escortWorker = Read<object>(host, "floorEscortWorker");
            Transform escortRoot = escortWorker == null ? null : FloorWorkerRoot(escortWorker);
            object escortActor = escortWorker == null ? null : FloorPublicRead<object>(escortWorker, "actor");
            string escortName = escortActor == null ? "" : FloorPublicRead<string>(FloorPublicRead<object>(escortActor, "person"), "name");
            report.Check(escortRoot != null && (escortName == "Server" || escortName == "Bartender")
                && Read<IList>(host, "motionActors").Contains(escortActor),
                "The escort reserves an existing bar employee instead of creating an imaginary staff member.");
            Vector3 guestStart = guestRoot.position, escortStart = escortRoot != null ? escortRoot.position : Vector3.zero;
            bool escortMoved = false, guestMoved = false; int joinTicks = 0;
            while (!guestMoved && joinTicks++ < 150)
            {
                FloorAdvance(host, 1);
                escortMoved |= escortRoot != null && Vector3.Distance(escortStart, escortRoot.position) > .1f;
                guestMoved = Vector3.Distance(guestStart, guestRoot.position) > .1f;
            }
            report.Check(escortMoved && guestMoved && !Property<bool>(host, "JourneyIsResults")
                && Read<bool>(host, "floorEscortJoined") && guestRoot.gameObject.activeSelf,
                "The existing employee first reaches the guest, then visibly accompanies the guest toward the exit.");
            int exitTicks = 0;
            while (!Property<bool>(host, "JourneyIsResults") && joinTicks + exitTicks++ < 400) FloorAdvance(host, 1);
            if (!Property<bool>(host, "JourneyIsResults")) report.measurements.Add("Escort/exit timeout: " + FloorActorDiagnostic(host));
            report.Check(Property<bool>(host, "JourneyIsResults") && Read<bool>(host, "paused")
                && Property<bool>(host, "FloorGuestExitFinished") && !guestRoot.gameObject.activeSelf
                && Read<object>(host, "floorEscortWorker") == null,
                "The guest reaches the exit and the real employee returns before the paused nightly result opens.");
            report.Check(state.cleanupRewardClaimed && FloorJourneyRead<int>(host, "resultFloorTasks") == 2
                && Mathf.Abs(FloorJourneyRead<float>(host, "resultFloorBonus") - SimBarFloorMissions.CleanupReward) < .001f
                && Mathf.Abs(Read<float>(host, "cash") - cash - Property<int>(host, "JourneyResultBonus") - SimBarFloorMissions.CleanupReward) < .001f,
                "Both completed floor tasks appear on the receipt and cleanup pays exactly once alongside the ordinary night bonus.");
            cash = Read<float>(host, "cash"); fans = Read<int>(host, "fans"); int xp = Property<int>(host, "JourneyXP");
            host.SaveOperations(); Call(host, "LoadOperations"); Call(host, "ContinueJourney");
            Call(host, "FinishJourneyNight"); Call(host, "SettleJourneyNight"); Call(host, "FloorGuestChoice", 2);
            report.Check(Read<float>(host, "cash") == cash && Read<int>(host, "fans") == fans && Property<int>(host, "JourneyXP") == xp,
                "Reloading and repeating completion cannot duplicate floor, night or XP receipts.");
            report.measurements.Add("Real actors: cleanup ticks=" + cleanupTicks + ", peak arrived staff=" + peakReady + ", escort join ticks=" + joinTicks + ", guest exit/return ticks=" + exitTicks
                + "; maximum 120 simulated cleanup seconds and 40 escort/exit/return seconds; original wallet/preferences restored.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            if (report.failures.Count > 0) report.measurements.Add("Failure actor state: " + FloorActorDiagnostic(host));
            Call(host, "CloseFloorMissionDrawer"); snapshot.Restore(host); motion.Restore();
            Call(host, "SyncFloorWorld"); Call(host, "RefreshFloorMissionUI");
        }
        return report.ToString();
    }

    [MenuItem("SimBar/Validation/Check Live and Concurrent Floor Routes")]
    public static void CheckFloorAlternateRoutesMenu() { Debug.Log(CheckFloorAlternateRoutes()); }

    public static string CheckFloorAlternateRoutes()
    {
        var report = new Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress || Read<bool>(host, "opMenuOpen"))
        { report.failures.Add("Enter stable Play Mode and close management/pour controls before alternate floor route checks."); return report.ToString(); }
        var initialFloor = Property<SimBarFloorMissions.State>(host, "FloorState");
        if (initialFloor != null && (initialFloor.litter > 0 || initialFloor.guestSpawned))
        { report.failures.Add("Run alternate floor checks from a fresh lobby before starting floor missions."); return report.ToString(); }
        var snapshot = new JourneyCheckSnapshot(host);
        var motion = new FloorVerificationMotionSnapshot(host);
        try
        {
            FloorStartVerificationNight(host);
            FloorJourneyWrite(host, "secondsLeft", 110f); Call(host, "TickFloorMissions", .1f);
            var state = Property<SimBarFloorMissions.State>(host, "FloorState");
            object queue = Read<object>(host, "orderQueue");
            Array orders = (Array)queue.GetType().GetMethod("ToArray").Invoke(queue, null);
            int served = Property<int>(host, "JourneyOrders");
            float clock = Property<float>(host, "JourneySecondsLeft");
            Call(host, "FloorGuestChoice", 0); Call(host, "FloorGuestChoice", 1); Call(host, "FloorGuestChoice", 2);
            object worker = Read<object>(host, "floorEscortWorker");
            Transform workerRoot = worker == null ? null : FloorWorkerRoot(worker);
            Vector3 workerStart = workerRoot == null ? Vector3.zero : workerRoot.position;
            Vector3 workerHome = worker == null ? Vector3.zero : FloorPublicRead<Vector3>(worker, "savedPosition");
            report.Check(state.guestComplete && state.litter == 0 && worker != null && Property<bool>(host, "JourneyIsPlaying")
                && !Read<bool>(host, "paused") && !state.cleanupStarted,
                "A courteous exit during the show borrows the real Server without creating a cleanup job or pausing service.");
            int liveTicks = 0;
            while (workerRoot != null && Vector3.Distance(workerStart, workerRoot.position) < .1f && liveTicks++ < 30)
                FloorAdvanceLive(host);
            Array currentOrders = (Array)queue.GetType().GetMethod("ToArray").Invoke(queue, null);
            bool sameQueue = orders.Length == currentOrders.Length;
            for (int i = 0; sameQueue && i < orders.Length; i++) sameQueue &= ReferenceEquals(orders.GetValue(i), currentOrders.GetValue(i));
            report.Check(workerRoot != null && Vector3.Distance(workerStart, workerRoot.position) > .1f
                && Property<float>(host, "JourneySecondsLeft") < clock && Property<bool>(host, "JourneyIsPlaying")
                && sameQueue && Property<int>(host, "JourneyOrders") == served,
                "The escort employee walks while the live clock advances; the route cannot consume or replace an ordinary queued order.");
            Call(host, "CommitPlayerPour", FrontDrink(host), 1f);
            report.Check(Property<int>(host, "JourneyOrders") == served + 1 && Property<bool>(host, "JourneyIsPlaying")
                && Read<object>(host, "floorEscortWorker") != null,
                "The player can still serve an ordinary customer while the Server assists the departing guest.");
            float serviceCash = Read<float>(host, "cash");
            while (!Property<bool>(host, "FloorGuestExitFinished") && liveTicks++ < 400) FloorAdvanceLive(host);
            if (!Property<bool>(host, "FloorGuestExitFinished")) report.measurements.Add("Live escort timeout: " + FloorActorDiagnostic(host));
            Transform world = Read<Transform>(host, "worldRoot");
            report.Check(Property<bool>(host, "FloorGuestExitFinished") && Read<object>(host, "floorEscortWorker") == null
                && workerRoot != null && Vector3.Distance(world.InverseTransformPoint(workerRoot.position), workerHome) <= .22f,
                "The live escort reaches the exit and returns the existing employee to the original bar position.");
            report.Check(Property<bool>(host, "JourneyIsPlaying") && !Read<bool>(host, "paused")
                && !state.cleanupStarted && !state.cleanupRewardClaimed && Read<float>(host, "cash") == serviceCash,
                "Completing a guest-only mission leaves the show running and cannot manufacture a cleanup payment.");
            Call(host, "TickJourney", 180f);
            report.Check(Property<bool>(host, "JourneyIsResults") && FloorJourneyRead<int>(host, "resultFloorTasks") == 1
                && FloorJourneyRead<float>(host, "resultFloorBonus") == 0f,
                "A later clean-floor last call settles normally with one guest mission and no paper-cleanup bonus.");

            // Start a second temporary night and resolve both jobs at the same time.
            FloorStartVerificationNight(host);
            Call(host, "FloorThrow", 0); Call(host, "FloorThrow", 1);
            FloorJourneyWrite(host, "secondsLeft", 110f); Call(host, "TickFloorMissions", .1f);
            state = Property<SimBarFloorMissions.State>(host, "FloorState");
            float openingCash = Read<float>(host, "cash");
            Call(host, "TickJourney", 180f); Call(host, "CloseFloorMissionDrawer"); Call(host, "FloorAssignCleanup");
            IList crew = Read<IList>(host, "floorCleaningCrew");
            Call(host, "FloorGuestChoice", 0); Call(host, "FloorGuestChoice", 1); Call(host, "FloorGuestChoice", 2);
            worker = Read<object>(host, "floorEscortWorker");
            report.Check(Property<bool>(host, "JourneyIsFloorClosing") && state.cleanupActive && crew.Count == 2
                && worker != null && crew.Contains(worker) && Property<int>(host, "CleanupReadyWorkers") <= 1,
                "Concurrent closing lends one actual cleanup employee to the guest escort and excludes that employee from ready labor.");
            float escortStartCleaned = state.cleaned, cleanedAtReturn = state.cleaned;
            int concurrentTicks = 0, peakOtherReady = 0;
            bool escortReturned = false, workerResumed = false, remainedClosingUntilReturn = true, capturedConcurrentBlocker = false;
            while (!Property<bool>(host, "JourneyIsResults") && concurrentTicks++ < 1600)
            {
                FloorAdvance(host, 1);
                if (Read<object>(host, "floorEscortWorker") != null)
                {
                    peakOtherReady = Mathf.Max(peakOtherReady, Property<int>(host, "CleanupReadyWorkers"));
                    remainedClosingUntilReturn &= Property<bool>(host, "JourneyIsFloorClosing") && !state.cleanupRewardClaimed;
                    if (!capturedConcurrentBlocker)
                        foreach (object cleaner in crew)
                            if (!ReferenceEquals(cleaner, worker) && FloorPublicRead<float>(FloorPublicRead<object>(cleaner, "route"), "blocked") >= 2f)
                            {
                                report.measurements.Add("Concurrent employee blocked during escort at tick " + concurrentTicks + ": " + FloorActorDiagnostic(host));
                                capturedConcurrentBlocker = true; break;
                            }
                }
                else if (!escortReturned)
                { escortReturned = true; cleanedAtReturn = state.cleaned; }
                if (escortReturned && worker != null && crew.Contains(worker))
                    workerResumed |= FloorPublicRead<bool>(FloorPublicRead<object>(worker, "route"), "ready") && state.cleaned > escortStartCleaned;
            }
            if (!Property<bool>(host, "JourneyIsResults")) report.measurements.Add("Concurrent closing timeout: " + FloorActorDiagnostic(host));
            report.Check(peakOtherReady == 1 && cleanedAtReturn > escortStartCleaned && remainedClosingUntilReturn,
                "During the escort, only the other arrived employee advances cleanup and settlement remains deferred.");
            report.Check(escortReturned && workerResumed,
                "The borrowed Server returns to an actual cleanup target and resumes credited floor work.");
            report.Check(Property<bool>(host, "JourneyIsResults") && state.cleanupComplete && state.guestComplete
                && Property<bool>(host, "FloorGuestExitFinished") && SimBarFloorMissions.Remaining(state) == 0,
                "Concurrent cleanup and courteous exit both finish through real movement before opening the nightly result.");
            report.Check(FloorJourneyRead<int>(host, "resultFloorTasks") == 2
                && FloorJourneyRead<float>(host, "resultFloorBonus") == SimBarFloorMissions.CleanupReward
                && Mathf.Abs(Read<float>(host, "cash") - openingCash - Property<int>(host, "JourneyResultBonus") - SimBarFloorMissions.CleanupReward) < .001f,
                "Concurrent jobs produce the same single cleanup receipt and two completed mission records.");
            float settledCash = Read<float>(host, "cash"); Call(host, "SettleJourneyNight"); Call(host, "FinishJourneyNight");
            report.Check(Read<float>(host, "cash") == settledCash, "Concurrent completion is also idempotent under repeated settlement callbacks.");
            report.measurements.Add("Alternate actor paths: guest-only live escort=" + liveTicks + " ticks; concurrent closing=" + concurrentTicks
                + " ticks; other ready staff=" + peakOtherReady + "; work completed before escort return=" + cleanedAtReturn.ToString("F2")
                + "; limits 40 live seconds / 160 concurrent seconds; snapshots restore original save and motion.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            if (report.failures.Count > 0) report.measurements.Add("Alternate failure actor state: " + FloorActorDiagnostic(host));
            Call(host, "CloseFloorMissionDrawer"); snapshot.Restore(host); motion.Restore();
            Call(host, "SyncFloorWorld"); Call(host, "RefreshFloorMissionUI");
        }
        return report.ToString();
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
