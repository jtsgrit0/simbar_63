using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>Shows the selected pixel interior with live actor markers, or the original camera overview.</summary>
public sealed class SimBarMiniMap : MonoBehaviour
{
    private RenderTexture target;
    private Camera overviewCamera;
    private RawImage overviewImage,pixelPlate;
    private RectTransform pixelRoot;
    private readonly List<Image> pixelMarkers=new List<Image>();
    private RectTransform pixelPropsRoot;
    private readonly List<RawImage> pixelProps=new List<RawImage>();

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
        owner.overviewCamera = camera;
        camera.cullingMask &= ~SimBarNightBackdrop.LayerMask;
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
        owner.overviewImage = image;
    }

    /// <summary>Use the chosen pixel room, or pass null to retain the original 3D overview.</summary>
    public void BeginPixelOverview(Texture venue)
    {
        if(overviewImage==null)return;
        if(venue==null){RestoreCameraOverview();return;}
        if(pixelRoot==null)
        {
            pixelRoot=new GameObject("Selected pixel venue overview",typeof(RectTransform)).GetComponent<RectTransform>();
            pixelRoot.SetParent(overviewImage.transform,false);
            pixelRoot.anchorMin=Vector2.zero;pixelRoot.anchorMax=Vector2.one;pixelRoot.offsetMin=pixelRoot.offsetMax=Vector2.zero;
            var plateObject=new GameObject("Selected pixel venue plate",typeof(RectTransform),typeof(RawImage));
            plateObject.transform.SetParent(pixelRoot,false);pixelPlate=plateObject.GetComponent<RawImage>();pixelPlate.raycastTarget=false;
            pixelPlate.rectTransform.anchorMin=pixelPlate.rectTransform.anchorMax=new Vector2(.5f,.5f);
            pixelPlate.rectTransform.pivot=new Vector2(.5f,.5f);pixelPlate.rectTransform.anchoredPosition=Vector2.zero;
        }
        overviewImage.enabled=false;if(overviewCamera!=null)overviewCamera.enabled=false;
        if(!pixelRoot.gameObject.activeSelf)pixelRoot.gameObject.SetActive(true);
        if(pixelPlate.texture!=venue)pixelPlate.texture=venue;
        Rect bounds=overviewImage.rectTransform.rect;float scale=Mathf.Min(bounds.width/960f,bounds.height/558f);
        pixelPlate.rectTransform.sizeDelta=new Vector2(960f,558f)*scale;
    }

    public void SetPixelMarker(int index,Vector2 point,Color color)
    {
        if(pixelPlate==null||!pixelRoot.gameObject.activeSelf)return;
        while(pixelMarkers.Count<=index)
        {
            var markerObject=new GameObject("Live venue actor "+pixelMarkers.Count,typeof(RectTransform),typeof(Image));
            markerObject.transform.SetParent(pixelPlate.transform,false);var marker=markerObject.GetComponent<Image>();marker.raycastTarget=false;
            marker.rectTransform.anchorMin=marker.rectTransform.anchorMax=new Vector2(0f,1f);marker.rectTransform.pivot=new Vector2(.5f,.5f);
            marker.rectTransform.sizeDelta=new Vector2(3f,3f);pixelMarkers.Add(marker);
        }
        Image dot=pixelMarkers[index];if(!dot.gameObject.activeSelf)dot.gameObject.SetActive(true);
        Vector2 size=pixelPlate.rectTransform.sizeDelta;
        dot.rectTransform.anchoredPosition=new Vector2(point.x/960f*size.x,-point.y/558f*size.y);dot.color=color;
    }

    public void SetPixelProp(int index,Texture texture,Vector2 point,Vector2 drawSize,Vector2 foot)
    {
        if(pixelPlate==null||!pixelRoot.gameObject.activeSelf)return;
        if(pixelPropsRoot==null)
        {
            pixelPropsRoot=new GameObject("Selected venue furniture",typeof(RectTransform)).GetComponent<RectTransform>();
            pixelPropsRoot.SetParent(pixelPlate.transform,false);pixelPropsRoot.anchorMin=Vector2.zero;pixelPropsRoot.anchorMax=Vector2.one;
            pixelPropsRoot.offsetMin=pixelPropsRoot.offsetMax=Vector2.zero;pixelPropsRoot.SetAsFirstSibling();
        }
        while(pixelProps.Count<=index)
        {
            var image=PixelImageForProp(pixelPropsRoot,pixelProps.Count);pixelProps.Add(image);
        }
        RawImage prop=pixelProps[index];if(!prop.gameObject.activeSelf)prop.gameObject.SetActive(true);
        prop.texture=texture;Vector2 size=pixelPlate.rectTransform.sizeDelta;
        prop.rectTransform.anchoredPosition=new Vector2((point.x-foot.x)/960f*size.x,-(point.y-foot.y)/558f*size.y);
        prop.rectTransform.sizeDelta=new Vector2(drawSize.x/960f*size.x,drawSize.y/558f*size.y);
    }

    private static RawImage PixelImageForProp(Transform parent,int index)
    {
        var propObject=new GameObject("Venue furniture "+index,typeof(RectTransform),typeof(RawImage));
        propObject.transform.SetParent(parent,false);RawImage prop=propObject.GetComponent<RawImage>();prop.raycastTarget=false;
        prop.rectTransform.anchorMin=prop.rectTransform.anchorMax=new Vector2(0f,1f);prop.rectTransform.pivot=new Vector2(0f,1f);
        return prop;
    }

    public void EndPixelProps(int visibleCount)
    {
        for(int i=visibleCount;i<pixelProps.Count;i++)if(pixelProps[i].gameObject.activeSelf)pixelProps[i].gameObject.SetActive(false);
    }

    public void EndPixelOverview(int visibleCount)
    {
        for(int i=visibleCount;i<pixelMarkers.Count;i++)if(pixelMarkers[i].gameObject.activeSelf)pixelMarkers[i].gameObject.SetActive(false);
    }

    private void RestoreCameraOverview()
    {
        if(pixelRoot!=null&&pixelRoot.gameObject.activeSelf)pixelRoot.gameObject.SetActive(false);
        if(overviewImage!=null)overviewImage.enabled=true;
        if(overviewCamera!=null)overviewCamera.enabled=true;
    }

    private void OnDisable(){RestoreCameraOverview();}

    private void OnDestroy()
    {
        if(pixelRoot!=null)Destroy(pixelRoot.gameObject);pixelMarkers.Clear();pixelProps.Clear();pixelPropsRoot=null;
        if (target == null) return;
        target.Release();
        Destroy(target);
    }
}
