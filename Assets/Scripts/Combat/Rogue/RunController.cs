using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 修復 CS0103: The name 'GameEvents' does not exist in the current context
// 以別名直接綁定全域事件总线，確保每個檔案都能解析到 GameEvents（不受專案載入順序影響）。
using GameEvents = global::GameEvents;

/// <summary>
/// Roguelite「靈玉秘境」run 控制器：波次自動戰鬥＋每 2 波三選一 buff＋結算戰利品。
/// 戰鬥判定走 BattleSimulator（純數學），本腳色只管節奏與事件廣播 → 性能開銷為零。
/// </summary>
public class RunController : MonoBehaviour
{
    public static RunController I { get; private set; }
    public bool IsRunning { get; private set; }

    // ---- UI 讀取的狀態 ----
    public bool LastRunWin { get; private set; }
    public int Stage { get; private set; }
    public int Wave { get; private set; }
    public long RunCrystal { get; private set; }
    public long RunGold { get; private set; }
    public List<EquipmentInstance> RunLoot { get; private set; } = new List<EquipmentInstance>();
    public float HeroHp, HeroMaxHp, EnemyHp, EnemyMaxHp;
    public string EnemyName { get; private set; } = "";
    public float LastHitTime { get; private set; }
    public float LastEnemyHit { get; private set; }
    public IReadOnlyList<RogueBuffDef> OwnedBuffs => buffs;
    public int stageId => Stage;          // 相容舊命名（WorldView 等使用）
    public int totalWaves => TotalWaves;

    readonly List<RogueBuffDef> buffs = new List<RogueBuffDef>();
    int TotalWaves;
    const int ChoiceEvery = 2;   // 每 2 波選一次卡

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    // ================= 對外 API =================

    public void StartRun(int stage)
    {
        if (IsRunning) return;
        IsRunning = true;
        Stage = Mathf.Max(1, stage);
        TotalWaves = 4 + Stage / 10;      // 最後一波視為 BOSS
        Wave = 0;
        RunLoot.Clear();
        RunGold = 0; RunCrystal = 0;
        buffs.Clear();

        var s = CharacterSystem.FinalStats();
        HeroMaxHp = s.hp; HeroHp = s.hp;

        GameEvents.RaiseRunStart();
        GameEvents.Toast($"⚔ 進入第 {Stage} 夜 · 靈玉秘境…");
        StartCoroutine(RunRoutine());
    }

    IEnumerator RunRoutine()
    {
        for (int w = 1; w <= TotalWaves; w++)
        {
            Wave = w;
            GameEvents.RaiseRunWave(w, TotalWaves);
            GameEvents.RaiseBattleStart(w);
            yield return StartCoroutine(FightOneWave(w));

            bool alive = HeroHp > 0f;
            GameEvents.RaiseBattleEnd(alive, new BattleResult { stageId = Stage, waveReached = w });
            if (!alive) { Finish(false); yield break; }
            GameEvents.RaiseWaveCleared(w + 1);   // 世界視窗預備下一波敵人

            if (w % ChoiceEvery == 0 && w < TotalWaves)
            {
                var options = RogueBuffPool.Draft(3, buffs);
                bool picked = false; int chosen = 0;
                GameEvents.RaiseRunChoice(options, idx => { picked = true; chosen = idx; });
                while (!picked) yield return null;     // 等待玩家三選一
                ApplyBuff(options[Mathf.Clamp(chosen, 0, options.Count - 1)]);
            }
            yield return new WaitForSeconds(0.35f);
        }
        Finish(true);
    }

    /// <summary>單波交鋒：以 0.25s 為步長做可視化的回合對拼（傷害進浮動數字池）。</summary>
    IEnumerator FightOneWave(int wave)
    {
        bool isBoss = wave >= TotalWaves;
        var s = BuffedStats();
        float mHp = GameMath.MonsterHp(Stage, wave) * (isBoss ? 6f : 1f);
        float mAtk = GameMath.MonsterAtk(Stage, wave) * (isBoss ? 1.8f : 1f);
        float mDef = Stage * 2f + wave;
        EnemyName = isBoss ? "鬼王" : $"怨鬼 x{wave}";
        EnemyMaxHp = mHp; EnemyHp = mHp;

        while (HeroHp > 0f && EnemyHp > 0f)
        {
            // 我方出手（連擊 buff 會多打几下）
            int hits = 1 + SumExtraHits();
            for (int i = 0; i < hits && EnemyHp > 0f; i++)
            {
                float dmg = Damage(s.atk, mDef, s.critRate);
                if (s.executeBonus > 0f && EnemyHp < EnemyMaxHp * 0.3f) dmg *= 1f + s.executeBonus;
                EnemyHp -= dmg;
                LastEnemyHit = dmg; LastHitTime = Time.unscaledTime;
                if (s.lifesteal > 0f) HeroHp = Mathf.Min(HeroMaxHpAll(), HeroHp + dmg * s.lifesteal);
            }
            if (EnemyHp <= 0f) break;
            // 敵方出手
            HeroHp -= Damage(mAtk, s.def, .05f);
            yield return new WaitForSeconds(0.25f);
        }
        if (EnemyHp <= 0f && s.healPerWave > 0f)
            HeroHp = Mathf.Min(HeroMaxHpAll(), HeroHp + HeroMaxHpAll() * s.healPerWave);
        EnemyHp = Mathf.Max(0f, EnemyHp);
        HeroHp = Mathf.Max(0f, HeroHp);
    }

    void Finish(bool win)
    {
        LastRunWin = win;
        var d = GameSave.Data;
        if (win)
        {
            int lootN = GameMath.RandInt(2, 3);
            for (int i = 0; i < lootN; i++) RunLoot.Add(EquipmentDatabase.RollDrop(Stage));
            RunLoot.Add(EquipmentDatabase.RollBossDrop(Stage));

            RunGold = 500 * Stage;
            RunCrystal = 10 * Stage;
            d.gold += RunGold;
            d.spiritCrystal += RunCrystal;
            CharacterSystem.AddExp(200 * Stage);

            if (Stage == d.maxStageCleared + 1) { d.maxStageCleared = Stage; GameEvents.RaiseStage(); }

            // 交由結算 UI 讓玩家「保留／分解」；若場景沒掛 RunResultUI 就直接入包
            if (RunResultUI.I != null)
                RunResultUI.I.Show(true, RunLoot, (keep, sellGold) =>
                {
                    foreach (var k in keep) InventorySystem.AddToBag(k);
                    d.gold += sellGold;
                    GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
                    GameEvents.Toast($"✅ 秘境通關！金幣+{RunGold + sellGold} 靈玉+{RunCrystal}");
                });
            else
            {
                foreach (var k in RunLoot) InventorySystem.AddToBag(k);
                GameEvents.Toast($"✅ 秘境通關！金幣+{RunGold} 靈玉+{RunCrystal}");
            }
        }
        else
        {
            RunCrystal = Stage * 2;      // 安慰獎
            d.spiritCrystal += RunCrystal;
            GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, d.spiritCrystal);
            if (RunResultUI.I != null)
                RunResultUI.I.Show(false, RunLoot, (keep, sellGold) =>
                {
                    foreach (var k in keep) InventorySystem.AddToBag(k);
                    d.gold += sellGold;
                    GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
                });
            GameEvents.Toast("❌ 歷練失敗…建議強化裝備、升級功法再挑戰");
        }

        GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, d.spiritCrystal);
        GameEvents.RaiseHero();
        GameSave.Save();

        IsRunning = false;
        GameEvents.RaiseRunEnd();
    }

    // ================= Buff 聚合 =================

    struct RunStats
    {
        public float atk, def, critRate, lifesteal, healPerWave, executeBonus;
        public int extraHits;
    }

    RunStats BuffedStats()
    {
        var b = CharacterSystem.FinalStats();
        var r = new RunStats { atk = b.atk, def = b.def, critRate = b.critRate };
        float atkMul = 1f, defMul = 1f, hpMul = 1f;
        foreach (var g in buffs)
        {
            atkMul *= g.atkMul; defMul *= g.defMul; hpMul *= g.hpMul;
            r.critRate += g.critAdd; r.lifesteal += g.lifesteal;
            r.healPerWave += g.healPerWave; r.executeBonus += g.executeBonus;
            r.extraHits += g.extraHits;
        }
        r.atk *= atkMul; r.def *= defMul;
        HeroMaxHp = b.hp * hpMul;
        if (HeroHp > HeroMaxHp) HeroHp = HeroMaxHp;
        return r;
    }

    int SumExtraHits()
    {
        int n = 0; foreach (var g in buffs) n += g.extraHits; return n;
    }

    float HeroMaxHpAll() => HeroMaxHp > 0f ? HeroMaxHp : 1f;

    void ApplyBuff(RogueBuffDef b)
    {
        if (b == null) return;
        buffs.Add(b);
        var s = CharacterSystem.FinalStats();
        HeroMaxHp = s.hp;                       // 簡化：上限隨 buff 重算，保留當前比例
        HeroHp = Mathf.Min(HeroHp, HeroMaxHp);
        GameEvents.Toast($"🃏 獲得【{b.name}】：{b.desc}");
    }

    static float Damage(float atk, float def, float crit)
    {
        float baseDmg = Mathf.Max(1f, atk - def * 0.5f);
        if (GameMath.Chance(crit)) baseDmg *= 2f;
        return baseDmg * GameMath.RandFloat(0.9f, 1.1f);
    }

    public static string RewardPreview(int stage) => $"預估：{500 * stage} 金幣・{10 * stage} 靈玉・{2}~{3} 件装备";
}
