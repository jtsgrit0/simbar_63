using System;
using UnityEngine;

/// <summary>References authored art without changing the user's source FBX assets.</summary>
[CreateAssetMenu(menuName = "SimBar/Art Library", fileName = "SimBarArtLibrary")]
public sealed class SimBarArtLibrary : ScriptableObject
{
    [Serializable]
    public sealed class CharacterAsset
    {
        public string key;
        public string role;
        public GameObject sourceModel;
        public AnimationClip sourceAnimation;
        [Tooltip("Rotation-only animation remapped onto this model's exact bone paths.")]
        public AnimationClip performanceAnimation;
        public float height = 1.85f;
        public Vector3 rotation;
    }

    [Serializable]
    public sealed class MaterialReplacement
    {
        public Material original;
        public Material universal;
    }

    public CharacterAsset[] characters = Array.Empty<CharacterAsset>();
    public MaterialReplacement[] materials = Array.Empty<MaterialReplacement>();
    [Tooltip("Source provenance only: keeping a path avoids bundling the 99 MB original as a runtime dependency.")]
    public string originalGuitarAssetPath = "Assets/Model/gibson.fbx";
    [Tooltip("Optional optimized stage guitar. The original high-poly asset is never cloned as a fallback.")]
    public GameObject stageGuitar;

    public CharacterAsset Find(string role, int variant)
    {
        int count = 0;
        foreach (var entry in characters)
            if (entry != null && entry.sourceModel != null && entry.role == role) count++;
        if (count == 0) return null;
        int index = ((variant % count) + count) % count;
        foreach (var entry in characters)
        {
            if (entry == null || entry.sourceModel == null || entry.role != role) continue;
            if (index-- == 0) return entry;
        }
        return null;
    }

    public Material FindMaterial(Material source)
    {
        foreach (var entry in materials)
            if (entry != null && entry.original == source && entry.universal != null) return entry.universal;
        return null;
    }
}
