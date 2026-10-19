using System.Collections.Generic;
using UnityEngine;

// 한 판의 결과 기록 — RunResultManager가 결과 판정 시 채우고, ResultScene(ResultSceneUI)이 읽음 (GDD 8-6 #10)
// 최고 기록(도달 라운드·최단 클리어 시간)은 PlayerPrefs에 저장
public static class RunResultData
{
    public struct ModuleSnapshot
    {
        public ModuleDataSO module;
        public int x, y, width, height;
        public bool[] shape;
    }

    public static bool HasData { get; private set; }
    public static bool Cleared;
    public static int Round;
    public static int MaxRound;
    public static float CombatTime;
    public static int NormalKills;
    public static int EliteKills;
    public static float TotalDamage;
    public static float MaxHit;
    public static int Deaths;
    public static int ModulesCollected;

    public static WeaponDataSO[] Weapons = new WeaponDataSO[0];
    public static List<ModuleEffect>[] WeaponMechanisms = new List<ModuleEffect>[0]; // 무기별 장착 작동 모듈 (변형 아이콘용)
    public static int GridWidth, GridHeight;
    public static List<ModuleSnapshot> Modules = new List<ModuleSnapshot>();

    // 최고 기록 (이번 판 반영 후 값) + 이번 판에 갱신했는지
    public static int BestRound;
    public static float BestClearTime; // 0 = 클리어 기록 없음
    public static bool NewBestRound;
    public static bool NewBestClearTime;

    private const string BestRoundKey = "TDS_BEST_ROUND";
    private const string BestClearTimeKey = "TDS_BEST_CLEAR_TIME";

    public static void MarkFilled()
    {
        HasData = true;
        UpdateBestRecords();
    }

    private static void UpdateBestRecords()
    {
        int previousRound = PlayerPrefs.GetInt(BestRoundKey, 0);
        float previousClear = PlayerPrefs.GetFloat(BestClearTimeKey, 0f);

        NewBestRound = Round > previousRound;
        BestRound = Mathf.Max(Round, previousRound);

        NewBestClearTime = Cleared && (previousClear <= 0f || CombatTime < previousClear);
        BestClearTime = NewBestClearTime ? CombatTime : previousClear;

        PlayerPrefs.SetInt(BestRoundKey, BestRound);
        PlayerPrefs.SetFloat(BestClearTimeKey, BestClearTime);
        PlayerPrefs.Save();
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
