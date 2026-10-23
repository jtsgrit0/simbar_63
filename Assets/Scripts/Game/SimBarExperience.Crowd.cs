using UnityEngine;

public sealed partial class SimBarExperience
{
    private static readonly Vector3 ConcertPitCenter = new Vector3(5.75f, 0f, -1.25f);
    private readonly MotionActor[] concertActors = new MotionActor[12];
    public int VenueCrowdParticipants { get; private set; }
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
            return ConcertPitCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.4f;
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
            float angle = slot * Mathf.PI / 3f + motionClock * .65f;
            target = ConcertPitCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.4f;
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            facing = Quaternion.LookRotation(tangent).eulerAngles.y;
            action = SimBarCrowdAction.Mosh;
        }
        else if (slot < 8)
        {
            float side = slot == 6 ? -1f : 1f;
            float recoil = .52f - .16f * Mathf.Sin(motionClock * Mathf.PI * 2f);
            target = ConcertPitCenter + new Vector3(side * recoil, 0f, Mathf.Sin(motionClock * 2f) * .07f);
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
        if (actor.visual != null) actor.visual.SetCrowdPerformance(action, weight);
        return true;
    }
}
