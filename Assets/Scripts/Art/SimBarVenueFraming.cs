using UnityEngine;

/// <summary>Fills the display with the bar, dance floor and stage at the original isometric angle.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class SimBarVenueFraming : MonoBehaviour
{
    private const float ReferenceAspect = 16f / 9f;
    private const float ReferenceHalfHeight = 5.5f;
    private Camera venueCamera;
    private int lastWidth, lastHeight;
    private float lastAspect;

    public static void Apply(Camera camera)
    {
        if (camera == null || camera.targetTexture != null) return;
        SimBarVenueFraming framing = camera.GetComponent<SimBarVenueFraming>();
        if (framing == null) framing = camera.gameObject.AddComponent<SimBarVenueFraming>();
        framing.venueCamera = camera;
        framing.Refresh();
    }

    private void LateUpdate()
    {
        if (venueCamera == null) venueCamera = GetComponent<Camera>();
        if (venueCamera == null || venueCamera.targetTexture != null) return;
        if (venueCamera.pixelWidth != lastWidth || venueCamera.pixelHeight != lastHeight
            || !Mathf.Approximately(venueCamera.aspect, lastAspect)) Refresh();
    }

    private void Refresh()
    {
        if (venueCamera == null) return;
        venueCamera.ResetAspect();
        float aspect = Mathf.Max(0.01f, venueCamera.aspect);
        // Cover the reference composition with uniform scaling. Wide screens
        // crop extra foreground/roof rather than revealing empty room edges.
        venueCamera.orthographic = true;
        venueCamera.orthographicSize = ReferenceHalfHeight * Mathf.Min(1f, ReferenceAspect / aspect);
        Vector3 viewOffset = new Vector3(13f, 19.55f, -25.15f);
        Vector3 focus = new Vector3(0.35f, 1.3f, 0.35f);
        venueCamera.transform.SetPositionAndRotation(focus + viewOffset,
            Quaternion.LookRotation(-viewOffset, Vector3.up));
        lastWidth = venueCamera.pixelWidth;
        lastHeight = venueCamera.pixelHeight;
        lastAspect = venueCamera.aspect;
    }
}
