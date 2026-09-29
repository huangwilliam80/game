using System;
using System.Collections.Generic;

/// <summary>
/// 全域事件总线（靜態 event）。
/// 各系統（掛機／戰鬥／背包／存檔）只負責「發事件」，UI 只負責「收事件」，
/// 兩邊完全解耦：新增面板不用改邏輯代碼。
/// ★ 注意：本檔案只放事件；SaveData / GameSave 在 GameSave.cs，這裡重複定義會 CS0101！
/// </summary>
public static class GameEvents
{
    // ---- UI 提示 ----
    public static event Action<string> OnToast;
    public static void Toast(string msg) => OnToast?.Invoke(msg);

    // ---- 資源 / 角色 ----
    public static event Action<CurrencyType, long> OnCurrencyChanged;
    public static void RaiseCurrency(CurrencyType t, long value) => OnCurrencyChanged?.Invoke(t, value);

    public static event Action OnHeroChanged;
    public static void RaiseHero() => OnHeroChanged?.Invoke();

    public static event Action OnStageChanged;
    public static void RaiseStage() => OnStageChanged?.Invoke();

    // ---- 掛機收益（每秒入帳通知，頂欄顯示 +xx/s）----
    public static event Action<long, long> OnIdleIncome;      // (金幣/秒, 經驗/秒)
    public static void RaiseIdleIncome(long g, long e) => OnIdleIncome?.Invoke(g, e);

    // ---- 背包 / 裝備 ----
    public static event Action OnInventoryChanged;
    public static void RaiseInventory() => OnInventoryChanged?.Invoke();

    public static event Action<EquipmentInstance> OnLootAcquired;
    public static void RaiseLoot(EquipmentInstance e) => OnLootAcquired?.Invoke(e);

    // ---- 自動戰鬥（波次）----
    public static event Action<int> OnBattleStart;            // 波次編號
    public static void RaiseBattleStart(int wave) => OnBattleStart?.Invoke(wave);

    public static event Action<bool, BattleResult> OnBattleEnd;
    public static void RaiseBattleEnd(bool win, BattleResult r) => OnBattleEnd?.Invoke(win, r);

    // ---- Roguelite Run ----
    public static event Action OnRunStart;
    public static void RaiseRunStart() => OnRunStart?.Invoke();

    public static event Action OnRunEnd;
    public static void RaiseRunEnd() => OnRunEnd?.Invoke();

    public static event Action<int> OnWaveCleared;            // 清完後，下一波編號
    public static void RaiseWaveCleared(int nextWave) => OnWaveCleared?.Invoke(nextWave);

    public static event Action<int, int> OnRunWave;           // (當前波, 總波數)
    public static void RaiseRunWave(int cur, int total) => OnRunWave?.Invoke(cur, total);

    /// <summary>三張 buff 卡供玩家選擇；選完回調 idx（0~2）。</summary>
    public static event Action<List<RogueBuffDef>, Action<int>> OnRunChoice;
    public static void RaiseRunChoice(List<RogueBuffDef> options, Action<int> onPick)
        => OnRunChoice?.Invoke(options, onPick);
}
