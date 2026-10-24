using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Read-only contracts for the gray venue envelope and glazed Seoul streetscape.</summary>
public static class SimBarSeoulVenueVerification
{
    [MenuItem("SimBar/Validation/Check Running Seoul Venue")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        if (!Application.isPlaying)
        {
            report.failures.Add("Enter Play Mode in SimBar_Reference before checking the Seoul venue.");
            return report.ToString();
        }
        try { CheckEnvelope(report); }
        catch (Exception error) { report.failures.Add("Gray venue envelope: " + error.GetBaseException()); }
        try { CheckWindows(report); }
        catch (Exception error) { report.failures.Add("Seoul glazing: " + error.GetBaseException()); }
        try { CheckPanorama(report); }
        catch (Exception error) { report.failures.Add("Seoul panorama: " + error.GetBaseException()); }
        report.measurements.Add("Read-only inspection; no assets, preferences or live gameplay state are changed.");
        return report.ToString();
    }

    private static void CheckEnvelope(SimBarRuntimeVerification.Report report)
    {
        GameObject source = Resources.Load<GameObject>("SimBarArt/SimBarVenueReady");
        MeshFilter[] sourceFilters = source != null ? source.GetComponentsInChildren<MeshFilter>(true) : Array.Empty<MeshFilter>();
        bool sourceValid = sourceFilters.Length == 10;
        foreach (MeshFilter filter in sourceFilters) sourceValid &= filter.sharedMesh != null;
        report.Check(sourceValid, "All ten assembled venue meshes remain bound in the imported Resource prefab.");

        MeshFilter floor = null, walls = null;
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (filter.name == "SimBar_Floor") floor = filter;
            if (filter.name == "SimBar_BrickWalls") walls = filter;
        }
        report.Check(floor != null && walls != null, "The live reference venue contains the imported floor and masonry envelope.");
        if (floor == null || walls == null) return;
        MeshFilter[] liveFilters = floor.transform.parent.GetComponentsInChildren<MeshFilter>(true);
        bool liveValid = liveFilters.Length == 10;
        foreach (MeshFilter filter in liveFilters) liveValid &= filter.sharedMesh != null;
        report.Check(liveValid, "All ten venue meshes remain bound in the running instance.");

        MeshRenderer floorRenderer = floor.GetComponent<MeshRenderer>();
        MeshRenderer wallRenderer = walls.GetComponent<MeshRenderer>();
        report.Check(floorRenderer != null && Mathf.Abs(floorRenderer.bounds.max.y - 0.0615f) < 0.003f,
            "The continuous concrete walking surface retains its gameplay elevation.");
        report.Check(wallRenderer != null && Mathf.Abs(wallRenderer.bounds.max.y - SimBarSeoulWindows.SillHeight) < 0.005f,
            "Solid charcoal masonry occupies the lower third, leaving the upper wall open for glazing.");
        report.Check(floor.sharedMesh != null && floor.sharedMesh.vertexCount < 1000
            && walls.sharedMesh != null && walls.sharedMesh.vertexCount < 1000,
            "The envelope contains simple continuous surfaces rather than the old tile grid and individual bricks.");

        Material floorMaterial = SingleMaterial(floorRenderer);
        Material wallMaterial = SingleMaterial(wallRenderer);
        report.Check(floorMaterial != null && floorMaterial.name.Contains("FloorConcrete"),
            "A single FloorConcrete material covers the floor without alternating brown tiles.");
        report.Check(wallMaterial != null && wallMaterial.name.Contains("WallCharcoal"),
            "A single WallCharcoal material covers the low walls without brick colors.");
        if (floorMaterial != null && wallMaterial != null)
        {
            Color floorColor = MaterialColor(floorMaterial), wallColor = MaterialColor(wallMaterial);
            report.Check(Luminance(floorColor) > Luminance(wallColor) + 0.10f,
                "The neutral gray floor is visibly lighter than the charcoal walls.");
            report.Check(NeutralGray(floorColor) && NeutralGray(wallColor),
                "Both new envelope finishes remain neutral or cool gray rather than brown.");
            report.measurements.Add("Envelope: floor top " + floorRenderer.bounds.max.y.ToString("F4")
                + "m, wall top " + wallRenderer.bounds.max.y.ToString("F3") + "m, floor/wall luminance "
                + Luminance(floorColor).ToString("F3") + "/" + Luminance(wallColor).ToString("F3") + ".");
        }
    }

    private static void CheckWindows(SimBarRuntimeVerification.Report report)
    {
        SimBarSeoulWindows[] owners = Object.FindObjectsByType<SimBarSeoulWindows>(FindObjectsSortMode.None);
        report.Check(owners.Length == 1, "Exactly one static Seoul window system is active.");
        if (owners.Length == 0) return;
        SimBarSeoulWindows owner = owners[0];
        int leaves = 0, open = 0, apertures = 0, panoramas = 0, glassBatches = 0;
        bool shadowsOff = true, clippingBounds = true, outdoor = true, imageAspect = true;
        foreach (Transform child in owner.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.EndsWith("Hinged Leaf", StringComparison.Ordinal))
            {
                leaves++;
                float angle = Mathf.DeltaAngle(0f, child.localEulerAngles.y);
                if (Mathf.Abs(angle) > 1f) open++;
            }
            if (child.name == "Architectural Window Aperture")
            {
                apertures++;
                float low = child.localPosition.y - child.localScale.y * 0.5f;
                float high = child.localPosition.y + child.localScale.y * 0.5f;
                clippingBounds &= low >= SimBarSeoulWindows.SillHeight && high <= SimBarSeoulWindows.HeadHeight;
                Material mask = child.GetComponent<Renderer>()?.sharedMaterial;
                clippingBounds &= mask != null && mask.shader.name == "SimBar/Seoul Window Aperture"
                    && mask.renderQueue == (int)RenderQueue.Geometry + 1;
            }
            if (child.name == "Exterior Seoul Street Panorama")
            {
                panoramas++;
                outdoor &= child.localPosition.z > 0.3f;
                Material material = child.GetComponent<Renderer>()?.sharedMaterial;
                Texture texture = material != null ? material.mainTexture : null;
                imageAspect &= texture != null && Mathf.Abs(child.localScale.x / child.localScale.y - texture.width / (float)texture.height) < 0.003f;
                outdoor &= material != null && material.shader.name == "SimBar/Seoul Night Window View"
                    && material.renderQueue == (int)RenderQueue.Geometry + 2;
            }
        }
        foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
        {
            shadowsOff &= renderer.shadowCastingMode == ShadowCastingMode.Off && !renderer.receiveShadows;
            if (renderer.name != "Transparent Glass Panes") continue;
            glassBatches++;
            Material material = renderer.sharedMaterial;
            Color tint = material != null ? MaterialColor(material) : Color.clear;
            report.Check(material != null && material.renderQueue >= (int)RenderQueue.Transparent
                && material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f
                && material.HasProperty("_ZWrite") && material.GetFloat("_ZWrite") < 0.5f
                && tint.a >= 0.05f && tint.a <= 0.20f,
                "The " + renderer.name + " renderer uses lightly tinted, genuinely transparent glazing.");
        }
        report.Check(leaves == 22 && open == 6, "Twenty-two hinged casements include six outward-open leaves.");
        report.Check(apertures == 2 && clippingBounds, "Two architectural stencil apertures limit the street view to the upper-wall openings.");
        report.Check(panoramas == 2 && outdoor && imageAspect,
            "Two full-aspect Seoul panoramas sit outside the walls and use the aperture-tested night-view shader.");
        report.Check(glassBatches == 2, "Rear and left glazing are batched into two transparent renderers.");
        report.Check(owner.GetComponentsInChildren<Collider>(true).Length == 0, "The window scenery adds no NPC or player collision obstacles.");
        report.Check(shadowsOff, "The decorative window system casts and receives no shadows.");
        foreach (string key in new[] { "SeoulAperture", "SeoulNightView" })
        {
            Material material = Resources.Load<Material>("SimBarArt/Seoul/" + key);
            report.Check(material != null && material.shader != null && material.shader.isSupported
                && !ShaderUtil.ShaderHasError(material.shader), key + " Resource shader is supported and has no compilation errors.");
        }
        report.measurements.Add("Windows: " + leaves + " leaves, " + open + " open, " + apertures + " clipped apertures, "
            + panoramas + " outdoor panoramas and " + glassBatches + " glass batches.");
    }

    private static void CheckPanorama(SimBarRuntimeVerification.Report report)
    {
        Texture2D texture = Resources.Load<Texture2D>(SimBarSeoulWindows.PanoramaResource);
        report.Check(texture != null, "The Seoul neon night image loads from Resources.");
        if (texture == null) return;
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        report.Check(importer != null && importer.maxTextureSize == 2048 && importer.mipmapEnabled
            && importer.wrapMode == TextureWrapMode.Clamp && importer.npotScale == TextureImporterNPOTScale.None
            && importer.sRGBTexture && texture.mipmapCount > 1 && texture.width <= 2048 && texture.height <= 2048,
            "The panorama imports at up to 2048 pixels with mipmaps, clamped edges and preserved NPOT proportions.");
        using (FileStream source = File.OpenRead(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path))))
        {
            byte[] header = new byte[24];
            int count = source.Read(header, 0, header.Length);
            int width = count == header.Length ? BigEndianInt(header, 16) : 0;
            int height = count == header.Length ? BigEndianInt(header, 20) : 0;
            report.Check(width > 0 && height > 0 && Mathf.Abs(texture.width / (float)texture.height - width / (float)height) < 0.005f,
                "Imported image proportions match the source PNG rather than stretching the city.");
            report.measurements.Add("Panorama: source " + width + "x" + height + ", imported " + texture.width + "x" + texture.height + ".");
        }
    }

    private static int BigEndianInt(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
    private static Material SingleMaterial(Renderer renderer) => renderer != null && renderer.sharedMaterials.Length == 1 ? renderer.sharedMaterial : null;
    private static Color MaterialColor(Material material) => material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
    private static float Luminance(Color color) => color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    private static bool NeutralGray(Color color) => color.b >= color.r * 0.95f && Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b) < 0.20f;
}
