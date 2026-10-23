using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Opaque, correctly facing music art on the reference venue's existing and new frames.</summary>
public sealed class SimBarMusicPosters : MonoBehaviour
{
    private static readonly Vector2 ArtworkSize = new Vector2(1.79f, 2.19f);
    private static readonly Color PaperTint = new Color(0.90f, 0.88f, 0.85f, 1f);
    [SerializeField] private Mesh quadMesh;
    [SerializeField] private Mesh boxMesh;
    [SerializeField] private List<Material> ownedMaterials = new List<Material>();
    private Material wood, cream, black;

    /// <summary>Call after the venue is built. Repeated calls for the same parent are harmless.</summary>
    public static void Build(Transform parent)
    {
        if (parent == null || parent.Find("Music Posters") != null) return;
        var root = new GameObject("Music Posters");
        root.transform.SetParent(parent, false);
        var owner = root.AddComponent<SimBarMusicPosters>();
        owner.Initialize();
    }

    private void Initialize()
    {
        Material template = Resources.Load<Material>("SimBarArt/Posters/PosterUnlit");
        if (template == null || template.shader == null)
        {
            Debug.LogWarning("SimBar music posters require the PosterUnlit resource material.");
            return;
        }
        quadMesh = CreateQuad();
        boxMesh = CreateBox();
        Shader frameShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? template.shader;
        wood = OwnMaterial(frameShader, "SimBar Poster Wood", new Color(0.24f, 0.12f, 0.055f, 1f));
        cream = OwnMaterial(frameShader, "SimBar Poster Cream Mat", new Color(0.90f, 0.86f, 0.76f, 1f));
        black = OwnMaterial(template, "SimBar Poster Letterbox", new Color(0.025f, 0.022f, 0.018f, 1f));
        black.mainTexture = Texture2D.whiteTexture;
        Poster("Nirvana", new Vector3(-8.3f, 3.72f, 7.105f), 0f, false, template);
        Poster("TheBeatles", new Vector3(-4.8f, 3.72f, 7.105f), 0f, false, template);
        Poster("Blackpink", new Vector3(-0.95f, 3.72f, 7.105f), 0f, false, template);
        Poster("Queen", new Vector3(-9.995f, 3.72f, -3.65f), -90f, true, template);
        Poster("PopNight", new Vector3(-9.995f, 3.72f, -0.95f), -90f, true, template);
        Poster("TaylorSwift", new Vector3(-9.995f, 3.72f, 3.65f), -90f, true, template);
    }

    private void Poster(string key, Vector3 position, float yaw, bool addFrame, Material template)
    {
        Texture2D texture = Resources.Load<Texture2D>("SimBarArt/Posters/" + key);
        if (texture == null) { Debug.LogWarning("SimBar poster texture is missing: " + key); return; }
        var group = new GameObject(key).transform;
        group.SetParent(transform, false);
        group.localPosition = position;
        group.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (addFrame)
        {
            Surface("Wood Frame", group, boxMesh, wood, new Vector3(0f, 0f, 0.125f), new Vector3(2.05f, 2.45f, 0.14f));
            Surface("Cream Mat", group, boxMesh, cream, new Vector3(0f, 0f, 0.04f), new Vector3(1.89f, 2.29f, 0.035f));
        }
        Surface("Artwork Backer", group, quadMesh, black, new Vector3(0f, 0f, 0.003f), new Vector3(ArtworkSize.x, ArtworkSize.y, 1f));
        float aspect = texture.width / (float)Mathf.Max(1, texture.height);
        Vector2 fitted = ArtworkSize;
        if (aspect > ArtworkSize.x / ArtworkSize.y) fitted.y = fitted.x / aspect;
        else fitted.x = fitted.y * aspect;
        Material material = OwnMaterial(template, "SimBar Poster " + key, PaperTint);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        Surface("Artwork", group, quadMesh, material, Vector3.zero, new Vector3(fitted.x, fitted.y, 1f));
    }

    private Material OwnMaterial(Shader shader, string label, Color color) => ConfigureMaterial(new Material(shader), label, color);

    private Material OwnMaterial(Material template, string label, Color color) => ConfigureMaterial(new Material(template), label, color);

    private Material ConfigureMaterial(Material material, string label, Color color)
    {
        material.name = label;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = (int)RenderQueue.Geometry;
        material.doubleSidedGI = false;
        ownedMaterials.Add(material);
        return material;
    }

    private static void Surface(string label, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 size)
    {
        var surface = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        surface.transform.SetParent(parent, false);
        surface.transform.localPosition = position;
        surface.transform.localScale = size;
        surface.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh CreateQuad()
    {
        var mesh = new Mesh { name = "SimBar Music Poster Quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateBox()
    {
        var vertices = new List<Vector3>(24); var normals = new List<Vector3>(24);
        var uv = new List<Vector2>(24); var triangles = new List<int>(36);
        BoxFace(vertices, normals, uv, triangles, Vector3.forward, Vector3.right, Vector3.up);
        BoxFace(vertices, normals, uv, triangles, Vector3.back, Vector3.left, Vector3.up);
        BoxFace(vertices, normals, uv, triangles, Vector3.right, Vector3.back, Vector3.up);
        BoxFace(vertices, normals, uv, triangles, Vector3.left, Vector3.forward, Vector3.up);
        BoxFace(vertices, normals, uv, triangles, Vector3.up, Vector3.right, Vector3.back);
        BoxFace(vertices, normals, uv, triangles, Vector3.down, Vector3.right, Vector3.forward);
        var mesh = new Mesh { name = "SimBar Music Poster Frame Box" };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BoxFace(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles, Vector3 normal, Vector3 right, Vector3 up)
    {
        int first = vertices.Count;
        Vector3 center = normal * 0.5f, r = right * 0.5f, u = up * 0.5f;
        vertices.Add(center - r - u); vertices.Add(center + r - u); vertices.Add(center + r + u); vertices.Add(center - r + u);
        for (int i = 0; i < 4; i++) normals.Add(normal);
        uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
    }

    private void OnDestroy()
    {
        foreach (Material material in ownedMaterials) Release(material);
        Release(quadMesh); Release(boxMesh);
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}
