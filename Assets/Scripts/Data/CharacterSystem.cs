using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>功法定義（Roguelite 永久強化，用金幣／靈玉升級）。</summary>
[System.Serializable]
public class GongfaDef
{
    public int id;
    public string name;
    public string desc;
    public StatBlock perLevel;      // 每級加成
    public int maxLevel = 20;
}

/// <summary>
/// 角色系統：等級、功法、套裝效果、最終屬性聚合。
/// 所有面板戰力都從這裡算 → 單一數據源，UI 永遠一致。
/// </summary>
public static class CharacterSystem
{
    static readonly List<GongfaDef> gongfas = new List<GongfaDef>();
    static bool inited;

    public static IReadOnlyList<GongfaDef> Gongfas => gongfas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { inited = false; }

    public static void Init()
    {
        if (inited) return;
        inited = true;
        gongfas.Clear();
        gongfas.Add(new GongfaDef { id = 0, name = "葵花煉氣訣", desc = "每級 +攻擊", perLevel = SB(atk: 2.2f) });
        gongfas.Add(new GongfaDef { id = 1, name = "金鐘護體", desc = "每級 +防禦", perLevel = SB(def: 2.0f) });
        gongfas.Add(new GongfaDef { id = 2, name = "龜息長生功", desc = "每級 +生命", perLevel = SB(hp: 26f) });
        gongfas.Add(new GongfaDef { id = 3, name = "破妄劍意", desc = "每級 +暴擊率", perLevel = SB(crit: .008f) });
        gongfas.Add(new GongfaDef { id = 4, name = "凌波微步", desc = "每級 +速度", perLevel = SB(speed: .012f) });
        gongfas.Add(new GongfaDef { id = 5, name = "天師法旨", desc = "每級 +特效強度", perLevel = SB(special: .02f) });
        gongfas.Add(new GongfaDef { id = 6, name = "聚寶秘術", desc = "掛機收益 +4%/級", perLevel = new StatBlock() });
        gongfas.Add(new GongfaDef { id = 7, name = "煉器心法", desc = "打造費用 -3%/級", perLevel = new StatBlock() });
    }

    static StatBlock SB(float atk = 0, float def = 0, float hp = 0, float crit = 0, float speed = 0, float special = 0)
        => new StatBlock { atk = atk, def = def, hp = hp, critRate = crit, speed = speed, special = special };

    // ================= 等級 / 經驗 =================

    /// <summary>餵經驗並自動升級；回傳是否升級。</summary>
    public static bool AddExp(long amount)
    {
        var d = GameSave.Data;
        d.exp += amount;
        bool leveled = false;
        while (d.exp >= GameMath.ExpToLevel(d.heroLevel))
        {
            d.exp -= GameMath.ExpToLevel(d.heroLevel);
            d.heroLevel++;
            leveled = true;
        }
        if (leveled) { GameEvents.RaiseHero(); GameEvents.Toast($"突破至 {d.heroLevel} 級！"); }
        return leveled;
    }

    // ================= 屬性聚合 =================

    /// <summary>英雄基礎屬性（含等級成長）。</summary>
    public static StatBlock BaseStats()
    {
        int lv = GameSave.Data.heroLevel;
        return new StatBlock
        {
            atk = 8 + lv * 1.6f,
            def = 4 + lv * 1.1f,
            hp = 90 + lv * 22f,
            critRate = .05f + lv * .001f,
            speed = .1f + lv * .004f,
            special = lv * .01f,
        };
    }

    /// <summary>功法總和。</summary>
    public static StatBlock GongfaStats()
    {
        Init();
        var total = new StatBlock();
        var d = GameSave.Data;
        for (int i = 0; i < gongfas.Count && i < d.gongfaLevel.Length; i++)
        {
            int lv = d.gongfaLevel[i];
            var g = gongfas[i].perLevel;
            total.atk += g.atk * lv; total.def += g.def * lv; total.hp += g.hp * lv;
            total.critRate += g.critRate * lv; total.speed += g.speed * lv; total.special += g.special * lv;
        }
        return total;
    }

    public static int GongfaLevel(int id) =>
        GameSave.Data.gongfaLevel != null && id < GameSave.Data.gongfaLevel.Length ? GameSave.Data.gongfaLevel[id] : 0;

    public static int GongfaUpCost(int id) => (int)(250 * Mathf.Pow(1.42f, GongfaLevel(id)));

    /// <summary>升級功法（消耗金幣）。回傳成功與否。</summary>
    public static bool UpgradeGongfa(int id)
    {
        Init();
        var def = gongfas[id];
        if (GongfaLevel(id) >= def.maxLevel) { GameEvents.Toast("已達功法上限"); return false; }
        int cost = GongfaUpCost(id);
        if (GameSave.Data.gold < cost) { GameEvents.Toast("金幣不足"); return false; }
        GameSave.Data.gold -= cost;
        GameSave.Data.gongfaLevel[id]++;
        GameEvents.RaiseCurrency(CurrencyType.Gold, GameSave.Data.gold);
        GameEvents.RaiseHero();
        return true;
    }

    /// <summary>套裝統計：回傳各套裝「已穿戴件數」。</summary>
    public static Dictionary<SetId, int> CountSets()
    {
        var dic = new Dictionary<SetId, int>();
        foreach (var e in GameSave.Data.equipped)
        {
            if (e == null || e.setId == SetId.None) continue;
            dic.TryGetValue(e.setId, out int c);
            dic[e.setId] = c + 1;
        }
        return dic;
    }

    /// <summary>套裝加成（2件小成、4件大成）。</summary>
    public static StatBlock SetBonus()
    {
        var s = new StatBlock();
        foreach (var kv in CountSets())
        {
            switch (kv.Key)
            {
                case SetId.GhostSlayer:   // 滅鬼：攻擊＋暴擊
                    if (kv.Value >= 2) s.atk += 15;
                    if (kv.Value >= 4) s.critRate += .12f;
                    break;
                case SetId.CloudStep:     // 雲履：速度＋生命
                    if (kv.Value >= 2) s.speed += .15f;
                    if (kv.Value >= 4) s.hp += 250;
                    break;
                case SetId.SpiritLord:    // 靈主：特效＋防禦
                    if (kv.Value >= 2) s.special += .2f;
                    if (kv.Value >= 4) s.def += 30;
                    break;
            }
        }
        return s;
    }

    /// <summary>最终戰鬥屬性＝基礎＋装备＋功法＋套裝。</summary>
    public static StatBlock FinalStats()
    {
        var t = BaseStats() + GongfaStats() + SetBonus();
        foreach (var e in GameSave.Data.equipped)
            if (e != null) t = t + e.FinalStats();
        t.critRate = Mathf.Clamp(t.critRate, 0, .85f);
        return t;
    }

    public static long Power() 
    { 
        var s = FinalStats(); 
        return GameMath.EstimatePower(s.atk, s.def, s.hp, s.critRate, s.speed, s.special); 
    }
    /// <summary>聚寶秘術：掛機收益加成係數。</summary>
    public static float IdleBonus() => 1f + GongfaLevel(6) * .04f;
    /// <summary>煉器心法：打造折扣。</summary>
    public static float ForgeDiscount() => Mathf.Clamp(1f - GongfaLevel(7) * .03f, .5f, 1f);
}
