using System;
using UnityEngine;

/// <summary>
/// 全站共用的數值公式。集中管理方便調參（平衡性只需改這裡）。
/// </summary>
public static class GameMath
{
    static readonly System.Random rng = new((int)DateTime.Now.Ticks);

    public static int RandInt(int min, int maxInclusive) =>
        maxInclusive <= min ? min : rng.Next(min, maxInclusive + 1);

    public static float RandFloat(float min, float max) => (float)(rng.NextDouble() * (max - min) + min);

    public static bool Chance(float p) => rng.NextDouble() < p;

    /// <summary>加權隨機：weights 與 items 等長。</summary>
    public static T WeightedPick<T>(T[] items, float[] weights)
    {
        float total = 0; foreach (var w in weights) total += w;
        float r = (float)rng.NextDouble() * total;
        for (int i = 0; i < items.Length; i++)
        {
            r -= weights[i];
            if (r <= 0) return items[i];
        }
        return items[^1];
    }

    /// <summary>升級所需經驗：二次曲線，後期拉長養成曲線。</summary>
    public static long ExpToLevel(int level) => (long)(80 * Math.Pow(level, 1.75) + 120 * level);

    /// <summary>掛機收益依據「已通關最高關卡」成長。</summary>
    public static float StagePowerMult(int stage) => 1f + stage * 0.35f;

    /// <summary>怪物血量曲線。</summary>
    public static float MonsterHp(int stage, int wave) =>
        (40f + stage * 26f) * (1f + wave * 0.22f) * Mathf.Pow(1.06f, stage);

    public static float MonsterAtk(int stage, int wave) =>
        (6f + stage * 3.4f) * (1f + wave * 0.10f) * Mathf.Pow(1.05f, stage);

    /// <summary>強化費用／成功率（隨等級遞減）。</summary>
    public static int EnhanceCost(int plusLevel) => (int)(120 * Mathf.Pow(1.55f, plusLevel));
    public static float EnhanceRate(int plusLevel) => Mathf.Clamp(1f - plusLevel * 0.045f, 0.25f, 1f);

    /// <summary>洗鍊費用。</summary>
    public static int ReforgeCost(int quality) => (int)(400 * Mathf.Pow(1.9f, quality));

    /// <summary>戰力估算（顯示用，非戰鬥判定）。</summary>
    public static long EstimatePower(in StatBlock s) =>
        (long)(s.atk * 6 + s.def * 4 + s.hp * 0.25f + s.critRate * 900 + s.speed * 40 + s.special * 30);
}

/// <summary>掛機產率公式（離線 / 在線共用同一套 → 行為一致、可預測）。</summary>
public static class IdleFormula
{
    public static void IdleRate(out long goldPerSec, out long expPerSec, int maxStage, int heroLevel)
    {
        float m = GameMath.StagePowerMult(Mathf.Max(1, maxStage));
        goldPerSec = (long)(10 * m * (1 + heroLevel * 0.05f));
        expPerSec = (long)(6 * m * (1 + heroLevel * 0.03f));
    }
}
