using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>貨幣類型（頂欄統一顯示）。</summary>
public enum CurrencyType { Gold = 0, SpiritCrystal = 1, ForgeShard = 2 }

/// <summary>
/// 遊戲事件中心：所有跨系統的通訊都走這裡，避免循環引用。
/// 原則：數據改變 → Raise 事件 → UI 只負責「被通知後重畫」，UI 之間互不認識。
/// </summary>
public static class GameEvents
{
    // ---- 資源 / 角色 / 背包 ----
    public static event Action<CurrencyType, long> OnCurrencyChanged;
    public static event Action OnHeroChanged;                 // 等級、屬性、功法變化
    public static event Action OnInventoryChanged;            // 背包內容變化
    public static event Action<EquipmentInstance> OnLoot;     // 新掉落（可做撿取動畫）
    public static event Action OnStageChanged;                // 關卡進度變化
    public static event Action<long, long> OnIdleIncome;      // 掛機產率（金幣/秒、經驗/秒）
    public static event Action<string> OnToast;               // 飄字提示

    // ---- 戰鬥（掃蕩結算）----
    public static event Action<int> OnBattleStart;            // 參數：波次
    public static event Action<bool, BattleResult> OnBattleEnd;

    // ---- Roguelite Run ----
    public static event Action OnRunStart;
    public static event Action OnRunEnd;
    public static event Action<int> OnWaveCleared;            // 參數：下一波 index
    public static event Action OnChoiceRequested;
    public static event Action<List<EquipmentInstance>> OnRunFinished;

    // ---- 觸發器 ----
    public static void RaiseCurrency(CurrencyType t, long v) => OnCurrencyChanged?.Invoke(t, v);
    public static void RaiseHero() => OnHeroChanged?.Invoke();
    public static void RaiseInventory() => OnInventoryChanged?.Invoke();
    public static void RaiseLoot(EquipmentInstance e) => OnLoot?.Invoke(e);
    public static void RaiseStage() => OnStageChanged?.Invoke();
    public static void RaiseIdleIncome(long g, long e) => OnIdleIncome?.Invoke(g, e);
    public static void Toast(string msg) => OnToast?.Invoke(msg);

    public static void RaiseBattleStart(int w) => OnBattleStart?.Invoke(w);
    public static void RaiseBattleEnd(bool win, BattleResult r) => OnBattleEnd?.Invoke(win, r);

    public static void RaiseRunStart() => OnRunStart?.Invoke();
    public static void RaiseRunEnd() => OnRunEnd?.Invoke();
    public static void RaiseWaveCleared(int nextWave) => OnWaveCleared?.Invoke(nextWave);
    public static void RaiseChoiceRequested() => OnChoiceRequested?.Invoke();
    public static void RaiseRunFinished(List<EquipmentInstance> loot) => OnRunFinished?.Invoke(loot);
}
