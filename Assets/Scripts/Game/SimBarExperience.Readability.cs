using UnityEngine;
using UnityEngine.UI;

public sealed partial class SimBarExperience
{
    private static readonly string[] CompactEnglishDrinks = { "Ale", "Neon Fizz", "Whiskey" };
    private static readonly string[] CompactKoreanDrinks = { "앰버 에일", "네온 피즈", "위스키" };
    private static readonly string[] CompactEnglishGoals = { "Orders", "Perfect pours", "Best combo" };
    private static readonly string[] CompactKoreanGoals = { "주문 제공", "퍼펙트", "최고 콤보" };
    public bool ReadabilityPlaying => JourneyIsPlaying && !JourneyValidationMode;

    public void RefreshReadableModalLayout()
    {
        ApplyReadableModalLayout();
        LayoutReadableSettingsModal();
    }

    public void RefreshReadableLabels(bool compact = false, bool inlineDrinks = false)
    {
        if (opStaffCaption != null)
            opStaffCaption.text = SimBarLocalization.Format("STAFF {0}", "직원 {0}명", 2 + OpHireCount);
        for (int i = 0; i < juiGoals.Length; i++)
            if (juiGoals[i] != null)
                juiGoals[i].text = (SimBarLocalization.IsKorean ? CompactKoreanGoals[i] : CompactEnglishGoals[i]) + "  " + JourneyGoalValue(i) + " / " + JourneyGoalTarget(i);
        if (juiFever != null)
            juiFever.text = JourneyFeverSeconds > 0
                ? SimBarLocalization.Format("Fever {0}s", "피버 {0}초", Mathf.CeilToInt(JourneyFeverSeconds))
                : SimBarLocalization.Format("Fever {0}%", "피버 {0}%", Mathf.RoundToInt(JourneyFever * 100));
        if (compact && orderText != null)
            orderText.text = orderQueue.Count > 0
                ? orderQueue.Peek().name.ToUpperInvariant() + ": " + DrinkNames[orderQueue.Peek().drinkIndex]
                : SimBarLocalization.Text("Waiting for orders", "주문을 기다리는 중");
        if (compact && queueText != null && orderQueue.Count > 0)
            queueText.text = SimBarLocalization.Format("Queue {0} · {1}s left", "대기 {0}명 · 남은 시간 {1}초", orderQueue.Count, Mathf.CeilToInt(orderQueue.Peek().patience));
        for (int i = 0; i < drinkButtonLabels.Length; i++)
            if (drinkButtonLabels[i] != null)
                drinkButtonLabels[i].text = compact
                    ? (SimBarLocalization.IsKorean ? CompactKoreanDrinks[i] : CompactEnglishDrinks[i]) + (inlineDrinks ? "  $" : "\n$") + DrinkPrices[i]
                    : (i + 1) + "  " + DrinkNames[i] + "  $" + DrinkPrices[i];
    }

    private static float ResponsiveModalTextHeight(Text label, float width, float minimum = 0f)
    {
        if (label == null) return minimum;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = false;
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(40f, width));
        return Mathf.Max(minimum, Mathf.Ceil(label.preferredHeight) + 6f);
    }

    private float ResponsiveModalButtonHeight(Button button, float width, float minimum = 44f)
    {
        if (button == null) return minimum;
        Text label = button.GetComponentInChildren<Text>(true);
        float height = Mathf.Max(minimum, ResponsiveModalTextHeight(label, width - 20f) + 14f);
        if (label != null) SetRect(label.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(width - 20f, height - 12f));
        return height;
    }

    private void ResponsiveModalViewport(RectTransform content, RectTransform frame, float top, float bottom)
    {
        if (content == null || frame == null) return;
        RectTransform viewport = (RectTransform)content.parent;
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        float normalized = scroll != null ? scroll.verticalNormalizedPosition : 1f;
        float height = Mathf.Max(40f, frame.rect.height - top - bottom);
        SetRect(viewport, new Vector2(0.5f, 1f), new Vector2(-5f, -top - height * 0.5f), new Vector2(frame.rect.width - 30f, height));
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport.rect.width - 12f);
        if (scroll != null && scroll.verticalScrollbar != null)
        {
            Scrollbar bar = scroll.verticalScrollbar;
            SetRect(bar.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-10f, -top - height * 0.5f), new Vector2(8f, height));
            // Scrollbar anchors provide the thumb's full dimensions. Authored
            // handle sizes otherwise add to that range and escape the track.
            if (bar.handleRect != null)
            {
                bar.handleRect.sizeDelta = Vector2.zero;
                bar.handleRect.anchoredPosition = Vector2.zero;
            }
        }
        if (scroll != null) scroll.verticalNormalizedPosition = Mathf.Clamp01(normalized);
    }

    private void ResponsiveModalTextRow(Transform parent, string name, ref float top, float width, float minimum, float gap)
    {
        Transform node = parent.Find(name);
        if (node == null) return;
        float height = ResponsiveModalTextHeight(node.GetComponent<Text>(), width, minimum);
        ReadablePlace(node, 0f, top, width, height);
        top += height + gap;
    }
}
