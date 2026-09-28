using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainPanelUI : MonoBehaviour
{
    public TMP_Text goalLabel;
    public TMP_Text offlineBanner;
    public Button claimBtn, forgeBtn, stageBtn, dungeonBtn;

    void OnEnable() {
        GameEvents.OnHeroChanged += RefreshGoal;
        GameEvents.OnStageChanged += RefreshGoal;
        if (claimBtn) claimBtn.onClick.AddListener(OnClaim);
        if (forgeBtn) forgeBtn.onClick.AddListener(() => UIManager.I.Open("Forge"));
        if (stageBtn) stageBtn.onClick.AddListener(() => UIManager.I.Open("Stages"));
        
        // ★ 修復秘境按鈕點擊事件
        if (dungeonBtn) dungeonBtn.onClick.AddListener(() => { if (RunController.I != null) RunController.I.StartRun(GameSave.Data.currentStage); else GameEvents.Toast("控制器未就緒"); });
        
        ShowOfflineGreeting();
        RefreshGoal();
    }
    void OnDisable() {
        GameEvents.OnHeroChanged -= RefreshGoal;
        GameEvents.OnStageChanged -= RefreshGoal;
    }

    void ShowOfflineGreeting() {
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long diff = now - GameSave.Data.lastUnixSec;
        if (diff > 60 && offlineBanner) {
            IdleFormula.IdleRate(out long g, out long e, GameSave.Data.maxStageCleared, GameSave.Data.heroLevel);
            float bonus = CharacterSystem.IdleBonus();
            offlineBanner.text = $"歡迎回來！離線 {IdleSystem.Fmt(diff)}\n預估收益 金幣+{(long)(g * bonus * diff):N0} · 經驗+{(long)(e * bonus * diff):N0}";
            offlineBanner.gameObject.SetActive(true);
        } else if (offlineBanner) {
            offlineBanner.gameObject.SetActive(false);
        }
    }

    void OnClaim() {
        IdleSystem.I.CollectOfflineNow();
        if (offlineBanner) offlineBanner.gameObject.SetActive(false);
        RefreshGoal();
    }

    void RefreshGoal() {
        var d = GameSave.Data;
        long power = CharacterSystem.Power();
        float need = GameMath.MonsterHp(d.currentStage, 4 + d.currentStage / 10) * 0.6f;
        string msg = power < need ? $"⚔ 戰力不足：{power:N0}/{need:N0}\n建議：強化裝備、升級功法" : $"✅ 可挑戰【第 {d.currentStage} 夜】！\n前往『關卡』一鍵掃蕩";
        int trash = 0;
        foreach (var b in d.bags) if (b.quality <= 1 && b.plus == 0) trash++;
        if (trash >= 5) msg += $"\n🧹 背包有 {trash} 件垃圾可一鍵分解";
        if (goalLabel) goalLabel.text = msg;
    }
}