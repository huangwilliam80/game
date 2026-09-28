using System;
using UnityEngine;

// ================= 共用型別定義 =================

/// <summary>裝備六大槽位。</summary>
public enum EquipSlot { Weapon = 0, Head = 1, Armor = 2, Accessory = 3, Artifact = 4, Boots = 5 }

/// <summary>品質階級：普通 → 精良 → 稀有 → 史詩 → 傳說。</summary>
public enum Quality { Common = 0, Fine = 1, Rare = 2, Epic = 3, Legend = 4 }

/// <summary>屬性容器（戰鬥與面板共用同一結構，方便加總）。</summary>
[Serializable]
public struct StatBlock
{
    public float atk;
    public float def;
    public float hp;
    public float critRate;
    public float speed;
    public float special;

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
    public string name;
    public StatBlock value;
}

/// <summary>套裝定義 id。</summary>
public enum SetId { None = 0, GhostSlayer = 1, CloudStep = 2, SpiritLord = 3 }

// ================= 裝備實例 =================

/// <summary>
/// 装备實例：存檔裡真正被序列化的是這個類別。
/// 只存「種子＋成長狀態」，其餘派生數值在运行时重算 → 存檔極小。
/// </summary>
[Serializable]
public class EquipmentInstance
{
    public int defId;
    public int quality = 0;
    public int plus = 0;
    public int level = 1;
    public int seed;
    public Affix[] affixes;
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
        int count = Mathf.Clamp(quality, 0, 4);

        var pool = new (string name, float atk, float def, float hp, float crit, float spd, float spc)[]
        {
            ("攻擊",.06f,0,0,0,0,0),("防禦",0,.08f,0,0,0,0),
            ("生命",0,0,.07f,0,0,0),("暴擊",0,0,0,.012f,0,0),
            ("速度",0,0,0,0,.03f,0),("特效",0,0,0,0,0,.05f),
        };

        affixes = new Affix[count];
        for (int i = 0; i < count; i++)
        {
            var p = pool[rng.Next(pool.Length)];
            float roll = 0.5f + (float)rng.NextDouble();
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

        // ★★★ 防空保護：找不到模板時返回空屬性 ★★★
        if (def == null)
        {
            Debug.LogWarning($"[裝備異常] 找不到 defId={defId} 的裝備模板，已自動忽略。");
            return new StatBlock();
        }

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