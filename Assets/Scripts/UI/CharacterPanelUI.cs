using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色面板 UI：顯示角色屬性、戰力、經驗條，以及 8 個功法（Roguelite 永久強化）的升級按鈕。
/// 對應 GameBootstrapper.cs 中生成的 P_Char 面板。
/// </summary>
public class CharacterPanelUI : MonoBehaviour
{
    [Header("UI References (Assigned by GameBootstrapper)")]
    public TextMeshProUGUI statText;
    public Transform gongfaRow;
    public Button[] upgradeButtons;

    // 功法名稱（對應 CharacterSystem 的 8 個功法）
    private readonly string[] gongfaNames = { "吐納術", "強身訣", "凝神功", "疾風步", "鐵布衫", "聚靈陣", "破甲擊", "回春術" };

    void OnEnable()
    {
        GameEvents.OnHeroChanged += Refresh;
        GameEvents.OnCurrencyChanged += OnCurrencyChanged;
    }

    void OnDisable()
    {
        GameEvents.OnHeroChanged -= Refresh;
        GameEvents.OnCurrencyChanged -= OnCurrencyChanged;
    }

    void Start()
    {
        if (upgradeButtons != null)
        {
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int index = i;
                upgradeButtons[i].onClick.AddListener(() => OnUpgradeGongfaClicked(index));
            }
        }
        Refresh();
    }

    private void OnCurrencyChanged(CurrencyType type, long amount)
    {
        RefreshGongfaButtons();
    }

    public void Refresh()
    {
        UpdateStatText();
        RefreshGongfaButtons();
    }

    /// <summary>
    /// 手動計算角色總屬性（基礎 + 全部裝備）
    /// </summary>
    private StatBlock CalculateTotalStats()
    {
        var data = GameSave.Data;
        StatBlock total = new StatBlock();

        // 基礎屬性（隨等級成長）
        total.atk = 10f + data.heroLevel * 2f;
        total.def = 5f + data.heroLevel * 1.5f;
        total.hp = 100f + data.heroLevel * 20f;
        total.critRate = 0.05f;
        total.speed = 1f;
        total.special = 0f;

        // 加上所有已裝備物品的屬性
        if (data.equipped != null)
        {
            for (int i = 0; i < data.equipped.Length; i++)
            {
                if (data.equipped[i] != null)
                {
                    total = total + data.equipped[i].FinalStats();
                }
            }
        }

        return total;
    }

    private void UpdateStatText()
    {
        if (statText == null) return;

        var data = GameSave.Data;
        StatBlock finalStats = CalculateTotalStats();

        long expToNext = GameMath.ExpToLevel(data.heroLevel);
        long currentExp = data.exp;

        string statsStr = $"<color=#FFD700>【角色屬性】</color>\n";
        statsStr += $"等級：Lv.{data.heroLevel} ({currentExp:N0}/{expToNext:N0})\n";
        statsStr += $"戰力：<color=#00FF00>{CharacterSystem.Power():N0}</color>\n";
        statsStr += $"攻擊：{finalStats.atk:N0}  防禦：{finalStats.def:N0}\n";
        statsStr += $"生命：{finalStats.hp:N0}  速度：{finalStats.speed:N1}\n";
        statsStr += $"暴擊：{finalStats.critRate * 100f:N1}%  特效：{finalStats.special:N0}\n";
        statsStr += $"\n<color=#87CEEB>【掛機加成】</color>\n";
        statsStr += $"掛機收益倍率：x{CharacterSystem.IdleBonus():F2}";

        statText.text = statsStr;
    }

    private void RefreshGongfaButtons()
    {
        if (upgradeButtons == null) return;

        var data = GameSave.Data;

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            var btn = upgradeButtons[i];
            if (btn == null) continue;

            int level = (data.gongfaLevel != null && i < data.gongfaLevel.Length) ? data.gongfaLevel[i] : 0;
            long cost = GetGongfaUpgradeCost(i, level);

            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                string name = i < gongfaNames.Length ? gongfaNames[i] : $"功法 {i + 1}";
                btnText.text = $"{name}\nLv.{level}\n費用：{cost:N0}";
            }

            bool canAfford = data.gold >= cost;
            btn.interactable = canAfford;
            var colors = btn.colors;
            colors.normalColor = canAfford ? new Color(0.3f, 0.35f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
            btn.colors = colors;
        }
    }

    private long GetGongfaUpgradeCost(int index, int level)
    {
        return (long)(100 * Mathf.Pow(1.5f, level));
    }

    private void OnUpgradeGongfaClicked(int index)
    {
        var data = GameSave.Data;
        if (data.gongfaLevel == null || index >= data.gongfaLevel.Length) return;

        long cost = GetGongfaUpgradeCost(index, data.gongfaLevel[index]);

        if (data.gold >= cost)
        {
            data.gold -= cost;
            data.gongfaLevel[index]++;

            GameEvents.RaiseCurrency(CurrencyType.Gold, data.gold);
            GameEvents.RaiseHero();
            GameEvents.Toast($"【{gongfaNames[index]}】升級至 Lv.{data.gongfaLevel[index]}！");
        }
        else
        {
            GameEvents.Toast("金幣不足，無法升級功法！");
        }
    }
}