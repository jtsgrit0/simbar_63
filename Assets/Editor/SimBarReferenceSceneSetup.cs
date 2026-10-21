using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Rebuildable import and scene authoring for the Blender/FBX reference venue.</summary>
public static class SimBarReferenceSceneSetup
{
    public const string ScenePath = "Assets/Scenes/SimBar_Reference.unity";
    private const string Source = "Assets/Resources/SimBarArt/SimBarVenue.fbx";
    private const string PrefabPath = "Assets/Resources/SimBarArt/SimBarVenueReady.prefab";
    private const string Output = "Assets/SimBarGenerated/Venue";

    [MenuItem("SimBar/Art/Import Blender Venue and Build Reference Scene")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play Mode and allow the current player build to finish before scene authoring.");
        SetupVenue();
        SimBarArtAssetSetup.Setup();
        BuildScene();
    }

    public static void SetupVenue()
    {
        Directory.CreateDirectory(Output + "/Materials");
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Blender venue FBX has not finished exporting.", Source);
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.globalScale = 1f;
        importer.importLights = false;
        importer.importCameras = false;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var working = Object.Instantiate(model);
        working.name = "SIMBAR Blender Reference Venue";
        // Blender's baked FBX forward axis is opposite Unity's scene forward.
        // Correct the model once in its generated prefab; gameplay stays in metres.
        working.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * working.transform.rotation;
        try
        {
            var converted = new Dictionary<Material, Material>();
            foreach (var renderer in working.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (!converted.TryGetValue(source, out var material))
                    {
                        string safe = new string(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                        string path = Output + "/Materials/" + safe + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        if (material == null)
                        {
                            material = new Material(shader) { name = "Venue_" + safe };
                            AssetDatabase.CreateAsset(material, path);
                        }
                        material.shader = shader;
                        Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                        material.SetColor("_BaseColor", color);
                        string key = safe.ToLowerInvariant();
                        material.SetFloat("_Smoothness", key.Contains("metal") || key.Contains("chrome") ? 0.6f : key.Contains("glass") ? 0.65f : 0.28f);
                        material.SetFloat("_Metallic", key.Contains("metal") || key.Contains("chrome") || key.Contains("brass") ? 0.5f : 0.03f);
                        if (key.Contains("neon") || key.Contains("glow") || key.Contains("emission"))
                        {
                            material.EnableKeyword("_EMISSION");
                            material.SetColor("_EmissionColor", color * 1.8f);
                        }
                        else material.DisableKeyword("_EMISSION");
                        if (source.mainTexture != null) material.SetTexture("_BaseMap", source.mainTexture);
                        EditorUtility.SetDirty(material);
                        converted.Add(source, material);
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
            PrefabUtility.SaveAsPrefabAsset(working, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SimBar Reference] Imported Blender venue: {working.GetComponentsInChildren<MeshRenderer>().Length} mesh groups, {converted.Count} URP materials. Original FBX retained at {Source}.");
        }
        finally { Object.DestroyImmediate(working); }
    }

    public static void BuildScene()
    {
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("The current scene has unsaved changes. The art assets are prepared; save that scene before building the reference scene.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new FileNotFoundException("Run SetupVenue before building the reference scene.", PrefabPath);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SimBar_Reference";
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        SimBarExperience.FrameReferenceCamera(camera);
        new GameObject("SimBarExperience").AddComponent<SimBarExperience>();
        var preview = new GameObject("SimBar_EditorPreview");
        var environment = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        environment.transform.SetParent(preview.transform, false);
        PreviewPerson(preview.transform, "singer", new Vector3(5.5f, 0.62f, 2.7f), 180f, 0);
        PreviewPerson(preview.transform, "guitarist", new Vector3(3f, 0.62f, 3.8f), 180f, 1);
        PreviewPerson(preview.transform, "bassist", new Vector3(8f, 0.62f, 4.6f), 180f, 2);
        PreviewPerson(preview.transform, "drummer", new Vector3(6f, 0.62f, 6.2f), 180f, 3);
        PreviewPerson(preview.transform, "bartender", new Vector3(-7.3f, 0f, -0.3f), 180f, 4);
        for (int i = 0; i < 18; i++)
            PreviewPerson(preview.transform, "guest", new Vector3(0.25f + (i % 6) * 1.48f, 0f, 0.25f - (i / 6) * 1.9f), (i % 3 - 1) * 9f, i);
        SimBarReferenceLighting.Build(preview.transform, camera);
        PersistPreviewProfile(preview);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        previous.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = previous.ToArray();
        Selection.activeGameObject = environment;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0f, 2f, 0f), new Vector3(23f, 10f, 18f)), false);
        AssetDatabase.SaveAssets();
        Debug.Log("[SimBar Reference] Saved " + ScenePath + " as build entry scene. Original scenes remain in the project.");
    }

    private static void PreviewPerson(Transform parent, string role, Vector3 position, float yaw, int variant)
    {
        var person = SimBarCharacterVisual.Create(role, parent, position, new Color(0.8f, 0.15f, 0.5f), variant);
        if (person != null) person.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static void PersistPreviewProfile(GameObject preview)
    {
        var volume = preview.GetComponentInChildren<Volume>();
        if (volume == null) return;
        var generated = volume.profile;
        const string path = Output + "/ReferenceColorGrade.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = generated;
            AssetDatabase.CreateAsset(profile, path);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        }
        volume.sharedProfile = profile;
        volume.profile = null;
        if (generated != profile) Object.DestroyImmediate(generated);
    }
}
