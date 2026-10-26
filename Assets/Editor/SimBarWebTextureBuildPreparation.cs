using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Exports source art at its imported dimensions without bundling uncompressed RGB textures.</summary>
public sealed class SimBarWebTextureBuildPreparation : IPreprocessBuildWithReport
{
    private const int JpegQuality = 94;
    private const string ExporterVersion = "2";
    private const string CacheDirectory = "Library/SimBarWebTextures";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report) => Prepare();

    [MenuItem("SimBar/Build/Prepare Compact Startup Images")]
    public static void Prepare()
    {
        Directory.CreateDirectory(CacheDirectory);
        foreach (SimBarWebTextures.Definition definition in SimBarWebTextures.Definitions)
            PrepareImage(definition);
    }

    private static void PrepareImage(SimBarWebTextures.Definition definition)
    {
        var importer = AssetImporter.GetAtPath(definition.sourcePath) as TextureImporter;
        if (importer == null)
            throw new BuildFailedException("Startup image source is missing: " + definition.sourcePath);
        string target = "Assets/Resources/" + definition.encodedKey + ".bytes";
        string cache = Path.Combine(CacheDirectory, Path.GetFileName(target) + ".cache");
        string inputVersion = ExporterVersion + ":" + JpegQuality + ":" + AssetDatabase.GetAssetDependencyHash(definition.sourcePath);
        if (File.Exists(target) && File.Exists(cache)
            && File.ReadAllText(cache) == inputVersion + ":" + FileHash(target)) return;

        bool wasReadable = importer.isReadable;
        Texture2D copy = null;
        Texture2D check = null;
        byte[] encoded = null;
        int width = 0, height = 0;
        double meanError = 0d;
        bool preserveAlpha = false;
        try
        {
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(definition.sourcePath);
            if (source == null) throw new BuildFailedException("Could not read startup image: " + definition.sourcePath);
            width = source.width;
            height = source.height;
            Color32[] originalPixels = source.GetPixels32();
            preserveAlpha = originalPixels.Any(pixel => pixel.a != 255);
            // Copy raw imported pixels on the CPU: no render-texture gamma conversion or resizing.
            copy = new Texture2D(width, height, preserveAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, false);
            copy.SetPixels32(originalPixels);
            copy.Apply(false, false);
            encoded = preserveAlpha ? ImageConversion.EncodeToPNG(copy) : ImageConversion.EncodeToJPG(copy, JpegQuality);
            if (encoded == null || encoded.Length == 0)
                throw new BuildFailedException("Could not encode startup image: " + definition.sourcePath);

            check = new Texture2D(2, 2, TextureFormat.RGB24, false, false);
            if (!ImageConversion.LoadImage(check, encoded) || check.width != width || check.height != height)
                throw new BuildFailedException("Compact startup image changed dimensions: " + definition.sourcePath);
            Color32[] compressedPixels = check.GetPixels32();
            long absoluteError = 0;
            for (int i = 0; i < originalPixels.Length; i++)
            {
                absoluteError += Math.Abs(originalPixels[i].r - compressedPixels[i].r);
                absoluteError += Math.Abs(originalPixels[i].g - compressedPixels[i].g);
                absoluteError += Math.Abs(originalPixels[i].b - compressedPixels[i].b);
                if (originalPixels[i].a != compressedPixels[i].a)
                    throw new BuildFailedException("Compact startup image changed transparency: " + definition.sourcePath);
            }
            meanError = absoluteError / (originalPixels.Length * 3d * 255d);
            if (meanError > 0.025d)
                throw new BuildFailedException("Compact startup image exceeds the color error budget: " + definition.sourcePath);
        }
        finally
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            if (check != null) UnityEngine.Object.DestroyImmediate(check);
            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target));
        bool changed = !File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(encoded);
        if (changed)
        {
            File.WriteAllBytes(target, encoded);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
        }
        File.WriteAllText(cache, inputVersion + ":" + FileHash(target));
        Debug.Log("[SimBar Web Art] " + definition.resourceKey + ": " + width + "x" + height
            + ", " + encoded.Length.ToString("N0") + (preserveAlpha ? " PNG" : " JPEG")
            + " bytes, mean RGB error " + (meanError * 100d).ToString("F3") + "%.");
    }

    private static string FileHash(string path)
    {
        using (SHA256 algorithm = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
    }
}
