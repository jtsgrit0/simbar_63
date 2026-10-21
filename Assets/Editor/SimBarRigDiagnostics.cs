using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mixamo 모델들의 스켈레톤(본 이름)과 애니메이션 클립 커브 경로를 덤프하는 진단 도구.
///
/// 목적: "Standing Jump.fbx" 의 점프 클립 커브 경로가 정적 캐릭터 모델
/// (Adventurer, Casual 등)의 실제 본 이름과 일치하는지 확인하여,
/// 커브 재매핑(StageJumpPlayer.RemapClipToSkeleton) 방식이 유효한지 검증한다.
///
/// 배치모드 실행:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod SimBarRigDiagnostics.DumpAll -logFile rig.log
/// </summary>
public static class SimBarRigDiagnostics
{
    private const string ModelFolder = "Assets/Model";

    public static void DumpAll()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== SimBar Rig Diagnostics =====");

        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { ModelFolder });
        var modelPaths = new List<string>();
        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                modelPaths.Add(path);
        }
        modelPaths.Sort();

        foreach (string path in modelPaths)
        {
            sb.AppendLine();
            sb.AppendLine($"--- MODEL: {path} ---");

            // 1) 클립 정보 (커브 경로)
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            bool anyClip = false;
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    anyClip = true;
                    EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                    sb.AppendLine($"  CLIP: {clip.name}  (length={clip.length:F3}s, curves={bindings.Length})");
                    var seen = new HashSet<string>();
                    foreach (var b in bindings)
                    {
                        if (seen.Add(b.path))
                            sb.AppendLine($"    path: {b.path}");
                    }
                }
            }
            if (!anyClip)
                sb.AppendLine("  CLIP: (none)");

            // 2) 스켈레톤 본 이름 (Transform 계층)
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null)
            {
                var boneNames = new List<string>();
                CollectTransforms(root.transform, boneNames, 0, 3);
                sb.AppendLine($"  SKELETON ({boneNames.Count} transforms, depth<=3):");
                foreach (string n in boneNames)
                    sb.AppendLine($"    bone: {n}");
            }
        }

        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText("rig_diagnostics.txt", sb.ToString());
        AssetDatabase.Refresh();
    }

    private static void CollectTransforms(Transform t, List<string> names, int depth, int maxDepth)
    {
        names.Add(t.name);
        if (depth >= maxDepth)
            return;
        for (int i = 0; i < t.childCount; i++)
            CollectTransforms(t.GetChild(i), names, depth + 1, maxDepth);
    }
}
