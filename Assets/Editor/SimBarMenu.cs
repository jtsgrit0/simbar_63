using UnityEngine;
using UnityEditor;
using System.IO;

public static class SimBarMenu
{
    private static string MarketPath => "Assets/kenney_mini-market/Models/FBX format";
    private static string ArcadePath => "Assets/kenney_mini-arcade/Models/FBX format";
    private static string ModelPath => "Assets/Model";

    private static T LoadModel<T>(string relativePath) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(relativePath);
    }

    [MenuItem("GameObject/SimBar/Setup Isometric Scene", false, 10)]
    private static void SetupIsometricScene(MenuCommand menuCommand)
    {
        // This method is intentionally disabled to avoid duplicating the cozy room layout.
        Debug.Log("Scene-object generation is disabled. Use the cozy room layout as the single source of environment construction.");
    }

    [MenuItem("GameObject/SimBar/Create Cozy Room (Square Isometric)", false, 11)]
    private static void CreateCozyRoom(MenuCommand menuCommand)
    {
        // This method is intentionally disabled to avoid duplicating the cozy room layout.
        Debug.Log("Cozy room generation is handled by SimBarLayout, which is the single environment layout source.");
    }

    private static void CreateWall(Transform parent, Vector3 pos, Vector3 size, string name)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.localPosition = pos;
        wall.transform.localScale = size;
        wall.AddComponent<BoxCollider>();
    }

    [MenuItem("GameObject/SimBar/Create Bar Counter", false, 12)]
    private static void CreateBarCounter(MenuCommand menuCommand)
    {
        GameObject bar = CreateBarCounterInternal();
        GameObjectUtility.SetParentAndAlign(bar, menuCommand.context as GameObject);
        Selection.activeGameObject = bar;
    }

    private static GameObject CreateBarCounterInternal()
    {
        GameObject bar = new GameObject("BarCounter");
        bar.AddComponent<BoxCollider>();
        bar.AddComponent<BarCounter>();

        var floorModel = LoadModel<GameObject>(Path.Combine(MarketPath, "floor.fbx"));
        if (floorModel != null)
        {
            GameObject floor = (GameObject)PrefabUtility.InstantiatePrefab(floorModel, bar.transform);
            floor.transform.localPosition = new Vector3(0, 0.2f, 0);
            floor.transform.localScale = Vector3.one * 1.5f;
        }

        var shelfModel = LoadModel<GameObject>(Path.Combine(MarketPath, "shelf-boxes.fbx"));
        if (shelfModel != null)
        {
            GameObject shelf = (GameObject)PrefabUtility.InstantiatePrefab(shelfModel, bar.transform);
            shelf.transform.localPosition = new Vector3(0, 0.8f, -0.4f);
        }

        var freezerModel = LoadModel<GameObject>(Path.Combine(MarketPath, "freezer.fbx"));
        if (freezerModel != null)
        {
            GameObject freezer = (GameObject)PrefabUtility.InstantiatePrefab(freezerModel, bar.transform);
            freezer.transform.localPosition = new Vector3(-1.5f, 0.5f, 0f);
        }

        var cashModel = LoadModel<GameObject>(Path.Combine(MarketPath, "cash-register.fbx"));
        if (cashModel != null)
        {
            GameObject cash = (GameObject)PrefabUtility.InstantiatePrefab(cashModel, bar.transform);
            cash.transform.localPosition = new Vector3(1.5f, 0.6f, 0.2f);
        }

        return bar;
    }

    [MenuItem("GameObject/SimBar/Create Stage", false, 13)]
    private static void CreateStage(MenuCommand menuCommand)
    {
        GameObject stage = CreateStageInternal();
        GameObjectUtility.SetParentAndAlign(stage, menuCommand.context as GameObject);
        Selection.activeGameObject = stage;
    }

    private static GameObject CreateStageInternal()
    {
        GameObject stage = new GameObject("Stage");
        stage.AddComponent<Stage>();

        var guitarModel = LoadModel<GameObject>(Path.Combine(ModelPath, "Guitar Playing.fbx"));
        if (guitarModel != null)
        {
            GameObject guitar = (GameObject)PrefabUtility.InstantiatePrefab(guitarModel, stage.transform);
            guitar.transform.localPosition = new Vector3(-0.8f, 0, -0.5f);
            guitar.transform.localRotation = Quaternion.Euler(0, 30f, 0);
        }

        var singingModel = LoadModel<GameObject>(Path.Combine(ModelPath, "Singing.fbx"));
        if (singingModel != null)
        {
            GameObject singer = (GameObject)PrefabUtility.InstantiatePrefab(singingModel, stage.transform);
            singer.transform.localPosition = new Vector3(0.8f, 0, -0.5f);
            singer.transform.localRotation = Quaternion.Euler(0, -30f, 0);
        }

        var floorModel = LoadModel<GameObject>(Path.Combine(ArcadePath, "floor.fbx"));
        if (floorModel != null)
        {
            GameObject stageFloor = (GameObject)PrefabUtility.InstantiatePrefab(floorModel, stage.transform);
            stageFloor.transform.localPosition = new Vector3(0, -0.1f, 0.3f);
            stageFloor.transform.localScale = Vector3.one * 2f;
        }

        return stage;
    }

    [MenuItem("GameObject/SimBar/Create Table Set", false, 14)]
    private static void CreateTableSet(MenuCommand menuCommand)
    {
        GameObject tablesParent = new GameObject("TableSet");
        GameObjectUtility.SetParentAndAlign(tablesParent, menuCommand.context as GameObject);
        CreateTableSetInternal(tablesParent.transform, 12f);
        Selection.activeGameObject = tablesParent;
    }

    private static void CreateTableSetInternal(Transform parent, float mapSize)
    {
        var tableModel = LoadModel<GameObject>(Path.Combine(ArcadePath, "air-hockey.fbx"));
        if (tableModel == null)
        {
            tableModel = LoadModel<GameObject>(Path.Combine(MarketPath, "display-bread.fbx"));
        }

        Vector3[] positions = {
            new Vector3(-2.5f, 0, -2.5f),
            new Vector3(2.5f, 0, -2.5f),
            new Vector3(-2.5f, 0, 2.5f),
            new Vector3(2.5f, 0, 2.5f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject table = new GameObject($"Table_{i + 1}");
            table.transform.SetParent(parent);
            table.transform.localPosition = positions[i];
            table.AddComponent<Table>();
            table.AddComponent<BoxCollider>();

            if (tableModel != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(tableModel, table.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = Vector3.one * 0.8f;
            }
        }
    }

    [MenuItem("GameObject/SimBar/Create Customer Spawn Point", false, 15)]
    private static void CreateSpawnPoint(MenuCommand menuCommand)
    {
        GameObject spawn = new GameObject("CustomerSpawnPoint");
        GameObjectUtility.SetParentAndAlign(spawn, menuCommand.context as GameObject);
        spawn.AddComponent<CustomerSpawner>();
        Selection.activeGameObject = spawn;
    }

    [MenuItem("GameObject/SimBar/Create Warm Light", false, 16)]
    private static void CreateWarmLight(MenuCommand menuCommand)
    {
        GameObject light = new GameObject("WarmLight");
        GameObjectUtility.SetParentAndAlign(light, menuCommand.context as GameObject);
        Light lt = light.AddComponent<Light>();
        lt.color = new Color(1f, 0.85f, 0.6f);
        lt.intensity = 1.2f;
        lt.range = 10f;
        lt.shadows = LightShadows.Soft;
        Selection.activeGameObject = light;
    }

    [MenuItem("GameObject/SimBar/Create Deco Columns", false, 17)]
    private static void CreateDecoColumns(MenuCommand menuCommand)
    {
        GameObject columnsParent = new GameObject("DecoColumns");
        GameObjectUtility.SetParentAndAlign(columnsParent, menuCommand.context as GameObject);
        CreateDecoColumnsInternal(columnsParent.transform);
        Selection.activeGameObject = columnsParent;
    }

    private static void CreateDecoColumnsInternal(Transform parent)
    {
        var columnModel = LoadModel<GameObject>(Path.Combine(MarketPath, "column.fbx"));
        if (columnModel == null)
        {
            columnModel = LoadModel<GameObject>(Path.Combine(ArcadePath, "column.fbx"));
        }

        if (columnModel != null)
        {
            Vector3[] positions = {
                new Vector3(-4.5f, 0, -4.5f), new Vector3(4.5f, 0, -4.5f),
                new Vector3(-4.5f, 0, 4.5f), new Vector3(4.5f, 0, 4.5f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject col = (GameObject)PrefabUtility.InstantiatePrefab(columnModel, parent);
                col.transform.localPosition = positions[i];
            }
        }
    }

    private static void CreateWarmLight(Transform parent, Vector3 localPos, float intensity, float range)
    {
        GameObject light = new GameObject("WarmLight");
        light.transform.SetParent(parent);
        light.transform.localPosition = localPos;
        Light lt = light.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = new Color(1f, 0.85f, 0.6f);
        lt.intensity = intensity;
        lt.range = range;
        lt.shadows = LightShadows.Soft;
    }
}
