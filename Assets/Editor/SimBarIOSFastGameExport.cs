#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

internal static class SimBarIOSFastGameExport
{
    [PostProcessBuild(120)]
    public static void ConfigureFastGame(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        string pbxPath = PBXProject.GetPBXProjectPath(output);
        var pbx = new PBXProject();
        pbx.ReadFromFile(pbxPath);
        pbx.AddFrameworkToProject(pbx.GetUnityFrameworkTargetGuid(), "WebKit.framework", false);
        pbx.WriteToFile(pbxPath);

        string plistPath = Path.Combine(output, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var orientations = plist.root.CreateArray("UISupportedInterfaceOrientations");
        orientations.AddString("UIInterfaceOrientationLandscapeLeft");
        orientations.AddString("UIInterfaceOrientationLandscapeRight");
        plist.WriteToFile(plistPath);
    }
}
#endif
