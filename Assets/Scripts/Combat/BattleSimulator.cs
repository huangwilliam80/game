using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戰鬥模擬器：純數學、零 GameObject → 一個關卡结算只需幾十微秒，性能極佳。
/// 用於「挑戰更高副本」時的真實勝負判定與結算（波次＋首領）。
/// </summary>
public static class BattleSimulator
{
    public class UnitSnap
    {
        public string name; public bool isHeroSide;
        public float hp, maxHp, atk, def, speed, critRate, special;
    }

    /// <summary>執行一場自動戰鬥，回傳結果與統計。</summary>
    public static BattleResult Simulate(int stageId, out bool win, out List<UnitSnap> log)
    {
        var stage = new StageDef { id = stageId, waves = 4 + stageId / 10 };
        var hero = CharacterSystem.FinalStats();

        // ---- 我方：主角 + 床鋪守衛（bedCount）----
        int beds = Mathf.Clamp(GameSave.Data.bedCount, 1, 6);
        float teamHp = hero.hp * (1f + beds * 0.35f);
        float teamAtk = hero.atk * (1f + beds * 0.22f);
        float teamDef = hero.def * (1f + beds * 0.18f);

        log = new List<UnitSnap>();
        log.Add(new UnitSnap { name = $"主角(+{beds}床)", isHeroSide = true,
                               hp = teamHp, maxHp = teamHp, atk = teamAtk, def = teamDef,
                               speed = hero.speed, critRate = hero.critRate, special = hero.special });

        var result = new BattleResult { stageId = stageId };
        float simTime = 0f;
        const float TICK = 0.5f;              // 半秒一跳，足够精細且便宜
        const float TIME_LIMIT = 90f;         // 超時視為失敗（防守崩盤）

        for (int wave = 1; wave <= stage.waves + 1; wave++)   // 最後一波是首領
        {
            bool isBossWave = wave > stage.waves;
            float mHp = GameMath.MonsterHp(stageId, wave) * (isBossWave ? 6f : 1f);
            float mAtk = GameMath.MonsterAtk(stageId, wave) * (isBossWave ? 1.8f : 1f);
            float mDef = stageId * 2f + wave;

            log.Add(new UnitSnap { name = isBossWave ? "鬼王" : $"怨鬼 x{wave}", isHeroSide = false,
                                   hp = mHp, maxHp = mHp, atk = mAtk, def = mDef, speed = .12f, critRate = .05f, special = 0 });

            // 本波對拼
            float ourHp = teamHp;
            while (mHp > 0 && ourHp > 0 && simTime < TIME_LIMIT)
            {
                simTime += TICK;
                // 我方出手
                float dmg = CalcDamage(teamAtk, mDef, hero.critRate);
                mHp -= dmg;
                if (mHp <= 0) break;
                // 敵方出手
                float edmg = CalcDamage(mAtk, teamDef, .05f);
                ourHp -= edmg;
            }
            teamHp = Mathf.Max(1f, ourHp * 0.35f + teamHp * 0.65f); // 波間小幅回復（宿舍治療）

            if (ourHp <= 0 || simTime >= TIME_LIMIT)
            {
                win = false;
                result.waveReached = wave;
                result.duration = simTime;
                return result;
            }
        }

        win = true;
        result.waveReached = stage.waves + 1;
        result.duration = simTime;
        result.goldReward = stage.GoldReward;
        result.expReward = stage.ExpReward;
        // 掉落結算
        int lootN = GameMath.RandInt(1, 3);
        for (int i = 0; i < lootN; i++) result.loot.Add(EquipmentDatabase.RollDrop(stageId));
        result.loot.Add(EquipmentDatabase.RollBossDrop(stageId));
        return result;
    }

    static float CalcDamage(float atk, float def, float crit)
    {
        float baseDmg = Mathf.Max(1f, atk - def * 0.5f);          // 防禦減傷
        if (GameMath.Chance(crit)) baseDmg *= 2f;                  // 暴擊 2 倍
        return baseDmg * GameMath.RandFloat(0.9f, 1.1f);           // ±10% 波動
    }
}
