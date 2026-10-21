using UnityEngine;
using UnityEditor;
using System.IO;

public static class KenneyUIFullReimport
{
    [MenuItem("GameObject/SimBar UI/Force Reimport Kenney Sprites", false, 18)]
    private static void ForceReimport()
    {
        string folder = "Assets/Resources/UI/Kenney/Yellow/Default";
        if (!Directory.Exists(folder))
        {
            Debug.LogWarning("Folder not found: " + folder);
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
        Debug.Log($"Found {files.Length} PNG files in {folder}");

        int imported = 0;
        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            if (assetPath.StartsWith("Assets/"))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                    imported++;
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"Force reimported and set {imported} sprites as Sprite type");

        // Verify
        string testPath = folder + "/button_rectangle_depth_flat.png";
        Sprite testSprite = AssetDatabase.LoadAssetAtPath<Sprite>(testPath);
        Debug.Log($"Verification - button_rectangle_depth_flat.png loaded: {testSprite != null}");
    }
}
