using System.Collections.Generic;
using UnityEngine;

/// <summary>Creates render-only shapes without allocating physics components that are immediately removed.</summary>
public static class SimBarVisualPrimitive
{
    private static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();

    public static GameObject Create(PrimitiveType type)
    {
        if (!meshes.TryGetValue(type, out Mesh mesh) || mesh == null)
        {
            mesh = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            if (mesh == null) return GameObject.CreatePrimitive(type);
            meshes[type] = mesh;
        }
        var shape = new GameObject(type.ToString(), typeof(MeshFilter), typeof(MeshRenderer));
        shape.GetComponent<MeshFilter>().sharedMesh = mesh;
        return shape;
    }
}
