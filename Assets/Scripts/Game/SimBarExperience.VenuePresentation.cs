using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SimBarExperience
{
    private sealed class VenueHudGroup
    {
        internal CanvasGroup Group;
        internal float Alpha;
        internal bool Interactable;
        internal bool BlocksRaycasts;
    }

    private readonly List<VenueHudGroup> venueHudGroups = new List<VenueHudGroup>();
    private readonly List<UnityEngine.Object> venueHudArt = new List<UnityEngine.Object>();
    private RectTransform venueHudControls;
    private Image venueHudModeIcon;
    private Text venueHudModeLabel;
    private Sprite venueHudViewIcon;
    private Sprite venueHudManageIcon;
    private Transform venueHudLegacySettings;
    private bool venueHudViewing;

    private void BuildVenuePresentationControls()
    {
        if (canvas == null || venueHudControls != null) return;
        string[] panels = {
            "Venue Panel", "Staff Management Card", "Day and Profit Card",
            "Mood Panel", "Order Border", "Journey Night HUD", "CASH Card",
            "FANS Card", "TIME Card", "Operations Live Status",
            "Operations UPGRADES", "Operations GUEST INFO", "Operations EVENTS"
        };
        foreach (string panel in panels)
        {
            Transform target = canvas.transform.Find(panel);
            if (target == null) continue;
            if (panel == "Venue Panel" || panel == "Staff Management Card")
                ((RectTransform)target).anchoredPosition += new Vector2(0f, 112f);
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            venueHudGroups.Add(new VenueHudGroup { Group = group });
        }

        venueHudLegacySettings = canvas.transform.Find("\u2699 Button");
        var controls = new GameObject("Venue Presentation Controls", typeof(RectTransform));
        venueHudControls = controls.GetComponent<RectTransform>();
        venueHudControls.SetParent(canvas.transform, false);
        venueHudViewIcon = LoadVenueHudIcon("VenueControlView", 20, 12, 40, 36);
        venueHudManageIcon = LoadVenueHudIcon("VenueControlManage", 20, 12, 40, 36);
        Sprite settingsIcon = LoadVenueHudIcon("VenueControlSettings", 0, 0, 49, 45);
        Sprite ring = CreateVenueHudRing();

        Button mode = CreateVenueHudButton("Venue View Mode Button", ring, 54f, out venueHudModeIcon, out venueHudModeLabel);
        mode.onClick.AddListener(ToggleVenuePresentation);
        Button settings = CreateVenueHudButton("Venue Settings Button", ring, 164f, out Image settingsImage, out Text settingsLabel);
        settingsImage.sprite = settingsIcon;
        settingsImage.enabled = settingsIcon != null;
        SimBarLocalization.Bind(settingsLabel, "SETTINGS", "\uC124\uC815");
        settings.onClick.AddListener(OpenLanguageSettings);
        RefreshVenuePresentationControls();
    }

    private Button CreateVenueHudButton(string name, Sprite ring, float x, out Image icon, out Text label)
    {
        Button button = CreateButton(string.Empty, venueHudControls, Vector2.zero, new Vector2(x, 54f), new Vector2(100f, 100f), Color.white, 15);
        button.name = name;
        button.targetGraphic = button.GetComponent<Image>();
        button.image.sprite = ring;
        button.image.type = Image.Type.Simple;
        button.image.alphaHitTestMinimumThreshold = 0.4f;

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon = iconObject.GetComponent<Image>();
        icon.transform.SetParent(button.transform, false);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        SetRect(icon.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 14f), new Vector2(46f, 42f));

        label = button.GetComponentInChildren<Text>();
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.fontStyle = FontStyle.Bold;
        SetRect(label.rectTransform, Vector2.one * 0.5f, new Vector2(0f, -19f), new Vector2(88f, 22f));
        return button;
    }

    private void ToggleVenuePresentation()
    {
        if (!JourneyIsPlaying || paused || serviceMenuOpen || opMenuOpen || floorMissionDrawerOpen) return;
        SetVenuePresentationViewing(!venueHudViewing);
        RefreshVenuePresentationControls();
    }

    private void SetVenuePresentationViewing(bool viewing)
    {
        if (viewing == venueHudViewing) return;
        venueHudViewing = viewing;
        foreach (VenueHudGroup entry in venueHudGroups)
        {
            CanvasGroup group = entry.Group;
            if (group == null) continue;
            if (viewing)
            {
                entry.Alpha = group.alpha;
                entry.Interactable = group.interactable;
                entry.BlocksRaycasts = group.blocksRaycasts;
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            else
            {
                group.alpha = entry.Alpha;
                group.interactable = entry.Interactable;
                group.blocksRaycasts = entry.BlocksRaycasts;
            }
        }
    }

    private void RefreshVenuePresentationControls()
    {
        if (venueHudControls == null) return;
        bool visible = !JourneyValidationMode && JourneyIsPlaying && !AfterShowFarewellPending;
        if (!visible) SetVenuePresentationViewing(false);
        if (venueHudControls.gameObject.activeSelf != visible) venueHudControls.gameObject.SetActive(visible);
        if (venueHudLegacySettings != null && venueHudLegacySettings.gameObject.activeSelf == visible)
            venueHudLegacySettings.gameObject.SetActive(!visible);
        if (!visible) return;

        Rect safe = Screen.safeArea;
        RectTransform root = (RectTransform)canvas.transform;
        Vector2 inset = new Vector2(safe.xMin * root.rect.width / Mathf.Max(1, Screen.width),
            safe.yMin * root.rect.height / Mathf.Max(1, Screen.height));
        SetRect(venueHudControls, Vector2.zero, new Vector2(12f, 12f) + inset, Vector2.zero);
        venueHudModeIcon.sprite = venueHudViewing ? venueHudManageIcon : venueHudViewIcon;
        venueHudModeIcon.enabled = venueHudModeIcon.sprite != null;
        venueHudModeLabel.text = venueHudViewing
            ? SimBarLocalization.Text("MANAGE", "\uACBD\uC601\uBAA8\uB4DC")
            : SimBarLocalization.Text("VIEW", "\uAC10\uC0C1\uBAA8\uB4DC");
    }

    private Sprite LoadVenueHudIcon(string key, int x, int top, int width, int height)
    {
        TextAsset asset = Resources.Load<TextAsset>("SimBarPixelArt/" + key);
        if (asset == null) return null;
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(asset.bytes) || x + width > source.width || top + height > source.height)
        {
            Destroy(source);
            return null;
        }
        Color[] pixels = source.GetPixels(x, source.height - top - height, width, height);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            pixel.a *= Mathf.InverseLerp(0.12f, 0.38f, Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)));
            pixels[i] = pixel;
        }
        Destroy(source);
        return MakeVenueHudSprite(key, width, height, pixels);
    }

    private Sprite CreateVenueHudRing()
    {
        const int size = 100;
        var pixels = new Color[size * size];
        Color fill = new Color(0.055f, 0.067f, 0.09f, 0.95f);
        Color edge = new Color(0.50f, 0.55f, 0.62f, 1f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(50f, 50f));
            Color pixel = Color.Lerp(fill, edge, Mathf.InverseLerp(47f, 49f, radius));
            pixel.a *= 1f - Mathf.InverseLerp(49f, 50f, radius);
            pixels[y * size + x] = pixel;
        }
        return MakeVenueHudSprite("Venue Control Ring", size, size, pixels);
    }

    private Sprite MakeVenueHudSprite(string name, int width, int height, Color[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        venueHudArt.Add(sprite);
        venueHudArt.Add(texture);
        return sprite;
    }

    private void DestroyVenuePresentationArt()
    {
        foreach (UnityEngine.Object asset in venueHudArt)
            if (asset != null) Destroy(asset);
        venueHudArt.Clear();
    }
}

