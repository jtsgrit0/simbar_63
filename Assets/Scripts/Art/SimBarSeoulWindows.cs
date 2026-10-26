using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Upper-wall casement glazing with an exterior Seoul night panorama.</summary>
public sealed class SimBarSeoulWindows : MonoBehaviour
{
    public const string RootName = "Seoul Night Windows";
    public const string PanoramaResource = "SimBarArt/Seoul/SeoulNeonNight";
    public const int RearBayCount = 6;
    public const int LeftBayCount = 5;
    public const float SillHeight = 1.67f;
    public const float HeadHeight = 5.0f;

    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();
    private Mesh unitCube, unitQuad;
    private Material graphite, hardware, glass, city, aperture;

    /// <summary>Creates the fixed geometry once, after the venue envelope is instantiated.</summary>
    public static void Build(Transform parent)
    {
        if (parent == null || parent.Find(RootName) != null) return;
        var root = new GameObject(RootName);
        root.transform.SetParent(parent, false);
        root.AddComponent<SimBarSeoulWindows>().Initialize();
    }

    private void Initialize()
    {
        Material opaqueTemplate = Resources.Load<Material>("SimBarArt/Posters/PosterUnlit");
        Material glassTemplate = Resources.Load<Material>("SimBarArt/Seoul/SeoulGlass")
            ?? Resources.Load<Material>("SimBarArt/Graffiti/GraffitiUnlit");
        Material apertureTemplate = Resources.Load<Material>("SimBarArt/Seoul/SeoulAperture");
        Material cityTemplate = Resources.Load<Material>("SimBarArt/Seoul/SeoulNightView");
        if (opaqueTemplate == null || glassTemplate == null || apertureTemplate == null || cityTemplate == null)
        {
            Debug.LogWarning("SimBar Seoul windows require the frame, glass, aperture and night-view resource materials.");
            return;
        }

        unitCube = OwnMesh(CreateCube());
        unitQuad = OwnMesh(CreateQuad());
        graphite = OwnMaterial(opaqueTemplate, "SimBar Graphite Window Frames", new Color(0.075f, 0.09f, 0.105f, 1f), false);
        hardware = OwnMaterial(opaqueTemplate, "SimBar Casement Brushed Hardware", new Color(0.34f, 0.38f, 0.40f, 1f), false);
        glass = OwnMaterial(glassTemplate, "SimBar Clear Blue Grey Casement Glass", new Color(0.48f, 0.64f, 0.70f, 0.115f), true);
        aperture = new Material(apertureTemplate) { name = "SimBar Window Aperture Stencil", renderQueue = (int)RenderQueue.Geometry + 1 };
        ownedMaterials.Add(aperture);

        Texture2D panorama = SimBarWebTextures.Load(PanoramaResource);
        if (panorama != null)
        {
            city = OwnMaterial(cityTemplate, "SimBar Seoul Neon Night Exterior", new Color(0.83f, 0.83f, 0.86f, 1f), false);
            city.renderQueue = (int)RenderQueue.Geometry + 2;
            SetTexture(city, panorama);
        }
        else Debug.LogWarning("SimBar Seoul night panorama is missing: " + PanoramaResource);

        BuildWall("Rear Casement Windows", new Vector3(0f, 0f, 7.395f), 0f, 20.4f, RearBayCount, panorama, 0);
        BuildWall("Left Casement Windows", new Vector3(-10.195f, 0f, 0f), -90f, 15f, LeftBayCount, panorama, 1);
    }

    private void BuildWall(string label, Vector3 position, float yaw, float width, int count, Texture2D panorama, int wallIndex)
    {
        Transform wall = Child(label, transform, position, Quaternion.Euler(0f, yaw, 0f));
        var frameShapes = new List<CombineInstance>();
        var hardwareShapes = new List<CombineInstance>();
        var glassShapes = new List<CombineInstance>();
        float height = HeadHeight - SillHeight;
        float middleY = (HeadHeight + SillHeight) * 0.5f;
        float bayWidth = width / count;

        // A low horizontal sill continues the charcoal masonry. The tall glazing above
        // remains mostly clear, with thin metalwork instead of a dense decorative grid.
        AddCube(frameShapes, wall, new Vector3(0f, SillHeight + 0.045f, 0f), new Vector3(width + 0.12f, 0.09f, 0.17f));
        AddCube(frameShapes, wall, new Vector3(0f, HeadHeight - 0.045f, 0f), new Vector3(width + 0.12f, 0.09f, 0.15f));
        AddCube(hardwareShapes, wall, new Vector3(0f, SillHeight + 0.086f, -0.035f), new Vector3(width + 0.14f, 0.012f, 0.17f));
        for (int boundary = 0; boundary <= count; boundary++)
            AddCube(frameShapes, wall, new Vector3(-width * 0.5f + bayWidth * boundary, middleY, 0f), new Vector3(0.085f, height, 0.14f));

        for (int bay = 0; bay < count; bay++)
        {
            float center = -width * 0.5f + bayWidth * (bay + 0.5f);
            Transform bayRoot = Child("Casement Bay " + (bay + 1), wall, new Vector3(center, 0f, 0f), Quaternion.identity);
            float leafWidth = (bayWidth - 0.12f) * 0.5f;
            float leafHeight = height - 0.20f;

            // Leaves hinge at their outside edges and open toward the street. Their
            // physical glazed planes rotate with the metal perimeter and the handles.
            float leftAngle = (bay + wallIndex * 2) % 3 == 0 ? -14f : 0f;
            float rightAngle = (bay + wallIndex) % 4 == 2 ? 11f : 0f;
            BuildLeaf(bayRoot, "Left Hinged Leaf", -leafWidth, 1f, leafWidth, leafHeight, middleY, leftAngle,
                frameShapes, hardwareShapes, glassShapes);
            BuildLeaf(bayRoot, "Right Hinged Leaf", leafWidth, -1f, leafWidth, leafHeight, middleY, rightAngle,
                frameShapes, hardwareShapes, glassShapes);
        }

        Batch("Graphite Frame Geometry", wall, frameShapes, graphite);
        Batch("Casement Hardware Geometry", wall, hardwareShapes, hardware);
        Batch("Transparent Glass Panes", wall, glassShapes, glass);

        // Write the window opening into stencil after the opaque room has written
        // depth. This aperture is architectural, rather than camera dependent: the
        // oversized exterior panorama cannot appear above or outside the window.
        const float apertureBottom = 1.73f;
        const float apertureTop = 4.94f;
        var opening = Child("Architectural Window Aperture", wall,
            new Vector3(0f, (apertureBottom + apertureTop) * 0.5f, -0.01f), Quaternion.identity);
        opening.localScale = new Vector3(width - 0.085f, apertureTop - apertureBottom, 1f);
        Surface(opening, unitQuad, aperture);

        if (city != null && panorama != null)
        {
            float panoramaWidth = width + 1.8f;
            float panoramaHeight = panoramaWidth * panorama.height / Mathf.Max(1f, panorama.width);
            // The print is outside the physical envelope, visible through actual
            // transparent glass. One continuous image spans the entire wall.
            var backdrop = Child("Exterior Seoul Street Panorama", wall, new Vector3(0f, 3.35f, 0.63f), Quaternion.identity);
            backdrop.localScale = new Vector3(panoramaWidth, panoramaHeight, 1f);
            Surface(backdrop, unitQuad, city);
        }
    }

    private void BuildLeaf(Transform bay, string label, float hingeX, float inwardSign, float width, float height, float middleY,
        float angle, List<CombineInstance> frames, List<CombineInstance> handles, List<CombineInstance> glazing)
    {
        Transform hinge = Child(label, bay, new Vector3(hingeX, middleY, 0.008f), Quaternion.Euler(0f, angle, 0f));
        float centerX = inwardSign * width * 0.5f;
        const float stile = 0.050f;
        AddCube(frames, hinge, new Vector3(0f, 0f, 0f), new Vector3(stile, height, 0.075f));
        AddCube(frames, hinge, new Vector3(inwardSign * width, 0f, 0f), new Vector3(stile, height, 0.075f));
        AddCube(frames, hinge, new Vector3(centerX, -height * 0.5f, 0f), new Vector3(width, stile, 0.075f));
        AddCube(frames, hinge, new Vector3(centerX, height * 0.5f, 0f), new Vector3(width, stile, 0.075f));

        // One substantial, uninterrupted pane per leaf keeps the Seoul lights clear.
        AddQuad(glazing, hinge, new Vector3(centerX, 0f, 0f), new Vector2(width - stile, height - stile));
        float handleX = inwardSign * (width - 0.105f);
        AddCube(handles, hinge, new Vector3(handleX, -0.14f, -0.073f), new Vector3(0.025f, 0.23f, 0.025f));
        AddCube(handles, hinge, new Vector3(handleX, -0.24f, -0.050f), new Vector3(0.042f, 0.035f, 0.065f));
        AddCube(handles, hinge, new Vector3(handleX, -0.04f, -0.050f), new Vector3(0.042f, 0.035f, 0.065f));
        foreach (float y in new[] { -height * 0.34f, height * 0.34f })
            AddCube(handles, hinge, new Vector3(0f, y, -0.015f), new Vector3(0.075f, 0.16f, 0.095f));
    }

    private static Transform Child(string label, Transform parent, Vector3 position, Quaternion rotation)
    {
        var child = new GameObject(label).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        child.localRotation = rotation;
        return child;
    }

    private void AddCube(List<CombineInstance> shapes, Transform parent, Vector3 center, Vector3 size)
    {
        shapes.Add(new CombineInstance
        {
            mesh = unitCube,
            transform = transform.worldToLocalMatrix * parent.localToWorldMatrix * Matrix4x4.TRS(center, Quaternion.identity, size)
        });
    }

    private void AddQuad(List<CombineInstance> shapes, Transform parent, Vector3 center, Vector2 size)
    {
        shapes.Add(new CombineInstance
        {
            mesh = unitQuad,
            transform = transform.worldToLocalMatrix * parent.localToWorldMatrix
                * Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size.x, size.y, 1f))
        });
    }

    private void Batch(string label, Transform wall, List<CombineInstance> shapes, Material material)
    {
        if (shapes.Count == 0) return;
        // Inputs are already transformed to the shared root so no nested leaf needs
        // a renderer or update. The named hinge transforms remain for inspection.
        var mesh = OwnMesh(new Mesh { name = wall.name + " " + label });
        mesh.CombineMeshes(shapes.ToArray(), true, true);
        var batch = Child(label, transform, Vector3.zero, Quaternion.identity);
        Surface(batch, mesh, material);
    }

    private static void Surface(Transform target, Mesh mesh, Material material)
    {
        target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = target.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private Material OwnMaterial(Material template, string label, Color color, bool transparent)
    {
        var material = new Material(template) { name = label };
        SetTexture(material, Texture2D.whiteTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", transparent ? (float)CullMode.Off : (float)CullMode.Back);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1f : 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
        material.SetShaderPassEnabled("ShadowCaster", false);
        if (transparent) material.SetShaderPassEnabled("DepthOnly", false);
        material.doubleSidedGI = false;
        ownedMaterials.Add(material);
        return material;
    }

    private static void SetTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
    }

    private Mesh OwnMesh(Mesh mesh) { ownedMeshes.Add(mesh); return mesh; }

    private static Mesh CreateQuad()
    {
        var mesh = new Mesh { name = "SimBar Window Plane" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateCube()
    {
        var mesh = new Mesh { name = "SimBar Window Metal Box" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 2, 3, 1, 4, 5, 6, 6, 5, 7,
            0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5,
            0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7
        };
        mesh.RecalculateNormals();
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
