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
            CheckRouteRegressions(report, host, actors);
            Call(host, "TickVenueMotion", 1f / 30f);
            CheckManualHold(report, host, actors);
            CheckTravel(report, host, actors);
            report.Check(gameplay.Matches(host), "Motion alone preserves queue identity/order, drinks, patience, economy, timers and career state.");
            CheckFrozen(report, host, "paused");
            CheckFrozen(report, host, "lobby");
            CheckFrozen(report, host, "management");
            CheckBandPresentation(report, Read<Transform>(host, "worldRoot"));
            report.Check(snapshot.PreferencesMatch(), "Movement and presentation never rewrite gameplay or interface preferences.");
            report.measurements.Add("Simulated 40 seconds of travel plus a two-second manual-pour hold in 1/30-second motion steps, without Update, economy or journey ticks. All live transforms, motion state, presentation state and preferences restored.");
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
        public float distance;
        public Vector3 previous;
        public string Kind => Convert.ToString(Read<object>(state, "kind"));
        public bool IsWorker => Kind == "BarStaff" || Kind == "FloorStaff";
        public bool IsMobile => Kind == "Guest" || IsWorker;
        public bool IsCollidable => Kind != "Band";
    }

    private static List<Actor> CollectActors(SimBarExperience host)
    {
        var actors = new List<Actor>();
        foreach (object state in Read<IEnumerable>(host, "motionActors"))
        {
            object person = Read<object>(state, "person");
            Transform root = Read<Transform>(person, "root");
            if (root == null) continue;
            actors.Add(new Actor { state = state, person = person, root = root, visual = root.GetComponent<SimBarCharacterVisual>(), previous = root.position });
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
        float maximumSpeed = 0f;
        int blockedSamples = 0, newOverlaps = 0, markerFailures = 0;
        string firstBlocked = "", firstOverlap = "";
        float beginning = Read<float>(host, "motionClock");
        for (int frame = 0; frame < 1200; frame++)
        {
            Call(host, "TickVenueMotion", step);
            foreach (Actor actor in actors)
            {
                if (actor.root == null || !actor.root.gameObject.activeInHierarchy) continue;
                float moved = FlatDistance(actor.previous, actor.root.position);
                actor.distance += moved;
                if (actor.IsMobile)
                {
                    maximumSpeed = Mathf.Max(maximumSpeed, moved / step);
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
                float distance = FlatDistance(pair.a.root.position, pair.b.root.position);
                pair.minimum = Mathf.Min(pair.minimum, distance); pair.best = Mathf.Max(pair.best, distance);
                if (pair.initial >= 0.4f && distance < 0.395f)
                {
                    newOverlaps++;
                    if (firstOverlap.Length == 0) firstOverlap = pair.a.root.name + " / " + pair.b.root.name + " = " + distance.ToString("F3");
                }
            }
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
        report.Check(Mathf.Abs(Read<float>(host, "motionClock") - beginning - 40f) < 0.02f, "Only the managed motion clock advances by the simulated 40 seconds.");
        report.Check(guestsMoved >= 3, "At least three guests travel along their routes.");
        report.Check(staffMoved >= 1, "At least one bartender/server travels to a work or order position.");
        report.Check(maximumSpeed <= 1.2f + 0.015f, "Every moving NPC stays within 1.2 m/s; peak=" + maximumSpeed.ToString("F3") + ".");
        report.Check(blockedSamples == 0, "Moving NPC samples stay outside static obstacles. " + firstBlocked);
        report.Check(newOverlaps == 0, "Movement does not introduce NPC separations below 0.4 m. " + firstOverlap);
        report.Check(inheritedDidNotWorsen && improvedOverlaps == inheritedOverlaps, "Initial overlaps do not worsen and improve rather than deadlock.");
        report.Check(markerFailures == 0, "Visible order markers follow their moving guest roots.");
        report.measurements.Add("Moving guests=" + guestsMoved + ", staff=" + staffMoved + "; longest path=" + longest.ToString("F2")
            + " m; peak speed=" + maximumSpeed.ToString("F3") + " m/s; initial overlaps=" + inheritedOverlaps + ", improved=" + improvedOverlaps + ".");
    }

    private sealed class Pair
    {
        public Actor a, b;
        public float initial, minimum, best;
        public Pair(Actor a, Actor b) { this.a = a; this.b = b; initial = minimum = best = FlatDistance(a.root.position, b.root.position); }
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
        float clock = Read<float>(host, "motionClock");
        if (context == "lobby") { SetPhase(host, "Lobby"); Write(host, "paused", false); }
        else { Write(host, "paused", true); Write(host, "opMenuOpen", context == "management"); }
        try
        {
            Call(host, "TickVenueMotion", 1f);
            report.Check(Read<float>(host, "motionClock") == clock && SamePose(pose), context + " freezes routes, bones and held props on the same presentation clock.");
        }
        finally { Write(host, "paused", paused); Write(host, "opMenuOpen", managing); Write(host, "journeyPhase", phase); }
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

    private sealed class Snapshot
    {
        private readonly ObjectState hostFields;
        private readonly List<ObjectState> objects = new List<ObjectState>();
        private readonly List<TransformState> transforms;
        private readonly List<MeshState> meshes = new List<MeshState>();
        private readonly List<KeyValuePair<Light, float>> lights = new List<KeyValuePair<Light, float>>();
        private readonly Array queue;
        private readonly object journey;
        private readonly string journeyJson;
        private readonly Dictionary<string, string> preferences = new Dictionary<string, string>();
        private readonly bool hadSize;
        private readonly int textSize;
        private readonly IList actors, occupancy;
        private readonly object[] actorItems, occupancyItems;
        public Snapshot(SimBarExperience host)
        {
            hostFields = new ObjectState(host, false);
            journey = Read<object>(host, "journey"); journeyJson = (string)Call(host, "ExportJourneyState"); queue = QueueContents(host);
            actors = Read<IList>(host, "motionActors"); actorItems = CopyList(actors);
            occupancy = Read<IList>(host, "motionOccupancy"); occupancyItems = CopyList(occupancy);
            foreach (object actor in Read<IEnumerable>(host, "motionActors")) objects.Add(new ObjectState(actor, true));
            objects.Add(new ObjectState(Read<object>(host, "motionNavigation"), true));
            foreach (object person in Read<IEnumerable>(host, "patrons")) objects.Add(new ObjectState(person, false));
            Transform world = Read<Transform>(host, "worldRoot"); transforms = CapturePose(world);
            foreach (SimBarCharacterVisual visual in world.GetComponentsInChildren<SimBarCharacterVisual>(true)) objects.Add(new ObjectState(visual, false));
            foreach (SimBarGuitarPerformance guitar in world.GetComponentsInChildren<SimBarGuitarPerformance>(true))
            {
                objects.Add(new ObjectState(guitar, true));
                Mesh strap = Read<Mesh>(guitar, "strapMesh"); if (strap != null) meshes.Add(new MeshState(strap));
            }
            foreach (Light light in world.GetComponentsInChildren<Light>(true)) lights.Add(new KeyValuePair<Light, float>(light, light.intensity));
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
            foreach (var light in lights) if (light.Key != null) light.Key.intensity = light.Value;
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
    private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static Array QueueContents(SimBarExperience host) => (Array)Call(Read<object>(host, "orderQueue"), "ToArray");
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    private static void Write(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
    private static void SetPhase(SimBarExperience host, string phase) { FieldInfo field = host.GetType().GetField("journeyPhase", Fields); field.SetValue(host, Enum.Parse(field.FieldType, phase)); }
}
