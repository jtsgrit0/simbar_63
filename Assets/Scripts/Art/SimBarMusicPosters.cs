using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Layered, torn flyposters and old gig-paper scraps on the pub walls.</summary>
public sealed class SimBarMusicPosters : MonoBehaviour
{
    public const int WallArtworkCount = 16;
    public const int ColumnArtworkCount = 6;
    public const int ArtworkCount = WallArtworkCount + ColumnArtworkCount;
    public const string BackWallRootName = "Back Wall Collage";
    public const string LeftWallRootName = "Left Wall Collage";
    public const string ColumnRootName = "Gray Column Collage";

    [SerializeField] private List<Material> ownedMaterials = new List<Material>();
    [SerializeField] private List<Mesh> ownedMeshes = new List<Mesh>();
    private readonly Dictionary<string, Material> artistMaterials = new Dictionary<string, Material>();
    private readonly Dictionary<string, Texture2D> artistTextures = new Dictionary<string, Texture2D>();
    private Mesh quadMesh;
    private Material paper, oldPaper, charcoal, tape, pinkTicket, yellowTicket;

    /// <summary>Call after the venue is built. Repeated calls for the same parent are harmless.</summary>
    public static void Build(Transform parent)
    {
        if (parent == null || parent.Find("Music Posters") != null) return;
        var root = new GameObject("Music Posters");
        root.transform.SetParent(parent, false);
        root.AddComponent<SimBarMusicPosters>().Initialize();
    }

    private void Initialize()
    {
        Material template = Resources.Load<Material>("SimBarArt/Posters/PosterUnlit");
        if (template == null || template.shader == null)
        {
            Debug.LogWarning("SimBar music posters require the PosterUnlit resource material.");
            return;
        }

        quadMesh = OwnMesh(CreateQuad());
        paper = OwnMaterial(template, "SimBar Wheatpaste Paper", new Color(0.66f, 0.61f, 0.49f, 1f));
        oldPaper = OwnMaterial(template, "SimBar Old Gig Paper", new Color(0.44f, 0.40f, 0.30f, 1f));
        charcoal = OwnMaterial(template, "SimBar Flyer Ink", new Color(0.075f, 0.065f, 0.056f, 1f));
        tape = OwnMaterial(template, "SimBar Aged Masking Tape", new Color(0.56f, 0.49f, 0.31f, 1f));
        pinkTicket = OwnMaterial(template, "SimBar Faded Pink Ticket", new Color(0.56f, 0.23f, 0.27f, 1f));
        yellowTicket = OwnMaterial(template, "SimBar Faded Yellow Ticket", new Color(0.67f, 0.53f, 0.17f, 1f));
        Texture2D wear = SimBarWebTextures.Load("SimBarArt/Posters/PaperWear");
        if (wear != null)
        {
            paper.mainTexture = wear;
            oldPaper.mainTexture = wear;
        }

        foreach (string key in new[] { "Queen", "Nirvana", "TheBeatles", "PopNight", "TaylorSwift", "Blackpink" })
        {
            Texture2D texture = SimBarWebTextures.Load("SimBarArt/Posters/" + key);
            if (texture == null) { Debug.LogWarning("SimBar poster texture is missing: " + key); continue; }
            Material material = OwnMaterial(template, "SimBar Poster " + key, new Color(0.89f, 0.86f, 0.80f, 1f));
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            artistMaterials.Add(key, material);
            artistTextures.Add(key, texture);
        }

        Transform back = Wall(BackWallRootName, new Vector3(0f, 0f, 7.365f), 0f);
        Transform left = Wall(LeftWallRootName, new Vector3(-10.165f, 0f, 0f), -90f);

        // The papers are pasted to the solid charcoal wall, below the 1.67 m sill.
        // Their uneven clusters and exposed scraps retain the old pub collage.
        Remnant(back, -8.12f, 1.00f, 2.0f, 2.30f, 5f, 11, oldPaper);
        Remnant(back, -7.08f, .65f, 1.2f, 1.65f, -13f, 12, paper);
        Remnant(back, -5.48f, 1.02f, 1.25f, 2.15f, 9f, 13, oldPaper);
        Remnant(back, -2.55f, .96f, 2.0f, 2.28f, -8f, 14, paper);
        Remnant(back, -1.70f, 1.13f, 1.35f, 1.66f, -15f, 15, oldPaper);
        Ticket(back, -7.24f, 1.49f, 0.80f, 0.24f, -6f, yellowTicket, 1);
        Ticket(back, -5.12f, .59f, 0.66f, 0.27f, 8f, pinkTicket, 2);
        Ticket(back, -1.51f, 1.43f, 0.93f, 0.23f, 12f, yellowTicket, 3);

        Poster(back, "Nirvana", -8.15f, 1.04f, 2.18f, -7f, 3, 101, .50f);
        Poster(back, "Queen", -7.59f, 1.15f, 1.46f, 8f, 1, 102, .50f);
        Poster(back, "TheBeatles", -5.65f, .96f, 2.35f, 4f, 4, 103, .52f);
        Poster(back, "TaylorSwift", -5.10f, 1.14f, 1.35f, -11f, 2, 104, .52f);
        Poster(back, "Blackpink", -2.58f, 1.14f, 1.55f, -5f, 5, 105, .56f);
        Poster(back, "PopNight", -1.97f, .99f, 1.82f, 9f, 4, 106, .50f);
        Poster(back, "Nirvana", -7.12f, .48f, .76f, 14f, 2, 107, .58f);
        Poster(back, "PopNight", -3.01f, .56f, .82f, -14f, 2, 108, .60f);

        Remnant(left, -4.73f, .98f, 2.05f, 2.45f, -7f, 21, paper);
        Remnant(left, -4.00f, .66f, 1.2f, 2.18f, 14f, 22, oldPaper);
        Remnant(left, -3.13f, 1.03f, 1.75f, 2.12f, 9f, 23, paper);
        Remnant(left, -2.23f, .94f, 2.12f, 2.49f, 7f, 24, oldPaper);
        Remnant(left, -1.56f, 1.13f, 1.75f, 2.08f, -12f, 25, paper);
        Ticket(left, -4.23f, 1.51f, .91f, .25f, -8f, pinkTicket, 4);
        Ticket(left, -2.83f, .55f, .71f, .22f, 13f, yellowTicket, 5);
        Ticket(left, -1.80f, 1.45f, .73f, .26f, -12f, pinkTicket, 6);

        Poster(left, "Queen", -4.73f, 1.02f, 2.25f, 10f, 4, 201, .50f);
        Poster(left, "Nirvana", -4.16f, 1.19f, 1.26f, -8f, 1, 202, .48f);
        Poster(left, "PopNight", -3.61f, .93f, 1.90f, -6f, 5, 203, .54f);
        Poster(left, "Blackpink", -3.05f, 1.15f, 1.40f, 11f, 2, 204, .48f);
        Poster(left, "TaylorSwift", -2.40f, 1.00f, 2.35f, -5f, 4, 205, .52f);
        Poster(left, "TheBeatles", -1.77f, 1.06f, 1.90f, 8f, 5, 206, .50f);
        Poster(left, "Queen", -4.03f, .47f, .72f, -12f, 2, 207, .60f);
        Poster(left, "Blackpink", -2.04f, .43f, .88f, -10f, 2, 208, .56f);

        ConstrainPaperToWall(back);
        ConstrainPaperToWall(left);
        CombinePaperDecorations(back);
        CombinePaperDecorations(left);
        BuildColumnPosters();
    }

    private Transform Wall(string label, Vector3 position, float yaw)
    {
        var wall = new GameObject(label).transform;
        wall.SetParent(transform, false);
        wall.localPosition = position;
        wall.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return wall;
    }

    private void BuildColumnPosters()
    {
        var columns = new GameObject(ColumnRootName).transform;
        columns.SetParent(transform, false);
        // The four gray pillars replace the four former planters. Paper fronts
        // face the fixed game camera; each remains inside its 0.62 m square face.
        ColumnPoster(columns, "Front Left Front", new Vector3(-9.55f, 0f, -6.225f), 0f,
            "Queen", 2.85f, -4f, 301);
        ColumnPoster(columns, "Front Left Right", new Vector3(-9.225f, 0f, -5.9f), -90f,
            "Nirvana", 3.58f, 3f, 302);
        ColumnPoster(columns, "Rear Middle Front", new Vector3(-1.25f, 0f, 5.425f), 0f,
            "Blackpink", 2.72f, 4f, 303);
        ColumnPoster(columns, "Rear Middle Right", new Vector3(-.925f, 0f, 5.75f), -90f,
            "PopNight", 3.50f, -5f, 304);
        ColumnPoster(columns, "Rear Left Right", new Vector3(-9.075f, 0f, 6.7f), -90f,
            "TaylorSwift", 3.10f, -3f, 305);
        ColumnPoster(columns, "Stage Right Front", new Vector3(9.73f, 0f, 6.325f), 0f,
            "TheBeatles", 2.84f, 5f, 306);
        CombinePaperDecorations(columns);
    }

    private void ColumnPoster(Transform parent, string label, Vector3 position, float yaw,
        string key, float height, float angle, int seed)
    {
        var face = new GameObject(label).transform;
        face.SetParent(parent, false);
        face.localPosition = position;
        face.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Poster(face, key, 0f, height, .54f, angle, 0, seed, 1f);
    }

    private void Poster(Transform wall, string key, float horizontal, float height, float paperHeight, float angle, int layer, int seed, float scale)
    {
        if (!artistMaterials.TryGetValue(key, out Material material)) return;
        Texture2D texture = artistTextures[key];
        float width = paperHeight * texture.width / Mathf.Max(1f, texture.height);
        var group = new GameObject(key + " Flyposter " + seed).transform;
        group.SetParent(wall, false);
        group.localPosition = new Vector3(horizontal, height, -0.020f - layer * 0.009f);
        group.localRotation = Quaternion.Euler(0f, 0f, angle);
        group.localScale = Vector3.one * scale;
        Mesh torn = OwnMesh(CreateTornPaper(seed));
        Surface("Wheatpaste Edge", group, torn, seed % 2 == 0 ? oldPaper : paper,
            new Vector3(0f, 0f, 0.0018f), new Vector3(width * 1.025f, paperHeight * 1.023f, 1f));
        Surface("Artwork", group, torn, material, Vector3.zero, new Vector3(width, paperHeight, 1f));
        Tape(group, -width * 0.33f, paperHeight * 0.47f, -24f + seed % 13);
        if (paperHeight > 1.5f) Tape(group, width * 0.35f, paperHeight * 0.45f, 31f - seed % 17);
    }

    private void Remnant(Transform wall, float x, float y, float width, float height, float angle, int seed, Material material)
    {
        var remnant = new GameObject("Ripped Gig Paper " + seed).transform;
        remnant.SetParent(wall, false);
        remnant.localPosition = new Vector3(x, y, -0.003f - (seed % 3) * 0.002f);
        remnant.localRotation = Quaternion.Euler(0f, 0f, angle);
        remnant.localScale = Vector3.one * .50f;
        Surface("Old Paper", remnant, OwnMesh(CreateTornPaper(seed)), material, Vector3.zero, new Vector3(width, height, 1f));
        // Exposed remnants of the previous print: chunky ink bars, like half-peeled gig flyers.
        for (int i = 0; i < 3; i++)
            Surface("Weathered Print", remnant, quadMesh, charcoal, new Vector3(-width * 0.15f, height * (0.30f - i * 0.12f), -0.001f),
                new Vector3(width * (0.52f - i * 0.08f), height * 0.038f, 1f));
    }

    private void Ticket(Transform wall, float x, float y, float width, float height, float angle, Material material, int seed)
    {
        var ticket = new GameObject("Old Gig Ticket " + seed).transform;
        ticket.SetParent(wall, false);
        ticket.localPosition = new Vector3(x, y, -0.014f);
        ticket.localRotation = Quaternion.Euler(0f, 0f, angle);
        ticket.localScale = Vector3.one * .50f;
        Surface("Ticket", ticket, OwnMesh(CreateTornPaper(seed + 300)), material, Vector3.zero, new Vector3(width, height, 1f));
        for (int i = 0; i < 4; i++)
            Surface("Ticket Ink", ticket, quadMesh, charcoal, new Vector3(width * (-0.28f + i * 0.17f), 0f, -0.001f),
                new Vector3(width * (i == 1 ? 0.08f : 0.035f), height * 0.60f, 1f));
    }

    private void Tape(Transform poster, float x, float y, float angle)
    {
        var strip = new GameObject("Old Masking Tape").transform;
        strip.SetParent(poster, false);
        strip.localPosition = new Vector3(x, y, -0.002f);
        strip.localRotation = Quaternion.Euler(0f, 0f, angle);
        Surface("Tape", strip, quadMesh, tape, Vector3.zero, new Vector3(0.22f, 0.071f, 1f));
    }

    private Material OwnMaterial(Material template, string label, Color color)
    {
        var material = new Material(template) { name = label };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = (int)RenderQueue.Geometry;
        material.doubleSidedGI = false;
        ownedMaterials.Add(material);
        return material;
    }

    private Mesh OwnMesh(Mesh mesh) { ownedMeshes.Add(mesh); return mesh; }

    private static void ConstrainPaperToWall(Transform wall)
    {
        // Include rotated paper corners, borders and tape on the solid wall.
        foreach (Transform group in wall)
        {
            float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>())
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    float y = wall.InverseTransformPoint(filter.transform.TransformPoint(vertex)).y;
                    top = Mathf.Max(top, y);
                    bottom = Mathf.Min(bottom, y);
                }
            if (top > 1.62f) group.localPosition -= Vector3.up * (top - 1.62f);
            else if (bottom < .22f) group.localPosition += Vector3.up * (.22f - bottom);
        }
    }

    private void CombinePaperDecorations(Transform wall)
    {
        // Keep each image separate for clarity; paper edges, tape, and ticket ink share
        // six batches per wall instead of adding a draw call for every little strip.
        var batches = new Dictionary<Material, List<CombineInstance>>();
        var originals = new List<GameObject>();
        foreach (MeshRenderer renderer in wall.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name == "Artwork") continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Material material = renderer.sharedMaterial;
            if (!batches.TryGetValue(material, out List<CombineInstance> instances))
            {
                instances = new List<CombineInstance>();
                batches.Add(material, instances);
            }
            instances.Add(new CombineInstance
            {
                mesh = filter.sharedMesh,
                transform = wall.worldToLocalMatrix * filter.transform.localToWorldMatrix
            });
            renderer.enabled = false;
            originals.Add(renderer.gameObject);
        }
        foreach (var batch in batches)
        {
            var mesh = OwnMesh(new Mesh { name = wall.name + " " + batch.Key.name });
            mesh.CombineMeshes(batch.Value.ToArray(), true, true);
            Surface("Paper Layers " + batch.Key.name, wall, mesh, batch.Key, Vector3.zero, Vector3.one);
        }
        foreach (GameObject original in originals) Release(original);
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
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static Mesh CreateQuad()
    {
        var mesh = new Mesh { name = "SimBar Flyposter Paper Quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateTornPaper(int seed)
    {
        // A deterministic paper silhouette; UV coordinates preserve the original image aspect.
        // Shallow bites trim the margins and leave the artist titles untouched.
        const int sideSteps = 10;
        var vertices = new List<Vector3> { Vector3.zero };
        var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var normals = new List<Vector3> { Vector3.back };
        var triangles = new List<int>();
        var random = new System.Random(seed);
        Vector2[] corners = { new Vector2(-0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, -0.5f) };
        for (int side = 0; side < 4; side++)
        {
            Vector2 from = corners[side], to = corners[(side + 1) % 4];
            for (int step = 0; step < sideSteps; step++)
            {
                Vector2 point = Vector2.Lerp(from, to, step / (float)sideSteps);
                float bite = 0.002f + (float)random.NextDouble() * 0.007f;
                if (step == 3 || step == 7) bite += 0.009f;
                if (side == 0) point.x += bite;
                else if (side == 1) point.y -= bite;
                else if (side == 2) point.x -= bite;
                else point.y += bite;
                vertices.Add(new Vector3(point.x, point.y, 0f));
                normals.Add(Vector3.back);
                uv.Add(point + new Vector2(0.5f, 0.5f));
            }
        }
        for (int i = 1; i < vertices.Count; i++)
        {
            triangles.Add(0); triangles.Add(i); triangles.Add(i == vertices.Count - 1 ? 1 : i + 1);
        }
        var mesh = new Mesh { name = "SimBar Torn Flyposter " + seed };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        foreach (Material material in ownedMaterials) Release(material);
        foreach (Mesh mesh in ownedMeshes) Release(mesh);
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}
