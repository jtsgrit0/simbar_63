using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Running Player Journey")]
    public static void CheckJourneyMenu() => Debug.Log(CheckJourney());

    public static string CheckJourney()
    {
        var report = new Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null || host.IsPourInProgress || Read<bool>(host, "opMenuOpen"))
        {
            report.failures.Add("Enter Play Mode and close management/pour controls before checking the player journey.");
            return report.ToString();
        }
        var snapshot = new JourneyCheckSnapshot(host);
        try
        {
            Write(host, "journeyValidationMode", false);
            Call(host, "ImportJourneyState", "");
            SetJourneyPhase(host, "Lobby"); Write(host, "paused", true);
            Write(host, "cash", 20000f); Write(host, "fans", 256);
            Write(host, "opActiveEvent", -1); Write(host, "opWageDebt", 0); Write(host, "opWageTimer", 60f);
            Array.Clear(Read<int[]>(host, "opStaff"), 0, 3);
            Array.Clear(Read<int[]>(host, "opUpgrades"), 0, 3);
            Array.Clear(Read<float[]>(host, "opCooldowns"), 0, 3);
            Call(host, "RefreshUI");
            report.Check(Property<bool>(host, "JourneyIsLobby") && Read<bool>(host, "paused"), "New players arrive in a paused lobby.");
            report.Check(!Property<bool>(host, "JourneyCanManage"), "Management cannot replace the welcome overlay.");
            float time = Property<float>(host, "JourneySecondsLeft");
            Call(host, "TickJourney", 10f);
            report.Check(Property<float>(host, "JourneySecondsLeft") == time, "The night timer does not run behind the lobby.");
            Call(host, "RequestPlayerPour", 0);
            report.Check(!host.IsPourInProgress, "Lobby input cannot start serving behind the overlay.");
            var begin = GameObject.Find("Journey Begin Button").GetComponent<Button>();
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            Read<Canvas>(host, "canvas").GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, begin.transform.position) }, hits);
            bool reachable = hits.Exists(hit => hit.gameObject == begin.gameObject);
            Image beginGraphic = begin.GetComponent<Image>();
            // Importing a fresh night can enable a previously hidden button in this same frame.
            // Unity assigns its render depth on the next render; still check screen bounds and all mask filters.
            if (!reachable && beginGraphic.depth < 0)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, begin.transform.position);
                reachable = begin.isActiveAndEnabled && begin.interactable && beginGraphic.raycastTarget
                    && new Rect(0f, 0f, Screen.width, Screen.height).Contains(point) && beginGraphic.Raycast(point, null);
            }
            report.Check(reachable, "The title's start button is pointer-accessible.");
            begin.onClick.Invoke();
            report.Check(Property<bool>(host, "JourneyIsGuide") && Read<bool>(host, "paused"), "The first night starts with an onboarding guide.");
            Call(host, "TickJourney", 10f);
            report.Check(Property<float>(host, "JourneySecondsLeft") == 180f, "Reading the guide never consumes play time.");
            for (int step = 0; step < 3; step++) Call(host, "JourneyAdvanceGuide");
            report.Check(Property<bool>(host, "JourneyIsPlaying") && !Read<bool>(host, "paused"), "Completing the guide opens the playable night.");
            report.Check(Property<int>(host, "JourneyDay") == 1 && Property<float>(host, "JourneySecondsLeft") == 180f, "A fresh night has day one and exactly three minutes.");
            Call(host, "TickJourney", 1f);
            report.Check(Property<float>(host, "JourneySecondsLeft") == 179f, "Active play advances the countdown.");
            Call(host, "TogglePause"); time = Property<float>(host, "JourneySecondsLeft"); Call(host, "TickJourney", 5f);
            report.Check(Property<float>(host, "JourneySecondsLeft") == time, "Gear pause freezes the shift clock.");
            Call(host, "TogglePause");
            Call(host, "OnApplicationFocus", false); time = Property<float>(host, "JourneySecondsLeft");
            Call(host, "TickJourney", 5f);
            report.Check(Read<bool>(host, "paused") && Property<float>(host, "JourneySecondsLeft") == time,
                "Leaving the game automatically pauses instead of consuming the shift.");
            Call(host, "TogglePause");

            for (int i = 0; i < 5; i++) Call(host, "CommitPlayerPour", FrontDrink(host), 1f);
            report.Check(Property<int>(host, "JourneyPerfects") == 5 && Property<int>(host, "JourneyBestCombo") == 5, "Five precise manual services build perfect and combo goals.");
            report.Check(Property<float>(host, "JourneyFeverSeconds") == 12f && Property<float>(host, "JourneyTipMultiplier") > 1f, "A five-pour streak activates a twelve-second FEVER bonus.");
            int combo = Property<int>(host, "JourneyCombo"); int perfects = Property<int>(host, "JourneyPerfects");
            Call(host, "ServeDrink", FrontDrink(host));
            report.Check(Property<int>(host, "JourneyCombo") == combo && Property<int>(host, "JourneyPerfects") == perfects, "Automatic staff do not manufacture manual combos or perfects.");
            Call(host, "CommitPlayerPour", FrontDrink(host), 0.2f);
            report.Check(Property<int>(host, "JourneyCombo") == 0 && Property<float>(host, "JourneyFeverSeconds") == 0f, "A spilled pour breaks the streak and FEVER.");

            // The same real button callback used by the player resolves the timed service.
            BeginCheckPour(host);
            report.Check(host.IsPourInProgress, "Choosing the correct drink opens the timing game.");
            float patience = FrontPatience(host); int orders = Property<int>(host, "JourneyOrders");
            Read<int[]>(host, "opStaff")[0] = 1; Write(host, "opAutoServeTimer", 0f);
            Call(host, "TickOperations", 0.1f); Call(host, "UpdatePatience", 2f);
            report.Check(Property<int>(host, "JourneyOrders") == orders && FrontPatience(host) == patience, "An active pour reserves its guest against staff and patience expiry.");
            Read<int[]>(host, "opStaff")[0] = 0;
            float remaining = host.PourRemainingSeconds;
            Write(host, "paused", true); Call(host, "TickPourGame", 1f);
            report.Check(host.PourRemainingSeconds == remaining && !Read<GameObject>(host, "pourPanel").activeSelf, "Pause hides and freezes the timing surface.");
            Write(host, "paused", false); Call(host, "TickPourGame", 0f);
            report.Check(Read<GameObject>(host, "pourPanel").activeSelf, "Resuming restores the reserved pour.");
            StopCheckPour(host, host.PourPerfectCenter);
            report.Check(host.LastPourQuality == 1f && !host.IsPourInProgress && Property<int>(host, "JourneyOrders") == orders + 1, "The gold timing zone serves exactly one perfect drink.");
            Call(host, "StopPlayerPour");
            report.Check(Property<int>(host, "JourneyOrders") == orders + 1, "A repeated stop cannot serve or reward a second guest.");
            BeginCheckPour(host);
            float goodPoint = host.PourPerfectCenter + (Property<float>(host, "ActivePourPerfectWidth") + Property<float>(host, "ActivePourGoodWidth")) * 0.25f;
            StopCheckPour(host, goodPoint);
            report.Check(Mathf.Abs(host.LastPourQuality - 0.65f) < 0.001f, "The green timing zone gives a good pour.");
            BeginCheckPour(host); orders = Property<int>(host, "JourneyOrders");
            Call(host, "TickPourGame", 3.1f);
            report.Check(!host.IsPourInProgress && host.LastPourQuality == 0.2f && Property<int>(host, "JourneyOrders") == orders + 1, "A timeout serves a low-quality drink without softlocking the queue.");
            BeginCheckPour(host); orders = Property<int>(host, "JourneyOrders");
            Call(host, "CancelPourGame", false);
            report.Check(!host.IsPourInProgress && Property<int>(host, "JourneyOrders") == orders, "A lifecycle cancellation does not charge or consume a guest.");
            BeginCheckPour(host); orders = Property<int>(host, "JourneyOrders");
            Call(host, "ServeDrink", FrontDrink(host)); Call(host, "TickPourGame", 0.1f);
            report.Check(!host.IsPourInProgress && Property<int>(host, "JourneyOrders") == orders + 1, "A replaced front guest cannot receive a second stale pour.");

            Call(host, "TickJourney", 7f); time = Property<float>(host, "JourneySecondsLeft");
            int score = Property<int>(host, "JourneyScore"); host.SaveOperations();
            Call(host, "ImportJourneyState", ""); Call(host, "LoadOperations");
            Call(host, "JourneyReturnToLobby");
            report.Check(Property<bool>(host, "JourneyHasActiveNight") && Read<bool>(host, "paused"), "A saved active night returns safely to the title screen.");
            Call(host, "ContinueJourney");
            report.Check(Property<bool>(host, "JourneyIsPlaying") && Property<float>(host, "JourneySecondsLeft") == time
                && Property<int>(host, "JourneyScore") == score, "Continue preserves the exact saved clock and score.");

            while (Property<int>(host, "JourneyOrders") < 12) Call(host, "CommitPlayerPour", FrontDrink(host), 1f);
            while (Property<int>(host, "JourneyPerfects") < 4 || Property<int>(host, "JourneyBestCombo") < 5)
                Call(host, "CommitPlayerPour", FrontDrink(host), 1f);
            report.Check(host.TryHireStaff(0) && host.TryPurchaseUpgrade(0), "Venue purchases remain available during a live night.");
            report.Check(host.TryStartEvent(0), "A live night supports the existing event system.");
            Write(host, "servedTonight", Read<int>(host, "opEventStartPours") + 6);
            float money = Read<float>(host, "cash"); int xp = Property<int>(host, "JourneyXP");
            Call(host, "TickJourney", 180f);
            report.Check(Property<bool>(host, "JourneyIsResults") && Read<bool>(host, "paused"), "Last call transitions to paused results.");
            report.Check(Property<int>(host, "JourneyStars") == 3 && Property<int>(host, "JourneyResultXP") > 0, "Meeting all three goals grants three stars and XP.");
            report.Check(Read<int>(host, "opActiveEvent") == -1 && Read<float>(host, "cash") == money + 300f + Property<int>(host, "JourneyResultBonus"), "Closing settles a qualifying live event and the night bonus once.");
            report.Check(Read<float[]>(host, "opCooldowns")[0] <= 90f, "An event settled at last call uses its normal post-event cooldown.");
            report.Check(Property<int>(host, "JourneyXP") > xp, "Night XP persists in the career.");
            money = Read<float>(host, "cash"); xp = Property<int>(host, "JourneyXP");
            Call(host, "FinishJourneyNight"); Call(host, "SettleJourneyNight"); host.SaveOperations(); Call(host, "LoadOperations"); Call(host, "ContinueJourney");
            report.Check(Read<float>(host, "cash") == money && Property<int>(host, "JourneyXP") == xp, "Closing, reloading and reviewing results never duplicate awards.");
            int day = Property<int>(host, "JourneyDay"); Call(host, "JourneyNextNight");
            report.Check(Property<int>(host, "JourneyDay") == day + 1 && Property<float>(host, "JourneySecondsLeft") == 180f
                && Read<float>(host, "cash") == money && Read<int[]>(host, "opStaff")[0] == 1 && Read<int[]>(host, "opUpgrades")[0] == 1,
                "The next night resets its goals while preserving cash, employees and upgrades.");
            report.Check(Property<bool>(host, "JourneyIsPlaying"), "Returning players do not repeat the completed onboarding guide.");
            Call(host, "TickJourney", 180f); day = Property<int>(host, "JourneyDay"); money = Read<float>(host, "cash");
            Call(host, "JourneyRetryNight");
            report.Check(Property<int>(host, "JourneyDay") == day && Read<float>(host, "cash") == money && Property<bool>(host, "JourneyIsPlaying"), "Retry reopens the same day without erasing the venue or paying an extra reward.");
            Call(host, "TickJourney", 180f); Call(host, "JourneyReturnToLobby"); Call(host, "JourneyStartNight", true);
            report.Check(Property<bool>(host, "JourneyChallenge") && Property<float>(host, "ActivePourPerfectWidth") < 0.10f
                && Property<float>(host, "JourneyPatienceBonus") < 0f, "Challenge mode actually tightens pour timing and guest patience.");

            // A crash after event settlement but before final night settlement has one recoverable receipt.
            Call(host, "CancelPourGame", false); Write(host, "cash", 20000f); Write(host, "opActiveEvent", -1);
            Call(host, "ImportJourneyState", "{\"version\":1,\"day\":3,\"session\":5,\"settledSession\":4,\"settlementPending\":true,\"orders\":12,\"manualPerfects\":4,\"bestCombo\":5,\"openingCash\":19000,\"tutorialComplete\":true}");
            Call(host, "SettleJourneyNight"); money = Read<float>(host, "cash"); xp = Property<int>(host, "JourneyXP");
            Call(host, "SettleJourneyNight");
            report.Check(money > 20000f && Read<float>(host, "cash") == money && Property<int>(host, "JourneyXP") == xp,
                "A pending crash-recovery receipt can finish, but never pay out twice.");
            Call(host, "ImportJourneyState", "{\"version\":1,\"day\":-10,\"secondsLeft\":9999,\"score\":-1,\"session\":1,\"inProgress\":true,\"feverSeconds\":999}");
            report.Check(Property<int>(host, "JourneyDay") == 1 && Property<float>(host, "JourneySecondsLeft") == 180f
                && Property<int>(host, "JourneyScore") == 0 && Property<float>(host, "JourneyFeverSeconds") == 12f,
                "Out-of-range saved career values are sanitized.");
            report.measurements.Add("Welcome/guide/continue; timing-button service; staff reservation; combos/FEVER; 180s close; stars/XP; next/retry; atomic recovery.");
        }
        catch (Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally { snapshot.Restore(host); }
        return report.ToString();
    }

    private static int FrontDrink(SimBarExperience host)
    {
        object queue = Read<object>(host, "orderQueue");
        object patron = queue.GetType().GetMethod("Peek").Invoke(queue, null);
        return (int)patron.GetType().GetField("drinkIndex").GetValue(patron);
    }

    private static float FrontPatience(SimBarExperience host)
    {
        object queue = Read<object>(host, "orderQueue");
        object patron = queue.GetType().GetMethod("Peek").Invoke(queue, null);
        return (float)patron.GetType().GetField("patience").GetValue(patron);
    }

    private static void BeginCheckPour(SimBarExperience host)
    {
        Write(host, "pourActionFrame", -10); Write(host, "pourInputGuardUntil", -1f);
        Call(host, "RequestPlayerPour", FrontDrink(host));
    }

    private static void StopCheckPour(SimBarExperience host, float cursor)
    {
        Write(host, "pourBeginFrame", -10); Write(host, "pourActionFrame", -10); Write(host, "pourInputGuardUntil", -1f);
        Write(host, "pourCursorPosition", cursor);
        Read<Button>(host, "pourStopButton").onClick.Invoke();
    }

    private static void SetJourneyPhase(SimBarExperience host, string phase)
    {
        var field = typeof(SimBarExperience).GetField("journeyPhase", PrivateInstance);
        field.SetValue(host, Enum.Parse(field.FieldType, phase));
    }

    private sealed class JourneyCheckSnapshot
    {
        private readonly bool hadSave, paused, validation;
        private readonly string saved, live;
        private readonly object phase, queue;
        private readonly Array orders;
        private readonly float requestTimer;
        private readonly string toastMessage;
        private readonly bool toastVisible;
        private readonly float toastTimer;
        private readonly List<PatronSnapshot> patrons = new List<PatronSnapshot>();
        public JourneyCheckSnapshot(SimBarExperience host)
        {
            string key = SimBarExperience.OperationsSaveKey;
            hadSave = PlayerPrefs.HasKey(key); saved = PlayerPrefs.GetString(key, "");
            host.SaveOperations(); live = PlayerPrefs.GetString(key);
            paused = Read<bool>(host, "paused"); validation = Read<bool>(host, "journeyValidationMode"); phase = Read<object>(host, "journeyPhase");
            requestTimer = Read<float>(host, "requestTimer"); queue = Read<object>(host, "orderQueue");
            Text toast = Read<Text>(host, "toastText"); toastMessage = toast.text;
            toastVisible = toast.transform.parent.gameObject.activeSelf; toastTimer = Read<float>(host, "toastTimer");
            orders = (Array)queue.GetType().GetMethod("ToArray").Invoke(queue, null);
            foreach (object patron in Read<System.Collections.IEnumerable>(host, "patrons"))
            {
                var marker = (GameObject)patron.GetType().GetField("orderMarker").GetValue(patron);
                patrons.Add(new PatronSnapshot { patron = patron, drink = (int)patron.GetType().GetField("drinkIndex").GetValue(patron),
                    patience = (float)patron.GetType().GetField("patience").GetValue(patron), marker = marker, markerVisible = marker != null && marker.activeSelf,
                    markerMaterial = marker != null ? marker.GetComponent<Renderer>().sharedMaterial : null });
            }
        }
        public void Restore(SimBarExperience host)
        {
            Call(host, "CancelPourGame", false); Call(host, "CloseOperations");
            string key = SimBarExperience.OperationsSaveKey;
            PlayerPrefs.SetString(key, live); Call(host, "LoadOperations");
            queue.GetType().GetMethod("Clear").Invoke(queue, null);
            foreach (object patron in orders) queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { patron });
            foreach (var state in patrons)
            {
                state.patron.GetType().GetField("drinkIndex").SetValue(state.patron, state.drink);
                state.patron.GetType().GetField("patience").SetValue(state.patron, state.patience);
                if (state.marker != null) { state.marker.GetComponent<Renderer>().sharedMaterial = state.markerMaterial; state.marker.SetActive(state.markerVisible); }
            }
            Write(host, "journeyPhase", phase); Write(host, "paused", paused); Write(host, "journeyValidationMode", validation); Write(host, "requestTimer", requestTimer);
            Read<GameObject>(host, "pausePanel").SetActive(paused && Property<bool>(host, "JourneyIsPlaying"));
            Call(host, "UpdateOperationsWorldVisuals"); Call(host, "RefreshUI");
            Text toast = Read<Text>(host, "toastText"); toast.text = toastMessage;
            toast.transform.parent.gameObject.SetActive(toastVisible); Write(host, "toastTimer", toastTimer);
            if (hadSave) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
