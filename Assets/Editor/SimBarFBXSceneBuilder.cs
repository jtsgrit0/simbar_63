using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimBarFBXSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SimBar_AutoScene.unity";

    [MenuItem("SimBar/Build FBX Reference Scene")]
    public static void BuildReferenceScene()
    {
        Debug.Log("FBX reference scene generation is disabled to avoid duplicating the cozy room layout.");
    }

    private static void ClearScene()
    {
        var rootObjects = sceneToOpenObjects();
        foreach (var obj in rootObjects)
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    private static UnityEngine.Object[] sceneToOpenObjects()
    {
        return SceneManager.GetActiveScene().GetRootGameObjects();
    }

    private static void CreateGround()
    {
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/floor.fbx", new Vector3(0f, 0f, 0f), Vector3.one, "Floor");
        SpawnModel("Assets/kenney_mini-arcade/Models/FBX format/floor.fbx", new Vector3(0f, 0f, 0f), new Vector3(1.15f, 1f, 1.15f), "ArcadeFloor");
    }

    private static void CreateWalls()
    {
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/wall.fbx", new Vector3(-6f, 0f, -8f), new Vector3(1f, 1f, 1f), "WallLeftBack");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/wall.fbx", new Vector3(6f, 0f, -8f), new Vector3(1f, 1f, 1f), "WallRightBack");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/wall-corner.fbx", new Vector3(-11f, 0f, -8f), new Vector3(1f, 1f, 1f), "WallCornerLeft");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/wall-corner.fbx", new Vector3(11f, 0f, -8f), new Vector3(1f, 1f, 1f), "WallCornerRight");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/wall-window.fbx", new Vector3(0f, 0f, -8f), new Vector3(1.6f, 1f, 1f), "WallWindow");
    }

    private static void CreateBar()
    {
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/cash-register.fbx", new Vector3(-7.5f, 0f, 0.5f), new Vector3(1.5f, 1.5f, 1.5f), "CashRegister");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/freezer.fbx", new Vector3(-10f, 0f, 2.8f), new Vector3(1.3f, 1.3f, 1.3f), "Freezer");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/shelf-bags.fbx", new Vector3(-10.5f, 0f, -2.8f), new Vector3(1.4f, 1.4f, 1.4f), "ShelfBags");
        SpawnModel("Assets/kenney_mini-market/Models/FBX format/shelf-end.fbx", new Vector3(-8.8f, 0f, -5.3f), new Vector3(1.5f, 1.5f, 1.5f), "ShelfEnd");

        var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = "BarCounterTop";
        counter.transform.position = new Vector3(-7.4f, 1.4f, 0.4f);
        counter.transform.localScale = new Vector3(4.5f, 0.7f, 2.2f);
        var mat = CreateSafeMaterial(new Color(0.75f, 0.48f, 0.28f, 1f));
        var rend = counter.GetComponent<Renderer>();
        rend.material = mat;
    }

    private static void CreateStage()
    {
        var guitar = SpawnCharacterFromModel("Assets/Model/Guitar Playing.fbx", new Vector3(7.2f, 0f, -5.6f), new Vector3(1.2f, 1.2f, 1.2f), "PerformerGuitar");
        var singing = SpawnCharacterFromModel("Assets/Model/Singing.fbx", new Vector3(9.8f, 0f, -5.5f), new Vector3(1.2f, 1.2f, 1.2f), "PerformerSinging");

        if (guitar != null)
        {
            AttachGuitarToPerformer(guitar, "Assets/Model/gibson.fbx");
            guitar.AddComponent<Animator>();
        }

        if (singing != null)
        {
            singing.AddComponent<Animator>();
        }

        var stage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stage.name = "StagePlatform";
        stage.transform.position = new Vector3(8.4f, 0.55f, -5.5f);
        stage.transform.localScale = new Vector3(6.2f, 1.1f, 4.4f);
        var stageMat = CreateSafeMaterial(new Color(0.18f, 0.18f, 0.24f, 1f));
        stage.GetComponent<Renderer>().material = stageMat;

        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "StageSign";
        sign.transform.position = new Vector3(8.5f, 3.2f, -2.8f);
        sign.transform.localScale = new Vector3(4.5f, 1.1f, 0.25f);
        var signMat = CreateSafeMaterial(new Color(0.12f, 0.2f, 0.35f, 1f));
        sign.GetComponent<Renderer>().material = signMat;

        var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glow.name = "StageGlow";
        glow.transform.position = new Vector3(8.5f, 3.2f, -2.55f);
        glow.transform.localScale = new Vector3(3.6f, 0.55f, 0.08f);
        var glowMat = CreateSafeMaterial(new Color(0.2f, 0.8f, 1f, 1f));
        glow.GetComponent<Renderer>().material = glowMat;

        PlacePubGuests();
    }

    private static void CreateTables()
    {
        SpawnTable(new Vector3(-1.5f, 0f, 3.7f));
        SpawnTable(new Vector3(2.0f, 0f, 4.2f));
        SpawnTable(new Vector3(-2.5f, 0f, -2.8f));
        SpawnTable(new Vector3(2.8f, 0f, -2.2f));
    }

    private static void PlacePubGuests()
    {
        var positions = new[]
        {
            new Vector3(-5.2f, 0f, 4.2f),
            new Vector3(-3.2f, 0f, 5.5f),
            new Vector3(0.8f, 0f, 5.0f),
            new Vector3(4.0f, 0f, 4.5f),
            new Vector3(-4.8f, 0f, -1.8f),
            new Vector3(3.0f, 0f, -1.3f),
            new Vector3(5.3f, 0f, 1.4f),
            new Vector3(-1.2f, 0f, -4.2f)
        };

        string[] modelPaths = Directory.GetFiles("Assets/Model", "*.fbx");
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/Model/Guitar Playing.fbx",
            "Assets/Model/Singing.fbx",
            "Assets/Model/gibson.fbx"
        };

        int index = 0;
        foreach (var path in modelPaths)
        {
            if (used.Contains(path.Replace('\\', '/')))
            {
                continue;
            }

            if (index >= positions.Length)
            {
                break;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (asset == null)
            {
                continue;
            }

            var guest = UnityEngine.Object.Instantiate(asset);
            guest.name = Path.GetFileNameWithoutExtension(path);
            guest.transform.position = positions[index];
            guest.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            guest.transform.localScale = Vector3.one * 1.05f;
            guest.AddComponent<Animator>();

            index++;
        }
    }

    private static void SpawnTable(Vector3 pos)
    {
        var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        table.name = "Table";
        table.transform.position = pos + new Vector3(0f, 0.7f, 0f);
        table.transform.localScale = new Vector3(1.2f, 0.3f, 1.2f);
        var tableMat = CreateSafeMaterial(new Color(0.45f, 0.32f, 0.22f, 1f));
        table.GetComponent<Renderer>().material = tableMat;

        for (int i = 0; i < 4; i++)
        {
            var chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = "Chair";
            chair.transform.position = pos + new Vector3(Mathf.Cos(i * 90f * Mathf.Deg2Rad) * 1.2f, 0.35f, Mathf.Sin(i * 90f * Mathf.Deg2Rad) * 1.2f);
            chair.transform.localScale = new Vector3(0.45f, 0.7f, 0.45f);
            chair.GetComponent<Renderer>().material = CreateSafeMaterial(new Color(0.2f, 0.2f, 0.28f, 1f));
        }
    }

    private static void CreateArcadeDecor()
    {
        SpawnModel("Assets/kenney_mini-arcade/Models/FBX format/arcade-machine.fbx", new Vector3(10f, 0f, 1.2f), new Vector3(1.4f, 1.4f, 1.4f), "ArcadeMachine");
        SpawnModel("Assets/kenney_mini-arcade/Models/FBX format/vending-machine.fbx", new Vector3(11.3f, 0f, 4.4f), new Vector3(1.5f, 1.5f, 1.5f), "VendingMachine");
        SpawnModel("Assets/kenney_mini-arcade/Models/FBX format/prize-wheel.fbx", new Vector3(5.5f, 0f, 5.2f), new Vector3(1.4f, 1.4f, 1.4f), "PrizeWheel");
        SpawnModel("Assets/kenney_mini-arcade/Models/FBX format/air-hockey.fbx", new Vector3(0.8f, 0f, 6.0f), new Vector3(1.4f, 1.4f, 1.4f), "AirHockey");
    }

    private static void CreateCamera()
    {
        var cameraGo = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cameraGo == null)
        {
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            cam.orthographic = false;
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camera.transform.position = new Vector3(0f, 10f, -12f);
            camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            camera.transform.LookAt(new Vector3(0f, 2f, 0f));
        }
        else
        {
            var cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            cam.transform.position = new Vector3(0f, 10f, -12f);
            cam.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cam.transform.LookAt(new Vector3(0f, 2f, 0f));
        }
    }

    private static void CreateLighting()
    {
        var dir = new GameObject("Directional Light");
        var light = dir.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.92f, 0.82f, 1f);
        light.intensity = 1.1f;
        dir.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

        var warm = new GameObject("WarmLight");
        var warmLight = warm.AddComponent<Light>();
        warmLight.type = LightType.Point;
        warmLight.color = new Color(1f, 0.75f, 0.52f, 1f);
        warmLight.intensity = 18f;
        warmLight.range = 18f;
        warm.transform.position = new Vector3(-5f, 4.5f, 2f);

        var cyan = new GameObject("CyanLight");
        var cyanLight = cyan.AddComponent<Light>();
        cyanLight.type = LightType.Point;
        cyanLight.color = new Color(0.2f, 0.9f, 1f, 1f);
        cyanLight.intensity = 18f;
        cyanLight.range = 16f;
        cyan.transform.position = new Vector3(8f, 4.5f, -5f);

        RenderSettings.ambientLight = new Color(0.6f, 0.55f, 0.7f, 1f);
    }

    private static Shader GetSafeShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static Material CreateSafeMaterial(Color color)
    {
        var material = new Material(GetSafeShader());
        material.color = color;
        return material;
    }

    private static void SpawnModel(string path, Vector3 position, Vector3 scale, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            var fallback = CreateFallbackObject(name, position, scale, path);
            if (fallback != null)
            {
                Debug.LogWarning($"FBX not found or failed to load: {path}. Generated fallback render object instead.");
            }
            return;
        }

        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(asset))
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }
        else
        {
            instance = UnityEngine.Object.Instantiate(asset);
        }

        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        instance.transform.localScale = scale;
    }

    private static GameObject SpawnCharacterFromModel(string path, Vector3 position, Vector3 scale, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            return CreateFallbackObject(name, position, scale, path);
        }

        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(asset))
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }
        else
        {
            instance = UnityEngine.Object.Instantiate(asset);
        }

        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        instance.transform.localScale = scale;

        var nav = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav == null)
        {
            instance.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        if (instance.GetComponent<Customer>() == null)
        {
            instance.AddComponent<Customer>();
        }

        return instance;
    }

    private static void AttachGuitarToPerformer(GameObject performer, string guitarPath)
    {
        var guitarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(guitarPath);
        if (guitarAsset == null || performer == null)
        {
            return;
        }

        GameObject guitarInstance;
        if (PrefabUtility.IsPartOfPrefabAsset(guitarAsset))
        {
            guitarInstance = (GameObject)PrefabUtility.InstantiatePrefab(guitarAsset);
        }
        else
        {
            guitarInstance = UnityEngine.Object.Instantiate(guitarAsset);
        }

        guitarInstance.name = "GibsonGuitar";

        var hand = FindHandTransform(performer.transform);
        if (hand != null)
        {
            guitarInstance.transform.SetParent(hand, false);
            guitarInstance.transform.localPosition = new Vector3(0.22f, -0.08f, 0.16f);
            guitarInstance.transform.localRotation = Quaternion.Euler(10f, 90f, -35f);
            guitarInstance.transform.localScale = Vector3.one * 0.12f;
            return;
        }

        guitarInstance.transform.SetParent(performer.transform, false);
        guitarInstance.transform.localPosition = new Vector3(0.6f, 1.0f, 0.25f);
        guitarInstance.transform.localRotation = Quaternion.Euler(0f, 90f, -20f);
        guitarInstance.transform.localScale = Vector3.one * 0.12f;
    }

    private static Transform FindHandTransform(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var name = current.name.ToLowerInvariant();
            if (name.Contains("hand") || name.Contains("arm") || name.Contains("right") || name.Contains("left"))
            {
                if (name.Contains("hand") || name.Contains("right") || name.Contains("left"))
                {
                    return current;
                }
            }

            foreach (Transform child in current)
            {
                queue.Enqueue(child);
            }
        }

        return null;
    }

    private static GameObject CreateFallbackObject(string name, Vector3 position, Vector3 scale, string path)
    {
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = name;
        fallback.transform.position = position;
        fallback.transform.localScale = scale;
        fallback.GetComponent<Renderer>().material = CreateSafeMaterial(new Color(0.3f, 0.7f, 1f, 1f));
        return fallback;
    }
}