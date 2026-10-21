using UnityEngine;

public class BarSceneBuilder : MonoBehaviour
{
    public bool buildOnStart = true;
    public bool createFrontBackWalls = true;
    public bool createSideWalls = true;
    private bool isBuiltInternal;
    public bool isBuilt => isBuiltInternal;

    public void BuildScene()
    {
        if (isBuiltInternal)
            return;

        CreateFloor();
        CreateWalls();
        CreateCounter();

        var barUI = FindFirstObjectByType<BarUIBuilder>();
        if (barUI == null)
        {
            var uiObject = new GameObject("BarUIBuilder");
            barUI = uiObject.AddComponent<BarUIBuilder>();
        }
        barUI.BuildUI();

        isBuiltInternal = true;
    }

    private void Start()
    {
        if (buildOnStart)
            BuildScene();
    }

    private void CreateFloor()
    {
        if (GameObject.Find("BarFloor") != null)
            return;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "BarFloor";
        floor.transform.position = new Vector3(0f, 0f, 0f);
        floor.transform.localScale = new Vector3(12f, 0.5f, 12f);
        var floorRenderer = floor.GetComponent<Renderer>();
        if (floorRenderer != null)
        {
            floorRenderer.material.color = new Color(0.45f, 0.42f, 0.55f);
        }
    }

    private void CreateWalls()
    {
        if (createFrontBackWalls)
        {
            CreateWall("FrontWall", new Vector3(0f, 2.25f, 5.75f), new Vector3(12f, 4.5f, 0.5f));
            CreateWall("BackWall", new Vector3(0f, 2.25f, -5.75f), new Vector3(12f, 4.5f, 0.5f));
        }

        if (createSideWalls)
        {
            CreateWall("LeftWall", new Vector3(-5.75f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 12f));
            CreateWall("RightWall", new Vector3(5.75f, 2.25f, 0f), new Vector3(0.5f, 4.5f, 12f));
        }
    }

    private void CreateCounter()
    {
        if (GameObject.Find("BarCounter") != null)
            return;

        var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = "BarCounter";
        counter.transform.position = new Vector3(0f, 1.25f, -2.5f);
        counter.transform.localScale = new Vector3(8f, 2.5f, 1.5f);
        var renderer = counter.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.9f, 0.75f, 0.38f);
        }
    }

    private void CreateWall(string wallName, Vector3 position, Vector3 scale)
    {
        if (GameObject.Find(wallName) != null)
            return;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.position = position;
        wall.transform.localScale = scale;

        var renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.35f, 0.35f, 0.45f);
        }
    }
}