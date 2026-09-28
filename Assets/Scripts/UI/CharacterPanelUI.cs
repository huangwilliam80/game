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

    // 功法名稱與描述（對應 CharacterSystem 的 8 個功法）
    private readonly string[] gongfaNames = { "吐納術", "強身訣", "凝神功", "疾風步", "鐵布衫", "聚靈陣", "破甲擊", "回春術" };
    private readonly string[] gongfaDescs = { "增加掛機金幣收益", "增加角色生命上限", "增加暴擊機率", "增加攻擊速度", "增加防禦力", "增加掛機經驗收益", "增加攻擊力", "增加特殊效果強度" };

    void OnEnable()
    {
        // 訂閱事件：當角色數據或貨幣發生變化時，刷新 UI
        GameEvents.OnHeroChanged += Refresh;
        GameEvents.OnCurrencyChanged += OnCurrencyChanged;
    }

    void OnDisable()
    {
        // 取消訂閱，防止內存洩漏
        GameEvents.OnHeroChanged -= Refresh;
        GameEvents.OnCurrencyChanged -= OnCurrencyChanged;
    }

    void Start()
    {
        // 初始化功法按鈕的文字和點擊事件
        if (upgradeButtons != null)
        {
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int index = i; // 閉包捕獲
                upgradeButtons[i].onClick.AddListener(() => OnUpgradeGongfaClicked(index));
            }
        }
        Refresh();
    }

    /// <summary>
    /// 當貨幣變化時，只需要刷新按鈕的可升級狀態（因為升級功法需要消耗貨幣）
    /// </summary>
    private void OnCurrencyChanged(CurrencyType type, long amount)
    {
        RefreshGongfaButtons();
    }

    /// <summary>
    /// 刷新整個角色面板
    /// </summary>
    public void Refresh()
    {
        UpdateStatText();
        RefreshGongfaButtons();
    }

    /// <summary>
    /// 更新角色屬性文字
    /// </summary>
    private void UpdateStatText()
    {
        if (statText == null) return;

        var data = GameSave.Data;
        
        // 獲取角色總屬性（基礎 + 裝備 + 功法加成）
        // 假設 CharacterSystem 有一個方法可以獲取最終屬性
        StatBlock finalStats = CharacterSystem.GetFinalStats();
        
        long expToNext = GameMath.ExpToLevel(data.heroLevel);
        long currentExp = data.exp;
        float expPercent = expToNext > 0 ? Mathf.Clamp01((float)currentExp / expToNext) : 1f;

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

    /// <summary>
    /// 刷新 8 個功法升級按鈕的狀態
    /// </summary>
    private void RefreshGongfaButtons()
    {
        if (upgradeButtons == null || gongfaRow == null) return;

        var data = GameSave.Data;

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            var btn = upgradeButtons[i];
            if (btn == null) continue;

            int level = data.gongfaLevel[i];
            long cost = GetGongfaUpgradeCost(i, level);
            
            // 獲取按鈕上的文字元件
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                string name = i < gongfaNames.Length ? gongfaNames[i] : $"功法 {i + 1}";
                btnText.text = $"{name}\nLv.{level}\n費用：{cost:N0}";
            }

            // 判斷是否有足夠的貨幣來升級（假設升級功法消耗金幣或靈玉，這裡用金幣作為示例）
            // 你可以根據 CharacterSystem 的實際邏輯修改這裡
            bool canAfford = data.gold >= cost;
            
            // 設置按鈕的可交互狀態和顏色
            btn.interactable = canAfford;
            var colors = btn.colors;
            colors.normalColor = canAfford ? new Color(0.3f, 0.35f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
            btn.colors = colors;
        }
    }

    /// <summary>
    /// 計算功法升級費用（指數增長）
    /// </summary>
    private long GetGongfaUpgradeCost(int index, int level)
    {
        // 基礎費用 100 金幣，每級增長 1.5 倍
        // 你可以根據遊戲的實際數值調整這裡
        return (long)(100 * Mathf.Pow(1.5f, level));
    }

    /// <summary>
    /// 當玩家點擊升級功法按鈕時
    /// </summary>
    private void OnUpgradeGongfaClicked(int index)
    {
        var data = GameSave.Data;
        long cost = GetGongfaUpgradeCost(index, data.gongfaLevel[index]);

        if (data.gold >= cost)
        {
            data.gold -= cost;
            data.gongfaLevel[index]++;
            
            // 觸發事件，通知其他系統（包括本 UI）刷新
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