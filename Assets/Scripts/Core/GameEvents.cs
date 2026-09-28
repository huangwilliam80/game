using System;

/// <summary>
/// 全域事件中心（無縫耦合的 UI ↔ 邏輯通訊）。
/// 注意：BattleResult 與 CurrencyType 的「類型定義」已移至獨立檔案，
/// 此檔只負責事件廣播，避免 CS0101 重複定義。
/// </summary>
public static class GameEvents
{
    // 資源／經驗變動
    public static event Action<CurrencyType, long> OnCurrencyChanged;
    // 掛機收益更新
    public static event Action<long, long> OnIdleIncome;
    // 戰利品入包
    public static event Action<EquipmentInstance> OnLootDropped;
    // 背包／裝備狀態改變
    public static event Action OnInventoryChanged;
    // 角色面板改變
    public static event Action OnHeroChanged;
    // 戰鬥開始／結束
    public static event Action<int> OnBattleStart;
    public static event Action<bool, BattleResult> OnBattleEnd;
    // 關卡進度改變
    public static event Action OnStageChanged;
    // 通用提示訊息
    public static event Action<string> OnToast;

    // ★ Roguelite 秘境事件 ★
    public static event Action OnRunStart;
    public static event Action OnRunEnd;
    public static event Action<int, int> OnRunWave;          // (當前波, 總波數)
    public static event Action<RunBuffChoice> OnRunChoice;   // 三選一卡牌

    // ---- 廣播方法 ----
    public static void RaiseCurrency(CurrencyType c, long amount) => OnCurrencyChanged?.Invoke(c, amount);
    public static void RaiseIdleIncome(long gold, long exp) => OnIdleIncome?.Invoke(gold, exp);
    public static void RaiseLoot(EquipmentInstance e) => OnLootDropped?.Invoke(e);
    public static void RaiseInventory() => OnInventoryChanged?.Invoke();
    public static void RaiseHero() => OnHeroChanged?.Invoke();
    public static void RaiseBattleStart(int stage) => OnBattleStart?.Invoke(stage);
    public static void RaiseBattleEnd(bool win, BattleResult r) => OnBattleEnd?.Invoke(win, r);
    public static void RaiseStage() => OnStageChanged?.Invoke();
    public static void Toast(string msg) => OnToast?.Invoke(msg);

    // ★ 新增廣播方法 ★
    public static void RaiseRunStart() => OnRunStart?.Invoke();
    public static void RaiseRunEnd() => OnRunEnd?.Invoke();
    public static void RaiseRunWave(int cur, int total) => OnRunWave?.Invoke(cur, total);
    public static void RaiseRunChoice(RunBuffChoice c) => OnRunChoice?.Invoke(c);
}