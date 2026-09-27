using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备面板：左側六槽位、右側背包格子（虛擬化：只建可見格子的 GameObject）。
/// </summary>
public class EquipmentPanelUI : MonoBehaviour
{
    [Header("由生成器指派")]
    public Transform equipRow;          // 6 個槽位按鈕父節點
    public Transform bagGrid;           // 背包格子父節點（GridLayout）
    public GameObject cellPrefab;       // 單格模板
    public TMP_Text detailText;         // 選中裝備詳情
    public Button btnEnhance, btnReforge, btnUpgradeQ, btnEquipSel, btnDecompose, btnCleanup;

    readonly List<GameObject> cells = new();
    int selectedIndex = -1;             // 背包索引；-1 = 看已穿戴

    void OnEnable()
    {
        GameEvents.OnInventoryChanged += Refresh;
        BuildRows();
        Refresh();
        if (btnEnhance) btnEnhance.onClick.AddListener(() => WithSelected(e => InventorySystem.Enhance(e)));
        if (btnReforge) btnReforge.onClick.AddListener(() => WithSelected(e => InventorySystem.Reforge(e)));
        if (btnUpgradeQ) btnUpgradeQ.onClick.AddListener(() => WithSelected(e => InventorySystem.UpgradeQuality(e)));
        if (btnEquipSel) btnEquipSel.onClick.AddListener(() => { if (selectedIndex >= 0) InventorySystem.Equip(selectedIndex); });
        if (btnDecompose) btnDecompose.onClick.AddListener(() => { if (selectedIndex >= 0) InventorySystem.Decompose(GameSave.Data.bags[selectedIndex]); });
        if (btnCleanup) btnCleanup.onClick.AddListener(InventorySystem.CleanupTrash);
    }
    void OnDisable() => GameEvents.OnInventoryChanged -= Refresh;

    void WithSelected(System.Action<EquipmentInstance> act)
    {
        var e = Current();
        if (e == null) { GameEvents.Toast("請先選擇一件裝備"); return; }
        act(e);
        Refresh();
    }

    EquipmentInstance Current() =>
        selectedIndex >= 0 && selectedIndex < GameSave.Data.bags.Count ? GameSave.Data.bags[selectedIndex] : null;

    void BuildRows()
    {
        // 背包格子池：一次建滿上限，之後只開關 → 零 Instantiate
        int cap = InventorySystem.BagCapacity;
        while (cells.Count < cap)
        {
            var c = Instantiate(cellPrefab, bagGrid);
            c.SetActive(false);
            int idx = cells.Count;
            c.GetComponent<Button>().onClick.AddListener(() => { selectedIndex = idx; ShowDetail(); });
            cells.Add(c);
        }
    }

    void Refresh()
    {
        var bags = GameSave.Data.bags;
        for (int i = 0; i < cells.Count; i++)
        {
            bool on = i < bags.Count;
            cells[i].SetActive(on);
            if (!on) continue;
            var e = bags[i];
            var img = cells[i].GetComponentInChildren<Image>();
            if (img) img.color = e.QualityColor;
            var txt = cells[i].GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = $"{EquipmentDatabase.Get(e.defId).name}\n+{e.plus} Lv{e.level}";
        }
        // 槽位列刷新
        if (equipRow != null)
            for (int s = 0; s < equipRow.childCount && s < 6; s++)
            {
                var slotTxt = equipRow.GetChild(s).GetComponentInChildren<TMP_Text>();
                var e = GameSave.Data.equipped[s];
                if (slotTxt) slotTxt.text = SlotName((EquipSlot)s) + "\n" + (e ? $"{EquipmentDatabase.Get(e.defId).name} +{e.plus}" : "—");
            }
        if (selectedIndex >= bags.Count) selectedIndex = -1;
        ShowDetail();
    }

    static string SlotName(EquipSlot s) => s switch
    {
        EquipSlot.Weapon => "武器", EquipSlot.Head => "頭部", EquipSlot.Armor => "衣服",
        EquipSlot.Accessory => "飾品", EquipSlot.Artifact => "法器", _ => "鞋子"
    };

    void ShowDetail()
    {
        var e = Current();
        if (detailText == null) return;
        if (e == null) { detailText.text = "點選背包中的裝備查看"; return; }
        var def = EquipmentDatabase.Get(e.defId);
        var st = e.FinalStats();
        string sb = "";
        if (e.affixes != null) foreach (var a in e.affixes)
            sb += $"\n· {a.name} +{Format(a.value)}";
        detailText.text =
            $"<color=#{Hex(e.QualityColor)}>{def.name} [{e.QualityName}] +{e.plus}</color>\n" +
            $"部位：{SlotName(def.slot)}  戰力：{e.Power:N0}\n" +
            $"攻擊 {st.atk:N1} / 防禦 {st.def:N1} / 生命 {st.hp:N0}\n" +
            $"暴擊 {st.critRate:P1} / 速度 {st.speed:N2} / 特效 {st.special:N2}" + sb +
            (def.setId != SetId.None ? $"\n套裝：{def.setId}" : "");
    }

    static string Format(StatBlock s)
    {
        if (s.atk > 0) return $"{s.atk:P0}攻";
        if (s.def > 0) return $"{s.def * 100:N0}防";
        if (s.hp > 0) return $"{s.hp * 100:N0}命";
        if (s.critRate > 0) return $"{s.critRate:P1}暴";
        if (s.speed > 0) return $"{s.speed:P0}速";
        return $"{s.special:P0}效";
    }
    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}
