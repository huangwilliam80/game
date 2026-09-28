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

    /// <summary>目前是否有 run 進行中（UI 用它來禁用按鈕）。</summary>
    public bool IsRunning => state != RunState.Home;

    /// <summary>某關卡「靈玉秘境」的预估獎勵（給按鈕文案用，與 Confirm 公式保持一致）。</summary>
    public static string RewardPreview(int stageId)
    {
        var sd = new StageDef { id = stageId };
        return $"金幣 {TopBarUI.Num(sd.GoldReward)}｜經驗 {TopBarUI.Num(sd.ExpReward * 2)}｜靈玉 {3 + stageId / 3}｜碎片 {4 + stageId / 5}";
    }

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
        // 除錯快捷鍵：Play 模式下按 R 直接開一場秘境（正式版靠 UI 按鈕）
        if (DebugShortcutPressed() && state == RunState.Home)
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
        if (state != RunState.Home) { GameEvents.Toast("正在歷練中…"); return; }
        if (GameSave.Data.bags.Count >= InventorySystem.BagCapacity)
        { GameEvents.Toast("背包已滿，先整理再進秘境"); return; }
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
            d.spiritCrystal += 3 + stageId / 3;      // 靈玉：洗鍊的來源
            d.forgeShard += 4 + stageId / 5;         // 碎片：鍛造的來源
            CharacterSystem.AddExp(sd.ExpReward * 2); // 秘境經驗雙倍 → 玩家願意主動打
            if (stageId > d.maxStageCleared)
            {
                d.maxStageCleared = stageId;
                d.currentStage = Mathf.Min(stageId + 1, 200);
                GameEvents.RaiseStage();
            }
        }
        GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, d.spiritCrystal);
        GameEvents.RaiseCurrency(CurrencyType.ForgeShard, d.forgeShard);
        GameEvents.RaiseHero();
        GameEvents.Toast(lastWin ? "秘境通關！資源已入帳" : "歷練失敗…先強化裝備再來");
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

    /// <summary>同時相容舊/新輸入系統的除錯快捷鍵（R 開一把）。</summary>
    static bool DebugShortcutPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#elif ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.rKey.wasPressedThisFrame;
#else
        return false;
#endif
    }
}