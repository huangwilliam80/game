using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentPanelUI : MonoBehaviour
{
    public Transform equipRow;
    public Transform bagGrid;
    public GameObject cellPrefab;
    public TMP_Text detailText;
    public Button btnEnhance, btnReforge, btnUpgradeQ, btnEquipSel, btnDecompose, btnCleanup;

    private EquipmentInstance selectedEquip;
    private int selectedBagIndex = -1;

    void OnEnable() {
        GameEvents.OnInventoryChanged += Refresh;
        GameEvents.OnHeroChanged += Refresh;
        btnEnhance.onClick.AddListener(() => { if(selectedEquip != null) InventorySystem.Enhance(selectedEquip); });
        btnReforge.onClick.AddListener(() => { if(selectedEquip != null) InventorySystem.Reforge(selectedEquip); });
        btnUpgradeQ.onClick.AddListener(() => { if(selectedEquip != null) InventorySystem.UpgradeQuality(selectedEquip); });
        btnEquipSel.onClick.AddListener(() => { if(selectedBagIndex >= 0) InventorySystem.Equip(selectedBagIndex); });
        btnDecompose.onClick.AddListener(() => { if(selectedEquip != null) InventorySystem.Decompose(selectedEquip); });
        btnCleanup.onClick.AddListener(() => InventorySystem.CleanupTrash());
        Refresh();
    }
    void OnDisable() {
        GameEvents.OnInventoryChanged -= Refresh;
        GameEvents.OnHeroChanged -= Refresh;
    }

    void Refresh() {
        for (int i = 0; i < equipRow.childCount; i++) {
            Button btn = equipRow.GetChild(i).GetComponent<Button>();
            EquipmentInstance eq = GameSave.Data.equipped[i];
            btn.GetComponentInChildren<TMP_Text>().text = eq != null ? EquipmentDatabase.Get(eq.defId).name : "空";
            btn.GetComponent<Image>().color = eq != null ? eq.QualityColor : new Color(0.2f, 0.22f, 0.3f);
            int slotIndex = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => InventorySystem.Unequip((EquipSlot)slotIndex));
        }

        foreach (Transform child in bagGrid) { if (child.name != "cellPrefab") Destroy(child.gameObject); }
        for (int i = 0; i < GameSave.Data.bags.Count; i++) {
            GameObject cell = Instantiate(cellPrefab, bagGrid);
            cell.name = $"Cell_{i}"; cell.SetActive(true);
            EquipmentInstance eq = GameSave.Data.bags[i];
            cell.GetComponentInChildren<TMP_Text>().text = $"+{eq.plus} {EquipmentDatabase.Get(eq.defId).name}";
            cell.GetComponent<Image>().color = eq.QualityColor;
            int bagIdx = i;
            cell.GetComponent<Button>().onClick.RemoveAllListeners();
            cell.GetComponent<Button>().onClick.AddListener(() => SelectItem(bagIdx));
        }
        detailText.text = "點擊背包裝備查看詳情";
    }

    void SelectItem(int bagIndex) {
        selectedBagIndex = bagIndex;
        selectedEquip = GameSave.Data.bags[bagIndex];
        var def = EquipmentDatabase.Get(selectedEquip.defId);
        StatBlock stats = selectedEquip.FinalStats();
        detailText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(selectedEquip.QualityColor)}>{def.name} +{selectedEquip.plus}</color>\n" +
                          $"品質: {selectedEquip.QualityName}\n攻擊: {stats.atk:F1} | 防禦: {stats.def:F1}\n" +
                          $"生命: {stats.hp:F0} | 暴擊: {stats.critRate*100:F1}%\n" +
                          $"速度: {stats.speed*100:F1}% | 特效: {stats.special*100:F1}%\n戰力: {selectedEquip.Power:N0}";
    }
}