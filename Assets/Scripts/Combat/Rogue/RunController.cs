using System;
using System.Collections.Generic;
using UnityEngine;

public class RunBuff
{
    public string name; public string desc;
    public float atkMul = 1f, defMul = 1f, hpMul = 1f;
    public float critAdd, speedAdd, healPct, lootAdd;
}
public class RunBuffChoice { public List<RunBuff> options = new List<RunBuff>(); }

public class RunController : MonoBehaviour
{
    public static RunController I { get; private set; }
    [Header("每次出手間隔(秒)")] public float tickInterval = 0.4f;
    [Header("基礎波數(+stage/10)")] public int baseWaves = 5;

    public bool IsRunning { get; private set; }
    public bool WaitingChoice { get; private set; }
    public bool LastRunWin { get; private set; }
    public int Stage { get; private set; }
    public int Wave { get; private set; }
    public int TotalWaves { get; private set; }
    public float HeroHp, HeroMaxHp, EnemyHp, EnemyMaxHp;
    public string EnemyName = "";
    public float LastEnemyHit, LastHitTime;
    public int RunCrystal; public long RunGold;
    public List<EquipmentInstance> RunLoot = new List<EquipmentInstance>();

    readonly List<RunBuff> buffs = new List<RunBuff>();
    float heroAtk, heroDef, heroCrit, heroSpeed, lootBonus, tick;
    RunBuffChoice pending = new RunBuffChoice();

    void Awake() { if (I != null && I != this) { Destroy(gameObject); return; } I = this; }

    public static string RewardPreview(int stage) =>
        $"靈玉 x{2 + stage / 5}・金幣 x{TopBarUI.Num(stage * 120L)}・保底稀有+裝備 x1";

    public void StartRun(int stage)
    {
        if (IsRunning) return;
        var s = CharacterSystem.FinalStats();
        Stage = stage; Wave = 0; TotalWaves = baseWaves + stage / 10;
        buffs.Clear(); RunLoot.Clear(); RunCrystal = 0; RunGold = 0;
        heroAtk = s.atk; heroDef = s.def; heroCrit = Mathf.Clamp(s.critRate, 0f, .85f);
        heroSpeed = s.speed; lootBonus = 0f;
        HeroMaxHp = HeroHp = s.hp;
        IsRunning = true;
        GameEvents.RaiseRunStart();
        NextWave();
    }

    void NextWave()
    {
        Wave++;
        if (Wave > TotalWaves) { EndRun(true); return; }
        bool boss = Wave == TotalWaves;
        EnemyMaxHp = EnemyHp = GameMath.MonsterHp(Stage, Wave) * (boss ? 4f : 1f);
        EnemyName = boss ? "鬼王" : $"怨鬼・第{Wave}波";
        GameEvents.RaiseRunWave(Wave, TotalWaves);
        tick = 0f;
    }

    void Update()
    {
        if (!IsRunning || WaitingChoice) return;
        tick += Time.deltaTime;
        if (tick < tickInterval) return;
        tick -= tickInterval;

        float dmg = Mathf.Max(1f, heroAtk - (Stage * 2f + Wave) * 0.5f);
        if (GameMath.Chance(heroCrit)) dmg *= 2f;
        dmg *= GameMath.RandFloat(0.9f, 1.1f) * (1f + heroSpeed * 0.5f);
        EnemyHp -= dmg;
        LastEnemyHit = dmg; LastHitTime = Time.time;

        if (EnemyHp <= 0)
        {
            if (GameMath.Chance(0.45f + lootBonus)) RunLoot.Add(EquipmentDatabase.RollDrop(Stage));
            WaitingChoice = true;
            GameEvents.RaiseRunChoice(RollChoice());
            return;
        }
        float edmg = Mathf.Max(1f, GameMath.MonsterAtk(Stage, Wave) - heroDef * 0.5f) * GameMath.RandFloat(0.9f, 1.1f);
        HeroHp -= edmg;
        if (HeroHp <= 0) EndRun(false);
    }

    public void PickBuff(int idx)
    {
        if (!WaitingChoice || idx >= pending.options.Count) return;
        var b = pending.options[idx];
        buffs.Add(b);
        heroAtk *= b.atkMul; heroDef *= b.defMul;
        HeroMaxHp *= b.hpMul;
        HeroHp = Mathf.Min(HeroMaxHp, HeroHp * b.hpMul + HeroMaxHp * b.healPct);
        heroCrit = Mathf.Clamp(heroCrit + b.critAdd, 0f, .85f);
        heroSpeed += b.speedAdd; lootBonus += b.lootAdd;
        WaitingChoice = false;
        NextWave();
    }

    RunBuffChoice RollChoice()
    {
        var pool = new List<RunBuff>
        {
            new RunBuff{ name="鋒刃", desc="攻擊 +25%", atkMul=1.25f },
            new RunBuff{ name="鐵壁", desc="防禦 +25%", defMul=1.25f },
            new RunBuff{ name="氣血", desc="生命上限 +30%", hpMul=1.3f },
            new RunBuff{ name="破妄", desc="暴擊 +10%", critAdd=.1f },
            new RunBuff{ name="疾步", desc="速度 +15%", speedAdd=.15f },
            new RunBuff{ name="回春", desc="立即回復 40% 生命", healPct=.4f },
            new RunBuff{ name="聚財", desc="本局掉寶 +20%", lootAdd=.2f },
            new RunBuff{ name="殺意", desc="攻擊+12%・暴擊+5%", atkMul=1.12f, critAdd=.05f },
        };
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = GameMath.RandInt(0, i);
            var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
        }
        pending = new RunBuffChoice();
        for (int i = 0; i < 3; i++) pending.options.Add(pool[i]);
        return pending;
    }

    void EndRun(bool win)
    {
        IsRunning = false; WaitingChoice = false; LastRunWin = win;
        var d = GameSave.Data;
        if (win)
        {
            RunCrystal = 2 + Stage / 5; RunGold = Stage * 120L;
            d.spiritCrystal += RunCrystal; d.gold += RunGold;
            InventorySystem.AddToBag(EquipmentDatabase.RollBossDrop(Stage));
            foreach (var e in RunLoot) InventorySystem.AddToBag(e);
            CharacterSystem.AddExp(Stage * 40L);
        }
        else
        {
            RunCrystal = 1 + Stage / 10; d.spiritCrystal += RunCrystal;
            for (int i = 0; i < RunLoot.Count / 2; i++) InventorySystem.AddToBag(RunLoot[i]);
        }
        GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, d.spiritCrystal);
        GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        GameEvents.RaiseInventory();
        GameEvents.RaiseRunEnd();
        GameSave.Save();
    }
}