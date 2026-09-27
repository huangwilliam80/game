using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主面板（掛機首頁）：顯示「現在能做什麼、收益、下一目標」＋自動戰鬥舞臺。
/// UX 三問則：① 頂欄=資源 ② 中央=正在發生什麼 ③ 按鈕=下一步行動。
/// </summary>
public class MainPanelUI : MonoBehaviour
{
    public TMP_Text goalLabel;        // 下一個目標
    public TMP_Text offlineBanner;    // 離線收益橫幅
    public Button claimBtn, forgeBtn, stageBtn;

    void OnEnable()
    {
        GameEvents.OnHeroChanged += RefreshGoal;
        GameEvents.OnStageChanged += RefreshGoal;
        if (claimBtn) claimBtn.onClick.AddListener(OnClaim);
        if (forgeBtn) forgeBtn.onClick.AddListener(() => UIManager.I.Open("Forge"));
        if (stageBtn) stageBtn.onClick.AddListener(() => UIManager.I.Open("Stages"));
        ShowOfflineGreeting();
        RefreshGoal();
    }
    void OnDisable()
    {
        GameEvents.OnHeroChanged -= RefreshGoal;
        GameEvents.OnStageChanged -= RefreshGoal;
    }

    /// <summary>開場若有離線時間，先播「歡迎回來」橫幅（先給甜頭再談操作）。</summary>
    void ShowOfflineGreeting()
    {
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long diff = now - GameSave.Data.lastUnixSec;
        if (diff > 60 && offlineBanner)
        {
            IdleFormula.IdleRate(out long g, out long e, GameSave.Data.maxStageCleared, GameSave.Data.heroLevel);
            float bonus = CharacterSystem.IdleBonus();
            offlineBanner.text = $"歡迎回來！离线 {IdleSystem.Fmt(diff)}\n預估收益 金幣+{TopBarUI.Num((long)(g * bonus * diff))} · 經驗+{TopBarUI.Num((long)(e * bonus * diff))}";
            offlineBanner.gameObject.SetActive(true);
        }
        else if (offlineBanner) offlineBanner.gameObject.SetActive(false);
    }

    void OnClaim()
    {
        IdleSystem.I.CollectOfflineNow();
        if (offlineBanner) offlineBanner.gameObject.SetActive(false);
        RefreshGoal();
    }

    /// <summary>動態生成「下一目標」提示——放置遊戲的靈魂。</summary>
    void RefreshGoal()
    {
        var d = GameSave.Data;
        long power = CharacterSystem.Power();
        float need = GameMath.MonsterHp(d.currentStage, 4 + d.currentStage / 10) * 0.6f;
        string msg;
        if (power < need)
            msg = $"⚔ 戰力不足：{power:N0}/{need:N0}\n建議：強化裝備、升級功法、整理背包";
        else
            msg = $"✅ 可挑戰【第 {d.currentStage} 夜】！\n前往『關卡』一鍵掃蕩，或繼續掛機累積";
        int trash = 0;
        foreach (var b in d.bags) if (b.quality <= 1 && b.plus == 0) trash++;
        if (trash >= 5) msg += $"\n🧹 背包有 {trash} 件垃圾可一鍵分解";
        if (goalLabel) goalLabel.text = msg;
    }
}
