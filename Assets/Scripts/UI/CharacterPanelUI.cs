using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// ★ 修復 CS0103: The name 'GameEvents' does not exist in the current context
// 以別名直接綁定全域事件总线，確保每個檔案都能解析到 GameEvents（不受專案載入順序影響）。
using GameEvents = global::GameEvents;

/// <summary>
/// 角色面板 UI：顯示角色真實屬性、戰力、經驗條，以及 8 個功法的升級按鈕。
/// </summary>
public class CharacterPanelUI : MonoBehaviour
{
    [Header("UI References (Assigned by GameBootstrapper)")]
    public TMP_Text statText;
    public Transform gongfaRow;
    public Button[] upgradeButtons;

    // ★ 修復：名稱必須對應 CharacterSystem.cs 中的真實定義
    private readonly string[] gongfaNames = { 
        "葵花煉氣訣", "金鐘護體", "龜息長生功", "破妄劍意", 
        "凌波微步", "天師法旨", "聚寶秘術", "煉器心法" 
    };

    void OnEnable()
    {
        GameEvents.OnHeroChanged += Refresh;
        GameEvents.OnCurrencyChanged += OnCurrencyChanged;
        
        if (upgradeButtons != null)
        {
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int index = i;
                upgradeButtons[i].onClick.RemoveAllListeners();
                upgradeButtons[i].onClick.AddListener(() => OnUpgradeGongfaClicked(index));
            }
        }
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.OnHeroChanged -= Refresh;
        GameEvents.OnCurrencyChanged -= OnCurrencyChanged;
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

    private void UpdateStatText()
    {
        if (statText == null) return;
        var data = GameSave.Data;

        // ★ 修復：直接調用 CharacterSystem.FinalStats() 獲取包含裝備、套裝的真實戰鬥屬性！
        StatBlock finalStats = CharacterSystem.FinalStats();
        long expToNext = GameMath.ExpToLevel(data.heroLevel);
        long currentExp = data.exp;

        string statsStr = $"<color=#FFD700>【角色屬性】</color>\n";
        statsStr += $"等級：Lv.{data.heroLevel} ({currentExp:N0}/{expToNext:N0})\n";
        statsStr += $"戰力：<color=#00FF00>{CharacterSystem.Power():N0}</color>\n";
        statsStr += $"攻擊：{finalStats.atk:N0} 防禦：{finalStats.def:N0}\n";
        statsStr += $"生命：{finalStats.hp:N0} 速度：{finalStats.speed:F1}\n";
        statsStr += $"暴擊：{finalStats.critRate * 100f:N1}% 特效：{finalStats.special:N0}\n";
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
            long cost = CharacterSystem.GongfaUpCost(i);          // ★ 公式只有一份：與 Inventory 端一致

            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                string name = i < gongfaNames.Length ? gongfaNames[i] : $"功法 {i + 1}";
                string effect = i < CharacterSystem.Gongfas.Count ? CharacterSystem.Gongfas[i].desc : "";
                bool maxed = level >= 20;
                btnText.text = maxed
                    ? $"{name}\nLv.{level}（已滿）\n{effect}"
                    : $"{name}\nLv.{level} → {level + 1}\n<color=#FFD37A>{cost:N0} 金幣</color>\n{effect}";
            }

            bool canAfford = data.gold >= cost;
            btn.interactable = canAfford;
            var colors = btn.colors;
            colors.normalColor = canAfford ? new Color(0.3f, 0.35f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
            btn.colors = colors;
        }
    }

    private void OnUpgradeGongfaClicked(int index)
    {
        var data = GameSave.Data;
        if (data.gongfaLevel == null || index >= data.gongfaLevel.Length) return;

        long cost = CharacterSystem.GongfaUpCost(index);
        if (data.gold >= cost)
        {
            data.gold -= cost;
            data.gongfaLevel[index]++;
            
            GameSave.Save(); // ★ 升級後立即存檔防止閃退丟失
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