using UnityEngine;

public class SceneStylePalette : MonoBehaviour
{
    [Header("Palette")]
    public Color wallColor = new Color(0.31f, 0.20f, 0.22f, 1f);
    public Color woodColor = new Color(0.49f, 0.33f, 0.22f, 1f);
    public Color neonCyan = new Color(0.22f, 0.85f, 1f, 1f);
    public Color neonPink = new Color(1f, 0.30f, 0.85f, 1f);
    public Color warmGold = new Color(0.96f, 0.78f, 0.55f, 1f);
    public Color floorColor = new Color(0.16f, 0.11f, 0.12f, 1f);

    [Header("Mood")]
    [Range(0f, 1f)] public float neonIntensity = 0.8f;
    [Range(0f, 1f)] public float warmLightIntensity = 0.7f;
}
