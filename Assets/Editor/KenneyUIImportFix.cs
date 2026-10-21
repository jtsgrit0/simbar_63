using UnityEngine;
using UnityEditor;
using System.IO;

public static class KenneyUIImportFix
{
    [MenuItem("GameObject/SimBar UI/Reimport Kenney UI Sprites", false, 19)]
    private static void ReimportSprites()
    {
        string folder = "Assets/Resources/UI/Kenney/Yellow/Default";
        if (!Directory.Exists(folder))
        {
            Debug.LogWarning("Folder not found: " + folder);
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.png");
        int count = 0;
        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            if (assetPath.StartsWith("Assets/"))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }

        Debug.Log($"Fixed {count} sprites in {folder}");
    }

    [MenuItem("GameObject/SimBar UI/Fix Art Img Import Settings", false, 20)]
    private static void FixArtImgImportSettings()
    {
        string folder = "Assets/Art/Img";
        if (!Directory.Exists(folder))
        {
            Debug.LogWarning("Folder not found: " + folder);
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
        int fixedCount = 0;

        foreach (string file in files)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".psd")
                continue;

            string assetPath = file.Replace("\\", "/");
            if (!assetPath.StartsWith("Assets/"))
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                continue;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.spritePixelsPerUnit != 100f)
            {
                importer.spritePixelsPerUnit = 100f;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.ToNearest)
            {
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            importer.compressionQuality = 100;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();

            if (changed)
                fixedCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Applied sharp UI import settings to {fixedCount} files under Assets/Art/Img");
    }
}
