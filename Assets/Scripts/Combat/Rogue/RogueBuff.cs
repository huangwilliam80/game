using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Roguelite 臨時 buff 定義(只存在於單場 run,不存檔)。</summary>
[Serializable]
public class RogueBuffDef
{
    public string id;
    public string name;
    public string desc;
    public int rarity = 1;              // 1 普通 / 2 稀有 / 3 傳說
    public float atkMul = 1f;
    public float hpMul = 1f;
    public float defMul = 1f;
    public float critAdd = 0f;
    public float lifesteal = 0f;
    public float speedMul = 1f;
    public int extraHits = 0;
    public float healPerWave = 0f;      // 每波結束回復 % 最大生命
    public float executeBonus = 0f;     // 敵人血量 <30% 時增傷
}

public static class RogueBuffPool
{
    static readonly List<RogueBuffDef> all = new List<RogueBuffDef>();

    public static void Init()
    {
        if (all.Count > 0) return;
        all.Add(new RogueBuffDef { id = "sword",    name = "劍意",     desc = "攻擊 +15%",                rarity = 1, atkMul = 1.15f });
        all.Add(new RogueBuffDef { id = "wall",     name = "鐵壁",     desc = "防禦 +20%",                rarity = 1, defMul = 1.20f });
        all.Add(new RogueBuffDef { id = "vital",    name = "生機",     desc = "生命 +20%",                rarity = 1, hpMul = 1.20f });
        all.Add(new RogueBuffDef { id = "focus",    name = "會心",     desc = "暴擊率 +10%",              rarity = 1, critAdd = 0.10f });
        all.Add(new RogueBuffDef { id = "swift",    name = "疾步",     desc = "攻速 +15%",                rarity = 1, speedMul = 1.15f });
        all.Add(new RogueBuffDef { id = "leech",    name = "吸血",     desc = "造成傷害的 8% 轉為治療",    rarity = 2, lifesteal = 0.08f });
        all.Add(new RogueBuffDef { id = "medic",    name = "療傷",     desc = "每波結束回復 25% 生命",     rarity = 2, healPerWave = 0.25f });
        all.Add(new RogueBuffDef { id = "rage",     name = "狂怒",     desc = "攻擊 +35%、生命 -10%",      rarity = 2, atkMul = 1.35f, hpMul = 0.90f });
        all.Add(new RogueBuffDef { id = "exec",     name = "斬殺",     desc = "敵人血量低於 30% 時增傷 50%", rarity = 2, executeBonus = 0.50f });
        all.Add(new RogueBuffDef { id = "double",   name = "連擊",     desc = "每次攻擊額外出手 1 次",     rarity = 3, extraHits = 1 });
        all.Add(new RogueBuffDef { id = "blade",    name = "劍神之勢", desc = "攻擊 +25%、暴擊 +10%",      rarity = 3, atkMul = 1.25f, critAdd = 0.10f });
        all.Add(new RogueBuffDef { id = "immortal", name = "不動明王", desc = "生命 +30%、防禦 +25%",      rarity = 3, hpMul = 1.30f, defMul = 1.25f });
    }

    /// <summary>抽 n 張不重複卡牌;同一 buff 本 run 最多疊 3 層。</summary>
    public static List<RogueBuffDef> Draft(int n, List<RogueBuffDef> owned)
    {
        Init();
        var result = new List<RogueBuffDef>();
        var pool = new List<RogueBuffDef>();

        for (int i = 0; i < all.Count; i++)
        {
            int count = 0;
            if (owned != null)
                for (int j = 0; j < owned.Count; j++)
                    if (owned[j].id == all[i].id) count++;
            if (count >= 3) continue;
            pool.Add(all[i]);
        }

        for (int pick = 0; pick < n && pool.Count > 0; pick++)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++) total += Weight(pool[i].rarity);
            float r = UnityEngine.Random.value * total;
            int idx = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                r -= Weight(pool[i].rarity);
                if (r <= 0f) { idx = i; break; }
            }
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }

    static float Weight(int rarity)
    {
        switch (rarity)
        {
            case 2: return 30f;
            case 3: return 10f;
            default: return 60f;
        }
    }
}