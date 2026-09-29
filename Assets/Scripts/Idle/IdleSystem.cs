using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class StageDef {
    public int id; public string name; public int waves = 5; public bool hasBoss = true;
    public float hpScale => 1f + (id - 1) * 0.35f;
    public long GoldReward => (long)(150 * id * Mathf.Pow(1.12f, id));
    public long ExpReward => (long)(90 * id * Mathf.Pow(1.10f, id));
}

public class IdleSystem : MonoBehaviour
{
    public static IdleSystem I { get; private set; }
    public float secondsPerWave = 4f;
    public float payoutInterval = 1f;
    public float dropChance = 0.18f;
    readonly List<StageDef> stages = new List<StageDef>();
    float waveTimer, payoutTimer;
    int currentWave;
    public bool AutoFight { get; set; } = true;

    void Awake() {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        for (int i = 1; i <= 200; i++) stages.Add(new StageDef { id = i, name = $"第 {i} 夜 · 鬼舍", waves = 4 + i / 10 });
    }

    void Update() {
        if (!AutoFight) return;
        waveTimer += Time.deltaTime;
        if (waveTimer >= secondsPerWave) {
            waveTimer -= secondsPerWave;
            SimulateWave();
            GameEvents.RaiseBattleStart(currentWave);
        }
        payoutTimer += Time.deltaTime;
        if (payoutTimer >= payoutInterval) {
            payoutTimer = 0;
            PayoutTick();
        }
    }

    void SimulateWave() {
        var d = GameSave.Data;
        int stage = Mathf.Max(1, d.currentStage);
        currentWave++;
        if (GameMath.Chance(dropChance)) InventorySystem.AddToBag(EquipmentDatabase.RollDrop(stage));
        if (currentWave > stages[stage - 1].waves) {
            currentWave = 0;
            long power = CharacterSystem.Power();
            float need = GameMath.MonsterHp(stage, stages[stage - 1].waves) * 0.6f;
            if (power >= need && stage == d.maxStageCleared + 1) {
                d.maxStageCleared = stage;
                d.currentStage = Mathf.Min(stage + 1, stages.Count);
                d.gold += stages[stage - 1].GoldReward;
                CharacterSystem.AddExp(stages[stage - 1].ExpReward);
                InventorySystem.AddToBag(EquipmentDatabase.RollBossDrop(stage));
                GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
                GameEvents.RaiseStage();
                GameEvents.Toast($"通關【{stages[stage - 1].name}】！");
            }
        }
    }

    void PayoutTick() {
        var d = GameSave.Data;
        IdleFormula.IdleRate(out long gps, out long eps, d.maxStageCleared, d.heroLevel);
        float bonus = CharacterSystem.IdleBonus();
        d.gold += (long)(gps * bonus);
        CharacterSystem.AddExp((long)(eps * bonus));
        GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
        GameEvents.RaiseIdleIncome((long)(gps * bonus), (long)(eps * bonus));
    }

    public void CollectOfflineNow() {
        var (sec, g, e) = GameSave.SettleOffline();
        if (sec > 0) {
            CharacterSystem.AddExp(e);
            GameEvents.RaiseCurrency(CurrencyType.Gold, GameSave.Data.gold);
            GameEvents.RaiseHero();
            GameEvents.Toast($"領取離線收益：{Fmt(sec)} → 金幣+{g:N0} 經驗+{e:N0}");
        }
    }
    public static string Fmt(long sec) {
        long h = sec / 3600, m = sec % 3600 / 60, s = sec % 60;
        return h > 0 ? $"{h}時{m}分" : m > 0 ? $"{m}分{s}秒" : $"{s}秒";
    }
    void OnApplicationQuit() { GameSave.Save(); }
}