using System.Collections.Generic;
using UnityEngine;

/// <summary>關卡定義（猛鬼宿舍式：守床 N 波 → 打首領）。</summary>
[System.Serializable]
public class StageDef
{
    public int id;            // 1-based
    public string name;
    public int waves = 5;     // 小怪波數
    public bool hasBoss = true;
    public float hpScale => 1f + (id - 1) * 0.35f;
    public long GoldReward => (long)(150 * id * Mathf.Pow(1.12f, id));
    public long ExpReward => (long)(90 * id * Mathf.Pow(1.10f, id));
}

/// <summary>
/// 掛機系統：在線以「自動戰鬥迴圈」產出收益；離線用時間戳結算。
/// 核心原則：所有收益公式只有一份（IdleFormula），線上線下一致。
/// </summary>
public class IdleSystem : MonoBehaviour
{
    public static IdleSystem I { get; private set; }

    [Header("節奏設定")]
    [Tooltip("一次戰鬥（一波）的秒數，越短越有『刷怪』感")]
    public float secondsPerWave = 4f;
    [Tooltip("每幾秒批量入帳一次（降低 UI 刷新壓力）")]
    public float payoutInterval = 1f;
    [Range(0f, 1f)]
    [Tooltip("掉落機率／每波")]
    public float dropChance = 0.18f;

    readonly List<StageDef> stages = new();
    public IReadOnlyList<StageDef> Stages => stages;

    float waveTimer, payoutTimer;
    int currentWave;

    public bool AutoFight { get; set; } = true;   // 放置遊戲預設全自动

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        BuildStages();
    }

    void BuildStages()
    {
        stages.Clear();
        for (int i = 1; i <= 200; i++)
            stages.Add(new StageDef { id = i, name = $"第 {i} 夜 · 鬼舍", waves = 4 + i / 10 });
    }

    void Update()
    {
        if (!AutoFight) return;
        float dt = Time.deltaTime;

        // ---- 戰鬥節拍：模擬「打完一波怪」----
        waveTimer += dt;
        if (waveTimer >= secondsPerWave)
        {
            waveTimer -= secondsPerWave;
            SimulateWave();
            GameEvents.RaiseBattleStart(currentWave);   // 驅動視覺舞臺播放
        }

        // ---- 批量入帳 ----
        payoutTimer += dt;
        if (payoutTimer >= payoutInterval)
        {
            payoutTimer = 0;
            PayoutTick();
        }
    }

    void SimulateWave()
    {
        var d = GameSave.Data;
        int stage = Mathf.Max(1, d.currentStage);
        currentWave++;

        // 掉落擲骰（含品質隨深度提升）
        if (GameMath.Chance(dropChance))
        {
            var loot = EquipmentDatabase.RollDrop(stage);
            InventorySystem.AddToBag(loot);
        }

        // 完成整關 → 進下一關（戰力檢查：過強才推進，否則原地farm）
        if (currentWave > stages[stage - 1].waves)
        {
            currentWave = 0;
            TryAdvanceStage(stage);
        }
    }

    void TryAdvanceStage(int stage)
    {
        var d = GameSave.Data;
        long power = CharacterSystem.Power();
        float need = GameMath.MonsterHp(stage, stages[stage - 1].waves) * 0.6f;

        if (power >= need && stage == d.maxStageCleared + 1)
        {
            d.maxStageCleared = stage;
            d.currentStage = Mathf.Min(stage + 1, stages.Count);
            d.gold += stages[stage - 1].GoldReward;
            CharacterSystem.AddExp(stages[stage - 1].ExpReward);
            // 首領保底掉一件好裝
            InventorySystem.AddToBag(EquipmentDatabase.RollBossDrop(stage));
            GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
            GameEvents.RaiseStage();
            GameEvents.Toast($"通關【{stages[stage - 1].name}】！");
        }
        else if (stage < d.maxStageCleared)
        {
            // farm 已通關關卡：仍給少量獎勵
            d.gold += stages[stage - 1].GoldReward / 4;
            CharacterSystem.AddExp(stages[stage - 1].ExpReward / 4);
            GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        }
    }

    void PayoutTick()
    {
        var d = GameSave.Data;
        IdleFormula.IdleRate(out long gps, out long eps, d.maxStageCleared, d.heroLevel);
        float bonus = CharacterSystem.IdleBonus();
        long g = (long)(gps * bonus), e = (long)(eps * bonus);
        d.gold += g;
        CharacterSystem.AddExp(e);
        GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        GameEvents.RaiseIdleIncome(g, e);
    }

    /// <summary>手動領取離線收益時呼叫（UI 按鈕）。</summary>
    public void CollectOfflineNow()
    {
        var (sec, g, e) = GameSave.SettleOffline();
        if (sec > 0)
        {
            CharacterSystem.AddExp(e);
            GameEvents.RaiseCurrency(CurrencyType.Gold, GameSave.Data.gold);
            GameEvents.RaiseHero();
            GameEvents.Toast($"領取離線收益：{Fmt(sec)} → 金幣+{g:N0} 經驗+{e:N0}");
        }
    }

    public static string Fmt(long sec)
    {
        long h = sec / 3600, m = sec % 3600 / 60, s = sec % 60;
        return h > 0 ? $"{h}時{m}分" : m > 0 ? $"{m}分{s}秒" : $"{s}秒";
    }

    void OnApplicationPause(bool pause) { if (pause) GameSave.Save(); }
    void OnApplicationQuit() { GameSave.Save(); }
}
