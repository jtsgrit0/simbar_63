using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Keeps photographic startup art encoded in the player instead of serializing RGB pixels.</summary>
public static class SimBarWebTextures
{
    public sealed class Definition
    {
        public readonly string resourceKey;
        public readonly string sourcePath;
        public readonly string encodedKey;
        public readonly bool mipmaps;

        public Definition(string resourceKey, string sourcePath, string encodedKey, bool mipmaps)
        {
            this.resourceKey = resourceKey;
            this.sourcePath = sourcePath;
            this.encodedKey = encodedKey;
            this.mipmaps = mipmaps;
        }
    }

    private static readonly Definition[] definitions =
    {
        new Definition("SimBarArt/Owners/OwnerFemale", "Assets/Art/StartupTextures/Owners/OwnerFemale.png", "SimBarWebTextures/OwnerFemale", false),
        new Definition("SimBarArt/Owners/OwnerFF", "Assets/Art/StartupTextures/Owners/OwnerFF.png", "SimBarWebTextures/OwnerFF", false),
        new Definition("SimBarArt/Owners/OwnerPetSounds", "Assets/Art/StartupTextures/Owners/OwnerPetSounds.png", "SimBarWebTextures/OwnerPetSounds", false),
        new Definition("SimBarArt/Seoul/SeoulNeonNight", "Assets/Art/StartupTextures/Seoul/SeoulNeonNight.png", "SimBarWebTextures/SeoulNeonNight", true)
    };
    private static readonly Dictionary<string, Texture2D> decoded = new Dictionary<string, Texture2D>();

    public static IReadOnlyList<Definition> Definitions => definitions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (Texture2D texture in decoded.Values)
        {
            if (texture == null) continue;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
        decoded.Clear();
    }

    public static Texture2D Load(string resourceKey)
    {
        Definition definition = Array.Find(definitions, entry => entry.resourceKey == resourceKey);
        if (definition == null) return Resources.Load<Texture2D>(resourceKey);

#if UNITY_EDITOR
        // Source PNGs stay available for authoring and art verification, outside Resources.
        Texture2D original = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(definition.sourcePath);
        if (original != null) return original;
        original = Resources.Load<Texture2D>(resourceKey);
        if (original != null) return original;
#endif

        if (decoded.TryGetValue(resourceKey, out Texture2D cached) && cached != null) return cached;
        TextAsset image = Resources.Load<TextAsset>(definition.encodedKey);
        if (image == null)
        {
            Debug.LogError("Missing compact startup image: " + definition.encodedKey + ". Prepare startup images before building.");
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGB24, definition.mipmaps, false)
        {
            name = resourceKey.Substring(resourceKey.LastIndexOf('/') + 1),
            wrapMode = TextureWrapMode.Clamp,
            filterMode = definition.mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear
        };
        if (!ImageConversion.LoadImage(texture, image.bytes, true))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError("Could not decode compact startup image: " + definition.encodedKey);
            return null;
        }
        decoded[resourceKey] = texture;
        // The decoded texture is shared by the title backdrop and venue windows.
        Resources.UnloadAsset(image);
        return texture;
    }
}
