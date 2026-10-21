using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public Text moneyText;
    public Text reputationText;
    public Text orderCountText;
    public Text timeText;
    public GameObject orderPanel;
    public GameObject pauseMenu;
    public GameObject orderNotificationPanel;
    public Text orderNotificationContent;

    public Sprite panelBackgroundSprite;
    public Sprite buttonBackgroundSprite;
    public Sprite sliderBackgroundSprite;
    public Sprite sliderFillSprite;
    public Sprite prevButtonSprite;
    public Sprite nextButtonSprite;
    public Sprite playButtonSprite;

    private Canvas canvas;
    private GameObject musicPlayerPanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        LoadKenneySprites();
        CreateCanvasIfMissing();
        // The screenshot-style UI is created by BarUIBuilder and the generic HUD is intentionally disabled to avoid duplicate overlays.
        CreateOrderNotificationIfMissing();
    }

    private void LoadKenneySprites()
    {
        if (panelBackgroundSprite == null)
            panelBackgroundSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/button_rectangle_depth_flat");
        if (buttonBackgroundSprite == null)
            buttonBackgroundSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/button_rectangle_flat");
        if (sliderBackgroundSprite == null)
            sliderBackgroundSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/slide_horizontal_grey");
        if (sliderFillSprite == null)
            sliderFillSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/slide_horizontal_color_section");
        if (prevButtonSprite == null)
            prevButtonSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/arrow_basic_w");
        if (nextButtonSprite == null)
            nextButtonSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/arrow_basic_e");
        if (playButtonSprite == null)
            playButtonSprite = Resources.Load<Sprite>("UI/Kenney/Yellow/Default/icon_circle");
    }

    private void Start()
    {
        if (orderNotificationPanel != null)
        {
            orderNotificationPanel.SetActive(false);
        }

        if (musicPlayerPanel != null)
        {
            musicPlayerPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null)
        {
            if (moneyText != null)
                moneyText.text = $"$ {GameManager.Instance.money:F0}";
            if (reputationText != null)
                reputationText.text = $"Rep: {GameManager.Instance.reputation}";
            if (orderCountText != null)
                orderCountText.text = $"Orders: {GameManager.Instance.activeOrders.Count}";
        }
    }

    public void TogglePause()
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(!pauseMenu.activeSelf);
        }
        Time.timeScale = Time.timeScale > 0 ? 0 : 1;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ShowOrderNotification(Order order)
    {
        if (orderNotificationPanel == null || orderNotificationContent == null) return;
        orderNotificationContent.text = $"{order.customer.customerName}\nwants: {order.drink.displayName}";
        orderNotificationPanel.SetActive(true);
        CancelInvoke(nameof(HideOrderNotification));
        Invoke(nameof(HideOrderNotification), 4f);
    }

    private void HideOrderNotification()
    {
        if (orderNotificationPanel != null)
        {
            orderNotificationPanel.SetActive(false);
        }
    }

    private void CreateCanvasIfMissing()
    {
        canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("SimBar_Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 1024f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
        }
    }

    private void CreateHUDIfMissing()
    {
        if (moneyText != null && reputationText != null && orderCountText != null) return;

        GameObject hud = new GameObject("HUD");
        hud.transform.SetParent(canvas.transform, false);
        RectTransform rt = hud.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(10f, -100f);
        rt.offsetMax = new Vector2(-10f, -10f);

        // Add subtle background panel for HUD
        Image hudBg = hud.AddComponent<Image>();
        hudBg.color = new Color(0.18f, 0.16f, 0.22f, 0.7f);

        moneyText = CreateText("MoneyText", hud.transform, "$ 0", new Vector2(0f, 1f), new Vector2(0.3f, 0.4f)).GetComponent<Text>();
        reputationText = CreateText("RepText", hud.transform, "Rep: 0", new Vector2(0.35f, 1f), new Vector2(0.3f, 0.4f)).GetComponent<Text>();
        orderCountText = CreateText("OrdersText", hud.transform, "Orders: 0", new Vector2(0.7f, 1f), new Vector2(0.3f, 0.4f)).GetComponent<Text>();
    }

    private void CreateOrderNotificationIfMissing()
    {
        if (orderNotificationPanel != null) return;

        GameObject panel = new GameObject("OrderNotificationPanel");
        panel.transform.SetParent(canvas.transform, false);
        panel.SetActive(false);

        Image bg = panel.AddComponent<Image>();
        if (panelBackgroundSprite != null)
        {
            bg.sprite = panelBackgroundSprite;
            bg.type = Image.Type.Sliced;
        }
        else
        {
            bg.color = new Color(0.22f, 0.18f, 0.26f, 0.95f);
        }

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(300f, 150f);

        GameObject title = CreateText("Title", panel.transform, "New Order!", new Vector2(0.5f, 1f), new Vector2(1f, 0.3f));
        title.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);

        GameObject content = CreateText("Content", panel.transform, "Customer wants: ?", new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.6f));
        content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
        content.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        content.GetComponent<Text>().fontSize = 20;

        orderNotificationPanel = panel;
        orderNotificationContent = content.GetComponent<Text>();
    }

    private void CreateMusicPlayerIfMissing()
    {
        if (musicPlayerPanel != null) return;

        GameObject panel = BuildMusicPanel();
        BuildProgressSlider(panel.transform);
        BuildPlayerControls(panel.transform);

        musicPlayerPanel = panel;
    }

    private GameObject BuildMusicPanel()
    {
        GameObject panel = new GameObject("MusicPlayerPanel");
        panel.transform.SetParent(canvas.transform, false);

        // Compact widget-style panel (like Lofi Cozy Room)
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.16f, 0.22f, 0.9f);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(340f, 64f);

        // Song title - compact
        GameObject songInfo = CreateText("SongInfo", panel.transform, "No Music Playing", new Vector2(0f, 0.5f), new Vector2(0.55f, 0.45f));
        RectTransform infoRT = songInfo.GetComponent<RectTransform>();
        infoRT.offsetMin = new Vector2(8f, 4f);
        infoRT.offsetMax = new Vector2(-8f, -4f);

        return panel;
    }

    private Slider BuildProgressSlider(Transform parent)
    {
        GameObject progress = new GameObject("ProgressBar");
        progress.transform.SetParent(parent, false);
        Slider slider = progress.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.handleRect = null;
        slider.transition = Selectable.Transition.ColorTint;
        slider.colors = new ColorBlock
        {
            normalColor = new Color(0.28f, 0.24f, 0.32f, 0.9f),
            highlightedColor = new Color(0.35f, 0.30f, 0.40f, 0.9f),
            pressedColor = new Color(0.20f, 0.18f, 0.25f, 0.9f),
            disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f),
            colorMultiplier = 1f
        };
        RectTransform progRT = progress.GetComponent<RectTransform>();
        progRT.anchorMin = new Vector2(0.6f, 0.15f);
        progRT.anchorMax = new Vector2(0.95f, 0.4f);
        progRT.pivot = new Vector2(0.5f, 0.5f);
        progRT.offsetMin = new Vector2(0f, 0f);
        progRT.offsetMax = new Vector2(0f, 0f);

        GameObject bgSlider = new GameObject("Background");
        bgSlider.transform.SetParent(progress.transform, false);
        Image bgImg = bgSlider.AddComponent<Image>();
        bgImg.color = new Color(0.28f, 0.24f, 0.32f, 0.9f);
        RectTransform bgRT = bgSlider.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(progress.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.95f, 0.80f, 0.55f, 0.9f); // Warm amber
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        slider.fillRect = fillRT;

        return slider;
    }

    private void BuildPlayerControls(Transform parent)
    {
        GameObject controls = new GameObject("Controls");
        controls.transform.SetParent(parent, false);
        RectTransform ctrlRT = controls.AddComponent<RectTransform>();
        ctrlRT.anchorMin = new Vector2(0.05f, 0.15f);
        ctrlRT.anchorMax = new Vector2(0.55f, 0.85f);
        ctrlRT.pivot = new Vector2(0.5f, 0.5f);
        ctrlRT.offsetMin = new Vector2(0f, 0f);
        ctrlRT.offsetMax = new Vector2(0f, 0f);

        // Small cozy buttons
        CreateCozyButton("Prev", controls.transform, new Vector2(0.2f, 0.5f), new Vector2(28f, 28f));
        CreateCozyButton("Play", controls.transform, new Vector2(0.5f, 0.5f), new Vector2(32f, 32f));
        CreateCozyButton("Next", controls.transform, new Vector2(0.8f, 0.5f), new Vector2(28f, 28f));
    }

    private GameObject CreateCozyButton(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        return CreateButtonInternal(
            name,
            parent,
            anchor,
            size,
            null,
            12,
            new Color(0.35f, 0.28f, 0.42f, 0.95f),
            new Color(0.50f, 0.40f, 0.58f, 0.95f),
            new Color(0.25f, 0.20f, 0.30f, 0.95f),
            new Color(1f, 0.96f, 0.88f, 1f));
    }

    private GameObject CreateText(string name, Transform parent, string text, Vector2 anchor, Vector2 size)
    {
        GameObject txtGO = new GameObject(name);
        txtGO.transform.SetParent(parent, false);
        Text txt = txtGO.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.color = new Color(1f, 0.95f, 0.85f);
        txt.alignment = TextAnchor.MiddleLeft;

        RectTransform rt = txtGO.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor + size;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(10f, 0f);
        rt.offsetMax = new Vector2(-10f, 0f);

        return txtGO;
    }

    private GameObject CreateButton(string name, Transform parent, Vector2 anchor, Vector2 size, Sprite iconSprite = null)
    {
        return CreateButtonInternal(
            name,
            parent,
            anchor,
            size,
            iconSprite,
            16,
            new Color(0.28f, 0.22f, 0.32f, 0.95f),
            new Color(0.36f, 0.30f, 0.42f, 0.95f),
            new Color(0.20f, 0.16f, 0.24f, 0.95f),
            new Color(1f, 0.95f, 0.85f));
    }

    private GameObject CreateButtonInternal(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 size,
        Sprite iconSprite,
        int fontSize,
        Color normalColor,
        Color highlightedColor,
        Color pressedColor,
        Color textColor)
    {
        GameObject btn = new GameObject(name);
        btn.transform.SetParent(parent, false);
        Button button = btn.AddComponent<Button>();
        Image img = btn.AddComponent<Image>();
        if (iconSprite != null)
        {
            img.sprite = iconSprite;
            img.type = Image.Type.Sliced;
        }
        else if (buttonBackgroundSprite != null)
        {
            img.sprite = buttonBackgroundSprite;
            img.type = Image.Type.Sliced;
            img.color = normalColor;
        }
        else
        {
            img.color = normalColor;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = pressedColor;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = anchor - new Vector2(0.5f, 0.5f);
        rt.anchorMax = anchor + new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-size.x / 2f, -size.y / 2f);
        rt.offsetMax = new Vector2(size.x / 2f, size.y / 2f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btn.transform, false);
        Text txt = textGO.AddComponent<Text>();
        txt.text = name;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = textColor;
        txt.alignment = TextAnchor.MiddleCenter;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return btn;
    }
}