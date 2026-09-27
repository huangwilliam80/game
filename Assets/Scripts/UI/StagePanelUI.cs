using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 關卡面板：列出關卡、顯示需求戰力 vs 我方戰力，一鍵「掃蕩」跑 BattleSimulator。
/// </summary>
public class StagePanelUI : MonoBehaviour
{
    public Transform listParent;        // 關卡条目父節點
    public GameObject itemPrefab;       // 条目模板（含 TMP_Text + Button）
    public TMP_Text resultText;         // 結算區
    public Button backBtn;

    readonly List<GameObject> items = new();

    void OnEnable()
    {
        GameEvents.OnStageChanged += Refresh;
        GameEvents.OnHeroChanged += Refresh;
        if (backBtn) backBtn.onClick.AddListener(() => UIManager.I.CloseAll());
        BuildItems();
        Refresh();
    }
    void OnDisable()
    {
        GameEvents.OnStageChanged -= Refresh;
        GameEvents.OnHeroChanged -= Refresh;
    }

    void BuildItems()
    {
        // 只建立「已解鎖 + 下一關」的条目，避免一次實例化 200 個物件（性能）
        int total = Mathf.Min(GameSave.Data.maxStageCleared + 2, IdleSystem.I != null ? IdleSystem.I.Stages.Count : 0);
        while (items.Count < total)
        {
            int idx = items.Count;
            var go = Instantiate(itemPrefab, listParent);
            go.GetComponent<Button>().onClick.AddListener(() => Sweep(idx + 1));
            items.Add(go);
        }
    }

    void Refresh()
    {
        int maxCleared = GameSave.Data.maxStageCleared;
        long power = CharacterSystem.Power();
        for (int i = 0; i < items.Count; i++)
        {
            int stageId = i + 1;
            items[i].SetActive(true);
            var txt = items[i].GetComponentInChildren<TMP_Text>();
            if (!txt) continue;
            float need = GameMath.MonsterHp(stageId, 4 + stageId / 10) * 0.6f;
            string tag = stageId <= maxCleared ? "✅" : (power >= need ? "⚔可戰" : "🔒");
            txt.text = $"第 {stageId} 夜 {tag}\n需求≈{need:N0}";
            items[i].GetComponent<Image>().color = stageId <= maxCleared
                ? new Color(.3f, .5f, .3f) : (power >= need ? new Color(.7f, .55f, .2f) : Color.gray);
        }
    }

    /// <summary>掃蕩：執行自動戰鬥模擬 → 結算掉落。</summary>
    void Sweep(int stageId)
    {
        var d = GameSave.Data;
        if (stageId > d.maxStageCleared + 1) { GameEvents.Toast("尚未解鎖"); return; }

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
            resultText.text =
                $"<color=#7CFC9E>大捷！</color>用時 {res.duration:N1}s｜波次 {res.waveReached}\n" +
                $"金幣 +{TopBarUI.Num(res.goldReward)} 經驗 +{TopBarUI.Num(res.expReward)}\n" +
                $"掉落 {res.loot.Count} 件（已入背包）";
            GameEvents.RaiseBattleEnd(true, res);
        }
        else
        {
            resultText.text =
                $"<color=#FF8A8A>守床失敗…</color>撐到第 {res.waveReached} 波\n" +
                $"建議：強化／升階裝備、升級功法、增加床鋪數";
            GameEvents.RaiseBattleEnd(false, res);
        }
        GameSave.Save();
    }
}
