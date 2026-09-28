using System.Collections.Generic;

/// <summary>
/// 一次戰鬥模擬的結果。
/// </summary>
public sealed class BattleResult
{
    /// <summary>玩家方是否獲勝。</summary>
    public bool Victory;

    /// <summary>模擬耗時(秒)。</summary>
    public float Duration;

    /// <summary>交戰回合數。</summary>
    public int Rounds;

    /// <summary>玩家方造成的總傷害。</summary>
    public float DamageDealt;

    /// <summary>玩家方承受的總傷害。</summary>
    public float DamageTaken;

    /// <summary>戰鬥結束時玩家殘血比例(0~1)。</summary>
    public float HpRemaining;

    /// <summary>戰鬥結束時敵人殘血比例(0~1)。</summary>
    public float EnemyHpRemaining;

    /// <summary>掉落/獎勵清單。</summary>
    public List<BattleReward> Rewards = new List<BattleReward>();

    /// <summary>失敗原因(Victory=true 時為 null)。</summary>
    public string FailReason;
}

/// <summary>單一獎勵條目。</summary>
public sealed class BattleReward
{
    public string ItemId;
    public int Count;
    public float Quality;
}