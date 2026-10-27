using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>Uses browser CSS pixels so planning controls stay readable at every DPR.</summary>
public sealed class SimBarBookingLayout : MonoBehaviour
{
    private Canvas target;
    private Action<Vector2> resize;
    private Vector2 previous;
    private float previousRatio;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern float SimBar_GetCanvasPixelRatio();
#endif

    public static SimBarBookingLayout Attach(Canvas canvas, Action<Vector2> onResize)
    {
        var layout = canvas.gameObject.AddComponent<SimBarBookingLayout>();
        layout.target = canvas;
        layout.resize = onResize;
        layout.Fit(true);
        return layout;
    }

    public void RefreshLayout() { Fit(true); }
    private void LateUpdate() { Fit(false); }

    private void Fit(bool force)
    {
        if (target == null || resize == null) return;
        float ratio = 1f;
#if UNITY_WEBGL && !UNITY_EDITOR
        ratio = Mathf.Clamp(SimBar_GetCanvasPixelRatio(), 0.5f, 4f);
#endif
        var pixels = new Vector2(Screen.width, Screen.height);
        if (!force && pixels == previous && Mathf.Approximately(ratio, previousRatio)) return;
        previous = pixels;
        previousRatio = ratio;
        target.scaleFactor = ratio;
        resize(new Vector2(Mathf.Max(240f, pixels.x / ratio), Mathf.Max(240f, pixels.y / ratio)));
    }
}
