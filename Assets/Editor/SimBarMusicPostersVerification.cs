using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Read-only checks of the running flyposter collage and transparent floor spray paint.</summary>
public static class SimBarMusicPostersVerification
{
    [MenuItem("SimBar/Validation/Check Running Music Art")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        if (!Application.isPlaying)
        {
            report.failures.Add("Enter Play Mode in the reference venue before checking the music art.");
            return report.ToString();
        }
        try { CheckWalls(report); }
        catch (Exception error) { report.failures.Add("Wall collage: " + error.GetBaseException()); }
        try { CheckFloor(report); }
        catch (Exception error) { report.failures.Add("Floor graffiti: " + error.GetBaseException()); }
        report.measurements.Add("Validation reads the live scene and source image files; persistent assets and live gameplay state are unchanged.");
        return report.ToString();
    }

    private static void CheckWalls(SimBarRuntimeVerification.Report report)
    {
        SimBarMusicPosters owner = Object.FindAnyObjectByType<SimBarMusicPosters>();
        report.Check(owner != null, "The running reference venue contains the wall collage.");
        if (owner == null) return;
        Material template = Resources.Load<Material>("SimBarArt/Posters/PosterUnlit");
        report.Check(Opaque(template) && Unlit(template) && template.enabledKeywords.Length == 0,
            "The poster Resource material retains the opaque URP Unlit shader variant.");
        string[] keys = { "Queen", "Nirvana", "TheBeatles", "PopNight", "TaylorSwift", "Blackpink" };
        var textures = new HashSet<Texture2D>();
        foreach (string key in keys)
        {
            Texture2D texture = Resources.Load<Texture2D>("SimBarArt/Posters/" + key);
            report.Check(texture != null, key + " wall artwork loads from Resources.");
            if (texture == null) continue;
            textures.Add(texture);
            CheckImport(report, texture, key + " wall artwork", false);
        }
        var materials = new HashSet<Material>();
        var usedTextures = new HashSet<Texture>();
        int artworkCount = 0, tapeCount = 0, paperCount = 0;
        Transform[] walls = {
            owner.transform.Find(SimBarMusicPosters.BackWallRootName),
            owner.transform.Find(SimBarMusicPosters.LeftWallRootName)
        };
        for (int w = 0; w < walls.Length; w++)
        {
            Transform wall = walls[w];
            report.Check(wall != null, "The " + (w == 0 ? "back" : "left") + " wall has a collage group.");
            if (wall == null) continue;
            float highestPaper = float.NegativeInfinity;
            foreach (MeshFilter surface in wall.GetComponentsInChildren<MeshFilter>())
                foreach (Vector3 vertex in surface.sharedMesh.vertices)
                    highestPaper = Mathf.Max(highestPaper, wall.InverseTransformPoint(surface.transform.TransformPoint(vertex)).y);
            report.Check(highestPaper <= 2.36f, wall.name + " keeps the upper window view clear, including rotated paper and tape.");
            Vector3 expected = owner.transform.TransformDirection(w == 0 ? Vector3.back : Vector3.right);
            var areas = new List<Rect>();
            float minHeight = float.PositiveInfinity, maxHeight = float.NegativeInfinity;
            float minSize = float.PositiveInfinity, maxSize = 0f;
            bool positiveAngle = false, negativeAngle = false;
            foreach (Transform group in wall)
            {
                if (group.name.StartsWith("Ripped Gig Paper", StringComparison.Ordinal)) paperCount++;
                Transform artwork = group.Find("Artwork");
                if (artwork == null) continue;
                artworkCount++;
                Renderer renderer = artwork.GetComponent<Renderer>();
                MeshFilter filter = artwork.GetComponent<MeshFilter>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (material != null) materials.Add(material);
                if (texture != null) usedTextures.Add(texture);
                report.Check(renderer != null && mesh != null && texture != null && textures.Contains(texture)
                    && material != template && template != null && material.shader == template.shader && Opaque(material),
                    group.name + " binds an imported artist image to an opaque Unlit material clone.");
                if (mesh == null || texture == null) continue;
                report.Check(Vector3.Dot(artwork.TransformDirection(mesh.normals[0]).normalized, expected) > 0.999f
                    && CorrectWinding(mesh), group.name + " faces inward with matching front-face winding.");
                report.Check(Mathf.Abs(artwork.localScale.x / artwork.localScale.y - texture.width / (float)texture.height) < 0.001f
                    && PosterUv(mesh), group.name + " preserves lettering proportions and non-mirrored image UVs.");
                report.Check(mesh.vertexCount > 4 && HasBatchedPaperBorder(wall, artwork, mesh),
                    group.name + " has irregular edges with a paper border retained in the combined decoration mesh.");
                minHeight = Mathf.Min(minHeight, group.localPosition.y);
                maxHeight = Mathf.Max(maxHeight, group.localPosition.y);
                minSize = Mathf.Min(minSize, artwork.localScale.y);
                maxSize = Mathf.Max(maxSize, artwork.localScale.y);
                float angle = Mathf.DeltaAngle(0f, group.localEulerAngles.z);
                positiveAngle |= angle > 3f; negativeAngle |= angle < -3f;
                areas.Add(PaperRect(wall, artwork, mesh));
                foreach (Transform child in group)
                    if (child.name == "Old Masking Tape") tapeCount++;
            }
            bool overlaps = false;
            for (int a = 0; a < areas.Count; a++)
                for (int b = a + 1; b < areas.Count; b++) overlaps |= areas[a].Overlaps(areas[b]);
            report.Check(areas.Count >= 6 && maxHeight - minHeight > 0.8f && maxSize > minSize * 2f
                && positiveAngle && negativeAngle, wall.name + " varies poster heights, sizes and tilt directions.");
            report.Check(overlaps, wall.name + " layers overlapping papers instead of placing a straight row.");
        }
        report.Check(artworkCount == SimBarMusicPosters.ArtworkCount && artworkCount >= 12,
            "Both walls contain the complete layered flyposter collection.");
        report.Check(materials.Count == keys.Length && usedTextures.Count == keys.Length,
            "All six artist images appear, sharing one material per image across repeat posters.");
        report.Check(tapeCount >= artworkCount && paperCount >= 6,
            "Aged masking tape and torn old gig papers remain visible around the collaged art.");
        bool framesAbsent = true;
        foreach (Transform child in owner.GetComponentsInChildren<Transform>(true))
            framesAbsent &= child.name.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) < 0
                && child.name.IndexOf("Cream Mat", StringComparison.OrdinalIgnoreCase) < 0;
        report.Check(framesAbsent, "The collage uses pasted paper without new picture frames or mats.");
        CheckDecorativeOnly(report, owner, "Wall collage");
        report.measurements.Add("Wall collage: " + artworkCount + " artworks, " + materials.Count + " artist materials, "
            + paperCount + " torn remnants and " + tapeCount + " tape strips across two walls.");
    }

    private static void CheckFloor(SimBarRuntimeVerification.Report report)
    {
        SimBarFloorGraffiti owner = Object.FindAnyObjectByType<SimBarFloorGraffiti>();
        report.Check(owner != null, "The running venue contains the floor graffiti.");
        if (owner == null) return;
        Material template = Resources.Load<Material>("SimBarArt/Graffiti/GraffitiUnlit");
        report.Check(Transparent(template) && Unlit(template), "The spray-paint Resource material retains the transparent URP Unlit variant.");
        var materials = new HashSet<Material>();
        var meshes = new HashSet<Mesh>();
        var pigments = new List<Color>();
        int titleCount = 0;
        bool positiveYaw = false, negativeYaw = false;
        foreach (string key in SimBarFloorGraffiti.ArtistKeys)
        {
            Texture2D texture = Resources.Load<Texture2D>("SimBarArt/Graffiti/" + key);
            Transform paint = owner.transform.Find(key);
            report.Check(texture != null && paint != null, key + " floor name and Resource texture are present.");
            if (texture == null || paint == null) continue;
            CheckImport(report, texture, key + " floor paint", true);
            MeshFilter filter = paint.GetComponent<MeshFilter>();
            Renderer renderer = paint.GetComponent<Renderer>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (mesh != null) meshes.Add(mesh);
            if (material != null) materials.Add(material);
            report.Check(mesh != null && material != null && material != template && template != null
                && material.shader == template.shader && material.mainTexture == texture && Transparent(material),
                key + " uses an alpha-blended material clone with its own transparent image.");
            if (mesh == null || material == null) continue;
            Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            report.Check(tint.a > 0.5f && tint.a < 1f, key + " retains partial paint opacity so the grey floor reads through.");
            report.Check(paint.localPosition.y > 0.0655f && paint.localPosition.y < 0.09f
                && Vector3.Dot(paint.TransformDirection(mesh.normals[0]).normalized, Vector3.up) > 0.999f
                && CorrectWinding(mesh), key + " sits just above the floor with an upward visible face.");
            bool titleQuads = FloorTitleQuads(mesh, paint, owner.transform,
                texture.width / (float)texture.height, out int count);
            titleCount += count;
            report.Check(titleQuads && count == 2,
                key + " batches two upward floor tags with correct lettering proportions and unmirrored UVs.");
            float angle = Mathf.DeltaAngle(0f, paint.localEulerAngles.y);
            positiveYaw |= angle > 3f; negativeYaw |= angle < -3f;
            if (CheckPaintPixels(report, texture, tint, key, out Color pigment)) pigments.Add(pigment);
        }
        report.Check(materials.Count == 5 && meshes.Count == 5 && titleCount == SimBarFloorGraffiti.TitleCount,
            "Ten floor names are combined into five artist meshes and five alpha materials.");
        report.Check(positiveYaw && negativeYaw, "Floor names vary their spray angles in both directions.");
        bool distinct = pigments.Count == 5;
        float smallestHueGap = 1f;
        for (int a = 0; a < pigments.Count; a++)
            for (int b = a + 1; b < pigments.Count; b++)
            {
                Color.RGBToHSV(pigments[a], out float ha, out _, out _);
                Color.RGBToHSV(pigments[b], out float hb, out _, out _);
                float gap = Mathf.Abs(Mathf.DeltaAngle(ha * 360f, hb * 360f)) / 360f;
                smallestHueGap = Mathf.Min(smallestHueGap, gap);
                distinct &= gap > 0.025f;
            }
        report.Check(distinct, "Rendered floor pigments distinguish all five artist names by hue.");
        CheckSplatter(report, owner);
        CheckDecorativeOnly(report, owner, "Floor graffiti");
        report.measurements.Add("Floor paint: " + titleCount + " tags in " + meshes.Count + " artist meshes and "
            + materials.Count + " alpha materials; minimum artist hue gap "
            + (smallestHueGap * 360f).ToString("F1") + " degrees.");
    }

    private static void CheckSplatter(SimBarRuntimeVerification.Report report, SimBarFloorGraffiti owner)
    {
        Texture2D stamp = Resources.Load<Texture2D>("SimBarArt/Graffiti/SpraySplatter");
        report.Check(stamp != null, "The transparent spray-droplet stamp loads from Resources.");
        if (stamp == null) return;
        CheckImport(report, stamp, "Spray droplets", true);
        int stamps = 0;
        float minSize = float.PositiveInfinity, maxSize = 0f;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool valid = true;
        foreach (string key in SimBarFloorGraffiti.ArtistKeys)
        {
            Transform batch = owner.transform.Find(key + " Spray Splatter");
            if (batch == null) { valid = false; continue; }
            Mesh mesh = batch.GetComponent<MeshFilter>().sharedMesh;
            Material material = batch.GetComponent<Renderer>().sharedMaterial;
            valid &= mesh != null && material != null && material.mainTexture == stamp && Transparent(material)
                && batch.localPosition.y > 0.0655f && batch.localPosition.y < 0.09f;
            if (mesh == null) continue;
            Vector3[] vertices = mesh.vertices;
            stamps += vertices.Length / 4;
            for (int v = 0; v + 3 < vertices.Length; v += 4)
            {
                float size = Vector3.Distance(batch.TransformPoint(vertices[v]), batch.TransformPoint(vertices[v + 1]));
                minSize = Mathf.Min(minSize, size); maxSize = Mathf.Max(maxSize, size);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = owner.transform.InverseTransformPoint(batch.TransformPoint(vertices[v + corner]));
                    minimum = Vector2.Min(minimum, new Vector2(point.x, point.z));
                    maximum = Vector2.Max(maximum, new Vector2(point.x, point.z));
                }
            }
        }
        report.Check(valid && stamps >= 45, "Five colored, transparent floor batches contain dispersed spray droplets.");
        report.Check(maxSize > minSize * 2f, "Droplet clusters vary between small satellites and larger paint impacts.");
        report.measurements.Add("Spray splatter: " + stamps + " irregular stamps in five batches; sizes "
            + minSize.ToString("F2") + "–" + maxSize.ToString("F2") + " m; floor coverage X "
            + minimum.x.ToString("F2") + "–" + maximum.x.ToString("F2") + ", Z "
            + minimum.y.ToString("F2") + "–" + maximum.y.ToString("F2") + " m.");
    }

    private static void CheckImport(SimBarRuntimeVerification.Report report, Texture2D texture, string label, bool alpha)
    {
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        int sourceWidth = 0, sourceHeight = 0;
        if (importer != null) importer.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
        report.Check(importer != null && importer.maxTextureSize == 512 && importer.npotScale == TextureImporterNPOTScale.None
            && !importer.isReadable && importer.mipmapEnabled && texture.mipmapCount > 1
            && texture.wrapMode == TextureWrapMode.Clamp && texture.filterMode == FilterMode.Trilinear,
            label + " uses a 512-pixel, non-readable, clamped mipmapped import without NPOT stretching.");
        report.Check(sourceWidth > 0 && sourceHeight > 0
            && Mathf.Abs(texture.width / (float)texture.height - sourceWidth / (float)sourceHeight) < 0.012f,
            label + " retains its source aspect ratio after import.");
        if (alpha)
            report.Check(importer != null && importer.alphaSource == TextureImporterAlphaSource.FromInput
                && importer.DoesSourceTextureHaveAlpha() && texture.format == TextureFormat.RGBA32,
                label + " retains source transparency in RGBA32 instead of an opaque rectangle.");
    }

    private static bool CheckPaintPixels(SimBarRuntimeVerification.Report report, Texture2D texture, Color tint, string key, out Color pigment)
    {
        pigment = Color.black;
        Texture2D probe = null;
        try
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(texture)));
            probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(probe, File.ReadAllBytes(path), false))
            {
                report.failures.Add(key + " source PNG could not be decoded for transparency validation.");
                return false;
            }
            Color32[] pixels = probe.GetPixels32();
            int samples = 0, transparent = 0, soft = 0;
            float weightSum = 0f;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pixels.Length; i += 13)
            {
                Color color = pixels[i]; samples++;
                if (color.a < 0.02f) transparent++;
                if (color.a > 0.02f && color.a < 0.98f) soft++;
                Color.RGBToHSV(color, out _, out float saturation, out _);
                float weight = color.a * saturation;
                sum += new Vector3(color.r * tint.r, color.g * tint.g, color.b * tint.b) * weight;
                weightSum += weight;
            }
            report.Check(transparent > samples * 0.15f && soft > samples * 0.005f,
                key + " source PNG has transparent negative space and soft spray-paint edges.");
            if (weightSum <= 0.001f) return false;
            Vector3 mean = sum / weightSum;
            pigment = new Color(mean.x, mean.y, mean.z, 1f);
            return true;
        }
        finally { if (probe != null) Object.DestroyImmediate(probe); }
    }

    private static void CheckDecorativeOnly(SimBarRuntimeVerification.Report report, Component owner, string label)
    {
        report.Check(owner.GetComponentsInChildren<Collider>(true).Length == 0, label + " adds no collision obstacles.");
        bool shadowsOff = true;
        foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
            shadowsOff &= renderer.shadowCastingMode == ShadowCastingMode.Off && !renderer.receiveShadows;
        report.Check(shadowsOff, label + " casts and receives no shadows.");
    }

    private static Rect PaperRect(Transform wall, Transform artwork, Mesh mesh)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 vertex in mesh.vertices)
        {
            Vector3 point = wall.InverseTransformPoint(artwork.TransformPoint(vertex));
            min = Vector2.Min(min, new Vector2(point.x, point.y)); max = Vector2.Max(max, new Vector2(point.x, point.y));
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool HasBatchedPaperBorder(Transform wall, Transform artwork, Mesh mesh)
    {
        // Paper and tape are batched for draw-call efficiency. Inspect their actual geometry
        // in artwork coordinates instead of requiring a separate border GameObject.
        Vector3[] edge = mesh.vertices;
        var matched = new HashSet<int>();
        var quadrants = new bool[4];
        foreach (MeshFilter batch in wall.GetComponentsInChildren<MeshFilter>())
        {
            if (!batch.name.StartsWith("Paper Layers ", StringComparison.Ordinal) || batch.sharedMesh == null) continue;
            Renderer renderer = batch.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled || !Opaque(renderer.sharedMaterial)) continue;
            Matrix4x4 intoArtwork = artwork.worldToLocalMatrix * batch.transform.localToWorldMatrix;
            foreach (Vector3 vertex in batch.sharedMesh.vertices)
            {
                Vector3 point = intoArtwork.MultiplyPoint3x4(vertex);
                // The edge is slightly behind the printed sheet and slightly outside it.
                // This excludes front-facing tape and distant underlying flyer remnants.
                if (point.z <= 0.0001f || point.z >= 0.02f) continue;
                Vector2 position = new Vector2(point.x, point.y);
                for (int i = 0; i < edge.Length; i++)
                {
                    Vector2 target = new Vector2(edge[i].x, edge[i].y);
                    if (target.sqrMagnitude < 0.1f) continue;
                    Vector2 outward = position - target;
                    if (outward.sqrMagnitude < 0.000001f || outward.sqrMagnitude > 0.0016f
                        || Vector2.Dot(outward, target) <= 0.0005f) continue;
                    matched.Add(i);
                    quadrants[(target.x >= 0f ? 1 : 0) + (target.y >= 0f ? 2 : 0)] = true;
                    break;
                }
            }
        }
        return matched.Count >= 8 && quadrants[0] && quadrants[1] && quadrants[2] && quadrants[3];
    }

    private static bool CorrectWinding(Mesh mesh)
    {
        int[] triangles = mesh.triangles; Vector3[] vertices = mesh.vertices;
        return triangles.Length >= 3 && mesh.normals.Length > 0
            && Vector3.Dot(Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]], vertices[triangles[2]] - vertices[triangles[0]]).normalized,
                mesh.normals[triangles[0]]) > 0.999f;
    }

    private static bool PosterUv(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices; Vector2[] uv = mesh.uv;
        if (uv.Length != vertices.Length) return false;
        for (int i = 0; i < vertices.Length; i++)
            if (Vector2.Distance(uv[i], new Vector2(vertices[i].x + 0.5f, vertices[i].y + 0.5f)) > 0.001f) return false;
        return true;
    }

    private static bool FloorTitleQuads(Mesh mesh, Transform paint, Transform owner, float textureAspect, out int count)
    {
        Vector3[] vertices = mesh.vertices; Vector2[] uv = mesh.uv;
        count = vertices.Length / 4;
        if (vertices.Length == 0 || vertices.Length % 4 != 0 || uv.Length != vertices.Length
            || mesh.triangles.Length != count * 6) return false;
        for (int first = 0; first < vertices.Length; first += 4)
        {
            Vector3[] points = new Vector3[4];
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 expected = new Vector2((corner & 1) == 0 ? 0f : 1f, (corner & 2) == 0 ? 0f : 1f);
                if (Vector2.Distance(uv[first + corner], expected) > 0.001f) return false;
                points[corner] = owner.InverseTransformPoint(paint.TransformPoint(vertices[first + corner]));
                Vector3 point = points[corner];
                if (point.x < -10.12f || point.x > 10.2f || point.z < -7.5f || point.z > 7.25f
                    || point.y <= 0.0655f || point.y >= 0.09f) return false;
            }
            Vector3 across = points[1] - points[0], upImage = points[2] - points[0];
            if (upImage.magnitude <= 0f || Mathf.Abs(across.magnitude / upImage.magnitude - textureAspect) > 0.002f
                || Mathf.Abs(Vector3.Dot(across.normalized, upImage.normalized)) > 0.001f
                || Vector3.Dot(Vector3.Cross(upImage, across).normalized, Vector3.up) < 0.999f) return false;
        }
        return true;
    }

    private static bool Unlit(Material material) => material != null && material.shader != null
        && material.shader.name == "Universal Render Pipeline/Unlit";

    private static bool Opaque(Material material) => material != null && material.renderQueue <= (int)RenderQueue.GeometryLast
        && material.GetTag("RenderType", false) == "Opaque" && !material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
        && material.HasProperty("_Surface") && Mathf.Approximately(material.GetFloat("_Surface"), 0f)
        && material.HasProperty("_Cull") && Mathf.Approximately(material.GetFloat("_Cull"), (float)CullMode.Back)
        && material.HasProperty("_ZWrite") && Mathf.Approximately(material.GetFloat("_ZWrite"), 1f);

    private static bool Transparent(Material material) => material != null && material.renderQueue > (int)RenderQueue.GeometryLast
        && material.GetTag("RenderType", false) == "Transparent" && material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
        && material.HasProperty("_Surface") && Mathf.Approximately(material.GetFloat("_Surface"), 1f)
        && material.HasProperty("_ZWrite") && Mathf.Approximately(material.GetFloat("_ZWrite"), 0f)
        && material.HasProperty("_SrcBlend") && Mathf.Approximately(material.GetFloat("_SrcBlend"), (float)BlendMode.SrcAlpha)
        && material.HasProperty("_DstBlend") && Mathf.Approximately(material.GetFloat("_DstBlend"), (float)BlendMode.OneMinusSrcAlpha)
        && material.HasProperty("_Cull") && Mathf.Approximately(material.GetFloat("_Cull"), (float)CullMode.Back)
        && material.HasProperty("_AlphaClip") && Mathf.Approximately(material.GetFloat("_AlphaClip"), 0f);
}
