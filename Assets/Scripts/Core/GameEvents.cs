using System;
using System.Collections.Generic;
using UnityEngine;

public enum CurrencyType { Gold, Crystal, Shard }

/// <summary>
/// 遊戲事件中心：所有跨系統的通訊都走這裡，避免循環引用。
/// </summary>
public static class GameEvents
{
    // ---- 原有事件 ----
    public static event Action<int> OnBattleStart;       // 參數: wave num
    public static event Action OnStage;
    public static event Action<CurrencyType, long> OnCurrency;
    public static event Action<long, long> OnIdleIncome;
    public static event Action OnHeroChanged;
    public static event Action OnStageChanged;
    public static event Action<string> OnToast;

    // ---- Roguelite Run 專用事件 ----
    public static event Action OnRunStart;
    public static event Action OnRunEnd;
    public static event Action<int> OnWaveCleared;       // 參數: next wave index
    public static event Action OnChoiceRequested;
    public static event Action<List<EquipmentInstance>> OnRunFinished;

    // ---- 觸發器 ----
    public static void RaiseBattleStart(int w) => OnBattleStart?.Invoke(w);
    public static void RaiseStage() => OnStage?.Invoke();
    public static void RaiseCurrency(CurrencyType t, long v) => OnCurrency?.Invoke(t, v);
    public static void RaiseIdleIncome(long g, long e) => OnIdleIncome?.Invoke(g, e);
    public static void RaiseHero() => OnHeroChanged?.Invoke();
    public static void RaiseStageChanged() => OnStageChanged?.Invoke(); 
    public static void Toast(string msg) => OnToast?.Invoke(msg);

    public static void RaiseRunStart() => OnRunStart?.Invoke();
    public static void RaiseRunEnd() => OnRunEnd?.Invoke();
    public static void RaiseWaveCleared(int nextWave) => OnWaveCleared?.Invoke(nextWave);
    public static void RaiseChoiceRequested() => OnChoiceRequested?.Invoke();
    public static void RaiseRunFinished(List<EquipmentInstance> loot) => OnRunFinished?.Invoke(loot);
}