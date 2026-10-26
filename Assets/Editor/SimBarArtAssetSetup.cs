using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Creates derived URP materials and matching performance clips, preserving every original FBX.</summary>
public static class SimBarArtAssetSetup
{
    private const string Output = "Assets/SimBarGenerated/Characters";
    private const string LibraryPath = "Assets/Resources/SimBarArtLibrary.asset";

    [MenuItem("SimBar/Art/Setup FBX Character Library")]
    public static void Setup()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(Output + "/Materials");
        EnsureFolder(Output + "/Animations");
        var library = AssetDatabase.LoadAssetAtPath<SimBarArtLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SimBarArtLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        var entries = new List<SimBarArtLibrary.CharacterAsset>();
        var replacements = new Dictionary<Material, Material>();
        // Standing audiences use different source models from the Punk/Casual2 band.
        Add(entries, replacements, "Casual", "guest", null, 1.82f);
        Add(entries, replacements, "Suit", "guest", null, 1.9f);
        Add(entries, replacements, "Beach", "guest", null, 1.84f);
        Add(entries, replacements, "Worker", "guest", null, 1.86f);
        Add(entries, replacements, "Farmer", "guest", null, 1.83f);
        Add(entries, replacements, "Adventurer", "guest", null, 1.88f);
        Add(entries, replacements, "Casual", "seated", null, 1.82f);
        Add(entries, replacements, "Casual2", "seated", null, 1.88f);
        Add(entries, replacements, "Suit", "seated", null, 1.9f);
        Add(entries, replacements, "Punk", "seated", null, 1.87f);
        Add(entries, replacements, "Suit", "bartender", null, 1.86f);
        Add(entries, replacements, "Punk", "singer", "Singing", 1.88f);
        Add(entries, replacements, "Casual2", "guitarist", "Guitar Playing", 1.88f);
        Add(entries, replacements, "Punk", "bassist", "Guitar Playing", 1.87f);
        Add(entries, replacements, "Punk", "drummer", null, 1.84f);
        library.originalGuitarAssetPath = "Assets/Model/gibson.fbx";
        library.stageGuitar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SimBarArt/Gibson_Stage.fbx")
            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SimBarGenerated/Models/Gibson_Stage.fbx");
        library.legacyWalkingModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Walking.fbx");
        library.legacyWalkingClip = FindClip("Assets/Model/Walking.fbx");
        if (library.stageGuitar != null) CollectMaterials(library.stageGuitar, replacements);
        library.characters = entries.ToArray();
        library.materials = replacements.Select(pair => new SimBarArtLibrary.MaterialReplacement { original = pair.Key, universal = pair.Value }).ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SimBar Art] Created {entries.Count} FBX character roles, {replacements.Count} preserved-color URP materials and rotation-retargeted musician clips. Library: {LibraryPath}. Original FBX import settings and meshes were preserved. Optimized guitar: {(library.stageGuitar != null ? library.stageGuitar.name : "lightweight stage prop")}");
    }

    private static void Add(List<SimBarArtLibrary.CharacterAsset> entries, Dictionary<Material, Material> materials, string modelName, string role, string animationName, float height)
    {
        string path = "Assets/Model/" + modelName + ".fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            Debug.LogWarning("[SimBar Art] Missing character FBX: " + path);
            return;
        }
        var sourceClip = string.IsNullOrEmpty(animationName) ? null : FindClip("Assets/Model/" + animationName + ".fbx");
        var performance = sourceClip != null ? RemapRotations(sourceClip, model, modelName.Replace(" ", "") + "_" + role) : null;
        entries.Add(new SimBarArtLibrary.CharacterAsset
        {
            key = modelName,
            role = role,
            sourceModel = model,
            sourceAnimation = sourceClip,
            performanceAnimation = performance,
            height = height,
            rotation = Vector3.zero
        });
        CollectMaterials(model, materials);
    }

    private static AnimationClip FindClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .OrderByDescending(clip => clip.length).FirstOrDefault();
    }

    private static AnimationClip RemapRotations(AnimationClip source, GameObject target, string name)
    {
        var targetPaths = new Dictionary<string, string>();
        foreach (var bone in target.GetComponentsInChildren<Transform>(true))
        {
            string key = BoneKey(bone.name);
            if (!targetPaths.ContainsKey(key)) targetPaths.Add(key, AnimationUtility.CalculateTransformPath(bone, target.transform));
        }
        var result = new AnimationClip { name = name, legacy = true, frameRate = source.frameRate, wrapMode = WrapMode.Loop };
        int count = 0;
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
            // Position/scale curves from FBX can overwrite normalization and different character proportions.
            // Bone rotations preserve the authored performance on the target's actual skeleton.
            if (!binding.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                && !binding.propertyName.StartsWith("localEulerAngles", StringComparison.Ordinal)) continue;
            string leaf = binding.path.Substring(binding.path.LastIndexOf('/') + 1);
            string key = BoneKey(leaf);
            if (key == "root" || key == "characterarmature" || key == "armature") continue;
            if (!targetPaths.TryGetValue(key, out var targetPath) || string.IsNullOrEmpty(targetPath)) continue;
            var mapped = binding;
            mapped.path = targetPath;
            AnimationUtility.SetEditorCurve(result, mapped, AnimationUtility.GetEditorCurve(source, binding));
            count++;
        }
        if (count == 0)
        {
            Object.DestroyImmediate(result);
            Debug.LogWarning($"[SimBar Art] No matching rotation curves for {target.name}; procedural performer pose will be used.");
            return null;
        }
        result.EnsureQuaternionContinuity();
        string path = Output + "/Animations/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) AssetDatabase.CreateAsset(result, path);
        else
        {
            EditorUtility.CopySerialized(result, existing);
            Object.DestroyImmediate(result);
            EditorUtility.SetDirty(existing);
            result = existing;
        }
        return result;
    }

    private static void CollectMaterials(GameObject model, Dictionary<Material, Material> replacements)
    {
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        foreach (var source in renderer.sharedMaterials)
        {
            if (source == null || replacements.ContainsKey(source)) continue;
            replacements.Add(source, CreateUniversalMaterial(source));
        }
    }

    private static Material CreateUniversalMaterial(Material source)
    {
        string guid;
        long localId;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out localId);
        string safeName = new string(source.name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray());
        string path = Output + "/Materials/" + safeName + "_" + guid + "_" + localId + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result == null)
        {
            result = new Material(shader) { name = source.name + "_SimBarURP" };
            AssetDatabase.CreateAsset(result, path);
        }
        else result.shader = shader;
        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
        else result.color = color;
        CopyTexture(source, result, "_BaseMap", "_MainTex", "_BaseMap");
        CopyTexture(source, result, "_BumpMap", "_BumpMap", "_BumpMap");
        CopyTexture(source, result, "_MetallicGlossMap", "_MetallicGlossMap", "_MetallicGlossMap");
        CopyTexture(source, result, "_OcclusionMap", "_OcclusionMap", "_OcclusionMap");
        CopyTexture(source, result, "_EmissionMap", "_EmissionMap", "_EmissionMap");
        if (source.HasProperty("_EmissionColor") && result.HasProperty("_EmissionColor"))
        {
            var emission = source.GetColor("_EmissionColor");
            result.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0f) result.EnableKeyword("_EMISSION");
        }
        if (result.HasProperty("_BumpMap") && result.GetTexture("_BumpMap") != null) result.EnableKeyword("_NORMALMAP");
        if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 0.24f);
        if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", 0.02f);
        if (result.HasProperty("_Cull")) result.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(result);
        return result;
    }

    private static void CopyTexture(Material source, Material target, string primary, string fallback, string destination)
    {
        if (!target.HasProperty(destination)) return;
        string property = source.HasProperty(primary) && source.GetTexture(primary) != null ? primary : fallback;
        if (!source.HasProperty(property) || source.GetTexture(property) == null) return;
        target.SetTexture(destination, source.GetTexture(property));
        target.SetTextureScale(destination, source.GetTextureScale(property));
        target.SetTextureOffset(destination, source.GetTextureOffset(property));
    }

    private static string BoneKey(string name)
    {
        int colon = name.LastIndexOf(':');
        if (colon >= 0) name = name.Substring(colon + 1);
        return name.ToLowerInvariant().Replace(".", "").Replace("_", "").Replace(" ", "");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
