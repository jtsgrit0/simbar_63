using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies the Mixamo Standing Jump clip to unanimated, rigged character models
/// placed below a Stage. Static stage props are deliberately ignored.
/// </summary>
public static class StageJumpAnimationApplier
{
    private const string JumpSourcePath = "Assets/Model/Standing Jump.fbx";
    private const string ClipOutputFolder = "Assets/Animation";
    private const string ClipOutputPath = ClipOutputFolder + "/Jump.anim";
    private const string JumpStateName = "MixamoJump";
    private const string ShakeFistSourcePath = "Assets/Animation/Shake Fist.fbx";
    private const string ShakeFistClipOutputPath = ClipOutputFolder + "/ShakeFist.anim";
    private const string ShakeFistStateName = "MixamoShakeFist";
    private const string MixamoRigPath = "CharacterArmature/Root/Hips";

    [MenuItem("SimBar/Apply Mixamo Jump Animation To Stage Models", false, 40)]
    public static void ApplyJumpAnimationToStageModels()
    {
        Stage stage = Object.FindFirstObjectByType<Stage>();
        if (stage == null)
        {
            Debug.LogWarning("[StageJump] No Stage component was found in the open scene.");
            return;
        }

        Transform layoutRoot = FindLayoutRoot(stage.transform);
        int applied = ApplyJumpToLayoutModels(layoutRoot);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log($"[StageJump] Added Jump to {applied} new model(s) and randomized Jump / Shake Fist below '{layoutRoot.name}'.");
    }

    [MenuItem("SimBar/Randomize Jump And Shake Fist", false, 41)]
    public static void RandomizeJumpAndShakeFistFromMenu()
    {
        Stage stage = Object.FindFirstObjectByType<Stage>();
        if (stage == null)
        {
            Debug.LogWarning("[StageJump] No Stage component was found in the open scene.");
            return;
        }

        Transform layoutRoot = FindLayoutRoot(stage.transform);
        int shakeFistCount = RandomizeJumpAndShakeFist(layoutRoot);
        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        Debug.Log($"[StageJump] Randomized {shakeFistCount} model(s) to Shake Fist below '{layoutRoot.name}'.");
    }

    /// <summary>
    /// Used by SimBarLayout after it creates a stage and by the manual menu item.
    /// </summary>
    public static int ApplyJumpToStageChildren(Transform stageRoot, Transform[] performers = null)
    {
        return ApplyJumpToModels(stageRoot, performers);
    }

    /// <summary>
    /// Applies jump to the unanimated character models generated anywhere in a
    /// SimBar layout. This includes the static guest models that are siblings
    /// of the Stage object, rather than direct children of it.
    /// </summary>
    public static int ApplyJumpToLayoutModels(Transform layoutRoot)
    {
        int applied = ApplyJumpToModels(layoutRoot, null);
        RandomizeJumpAndShakeFist(layoutRoot);
        return applied;
    }

    private static int ApplyJumpToModels(Transform searchRoot, Transform[] performers)
    {
        if (searchRoot == null)
            return 0;

        AnimationClip jumpClip = GetOrCreateJumpClip();
        if (jumpClip == null)
        {
            Debug.LogWarning($"[StageJump] The Mixamo jump clip could not be loaded: {JumpSourcePath}");
            return 0;
        }

        int applied = 0;
        foreach (Transform modelRoot in CollectRiggedModelRoots(searchRoot, performers))
        {
            if (HasPlayableAnimation(modelRoot))
                continue;

            if (!CanResolveAllCurves(modelRoot, jumpClip))
            {
                Debug.LogWarning($"[StageJump] Skipped '{modelRoot.name}': its rig does not match the Mixamo jump clip.", modelRoot);
                continue;
            }

            AssignJumpClip(modelRoot.gameObject, jumpClip);
            applied++;
        }

        return applied;
    }

    /// <summary>
    /// Shuffles models previously managed by this tool and assigns roughly half
    /// of them to Shake Fist. The rest continue with Jump. Both states remain
    /// on every model so this command can be safely run again for a new mix.
    /// </summary>
    public static int RandomizeJumpAndShakeFist(Transform searchRoot)
    {
        if (searchRoot == null)
            return 0;

        AnimationClip jumpClip = GetOrCreateJumpClip();
        AnimationClip shakeFistClip = GetOrCreateShakeFistClip();
        if (jumpClip == null || shakeFistClip == null)
        {
            Debug.LogWarning($"[StageJump] Shake Fist randomization was skipped. Expected clip: {ShakeFistSourcePath}");
            return 0;
        }

        var candidates = new List<Transform>();
        foreach (Transform modelRoot in CollectRiggedModelRoots(searchRoot, null))
        {
            Animation animation = modelRoot.GetComponent<Animation>();
            if (animation == null || !IsJumpManaged(animation, jumpClip))
                continue;

            if (!CanResolveAllCurves(modelRoot, shakeFistClip))
            {
                Debug.LogWarning($"[StageJump] Kept Jump on '{modelRoot.name}': its rig does not match Shake Fist.", modelRoot);
                continue;
            }

            AddClipIfMissing(animation, shakeFistClip, ShakeFistStateName);
            candidates.Add(modelRoot);
        }

        int shakeFistCount = 0;
        foreach (Transform candidate in candidates)
        {
            Animation animation = candidate.GetComponent<Animation>();
            if (Random.value > 0.5f)
            {
                AssignManagedClip(animation, shakeFistClip, ShakeFistStateName);
                shakeFistCount++;
            }
            else
            {
                AssignManagedClip(animation, jumpClip, JumpStateName);
            }
        }

        return shakeFistCount;
    }

    private static bool IsJumpManaged(Animation animation, AnimationClip jumpClip)
    {
        return animation.GetClip(JumpStateName) == jumpClip || animation.clip == jumpClip;
    }

    private static void Shuffle(List<Transform> transforms)
    {
        for (int i = transforms.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Transform temporary = transforms[i];
            transforms[i] = transforms[randomIndex];
            transforms[randomIndex] = temporary;
        }
    }

    private static Transform FindLayoutRoot(Transform stageRoot)
    {
        for (Transform current = stageRoot; current != null; current = current.parent)
        {
            if (current.name == "SimBar_CozyRoom")
                return current;
        }

        // A manually created Stage may not be part of the generated layout.
        // In that case retain the original, stage-only behavior.
        return stageRoot;
    }

    private static IEnumerable<Transform> CollectRiggedModelRoots(Transform stageRoot, Transform[] performers)
    {
        var roots = new HashSet<Transform>();

        if (performers != null)
        {
            foreach (Transform performer in performers)
                AddRiggedModelRoot(roots, performer, stageRoot);
        }

        // A SkinnedMeshRenderer identifies a character model without mistaking
        // the stage floor, guitar, lights, or other static props for a target.
        foreach (SkinnedMeshRenderer renderer in stageRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            AddRiggedModelRoot(roots, renderer.transform, stageRoot);

        return roots;
    }

    private static void AddRiggedModelRoot(HashSet<Transform> roots, Transform start, Transform stageRoot)
    {
        Transform modelRoot = FindMixamoModelRoot(start, stageRoot);
        if (modelRoot != null)
            roots.Add(modelRoot);
    }

    private static Transform FindMixamoModelRoot(Transform start, Transform stageRoot)
    {
        // The imported FBX root is the first ancestor whose direct hierarchy
        // contains CharacterArmature/Root/Hips. Applying a legacy clip to a
        // mesh child would make every clip binding miss its target.
        for (Transform current = start; current != null && current != stageRoot; current = current.parent)
        {
            if (current.Find(MixamoRigPath) != null)
                return current;
        }

        return null;
    }

    private static bool HasPlayableAnimation(Transform modelRoot)
    {
        foreach (Animator animator in modelRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController != null)
                return true;
        }

        foreach (Animation animation in modelRoot.GetComponentsInChildren<Animation>(true))
        {
            if (animation.clip != null)
                return true;
        }

        return false;
    }

    private static bool CanResolveAllCurves(Transform modelRoot, AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        if (bindings.Length == 0)
            return false;

        foreach (EditorCurveBinding binding in bindings)
        {
            if (!string.IsNullOrEmpty(binding.path) && modelRoot.Find(binding.path) == null)
                return false;
        }

        return true;
    }

    private static void AssignJumpClip(GameObject modelRoot, AnimationClip jumpClip)
    {
        Animation animation = modelRoot.GetComponent<Animation>();
        if (animation == null)
            animation = Undo.AddComponent<Animation>(modelRoot);

        Undo.RecordObject(animation, "Apply Mixamo Jump Animation");
        AddClipIfMissing(animation, jumpClip, JumpStateName);
        AssignManagedClip(animation, jumpClip, JumpStateName);

        // Some layout-created character objects contain an empty Animator. It
        // cannot play a controller and would otherwise compete with Animation.
        foreach (Animator animator in modelRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController == null && animator.enabled)
            {
                Undo.RecordObject(animator, "Disable Empty Animator For Mixamo Jump");
                animator.enabled = false;
            }
        }

        EditorUtility.SetDirty(modelRoot);
        EditorUtility.SetDirty(animation);
    }

    private static void AddClipIfMissing(Animation animation, AnimationClip clip, string stateName)
    {
        if (animation.GetClip(stateName) == null)
            animation.AddClip(clip, stateName);
    }

    private static void AssignManagedClip(Animation animation, AnimationClip clip, string stateName)
    {
        Undo.RecordObject(animation, "Assign Mixamo Animation");
        AddClipIfMissing(animation, clip, stateName);
        animation.clip = clip;
        animation.playAutomatically = true;
        animation.wrapMode = WrapMode.Loop;
        animation.Play(stateName);
        EditorUtility.SetDirty(animation);
    }

    /// <summary>
    /// Extracts the embedded legacy clip once so each target can use the same
    /// independent, looping asset rather than a clip sub-asset in the FBX.
    /// </summary>
    public static AnimationClip GetOrCreateJumpClip()
    {
        return GetOrCreateLegacyClip(JumpSourcePath, ClipOutputPath, "Jump");
    }

    public static AnimationClip GetOrCreateShakeFistClip()
    {
        return GetOrCreateLegacyClip(ShakeFistSourcePath, ShakeFistClipOutputPath, "ShakeFist");
    }

    private static AnimationClip GetOrCreateLegacyClip(string sourcePath, string outputPath, string clipName)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
        if (existing != null)
        {
            existing.wrapMode = WrapMode.Loop;
            return existing;
        }

        AnimationClip sourceClip = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__"))
            {
                sourceClip = clip;
                break;
            }
        }

        if (sourceClip == null)
            return null;

        if (!Directory.Exists(ClipOutputFolder))
        {
            Directory.CreateDirectory(ClipOutputFolder);
            AssetDatabase.Refresh();
        }

        AnimationClip copy = Object.Instantiate(sourceClip);
        copy.name = clipName;

        // Strip root position curves (usually on the FBX root) so the
        // looping legacy clip does not 'teleport' at the loop boundary.
        RemoveRootPositionCurves(copy);

        copy.legacy = true;
        copy.wrapMode = WrapMode.Loop;
        AssetDatabase.CreateAsset(copy, outputPath);
        AssetDatabase.SaveAssets();
        return copy;
    }

    private static void RemoveRootPositionCurves(AnimationClip clip)
    {
        if (clip == null)
            return;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (EditorCurveBinding binding in bindings)
        {
            string path = binding.path ?? string.Empty;
            string prop = binding.propertyName ?? string.Empty;

            bool isRootPath = string.IsNullOrEmpty(path) || path.StartsWith("CharacterArmature/Root");
            string propLower = prop.ToLower();

            // Remove transform curves (position, rotation, scale) on the
            // FBX root or the CharacterArmature root so the clip won't
            // modify the model's base transform when looping (which can
            // appear as a 'capsule' or collapsed mesh in some builds).
            if (isRootPath && (propLower.Contains("position") || propLower.Contains("rotation") || propLower.Contains("scale")))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }
    }
}