using System;

/// <summary>
/// 全域事件中心（無縫耦合的 UI ↔ 邏輯通訊）。
/// 所有系統只透過 GameEvents 廣播 / 訂閱，避免互相 find 物件，效能最佳。
/// </summary>
public static class GameEvents
{
    // 資源／經驗變動（參數：資源類型, 新數量）
    public static event Action<CurrencyType, long> OnCurrencyChanged;
    // 掛機收益更新（每秒或每批結算時觸發）
    public static event Action<long, long> OnIdleIncome;           // (金幣/秒, 經驗/秒)
    // 戰利品入包（參數：装备实例）
    public static event Action<EquipmentInstance> OnLootDropped;
    // 背包／裝備狀態改變（需要刷新 UI）
    public static event Action OnInventoryChanged;
    // 角色面板改變（等級、功法、戰力）
    public static event Action OnHeroChanged;
    // 戰鬥開始／結束
    public static event Action<int> OnBattleStart;                 // stageId
    public static event Action<bool, BattleResult> OnBattleEnd;    // (勝利, 結果資料)
    // 關卡進度改變
    public static event Action OnStageChanged;
    // 通用提示訊息（飘字 toast）
    public static event Action<string> OnToast;

    // ---- 廣播方法（其他系統呼叫這些）----
    public static void RaiseCurrency(CurrencyType c, long amount) => OnCurrencyChanged?.Invoke(c, amount);
    public static void RaiseIdleIncome(long gold, long exp) => OnIdleIncome?.Invoke(gold, exp);
    public static void RaiseLoot(EquipmentInstance e) => OnLootDropped?.Invoke(e);
    public static void RaiseInventory() => OnInventoryChanged?.Invoke();
    public static void RaiseHero() => OnHeroChanged?.Invoke();
    public static void RaiseBattleStart(int stage) => OnBattleStart?.Invoke(stage);
    public static void RaiseBattleEnd(bool win, BattleResult r) => OnBattleEnd?.Invoke(win, r);
    public static void RaiseStage() => OnStageChanged?.Invoke();
    public static void Toast(string msg) => OnToast?.Invoke(msg);
}

public enum CurrencyType { Gold, Exp, SpiritCrystal /*靈玉*/, ForgeShard /*鍛造碎片*/ }

/// <summary>戰鬥結束時的統計，供結算面板顯示。</summary>
public class BattleResult
{
    public int stageId;
    public int waveReached;
    public float duration;
    public System.Collections.Generic.List<EquipmentInstance> loot = new();
    public long goldReward;
    public long expReward;
}
