using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>鍛造面板：選部位 → 消耗碎片打造；以及三合一合成的快捷入口。</summary>
public class ForgePanelUI : MonoBehaviour
{
    public Button[] slotButtons;        // 6 個部位按鈕
    public TMP_Text infoText;
    public Button backBtn;
    EquipSlot chosen = EquipSlot.Weapon;

    bool listenersAdded;
    void OnEnable()
    {
        if (!listenersAdded)
        {
            listenersAdded = true;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var s = (EquipSlot)i;
                slotButtons[i].onClick.AddListener(() => { chosen = s; Refresh(); });
            }
            if (backBtn) backBtn.onClick.AddListener(() => UIManager.I.CloseAll());
        }
        GameEvents.OnInventoryChanged += Refresh;
        GameEvents.OnCurrencyChanged += OnCur;
        Refresh();
    }
    void OnDisable() { GameEvents.OnInventoryChanged -= Refresh; GameEvents.OnCurrencyChanged -= OnCur; }
    void OnCur(CurrencyType t, long v) => Refresh();

    public void DoForge()
    {
        if (InventorySystem.ForgeItem(chosen))
            GameEvents.Toast($"鍛造出一件 {chosen} 部位裝備！");
        Refresh();
    }

    void Refresh()
    {
        if (infoText)
            infoText.text = $"選擇部位後鍛造（消耗 {InventorySystem.ForgeCost} 碎片）\n" +
                            $"目前碎片：{TopBarUI.Num(GameSave.Data.forgeShard)}\n" +
                            $"已選：{SlotName(chosen)}";
    }

    static string SlotName(EquipSlot s) => s switch
    {
        EquipSlot.Weapon => "武器", EquipSlot.Head => "頭部", EquipSlot.Armor => "衣服",
        EquipSlot.Accessory => "飾品", EquipSlot.Artifact => "法器", _ => "鞋子"
    };
}
