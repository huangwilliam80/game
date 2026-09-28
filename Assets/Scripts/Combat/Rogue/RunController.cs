using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunController : MonoBehaviour
{
    public static RunController I { get; private set; }
    public bool IsRunning { get; private set; }

    // ★ 補齊 UI 腳本需要的屬性
    public bool LastRunWin { get; private set; }
    public int Stage { get; private set; }
    public int Wave { get; private set; }
    public long RunCrystal { get; private set; }
    public long RunGold { get; private set; }
    public List<EquipmentInstance> RunLoot { get; private set; } = new List<EquipmentInstance>();
    public float HeroHp { get; private set; }
    public float HeroMaxHp { get; private set; }
    public float EnemyHp { get; private set; }
    public float EnemyMaxHp { get; private set; }
    public string EnemyName { get; private set; }
    public float LastHitTime { get; private set; }
    public float LastEnemyHit { get; private set; }
    public int stageId { get; private set; }
    public int totalWaves { get; private set; }

    void Awake() 
    { 
        if (I != null && I != this) { Destroy(gameObject); return; } 
        I = this; 
    }

    public void StartRun(int stage)
    {
        if (IsRunning) return;
        IsRunning = true;
        this.stageId = stage;
        this.totalWaves = 5;
        this.Stage = stage;
        GameEvents.RaiseRunStart();
        StartCoroutine(SimulateRun(stage));
    }

    public void PickBuff(int idx) { /* Dummy */ }

    IEnumerator SimulateRun(int stage)
    {
        GameEvents.Toast($"⚔ 進入第 {stage} 夜 · 靈玉秘境...");
        yield return new WaitForSeconds(1f); 

        long power = CharacterSystem.Power();
        float monHp = GameMath.MonsterHp(stage, 5) * 0.6f;
        bool win = power >= monHp;

        for (int w = 1; w <= totalWaves; w++)
        {
            Wave = w;
            GameEvents.RaiseBattleStart(w); 
            yield return new WaitForSeconds(1.2f);
            GameEvents.RaiseBattleEnd(win, new BattleResult { stageId = stage, waveReached = w });
        }

        var loot = new List<EquipmentInstance>();
        long goldReward = 0;
        long crystalReward = 0;

        if (win) {
            for (int i = 0; i < 2; i++) loot.Add(EquipmentDatabase.RollDrop(stage));
            goldReward = 500 * stage;
            crystalReward = 10 * stage;
            GameSave.Data.gold += goldReward;
            GameSave.Data.spiritCrystal += crystalReward;
            foreach (var item in loot) InventorySystem.AddToBag(item);
            GameEvents.RaiseCurrency(CurrencyType.Gold, GameSave.Data.gold);
            GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, GameSave.Data.spiritCrystal);
            LastRunWin = true;
            GameEvents.Toast($"✅ 秘境通關！金幣+{goldReward} 靈玉+{crystalReward}");
        } else {
            LastRunWin = false;
            GameEvents.Toast($"❌ 戰力不足，歷練失敗！建議強化裝備。");
        }

        this.RunGold = goldReward;
        this.RunCrystal = crystalReward;
        this.RunLoot = loot;

        IsRunning = false;
        GameEvents.RaiseRunEnd();
    }

    public static string RewardPreview(int stage) => $"預估獲得：{500 * stage} 金幣, {10 * stage} 靈玉";
}