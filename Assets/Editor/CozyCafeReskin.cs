using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public static class CozyCafeReskin
{
    [MenuItem("GameObject/SimBar UI/Apply Cozy Pixel Style", false, 21)]
    private static void ApplyCozyStyle(MenuCommand menuCommand)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("No Canvas found in scene.");
            return;
        }

        // Cozy Cafe / Lofi Cozy Room inspired palette
        Color panelBg = new Color(0.18f, 0.16f, 0.22f, 0.95f);      // Dark cozy purple-brown
        Color buttonNormal = new Color(0.35f, 0.28f, 0.42f, 0.95f);  // Soft purple button
        Color buttonHover = new Color(0.45f, 0.36f, 0.52f, 0.95f);   // Lighter purple hover
        Color buttonPressed = new Color(0.25f, 0.20f, 0.30f, 0.95f); // Darker pressed
        Color sliderBg = new Color(0.28f, 0.24f, 0.32f, 0.9f);       // Slider track
        Color sliderFill = new Color(0.95f, 0.80f, 0.55f, 0.9f);     // Warm amber fill
        Color textPrimary = new Color(1.0f, 0.96f, 0.88f, 1.0f);     // Warm cream text
        Color textSecondary = new Color(0.85f, 0.78f, 0.70f, 1.0f);  // Muted warm text

        int changed = 0;

        // Apply to all Images
        Image[] images = canvas.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            string name = img.gameObject.name.ToLower();
            if (name.Contains("panel") || name.Contains("notification") || name.Contains("background"))
            {
                img.color = panelBg;
                changed++;
            }
            else if (name.Contains("slider") && !name.Contains("fill") && !name.Contains("progress"))
            {
                img.color = sliderBg;
                changed++;
            }
            else if (name.Contains("fill") || name.Contains("progress"))
            {
                img.color = sliderFill;
                changed++;
            }
            else if (name.Contains("button") || name.Contains("prev") || name.Contains("next") || name.Contains("play"))
            {
                img.color = buttonNormal;
                changed++;
            }
        }

        // Apply to all Text
        Text[] texts = canvas.GetComponentsInChildren<Text>(true);
        foreach (Text txt in texts)
        {
            txt.color = textPrimary;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            changed++;
        }

        // Make buttons interactive with hover/pressed colors
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = buttonNormal;
            colors.highlightedColor = buttonHover;
            colors.pressedColor = buttonPressed;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            changed++;
        }

        // Style sliders
        Slider[] sliders = canvas.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            ColorBlock sb = slider.colors;
            sb.normalColor = sliderBg;
            sb.highlightedColor = sliderBg;
            sb.pressedColor = sliderBg;
            sb.disabledColor = sliderBg;
            sb.colorMultiplier = 1f;
            slider.colors = sb;
            
            if (slider.fillRect != null)
            {
                Image fillImg = slider.fillRect.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.color = sliderFill;
                }
            }
            changed++;
        }

        Debug.Log($"Cozy Pixel style applied to {changed} UI elements.");
    }
}