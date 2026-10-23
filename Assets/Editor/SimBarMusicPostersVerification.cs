using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Read-only checks for the six wall posters and their imported textures.</summary>
public static class SimBarMusicPostersVerification
{
    [MenuItem("SimBar/Validation/Check Running Music Posters")]
    public static void CheckMenu() => Debug.Log(Verify());

    public static string Verify()
    {
        var report = new SimBarRuntimeVerification.Report();
        SimBarMusicPosters owner = Object.FindAnyObjectByType<SimBarMusicPosters>();
        if (!Application.isPlaying || owner == null)
        {
            report.failures.Add("Enter Play Mode in the reference venue before checking the music posters.");
            return report.ToString();
        }
        string[] names = { "Nirvana", "TheBeatles", "Blackpink", "Queen", "PopNight", "TaylorSwift" };
        Vector3[] positions = {
            new Vector3(-8.3f, 3.72f, 7.105f), new Vector3(-4.8f, 3.72f, 7.105f), new Vector3(-0.95f, 3.72f, 7.105f),
            new Vector3(-9.995f, 3.72f, -3.65f), new Vector3(-9.995f, 3.72f, -0.95f), new Vector3(-9.995f, 3.72f, 3.65f) };
        var imageMaterials = new HashSet<Material>();
        var imageMeshes = new HashSet<Mesh>();
        try
        {
            Material template = Resources.Load<Material>("SimBarArt/Posters/PosterUnlit");
            report.Check(Opaque(template) && template.shader != null && template.shader.name == "Universal Render Pipeline/Unlit"
                && template.enabledKeywords.Length == 0,
                "The Resource template explicitly retains the opaque URP Unlit variant with no enabled keywords.");
            report.Check(owner.transform.childCount == 6, "Exactly six music poster groups are installed.");
            for (int i = 0; i < names.Length; i++)
            {
                string key = names[i];
                Texture2D texture = Resources.Load<Texture2D>("SimBarArt/Posters/" + key);
                Transform poster = owner.transform.Find(key);
                report.Check(texture != null && texture.width > 0 && texture.height > 0, key + " texture loads from Resources.");
                report.Check(texture != null && texture.mipmapCount > 1 && texture.wrapMode == TextureWrapMode.Clamp,
                    key + " uses mipmaps and clamped wrapping.");
                report.Check(poster != null && Close(poster.localPosition, positions[i]), key + " occupies its specified wall position.");
                if (poster == null || texture == null) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                int sourceWidth = 0, sourceHeight = 0;
                if (importer != null) importer.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
                report.Check(importer != null && importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == 512,
                    key + " preserves non-power-of-two proportions with a 512-pixel import limit.");
                float sourceAspect = sourceWidth / (float)Mathf.Max(1, sourceHeight);
                float runtimeAspect = texture.width / (float)Mathf.Max(1, texture.height);
                report.Check(importer != null && sourceWidth > 0 && sourceHeight > 0 && Mathf.Abs(runtimeAspect - sourceAspect) <= 0.01f,
                    key + " retains the source image aspect ratio after import (source " + sourceWidth + "x" + sourceHeight
                    + ", runtime " + texture.width + "x" + texture.height + ").");
                bool leftWall = i >= 3;
                report.Check(Mathf.Abs(Quaternion.Dot(poster.localRotation, Quaternion.Euler(0f, leftWall ? -90f : 0f, 0f))) > 0.99999f,
                    key + " uses the intended wall rotation.");
                Transform artwork = poster.Find("Artwork");
                MeshRenderer renderer = artwork.GetComponent<MeshRenderer>();
                Mesh mesh = artwork.GetComponent<MeshFilter>().sharedMesh;
                Material material = renderer.sharedMaterial;
                imageMaterials.Add(material); imageMeshes.Add(mesh);
                report.Check(material != null && material != template && template != null && material.mainTexture == texture
                    && material.shader == template.shader && material.enabledKeywords.Length == 0,
                    key + " binds its own texture to a keyword-free clone of the Unlit template.");
                report.Check(Opaque(material), key + " renders opaque with back-face culling.");
                Vector3 normal = artwork.TransformDirection(mesh.normals[0]).normalized;
                Vector3 expected = owner.transform.TransformDirection(leftWall ? Vector3.right : Vector3.back);
                int[] triangle = mesh.triangles; Vector3[] vertices = mesh.vertices;
                Vector3 winding = Vector3.Cross(vertices[triangle[1]] - vertices[triangle[0]], vertices[triangle[2]] - vertices[triangle[0]]).normalized;
                report.Check(Vector3.Dot(normal, expected) > 0.999f && Vector3.Dot(winding, mesh.normals[0]) > 0.999f,
                    key + " faces into the room with matching front-face winding and normals.");
                Vector3 size = artwork.localScale;
                float aspect = texture.width / (float)texture.height;
                report.Check(Close(artwork.localPosition, Vector3.zero) && size.x <= 1.791f && size.y <= 2.191f
                    && Mathf.Abs(size.x / size.y - aspect) < 0.001f && (Mathf.Abs(size.x - 1.79f) < 0.001f || Mathf.Abs(size.y - 2.19f) < 0.001f),
                    key + " fits inside the image area without stretching or cropping.");
                Transform backer = poster.Find("Artwork Backer");
                report.Check(backer != null && Close(backer.localScale, new Vector3(1.79f, 2.19f, 1f))
                    && backer.localPosition.z > artwork.localPosition.z && Opaque(backer.GetComponent<Renderer>().sharedMaterial),
                    key + " has an opaque backer behind any letterbox margins.");
                Material backerMaterial = backer != null ? backer.GetComponent<Renderer>().sharedMaterial : null;
                report.Check(backerMaterial != null && backerMaterial != template && template != null
                    && backerMaterial.shader == template.shader && backerMaterial.enabledKeywords.Length == 0,
                    key + " uses the same explicitly retained opaque Unlit variant for its backer.");
                if (leftWall)
                {
                    CheckFrame(report, owner.transform, poster.Find("Wood Frame"), new Vector3(-10.12f, 3.72f, positions[i].z), new Vector3(2.05f, 2.45f, 0.14f), key + " wood frame");
                    CheckFrame(report, owner.transform, poster.Find("Cream Mat"), new Vector3(-10.035f, 3.72f, positions[i].z), new Vector3(1.89f, 2.29f, 0.035f), key + " cream mat");
                }
                else report.Check(poster.Find("Wood Frame") == null && poster.Find("Cream Mat") == null, key + " reuses the existing back-wall frame.");
            }
            report.Check(imageMaterials.Count == 6 && imageMeshes.Count == 1, "Each texture owns one shared material while all artworks reuse one quad mesh.");
            report.Check(owner.GetComponentsInChildren<Collider>(true).Length == 0, "Posters, frames and mats add no colliders.");
            bool shadowsOff = true;
            foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true)) shadowsOff &= renderer.shadowCastingMode == ShadowCastingMode.Off && !renderer.receiveShadows;
            report.Check(shadowsOff, "All poster surfaces render without casting or receiving shadows.");
            report.measurements.Add("Six separate Resource textures; three existing back-wall frames and three new left-wall frames; opaque Unlit surfaces with preserved image proportions. Live state unchanged.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        return report.ToString();
    }

    private static bool Opaque(Material material) => material != null && material.renderQueue <= (int)RenderQueue.GeometryLast
        && material.GetTag("RenderType", false) == "Opaque" && !material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
        && (!material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0f)
        && (!material.HasProperty("_AlphaClip") || material.GetFloat("_AlphaClip") == 0f)
        && (!material.HasProperty("_Cull") || Mathf.Approximately(material.GetFloat("_Cull"), (float)CullMode.Back))
        && (!material.HasProperty("_BaseColor") || Mathf.Approximately(material.GetColor("_BaseColor").a, 1f));
    private static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.001f;
    private static void CheckFrame(SimBarRuntimeVerification.Report report, Transform root, Transform frame, Vector3 position, Vector3 size, string label) =>
        report.Check(frame != null && Close(root.InverseTransformPoint(frame.position), position) && Close(frame.localScale, size), label + " has the specified dimensions and position.");
}
