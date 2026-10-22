using UnityEngine;

/// <summary>Unhurried guests and deterministic seated drinking, driven by the host clock.</summary>
public sealed partial class SimBarCharacterVisual
{
    public Transform DrinkingCup => drinkingCup;
    public Transform DrinkingCupRim => drinkingCupRim;
    public float SipAmount { get; private set; }
    public float SipCycleSeconds => role == "barseated" ? 8f + (variant % 4) * 1.1f : 0f;

    private const float DrinkingCupHalfHeight = 0.085f;
    private static readonly Vector3 DrinkingPalmOffset = new Vector3(-0.035f, 0.045f, 0.025f);
    private static readonly string[] DrinkingFingerNames = { "index", "middle", "ring", "pinky" };
    private readonly Transform[] drinkingFingerRoots = new Transform[4];
    private Transform drinkingMiddle;
    private Transform drinkingThumb;
    private Transform drinkingCup;
    private Transform drinkingCupRim;
    private Quaternion drinkingLocalRotation = Quaternion.identity;

    private void ApplyRelaxedGuestPresentation(float slowBeat)
    {
        float idle = 1f - walkingBlend;
        RotatePresentationBone("torso", Mathf.Sin(slowBeat * 0.35f) * 0.7f * idle, Mathf.Sin(slowBeat * 0.25f) * 0.9f * idle, 0f);
        RotatePresentationBone("head", Mathf.Sin(slowBeat * 0.3f) * 1.2f * idle, Mathf.Sin(slowBeat * 0.2f) * 2f * idle, 0f);
    }

    private void ApplyBarSeatedLegPose()
    {
        // Knees point toward the counter; ankles tuck back toward the stool ring.
        // Only rotations change, so the imported leg lengths remain intact.
        Aim("upperlegl", "lowerlegl", new Vector3(-0.13f, -0.45f, 1f));
        Aim("upperlegr", "lowerlegr", new Vector3(0.13f, -0.45f, 1f));
        Aim("lowerlegl", "footl", new Vector3(0f, -1f, -0.38f));
        Aim("lowerlegr", "footr", new Vector3(0f, -1f, -0.38f));
        LevelBarSeatedFoot("footl");
        LevelBarSeatedFoot("footr");
    }

    private void LevelBarSeatedFoot(string key)
    {
        if (!bones.TryGetValue(key, out var foot) || foot.childCount == 0) return;
        Vector3 current = foot.GetChild(0).position - foot.position;
        if (current.sqrMagnitude > 0.0000001f)
            foot.rotation = Quaternion.FromToRotation(current, pivot.forward) * foot.rotation;
    }

    private void ApplyBarSeatedPresentation(float timeSeconds, float slowBeat)
    {
        float cycle = Mathf.Repeat(timeSeconds + variant * 1.37f, SipCycleSeconds);
        float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((cycle - 2f) / 0.9f));
        float lower = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((cycle - 4.2f) / 1f));
        SipAmount = raise * (1f - lower);
        RotatePresentationBone("torso", 1.2f - SipAmount * 1.5f, Mathf.Sin(slowBeat * 0.2f) * 0.7f, 0f);
        RotatePresentationBone("head", -SipAmount * 6f + Mathf.Sin(slowBeat * 0.25f) * 0.5f, Mathf.Sin(slowBeat * 0.18f) * 1.5f * (1f - SipAmount), 0f);

        Vector3 head = PresentationBonePosition("head", new Vector3(0f, 1.68f, 0f));
        Vector3 mouth = head + new Vector3(0f, -0.055f, 0.11f);
        drinkingLocalRotation = Quaternion.Euler(-30f * SipAmount, 0f, Mathf.Sin(slowBeat * 0.2f) * (1f - SipAmount));
        Vector3 restWrist = new Vector3(0.25f, Mathf.Clamp(head.y - 0.30f, 1.28f, 1.43f), 0.39f);
        Vector3 sipWrist = mouth - drinkingLocalRotation * (DrinkingPalmOffset + Vector3.up * DrinkingCupHalfHeight);
        SolvePresentationArm("r", Vector3.Lerp(restWrist, sipWrist, SipAmount), new Vector3(0.48f, head.y - 0.26f, 0.16f), 1f);
        SolvePresentationArm("l", new Vector3(-0.25f, head.y - 0.34f, 0.28f), new Vector3(-0.44f, head.y - 0.25f, 0.12f), 0.95f);
        PoseDrinkingGrip();
    }

    private void CreateDrinkingAccessory()
    {
        drinkingCup = Primitive("Bar guest drinking cup", PrimitiveType.Cylinder,
            new Vector3(0.075f, DrinkingCupHalfHeight, 0.075f), new Color(0.79f, 0.44f, 0.14f));
        Transform foam = Primitive("Bar guest drink foam", PrimitiveType.Cylinder, Vector3.one, new Color(0.96f, 0.92f, 0.78f));
        foam.SetParent(drinkingCup, false);
        foam.localPosition = new Vector3(0f, 0.86f, 0f);
        foam.localScale = new Vector3(1.02f, 0.12f, 1.02f);
        drinkingCupRim = new GameObject("Bar guest cup rim").transform;
        drinkingCupRim.SetParent(drinkingCup, false);
        drinkingCupRim.localPosition = Vector3.up;
        for (int index = 0; index < drinkingFingerRoots.Length; index++)
            drinkingFingerRoots[index] = DrinkingFingerRoot(DrinkingFingerNames[index]);
        drinkingMiddle = drinkingFingerRoots[1] ?? drinkingFingerRoots[0];
        drinkingThumb = DrinkingFingerRoot("thumb");
    }

    private void UpdateDrinkingAccessory()
    {
        if (drinkingCup == null || !bones.TryGetValue("handr", out var hand)) return;
        Quaternion rotation = pivot.rotation * drinkingLocalRotation;
        drinkingCup.rotation = rotation;
        drinkingCup.position = hand.position + rotation * DrinkingPalmOffset;
    }

    private void PoseDrinkingGrip()
    {
        if (!bones.TryGetValue("handr", out var hand)) return;
        Transform middle = drinkingMiddle;
        if (middle == null) return;
        Quaternion cupRotation = pivot.rotation * drinkingLocalRotation;
        Vector3 fingerDirection = cupRotation * new Vector3(-0.7f, 0.1f, 0.7f).normalized;
        Vector3 current = middle.position - hand.position;
        if (current.sqrMagnitude > 0.0000001f)
            hand.rotation = Quaternion.FromToRotation(current, fingerDirection) * hand.rotation;
        foreach (Transform root in drinkingFingerRoots)
        {
            Transform joint = root;
            for (int depth = 0; joint != null && joint.childCount > 0 && depth < 4; depth++)
            {
                Transform child = joint.GetChild(0);
                float curl = 0.45f + depth * 0.43f;
                Vector3 wanted = fingerDirection * Mathf.Cos(curl) - cupRotation * Vector3.forward * Mathf.Sin(curl);
                Vector3 boneDirection = child.position - joint.position;
                if (boneDirection.sqrMagnitude > 0.0000001f)
                    joint.rotation = Quaternion.FromToRotation(boneDirection, wanted) * joint.rotation;
                joint = child;
            }
        }
        Transform thumb = drinkingThumb;
        if (thumb != null && thumb.childCount > 0)
        {
            Vector3 contact = hand.position + cupRotation * new Vector3(-0.04f, 0.04f, -0.015f);
            Vector3 currentThumb = thumb.GetChild(0).position - thumb.position;
            if (currentThumb.sqrMagnitude > 0.0000001f)
                thumb.rotation = Quaternion.FromToRotation(currentThumb, contact - thumb.position) * thumb.rotation;
        }
    }

    private Transform DrinkingFingerRoot(string finger)
    {
        for (int index = 1; index <= 4; index++)
            if (bones.TryGetValue(finger + index + "r", out var joint)) return joint;
        return null;
    }
}
