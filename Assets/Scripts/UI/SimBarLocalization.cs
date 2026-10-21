using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared language preference and live bindings for the runtime interface.</summary>
public static class SimBarLocalization
{
    public const string LanguageKey = "SimBar.Language";
    private static bool initialized;
    private static bool korean;
    private static Font koreanFont;
    private static Font englishFont;
    public static event Action Changed;

    private sealed class Binding
    {
        public Text label;
        public string english;
        public string korean;
        public string applied;
    }

    private static readonly List<Binding> bindings = new List<Binding>();
    private static readonly Dictionary<string, string> translations = new Dictionary<string, string>
    {
        { "CASH", "현금" }, { "FANS", "팬" }, { "TIME", "시간" },
        { "♪  PLAYLIST", "♪  재생 목록" }, { "BAR SERVICE  •  LIVE QUEUE", "바 서비스  •  주문 대기열" },
        { "GUEST SATISFACTION", "손님 만족도" }, { "PERFORMANCE ENERGY", "공연 에너지" },
        { "SERVICE PAUSED", "일시정지" },
        { "Tap the gear or press P to return to the bar.", "설정 버튼이나 P 키를 누르면 영업으로 돌아갑니다." },
        { "RETURN TO THE FLOOR", "영업으로 돌아가기" },
        { "YOUR BAR. YOUR CROWD. ONE GREAT NIGHT.", "나만의 바, 나만의 손님, 멋진 하룻밤." },
        { "COZY NIGHT", "여유로운 밤" }, { "CHALLENGE NIGHT", "도전의 밤" },
        { "3 min / 12 orders / 4 perfects / x5 combo\nLonger guest patience. A gentler pace.", "3분 / 주문 12개 / 퍼펙트 4회 / 5콤보\n손님들이 더 오래 기다리는 여유로운 모드." },
        { "3 min / 20 orders / 7 perfects / x8 combo\nFaster orders. Less time per guest.", "3분 / 주문 20개 / 퍼펙트 7회 / 8콤보\n주문이 빠르게 들어오고 대기 시간이 짧은 모드." },
        { "CONTINUE SAVED NIGHT", "저장한 영업 이어하기" }, { "OPEN TONIGHT", "오늘의 영업 시작" },
        { "Your venue, hired staff and upgrades stay with you.", "매장, 고용한 직원, 업그레이드는 계속 유지됩니다." },
        { "1 / 2 / 3 CHOOSE DRINK     SPACE / STOP POUR     P PAUSE", "1 / 2 / 3 음료 선택     SPACE / 멈춤으로 따르기     P 일시정지" },
        { "Take your time. The doors open after this guide.", "천천히 살펴보세요. 안내가 끝나면 영업을 시작합니다." },
        { "NIGHT PROFIT", "오늘의 수익" }, { "GUESTS SERVED", "응대한 손님" }, { "XP EARNED", "획득 경험치" }, { "STAR", "별" },
        { "RETRY THIS NIGHT", "같은 날 다시 도전" }, { "NEXT NIGHT", "다음 날 영업" }, { "BACK TO LOBBY", "로비로 돌아가기" },
        { "PROGRESS SAVED  /  YOUR VENUE CARRIES INTO THE NEXT NIGHT", "진행 상황 저장 완료  /  매장 상태는 다음 날에도 유지됩니다." },
        { "SAVE & LOBBY", "저장 후 로비로" }, { "NEXT", "다음" },
        { "SETTINGS", "설정" }, { "LANGUAGE", "언어" }, { "CLOSE", "닫기" },
        { "Changes apply immediately and are saved for next time.", "언어는 즉시 적용되며 다음 실행에도 유지됩니다." },
        { "UPGRADES", "업그레이드" }, { "GUEST INFO", "손님 정보" }, { "EVENTS", "이벤트" },
        { "SIMBAR  /  HOUSE MANAGEMENT", "SIMBAR  /  매장 관리" }, { "CLOSE  X", "닫기  X" },
        { "STAFF", "직원" }, { "FACILITIES", "시설" },
        { "AUTO-SAVED  /  P OR CLOSE TO RETURN TO THE FLOOR", "자동 저장  /  P 키 또는 닫기로 매장에 복귀" }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        initialized = false;
        koreanFont = null;
        englishFont = null;
        bindings.Clear();
        Changed = null;
    }

    public static bool IsKorean
    {
        get
        {
            if (!initialized)
            {
                korean = PlayerPrefs.GetString(LanguageKey, "ko") != "en";
                initialized = true;
            }
            return korean;
        }
    }

    public static Font Font
    {
        get
        {
            if (IsKorean)
            {
                if (koreanFont == null) koreanFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
                if (koreanFont != null) return koreanFont;
            }
            if (englishFont == null) englishFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return englishFont;
        }
    }

    public static string Text(string english, string koreanText)
    {
        translations[english] = koreanText;
        return IsKorean ? koreanText : english;
    }

    public static string Format(string english, string koreanText, params object[] args)
    {
        return string.Format(Text(english, koreanText), args);
    }

    public static void Bind(Text label, string source)
    {
        if (translations.TryGetValue(source, out string translated)) Bind(label, source, translated);
        else
        {
            // Explicitly translated labels may already contain the selected language.
            foreach (var entry in translations)
                if (entry.Value == source) { Bind(label, entry.Key, entry.Value); return; }
            label.text = source;
        }
    }

    public static void Bind(Text label, string english, string koreanText)
    {
        if (label == null) return;
        translations[english] = koreanText;
        Binding binding = bindings.Find(item => item.label == label);
        if (binding == null)
        {
            binding = new Binding { label = label };
            bindings.Add(binding);
        }
        binding.english = english;
        binding.korean = koreanText;
        binding.applied = IsKorean ? koreanText : english;
        label.font = Font;
        label.text = binding.applied;
    }

    public static void SetLanguage(bool useKorean)
    {
        bool previous = IsKorean;
        korean = useKorean;
        PlayerPrefs.SetString(LanguageKey, useKorean ? "ko" : "en");
        PlayerPrefs.Save();
        if (previous == useKorean) return;
        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            Binding binding = bindings[i];
            if (binding.label == null) { bindings.RemoveAt(i); continue; }
            binding.label.font = Font;
            // A gameplay refresh may have replaced an initial placeholder.
            if (binding.label.text != binding.applied) continue;
            binding.applied = useKorean ? binding.korean : binding.english;
            binding.label.text = binding.applied;
        }
        Changed?.Invoke();
    }
}
