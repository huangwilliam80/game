using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// ★ 修復 CS0103: The name 'GameEvents' does not exist in the current context
// 以別名直接綁定全域事件总线，確保每個檔案都能解析到 GameEvents（不受專案載入順序影響）。
using GameEvents = global::GameEvents;

public class StagePanelUI : MonoBehaviour
{
    public Transform listParent;
    public GameObject itemPrefab;
    public TMP_Text resultText;
    public Button backBtn;
    public Button runBtn;
    public TMP_Text runHint;

    readonly List<GameObject> items = new List<GameObject>();

    void OnEnable()
    {
        GameEvents.OnStageChanged += Refresh;
        GameEvents.OnHeroChanged += Refresh;
        if (backBtn) backBtn.onClick.AddListener(() => UIManager.I.CloseAll());
        if (runBtn) runBtn.onClick.AddListener(OnRunClicked);
        GameEvents.OnRunStart += OnRunStateChanged;
        GameEvents.OnRunEnd += OnRunStateChanged;
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

    void OnRunStateChanged() => RefreshRunButton();

    void RefreshRunButton()
    {
        int stage = Mathf.Max(1, GameSave.Data.currentStage);
        bool busy = RunController.I != null && RunController.I.IsRunning;
        if (runBtn) runBtn.interactable = !busy;
        if (runHint)
            runHint.text = busy
                ? "歷練進行中…（請查看主頁戰鬥畫面）"
                : $"第 {stage} 夜 · 靈玉秘境\n通關獎勵：{RunController.RewardPreview(stage)}";
    }

    void OnRunClicked()
    {
        if (RunController.I == null) { GameEvents.Toast("秘境系統未就緒"); return; }
        RunController.I.StartRun(Mathf.Max(1, GameSave.Data.currentStage));
    }

    // ★ 補全的方法
    void Refresh()
    {
        if (resultText) resultText.text = $"已通關最高：第 {GameSave.Data.maxStageCleared} 夜\n目前戰力：{CharacterSystem.Power():N0}";
    }

    void BuildItems()
    {
        if (listParent == null || itemPrefab == null) return;
        foreach (Transform child in listParent) Destroy(child.gameObject);
        int maxShow = Mathf.Min(20, GameSave.Data.maxStageCleared + 5);
        for (int i = 1; i <= maxShow; i++)
        {
            var item = Instantiate(itemPrefab, listParent);
            item.SetActive(true);
            var txt = item.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = $"第 {i} 夜";
        }
    }
}