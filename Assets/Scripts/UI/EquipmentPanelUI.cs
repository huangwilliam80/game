using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 裝備面板 UI：顯示背包網格、裝備詳情，並處理所有裝備養成操作。
/// </summary>
public class EquipmentPanelUI : MonoBehaviour
{
    [Header("引用（由 GameBootstrapper 自動指派）")]
    public Transform equipRow;
    public Transform bagGrid;
    public GameObject cellPrefab;
    public TMP_Text detailText;

    [Header("操作按鈕")]
    public Button btnEnhance;
    public Button btnReforge;
    public Button btnUpgradeQ;
    public Button btnEquipSel;
    public Button btnDecompose;
    public Button btnCleanup;

    private List<GameObject> cells = new List<GameObject>();
    private EquipmentInstance selected;

    void OnEnable()
    {
        // ★ 修復：確保按鈕事件正確綁定，並防止重複綁定
        if (btnEnhance) { btnEnhance.onClick.RemoveAllListeners(); btnEnhance.onClick.AddListener(OnEnhance); }
        if (btnReforge) { btnReforge.onClick.RemoveAllListeners(); btnReforge.onClick.AddListener(OnReforge); }
        if (btnUpgradeQ) { btnUpgradeQ.onClick.RemoveAllListeners(); btnUpgradeQ.onClick.AddListener(OnUpgradeQuality); }
        if (btnEquipSel) { btnEquipSel.onClick.RemoveAllListeners(); btnEquipSel.onClick.AddListener(OnEquipSelected); }
        if (btnDecompose) { btnDecompose.onClick.RemoveAllListeners(); btnDecompose.onClick.AddListener(OnDecompose); }
        if (btnCleanup) { btnCleanup.onClick.RemoveAllListeners(); btnCleanup.onClick.AddListener(OnCleanup); }

        GameEvents.OnInventoryChanged += BuildRows;
        BuildRows();
        RefreshDetail();
    }

    void OnDisable()
    {
        GameEvents.OnInventoryChanged -= BuildRows;
    }

    // ★ 修復：補上缺失的獲取裝備名稱方法
    string GetEquipName(EquipmentInstance equip)
    {
        if (equip == null) return "未知";
        var def = EquipmentDatabase.Get(equip.defId);
        return def != null ? def.name : "未知";
    }

    string GetShortName(EquipmentInstance equip)
    {
        if (equip == null) return "?";
        return $"{equip.QualityName} {GetEquipName(equip)}\n+{equip.plus}";
    }

    string DescribeEquipment(EquipmentInstance equip)
    {
        if (equip == null) return "(空)";
        string txt = $"【{equip.QualityName}】{GetEquipName(equip)}\n";
        txt += $"等級：Lv.{equip.level} 強化：+{equip.plus}\n";
        txt += $"戰力：{equip.Power}\n\n";

        var stats = equip.FinalStats();
        txt += "--- 最終屬性 ---\n";
        if (stats.atk > 0.01f) txt += $"攻擊：{stats.atk:F1}\n";
        if (stats.def > 0.01f) txt += $"防禦：{stats.def:F1}\n";
        if (stats.hp > 0.01f) txt += $"生命：{stats.hp:F1}\n";
        if (stats.critRate > 0.001f) txt += $"暴擊：{stats.critRate:P1}\n";
        if (stats.speed > 0.01f) txt += $"速度：{stats.speed:F2}\n";
        if (stats.special > 0.01f) txt += $"特效：{stats.special:F1}\n";

        if (equip.affixes != null && equip.affixes.Length > 0)
        {
            txt += "\n--- 詞綴 ---\n";
            foreach (var affix in equip.affixes)
                txt += $"· {affix.name}\n";
        }
        return txt;
    }

    public void BuildRows()
    {
        foreach (var c in cells)
            if (c != null) Destroy(c);
        cells.Clear();

        var bag = GameSave.Data.bags;
        if (bag == null) return;

        for (int i = 0; i < bag.Count; i++)
        {
            var cell = Instantiate(cellPrefab, bagGrid);
            cell.SetActive(true);

            // ★ 修復：Bootstrapper 生成的是 TMP_Text，這裡必須用 TMP_Text 才能抓到！
            var txt = cell.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.text = GetShortName(bag[i]);
                txt.color = bag[i].QualityColor; // 根據品質改變文字顏色
            }

            var img = cell.GetComponent<Image>();
            if (img != null) img.color = new Color(0.2f, 0.22f, 0.3f, 0.8f); // 統一背景色

            int idx = i;
            var btn = cell.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectItem(idx));
            }
            cells.Add(cell);
        }
    }

    void SelectItem(int index)
    {
        var bag = GameSave.Data.bags;
        if (bag == null || index < 0 || index >= bag.Count) return;
        selected = bag[index];
        RefreshDetail();
    }

    void RefreshDetail()
    {
        if (detailText == null) return;
        if (selected == null)
        {
            detailText.text = "點擊背包中的裝備查看詳情";
            return;
        }
        detailText.text = DescribeEquipment(selected);
    }

    // ================= 操作邏輯綁定 =================

    public void OnEnhance()
    {
        if (selected == null) return;
        InventorySystem.Enhance(selected);
        BuildRows();
        RefreshDetail();
    }

    public void OnReforge()
    {
        if (selected == null) return;
        InventorySystem.Reforge(selected);
        BuildRows();
        RefreshDetail();
    }

    public void OnUpgradeQuality()
    {
        if (selected == null) return;
        InventorySystem.UpgradeQuality(selected);
        BuildRows();
        RefreshDetail();
    }

    public void OnEquipSelected()
    {
        if (selected == null) return;
        int index = GameSave.Data.bags.IndexOf(selected);
        if (index >= 0) InventorySystem.Equip(index);
        BuildRows();
        RefreshDetail();
    }

    public void OnDecompose()
    {
        if (selected == null) return;
        InventorySystem.Decompose(selected);
        selected = null;
        BuildRows();
        RefreshDetail();
    }

    public void OnCleanup()
    {
        InventorySystem.CleanupTrash();
        selected = null;
        BuildRows();
        RefreshDetail();
    }
}