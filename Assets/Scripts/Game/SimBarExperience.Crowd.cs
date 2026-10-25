using UnityEngine;

public sealed partial class SimBarExperience
{
    private static readonly Vector3 ConcertPitCenter = new Vector3(5.75f, 0f, -1.25f);
    private const float ConcertMoshRadius = 1.58f;
    private const float SlamBodyRadius = .42f;
    private const float SlamRestHalfDistance = .68f;
    private readonly MotionActor[] concertActors = new MotionActor[12];
    private bool slamPairPrepared;
    private float slamVelocity = -.9f, slamImpact, slamRecoil, slamRest;
    public int VenueCrowdParticipants { get; private set; }
    public int VenueSlamContacts { get; private set; }
    public float CrowdSurfWeight => ConcertSurfAmount(motionClock);

    internal static bool CrowdFloorReserved(Vector3 point)
    {
        Vector3 offset = point - ConcertPitCenter; offset.y = 0f;
        return offset.sqrMagnitude < 2.05f * 2.05f
            || point.x > 4.3f && point.x < 7.8f && point.z > -5.25f && point.z < -3.15f;
    }

    private static Vector3 ConcertHome(int slot)
    {
        if (slot < 6)
        {
            float angle = slot * Mathf.PI / 3f;
            return ConcertPitCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ConcertMoshRadius;
        }
        if (slot < 8) return ConcertPitCenter + Vector3.right * (slot == 6 ? -.68f : .68f);
        Vector3 center = new Vector3(6f, 0f, -4.25f);
        return center + (slot == 8 ? Vector3.left * .65f : slot == 9 ? Vector3.right * .65f
            : slot == 10 ? Vector3.back * .65f : Vector3.zero);
    }

    private void AssignConcertSlot(Patron person, int slot)
    {
        if (!motionLookup.TryGetValue(person, out MotionActor actor)) return;
        actor.pitSlot = slot; actor.kind = MotionKind.Guest;
        concertActors[slot] = actor;
    }

    private static float ConcertSurfAmount(float time)
    {
        float cycle = Mathf.Repeat(time, 24f);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 4f, cycle))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 18f, cycle)));
    }

    private bool ConcertActorAvailable(MotionActor actor)
    {
        return actor != null && actor.person.root != null && actor.person.root.gameObject.activeInHierarchy
            && actor.queueIndex < 0 && actor.person.drinkIndex < 0
            && !(IsPourInProgress && ReferenceEquals(pourPatron, actor.person))
            && (actor.state != MotionState.Returning || FlatDistance(MotionPosition(actor), actor.home) <= .18f);
    }

    // These two kinematic bodies advance together. A swept body contact stops the inward
    // velocity and applies an equal outward impulse; the animation never drives their roots.
    private void TickConcertSlam(float delta)
    {
        slamPairPrepared = false;
        slamImpact *= Mathf.Exp(-12f * delta);
        slamRecoil = Mathf.MoveTowards(slamRecoil, slamVelocity > 0f ? 1f : 0f, delta * 6f);
        slamRest = Mathf.Max(0f, slamRest - delta);
        MotionActor left = concertActors[6], right = concertActors[7];
        if (!ConcertActorAvailable(left) || !ConcertActorAvailable(right))
        { slamVelocity = -.9f; slamImpact = slamRecoil = slamRest = 0f; return; }
        Vector3 a = MotionPosition(left), b = MotionPosition(right);
        // Returning guests first recover their own side; no snapping through their partner.
        if (a.x >= ConcertPitCenter.x || b.x <= ConcertPitCenter.x
            || Mathf.Abs(a.z - ConcertPitCenter.z) > .06f || Mathf.Abs(b.z - ConcertPitCenter.z) > .06f) return;
        slamPairPrepared = true;
        if (Quaternion.Angle(left.person.root.rotation, worldRoot.rotation * Quaternion.Euler(0, 90f, 0)) > 12f
            || Quaternion.Angle(right.person.root.rotation, worldRoot.rotation * Quaternion.Euler(0, 270f, 0)) > 12f) return;
        if (slamRest > 0f) return;
        if (Mathf.Abs(slamVelocity) < .01f) slamVelocity = -.9f;
        float movement = slamVelocity * delta;
        Vector3 nextA = a - Vector3.right * movement, nextB = b + Vector3.right * movement;
        nextA.z = Mathf.MoveTowards(a.z, ConcertPitCenter.z, delta * .6f);
        nextB.z = Mathf.MoveTowards(b.z, ConcertPitCenter.z, delta * .6f);
        bool contact = slamVelocity < 0f && nextB.x - nextA.x <= SlamBodyRadius * 2f;
        if (contact)
        {
            float allowed = Mathf.Max(0f, b.x - a.x - SlamBodyRadius * 2f) * .5f;
            nextA.x = a.x + allowed; nextB.x = b.x - allowed;
        }
        bool rested = false;
        if (slamVelocity > 0f)
        {
            nextA.x = Mathf.Max(nextA.x, ConcertPitCenter.x - SlamRestHalfDistance);
            nextB.x = Mathf.Min(nextB.x, ConcertPitCenter.x + SlamRestHalfDistance);
            rested = nextA.x <= ConcertPitCenter.x - SlamRestHalfDistance + .0001f
                && nextB.x >= ConcertPitCenter.x + SlamRestHalfDistance - .0001f;
        }
        if (!MotionStepAllowed(left, a, nextA) || !MotionStepAllowed(right, b, nextB)) return;
        left.person.root.position = worldRoot.TransformPoint(nextA);
        right.person.root.position = worldRoot.TransformPoint(nextB);
        if (contact) { slamVelocity = .9f; slamImpact = 1f; VenueSlamContacts++; }
        else if (rested) { slamVelocity = 0f; slamRest = .28f; }
    }

    private bool TryTickConcertCrowd(MotionActor actor, float delta)
    {
        actor.pitActive = false;
        if (actor.pitSlot < 0 || actor.queueIndex >= 0 || actor.person.drinkIndex >= 0) return false;
        Vector3 position = MotionPosition(actor);
        if (actor.state == MotionState.Returning && FlatDistance(position, actor.home) > .18f) return false;
        int slot = actor.pitSlot;
        Vector3 target;
        float facing;
        float weight = 1f;
        SimBarCrowdAction action;
        if (slot < 6)
        {
            Vector3 radial = position - ConcertPitCenter; radial.y = 0f;
            float radius = radial.magnitude;
            float angle = Mathf.Atan2(radial.z, radial.x);
            float desired = slot * Mathf.PI / 3f + motionClock * .65f;
            // Resume along the rim after yielding/ordering instead of cutting a long
            // chord through the slam pair to catch a target several beats ahead.
            if (radius >= ConcertMoshRadius - .08f)
                angle += Mathf.Min(Mathf.Repeat(desired - angle, Mathf.PI * 2f), 1.28f * delta / Mathf.Max(.5f, radius));
            radius = Mathf.MoveTowards(radius, ConcertMoshRadius, .7f * delta);
            target = ConcertPitCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            facing = Quaternion.LookRotation(tangent).eulerAngles.y;
            action = SimBarCrowdAction.Mosh;
        }
        else if (slot < 8)
        {
            float side = slot == 6 ? -1f : 1f;
            target = slamPairPrepared ? position : ConcertPitCenter + Vector3.right * (side * SlamRestHalfDistance);
            facing = side < 0f ? 90f : 270f;
            action = SimBarCrowdAction.Slam;
        }
        else
        {
            Vector3 carry = new Vector3(6f + Mathf.Sin(motionClock * .22f) * .35f, 0f,
                -4.25f + Mathf.Sin(motionClock * .3f) * .12f);
            weight = CrowdSurfWeight;
            if (slot == 11)
            {
                target = carry + Vector3.up * ((1.85f + Mathf.Sin(motionClock * 4f) * .035f) * weight);
                facing = Mathf.LerpAngle(actor.person.baseYaw, 90f, weight);
                action = SimBarCrowdAction.Surf;
            }
            else
            {
                Vector3 offset = slot == 8 ? Vector3.left * .65f : slot == 9 ? Vector3.right * .65f : Vector3.back * .65f;
                target = carry + offset;
                facing = Quaternion.LookRotation(-offset).eulerAngles.y;
                action = SimBarCrowdAction.Support;
                Vector3 contact = carry + new Vector3(slot == 8 ? -.43f : slot == 9 ? .43f : 0f,
                    (1.77f + Mathf.Sin(motionClock * 4f) * .035f) * weight, slot == 10 ? -.13f : 0f);
                if (actor.visual != null) actor.visual.SetCrowdSupportTarget(worldRoot.TransformPoint(contact));
            }
        }
        Vector3 next = Vector3.MoveTowards(position, target, 1.35f * delta);
        if (MotionStepAllowed(actor, position, next)) actor.person.root.position = worldRoot.TransformPoint(next);
        actor.person.root.rotation = Quaternion.RotateTowards(actor.person.root.rotation,
            worldRoot.rotation * Quaternion.Euler(0f, facing, 0f), 220f * delta);
        actor.state = MotionState.Idle; actor.path.Clear(); actor.pathIndex = 0; actor.routeFailed = false;
        actor.pitActive = true;
        if (actor.visual != null)
        {
            actor.visual.SetCrowdPerformance(action, weight);
            if (action == SimBarCrowdAction.Slam)
                actor.visual.SetCrowdSlamContact(slamPairPrepared ? slamImpact : 0f,
                    slamPairPrepared ? slamRecoil : 0f,
                    worldRoot.TransformPoint(ConcertPitCenter + Vector3.up * 1.2f));
        }
        return true;
    }
}
