using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// <summary>Release Web players use uGUI, so they do not need the UI Toolkit input provider.</summary>
public sealed class SimBarWebAssemblyFilter : IFilterBuildAssemblies
{
    public int callbackOrder => 0;

    public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL
            || (buildOptions & (BuildOptions.Development | BuildOptions.IncludeTestAssemblies)) != 0)
            return assemblies;

        // InputSystemUIInputModule (uGUI) lives in Unity.InputSystem and stays included.
        // ForUI only registers a UI Toolkit provider. No player assembly references it.
        // Retain it in the Editor/development builds and revisit this filter if a
        // release scene later adds UIDocument/UI Toolkit presentation.
        return assemblies.Where(path => Path.GetFileName(path) != "Unity.InputSystem.ForUI.dll").ToArray();
    }
}
