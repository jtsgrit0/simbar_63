using UnityEngine;
using UnityEngine.UI;

/// <summary>The five-item service catalog from mobile_menu_mockup.html.</summary>
public sealed partial class SimBarExperience
{
    private static readonly string[] ServiceDescriptionsEn =
        { "Recommended solo drink", "An easy drink", "A quick snack", "Bar snack favorite", "A light bite" };
    private static readonly string[] ServiceDescriptionsKo =
        { "혼술 추천", "가벼운 한잔", "간단한 안주", "바 안주 베스트", "가볍게 먹기 좋음" };
    private GameObject serviceMenuRoot;
    private bool serviceMenuOpen;
    private bool serviceWasPaused;
    private Text serviceOwnerLabel;
    private Text serviceRequestLabel;
    private readonly Button[] serviceItemButtons = new Button[DrinkPrices.Length];
    private readonly Text[] serviceDescriptionLabels = new Text[DrinkPrices.Length];

    private static bool IsFood(int index) => index >= 2;

    private void BuildServiceMenu()
    {
        serviceMenuRoot = new GameObject("Bar Service Menu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas menuCanvas = serviceMenuRoot.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = canvas.sortingOrder + 100;
        GameObject scrim = CreatePanel("Service Menu Scrim", serviceMenuRoot.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, new Color(0.015f, 0.01f, 0.025f, 0.6f));
        StretchServiceRect((RectTransform)scrim.transform);
        scrim.AddComponent<Button>().onClick.AddListener(CloseServiceMenu);

        GameObject drawer = CreatePanel("Service Menu Drawer", serviceMenuRoot.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(430f, 700f), new Color(0.075f, 0.055f, 0.085f, 0.99f));
        drawer.GetComponent<Image>().raycastTarget = true;
        OpText("Service Menu Heading", drawer.transform, "MENU", 25, Vector2.zero, new Vector2(280f, 34f), Color.white, TextAnchor.MiddleLeft);
        Text heading = drawer.transform.Find("Service Menu Heading").GetComponent<Text>();
        SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -30f), new Vector2(280f, 36f));
        SimBarLocalization.Bind(heading, "BAR SERVICE MENU", "바 주문 서비스");
        heading.rectTransform.anchorMax = new Vector2(1f, 1f);
        heading.rectTransform.anchoredPosition = new Vector2(-25f, -30f);
        heading.rectTransform.sizeDelta = new Vector2(-90f, 36f);
        heading.resizeTextForBestFit = true; heading.resizeTextMinSize = 17; heading.resizeTextMaxSize = 25;
        heading.horizontalOverflow = HorizontalWrapMode.Wrap; heading.verticalOverflow = VerticalWrapMode.Truncate;
        Button close = CreateButton("×", drawer.transform, new Vector2(1f, 1f), new Vector2(-32f, -31f), new Vector2(44f, 44f), new Color(0.16f, 0.13f, 0.18f), 28);
        close.name = "Service Menu Close Button";
        close.onClick.AddListener(CloseServiceMenu);
        serviceOwnerLabel = CreateText("Service Menu Owner", drawer.transform, string.Empty, 16, TextAnchor.MiddleLeft, new Color(0.95f, 0.77f, 0.48f));
        SetRect(serviceOwnerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(190f, -64f), new Vector2(340f, 26f));
        serviceOwnerLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        serviceOwnerLabel.rectTransform.anchoredPosition = new Vector2(0f, -64f);
        serviceOwnerLabel.rectTransform.sizeDelta = new Vector2(-40f, 26f);
        serviceRequestLabel = CreateText("Service Menu Requested Order", drawer.transform, string.Empty, 15, TextAnchor.MiddleLeft, new Color(0.81f, 0.77f, 0.84f));
        RectTransform requestRect = serviceRequestLabel.rectTransform;
        requestRect.anchorMin = new Vector2(0f, 1f); requestRect.anchorMax = new Vector2(1f, 1f);
        requestRect.anchoredPosition = new Vector2(0f, -96f); requestRect.sizeDelta = new Vector2(-40f, 35f);
        serviceRequestLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

        GameObject viewport = CreatePanel("Service Menu Viewport", drawer.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.zero, Color.clear);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        StretchServiceRect(viewportRect);
        viewportRect.offsetMin = new Vector2(18f, 64f); viewportRect.offsetMax = new Vector2(-18f, -124f);
        viewport.AddComponent<RectMask2D>();
        var content = new GameObject("Service Menu Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f); contentRect.sizeDelta = new Vector2(0f, 536f);
        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect; scroll.content = contentRect;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35f;
        scroll.verticalNormalizedPosition = 1f;

        CreateServiceSection(contentRect, 0f, 220f, "COCKTAILS / SOLO", "칵테일 / 혼술", 0, 2);
        CreateServiceSection(contentRect, -236f, 300f, "FOOD", "푸드", 2, 3);
        Text help = CreateText("Service Menu Hint", drawer.transform, string.Empty, 13, TextAnchor.MiddleCenter, new Color(0.73f, 0.68f, 0.76f));
        SetRect(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 31f), new Vector2(350f, 42f));
        SimBarLocalization.Bind(help, "1–5 select  ·  M / P close\nChoose an item, then serve on the beat.", "1–5 메뉴 선택  ·  M / P 닫기\n메뉴를 선택하고 타이밍에 맞춰 서빙하세요.");
        help.horizontalOverflow = HorizontalWrapMode.Wrap;
        SimBarServiceMenuLayout.Attach(menuCanvas, (RectTransform)drawer.transform);
        RefreshServiceMenuLabels();
        serviceMenuRoot.SetActive(false);
    }

    private void CreateServiceSection(RectTransform parent, float top, float height, string english, string korean, int first, int count)
    {
        GameObject section = OpPanel("Service " + english, parent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height), new Color(0.12f, 0.095f, 0.14f, 1f), false);
        RectTransform rect = (RectTransform)section.transform;
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = new Vector2(0f, top);
        Text category = CreateText(english, section.transform, english, 15, TextAnchor.MiddleLeft, new Color(1f, 0.79f, 0.43f));
        SetRect(category.rectTransform, new Vector2(0f, 1f), new Vector2(153f, -24f), new Vector2(280f, 28f));
        SimBarLocalization.Bind(category, english, korean);
        for (int row = 0; row < count; row++)
        {
            int index = first + row;
            Button item = CreateButton(string.Empty, section.transform, new Vector2(0.5f, 1f), new Vector2(0f, -85f - row * 82f), new Vector2(0f, 76f), new Color(0.13f, 0.105f, 0.15f), 18);
            item.name = "Service Menu Item " + index;
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f); itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.sizeDelta = new Vector2(-16f, 76f);
            item.navigation = new Navigation { mode = Navigation.Mode.None };
            item.onClick.AddListener(() => SelectServiceItem(index));
            serviceItemButtons[index] = item;
            Text title = item.GetComponentInChildren<Text>();
            title.name = "Service Item Name " + index;
            title.alignment = TextAnchor.MiddleLeft;
            title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(-47f, -23f); title.rectTransform.sizeDelta = new Vector2(-122f, 31f);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 12; title.resizeTextMaxSize = 18;
            title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
            drinkButtonLabels[index] = title;
            Text description = CreateText("Service Item Description " + index, item.transform, string.Empty, 12, TextAnchor.MiddleLeft, new Color(0.67f, 0.62f, 0.70f));
            description.rectTransform.anchorMin = new Vector2(0f, 1f); description.rectTransform.anchorMax = new Vector2(1f, 1f);
            description.rectTransform.anchoredPosition = new Vector2(-47f, -52f); description.rectTransform.sizeDelta = new Vector2(-122f, 22f);
            description.fontStyle = FontStyle.Normal;
            serviceDescriptionLabels[index] = description;
            Text price = CreateText("Service Item Price " + index, item.transform, SimBarMoney.Format(DrinkPrices[index]), 18, TextAnchor.MiddleRight, new Color(1f, 0.78f, 0.40f));
            SetRect(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-61f, 0f), new Vector2(100f, 30f));
        }
    }

    private static void StretchServiceRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private void RefreshServiceMenuLabels()
    {
        if (serviceMenuRoot == null) return;
        foreach (Text text in serviceMenuRoot.GetComponentsInChildren<Text>(true)) { text.font = SimBarLocalization.Font; text.raycastTarget = false; }
        if (serviceOwnerLabel != null) serviceOwnerLabel.text = SelectedOwnerName;
        if (serviceRequestLabel != null) serviceRequestLabel.text = orderQueue.Count == 0
            ? SimBarLocalization.Text("No open orders", "대기 중인 주문이 없어요")
            : SimBarLocalization.Format("ORDER  /  {0}", "주문  /  {0}", DrinkNames[orderQueue.Peek().drinkIndex]);
        for (int i = 0; i < DrinkPrices.Length; i++)
        {
            if (drinkButtonLabels[i] != null) drinkButtonLabels[i].text = (i + 1) + "  " + DrinkNames[i];
            if (serviceDescriptionLabels[i] != null) serviceDescriptionLabels[i].text = SimBarLocalization.Text(ServiceDescriptionsEn[i], ServiceDescriptionsKo[i]);
            if (serviceItemButtons[i] != null) serviceItemButtons[i].GetComponent<Image>().color = orderQueue.Count > 0 && orderQueue.Peek().drinkIndex == i
                ? new Color(0.28f, 0.18f, 0.13f) : new Color(0.13f, 0.105f, 0.15f);
        }
    }

    private void OpenServiceMenu()
    {
        if (serviceMenuRoot == null || serviceMenuOpen || !JourneyIsPlaying || paused || IsPourInProgress) return;
        serviceWasPaused = paused; paused = true; serviceMenuOpen = true;
        RefreshServiceMenuLabels();
        serviceMenuRoot.SetActive(true);
    }

    private void CloseServiceMenu()
    {
        if (!serviceMenuOpen) return;
        serviceMenuOpen = false;
        serviceMenuRoot.SetActive(false);
        paused = serviceWasPaused;
    }

    private void SelectServiceItem(int index)
    {
        if (serviceMenuOpen) CloseServiceMenu();
        RequestPlayerPour(index);
    }

    private void PrepareRequestedService()
    {
        if (orderQueue.Count > 0) SelectServiceItem(orderQueue.Peek().drinkIndex);
        else RequestPlayerPour(0);
    }
}
