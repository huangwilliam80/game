using System;
using UnityEngine;

/// <summary>裝備六大槽位。</summary>
public enum EquipSlot { Weapon = 0, Head = 1, Armor = 2, Accessory = 3, Artifact = 4, Boots = 5 }

/// <summary>品質階級：普通 → 精良 → 稀有 → 史詩 → 傳說。</summary>
public enum Quality { Common = 0, Fine = 1, Rare = 2, Epic = 3, Legend = 4 }

/// <summary>屬性容器（戰鬥與面板共用同一結構，方便加總）。</summary>
[Serializable]
public struct StatBlock
{
    public float atk;        // 攻擊
    public float def;        // 防禦
    public float hp;         // 生命
    public float critRate;   // 暴擊率 0~1
    public float speed;      // 速度（攻擊間隔縮短）
    public float special;    // 特殊效果強度（技能傷害 / 治療加成）

    public static StatBlock operator +(StatBlock a, StatBlock b) => new StatBlock
    {
        atk = a.atk + b.atk, def = a.def + b.def, hp = a.hp + b.hp,
        critRate = a.critRate + b.critRate, speed = a.speed + b.speed, special = a.special + b.special
    };
}

/// <summary>詞綴（洗鍊產出的隨機小屬性）。</summary>
[Serializable]
public struct Affix
{
    public string name;     // 顯示名
    public StatBlock value; // 數值
}

/// <summary>套裝定義 id。</summary>
public enum SetId { None = 0, GhostSlayer = 1 /*滅鬼*/, CloudStep = 2 /*雲履*/, SpiritLord = 3 /*靈主*/ }

/// <summary>
/// 装备實例：存檔裡真正被序列化的是這個類別。
/// 只存「種子＋成長狀態」，其餘派生數值在运行时重算 → 存檔極小。
/// </summary>
[Serializable]
public class EquipmentInstance
{
    public int defId;        // 對應 EquipmentDef 模板
    public int quality = 0;  // Quality 枚舉序號（可升階改變）
    public int plus = 0;     // 強化等級 +0..+15
    public int level = 1;    // 掉落時的基礎等級（影響數值規模）
    public int seed;         // 詞綴隨機種子（保證同樣子→同詞綴，可還原）
    public Affix[] affixes;  // 洗鍊詞綴
    public SetId setId = SetId.None;

    static readonly Color[] QColor =
    {
        new Color(.8f,.8f,.8f), new Color(.55f,.9f,.5f), new Color(.4f,.7f,1f),
        new Color(.75f,.45f,1f), new Color(1f,.75f,.25f)
    };

    public Color QualityColor => QColor[Mathf.Clamp(quality, 0, 4)];
    public string QualityName => ((Quality)quality) switch
    {
        Quality.Common => "普通", Quality.Fine => "精良", Quality.Rare => "稀有",
        Quality.Epic => "史詩", Quality.Legend => "傳說", _ => "普通"
    };

    /// <summary>依種子重建詞綴（載入存檔時呼叫）。</summary>
    public void RegenerateAffixes()
    {
        var rng = new System.Random(seed);
        int count = Mathf.Clamp(quality, 0, 4);           // 品質決定詞綴數量
        // (名稱, atk, def, hp, crit, speed, special) —— 每種詞綴只填一個欄位
        var pool = new (string name, float atk, float def, float hp, float crit, float spd, float spc)[]
        {
            ("攻擊", .06f, 0, 0, 0, 0, 0), ("防禦", 0, .08f, 0, 0, 0, 0),
            ("生命", 0, 0, .07f, 0, 0, 0), ("暴擊", 0, 0, 0, .012f, 0, 0),
            ("速度", 0, 0, 0, 0, .03f, 0), ("特效", 0, 0, 0, 0, 0, .05f),
        };
        affixes = new Affix[count];
        for (int i = 0; i < count; i++)
        {
            var p = pool[rng.Next(pool.Length)];
            float roll = 0.5f + (float)rng.NextDouble();  // 隨機波動
            affixes[i] = new Affix
            {
                name = p.name,
                value = new StatBlock
                {
                    atk = p.atk * roll, def = p.def * roll, hp = p.hp * roll,
                    critRate = p.crit * roll, speed = p.spd * roll, special = p.spc * roll
                }
            };
        }
    }

    /// <summary>最終屬性 = 模板基礎 × 品質倍率 × 強化倍率 ＋ 詞綴。</summary>
    public StatBlock FinalStats()
    {
        var def = EquipmentDatabase.Get(defId);
        float qm = 1f + quality * 0.35f;
        float pm = 1f + plus * 0.09f;
        float lm = 1f + (level - 1) * 0.12f;
        var baseS = def.baseStats;
        baseS.atk *= qm * pm * lm; baseS.def *= qm * pm * lm; baseS.hp *= qm * pm * lm;
        baseS.critRate *= (1f + quality * 0.15f); baseS.speed *= (1f + quality * 0.1f);
        baseS.special *= qm * pm;
        StatBlock total = baseS;
        if (affixes != null) foreach (var a in affixes) total = total + a.value;
        return total;
    }

    public long Power => GameMath.EstimatePower(FinalStats());

    /// <summary>深拷貝（用於合成／展示，避免污染存檔物件）。</summary>
    public EquipmentInstance Clone()
    {
        var e = (EquipmentInstance)MemberwiseClone();
        if (affixes != null) e.affixes = (Affix[])affixes.Clone();
        return e;
    }
}
