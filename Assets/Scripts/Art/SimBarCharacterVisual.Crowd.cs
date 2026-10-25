using UnityEngine;

/// <summary>Stage-front actions selected by gameplay; other guests keep their unhurried poses.</summary>
public enum SimBarCrowdAction
{
    None,
    Mosh,
    Slam,
    Support,
    Surf
}

public sealed partial class SimBarCharacterVisual
{
    public SimBarCrowdAction CrowdAction { get; private set; }
    public float CrowdWeight { get; private set; }
    /// <summary>Normalized, shared 120 BPM beat from the host gameplay clock.</summary>
    public float CrowdPhase { get; private set; }
    public float CrowdSlamImpact { get; private set; }
    public float CrowdSlamRecoil { get; private set; }
    public bool HasCrowdSupportTarget => hasCrowdSupportTarget;
    /// <summary>World-space midpoint requested for the two supporting wrists.</summary>
    public Vector3 CrowdSupportTarget => crowdSupportTarget;
    public Transform CrowdHipAnchor
    {
        get
        {
            if (bones.TryGetValue("hips", out var hips)) return hips;
            return bones.TryGetValue("upperlegl", out var leg) ? leg : null;
        }
    }

    private struct CrowdArm
    {
        public Transform upper;
        public Transform lower;
        public Transform hand;
        public Transform middle;
        public Transform index;
    }

    private bool crowdBonesCached;
    private CrowdArm crowdLeftArm;
    private CrowdArm crowdRightArm;
    private bool hasCrowdSupportTarget;
    private Vector3 crowdSupportTarget;
    private Vector3 crowdSlamContactPoint;
    private bool hasCrowdSlamContact;

    /// <summary>
    /// Set before ApplyPresentation. Weight is immediate: gameplay owns transitions.
    /// None/zero restores the ordinary guest pose on the next presentation tick.
    /// The actor root remains exclusively under gameplay's control.
    /// </summary>
    public void SetCrowdPerformance(SimBarCrowdAction action, float weight = 1f)
    {
        if ((int)action < (int)SimBarCrowdAction.None || (int)action > (int)SimBarCrowdAction.Surf)
            action = SimBarCrowdAction.None;
        CrowdAction = action;
        CrowdWeight = action == SimBarCrowdAction.None || !FinitePresentation(weight) ? 0f : Mathf.Clamp01(weight);
        if (action != SimBarCrowdAction.Support) hasCrowdSupportTarget = false;
        if (action != SimBarCrowdAction.Slam)
        { hasCrowdSlamContact = false; CrowdSlamImpact = CrowdSlamRecoil = 0f; }
    }

    /// <summary>The host's physical contact, not the beat, triggers the bracing/recoil pose.</summary>
    public void SetCrowdSlamContact(float impact, float recoil, Vector3 worldContactPoint)
    {
        CrowdSlamImpact = FinitePresentation(impact) ? Mathf.Clamp01(impact) : 0f;
        CrowdSlamRecoil = FinitePresentation(recoil) ? Mathf.Clamp01(recoil) : 0f;
        hasCrowdSlamContact = FinitePresentation(worldContactPoint.x)
            && FinitePresentation(worldContactPoint.y) && FinitePresentation(worldContactPoint.z);
        if (hasCrowdSlamContact) crowdSlamContactPoint = worldContactPoint;
    }

    /// <summary>Optional world contact; wrists aim 12 cm either side, within the rig's reach.</summary>
    public void SetCrowdSupportTarget(Vector3 worldPoint)
    {
        hasCrowdSupportTarget = FinitePresentation(worldPoint.x) && FinitePresentation(worldPoint.y) && FinitePresentation(worldPoint.z);
        if (hasCrowdSupportTarget) crowdSupportTarget = worldPoint;
    }

    public void ClearCrowdSupportTarget() => hasCrowdSupportTarget = false;

    private void ApplyCrowdPresentation(float timeSeconds, float energy)
    {
        CrowdPhase = Mathf.Repeat(timeSeconds * 2f, 1f);
        if (CrowdAction == SimBarCrowdAction.None || CrowdWeight <= 0.001f) return;
        CacheCrowdBones();
        float beat = timeSeconds * Mathf.PI * 4f;
        if (CrowdAction == SimBarCrowdAction.Mosh) ApplyMoshPresentation(beat, energy, false);
        else if (CrowdAction == SimBarCrowdAction.Slam) ApplySlamPresentation(beat, energy);
        else if (CrowdAction == SimBarCrowdAction.Support) ApplyCrowdSupportPresentation(beat, energy);
        else if (CrowdAction == SimBarCrowdAction.Surf) ApplyCrowdSurfPresentation(beat, energy);
    }

    private void ApplySlamPresentation(float beat, float energy)
    {
        float weight = CrowdWeight, impact = CrowdSlamImpact, recoil = CrowdSlamRecoil * (1f - impact);
        float strength = Mathf.Lerp(.75f, 1f, energy);
        // The old deep forward hinge let the skeleton pass through its partner even
        // when the feet were separated. Keep the body inside its 42 cm contact radius.
        BlendCrowdPivot(new Vector3(0f, -.045f - impact * .035f, -.055f * recoil),
            new Vector3((6f - recoil * 11f - impact * 2f) * strength, 0f, Mathf.Sin(beat * .5f) * 1.4f), weight);
        AimPresentation("upperlegl", "lowerlegl", new Vector3(-.24f, -1f, .18f + impact * .18f), weight);
        AimPresentation("upperlegr", "lowerlegr", new Vector3(.24f, -1f, .18f + impact * .18f), weight);
        AimPresentation("lowerlegl", "footl", new Vector3(.025f, -1f, -.24f - impact * .16f), weight);
        AimPresentation("lowerlegr", "footr", new Vector3(-.025f, -1f, -.24f - impact * .16f), weight);
        RotatePresentationBone("torso", (2f - recoil * 4f) * weight * strength, 0f, 0f);
        RotatePresentationBone("head", (-2f + impact * 7f - recoil * 5f) * weight, 0f, 0f);

        float shoulder = PresentationBonePosition("upperarml", new Vector3(-.18f, 1.4f, 0f)).y;
        float reach = .32f + impact * .04f;
        if (hasCrowdSlamContact)
        {
            // Hands brace on our side of the shared contact plane, including during recoil.
            Vector3 contact = pivot.InverseTransformPoint(crowdSlamContactPoint);
            reach = Mathf.Min(reach, Mathf.Max(.12f, contact.z - .075f));
        }
        SolveCrowdArm(crowdLeftArm, new Vector3(-.19f, shoulder - .16f + impact * .08f, reach),
            new Vector3(-.43f, shoulder - .28f, .03f), weight);
        SolveCrowdArm(crowdRightArm, new Vector3(.19f, shoulder - .12f + impact * .08f, reach),
            new Vector3(.43f, shoulder - .24f, .03f), weight);
        PoseCrowdPalm(crowdLeftArm, false, pivot.up, -pivot.forward, weight);
        PoseCrowdPalm(crowdRightArm, true, pivot.up, -pivot.forward, weight);
    }

    private void ApplyMoshPresentation(float beat, float energy, bool slam)
    {
        float weight = CrowdWeight;
        float strength = Mathf.Lerp(0.65f, 1f, energy);
        float side = Mathf.Sin(beat * 0.5f);
        float hit = Mathf.SmoothStep(0f, 1f, (Mathf.Sin(beat) + 1f) * 0.5f);
        float alternate = Mathf.Cos(beat) * ((variant & 1) == 0 ? 1f : -1f);
        float crouch = hit * (slam ? 0.13f : 0.075f) * strength;
        Vector3 offset = new Vector3(side * (slam ? 0.045f : 0.09f) * strength,
            -crouch + Mathf.Max(0f, -Mathf.Sin(beat)) * 0.018f * strength,
            hit * (slam ? 0.065f : 0.035f) * strength);
        Vector3 angles = new Vector3((slam ? 9f + hit * 14f : 3f + hit * 6f) * strength,
            side * (slam ? 5f : 11f) * strength, side * (slam ? 4f : 8f) * strength);
        BlendCrowdPivot(offset, angles, weight);

        // A wide stance and bent knees absorb the beat instead of translating the actor root.
        AimPresentation("upperlegl", "lowerlegl", new Vector3(-0.25f, -1f, 0.15f + hit * 0.32f), weight);
        AimPresentation("upperlegr", "lowerlegr", new Vector3(0.25f, -1f, 0.15f + hit * 0.32f), weight);
        AimPresentation("lowerlegl", "footl", new Vector3(0.05f, -1f, -0.18f - hit * 0.36f), weight);
        AimPresentation("lowerlegr", "footr", new Vector3(-0.05f, -1f, -0.18f - hit * 0.36f), weight);
        RotatePresentationBone("torso", hit * (slam ? 15f : 7f) * weight * strength, alternate * 7f * weight * strength, 0f);
        RotatePresentationBone("head", (hit * (slam ? 27f : 15f) - 5f) * weight * strength, -side * 7f * weight, 0f);

        float shoulderHeight = PresentationBonePosition("upperarml", new Vector3(-0.18f, 1.4f, 0f)).y;
        float reach = slam ? 0.47f : 0.42f;
        Vector3 left = new Vector3(-0.43f - side * 0.06f, shoulderHeight - 0.24f + alternate * 0.16f, reach + hit * 0.07f);
        Vector3 right = new Vector3(0.43f - side * 0.06f, shoulderHeight - 0.24f - alternate * 0.16f, reach - hit * 0.05f);
        if (slam)
        {
            left.y += (1f - hit) * 0.32f;
            right.y += hit * 0.32f;
        }
        SolveCrowdArm(crowdLeftArm, left, new Vector3(-0.55f, shoulderHeight - 0.26f, 0.08f), weight);
        SolveCrowdArm(crowdRightArm, right, new Vector3(0.55f, shoulderHeight - 0.26f, 0.08f), weight);
        PoseCrowdPalm(crowdLeftArm, false, pivot.forward, pivot.right, weight * 0.65f);
        PoseCrowdPalm(crowdRightArm, true, pivot.forward, -pivot.right, weight * 0.65f);
    }

    private void ApplyCrowdSupportPresentation(float beat, float energy)
    {
        float weight = CrowdWeight;
        float brace = (Mathf.Sin(beat) + 1f) * 0.5f;
        BlendCrowdPivot(new Vector3(0f, -0.025f - brace * 0.018f * energy, 0f),
            new Vector3(-2f, 0f, Mathf.Sin(beat * 0.5f) * 1.2f), weight);
        AimPresentation("upperlegl", "lowerlegl", new Vector3(-0.23f, -1f, 0.18f), weight);
        AimPresentation("upperlegr", "lowerlegr", new Vector3(0.23f, -1f, 0.18f), weight);
        AimPresentation("lowerlegl", "footl", new Vector3(0.025f, -1f, -0.22f), weight);
        AimPresentation("lowerlegr", "footr", new Vector3(-0.025f, -1f, -0.22f), weight);
        RotatePresentationBone("head", -14f * weight, 0f, 0f);

        Vector3 target = hasCrowdSupportTarget ? pivot.InverseTransformPoint(crowdSupportTarget)
            : new Vector3(0f, PresentationBonePosition("head", new Vector3(0f, 1.67f, 0f)).y + 0.14f, 0.20f);
        Vector3 leftTarget = target - Vector3.right * 0.12f;
        Vector3 rightTarget = target + Vector3.right * 0.12f;
        SolveCrowdArm(crowdLeftArm, leftTarget, new Vector3(-0.48f, target.y - 0.35f, target.z - 0.16f), weight);
        SolveCrowdArm(crowdRightArm, rightTarget, new Vector3(0.48f, target.y - 0.35f, target.z - 0.16f), weight);
        PoseCrowdPalm(crowdLeftArm, false, pivot.forward, transform.up, weight);
        PoseCrowdPalm(crowdRightArm, true, pivot.forward, transform.up, weight);
    }

    private void ApplyCrowdSurfPresentation(float beat, float energy)
    {
        float weight = CrowdWeight;
        // Neutralize the walking hip twist before laying the body across the supporters.
        RestoreCrowdBone("hips", weight);
        RestoreCrowdBone("torso", weight);
        RestoreCrowdBone("chest", weight);
        RestoreCrowdBone("head", weight);
        float sway = Mathf.Sin(beat * 0.5f);
        AimPresentation("upperarml", "lowerarml", new Vector3(-0.88f, -0.28f, 0.18f + sway * 0.08f), weight);
        AimPresentation("upperarmr", "lowerarmr", new Vector3(0.88f, -0.28f, 0.18f - sway * 0.08f), weight);
        AimPresentation("lowerarml", "handl", new Vector3(-0.85f, -0.22f, 0.18f), weight);
        AimPresentation("lowerarmr", "handr", new Vector3(0.85f, -0.22f, 0.18f), weight);
        AimPresentation("upperlegl", "lowerlegl", new Vector3(-0.13f, -1f, 0.08f + sway * 0.045f), weight);
        AimPresentation("upperlegr", "lowerlegr", new Vector3(0.13f, -1f, 0.08f - sway * 0.045f), weight);
        AimPresentation("lowerlegl", "footl", new Vector3(0f, -1f, -0.10f), weight);
        AimPresentation("lowerlegr", "footr", new Vector3(0f, -1f, -0.10f), weight);
        RotatePresentationBone("head", -3f * weight, sway * 4f * weight * energy, 0f);

        Transform hip = CrowdHipAnchor;
        if (hip == null) return;
        Vector3 hipLocal = pivot.InverseTransformPoint(hip.position);
        Vector3 standingHip = pivot.localPosition + pivot.localRotation * Vector3.Scale(hipLocal, pivot.localScale);
        // X=-90 puts the face upward, head toward root-local -Z, feet toward +Z.
        // Rotate around the hips; at full weight their anchor is exactly the actor origin.
        Quaternion horizontal = Quaternion.AngleAxis(sway * 3f * energy, Vector3.back) * Quaternion.Euler(-90f, 0f, 0f);
        Quaternion rotation = Quaternion.Slerp(pivot.localRotation, horizontal, weight);
        Vector3 wantedHip = Vector3.Lerp(standingHip, Vector3.zero, weight);
        pivot.localRotation = rotation;
        pivot.localPosition = wantedHip - rotation * Vector3.Scale(hipLocal, pivot.localScale);
    }

    private void BlendCrowdPivot(Vector3 offset, Vector3 angles, float weight)
    {
        pivot.localPosition += offset * weight;
        pivot.localRotation = Quaternion.Slerp(pivot.localRotation, Quaternion.Euler(angles), weight);
    }

    private void RestoreCrowdBone(string key, float weight)
    {
        if (bones.TryGetValue(key, out var bone) && restRotations.TryGetValue(bone, out var rest))
            bone.localRotation = Quaternion.Slerp(bone.localRotation, rest, weight);
    }

    private void CacheCrowdBones()
    {
        if (crowdBonesCached) return;
        crowdBonesCached = true;
        crowdLeftArm = CacheCrowdArm("l");
        crowdRightArm = CacheCrowdArm("r");
    }

    private CrowdArm CacheCrowdArm(string side)
    {
        CrowdArm arm = new CrowdArm();
        bones.TryGetValue("upperarm" + side, out arm.upper);
        bones.TryGetValue("lowerarm" + side, out arm.lower);
        bones.TryGetValue("hand" + side, out arm.hand);
        for (int index = 1; index <= 4; index++)
        {
            if (arm.middle == null) bones.TryGetValue("middle" + index + side, out arm.middle);
            if (arm.index == null) bones.TryGetValue("index" + index + side, out arm.index);
        }
        if (arm.middle == null) arm.middle = arm.index;
        return arm;
    }

    private void SolveCrowdArm(CrowdArm arm, Vector3 target, Vector3 hint, float weight)
        => SolvePresentationArm(arm.upper, arm.lower, arm.hand, target, hint, weight);

    private void PoseCrowdPalm(CrowdArm arm, bool right, Vector3 fingerDirection, Vector3 palmNormal, float weight)
    {
        if (arm.hand == null || arm.middle == null) return;
        Quaternion start = arm.hand.rotation;
        Vector3 forward = arm.middle.position - arm.hand.position;
        if (forward.sqrMagnitude < 0.000001f) return;
        arm.hand.rotation = Quaternion.FromToRotation(forward, fingerDirection) * start;
        if (arm.index != null && arm.index != arm.middle)
        {
            Vector3 normal = Vector3.Cross(arm.middle.position - arm.hand.position, arm.index.position - arm.middle.position);
            if (right) normal = -normal;
            Vector3 axis = fingerDirection.normalized;
            Vector3 current = Vector3.ProjectOnPlane(normal, axis);
            Vector3 wanted = Vector3.ProjectOnPlane(palmNormal, axis);
            if (current.sqrMagnitude > 0.000001f && wanted.sqrMagnitude > 0.000001f)
                arm.hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(current, wanted, axis), axis) * arm.hand.rotation;
        }
        arm.hand.rotation = Quaternion.Slerp(start, arm.hand.rotation, weight);
    }
}
