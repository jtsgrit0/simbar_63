using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Post-animation guitar posing. IK uses joint-to-joint world directions rather than assuming FBX bone axes.
/// The imported Gibson's measured Unity convention is neck -X, body +X and fretboard/front +Z.
/// </summary>
public sealed class SimBarGuitarPerformance : MonoBehaviour
{
    public float LeftHandError { get; private set; }
    public float RightHandError { get; private set; }
    public float LeftElbowAngle { get; private set; }
    public float RightElbowAngle { get; private set; }
    public bool LeftGripApplied { get; private set; }
    public bool RightGripApplied { get; private set; }
    public bool LeftThumbApplied { get; private set; }
    public bool RightThumbApplied { get; private set; }
    public float LeftFingerCurl { get; private set; }
    public float RightFingerCurl { get; private set; }
    public float LeftPalmAlignment { get; private set; }
    public float RightPalmAlignment { get; private set; }
    public bool FretReachClamped { get; private set; }
    public bool FretTargetReachable { get; private set; }
    public float FretReachMargin { get; private set; }
    public Transform FrettingTarget { get; private set; }
    public Transform PickingTarget { get; private set; }
    public Transform Guitar { get; private set; }

    private Transform pivot;
    private Dictionary<string, Transform> bones;
    private Dictionary<Transform, Quaternion> rest;
    private bool bass;
    private Mesh strapMesh;
    private Material strapMaterial;
    private readonly Vector3[] strapPoints = new Vector3[9];
    private readonly Vector3[] strapVertices = new Vector3[36];
    private readonly Vector2[] strapUV = new Vector2[36];
    private readonly int[] strapIndices = new int[192];
    private readonly List<Material> hardwareMaterials = new List<Material>();
    private static readonly string[] FingerNames = { "index", "middle", "ring", "pinky", "thumb" };

    public void Initialize(Transform visualPivot, Transform guitar, Dictionary<string, Transform> skeleton,
        Dictionary<Transform, Quaternion> neutralRotations, bool isBass)
    {
        pivot = visualPivot;
        Guitar = guitar;
        bones = skeleton;
        rest = neutralRotations;
        bass = isBass;
        FrettingTarget = Anchor("Fretboard hand target", new Vector3(bass ? -0.15f : -0.18f, -0.09f, 0.045f));
        PickingTarget = Anchor("Pickup hand target", new Vector3(0.27f, 0.09f, 0.10f));
        CreateStrap();
    }

    // Called explicitly AFTER the original clip is sampled and the visual pivot's sway is applied.
    // This has no Update/LateUpdate of its own, so edit preview and Play use exactly the same ordering.
    public void Apply(float time)
    {
        Apply(time, 0.65f);
    }

    /// <summary>Energy-scaled strumming on the same 120 BPM host clock as the band.</summary>
    public void Apply(float time, float energy)
    {
        if (pivot == null || Guitar == null || bones == null) return;
        energy = float.IsNaN(energy) || float.IsInfinity(energy) ? 0.5f : Mathf.Clamp01(energy);
        float beat = time * (Mathf.PI * 4f);
        StabilizeTorso("abdomen", 0.55f);
        StabilizeTorso("torso", 0.45f);
        StabilizeTorso("chest", 0.4f);
        StabilizeTorso("shoulderl", 0.12f);
        StabilizeTorso("shoulderr", 0.12f);

        Vector3 leftShoulder = BonePosition("upperarml", new Vector3(-0.17f, 1.4f, 0f));
        Vector3 rightShoulder = BonePosition("upperarmr", new Vector3(0.17f, 1.4f, 0f));
        Vector3 hips = BonePosition("hips", new Vector3(0f, 1.0f, 0f));
        float shoulderHeight = (leftShoulder.y + rightShoulder.y) * 0.5f;
        float bodyHeight = Mathf.Clamp(hips.y + 0.10f, shoulderHeight - 0.3f, shoulderHeight - 0.20f);
        // The guitar body sits at the upper waist; its neck rises toward the player's left hand.
        float lean = bass ? -16f : -18f;
        Guitar.localRotation = Quaternion.Euler(0f, Mathf.Sin(beat * 0.25f) * energy * 1.8f, lean + Mathf.Sin(beat * 0.5f) * Mathf.Lerp(1f, 3.2f, energy));
        Vector3 bodyAnchor = new Vector3(0.30f, 0f, 0f);
        Vector3 bodyPosition = new Vector3((leftShoulder.x + rightShoulder.x) * 0.5f + 0.10f, bodyHeight,
            Mathf.Max(leftShoulder.z, rightShoulder.z) + 0.15f);
        Guitar.localPosition = bodyPosition - Guitar.localRotation * bodyAnchor;

        float slide = Mathf.Sin(beat * 0.125f) * Mathf.Lerp(0.018f, 0.04f, energy);
        // The stylized palm is about 15 cm long: the wrist belongs BELOW the neck,
        // with its palm crossing the fingerboard, rather than its wrist sitting on the strings.
        FrettingTarget.localPosition = FitFretTarget((bass ? -0.15f : -0.18f) + slide);
        float stroke = Mathf.Sin(beat * (bass ? 0.5f : 1f));
        float strokeWidth = bass ? Mathf.Lerp(0.01f, 0.024f, energy) : Mathf.Lerp(0.018f, 0.04f, energy);
        PickingTarget.localPosition = new Vector3(0.27f + Mathf.Cos(beat) * 0.006f * energy, 0.09f + stroke * strokeWidth, 0.10f);

        Vector3 leftHint = new Vector3(leftShoulder.x - 0.20f, leftShoulder.y - 0.20f, leftShoulder.z + 0.015f);
        Vector3 rightHint = new Vector3(rightShoulder.x + 0.15f, rightShoulder.y - 0.23f, rightShoulder.z + 0.015f);
        SolveArm("l", FrettingTarget.position, pivot.TransformPoint(leftHint));
        SolveArm("r", PickingTarget.position, pivot.TransformPoint(rightHint));
        PoseHand("l", Guitar.up, -Guitar.forward, true);
        PoseHand("r", -Guitar.up, -Guitar.forward, false);

        LeftHandError = HandError("l", FrettingTarget.position);
        RightHandError = HandError("r", PickingTarget.position);
        LeftElbowAngle = ElbowAngle("l");
        RightElbowAngle = ElbowAngle("r");
        UpdateStrap(leftShoulder, bodyPosition);
    }

    private void StabilizeTorso(string key, float animationWeight)
    {
        if (!bones.TryGetValue(key, out var bone) || !rest.TryGetValue(bone, out var neutral)) return;
        bone.localRotation = Quaternion.Slerp(neutral, bone.localRotation, animationWeight);
    }

    private Vector3 FitFretTarget(float desiredX)
    {
        var desired = new Vector3(desiredX, -0.09f, 0.045f);
        FretReachClamped = false;
        FretTargetReachable = false;
        FretReachMargin = 0f;
        if (!bones.TryGetValue("upperarml", out var upper)
            || !bones.TryGetValue("lowerarml", out var lower)
            || !bones.TryGetValue("handl", out var hand)) return desired;
        float armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
        // Use a little less than SolveArm's 97% reach. This makes the actual desired wrist point
        // reachable before IK, rather than accepting an error from its emergency radial clamp.
        float radius = armLength * 0.962f;
        Vector3 shoulder = upper.position;
        for (int step = 0; step <= 5; step++)
        {
            // Prefer the lower, wrapping wrist line. A higher line is only needed when the source
            // clip lifts the shoulder so far that even the highest fret cannot reach that first line.
            float wristY = -0.09f + step * 0.01f;
            Vector3 start = Guitar.TransformPoint(new Vector3(-0.4f, wristY, desired.z));
            Vector3 end = Guitar.TransformPoint(new Vector3(-0.1f, wristY, desired.z));
            Vector3 axis = end - start;
            float lineLength = axis.magnitude;
            if (lineLength < 0.0001f) break;
            axis /= lineLength;
            Vector3 fromStart = shoulder - start;
            float projection = Vector3.Dot(fromStart, axis);
            float perpendicularSquared = Mathf.Max(0f, fromStart.sqrMagnitude - projection * projection);
            float discriminant = radius * radius - perpendicularSquared;
            if (discriminant < 0f) continue;
            float extent = Mathf.Sqrt(discriminant);
            float reachableMin = Mathf.Max(0f, projection - extent);
            float reachableMax = Mathf.Min(lineLength, projection + extent);
            if (reachableMin > reachableMax) continue;
            Vector3 wanted = Guitar.TransformPoint(new Vector3(desiredX, wristY, desired.z));
            float along = Mathf.Clamp(Vector3.Dot(wanted - start, axis), reachableMin, reachableMax);
            Vector3 fittedWorld = start + axis * along;
            Vector3 fitted = Guitar.InverseTransformPoint(fittedWorld);
            FretReachClamped = Vector3.Distance(fitted, desired) > 0.0001f;
            FretTargetReachable = true;
            FretReachMargin = armLength * 0.97f - Vector3.Distance(shoulder, fittedWorld);
            return fitted;
        }
        // Keep an honest diagnostic if an unrelated skeleton cannot reach the instrument.
        // Both supplied rigs intersect the first wrist line near high frets (-0.15..-0.10).
        return new Vector3(-0.1f, -0.04f, desired.z);
    }

    private void SolveArm(string side, Vector3 requestedTarget, Vector3 elbowHint)
    {
        if (!bones.TryGetValue("upperarm" + side, out var upper)
            || !bones.TryGetValue("lowerarm" + side, out var lower)
            || !bones.TryGetValue("hand" + side, out var hand)) return;
        // Reset only the arm chain to its OWN FBX's neutral axes. Copying source local rotations here
        // would retain the Mixamo/Quaternius bone-roll mismatch even after aiming toward the right point.
        Restore(upper); Restore(lower); Restore(hand);
        Vector3 shoulder = upper.position;
        float upperLength = Vector3.Distance(shoulder, lower.position);
        float lowerLength = Vector3.Distance(lower.position, hand.position);
        if (upperLength < 0.001f || lowerLength < 0.001f) return;
        Vector3 delta = requestedTarget - shoulder;
        float rawDistance = delta.magnitude;
        Vector3 direction = rawDistance > 0.001f ? delta / rawDistance : pivot.forward;
        // The 97% limit leaves a slight elbow bend and prevents hyperextension/unstable straight-arm flips.
        float distance = Mathf.Clamp(rawDistance, Mathf.Abs(upperLength - lowerLength) + 0.015f, (upperLength + lowerLength) * 0.97f);
        Vector3 target = shoulder + direction * distance;
        Vector3 bend = Vector3.ProjectOnPlane(elbowHint - shoulder, direction);
        if (bend.sqrMagnitude < 0.00001f) bend = Vector3.ProjectOnPlane(-pivot.up, direction);
        bend.Normalize();
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 elbow = shoulder + direction * along + bend * height;
        AimJoint(upper, lower.position - upper.position, elbow - shoulder);
        AimJoint(lower, hand.position - lower.position, target - lower.position);
    }

    private static void AimJoint(Transform joint, Vector3 current, Vector3 desired)
    {
        if (current.sqrMagnitude < 0.000001f || desired.sqrMagnitude < 0.000001f) return;
        joint.rotation = Quaternion.FromToRotation(current, desired) * joint.rotation;
    }

    private void PoseHand(string side, Vector3 fingerDirection, Vector3 palmNormal, bool grip)
    {
        bool applied = false;
        bool thumbApplied = false;
        float curlSum = 0f;
        int curledJoints = 0;
        float alignment = 0f;
        SetGripDiagnostics(side, false, false, 0f, 0f);
        if (!bones.TryGetValue("hand" + side, out var hand)) return;
        Restore(hand);
        foreach (string finger in FingerNames)
        {
            Transform joint = FindFingerRoot(finger, side);
            for (int depth = 0; joint != null && depth < 6; depth++, joint = FingerChild(joint)) Restore(joint);
        }

        // The source characters start at Index2/Middle2/Thumb2 and omit ring/pinky.
        // Motion FBXs can start at 1, so discover each actual chain instead of requiring either convention.
        Transform middle = FindFingerRoot("middle", side) ?? FindFingerRoot("index", side);
        Transform index = FindFingerRoot("index", side);
        if (middle == null) return;
        Vector3 forward = middle.position - hand.position;
        if (forward.sqrMagnitude < 0.000001f) return;
        hand.rotation = Quaternion.FromToRotation(forward, fingerDirection) * hand.rotation;
        if (index != null && index != middle)
        {
            // Index-to-middle knuckle spacing supplies a palm width even on the two-finger control rig.
            Vector3 currentNormal = Vector3.Cross(middle.position - hand.position, index.position - middle.position).normalized;
            if (side == "r") currentNormal = -currentNormal;
            Vector3 axis = fingerDirection.normalized;
            Vector3 currentPlane = Vector3.ProjectOnPlane(currentNormal, axis);
            Vector3 desiredPlane = Vector3.ProjectOnPlane(palmNormal, axis);
            if (currentPlane.sqrMagnitude > 0.00001f && desiredPlane.sqrMagnitude > 0.00001f)
            {
                float twist = Vector3.SignedAngle(currentPlane, desiredPlane, axis);
                hand.rotation = Quaternion.AngleAxis(twist, axis) * hand.rotation;
            }
        }
        alignment = Vector3.Dot((middle.position - hand.position).normalized, fingerDirection.normalized);
        // Curl the fingertips toward the strings using each joint's measured child direction.
        // World-space aiming avoids assuming which imported local axis is the flex axis.
        foreach (string finger in FingerNames)
        {
            if (finger == "thumb") continue;
            Transform joint = FindFingerRoot(finger, side);
            for (int depth = 0; joint != null && depth < 4; depth++)
            {
                Transform child = FingerChild(joint);
                if (child == null) break;
                float curl = grip ? 0.62f + depth * 0.49f : 0.32f + depth * 0.34f;
                Vector3 desired = (fingerDirection.normalized * Mathf.Cos(curl) + palmNormal.normalized * Mathf.Sin(curl)).normalized;
                AimJoint(joint, child.position - joint.position, desired);
                if (rest.TryGetValue(joint, out var neutral)) curlSum += Quaternion.Angle(neutral, joint.localRotation);
                curledJoints++;
                joint = child;
            }
        }
        applied = curledJoints > 0;
        Transform thumb = FindFingerRoot("thumb", side);
        if (thumb != null)
        {
            Transform thumbMid = FingerChild(thumb);
            Transform thumbTip = FingerChild(thumbMid);
            if (thumbMid != null)
            {
                // The thumb opposes the fingers on the BACK of the neck, instead of fanning out flat.
                Vector3 contact = grip
                    ? Guitar.TransformPoint(new Vector3(FrettingTarget.localPosition.x + 0.025f, -0.005f, -0.028f))
                    : PickingTarget.position + Guitar.right * 0.018f - Guitar.up * 0.075f - Guitar.forward * 0.028f;
                AimJoint(thumb, thumbMid.position - thumb.position, contact - thumb.position);
                if (thumbTip != null) AimJoint(thumbMid, thumbTip.position - thumbMid.position, contact - thumbMid.position);
                thumbApplied = true;
            }
        }
        SetGripDiagnostics(side, applied, thumbApplied, curledJoints > 0 ? curlSum / curledJoints : 0f, alignment);
    }

    private Transform FindFingerRoot(string finger, string side)
    {
        for (int i = 1; i <= 4; i++)
            if (bones.TryGetValue(finger + i + side, out var bone)) return bone;
        return null;
    }

    private static Transform FingerChild(Transform joint)
    {
        // Following the hierarchy also includes Index4.L_end / Thumb3.L_end, whose keys are not numeric.
        return joint != null && joint.childCount > 0 ? joint.GetChild(0) : null;
    }

    private void SetGripDiagnostics(string side, bool applied, bool thumb, float curl, float alignment)
    {
        if (side == "l")
        {
            LeftGripApplied = applied; LeftThumbApplied = thumb; LeftFingerCurl = curl; LeftPalmAlignment = alignment;
        }
        else
        {
            RightGripApplied = applied; RightThumbApplied = thumb; RightFingerCurl = curl; RightPalmAlignment = alignment;
        }
    }

    private void Restore(Transform bone)
    {
        if (rest.TryGetValue(bone, out var rotation)) bone.localRotation = rotation;
    }

    private Vector3 BonePosition(string key, Vector3 fallback)
    {
        return bones.TryGetValue(key, out var bone) ? pivot.InverseTransformPoint(bone.position) : fallback;
    }

    private float HandError(string side, Vector3 target)
    {
        return bones.TryGetValue("hand" + side, out var hand) ? Vector3.Distance(hand.position, target) : -1f;
    }

    private float ElbowAngle(string side)
    {
        if (!bones.TryGetValue("upperarm" + side, out var upper) || !bones.TryGetValue("lowerarm" + side, out var lower)
            || !bones.TryGetValue("hand" + side, out var hand)) return -1f;
        return Vector3.Angle(upper.position - lower.position, hand.position - lower.position);
    }

    private Transform Anchor(string name, Vector3 position)
    {
        var target = new GameObject(name).transform;
        target.SetParent(Guitar, false);
        target.localPosition = position;
        return target;
    }

    private void CreateStrap()
    {
        var strap = new GameObject("Leather strap over left shoulder", typeof(MeshFilter), typeof(MeshRenderer));
        strap.transform.SetParent(pivot, false);
        strapMesh = new Mesh { name = "Continuous shouldered guitar strap" };
        strapMesh.MarkDynamic();
        int triangle = 0;
        for (int segment = 0; segment < 8; segment++)
        for (int side = 0; side < 4; side++)
        {
            int a = segment * 4 + side;
            int b = segment * 4 + (side + 1) % 4;
            int c = a + 4;
            int d = b + 4;
            strapIndices[triangle++] = a; strapIndices[triangle++] = c; strapIndices[triangle++] = b;
            strapIndices[triangle++] = b; strapIndices[triangle++] = c; strapIndices[triangle++] = d;
        }
        strap.GetComponent<MeshFilter>().sharedMesh = strapMesh;
        strapMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            { name = "Espresso leather guitar strap", color = new Color(0.075f, 0.038f, 0.023f) };
        if (strapMaterial.HasProperty("_Smoothness")) strapMaterial.SetFloat("_Smoothness", 0.32f);
        if (strapMaterial.HasProperty("_Cull")) strapMaterial.SetFloat("_Cull", 0f);
        strap.GetComponent<MeshRenderer>().sharedMaterial = strapMaterial;
        Buckle("Tail strap button", new Vector3(0.48f, -0.025f, -0.012f));
        Buckle("Neck heel strap button", new Vector3(0.025f, 0.13f, -0.015f));
    }

    private void UpdateStrap(Vector3 shoulder, Vector3 body)
    {
        strapPoints[0] = pivot.InverseTransformPoint(Guitar.TransformPoint(new Vector3(0.48f, -0.025f, -0.012f)));
        strapPoints[1] = new Vector3(body.x + 0.04f, body.y + 0.01f, -0.13f);
        strapPoints[2] = new Vector3(-0.015f, shoulder.y - 0.10f, -0.15f);
        strapPoints[3] = new Vector3(shoulder.x - 0.025f, shoulder.y + 0.025f, -0.075f);
        strapPoints[4] = new Vector3(shoulder.x - 0.025f, shoulder.y + 0.085f, shoulder.z - 0.015f);
        strapPoints[5] = new Vector3(shoulder.x - 0.025f, shoulder.y + 0.05f, shoulder.z + 0.08f);
        strapPoints[6] = new Vector3(shoulder.x - 0.005f, shoulder.y - 0.055f, shoulder.z + 0.13f);
        strapPoints[7] = Vector3.Lerp(strapPoints[6], pivot.InverseTransformPoint(Guitar.TransformPoint(new Vector3(0.025f, 0.13f, -0.015f))), 0.52f);
        strapPoints[8] = pivot.InverseTransformPoint(Guitar.TransformPoint(new Vector3(0.025f, 0.13f, -0.015f)));
        for (int i = 0; i < strapPoints.Length; i++)
        {
            Vector3 tangent = (strapPoints[Mathf.Min(i + 1, 8)] - strapPoints[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 width = Vector3.ProjectOnPlane(Vector3.right, tangent).normalized;
            if (width.sqrMagnitude < 0.1f) width = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
            Vector3 depth = Vector3.Cross(width, tangent).normalized;
            for (int corner = 0; corner < 4; corner++)
            {
                float across = corner == 0 || corner == 3 ? -1f : 1f;
                float thick = corner < 2 ? -1f : 1f;
                strapVertices[i * 4 + corner] = strapPoints[i] + width * (0.024f * across) + depth * (0.004f * thick);
                strapUV[i * 4 + corner] = new Vector2(across > 0f ? 1f : 0f, i * 0.25f);
            }
        }
        strapMesh.vertices = strapVertices;
        strapMesh.uv = strapUV;
        strapMesh.triangles = strapIndices;
        strapMesh.RecalculateNormals();
        strapMesh.RecalculateBounds();
    }

    private void Buckle(string name, Vector3 position)
    {
        var button = SimBarVisualPrimitive.Create(PrimitiveType.Cylinder);
        button.name = name;
        button.transform.SetParent(Guitar, false);
        button.transform.localPosition = position;
        button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        button.transform.localScale = new Vector3(0.025f, 0.008f, 0.025f);
        var collider = button.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; DestroyOwned(collider); }
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            { color = new Color(0.42f, 0.36f, 0.24f), name = "Brass strap hardware" };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.75f);
        hardwareMaterials.Add(material);
        button.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void OnDestroy()
    {
        DestroyOwned(strapMesh);
        DestroyOwned(strapMaterial);
        foreach (var material in hardwareMaterials) DestroyOwned(material);
    }

    private static void DestroyOwned(Object instance)
    {
        if (instance == null) return;
        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
    }
}
