using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A single, non-interactive Seoul night image behind the playable venue.</summary>
public sealed class SimBarNightBackdrop : MonoBehaviour
{
    public const string RootName = "Seoul Night District Backdrop";
    public const string PanoramaResource = "SimBarArt/Seoul/SeoulNightDistricts";
    // This otherwise unused user layer keeps the image out of the venue minimap.
    public const int BackdropLayer = 30;
    public const int LayerMask = 1 << BackdropLayer;

    private Camera viewCamera;
    private Material ownedMaterial;
    private Mesh ownedMesh;
    private float imageAspect;
    private float fittedAspect = -1f;

    public static void Build(Transform environment, Camera camera)
    {
        // Authored scenes remain untouched; the world root also owns lobby visibility.
        if (!Application.isPlaying || environment == null || camera == null
            || environment.Find(RootName) != null) return;

        var root = new GameObject(RootName) { layer = BackdropLayer };
        root.transform.SetParent(environment, false);
        // Start waits while the title keeps this world root inactive, avoiding a
        // hidden city-image decode on the initial lobby frame.
        root.AddComponent<SimBarNightBackdrop>().viewCamera = camera;
    }

    private void Start()
    {
        if (viewCamera == null)
        {
            Destroy(gameObject);
            return;
        }

        Texture2D panorama = SimBarWebTextures.Load(PanoramaResource);
        Material template = Resources.Load<Material>("SimBarArt/Posters/PosterUnlit");
        if (panorama == null || template == null)
        {
            Debug.LogWarning("SimBar night backdrop requires the district panorama and PosterUnlit resource material.");
            Destroy(gameObject);
            return;
        }

        Initialize(panorama, template);
    }

    private void Initialize(Texture2D panorama, Material template)
    {
        viewCamera.cullingMask |= LayerMask;
        imageAspect = panorama.width / Mathf.Max(1f, panorama.height);

        // Reuse a referenced URP shader so player shader stripping cannot lose it.
        ownedMaterial = new Material(template)
        {
            name = "SimBar Seoul Night District Background",
            renderQueue = (int)RenderQueue.Background
        };
        if (ownedMaterial.HasProperty("_BaseMap")) ownedMaterial.SetTexture("_BaseMap", panorama);
        if (ownedMaterial.HasProperty("_MainTex")) ownedMaterial.SetTexture("_MainTex", panorama);
        if (ownedMaterial.HasProperty("_BaseColor")) ownedMaterial.SetColor("_BaseColor", Color.white);
        if (ownedMaterial.HasProperty("_Color")) ownedMaterial.SetColor("_Color", Color.white);
        if (ownedMaterial.HasProperty("_ZWrite")) ownedMaterial.SetFloat("_ZWrite", 0f);
        ownedMaterial.SetShaderPassEnabled("ShadowCaster", false);
        ownedMaterial.SetShaderPassEnabled("DepthOnly", false);
        ownedMaterial.SetShaderPassEnabled("DepthNormalsOnly", false);
        ownedMaterial.SetShaderPassEnabled("MotionVectors", false);

        ownedMesh = new Mesh { name = "SimBar Night Backdrop Quad" };
        ownedMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        ownedMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        ownedMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        ownedMesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ownedMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        FitToCamera();
    }

    private void LateUpdate()
    {
        FitToCamera();
    }

    private void FitToCamera()
    {
        if (viewCamera == null || ownedMaterial == null) return;

        float aspect = Mathf.Max(0.01f, viewCamera.aspect);
        float depth = Mathf.Lerp(viewCamera.nearClipPlane, viewCamera.farClipPlane, 0.98f);
        float height = viewCamera.orthographic
            ? viewCamera.orthographicSize * 2f
            : 2f * depth * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Transform cameraTransform = viewCamera.transform;
        transform.SetPositionAndRotation(cameraTransform.position + cameraTransform.forward * depth, cameraTransform.rotation);
        // A tiny uniform overscan prevents subpixel gaps along viewport edges.
        transform.localScale = new Vector3(height * aspect, height, 1f) * 1.002f;

        if (Mathf.Approximately(aspect, fittedAspect)) return;
        fittedAspect = aspect;
        Vector2 scale = imageAspect > aspect
            ? new Vector2(aspect / imageAspect, 1f)
            : new Vector2(1f, imageAspect / aspect);
        Vector2 offset = (Vector2.one - scale) * 0.5f;
        // Center crop preserves the image's proportions at every browser size.
        if (ownedMaterial.HasProperty("_BaseMap"))
        {
            ownedMaterial.SetTextureScale("_BaseMap", scale);
            ownedMaterial.SetTextureOffset("_BaseMap", offset);
        }
        if (ownedMaterial.HasProperty("_MainTex"))
        {
            ownedMaterial.SetTextureScale("_MainTex", scale);
            ownedMaterial.SetTextureOffset("_MainTex", offset);
        }
    }

    private void OnDestroy()
    {
        if (ownedMaterial != null) Destroy(ownedMaterial);
        if (ownedMesh != null) Destroy(ownedMesh);
        // The decoded panorama is shared and owned by SimBarWebTextures.
    }
}
