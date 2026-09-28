using System;
using UnityEngine;

public enum EquipSlot { Weapon = 0, Head = 1, Armor = 2, Accessory = 3, Artifact = 4, Boots = 5 }
public enum Quality { Common = 0, Fine = 1, Rare = 2, Epic = 3, Legend = 4 }

[Serializable]
public struct StatBlock
{
    public float atk, def, hp, critRate, speed, special;
    public static StatBlock operator +(StatBlock a, StatBlock b) => new StatBlock {
        atk = a.atk + b.atk, def = a.def + b.def, hp = a.hp + b.hp,
        critRate = a.critRate + b.critRate, speed = a.speed + b.speed, special = a.special + b.special
    };
}

[Serializable]
public struct Affix { public string name; public StatBlock value; }
public enum SetId { None = 0, GhostSlayer = 1, CloudStep = 2, SpiritLord = 3 }

[Serializable]
public class EquipmentInstance
{
    public int defId, quality = 0, plus = 0, level = 1, seed;
    public Affix[] affixes;
    public SetId setId = SetId.None;

    static readonly Color[] QColor = { new Color(.8f,.8f,.8f), new Color(.55f,.9f,.5f), new Color(.4f,.7f,1f), new Color(.75f,.45f,1f), new Color(1f,.75f,.25f) };
    public Color QualityColor => QColor[Mathf.Clamp(quality, 0, 4)];
    
    public string QualityName
    {
        get {
            switch ((Quality)quality) {
                case Quality.Common: return "普通"; case Quality.Fine: return "精良";
                case Quality.Rare: return "稀有"; case Quality.Epic: return "史詩";
                case Quality.Legend: return "傳說"; default: return "普通";
            }
        }
    }

    // ★ 補齊 Clone 方法
    public EquipmentInstance Clone()
    {
        return new EquipmentInstance
        {
            defId = this.defId,
            quality = this.quality,
            plus = this.plus,
            level = this.level,
            seed = this.seed,
            setId = this.setId,
            affixes = this.affixes != null ? (Affix[])this.affixes.Clone() : null
        };
    }

    public void RegenerateAffixes()
    {
        var rng = new System.Random(seed);
        int count = Mathf.Clamp(quality, 0, 4);
        var pool = new (string name, float atk, float def, float hp, float crit, float spd, float spc)[] {
            ("攻擊", .06f, 0, 0, 0, 0, 0), ("防禦", 0, .08f, 0, 0, 0, 0), ("生命", 0, 0, .07f, 0, 0, 0),
            ("暴擊", 0, 0, 0, .012f, 0, 0), ("速度", 0, 0, 0, 0, .03f, 0), ("特效", 0, 0, 0, 0, 0, .05f)
        };
        affixes = new Affix[count];
        for (int i = 0; i < count; i++) {
            var p = pool[rng.Next(pool.Length)];
            float roll = 0.5f + (float)rng.NextDouble();
            affixes[i] = new Affix { name = p.name, value = new StatBlock { atk = p.atk * roll, def = p.def * roll, hp = p.hp * roll, critRate = p.crit * roll, speed = p.spd * roll, special = p.spc * roll } };
        }
    }

    public StatBlock FinalStats()
    {
        var def = EquipmentDatabase.Get(defId);
        float qm = 1f + quality * 0.35f, pm = 1f + plus * 0.09f, lm = 1f + (level - 1) * 0.12f;
        var baseS = def.baseStats;
        baseS.atk *= qm * pm * lm; baseS.def *= qm * pm * lm; baseS.hp *= qm * pm * lm;
        baseS.critRate *= (1f + quality * 0.15f); baseS.speed *= (1f + quality * 0.1f); baseS.special *= qm * pm;
        StatBlock total = baseS;
        if (affixes != null) foreach (var a in affixes) total = total + a.value;
        return total;
    }

    public long SellPrice => (long)(10 * (quality + 1) * (quality + 1) + plus * 5 + level * 2);

    public long Power => GameMath.EstimatePower(FinalStats().atk, FinalStats().def, FinalStats().hp, FinalStats().critRate, FinalStats().speed, FinalStats().special);
}