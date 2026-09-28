using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色面板：顯示主角屬性與功法升級按鈕。
/// 修復：僅使用 CharacterSystem 已確認存在的方法。
/// </summary>
public class CharacterPanelUI : MonoBehaviour
{
    [Header("引用（由 GameBootstrapper 自動指派）")]
    public Text statText;
    public Transform gongfaRow;
    public Button[] upgradeButtons;

    // ──────────────────────────────────────────────
    void OnEnable()
    {
        if (statText == null) return;   // ★ 空值保護
        RefreshStats();
        BindButtons();
    }

    // ──────────────────────────────────────────────
    //  刷新屬性（只用已確認存在的 FinalStats）
    // ──────────────────────────────────────────────
    void RefreshStats()
    {
        if (statText == null) return;

        var s = CharacterSystem.FinalStats();

        statText.text =
            "<b>主角屬性</b>\n" +
            $"生命：{s.hp:F0}\n" +
            $"攻擊：{s.atk:F0}\n" +
            $"防禦：{s.def:F0}\n" +
            $"速度：{s.speed:F2}\n" +
            $"暴擊率：{s.critRate:P1}\n" +
            $"特殊：{s.special:F1}";
    }

    // ──────────────────────────────────────────────
    //  綁定功法按鈕（不依賴不存在的方法）
    // ──────────────────────────────────────────────
    void BindButtons()
    {
        if (upgradeButtons == null) return;

        string[] gongfaNames =
        {
            "吐納術", "金鐘罩", "凌波微步", "破甲擊",
            "聚氣訣", "鐵布衫", "疾風步", "會心訣"
        };

        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            if (upgradeButtons[i] == null) continue;

            int idx = i;
            upgradeButtons[i].onClick.RemoveAllListeners();
            upgradeButtons[i].onClick.AddListener(() => OnUpgrade(idx));

            var txt = upgradeButtons[i].GetComponentInChildren<Text>();
            if (txt != null)
            {
                string name = idx < gongfaNames.Length ? gongfaNames[idx] : $"功法{idx + 1}";
                txt.text = $"{name}\nLv.1";
            }
        }
    }

    void OnUpgrade(int index)
    {
        // TODO: 對接 CharacterSystem 的實際升級方法
        // 目前先用 Debug.Log 佔位，等確認 CharacterSystem 有哪些方法後再對接
        Debug.Log($"升級功法 #{index}");
        RefreshStats();
    }
}