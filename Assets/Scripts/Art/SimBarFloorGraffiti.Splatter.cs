using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class SimBarFloorGraffiti
{
    [SerializeField] private List<Mesh> ownedSplatterMeshes = new List<Mesh>();
    [SerializeField] private List<Material> ownedSplatterMaterials = new List<Material>();

    private void CreateSplatter(int artist, Transform title, Vector2 titleSize, Material template)
    {
        Texture2D stamp = Resources.Load<Texture2D>("SimBarArt/Graffiti/SpraySplatter");
        if (stamp == null) return;

        // A private seed keeps the venue stable without consuming gameplay's random sequence.
        var random = new System.Random(0x63A73 + artist * 7919);
        float side = random.NextDouble() < 0.5 ? -1f : 1f;
        float end = random.NextDouble() < 0.5 ? -1f : 1f;
        if (artist == 2) side = -1f; // Away from the stage step.
        if (artist == 3) side = 1f;  // Away from the front wall.
        if (artist == 0) end = 1f;   // Away from the front booth.
        if (artist == 4) end = -1f;  // Away from the right wall.

        Vector2 lobe = new Vector2(end * titleSize.x * Range(random, 0.12f, 0.29f),
            side * (titleSize.y * 0.52f + Range(random, 0.10f, 0.20f)));
        Vector2 oppositeEnd = new Vector2(-end * (titleSize.x * 0.5f + 0.13f),
            -side * titleSize.y * Range(random, 0.10f, 0.45f));
        Vector2 tail = new Vector2(end * Range(random, 0.45f, 0.8f), side * 0.25f);
        var centers = new List<Vector2>();
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        int requested = 13 + artist % 3;

        for (int index = 0; index < requested; index++)
        {
            bool large = index == 0;
            bool satellite = index >= requested - 4;
            Vector2 size = large
                ? new Vector2(Range(random, 1.30f, 1.75f), Range(random, 0.90f, 1.25f))
                : satellite
                    ? new Vector2(Range(random, 0.40f, 0.75f), Range(random, 0.30f, 0.60f))
                    : new Vector2(Range(random, 0.65f, 1.15f), Range(random, 0.50f, 0.95f));
            float angle = Range(random, -Mathf.PI, Mathf.PI);

            for (int attempt = 0; attempt < 80; attempt++)
            {
                Vector2 center;
                if (index < 6)
                    center = lobe + new Vector2(Gaussian(random) * 0.38f, Gaussian(random) * 0.24f);
                else if (!satellite)
                    center = oppositeEnd + new Vector2(Gaussian(random) * 0.24f, Gaussian(random) * 0.37f);
                else
                    center = lobe + tail * Range(random, 0.25f, 1.4f)
                        + new Vector2(Gaussian(random) * 0.32f, Gaussian(random) * 0.24f);

                // A blocked lobe can spill toward another edge instead of being clamped into a row.
                if (attempt > 32 && random.NextDouble() < 0.5) center.y = -center.y;
                if (!ClearOfLetters(center, size, titleSize) || TooClose(center, centers)) continue;
                Vector3 world = title.TransformPoint(new Vector3(center.x / titleSize.x, 0f, center.y / titleSize.y));
                float radius = size.magnitude * 0.5f;
                if (!OpenFloor(world, radius, artist)) continue;

                centers.Add(center);
                AddStamp(center, size, angle, random.NextDouble() < 0.5,
                    random.NextDouble() < 0.5, vertices, normals, uv, triangles);
                break;
            }
        }

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
        surface.transform.localPosition = title.localPosition + Vector3.down * 0.002f;
        surface.transform.localRotation = title.localRotation;
        // Offsets and stamp dimensions are metres, independent of the fitted title texture scale.
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

    private static bool OpenFloor(Vector3 point, float margin, int artist)
    {
        if (point.x - margin < -9.95f || point.x + margin > 9.5f
            || point.z - margin < -7.25f || point.z + margin > 7.15f) return false;
        if (artist == 2 && point.z + margin > 1.4f) return false;
        if (artist == 0 && point.x - margin < -1.6f) return false;
        if (InsideExpanded(point, -10.1f, -2.0f, -2.85f, 1.5f, margin)) return false;
        if (InsideExpanded(point, -9.75f, -0.7f, -7.3f, -3.5f, margin)) return false;
        if (InsideExpanded(point, -9.75f, -1.7f, 3.25f, 7.2f, margin)) return false;
        if (InsideExpanded(point, 1.25f, 9.6f, 2.1f, 6.85f, margin)) return false;
        if (InsideExpanded(point, 4.1f, 6.6f, 1.48f, 2.42f, margin)) return false;
        for (int stool = 0; stool < 5; stool++)
        {
            float x = -9.1f + stool * 1.55f;
            if (new Vector2(point.x - x, point.z + 3.15f).sqrMagnitude < Mathf.Pow(0.38f + margin, 2)) return false;
        }
        return true;
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
