using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Keep interface measurements in browser CSS pixels, including on Retina displays.</summary>
[DefaultExecutionOrder(10000)]
public sealed class SimBarReadability : MonoBehaviour
{
    public const string TextSizeKey = "SimBar.TextSize";
    private static bool initialized;
    private static int sizeMode;
    public static event Action Changed;
    private Canvas canvas;
    private SimBarExperience host;
    private CanvasScaler scaler;
    private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>();
    private readonly Dictionary<Text, FontState> fonts = new Dictionary<Text, FontState>();
    private readonly List<Text> labels = new List<Text>();
    private readonly List<Text> deadLabels = new List<Text>();
    private Button musicToggle, venueToggle, musicClose, venueClose;
    private bool musicOpen, venueOpen, dirty = true;
    private Vector2 lastViewport;
    private float deviceRatio = 1f;
    private int lastMode = -1;
    private bool lastPour;
    private bool lastBlocked;

    private sealed class FontState
    {
        public int authored, applied;
        public string content;
        public bool modal;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern float SimBarGetPixelRatio();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() { initialized = false; Changed = null; }

    public static int SizeMode
    {
        get
        {
            if (!initialized) { sizeMode = Mathf.Clamp(PlayerPrefs.GetInt(TextSizeKey, 1), 0, 2); initialized = true; }
            return sizeMode;
        }
    }

    public static void SetSizeMode(int value)
    {
        int previous = SizeMode;
        sizeMode = Mathf.Clamp(value, 0, 2);
        PlayerPrefs.SetInt(TextSizeKey, sizeMode);
        PlayerPrefs.Save();
        if (previous != sizeMode) Changed?.Invoke();
    }

    public bool IsCompact { get; private set; }
    public float PixelsPerCssUnit => canvas != null ? canvas.scaleFactor / deviceRatio : 1f;
    public Vector2 CssViewport => new Vector2(Screen.width / deviceRatio, Screen.height / deviceRatio);

    public static void Attach(Canvas target, SimBarExperience experience)
    {
        var owner = target.gameObject.AddComponent<SimBarReadability>();
        owner.canvas = target;
        owner.host = experience;
        owner.scaler = target.GetComponent<CanvasScaler>();
        foreach (RectTransform rect in target.GetComponentsInChildren<RectTransform>(true))
            if (!owner.nodes.ContainsKey(rect.name)) owner.nodes.Add(rect.name, rect);
        owner.CreateAuxiliaryControls();
        owner.ConfigureText();
        Changed += owner.Invalidate;
        SimBarLocalization.Changed += owner.Invalidate;
        owner.RefreshLayout();
    }

    private void OnDestroy()
    {
        Changed -= Invalidate;
        SimBarLocalization.Changed -= Invalidate;
    }

    private void Invalidate() { dirty = true; }

    private void LateUpdate()
    {
        if (canvas == null) return;
        float ratio = ReadDeviceRatio();
        bool pourVisible = Node("Timing Pour") != null && Node("Timing Pour").gameObject.activeSelf;
        bool blocked = BlockingOverlay();
        if (!Mathf.Approximately(ratio, deviceRatio)) { deviceRatio = ratio; dirty = true; }
        if (dirty || lastViewport != CssViewport || lastMode != SizeMode || lastPour != pourVisible || lastBlocked != blocked) RefreshLayout();
        host.RefreshReadableLabels();
        // Gameplay refreshes the night HUD and fever text, and management creates rows on demand.
        if (IsCompact && Node("Journey Night HUD") != null)
            Node("Journey Night HUD").gameObject.SetActive(host.ReadabilityPlaying && !pourVisible);
        bool changed = ApplyFonts();
        if (changed) host.RefreshReadableModalLayout();
    }

    public float EffectiveFontCssSize(Text text)
    {
        return text.fontSize * Mathf.Abs(text.transform.lossyScale.y / canvas.transform.lossyScale.y) * PixelsPerCssUnit;
    }

    public void RefreshLayout()
    {
        if (canvas == null) return;
        deviceRatio = ReadDeviceRatio();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = deviceRatio;
        canvas.scaleFactor = deviceRatio;
        Canvas.ForceUpdateCanvases();
        Vector2 viewport = CssViewport;
        IsCompact = viewport.x < 1400f || viewport.y < 880f || viewport.x / Mathf.Max(1f, viewport.y) < 1.55f;
        lastViewport = viewport; lastMode = SizeMode; dirty = false;
        bool pourVisible = Node("Timing Pour") != null && Node("Timing Pour").gameObject.activeSelf;
        lastPour = pourVisible;
        lastBlocked = BlockingOverlay();
        LayoutHeader(viewport);
        float serviceHeight = IsCompact ? (SizeMode == 2 ? 216f : 196f) : 224f;
        float serviceWidth = IsCompact ? viewport.x - 32f : Mathf.Min(960f, viewport.x - 620f);
        serviceWidth = Mathf.Max(280f, serviceWidth);
        LayoutService(serviceWidth, serviceHeight);
        LayoutNavigation(serviceWidth, serviceHeight);
        LayoutJourneyHUD(viewport, pourVisible);
        LayoutSidePanels(viewport, serviceHeight);
        LayoutPour(serviceWidth, serviceHeight);
        FitModals(viewport);
        ApplyFonts();
        host.RefreshReadableModalLayout();
        Canvas.ForceUpdateCanvases();
    }

    private static float ReadDeviceRatio()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        float ratio = SimBarGetPixelRatio();
        return ratio > 0f && !float.IsNaN(ratio) && !float.IsInfinity(ratio) ? ratio : 1f;
#else
        return 1f;
#endif
    }

    private RectTransform Node(string name) { nodes.TryGetValue(name, out RectTransform result); return result; }
    private Text Label(string name) { var node = Node(name); return node != null ? node.GetComponent<Text>() : null; }
    private void Show(string name, bool visible) { var node = Node(name); if (node != null && node.gameObject.activeSelf != visible) node.gameObject.SetActive(visible); }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void Place(string name, Vector2 anchor, float x, float y, float width, float height)
        => Place(Node(name), anchor, new Vector2(x, y), new Vector2(width, height));

    private void ConfigureText()
    {
        canvas.GetComponentsInChildren(true, labels);
        foreach (Text text in labels)
        {
            fonts[text] = new FontState { authored = text.fontSize, applied = text.fontSize, content = text.text, modal = IsModalText(text) };
            text.resizeTextForBestFit = false;
            if (text.color.r > 0.45f && text.color.g > 0.45f && text.color.b > 0.45f)
                text.color = new Color(Mathf.Max(0.82f, text.color.r), Mathf.Max(0.82f, text.color.g), Mathf.Max(0.82f, text.color.b), text.color.a);
        }
        foreach (string key in new[] { "CASH", "FANS", "TIME" })
        {
            SetBase(key, 18); SetBase(key + " Value", 25);
        }
        SetBase("Journey Countdown", 28);
        Text help = Label("Service Help");
        if (help != null) SimBarLocalization.Bind(help, "1–3 drink   SPACE stop   E match order   P pause", "1–3 음료   SPACE 멈춤   E 주문 선택   P 일시정지");
        Text note = Label("Language Save Note");
        if (note != null) note.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void SetBase(string name, int value)
    {
        Text text = Label(name);
        if (text != null && fonts.TryGetValue(text, out FontState state)) state.authored = value;
    }

    private bool ApplyFonts()
    {
        bool changed = false;
        canvas.GetComponentsInChildren(true, labels);
        float multiplier = SizeMode == 0 ? 1f : SizeMode == 1 ? 1.12f : 1.25f;
        float minimum = SizeMode == 0 ? 16f : SizeMode == 1 ? 18f : 20f;
        foreach (Text text in labels)
        {
            if (!fonts.TryGetValue(text, out FontState state))
            {
                state = new FontState { authored = text.fontSize, applied = text.fontSize, content = text.text, modal = IsModalText(text) };
                fonts.Add(text, state); changed |= state.modal;
            }
            else if (text.fontSize != state.applied) state.authored = text.fontSize;
            float target = Mathf.Max(minimum, state.authored * multiplier);
            if (state.authored >= 34) target = Mathf.Max(minimum, Mathf.Min(target, CssViewport.y * 0.085f));
            float scale = Mathf.Max(0.01f, Mathf.Abs(text.transform.lossyScale.y / canvas.transform.lossyScale.y) * PixelsPerCssUnit);
            int size = Mathf.CeilToInt(target / scale);
            text.resizeTextForBestFit = false;
            if (text.fontSize != size) { text.fontSize = size; changed |= state.modal; }
            if (state.content != text.text) { changed |= state.modal; state.content = text.text; }
            state.applied = size;
        }
        if (Time.frameCount % 120 == 0)
        {
            deadLabels.Clear();
            foreach (var entry in fonts) if (entry.Key == null) deadLabels.Add(entry.Key);
            foreach (Text dead in deadLabels) fonts.Remove(dead);
        }
        return changed;
    }

    private static bool IsModalText(Text text)
    {
        for (Transform parent = text.transform.parent; parent != null; parent = parent.parent)
            if (parent.name == "Journey Lobby Panel" || parent.name == "Journey Guide Panel" || parent.name == "Journey Results Panel"
                || parent.name == "Operations Modal" || parent.name == "Language Settings Panel" || parent.name == "Pause Panel") return true;
        return false;
    }

    private void LayoutHeader(Vector2 view)
    {
        bool hideDuringPour = IsCompact && lastPour && view.y < 520f;
        foreach (Button candidate in canvas.GetComponentsInChildren<Button>(true))
            if (candidate.transform.parent == canvas.transform && candidate.name.Contains("⚙"))
                Place(candidate.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(40f, -42f), new Vector2(56f, 56f));
        Place("Gear Icon", Vector2.one * 0.5f, 0f, 0f, 46f, 46f);
        float right = 16f;
        bool narrow = view.x < 800f;
        Show("TIME Card", !narrow && !hideDuringPour);
        string[] keys = narrow ? new[] { "FANS", "CASH" } : new[] { "TIME", "FANS", "CASH" };
        foreach (string key in keys)
        {
            Show(key + " Card", !hideDuringPour);
            float width = key == "CASH" ? (narrow ? 120f : 156f) : key == "FANS" ? 112f : 104f;
            Place(key + " Card", Vector2.one, -right - width * 0.5f, -42f, width, 70f);
            Show(key + " Icon", false);
            Place(key, Vector2.one * 0.5f, 0f, 18f, width - 12f, 28f);
            Place(key + " Value", Vector2.one * 0.5f, 0f, -16f, width - 10f, 36f);
            right += width + 10f;
        }
        musicToggle.gameObject.SetActive(IsCompact);
        venueToggle.gameObject.SetActive(IsCompact);
        musicToggle.interactable = !lastPour;
        venueToggle.interactable = !lastPour;
        float auxiliaryWidth = view.x < 700f ? 76f : 102f;
        Place(musicToggle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(84f + auxiliaryWidth * 0.5f, -42f), new Vector2(auxiliaryWidth, 46f));
        Place(venueToggle.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(94f + auxiliaryWidth * 1.5f, -42f), new Vector2(auxiliaryWidth, 46f));
        Show("Operations Live Status", false);
    }

    private void LayoutService(float width, float height)
    {
        Show("Order Border", !(IsCompact && lastPour && CssViewport.y < 480f));
        Place("Order Border", new Vector2(0.5f, 0f), 0f, 16f + height * 0.5f, width, height);
        Place("Order Console", Vector2.one * 0.5f, 0f, 0f, width - 8f, height - 8f);
        Place("Order Header", new Vector2(0.5f, 1f), 0f, -18f, width - 32f, 28f);
        Place("Current Order", new Vector2(0.5f, 1f), 0f, -51f, width - 32f, 40f);
        Place("Queue", new Vector2(0.5f, 1f), 0f, -85f, width - 32f, 30f);
        SetBase("Order Header", 16); SetBase("Current Order", 24); SetBase("Queue", 16);
        var panel = Node("Order Console");
        if (panel != null)
        {
            Button[] drinks = panel.GetComponentsInChildren<Button>(true);
            float buttonWidth = (width - 40f) / 3f;
            for (int i = 0; i < drinks.Length; i++)
            {
                Place(drinks[i].GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2((i - 1) * (buttonWidth + 8f), 57f), new Vector2(buttonWidth, 50f));
                Text label = drinks[i].GetComponentInChildren<Text>(true);
                Place(label.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(buttonWidth - 12f, 44f));
            }
        }
        Place("Service Help", new Vector2(0.5f, 0f), 0f, 17f, width - 32f, 28f);
    }

    private void LayoutNavigation(float width, float serviceHeight)
    {
        string[] buttons = { "Staff Management Card", "Operations UPGRADES", "Operations GUEST INFO", "Operations EVENTS" };
        float buttonWidth = (width - 24f) / 4f;
        for (int i = 0; i < buttons.Length; i++)
        {
            Show(buttons[i], !IsCompact || !lastPour);
            RectTransform button = Node(buttons[i]);
            Place(button, new Vector2(0.5f, 0f), new Vector2((i - 1.5f) * (buttonWidth + 8f), serviceHeight + 52f), new Vector2(buttonWidth, 48f));
            if (button != null)
            {
                Text label = button.GetComponentInChildren<Text>(true);
                Place(label.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(buttonWidth - 12f, 44f));
            }
        }
        Show("Staff Icon", false);
    }

    private void LayoutJourneyHUD(Vector2 view, bool pourVisible)
    {
        float width = IsCompact ? view.x - 32f : 420f;
        float height = IsCompact ? (SizeMode == 2 ? 148f : 132f) : 264f;
        Place("Journey Night HUD", new Vector2(0f, 1f), 16f + width * 0.5f, -88f - height * 0.5f, width, height);
        if (IsCompact) Show("Journey Night HUD", host.ReadabilityPlaying && !pourVisible);
        Place("Journey Night Title", new Vector2(0.5f, 1f), -50f, -21f, width - 140f, 32f);
        Place("Journey Countdown", new Vector2(1f, 1f), -60f, -22f, 100f, 36f);
        Place("Journey Score", new Vector2(0f, 1f), 104f, -56f, 184f, 32f);
        Place("Journey Combo", new Vector2(0.5f, 1f), IsCompact ? 0f : 100f, -56f, 182f, 32f);
        Show("Journey Rank", !IsCompact);
        Place("Journey Rank", new Vector2(0f, 1f), 115f, -94f, 206f, 28f);
        Place("Journey Fever", IsCompact ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f), IsCompact ? -132f : 104f, IsCompact ? -56f : -94f, 232f, 28f);
        Place("Journey Fever Track", new Vector2(0.5f, 1f), 0f, IsCompact ? -77f : -115f, 466f, 5f);
        RectTransform fever = Node("Journey Fever Track");
        if (fever != null) fever.localScale = new Vector3((width - 32f) / 466f, 1f, 1f);
        for (int i = 0; i < 3; i++)
        {
            float goalWidth = IsCompact ? (width - 32f) / 3f : width - 104f;
            float x = IsCompact ? (i - 1) * (goalWidth + 4f) : -38f;
            float y = IsCompact ? -102f : -144f - i * 40f;
            Place("Journey Goal " + i, new Vector2(0.5f, 1f), x, y, goalWidth, IsCompact ? 36f : 34f);
            Place("Journey Goal Track " + i, new Vector2(0.5f, 1f), IsCompact ? x : width * 0.5f - 43f, IsCompact ? -height + 8f : y, 96f, 8f);
            RectTransform track = Node("Journey Goal Track " + i);
            if (track != null) track.localScale = new Vector3(IsCompact ? Mathf.Min(1f, goalWidth / 110f) : 0.6f, 1f, 1f);
        }
    }

    private void LayoutSidePanels(Vector2 view, float serviceHeight)
    {
        bool blocked = BlockingOverlay() || lastPour;
        if (blocked) musicOpen = venueOpen = false;
        Show("Playlist Border", !IsCompact || musicOpen);
        Show("Venue Panel", !IsCompact || venueOpen);
        Show("Day and Profit Card", !IsCompact);
        Show("Mood Panel", !IsCompact);
        musicClose.gameObject.SetActive(IsCompact);
        venueClose.gameObject.SetActive(IsCompact);
        float playlistWidth = IsCompact ? Mathf.Min(420f, view.x - 32f) : 284f;
        Place("Playlist Border", IsCompact ? new Vector2(0f, 1f) : new Vector2(1f, 0f), IsCompact ? 16f + playlistWidth / 2f : -16f - playlistWidth / 2f, IsCompact ? -92f - 185f : 402f, playlistWidth, 370f);
        Place("Playlist", Vector2.one * 0.5f, 0f, 0f, playlistWidth - 8f, 362f);
        Place("Playlist Title", new Vector2(0.5f, 1f), -12f, -27f, playlistWidth - 60f, 42f);
        SetBase("Playlist Title", 23);
        Place("Now Playing", new Vector2(0.5f, 1f), 0f, -65f, playlistWidth - 28f, 30f);
        RectTransform playlist = Node("Playlist");
        if (playlist != null)
        {
            Button[] rows = playlist.GetComponentsInChildren<Button>(true);
            int row = 0;
            foreach (Button button in rows)
            {
                if (button.name == "Close Playlist Button") continue;
                Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -108f - row++ * 43f), new Vector2(playlistWidth - 28f, 39f));
                Text label = button.GetComponentInChildren<Text>(true);
                Place(label.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(playlistWidth - 48f, 36f));
                label.alignment = TextAnchor.MiddleCenter;
                foreach (Image image in button.GetComponentsInChildren<Image>(true)) if (image.transform != button.transform) image.gameObject.SetActive(false);
            }
        }
        Show("Playlist Scroll Track", false);
        Place("Venue Panel", IsCompact ? new Vector2(0f, 1f) : Vector2.zero, IsCompact ? 16f + 170f : 146f, IsCompact ? -92f - 150f : 366f, IsCompact ? 340f : 260f, 280f);
        Place("Venue Label", new Vector2(0.5f, 1f), -12f, -22f, IsCompact ? 270f : 210f, 32f);
        Place("Mini Map", Vector2.one * 0.5f, 0f, -1f, IsCompact ? 312f : 236f, 200f);
        Place("Map Caption", new Vector2(0.5f, 0f), 0f, 24f, IsCompact ? 310f : 242f, 40f);
        Place("Day and Profit Card", Vector2.zero, 146f, 106f, 260f, 180f);
        Place("Daily Cash", Vector2.one * 0.5f, 20f, -2f, 194f, 50f);
        Place("Daily Profit", Vector2.one * 0.5f, 0f, -57f, 246f, 48f);
        Place("Mood Panel", new Vector2(1f, 0f), -158f, 106f, 284f, 180f);
        for (int i = 0; i < 2; i++)
        {
            string key = i == 0 ? "GUEST SATISFACTION" : "PERFORMANCE ENERGY";
            Place(key + " Card", Vector2.one * 0.5f, i == 0 ? -73f : 73f, 0f, 138f, 180f);
            Place(key + " Icon", Vector2.one * 0.5f, 0f, 38f, 94f, 82f);
            Place(key, Vector2.one * 0.5f, 0f, -30f, 130f, 48f);
            Place(key + " Percent", Vector2.one * 0.5f, 0f, -62f, 126f, 30f);
            RectTransform back = Node(key + " Back");
            if (back != null) back.localScale = new Vector3(0.45f, 1f, 1f);
        }
        float toastWidth = Mathf.Min(780f, view.x - 32f);
        Place("Toast", new Vector2(0.5f, 0f), 0f, serviceHeight + (lastPour ? 300f : 108f), toastWidth, 62f);
        Place("ToastText", Vector2.one * 0.5f, 0f, 0f, toastWidth - 24f, 58f);
        if (!blocked)
        {
            if (musicOpen && Node("Playlist Border") != null) Node("Playlist Border").SetAsLastSibling();
            if (venueOpen && Node("Venue Panel") != null) Node("Venue Panel").SetAsLastSibling();
        }
    }

    private void LayoutPour(float serviceWidth, float serviceHeight)
    {
        float width = Mathf.Min(800f, serviceWidth);
        float center = IsCompact ? (CssViewport.y < 480f ? CssViewport.y * 0.5f : serviceHeight + 124f) : serviceHeight + 190f;
        Place("Timing Pour", new Vector2(0.5f, 0f), 0f, center, width, 196f);
        Place("Pour Title", new Vector2(0.5f, 1f), -50f, -26f, width - 136f, 38f);
        Place("Pour Countdown", new Vector2(1f, 1f), -54f, -26f, 88f, 36f);
        Place("Pour Instruction", new Vector2(0.5f, 1f), 0f, -68f, width - 32f, 52f);
        Place("Pour Timing Track", Vector2.one * 0.5f, -70f, -24f, 430f, 34f);
        RectTransform track = Node("Pour Timing Track");
        if (track != null) track.localScale = new Vector3(Mathf.Max(0.25f, (width - 180f) / 430f), 1f, 1f);
        RectTransform pour = Node("Timing Pour");
        if (pour != null)
            foreach (Button button in pour.GetComponentsInChildren<Button>(true))
                if (button.transform.parent == pour)
                {
                    Text label = button.GetComponentInChildren<Text>(true);
                    if (label == null) continue;
                    Place(button.GetComponent<RectTransform>(), Vector2.one * 0.5f, new Vector2(width * 0.5f - 76f, -24f), new Vector2(128f, 58f));
                    Place(label.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(116f, 52f));
                }
        Place("Pour Key Hint", new Vector2(0.5f, 0f), 0f, 20f, width - 32f, 34f);
    }

    private void FitModals(Vector2 view)
    {
        foreach (string name in new[] { "Journey Lobby Panel", "Journey Guide Panel", "Journey Results Panel", "Language Settings Panel", "Operations Modal", "Pause Panel" })
        {
            RectTransform panel = Node(name);
            if (panel == null) continue;
            float fit = Mathf.Min(1f, (view.x - 32f) / Mathf.Max(1f, panel.sizeDelta.x), (view.y - 32f) / Mathf.Max(1f, panel.sizeDelta.y));
            panel.localScale = Vector3.one * Mathf.Max(0.1f, fit);
        }
        foreach (string name in new[] { "Journey Lobby", "Journey Guide", "Journey Results" })
        {
            RectTransform overlay = Node(name);
            if (overlay == null) continue;
            foreach (Button button in overlay.GetComponentsInChildren<Button>(true))
                if (button.name == "Journey Settings Button")
                    Place(button.GetComponent<RectTransform>(), Vector2.one, new Vector2(-86f, -38f), new Vector2(148f, 48f));
        }
    }

    private bool BlockingOverlay()
    {
        foreach (string name in new[] { "Journey Lobby", "Journey Guide", "Journey Results", "Language Settings", "Operations Modal Overlay", "Pause Panel" })
            if (Node(name) != null && Node(name).gameObject.activeSelf) return true;
        return false;
    }

    private void CreateAuxiliaryControls()
    {
        musicToggle = MakeButton("Compact Music Button", canvas.transform, "MUSIC", "음악", () => { musicOpen = !musicOpen; venueOpen = false; RefreshLayout(); });
        venueToggle = MakeButton("Compact Venue Button", canvas.transform, "VENUE", "지도", () => { venueOpen = !venueOpen; musicOpen = false; RefreshLayout(); });
        foreach (string name in new[] { "Playlist Border", "Venue Panel" })
        {
            RectTransform parent = Node(name);
            if (parent == null) continue;
            bool music = name == "Playlist Border";
            Button close = MakeButton(music ? "Close Playlist Button" : "Close Venue Button", parent, "×", "×", () => { musicOpen = venueOpen = false; RefreshLayout(); });
            if (music) musicClose = close; else venueClose = close;
            Place(close.GetComponent<RectTransform>(), Vector2.one, new Vector2(-23f, -24f), new Vector2(32f, 34f));
        }
    }

    private Button MakeButton(string name, Transform parent, string english, string korean, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.12f, 0.17f, 0.23f, 0.98f);
        Button button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(action);
        var labelObject = new GameObject(name + " Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(go.transform, false);
        Text text = labelObject.GetComponent<Text>(); text.font = SimBarLocalization.Font; text.fontSize = 18; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        SimBarLocalization.Bind(text, english, korean);
        RectTransform label = text.rectTransform; label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one; label.offsetMin = new Vector2(4f, 2f); label.offsetMax = new Vector2(-4f, -2f);
        return button;
    }
}
