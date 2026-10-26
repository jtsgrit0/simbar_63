using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Artist name spray paint scattered through the reference pub's open floor.</summary>
public sealed partial class SimBarFloorGraffiti : MonoBehaviour
{
    public const float SurfaceHeight = 0.0715f;
    public const int TitleCount = 10;
    public static readonly string[] ArtistKeys = { "Queen", "Nirvana", "TheBeatles", "TaylorSwift", "Blackpink" };
    public static readonly Color[] ArtistColors =
    {
        new Color(1f, 0.68f, 0.14f),
        new Color(1f, 0.45f, 0.08f),
        new Color(0.10f, 0.79f, 0.79f),
        new Color(0.73f, 0.45f, 0.95f),
        new Color(1f, 0.13f, 0.53f)
    };

    [SerializeField] private List<Mesh> ownedTitleMeshes = new List<Mesh>();
    [SerializeField] private List<Material> ownedMaterials = new List<Material>();
    private List<PaintMark>[] paintMarks;

    private struct PaintMark
    {
        public Vector2 position;
        public float yaw;
        public Vector2 area;
        public Vector2 size;

        public PaintMark(float x, float z, float yaw, float width, float depth)
        {
            position = new Vector2(x, z);
            this.yaw = yaw;
            area = new Vector2(width, depth);
            size = area;
        }
    }

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

        // The five main names retain their readable foreground scale. Smaller repeat tags
        // reach the rear bar aisle, booth passage and right edge without following tile rows.
        paintMarks = new[]
        {
            new List<PaintMark> { new PaintMark(0.6f, -3.7f, -10f, 3f, 1.30f),
                new PaintMark(-7.4f, 2.28f, -7f, 2.3f, 0.86f) },
            new List<PaintMark> { new PaintMark(3.7f, -1.8f, 21f, 3.7f, 1.30f),
                new PaintMark(-5.15f, -5.2f, -89f, 1.8f, 0.48f) },
            new List<PaintMark> { new PaintMark(0.9f, 0.6f, -27f, 3.1f, 1.30f),
                new PaintMark(-3.7f, 2.30f, 17f, 2.25f, 0.88f) },
            new List<PaintMark> { new PaintMark(3.1f, -5.8f, -8f, 3.7f, 1.30f),
                new PaintMark(-0.3f, 4.8f, -83f, 2.2f, 0.70f) },
            new List<PaintMark> { new PaintMark(7f, -4.4f, 14f, 3.6f, 1.30f),
                new PaintMark(8.7f, 0.65f, -24f, 2f, 0.70f) }
        };
        for (int artist = 0; artist < ArtistKeys.Length; artist++) FitTitles(artist);
        for (int artist = 0; artist < ArtistKeys.Length; artist++) Paint(artist, template);
    }

    private void FitTitles(int artist)
    {
        Texture2D texture = SimBarWebTextures.Load("SimBarArt/Graffiti/" + ArtistKeys[artist]);
        if (texture == null) return;
        float aspect = texture.width / (float)Mathf.Max(1, texture.height);
        for (int index = 0; index < paintMarks[artist].Count; index++)
        {
            PaintMark mark = paintMarks[artist][index];
            Vector2 size = mark.area;
            if (aspect > size.x / size.y) size.y = size.x / aspect;
            else size.x = size.y * aspect;
            mark.size = size;
            paintMarks[artist][index] = mark;
        }
    }

    private void Paint(int artist, Material template)
    {
        string key = ArtistKeys[artist];
        Texture2D texture = SimBarWebTextures.Load("SimBarArt/Graffiti/" + key);
        if (texture == null)
        {
            Debug.LogWarning("SimBar floor graffiti texture is missing: " + key);
            return;
        }

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
        PaintMark first = paintMarks[artist][0];
        surface.transform.localPosition = new Vector3(first.position.x, SurfaceHeight + artist * 0.0003f, first.position.y);
        surface.transform.localRotation = Quaternion.Euler(0f, first.yaw, 0f);
        surface.transform.localScale = new Vector3(first.size.x, 1f, first.size.y);
        surface.GetComponent<MeshFilter>().sharedMesh = CreateTitleBatch(artist, surface.transform);
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        CreateSplatter(artist, template);
    }

    private Mesh CreateTitleBatch(int artist, Transform batch)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        foreach (PaintMark mark in paintMarks[artist])
        {
            int first = vertices.Count;
            Matrix4x4 placement = Matrix4x4.TRS(new Vector3(mark.position.x, batch.localPosition.y, mark.position.y),
                Quaternion.Euler(0f, mark.yaw, 0f), new Vector3(mark.size.x, 1f, mark.size.y));
            Matrix4x4 toBatch = batch.worldToLocalMatrix * transform.localToWorldMatrix * placement;
            for (int corner = 0; corner < 4; corner++)
            {
                float u = (corner & 1) == 0 ? 0f : 1f;
                float v = (corner & 2) == 0 ? 0f : 1f;
                vertices.Add(toBatch.MultiplyPoint3x4(new Vector3(u - 0.5f, 0f, v - 0.5f)));
                normals.Add(Vector3.up);
                uv.Add(new Vector2(u, v));
            }
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
        }
        var mesh = new Mesh { name = "SimBar " + ArtistKeys[artist] + " Floor Tags" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        // The top of each name points toward the stage, so it reads from the main camera.
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        ownedTitleMeshes.Add(mesh);
        return mesh;
    }

    private void OnDestroy()
    {
        ReleaseSplatter();
        foreach (Material material in ownedMaterials) Release(material);
        foreach (Mesh mesh in ownedTitleMeshes) Release(mesh);
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}
