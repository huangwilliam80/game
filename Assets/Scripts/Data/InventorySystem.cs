using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包／穿戴／強化／升階／洗鍊／合成／打造 —— 裝備系統的全部「操作」。
/// 純邏輯靜態類別：不依賴場景物件，方便單元測試與 UI 直接呼叫。
/// </summary>
public static class InventorySystem
{
    public const int BagCapacity = 60;

    static SaveData D => GameSave.Data;

    // ================= 撿取 / 丟棄 =================

    /// <summary>戰利品入包（自動撿取用）。回傳是否成功。</summary>
    public static bool AddToBag(EquipmentInstance e)
    {
        if (D.bags.Count >= BagCapacity)
        {
            // 包滿時自動分解最弱的一件（放置遊戲的減負設計）
            if (AutoDecomposeIfNeeded(e)) return true;
            GameEvents.Toast("背包已滿！請整理後再領取");
            return false;
        }
        D.bags.Add(e);
        GameEvents.RaiseLoot(e);
        GameEvents.RaiseInventory();
        return true;
    }

    static bool AutoDecomposeIfNeeded(EquipmentInstance incoming)
    {
        // 找一件比新貨差的分解掉
        int worst = -1; long worstPower = long.MaxValue;
        for (int i = 0; i < D.bags.Count; i++)
        {
            long p = D.bags[i].Power;
            if (p < worstPower) { worstPower = p; worst = i; }
        }
        if (worst >= 0 && incoming.Power > worstPower)
        {
            Decompose(D.bags[worst]);   // Decompose 內部會 RaiseInventory
            D.bags.Add(incoming);
            GameEvents.RaiseLoot(incoming);
            GameEvents.RaiseInventory();
            return true;
        }
        return false;
    }

    /// <summary>丟棄（返還鍛造碎片）。</summary>
    public static void Discard(int bagIndex)
    {
        if (bagIndex < 0 || bagIndex >= D.bags.Count) return;
        var e = D.bags[bagIndex];
        D.bags.RemoveAt(bagIndex);
        D.forgeShard += QualityShard(e.quality);
        GameEvents.RaiseCurrency(CurrencyType.ForgeShard, D.forgeShard);
        GameEvents.RaiseInventory();
    }

    /// <summary>分解（給碎片，比丟棄多一點）。</summary>
    public static void Decompose(EquipmentInstance e)
    {
        int idx = D.bags.IndexOf(e);
        if (idx < 0) return;
        D.bags.RemoveAt(idx);
        D.forgeShard += QualityShard(e.quality) + 2;
        D.gold += 30 * (e.quality + 1);
        GameEvents.RaiseCurrency(CurrencyType.ForgeShard, D.forgeShard);
        GameEvents.RaiseCurrency(CurrencyType.Gold, D.gold);
        GameEvents.RaiseInventory();
    }

    static int QualityShard(int q) => (q + 1) * (q + 1) * 3;

    /// <summary>一鍵分解所有未穿戴的低品質（≤精良且 plus==0）。</summary>
    public static int CleanupTrash()
    {
        int n = 0;
        for (int i = D.bags.Count - 1; i >= 0; i--)
        {
            var e = D.bags[i];
            if (e.quality <= (int)Quality.Fine && e.plus == 0) { Decompose(e); n++; }
        }
        GameEvents.Toast(n > 0 ? $"分解了 {n} 件垃圾裝備" : "沒有可分解的垃圾裝備");
        return n;
    }

    // ================= 穿戴 =================

    public static void Equip(int bagIndex)
    {
        if (bagIndex < 0 || bagIndex >= D.bags.Count) return;
        var e = D.bags[bagIndex];
        var slot = EquipmentDatabase.Get(e.defId).slot;
        int si = (int)slot;

        // 同槽位互換：舊的放回背包
        var old = D.equipped[si];
        D.equipped[si] = e;
        D.bags.RemoveAt(bagIndex);
        if (old != null) D.bags.Add(old);

        GameEvents.RaiseInventory();
        GameEvents.RaiseHero();
        GameEvents.Toast($"穿戴 {EquipmentDatabase.Get(e.defId).name}");
    }

    public static void Unequip(EquipSlot slot)
    {
        int si = (int)slot;
        var old = D.equipped[si];
        if (old == null) return;
        if (D.bags.Count >= BagCapacity) { GameEvents.Toast("背包已滿"); return; }
        D.equipped[si] = null;
        D.bags.Add(old);
        GameEvents.RaiseInventory();
        GameEvents.RaiseHero();
    }

    /// <summary>智能穿戴：從背包挑本槽位最強的一件穿上。</summary>
    public static void SmartEquipBest(EquipSlot slot)
    {
        int best = -1; long bp = long.MinValue;
        for (int i = 0; i < D.bags.Count; i++)
        {
            var def = EquipmentDatabase.Get(D.bags[i].defId);
            if (def.slot != slot) continue;
            long p = D.bags[i].Power;
            if (p > bp) { bp = p; best = i; }
        }
        if (best >= 0) Equip(best);
        else GameEvents.Toast("背包中沒有此部位裝備");
    }

    // ================= 成長四操作 =================

    /// <summary>強化 +1（金幣，有失敗率；失敗不降级只扣錢 → 對新手友善）。</summary>
    public static bool Enhance(EquipmentInstance e)
    {
        if (e.plus >= 15) { GameEvents.Toast("已達 +15 上限"); return false; }
        int cost = GameMath.EnhanceCost(e.plus);
        if (D.gold < cost) { GameEvents.Toast("金幣不足"); return false; }
        D.gold -= cost;
        if (GameMath.Chance(GameMath.EnhanceRate(e.plus)))
        {
            e.plus++;
            GameEvents.Toast($"{EquipmentDatabase.Get(e.defId).name} 強化成功 +{e.plus}！");
        }
        else GameEvents.Toast("強化失敗…材料燒毀");
        GameEvents.RaiseCurrency(CurrencyType.Gold, D.gold);
        GameEvents.RaiseInventory();
        GameEvents.RaiseHero();
        return true;
    }

    /// <summary>升階：消耗 3 件同品質墊材 → 品質 +1。</summary>
    public static bool UpgradeQuality(EquipmentInstance target)
    {
        if (target.quality >= 4) { GameEvents.Toast("已是傳說品質"); return false; }
        if (D.bags.Count >= BagCapacity) { /* 無所謂 */ }
        int need = 3;
        var mats = new List<EquipmentInstance>();
        foreach (var b in D.bags)
            if (b != target && b.quality == target.quality) { mats.Add(b); if (mats.Count >= need) break; }
        if (mats.Count < need) { GameEvents.Toast($"需要 {need} 件同品質墊材"); return false; }

        foreach (var m in mats) Decompose(m);   // 墊材轉碎片後再回收一部分
        target.quality++;
        D.forgeShard = Math.Max(0L, D.forgeShard - need * 2L);
        target.RegenerateAffixes();
        GameEvents.RaiseInventory();
        GameEvents.RaiseHero();
        GameEvents.Toast($"升階成功！→ {target.QualityName}");
        return true;
    }

    /// <summary>洗鍊：重Roll詞綴（消耗靈玉）。</summary>
    public static bool Reforge(EquipmentInstance e)
    {
        int cost = GameMath.ReforgeCost(e.quality);
        if (D.spiritCrystal < cost) { GameEvents.Toast("靈玉不足"); return false; }
        D.spiritCrystal -= cost;
        e.seed = GameMath.RandInt(int.MinValue, int.MaxValue - 1);
        e.RegenerateAffixes();
        GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, D.spiritCrystal);
        GameEvents.RaiseInventory();
        GameEvents.RaiseHero();
        GameEvents.Toast("洗鍊完成，查看新詞綴！");
        return true;
    }

    /// <summary>三合一合成：選 3 件背包裝備 → 高階品質。</summary>
    public static bool CombineThree(List<int> bagIndices)
    {
        if (bagIndices == null || bagIndices.Count != 3) { GameEvents.Toast("請選擇 3 件裝備"); return false; }
        var mats = new List<EquipmentInstance>();
        foreach (int i in bagIndices)
        {
            if (i < 0 || i >= D.bags.Count) return false;
            mats.Add(D.bags[i]);
        }
        if (mats[0].quality != mats[1].quality || mats[1].quality != mats[2].quality)
        { GameEvents.Toast("必須是同品質的三件"); return false; }
        if (mats[0].quality >= 4) { GameEvents.Toast("已是最高品質"); return false; }

        // 先移除再產出，避免索引錯亂
        foreach (int i in bagIndices) { } // 佔位
        var sorted = new List<int>(bagIndices); sorted.Sort();
        for (int k = sorted.Count - 1; k >= 0; k--) D.bags.RemoveAt(sorted[k]);

        var result = EquipmentDatabase.Combine(mats);
        D.bags.Add(result);
        GameEvents.RaiseInventory();
        GameEvents.RaiseHero();
        GameEvents.Toast($"合成成功 → {result.QualityName}·{EquipmentDatabase.Get(result.defId).name}");
        return true;
    }

    // ================= 打造 =================

    public static int ForgeCost => Mathf.CeilToInt(20 * CharacterSystem.ForgeDiscount());

    /// <summary>消耗鍛造碎片打造指定部位。</summary>
    public static bool ForgeItem(EquipSlot slot)
    {
        if (D.forgeShard < ForgeCost) { GameEvents.Toast("鍛造碎片不足"); return false; }
        if (D.bags.Count >= BagCapacity) { GameEvents.Toast("背包已滿"); return false; }
        D.forgeShard -= ForgeCost;
        var e = EquipmentDatabase.Forge(slot, D.maxStageCleared + 1);
        D.bags.Add(e);
        GameEvents.RaiseCurrency(CurrencyType.ForgeShard, D.forgeShard);
        GameEvents.RaiseLoot(e);
        GameEvents.RaiseInventory();
        return true;
    }
}
