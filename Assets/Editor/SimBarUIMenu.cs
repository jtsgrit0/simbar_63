using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

public static class SimBarUIMenu
{
    private static string KenneyUIPath => "Assets/Resources/UI/Kenney/Yellow/Default";

    private static Sprite LoadSprite(string fileName)
    {
        string path = Path.Combine(KenneyUIPath, fileName + ".png");
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("GameObject/SimBar UI/Create Main Canvas", false, 10)]
    private static void CreateMainCanvas(MenuCommand menuCommand)
    {
        GameObject canvasGO = new GameObject("SimBar_Canvas");
        GameObjectUtility.SetParentAndAlign(canvasGO, menuCommand.context as GameObject);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 1024f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        Selection.activeGameObject = canvasGO;
    }

    [MenuItem("GameObject/SimBar UI/Create Music Player Panel", false, 11)]
    private static void CreateMusicPlayer(MenuCommand menuCommand)
    {
        if (Selection.activeGameObject == null || Selection.activeGameObject.GetComponent<Canvas>() == null)
        {
            Debug.LogWarning("Please select a Canvas first.");
            return;
        }

        Canvas canvas = Selection.activeGameObject.GetComponent<Canvas>();
        Sprite panelSprite = LoadSprite("button_rectangle_depth_flat");
        Sprite sliderBg = LoadSprite("slide_horizontal_grey");
        Sprite sliderFill = LoadSprite("slide_horizontal_color_section");
        Sprite arrowW = LoadSprite("arrow_basic_w");
        Sprite arrowE = LoadSprite("arrow_basic_e");
        Sprite iconCircle = LoadSprite("icon_circle");

        GameObject panel = new GameObject("MusicPlayerPanel");
        panel.transform.SetParent(canvas.transform, false);

        Image bg = panel.AddComponent<Image>();
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
        }
        else
        {
            bg.color = new Color(0.22f, 0.18f, 0.26f, 0.95f);
        }

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 10f);
        rt.sizeDelta = new Vector2(400f, 80f);

        GameObject songInfo = CreateText("SongInfo", panel.transform, "No Music Playing", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
        songInfo.GetComponent<RectTransform>().offsetMin = new Vector2(10f, 5f);
        songInfo.GetComponent<RectTransform>().offsetMax = new Vector2(-10f, -5f);

        GameObject progress = new GameObject("ProgressBar");
        progress.transform.SetParent(panel.transform, false);
        Slider slider = progress.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        RectTransform progRT = progress.GetComponent<RectTransform>();
        progRT.anchorMin = new Vector2(0f, 0f);
        progRT.anchorMax = new Vector2(1f, 0.35f);
        progRT.pivot = new Vector2(0.5f, 0.5f);
        progRT.offsetMin = new Vector2(10f, 5f);
        progRT.offsetMax = new Vector2(-10f, -5f);

        GameObject bgSlider = new GameObject("Background");
        bgSlider.transform.SetParent(progress.transform, false);
        Image bgImg = bgSlider.AddComponent<Image>();
        if (sliderBg != null)
        {
            bgImg.sprite = sliderBg;
            bgImg.type = Image.Type.Sliced;
        }
        else
        {
            bgImg.color = new Color(0.28f, 0.24f, 0.32f, 0.9f);
        }
        RectTransform bgRT = bgSlider.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(progress.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        if (sliderFill != null)
        {
            fillImg.sprite = sliderFill;
            fillImg.type = Image.Type.Sliced;
        }
        else
        {
            fillImg.color = new Color(1f, 0.85f, 0.6f, 0.9f);
        }
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        slider.fillRect = fillRT;

        GameObject controls = new GameObject("Controls");
        controls.transform.SetParent(panel.transform, false);
        controls.AddComponent<RectTransform>();
        RectTransform ctrlRT = controls.GetComponent<RectTransform>();
        ctrlRT.anchorMin = new Vector2(0f, 0.35f);
        ctrlRT.anchorMax = new Vector2(1f, 1f);
        ctrlRT.pivot = new Vector2(0.5f, 0.5f);
        ctrlRT.offsetMin = new Vector2(10f, 5f);
        ctrlRT.offsetMax = new Vector2(-10f, -5f);

        CreateButton("Prev", controls.transform, new Vector2(0.15f, 0.5f), new Vector2(40f, 40f), arrowW);
        CreateButton("Play", controls.transform, new Vector2(0.5f, 0.5f), new Vector2(50f, 50f), iconCircle);
        CreateButton("Next", controls.transform, new Vector2(0.85f, 0.5f), new Vector2(40f, 40f), arrowE);

        Selection.activeGameObject = panel;
    }

    [MenuItem("GameObject/SimBar UI/Create HUD", false, 12)]
    private static void CreateHUD(MenuCommand menuCommand)
    {
        if (Selection.activeGameObject == null || Selection.activeGameObject.GetComponent<Canvas>() == null)
        {
            Debug.LogWarning("Please select a Canvas first.");
            return;
        }

        Canvas canvas = Selection.activeGameObject.GetComponent<Canvas>();
        Sprite panelSprite = LoadSprite("button_rectangle_depth_flat");
        GameObject hud = new GameObject("HUD");
        hud.transform.SetParent(canvas.transform, false);
        hud.AddComponent<RectTransform>();
        RectTransform rt = hud.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(10f, -120f);
        rt.offsetMax = new Vector2(-10f, -10f);

        GameObject money = CreateText("MoneyText", hud.transform, "$ 0", new Vector2(0f, 1f), new Vector2(0.3f, 0.4f));
        GameObject rep = CreateText("RepText", hud.transform, "Rep: 0", new Vector2(0.35f, 1f), new Vector2(0.3f, 0.4f));
        GameObject orders = CreateText("OrdersText", hud.transform, "Orders: 0", new Vector2(0.7f, 1f), new Vector2(0.3f, 0.4f));

        Selection.activeGameObject = hud;
    }

    [MenuItem("GameObject/SimBar UI/Create Order Notification Panel", false, 13)]
    private static void CreateOrderNotification(MenuCommand menuCommand)
    {
        if (Selection.activeGameObject == null || Selection.activeGameObject.GetComponent<Canvas>() == null)
        {
            Debug.LogWarning("Please select a Canvas first.");
            return;
        }

        Canvas canvas = Selection.activeGameObject.GetComponent<Canvas>();
        Sprite panelSprite = LoadSprite("button_rectangle_depth_flat");
        GameObject panel = new GameObject("OrderNotificationPanel");
        panel.transform.SetParent(canvas.transform, false);
        panel.SetActive(false);

        Image bg = panel.AddComponent<Image>();
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
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

        Selection.activeGameObject = panel;
    }

    private static GameObject CreateText(string name, Transform parent, string text, Vector2 anchor, Vector2 size)
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

    private static GameObject CreateButton(string name, Transform parent, Vector2 anchor, Vector2 size, Sprite iconSprite = null)
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
        else
        {
            img.color = new Color(0.28f, 0.22f, 0.32f, 0.95f);
        }

        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = anchor - new Vector2(0.5f, 0.5f);
        rt.anchorMax = anchor + new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-size.x / 2f, -size.y / 2f);
        rt.offsetMax = new Vector2(size.x / 2f, size.y / 2f);

        if (iconSprite != null)
        {
            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(btn.transform, false);
            Image iconImg = icon.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            RectTransform iconRT = icon.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
        }
        else
        {
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(btn.transform, false);
            Text txt = textGO.AddComponent<Text>();
            txt.text = name;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 16;
            txt.color = new Color(1f, 0.95f, 0.85f);
            txt.alignment = TextAnchor.MiddleCenter;

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
        }

        return btn;
    }
}
