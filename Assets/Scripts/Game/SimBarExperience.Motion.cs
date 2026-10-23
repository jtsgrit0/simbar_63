using System.Collections.Generic;
using UnityEngine;

public sealed partial class SimBarExperience
{
    private enum MotionKind { Guest, BarStaff, FloorStaff, Stationary, Band }
    private enum MotionState { Idle, Wandering, Resting, ToOrder, Waiting, Returning, Working }
    private sealed class MotionActor
    {
        public Patron person;
        public SimBarCharacterVisual visual;
        public MotionKind kind;
        public MotionState state;
        public Vector3 home, target;
        public readonly List<Vector3> path = new List<Vector3>(40);
        public int pathIndex, queueIndex = -1, cycle, index, pitSlot = -1;
        public float wait, speed, stuck, workMin, workMax, celebration;
        public bool moving, routeFailed, pitActive;
    }
    private readonly List<MotionActor> motionActors = new List<MotionActor>(48);
    private readonly Dictionary<Patron, MotionActor> motionLookup = new Dictionary<Patron, MotionActor>();
    private readonly List<Vector3> motionOccupancy = new List<Vector3>(48);
    private SimBarVenueNavigation motionNavigation;
    private bool motionReady, motionOrdersDirty, motionReferenceLayout;
    private float motionClock, motionServicePulse;
    private static readonly Vector3[] MotionOrderSlots = {
        new Vector3(-1.50f, 0, -1.75f), new Vector3(-.85f, 0, -2.25f),
        new Vector3(-.15f, 0, -2.70f), new Vector3(.6f, 0, -3.1f),
        new Vector3(1.4f, 0, -3.5f), new Vector3(2.2f, 0, -3.7f), new Vector3(3f, 0, -4f)
    };

    public float VenueMotionSeconds => motionClock;
    public int VenueMovingGuests { get; private set; }
    public int VenueMovingStaff { get; private set; }

    private void RegisterVenuePerson(Patron person, string role)
    {
        if (person == null || person.root == null) return;
        var actor = new MotionActor { person = person, visual = person.root.GetComponent<SimBarCharacterVisual>(),
            home = worldRoot.InverseTransformPoint(person.root.position), index = motionActors.Count };
        actor.target = actor.home;
        actor.kind = role == "singer" || role == "guitarist" || role == "bassist" || role == "drummer" ? MotionKind.Band
            : role == "bartender" ? MotionKind.BarStaff : person.name.StartsWith("Hired ") ? MotionKind.FloorStaff
            : role == "guest" && person.isDancing ? MotionKind.Guest : MotionKind.Stationary;
        actor.wait = 2f + actor.index % 8 * .8f;
        if (actor.kind == MotionKind.BarStaff)
        {
            actor.workMin = person.name == "Bartender" ? -9.1f : person.name == "Server" ? -4.9f : -6.3f;
            actor.workMax = person.name == "Bartender" ? -6.9f : person.name == "Server" ? -2.7f : -5.5f;
        }
        if (actor.visual != null) { actor.visual.SetManaged(true); actor.visual.ApplyPresentation(motionClock, performance, 0, 0); }
        motionActors.Add(actor); motionLookup.Add(person, actor);
        motionOrdersDirty = true;
    }

    private void InitializeVenueMotion()
    {
        motionNavigation = new SimBarVenueNavigation();
        motionReady = true; motionOrdersDirty = true;
        ApplyStageLighting();
    }

    // Movement never enqueues, serves or removes an order. Only the existing game loop owns those actions.
    private void MotionOrderChanged(Patron person)
    {
        motionOrdersDirty = true;
    }

    private void MotionServed(Patron person, bool automatic, float quality)
    {
        motionServicePulse = automatic ? 1.6f : quality >= .9f ? 3.2f : 1.8f;
        if (person != null && motionLookup.TryGetValue(person, out MotionActor actor)) actor.celebration = 2.5f;
        motionOrdersDirty = true;
    }

    private Vector3 MotionPosition(MotionActor actor) => worldRoot.InverseTransformPoint(actor.person.root.position);
    private bool IsMotionPointWalkable(Vector3 point) => motionNavigation != null && motionNavigation.IsWalkable(point);

    private void TickVenueMotion(float delta)
    {
        if (!motionReady || paused || !JourneyIsPlaying || opMenuOpen || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        delta = Mathf.Min(delta, .1f);
        motionClock += delta; motionServicePulse = Mathf.Max(0, motionServicePulse - delta);
        motionOccupancy.Clear();
        foreach (MotionActor actor in motionActors)
            if (actor.person.root != null && actor.person.root.gameObject.activeInHierarchy && actor.kind != MotionKind.Band)
                motionOccupancy.Add(MotionPosition(actor));
        if (motionOrdersDirty) RefreshMotionOrders();
        int walking = 0;
        foreach (MotionActor actor in motionActors) if (actor.moving) walking++;
        VenueMovingGuests = VenueMovingStaff = VenueCrowdParticipants = 0;
        foreach (MotionActor actor in motionActors)
        {
            if (actor.person.root == null || !actor.person.root.gameObject.activeInHierarchy) continue;
            Vector3 previous = MotionPosition(actor);
            actor.celebration = Mathf.Max(0, actor.celebration - delta);
            if (actor.visual != null) actor.visual.SetCrowdPerformance(SimBarCrowdAction.None, 0f);
            actor.pitActive = false;
            bool held = IsPourInProgress && ReferenceEquals(pourPatron, actor.person);
            bool concert = !held && TryTickConcertCrowd(actor, delta);
            if (concert) VenueCrowdParticipants++;
            if (!held && !concert)
            {
                if (actor.kind == MotionKind.BarStaff && motionReferenceLayout) TickBarStaff(actor);
                else if ((actor.kind == MotionKind.Guest || actor.kind == MotionKind.FloorStaff) && actor.queueIndex < 0
                    && actor.pathIndex >= actor.path.Count && actor.state != MotionState.Returning && !actor.routeFailed)
                {
                    actor.wait -= delta;
                    if (actor.wait <= 0 && walking < 8) { ChooseMotionWalk(actor); walking++; }
                }
                if (actor.kind == MotionKind.Guest || actor.kind == MotionKind.FloorStaff || actor.kind == MotionKind.BarStaff)
                    StepMotionActor(actor, delta);
            }
            actor.speed = Vector3.Distance(previous, MotionPosition(actor)) / delta;
            actor.moving = actor.speed > .03f;
            if (actor.moving && actor.kind == MotionKind.Guest) VenueMovingGuests++;
            if (actor.moving && (actor.kind == MotionKind.BarStaff || actor.kind == MotionKind.FloorStaff)) VenueMovingStaff++;
            float serving = actor.kind == MotionKind.BarStaff && !actor.moving
                ? Mathf.Max(IsPourInProgress ? .95f : orderQueue.Count > 0 ? .45f : .1f, motionServicePulse > 0 ? .9f : 0) : 0;
            float energy = Mathf.Clamp01(performance * .8f + .15f + (JourneyFeverSeconds > 0 ? .2f : 0) + (motionServicePulse > 0 ? .15f : 0));
            if (actor.visual != null) actor.visual.ApplyPresentation(motionClock, energy, concert ? 0f : actor.speed, serving);
            else PoseFallbackMotion(actor);
            if (actor.person.orderMarker != null && actor.person.orderMarker.activeSelf)
                actor.person.orderMarker.transform.localPosition = new Vector3(0, 2.48f + Mathf.Abs(Mathf.Sin(motionClock * 4f + actor.index)) * .12f, 0);
        }
        ApplyStageLighting();
    }

    private void RefreshMotionOrders()
    {
        motionOrdersDirty = false;
        foreach (MotionActor actor in motionActors)
        {
            if (actor.kind != MotionKind.Guest) continue;
            int slot = -1, i = 0;
            foreach (Patron patron in orderQueue) { if (ReferenceEquals(patron, actor.person)) { slot = i; break; } i++; }
            if (IsPourInProgress && ReferenceEquals(pourPatron, actor.person)) continue;
            if (slot >= 0 && (actor.queueIndex != slot || actor.state != MotionState.ToOrder && actor.state != MotionState.Waiting))
                SetMotionPath(actor, MotionOrderSlots[Mathf.Min(slot, MotionOrderSlots.Length - 1)], MotionState.ToOrder);
            else if (slot < 0 && actor.queueIndex >= 0) SetMotionPath(actor, actor.home, MotionState.Returning);
            actor.queueIndex = slot;
        }
    }

    private void SetMotionPath(MotionActor actor, Vector3 target, MotionState state)
    {
        actor.target = target; actor.state = state; actor.pathIndex = 0; actor.stuck = 0;
        actor.routeFailed = !(actor.pitSlot >= 0
            ? motionNavigation.FindPath(MotionPosition(actor), target, actor.path, motionOccupancy)
            : motionNavigation.FindAislePath(MotionPosition(actor), target, actor.path, motionOccupancy));
        if (actor.routeFailed)
        { actor.path.Clear(); actor.wait = 1.5f; }
    }

    private void ChooseMotionWalk(MotionActor actor)
    {
        actor.cycle++;
        if (actor.state == MotionState.Resting)
        { SetMotionPath(actor, actor.home, MotionState.Returning); return; }
        float angle = actor.index * 2.399f + actor.cycle * 1.7f;
        Vector3 target = actor.home + new Vector3(Mathf.Cos(angle) * 1.8f, 0, Mathf.Sin(angle) * 1.5f);
        if (!motionNavigation.IsWalkable(target) || actor.pitSlot < 0 && CrowdFloorReserved(target)
            || MotionOccupied(target, actor, .65f)) { actor.wait = 1.4f; return; }
        SetMotionPath(actor, target, MotionState.Wandering);
    }

    private void TickBarStaff(MotionActor actor)
    {
        float cycle = Mathf.Repeat(motionClock + actor.index * 1.9f, 9f);
        float x = cycle < 4.5f ? actor.workMin + .15f : actor.workMax - .15f;
        if (IsPourInProgress && actor.person.name == "Server") x = actor.workMax - .1f;
        if (IsPourInProgress && actor.person.name == "Bartender") x = Mathf.Lerp(actor.workMin + .1f, actor.workMax - .1f, pourDrinkIndex / 2f);
        Vector3 target = new Vector3(x, actor.home.y, -.565f);
        if (actor.state == MotionState.Working && (actor.target - target).sqrMagnitude < .01f) return;
        actor.target = target; actor.state = MotionState.Working; actor.path.Clear(); actor.path.Add(target); actor.pathIndex = 0;
    }

    private void StepMotionActor(MotionActor actor, float delta)
    {
        Vector3 p = MotionPosition(actor);
        if (actor.routeFailed)
        {
            actor.wait -= delta;
            if (actor.wait <= 0) SetMotionPath(actor, actor.target, actor.state);
            return;
        }
        while (actor.pathIndex < actor.path.Count && FlatDistance(p, actor.path[actor.pathIndex]) < .09f) actor.pathIndex++;
        if (actor.pathIndex >= actor.path.Count)
        {
            actor.moving = false;
            if (actor.state == MotionState.ToOrder) actor.state = MotionState.Waiting;
            if (actor.state == MotionState.Wandering) { actor.state = MotionState.Resting; actor.wait = 2f + actor.index % 3; }
            if (actor.state == MotionState.Returning) { actor.state = MotionState.Idle; actor.wait = 3f + actor.index % 7; }
            float facing = actor.queueIndex >= 0 ? 270f : actor.kind == MotionKind.BarStaff ? 180f : actor.person.baseYaw + Mathf.Sin(motionClock * .35f + actor.person.phase) * 3f;
            actor.person.root.rotation = Quaternion.RotateTowards(actor.person.root.rotation, worldRoot.rotation * Quaternion.Euler(0, facing, 0), 160f * delta);
            return;
        }
        Vector3 direction = actor.path[actor.pathIndex] - p; direction.y = 0;
        float speed = actor.kind == MotionKind.BarStaff ? .66f : actor.kind == MotionKind.FloorStaff ? 1.1f : .88f + actor.index % 4 * .06f;
        Vector3 step = Vector3.ClampMagnitude(direction, speed * delta);
        Vector3 candidate = p + step;
        if (!MotionStepAllowed(actor, p, candidate))
        {
            Vector3 side = new Vector3(-direction.z, 0, direction.x).normalized * speed * delta * .85f;
            candidate = p + side + step * .35f;
            if (actor.kind == MotionKind.BarStaff || !MotionStepAllowed(actor, p, candidate))
            {
                candidate = p - side + step * .35f;
                if (actor.kind == MotionKind.BarStaff || !MotionStepAllowed(actor, p, candidate)) candidate = p;
            }
        }
        if ((candidate - p).sqrMagnitude < .000001f)
        {
            actor.stuck += delta;
            if (actor.stuck > 1.5f && actor.kind != MotionKind.BarStaff) SetMotionPath(actor, actor.target, actor.state);
            return;
        }
        actor.stuck = 0;
        candidate.y = actor.home.y;
        actor.person.root.position = worldRoot.TransformPoint(candidate);
        Vector3 heading = candidate - p; heading.y = 0;
        if (heading.sqrMagnitude > .000001f)
            actor.person.root.rotation = Quaternion.RotateTowards(actor.person.root.rotation, worldRoot.rotation * Quaternion.LookRotation(heading), 240f * delta);
    }

    private bool MotionStepAllowed(MotionActor actor, Vector3 current, Vector3 next)
    {
        if (actor.kind == MotionKind.BarStaff)
        { if (next.x < actor.workMin || next.x > actor.workMax || Mathf.Abs(next.z + .565f) > .02f) return false; }
        else if (!motionNavigation.SegmentClear(current, next)) return false;
        if (actor.pitSlot < 0 && CrowdFloorReserved(next)) return false;
        foreach (MotionActor other in motionActors)
        {
            if (other == actor || other.kind == MotionKind.Band || other.person.root == null || !other.person.root.gameObject.activeInHierarchy) continue;
            Vector3 position = MotionPosition(other);
            if (Mathf.Abs(next.y - position.y) > 1.1f) continue;
            float nextDistance = FlatDistance(next, position);
            if (nextDistance < .46f && nextDistance < FlatDistance(current, position) + .00001f) return false;
        }
        return true;
    }

    private bool MotionOccupied(Vector3 point, MotionActor self, float distance)
    {
        foreach (MotionActor other in motionActors)
            if (other != self && other.person.root != null && other.person.root.gameObject.activeInHierarchy && FlatDistance(point, MotionPosition(other)) < distance) return true;
        return false;
    }
    private static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    private void PoseFallbackMotion(MotionActor actor)
    {
        float wave = Mathf.Sin(motionClock * (actor.moving ? 7f : .8f) + actor.index);
        if (actor.person.leftArm != null) actor.person.leftArm.localRotation = Quaternion.Euler(actor.moving ? wave * 30f : 0, 0, -12f - (actor.moving ? 0 : wave * 4f));
        if (actor.person.rightArm != null) actor.person.rightArm.localRotation = Quaternion.Euler(actor.moving ? -wave * 30f : 0, 0, 12f + (actor.moving ? 0 : wave * 4f));
    }
}
