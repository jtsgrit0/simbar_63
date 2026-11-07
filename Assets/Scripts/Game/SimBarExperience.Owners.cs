using UnityEngine;
using UnityEngine.UI;

/// <summary>The selected proprietor is a persistent player identity, independent of the venue save.</summary>
public sealed partial class SimBarExperience
{
    public const string OwnerPreferenceKey = "SimBar.Owner";
    private static readonly string[] OwnerIds = { "female", "ff", "pet-sounds" };
    private static readonly string[] OwnerPortraits = { "OwnerFemale", "OwnerFF", "OwnerPetSounds" };
    private static readonly Color[] OwnerAccents =
    {
        new Color(1f, 0.35f, 0.59f), new Color(0.32f, 0.91f, 0.95f), new Color(1f, 0.77f, 0.35f)
    };

    private int selectedOwner;
    private readonly Button[] ownerCards = new Button[3];
    private readonly Image[] ownerTopLines = new Image[3];
    private readonly Text[] ownerNames = new Text[3];
    private readonly Text[] ownerDescriptions = new Text[3];
    private readonly Text[] ownerBadges = new Text[3];
    private Text ownerSelectionLabel;
    private bool ownerTitleHasFullscreenBackdrop;

    public int SelectedOwnerIndex { get { return selectedOwner; } }
    public string SelectedOwnerId { get { return OwnerIds[Mathf.Clamp(selectedOwner, 0, 2)]; } }
    public string SelectedOwnerName { get { return OwnerName(selectedOwner); } }

    private static string OwnerName(int index)
    {
        if (index == 1) return SimBarLocalization.Text("EDISON", "에디슨");
        if (index == 2) return SimBarLocalization.Text("JAIDEN", "제이든");
        return SimBarLocalization.Text("JESSY", "제시");
    }

    private static string OwnerDescription(int index)
    {
        if (index == 1) return SimBarLocalization.Text("Rock&Roll for life.", "평생 Rock&Roll");
        if (index == 2) return SimBarLocalization.Text("What shall we listen to tonight?", "오늘밤은 무슨 음악을 들을까?");
        return SimBarLocalization.Text("Darling, come play live tonight.", "자기야 오늘밤 라이브 하러와");
    }

    private void LoadOwnerSelection()
    {
        string saved = PlayerPrefs.GetString(OwnerPreferenceKey, OwnerIds[0]);
        selectedOwner = 0;
        for (int i = 0; i < OwnerIds.Length; i++)
            if (saved == OwnerIds[i]) { selectedOwner = i; break; }
    }

    private void SelectOwner(int index)
    {
        if (index < 0 || index >= OwnerIds.Length || !JourneyIsLobby) return;
        selectedOwner = index;
        PlayerPrefs.SetString(OwnerPreferenceKey, OwnerIds[index]);
        PlayerPrefs.Save();
        RefreshOwnerSelection();
    }

    private void BuildOwnerTitleBackdrop(Transform panel)
    {
        panel.GetComponent<Image>().color = new Color(0.025f, 0.032f, 0.052f, 1f);
        Texture2D night = SimBarWebTextures.Load("SimBarArt/Seoul/SeoulNeonNight");
        Texture2D venue = LoadPixelTexture("SimBarPixelArt/Venue");
        if (venue != null) venue.filterMode = FilterMode.Point;
        if (night != null)
        {
            ownerTitleHasFullscreenBackdrop = true;
            // Fill the whole title screen, including the area outside its card.
            // The city stays visible through the venue plate's transparent corners.
            GameObject street = new GameObject("Owner Title Fullscreen Seoul Night", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            street.transform.SetParent(panel.parent, false);
            street.transform.SetAsFirstSibling();
            RawImage streetImage = street.GetComponent<RawImage>();
            streetImage.texture = night;
            streetImage.color = new Color(0.72f, 0.72f, 0.78f, 1f);
            streetImage.raycastTarget = false;
            RectTransform streetRect = streetImage.rectTransform;
            streetRect.anchorMin = Vector2.zero;
            streetRect.anchorMax = Vector2.one;
            streetRect.offsetMin = Vector2.zero;
            streetRect.offsetMax = Vector2.zero;
            AspectRatioFitter streetFit = street.GetComponent<AspectRatioFitter>();
            streetFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            streetFit.aspectRatio = night.width / (float)night.height;
        }
        if (venue != null)
        {
            ownerTitleHasFullscreenBackdrop = true;
            GameObject room = new GameObject("Owner Title Fullscreen Venue", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            room.transform.SetParent(panel.parent, false);
            room.transform.SetSiblingIndex(night != null ? 1 : 0);
            RawImage roomImage = room.GetComponent<RawImage>();
            roomImage.texture = venue;
            roomImage.color = new Color(0.72f, 0.72f, 0.78f, 1f);
            roomImage.raycastTarget = false;
            RectTransform roomRect = roomImage.rectTransform;
            roomRect.anchorMin = Vector2.zero;
            roomRect.anchorMax = Vector2.one;
            roomRect.offsetMin = Vector2.zero;
            roomRect.offsetMax = Vector2.zero;
            AspectRatioFitter roomFit = room.GetComponent<AspectRatioFitter>();
            roomFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            roomFit.aspectRatio = venue.width / (float)venue.height;
        }
        Texture2D titleBackdrop = venue != null ? venue : night;
        if (titleBackdrop != null)
        {

            GameObject backdrop = new GameObject("Owner Title Venue Backdrop", typeof(RectTransform), typeof(RawImage));
            backdrop.transform.SetParent(panel, false);
            RawImage image = backdrop.GetComponent<RawImage>();
            image.texture = titleBackdrop;
            image.color = new Color(0.38f, 0.38f, 0.45f, 1f);
            image.raycastTarget = false;
            SetRect(image.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(1468f, 888f));
            image.uvRect = OwnerPortraitCrop(titleBackdrop, 1468f / 888f);
            GameObject shade = CreatePanel("Owner Title Ink Wash", panel, Vector2.one * 0.5f, Vector2.zero, new Vector2(1468f, 888f), new Color(0.012f, 0.018f, 0.033f, 0.79f));
            shade.GetComponent<Image>().raycastTarget = false;
        }
        for (int i = 0; i < 3; i++)
        {
            GameObject line = CreatePanel("Owner Title Neon Rule " + i, panel, Vector2.one * 0.5f, new Vector2(-450f + 450f * i, 285f), new Vector2(420f, 3f), OwnerAccents[i]);
            line.GetComponent<Image>().raycastTarget = false;
        }
    }

    private void BuildOwnerSelection(Transform panel)
    {
        LoadOwnerSelection();
        for (int i = 0; i < 3; i++)
        {
            int choice = i;
            Button card = OpButton(string.Empty, panel, Vector2.one * 0.5f, new Vector2(-450f + 450f * i, 41f), new Vector2(420f, 464f), 22);
            card.name = "Journey Owner Card " + i;
            ownerCards[i] = card;
            card.onClick.AddListener(() => SelectOwner(choice));

            GameObject portraitObject = new GameObject("Journey Owner Portrait " + i, typeof(RectTransform), typeof(RawImage));
            portraitObject.transform.SetParent(card.transform, false);
            RawImage portrait = portraitObject.GetComponent<RawImage>();
            Texture2D texture = SimBarWebTextures.Load("SimBarArt/Owners/" + OwnerPortraits[i]);
            portrait.texture = texture;
            portrait.raycastTarget = false;
            SetRect(portrait.rectTransform, Vector2.one * 0.5f, new Vector2(0f, 38f), new Vector2(400f, 350f));
            if (texture != null) portrait.uvRect = OwnerPortraitCrop(texture, 400f / 350f);

            GameObject rule = CreatePanel("Journey Owner Accent " + i, card.transform, Vector2.one * 0.5f, new Vector2(0f, 226f), new Vector2(400f, 5f), OwnerAccents[i]);
            ownerTopLines[i] = rule.GetComponent<Image>();
            ownerTopLines[i].raycastTarget = false;
            GameObject badge = CreatePanel("Journey Owner Badge " + i, card.transform, Vector2.one * 0.5f, new Vector2(-139f, 193f), new Vector2(104f, 30f), new Color(0.02f, 0.025f, 0.04f, 0.92f));
            badge.GetComponent<Image>().raycastTarget = false;
            ownerBadges[i] = OpText("Journey Owner Badge Label " + i, badge.transform, string.Empty, 15, Vector2.zero, new Vector2(98f, 28f), OwnerAccents[i], TextAnchor.MiddleCenter);
            ownerNames[i] = OpText("Journey Owner Name " + i, card.transform, string.Empty, 30, new Vector2(0f, -158f), new Vector2(394f, 44f), Color.white, TextAnchor.MiddleCenter);
            ownerNames[i].fontStyle = FontStyle.Bold;
            ownerNames[i].resizeTextForBestFit = true;
            ownerNames[i].resizeTextMinSize = 20;
            ownerNames[i].resizeTextMaxSize = 30;
            ownerDescriptions[i] = OpText("Journey Owner Description " + i, card.transform, string.Empty, 16, new Vector2(0f, -202f), new Vector2(396f, 31f), JuiMuted, TextAnchor.MiddleCenter);
        }
        ownerSelectionLabel = OpText("Journey Selected Owner", panel, string.Empty, 19, new Vector2(0f, -211f), new Vector2(1340f, 28f), JuiGold, TextAnchor.MiddleCenter);
        RefreshOwnerSelection();
    }

    private void RefreshOwnerSelection()
    {
        for (int i = 0; i < ownerCards.Length; i++)
        {
            if (ownerCards[i] == null) continue;
            bool selected = selectedOwner == i;
            ownerCards[i].GetComponent<Image>().color = selected ? Color.Lerp(new Color(0.06f, 0.07f, 0.10f), OwnerAccents[i], 0.26f) : new Color(0.055f, 0.065f, 0.095f);
            ownerTopLines[i].color = selected ? OwnerAccents[i] : new Color(0.26f, 0.29f, 0.35f);
            ownerNames[i].text = OwnerName(i);
            ownerNames[i].color = selected ? OwnerAccents[i] : Color.white;
            ownerDescriptions[i].text = OwnerDescription(i);
            ownerBadges[i].text = selected ? SimBarLocalization.Text("SELECTED", "선택됨") : "0" + (i + 1);
            ownerBadges[i].color = selected ? OwnerAccents[i] : JuiMuted;
        }
        if (ownerSelectionLabel != null)
            ownerSelectionLabel.text = SimBarLocalization.Format("YOUR OWNER  /  {0}", "선택한 사장  /  {0}", SelectedOwnerName);
    }

    private static Rect OwnerPortraitCrop(Texture2D texture, float targetAspect)
    {
        float sourceAspect = texture.width / (float)texture.height;
        if (sourceAspect > targetAspect)
        {
            float width = targetAspect / sourceAspect;
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
        float height = sourceAspect / targetAspect;
        // Keep the upper portrait, where the face is composed, when cropping tall artwork.
        return new Rect(0f, 1f - height, 1f, height);
    }
}
