using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Playable live-music pub loop, using the Blender venue and the project's FBX
/// performers. The original geometry remains a fallback for missing art assets.
/// </summary>
public sealed partial class SimBarExperience : MonoBehaviour
{
    private const float StartingCash = 12340f;
    private static readonly string[] EnglishDrinkNames = { "AMBER ALE", "NEON FIZZ", "WHISKEY SOUR" };
    private static readonly string[] KoreanDrinkNames = { "앰버 에일", "네온 피즈", "위스키 사워" };
    private static string[] DrinkNames => SimBarLocalization.IsKorean ? KoreanDrinkNames : EnglishDrinkNames;
    private static readonly int[] DrinkPrices = { 28, 34, 42 };
    private static readonly Color[] DrinkColors =
    {
        new Color(1f, 0.60f, 0.14f),
        new Color(0.10f, 0.92f, 0.86f),
        new Color(0.95f, 0.33f, 0.48f)
    };
    private static readonly string[] Playlist =
    {
        "1. Electric Hearts",
        "2. Midnight Run",
        "3. Neon Avenue",
        "4. Afterparty Echo",
        "5. Velvet Thunder",
        "6. Last Call Anthem"
    };
    private static readonly string[] GuestNames =
    {
        "Mina", "Jae", "Sora", "Noel", "Dani", "Yuna", "Theo", "Ari", "Ivy", "Rin"
    };

    private sealed class Patron
    {
        public string name;
        public Transform root;
        public Transform leftArm;
        public Transform rightArm;
        public GameObject orderMarker;
        public int drinkIndex = -1;
        public float patience;
        public float phase;
        public float baseY;
        public bool isDancing;
        public float baseYaw;
    }

    private readonly List<Patron> patrons = new List<Patron>();
    private readonly Queue<Patron> orderQueue = new Queue<Patron>();
    private readonly List<Light> stageLights = new List<Light>();
    private readonly List<Image> playlistRows = new List<Image>();

    private Transform worldRoot;
    private Canvas canvas;
    private Text cashText;
    private Text fanText;
    private Text timeText;
    private Text orderText;
    private Text queueText;
    private Text toastText;
    private Text playlistNowText;
    private Text pauseText;
    private Image satisfactionFill;
    private Image performanceFill;
    private GameObject pausePanel;
    private AudioSource musicSource;

    private Material floorMat;
    private Material wallMat;
    private Material woodMat;
    private Material leatherMat;
    private Material metalMat;
    private Material stageMat;
    private Material cyanGlowMat;
    private Material pinkGlowMat;
    private Material amberGlowMat;
    private readonly Material[] orderMarkerMaterials = new Material[3];

    private float cash = StartingCash;
    private int fans = 256;
    private float satisfaction = 0.94f;
    private float performance = 0.98f;
    private float gameClock = 21.5f;
    private float beat;
    private float requestTimer = 4f;
    private float toastTimer;
    private int playlistIndex;
    private int servedTonight;
    private bool paused;
    private int characterVariant;

    private void Awake()
    {
        Application.runInBackground = true;
        SuppressLegacyPresentation();
        CreateMaterials();
        ConfigureCamera();
        BuildEnvironment();
        CreateMusicSource();
        BuildInterface();
        InitializeOperations();
        SeedOrders();
        InitializePourGame();
        InitializeJourney();
        InitializeLanguageSettings();
        SimBarReadability.Attach(canvas, this);
        RefreshUI();
        if (JourneyIsPlaying)
            ShowToast(SimBarLocalization.Text("LIVE HOUSE OPEN — keep the crowd glowing!", "라이브 바 오픈! 손님들과 멋진 밤을 만들어 보세요!"), 4f);
    }

    private void SuppressLegacyPresentation()
    {
        // The scene contains a useful authored prototype. Hide it while retaining
        // it in the scene file, then place the final runtime venue in front.
        GameObject prototype = GameObject.Find("SimBar_CozyRoom");
        if (prototype != null)
            prototype.SetActive(false);
        GameObject preview = GameObject.Find("SimBar_EditorPreview");
        if (preview != null)
            preview.SetActive(false);

        foreach (BarSceneBuilder builder in FindObjectsByType<BarSceneBuilder>(FindObjectsInactive.Include))
            builder.enabled = false;
        foreach (BarUIBuilder builder in FindObjectsByType<BarUIBuilder>(FindObjectsInactive.Include))
            builder.enabled = false;
        foreach (CustomerSpawner spawner in FindObjectsByType<CustomerSpawner>(FindObjectsInactive.Include))
            spawner.enabled = false;
        foreach (GameplayStarter starter in FindObjectsByType<GameplayStarter>(FindObjectsInactive.Include))
            starter.enabled = false;
        foreach (UIManager uiManager in FindObjectsByType<UIManager>(FindObjectsInactive.Include))
            uiManager.enabled = false;

        // UIManager is initialized before the scene opens. Remove its prototype
        // canvas so it cannot sit behind the purpose-built HUD.
        foreach (Canvas oldCanvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            Destroy(oldCanvas.gameObject);
    }

    private void ConfigureCamera()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.66f, 0.57f, 0.61f);
        RenderSettings.ambientIntensity = 1f;

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        FrameReferenceCamera(camera);
    }

    public static void FrameReferenceCamera(Camera camera)
    {
        camera.orthographic = true;
        camera.orthographicSize = 8.4f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.026f, 0.055f);
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.transform.position = new Vector3(13f, 21f, -25f);
        camera.transform.LookAt(new Vector3(0f, 1.45f, 0.15f));
    }

    private void CreateMaterials()
    {
        floorMat = MakeMaterial(new Color(0.075f, 0.045f, 0.075f), 0f);
        wallMat = MakeMaterial(new Color(0.31f, 0.065f, 0.055f), 0f);
        woodMat = MakeMaterial(new Color(0.16f, 0.045f, 0.025f), 0f);
        leatherMat = MakeMaterial(new Color(0.28f, 0.045f, 0.06f), 0f);
        metalMat = MakeMaterial(new Color(0.10f, 0.13f, 0.20f), 0f);
        stageMat = MakeMaterial(new Color(0.18f, 0.05f, 0.25f), 0f);
        cyanGlowMat = MakeMaterial(new Color(0.05f, 0.85f, 1f), 2.2f);
        pinkGlowMat = MakeMaterial(new Color(1f, 0.08f, 0.60f), 2.2f);
        amberGlowMat = MakeMaterial(new Color(1f, 0.52f, 0.08f), 1.5f);
        for (int i = 0; i < DrinkColors.Length; i++)
            orderMarkerMaterials[i] = MakeMaterial(DrinkColors[i], 1.5f);
    }

    private void CreateMusicSource()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0.14f;
        musicSource.clip = CreateProceduralTrack(playlistIndex);
        musicSource.Play();
    }

    private AudioClip CreateProceduralTrack(int trackIndex)
    {
        const int sampleRate = 24000;
        const float duration = 8f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float[] roots = { 130.81f, 146.83f, 164.81f, 110f, 146.83f, 123.47f };
        float root = roots[Mathf.Clamp(trackIndex, 0, roots.Length - 1)];
        float[] chordIntervals = { 1f, 1.1892f, 1.4983f };

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float beatPosition = time * 2f;
            int beatIndex = Mathf.FloorToInt(beatPosition);
            int barIndex = (beatIndex / 4) % 4;
            float chordRoot = root * (barIndex == 1 ? 0.8909f : barIndex == 2 ? 1.1892f : barIndex == 3 ? 1.0595f : 1f);

            float pad = 0f;
            for (int note = 0; note < chordIntervals.Length; note++)
            {
                float frequency = chordRoot * chordIntervals[note];
                pad += Mathf.Sin(2f * Mathf.PI * frequency * time + note * 0.19f) * (note == 0 ? 0.035f : 0.022f);
            }

            float beatPhase = beatPosition - Mathf.Floor(beatPosition);
            float kick = Mathf.Sin(2f * Mathf.PI * (54f - beatPhase * 18f) * time) * Mathf.Exp(-beatPhase * 12f) * 0.11f;
            float pluckPhase = (time * 4f) - Mathf.Floor(time * 4f);
            float pluckFrequency = chordRoot * ((beatIndex % 2 == 0) ? 2f : 2.3784f);
            float pluck = Mathf.Sin(2f * Mathf.PI * pluckFrequency * time) * Mathf.Exp(-pluckPhase * 9f) * 0.045f;
            float fade = Mathf.Min(1f, Mathf.Min(time * 4f, (duration - time) * 4f));
            samples[i] = (pad + kick + pluck) * fade;
        }

        AudioClip clip = AudioClip.Create("SimBar_Original_Loop_" + trackIndex, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private Material MakeMaterial(Color color, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (emission > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }
        return material;
    }

    private void BuildEnvironment()
    {
        worldRoot = new GameObject("SimBarLiveVenue").transform;

        GameObject venue = Resources.Load<GameObject>("SimBarArt/SimBarVenueReady")
            ?? Resources.Load<GameObject>("SimBarArt/SimBarVenue");
        if (venue != null)
        {
            GameObject art = Instantiate(venue, worldRoot, false);
            art.name = "Blender Venue — Reference Layout";
            CreateReferencePerformers();
            CreateReferenceHouseGuests();
            CreateCrowd();
            CreateLights();
            return;
        }

        CreateBox("Floor", new Vector3(0f, -0.2f, 0f), new Vector3(21f, 0.4f, 15.5f), floorMat, worldRoot);
        CreateBox("StageFloor", new Vector3(5.4f, 0.26f, 4.4f), new Vector3(7.8f, 0.55f, 4.2f), stageMat, worldRoot);
        CreateBox("BackWall", new Vector3(0f, 3.65f, 7.55f), new Vector3(21f, 7.5f, 0.32f), wallMat, worldRoot);
        CreateBox("LeftWall", new Vector3(-10.3f, 3.65f, 0f), new Vector3(0.32f, 7.5f, 15.5f), wallMat, worldRoot);
        CreateBrickAccent();
        CreateBarArea();
        CreateBoothsAndTables();
        CreateStage();
        CreateCrowd();
        CreateDecor();
        CreateLights();
    }

    private void CreateReferencePerformers()
    {
        Transform stage = new GameObject("FBX Live Band").transform;
        stage.SetParent(worldRoot, false);
        CreatePerson("Singer", new Vector3(5.5f, 0.62f, 2.7f), new Color(1f, 0.16f, 0.55f), Color.black, false, stage, "singer");
        CreatePerson("Guitarist", new Vector3(3f, 0.62f, 3.8f), new Color(0.14f, 0.72f, 0.94f), Color.black, false, stage, "guitarist");
        CreatePerson("Bassist", new Vector3(8f, 0.62f, 4.6f), new Color(0.5f, 0.12f, 0.7f), Color.black, false, stage, "bassist");
        CreatePerson("Drummer", new Vector3(6f, 0.62f, 6.2f), new Color(0.17f, 0.18f, 0.25f), Color.black, false, stage, "drummer");
    }

    private void CreateReferenceHouseGuests()
    {
        Transform guests = new GameObject("FBX Bar and Booth Guests").transform;
        guests.SetParent(worldRoot, false);
        CreatePerson("Bartender", new Vector3(-7.3f, 0f, -0.3f), Color.white, Color.black, false, guests, "bartender");
        CreatePerson("Server", new Vector3(-4.2f, 0f, -0.3f), Color.white, Color.black, false, guests, "bartender");
        for (int i = 0; i < 4; i++)
            CreatePerson("Bar Guest " + i, new Vector3(-8.1f + i * 1.65f, 0f, -3.05f), DrinkColors[i % 3], Color.black, false, guests, "guest");
        Vector3[] seats = {
            new Vector3(-8.55f, 0f, 6.4f), new Vector3(-7f, 0f, 6.4f),
            new Vector3(-4.45f, 0f, 6.4f), new Vector3(-2.95f, 0f, 6.4f),
            new Vector3(-8.35f, 0f, -6.5f), new Vector3(-6.85f, 0f, -6.5f),
            new Vector3(-3.45f, 0f, -6.5f)
        };
        for (int i = 0; i < seats.Length; i++)
            CreatePerson("Lounge Guest " + i, seats[i], DrinkColors[i % 3], Color.black, false, guests, "seated", i < 4 ? 180f : 0f);
    }

    private void CreateBrickAccent()
    {
        Transform bricks = new GameObject("Warm Brickwork").transform;
        bricks.SetParent(worldRoot, false);
        Material mortar = MakeMaterial(new Color(0.13f, 0.025f, 0.03f), 0f);
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 18; column++)
            {
                float x = -9.55f + column * 1.08f + (row % 2 == 0 ? 0f : 0.52f);
                CreateBox("Brick", new Vector3(x, 0.36f + row * 0.58f, 7.34f), new Vector3(0.98f, 0.46f, 0.08f), row % 3 == 0 ? mortar : wallMat, bricks);
            }
        }
    }

    private void CreateBarArea()
    {
        Transform bar = new GameObject("Cocktail Bar").transform;
        bar.SetParent(worldRoot, false);
        CreateBox("CounterBase", new Vector3(-4.8f, 0.85f, -1.5f), new Vector3(7.6f, 1.5f, 1.35f), woodMat, bar);
        CreateBox("CounterTop", new Vector3(-4.8f, 1.65f, -1.5f), new Vector3(8.05f, 0.18f, 1.72f), amberGlowMat, bar);
        CreateBox("BackBar", new Vector3(-5.0f, 2.55f, -2.62f), new Vector3(7.6f, 3.3f, 0.42f), metalMat, bar);

        for (int shelf = 0; shelf < 3; shelf++)
        {
            float y = 2.05f + shelf * 0.8f;
            CreateBox("GlassShelf", new Vector3(-5.0f, y, -2.35f), new Vector3(7.15f, 0.07f, 0.45f), cyanGlowMat, bar);
            for (int bottle = 0; bottle < 9; bottle++)
            {
                Color color = DrinkColors[(bottle + shelf) % DrinkColors.Length];
                Material bottleMat = MakeMaterial(color, 0.35f);
                CreateCylinder("Bottle", new Vector3(-8.05f + bottle * 0.76f, y + 0.22f, -2.14f), new Vector3(0.12f, 0.32f, 0.12f), bottleMat, bar);
            }
        }

        for (int stool = 0; stool < 6; stool++)
        {
            float x = -7.5f + stool * 1.12f;
            CreateCylinder("Bar stool", new Vector3(x, 0.45f, -0.35f), new Vector3(0.32f, 0.45f, 0.32f), leatherMat, bar);
        }

        CreatePerson("Bartender", new Vector3(-4.8f, 0f, -2.16f), new Color(0.95f, 0.76f, 0.52f), new Color(0.08f, 0.08f, 0.12f), false, bar);
        CreatePerson("Server", new Vector3(-2.4f, 0f, -2.16f), new Color(0.72f, 0.42f, 0.86f), new Color(0.16f, 0.06f, 0.2f), false, bar);
    }

    private void CreateBoothsAndTables()
    {
        Transform lounge = new GameObject("Lounge Booths").transform;
        lounge.SetParent(worldRoot, false);
        CreateBooth(new Vector3(-6.5f, 2.95f, 3.8f), lounge);
        CreateBooth(new Vector3(-2.1f, 3.6f, 4.9f), lounge);
        CreateTable(new Vector3(-2.9f, 0f, 1.7f), lounge, false);
        CreateTable(new Vector3(0.4f, 0f, 2.4f), lounge, false);
    }

    private void CreateBooth(Vector3 position, Transform parent)
    {
        CreateBox("BoothBack", position + new Vector3(0f, 1.12f, 0.85f), new Vector3(3.3f, 1.6f, 0.48f), leatherMat, parent);
        CreateBox("BoothSeat", position + new Vector3(0f, 0.48f, 0.30f), new Vector3(3.3f, 0.55f, 1.25f), leatherMat, parent);
        CreateBox("BoothTable", position + new Vector3(0f, 0.9f, -0.55f), new Vector3(1.8f, 0.15f, 1f), woodMat, parent);
        CreateCylinder("TableStem", position + new Vector3(0f, 0.42f, -0.55f), new Vector3(0.13f, 0.42f, 0.13f), metalMat, parent);
        CreatePerson("BoothGuest", position + new Vector3(-0.85f, 0f, 0.40f), new Color(0.35f, 0.65f, 1f), new Color(0.12f, 0.08f, 0.1f), false, parent);
        CreatePerson("BoothGuest", position + new Vector3(0.85f, 0f, 0.40f), new Color(1f, 0.35f, 0.55f), new Color(0.2f, 0.05f, 0.05f), false, parent);
    }

    private void CreateTable(Vector3 position, Transform parent, bool withGuests)
    {
        CreateCylinder("RoundTable", position + new Vector3(0f, 0.92f, 0f), new Vector3(0.82f, 0.09f, 0.82f), woodMat, parent);
        CreateCylinder("TableLeg", position + new Vector3(0f, 0.43f, 0f), new Vector3(0.12f, 0.45f, 0.12f), metalMat, parent);
        if (!withGuests)
            return;
        CreatePerson("TableGuest", position + new Vector3(-0.9f, 0f, 0.05f), new Color(0.45f, 0.78f, 0.65f), new Color(0.1f, 0.05f, 0.14f), false, parent);
        CreatePerson("TableGuest", position + new Vector3(0.9f, 0f, -0.05f), new Color(1f, 0.48f, 0.20f), new Color(0.12f, 0.08f, 0.06f), false, parent);
    }

    private void CreateStage()
    {
        Transform stage = new GameObject("Live Stage").transform;
        stage.SetParent(worldRoot, false);
        CreateBox("StageBack", new Vector3(5.4f, 2.7f, 6.3f), new Vector3(7.3f, 4.5f, 0.26f), metalMat, stage);
        CreateTextMesh("SIMBAR", new Vector3(5.4f, 4.25f, 6.08f), "SIMBAR", 0.34f, pinkGlowMat, stage);
        CreateTextMesh("LiveSign", new Vector3(5.4f, 3.35f, 6.05f), "LIVE TONIGHT", 0.17f, cyanGlowMat, stage);

        CreatePerson("Singer", new Vector3(5.05f, 0.52f, 4.35f), new Color(1f, 0.16f, 0.65f), new Color(0.12f, 0.03f, 0.14f), true, stage);
        CreatePerson("Guitarist", new Vector3(3.5f, 0.52f, 4.55f), new Color(0.12f, 0.85f, 0.78f), new Color(0.08f, 0.06f, 0.15f), true, stage);
        CreatePerson("Drummer", new Vector3(6.65f, 0.52f, 4.82f), new Color(0.94f, 0.55f, 0.20f), new Color(0.1f, 0.07f, 0.05f), true, stage);
        GameObject guitarBody = CreateSphere("Electric Guitar Body", new Vector3(3.55f, 1.25f, 3.88f), new Vector3(0.58f, 0.20f, 0.48f), pinkGlowMat, stage);
        guitarBody.transform.rotation = Quaternion.Euler(0f, 0f, -18f);
        GameObject guitarNeck = CreateBox("Electric Guitar Neck", new Vector3(3.94f, 1.52f, 3.88f), new Vector3(0.12f, 1.45f, 0.12f), woodMat, stage);
        guitarNeck.transform.rotation = Quaternion.Euler(0f, 0f, -18f);
        CreateCylinder("Kick Drum", new Vector3(6.65f, 1.0f, 4.2f), new Vector3(0.65f, 0.35f, 0.65f), pinkGlowMat, stage);
        CreateCylinder("Drum Tom", new Vector3(6.0f, 1.85f, 4.2f), new Vector3(0.35f, 0.25f, 0.35f), metalMat, stage);
        CreateCylinder("Drum Tom", new Vector3(6.9f, 1.95f, 4.2f), new Vector3(0.38f, 0.28f, 0.38f), metalMat, stage);
        GameObject cymbal = CreateCylinder("Cymbal", new Vector3(7.45f, 2.15f, 4.35f), new Vector3(0.55f, 0.035f, 0.55f), amberGlowMat, stage);
        cymbal.transform.rotation = Quaternion.Euler(0f, 0f, -8f);
        CreateBox("Mic Stand", new Vector3(5.2f, 1.2f, 3.9f), new Vector3(0.05f, 1.5f, 0.05f), metalMat, stage);
        CreateBox("Mic", new Vector3(5.2f, 2f, 3.9f), new Vector3(0.13f, 0.08f, 0.13f), cyanGlowMat, stage);
        CreateBox("Amp", new Vector3(2.55f, 0.95f, 5.32f), new Vector3(0.7f, 1.3f, 0.45f), metalMat, stage);
        CreateBox("Amp", new Vector3(8.18f, 0.95f, 5.32f), new Vector3(0.7f, 1.3f, 0.45f), metalMat, stage);
    }

    private void CreateCrowd()
    {
        Transform crowd = new GameObject("Dancing Crowd").transform;
        crowd.SetParent(worldRoot, false);
        Vector3[] positions =
        {
            new Vector3(1.0f, 0f, 0.6f), new Vector3(2.4f, 0f, 0.35f), new Vector3(3.7f, 0f, 0.85f),
            new Vector3(5.0f, 0f, 0.2f), new Vector3(6.6f, 0f, 0.7f), new Vector3(8f, 0f, 0.25f),
            new Vector3(0.2f, 0f, -1.05f), new Vector3(1.65f, 0f, -1.25f), new Vector3(3.0f, 0f, -0.9f),
            new Vector3(4.4f, 0f, -1.35f), new Vector3(5.8f, 0f, -1f), new Vector3(7.3f, 0f, -1.35f),
            new Vector3(-0.3f, 0f, -2.75f), new Vector3(1.15f, 0f, -2.6f), new Vector3(2.65f, 0f, -3.1f),
            new Vector3(4.1f, 0f, -2.75f), new Vector3(5.6f, 0f, -3.15f), new Vector3(7.25f, 0f, -2.9f),
            new Vector3(0.1f, 0f, -4.5f), new Vector3(1.7f, 0f, -4.25f), new Vector3(3.25f, 0f, -4.7f),
            new Vector3(4.8f, 0f, -4.35f), new Vector3(6.5f, 0f, -4.6f), new Vector3(7.8f, 0f, -4.3f)
        };
        Color[] outfits =
        {
            new Color(0.96f, 0.22f, 0.48f), new Color(0.11f, 0.78f, 0.96f), new Color(0.44f, 0.25f, 0.93f),
            new Color(0.94f, 0.52f, 0.15f), new Color(0.35f, 0.85f, 0.52f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Patron patron = CreatePerson(GuestNames[i % GuestNames.Length], positions[i], outfits[i % outfits.Length], new Color(0.08f, 0.04f, 0.12f), true, crowd);
            patron.phase = i * 0.71f;
            patrons.Add(patron);
        }
    }

    private void CreateDecor()
    {
        Transform decor = new GameObject("Venue Decor").transform;
        decor.SetParent(worldRoot, false);
        CreatePlant(new Vector3(-9.1f, 0f, 5.6f), decor);
        CreatePlant(new Vector3(-1.1f, 0f, 6.3f), decor);
        CreatePlant(new Vector3(9.15f, 0f, 5.65f), decor);
        for (int i = 0; i < 7; i++)
        {
            Material posterMat = i % 2 == 0 ? pinkGlowMat : cyanGlowMat;
            CreateBox("Poster", new Vector3(-8.7f + i * 1.75f, 4.75f, 7.1f), new Vector3(0.92f, 1.15f, 0.07f), posterMat, decor);
        }
    }

    private void CreatePlant(Vector3 position, Transform parent)
    {
        CreateCylinder("Planter", position + new Vector3(0f, 0.28f, 0f), new Vector3(0.35f, 0.28f, 0.35f), metalMat, parent);
        for (int i = 0; i < 5; i++)
        {
            GameObject leaf = CreatePrimitive("Leaf", PrimitiveType.Capsule, position + new Vector3(0f, 0.82f, 0f), new Vector3(0.10f, 0.6f, 0.20f), MakeMaterial(new Color(0.08f, 0.42f, 0.20f), 0f), parent);
            leaf.transform.localRotation = Quaternion.Euler(25f, i * 72f, 25f);
        }
    }

    private void CreateLights()
    {
        stageLights.AddRange(SimBarReferenceLighting.Build(worldRoot, Camera.main));
        return;
        /* Original prototype lighting retained below for layout history.
        Light key = CreateLight("Warm House Light", new Vector3(-3f, 7.0f, -1f), new Color(1f, 0.58f, 0.30f), 5f, 17f, LightType.Point);
        Light fill = CreateLight("Lounge Fill", new Vector3(-7.5f, 5.6f, 4.5f), new Color(1f, 0.23f, 0.44f), 3f, 13f, LightType.Point);
        CreateLight("Amber Bar Light", new Vector3(-5f, 6f, -1.6f), new Color(1f, 0.64f, 0.25f), 4f, 12f, LightType.Point);
        stageLights.Add(CreateLight("Cyan Stage", new Vector3(3f, 6.5f, 3.8f), new Color(0.05f, 0.8f, 1f), 3.1f, 11f, LightType.Spot));
        stageLights.Add(CreateLight("Pink Stage", new Vector3(7.7f, 6.5f, 3.8f), new Color(1f, 0.05f, 0.56f), 3.1f, 11f, LightType.Spot));
        stageLights.Add(CreateLight("Purple Stage", new Vector3(5.25f, 6.8f, 5.5f), new Color(0.55f, 0.20f, 1f), 2.7f, 11f, LightType.Spot));
        key.transform.LookAt(new Vector3(-3f, 0f, 0f));
        fill.transform.LookAt(new Vector3(-6f, 0f, 3f));
        foreach (Light stageLight in stageLights)
            stageLight.transform.LookAt(new Vector3(5.2f, 0f, 1.5f));

        Light sun = CreateLight("Soft Moonlight", new Vector3(0f, 9f, -4f), new Color(0.38f, 0.42f, 0.70f), 0.55f, 0f, LightType.Directional);
        sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        */
    }

    private Light CreateLight(string name, Vector3 position, Color color, float intensity, float range, LightType type)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(worldRoot, false);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = 48f;
        light.shadows = LightShadows.None;
        return light;
    }

    private Patron CreatePerson(string name, Vector3 position, Color outfit, Color hair, bool dancing, Transform parent, string roleOverride = null, float facing = 0f)
    {
        string role = roleOverride ?? (name == "Singer" ? "singer" : name == "Guitarist" ? "guitarist" : name == "Drummer" ? "drummer" : name == "Bartender" || name == "Server" ? "bartender" : "guest");
        GameObject imported = SimBarCharacterVisual.Create(role, parent, position, outfit, characterVariant++);
        if (imported != null)
        {
            imported.name = name + " — FBX";
            if (role == "seated") imported.GetComponent<SimBarCharacterVisual>().seatHeight = 0.52f;
            float yaw = role == "singer" || role == "guitarist" || role == "bassist" || role == "drummer" || role == "bartender" ? 180f : facing;
            yaw += UnityEngine.Random.Range(-12f, 12f);
            imported.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Patron person = new Patron { name = name, root = imported.transform, baseY = position.y, baseYaw = yaw,
                phase = UnityEngine.Random.value * Mathf.PI * 2f, isDancing = dancing };
            if (dancing)
            {
                person.orderMarker = CreateCylinder("OrderMarker", new Vector3(0f, 2.2f, 0f), new Vector3(0.12f, 0.035f, 0.12f), amberGlowMat, person.root);
                person.orderMarker.SetActive(false);
            }
            return person;
        }
        Transform root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = position;
        root.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-25f, 25f), 0f);
        Material skin = MakeMaterial(new Color(0.95f, 0.65f, 0.47f), 0f);
        Material outfitMat = MakeMaterial(outfit, 0.05f);
        Material hairMat = MakeMaterial(hair, 0f);

        CreateCapsule("Body", new Vector3(0f, 0.92f, 0f), new Vector3(0.34f, 0.60f, 0.28f), outfitMat, root);
        CreateSphere("Head", new Vector3(0f, 1.70f, 0f), new Vector3(0.43f, 0.43f, 0.43f), skin, root);
        CreateSphere("Hair", new Vector3(0f, 1.92f, -0.03f), new Vector3(0.45f, 0.22f, 0.44f), hairMat, root);
        CreateCapsule("Leg L", new Vector3(-0.14f, 0.30f, 0f), new Vector3(0.13f, 0.34f, 0.13f), metalMat, root);
        CreateCapsule("Leg R", new Vector3(0.14f, 0.30f, 0f), new Vector3(0.13f, 0.34f, 0.13f), metalMat, root);
        Transform leftArm = CreateCapsule("Arm L", new Vector3(-0.39f, 1.12f, 0f), new Vector3(0.10f, 0.38f, 0.10f), skin, root).transform;
        Transform rightArm = CreateCapsule("Arm R", new Vector3(0.39f, 1.12f, 0f), new Vector3(0.10f, 0.38f, 0.10f), skin, root).transform;
        leftArm.localRotation = Quaternion.Euler(0f, 0f, -22f);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, 22f);

        Patron patron = new Patron
        {
            name = name,
            root = root,
            leftArm = leftArm,
            rightArm = rightArm,
            phase = UnityEngine.Random.value * Mathf.PI * 2f,
            baseY = position.y,
            baseYaw = root.localEulerAngles.y,
            isDancing = dancing
        };
        if (dancing)
        {
            GameObject marker = CreateCylinder("OrderMarker", new Vector3(0f, 2.45f, 0f), new Vector3(0.14f, 0.04f, 0.14f), amberGlowMat, root);
            marker.SetActive(false);
            patron.orderMarker = marker;
        }
        return patron;
    }

    private void BuildInterface()
    {
        EnsureEventSystem();
        GameObject canvasObject = new GameObject("SimBar Live HUD");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        CreateTopBar();
        CreatePlaylistPanel();
        CreateOrderPanel();
        CreateSidePanels();
        CreatePausePanel();
        SimBarHudSkin.Apply(canvas);
        SimBarMiniMap.Attach(canvas, worldRoot);
    }

    private void EnsureEventSystem()
    {
        EventSystem existing = FindAnyObjectByType<EventSystem>();
        if (existing == null)
        {
            GameObject eventObject = new GameObject("EventSystem");
            existing = eventObject.AddComponent<EventSystem>();
        }
        StandaloneInputModule legacyModule = existing.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            legacyModule.enabled = false;
            Destroy(legacyModule);
        }
        if (existing.GetComponent<InputSystemUIInputModule>() == null)
            existing.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private void CreateTopBar()
    {
        Button gear = CreateButton("⚙", canvas.transform, new Vector2(0f, 1f), new Vector2(56f, -56f), new Vector2(74f, 74f), new Color(0.08f, 0.10f, 0.17f, 0.94f), 38);
        gear.onClick.AddListener(OpenLanguageSettings);

        cashText = CreateStatCard("CASH", new Vector2(-510f, -62f), new Color(0.12f, 0.16f, 0.20f, 0.96f));
        fanText = CreateStatCard("FANS", new Vector2(-305f, -62f), new Color(0.16f, 0.10f, 0.22f, 0.96f));
        timeText = CreateStatCard("TIME", new Vector2(-105f, -62f), new Color(0.14f, 0.12f, 0.20f, 0.96f));

        GameObject toast = CreatePanel("Toast", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(460f, 48f), new Color(0.04f, 0.06f, 0.12f, 0.88f));
        toastText = CreateText("ToastText", toast.transform, "", 18, TextAnchor.MiddleCenter, new Color(0.76f, 0.95f, 1f));
        SetRect(toastText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 40f));
    }

    private Text CreateStatCard(string label, Vector2 position, Color color)
    {
        GameObject panel = CreatePanel(label + " Card", canvas.transform, new Vector2(1f, 1f), position, new Vector2(184f, 74f), color);
        Text title = CreateText(label, panel.transform, label, 16, TextAnchor.UpperCenter, new Color(0.78f, 0.84f, 0.96f));
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(164f, 25f));
        Text value = CreateText(label + " Value", panel.transform, "", 25, TextAnchor.LowerCenter, Color.white);
        SetRect(value.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(164f, 42f));
        return value;
    }

    private void CreatePlaylistPanel()
    {
        GameObject outer = CreatePanel("Playlist Border", canvas.transform, new Vector2(1f, 0.5f), new Vector2(-184f, 84f), new Vector2(340f, 340f), new Color(0.12f, 0.52f, 0.72f, 0.75f));
        GameObject panel = CreatePanel("Playlist", outer.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 330f), new Color(0.035f, 0.045f, 0.085f, 0.96f));
        Text title = CreateText("Playlist Title", panel.transform, "♪  PLAYLIST", 25, TextAnchor.MiddleCenter, Color.white);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -27f), new Vector2(300f, 34f));
        playlistNowText = CreateText("Now Playing", panel.transform, "NOW PLAYING: ELECTRIC HEARTS", 12, TextAnchor.MiddleCenter, new Color(0.30f, 1f, 0.74f));
        SetRect(playlistNowText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -57f), new Vector2(300f, 22f));
        for (int i = 0; i < Playlist.Length; i++)
        {
            int index = i;
            Button row = CreateButton(Playlist[i], panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -90f - i * 34f), new Vector2(294f, 29f), new Color(0.10f, 0.12f, 0.19f, 0.88f), 14);
            row.GetComponent<Image>().color = i == 0 ? new Color(0.06f, 0.36f, 0.30f, 0.95f) : new Color(0.10f, 0.12f, 0.19f, 0.88f);
            playlistRows.Add(row.GetComponent<Image>());
            row.onClick.AddListener(() => SelectPlaylist(index));
        }
    }

    private void CreateOrderPanel()
    {
        GameObject border = CreatePanel("Order Border", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 154f), new Vector2(730f, 190f), new Color(0.95f, 0.22f, 0.62f, 0.75f));
        GameObject panel = CreatePanel("Order Console", border.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 180f), new Color(0.035f, 0.045f, 0.09f, 0.96f));
        Text header = CreateText("Order Header", panel.transform, "BAR SERVICE  •  LIVE QUEUE", 20, TextAnchor.MiddleCenter, new Color(1f, 0.80f, 0.90f));
        SetRect(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(670f, 28f));
        orderText = CreateText("Current Order", panel.transform, "", 26, TextAnchor.MiddleCenter, Color.white);
        SetRect(orderText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(670f, 35f));
        queueText = CreateText("Queue", panel.transform, "", 13, TextAnchor.MiddleCenter, new Color(0.65f, 0.78f, 0.94f));
        SetRect(queueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(670f, 22f));

        for (int i = 0; i < DrinkNames.Length; i++)
        {
            int index = i;
            Button drink = CreateButton((i + 1) + "  " + EnglishDrinkNames[i] + "  $" + DrinkPrices[i], panel.transform, new Vector2(0.5f, 0f), new Vector2(-230f + 230f * i, 37f), new Vector2(212f, 48f), DrinkColors[i] * 0.62f + new Color(0.08f, 0.08f, 0.10f), 14);
            drinkButtonLabels[i] = drink.GetComponentInChildren<Text>();
            drink.onClick.AddListener(() => RequestPlayerPour(index));
        }
        Text help = CreateText("Service Help", panel.transform, "1 / 2 / 3 choose  •  SPACE / STOP pour  •  E choose requested drink  •  P pause", 12, TextAnchor.MiddleCenter, new Color(0.70f, 0.76f, 0.88f));
        SetRect(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(680f, 20f));
    }

    private void CreateSidePanels()
    {
        GameObject left = CreatePanel("Venue Panel", canvas.transform, new Vector2(0f, 0f), new Vector2(122f, 132f), new Vector2(224f, 202f), new Color(0.035f, 0.045f, 0.09f, 0.95f));
        Text leftHeader = CreateText("Venue Label", left.transform, "SIMBAR  /  NIGHT 35", 16, TextAnchor.MiddleCenter, Color.white);
        SetRect(leftHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(202f, 26f));
        GameObject map = CreatePanel("Mini Map", left.transform, new Vector2(0.5f, 0.53f), Vector2.zero, new Vector2(184f, 105f), new Color(0.10f, 0.13f, 0.22f, 0.98f));
        CreatePanel("Bar Dot", map.transform, new Vector2(0.30f, 0.54f), Vector2.zero, new Vector2(40f, 20f), new Color(1f, 0.52f, 0.16f, 0.9f));
        CreatePanel("Stage Dot", map.transform, new Vector2(0.75f, 0.72f), Vector2.zero, new Vector2(48f, 29f), new Color(1f, 0.12f, 0.60f, 0.9f));
        CreatePanel("Crowd Dot", map.transform, new Vector2(0.58f, 0.40f), Vector2.zero, new Vector2(58f, 32f), new Color(0.10f, 0.80f, 1f, 0.75f));
        Text mapCaption = CreateText("Map Caption", left.transform, "STAFF ON DUTY  2  •  CROWD ON FIRE", 11, TextAnchor.MiddleCenter, new Color(0.46f, 0.96f, 0.73f));
        SetRect(mapCaption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(204f, 22f));

        GameObject right = CreatePanel("Mood Panel", canvas.transform, new Vector2(1f, 0f), new Vector2(-165f, 136f), new Vector2(310f, 160f), new Color(0.035f, 0.045f, 0.09f, 0.95f));
        CreateMeter(right.transform, "GUEST SATISFACTION", new Vector2(0.5f, 0.70f), new Color(1f, 0.57f, 0.18f), out satisfactionFill);
        CreateMeter(right.transform, "PERFORMANCE ENERGY", new Vector2(0.5f, 0.32f), new Color(0.16f, 0.82f, 1f), out performanceFill);
    }

    private void CreateMeter(Transform parent, string label, Vector2 anchor, Color color, out Image fill)
    {
        Text title = CreateText(label, parent, label, 14, TextAnchor.MiddleLeft, Color.white);
        SetRect(title.rectTransform, anchor, new Vector2(-126f, 18f), new Vector2(252f, 22f));
        GameObject background = CreatePanel(label + " Back", parent, anchor, new Vector2(0f, -12f), new Vector2(252f, 18f), new Color(0.15f, 0.17f, 0.25f, 1f));
        GameObject foreground = CreatePanel(label + " Fill", background.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(252f, 12f), color);
        RectTransform rect = foreground.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 0.5f);
        fill = foreground.GetComponent<Image>();
    }

    private void CreatePausePanel()
    {
        pausePanel = CreatePanel("Pause Panel", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430f, 200f), new Color(0.025f, 0.035f, 0.075f, 0.96f));
        Text title = CreateText("Pause Title", pausePanel.transform, "SERVICE PAUSED", 32, TextAnchor.MiddleCenter, Color.white);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(390f, 50f));
        pauseText = CreateText("Pause Text", pausePanel.transform, "Tap the gear or press P to return to the bar.", 17, TextAnchor.MiddleCenter, new Color(0.65f, 0.82f, 1f));
        SetRect(pauseText.rectTransform, new Vector2(0.5f, 0.46f), Vector2.zero, new Vector2(380f, 45f));
        Button resume = CreateButton("RETURN TO THE FLOOR", pausePanel.transform, new Vector2(0.5f, 0.20f), Vector2.zero, new Vector2(250f, 44f), new Color(0.12f, 0.55f, 0.46f, 1f), 16);
        resume.onClick.AddListener(TogglePause);
        pausePanel.SetActive(false);
    }

    private void SeedOrders()
    {
        AssignOrder(patrons[1], 0);
        AssignOrder(patrons[4], 1);
        AssignOrder(patrons[8], 2);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.money = cash;
            GameManager.Instance.reputation = fans;
            GameManager.Instance.isOpen = true;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) RequestPlayerPour(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) RequestPlayerPour(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) RequestPlayerPour(2);
            if (Keyboard.current.eKey.wasPressedThisFrame && orderQueue.Count > 0) RequestPlayerPour(orderQueue.Peek().drinkIndex);
            if (Keyboard.current.pKey.wasPressedThisFrame) TogglePause();
        }

        if (paused)
        {
            TickPourGame(0f);
            return;
        }

        float delta = Time.unscaledDeltaTime;
        TickJourney(delta);
        if (paused || !JourneyIsPlaying)
            return;
        TickPourGame(delta);
        TickOperations(delta);
        beat += delta * (2.2f + performance * 1.5f);
        gameClock += delta * 0.011f;
        if (gameClock >= 24f)
            gameClock = 18f;
        requestTimer -= delta;
        if (requestTimer <= 0f)
        {
            TryCreateOrder();
            requestTimer = UnityEngine.Random.Range(4.5f, 7.5f) * OperationsOrderIntervalMultiplier * JourneyOrderIntervalMultiplier;
        }

        AnimateVenue();
        UpdatePatience(delta);
        performance = Mathf.Clamp01(performance - delta * 0.0015f);
        if (toastTimer > 0f)
            toastTimer -= delta;
        else if (toastText != null)
            toastText.transform.parent.gameObject.SetActive(false);
        RefreshUI();
    }

    private void AnimateVenue()
    {
        for (int i = 0; i < patrons.Count; i++)
        {
            Patron patron = patrons[i];
            if (patron.root == null)
                continue;
            float wave = Mathf.Sin(beat + patron.phase);
            patron.root.localPosition = new Vector3(patron.root.localPosition.x, patron.baseY + (patron.isDancing ? wave * 0.075f : 0f), patron.root.localPosition.z);
            if (patron.isDancing)
            {
                patron.root.localRotation = Quaternion.Euler(0f, patron.baseYaw + Mathf.Sin(beat * 0.55f + patron.phase) * 10f, Mathf.Sin(beat + patron.phase) * 2.5f);
                if (patron.leftArm != null) patron.leftArm.localRotation = Quaternion.Euler(0f, 0f, -25f - wave * 56f);
                if (patron.rightArm != null) patron.rightArm.localRotation = Quaternion.Euler(0f, 0f, 25f + wave * 56f);
                if (patron.orderMarker != null && patron.orderMarker.activeSelf)
                    patron.orderMarker.transform.localPosition = new Vector3(0f, 2.48f + Mathf.Abs(wave) * 0.18f, 0f);
            }
        }
        for (int i = 0; i < stageLights.Count; i++)
        {
            Light stageLight = stageLights[i];
            if (stageLight != null)
                stageLight.intensity = 8f + Mathf.Abs(Mathf.Sin(beat + i * 1.9f)) * 8f * performance;
        }
    }

    private void UpdatePatience(float delta)
    {
        if (orderQueue.Count == 0)
            return;
        Patron[] queued = orderQueue.ToArray();
        foreach (Patron patron in queued)
            if (!IsPourInProgress || patron != orderQueue.Peek())
                patron.patience -= delta;

        Patron front = orderQueue.Peek();
        if (front.patience <= 0f && !IsPourInProgress)
        {
            orderQueue.Dequeue();
            ClearOrder(front);
            satisfaction = Mathf.Max(0.35f, satisfaction - 0.07f);
            performance = Mathf.Max(0.35f, performance - 0.04f);
            fans = Mathf.Max(0, fans - 3);
            JourneyRecordMistake();
            ShowToast(SimBarLocalization.Format("{0} left thirsty — fans noticed.", "{0} 손님이 음료를 받지 못하고 떠났어요. 팬이 줄었습니다.", front.name.ToUpperInvariant()), 3.5f);
        }
    }

    private void TryCreateOrder()
    {
        if (orderQueue.Count >= 4 + OperationsQueueBonus || patrons.Count == 0)
            return;
        for (int attempt = 0; attempt < patrons.Count; attempt++)
        {
            Patron candidate = patrons[UnityEngine.Random.Range(0, patrons.Count)];
            if (candidate.drinkIndex >= 0)
                continue;
            AssignOrder(candidate, UnityEngine.Random.Range(0, DrinkNames.Length));
            ShowToast(SimBarLocalization.Format("{0} raised a hand at the bar.", "{0} 손님이 음료를 주문했어요.", candidate.name.ToUpperInvariant()), 2.6f);
            return;
        }
    }

    private void AssignOrder(Patron patron, int drinkIndex)
    {
        patron.drinkIndex = drinkIndex;
        patron.patience = UnityEngine.Random.Range(52f, 68f) + OperationsPatienceBonus + JourneyPatienceBonus;
        if (patron.orderMarker != null)
        {
            patron.orderMarker.GetComponent<Renderer>().sharedMaterial = orderMarkerMaterials[drinkIndex];
            patron.orderMarker.SetActive(true);
        }
        orderQueue.Enqueue(patron);
    }

    private void ClearOrder(Patron patron)
    {
        patron.drinkIndex = -1;
        if (patron.orderMarker != null)
            patron.orderMarker.SetActive(false);
    }

    private void ServeDrink(int drinkIndex)
    {
        ResolveDrinkService(drinkIndex, true, 0.7f);
    }

    private void CommitPlayerPour(int drinkIndex, float quality)
    {
        ResolveDrinkService(drinkIndex, false, OpFinite(quality, 0f, 0f, 1f));
    }

    private void ResolveDrinkService(int drinkIndex, bool automatic, float quality)
    {
        if (paused || !JourneyIsPlaying || drinkIndex < 0 || drinkIndex >= DrinkNames.Length)
            return;
        if (orderQueue.Count == 0)
        {
            ShowToast(SimBarLocalization.Text("No active orders — enjoy the set!", "대기 중인 주문이 없어요. 공연을 즐겨 보세요!"), 2f);
            return;
        }

        Patron patron = orderQueue.Peek();
        if (patron.drinkIndex != drinkIndex)
        {
            satisfaction = Mathf.Max(0.30f, satisfaction - 0.025f);
            JourneyRecordMistake();
            ShowToast(SimBarLocalization.Format("That is not {0}'S ORDER.", "{0} 손님의 주문과 다른 음료예요.", patron.name.ToUpperInvariant()), 2.3f);
            return;
        }

        orderQueue.Dequeue();
        float qualityTip = automatic ? 1f : quality >= 0.9f ? 1.4f : quality >= 0.5f ? 1f : 0f;
        float tip = Mathf.RoundToInt(DrinkPrices[drinkIndex] * Mathf.Clamp01(patron.patience / 30f) * 0.45f * OperationsTipMultiplier * JourneyTipMultiplier * qualityTip);
        float sale = Mathf.RoundToInt(DrinkPrices[drinkIndex] * OperationsRevenueMultiplier);
        cash += sale + tip;
        fans += automatic || quality >= 0.5f ? 1 + (!automatic && quality >= 0.9f ? 1 : 0) : 0;
        servedTonight++;
        satisfaction = Mathf.Clamp(satisfaction + (automatic ? 0.012f : quality >= 0.5f ? 0.01f + quality * 0.015f : -0.025f), 0.3f, 1f);
        performance = Mathf.Clamp01(performance + (automatic || quality >= 0.5f ? 0.014f : -0.008f));
        ClearOrder(patron);
        TryCreateOrder();
        string verdict = automatic ? SimBarLocalization.Text("BARBACK SERVED", "바 보조가 서빙했어요")
            : quality >= 0.9f ? SimBarLocalization.Text("PERFECT POUR!", "완벽하게 따랐어요!")
            : quality >= 0.5f ? SimBarLocalization.Text("GOOD POUR!", "잘 따랐어요!")
            : SimBarLocalization.Text("SPILLED — NO TIP", "음료를 흘렸어요 — 팁 없음");
        ShowToast(verdict + "  +$" + (sale + tip), 2.8f);
        JourneyRecordServe(automatic, quality, sale + tip);
        RefreshUI();
        SaveOperations();
    }

    private void SelectPlaylist(int index)
    {
        playlistIndex = index;
        performance = Mathf.Min(1f, performance + 0.08f);
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = CreateProceduralTrack(index);
            musicSource.Play();
        }
        for (int i = 0; i < playlistRows.Count; i++)
            playlistRows[i].color = i == index ? new Color(0.06f, 0.36f, 0.30f, 0.96f) : new Color(0.10f, 0.12f, 0.19f, 0.88f);
        ShowToast(SimBarLocalization.Text("NOW PLAYING: ", "재생 중: ") + Playlist[index].Substring(3).ToUpperInvariant(), 2.4f);
        RefreshUI();
    }

    private void TogglePause()
    {
        if (settingsOverlay != null && settingsOverlay.activeSelf)
        {
            CloseLanguageSettings();
            return;
        }
        if (TryHandleJourneyPause())
            return;
        if (TryHandleOperationsPause())
            return;
        paused = !paused;
        if (pausePanel != null)
            pausePanel.SetActive(paused);
        if (!paused)
            ShowToast(SimBarLocalization.Text("BACK TO THE FLOOR — THE BAND IS WAITING.", "영업을 재개합니다. 밴드가 기다리고 있어요!"), 2.2f);
    }

    private void RefreshUI()
    {
        if (cashText == null)
            return;
        cashText.text = "$" + cash.ToString("N0");
        fanText.text = fans.ToString("N0");
        int hour = Mathf.FloorToInt(gameClock);
        int minute = Mathf.FloorToInt((gameClock - hour) * 60f);
        timeText.text = hour.ToString("00") + ":" + minute.ToString("00");
        if (playlistNowText != null)
            playlistNowText.text = SimBarLocalization.Text("NOW PLAYING: ", "재생 중: ") + Playlist[playlistIndex].Substring(3).ToUpperInvariant();

        if (orderQueue.Count == 0)
        {
            orderText.text = SimBarLocalization.Text("NO OPEN TABS — THE FLOOR IS YOURS", "대기 중인 주문이 없습니다");
            queueText.text = SimBarLocalization.Text("Keep the playlist hot to draw new guests.", "음악을 바꿔 새로운 손님을 맞이하세요.");
        }
        else
        {
            Patron patron = orderQueue.Peek();
            orderText.text = SimBarLocalization.Format("{0} WANTS {1}", "{0} 주문: {1}", patron.name.ToUpperInvariant(), DrinkNames[patron.drinkIndex]);
            queueText.text = SimBarLocalization.Format("QUEUE {0}  •  PATIENCE {1}s  •  TONIGHT'S POURS {2}", "대기 {0}명  •  남은 대기 시간 {1}초  •  오늘 서빙 {2}잔", orderQueue.Count, Mathf.CeilToInt(Mathf.Max(0f, patron.patience)), servedTonight);
        }
        SetMeter(satisfactionFill, satisfaction);
        SetMeter(performanceFill, performance);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.money = cash;
            GameManager.Instance.reputation = fans;
        }
        RefreshOperationsHUD();
        RefreshJourneyHUD();
    }

    private void SetMeter(Image fill, float value)
    {
        if (fill == null)
            return;
        RectTransform rect = fill.rectTransform;
        rect.sizeDelta = new Vector2(246f * Mathf.Clamp01(value), 12f);
    }

    private void ShowToast(string message, float duration)
    {
        if (toastText != null)
        {
            toastText.transform.parent.gameObject.SetActive(true);
            toastText.text = message;
        }
        toastTimer = duration;
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        SetRect(panel.GetComponent<RectTransform>(), anchor, position, size);
        return panel;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = SimBarLocalization.Font;
        SimBarLocalization.Bind(text, content);
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, int fontSize)
    {
        GameObject buttonObject = CreatePanel(label + " Button", parent, anchor, position, size, color);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        Text text = CreateText("Label", buttonObject.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white);
        SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(10f, 5f));
        return button;
    }

    private void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material, Transform parent)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;
        Renderer renderer = primitive.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return primitive;
    }

    private GameObject CreateBox(string name, Vector3 localPosition, Vector3 localScale, Material material, Transform parent)
    {
        return CreatePrimitive(name, PrimitiveType.Cube, localPosition, localScale, material, parent);
    }

    private GameObject CreateSphere(string name, Vector3 localPosition, Vector3 localScale, Material material, Transform parent)
    {
        return CreatePrimitive(name, PrimitiveType.Sphere, localPosition, localScale, material, parent);
    }

    private GameObject CreateCylinder(string name, Vector3 localPosition, Vector3 localScale, Material material, Transform parent)
    {
        return CreatePrimitive(name, PrimitiveType.Cylinder, localPosition, localScale, material, parent);
    }

    private GameObject CreateCapsule(string name, Vector3 localPosition, Vector3 localScale, Material material, Transform parent)
    {
        return CreatePrimitive(name, PrimitiveType.Capsule, localPosition, localScale, material, parent);
    }

    private void CreateTextMesh(string name, Vector3 position, string content, float size, Material material, Transform parent)
    {
        GameObject sign = new GameObject(name, typeof(TextMesh));
        sign.transform.SetParent(parent, false);
        sign.transform.position = position;
        sign.transform.rotation = Quaternion.LookRotation(Camera.main.transform.position - position, Vector3.up);
        sign.transform.localScale = new Vector3(-1f, 1f, 1f);
        TextMesh text = sign.GetComponent<TextMesh>();
        text.text = content;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = material.color;
    }
}
