using UnityEngine;

/// <summary>
/// Shared helpers for creating materials at runtime that work across the built-in
/// render pipeline and URP. Kept in one place so the shader fallback logic only
/// has to be maintained once.
/// </summary>
public static class RuntimeMaterialUtility
{
    public static Shader GetSafeShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    public static Material CreateSafeMaterial(Color color)
    {
        var shader = GetSafeShader();
        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader);
        material.color = color;
        return material;
    }
}
