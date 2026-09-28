using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 靈玉秘境（Roguelite 模式）控制器
/// </summary>
public class RunController : MonoBehaviour
{
    public static RunController I { get; private set; }
    public bool IsRunning { get; private set; }

    void Awake() 
    { 
        if (I != null && I != this) { Destroy(gameObject); return; } 
        I = this; 
    }

    public void StartRun(int stageId)
    {
        if (IsRunning) return;
        IsRunning = true;
        GameEvents.RaiseRunStart();
        StartCoroutine(SimulateRun(stageId));
    }

    IEnumerator SimulateRun(int stageId)
    {
        GameEvents.Toast($"進入第 {stageId} 夜 · 靈玉秘境...");
        // 模擬 Roguelite 戰鬥過程（後續可替換為真實的場景切換或動畫）
        yield return new WaitForSeconds(2.5f); 

        // 結算獎勵
        var loot = new List<EquipmentInstance>();
        for (int i = 0; i < 2; i++) loot.Add(EquipmentDatabase.RollDrop(stageId));
        
        long goldReward = 500 * stageId;
        long crystalReward = 10 * stageId;

        GameSave.Data.gold += goldReward;
        GameSave.Data.spiritCrystal += crystalReward;
        foreach (var item in loot) InventorySystem.AddToBag(item);

        GameEvents.RaiseCurrency(CurrencyType.Gold, GameSave.Data.gold);
        GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, GameSave.Data.spiritCrystal);
        
        IsRunning = false;
        GameEvents.RaiseRunEnd(true, new BattleResult { stageId = stageId, goldReward = goldReward });
        GameEvents.Toast("秘境通關！戰利品已發放至背包");
    }

    public static string RewardPreview(int stage) => $"預估獲得：{500 * stage} 金幣, {10 * stage} 靈玉";
}