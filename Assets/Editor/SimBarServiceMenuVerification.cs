using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises real service transactions in an isolated, inactive edit-mode host.</summary>
public static class SimBarServiceMenuVerification
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type HostType = typeof(SimBarExperience);
    private static readonly int[] Prices = { 14, 12, 9, 14, 14 };
    private static readonly int[] PerfectTips = { 9, 8, 6, 9, 9 };
    private static readonly int[] AutomaticTips = { 6, 5, 4, 6, 6 };
    private static readonly string[] Names =
    {
        "FAUST", "ADIOS MOTHERFUCKER", "PEPPERONI HALF PIZZA", "CHILI CHEESE FRIES", "CHILI CHEESE CHIPS"
    };
    private static readonly string[] KoreanNames =
    {
        "파우스트", "아디오스 마더퍼커", "페퍼로니 피자 하프", "칠리 치즈 프라이", "칠리 치즈 칩스"
    };

    [MenuItem("SimBar/Validation/Check Service Menu Without Play Mode")]
    public static void VerifyMenu() { Debug.Log(Verify()); }

    public static string Verify()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying,
            "Service verification requires stable Edit Mode.");
        var preferences = new[]
        {
            new PreferenceSnapshot(SimBarExperience.OperationsSaveKey),
            new PreferenceSnapshot(SimBarLocalization.LanguageKey),
            new PreferenceSnapshot(SimBarExperience.OwnerPreferenceKey),
            new PreferenceSnapshot(SimBarReadability.TextSizeKey, true)
        };
        // Text()/Format() cache translations and lazily initialize the language; preserve both.
        FieldInfo localized = typeof(SimBarLocalization).GetField("initialized", StaticFlags);
        FieldInfo language = typeof(SimBarLocalization).GetField("korean", StaticFlags);
        bool initializedBefore = (bool)localized.GetValue(null);
        bool koreanBefore = (bool)language.GetValue(null);
        var translations = (Dictionary<string, string>)typeof(SimBarLocalization)
            .GetField("translations", StaticFlags).GetValue(null);
        var translationsBefore = new Dictionary<string, string>(translations);
        Scene preview = default(Scene);
        GameObject hostObject = null;
        try
        {
            preview = EditorSceneManager.NewPreviewScene();
            hostObject = new GameObject("Service Menu Verification") { hideFlags = HideFlags.HideAndDontSave };
            hostObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(hostObject, preview);
            var host = hostObject.AddComponent<SimBarExperience>();
            Check(!Read<bool>(host, "opInitialized") && Read<object>(host, "cashText") == null,
                "The isolated host must not run Awake, build UI, or enable saving.");
            Write(host, "journeyValidationMode", true);
            Write(host, "paused", false);
            Check(((ICollection)Read<object>(host, "patrons")).Count == 0,
                "No live patrons may be attached to the isolated host.");

            int[] actualPrices = (int[])HostType.GetField("DrinkPrices", StaticFlags).GetValue(null);
            string[] actualNames = (string[])HostType.GetField("EnglishDrinkNames", StaticFlags).GetValue(null);
            string[] actualKoreanNames = (string[])HostType.GetField("KoreanDrinkNames", StaticFlags).GetValue(null);
            Check(actualPrices.Length == 5 && actualNames.Length == 5 && actualKoreanNames.Length == 5,
                "The service catalog must contain exactly five items.");
            Check(((Array)HostType.GetField("DrinkColors", StaticFlags).GetValue(null)).Length == 5
                && Read<Array>(host, "orderMarkerMaterials").Length == 5
                && Read<Array>(host, "drinkButtonLabels").Length == 5,
                "Colors, order markers, and labels must cover all five catalog items.");
            for (int i = 0; i < Prices.Length; i++)
            {
                Check(actualPrices[i] == Prices[i] && actualNames[i] == Names[i]
                    && actualKoreanNames[i] == KoreanNames[i], "Catalog mismatch at item " + i + ".");
                Check(SimBarMoney.Format(Prices[i]) == "₩" + (Prices[i] * 1000).ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                    "Menu price must display credits as Korean won for item " + i + ".");
                CheckService(host, i, false, PerfectTips[i]);
                CheckService(host, i, true, AutomaticTips[i]);
                CheckWrongItem(host, i);
            }
            Check(SimBarMoney.Format(12340f) == "₩12,340,000", "Saved wallet units must retain their won display scale.");
            return "{\"passed\":true,\"catalogItems\":5,\"manualPerfectCases\":5,\"automaticCases\":5,\"wrongItemCases\":5,\"preferencesUnchanged\":true,\"playModeUsed\":false}";
        }
        finally
        {
            if (hostObject != null) Object.DestroyImmediate(hostObject);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            localized.SetValue(null, initializedBefore);
            language.SetValue(null, koreanBefore);
            translations.Clear();
            foreach (var entry in translationsBefore) translations.Add(entry.Key, entry.Value);
            bool changed = false;
            foreach (var snapshot in preferences) changed |= snapshot.RestoreOnlyIfChanged();
            if (changed)
            {
                PlayerPrefs.Save();
                throw new InvalidOperationException("Service verification unexpectedly changed player preferences; original values were restored.");
            }
        }
    }

    private static void CheckService(SimBarExperience host, int index, bool automatic, int expectedTip)
    {
        object patron = QueueOrder(host, index);
        Invoke(host, "ResolveDrinkService", index, automatic, automatic ? 0.7f : 1f);
        string context = Names[index] + (automatic ? " automatic" : " manual perfect");
        Check(Read<float>(host, "cash") == 1000f + Prices[index] + expectedTip,
            context + " must credit the exact sale and rounded tip.");
        Check(Read<int>(host, "servedTonight") == 1, context + " must count exactly one service.");
        Check(QueueCount(host) == 0 && (int)patron.GetType().GetField("drinkIndex").GetValue(patron) == -1,
            context + " must consume and clear the matching order.");
        Check(!Read<bool>(host, "opInitialized"), context + " must leave saving disabled.");
    }

    private static void CheckWrongItem(SimBarExperience host, int index)
    {
        object patron = QueueOrder(host, index);
        Invoke(host, "ResolveDrinkService", (index + 1) % Prices.Length, false, 1f);
        object queue = Read<object>(host, "orderQueue");
        Check(Read<float>(host, "cash") == 1000f && Read<int>(host, "servedTonight") == 0,
            Names[index] + " wrong-item rejection must not credit money or count a service.");
        Check(QueueCount(host) == 1 && ReferenceEquals(queue.GetType().GetMethod("Peek").Invoke(queue, null), patron)
            && (int)patron.GetType().GetField("drinkIndex").GetValue(patron) == index,
            Names[index] + " wrong-item rejection must preserve the queued order.");
    }

    private static object QueueOrder(SimBarExperience host, int index)
    {
        object queue = Read<object>(host, "orderQueue");
        queue.GetType().GetMethod("Clear").Invoke(queue, null);
        Write(host, "cash", 1000f);
        Write(host, "servedTonight", 0);
        Type patronType = HostType.GetNestedType("Patron", BindingFlags.NonPublic);
        object patron = Activator.CreateInstance(patronType, true);
        patronType.GetField("name").SetValue(patron, "Verification Guest");
        patronType.GetField("drinkIndex").SetValue(patron, index);
        patronType.GetField("patience").SetValue(patron, 30f);
        queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { patron });
        return patron;
    }

    private static int QueueCount(SimBarExperience host)
    {
        object queue = Read<object>(host, "orderQueue");
        return (int)queue.GetType().GetProperty("Count").GetValue(queue);
    }

    private static T Read<T>(object host, string name) { return (T)HostType.GetField(name, InstanceFlags).GetValue(host); }
    private static void Write(object host, string name, object value) { HostType.GetField(name, InstanceFlags).SetValue(host, value); }
    private static void Invoke(object host, string name, params object[] args)
    {
        try { HostType.GetMethod(name, InstanceFlags).Invoke(host, args); }
        catch (TargetInvocationException error) { throw new InvalidOperationException(name + " failed.", error.InnerException); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class PreferenceSnapshot
    {
        private readonly string key;
        private readonly bool exists, integer;
        private readonly string text;
        private readonly int number;
        public PreferenceSnapshot(string key, bool integer = false)
        {
            this.key = key; this.integer = integer;
            exists = PlayerPrefs.HasKey(key);
            if (integer) number = PlayerPrefs.GetInt(key);
            else text = PlayerPrefs.GetString(key);
        }
        public bool RestoreOnlyIfChanged()
        {
            bool changed = exists != PlayerPrefs.HasKey(key)
                || (exists && (integer ? number != PlayerPrefs.GetInt(key) : text != PlayerPrefs.GetString(key)));
            if (!changed) return false;
            if (!exists) PlayerPrefs.DeleteKey(key);
            else if (integer) PlayerPrefs.SetInt(key, number);
            else PlayerPrefs.SetString(key, text);
            return true;
        }
    }
}
