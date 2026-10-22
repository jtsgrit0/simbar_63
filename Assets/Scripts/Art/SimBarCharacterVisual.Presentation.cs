using UnityEngine;

/// <summary>Host-clock animation layered on each character's own imported skeleton.</summary>
public sealed partial class SimBarCharacterVisual
{
    public bool Managed { get; private set; }
    public string Role => role;
    public bool IsWalking => walkingBlend > 0.05f;
    public bool IsServing => servingBlend > 0.05f;
    public float WalkingBlend => walkingBlend;
    public float ServingBlend => servingBlend;

    private bool presentationInitialized;
    private float lastPresentationTime;
    private float clipPresentationTime;
    private float gaitPhase;
    private float walkingBlend;
    private float servingBlend;
    private float leftDrumLift = 0.5f;
    private float rightDrumLift = 0.5f;
    private Transform servingGlass;

    public void SetManaged(bool managed)
    {
        if (Managed == managed) return;
        Managed = managed;
        presentationInitialized = false;
    }

    /// <summary>
    /// timeSeconds advances only with gameplay; 120 BPM is two beats per second.
    /// walkingSpeed is metres per second and servingAmount is a 0..1 gesture weight.
    /// This moves the visual pivot and bones; the caller owns the character root.
    /// </summary>
    public void ApplyPresentation(float timeSeconds, float energy, float walkingSpeed, float servingAmount)
    {
        if (model == null || pivot == null) return;
        timeSeconds = FinitePresentation(timeSeconds) ? Mathf.Max(0f, timeSeconds) : lastPresentationTime;
        energy = FinitePresentation(energy) ? Mathf.Clamp01(energy) : 0.5f;
        walkingSpeed = FinitePresentation(walkingSpeed) ? Mathf.Clamp(walkingSpeed, 0f, 1.2f) : 0f;
        servingAmount = FinitePresentation(servingAmount) ? Mathf.Clamp01(servingAmount) : 0f;
        float elapsed = presentationInitialized ? Mathf.Max(0f, timeSeconds - lastPresentationTime) : 0f;
        float playbackRate = Mathf.Lerp(0.85f, 1.18f, energy);
        if (!presentationInitialized || timeSeconds < lastPresentationTime || elapsed > 5f)
        {
            clipPresentationTime = timeSeconds * playbackRate + phase;
            gaitPhase = timeSeconds * 7f + phase;
            walkingBlend = walkingSpeed / 1.2f;
            servingBlend = servingAmount;
        }
        else
        {
            clipPresentationTime += elapsed * playbackRate;
            float blend = 1f - Mathf.Exp(-Mathf.Min(0.25f, elapsed) * 10f);
            walkingBlend = Mathf.Lerp(walkingBlend, walkingSpeed / 1.2f, blend);
            servingBlend = Mathf.Lerp(servingBlend, servingAmount, blend);
            gaitPhase += elapsed * Mathf.Lerp(5.4f, 8.8f, walkingBlend);
        }
        presentationInitialized = true;
        lastPresentationTime = timeSeconds;
        float beat = timeSeconds * (Mathf.PI * 4f);
        float rhythm = (role == "guest" || role == "barseated" ? beat / 3f : beat) + phase;

        // Always restore before sampling: unanimated finger/arm tracks must not
        // retain an additive pose from the previous frame.
        foreach (var pair in restRotations)
            if (pair.Key != null) pair.Key.localRotation = pair.Value;
        if (clip != null && clip.length > 0f && role != "guest" && role != "barseated")
            clip.SampleAnimation(model, Mathf.Repeat(clipPresentationTime, clip.length));
        else ApplyProceduralPose(rhythm);

        PoseVisualPivot(rhythm, energy);
        if (walkingBlend > 0.001f && role != "seated" && role != "barseated") ApplyWalkingPose();
        if (role == "singer") ApplyVocalPerformance(rhythm, energy);
        else if (role == "guitarist" || role == "bassist") ApplyStringPerformance(rhythm, energy);
        else if (role == "drummer") ApplyDrumPerformance(beat, energy);
        else if (role == "barseated") ApplyBarSeatedPresentation(timeSeconds, rhythm);
        else if (role == "guest")
        {
            ApplyRelaxedGuestPresentation(rhythm);
            if (servingBlend > 0.001f) ApplyServicePose(rhythm);
        }
        else if (role == "bartender" || servingBlend > 0.001f) ApplyServicePose(rhythm);
        UpdateAccessories(timeSeconds, energy);
    }

    private void PoseVisualPivot(float beat, float energy)
    {
        float side = Mathf.Sin(beat * 0.5f);
        float pulse = Mathf.Abs(Mathf.Sin(beat));
        Vector3 position = Vector3.zero;
        Vector3 rotation = Vector3.zero;
        if (role == "barseated")
        {
            position.y = seatHeight - standingHipHeight;
        }
        else if (role == "seated")
        {
            position.y = seatHeight - standingHipHeight;
            rotation = new Vector3(0f, side * 1.2f, Mathf.Sin(beat) * 0.5f);
        }
        else if (role == "drummer")
        {
            // Hips and feet stay planted behind the kit; upper-body bones do the work.
        }
        else if (role == "singer")
        {
            position = new Vector3(side * Mathf.Lerp(0.035f, 0.11f, energy), pulse * Mathf.Lerp(0.018f, 0.055f, energy), Mathf.Sin(beat * 0.25f) * 0.025f);
            rotation = new Vector3(Mathf.Sin(beat) * 2f, side * Mathf.Lerp(3f, 7f, energy), Mathf.Sin(beat * 0.5f) * Mathf.Lerp(2f, 5f, energy));
        }
        else if (role == "guitarist" || role == "bassist")
        {
            position = new Vector3(side * 0.055f * energy, pulse * Mathf.Lerp(0.012f, 0.045f, energy), Mathf.Sin(beat * 0.5f) * 0.02f * energy);
            rotation = new Vector3(2f + pulse * 3f * energy, side * 5f * energy, Mathf.Sin(beat) * 3.5f * energy);
        }
        else if (role == "bartender")
        {
            position = new Vector3(0f, pulse * 0.004f * (1f - walkingBlend), servingBlend * 0.025f);
            rotation = new Vector3(servingBlend * 5f, side * (1f - walkingBlend), 0f);
        }
        else if (role == "guest")
        {
            float idle = 1f - walkingBlend;
            position = new Vector3(side * 0.012f * idle, 0f, Mathf.Sin(beat * 0.27f) * 0.003f * idle);
            rotation = new Vector3(Mathf.Sin(beat * 0.35f) * 0.6f * idle, side * 1.5f * idle, Mathf.Sin(beat * 0.5f) * 0.6f * idle);
        }
        else
        {
            float idle = 1f - walkingBlend;
            position = new Vector3(side * 0.018f * energy * idle, pulse * Mathf.Lerp(0.02f, 0.055f, energy) * idle, 0f);
            rotation = new Vector3(0f, side * 6f * energy * idle, Mathf.Sin(beat) * 3f * energy * idle);
        }
        if (role != "seated" && role != "barseated" && role != "drummer")
        {
            position.y += Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.025f * walkingBlend;
            position.x += Mathf.Sin(gaitPhase) * 0.01f * walkingBlend;
            rotation.z += Mathf.Sin(gaitPhase) * 1.4f * walkingBlend;
        }
        pivot.localPosition = position;
        pivot.localRotation = Quaternion.Euler(rotation);
    }

    private void ApplyWalkingPose()
    {
        float stride = Mathf.Sin(gaitPhase);
        float opposite = -stride;
        float amount = walkingBlend;
        AimPresentation("upperlegl", "lowerlegl", new Vector3(-0.07f, -1f, stride * 0.58f), amount);
        AimPresentation("upperlegr", "lowerlegr", new Vector3(0.07f, -1f, opposite * 0.58f), amount);
        AimPresentation("lowerlegl", "footl", new Vector3(0f, -1f, Mathf.Max(0f, -stride) * 0.65f), amount);
        AimPresentation("lowerlegr", "footr", new Vector3(0f, -1f, Mathf.Max(0f, stride) * 0.65f), amount);
        float armWeight = amount * (1f - servingBlend * 0.85f);
        AimPresentation("upperarml", "lowerarml", new Vector3(-0.16f, -1f, -stride * 0.35f), armWeight);
        AimPresentation("upperarmr", "lowerarmr", new Vector3(0.16f, -1f, stride * 0.35f), armWeight);
        AimPresentation("lowerarml", "handl", new Vector3(0f, -1f, 0.15f - stride * 0.2f), armWeight);
        AimPresentation("lowerarmr", "handr", new Vector3(0f, -1f, 0.15f + stride * 0.2f), armWeight);
        RotatePresentationBone("hips", 0f, stride * 3f * amount, 0f);
    }

    private void ApplyVocalPerformance(float beat, float energy)
    {
        float phrase = (Mathf.Sin(beat * 0.125f) + 1f) * 0.5f;
        RotatePresentationBone("torso", Mathf.Sin(beat) * 3f * energy, Mathf.Sin(beat * 0.5f) * 5f * energy, 0f);
        RotatePresentationBone("chest", -2f * energy, Mathf.Sin(beat * 0.5f) * 3f * energy, 0f);
        RotatePresentationBone("head", Mathf.Sin(beat) * 6f * energy, Mathf.Sin(beat * 0.25f) * 8f * energy, 0f);
        Vector3 head = PresentationBonePosition("head", new Vector3(0f, 1.65f, 0f));
        SolvePresentationArm("r", head + new Vector3(0.10f, -0.13f, 0.13f), new Vector3(0.45f, 1.25f, 0.25f), 0.82f);
        Vector3 shoulder = PresentationBonePosition("upperarml", new Vector3(-0.18f, 1.4f, 0f));
        Vector3 gesture = new Vector3(-0.43f - phrase * 0.23f * energy, shoulder.y - 0.35f + phrase * 0.52f * energy, 0.28f + Mathf.Sin(beat * 0.25f) * 0.08f);
        SolvePresentationArm("l", gesture, new Vector3(-0.45f, 1.15f, 0.15f), 0.5f + energy * 0.35f);
    }

    private void ApplyStringPerformance(float beat, float energy)
    {
        RotatePresentationBone("torso", 3f * energy + Mathf.Sin(beat) * 2f * energy, Mathf.Sin(beat * 0.5f) * 6f * energy, 0f);
        RotatePresentationBone("chest", Mathf.Sin(beat) * 3f * energy, 0f, 0f);
        RotatePresentationBone("head", Mathf.Sin(beat) * 9f * energy, Mathf.Sin(beat * 0.25f) * 4f * energy, 0f);
    }

    private void ApplyDrumPerformance(float beat, float energy)
    {
        // Opposing half-beat arcs expose the raised hand above the rack toms,
        // rather than keeping both wrists below the kit during most of the bar.
        float alternate = Mathf.Cos(beat);
        rightDrumLift = Mathf.SmoothStep(0f, 1f, (1f - alternate) * 0.5f);
        leftDrumLift = Mathf.SmoothStep(0f, 1f, (1f + alternate) * 0.5f);
        float rightHit = 1f - rightDrumLift;
        float leftHit = 1f - leftDrumLift;
        float headHeight = PresentationBonePosition("head", new Vector3(0f, 1.68f, 0f)).y;
        float prepareHeight = Mathf.Clamp(headHeight + Mathf.Lerp(0.01f, 0.11f, energy), 1.65f, 1.84f);
        float strikeHeight = Mathf.Lerp(1.27f, 1.38f, energy);
        RotatePresentationBone("torso", 2f + Mathf.Max(rightHit, leftHit) * 4f * energy, (leftHit - rightHit) * 4f * energy, 0f);
        RotatePresentationBone("head", Mathf.Sin(beat) * 7f * energy, Mathf.Sin(beat * 0.25f) * 3f, 0f);
        Vector3 leftTarget = Vector3.Lerp(new Vector3(-0.34f, strikeHeight, 0.40f), new Vector3(-0.24f, prepareHeight, 0.12f), leftDrumLift);
        Vector3 rightTarget = Vector3.Lerp(new Vector3(0.34f, Mathf.Min(1.4f, strikeHeight + 0.02f), 0.42f), new Vector3(0.24f, prepareHeight, 0.12f), rightDrumLift);
        SolvePresentationArm("l", leftTarget, new Vector3(-0.52f, 1.48f, 0.14f), 1f);
        SolvePresentationArm("r", rightTarget, new Vector3(0.52f, 1.48f, 0.14f), 1f);
    }

    private void ApplyServicePose(float beat)
    {
        float idleWeight = role == "bartender" ? (1f - walkingBlend) * 0.65f : 0f;
        float weight = Mathf.Max(idleWeight, servingBlend);
        if (weight < 0.001f) return;
        float reach = servingBlend;
        AimPresentation("upperarmr", "lowerarmr", new Vector3(0.22f, -0.7f + reach * 0.65f, 0.32f + reach * 0.5f), weight);
        AimPresentation("lowerarmr", "handr", new Vector3(-0.08f, 0.1f + reach * 0.1f, 0.85f), weight);
        AimPresentation("upperarml", "lowerarml", new Vector3(-0.2f, -0.85f, 0.25f), weight * 0.8f);
        AimPresentation("lowerarml", "handl", new Vector3(0.12f, -0.2f, 0.8f), weight * 0.8f);
        RotatePresentationBone("head", servingBlend * 4f, Mathf.Sin(beat * 0.125f) * 3f * (1f - walkingBlend), 0f);
    }

    private void AimPresentation(string boneName, string childName, Vector3 localDirection, float weight)
    {
        if (!bones.TryGetValue(boneName, out var bone) || !bones.TryGetValue(childName, out var child)) return;
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.0000001f) return;
        Quaternion wanted = Quaternion.FromToRotation(current, pivot.TransformDirection(localDirection.normalized)) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, wanted, Mathf.Clamp01(weight));
    }

    private void SolvePresentationArm(string side, Vector3 localTarget, Vector3 localHint, float weight)
    {
        if (!bones.TryGetValue("upperarm" + side, out var upper) || !bones.TryGetValue("lowerarm" + side, out var lower) || !bones.TryGetValue("hand" + side, out var hand)) return;
        Quaternion upperStart = upper.rotation;
        Quaternion lowerStart = lower.rotation;
        Vector3 shoulder = upper.position;
        float upperLength = Vector3.Distance(shoulder, lower.position);
        float lowerLength = Vector3.Distance(lower.position, hand.position);
        if (upperLength < 0.001f || lowerLength < 0.001f) return;
        Vector3 delta = pivot.TransformPoint(localTarget) - shoulder;
        Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : pivot.forward;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.015f, (upperLength + lowerLength) * 0.97f);
        Vector3 target = shoulder + direction * distance;
        Vector3 bend = Vector3.ProjectOnPlane(pivot.TransformPoint(localHint) - shoulder, direction);
        if (bend.sqrMagnitude < 0.000001f) bend = Vector3.ProjectOnPlane(-pivot.up, direction);
        if (bend.sqrMagnitude < 0.000001f) bend = Vector3.ProjectOnPlane(pivot.right, direction);
        bend.Normalize();
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        Vector3 elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - shoulder) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
        Quaternion upperTarget = upper.rotation;
        Quaternion lowerTarget = lower.rotation;
        upper.rotation = Quaternion.Slerp(upperStart, upperTarget, weight);
        lower.rotation = Quaternion.Slerp(lowerStart, lowerTarget, weight);
    }

    private void RotatePresentationBone(string key, float pitch, float yaw, float roll)
    {
        if (!bones.TryGetValue(key, out var bone)) return;
        bone.rotation = Quaternion.AngleAxis(yaw, pivot.up) * Quaternion.AngleAxis(pitch, pivot.right) * Quaternion.AngleAxis(roll, pivot.forward) * bone.rotation;
    }

    private Vector3 PresentationBonePosition(string key, Vector3 fallback)
        => bones.TryGetValue(key, out var bone) ? pivot.InverseTransformPoint(bone.position) : fallback;

    private void CreateServingAccessory()
    {
        servingGlass = Primitive("Service glass", PrimitiveType.Cylinder, new Vector3(0.075f, 0.10f, 0.075f), new Color(0.92f, 0.57f, 0.13f));
        Transform foam = Primitive("Service glass foam", PrimitiveType.Cylinder, Vector3.one, new Color(0.96f, 0.92f, 0.78f));
        foam.SetParent(servingGlass, false);
        foam.localPosition = new Vector3(0f, 0.86f, 0f);
        foam.localScale = new Vector3(1.02f, 0.12f, 1.02f);
        servingGlass.gameObject.SetActive(false);
    }

    private void UpdateServingAccessory()
    {
        if (servingGlass == null) return;
        bool visible = servingBlend > 0.08f && bones.TryGetValue("handr", out _);
        servingGlass.gameObject.SetActive(visible);
        if (!visible) return;
        Transform hand = bones["handr"];
        servingGlass.position = hand.position + pivot.up * 0.065f + pivot.forward * 0.035f;
        servingGlass.rotation = Quaternion.LookRotation(pivot.forward, pivot.up);
    }

    private static bool FinitePresentation(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
