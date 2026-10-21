using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>A second camera renders the actual venue rather than a picture of another venue.</summary>
public sealed class SimBarMiniMap : MonoBehaviour
{
    private RenderTexture target;

    public static void Attach(Canvas canvas, Transform environment)
    {
        Transform frame = null;
        foreach (Transform candidate in canvas.GetComponentsInChildren<Transform>(true))
            if (candidate.name == "Mini Map") { frame = candidate; break; }
        if (frame == null) return;
        foreach (Graphic placeholder in frame.GetComponentsInChildren<Graphic>(true))
            if (placeholder.transform != frame) placeholder.enabled = false;
        var cameraObject = new GameObject("Live venue minimap camera");
        cameraObject.transform.SetParent(environment, false);
        cameraObject.transform.localPosition = new Vector3(0f, 26f, 0f);
        cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var owner = cameraObject.AddComponent<SimBarMiniMap>();
        owner.target = new RenderTexture(256, 224, 16, RenderTextureFormat.ARGB32) { name = "SIMBAR actual venue overview" };
        owner.target.Create();
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9.4f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 40f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.10f);
        camera.targetTexture = owner.target;
        camera.depth = -10f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var imageObject = new GameObject("Actual venue overview", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(frame, false);
        var rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * 4f;
        rect.offsetMax = Vector2.one * -4f;
        var image = imageObject.GetComponent<RawImage>();
        image.texture = owner.target;
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (target == null) return;
        target.Release();
        Destroy(target);
    }
}
