using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reference-inspired skin for the playable HUD. Existing texts, buttons and
/// meter images remain alive so SimBarExperience retains its gameplay bindings.
/// </summary>
public sealed class SimBarHudSkin : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.055f, 0.062f, 0.10f, 0.97f);
    private static readonly Color InkLight = new Color(0.12f, 0.135f, 0.20f, 0.98f);
    private static readonly Color Gold = new Color(0.76f, 0.63f, 0.32f, 1f);
    private static readonly Color Silver = new Color(0.46f, 0.49f, 0.57f, 1f);
    private static readonly Color Green = new Color(0.44f, 0.90f, 0.37f, 1f);
    private static Sprite rounded;
    private readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private readonly Dictionary<Sprite, float> iconAspects = new Dictionary<Sprite, float>();
    private Text sourceCash;
    private Text sourceTime;
    private Text dailyCash;
    private Text dailyTime;
    private Text dailyProfit;
    private Text satisfactionPercent;
    private Text performancePercent;
    private Image satisfaction;
    private Image performance;
    private Font font;
    private SimBarExperience experience;

    /// <summary>Call once, after the existing runtime interface is constructed.</summary>
    public static void Apply(Canvas canvas)
    {
        if (canvas == null || canvas.GetComponent<SimBarHudSkin>() != null)
            return;
        canvas.gameObject.AddComponent<SimBarHudSkin>().Style(canvas);
    }

    private void Style(Canvas canvas)
    {
        font = SimBarLocalization.Font;
        experience = UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
        foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true))
            if (!nodes.ContainsKey(node.name)) nodes.Add(node.name, node);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Panels should not consume clicks meant for the world or its buttons.
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = graphic.GetComponent<Button>() != null;
        foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
        {
            text.font = font;
            text.fontStyle = FontStyle.Normal;
            text.supportRichText = true;
        }

        StyleTop(canvas.transform);
        StylePlaylist();
        StyleOrderConsole();
        StyleVenue(canvas.transform);
        StyleMood();
        StylePause();
        LateUpdate();
    }

    private void StyleTop(Transform root)
    {
        Button gear = null;
        foreach (Button candidate in root.GetComponentsInChildren<Button>(true))
            if (candidate.transform.parent == root) { gear = candidate; break; }
        if (gear != null)
        {
            Rect(gear.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(52f, -50f), new Vector2(80f, 80f));
            Panel(gear.GetComponent<Image>(), InkLight, Silver, 1.8f);
            Text label = gear.GetComponentInChildren<Text>(true);
            if (label != null) label.text = string.Empty;
            AddIcon("Gear Icon", gear.transform, Icon("setup", new Rect(3f, 3f, 48f, 48f)), Vector2.zero, new Vector2(66f, 66f));
            ButtonColors(gear);
        }

        Stat("CASH", "Cash", "현금", new Vector2(-509f, -47f), new Vector2(240f, 80f),
            Icon("cash", new Rect(3f, 20f, 37f, 37f)), Green);
        Stat("FANS", "Fans", "팬", new Vector2(-284f, -47f), new Vector2(200f, 80f),
            Icon("fans", new Rect(3f, 14f, 43f, 40f)), new Color(1f, 0.80f, 0.26f));
        Stat("TIME", "Time", "시간", new Vector2(-97f, -47f), new Vector2(174f, 80f),
            Icon("time", new Rect(3f, 9f, 36f, 44f)), Color.white);
        sourceCash = Component<Text>("CASH Value");
        FitMoney(sourceCash, 18, 28);
        sourceTime = Component<Text>("TIME Value");

        Transform toast = Node("Toast");
        if (toast != null)
        {
            Rect((RectTransform)toast, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(530f, 44f));
            Panel(toast.GetComponent<Image>(), new Color(0.04f, 0.05f, 0.10f, 0.84f), new Color(0.25f, 0.29f, 0.39f), 1f);
            Text toastLabel = Component<Text>("ToastText");
            TextStyle(toastLabel, 18, TextAnchor.MiddleCenter, new Color(0.89f, 0.94f, 1f));
            if (toastLabel != null) Rect(toastLabel.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(510f, 36f));
        }
    }

    private void Stat(string key, string title, string koreanTitle, Vector2 position, Vector2 size, Sprite icon, Color valueColor)
    {
        Transform card = Node(key + " Card");
        if (card == null) return;
        Rect((RectTransform)card, Vector2.one, position, size);
        Panel(card.GetComponent<Image>(), Ink, new Color(0.17f, 0.18f, 0.25f), 1.4f);
        AddIcon(key + " Icon", card, icon, new Vector2(-size.x * 0.5f + 36f, -3f), new Vector2(58f, 58f));
        Text heading = Component<Text>(key);
        Text value = Component<Text>(key + " Value");
        TextStyle(heading, 26, TextAnchor.MiddleCenter, Color.white);
        TextStyle(value, 28, TextAnchor.MiddleCenter, valueColor);
        float textWidth = size.x - 67f;
        if (heading != null)
        {
            SimBarLocalization.Bind(heading, title, koreanTitle);
            Rect(heading.rectTransform, Vector2.one * 0.5f, new Vector2(30f, 21f), new Vector2(textWidth, 30f));
        }
        if (value != null)
        {
            value.fontStyle = FontStyle.Bold;
            Rect(value.rectTransform, Vector2.one * 0.5f, new Vector2(30f, -15f), new Vector2(textWidth, 38f));
        }
    }

    private void StylePlaylist()
    {
        Transform outer = Node("Playlist Border");
        Transform panel = Node("Playlist");
        if (outer == null || panel == null) return;
        Rect((RectTransform)outer, new Vector2(1f, 0f), new Vector2(-187f, 383f), new Vector2(358f, 308f));
        Panel(outer.GetComponent<Image>(), new Color(0.25f, 0.26f, 0.30f), Gold, 1.5f);
        Rect((RectTransform)panel, Vector2.one * 0.5f, Vector2.zero, new Vector2(346f, 296f));
        Panel(panel.GetComponent<Image>(), Ink, new Color(0.11f, 0.12f, 0.18f), 0.8f);
        Text title = Component<Text>("Playlist Title");
        TextStyle(title, 31, TextAnchor.MiddleCenter, new Color(0.94f, 0.92f, 0.85f));
        if (title != null)
        {
            SimBarLocalization.Bind(title, "[ Playlist ]", "[ 재생 목록 ]");
            Rect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(310f, 42f));
        }
        Text now = Component<Text>("Now Playing");
        TextStyle(now, 12, TextAnchor.MiddleCenter, Green);
        if (now != null) Rect(now.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(322f, 24f));

        Button[] rows = panel.GetComponentsInChildren<Button>(true);
        Color[] albumColors = { new Color(0.50f, 0.68f, 0.52f), new Color(0.30f, 0.64f, 0.80f), new Color(0.77f, 0.27f, 0.43f), new Color(0.88f, 0.63f, 0.28f), new Color(0.41f, 0.77f, 0.76f), new Color(0.61f, 0.40f, 0.78f) };
        for (int i = 0; i < rows.Length; i++)
        {
            Button row = rows[i];
            Rect(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-1f, -88f - i * 33.5f), new Vector2(316f, 30f));
            row.GetComponent<Image>().sprite = Rounded;
            row.GetComponent<Image>().type = Image.Type.Sliced;
            ButtonColors(row);
            Text label = row.GetComponentInChildren<Text>(true);
            TextStyle(label, 17, TextAnchor.MiddleLeft, new Color(0.94f, 0.94f, 0.91f));
            if (label != null) Rect(label.rectTransform, Vector2.one * 0.5f, new Vector2(14f, 0f), new Vector2(267f, 30f));
            Image album = NewImage("Album " + (i + 1), row.transform, albumColors[i % albumColors.Length]);
            Rect(album.rectTransform, Vector2.one * 0.5f, new Vector2(-143f, 0f), new Vector2(22f, 26f));
            Image stripe = NewImage("Album Stripe", album.transform, new Color(0.04f, 0.06f, 0.10f, 0.72f));
            Rect(stripe.rectTransform, Vector2.one * 0.5f, new Vector2(0f, -3f), new Vector2(20f, 6f));
        }
        Image scrollBack = NewImage("Playlist Scroll Track", panel, new Color(0.05f, 0.05f, 0.08f));
        Rect(scrollBack.rectTransform, new Vector2(1f, 0.5f), new Vector2(-6f, -25f), new Vector2(5f, 204f));
        Image scroll = NewImage("Playlist Scroll Thumb", scrollBack.transform, new Color(0.44f, 0.45f, 0.50f));
        Rect(scroll.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -31f), new Vector2(5f, 58f));
    }

    private void StyleOrderConsole()
    {
        Transform border = Node("Order Border");
        Transform panel = Node("Order Console");
        if (border == null || panel == null) return;
        Rect((RectTransform)border, new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(848f, 150f));
        Panel(border.GetComponent<Image>(), new Color(0.31f, 0.29f, 0.30f, 0.98f), Gold, 1.3f);
        Rect((RectTransform)panel, Vector2.one * 0.5f, Vector2.zero, new Vector2(838f, 140f));
        Panel(panel.GetComponent<Image>(), new Color(0.05f, 0.06f, 0.105f, 0.96f), new Color(0.12f, 0.14f, 0.21f), 0.8f);
        PlaceText("Order Header", "BAR SERVICE", 13, new Vector2(0f, -10f), new Vector2(760f, 18f), new Color(0.84f, 0.80f, 0.66f), "바 주문 서비스");
        PlaceText("Current Order", null, 24, new Vector2(0f, -34f), new Vector2(796f, 30f), Color.white);
        PlaceText("Queue", null, 13, new Vector2(0f, -57f), new Vector2(790f, 22f), new Color(0.61f, 0.75f, 0.83f));
        Button[] drinks = panel.GetComponentsInChildren<Button>(true);
        const float rowWidth = 790f;
        const float buttonGap = 16f;
        float buttonWidth = (rowWidth - buttonGap * Mathf.Max(0, drinks.Length - 1)) / Mathf.Max(1, drinks.Length);
        for (int i = 0; i < drinks.Length; i++)
        {
            Button drink = drinks[i];
            float x = -rowWidth * 0.5f + buttonWidth * 0.5f + (buttonWidth + buttonGap) * i;
            Rect(drink.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(x, 36f), new Vector2(buttonWidth, 43f));
            Image image = drink.GetComponent<Image>();
            Panel(image, new Color(image.color.r * 0.52f, image.color.g * 0.52f, image.color.b * 0.52f, 1f), new Color(0.56f, 0.57f, 0.60f), 1.2f);
            Text text = drink.GetComponentInChildren<Text>(true);
            TextStyle(text, 17, TextAnchor.MiddleCenter, Color.white);
            if (text != null) Rect(text.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(buttonWidth - 8f, 39f));
            ButtonColors(drink);
        }
        Text help = Component<Text>("Service Help");
        TextStyle(help, 11, TextAnchor.MiddleCenter, new Color(0.66f, 0.70f, 0.77f));
        if (help != null)
        {
            SimBarLocalization.Bind(help, "1–5 SELECT     M MENU     E PREPARE ORDER     P PAUSE", "1–5 메뉴 선택     M 메뉴     E 주문 준비     P 일시 정지");
            Rect(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(790f, 18f));
        }
    }

    private void StyleVenue(Transform root)
    {
        Transform venue = Node("Venue Panel");
        Transform map = Node("Mini Map");
        if (venue == null || map == null) return;
        Rect((RectTransform)venue, Vector2.zero, new Vector2(133f, 139f), new Vector2(242f, 252f));
        Panel(venue.GetComponent<Image>(), Ink, Silver, 2f);
        Text venueLabel = Component<Text>("Venue Label");
        if (venueLabel != null)
        {
            SimBarLocalization.Bind(venueLabel, "LIVE VENUE", "공연장 현황");
            TextStyle(venueLabel, 15, TextAnchor.MiddleCenter, new Color(0.72f, 0.76f, 0.85f));
            Rect(venueLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(216f, 22f));
        }
        Rect((RectTransform)map, Vector2.one * 0.5f, new Vector2(0f, -3f), new Vector2(216f, 200f));
        Panel(map.GetComponent<Image>(), new Color(0.035f, 0.045f, 0.065f), new Color(0.22f, 0.25f, 0.31f), 1f);
        foreach (string dotName in new[] { "Bar Dot", "Stage Dot", "Crowd Dot" })
        {
            Image dot = Component<Image>(dotName);
            if (dot != null) dot.color = Color.clear;
        }
        Text caption = Component<Text>("Map Caption");
        if (caption != null)
        {
            SimBarLocalization.Bind(caption, "2 STAFF  /  HOUSE OPEN", "직원 2명  /  영업 중");
            TextStyle(caption, 12, TextAnchor.MiddleCenter, Green);
            Rect(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 11f), new Vector2(222f, 18f));
        }

        Image staff = NewImage("Staff Management Card", root, InkLight);
        Rect(staff.rectTransform, Vector2.zero, new Vector2(133f, 292f), new Vector2(242f, 44f));
        Panel(staff, InkLight, Silver, 1.8f);
        AddIcon("Staff Icon", staff.transform, Icon("staff", new Rect(5f, 7f, 25f, 27f)), new Vector2(-96f, 0f), new Vector2(28f, 30f));
        NewText("Staff Management", staff.transform, "Staff Management", 19, new Vector2(17f, 0f), new Vector2(203f, 36f), Color.white, "직원 관리");

        Image day = NewImage("Day and Profit Card", root, Ink);
        Rect(day.rectTransform, Vector2.zero, new Vector2(397f, 82f), new Vector2(262f, 150f));
        Panel(day, Ink, Silver, 1.8f);
        Image dayPill = NewImage("Day Pill", day.transform, new Color(0.025f, 0.032f, 0.065f));
        Rect(dayPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(-67f, -27f), new Vector2(116f, 39f));
        NewText("Day 35", dayPill.transform, "Day 35", 25, Vector2.zero, new Vector2(112f, 37f), Color.white, "35일차");
        Image timePill = NewImage("Evening Pill", day.transform, new Color(0.025f, 0.032f, 0.065f));
        Rect(timePill.rectTransform, new Vector2(0.5f, 1f), new Vector2(60f, -27f), new Vector2(120f, 39f));
        dailyTime = NewText("Evening Time", timePill.transform, "21:30", 24, Vector2.zero, new Vector2(116f, 37f), Color.white);
        AddIcon("Daily Cash Icon", day.transform, Icon("cash", new Rect(3f, 20f, 37f, 37f)), new Vector2(-99f, -5f), new Vector2(43f, 43f));
        dailyCash = NewText("Daily Cash", day.transform, string.Empty, 33, new Vector2(20f, -4f), new Vector2(194f, 47f), Green);
        dailyCash.fontStyle = FontStyle.Bold;
        FitMoney(dailyCash, 20, 33);
        dailyProfit = NewText("Daily Profit", day.transform, string.Empty, 17, new Vector2(0f, -53f), new Vector2(250f, 29f), Color.white);
        dailyProfit.supportRichText = true;
        FitMoney(dailyProfit, 12, 17);
    }

    private void StyleMood()
    {
        Transform mood = Node("Mood Panel");
        if (mood == null) return;
        Rect((RectTransform)mood, new Vector2(1f, 0f), new Vector2(-187f, 112f), new Vector2(358f, 210f));
        mood.GetComponent<Image>().color = Color.clear;
        satisfaction = Component<Image>("GUEST SATISFACTION Fill");
        performance = Component<Image>("PERFORMANCE ENERGY Fill");
        satisfactionPercent = MoodCard(mood, "GUEST SATISFACTION", "GUEST\nSATISFACTION", "손님\n만족도", -92f,
            Icon("guestsati", new Rect(13f, 12f, 101f, 86f)), new Color(1f, 0.70f, 0.19f));
        performancePercent = MoodCard(mood, "PERFORMANCE ENERGY", "PERFORMANCE\nRESPONSE", "공연\n호응도", 92f,
            Icon("perrform", new Rect(12f, 7f, 98f, 92f)), new Color(0.33f, 0.86f, 0.99f));
    }

    private Text MoodCard(Transform parent, string key, string title, string koreanTitle, float x, Sprite icon, Color accent)
    {
        Image card = NewImage(key + " Card", parent, Ink);
        Rect(card.rectTransform, Vector2.one * 0.5f, new Vector2(x, 0f), new Vector2(174f, 210f));
        Panel(card, Ink, Gold, 2f);
        AddIcon(key + " Icon", card.transform, icon, new Vector2(0f, 42f), new Vector2(130f, 116f));
        Text label = Component<Text>(key);
        if (label != null)
        {
            label.transform.SetParent(card.transform, false);
            SimBarLocalization.Bind(label, title, koreanTitle);
            TextStyle(label, 17, TextAnchor.MiddleCenter, Color.white);
            Rect(label.rectTransform, Vector2.one * 0.5f, new Vector2(0f, -42f), new Vector2(162f, 42f));
        }
        Image back = Component<Image>(key + " Back");
        Image fill = Component<Image>(key + " Fill");
        if (back != null)
        {
            back.transform.SetParent(card.transform, false);
            Rect(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 11f), new Vector2(252f, 12f));
            // SimBarExperience writes a 246-pixel width every frame. Scaling
            // the track preserves that binding while fitting the narrow card.
            back.rectTransform.localScale = new Vector3(0.58f, 1f, 1f);
            Panel(back, new Color(0.18f, 0.19f, 0.24f), new Color(0.29f, 0.28f, 0.24f), 0.6f);
        }
        if (fill != null)
        {
            fill.sprite = Rounded;
            fill.type = Image.Type.Sliced;
            fill.color = accent;
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(3f, 0f);
        }
        return NewText(key + " Percent", card.transform, "94%", 18, new Vector2(0f, -77f), new Vector2(160f, 24f), Green);
    }

    private void StylePause()
    {
        Transform pause = Node("Pause Panel");
        if (pause == null) return;
        Rect((RectTransform)pause, Vector2.one * 0.5f, Vector2.zero, new Vector2(500f, 240f));
        Panel(pause.GetComponent<Image>(), Ink, Gold, 2f);
        TextStyle(Component<Text>("Pause Title"), 32, TextAnchor.MiddleCenter, new Color(0.98f, 0.92f, 0.75f));
        TextStyle(Component<Text>("Pause Text"), 18, TextAnchor.MiddleCenter, new Color(0.75f, 0.81f, 0.89f));
        foreach (Button button in pause.GetComponentsInChildren<Button>(true))
        {
            Panel(button.GetComponent<Image>(), new Color(0.15f, 0.35f, 0.30f), Gold, 1.4f);
            ButtonColors(button);
        }
        pause.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (sourceCash != null && dailyCash != null) dailyCash.text = sourceCash.text;
        if (dailyProfit != null && experience != null)
        {
            float profit = experience.JourneyProfitForHUD;
            string tint = profit < 0f ? "FF997F" : "78E05D";
            dailyProfit.text = SimBarLocalization.Text("DAILY PROFIT: ", "오늘의 수익: ") + "<color=#" + tint + ">"
                + SimBarMoney.Format(profit, true) + "</color>";
        }
        if (dailyTime != null && sourceTime != null) dailyTime.text = sourceTime.text;
        if (satisfactionPercent != null && satisfaction != null)
            satisfactionPercent.text = "(" + Mathf.RoundToInt(Mathf.Clamp01(satisfaction.rectTransform.sizeDelta.x / 246f) * 100f) + "%)";
        if (performancePercent != null && performance != null)
            performancePercent.text = "(" + Mathf.RoundToInt(Mathf.Clamp01(performance.rectTransform.sizeDelta.x / 246f) * 100f) + "%)";
    }

    private Transform Node(string name)
    {
        Transform node;
        return nodes.TryGetValue(name, out node) ? node : null;
    }

    private T Component<T>(string name) where T : Component
    {
        Transform node = Node(name);
        return node != null ? node.GetComponent<T>() : null;
    }

    private void PlaceText(string name, string content, int size, Vector2 position, Vector2 bounds, Color color, string koreanContent = null)
    {
        Text text = Component<Text>(name);
        if (text == null) return;
        if (content != null) SimBarLocalization.Bind(text, content, koreanContent ?? content);
        TextStyle(text, size, TextAnchor.MiddleCenter, color);
        Rect(text.rectTransform, new Vector2(0.5f, 1f), position, bounds);
    }

    private static void TextStyle(Text text, int size, TextAnchor alignment, Color color)
    {
        if (text == null) return;
        text.font = SimBarLocalization.Font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = FontStyle.Normal;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private static void FitMoney(Text text, int minSize, int maxSize)
    {
        if (text == null) return;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private Text NewText(string name, Transform parent, string content, int size, Vector2 position, Vector2 bounds, Color color, string koreanContent = null)
    {
        GameObject label = new GameObject(name, typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = font;
        if (koreanContent != null) SimBarLocalization.Bind(text, content, koreanContent);
        else text.text = content;
        TextStyle(text, size, TextAnchor.MiddleCenter, color);
        Rect(text.rectTransform, Vector2.one * 0.5f, position, bounds);
        return text;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void AddIcon(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        if (sprite == null) return;
        Image image = NewImage(name, parent, Color.white);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        // Fit using the PNG's original aspect, rather than its possibly
        // power-of-two-resized imported texture aspect.
        float aspect;
        if (iconAspects.TryGetValue(sprite, out aspect))
        {
            if (size.x / size.y > aspect) size.x = size.y * aspect;
            else size.y = size.x / aspect;
        }
        image.preserveAspect = false;
        Rect(image.rectTransform, Vector2.one * 0.5f, position, size);
    }

    // Art/Img contains the supplied reference's cropped cards. Only the icon
    // rectangles are reused; money, time, percentages and playlist remain live.
    private Sprite Icon(string name, Rect topLeftCrop)
    {
        Sprite sprite;
        if (icons.TryGetValue(name, out sprite)) return sprite;
        Texture2D texture = Resources.Load<Texture2D>("Art/Img/" + name);
        if (texture == null) return null;
        float sourceAspect = topLeftCrop.width / topLeftCrop.height;
        // These rectangles are measured in the supplied PNGs. Unity's default
        // non-power-of-two importer may resize each axis independently (for
        // example, a 123x156 card becomes 128x128), so scale both axes before
        // cropping. Otherwise a face crop can accidentally include its printed
        // caption and a banknote crop can include a baked-in cash value.
        Vector2 originalSize = IconSourceSize(name, texture);
        float scaleX = texture.width / originalSize.x;
        float scaleY = texture.height / originalSize.y;
        topLeftCrop = new Rect(topLeftCrop.x * scaleX, topLeftCrop.y * scaleY,
            topLeftCrop.width * scaleX, topLeftCrop.height * scaleY);
        float x = Mathf.Clamp(topLeftCrop.x, 0f, texture.width - 1f);
        float width = Mathf.Min(topLeftCrop.width, texture.width - x);
        float height = Mathf.Min(topLeftCrop.height, texture.height - topLeftCrop.y);
        float y = Mathf.Max(0f, texture.height - topLeftCrop.y - height);
        if (width <= 0f || height <= 0f) return null;
        sprite = Sprite.Create(texture, new Rect(x, y, width, height), Vector2.one * 0.5f, 100f);
        sprite.name = "SimBar HUD " + name;
        icons.Add(name, sprite);
        iconAspects.Add(sprite, sourceAspect);
        return sprite;
    }

    private static Vector2 IconSourceSize(string name, Texture2D fallback)
    {
        switch (name)
        {
            case "cash": return new Vector2(146f, 60f);
            case "fans": return new Vector2(127f, 61f);
            case "time": return new Vector2(117f, 63f);
            case "setup": return new Vector2(55f, 55f);
            case "staff": return new Vector2(175f, 40f);
            case "guestsati": return new Vector2(123f, 156f);
            case "perrform": return new Vector2(122f, 156f);
            default: return new Vector2(fallback.width, fallback.height);
        }
    }

    private static void Panel(Image image, Color fill, Color edge, float edgeWidth)
    {
        if (image == null) return;
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.color = fill;
        Outline line = image.GetComponent<Outline>();
        if (line == null) line = image.gameObject.AddComponent<Outline>();
        line.effectColor = edge;
        line.effectDistance = new Vector2(edgeWidth, -edgeWidth);
        line.useGraphicAlpha = true;
        Shadow shadow = image.GetComponent<Shadow>();
        if (shadow == null || shadow is Outline) shadow = image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(2f, -4f);
    }

    private static void ButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.69f, 0.72f, 0.77f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.GetComponent<Image>().raycastTarget = true;
    }

    private static void Rect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 64;
            const float radius = 15f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "SimBar Rounded HUD";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (31.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (31.5f - radius), 0f);
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
            rounded.name = "SimBar Rounded Nine Slice";
            return rounded;
        }
    }
}
