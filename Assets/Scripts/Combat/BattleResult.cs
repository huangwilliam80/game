using System.Collections.Generic;

/// <summary>
/// 一次「掃蕩／自動戰鬥」的結果。
/// 注意：欄位命名需與 BattleSimulator / StagePanelUI 的使用方式一致（小寫欄位）。
/// </summary>
public sealed class BattleResult
{
    public int stageId;              // 關卡 id
    public bool victory;             // 是否通關
    public float duration;           // 模擬耗時（秒）
    public int waveReached;          // 打到第幾波
    public long goldReward;          // 金幣獎勵
    public long expReward;           // 經驗獎勵
    public float hpRemaining = 1f;   // 我方殘血比例 0~1
    public readonly List<EquipmentInstance> loot = new List<EquipmentInstance>();
}
