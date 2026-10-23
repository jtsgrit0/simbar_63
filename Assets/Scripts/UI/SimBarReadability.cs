using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compatibility surface for older editor checks and saved preferences.
/// The authored f34743e interface owns its fonts, geometry and canvas scaling.
/// </summary>
public sealed class SimBarReadability : MonoBehaviour
{
    public const string TextSizeKey = "SimBar.TextSize";
    private static bool initialized;
    private static int sizeMode;
    public static event Action Changed;

    public static int SizeMode
    {
        get
        {
            if (!initialized)
            {
                sizeMode = Mathf.Clamp(PlayerPrefs.GetInt(TextSizeKey, 0), 0, 2);
                initialized = true;
            }
            return sizeMode;
        }
    }

    // Retain the old preference API without changing the restored interface.
    public static void SetSizeMode(int value)
    {
        int previous = SizeMode;
        sizeMode = Mathf.Clamp(value, 0, 2);
        PlayerPrefs.SetInt(TextSizeKey, sizeMode);
        PlayerPrefs.Save();
        if (previous != sizeMode) Changed?.Invoke();
    }

    public bool IsCompact => false;
    public Vector2 CssViewport => new Vector2(Screen.width, Screen.height);
    public Rect SceneViewportCss => new Rect(0f, 0f, Screen.width, Screen.height);
    public float PixelsPerCssUnit
    {
        get
        {
            Canvas target = GetComponent<Canvas>();
            return target != null ? target.scaleFactor : 1f;
        }
    }

    // No component, controls, camera crop or responsive layout is installed.
    public static void Attach(Canvas target, SimBarExperience experience) { }
    public void RefreshLayout() { }

    public float EffectiveFontCssSize(Text text)
    {
        if (text == null) return 0f;
        Canvas target = text.GetComponentInParent<Canvas>();
        if (target == null) return text.fontSize * Mathf.Abs(text.transform.lossyScale.y);
        float canvasScale = Mathf.Max(0.0001f, Mathf.Abs(target.transform.lossyScale.y));
        return text.fontSize * Mathf.Abs(text.transform.lossyScale.y) / canvasScale * target.scaleFactor;
    }
}
