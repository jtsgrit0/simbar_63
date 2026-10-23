using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Artist name spray paint on the reference pub's open floor.</summary>
public sealed class SimBarFloorGraffiti : MonoBehaviour
{
    public const float SurfaceHeight = 0.0715f;
    public static readonly string[] ArtistKeys = { "Queen", "Nirvana", "TheBeatles", "TaylorSwift", "Blackpink" };
    public static readonly Color[] ArtistColors =
    {
        new Color(1f, 0.68f, 0.14f),
        new Color(1f, 0.45f, 0.08f),
        new Color(0.10f, 0.79f, 0.79f),
        new Color(0.73f, 0.45f, 0.95f),
        new Color(1f, 0.13f, 0.53f)
    };

    [SerializeField] private Mesh floorQuad;
    [SerializeField] private List<Material> ownedMaterials = new List<Material>();

    /// <summary>Call after creating the venue. It is safe to call again for the same parent.</summary>
    public static void Build(Transform parent)
    {
        if (parent == null || parent.Find("Floor Graffiti") != null) return;
        var root = new GameObject("Floor Graffiti");
        root.transform.SetParent(parent, false);
        root.AddComponent<SimBarFloorGraffiti>().Initialize();
    }

    private void Initialize()
    {
        Material template = Resources.Load<Material>("SimBarArt/Graffiti/GraffitiUnlit");
        if (template == null || template.shader == null)
        {
            Debug.LogWarning("SimBar floor graffiti requires the GraffitiUnlit resource material.");
            return;
        }

        floorQuad = CreateFloorQuad();
        // Stagger the names through the open floor, clear of the bar, booth benches and stage.
        Paint(0, new Vector2(0.6f, -3.7f), -10f, new Vector2(3.0f, 1.30f), template);
        Paint(1, new Vector2(3.7f, -1.8f), 21f, new Vector2(3.7f, 1.30f), template);
        Paint(2, new Vector2(0.9f, 0.6f), -27f, new Vector2(3.1f, 1.30f), template);
        Paint(3, new Vector2(3.1f, -5.8f), -8f, new Vector2(3.7f, 1.30f), template);
        Paint(4, new Vector2(7.0f, -4.4f), 14f, new Vector2(3.6f, 1.30f), template);
    }

    private void Paint(int artist, Vector2 position, float yaw, Vector2 area, Material template)
    {
        string key = ArtistKeys[artist];
        Texture2D texture = Resources.Load<Texture2D>("SimBarArt/Graffiti/" + key);
        if (texture == null)
        {
            Debug.LogWarning("SimBar floor graffiti texture is missing: " + key);
            return;
        }

        // Preserve the generated lettering proportions; alpha carries the paint and overspray.
        float aspect = texture.width / (float)Mathf.Max(1, texture.height);
        Vector2 size = area;
        if (aspect > area.x / area.y) size.y = size.x / aspect;
        else size.x = size.y * aspect;

        Material material = new Material(template) { name = "SimBar Floor Spray " + key };
        Color tint = artist == 1 ? new Color(1f, 0.45f, 0.21f, 0.88f) : new Color(1f, 1f, 1f, 0.88f);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.doubleSidedGI = false;
        ownedMaterials.Add(material);

        var surface = new GameObject(key, typeof(MeshFilter), typeof(MeshRenderer));
        surface.transform.SetParent(transform, false);
        surface.transform.localPosition = new Vector3(position.x, SurfaceHeight + artist * 0.0003f, position.y);
        surface.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        surface.transform.localScale = new Vector3(size.x, 1f, size.y);
        surface.GetComponent<MeshFilter>().sharedMesh = floorQuad;
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static Mesh CreateFloorQuad()
    {
        var mesh = new Mesh { name = "SimBar Floor Spray Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
        };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        // The top of each name points toward the stage, so it reads from the main camera.
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        foreach (Material material in ownedMaterials) Release(material);
        Release(floorQuad);
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}
