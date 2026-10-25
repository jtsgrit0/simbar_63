using UnityEngine;

/// <summary>Fits only the illustrated title card inside landscape and portrait viewports.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SimBarOwnerTitleLayout : MonoBehaviour
{
    private const float EdgeMargin = 24f;
    private RectTransform title;
    private RectTransform viewport;
    private Vector2 previousViewport = new Vector2(-1f, -1f);
    private Vector2 previousTitle = new Vector2(-1f, -1f);

    public static void Attach(RectTransform panel)
    {
        if (panel == null) return;
        var layout = panel.GetComponent<SimBarOwnerTitleLayout>();
        if (layout == null) layout = panel.gameObject.AddComponent<SimBarOwnerTitleLayout>();
        layout.CacheRects();
        Canvas.ForceUpdateCanvases();
        layout.RefreshFit();
    }

    private void Awake() => CacheRects();
    private void OnEnable() => RefreshFit();
    private void OnRectTransformDimensionsChange() => RefreshFit();
    private void LateUpdate() => RefreshFit();

    private void CacheRects()
    {
        if (title == null) title = GetComponent<RectTransform>();
        if (viewport == null && title != null) viewport = title.parent as RectTransform;
    }

    public void RefreshFit()
    {
        CacheRects();
        if (title == null || viewport == null) return;
        Vector2 available = viewport.rect.size;
        Vector2 content = title.rect.size;
        if (available == previousViewport && content == previousTitle) return;
        previousViewport = available;
        previousTitle = content;
        if (available.x <= 0f || available.y <= 0f || content.x <= 0f || content.y <= 0f) return;
        float fit = Mathf.Min(1f,
            Mathf.Max(1f, available.x - EdgeMargin * 2f) / content.x,
            Mathf.Max(1f, available.y - EdgeMargin * 2f) / content.y);
        title.localScale = new Vector3(fit, fit, 1f);
    }
}
