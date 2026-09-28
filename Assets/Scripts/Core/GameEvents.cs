using System;

/// <summary>
/// 全域事件中心（無縫耦合的 UI ↔ 邏輯通訊）。
/// </summary>
public static class GameEvents
{
    public static event Action<CurrencyType, long> OnCurrencyChanged;
    public static event Action<long, long> OnIdleIncome;
    public static event Action<EquipmentInstance> OnLootDropped;
    public static event Action OnInventoryChanged;
    public static event Action OnHeroChanged;
    public static event Action<int> OnBattleStart;
    public static event Action<bool, BattleResult> OnBattleEnd;
    public static event Action OnStageChanged;
    public static event Action<string> OnToast;
    
    // ★ Roguelite 秘境事件
    public static event Action OnRunStart;
    public static event Action<bool, BattleResult> OnRunEnd;  // 保持这个签名

    public static void RaiseCurrency(CurrencyType c, long amount) => OnCurrencyChanged?.Invoke(c, amount);
    public static void RaiseIdleIncome(long gold, long exp) => OnIdleIncome?.Invoke(gold, exp);
    public static void RaiseLoot(EquipmentInstance e) => OnLootDropped?.Invoke(e);
    public static void RaiseInventory() => OnInventoryChanged?.Invoke();
    public static void RaiseHero() => OnHeroChanged?.Invoke();
    public static void RaiseBattleStart(int stage) => OnBattleStart?.Invoke(stage);
    public static void RaiseBattleEnd(bool win, BattleResult r) => OnBattleEnd?.Invoke(win, r);
    public static void RaiseStage() => OnStageChanged?.Invoke();
    public static void Toast(string msg) => OnToast?.Invoke(msg);
    
    public static void RaiseRunStart() => OnRunStart?.Invoke();
    public static void RaiseRunEnd(bool win, BattleResult r) => OnRunEnd?.Invoke(win, r);
}