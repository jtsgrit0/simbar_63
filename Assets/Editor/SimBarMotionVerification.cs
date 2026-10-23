using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks live movement and presentation without running the economy or consuming a night.</summary>
public static class SimBarMotionVerification
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("SimBar/Validation/Check Running Motion")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || !Read<bool>(host, "motionReady"))
        {
            report.failures.Add("Enter Play Mode and wait for venue motion initialization before checking motion.");
            return report.ToString();
        }
        if (host.IsPourInProgress || Read<bool>(host, "opMenuOpen") || Read<GameObject>(host, "settingsOverlay").activeSelf)
        {
            report.failures.Add("Finish the current pour and close management/settings before checking motion.");
            return report.ToString();
        }
        var snapshot = new Snapshot(host);
        try
        {
            Write(Read<object>(host, "journey"), "inProgress", true);
            SetPhase(host, "Playing");
            Write(host, "paused", false);
            Write(host, "journeyValidationMode", false);
            Write(host, "motionOrdersDirty", true);
            var gameplay = new GameplayFingerprint(host);
            var actors = CollectActors(host);
            report.Check(actors.Count >= 4, "The live movement registry contains the venue actors.");
            CheckCrowdModels(report, host, actors);
            CheckRouteRegressions(report, host, actors);
            Call(host, "TickVenueMotion", 1f / 30f);
            CheckManualHold(report, host, actors);
            CheckTravel(report, host, actors);
            report.Check(gameplay.Matches(host), "Motion alone preserves queue identity/order, drinks, patience, economy, timers and career state.");
            CheckCrowdOrderReservation(report, host, actors);
            CheckSeatedBarGuests(report, host, actors);
            CheckRelaxedGuests(report, Read<Transform>(host, "worldRoot"));
            CheckStageLighting(report, host);
            CheckFrozen(report, host, "paused");
            CheckFrozen(report, host, "lobby");
            CheckFrozen(report, host, "management");
            CheckBandPresentation(report, Read<Transform>(host, "worldRoot"));
            report.Check(snapshot.PreferencesMatch(), "Movement and presentation never rewrite gameplay or interface preferences.");
            report.measurements.Add("Simulated 60 seconds of travel/concert actions plus focused pour/order reservation checks in 1/30-second steps, without Update or economy/journey ticks. All live transforms, motion state, presentation state and preferences restored.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally
        {
            try { snapshot.Restore(host); }
            catch (Exception error) { report.failures.Add("Motion verification restore failed: " + error.GetBaseException()); }
        }
        return report.ToString();
    }

    private sealed class Actor
    {
        public object state, person;
        public Transform root;
        public SimBarCharacterVisual visual;
        public Transform world;
        public float distance;
        public Vector3 previous;
        public string Kind => Convert.ToString(Read<object>(state, "kind"));
        public bool IsWorker => Kind == "BarStaff" || Kind == "FloorStaff";
        public bool IsMobile => Kind == "Guest" || IsWorker;
        public bool IsCollidable => Kind != "Band";
        public int PitSlot => Read<int>(state, "pitSlot");
        public bool PitActive => Read<bool>(state, "pitActive");
        public bool IsAirborneSurfer => visual != null && visual.CrowdAction == SimBarCrowdAction.Surf
            && world.InverseTransformPoint(root.position).y - Read<Vector3>(state, "home").y > 0.6f;
    }

    private static List<Actor> CollectActors(SimBarExperience host)
    {
        var actors = new List<Actor>();
        foreach (object state in Read<IEnumerable>(host, "motionActors"))
        {
            object person = Read<object>(state, "person");
            Transform root = Read<Transform>(person, "root");
            if (root == null) continue;
            actors.Add(new Actor { state = state, person = person, root = root, world = Read<Transform>(host, "worldRoot"), visual = root.GetComponent<SimBarCharacterVisual>(), previous = root.position });
        }
        return actors;
    }

    private static void CheckRouteRegressions(SimBarRuntimeVerification.Report report, SimBarExperience host, List<Actor> actors)
    {
        object navigation = Read<object>(host, "motionNavigation");
        var path = new List<Vector3>();
        var occupancy = new List<Vector3>();
        Vector3 start = new Vector3(-0.48f, 0f, -3.60f);
        Vector3 goal = new Vector3(-1.50f, 0f, -1.75f);
        bool found = (bool)Call(navigation, "FindPath", start, goal, path, occupancy);
        bool edgesClear = found && path.Count > 0;
        Vector3 previous = start;
        foreach (Vector3 waypoint in path)
        {
            edgesClear &= (bool)Call(navigation, "SegmentClear", previous, waypoint);
            previous = waypoint;
        }
        report.Check(edgesClear && FlatDistance(previous, goal) < 0.001f,
            "The booth-corner route keeps every edge clear, including the exact start-to-first-grid-node edge.");

        Vector3 blocked = new Vector3(-6f, 0f, -1.5f);
        bool blockedGoalRejected = !(bool)Call(navigation, "FindPath", start, blocked, path, occupancy) && path.Count == 0;
        bool blockedStartRejected = !(bool)Call(navigation, "FindPath", blocked, goal, path, occupancy) && path.Count == 0;
        report.Check(blockedGoalRejected && blockedStartRejected,
            "A start or destination inside furniture is rejected instead of silently routing to a nearby grid point.");

        Actor guest = actors.Find(actor => actor.Kind == "Guest" && actor.root.gameObject.activeInHierarchy);
        if (guest == null) { report.failures.Add("A live moving guest is required to check failed-route state handling."); return; }
        var actorState = new ObjectState(guest.state, true);
        var rootState = new TransformState(guest.root);
        Vector3 position = guest.root.position;
        try
        {
            Type stateType = guest.state.GetType().GetField("state", Fields).FieldType;
            Call(host, "SetMotionPath", guest.state, blocked, Enum.Parse(stateType, "ToOrder"));
            Call(host, "StepMotionActor", guest.state, 0.1f);
            Call(host, "StepMotionActor", guest.state, 0.1f);
            report.Check(Vector3.Distance(position, guest.root.position) < 0.00001f && Read<bool>(guest.state, "routeFailed")
                && Convert.ToString(Read<object>(guest.state, "state")) == "ToOrder" && Read<IList>(guest.state, "path").Count == 0,
                "An impossible order route holds position and remains ToOrder/routeFailed instead of becoming Waiting.");
        }
        finally { actorState.Restore(); rootState.Restore(); }
    }

    private static void CheckTravel(SimBarRuntimeVerification.Report report, SimBarExperience host, List<Actor> actors)
    {
        const float step = 1f / 30f;
        foreach (Actor actor in actors) { actor.previous = actor.root.position; actor.distance = 0f; }
        var pairs = new List<Pair>();
        for (int i = 0; i < actors.Count; i++)
        for (int j = i + 1; j < actors.Count; j++)
            if (actors[i].IsCollidable && actors[j].IsCollidable && actors[i].root.gameObject.activeInHierarchy && actors[j].root.gameObject.activeInHierarchy)
                pairs.Add(new Pair(actors[i], actors[j]));
        float maximumSpeed = 0f, maximumPitSpeed = 0f;
        var crowd = new CrowdProbe(host, actors, report);
        int blockedSamples = 0, newOverlaps = 0, markerFailures = 0;
        string firstBlocked = "", firstOverlap = "";
        float beginning = Read<float>(host, "motionClock");
        for (int frame = 0; frame < 1800; frame++)
        {
            Call(host, "TickVenueMotion", step);
            foreach (Actor actor in actors)
            {
                if (actor.root == null || !actor.root.gameObject.activeInHierarchy) continue;
                float moved = FlatDistance(actor.previous, actor.root.position);
                actor.distance += moved;
                if (actor.IsMobile)
                {
                    if (actor.PitActive) maximumPitSpeed = Mathf.Max(maximumPitSpeed, moved / step);
                    else maximumSpeed = Mathf.Max(maximumSpeed, moved / step);
                    if (moved > 0.00001f && !PointWalkable(host, actor, actor.root.position))
                    {
                        blockedSamples++;
                        if (firstBlocked.Length == 0) firstBlocked = actor.root.name + " @ " + actor.root.position;
                    }
                }
                actor.previous = actor.root.position;
                GameObject marker = Read<GameObject>(actor.person, "orderMarker");
                if (marker != null && marker.activeSelf && (!marker.transform.IsChildOf(actor.root)
                    || FlatDistance(marker.transform.position, actor.root.position) > 0.22f)) markerFailures++;
            }
            foreach (Pair pair in pairs)
            {
                float distance = ActorSpacing(pair.a, pair.b);
                pair.minimum = Mathf.Min(pair.minimum, distance); pair.best = Mathf.Max(pair.best, distance);
                if (pair.initial >= 0.4f && distance < 0.395f)
                {
                    newOverlaps++;
                    if (firstOverlap.Length == 0) firstOverlap = pair.a.root.name + " / " + pair.b.root.name + " = " + distance.ToString("F3");
                }
            }
            crowd.Sample();
        }
        int guestsMoved = 0, staffMoved = 0, inheritedOverlaps = 0, improvedOverlaps = 0;
        float longest = 0f;
        foreach (Actor actor in actors)
        {
            longest = Mathf.Max(longest, actor.distance);
            if (actor.distance < 0.25f || !actor.IsMobile) continue;
            if (actor.IsWorker) staffMoved++; else guestsMoved++;
        }
        bool inheritedDidNotWorsen = true;
        foreach (Pair pair in pairs)
        {
            if (pair.initial >= 0.4f || (!pair.a.IsMobile && !pair.b.IsMobile)) continue;
            inheritedOverlaps++;
            inheritedDidNotWorsen &= pair.minimum >= pair.initial - 0.015f;
            if (pair.best >= Mathf.Min(0.4f, pair.initial + 0.025f)) improvedOverlaps++;
        }
        report.Check(Mathf.Abs(Read<float>(host, "motionClock") - beginning - 60f) < 0.02f, "Only the managed motion clock advances by the simulated 60 seconds.");
        report.Check(guestsMoved >= 3, "At least three guests travel along their routes.");
        report.Check(staffMoved >= 1, "At least one bartender/server travels to a work or order position.");
        report.Check(maximumSpeed <= 1.2f + 0.015f, "Ordinary routed NPCs stay within 1.2 m/s; peak=" + maximumSpeed.ToString("F3") + ".");
        report.Check(maximumPitSpeed <= 1.35f + 0.015f, "Concert participants stay within their 1.35 m/s horizontal movement limit; peak=" + maximumPitSpeed.ToString("F3") + ".");
        report.Check(blockedSamples == 0, "Moving NPC samples stay outside static obstacles. " + firstBlocked);
        report.Check(newOverlaps == 0, "Movement does not introduce NPC separations below 0.4 m. " + firstOverlap);
        report.Check(inheritedDidNotWorsen && improvedOverlaps == inheritedOverlaps, "Initial overlaps do not worsen and improve rather than deadlock.");
        report.Check(markerFailures == 0, "Visible order markers follow their moving guest roots.");
        crowd.Report();
        report.measurements.Add("Moving guests=" + guestsMoved + ", staff=" + staffMoved + "; longest path=" + longest.ToString("F2")
            + " m; peak ordinary/pit speed=" + maximumSpeed.ToString("F3") + "/" + maximumPitSpeed.ToString("F3") + " m/s; initial overlaps=" + inheritedOverlaps + ", improved=" + improvedOverlaps + ".");
    }

    private sealed class Pair
    {
        public Actor a, b;
        public float initial, minimum, best;
        public Pair(Actor a, Actor b) { this.a = a; this.b = b; initial = minimum = best = ActorSpacing(a, b); }
    }

    private static void CheckCrowdModels(SimBarRuntimeVerification.Report report, SimBarExperience host, List<Actor> actors)
    {
        var patrons = new HashSet<object>();
        foreach (object patron in Read<IEnumerable>(host, "patrons")) patrons.Add(patron);
        var bandSources = new HashSet<string>(); var crowdSources = new HashSet<string>();
        var slots = new HashSet<int>();
        int cosmetic = 0, orderedCosmetic = 0;
        foreach (Actor actor in actors)
        {
            if (actor.visual == null) continue;
            string source = ModelSource(actor.visual);
            if (actor.Kind == "Band") bandSources.Add(source);
            if (actor.PitSlot < 0) continue;
            slots.Add(actor.PitSlot); crowdSources.Add(source);
            if (actor.PitSlot >= 8)
            {
                cosmetic++;
                if (patrons.Contains(actor.person) || Read<int>(actor.person, "drinkIndex") >= 0) orderedCosmetic++;
                foreach (object queued in QueueContents(host)) if (ReferenceEquals(queued, actor.person)) orderedCosmetic++;
            }
        }
        int duplicates = 0;
        foreach (string source in crowdSources) if (bandSources.Contains(source)) duplicates++;
        report.Check(patrons.Count == 24 && slots.Count == 12 && cosmetic == 4 && orderedCosmetic == 0,
            "The original 24 orderable guests remain intact; four cosmetic concert actors never enter the order pool or queue.");
        report.Check(!crowdSources.Contains("") && crowdSources.Count >= 6 && duplicates == 0,
            "The stage-front audience uses at least six distinct source models and does not duplicate the band models. crowd=" + crowdSources.Count + ", band overlaps=" + duplicates + ".");
    }

    private static string ModelSource(SimBarCharacterVisual visual)
    {
        GameObject model = Read<GameObject>(visual, "model");
        var paths = new HashSet<string>();
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string path = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            if (!string.IsNullOrEmpty(path)) paths.Add(path);
        }
        var sorted = new List<string>(paths); sorted.Sort(StringComparer.Ordinal);
        return string.Join("|", sorted);
    }

    private sealed class CrowdProbe
    {
        private readonly SimBarExperience host;
        private readonly List<Actor> actors;
        private readonly SimBarRuntimeVerification.Report report;
        private readonly Dictionary<Actor, float> lastAngles = new Dictionary<Actor, float>();
        private readonly Dictionary<Actor, float> sweeps = new Dictionary<Actor, float>();
        private readonly HashSet<SimBarCrowdAction> seen = new HashSet<SimBarCrowdAction>();
        private int boundsFailures, floorFailures, phaseFailures, fullSurfs, validSurfs, supportedSurfs;
        private float slamMinimum = float.PositiveInfinity, slamMaximum;
        private float maximumHandTargetError;
        private bool airborneFreezeChecked;
        public CrowdProbe(SimBarExperience host, List<Actor> actors, SimBarRuntimeVerification.Report report) { this.host = host; this.actors = actors; this.report = report; }
        public void Sample()
        {
            Actor leftSlam = null, rightSlam = null, surfer = null;
            float clock = Read<float>(host, "motionClock");
            foreach (Actor actor in actors)
            {
                Vector3 p = actor.world.InverseTransformPoint(actor.root.position);
                if (actor.Kind == "Band" && (p.y < 0.5f || p.z < 2.2f)) floorFailures++;
                if (actor.visual != null && actor.visual.Role == "guest" && actor.PitSlot != 11 && Mathf.Abs(p.y) > 0.025f) floorFailures++;
                if (!actor.PitActive || actor.visual == null) continue;
                if (p.x < 3.48f || p.x > 8.02f || p.z < -5.02f || p.z > 0.52f) boundsFailures++;
                SimBarCrowdAction action = actor.visual.CrowdAction;
                if (actor.visual.CrowdWeight > 0.1f) seen.Add(action);
                if (Mathf.Abs(Mathf.DeltaAngle(actor.visual.CrowdPhase * 360f, Mathf.Repeat(clock * 2f, 1f) * 360f)) > 0.02f) phaseFailures++;
                if (action == SimBarCrowdAction.Mosh)
                {
                    float angle = Mathf.Atan2(p.z + 1.25f, p.x - 5.75f) * Mathf.Rad2Deg;
                    if (lastAngles.TryGetValue(actor, out float before)) sweeps[actor] += Mathf.DeltaAngle(before, angle);
                    else sweeps[actor] = 0f;
                    lastAngles[actor] = angle;
                }
                if (actor.PitSlot == 6) leftSlam = actor;
                if (actor.PitSlot == 7) rightSlam = actor;
                if (actor.PitSlot == 11 && actor.visual.CrowdWeight > 0.999f) surfer = actor;
            }
            if (leftSlam != null && rightSlam != null)
            {
                float distance = FlatDistance(leftSlam.root.position, rightSlam.root.position);
                slamMinimum = Mathf.Min(slamMinimum, distance); slamMaximum = Mathf.Max(slamMaximum, distance);
            }
            if (surfer == null) return;
            fullSurfs++;
            var surfBones = Read<Dictionary<string, Transform>>(surfer.visual, "bones");
            Transform hip = surfer.visual.CrowdHipAnchor;
            Vector3 head = surfBones["head"].position, feet = (surfBones["footl"].position + surfBones["footr"].position) * 0.5f;
            float height = surfer.world.InverseTransformPoint(hip.position).y;
            bool horizontal = Mathf.Abs(Vector3.Dot((head - hip.position).normalized, surfer.world.up)) < 0.4f;
            bool geometry = height >= 1.4f && height <= 2.3f && horizontal && Vector3.Distance(hip.position, surfer.root.position) < 0.08f;
            if (geometry) validSurfs++;
            int contacts = 0;
            foreach (Actor support in actors)
            {
                if (support.visual == null || support.visual.CrowdAction != SimBarCrowdAction.Support || support.visual.CrowdWeight < 0.999f
                    || !support.visual.HasCrowdSupportTarget) continue;
                var bones = Read<Dictionary<string, Transform>>(support.visual, "bones");
                Vector3 hands = (bones["handl"].position + bones["handr"].position) * 0.5f;
                Vector3 shoulders = (bones["upperarml"].position + bones["upperarmr"].position) * 0.5f;
                float error = Vector3.Distance(hands, support.visual.CrowdSupportTarget);
                maximumHandTargetError = Mathf.Max(maximumHandTargetError, error);
                if (error <= 0.35f && Vector3.Dot(hands - shoulders, support.world.up) > 0.05f
                    && PointSegmentDistance(support.visual.CrowdSupportTarget, head, feet) <= 0.4f) contacts++;
            }
            if (geometry && contacts >= 2) supportedSurfs++;
            if (geometry && !airborneFreezeChecked)
            {
                CheckFrozen(report, host, "paused"); airborneFreezeChecked = true;
            }
        }
        public void Report()
        {
            float largestSweep = 0f; foreach (float sweep in sweeps.Values) largestSweep = Mathf.Max(largestSweep, Mathf.Abs(sweep));
            report.Check(boundsFailures == 0 && floorFailures == 0, "Concert actors stay on the audience floor/in pit bounds, while band roots remain on the stage. bounds=" + boundsFailures + ", floor=" + floorFailures + ".");
            report.Check(seen.Contains(SimBarCrowdAction.Mosh) && seen.Contains(SimBarCrowdAction.Slam) && seen.Contains(SimBarCrowdAction.Support)
                && seen.Contains(SimBarCrowdAction.Surf) && largestSweep > 180f && slamMaximum - slamMinimum > 0.1f,
                "The live audience completes a moving mosh circle, recoiling slam pair, supporters and a crowd-surfer cycle.");
            report.Check(fullSurfs > 0 && validSurfs == fullSurfs && supportedSurfs > 0 && supportedSurfs * 2 >= fullSurfs && airborneFreezeChecked,
                "The airborne surfer lies horizontally above the crowd with at least two raised-hand supporters and freezes when paused. valid="
                + validSurfs + "/" + fullSurfs + ", supported=" + supportedSurfs + ", max hand-target error=" + maximumHandTargetError.ToString("F3") + " m.");
            report.Check(phaseFailures == 0, "Concert action phases use the shared gameplay clock.");
            report.measurements.Add("Mosh sweep=" + largestSweep.ToString("F0") + " degrees; slam separation range=" + (slamMaximum - slamMinimum).ToString("F3")
                + " m; supported airborne samples=" + supportedSurfs + "/" + fullSurfs + ".");
        }
    }

    private static void CheckCrowdOrderReservation(SimBarRuntimeVerification.Report report, SimBarExperience host, List<Actor> actors)
    {
        Actor target = actors.Find(actor => actor.PitSlot >= 0 && actor.PitSlot < 8 && actor.PitActive
            && Read<int>(actor.person, "drinkIndex") < 0 && actor.visual != null);
        if (target == null) { report.failures.Add("An unreserved mosh/slam participant is required for the order/pour exclusion check."); return; }
        var snapshot = new Snapshot(host);
        var economic = new Dictionary<string, object>();
        foreach (string field in new[] { "cash", "fans", "gameClock", "servedTonight", "satisfaction", "performance", "requestTimer", "opWageTimer", "opAutoServeTimer" }) economic[field] = Read<object>(host, field);
        string journey = (string)Call(host, "ExportJourneyState");
        Array originalQueue = QueueContents(host);
        Vector3 initialPosition = target.root.position;
        try
        {
            Call(host, "AssignOrder", target.person, 0);
            for (int frame = 0; frame < 180; frame++) Call(host, "TickVenueMotion", 1f / 30f);
            Array ordered = QueueContents(host);
            bool queueIntact = ordered.Length == originalQueue.Length + 1 && ReferenceEquals(ordered.GetValue(ordered.Length - 1), target.person);
            for (int i = 0; i < originalQueue.Length && queueIntact; i++) queueIntact &= ReferenceEquals(ordered.GetValue(i), originalQueue.GetValue(i));
            report.Check(queueIntact && !target.PitActive && target.visual.CrowdAction == SimBarCrowdAction.None
                && Read<int>(target.state, "queueIndex") >= 0 && FlatDistance(initialPosition, target.root.position) > 0.25f,
                "Assigning an order removes a concert participant from the pit and sends it along the normal service route without reordering existing guests.");

            object queue = Read<object>(host, "orderQueue");
            Call(queue, "Clear"); Call(queue, "Enqueue", target.person);
            CheckManualHold(report, host, actors);
            report.Check(!target.PitActive && target.visual.CrowdAction == SimBarCrowdAction.None
                && Mathf.Abs(target.world.InverseTransformPoint(target.root.position).y) < 0.025f,
                "A manual-pour target stays grounded and cannot rejoin the mosh/slam actions while reserved.");

            Call(host, "ClearOrder", target.person);
            Call(queue, "Clear"); foreach (object person in originalQueue) Call(queue, "Enqueue", person);
            bool returned = false;
            for (int frame = 0; frame < 360; frame++)
            {
                Call(host, "TickVenueMotion", 1f / 30f);
                returned |= target.PitActive && (target.visual.CrowdAction == SimBarCrowdAction.Mosh || target.visual.CrowdAction == SimBarCrowdAction.Slam);
            }
            report.Check(returned, "After its order clears, a participant returns through normal navigation and rejoins its concert action.");
            bool unchanged = journey == (string)Call(host, "ExportJourneyState");
            foreach (var field in economic) unchanged &= Equals(field.Value, Read<object>(host, field.Key));
            report.Check(unchanged, "Concert reservation, manual hold and return never serve drinks, pay rewards or advance economic/night timers.");
        }
        finally { snapshot.Restore(host); }
    }

    private static bool PointWalkable(SimBarExperience host, Actor actor, Vector3 position)
    {
        Vector3 local = Read<Transform>(host, "worldRoot").InverseTransformPoint(position);
        // Bar staff use the separate work lane behind the counter, outside the public navigation grid.
        if (actor.Kind == "BarStaff" && Read<bool>(host, "motionReferenceLayout"))
            return local.x >= Read<float>(actor.state, "workMin") - 0.001f && local.x <= Read<float>(actor.state, "workMax") + 0.001f
                && Mathf.Abs(local.z + 0.565f) <= 0.021f;
        return (bool)Call(host, "IsMotionPointWalkable", local);
    }

    private static void CheckManualHold(SimBarRuntimeVerification.Report report, SimBarExperience host, List<Actor> actors)
    {
        Array queue = QueueContents(host);
        if (queue.Length == 0) { report.failures.Add("A queued guest is required to check manual-pour movement reservation."); return; }
        object target = queue.GetValue(0);
        Transform root = Read<Transform>(target, "root");
        Vector3 position = root.position;
        float patience = Read<float>(target, "patience");
        object previousTarget = Read<object>(host, "pourPatron");
        int previousDrink = Read<int>(host, "pourDrinkIndex");
        Write(host, "pourRunning", true); Write(host, "pourPatron", target); Write(host, "pourDrinkIndex", Read<int>(target, "drinkIndex"));
        try
        {
            for (int frame = 0; frame < 60; frame++) Call(host, "TickVenueMotion", 1f / 30f);
            report.Check(FlatDistance(position, root.position) < 0.0001f && ReferenceEquals(QueueContents(host).GetValue(0), target)
                && Read<float>(target, "patience") == patience, "An active manual pour holds the captured guest position, queue identity and patience.");
        }
        finally { Write(host, "pourRunning", false); Write(host, "pourPatron", previousTarget); Write(host, "pourDrinkIndex", previousDrink); }
    }

    private static void CheckFrozen(SimBarRuntimeVerification.Report report, SimBarExperience host, string context)
    {
        bool paused = Read<bool>(host, "paused"), managing = Read<bool>(host, "opMenuOpen");
        object phase = Read<object>(host, "journeyPhase");
        var pose = CapturePose(Read<Transform>(host, "worldRoot"));
        var lighting = new LightingState(host);
        float clock = Read<float>(host, "motionClock");
        if (context == "lobby") { SetPhase(host, "Lobby"); Write(host, "paused", false); }
        else { Write(host, "paused", true); Write(host, "opMenuOpen", context == "management"); }
        try
        {
            Call(host, "TickVenueMotion", 1f);
            report.Check(Read<float>(host, "motionClock") == clock && SamePose(pose) && lighting.Matches(), context + " freezes routes, seated drinking, bones, held props, stage lights and visible beams on the same presentation clock.");
        }
        finally { Write(host, "paused", paused); Write(host, "opMenuOpen", managing); Write(host, "journeyPhase", phase); }
    }

    private static void CheckSeatedBarGuests(SimBarRuntimeVerification.Report report, SimBarExperience host, List<Actor> actors)
    {
        Transform world = Read<Transform>(host, "worldRoot");
        int count = 0;
        foreach (Actor actor in actors)
        {
            if (!Read<string>(actor.person, "name").StartsWith("Bar Guest ", StringComparison.Ordinal)) continue;
            count++;
            SimBarCharacterVisual visual = actor.visual;
            bool seated = visual != null && visual.Role == "barseated";
            report.Check(seated && actor.Kind == "Stationary" && Mathf.Abs(visual.seatHeight - 1.12f) < 0.01f
                && actor.distance < 0.001f, actor.root.name + " remains seated on its bar stool at the configured seat height.");
            if (!seated) continue;
            var bones = Read<Dictionary<string, Transform>>(visual, "bones");
            Transform pivot = Read<Transform>(visual, "pivot");
            Transform cup = Property<Transform>(visual, "DrinkingCup");
            Transform rim = Property<Transform>(visual, "DrinkingCupRim");
            report.Check(cup != null && rim != null && cup.IsChildOf(visual.transform), actor.root.name + " has a persistent drinking cup and rim anchor.");
            if (cup == null || rim == null) continue;
            Vector3 rootPosition = actor.root.position;
            float minSip = 1f, maxSip = 0f, minHandToHead = float.PositiveInfinity, maxHandToHead = 0f;
            float greatestCupHandOffset = 0f, smallestMouthError = float.PositiveInfinity, largestHipError = 0f;
            float lowestRestCup = float.PositiveInfinity;
            Vector3 lowestRestCupPosition = Vector3.zero;
            var peakCupPositions = new List<Vector3>();
            bool bentLegs = true, cupVisible = true;
            for (int frame = 0; frame <= 260; frame++)
            {
                visual.ApplyPresentation(frame * 0.05f, 0.95f, 0f, 0f);
                float sip = Property<float>(visual, "SipAmount");
                minSip = Mathf.Min(minSip, sip); maxSip = Mathf.Max(maxSip, sip);
                Transform hand = bones["handr"], head = bones["head"], hip = bones["upperlegl"];
                float handDistance = Vector3.Distance(hand.position, head.position);
                minHandToHead = Mathf.Min(minHandToHead, handDistance); maxHandToHead = Mathf.Max(maxHandToHead, handDistance);
                greatestCupHandOffset = Mathf.Max(greatestCupHandOffset, Vector3.Distance(cup.position, hand.position));
                largestHipError = Mathf.Max(largestHipError, Mathf.Abs(world.InverseTransformPoint(hip.position).y
                    - world.InverseTransformPoint(rootPosition).y - visual.seatHeight));
                if (sip > 0.9f)
                {
                    Vector3 mouth = head.position + pivot.TransformVector(new Vector3(0f, -0.055f, 0.11f));
                    smallestMouthError = Mathf.Min(smallestMouthError, Vector3.Distance(rim.position, mouth));
                    peakCupPositions.Add(cup.position);
                }
                if (sip < 0.1f && Vector3.Dot(cup.position, world.up) < lowestRestCup)
                {
                    lowestRestCup = Vector3.Dot(cup.position, world.up);
                    lowestRestCupPosition = cup.position;
                }
                foreach (string side in new[] { "l", "r" })
                {
                    Transform thigh = bones["upperleg" + side], knee = bones["lowerleg" + side], foot = bones["foot" + side];
                    Vector3 leg = (knee.position - thigh.position).normalized;
                    bentLegs &= Mathf.Abs(Vector3.Dot(leg, world.up)) < 0.55f && Vector3.Dot(foot.position - knee.position, world.up) < -0.08f;
                }
                cupVisible &= cup.gameObject.activeInHierarchy;
            }
            report.Check(Vector3.Distance(rootPosition, actor.root.position) < 0.00001f && largestHipError < 0.10f && bentLegs,
                actor.root.name + " keeps a fixed root, seated hips and bent knees throughout its drinking cycle.");
            float cupTravel = 0f, cupHeightDelta = 0f;
            if (!float.IsPositiveInfinity(lowestRestCup))
                foreach (Vector3 peak in peakCupPositions)
                {
                    float travel = Vector3.Distance(lowestRestCupPosition, peak);
                    if (travel <= cupTravel) continue;
                    cupTravel = travel;
                    cupHeightDelta = Vector3.Dot(peak - lowestRestCupPosition, world.up);
                }
            report.Check(minSip < 0.1f && maxSip > 0.9f && maxHandToHead - minHandToHead >= 0.12f && cupTravel >= 0.12f && cupHeightDelta >= 0.035f,
                actor.root.name + " periodically raises its hand to drink and returns to rest. minSip=" + minSip.ToString("F3")
                + ", maxSip=" + maxSip.ToString("F3") + ", handToHeadRange=" + (maxHandToHead - minHandToHead).ToString("F3")
                + " m, cupTravel=" + cupTravel.ToString("F3") + " m, cupHeightDelta=" + cupHeightDelta.ToString("F3") + " m.");
            report.Check(cupVisible && greatestCupHandOffset < 0.16f && smallestMouthError < 0.16f,
                actor.root.name + " keeps its cup attached to the hand and brings the rim near the mouth.");
        }
        report.Check(count == 4, "Exactly four guests occupy the bar stools.");
    }

    private static void CheckRelaxedGuests(SimBarRuntimeVerification.Report report, Transform world)
    {
        int count = 0;
        float maximumVerticalRange = 0f, maximumSideRange = 0f;
        var raised = new List<string>();
        foreach (SimBarCharacterVisual visual in world.GetComponentsInChildren<SimBarCharacterVisual>(true))
        {
            if (visual.Role != "guest") continue;
            count++;
            visual.SetCrowdPerformance(SimBarCrowdAction.None, 0f);
            visual.ClearCrowdSupportTarget();
            Transform pivot = Read<Transform>(visual, "pivot");
            var bones = Read<Dictionary<string, Transform>>(visual, "bones");
            Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            bool handsLow = true;
            for (int frame = 0; frame <= 120; frame++)
            {
                visual.ApplyPresentation(frame / 30f, 1f, 0f, 0f);
                minimum = Vector3.Min(minimum, pivot.localPosition); maximum = Vector3.Max(maximum, pivot.localPosition);
                foreach (string side in new[] { "l", "r" })
                    handsLow &= Vector3.Dot(bones["hand" + side].position - bones["upperarm" + side].position, world.up) <= 0.10f;
            }
            maximumVerticalRange = Mathf.Max(maximumVerticalRange, maximum.y - minimum.y);
            maximumSideRange = Mathf.Max(maximumSideRange, maximum.x - minimum.x);
            if (!handsLow) raised.Add(visual.name);
        }
        report.Check(count > 0 && maximumVerticalRange <= 0.015f && maximumSideRange <= 0.035f,
            "Standing guests use a relaxed idle without dance bouncing; vertical range=" + maximumVerticalRange.ToString("F3") + " m, side range=" + maximumSideRange.ToString("F3") + " m.");
        report.Check(raised.Count == 0, "Idle guest hands remain below shoulder height instead of using raised dance poses. " + string.Join(", ", raised));
    }

    private static void CheckStageLighting(SimBarRuntimeVerification.Report report, SimBarExperience host)
    {
        var lights = Read<List<Light>>(host, "stageLights");
        var beams = Read<List<Renderer>>(host, "stageBeams");
        float clock = Read<float>(host, "motionClock");
        var colors = new Color[lights.Count];
        var minimum = new float[lights.Count]; var maximum = new float[lights.Count];
        var first = new float[lights.Count]; var opposite = new float[lights.Count];
        var minAlpha = new float[beams.Count]; var maxAlpha = new float[beams.Count];
        bool bounded = true, colorsPreserved = true, beamColorsMatch = true;
        for (int i = 0; i < lights.Count; i++) { colors[i] = lights[i].color; minimum[i] = float.PositiveInfinity; }
        for (int i = 0; i < beams.Count; i++) minAlpha[i] = float.PositiveInfinity;
        try
        {
            for (int frame = 0; frame <= 32; frame++)
            {
                Write(host, "motionClock", frame / 32f); Call(host, "ApplyStageLighting");
                for (int i = 0; i < lights.Count; i++)
                {
                    float value = lights[i].intensity;
                    minimum[i] = Mathf.Min(minimum[i], value); maximum[i] = Mathf.Max(maximum[i], value);
                    bounded &= value >= 1.99f && value <= 30.01f;
                    colorsPreserved &= ColorClose(lights[i].color, colors[i]);
                    if (frame == 0) first[i] = value;
                    if (frame == 16) opposite[i] = value;
                }
                for (int i = 0; i < beams.Count; i++)
                {
                    Color tint = BeamTint(beams[i]);
                    minAlpha[i] = Mathf.Min(minAlpha[i], tint.a); maxAlpha[i] = Mathf.Max(maxAlpha[i], tint.a);
                    bounded &= tint.a >= 0.0069f && tint.a <= 0.1051f;
                    if (i < lights.Count) beamColorsMatch &= Mathf.Abs(tint.r - lights[i].color.r) < 0.001f
                        && Mathf.Abs(tint.g - lights[i].color.g) < 0.001f && Mathf.Abs(tint.b - lights[i].color.b) < 0.001f;
                }
            }
            bool pulsing = lights.Count >= 2, alternating = false, visibleBeamsPulse = beams.Count > 0;
            for (int i = 0; i < lights.Count; i++)
            {
                pulsing &= maximum[i] - minimum[i] > 8f;
                for (int j = i + 1; j < lights.Count; j++) alternating |= (opposite[i] - first[i]) * (opposite[j] - first[j]) < -4f;
            }
            for (int i = 0; i < beams.Count; i++) visibleBeamsPulse &= maxAlpha[i] - minAlpha[i] > 0.05f;
            report.Check(pulsing && alternating && bounded, "Stage light banks alternate their brightness rhythmically within the intended intensity/beam-alpha bounds.");
            report.Check(colorsPreserved && beamColorsMatch && visibleBeamsPulse, "Original stage colors remain intact while visible beam opacity pulses with the matching lights.");
            Write(host, "motionClock", 0.125f); Call(host, "ApplyStageLighting");
            var repeated = new LightingState(host);
            Call(host, "ApplyStageLighting");
            bool deterministic = repeated.Matches();
            Write(host, "motionClock", 1.125f); Call(host, "ApplyStageLighting");
            report.Check(deterministic && repeated.Matches(), "Lighting is deterministic at the same clock and repeats on its one-second bank cycle.");
        }
        finally { Write(host, "motionClock", clock); Call(host, "ApplyStageLighting"); }
    }

    private static void CheckBandPresentation(SimBarRuntimeVerification.Report report, Transform world)
    {
        var roles = new HashSet<string>();
        foreach (SimBarCharacterVisual visual in world.GetComponentsInChildren<SimBarCharacterVisual>(true))
        {
            string role = visual.Role;
            if (role != "singer" && role != "guitarist" && role != "bassist" && role != "drummer") continue;
            roles.Add(role);
            visual.SetManaged(true);
            visual.ApplyPresentation(5f, 0.85f, 0f, 0f);
            var first = CapturePose(visual.transform);
            visual.ApplyPresentation(5f, 0.85f, 0f, 0f);
            report.Check(SamePose(first), role + " is deterministic when the presentation clock is unchanged.");
            visual.ApplyPresentation(5.137f, 0.85f, 0f, 0f);
            report.Check(!SamePose(first), role + " changes its performance pose as the managed clock advances.");
            bool propMoves = false;
            foreach (TransformState state in first)
                if ((state.target.name.IndexOf("microphone", StringComparison.OrdinalIgnoreCase) >= 0
                    || state.target.name.IndexOf("drumstick", StringComparison.OrdinalIgnoreCase) >= 0
                    || state.target.name.StartsWith("Stage ", StringComparison.Ordinal)) && !state.Matches()) propMoves = true;
            report.Check(propMoves, role + " animates its microphone, instrument or sticks with its bones.");
            if (role == "drummer")
            {
                Vector3 position = visual.transform.position;
                visual.ApplyPresentation(5.3f, 0.85f, 0f, 0f);
                report.Check(visual.transform.position == position, "The drummer remains planted behind the kit.");
            }
        }
        report.Check(roles.Contains("singer") && roles.Contains("guitarist") && roles.Contains("bassist") && roles.Contains("drummer"), "All four live band roles receive managed presentation.");
    }

    private sealed class GameplayFingerprint
    {
        private readonly Array queue;
        private readonly string journey;
        private readonly Dictionary<string, object> fields = new Dictionary<string, object>();
        private readonly List<ObjectState> guests = new List<ObjectState>();
        public GameplayFingerprint(SimBarExperience host)
        {
            queue = QueueContents(host); journey = (string)Call(host, "ExportJourneyState");
            foreach (string field in new[] { "cash", "fans", "servedTonight", "gameClock", "requestTimer", "satisfaction", "performance", "opAutoServeTimer", "opWageTimer", "opEventRemaining" }) fields[field] = Read<object>(host, field);
            foreach (object guest in Read<IEnumerable>(host, "patrons")) guests.Add(new ObjectState(guest, false));
        }
        public bool Matches(SimBarExperience host)
        {
            Array current = QueueContents(host);
            if (current.Length != queue.Length || journey != (string)Call(host, "ExportJourneyState")) return false;
            for (int i = 0; i < queue.Length; i++) if (!ReferenceEquals(queue.GetValue(i), current.GetValue(i))) return false;
            foreach (var field in fields) if (!Equals(field.Value, Read<object>(host, field.Key))) return false;
            foreach (ObjectState guest in guests)
                if (!guest.Matches("drinkIndex") || !guest.Matches("patience")) return false;
            return true;
        }
    }

    private sealed class ObjectState
    {
        private readonly object target, clone;
        private readonly List<FieldInfo> fields = new List<FieldInfo>();
        private readonly Dictionary<FieldInfo, object> collectionCopies = new Dictionary<FieldInfo, object>();
        public ObjectState(object target, bool allFields)
        {
            this.target = target;
            clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
            foreach (FieldInfo field in target.GetType().GetFields(Fields))
            {
                if (field.IsStatic || (!allFields && !field.FieldType.IsValueType && field.FieldType != typeof(string) && !field.FieldType.IsArray)) continue;
                fields.Add(field);
                object value = field.GetValue(clone);
                if (value is Array array) collectionCopies[field] = array.Clone();
                else if (value is IList list && !list.IsReadOnly && !list.IsFixedSize)
                {
                    var copy = (IList)Activator.CreateInstance(value.GetType()); foreach (object item in list) copy.Add(item); collectionCopies[field] = copy;
                }
            }
        }
        public bool Matches(string name) { FieldInfo field = target.GetType().GetField(name, Fields); return Equals(field.GetValue(target), field.GetValue(clone)); }
        public void Restore()
        {
            foreach (FieldInfo field in fields)
            {
                object saved = collectionCopies.TryGetValue(field, out object collection) ? collection : field.GetValue(clone), current = field.GetValue(target);
                if (saved is Array source && current is Array destination && source.Length == destination.Length) Array.Copy(source, destination, source.Length);
                else if (saved is IList sourceList && current is IList destinationList && !destinationList.IsReadOnly && !destinationList.IsFixedSize)
                { destinationList.Clear(); foreach (object item in sourceList) destinationList.Add(item); }
                else if (!field.IsInitOnly) field.SetValue(target, saved);
            }
        }
    }

    private sealed class TransformState
    {
        public Transform target;
        private Vector3 position, scale;
        private Quaternion rotation;
        private bool active;
        public TransformState(Transform target) { this.target = target; position = target.localPosition; rotation = target.localRotation; scale = target.localScale; active = target.gameObject.activeSelf; }
        public bool Matches() => target != null && Vector3.Distance(target.localPosition, position) < 0.00002f && SameRotation(target.localRotation, rotation) && Vector3.Distance(target.localScale, scale) < 0.00002f && target.gameObject.activeSelf == active;
        public void Restore() { if (target == null) return; target.localPosition = position; target.localRotation = rotation; target.localScale = scale; target.gameObject.SetActive(active); }
    }

    private sealed class MeshState
    {
        private Mesh mesh;
        private Vector3[] vertices, normals;
        private Bounds bounds;
        public MeshState(Mesh mesh) { this.mesh = mesh; vertices = mesh.vertices; normals = mesh.normals; bounds = mesh.bounds; }
        public void Restore() { if (mesh == null) return; mesh.vertices = vertices; mesh.normals = normals; mesh.bounds = bounds; }
    }

    private sealed class LightState
    {
        private readonly Light target;
        private readonly float intensity, range, spotAngle;
        private readonly Color color;
        private readonly bool enabled;
        public LightState(Light target) { this.target = target; intensity = target.intensity; color = target.color; enabled = target.enabled; range = target.range; spotAngle = target.spotAngle; }
        public bool Matches() => target != null && Mathf.Abs(target.intensity - intensity) < 0.0001f && ColorClose(target.color, color)
            && target.enabled == enabled && Mathf.Abs(target.range - range) < 0.0001f && Mathf.Abs(target.spotAngle - spotAngle) < 0.0001f;
        public void Restore() { if (target == null) return; target.intensity = intensity; target.color = color; target.enabled = enabled; target.range = range; target.spotAngle = spotAngle; }
    }

    private sealed class BeamState
    {
        private readonly Renderer target;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock scratch = new MaterialPropertyBlock();
        private readonly Color baseColor, color;
        private readonly bool enabled;
        public BeamState(Renderer target)
        {
            this.target = target; enabled = target.enabled;
            target.GetPropertyBlock(properties); baseColor = properties.GetColor("_BaseColor"); color = properties.GetColor("_Color");
        }
        public bool Matches()
        {
            if (target == null || target.enabled != enabled) return false;
            target.GetPropertyBlock(scratch);
            return ColorClose(scratch.GetColor("_BaseColor"), baseColor) && ColorClose(scratch.GetColor("_Color"), color);
        }
        public void Restore() { if (target == null) return; target.enabled = enabled; target.SetPropertyBlock(properties); }
    }

    private sealed class LightingState
    {
        private readonly List<LightState> lights = new List<LightState>();
        private readonly List<BeamState> beams = new List<BeamState>();
        public LightingState(SimBarExperience host)
        {
            foreach (Light light in Read<List<Light>>(host, "stageLights")) if (light != null) lights.Add(new LightState(light));
            foreach (Renderer beam in Read<List<Renderer>>(host, "stageBeams")) if (beam != null) beams.Add(new BeamState(beam));
        }
        public bool Matches() { foreach (LightState light in lights) if (!light.Matches()) return false; foreach (BeamState beam in beams) if (!beam.Matches()) return false; return true; }
    }

    private sealed class Snapshot
    {
        private readonly ObjectState hostFields;
        private readonly List<ObjectState> objects = new List<ObjectState>();
        private readonly List<TransformState> transforms;
        private readonly List<MeshState> meshes = new List<MeshState>();
        private readonly List<LightState> lights = new List<LightState>();
        private readonly List<BeamState> beams = new List<BeamState>();
        private readonly List<KeyValuePair<Renderer, Material[]>> markerMaterials = new List<KeyValuePair<Renderer, Material[]>>();
        private readonly Array queue;
        private readonly object journey;
        private readonly string journeyJson;
        private readonly Dictionary<string, string> preferences = new Dictionary<string, string>();
        private readonly bool hadSize;
        private readonly int textSize;
        private readonly UnityEngine.Random.State randomState;
        private readonly IList actors, occupancy;
        private readonly object[] actorItems, occupancyItems;
        public Snapshot(SimBarExperience host)
        {
            hostFields = new ObjectState(host, false);
            randomState = UnityEngine.Random.state;
            journey = Read<object>(host, "journey"); journeyJson = (string)Call(host, "ExportJourneyState"); queue = QueueContents(host);
            actors = Read<IList>(host, "motionActors"); actorItems = CopyList(actors);
            occupancy = Read<IList>(host, "motionOccupancy"); occupancyItems = CopyList(occupancy);
            foreach (object actor in Read<IEnumerable>(host, "motionActors"))
            {
                objects.Add(new ObjectState(actor, true));
                object person = Read<object>(actor, "person");
                objects.Add(new ObjectState(person, false));
                GameObject marker = Read<GameObject>(person, "orderMarker");
                if (marker != null)
                {
                    Renderer renderer = marker.GetComponent<Renderer>();
                    markerMaterials.Add(new KeyValuePair<Renderer, Material[]>(renderer, renderer.sharedMaterials));
                }
            }
            objects.Add(new ObjectState(Read<object>(host, "motionNavigation"), true));
            Transform world = Read<Transform>(host, "worldRoot"); transforms = CapturePose(world);
            foreach (SimBarCharacterVisual visual in world.GetComponentsInChildren<SimBarCharacterVisual>(true)) objects.Add(new ObjectState(visual, false));
            foreach (SimBarGuitarPerformance guitar in world.GetComponentsInChildren<SimBarGuitarPerformance>(true))
            {
                objects.Add(new ObjectState(guitar, true));
                Mesh strap = Read<Mesh>(guitar, "strapMesh"); if (strap != null) meshes.Add(new MeshState(strap));
            }
            foreach (Light light in world.GetComponentsInChildren<Light>(true)) lights.Add(new LightState(light));
            foreach (Renderer beam in Read<List<Renderer>>(host, "stageBeams")) if (beam != null) beams.Add(new BeamState(beam));
            foreach (string key in new[] { SimBarExperience.OperationsSaveKey, SimBarLocalization.LanguageKey }) preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            hadSize = PlayerPrefs.HasKey(SimBarReadability.TextSizeKey); textSize = PlayerPrefs.GetInt(SimBarReadability.TextSizeKey);
        }
        public bool PreferencesMatch()
        {
            foreach (var preference in preferences)
                if (PlayerPrefs.HasKey(preference.Key) != (preference.Value != null) || (preference.Value != null && PlayerPrefs.GetString(preference.Key) != preference.Value)) return false;
            return hadSize == PlayerPrefs.HasKey(SimBarReadability.TextSizeKey) && (!hadSize || PlayerPrefs.GetInt(SimBarReadability.TextSizeKey) == textSize);
        }
        public void Restore(SimBarExperience host)
        {
            hostFields.Restore();
            JsonUtility.FromJsonOverwrite(journeyJson, journey);
            foreach (ObjectState state in objects) state.Restore();
            RestoreList(actors, actorItems); RestoreList(occupancy, occupancyItems);
            object liveQueue = Read<object>(host, "orderQueue"); Call(liveQueue, "Clear"); foreach (object person in queue) Call(liveQueue, "Enqueue", person);
            foreach (TransformState state in transforms) state.Restore();
            foreach (MeshState state in meshes) state.Restore();
            foreach (LightState light in lights) light.Restore();
            foreach (BeamState beam in beams) beam.Restore();
            foreach (var marker in markerMaterials) if (marker.Key != null) marker.Key.sharedMaterials = marker.Value;
            UnityEngine.Random.state = randomState;
            foreach (var preference in preferences) { if (preference.Value == null) PlayerPrefs.DeleteKey(preference.Key); else PlayerPrefs.SetString(preference.Key, preference.Value); }
            if (hadSize) PlayerPrefs.SetInt(SimBarReadability.TextSizeKey, textSize); else PlayerPrefs.DeleteKey(SimBarReadability.TextSizeKey);
            PlayerPrefs.Save();
        }
    }

    private static List<TransformState> CapturePose(Transform root) { var states = new List<TransformState>(); foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) states.Add(new TransformState(child)); return states; }
    private static object[] CopyList(IList list) { var copy = new object[list.Count]; list.CopyTo(copy, 0); return copy; }
    private static void RestoreList(IList list, object[] values) { list.Clear(); foreach (object value in values) list.Add(value); }
    private static bool SamePose(List<TransformState> states) { foreach (TransformState state in states) if (!state.Matches()) return false; return true; }
    private static bool SameRotation(Quaternion a, Quaternion b)
    {
        var first = new Vector4(a.x, a.y, a.z, a.w); var second = new Vector4(b.x, b.y, b.z, b.w);
        return Mathf.Min((first - second).sqrMagnitude, (first + second).sqrMagnitude) < 0.00000001f;
    }
    private static Color BeamTint(Renderer renderer) { var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block.GetColor("_BaseColor"); }
    private static bool ColorClose(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.0001f && Mathf.Abs(a.g - b.g) < 0.0001f && Mathf.Abs(a.b - b.b) < 0.0001f && Mathf.Abs(a.a - b.a) < 0.0001f;
    private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static float ActorSpacing(Actor a, Actor b) => a.IsAirborneSurfer || b.IsAirborneSurfer ? Vector3.Distance(a.root.position, b.root.position) : FlatDistance(a.root.position, b.root.position);
    private static float PointSegmentDistance(Vector3 point, Vector3 first, Vector3 last)
    {
        Vector3 delta = last - first;
        float amount = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(point - first, delta) / delta.sqrMagnitude) : 0f;
        return Vector3.Distance(point, first + delta * amount);
    }
    private static Array QueueContents(SimBarExperience host) => (Array)Call(Read<object>(host, "orderQueue"), "ToArray");
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name, Fields).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
    private static void SetPhase(SimBarExperience host, string phase) { FieldInfo field = host.GetType().GetField("journeyPhase", Fields); field.SetValue(host, Enum.Parse(field.FieldType, phase)); }
}
