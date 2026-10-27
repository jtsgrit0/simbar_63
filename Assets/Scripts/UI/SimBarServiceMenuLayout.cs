using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Keeps the menu drawer readable in browser CSS pixels, with a scrolling body.</summary>
public sealed class SimBarServiceMenuLayout : MonoBehaviour
{
    private Canvas targetCanvas;
    private RectTransform drawer;
    private Vector2 previousSize;
    private float previousRatio;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern float SimBar_GetCanvasPixelRatio();
#endif

    public static void Attach(Canvas target, RectTransform panel)
    {
        var layout = target.gameObject.AddComponent<SimBarServiceMenuLayout>();
        layout.targetCanvas = target; layout.drawer = panel;
        layout.Fit();
    }

    private void OnEnable() => Fit();
    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (targetCanvas == null || drawer == null) return;
        float ratio = 1f;
#if UNITY_WEBGL && !UNITY_EDITOR
        ratio = Mathf.Clamp(SimBar_GetCanvasPixelRatio(), 0.5f, 4f);
#endif
        var size = new Vector2(Screen.width, Screen.height);
        if (size == previousSize && Mathf.Approximately(ratio, previousRatio)) return;
        previousSize = size; previousRatio = ratio;
        targetCanvas.scaleFactor = ratio;
        drawer.anchorMin = drawer.anchorMax = new Vector2(1f, 0.5f);
        drawer.pivot = new Vector2(1f, 0.5f);
        drawer.sizeDelta = new Vector2(Mathf.Min(430f, size.x / ratio - 24f), Mathf.Max(180f, size.y / ratio - 24f));
        drawer.anchoredPosition = new Vector2(-12f, 0f);
    }
}
