using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 頂部資源欄：金幣／靈玉／碎片／等級戰力／掛機速率。
/// 只訂閱事件刷新（不做每帧 Update）→ UI 零常駐開銷。
/// </summary>
public class TopBarUI : MonoBehaviour
{
    [Header("Text 引用（由生成器指派）")]
    public TMP_Text gold, crystal, shard, levelPower, income, stageLabel;

    void OnEnable()
    {
        GameEvents.OnCurrencyChanged += OnAny; GameEvents.OnHeroChanged += Refresh;
        GameEvents.OnIdleIncome += OnIncome;
        GameEvents.OnStageChanged += Refresh;
        Refresh();
    }
    void OnDisable()
    {
        GameEvents.OnCurrencyChanged -= OnAny; GameEvents.OnHeroChanged -= Refresh;
        GameEvents.OnIdleIncome -= OnIncome;
        GameEvents.OnStageChanged -= Refresh;
    }
    void OnIncome(long g, long e) => Refresh();
    void OnAny(CurrencyType t, long v) => Refresh();

    public void Refresh()
    {
        var d = GameSave.Data;
        if (gold) gold.text = Num(d.gold);
        if (crystal) crystal.text = Num(d.spiritCrystal);
        if (shard) shard.text = Num(d.forgeShard);
        if (levelPower) levelPower.text = $"Lv.{d.heroLevel}  ⚔ {Num(CharacterSystem.Power())}";
        if (stageLabel) stageLabel.text = $"掛機中：第 {d.currentStage} 夜";
        IdleFormula.IdleRate(out long g, out _, d.maxStageCleared, d.heroLevel);
        if (income) income.text = $"+{Num((long)(g * CharacterSystem.IdleBonus()))}/s";
    }

    public static string Num(long n) =>
        n >= 100_000_000 ? $"{n / 1e6f:N1}M" : n >= 100_000 ? $"{n / 1e3f:N1}K" : $"{n:N0}";
}
