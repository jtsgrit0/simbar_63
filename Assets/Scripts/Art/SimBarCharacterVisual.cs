using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>FBX-backed people, grounded in metres, with source performances and fallback bone posing.</summary>
public sealed partial class SimBarCharacterVisual : MonoBehaviour
{
    [Tooltip("Hip height above the caller's floor origin for seated guests.")]
    public float seatHeight = 0.6f;
    private static SimBarArtLibrary library;
    private Transform pivot;
    private GameObject model;
    private AnimationClip clip;
    private string role;
    private string sourceModelKey;
    public string ModelKey => sourceModelKey;
    private int variant;
    private float phase;
    private float standingHipHeight;
    private Transform microphone;
    private Transform leftStick;
    private Transform rightStick;
    private SimBarGuitarPerformance guitarPerformance;
    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<Transform, Quaternion> restRotations = new Dictionary<Transform, Quaternion>();
    private readonly List<Material> ownedMaterials = new List<Material>();

    /// <summary>Creates a visual only; gameplay/colliders remain the caller's responsibility.</summary>
    public static GameObject Create(string role, Transform parent, Vector3 localPosition, Color outfit, int variant = 0)
    {
        if (library == null) library = Resources.Load<SimBarArtLibrary>("SimBarArtLibrary");
        var entry = library != null ? library.Find(role == "barseated" ? "seated" : role, variant) : null;
        // A missing art library is an explicit null so callers may retain their existing fallback.
        if (entry == null) return null;
        var root = new GameObject("FBX_" + role + "_" + entry.key);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        var visual = root.AddComponent<SimBarCharacterVisual>();
        visual.Initialize(entry, role, outfit, variant);
        return root;
    }

    private void Initialize(SimBarArtLibrary.CharacterAsset entry, string characterRole, Color outfit, int characterVariant)
    {
        role = characterRole;
        sourceModelKey = entry.key;
        if (role == "barseated") seatHeight = 1.12f;
        variant = characterVariant;
        phase = characterVariant * 1.618f + (role == "guest" ? 0.3f : 1.1f);
        pivot = new GameObject("Performance").transform;
        pivot.SetParent(transform, false);
        var normalization = new GameObject("FBX_Normalization").transform;
        normalization.SetParent(pivot, false);
        model = Instantiate(entry.sourceModel, normalization, false);
        model.name = entry.key + "_SourceFBX";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(entry.rotation);

        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var animation in model.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

        if (TryBounds(normalization, out var bounds) && bounds.size.y > 0.0001f)
        {
            float scale = entry.height / bounds.size.y;
            normalization.localScale = Vector3.one * scale;
            normalization.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
        }
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null) continue;
                var replacement = library.FindMaterial(source);
                materials[i] = replacement != null ? replacement : ConvertMaterial(source);
                // Only the head's red hair slots change for the reference vocalist. Skin, eyes and clothes stay authored.
                if (role == "singer" && renderer.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0
                    && source.name.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var pinkHair = new Material(materials[i]) { name = "Vocalist_PinkHair_" + source.name };
                    Color pink = source.name.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0
                        ? new Color(0.66f, 0.025f, 0.27f) : new Color(0.98f, 0.045f, 0.46f);
                    if (pinkHair.HasProperty("_BaseColor")) pinkHair.SetColor("_BaseColor", pink);
                    else pinkHair.color = pink;
                    ownedMaterials.Add(pinkHair);
                    materials[i] = pinkHair;
                }
            }
            renderer.sharedMaterials = materials;
            if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
        }
        ApplyAudienceAppearance(outfit);
        foreach (var bone in model.GetComponentsInChildren<Transform>(true))
        {
            string key = BoneKey(bone.name);
            if (!bones.ContainsKey(key)) bones.Add(key, bone);
            restRotations[bone] = bone.localRotation;
        }
        clip = entry.performanceAnimation;
        // The FBX's full-body rotation curves are sampled on its own exact skeleton, never on the scaled wrapper.
        if (clip != null) clip.SampleAnimation(model, Mathf.Repeat(phase, Mathf.Max(0.01f, clip.length)));
        else ApplyProceduralPose(0f);
        if ((role == "seated" || role == "barseated") && bones.TryGetValue("upperlegl", out var hip))
        {
            standingHipHeight = pivot.InverseTransformPoint(hip.position).y;
            pivot.localPosition = new Vector3(0f, seatHeight - standingHipHeight, 0f);
        }
        CreateAccessories(outfit);
        UpdateAccessories(0f, 0.65f);
    }

    private Material ConvertMaterial(Material source)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var result = new Material(shader) { name = source.name + "_RuntimeURP" };
        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
        else result.color = color;
        if (texture != null)
        {
            if (result.HasProperty("_BaseMap")) result.SetTexture("_BaseMap", texture);
            else result.mainTexture = texture;
        }
        if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 0.26f);
        ownedMaterials.Add(result);
        return result;
    }

    private void LateUpdate()
    {
        // Scene previews retain animation without a host. The game takes full
        // ownership so a paused shift freezes bones and held props together.
        if (!Managed) ApplyPresentation(Time.time, 0.65f, 0f, 0f);
    }

    private void ApplyProceduralPose(float t)
    {
        bool guitar = role == "guitarist" || role == "bassist";
        if (guitar)
        {
            Aim("upperarml", "lowerarml", new Vector3(-0.65f, -0.3f, 0.65f));
            Aim("lowerarml", "handl", new Vector3(-0.55f, 0.4f, 0.65f));
            Aim("upperarmr", "lowerarmr", new Vector3(0.32f, -0.8f, 0.55f));
            Aim("lowerarmr", "handr", new Vector3(-0.55f, 0.1f + Mathf.Sin(t * 3f) * 0.2f, 0.6f));
        }
        else if (role == "singer")
        {
            Aim("upperarmr", "lowerarmr", new Vector3(0.4f, -0.5f, 0.6f));
            Aim("lowerarmr", "handr", new Vector3(-0.1f, 0.9f, 0.25f));
            Aim("upperarml", "lowerarml", new Vector3(-0.7f, 0.1f + Mathf.Sin(t) * 0.25f, 0.2f));
            Aim("lowerarml", "handl", new Vector3(-0.25f, 0.6f, 0.3f));
        }
        else if (role == "drummer")
        {
            Aim("upperarml", "lowerarml", new Vector3(-0.4f, -0.6f, 0.55f));
            Aim("upperarmr", "lowerarmr", new Vector3(0.4f, -0.6f, 0.55f));
            Aim("lowerarml", "handl", new Vector3(0f, 0.2f + Mathf.Sin(t * 4f) * 0.6f, 1f));
            Aim("lowerarmr", "handr", new Vector3(0f, 0.2f - Mathf.Sin(t * 4f) * 0.6f, 1f));
            Aim("upperlegl", "lowerlegl", new Vector3(-0.15f, -0.15f, 1f));
            Aim("upperlegr", "lowerlegr", new Vector3(0.15f, -0.15f, 1f));
            Aim("lowerlegl", "footl", Vector3.down);
            Aim("lowerlegr", "footr", Vector3.down);
        }
        else if (role == "guest")
        {
            Aim("upperarml", "lowerarml", new Vector3(-0.10f, -1f, 0.035f));
            Aim("upperarmr", "lowerarmr", new Vector3(0.12f, -1f, 0.025f));
            Aim("lowerarml", "handl", new Vector3(0.025f, -1f, 0.13f));
            Aim("lowerarmr", "handr", new Vector3(-0.02f, -1f, 0.11f));
        }
        else
        {
            Aim("upperarml", "lowerarml", new Vector3(-0.15f, -1f, 0.1f));
            Aim("upperarmr", "lowerarmr", new Vector3(0.15f, -1f, 0.15f));
            Aim("lowerarml", "handl", new Vector3(-0.1f, -0.5f, 0.65f));
            Aim("lowerarmr", "handr", new Vector3(0.1f, -0.4f, 0.65f));
        }
        if (role == "seated")
        {
            Aim("upperlegl", "lowerlegl", new Vector3(-0.1f, -0.05f, 1f));
            Aim("upperlegr", "lowerlegr", new Vector3(0.1f, -0.05f, 1f));
            Aim("lowerlegl", "footl", new Vector3(0f, -1f, 0.08f));
            Aim("lowerlegr", "footr", new Vector3(0f, -1f, 0.08f));
        }
        else if (role == "barseated") ApplyBarSeatedLegPose();
    }

    private void Aim(string boneName, string childName, Vector3 localDirection)
    {
        if (!bones.TryGetValue(boneName, out var bone) || !bones.TryGetValue(childName, out var child)) return;
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.00000001f) return;
        bone.rotation = Quaternion.FromToRotation(current, pivot.TransformDirection(localDirection.normalized)) * bone.rotation;
    }

    private void CreateAccessories(Color outfit)
    {
        if (role == "singer")
        {
            microphone = Primitive("Vocal microphone", PrimitiveType.Cylinder, new Vector3(0.045f, 0.11f, 0.045f), new Color(0.09f, 0.1f, 0.13f));
            var grille = Primitive("Microphone grille", PrimitiveType.Sphere, Vector3.one * 0.075f, new Color(0.56f, 0.6f, 0.65f));
            grille.SetParent(microphone, false);
            grille.localPosition = new Vector3(0f, 0.55f, 0f);
            grille.localScale = new Vector3(1.65f, 0.38f, 1.65f);
        }
        else if (role == "guitarist" || role == "bassist")
        {
            var instrument = new GameObject(role == "bassist" ? "Stage bass guitar" : "Stage electric guitar").transform;
            instrument.SetParent(pivot, false);
            GameObject art;
            if (library.stageGuitar != null)
            {
                art = Instantiate(library.stageGuitar, instrument, false);
                foreach (var renderer in art.GetComponentsInChildren<Renderer>())
                {
                    var shared = renderer.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++) if (shared[i] != null) shared[i] = library.FindMaterial(shared[i]) ?? ConvertMaterial(shared[i]);
                    renderer.sharedMaterials = shared;
                }
            }
            else
            {
                art = new GameObject("Lightweight guitar geometry");
                art.transform.SetParent(instrument, false);
                CreateLightweightGuitar(art.transform, outfit);
                // Match the measured FBX convention: length X, neck -X, audience-facing surface +Z.
                art.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            if (TryBounds(instrument, out var b) && b.size.magnitude > 0.001f)
            {
                float length = role == "bassist" ? 1.18f : 1.12f;
                float scale = length / Mathf.Max(b.size.x, b.size.y, b.size.z);
                art.transform.localScale *= scale;
                art.transform.localPosition = (art.transform.localPosition - b.center) * scale;
            }
            guitarPerformance = gameObject.AddComponent<SimBarGuitarPerformance>();
            guitarPerformance.Initialize(pivot, instrument, bones, restRotations, role == "bassist");
        }
        else if (role == "drummer")
        {
            leftStick = Primitive("Left drumstick", PrimitiveType.Cylinder, new Vector3(0.014f, 0.23f, 0.014f), new Color(0.78f, 0.58f, 0.31f));
            rightStick = Primitive("Right drumstick", PrimitiveType.Cylinder, new Vector3(0.014f, 0.23f, 0.014f), new Color(0.78f, 0.58f, 0.31f));
        }
        else if (role == "bartender") CreateServingAccessory();
        else if (role == "barseated") CreateDrinkingAccessory();
    }

    private void CreateLightweightGuitar(Transform instrument, Color color)
    {
        // The 99 MB source Gibson stays in the library as provenance; this inexpensive stage prop has real strings and hardware.
        AddPart(instrument, "Lacquer lower bout", PrimitiveType.Sphere, new Vector3(0.02f, -0.06f, 0f), new Vector3(0.43f, 0.48f, 0.09f), color);
        AddPart(instrument, "Lacquer upper bout", PrimitiveType.Sphere, new Vector3(-0.01f, 0.16f, 0f), new Vector3(0.31f, 0.33f, 0.085f), color);
        AddPart(instrument, "Maple neck", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.075f, role == "bassist" ? 0.78f : 0.62f, 0.045f), new Color(0.24f, 0.13f, 0.07f));
        AddPart(instrument, "Headstock", PrimitiveType.Cube, new Vector3(0f, 0.89f, 0f), new Vector3(0.11f, 0.19f, 0.055f), color);
        AddPart(instrument, "Pickguard", PrimitiveType.Sphere, new Vector3(0.05f, 0.07f, 0.053f), new Vector3(0.24f, 0.31f, 0.012f), new Color(0.91f, 0.87f, 0.73f));
        for (int i = 0; i < 6; i++)
            AddPart(instrument, "String " + i, PrimitiveType.Cube, new Vector3((i - 2.5f) * 0.009f, 0.4f, 0.061f), new Vector3(0.002f, 0.88f, 0.002f), new Color(0.7f, 0.72f, 0.73f));
        for (int i = 0; i < 3; i++)
            AddPart(instrument, "Chrome pickup " + i, PrimitiveType.Cube, new Vector3(0f, -0.08f + i * 0.09f, 0.068f), new Vector3(0.13f, 0.025f, 0.025f), new Color(0.22f, 0.24f, 0.27f));
    }

    private void UpdateAccessories(float timeSeconds, float energy)
    {
        if (guitarPerformance != null) guitarPerformance.Apply(timeSeconds + phase * 0.025f, energy);
        if (microphone != null && bones.TryGetValue("handr", out var hand))
        {
            microphone.position = hand.position;
            Vector3 head = bones.TryGetValue("head", out var headBone) ? headBone.position : pivot.TransformPoint(new Vector3(0f, 1.65f, 0f));
            microphone.rotation = Quaternion.FromToRotation(Vector3.up, (head - hand.position).normalized);
        }
        PositionStick(leftStick, "handl", leftDrumLift);
        PositionStick(rightStick, "handr", rightDrumLift);
        UpdateServingAccessory();
        UpdateDrinkingAccessory();
    }

    private void PositionStick(Transform stick, string handName, float lift)
    {
        if (stick == null || !bones.TryGetValue(handName, out var hand)) return;
        Vector3 direction = (pivot.forward * Mathf.Lerp(1f, 0.25f, lift) + pivot.up * Mathf.Lerp(-0.12f, 1.25f, lift)).normalized;
        stick.position = hand.position + direction * 0.14f;
        stick.rotation = Quaternion.FromToRotation(Vector3.up, direction);
    }

    private Transform Primitive(string name, PrimitiveType type, Vector3 scale, Color color)
    {
        var go = SimBarVisualPrimitive.Create(type);
        go.name = name;
        go.transform.SetParent(pivot, false);
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; DestroyOwned(collider); }
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        ownedMaterials.Add(material);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    private void AddPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        var part = Primitive(name, type, scale, color);
        part.SetParent(parent, false);
        part.localPosition = position;
    }

    public static bool TryBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Bounds local = renderer.localBounds;
            // FBX skin.localBounds can be an importer culling box, rather than its rendered geometry.
            // These FBXs have 100x mesh/armature transforms. On this project's Unity importer,
            // BakeMesh(false) already returns 100x vertices; multiplying by localToWorld repeats that scale.
            // BakeMesh(true) was measured to return mesh-local vertices (Punk head extents ~0.01),
            // so localToWorld below applies the imported 100x scale exactly once.
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                if (baked.vertexCount == 0) { DestroyOwned(baked); continue; }
                baked.RecalculateBounds();
                local = baked.bounds;
                DestroyOwned(baked);
            }
            Matrix4x4 matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        return found;
    }

    private static string BoneKey(string name)
    {
        int colon = name.LastIndexOf(':');
        if (colon >= 0) name = name.Substring(colon + 1);
        return name.ToLowerInvariant().Replace(".", "").Replace("_", "").Replace(" ", "");
    }

    private void OnDestroy()
    {
        foreach (var material in ownedMaterials) if (material != null) DestroyOwned(material);
    }

    private static void DestroyOwned(UnityEngine.Object instance)
    {
        if (instance == null) return;
        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
    }
}
