using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

public static class KenneyUIReskin
{
    [MenuItem("GameObject/SimBar UI/Reskin Existing UI to Kenney", false, 20)]
    private static void ReskinExistingUI(MenuCommand menuCommand)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("No Canvas found in scene.");
            return;
        }

        string folder = "Assets/Resources/UI/Kenney/Yellow/Default";
        
        // Try loading sprites directly from texture files
        Sprite panelSprite = LoadSpriteFromFile(folder, "button_rectangle_depth_flat");
        Sprite buttonSprite = LoadSpriteFromFile(folder, "button_rectangle_flat");
        Sprite sliderBg = LoadSpriteFromFile(folder, "slide_horizontal_grey");
        Sprite sliderFill = LoadSpriteFromFile(folder, "slide_horizontal_color_section");
        Sprite arrowW = LoadSpriteFromFile(folder, "arrow_basic_w");
        Sprite arrowE = LoadSpriteFromFile(folder, "arrow_basic_e");
        Sprite iconCircle = LoadSpriteFromFile(folder, "icon_circle");

        Debug.Log($"Sprites loaded - panel: {panelSprite != null}, button: {buttonSprite != null}, sliderBg: {sliderBg != null}, sliderFill: {sliderFill != null}, arrowW: {arrowW != null}, arrowE: {arrowE != null}, iconCircle: {iconCircle != null}");

        int reskinned = 0;

        // Reskin all Images recursively
        Image[] images = canvas.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            string name = img.gameObject.name.ToLower();
            if (name.Contains("panel") || name.Contains("notification"))
            {
                if (panelSprite != null)
                {
                    img.sprite = panelSprite;
                    img.type = Image.Type.Sliced;
                }
                else
                {
                    img.color = new Color(0.22f, 0.18f, 0.26f, 0.95f);
                }
                reskinned++;
            }
            else if (name.Contains("background") || (name.Contains("slider") && !name.Contains("fill")))
            {
                if (sliderBg != null)
                {
                    img.sprite = sliderBg;
                    img.type = Image.Type.Sliced;
                }
                else
                {
                    img.color = new Color(0.28f, 0.24f, 0.32f, 0.9f);
                }
                reskinned++;
            }
            else if (name.Contains("fill") || name.Contains("progress"))
            {
                if (sliderFill != null)
                {
                    img.sprite = sliderFill;
                    img.type = Image.Type.Sliced;
                }
                else
                {
                    img.color = new Color(1f, 0.85f, 0.6f, 0.9f);
                }
                reskinned++;
            }
            else if (name.Contains("button") || name.Contains("prev") || name.Contains("next") || name.Contains("play"))
            {
                if (buttonSprite != null)
                {
                    img.sprite = buttonSprite;
                    img.type = Image.Type.Sliced;
                }
                else
                {
                    img.color = new Color(0.28f, 0.22f, 0.32f, 0.95f);
                }
                reskinned++;
            }
        }

        // Reskin Buttons: replace text with icons
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            string name = btn.gameObject.name.ToLower();
            Sprite icon = null;
            if (name.Contains("prev")) icon = arrowW;
            else if (name.Contains("next")) icon = arrowE;
            else if (name.Contains("play")) icon = iconCircle;

            if (icon != null)
            {
                Text txt = btn.GetComponentInChildren<Text>(true);
                if (txt != null)
                {
                    Object.DestroyImmediate(txt.gameObject);
                }

                GameObject iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(btn.transform, false);
                Image iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = icon;
                RectTransform rt = iconGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                reskinned++;
            }
        }

        // Reskin Text colors
        Text[] texts = canvas.GetComponentsInChildren<Text>(true);
        foreach (Text txt in texts)
        {
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = new Color(1f, 0.95f, 0.85f);
        }

        Debug.Log($"Kenney UI reskin complete. Modified {reskinned} elements.");
    }

    private static Sprite LoadSpriteFromFile(string folder, string fileName)
    {
        string path = Path.Combine(folder, fileName + ".png").Replace("\\", "/");
        if (!File.Exists(path)) return null;

        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(fileData))
        {
            Rect rect = new Rect(0, 0, texture.width, texture.height);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
        }
        return null;
    }
}