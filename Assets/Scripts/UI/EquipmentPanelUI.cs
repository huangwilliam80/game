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

    // 顯示字串統一交给 EquipDisplayUtil（與結算畫面共用一份邏輯）
    string GetEquipName(EquipmentInstance equip) => EquipDisplayUtil.Name(equip);

    string GetShortName(EquipmentInstance equip) => EquipDisplayUtil.ShortLabel(equip);

    string DescribeEquipment(EquipmentInstance equip) => EquipDisplayUtil.Describe(equip);

    /// <summary>上方 6 個部位按鈕：顯示當前穿戴 + 战力，一眼看出哪個部位拖後腿。</summary>
    void RefreshEquipRow()
    {
        if (equipRow == null) return;
        var btns = equipRow.GetComponentsInChildren<Button>(true);
        string[] slots = { "武器", "頭部", "衣服", "飾品", "法器", "鞋子" };
        for (int i = 0; i < btns.Length && i < 6; i++)
        {
            var eq = GameSave.Data.equipped[i];
            var t = btns[i].GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                t.text = eq == null
                    ? $"{slots[i]}\n<color=#FF9B9B>未穿戴</color>"
                    : $"{slots[i]}\n{EquipDisplayUtil.Name(eq)}\n<color=#{ColorUtility.ToHtmlStringRGB(eq.QualityColor)}>{eq.QualityName} +{eq.plus}</color>";
                t.fontSize = 20;
            }
        }
    }

    public void BuildRows()
    {
        RefreshEquipRow();
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
            if (img != null)
                img.color = ReferenceEquals(bag[i], selected)
                    ? new Color(.45f, .55f, .75f, 1f)                 // 選中高亮
                    : new Color(0.2f, 0.22f, 0.3f, 0.8f);

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