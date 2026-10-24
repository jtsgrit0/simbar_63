using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class SimBarFloorGraffiti
{
    [SerializeField] private List<Mesh> ownedSplatterMeshes = new List<Mesh>();
    [SerializeField] private List<Material> ownedSplatterMaterials = new List<Material>();

    private void CreateSplatter(int artist, Material template)
    {
        Texture2D stamp = Resources.Load<Texture2D>("SimBarArt/Graffiti/SpraySplatter");
        if (stamp == null) return;

        // A private seed keeps the venue stable without consuming gameplay's random sequence.
        var random = new System.Random(0x63A73 + artist * 7919);
        var centers = new List<Vector2>();
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        foreach (PaintMark mark in paintMarks[artist])
        {
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            float end = random.NextDouble() < 0.5 ? -1f : 1f;
            Vector2 lobe = new Vector2(end * mark.size.x * Range(random, 0.12f, 0.29f),
                side * (mark.size.y * 0.52f + Range(random, 0.10f, 0.20f)));
            Vector2 oppositeEnd = new Vector2(-end * (mark.size.x * 0.5f + 0.13f),
                -side * mark.size.y * Range(random, 0.10f, 0.45f));
            Vector2 tail = new Vector2(end * Range(random, 0.45f, 0.8f), side * 0.25f);
            int requested = 13 + artist % 3;
            float scale = Mathf.Min(1f, mark.size.y / 0.9f);
            for (int index = 0; index < requested; index++)
            {
                bool large = index == 0;
                bool satellite = index >= requested - 4;
                Vector2 size = scale * (large
                    ? new Vector2(Range(random, 1.30f, 1.75f), Range(random, 0.90f, 1.25f))
                    : satellite
                        ? new Vector2(Range(random, 0.40f, 0.75f), Range(random, 0.30f, 0.60f))
                        : new Vector2(Range(random, 0.65f, 1.15f), Range(random, 0.50f, 0.95f)));
                float angle = Range(random, -Mathf.PI, Mathf.PI);
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    Vector2 local;
                    if (index < 6)
                        local = lobe + new Vector2(Gaussian(random) * 0.38f, Gaussian(random) * 0.24f) * scale;
                    else if (!satellite)
                        local = oppositeEnd + new Vector2(Gaussian(random) * 0.24f, Gaussian(random) * 0.37f) * scale;
                    else
                        local = lobe + tail * Range(random, 0.25f, 1.4f) * scale
                            + new Vector2(Gaussian(random) * 0.32f, Gaussian(random) * 0.24f) * scale;

                    // A blocked lobe can spill toward another edge instead of being clamped into a row.
                    if (attempt > 32 && random.NextDouble() < 0.5) local.y = -local.y;
                    Vector3 offset = Quaternion.Euler(0f, mark.yaw, 0f) * new Vector3(local.x, 0f, local.y);
                    Vector2 center = mark.position + new Vector2(offset.x, offset.z);
                    if (!ClearOfAllLetters(center, size) || TooClose(center, centers)
                        || !OpenFloor(new Vector3(center.x, 0f, center.y), size.magnitude * 0.5f)) continue;
                    centers.Add(center);
                    AddStamp(center, size, angle - mark.yaw * Mathf.Deg2Rad, random.NextDouble() < 0.5,
                        random.NextDouble() < 0.5, vertices, normals, uv, triangles);
                    break;
                }
            }
        }

        // Not a repeating texture or a tiled grid: loose, uneven bursts reach walking spaces
        // throughout the room, while each narrow passage uses appropriately small droplets.
        foreach (ScatterRegion region in ScatterRegions(artist))
            AddAmbientBurst(region, random, centers, vertices, normals, uv, triangles);

        if (vertices.Count == 0) return;
        string key = ArtistKeys[artist];
        var mesh = new Mesh { name = "SimBar " + key + " Irregular Splatter (" + centers.Count + " stamps)" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        ownedSplatterMeshes.Add(mesh);

        Material material = new Material(template) { name = "SimBar " + key + " Spray Flecks" };
        Color color = ArtistColors[artist];
        color.a = 0.86f;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", stamp);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", stamp);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent - 1;
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.doubleSidedGI = false;
        ownedSplatterMaterials.Add(material);

        var surface = new GameObject(key + " Spray Splatter", typeof(MeshFilter), typeof(MeshRenderer));
        surface.transform.SetParent(transform, false);
        surface.transform.localPosition = new Vector3(0f, SurfaceHeight + artist * 0.0003f - 0.002f, 0f);
        // The whole floor is combined into one artist-colored batch, with coordinates in metres.
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = Vector3.one;
        surface.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private struct ScatterRegion
    {
        public Vector2 center, spread;
        public float size;
        public ScatterRegion(float x, float z, float spreadX, float spreadZ, float size)
        {
            center = new Vector2(x, z);
            spread = new Vector2(spreadX, spreadZ);
            this.size = size;
        }
    }

    private static ScatterRegion[] ScatterRegions(int artist)
    {
        switch (artist)
        {
            case 0: return new[] { new ScatterRegion(-8.7f, 2.2f, 0.58f, 0.25f, 0.65f),
                new ScatterRegion(0.8f, -6.6f, 0.6f, 0.28f, 0.72f),
                new ScatterRegion(7.9f, -0.9f, 0.72f, 0.65f, 0.82f),
                new ScatterRegion(0.2f, 6.6f, 0.5f, 0.23f, 0.63f) };
            case 1: return new[] { new ScatterRegion(-5.15f, -5.2f, 0.13f, 0.7f, 0.34f),
                new ScatterRegion(-0.6f, -0.4f, 0.65f, 0.68f, 0.73f),
                new ScatterRegion(9.8f, 3.7f, 0.09f, 0.63f, 0.31f),
                new ScatterRegion(4.8f, -6.6f, 0.71f, 0.33f, 0.77f) };
            case 2: return new[] { new ScatterRegion(-4.7f, 2.25f, 0.65f, 0.27f, 0.68f),
                new ScatterRegion(0.1f, 2.3f, 0.42f, 0.43f, 0.69f),
                new ScatterRegion(6.5f, -6.5f, 0.61f, 0.38f, 0.72f),
                new ScatterRegion(-8.8f, -0.565f, 0.42f, 0.045f, 0.22f) };
            case 3: return new[] { new ScatterRegion(-0.3f, 4.6f, 0.45f, 0.72f, 0.64f),
                new ScatterRegion(2.6f, -6.6f, 0.68f, 0.28f, 0.70f),
                new ScatterRegion(-3.6f, -0.565f, 0.49f, 0.045f, 0.22f),
                new ScatterRegion(-5.2f, -6.8f, 0.09f, 0.28f, 0.29f) };
            default: return new[] { new ScatterRegion(8.8f, 0.4f, 0.55f, 0.48f, 0.73f),
                new ScatterRegion(8.4f, -5.5f, 0.55f, 0.65f, 0.82f),
                new ScatterRegion(2.3f, -0.1f, 0.62f, 0.68f, 0.76f),
                new ScatterRegion(-6.2f, 2f, 0.58f, 0.25f, 0.61f) };
        }
    }

    private void AddAmbientBurst(ScatterRegion region, System.Random random, List<Vector2> centers,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
    {
        int count = 5 + random.Next(4);
        for (int index = 0; index < count; index++)
        {
            float width = region.size * Range(random, 0.40f, 1f);
            Vector2 size = new Vector2(width, width * Range(random, 0.65f, 1.1f));
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Vector2 center = region.center + new Vector2(Gaussian(random) * region.spread.x,
                    Gaussian(random) * region.spread.y);
                if (!ClearOfAllLetters(center, size) || TooClose(center, centers)
                    || !OpenFloor(new Vector3(center.x, 0f, center.y), size.magnitude * 0.5f)) continue;
                centers.Add(center);
                AddStamp(center, size, Range(random, -Mathf.PI, Mathf.PI), random.NextDouble() < 0.5,
                    random.NextDouble() < 0.5, vertices, normals, uv, triangles);
                break;
            }
        }
    }

    private bool ClearOfAllLetters(Vector2 point, Vector2 size)
    {
        foreach (List<PaintMark> artist in paintMarks)
            foreach (PaintMark mark in artist)
            {
                Vector2 delta = point - mark.position;
                Vector3 local = Quaternion.Euler(0f, -mark.yaw, 0f) * new Vector3(delta.x, 0f, delta.y);
                if (!ClearOfLetters(new Vector2(local.x, local.z), size, mark.size)) return false;
            }
        return true;
    }

    private static bool ClearOfLetters(Vector2 point, Vector2 stampSize, Vector2 titleSize)
    {
        float dx = Mathf.Max(Mathf.Abs(point.x) - titleSize.x * 0.39f, 0f);
        float dz = Mathf.Max(Mathf.Abs(point.y) - titleSize.y * 0.27f, 0f);
        float coreMargin = Mathf.Max(stampSize.x, stampSize.y) * 0.28f;
        return dx * dx + dz * dz > coreMargin * coreMargin;
    }

    private static bool TooClose(Vector2 point, List<Vector2> centers)
    {
        foreach (Vector2 other in centers)
            if ((point - other).sqrMagnitude < 0.021f) return true;
        return false;
    }

    private static bool OpenFloor(Vector3 point, float margin)
    {
        if (point.x - margin < -10.05f || point.x + margin > 10.12f
            || point.z - margin < -7.45f || point.z + margin > 7.2f) return false;
        // Actual furniture footprints leave the rear and staff aisles available for paint.
        if (InsideExpanded(point, -10.05f, -2.02f, -2.3f, -0.88f, margin)) return false;
        if (InsideExpanded(point, -9.75f, -2.4f, -0.25f, 0.45f, margin)) return false;
        if (InsideExpanded(point, -10.05f, -2.05f, 0.3f, 1.25f, margin)) return false;
        if (InsideExpanded(point, -9.54f, -5.66f, -7.2f, -3.6f, margin)) return false;
        if (InsideExpanded(point, -4.64f, -0.76f, -7.2f, -3.6f, margin)) return false;
        if (InsideExpanded(point, -9.615f, -5.985f, 3.35f, 7.15f, margin)) return false;
        if (InsideExpanded(point, -5.515f, -1.885f, 3.35f, 7.15f, margin)) return false;
        if (InsideExpanded(point, 1.45f, 9.35f, 2.2f, 6.55f, margin)) return false;
        if (InsideExpanded(point, 4.1f, 6.6f, 1.48f, 2.42f, margin)) return false;
        for (int stool = 0; stool < 5; stool++)
        {
            float x = -9.1f + stool * 1.55f;
            if (new Vector2(point.x - x, point.z + 3.15f).sqrMagnitude < Mathf.Pow(0.38f + margin, 2)) return false;
        }
        if (NearPlant(point, -9.55f, -5.9f, 0.38f + margin)
            || NearPlant(point, -1.25f, 5.75f, 0.42f + margin)
            || NearPlant(point, -9.4f, 6.7f, 0.35f + margin)
            || NearPlant(point, 9.73f, 6.65f, 0.31f + margin)) return false;
        return true;
    }

    private static bool NearPlant(Vector3 point, float x, float z, float radius)
    {
        return new Vector2(point.x - x, point.z - z).sqrMagnitude < radius * radius;
    }

    private static bool InsideExpanded(Vector3 point, float minX, float maxX, float minZ, float maxZ, float margin)
    {
        return point.x > minX - margin && point.x < maxX + margin
            && point.z > minZ - margin && point.z < maxZ + margin;
    }

    private static void AddStamp(Vector2 center, Vector2 size, float angle, bool flipX, bool flipY,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
    {
        int first = vertices.Count;
        float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
        for (int corner = 0; corner < 4; corner++)
        {
            float x = ((corner & 1) == 0 ? -0.5f : 0.5f) * size.x;
            float z = ((corner & 2) == 0 ? -0.5f : 0.5f) * size.y;
            vertices.Add(new Vector3(center.x + x * cosine - z * sine, 0f,
                center.y + x * sine + z * cosine));
            normals.Add(Vector3.up);
            float u = (corner & 1) == 0 ? 0f : 1f;
            float v = (corner & 2) == 0 ? 0f : 1f;
            uv.Add(new Vector2(flipX ? 1f - u : u, flipY ? 1f - v : v));
        }
        triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
        triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
    }

    private static float Range(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static float Gaussian(System.Random random)
    {
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(1.0 - random.NextDouble()))
            * System.Math.Cos(2.0 * System.Math.PI * random.NextDouble()));
    }

    private void ReleaseSplatter()
    {
        foreach (Material material in ownedSplatterMaterials) Release(material);
        foreach (Mesh mesh in ownedSplatterMeshes) Release(mesh);
        ownedSplatterMaterials.Clear();
        ownedSplatterMeshes.Clear();
    }
}
