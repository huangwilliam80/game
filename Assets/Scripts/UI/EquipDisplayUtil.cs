using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 裝備相關顯示工具（名稱／品質／詞綴）。
/// 抽成一個地方，避免 RunResultUI / EquipmentPanelUI 各寫一份而不同步。
/// </summary>
public static class EquipDisplayUtil
{
    public static readonly string[] QualityNames = { "普通", "精良", "稀有", "史詩", "傳說" };
    public static readonly Color[] QualityColors =
    {
        new Color(.80f, .80f, .80f), new Color(.55f, .90f, .50f), new Color(.40f, .70f, 1f),
        new Color(.75f, .45f, 1f), new Color(1f, .75f, .25f)
    };

    public static string SlotName(EquipSlot s) => s switch
    {
        EquipSlot.Weapon => "武器", EquipSlot.Head => "頭部", EquipSlot.Armor => "衣服",
        EquipSlot.Accessory => "飾品", EquipSlot.Artifact => "法器", _ => "鞋子"
    };

    public static string Name(EquipmentInstance e)
    {
        if (e == null) return "未知";
        var def = EquipmentDatabase.Get(e.defId);
        return def != null ? def.name : "未知装备";
    }

    public static string QualityName(int q) =>
        QualityNames[Mathf.Clamp(q, 0, QualityNames.Length - 1)];

    public static Color QualityColor(int q) =>
        QualityColors[Mathf.Clamp(q, 0, QualityColors.Length - 1)];

    /// <summary>背包格子用的超短標籤。</summary>
    public static string ShortLabel(EquipmentInstance e) =>
        e == null ? "?" : $"{QualityName(e.quality)} {Name(e)}\nLv.{e.level} +{e.plus}";

    /// <summary>詳情面板：完整屬性 + 詞綴。</summary>
    public static string Describe(EquipmentInstance e)
    {
        if (e == null) return "(未選擇裝備)";
        var def = EquipmentDatabase.Get(e.defId);
        string txt = $"【{QualityName(e.quality)}】{(e.plus > 0 ? "+" + e.plus + " " : "")}{Name(e)}";
        txt += $"\n部位：{SlotName(def != null ? def.slot : EquipSlot.Weapon)}｜Lv.{e.level}｜戰力 {e.Power:N0}";
        if (def != null && def.setId != SetId.None) txt += $"｜套裝：{SetLabel(def.setId)}";
        txt += "\n────────────\n";

        var s = e.FinalStats();
        if (s.atk > .01f)     txt += $"攻擊 {s.atk:N1}\n";
        if (s.def > .01f)     txt += $"防禦 {s.def:N1}\n";
        if (s.hp > .01f)      txt += $"生命 {s.hp:N0}\n";
        if (s.critRate > .001f) txt += $"暴擊 {s.critRate:P1}\n";
        if (s.speed > .001f)  txt += $"速度 {s.speed:F2}\n";
        if (s.special > .001f) txt += $"特效 {s.special:F2}\n";

        if (e.affixes != null && e.affixes.Length > 0)
        {
            txt += "\n詞綴：";
            for (int i = 0; i < e.affixes.Length; i++)
                txt += $"「{AffixText(e.affixes[i])}」";
        }
        return txt;
    }

    public static string AffixText(Affix a)
    {
        var v = a.value;
        if (v.atk > .001f) return $"攻擊 +{v.atk * 100f:F0}%";
        if (v.def > .001f) return $"防禦 +{v.def * 100f:F0}%";
        if (v.hp > .001f) return $"生命 +{v.hp * 100f:F0}%";
        if (v.critRate > .0001f) return $"暴擊 +{v.critRate * 100f:F1}%";
        if (v.speed > .0001f) return $"速度 +{v.speed * 100f:F0}%";
        return $"特效 +{v.special * 100f:F0}%";
    }

    public static string SetLabel(SetId id) => id switch
    {
        SetId.GhostSlayer => "滅鬼",
        SetId.CloudStep => "雲履",
        SetId.SpiritLord => "靈主",
        _ => "無"
    };

    /// <summary>把 TMP 文字染成該裝備的品質色（找不到就跳過）。</summary>
    public static void ApplyQualityColor(GameObject go, EquipmentInstance e)
    {
        if (go == null || e == null) return;
        var t = go.GetComponentInChildren<TMP_Text>();
        if (t != null) t.color = e.QualityColor;
    }
}
