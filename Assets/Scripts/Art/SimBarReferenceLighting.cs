using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Warm readable house lighting with colored concert pools and visible stage shafts.</summary>
public static class SimBarReferenceLighting
{
    public static List<Light> Build(Transform parent, Camera camera, List<Renderer> beams = null)
    {
        var animated = new List<Light>();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.66f, 0.59f, 0.64f);
        QualitySettings.shadowDistance = 45f;
        Light key = Light(parent, "Warm softbox", LightType.Directional, new Vector3(0f, 10f, -3f), new Color(1f, 0.86f, 0.72f), 1.75f, 0f);
        key.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.48f;
        key.shadowBias = 0.045f;
        RenderSettings.sun = key;
        Light(parent, "Bar amber practical", LightType.Point, new Vector3(-6f, 4.4f, -1.5f), new Color(1f, 0.66f, 0.33f), 16f, 11f);
        Light(parent, "Lounge warm practical", LightType.Point, new Vector3(-5.5f, 4.5f, 4.8f), new Color(1f, 0.69f, 0.46f), 12f, 10f);
        Light(parent, "Foreground warm practical", LightType.Point, new Vector3(-4.5f, 4f, -5.8f), new Color(1f, 0.8f, 0.62f), 10f, 9f);
        Vector3[] starts = { new Vector3(2.2f, 6.7f, 5.8f), new Vector3(4.3f, 6.7f, 5.8f), new Vector3(6.6f, 6.7f, 5.8f), new Vector3(8.6f, 6.7f, 5.8f) };
        Vector3[] ends = { new Vector3(1.8f, 0.12f, -1.2f), new Vector3(6.5f, 0.65f, 3f), new Vector3(3.9f, 0.12f, -1.8f), new Vector3(8.2f, 0.65f, 2.8f) };
        Color[] colors = { new Color(0.08f, 0.72f, 1f), new Color(0.9f, 0.08f, 0.7f), new Color(0.18f, 0.8f, 1f), new Color(0.73f, 0.12f, 1f) };
        for (int i = 0; i < starts.Length; i++)
        {
            Light spot = Light(parent, "Concert spot " + i, LightType.Spot, starts[i], colors[i], 18f, 14f);
            spot.spotAngle = 43f;
            spot.innerSpotAngle = 20f;
            spot.transform.LookAt(ends[i]);
            animated.Add(spot);
            Renderer beam = Beam(parent, starts[i], ends[i], colors[i], 1.25f);
            if (beams != null) beams.Add(beam);
        }
        if (camera != null)
        {
            camera.allowHDR = true;
            camera.allowMSAA = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
        var volumeObject = new GameObject("Concert color grade");
        volumeObject.transform.SetParent(parent, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.30f);
        bloom.scatter.Override(0.52f);
        var grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.Override(0.16f);
        grade.contrast.Override(7f);
        grade.saturation.Override(6f);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        return animated;
    }

    private static Light Light(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    private static Renderer Beam(Transform parent, Vector3 start, Vector3 end, Color color, float radius)
    {
        var go = new GameObject("Translucent concert beam");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = start;
        go.transform.localRotation = Quaternion.LookRotation(end - start);
        const int sides = 16;
        var vertices = new Vector3[sides + 1];
        var triangles = new int[sides * 3];
        float length = Vector3.Distance(start, end);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, length);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % sides + 1;
        }
        var mesh = new Mesh { name = "Stage light shaft", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = "Stage haze" };
        Color tint = new Color(color.r, color.g, color.b, 0.065f);
        material.color = tint;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }
}
