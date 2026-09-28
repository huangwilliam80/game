using System;
using System.Collections.Generic;
using UnityEngine;

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

    // ==== 補齊 Roguelite 秘境事件 ====
    public static event Action OnRunStart;
    public static event Action OnRunEnd;
    public static event Action<RunBuffChoice> OnRunChoice;
    public static event Action<int, int> OnRunWave;
    public static event Action<int> OnWaveCleared;

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
    public static void RaiseRunEnd() => OnRunEnd?.Invoke();
    public static void RaiseRunChoice(RunBuffChoice c) => OnRunChoice?.Invoke(c);
    public static void RaiseRunWave(int current, int total) => OnRunWave?.Invoke(current, total);
    public static void RaiseWaveCleared(int nextWave) => OnWaveCleared?.Invoke(nextWave);
}

// ==== 新增 RunBuffChoice 類別 (這個專案裡沒有，所以保留在這裡) ====
public class RunBuffChoice {
    public RogueBuffDef[] options = new RogueBuffDef[3];
}