using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 關卡面板：列出關卡、顯示需求戰力 vs 我方戰力，一鍵「掃蕩」跑 BattleSimulator。
/// 並提供「靈玉秘境」（Roguelite 即時戰鬥）入口。
/// </summary>
public class StagePanelUI : MonoBehaviour
{
    [Header("引用（由 GameBootstrapper 自動指派）")]
    public Transform listParent;        // 關卡条目父節點
    public GameObject itemPrefab;       // 条目模板（含 TMP_Text + Button）
    public TMP_Text resultText;         // 掃蕩結算區
    public Button backBtn;
    public Button runBtn;               // ★ 進入「靈玉秘境」
    public TMP_Text runHint;            // 秘境獎勵預覽文案

    readonly List<GameObject> items = new List<GameObject>();
    bool runBtnInited;                  // 防止重複綁定按鈕

    void OnEnable()
    {
        GameEvents.OnStageChanged += Refresh;
        GameEvents.OnHeroChanged += Refresh;
        GameEvents.OnRunStart += OnRunStateChanged;
        GameEvents.OnRunEnd += OnRunStateChanged;
        if (backBtn) backBtn.onClick.AddListener(() => UIManager.I.CloseAll());
        TryInitRunButton();
        BuildItems();
        Refresh();
        RefreshRunButton();
    }

    void OnDisable()
    {
        GameEvents.OnStageChanged -= Refresh;
        GameEvents.OnHeroChanged -= Refresh;
        GameEvents.OnRunStart -= OnRunStateChanged;
        GameEvents.OnRunEnd -= OnRunStateChanged;
    }

    void Update()
    {
        // Bootstrapper 是在 AddComponent（觸發 OnEnable）「之後」才指派 runBtn / itemPrefab，
        // 所以這裡偵測到 runBtn 就緒後補一次初始化（冪等，只執行一次）。
        TryInitRunButton();
    }

    void TryInitRunButton()
    {
        if (runBtnInited || runBtn == null) return;
        runBtnInited = true;
        runBtn.onClick.AddListener(OnRunClicked);
        BuildItems();
        Refresh();
        RefreshRunButton();
    }

    void OnRunStateChanged() => RefreshRunButton();

    /// <summary>秘境按鈕：顯示這一把會拿到什麼，並在有 run 進行時禁用。</summary>
    public void RefreshRunButton()
    {
        int stage = Mathf.Max(1, GameSave.Data.currentStage);
        bool busy = RunController.I != null && RunController.I.IsRunning;
        if (runBtn) runBtn.interactable = !busy;
        if (runHint)
            runHint.text = busy
                ? "歷練進行中…（上半部即為戰鬥畫面）"
                : $"第 {stage} 夜 · 靈玉秘境\n通關獎勵：{RunController.RewardPreview(stage)}\n特色：每波三選一功法、開局重置";
    }

    void OnRunClicked()
    {
        if (RunController.I == null) { GameEvents.Toast("秘境系統未就緒"); return; }
        RunController.I.StartRun(Mathf.Max(1, GameSave.Data.currentStage));
    }

    void BuildItems()
    {
        // 防禦：引用尚未指派時直接返回（避免 Instantiate null 報錯）
        if (itemPrefab == null || listParent == null) return;
        // 只建立「已解鎖 + 下一關」的条目，避免一次實例化 200 個物件（性能）
        int total = Mathf.Min(GameSave.Data.maxStageCleared + 2,
                              IdleSystem.I != null ? IdleSystem.I.Stages.Count : 0);
        while (items.Count < total)
        {
            int idx = items.Count;
            var go = Instantiate(itemPrefab, listParent);
            go.SetActive(true);
            go.GetComponent<Button>().onClick.AddListener(() => Sweep(idx + 1));
            items.Add(go);
        }
    }

    public void Refresh()
    {
        BuildItems();   // 補建可能缺失的条目（冪等，安全）
        int maxCleared = GameSave.Data.maxStageCleared;
        long power = CharacterSystem.Power();
        for (int i = 0; i < items.Count; i++)
        {
            int stageId = i + 1;
            items[i].SetActive(true);
            var txt = items[i].GetComponentInChildren<TMP_Text>();
            if (!txt) continue;
            float need = GameMath.MonsterHp(stageId, 4 + stageId / 10) * 0.6f;
            // ★ 已移除 Emoji，改用純文字標籤（思源黑體可正常顯示）
            string tag = stageId <= maxCleared ? "【已通】" : (power >= need ? "【可戰】" : "【鎖定】");
            txt.text = $"第 {stageId} 夜 {tag}｜點擊掃蕩\n需求戰力 ≈{need:N0}／我方 {power:N0}";
            items[i].GetComponent<Image>().color = stageId <= maxCleared
                ? new Color(.3f, .5f, .3f)
                : (power >= need ? new Color(.7f, .55f, .2f) : Color.gray);
        }
    }

    /// <summary>掃蕩：執行自動戰鬥模擬 → 結算掉落。</summary>
    void Sweep(int stageId)
    {
        var d = GameSave.Data;
        if (stageId > d.maxStageCleared + 1) { GameEvents.Toast("尚未解鎖"); return; }
        if (RunController.I != null && RunController.I.IsRunning)
        { GameEvents.Toast("秘境歷練進行中，請先結束"); return; }

        bool win;
        var res = BattleSimulator.Simulate(stageId, out win, out _);

        if (win)
        {
            d.gold += res.goldReward;
            d.spiritCrystal += 2 + stageId / 5;              // 靈玉來源之一
            CharacterSystem.AddExp(res.expReward);
            foreach (var e in res.loot) InventorySystem.AddToBag(e);
            if (stageId == d.maxStageCleared + 1) d.maxStageCleared = stageId;
            d.currentStage = Mathf.Min(d.maxStageCleared + 1, 200);
            GameEvents.RaiseCurrency(CurrencyType.Gold, d.gold);
            GameEvents.RaiseCurrency(CurrencyType.SpiritCrystal, d.spiritCrystal);
            GameEvents.RaiseStage();
            if (resultText)
                resultText.text =
                    $"<color=#7CFC9E>大捷！</color>用時 {res.duration:N1}s｜波次 {res.waveReached}\n" +
                    $"金幣 +{TopBarUI.Num(res.goldReward)} 經驗 +{TopBarUI.Num(res.expReward)}\n" +
                    $"掉落 {res.loot.Count} 件（已入背包）";
            GameEvents.RaiseBattleEnd(true, res);
        }
        else
        {
            if (resultText)
                resultText.text =
                    $"<color=#FF8A8A>守床失敗…</color>撐到第 {res.waveReached} 波\n" +
                    $"建議：強化／升階裝備、升級功法、增加床鋪數";
            GameEvents.RaiseBattleEnd(false, res);
        }
        GameSave.Save();
    }
}