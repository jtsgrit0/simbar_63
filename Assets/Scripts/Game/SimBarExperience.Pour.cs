using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class SimBarExperience
{
    private const float PourTimeLimit = 3f;
    private const float PourTrackWidth = 430f;
    // Base widths are the forgiving Cozy setting; Challenge narrows both visible and judged zones.
    private const float PourGoodWidth = 0.40f;
    private const float PourPerfectWidth = 0.16f;
    private static readonly Color PourGold = new Color(1f, 0.77f, 0.26f);
    private static readonly Color PourGreen = new Color(0.22f, 0.81f, 0.58f);
    private static readonly Color PourSpill = new Color(0.96f, 0.43f, 0.37f);
    private static readonly AudioClip[] pourSoundCache = new AudioClip[4];

    private GameObject pourPanel;
    private Image pourPanelImage;
    private Image pourCursorImage;
    private RectTransform pourCursorRect;
    private RectTransform pourGoodRect;
    private RectTransform pourPerfectRect;
    private Text pourTitle;
    private Text pourSubtitle;
    private Text pourClockText;
    private Text pourHint;
    private Text pourStopLabel;
    private Button pourStopButton;
    private Button pourTrackButton;
    private AudioSource pourAudio;
    private Patron pourPatron;
    private int pourDrinkIndex;
    private bool pourRunning;
    private float pourElapsed;
    private float pourCursorPosition;
    private float pourTargetCenter;
    private float pourFeedbackRemaining;
    private bool pourTimedOut;
    private float pourInputGuardUntil;
    private int pourBeginFrame = -1;
    private int pourActionFrame = -1;

    public bool IsPourInProgress => pourRunning;
    public float PourCursorPosition => pourCursorPosition;
    public float PourPerfectCenter => pourTargetCenter;
    public float PourRemainingSeconds => pourRunning ? Mathf.Max(0f, PourTimeLimit - pourElapsed) : 0f;
    public float LastPourQuality { get; private set; }
    private Patron PourPatron => pourPatron;
    private float ActivePourGoodWidth => JourneyChallenge ? 0.28f : PourGoodWidth;
    private float ActivePourPerfectWidth => JourneyChallenge ? 0.09f : PourPerfectWidth;

    private void InitializePourGame()
    {
        if (pourPanel != null || canvas == null) return;
        pourPanel = CreatePanel("Timing Pour", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0f, 370f),
            new Vector2(650f, 176f), new Color(0.035f, 0.055f, 0.09f, 0.98f));
        pourPanelImage = pourPanel.GetComponent<Image>();
        pourTitle = CreateText("Pour Title", pourPanel.transform, "HIT THE GOLDEN BEAT", 21, TextAnchor.MiddleLeft, PourGold);
        SetRect(pourTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-35f, 59f), new Vector2(510f, 28f));
        pourClockText = CreateText("Pour Countdown", pourPanel.transform, "3.0s", 17, TextAnchor.MiddleRight, Color.white);
        SetRect(pourClockText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(258f, 59f), new Vector2(80f, 28f));
        pourSubtitle = CreateText("Pour Instruction", pourPanel.transform, "Green is good. Gold is perfect.", 15, TextAnchor.MiddleLeft,
            new Color(0.75f, 0.84f, 0.94f));
        SetRect(pourSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(590f, 24f));

        var track = CreatePanel("Pour Timing Track", pourPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-58f, -8f),
            new Vector2(PourTrackWidth, 34f), new Color(0.12f, 0.17f, 0.23f));
        pourTrackButton = track.AddComponent<Button>();
        pourTrackButton.targetGraphic = track.GetComponent<Image>();
        pourTrackButton.transition = Selectable.Transition.None;
        pourTrackButton.navigation = new Navigation { mode = Navigation.Mode.None };
        pourTrackButton.onClick.AddListener(StopPlayerPour);
        var good = CreatePanel("Pour Good Zone", track.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(PourTrackWidth * ActivePourGoodWidth, 30f), PourGreen);
        pourGoodRect = good.GetComponent<RectTransform>();
        good.GetComponent<Image>().raycastTarget = false;
        var perfect = CreatePanel("Pour Perfect Zone", track.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(PourTrackWidth * ActivePourPerfectWidth, 30f), PourGold);
        pourPerfectRect = perfect.GetComponent<RectTransform>();
        perfect.GetComponent<Image>().raycastTarget = false;
        var cursor = CreatePanel("Pour Cursor", track.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(5f, 46f), Color.white);
        pourCursorImage = cursor.GetComponent<Image>();
        pourCursorImage.raycastTarget = false;
        pourCursorRect = cursor.GetComponent<RectTransform>();

        pourStopButton = CreateButton("STOP", pourPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(235f, -8f),
            new Vector2(116f, 52f), new Color(0.12f, 0.51f, 0.43f), 19);
        pourStopButton.navigation = new Navigation { mode = Navigation.Mode.None };
        pourStopButton.onClick.AddListener(StopPlayerPour);
        pourStopLabel = pourStopButton.GetComponentInChildren<Text>();
        pourHint = CreateText("Pour Key Hint", pourPanel.transform, "SPACE / STOP / tap the bar   •   3 seconds to pour", 14,
            TextAnchor.MiddleCenter, new Color(0.67f, 0.77f, 0.89f));
        SetRect(pourHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -57f), new Vector2(595f, 26f));
        foreach (var text in pourPanel.GetComponentsInChildren<Text>(true))
        {
            text.font = SimBarLocalization.Font;
            text.raycastTarget = false;
        }
        RefreshPourLocalization();

        pourAudio = gameObject.AddComponent<AudioSource>();
        pourAudio.playOnAwake = false;
        pourAudio.loop = false;
        pourAudio.spatialBlend = 0f;
        pourAudio.volume = 0.22f;
        pourPanel.SetActive(false);
    }

    private void RequestPlayerPour(int drinkIndex)
    {
        if (!JourneyIsPlaying || paused || drinkIndex < 0 || drinkIndex >= DrinkNames.Length) return;
        if (pourRunning || pourActionFrame == Time.frameCount || Time.unscaledTime < pourInputGuardUntil) return;
        if (orderQueue.Count == 0)
        {
            pourActionFrame = Time.frameCount;
            pourInputGuardUntil = Time.unscaledTime + 0.15f;
            ShowToast(SimBarLocalization.Text("No orders yet. Enjoy the set!", "아직 주문이 없어요. 공연을 즐겨 보세요!"), 1.8f);
            return;
        }
        Patron patron = orderQueue.Peek();
        if (patron.drinkIndex != drinkIndex)
        {
            pourActionFrame = Time.frameCount;
            pourInputGuardUntil = Time.unscaledTime + 0.15f;
            LastPourQuality = 0f;
            PlayPourSound(3);
            CommitPlayerPour(drinkIndex, 0f);
            return;
        }
        if (pourPanel == null) InitializePourGame();
        if (pourPanel == null) return;
        pourPatron = patron;
        pourDrinkIndex = drinkIndex;
        pourElapsed = 0f;
        pourFeedbackRemaining = 0f;
        pourTimedOut = false;
        pourCursorPosition = 0.04f;
        pourTargetCenter = 0.65f + Mathf.Sin(servedTonight * 0.87f + drinkIndex * 2.1f) * 0.10f;
        pourBeginFrame = Time.frameCount;
        pourInputGuardUntil = Time.unscaledTime + 0.12f;
        pourRunning = true;
        RefreshPourLocalization();
        pourTitle.color = PourGold;
        pourPanelImage.color = new Color(0.035f, 0.055f, 0.09f, 0.98f);
        pourCursorImage.color = Color.white;
        float centerX = (pourTargetCenter - 0.5f) * PourTrackWidth;
        pourGoodRect.sizeDelta = new Vector2(PourTrackWidth * ActivePourGoodWidth, 30f);
        pourPerfectRect.sizeDelta = new Vector2(PourTrackWidth * ActivePourPerfectWidth, 30f);
        pourGoodRect.anchoredPosition = new Vector2(centerX, 0f);
        pourPerfectRect.anchoredPosition = new Vector2(centerX, 0f);
        PositionPourCursor();
        SetPourButtons(true);
        pourPanel.SetActive(true);
        PlayPourSound(0);
    }

    private void TickPourGame(float delta)
    {
        if (pourPanel == null) return;
        if (!JourneyIsPlaying)
        {
            CancelPourGame(false);
            return;
        }
        if (paused)
        {
            // The pause/menu canvas owns the screen. Keep progress, but hide this input surface.
            pourPanel.SetActive(false);
            SetPourButtons(false);
            return;
        }
        if (!pourRunning)
        {
            if (pourFeedbackRemaining > 0f)
            {
                pourFeedbackRemaining -= Mathf.Max(0f, delta);
                pourPanel.SetActive(pourFeedbackRemaining > 0f);
            }
            return;
        }
        if (!IsCurrentPourPatron())
        {
            CancelPourGame(false);
            return;
        }
        pourPanel.SetActive(true);
        SetPourButtons(true);
        pourElapsed += Mathf.Max(0f, delta);
        pourCursorPosition = Mathf.PingPong(0.04f + pourElapsed * 0.82f, 1f);
        PositionPourCursor();
        pourClockText.text = SimBarLocalization.Format("{0:0.0}s", "{0:0.0}초", Mathf.Max(0f, PourTimeLimit - pourElapsed));
        if (pourElapsed >= PourTimeLimit)
        {
            FinishPlayerPour(0.2f, true);
            return;
        }
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) StopPlayerPour();
    }

    private void StopPlayerPour()
    {
        if (!pourRunning || !JourneyIsPlaying || paused || Time.frameCount <= pourBeginFrame
            || pourActionFrame == Time.frameCount || Time.unscaledTime < pourInputGuardUntil) return;
        if (!IsCurrentPourPatron()) { CancelPourGame(false); return; }
        float distance = Mathf.Abs(pourCursorPosition - pourTargetCenter);
        float quality = distance <= ActivePourPerfectWidth * 0.5f ? 1f : distance <= ActivePourGoodWidth * 0.5f ? 0.65f : 0.2f;
        FinishPlayerPour(quality, false);
    }

    private void FinishPlayerPour(float quality, bool timedOut)
    {
        if (!pourRunning || !IsCurrentPourPatron()) { CancelPourGame(false); return; }
        // Clear the captured order BEFORE committing: repeated clicks, Space-submit and an automatic
        // bartender cannot commit this pour a second time or accidentally serve the next queued patron.
        int drink = pourDrinkIndex;
        pourRunning = false;
        pourPatron = null;
        pourActionFrame = Time.frameCount;
        pourInputGuardUntil = Time.unscaledTime + 0.15f;
        LastPourQuality = Mathf.Clamp01(quality);
        pourTimedOut = timedOut;
        pourFeedbackRemaining = 0.85f;
        Color resultColor = quality >= 0.9f ? PourGold : quality >= 0.5f ? PourGreen : PourSpill;
        RefreshPourLocalization();
        pourTitle.color = resultColor;
        pourCursorImage.color = resultColor;
        pourPanelImage.color = Color.Lerp(new Color(0.035f, 0.055f, 0.09f, 0.98f), resultColor, 0.11f);
        SetPourButtons(false);
        PlayPourSound(quality >= 0.9f ? 1 : quality >= 0.5f ? 2 : 3);
        CommitPlayerPour(drink, LastPourQuality);
    }

    // Refresh from the settings language handler without resetting an active pour.
    private void RefreshPourLocalization()
    {
        if (pourPanel == null) return;
        foreach (var text in pourPanel.GetComponentsInChildren<Text>(true)) text.font = SimBarLocalization.Font;
        if (pourRunning && pourPatron != null)
        {
            pourTitle.text = IsFood(pourDrinkIndex)
                ? SimBarLocalization.Format("PLATE {0}", "{0} 플레이팅", DrinkNames[pourDrinkIndex])
                : SimBarLocalization.Format("POUR {0}", "{0} 따르기", DrinkNames[pourDrinkIndex]);
            pourSubtitle.text = SimBarLocalization.Format("{0} is waiting. Green is good. Gold is perfect.",
                "{0} 님이 기다려요. 초록은 성공, 금색은 완벽!", pourPatron.name.ToUpperInvariant());
            pourHint.text = SimBarLocalization.Text("SPACE / STOP / tap the bar   •   hit the golden beat",
                "스페이스 / 멈춤 / 막대 클릭   •   금색 구간을 맞추세요");
            pourStopLabel.text = SimBarLocalization.Text("STOP", "멈춤");
            pourClockText.text = SimBarLocalization.Format("{0:0.0}s", "{0:0.0}초", Mathf.Max(0f, PourTimeLimit - pourElapsed));
        }
        else if (pourFeedbackRemaining > 0f)
        {
            pourTitle.text = LastPourQuality >= 0.9f ? SimBarLocalization.Text("PERFECT SERVICE!", "완벽하게 서빙했어요!")
                : LastPourQuality >= 0.5f ? SimBarLocalization.Text("GOOD SERVICE!", "잘 서빙했어요!")
                : IsFood(pourDrinkIndex) ? SimBarLocalization.Text("A LITTLE UNTIDY", "플레이팅이 조금 흐트러졌어요") : SimBarLocalization.Text("A LITTLE SPILL", "조금 흘렸어요");
            pourSubtitle.text = LastPourQuality >= 0.9f ? SimBarLocalization.Text("Right on the golden beat. Beautiful!", "금색 구간에 정확히 맞췄어요. 멋져요!")
                : LastPourQuality >= 0.5f ? SimBarLocalization.Text("Nicely served. Try the gold zone for a perfect tip.", "잘 서빙했어요. 금색 구간을 맞추면 팁이 더 늘어요.")
                : pourTimedOut ? (IsFood(pourDrinkIndex)
                    ? SimBarLocalization.Text("Plating ran late. Still served — try a quicker stop next time.", "준비가 늦었지만 서빙은 완료했어요. 다음엔 더 빨리 멈춰 보세요.")
                    : SimBarLocalization.Text("The glass overflowed. Still served — try a quicker stop next time.", "잔이 넘쳤지만 서빙은 완료했어요. 다음엔 더 빨리 멈춰 보세요."))
                : SimBarLocalization.Text("Still served. Stop inside the green zone next time.", "서빙은 완료했어요. 다음엔 초록 구간에서 멈춰 보세요.");
            pourHint.text = SimBarLocalization.Text("Choose the next item when ready.", "준비되면 다음 메뉴를 선택하세요.");
            pourClockText.text = string.Empty;
            pourStopLabel.text = SimBarLocalization.Text("SERVED", "서빙 완료");
        }
        else
        {
            pourTitle.text = SimBarLocalization.Text("HIT THE GOLDEN BEAT", "금색 구간을 맞추세요");
            pourSubtitle.text = SimBarLocalization.Text("Green is good. Gold is perfect.", "초록은 성공, 금색은 완벽!");
            pourHint.text = SimBarLocalization.Text("SPACE / STOP / tap the bar   •   3 seconds to serve",
                "스페이스 / 멈춤 / 막대 클릭   •   제한 시간 3초");
            pourStopLabel.text = SimBarLocalization.Text("STOP", "멈춤");
            pourClockText.text = SimBarLocalization.Format("{0:0.0}s", "{0:0.0}초", PourTimeLimit);
        }
    }

    private void CancelPourGame(bool penalize = false)
    {
        bool commitSpill = penalize && pourRunning && IsCurrentPourPatron() && JourneyIsPlaying && !paused;
        int drink = pourDrinkIndex;
        pourRunning = false;
        pourPatron = null;
        pourFeedbackRemaining = 0f;
        if (pourPanel != null) pourPanel.SetActive(false);
        SetPourButtons(false);
        if (!commitSpill) return;
        pourActionFrame = Time.frameCount;
        pourInputGuardUntil = Time.unscaledTime + 0.15f;
        LastPourQuality = 0.2f;
        CommitPlayerPour(drink, 0.2f);
    }

    private bool IsCurrentPourPatron()
    {
        return pourPatron != null && orderQueue.Count > 0 && ReferenceEquals(pourPatron, orderQueue.Peek());
    }

    private void PositionPourCursor()
    {
        if (pourCursorRect != null) pourCursorRect.anchoredPosition = new Vector2((pourCursorPosition - 0.5f) * PourTrackWidth, 0f);
    }

    private void SetPourButtons(bool interactable)
    {
        if (pourStopButton != null) pourStopButton.interactable = interactable;
        if (pourTrackButton != null) pourTrackButton.interactable = interactable;
    }

    private void PlayPourSound(int style)
    {
        if (pourAudio == null) return;
        AudioClip sound = pourSoundCache[style];
        if (sound == null)
        {
            const int rate = 16000;
            float duration = style == 1 ? 0.24f : 0.14f;
            var samples = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration)) * Mathf.Exp(-t * 13f);
                float tone;
                if (style == 1)
                    tone = Mathf.Sin(2f * Mathf.PI * 659.25f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 830.61f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 987.77f * t);
                else if (style == 2)
                    tone = Mathf.Sin(2f * Mathf.PI * 523.25f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 659.25f * t);
                else if (style == 3)
                    tone = Mathf.Sin(2f * Mathf.PI * (155f * t - 90f * t * t)) * 0.75f;
                else
                    tone = Mathf.Sin(2f * Mathf.PI * (420f * t + 850f * t * t)) * 0.45f;
                samples[i] = tone * envelope * 0.22f;
            }
            sound = AudioClip.Create("SimBar original pour cue " + style, samples.Length, 1, rate, false);
            sound.SetData(samples, 0);
            pourSoundCache[style] = sound;
        }
        pourAudio.PlayOneShot(sound);
    }
}
