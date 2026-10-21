using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Small, repeatable checks against the real running FBX performers and pub economy.</summary>
public static partial class SimBarRuntimeVerification
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed class PatronSnapshot
    {
        public object patron;
        public int drink;
        public float patience;
        public GameObject marker;
        public bool markerVisible;
        public Material markerMaterial;
    }

    [Serializable]
    public sealed class Report
    {
        public int passed;
        public List<string> failures = new List<string>();
        public List<string> measurements = new List<string>();
        public void Check(bool condition, string description)
        {
            if (condition) passed++;
            else failures.Add(description);
        }
        public override string ToString() => JsonUtility.ToJson(this, true);
    }

    [MenuItem("SimBar/Validation/Check Running Guitar Poses")]
    public static void CheckGuitarsMenu() => Debug.Log(CheckGuitars());

    public static string CheckGuitars()
    {
        var report = new Report();
        if (!Application.isPlaying)
        {
            report.failures.Add("Enter Play Mode in SimBar_Reference before checking guitar poses.");
            return report.ToString();
        }
        var performers = Object.FindObjectsByType<SimBarGuitarPerformance>(FindObjectsSortMode.None);
        report.Check(performers.Length == 2, "Exactly one guitarist and one bassist are active.");
        foreach (var performance in performers)
        {
            var visual = performance.GetComponent<SimBarCharacterVisual>();
            var model = Read<GameObject>(visual, "model");
            var clip = Read<AnimationClip>(visual, "clip");
            var bones = Read<Dictionary<string, Transform>>(visual, "bones");
            float leftUpper = Vector3.Distance(bones["upperarml"].position, bones["lowerarml"].position);
            float rightUpper = Vector3.Distance(bones["upperarmr"].position, bones["lowerarmr"].position);
            float leftLower = Vector3.Distance(bones["lowerarml"].position, bones["handl"].position);
            float rightLower = Vector3.Distance(bones["lowerarmr"].position, bones["handr"].position);
            float maxLeft = 0f, maxRight = 0f, minAngle = 180f, maxAngle = 0f;
            for (int sample = 0; sample < 12; sample++)
            {
                float time = sample * 0.31f;
                if (clip != null) clip.SampleAnimation(model, Mathf.Repeat(time, Mathf.Max(0.01f, clip.length)));
                performance.Apply(time);
                maxLeft = Mathf.Max(maxLeft, performance.LeftHandError);
                maxRight = Mathf.Max(maxRight, performance.RightHandError);
                minAngle = Mathf.Min(minAngle, performance.LeftElbowAngle, performance.RightElbowAngle);
                maxAngle = Mathf.Max(maxAngle, performance.LeftElbowAngle, performance.RightElbowAngle);
                report.Check(Finite(performance.LeftHandError) && performance.LeftHandError <= 0.025f,
                    performance.name + " left wrist follows the fretboard at sample " + sample);
                report.Check(Finite(performance.RightHandError) && performance.RightHandError <= 0.025f,
                    performance.name + " right wrist follows the strings at sample " + sample);
                report.Check(performance.Guitar.TransformDirection(Vector3.left).y > 0.1f,
                    performance.name + " guitar neck points upward at sample " + sample);
                report.Check(performance.LeftElbowAngle > 15f && performance.LeftElbowAngle < 165f
                    && performance.RightElbowAngle > 15f && performance.RightElbowAngle < 165f,
                    performance.name + " elbows remain bent without hyperextension at sample " + sample);
                report.Check(performance.LeftGripApplied && performance.RightGripApplied
                    && performance.LeftThumbApplied && performance.RightThumbApplied,
                    performance.name + " uses the actual finger and thumb bones at sample " + sample);
                report.Check(performance.LeftFingerCurl > 5f && performance.RightFingerCurl > 5f
                    && performance.LeftPalmAlignment > 0.95f && performance.RightPalmAlignment > 0.95f,
                    performance.name + " curls the fingers and aligns each palm with the guitar at sample " + sample);
                report.Check(Mathf.Abs(Vector3.Distance(bones["upperarml"].position, bones["lowerarml"].position) - leftUpper) < 0.0001f
                    && Mathf.Abs(Vector3.Distance(bones["lowerarml"].position, bones["handl"].position) - leftLower) < 0.0001f
                    && Mathf.Abs(Vector3.Distance(bones["upperarmr"].position, bones["lowerarmr"].position) - rightUpper) < 0.0001f
                    && Mathf.Abs(Vector3.Distance(bones["lowerarmr"].position, bones["handr"].position) - rightLower) < 0.0001f,
                    performance.name + " arm bones keep their authored lengths at sample " + sample);
            }
            var strap = performance.transform.Find("Performance/Leather strap over left shoulder");
            report.Check(strap != null && strap.GetComponent<MeshFilter>().sharedMesh.vertexCount == 36,
                performance.name + " has a continuous shoulder strap.");
            report.measurements.Add(performance.name + ": max wrist errors " + maxLeft.ToString("F4") + "m / "
                + maxRight.ToString("F4") + "m; elbow range " + minAngle.ToString("F1") + ".." + maxAngle.ToString("F1") + "deg");
            typeof(SimBarCharacterVisual).GetMethod("LateUpdate", PrivateInstance)?.Invoke(visual, null);
        }
        return report.ToString();
    }

    [MenuItem("SimBar/Validation/Check Running Operations")]
    public static void CheckOperationsMenu() => Debug.Log(CheckOperations());

    public static string CheckOperations()
    {
        var report = new Report();
        var host = Object.FindAnyObjectByType<SimBarExperience>();
        if (!Application.isPlaying || host == null)
        {
            report.failures.Add("Enter Play Mode in SimBar_Reference before checking operations.");
            return report.ToString();
        }
        if (host.IsPourInProgress)
        {
            report.failures.Add("Finish or cancel the current pour before checking operations.");
            return report.ToString();
        }
        bool originalValidation = Read<bool>(host, "journeyValidationMode");
        Write(host, "journeyValidationMode", true);
        Call(host, "RefreshJourneyHUD");
        string key = SimBarExperience.OperationsSaveKey;
        bool hadSave = PlayerPrefs.HasKey(key);
        string originalSave = PlayerPrefs.GetString(key, "");
        host.SaveOperations();
        string liveState = PlayerPrefs.GetString(key);
        bool originalPause = Read<bool>(host, "paused");
        bool originalMenu = Read<bool>(host, "opMenuOpen");
        bool originalMenuPause = Read<bool>(host, "opWasPaused");
        int originalPage = Read<int>(host, "opPage");
        bool originalPausePanel = Read<GameObject>(host, "pausePanel").activeSelf;
        float originalRequestTimer = Read<float>(host, "requestTimer");
        Text originalToast = Read<Text>(host, "toastText");
        string originalToastMessage = originalToast.text;
        bool originalToastVisible = originalToast.transform.parent.gameObject.activeSelf;
        float originalToastTimer = Read<float>(host, "toastTimer");
        object originalQueue = Read<object>(host, "orderQueue");
        Array originalOrders = (Array)originalQueue.GetType().GetMethod("ToArray").Invoke(originalQueue, null);
        var originalPatrons = new List<PatronSnapshot>();
        foreach (object patron in Read<System.Collections.IEnumerable>(host, "patrons"))
        {
            var fields = BindingFlags.Instance | BindingFlags.Public;
            var marker = (GameObject)patron.GetType().GetField("orderMarker", fields).GetValue(patron);
            originalPatrons.Add(new PatronSnapshot
            {
                patron = patron,
                drink = (int)patron.GetType().GetField("drinkIndex", fields).GetValue(patron),
                patience = (float)patron.GetType().GetField("patience", fields).GetValue(patron),
                marker = marker,
                markerVisible = marker != null && marker.activeSelf,
                markerMaterial = marker != null ? marker.GetComponent<Renderer>().sharedMaterial : null
            });
        }
        try
        {
            Call(host, "CloseOperations");
            Write(host, "paused", false);
            var staff = Read<int[]>(host, "opStaff");
            var upgrades = Read<int[]>(host, "opUpgrades");
            var cooldowns = Read<float[]>(host, "opCooldowns");
            Array.Clear(staff, 0, 3); Array.Clear(upgrades, 0, 3); Array.Clear(cooldowns, 0, 3);
            Write(host, "cash", 50000f); Write(host, "fans", 256);
            Write(host, "opWageDebt", 0); Write(host, "opWageTimer", 60f);
            Write(host, "opActiveEvent", -1); Write(host, "opCompletedEvents", 0);

            var staffCard = GameObject.Find("Staff Management Card").GetComponent<Button>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, staffCard.transform.position)
            };
            var hits = new List<RaycastResult>();
            Read<Canvas>(host, "canvas").GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            report.Check(hits.Exists(hit => hit.gameObject == staffCard.gameObject), "Staff menu is pointer-raycast accessible.");
            staffCard.onClick.Invoke();
            report.Check(Read<bool>(host, "opMenuOpen") && Read<bool>(host, "paused"), "Staff button opens a paused management modal.");
            float frozenTimer = Read<float>(host, "opWageTimer");
            Call(host, "TickOperations", 5f);
            report.Check(Read<float>(host, "opWageTimer") == frozenTimer, "Management menus freeze the simulation timers.");
            Call(host, "TogglePause");
            report.Check(!Read<bool>(host, "opMenuOpen") && !Read<bool>(host, "paused"), "P closes management and restores running state.");
            Call(host, "TogglePause"); Call(host, "OpenOperations", 1); Call(host, "TogglePause");
            report.Check(!Read<bool>(host, "opMenuOpen") && Read<bool>(host, "paused"), "Management preserves a pre-existing gear pause.");
            Call(host, "TogglePause");

            float money = Read<float>(host, "cash");
            report.Check(host.TryHireStaff(0) && Read<float>(host, "cash") == money - 450f && staff[0] == 1, "Hiring deducts the exact fee and creates the employee.");
            money = Read<float>(host, "cash");
            report.Check(!host.TryHireStaff(0) && Read<float>(host, "cash") == money, "An employee cannot be hired or charged twice.");
            Write(host, "cash", 0f);
            report.Check(!host.TryHireStaff(1) && staff[1] == 0 && Read<float>(host, "cash") == 0f, "Insufficient cash blocks hiring without changing state.");
            Write(host, "cash", money);
            report.Check(host.TryHireStaff(1) && host.TryHireStaff(2), "Host and promoter can be hired.");
            report.Check(!host.TryHireStaff(-1) && !host.TryHireStaff(3), "Invalid employee indices are rejected.");
            report.Check(Read<GameObject[]>(host, "opStaffVisuals")[0] != null
                && Read<GameObject[]>(host, "opStaffVisuals")[1] != null
                && Read<GameObject[]>(host, "opStaffVisuals")[2] != null, "Hired employees appear as actual venue characters.");

            int[,] costs = (int[,])typeof(SimBarExperience).GetField("OpUpgradeCosts", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            for (int facility = 0; facility < 3; facility++)
            for (int level = 0; level < 3; level++)
            {
                money = Read<float>(host, "cash");
                report.Check(host.TryPurchaseUpgrade(facility) && upgrades[facility] == level + 1
                    && Read<float>(host, "cash") == money - costs[facility, level], "Facility " + facility + " level " + (level + 1) + " has the correct cost and level.");
            }
            money = Read<float>(host, "cash");
            report.Check(!host.TryPurchaseUpgrade(0) && Read<float>(host, "cash") == money && upgrades[0] == 3, "Max-level upgrades cannot be bought or charged again.");
            upgrades[0] = 0; Write(host, "cash", 0f);
            report.Check(!host.TryPurchaseUpgrade(0) && upgrades[0] == 0, "Insufficient cash blocks facility purchases.");
            upgrades[0] = 3; Write(host, "cash", money);
            report.Check(Mathf.Abs(Property<float>(host, "OperationsRevenueMultiplier") - 1.24f) < 0.001f, "Draught facilities apply their revenue bonus.");
            report.Check(Mathf.Abs(Property<float>(host, "OperationsTipMultiplier") - 1.3f) < 0.001f, "Sound facilities apply their tip bonus.");
            report.Check(Property<int>(host, "OperationsQueueBonus") == 3 && Property<float>(host, "OperationsPatienceBonus") == 36f, "VIP facilities and a host increase queue capacity and patience.");

            Call(host, "TryCreateOrder");
            object queue = Read<object>(host, "orderQueue");
            object front = queue.GetType().GetMethod("Peek").Invoke(queue, null);
            var publicFields = BindingFlags.Instance | BindingFlags.Public;
            int drink = (int)front.GetType().GetField("drinkIndex", publicFields).GetValue(front);
            front.GetType().GetField("patience", publicFields).SetValue(front, 65f);
            int[] prices = (int[])typeof(SimBarExperience).GetField("DrinkPrices", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            money = Read<float>(host, "cash");
            Call(host, "ServeDrink", drink);
            float expectedSale = Mathf.RoundToInt(prices[drink] * 1.24f) + Mathf.RoundToInt(prices[drink] * 0.45f * 1.3f);
            report.Check(Read<float>(host, "cash") == money + expectedSale, "Actual drink sales include purchased revenue and tip bonuses.");
            int pours = Read<int>(host, "servedTonight");
            Write(host, "opAutoServeTimer", 0.1f); Write(host, "opWageTimer", 60f);
            Call(host, "TickOperations", 0.2f);
            report.Check(Read<int>(host, "servedTonight") == pours + 1, "The hired barback automatically serves a real queued order.");

            Write(host, "cash", 1000f); Write(host, "opAutoServeTimer", 12f); Write(host, "opWageTimer", 0.1f);
            Call(host, "TickOperations", 0.2f);
            report.Check(Read<float>(host, "cash") == 870f && Read<int>(host, "opWageDebt") == 0, "Payroll deducts all three wages exactly once.");
            Write(host, "cash", 0f); Write(host, "opWageTimer", 0.1f);
            Call(host, "TickOperations", 0.2f);
            report.Check(Read<int>(host, "opWageDebt") == 130 && !(bool)Call(host, "OpStaffOnDuty", 0), "Unpaid employees go off duty without a negative balance.");
            pours = Read<int>(host, "servedTonight"); Write(host, "opAutoServeTimer", 0f);
            Call(host, "TickOperations", 0.2f);
            report.Check(Read<int>(host, "servedTonight") == pours, "An unpaid barback does not keep serving.");
            Write(host, "cash", 500f); Call(host, "PayOperationsWages");
            report.Check(Read<float>(host, "cash") == 370f && Read<int>(host, "opWageDebt") == 0
                && (bool)Call(host, "OpStaffOnDuty", 0), "Settling overdue wages restores the team.");

            // Isolate event accounting from staff revenue and payroll.
            Array.Clear(staff, 0, 3); Write(host, "opWageDebt", 0); Write(host, "cash", 10000f);
            report.Check(host.TryStartEvent(0) && Read<float>(host, "cash") == 9820f && Read<float>(host, "opEventRemaining") == 60f, "Booking an event charges its fee and starts its exact duration.");
            money = Read<float>(host, "cash");
            report.Check(!host.TryStartEvent(1) && Read<float>(host, "cash") == money, "A second simultaneous event is rejected without a charge.");
            Write(host, "servedTonight", Read<int>(host, "opEventStartPours") + 6);
            int initialFans = Read<int>(host, "fans"); Call(host, "TickOperations", 60f);
            report.Check(Read<float>(host, "cash") == money + 300f && Read<int>(host, "fans") == initialFans + 12
                && Read<int>(host, "opCompletedEvents") == 1 && Read<int>(host, "opActiveEvent") == -1, "Meeting the event goal grants its cash and fan rewards.");
            money = Read<float>(host, "cash"); Call(host, "CompleteOperationsEvent");
            report.Check(Read<float>(host, "cash") == money, "A completed event cannot pay out twice.");
            report.Check(!host.TryStartEvent(0) && cooldowns[0] > 0f, "Event cooldown prevents immediate repeat booking.");
            Call(host, "TickOperations", 91f); report.Check(host.TryStartEvent(0), "An event can be booked again after its cooldown.");
            money = Read<float>(host, "cash"); Call(host, "TickOperations", 60f);
            report.Check(Read<float>(host, "cash") == money + 50f && Read<int>(host, "opCompletedEvents") == 1, "Missing the goal grants only the advertised consolation outcome, not a success.");
            report.Check(host.TryStartEvent(1), "Rock Showcase can be booked.");
            Write(host, "performance", 0.5f); Call(host, "TickOperations", 1f);
            report.Check(Property<float>(host, "OperationsTipMultiplier") > 1.3f && Read<float>(host, "performance") > 0.5f, "Rock Showcase improves tips and band energy while active.");
            Write(host, "servedTonight", Read<int>(host, "opEventStartPours") + 9); Call(host, "TickOperations", 89f);
            report.Check(Read<int>(host, "opCompletedEvents") == 2, "Rock Showcase completes its own goal.");
            report.Check(host.TryStartEvent(2), "Fan Fest can be booked.");
            initialFans = Read<int>(host, "fans"); Call(host, "TickOperations", 10f);
            report.Check(Read<int>(host, "fans") == initialFans + 2 && Property<float>(host, "OperationsOrderIntervalMultiplier") < 1f, "Fan Fest gains periodic fans and speeds up incoming orders.");

            // Save/load a running event plus purchased facilities and the current wallet.
            staff[0] = 1; host.SaveOperations();
            money = Read<float>(host, "cash"); float remaining = Read<float>(host, "opEventRemaining");
            Write(host, "cash", 0f); staff[0] = 0; upgrades[0] = 0; Write(host, "opActiveEvent", -1);
            Call(host, "LoadOperations");
            report.Check(Read<float>(host, "cash") == money && staff[0] == 1 && upgrades[0] == 3
                && Read<int>(host, "opActiveEvent") == 2 && Read<float>(host, "opEventRemaining") == remaining,
                "Save/load restores money, staff, facilities and an active event with its remaining time.");
            PlayerPrefs.SetString(key, "{\"version\":1,\"money\":-12,\"staff\":[99,-2],\"upgrades\":[99,-1,2],\"activeEvent\":99,\"cooldowns\":[-5]}");
            Call(host, "LoadOperations");
            report.Check(Read<float>(host, "cash") == 0f && staff[0] == 1 && staff[1] == 0 && staff[2] == 0
                && upgrades[0] == 3 && upgrades[1] == 0 && Read<int>(host, "opActiveEvent") == -1,
                "Malformed ranges and short save arrays are sanitized safely.");
            report.measurements.Add("3 employee roles; 9 facility purchases; 3 timed events; payroll/debt; real serving; UI pause/raycast; persistence.");
        }
        catch (Exception error)
        {
            report.failures.Add(error.GetBaseException().ToString());
        }
        finally
        {
            // Restore the pre-check live economy and the pre-check stored save; test purchases never become player progress.
            Call(host, "CloseOperations");
            PlayerPrefs.SetString(key, liveState); Call(host, "LoadOperations");
            originalQueue.GetType().GetMethod("Clear").Invoke(originalQueue, null);
            foreach (object patron in originalOrders) originalQueue.GetType().GetMethod("Enqueue").Invoke(originalQueue, new[] { patron });
            foreach (var snapshot in originalPatrons)
            {
                var fields = BindingFlags.Instance | BindingFlags.Public;
                snapshot.patron.GetType().GetField("drinkIndex", fields).SetValue(snapshot.patron, snapshot.drink);
                snapshot.patron.GetType().GetField("patience", fields).SetValue(snapshot.patron, snapshot.patience);
                if (snapshot.marker != null)
                {
                    snapshot.marker.GetComponent<Renderer>().sharedMaterial = snapshot.markerMaterial;
                    snapshot.marker.SetActive(snapshot.markerVisible);
                }
            }
            Write(host, "requestTimer", originalRequestTimer);
            Call(host, "UpdateOperationsWorldVisuals");
            Write(host, "paused", originalMenu ? originalMenuPause : originalPause);
            Read<GameObject>(host, "pausePanel").SetActive(originalPausePanel);
            if (originalMenu) Call(host, "OpenOperations", originalPage);
            Call(host, "RefreshUI");
            if (hadSave) PlayerPrefs.SetString(key, originalSave); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Write(host, "journeyValidationMode", originalValidation);
            Call(host, "RefreshJourneyHUD");
            originalToast.text = originalToastMessage;
            originalToast.transform.parent.gameObject.SetActive(originalToastVisible);
            Write(host, "toastTimer", originalToastTimer);
        }
        return report.ToString();
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static T Read<T>(object instance, string field) => (T)instance.GetType().GetField(field, PrivateInstance).GetValue(instance);
    private static T Property<T>(object instance, string property) => (T)instance.GetType().GetProperty(property, PrivateInstance).GetValue(instance);
    private static void Write(object instance, string field, object value) => instance.GetType().GetField(field, PrivateInstance).SetValue(instance, value);
    private static object Call(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, PrivateInstance).Invoke(instance, args);
}
