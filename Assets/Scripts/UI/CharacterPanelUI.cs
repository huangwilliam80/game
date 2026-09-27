using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>功法／角色面板：顯示最終屬性 + 8 門功法一鍵升級。</summary>
public class CharacterPanelUI : MonoBehaviour
{
    public TMP_Text statText;
    public Transform gongfaRow;         // 子物體順序 = gongfa id
    public Button[] upgradeButtons;

    void OnEnable()
    {
        GameEvents.OnHeroChanged += Refresh;
        CharacterSystem.Init();
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            int id = i;
            upgradeButtons[i].onClick.AddListener(() => { if (CharacterSystem.UpgradeGongfa(id)) Refresh(); });
        }
        Refresh();
    }
    void OnDisable() => GameEvents.OnHeroChanged -= Refresh;

    void Refresh()
    {
        var s = CharacterSystem.FinalStats();
        var d = GameSave.Data;
        long need = GameMath.ExpToLevel(d.heroLevel);
        if (statText)
            statText.text =
                $"Lv.{d.heroLevel}  戰力 {TopBarUI.Num(CharacterSystem.Power())}\n" +
                $"經驗 {d.exp:N0}/{need:N0}\n" +
                $"攻擊 {s.atk:N1}｜防禦 {s.def:N1}｜生命 {s.hp:N0}\n" +
                $"暴擊 {s.critRate:P1}｜速度 {s.speed:N2}｜特效 {s.special:N2}";

        if (gongfaRow != null)
            for (int i = 0; i < gongfaRow.childCount && i < CharacterSystem.Gongfas.Count; i++)
            {
                var def = CharacterSystem.Gongfas[i];
                int lv = CharacterSystem.GongfaLevel(i);
                var txt = gongfaRow.GetChild(i).GetComponentInChildren<TMP_Text>();
                if (txt) txt.text = $"{def.name} Lv{lv}/{def.maxLevel}\n{def.desc}\n費用 {TopBarUI.Num(CharacterSystem.GongfaUpCost(i))}金";
            }
    }
}
