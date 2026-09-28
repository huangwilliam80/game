using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 裝備面板：顯示已裝備欄位、背包格子、裝備詳情與操作按鈕。
/// 修復：OnEnable / BuildRows 加入空值保護，避免 AddComponent 時引用尚未賦值。
/// </summary>
public class EquipmentPanelUI : MonoBehaviour
{
    [Header("引用（由 GameBootstrapper 自動指派）")]
    public Transform equipRow;       // 6 個裝備槽位容器
    public Transform bagGrid;        // 背包格子容器
    public GameObject cellPrefab;    // 背包格子模板
    public Text detailText;          // 裝備詳情文字

    [Header("操作按鈕")]
    public Button btnEnhance;        // 強化
    public Button btnReforge;        // 洗鍊
    public Button btnUpgradeQ;       // 升階
    public Button btnEquipSel;       // 穿戴
    public Button btnDecompose;      // 分解
    public Button btnCleanup;        // 一鍵清理

    readonly List<GameObject> cells = new List<GameObject>();
    EquipmentInstance selected;

    // ──────────────────────────────────────────────
    //  生命週期
    // ──────────────────────────────────────────────
    void OnEnable()
    {
        // ★ 修復：引用尚未賦值時直接返回，避免 NullReference
        if (cellPrefab == null || bagGrid == null) return;

        BuildRows();
        RefreshDetail();
    }

    // ──────────────────────────────────────────────
    //  建立 / 刷新背包格子
    // ──────────────────────────────────────────────
    void BuildRows()
    {
        // ★ 修復：空值保護
        if (cellPrefab == null || bagGrid == null) return;

        // 清除舊格子
        foreach (var c in cells)
            if (c != null) Destroy(c);
        cells.Clear();

        var bag = GameSave.Data.bags;
        if (bag == null) return;

        for (int i = 0; i < bag.Count; i++)
        {
            var cell = Instantiate(cellPrefab, bagGrid);
            cell.SetActive(true);

            var txt = cell.GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = bag[i].name;

            // 點擊選中
            int idx = i;
            var btn = cell.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SelectItem(idx));

            cells.Add(cell);
        }
    }

    // ──────────────────────────────────────────────
    //  選中裝備
    // ──────────────────────────────────────────────
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

        string txt = $"<b>{selected.name}</b>\n";
        txt += $"品質：{selected.rarity}\n";
        txt += $"部位：{selected.slot}\n";
        txt += $"強化：+{selected.enhanceLevel}\n";

        if (selected.stats != null)
        {
            foreach (var s in selected.stats)
                txt += $"{s.type} +{s.value:F1}\n";
        }

        detailText.text = txt;
    }

    // ──────────────────────────────────────────────
    //  按鈕回調（由 GameBootstrapper 綁定）
    // ──────────────────────────────────────────────
    public void OnEnhance()
    {
        if (selected == null) return;
        // TODO: 強化邏輯
        RefreshDetail();
    }

    public void OnReforge()
    {
        if (selected == null) return;
        // TODO: 洗鍊邏輯
        RefreshDetail();
    }

    public void OnUpgradeQuality()
    {
        if (selected == null) return;
        // TODO: 升階邏輯
        RefreshDetail();
    }

    public void OnEquipSelected()
    {
        if (selected == null) return;
        InventorySystem.Equip(selected);
        BuildRows();
        RefreshDetail();
    }

    public void OnDecompose()
    {
        if (selected == null) return;
        InventorySystem.Remove(selected);
        selected = null;
        BuildRows();
        RefreshDetail();
    }

    public void OnCleanup()
    {
        InventorySystem.CleanupBag();
        selected = null;
        BuildRows();
        RefreshDetail();
    }
}