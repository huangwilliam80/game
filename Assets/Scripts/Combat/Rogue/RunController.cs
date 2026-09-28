using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunState { Home, Fighting, Choosing, Result }

/// <summary>
/// Roguelite 單場 run 的狀態機:
/// Home → Fighting → (每波結束) Choosing → Fighting … → Result → Home
/// </summary>
public class RunController : MonoBehaviour
{
    public static RunController I { get; private set; }

    public RunState state = RunState.Home;
    public int stageId;
    public int wave;
    public int totalWaves;
    public readonly List<RogueBuffDef> buffs = new List<RogueBuffDef>();

    // 我方戰鬥數值(含 buff 加成)
    float heroHp, heroHpMax, heroAtk, heroDef, heroCrit, heroLifesteal;
    int heroExtraHits;
    float attackInterval = 0.5f;

    // 敵方戰鬥數值
    float waveHp, waveHpMax, enemyAtk, enemyDef;

    float tickTimer;
    bool lastWin;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    void Update()
    {
        // ★ 測試用:Play 模式下按 R 直接開 run(之後改由 UI 按鈕觸發)
        if (Input.GetKeyDown(KeyCode.R) && state == RunState.Home)
            StartRun(Mathf.Max(1, GameSave.Data.currentStage));

        if (state != RunState.Fighting) return;
        tickTimer += Time.deltaTime;
        while (tickTimer >= attackInterval)
        {
            tickTimer -= attackInterval;
            Tick();
            if (state != RunState.Fighting) break;
        }
    }

    // ================= Run 流程 =================

    public void StartRun(int id)
    {
        if (state != RunState.Home) return;
        stageId = id;
        wave = 0;
        buffs.Clear();
        totalWaves = 4 + id / 10 + 1;   // 最後一波 = 首領

        RecalcBuffMods();
        heroHp = heroHpMax;

        if (IdleSystem.I != null) IdleSystem.I.AutoFight = false;  // ★ 暫停掛機
        state = RunState.Fighting;
        GameEvents.RaiseRunStart();
        NextWave();
    }

    void NextWave()
    {
        wave++;
        bool boss = wave >= totalWaves;
        waveHpMax = GameMath.MonsterHp(stageId, wave) * (boss ? 6f : 1f);
        waveHp = waveHpMax;
        enemyAtk = GameMath.MonsterAtk(stageId, wave) * (boss ? 1.8f : 1f);
        enemyDef = stageId * 2f + wave;

        GameEvents.RaiseWaveCleared(wave);   // WorldView 更新敵人名稱/血條上限
        if (WorldView.I != null) WorldView.I.SetEnemyHp(waveHp);
    }

    void OnWaveCleared()
    {
        // 波間治療 buff
        float healPct = 0f;
        for (int i = 0; i < buffs.Count; i++) healPct += buffs[i].healPerWave;
        if (healPct > 0f)
        {
            heroHp = Mathf.Min(heroHpMax, heroHp + heroHpMax * healPct);
            if (WorldView.I != null) WorldView.I.SetHeroHp(heroHp);
        }

        if (wave >= totalWaves) { Finish(true); return; }
        OfferChoice();
    }

    void OfferChoice()
    {
        state = RunState.Choosing;
        GameEvents.RaiseChoiceRequested();
        if (ChoiceOverlayUI.I != null)
            ChoiceOverlayUI.I.Show(RogueBuffPool.Draft(3, buffs), Pick);
        else
            NextWave();   // 保險:沒有 UI 時直接繼續
    }

    void Pick(RogueBuffDef b)
    {
        buffs.Add(b);
        float hpRatio = Mathf.Clamp01(heroHp / Mathf.Max(1f, heroHpMax));
        RecalcBuffMods();
        heroHp = heroHpMax * hpRatio;   // 維持當前血量比例
        if (WorldView.I != null)
        {
            WorldView.I.SetBuffs(buffs);
            WorldView.I.SetHeroHp(heroHp);
        }
        state = RunState.Fighting;
        NextWave();
    }

    void Finish(bool win)
    {
        lastWin = win;
        state = RunState.Result;
        var loot = RollLoot(win);
        GameEvents.RaiseRunFinished(loot);
        if (RunResultUI.I != null)
            RunResultUI.I.Show(win, loot, Confirm);
        else
            Confirm(loot, 0);
    }

    List<EquipmentInstance> RollLoot(bool win)
    {
        var list = new List<EquipmentInstance>();
        int n = win ? 2 : 1;
        for (int i = 0; i < n; i++) list.Add(EquipmentDatabase.RollDrop(stageId));
        if (win) list.Add(EquipmentDatabase.RollBossDrop(stageId));
        return list;
    }

    void Confirm(List<EquipmentInstance> keep, long sellGold)
    {
        if (keep != null)
            for (int i = 0; i < keep.Count; i++) InventorySystem.AddToBag(keep[i]);

        var d = GameSave.Data;
        d.gold += sellGold;
        if (lastWin)
        {
            var sd = new StageDef { id = stageId };
            d.gold += sd.GoldReward;
            CharacterSystem.AddExp(sd.ExpReward);
            // d.shard += 1;   // 若你的存檔有 shard 欄位,取消註解即可當 run 貨幣
        }
        GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        GameEvents.RaiseHero();
        ExitRun();
    }

    public void ExitRun()
    {
        state = RunState.Home;
        buffs.Clear();
        if (IdleSystem.I != null) IdleSystem.I.AutoFight = true;   // 恢復掛機
        if (WorldView.I != null) WorldView.I.SetBuffs(buffs);
        GameEvents.RaiseRunEnd();
        GameSave.Save();
    }

    // ================= 戰鬥 Tick =================

    void Tick()
    {
        // ---- 我方出手 ----
        float dealt = 0f;
        int hits = 1 + heroExtraHits;
        for (int i = 0; i < hits && waveHp > 0f; i++)
        {
            bool crit = GameMath.Chance(heroCrit);
            float dmg = CalcDamage(heroAtk, enemyDef, crit) * ExecuteMul();
            waveHp -= dmg;
            dealt += dmg;
            if (WorldView.I != null) { WorldView.I.SpawnDmg(dmg, crit, false); WorldView.I.FlashEnemy(); }
        }
        if (heroLifesteal > 0f && dealt > 0f)
            heroHp = Mathf.Min(heroHpMax, heroHp + dealt * heroLifesteal);
        if (WorldView.I != null) { WorldView.I.SetEnemyHp(waveHp); WorldView.I.SetHeroHp(heroHp); }

        if (waveHp <= 0f) { OnWaveCleared(); return; }

        // ---- 敵方出手 ----
        bool ecrit = GameMath.Chance(0.05f);
        float edmg = CalcDamage(enemyAtk, heroDef, ecrit);
        heroHp -= edmg;
        if (WorldView.I != null)
        {
            WorldView.I.SpawnDmg(edmg, ecrit, true);
            WorldView.I.FlashHero();
            WorldView.I.SetHeroHp(heroHp);
        }

        if (heroHp <= 0f) Finish(false);
    }

    // ================= 數值工具 =================

    void RecalcBuffMods()
    {
        var h = CharacterSystem.FinalStats();
        float atkMul = 1f, defMul = 1f, hpMul = 1f, spdMul = 1f;
        float critAdd = 0f, ls = 0f;
        int extra = 0;
        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            atkMul *= b.atkMul; defMul *= b.defMul; hpMul *= b.hpMul; spdMul *= b.speedMul;
            critAdd += b.critAdd; ls += b.lifesteal; extra += b.extraHits;
        }
        heroAtk = h.atk * atkMul;
        heroDef = h.def * defMul;
        heroHpMax = h.hp * hpMul;
        heroCrit = Mathf.Clamp01(h.critRate + critAdd);
        heroLifesteal = Mathf.Clamp01(ls);
        heroExtraHits = extra;
        attackInterval = 0.5f / Mathf.Max(0.5f, spdMul);
    }

    float ExecuteMul()
    {
        float mul = 1f;
        if (waveHpMax > 0f && waveHp / waveHpMax < 0.3f)
            for (int i = 0; i < buffs.Count; i++) mul += buffs[i].executeBonus;
        return mul;
    }

    static float CalcDamage(float atk, float def, bool crit)
    {
        float dmg = Mathf.Max(1f, atk - def * 0.5f);
        if (crit) dmg *= 2f;
        return dmg * GameMath.RandFloat(0.9f, 1.1f);
    }
}