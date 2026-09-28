using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection; 

public class EquipmentPanelUI : MonoBehaviour
{
    [Header("引用（由 GameBootstrapper 自動指派）")]
    public Transform equipRow;
    public Transform bagGrid;
    public GameObject cellPrefab;
    public Text detailText;

    [Header("操作按鈕")]
    public Button btnEnhance;
    public Button btnReforge;
    public Button btnUpgradeQ;
    public Button btnEquipSel;
    public Button btnDecompose;
    public Button btnCleanup;

    readonly List<GameObject> cells = new List<GameObject>();
    EquipmentInstance selected;

    void OnEnable()
    {
        if (cellPrefab == null || bagGrid == null) return;
        BuildRows();
        RefreshDetail();
    }

    string GetEquipName(EquipmentInstance equip)
    {
        if (equip == null) return "?";
        
        var def = EquipmentDatabase.Get(equip.defId);
        if (def == null) return $"裝備ID:{equip.defId}";

        var type = def.GetType();
        var nameProp = type.GetProperty("name") ?? type.GetProperty("equipName") ?? type.GetProperty("itemName");
        var nameField = type.GetField("name") ?? type.GetField("equipName") ?? type.GetField("itemName");

        if (nameProp != null) return nameProp.GetValue(def)?.ToString() ?? $"裝備{equip.defId}";
        if (nameField != null) return nameField.GetValue(def)?.ToString() ?? $"裝備{equip.defId}";

        return $"裝備{equip.defId}";
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
        txt += $"等級：Lv.{equip.level}  強化：+{equip.plus}\n";
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
            {
                txt += $"· {affix.name}\n";
            }
        }

        return txt;
    }

    void BuildRows()
    {
        if (cellPrefab == null || bagGrid == null) return;

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
            {
                txt.text = GetShortName(bag[i]);
                var img = cell.GetComponent<Image>();
                if (img != null) img.color = bag[i].QualityColor; 
            }

            int idx = i;
            var btn = cell.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => SelectItem(idx));

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

    public void OnEnhance()
    {
        if (selected == null) return;
        Debug.Log("強化：" + GetEquipName(selected));
        RefreshDetail();
    }

    public void OnReforge()
    {
        if (selected == null) return;
        Debug.Log("洗鍊：" + GetEquipName(selected));
        selected.seed = Random.Range(0, 999999); 
        selected.RegenerateAffixes();
        BuildRows();
        RefreshDetail();
    }

    public void OnUpgradeQuality()
    {
        if (selected == null) return;
        Debug.Log("升階：" + GetEquipName(selected));
        RefreshDetail();
    }

    public void OnEquipSelected()
    {
        if (selected == null) return;
        InventorySystem.Equip(selected);
        BuildRows();
        RefreshDetail();
    }

    // ★ 修復：分解裝備
    public void OnDecompose()
    {
        if (selected == null) return;
        
        // 獲取該裝備在背包列表中的索引 (int)
        int index = GameSave.Data.bags.IndexOf(selected);
        if (index >= 0)
        {
            // 將索引傳給 Remove 方法
            InventorySystem.Remove(index);
        }
        
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