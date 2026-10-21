using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.AI.Navigation;

public class SimBarLayout : EditorWindow
{
    private float roomSize = 12f;
    private float wallHeight = 2.5f;
    private bool hideBottomWalls = false;

    [MenuItem("GameObject/SimBar/Setup Layout", false, 10)]
    public static void ShowWindow()
    {
        GetWindow<SimBarLayout>("SimBar Layout").Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("SimBar Cozy Room Settings", EditorStyles.boldLabel);
        roomSize = EditorGUILayout.FloatField("Room Size", roomSize);
        wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
        hideBottomWalls = EditorGUILayout.Toggle("Hide Bottom Walls", hideBottomWalls);

        if (GUILayout.Button("Create Cozy Room Layout"))
        {
            CreateCozyRoomLayout();
        }
    }

    private string MarketPath => "Assets/kenney_mini-market/Models/FBX format";
    private string ArcadePath => "Assets/kenney_mini-arcade/Models/FBX format";
    private string ModelPath => "Assets/Model";

    private GameObject LoadPrefab(string relativePath)
    {
        string path = relativePath;
        if (!path.EndsWith(".fbx") && !path.EndsWith(".FBX"))
        {
            path += ".fbx";
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private void CreateCozyRoomLayout()
    {
        float roomWidth = roomSize;
        float roomDepth = roomSize;
        GameObject root = new GameObject("SimBar_CozyRoom");
        Undo.RegisterCreatedObjectUndo(root, "Create SimBar Layout");

        float hw = roomWidth / 2f;
        float hd = roomDepth / 2f;

        // Floor tiles
        GameObject floorParent = new GameObject("Floor");
        floorParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(floorParent, "Create Floor");

        GameObject floorPrefab = LoadPrefab(Path.Combine(ArcadePath, "floor"));
        if (floorPrefab == null)
        {
            floorPrefab = LoadPrefab(Path.Combine(MarketPath, "floor"));
        }

        if (floorPrefab != null)
        {
            Bounds floorBounds = GetPrefabBounds(floorPrefab);
            float tileSize = floorBounds.size.x;
            int tilesX = Mathf.RoundToInt(roomWidth / tileSize);
            int tilesZ = Mathf.RoundToInt(roomDepth / tileSize);

            for (int x = 0; x < tilesX; x++)
            {
                for (int z = 0; z < tilesZ; z++)
                {
                    GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                    tile.name = "Floor_" + x + "_" + z;
                    tile.transform.SetParent(floorParent.transform);
                    tile.transform.localPosition = new Vector3(x * tileSize - hw + tileSize / 2f, 0f, z * tileSize - hd + tileSize / 2f);
                    Undo.RegisterCreatedObjectUndo(tile, "Place Floor Tile");
                }
            }
        }

        // Walls
        GameObject wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(wallsParent, "Create Walls");

        GameObject wallPrefab = LoadPrefab(Path.Combine(ArcadePath, "wall"));
        GameObject cornerPrefab = LoadPrefab(Path.Combine(ArcadePath, "wall-corner"));

        if (wallPrefab == null)
        {
            wallPrefab = LoadPrefab(Path.Combine(MarketPath, "wall"));
        }
        if (cornerPrefab == null)
        {
            cornerPrefab = LoadPrefab(Path.Combine(MarketPath, "wall-corner"));
        }

        if (wallPrefab != null)
        {
            Bounds wallBounds = GetPrefabBounds(wallPrefab);
            float wallWidth = wallBounds.size.x;
            float wallDepth = wallBounds.size.z;
            float wallOriginalHeight = wallBounds.size.y;

            if (hideBottomWalls)
            {
                PlaceWallLine(wallsParent, wallPrefab, "Wall_Back", -hw, hd + wallDepth / 2f, roomWidth, 0f, 180f, wallHeight, wallOriginalHeight);
                PlaceWallLine(wallsParent, wallPrefab, "Wall_Right", hw + wallDepth / 2f, -hd, roomDepth, 0f, -90f, wallHeight, wallOriginalHeight);
            }
            else
            {
                PlaceWallLine(wallsParent, wallPrefab, "Wall_Front", -hw, -hd - wallDepth / 2f, roomWidth, 0f, 0f, wallHeight, wallOriginalHeight);
                PlaceWallLine(wallsParent, wallPrefab, "Wall_Back", -hw, hd + wallDepth / 2f, roomWidth, 0f, 180f, wallHeight, wallOriginalHeight);
                PlaceWallLine(wallsParent, wallPrefab, "Wall_Left", -hw - wallDepth / 2f, -hd, roomDepth, 0f, 90f, wallHeight, wallOriginalHeight);
                PlaceWallLine(wallsParent, wallPrefab, "Wall_Right", hw + wallDepth / 2f, -hd, roomDepth, 0f, -90f, wallHeight, wallOriginalHeight);
            }

        }

        // Corners
        GameObject cornersParent = new GameObject("Corners");
        cornersParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(cornersParent, "Create Corners");

        if (cornerPrefab != null)
        {
            Bounds cornerBounds = GetPrefabBounds(cornerPrefab);
            float cornerOriginalHeight = cornerBounds.size.y;

            if (hideBottomWalls)
            {
                PlaceCorner(cornersParent, cornerPrefab, "Corner_BR", hw, 0f, hd, wallHeight, cornerOriginalHeight);
                PlaceCorner(cornersParent, cornerPrefab, "Corner_BL", -hw, 0f, hd, wallHeight, cornerOriginalHeight);
            }
            else
            {
                PlaceCorner(cornersParent, cornerPrefab, "Corner_FL", -hw, 0f, -hd, wallHeight, cornerOriginalHeight);
                PlaceCorner(cornersParent, cornerPrefab, "Corner_FR", hw, 0f, -hd, wallHeight, cornerOriginalHeight);
                PlaceCorner(cornersParent, cornerPrefab, "Corner_BL", -hw, 0f, hd, wallHeight, cornerOriginalHeight);
                PlaceCorner(cornersParent, cornerPrefab, "Corner_BR", hw, 0f, hd, wallHeight, cornerOriginalHeight);
            }
        }

        // Deco Columns
        GameObject decoParent = new GameObject("Deco");
        decoParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(decoParent, "Create Deco");

        GameObject columnPrefab = LoadPrefab(Path.Combine(MarketPath, "column"));
        if (columnPrefab == null)
        {
            columnPrefab = LoadPrefab(Path.Combine(ArcadePath, "column"));
        }

        if (columnPrefab != null)
        {
            Bounds colBounds = GetPrefabBounds(columnPrefab);
            float colY = colBounds.extents.y;
            Vector3[] positions = {
                new Vector3(-hw + 1f, colY, -hd + 1f),
                new Vector3(hw - 1f, colY, -hd + 1f),
                new Vector3(-hw + 1f, colY, hd - 1f),
                new Vector3(hw - 1f, colY, hd - 1f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                PlaceMachine(decoParent, columnPrefab, "Column_" + (i + 1), positions[i].x, 0f, positions[i].z, 0f);
            }
        }

        // Bar Counter (back wall center)
        GameObject barParent = new GameObject("BarCounter");
        barParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(barParent, "Create BarCounter");

        float barZ = hideBottomWalls ? hd - 1.5f : hd - 0.5f;
        CreateBarCounter(barParent, 0f, 0f, barZ, 0f, roomWidth);

        // Stage (front wall center)
        GameObject stageParent = new GameObject("Stage");
        stageParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(stageParent, "Create Stage");

        float stageZ = hideBottomWalls ? -hd + 1.5f : -hd + 0.5f;
        CreateStage(stageParent, 0f, 0f, stageZ, 0f);

        // Tables (center area)
        GameObject tablesParent = new GameObject("Tables");
        tablesParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(tablesParent, "Create Tables");

        CreateTableSet(tablesParent, roomWidth, roomDepth);

        PlaceRiggedCharacterModels(root.transform, roomWidth, roomDepth);
        PlaceAnimationReferenceModels(root.transform, roomDepth);
        StageJumpAnimationApplier.ApplyJumpToLayoutModels(root.transform);

        // Customer Spawn Point
        GameObject spawn = new GameObject("CustomerSpawnPoint");
        spawn.transform.SetParent(root.transform);
        spawn.transform.localPosition = new Vector3(-hw + 2f, 0f, hd - 2f);
        spawn.AddComponent<CustomerSpawner>();
        Undo.RegisterCreatedObjectUndo(spawn, "Create SpawnPoint");

        // Customer Exit Point
        GameObject exit = new GameObject("CustomerExitPoint");
        exit.transform.SetParent(root.transform);
        exit.transform.localPosition = new Vector3(hw - 2f, 0f, hd - 2f);
        Undo.RegisterCreatedObjectUndo(exit, "Create ExitPoint");

        // Player with walking.fbx model
        string walkingPath = Path.Combine(ModelPath, "Walking");
        GameObject walkingPrefab = LoadPrefab(walkingPath);
        // Configure the importer itself, not just the temporary default clip
        // copies. This persists Loop Time and Loop Pose into Walking.fbx.
        if (walkingPrefab != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(walkingPrefab);
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                ModelImporterClipAnimation[] clips = modelImporter.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.wrapMode = WrapMode.Loop;
                    clip.loop = true;
                    clip.loopTime = true;
                    clip.loopPose = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootRotation = true;
                }

                modelImporter.animationWrapMode = WrapMode.Loop;
                modelImporter.clipAnimations = clips;
                modelImporter.SaveAndReimport();
            }
        }
        GameObject player;
        if (walkingPrefab != null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(walkingPrefab);
            player.name = "Player";
            player.transform.SetParent(root.transform);
            player.transform.localPosition = new Vector3(0f, 0f, hd - 3f);
            player.tag = "Player";
            // Adjust collider bounds to match model size
            Renderer playerRenderer = player.GetComponentInChildren<Renderer>();
            if (playerRenderer != null)
            {
                Bounds b = playerRenderer.bounds;
                player.transform.localPosition = new Vector3(0f, -b.min.y, hd - 3f);
            }
        }
        else
        {
            player = new GameObject("Player");
            player.transform.SetParent(root.transform);
            player.transform.localPosition = new Vector3(0f, 0f, hd - 3f);
            player.tag = "Player";
        }
        Undo.RegisterCreatedObjectUndo(player, "Create Player");

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.center = new Vector3(0f, 1f, 0f);

        PlayerController pc = player.AddComponent<PlayerController>();
        pc.moveSpeed = 4f;
        pc.rotateSpeed = 10f;

        UnityEngine.InputSystem.PlayerInput pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
        pi.actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        pi.uiInputModule = null;

        CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1f, 0f);

        // Set a default legacy Animation state explicitly. WrapMode.Loop alone
        // does not blend the first and final pose of an imported FBX clip.
        Animation playerAnimation = player.GetComponent<Animation>();
        if (playerAnimation == null)
            playerAnimation = player.AddComponent<Animation>();

        AnimationClip walkingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Model/Walking.fbx");
        if (walkingClip != null)
        {
            walkingClip.wrapMode = WrapMode.Loop;
            if (playerAnimation.GetClip("Walking") == null)
                playerAnimation.AddClip(walkingClip, "Walking");

            playerAnimation.clip = walkingClip;
            playerAnimation.wrapMode = WrapMode.Loop;
            playerAnimation.playAutomatically = true;
            playerAnimation.Play("Walking");
        }

        // NavMesh Setup (바닥에 NavMeshSurface 추가)
        GameObject floor = GameObject.Find("*Floor*");
        if (floor == null)
        {
            floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            MeshFilter floorFilter = floor.AddComponent<MeshFilter>();
            MeshRenderer floorRenderer = floor.AddComponent<MeshRenderer>();
            floorRenderer.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wood.mat");
            BoxCollider floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(roomWidth + 2f, 0.2f, roomDepth + 2f);
        }

        NavMeshSurface navSurface = floor.AddComponent<NavMeshSurface>();
        navSurface.buildHeightMesh = true;
        navSurface.layerMask = 1 << LayerMask.NameToLayer("Default");
        Undo.RegisterCreatedObjectUndo(navSurface, "Add NavMeshSurface");

        // Create GameManager
        if (GameObject.FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");
        }

        // Create OrderManager
        if (GameObject.FindFirstObjectByType<OrderManager>() == null)
        {
            GameObject om = new GameObject("OrderManager");
            om.AddComponent<OrderManager>();
            Undo.RegisterCreatedObjectUndo(om, "Create OrderManager");
        }

        // Create default DrinkData assets if they don't exist
        CreateDefaultDrinkData();

        // Lights
        GameObject lightsParent = new GameObject("WarmLights");
        lightsParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(lightsParent, "Create Lights");

        CreateWarmLight(lightsParent.transform, new Vector3(0f, 3f, 0f), 1.8f, 12f);
        CreateWarmLight(lightsParent.transform, new Vector3(-hw + 2f, 2.5f, 0f), 1.2f, 9f);
        CreateWarmLight(lightsParent.transform, new Vector3(hw - 2f, 2.5f, 0f), 1.2f, 9f);
        CreateWarmLight(lightsParent.transform, new Vector3(0f, 2f, hd - 2f), 1f, 8f);

        Selection.activeGameObject = root;
        SetupSceneView();
        SetupGameCamera();
        Debug.Log("SimBar cozy room layout created successfully!");
    }

    private void CreateDefaultDrinkData()
    {
        string path = "Assets/ScriptableObjects";
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        // Create Beer drink data
        string beerPath = path + "/Beer.asset";
        if (AssetDatabase.LoadAssetAtPath<DrinkData>(beerPath) == null)
        {
            DrinkData beer = ScriptableObject.CreateInstance<DrinkData>();
            beer.name = "Beer";
            beer.price = 5;
            beer.prepTime = 3f;
            AssetDatabase.CreateAsset(beer, beerPath);
        }

        // Create Cocktail drink data
        string cocktailPath = path + "/Cocktail.asset";
        if (AssetDatabase.LoadAssetAtPath<DrinkData>(cocktailPath) == null)
        {
            DrinkData cocktail = ScriptableObject.CreateInstance<DrinkData>();
            cocktail.name = "Cocktail";
            cocktail.price = 10;
            cocktail.prepTime = 5f;
            AssetDatabase.CreateAsset(cocktail, cocktailPath);
        }

        // Create Soda drink data
        string sodaPath = path + "/Soda.asset";
        if (AssetDatabase.LoadAssetAtPath<DrinkData>(sodaPath) == null)
        {
            DrinkData soda = ScriptableObject.CreateInstance<DrinkData>();
            soda.name = "Soda";
            soda.price = 3;
            soda.prepTime = 1f;
            AssetDatabase.CreateAsset(soda, sodaPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }



    private void CreateBarCounter(GameObject parent, float x, float y, float z, float rotY, float roomWidth)
    {
        GameObject bar = new GameObject("BarCounter");
        bar.transform.SetParent(parent.transform);
        bar.transform.localPosition = new Vector3(x, y, z);
        bar.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        bar.AddComponent<BoxCollider>();
        bar.AddComponent<BarCounter>();
        Undo.RegisterCreatedObjectUndo(bar, "Create BarCounter");

        var floorModel = LoadPrefab(Path.Combine(MarketPath, "floor"));
        if (floorModel != null)
        {
            GameObject floor = (GameObject)PrefabUtility.InstantiatePrefab(floorModel, bar.transform);
            floor.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            floor.transform.localScale = Vector3.one * 1.5f;
            Undo.RegisterCreatedObjectUndo(floor, "Place Bar Floor");
        }

        var shelfModel = LoadPrefab(Path.Combine(MarketPath, "shelf-boxes"));
        if (shelfModel != null)
        {
            GameObject shelf = (GameObject)PrefabUtility.InstantiatePrefab(shelfModel, bar.transform);
            shelf.transform.localPosition = new Vector3(0f, 0.8f, -0.4f);
            Undo.RegisterCreatedObjectUndo(shelf, "Place Bar Shelf");
        }

        var freezerModel = LoadPrefab(Path.Combine(MarketPath, "freezer"));
        if (freezerModel != null)
        {
            GameObject freezer = (GameObject)PrefabUtility.InstantiatePrefab(freezerModel, bar.transform);
            freezer.transform.localPosition = new Vector3(-roomWidth * 0.25f, 0.5f, 0f);
            Undo.RegisterCreatedObjectUndo(freezer, "Place Bar Freezer");
        }

        var cashModel = LoadPrefab(Path.Combine(MarketPath, "cash-register"));
        if (cashModel != null)
        {
            GameObject cash = (GameObject)PrefabUtility.InstantiatePrefab(cashModel, bar.transform);
            cash.transform.localPosition = new Vector3(roomWidth * 0.25f, 0.6f, 0.2f);
            Undo.RegisterCreatedObjectUndo(cash, "Place CashRegister");
        }
    }

    private void CreateStage(GameObject parent, float x, float y, float z, float rotY)
    {
        GameObject stage = new GameObject("Stage");
        stage.transform.SetParent(parent.transform);
        stage.transform.localPosition = new Vector3(x, y, z);
        stage.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        stage.AddComponent<Stage>();
        Undo.RegisterCreatedObjectUndo(stage, "Create Stage");

        var guitarModel = LoadPrefab(Path.Combine(ModelPath, "Guitar Playing"));
        var gibsonModel = LoadPrefab(Path.Combine(ModelPath, "gibson"));
        if (guitarModel != null)
        {
            GameObject guitarPerformer = (GameObject)PrefabUtility.InstantiatePrefab(guitarModel, stage.transform);
            guitarPerformer.name = "GuitarPerformer";
            guitarPerformer.transform.localPosition = new Vector3(-0.8f, 0f, -0.5f);
            guitarPerformer.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            Undo.RegisterCreatedObjectUndo(guitarPerformer, "Place GuitarPerformer");

            // 기타리스트 캐릭터에게 gibson 기타를 손에 쥐도록 Root transform 아래에 자식으로 추가
            if (gibsonModel != null)
            {
                GameObject guitarItem = (GameObject)PrefabUtility.InstantiatePrefab(gibsonModel, guitarPerformer.transform);
                guitarItem.name = "GibsonGuitar";

                // 캐릭터 Root 아래에 배치하고 전체 크기에 맞춰 위치/크기 조정
                guitarItem.transform.localPosition = new Vector3(-0.1f, 1.0f, 0.05f);
                guitarItem.transform.localRotation = Quaternion.Euler(0f, -90f, 10f);
                guitarItem.transform.localScale = Vector3.one * 20f;

                Undo.RegisterCreatedObjectUndo(guitarItem, "Place Gibson Guitar on Performer");
            }
        }

        var singingModel = LoadPrefab(Path.Combine(ModelPath, "Singing"));
        if (singingModel != null)
        {
            GameObject singer = (GameObject)PrefabUtility.InstantiatePrefab(singingModel, stage.transform);
            singer.name = "Singer";
            singer.transform.localPosition = new Vector3(0.8f, 0f, -0.5f);
            singer.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(singer, "Place Singer");
        }

        var floorModel = LoadPrefab(Path.Combine(ArcadePath, "floor"));
        if (floorModel != null)
        {
            GameObject stageFloor = (GameObject)PrefabUtility.InstantiatePrefab(floorModel, stage.transform);
            stageFloor.transform.localPosition = new Vector3(0f, -0.1f, 0.3f);
            stageFloor.transform.localScale = Vector3.one * 2f;
            Undo.RegisterCreatedObjectUndo(stageFloor, "Place StageFloor");
        }

    }

    private void CreateTableSet(GameObject parent, float roomWidth, float roomDepth)
    {
        var tableModel = LoadPrefab(Path.Combine(ArcadePath, "air-hockey"));
        if (tableModel == null)
        {
            tableModel = LoadPrefab(Path.Combine(MarketPath, "display-bread"));
        }

        Vector3[] positions = {
            new Vector3(-roomWidth * 0.25f, 0f, -roomDepth * 0.15f),
            new Vector3(roomWidth * 0.25f, 0f, -roomDepth * 0.15f),
            new Vector3(-roomWidth * 0.25f, 0f, roomDepth * 0.15f),
            new Vector3(roomWidth * 0.25f, 0f, roomDepth * 0.15f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject table = new GameObject("Table_" + (i + 1));
            table.transform.SetParent(parent.transform);
            table.transform.localPosition = positions[i];
            table.AddComponent<Table>();
            table.AddComponent<BoxCollider>();
            Undo.RegisterCreatedObjectUndo(table, "Create Table");

            if (tableModel != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(tableModel, table.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = Vector3.one * 0.8f;
                Undo.RegisterCreatedObjectUndo(model, "Place TableModel");
            }
        }
    }

    private void SetupGameCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject camObj = new GameObject("MainCamera");
            camObj.tag = "MainCamera";
            mainCamera = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = Mathf.Max(roomSize, roomSize) * 0.6f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = 50f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.12f, 0.1f, 0.14f);

        float hw = roomSize / 2f;
        float hd = roomSize / 2f;
        float dist = Mathf.Max(roomSize, roomSize) * 1.3f;
        Vector3 roomCenter = new Vector3(0f, wallHeight * 0.35f, 0f);

        mainCamera.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
        mainCamera.transform.position = roomCenter - mainCamera.transform.forward * dist;

        if (mainCamera.gameObject.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }

        Selection.activeGameObject = mainCamera.gameObject;
    }

    private void SetupSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        float hw = roomSize / 2f;
        float hd = roomSize / 2f;
        float maxDim = Mathf.Max(roomSize, roomSize);
        float dist = maxDim * 1.3f;
        Vector3 roomCenter = new Vector3(0f, wallHeight * 0.35f, 0f);

        sceneView.pivot = roomCenter;
        sceneView.size = dist;
        sceneView.rotation = Quaternion.Euler(35f, 45f, 0f);
        sceneView.orthographic = true;
        sceneView.Repaint();
    }

    private Bounds GetPrefabBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void PlaceWallLine(GameObject parent, GameObject prefab, string baseName, float startX, float startZ, float length, float y, float rotY, float height, float originalHeight)
    {
        Bounds wallBounds = GetPrefabBounds(prefab);
        float wallWidth = wallBounds.size.x;
        int count = Mathf.Max(1, Mathf.RoundToInt(length / wallWidth));

        for (int i = 0; i < count; i++)
        {
            float x = startX;
            float z = startZ;
            if (rotY == 0f || rotY == 180f)
            {
                x = startX + (i + 0.5f) * wallWidth;
            }
            else
            {
                z = startZ + (i + 0.5f) * wallWidth;
            }
            PlaceWall(parent, prefab, baseName + "_" + i, x, y, z, height, rotY, 0f, originalHeight);
        }
    }

    private void PlaceWall(GameObject parent, GameObject prefab, string name, float x, float y, float z, float height, float rotY, float rotZ, float originalHeight)
    {
        if (prefab == null) return;
        GameObject wall = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        wall.name = name;
        wall.transform.SetParent(parent.transform);
        wall.transform.localPosition = new Vector3(x, y, z);
        wall.transform.localRotation = Quaternion.Euler(0f, rotY, rotZ);
        float scaleY = originalHeight > 0.001f ? height / originalHeight : 1f;
        wall.transform.localScale = new Vector3(1f, scaleY, 1f);

        Renderer renderer = wall.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Bounds b = renderer.bounds;
            wall.transform.localPosition = new Vector3(x, y - b.min.y, z);
        }

        Undo.RegisterCreatedObjectUndo(wall, "Place Wall");
    }

    private void PlaceCorner(GameObject parent, GameObject prefab, string name, float x, float y, float z, float height, float originalHeight)
    {
        if (prefab == null) return;
        GameObject corner = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        corner.name = name;
        corner.transform.SetParent(parent.transform);
        corner.transform.localPosition = new Vector3(x, y, z);
        float scaleY = originalHeight > 0.001f ? height / originalHeight : 1f;
        corner.transform.localScale = new Vector3(1f, scaleY, 1f);
        Undo.RegisterCreatedObjectUndo(corner, "Place Corner");
    }

    private void PlaceMachine(GameObject parent, GameObject prefab, string name, float x, float y, float z, float rotY)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Prefab not found for: " + name);
            return;
        }
        GameObject machine = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        machine.name = name;
        machine.transform.SetParent(parent.transform);
        machine.transform.localPosition = new Vector3(x, y, z);
        machine.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

        Renderer renderer = machine.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Bounds b = renderer.bounds;
            machine.transform.localPosition = new Vector3(x, y - b.min.y, z);
        }

        Undo.RegisterCreatedObjectUndo(machine, "Place Machine");
    }

    private void PlaceRiggedCharacterModels(Transform root, float roomWidth, float roomDepth)
    {
        string[] modelPaths = Directory.GetFiles(ModelPath, "*.fbx", SearchOption.TopDirectoryOnly);
        if (modelPaths.Length == 0)
        {
            return;
        }

        Vector3[] positions =
        {
            new Vector3(-roomWidth * 0.38f, 0f, roomDepth * 0.28f),
            new Vector3(-roomWidth * 0.12f, 0f, roomDepth * 0.34f),
            new Vector3(roomWidth * 0.15f, 0f, roomDepth * 0.30f),
            new Vector3(roomWidth * 0.38f, 0f, roomDepth * 0.18f),
            new Vector3(-roomWidth * 0.38f, 0f, -roomDepth * 0.16f),
            new Vector3(-roomWidth * 0.12f, 0f, -roomDepth * 0.22f),
            new Vector3(roomWidth * 0.15f, 0f, -roomDepth * 0.18f),
            new Vector3(roomWidth * 0.38f, 0f, -roomDepth * 0.28f)
        };

        int index = 0;
        foreach (var path in modelPaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, "Guitar Playing", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Singing", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "gibson", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index >= positions.Length)
            {
                break;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (asset == null)
            {
                continue;
            }

            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            character.name = fileName;
            character.transform.localPosition = positions[index];
            character.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            character.transform.localScale = Vector3.one * 1.05f;
            Undo.RegisterCreatedObjectUndo(character, "Place Character FBX");

            if (string.Equals(fileName, "Casual", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Casual2", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Adventurer", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Beach", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Farmer", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "King", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Punk", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Suit", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Spacesuit", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Swat", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Worker", System.StringComparison.OrdinalIgnoreCase))
            {
                character.transform.localScale = new Vector3(1f, 1f, 1f);
            }

            index++;
        }
    }

    private void PlaceAnimationReferenceModels(Transform root, float roomDepth)
        {
            GameObject jumpModel = LoadPrefab(Path.Combine(ModelPath, "Standing Jump"));
            GameObject spell1HModel = LoadPrefab(Path.Combine(ModelPath, "Standing 1H Cast Spell 01"));
            GameObject spell2HModel = LoadPrefab(Path.Combine(ModelPath, "Standing 2H Cast Spell 01"));

            float hd = roomDepth / 2f;
            float stageZ = hideBottomWalls ? -hd + 1.5f : -hd + 0.5f; // Re-use stageZ calculation

            if (jumpModel != null)
            {
                GameObject jumpChar = (GameObject)PrefabUtility.InstantiatePrefab(jumpModel, root);
                jumpChar.name = "JumpReference";
                jumpChar.transform.localPosition = new Vector3(-2.5f, 0f, stageZ + 1.5f); // 왼쪽에 배치
                jumpChar.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 방을 바라보도록 설정
                jumpChar.transform.localScale = Vector3.one * 1.05f;
                Undo.RegisterCreatedObjectUndo(jumpChar, "Place Jump Reference Model");
            }

            if (spell1HModel != null)
            {
                GameObject spell1HChar = (GameObject)PrefabUtility.InstantiatePrefab(spell1HModel, root);
                spell1HChar.name = "Spell1HReference";
                spell1HChar.transform.localPosition = new Vector3(0f, 0f, stageZ + 1.5f); // 가운데에 배치
                spell1HChar.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                spell1HChar.transform.localScale = Vector3.one * 1.05f;
                Undo.RegisterCreatedObjectUndo(spell1HChar, "Place Spell 1H Reference Model");
            }

            if (spell2HModel != null)
            {
                GameObject spell2HChar = (GameObject)PrefabUtility.InstantiatePrefab(spell2HModel, root);
                spell2HChar.name = "Spell2HReference";
                spell2HChar.transform.localPosition = new Vector3(2.5f, 0f, stageZ + 1.5f); // 오른쪽에 배치
                spell2HChar.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                spell2HChar.transform.localScale = Vector3.one * 1.05f;
                Undo.RegisterCreatedObjectUndo(spell2HChar, "Place Spell 2H Reference Model");
            }
        }


    private void CreateWarmLight(Transform parent, Vector3 localPos, float intensity, float range)
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
        Undo.RegisterCreatedObjectUndo(light, "Create WarmLight");
    }
}