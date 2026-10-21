using UnityEngine;
using UnityEditor;

public class MiniArcadeLayout : EditorWindow
{
    private float roomSize = 5f;
    private float wallHeight = 2.5f;
    private bool squareRoom = true;

    [MenuItem("GameObject/Mini Arcade/Setup Layout")]
    public static void ShowWindow()
    {
        GetWindow<MiniArcadeLayout>("Mini Arcade Layout");
    }

    private void OnGUI()
    {
        GUILayout.Label("Mini Arcade Room Settings", EditorStyles.boldLabel);
        squareRoom = EditorGUILayout.Toggle("Square Room", squareRoom);
        roomSize = EditorGUILayout.FloatField("Room Size", roomSize);
        if (!squareRoom)
        {
            roomDepth = EditorGUILayout.FloatField("Room Depth", roomDepth);
        }
        wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
        hideBottomWalls = EditorGUILayout.Toggle("Hide Bottom Walls", hideBottomWalls);

        if (GUILayout.Button("Create Layout"))
        {
            CreateMiniArcadeLayout();
        }
    }

    private float roomWidth = 5f;
    private float roomDepth = 5f;
    private bool hideBottomWalls = false;

    private GameObject LoadPrefab(string relativePath)
    {
        string path = "Assets/Kenny_arcade/FBX format/" + relativePath + ".fbx";
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private void CreateMiniArcadeLayout()
    {
        roomWidth = roomSize;
        roomDepth = squareRoom ? roomSize : roomDepth;
        GameObject root = new GameObject("MiniArcade_Room");
        Undo.RegisterCreatedObjectUndo(root, "Create Mini Arcade Layout");

        float hw = roomWidth / 2f;
        float hd = roomDepth / 2f;

        // Floor tiles
        GameObject floorParent = new GameObject("Floor");
        floorParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(floorParent, "Create Floor");

        GameObject floorPrefab = LoadPrefab("floor");
        if (floorPrefab == null)
        {
            Debug.LogError("Floor prefab not found at Assets/Kenny_arcade/FBX format/floor.fbx");
            return;
        }

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

        // Walls
        GameObject wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(wallsParent, "Create Walls");

        GameObject wallPrefab = LoadPrefab("wall");
        GameObject cornerPrefab = LoadPrefab("wall-corner");

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

        // Arcade Machines
        GameObject machinesParent = new GameObject("ArcadeMachines");
        machinesParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(machinesParent, "Create Arcade Machines");

        // Front wall (Z = -hd, facing +Z)
        float frontZ = hideBottomWalls ? -hd + 1.0f : -hd + 0.35f;
        PlaceMachine(machinesParent, LoadPrefab("arcade-machine"), "ArcadeMachine_1", -hw + 1.2f, 0f, frontZ, 0f);
        PlaceMachine(machinesParent, LoadPrefab("basketball-game"), "BasketballGame_1", -hw + 2.4f, 0f, frontZ, 0f);
        PlaceMachine(machinesParent, LoadPrefab("air-hockey"), "AirHockey_1", hw - 1.2f, 0f, frontZ, 0f);
        PlaceMachine(machinesParent, LoadPrefab("pinball"), "Pinball_1", hw - 2.4f, 0f, frontZ, 0f);

        // Back wall (Z = hd, facing -Z)
        float backZ = hideBottomWalls ? hd - 1.0f : hd - 0.35f;
        PlaceMachine(machinesParent, LoadPrefab("gambling-machine"), "GamblingMachine_1", -hw + 1.2f, 0f, backZ, 180f);
        PlaceMachine(machinesParent, LoadPrefab("ticket-machine"), "TicketMachine_1", 0f, 0f, backZ, 180f);
        PlaceMachine(machinesParent, LoadPrefab("vending-machine"), "VendingMachine_1", hw - 1.2f, 0f, backZ, 180f);

        // Left wall (X = -hw, facing +X)
        PlaceMachine(machinesParent, LoadPrefab("claw-machine"), "ClawMachine_1", -hw + 0.35f, 0f, -hd + 1.2f, 90f);
        PlaceMachine(machinesParent, LoadPrefab("dance-machine"), "DanceMachine_1", -hw + 0.35f, 0f, -hd + 2.4f, 90f);

        // Right wall (X = hw, facing -X)
        PlaceMachine(machinesParent, LoadPrefab("cash-register"), "CashRegister_1", hw - 0.35f, 0f, -hd + 1.2f, -90f);
        PlaceMachine(machinesParent, LoadPrefab("prizes"), "Prizes_1", hw - 0.35f, 0f, -hd + 2.4f, -90f);

        // Central features
        GameObject centralParent = new GameObject("CentralFeatures");
        centralParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(centralParent, "Create Central Features");

        GameObject prizeWheelPrefab = LoadPrefab("prize-wheel");
        GameObject columnPrefab = LoadPrefab("column");
        GameObject singingPrefab = LoadPrefab("../Model/Singing");
        GameObject guitarPrefab = LoadPrefab("../Model/Guitar Playing");

        if (prizeWheelPrefab != null)
        {
            Bounds pwBounds = GetPrefabBounds(prizeWheelPrefab);
            PlaceMachine(centralParent, prizeWheelPrefab, "PrizeWheel_1", 0f, pwBounds.extents.y, 0f, 0f);
        }
        if (columnPrefab != null)
        {
            Bounds colBounds = GetPrefabBounds(columnPrefab);
            PlaceMachine(centralParent, columnPrefab, "Column_1", -1.5f, colBounds.extents.y, -1f, 0f);
            PlaceMachine(centralParent, columnPrefab, "Column_2", 1.5f, colBounds.extents.y, 1f, 0f);
        }
        if (singingPrefab != null)
        {
            PlaceSingingCharacter(centralParent, singingPrefab, "SingingCharacter", 0f, 0f, 1.2f);
        }
        else
        {
            GameObject directSinging = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Singing.fbx");
            if (directSinging != null)
            {
                PlaceSingingCharacter(centralParent, directSinging, "SingingCharacter", 0f, 0f, 1.2f);
            }
        }
        if (guitarPrefab != null)
        {
            PlaceSingingCharacter(centralParent, guitarPrefab, "GuitarCharacter", -1.8f, 0f, 0.5f);
        }
        else
        {
            GameObject directGuitar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Guitar Playing.fbx");
            if (directGuitar != null)
            {
                PlaceSingingCharacter(centralParent, directGuitar, "GuitarCharacter", -1.8f, 0f, 0.5f);
            }
        }

        Selection.activeGameObject = root;
        SetupSceneView();
        SetupGameCamera();
        Debug.Log("Mini Arcade layout created successfully!");
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
        mainCamera.orthographicSize = Mathf.Max(roomWidth, roomDepth) * 0.6f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = 50f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

        float hw = roomWidth / 2f;
        float hd = roomDepth / 2f;
        float dist = Mathf.Max(roomWidth, roomDepth) * 1.3f;
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

        float hw = roomWidth / 2f;
        float hd = roomDepth / 2f;
        float maxDim = Mathf.Max(roomWidth, roomDepth);
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

    private void PlaceSingingCharacter(GameObject parent, GameObject prefab, string name, float x, float y, float z)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Singing prefab not found at Assets/Model/Singing.fbx");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (!string.IsNullOrEmpty(assetPath))
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Legacy)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.SaveAndReimport();
            }
        }

        GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.name = name;
        character.transform.SetParent(parent.transform);
        character.transform.localPosition = new Vector3(x, 0f, z);
        character.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        character.transform.localScale = new Vector3(1f, 1f, 1f);

        Animation animation = character.GetComponent<Animation>();
        if (animation == null)
        {
            animation = character.AddComponent<Animation>();
        }

        if (!string.IsNullOrEmpty(assetPath))
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip[] clips = System.Array.FindAll(assets, a => a is AnimationClip) as AnimationClip[];
            if (clips != null && clips.Length > 0)
            {
                string safeName = clips[0].name.Replace(" ", "_").Replace(".", "_");
                animation.AddClip(clips[0], safeName);
                animation.Play(safeName);
            }
        }

        Undo.RegisterCreatedObjectUndo(character, "Place Singing Character");
    }
}
