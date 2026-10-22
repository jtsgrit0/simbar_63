using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Readable controls around a reserved venue view, measured in browser CSS pixels.</summary>
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
    private Button menuToggle, musicToggle, venueToggle, goalsToggle, menuClose, musicClose, venueClose, goalsClose;
    private RectTransform menuPanel;
    private Text compactSummary;
    private bool menuOpen, musicOpen, venueOpen, goalsOpen, dirty = true;
    private Vector2 lastViewport;
    private float deviceRatio = 1f;
    private int lastMode = -1;
    private bool lastPour, lastBlocked;
    private Camera venueCamera, backgroundCamera;
    private static readonly string[] ManagementButtons = { "Staff Management Card", "Operations UPGRADES", "Operations GUEST INFO", "Operations EVENTS" };
    private sealed class FontState { public int authored, applied; public string content; public bool modal; }

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
        int previous = SizeMode; sizeMode = Mathf.Clamp(value, 0, 2);
        PlayerPrefs.SetInt(TextSizeKey, sizeMode); PlayerPrefs.Save();
        if (previous != sizeMode) Changed?.Invoke();
    }
    public bool IsCompact { get; private set; }
    public bool IsMenuOpen => menuOpen;
    public float PixelsPerCssUnit => canvas != null ? canvas.scaleFactor / deviceRatio : 1f;
    public Vector2 CssViewport => new Vector2(Screen.width / deviceRatio, Screen.height / deviceRatio);
    /// <summary>Clear venue area in CSS pixels, using a bottom-left origin.</summary>
    public Rect SceneViewportCss { get; private set; }

    public static void Attach(Canvas target, SimBarExperience experience)
    {
        var owner = target.gameObject.AddComponent<SimBarReadability>();
        owner.canvas = target; owner.host = experience; owner.scaler = target.GetComponent<CanvasScaler>(); owner.venueCamera = Camera.main;
        foreach (RectTransform rect in target.GetComponentsInChildren<RectTransform>(true))
            if (!owner.nodes.ContainsKey(rect.name)) owner.nodes.Add(rect.name, rect);
        owner.CreateAuxiliaryControls(); owner.ConfigureText();
        Changed += owner.Invalidate; SimBarLocalization.Changed += owner.Invalidate;
        owner.RefreshLayout();
    }
    private void OnDestroy()
    {
        Changed -= Invalidate; SimBarLocalization.Changed -= Invalidate;
        if (venueCamera != null) { venueCamera.rect = new Rect(0, 0, 1, 1); venueCamera.orthographicSize = 8.4f; }
        if (backgroundCamera != null) Destroy(backgroundCamera.gameObject);
    }
    private void Invalidate() { dirty = true; }
    private void LateUpdate()
    {
        if (canvas == null) return;
        float ratio = ReadDeviceRatio();
        bool pouring = Node("Timing Pour") != null && Node("Timing Pour").gameObject.activeSelf;
        bool blocked = BlockingOverlay();
        if (!Mathf.Approximately(ratio, deviceRatio)) { deviceRatio = ratio; dirty = true; }
        if (dirty || lastViewport != CssViewport || lastMode != SizeMode || lastPour != pouring || lastBlocked != blocked) RefreshLayout();
        host.RefreshReadableLabels(IsCompact, UsesSideDock(CssViewport));
        if (IsCompact)
        {
            Show("Journey Night HUD", host.ReadabilityPlaying && goalsOpen && !pouring && !blocked);
            string countdown = Label("Journey Countdown") != null ? Label("Journey Countdown").text : "";
            string score = Label("Journey Score") != null ? Label("Journey Score").text : "";
            int separator = score.LastIndexOf(' ');
            string points = separator >= 0 ? score.Substring(separator + 1) : score;
            compactSummary.text = host.ReadabilityPlaying ? countdown + (CssViewport.x >= 360 ? " · " + points : "") : "SimBar";
        }
        if (ApplyFonts()) host.RefreshReadableModalLayout();
    }
    public float EffectiveFontCssSize(Text text)
        => text.fontSize * Mathf.Abs(text.transform.lossyScale.y / canvas.transform.lossyScale.y) * PixelsPerCssUnit;

    public void RefreshLayout()
    {
        if (canvas == null) return;
        deviceRatio = ReadDeviceRatio(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = deviceRatio; canvas.scaleFactor = deviceRatio;
        Canvas.ForceUpdateCanvases();
        Vector2 view = CssViewport;
        IsCompact = view.x < 1800f || view.y < 1000f;
        lastViewport = view; lastMode = SizeMode; dirty = false;
        lastPour = Node("Timing Pour") != null && Node("Timing Pour").gameObject.activeSelf; lastBlocked = BlockingOverlay();
        if (lastBlocked || lastPour) CloseDrawers();
        float dock = IsCompact ? (view.y < 480f ? 112f : SizeMode == 2 ? 156f : 148f) : 156f;
        LayoutHeader(view); LayoutService(view, dock); LayoutNavigation(view, dock); LayoutJourneyHUD(view);
        LayoutSidePanels(view, dock); LayoutPour(view, dock); LayoutScene(view, dock); FitModals(view);
        ApplyFonts(); host.RefreshReadableModalLayout(); Canvas.ForceUpdateCanvases();
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
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size;
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
        }
        foreach (string key in new[] { "CASH", "FANS", "TIME" }) { SetBase(key, 14); SetBase(key + " Value", 18); }
        SetBase("Journey Countdown", 24); SetBase("Order Header", 14); SetBase("Current Order", 18); SetBase("Queue", 14);
        SetBase("Journey Night Title", 16); SetBase("Journey Score", 18); SetBase("Journey Combo", 14); SetBase("Journey Fever", 14);
        SetBase("Pause Title", 24); SetBase("Daily Cash", 22); SetBase("Day 35", 16); SetBase("Evening Time", 16);
        foreach (string key in new[] { "GUEST SATISFACTION", "PERFORMANCE ENERGY" })
        {
            SetBase(key, 14); SetBase(key + " Percent", 14);
            if (Label(key) != null) Label(key).horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        if (Label("GUEST SATISFACTION") != null) SimBarLocalization.Bind(Label("GUEST SATISFACTION"), "Guest\nsatisfaction", "손님\n만족도");
        if (Label("PERFORMANCE ENERGY") != null) SimBarLocalization.Bind(Label("PERFORMANCE ENERGY"), "Show\nenergy", "공연\n호응도");
        for (int i = 0; i < 3; i++) SetBase("Journey Goal " + i, 14);
        if (Label("Pour Instruction") != null) Label("Pour Instruction").horizontalOverflow = HorizontalWrapMode.Wrap;
        foreach (string name in ManagementButtons) if (Node(name) != null) SetBase(Node(name).GetComponentInChildren<Text>(true), 16);
    }
    private void SetBase(string name, int value) => SetBase(Label(name), value);
    private void SetBase(Text text, int value) { if (text != null && fonts.TryGetValue(text, out FontState state)) state.authored = value; }
    private bool ApplyFonts()
    {
        bool changed = false; canvas.GetComponentsInChildren(true, labels);
        float multiplier = SizeMode == 0 ? 1f : SizeMode == 1 ? 1.10f : 1.2f;
        foreach (Text text in labels)
        {
            if (!fonts.TryGetValue(text, out FontState state))
            {
                state = new FontState { authored = text.fontSize, applied = text.fontSize, content = text.text, modal = IsModalText(text) };
                fonts.Add(text, state); changed |= state.modal;
            }
            else if (text.fontSize != state.applied) state.authored = text.fontSize;
            float minimum = (state.modal ? 16f : 14f) + SizeMode * 2f;
            float target = Mathf.Max(minimum, state.authored * multiplier);
            if (state.authored >= 28) target = Mathf.Max(minimum, Mathf.Min(target, IsCompact ? 28f : 36f));
            float scale = Mathf.Max(0.01f, Mathf.Abs(text.transform.lossyScale.y / canvas.transform.lossyScale.y) * PixelsPerCssUnit);
            int size = Mathf.CeilToInt(target / scale); text.resizeTextForBestFit = false;
            if (text.fontSize != size) { text.fontSize = size; changed |= state.modal; }
            if (state.content != text.text) { changed |= state.modal; state.content = text.text; }
            state.applied = size;
        }
        if (Time.frameCount % 120 == 0)
        {
            deadLabels.Clear(); foreach (var entry in fonts) if (entry.Key == null) deadLabels.Add(entry.Key);
            foreach (Text dead in deadLabels) fonts.Remove(dead);
        }
        return changed;
    }
    private static bool IsModalText(Text text)
    {
        for (Transform p = text.transform.parent; p != null; p = p.parent)
            if (p.name == "Journey Lobby Panel" || p.name == "Journey Guide Panel" || p.name == "Journey Results Panel" || p.name == "Operations Modal" || p.name == "Language Settings Panel" || p.name == "Pause Panel") return true;
        return false;
    }
    private void LayoutHeader(Vector2 view)
    {
        foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
            if (b.transform.parent == canvas.transform && b.name.Contains("⚙")) Place(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(30, -26), new Vector2(44, 44));
        Place("Gear Icon", Vector2.one * .5f, 0, 0, 38, 38);
        menuToggle.gameObject.SetActive(IsCompact && !lastBlocked); menuToggle.interactable = !lastPour;
        Place(menuToggle.GetComponent<RectTransform>(), Vector2.one, new Vector2(-40, -26), new Vector2(64, 44));
        compactSummary.gameObject.SetActive(IsCompact && !lastBlocked);
        float cashWidth = view.x < 500 ? 104 : 140;
        Show("CASH Card", !lastBlocked || !IsCompact); Show("FANS Card", !IsCompact || view.x >= 650 && !lastBlocked); Show("TIME Card", !IsCompact);
        if (IsCompact)
        {
            Place("CASH Card", new Vector2(0, 1), 60 + cashWidth / 2, -26, cashWidth, 44);
            Show("CASH", false); Show("CASH Icon", view.x >= 500);
            Place("CASH Icon", Vector2.one * .5f, -cashWidth / 2 + 20, 0, 30, 30);
            Place("CASH Value", Vector2.one * .5f, view.x >= 500 ? 15 : 0, 0, cashWidth - (view.x >= 500 ? 34 : 8), 36);
            float left = 68 + cashWidth, available = view.x - left - 80;
            if (view.x >= 650)
            {
                Place("FANS Card", new Vector2(0, 1), left + 42, -26, 84, 44); Show("FANS", false); Show("FANS Icon", false);
                Place("FANS Value", Vector2.one * .5f, 0, 0, 76, 36); left += 92; available -= 92;
            }
            Place(compactSummary.rectTransform, new Vector2(0, 1), new Vector2(left + available / 2, -26), new Vector2(available, 44));
            compactSummary.horizontalOverflow = HorizontalWrapMode.Wrap; SetBase(compactSummary, 14);
        }
        else
        {
            float right = 12;
            foreach (string key in new[] { "TIME", "FANS", "CASH" })
            {
                float width = key == "CASH" ? 160 : 124;
                Place(key + " Card", Vector2.one, -right - width / 2, -36, width, 60); Show(key, true); Show(key + " Icon", true);
                Place(key + " Icon", Vector2.one * .5f, -width / 2 + 22, 0, 34, 34);
                Place(key, Vector2.one * .5f, 17, 14, width - 44, 25); Place(key + " Value", Vector2.one * .5f, 17, -12, width - 44, 30);
                right += width + 6;
            }
        }
        Show("Operations Live Status", false);
    }
    private bool UsesSideDock(Vector2 view) => IsCompact && view.x >= 640 && view.x / Mathf.Max(1, view.y) > 1.45f;
    private float DockWidth(Vector2 view) => UsesSideDock(view) ? Mathf.Clamp(view.x * .25f, 248, 300) : IsCompact ? view.x - 16 : Mathf.Min(860, view.x - 960);
    private void LayoutService(Vector2 view, float dock)
    {
        float width = DockWidth(view); bool side = UsesSideDock(view), shortScreen = IsCompact && view.y < 480;
        if (side) dock = view.y - 64;
        Show("Order Border", !IsCompact || !lastPour);
        Place("Order Border", new Vector2(side ? 1 : .5f, 0), side ? -8 - width / 2 : 0, 8 + dock / 2, width, dock); Place("Order Console", Vector2.one * .5f, 0, 0, width - 6, dock - 6);
        Show("Order Header", !IsCompact); Show("Queue", !shortScreen && !side); Show("Service Help", !IsCompact);
        Place("Order Header", new Vector2(.5f, 1), 0, -14, width - 24, 26);
        Place("Current Order", new Vector2(.5f, 1), 0, side ? (dock < 250 ? -30 : -42) : IsCompact ? -22 : -42, width - 24, side ? (dock < 250 ? 52 : 76) : IsCompact ? 40 : 32);
        Place("Queue", new Vector2(.5f, 1), 0, IsCompact ? -57 : -69, width - 24, 30);
        Text order = Label("Current Order"); if (order != null) order.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform panel = Node("Order Console");
        if (panel != null)
        {
            Button[] drinks = panel.GetComponentsInChildren<Button>(true); float bw = side ? width - 24 : (width - 28) / 3;
            float bh = side ? Mathf.Clamp((dock - 92) / 3 - 6, 44, 68) : IsCompact ? (shortScreen ? 48 : 60) : 44;
            for (int i = 0; i < drinks.Length; i++)
            {
                Place(drinks[i].GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(side ? 0 : (i - 1) * (bw + 6), side ? 10 + bh / 2 + (2 - i) * (bh + 6) : IsCompact ? 8 + bh / 2 : 42), new Vector2(bw, bh));
                Text text = drinks[i].GetComponentInChildren<Text>(true); Place(text.rectTransform, Vector2.one * .5f, Vector2.zero, new Vector2(bw - 8, bh - 4));
                text.horizontalOverflow = HorizontalWrapMode.Wrap; SetBase(text, 14);
            }
        }
        Place("Service Help", new Vector2(.5f, 0), 0, 12, width - 24, 25);
    }
    private void LayoutNavigation(Vector2 view, float dock)
    {
        menuPanel.gameObject.SetActive(IsCompact && menuOpen && !lastBlocked && !lastPour);
        float width = Mathf.Min(360, view.x - 16), height = Mathf.Min(280, view.y - 64);
        Place(menuPanel, Vector2.one, new Vector2(-8 - width / 2, -56 - height / 2), new Vector2(width, height));
        Place("Compact Menu Title", new Vector2(0, 1), 100, -25, 176, 44);
        Place(menuClose.GetComponent<RectTransform>(), Vector2.one, new Vector2(-26, -25), new Vector2(44, 44));
        var entries = new List<RectTransform>();
        foreach (string name in ManagementButtons) if (Node(name) != null) entries.Add(Node(name));
        entries.Add(goalsToggle.GetComponent<RectTransform>()); entries.Add(musicToggle.GetComponent<RectTransform>()); entries.Add(venueToggle.GetComponent<RectTransform>());
        for (int i = 0; i < entries.Count; i++)
        {
            RectTransform button = entries[i]; button.SetParent(IsCompact ? menuPanel : canvas.transform, false);
            bool management = i < ManagementButtons.Length; button.gameObject.SetActive(IsCompact || management);
            if (IsCompact) Place(button, new Vector2(.5f, 1), new Vector2((i % 2 == 0 ? -1 : 1) * (width - 24) / 4, -78 - (i / 2) * 52), new Vector2((width - 32) / 2, 46));
            else if (management)
            {
                float bw = (DockWidth(view) - 18) / 4;
                Place(button, new Vector2(.5f, 0), new Vector2((i - 1.5f) * (bw + 6), dock + 38), new Vector2(bw, 40));
            }
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) Place(text.rectTransform, Vector2.one * .5f, Vector2.zero, new Vector2(button.sizeDelta.x - 8, button.sizeDelta.y - 4));
        }
        Show("Staff Icon", false);
    }
    private void LayoutJourneyHUD(Vector2 view)
    {
        float width = IsCompact ? Mathf.Min(420, view.x - 16) : 310, height = 264;
        Place("Journey Night HUD", new Vector2(0, 1), 8 + width / 2, -(IsCompact ? 56 : 80) - height / 2, width, height);
        Show("Journey Night HUD", host.ReadabilityPlaying && (!IsCompact || goalsOpen && !lastPour && !lastBlocked)); goalsClose.gameObject.SetActive(IsCompact);
        Place(goalsClose.GetComponent<RectTransform>(), Vector2.one, new Vector2(-26, -25), new Vector2(44, 44));
        Place("Journey Night Title", new Vector2(0, 1), 12 + (width - 76) / 2, -23, width - 76, 34);
        Place("Journey Countdown", new Vector2(0, 1), 58, -58, 94, 44); Place("Journey Score", new Vector2(0, 1), width - 100, -58, 184, 40);
        Place("Journey Combo", new Vector2(0, 1), 74, -93, 132, 32); Show("Journey Rank", false);
        Place("Journey Fever", new Vector2(1, 1), -85, -93, 166, 32); Place("Journey Fever Track", new Vector2(.5f, 1), 0, -116, 466, 5);
        RectTransform fever = Node("Journey Fever Track"); if (fever != null) fever.localScale = new Vector3((width - 24) / 466, 1, 1);
        for (int i = 0; i < 3; i++)
        {
            float y = -143 - i * 40; Place("Journey Goal " + i, new Vector2(.5f, 1), -31, y, width - 74, 36);
            Place("Journey Goal Track " + i, new Vector2(1, 1), -34, y, 96, 8);
            RectTransform track = Node("Journey Goal Track " + i); if (track != null) track.localScale = new Vector3(.45f, 1, 1);
        }
    }
    private void LayoutSidePanels(Vector2 view, float dock)
    {
        Show("Playlist Border", !IsCompact || musicOpen); Show("Venue Panel", !IsCompact || venueOpen); Show("Day and Profit Card", !IsCompact); Show("Mood Panel", !IsCompact);
        musicClose.gameObject.SetActive(IsCompact); venueClose.gameObject.SetActive(IsCompact);
        float width = IsCompact ? Mathf.Min(360, view.x - 16) : 280, height = IsCompact ? Mathf.Min(372, view.y - 64) : 340;
        Place("Playlist Border", IsCompact ? new Vector2(1, 1) : new Vector2(1, 0), -8 - width / 2, IsCompact ? -56 - height / 2 : 200 + height / 2, width, height);
        Place("Playlist", Vector2.one * .5f, 0, 0, width - 6, height - 6); Place("Playlist Title", new Vector2(.5f, 1), -18, -25, width - 64, 40); SetBase("Playlist Title", 21);
        Show("Now Playing", height >= 360); Place("Now Playing", new Vector2(.5f, 1), 0, -58, width - 20, 28);
        RectTransform playlist = Node("Playlist");
        if (playlist != null)
        {
            var rows = new List<Button>(); foreach (Button b in playlist.GetComponentsInChildren<Button>(true)) if (b.name != "Close Playlist Button") rows.Add(b);
            float first = height >= 360 ? 92 : 72, rowHeight = IsCompact ? 44 : 36; bool columns = IsCompact && height < 330;
            for (int i = 0; i < rows.Count; i++)
            {
                float rw = columns ? (width - 24) / 2 : width - 20;
                Place(rows[i].GetComponent<RectTransform>(), new Vector2(.5f, 1), new Vector2(columns ? (i % 2 == 0 ? -1 : 1) * (rw + 4) / 2 : 0, -first - (columns ? i / 2 : i) * (rowHeight + 2)), new Vector2(rw, rowHeight));
                Text text = rows[i].GetComponentInChildren<Text>(true); Place(text.rectTransform, Vector2.one * .5f, Vector2.zero, new Vector2(rw - 8, rowHeight - 2));
                SetBase(text, 14); text.horizontalOverflow = HorizontalWrapMode.Wrap; text.alignment = TextAnchor.MiddleCenter;
                foreach (Image img in rows[i].GetComponentsInChildren<Image>(true)) if (img.transform != rows[i].transform) img.gameObject.SetActive(false);
            }
        }
        Show("Playlist Scroll Track", false);
        float mw = IsCompact ? Mathf.Min(340, view.x - 16) : 250, mh = IsCompact ? Mathf.Min(280, view.y - 64) : 252;
        Place("Venue Panel", IsCompact ? new Vector2(0, 1) : Vector2.zero, 8 + mw / 2, IsCompact ? -56 - mh / 2 : 8 + mh / 2, mw, mh);
        Place("Venue Label", new Vector2(.5f, 1), -18, -23, mw - 64, 34); Place("Mini Map", Vector2.one * .5f, 0, 0, mw - 20, mh - 86);
        Place("Map Caption", new Vector2(.5f, 0), 0, 22, mw - 16, 36);
        Place("Day and Profit Card", Vector2.zero, 356, 86, 180, 156);
        Place("Day Pill", new Vector2(.5f, 1), -40, -25, 76, 38); Place("Evening Pill", new Vector2(.5f, 1), 40, -25, 76, 38);
        Place("Day 35", Vector2.one * .5f, 0, 0, 72, 36); Place("Evening Time", Vector2.one * .5f, 0, 0, 72, 36);
        Place("Daily Cash Icon", Vector2.one * .5f, -68, 2, 32, 32); Place("Daily Cash", Vector2.one * .5f, 12, 2, 128, 52); Place("Daily Profit", Vector2.one * .5f, 0, -52, 176, 44);
        Place("Mood Panel", new Vector2(1, 0), -148, 98, 280, 180);
        for (int i = 0; i < 2; i++)
        {
            string key = i == 0 ? "GUEST SATISFACTION" : "PERFORMANCE ENERGY";
            Place(key + " Card", Vector2.one * .5f, i == 0 ? -71 : 71, 0, 136, 180); Place(key + " Icon", Vector2.one * .5f, 0, 54, 70, 50);
            Place(key, Vector2.one * .5f, 0, -4, 130, 56); Place(key + " Percent", Vector2.one * .5f, 0, -48, 126, 32);
            RectTransform back = Node(key + " Back"); if (back != null) back.localScale = new Vector3(.45f, 1, 1);
        }
        bool side = UsesSideDock(view);
        float tw = Mathf.Min(680, side ? view.x - DockWidth(view) - 48 : view.x - 24);
        Place("Toast", new Vector2(side ? 0 : .5f, 0), side ? (view.x - DockWidth(view) - 24) / 2 : 0, side ? view.y - 86 : dock + (IsCompact ? 38 : 94), tw, 48);
        Place("ToastText", Vector2.one * .5f, 0, 0, tw - 16, 44);
        if (menuOpen) menuPanel.SetAsLastSibling();
        foreach (string name in new[] { musicOpen ? "Playlist Border" : "", venueOpen ? "Venue Panel" : "", goalsOpen ? "Journey Night HUD" : "" }) if (Node(name) != null) Node(name).SetAsLastSibling();
    }
    private void LayoutPour(Vector2 view, float dock)
    {
        bool side = UsesSideDock(view);
        float width = side ? DockWidth(view) : IsCompact ? view.x - 16 : Mathf.Min(780, DockWidth(view)), height = side ? view.y - 64 : IsCompact ? dock : 164;
        if (side)
        {
            Place("Timing Pour", new Vector2(1, 0), -8 - width / 2, 8 + height / 2, width, height);
            Place("Pour Title", new Vector2(.5f, 1), 0, -72, width - 24, 64); SetBase("Pour Title", 16);
            if (Label("Pour Title") != null) Label("Pour Title").horizontalOverflow = HorizontalWrapMode.Wrap;
            Place("Pour Countdown", new Vector2(1, 1), -40, -20, 64, 34);
            Show("Pour Instruction", height >= 300); Place("Pour Instruction", new Vector2(.5f, 1), 0, -156, width - 24, 66);
            Show("Pour Key Hint", false);
            Place("Pour Timing Track", new Vector2(.5f, 0), 0, 94, 430, 44);
            RectTransform sideTrack = Node("Pour Timing Track"); if (sideTrack != null) sideTrack.localScale = new Vector3((width - 28) / 430, 1, 1);
            RectTransform sidePour = Node("Timing Pour");
            if (sidePour != null) foreach (Button b in sidePour.GetComponentsInChildren<Button>(true)) if (b.transform.parent == sidePour && b.GetComponentInChildren<Text>(true) != null)
            {
                Place(b.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(0, 36), new Vector2(width - 28, 48));
                Place(b.GetComponentInChildren<Text>(true).rectTransform, Vector2.one * .5f, Vector2.zero, new Vector2(width - 36, 44));
            }
            return;
        }
        Place("Timing Pour", new Vector2(.5f, 0), 0, IsCompact ? 8 + height / 2 : dock + 136, width, height);
        bool narrow = IsCompact && view.x < 360;
        Place("Pour Title", new Vector2(.5f, 1), -34, narrow ? -32 : -24, width - 100, narrow ? 56 : 38); SetBase("Pour Title", 16);
        if (Label("Pour Title") != null) Label("Pour Title").horizontalOverflow = HorizontalWrapMode.Wrap;
        Place("Pour Countdown", new Vector2(1, 1), -35, -24, 58, 36); Show("Pour Instruction", !narrow && (!IsCompact || view.y >= 480));
        Place("Pour Instruction", new Vector2(.5f, 1), 0, -59, width - 20, 32); Show("Pour Key Hint", !IsCompact);
        float y = IsCompact ? (view.y < 480 ? narrow ? -26 : -17 : -33) : -24;
        Place("Pour Timing Track", Vector2.one * .5f, -43, y, 430, 44);
        RectTransform track = Node("Pour Timing Track"); if (track != null) track.localScale = new Vector3(Mathf.Max(.2f, (width - 120) / 430), 1, 1);
        RectTransform pour = Node("Timing Pour");
        if (pour != null) foreach (Button b in pour.GetComponentsInChildren<Button>(true)) if (b.transform.parent == pour && b.GetComponentInChildren<Text>(true) != null)
        {
            Text text = b.GetComponentInChildren<Text>(true); Place(b.GetComponent<RectTransform>(), Vector2.one * .5f, new Vector2(width / 2 - 48, y), new Vector2(80, 48));
            Place(text.rectTransform, Vector2.one * .5f, Vector2.zero, new Vector2(74, 44));
        }
        Place("Pour Key Hint", new Vector2(.5f, 0), 0, 18, width - 20, 30);
    }
    private void LayoutScene(Vector2 view, float dock)
    {
        if (IsCompact)
        {
            SceneViewportCss = UsesSideDock(view)
                ? new Rect(8, 8, Mathf.Max(1, view.x - DockWidth(view) - 32), Mathf.Max(1, view.y - 64))
                : new Rect(0, dock + 16, view.x, Mathf.Max(1, view.y - dock - 72));
            if (Node("Journey Lobby") != null && Node("Journey Lobby").gameObject.activeSelf)
                SceneViewportCss = UsesSideDock(view)
                    ? new Rect(8, 8, Mathf.Max(1, view.x - LobbyWidth(view) - 24), view.y - 16)
                    : new Rect(0, LobbyHeight(view) + 16, view.x, Mathf.Max(1, view.y - LobbyHeight(view) - 24));
            if (venueCamera != null)
            {
                if (backgroundCamera == null)
                {
                    backgroundCamera = new GameObject("SimBar Interface Background").AddComponent<Camera>();
                    backgroundCamera.clearFlags = CameraClearFlags.SolidColor; backgroundCamera.backgroundColor = venueCamera.backgroundColor;
                    backgroundCamera.cullingMask = 0; backgroundCamera.depth = venueCamera.depth - 1;
                }
                backgroundCamera.enabled = true;
                venueCamera.rect = new Rect(SceneViewportCss.x / view.x, SceneViewportCss.y / view.y, SceneViewportCss.width / view.x, SceneViewportCss.height / view.y);
                float aspect = SceneViewportCss.width / SceneViewportCss.height;
                venueCamera.orthographicSize = Mathf.Max(8.4f, 8.4f * 1.6f / aspect);
            }
        }
        else
        {
            SceneViewportCss = new Rect(320, 220, Mathf.Max(1, view.x - 640), Mathf.Max(1, view.y - 300));
            if (backgroundCamera != null) backgroundCamera.enabled = false;
            if (venueCamera != null) { venueCamera.rect = new Rect(0, 0, 1, 1); venueCamera.orthographicSize = 8.4f; }
        }
    }
    private float LobbyWidth(Vector2 view) => Mathf.Min(400, view.x * .44f);
    private float LobbyHeight(Vector2 view) => Mathf.Min(620, view.y * .58f);
    private void FitModals(Vector2 view)
    {
        foreach (string name in new[] { "Journey Lobby Panel", "Journey Guide Panel", "Journey Results Panel", "Language Settings Panel", "Operations Modal", "Pause Panel" })
        {
            RectTransform panel = Node(name); if (panel == null) continue;
            Vector2 max = name == "Language Settings Panel" ? new Vector2(780, 660) : name == "Pause Panel" ? new Vector2(500, 380) : new Vector2(1100, 850);
            panel.localScale = Vector3.one; panel.sizeDelta = new Vector2(Mathf.Min(max.x, view.x - 16), Mathf.Min(max.y, view.y - 16));
            Place(panel, Vector2.one * .5f, Vector2.zero, panel.sizeDelta);
            if (name == "Journey Lobby Panel" && IsCompact)
            {
                if (UsesSideDock(view)) Place(panel, new Vector2(1, .5f), new Vector2(-8 - LobbyWidth(view) / 2, 0), new Vector2(LobbyWidth(view), view.y - 16));
                else Place(panel, new Vector2(.5f, 0), new Vector2(0, 8 + LobbyHeight(view) / 2), new Vector2(view.x - 16, LobbyHeight(view)));
            }
        }
        RectTransform pause = Node("Pause Panel");
        if (pause != null)
        {
            float w = pause.sizeDelta.x;
            Place("Pause Title", new Vector2(.5f, 1), 0, -28, w - 24, 44);
            Place("Pause Text", new Vector2(.5f, 1), 0, -84, w - 24, 64);
            if (Label("Pause Text") != null) Label("Pause Text").horizontalOverflow = HorizontalWrapMode.Wrap;
            Button[] buttons = pause.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Place(buttons[i].GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(0, 26 + (buttons.Length - 1 - i) * 50), new Vector2(w - 32, 44));
                Text text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null) Place(text.rectTransform, Vector2.one * .5f, Vector2.zero, new Vector2(w - 44, 40));
            }
        }
        foreach (string name in new[] { "Journey Lobby", "Journey Guide", "Journey Results" })
        {
            RectTransform overlay = Node(name); if (overlay == null) continue;
            foreach (Button b in overlay.GetComponentsInChildren<Button>(true)) if (b.name == "Journey Settings Button")
                Place(b.GetComponent<RectTransform>(), IsCompact ? new Vector2(0, 1) : Vector2.one, new Vector2(IsCompact ? 56 : -54, -28), new Vector2(96, 44));
        }
    }
    private bool BlockingOverlay()
    {
        foreach (string name in new[] { "Journey Lobby", "Journey Guide", "Journey Results", "Language Settings", "Operations Modal Overlay", "Pause Panel" }) if (Node(name) != null && Node(name).gameObject.activeSelf) return true;
        return false;
    }
    private void CloseDrawers() { menuOpen = musicOpen = venueOpen = goalsOpen = false; }
    private void OpenDrawer(int drawer)
    {
        bool wasOpen = drawer == 0 ? musicOpen : drawer == 1 ? venueOpen : goalsOpen; CloseDrawers();
        if (drawer == 0) musicOpen = !wasOpen; else if (drawer == 1) venueOpen = !wasOpen; else goalsOpen = !wasOpen; RefreshLayout();
    }
    private void CreateAuxiliaryControls()
    {
        var go = new GameObject("Compact Menu Panel", typeof(RectTransform), typeof(Image)); go.transform.SetParent(canvas.transform, false);
        go.GetComponent<Image>().color = new Color(.055f, .065f, .10f, .98f); menuPanel = go.GetComponent<RectTransform>(); nodes.Add(go.name, menuPanel);
        MakeLabel("Compact Menu Title", menuPanel, "BAR MENU", "매장 메뉴", 18);
        menuToggle = MakeButton("Compact Menu Button", canvas.transform, "Menu", "메뉴", () => { bool open = !menuOpen; CloseDrawers(); menuOpen = open; RefreshLayout(); });
        menuClose = MakeButton("Close Compact Menu Button", menuPanel, "×", "×", () => { CloseDrawers(); RefreshLayout(); });
        musicToggle = MakeButton("Compact Music Button", menuPanel, "Music", "음악", () => OpenDrawer(0));
        venueToggle = MakeButton("Compact Venue Button", menuPanel, "Venue map", "지도", () => OpenDrawer(1));
        goalsToggle = MakeButton("Compact Goals Button", menuPanel, "Night goals", "오늘의 목표", () => OpenDrawer(2));
        compactSummary = MakeLabel("Compact Journey Summary", canvas.transform, "SimBar", "SimBar", 14);
        foreach (string name in new[] { "Playlist Border", "Venue Panel", "Journey Night HUD" })
        {
            RectTransform parent = Node(name); if (parent == null) continue;
            Button close = MakeButton(name == "Playlist Border" ? "Close Playlist Button" : name == "Venue Panel" ? "Close Venue Button" : "Close Goals Button", parent, "×", "×", () => { CloseDrawers(); RefreshLayout(); });
            if (name == "Playlist Border") musicClose = close; else if (name == "Venue Panel") venueClose = close; else goalsClose = close;
            Place(close.GetComponent<RectTransform>(), Vector2.one, new Vector2(-26, -25), new Vector2(44, 44));
        }
    }
    private Text MakeLabel(string name, Transform parent, string en, string ko, int size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>(); text.font = SimBarLocalization.Font; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        SimBarLocalization.Bind(text, en, ko); nodes.Add(name, text.rectTransform); return text;
    }
    private Button MakeButton(string name, Transform parent, string en, string ko, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(.12f, .17f, .23f, .98f); Button button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(action);
        Text text = MakeLabel(name + " Label", go.transform, en, ko, 16); RectTransform label = text.rectTransform; label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(4, 2); label.offsetMax = new Vector2(-4, -2); nodes.Add(name, go.GetComponent<RectTransform>()); return button;
    }
}
