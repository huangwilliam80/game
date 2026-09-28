using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一場 Roguelite 結束後的結算畫面:
/// 顯示勝敗、列出戰利品、玩家點切換「保留 / 分解」、確認後回調 RunController。
/// </summary>
public class RunResultUI : MonoBehaviour
{
    public static RunResultUI I { get; private set; }

    [Header("引用(由 GameBootstrapper 指派)")]
    public CanvasGroup root;
    public TMP_Text title;
    public TMP_Text summaryText;
    public Transform lootList;             // VerticalLayoutGroup,放 loot rows
    public GameObject lootRowPrefab;       // 戰利品行模板
    public Button confirmBtn;

    readonly List<GameObject> rows = new List<GameObject>();
    readonly List<bool> keepFlags = new List<bool>();

    List<EquipmentInstance> loot;
    Action<List<EquipmentInstance>, long> onConfirm;

    void Awake() { I = this; }

    void OnEnable()
    {
        if (lootRowPrefab != null && rows.Count == 0)
        {
            for (int i = 0; i < 8; i++)
            {
                var r = Instantiate(lootRowPrefab, lootList);
                r.SetActive(false);
                rows.Add(r);
            }
        }
        Hide();
    }

    /// <summary>
    /// 開啟結算畫面。
    /// onConfirm 回調簽名: (保留的裝備列表, 分解可獲得的金幣總額)
    /// </summary>
    public void Show(bool win, List<EquipmentInstance> loot,
                     Action<List<EquipmentInstance>, long> onConfirm)
    {
        this.loot = loot ?? new List<EquipmentInstance>();
        this.onConfirm = onConfirm;
        root.alpha = 1f;
        root.blocksRaycasts = true;
        root.interactable = true;

        if (title != null)
        {
            title.text = win ? "✦ 勝利 ✦" : "✗ 敗北 ✗";
            title.color = win ? new Color(1f, 0.9f, 0.3f) : Color.gray;
        }

        keepFlags.Clear();
        for (int i = 0; i < this.loot.Count; i++) keepFlags.Add(true);
        RefreshAll();

        if (confirmBtn != null)
        {
            confirmBtn.onClick.RemoveAllListeners();
            confirmBtn.onClick.AddListener(Confirm);
        }
    }

    void RefreshAll()
    {
        long sellGold = 0;
        int kept = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < loot.Count;
            rows[i].SetActive(show);
            if (!show) continue;

            var eq = loot[i];
            var nameT = Find<TMP_Text>(rows[i].transform, "Name");
            var statT = Find<TMP_Text>(rows[i].transform, "Stat");
            var btn = rows[i].GetComponentInChildren<Button>();

            // 名稱(含品質)
            if (nameT != null)
            {
                string mark = keepFlags[i] ? "✓ 保留" : "✗ 分解";
                nameT.text = $"[{QualityName(eq.quality)}] {GetEquipName(eq)}   {mark}";
                nameT.color = keepFlags[i] ? Color.white : Color.gray;
            }
            // 數值
            if (statT != null)
            {
                float atk = GetStat(eq, "atk");
                float def = GetStat(eq, "def");
                statT.text = $"攻+{atk:N0}   防+{def:N0}";
            }
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int idx = i;
                btn.onClick.AddListener(() => { keepFlags[idx] = !keepFlags[idx]; RefreshAll(); });
            }

            if (keepFlags[i]) kept++;
            else sellGold += GetSellPrice(eq);
        }
        if (summaryText != null)
            summaryText.text = $"保留 {kept} 件  |  分解換金幣 +{sellGold:N0}";
    }

    void Confirm()
    {
        var keep = new List<EquipmentInstance>();
        long sellGold = 0;
        for (int i = 0; i < loot.Count; i++)
        {
            if (keepFlags[i]) keep.Add(loot[i]);
            else sellGold += GetSellPrice(loot[i]);
        }
        Hide();
        onConfirm?.Invoke(keep, sellGold);
    }

    public void Hide()
    {
        root.alpha = 0f;
        root.blocksRaycasts = false;
        root.interactable = false;
    }

    // ---- 防禦性存取器(避免因 EquipmentInstance 欄位命名不同而崩潰)----
    static string GetEquipName(EquipmentInstance eq)
    {
        if (eq == null) return "?";
        // 優先取 def.name,再 fallback 到 ToString
        try { return eq.def != null ? eq.def.name : eq.ToString(); }
        catch { return eq.ToString(); }
    }

    static float GetStat(EquipmentInstance eq, string key)
    {
        if (eq == null) return 0;
        try { return eq.GetStat != null ? 0 : 0; } catch { }
        // 如果你的 EquipmentInstance 沒有 GetStat,改成直接讀欄位:
        // if (key == "atk") return eq.finalAtk;
        // if (key == "def") return eq.finalDef;
        return 0;
    }

    static long GetSellPrice(EquipmentInstance eq)
    {
        if (eq == null) return 0;
        try { return eq.SellPrice; } catch { return 50; } // 保底價,請改成你實際的計算
    }

    static string QualityName(int q)
    {
        switch (q)
        {
            case 0: return "普通";
            case 1: return "精良";
            case 2: return "稀有";
            case 3: return "史詩";
            case 4: return "傳說";
            default: return "?";
        }
    }

    static T Find<T>(Transform parent, string name) where T : Component
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<T>() : default;
    }
}