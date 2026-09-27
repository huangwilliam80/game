using System.Collections.Generic;
using UnityEngine;

/// <summary>裝備模板（靜態資料，不進存檔）。</summary>
[System.Serializable]
public class EquipmentDef
{
    public int id;
    public string name;
    public EquipSlot slot;
    public StatBlock baseStats;
    public SetId setId = SetId.None;
    public Sprite icon;               // 可選：留空則用色塊
    public string desc;
}

/// <summary>
/// 裝備資料庫：內建模板清單 ＋ 掉落／打造生成器。
/// 新手可直接在此列表增刪裝備（也可之後改用 ScriptableObject / JSON）。
/// </summary>
public static class EquipmentDatabase
{
    static readonly Dictionary<int, EquipmentDef> defs = new();
    static bool inited;

    public static IReadOnlyDictionary<int, EquipmentDef> All => defs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { inited = false; }

    public static void Init()
    {
        if (inited) return;
        inited = true;
        defs.Clear();

        // ---- 武器（主攻擊）----
        Add(101, "桃木劍", EquipSlot.Weapon, atk: 12, desc: "道門入門法器，驅鬼有奇效。");
        Add(102, "青蓮拂塵", EquipSlot.Weapon, atk: 20, crit: .05f, set: SetId.SpiritLord, desc: "靈主套裝部件。");
        Add(103, "滅鬼雷紋刀", EquipSlot.Weapon, atk: 34, crit: .08f, special: .1f, set: SetId.GhostSlayer, desc: "滅鬼套裝部件。");
        Add(104, "誅邪古劍", EquipSlot.Weapon, atk: 55, crit: .12f, special: .2f, set: SetId.GhostSlayer, desc: "傳說級滅鬼武器。");
        // ---- 頭部（防禦＋生命）----
        Add(201, "布帽", EquipSlot.Head, def: 6, hp: 40);
        Add(202, "銅鈴斗笠", EquipSlot.Head, def: 12, hp: 90, speed: .04f);
        Add(203, "雲紋天冠", EquipSlot.Head, def: 22, hp: 180, special: .12f, set: SetId.CloudStep);
        // ---- 衣服（生命＋防禦）----
        Add(301, "粗布道袍", EquipSlot.Armor, def: 8, hp: 70);
        Add(302, "八卦法衣", EquipSlot.Armor, def: 16, hp: 160, set: SetId.GhostSlayer);
        Add(303, "金縷法衣", EquipSlot.Armor, def: 28, hp: 300, crit: .04f, set: SetId.GhostSlayer);
        // ---- 飾品（暴擊／特效）----
        Add(401, "紅繩玉佩", EquipSlot.Accessory, crit: .06f, special: .08f);
        Add(402, "鎮魂鈴", EquipSlot.Accessory, crit: .10f, special: .15f, set: SetId.SpiritLord);
        Add(403, "九轉靈珠", EquipSlot.Accessory, crit: .16f, special: .28f, speed: .06f, set: SetId.SpiritLord);
        // ---- 法器（特效／攻擊）----
        Add(501, "羅盤", EquipSlot.Artifact, atk: 6, special: .12f);
        Add(502, "五雷令", EquipSlot.Artifact, atk: 12, special: .25f, crit: .05f);
        Add(503, "陰陽鏡", EquipSlot.Artifact, atk: 18, special: .4f, def: 8, set: SetId.CloudStep);
        // ---- 鞋子（速度）----
        Add(601, "草鞋", EquipSlot.Boots, speed: .08f, hp: 20);
        Add(602, "追風靴", EquipSlot.Boots, speed: .16f, hp: 45);
        Add(603, "雲履", EquipSlot.Boots, speed: .26f, hp: 80, def: 10, set: SetId.CloudStep);
    }

    static void Add(int id, string name, EquipSlot slot, float atk = 0, float def = 0, float hp = 0,
                    float crit = 0, float speed = 0, float special = 0, SetId set = SetId.None, string desc = "")
    {
        defs[id] = new EquipmentDef
        {
            id = id, name = name, slot = slot, setId = set, desc = desc,
            baseStats = new StatBlock { atk = atk, def = def, hp = hp, critRate = crit, speed = speed, special = special }
        };
    }

    public static EquipmentDef Get(int id) => defs.TryGetValue(id, out var d) ? d : null;

    public static List<EquipmentDef> BySlot(EquipSlot s)
    {
        var list = new List<EquipmentDef>();
        foreach (var kv in defs) if (kv.Value.slot == s) list.Add(kv.Value);
        list.Sort((a, b) => a.id.CompareTo(b.id));
        return list;
    }

    // ================= 生成器 =================

    /// <summary>品質權重：隨關卡深度提高稀有度（Roguelite「越深越好」體驗）。</summary>
    static float[] QualityWeights(int stageDepth)
    {
        float boost = Mathf.Clamp01(stageDepth / 60f);   // 0~1
        return new float[]
        {
            60 - 45 * boost,      // 普通
            26 + 4 * boost,       // 精良
            10 + 12 * boost,      // 稀有
            3.2f + 8 * boost,     // 史詩
            0.6f + 3f * boost,    // 傳說
        };
    }

    /// <summary>打怪／掛機掉落：指定槽位或隨機槽位。</summary>
    public static EquipmentInstance RollDrop(int stageDepth, EquipSlot? slot = null)
    {
        Init();
        var s = slot ?? (EquipSlot)GameMath.RandInt(0, 5);
        var pool = BySlot(s);
        var def = pool[GameMath.RandInt(0, pool.Count - 1)];   // 同槽位隨機模板

        var q = (int)GameMath.WeightedPick(new[] { Quality.Common, Quality.Fine, Quality.Rare, Quality.Epic, Quality.Legend },
                                            QualityWeights(stageDepth));

        int lvl = Mathf.Max(1, stageDepth / 3 + GameMath.RandInt(0, 2));

        return new EquipmentInstance
        {
            defId = def.id, quality = q, plus = 0, level = lvl,
            seed = Random.Range(int.MinValue, int.MaxValue),
            setId = def.setId
        }.Also(e => e.RegenerateAffixes());
    }

    /// <summary>首領掉落：保底稀有以上。</summary>
    public static EquipmentInstance RollBossDrop(int stageDepth)
    {
        var e = RollDrop(stageDepth + 15);
        if (e.quality < (int)Quality.Rare) { e.quality = (int)Quality.Rare; e.RegenerateAffixes(); }
        return e;
    }

    /// <summary>打造：玩家選槽位，消耗碎片產出。</summary>
    public static EquipmentInstance Forge(EquipSlot slot, int stageDepth)
    {
        var e = RollDrop(Mathf.Max(1, stageDepth - 5), slot);
        return e;
    }

    /// <summary>合成：三件同品質 → 一件高階品質（Roguelite 決策點）。</summary>
    public static EquipmentInstance Combine(List<EquipmentInstance> mats)
    {
        int q = Mathf.Min(mats[0].quality + 1, 4);
        var result = mats[0].Clone();
        result.quality = q;
        result.plus = 0;
        result.level = mats[0].level + 2;
        result.seed = Random.Range(int.MinValue, int.MaxValue);
        result.RegenerateAffixes();
        return result;
    }
}

/// <summary>鏈式小工具：讓初始化時能順帶執行方法。</summary>
public static class Ext
{
    public static T Also<T>(this T obj, System.Action<T> act) { act(obj); return obj; }
}
