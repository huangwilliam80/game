using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// ★ 修復 CS0103: The name 'GameEvents' does not exist in the current context
// 以別名直接綁定全域事件总线，確保每個檔案都能解析到 GameEvents（不受專案載入順序影響）。
using GameEvents = global::GameEvents;

/// <summary>
/// 秘境 run 的戰鬥視覺：血條、波次、傷害浮動數字、三選一 buff 面板、結算面板。
/// ★ 事件簽名與 GameEvents 保持一致（OnRunChoice = Action&lt;List&lt;RogueBuffDef&gt;, Action&lt;int&gt;&gt;）。
/// </summary>
public class RunViewUI : MonoBehaviour
{
    public Image heroFill, enemyFill;
    public TMP_Text heroLabel, enemyLabel, waveLabel, buffLabel;
    public RectTransform floatRoot;

    [Header("三選一 buff 面板")]
    public GameObject choicePanel;
    public Button[] choiceButtons = new Button[3];
    public TMP_Text[] choiceTexts = new TMP_Text[3];

    [Header("結算面板")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button resultCloseBtn;

    readonly List<TMP_Text> floatPool = new List<TMP_Text>();
    readonly List<float> floatAge = new List<float>();
    float lastHitTime = -1f;
    Action<int> pendingPick;   // 等待玩家點選的回调

    bool btnsBound;
    void OnEnable()
    {
        GameEvents.OnRunStart += OnRunStart;
        GameEvents.OnRunEnd += OnEnd;
        GameEvents.OnRunChoice += OnChoice;
        GameEvents.OnRunWave += OnWave;
        if (!btnsBound)
        {
            btnsBound = true;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                if (choiceButtons[i]) choiceButtons[i].onClick.AddListener(() => Pick(idx));
            }
            if (resultCloseBtn) resultCloseBtn.onClick.AddListener(() => Hide(resultPanel));
        }
        BuildFloatPool();
        Hide(choicePanel); Hide(resultPanel);
    }

    void OnDisable()
    {
        GameEvents.OnRunStart -= OnRunStart;
        GameEvents.OnRunEnd -= OnEnd;
        GameEvents.OnRunChoice -= OnChoice;
        GameEvents.OnRunWave -= OnWave;
    }

    void OnRunStart()
    {
        Hide(choicePanel); Hide(resultPanel);
        lastHitTime = -1f; pendingPick = null;
    }

    void OnWave(int cur, int total)
    {
        if (waveLabel) waveLabel.text = $"波次 {cur}/{total}";
    }

    static void Hide(GameObject g) { if (g) g.SetActive(false); }

    void BuildFloatPool()
    {
        if (floatRoot == null || floatPool.Count > 0) return;
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject("Dmg" + i, typeof(RectTransform));
            go.transform.SetParent(floatRoot, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = 44; t.alignment = TextAlignmentOptions.Center;
            go.SetActive(false);
            floatPool.Add(t); floatAge.Add(0f);
        }
    }

    void OnChoice(List<RogueBuffDef> options, Action<int> onPick)
    {
        pendingPick = onPick;
        if (!choicePanel) { pendingPick?.Invoke(0); pendingPick = null; return; }
        for (int i = 0; i < 3; i++)
        {
            bool has = i < options.Count;
            if (choiceTexts[i])
                choiceTexts[i].text = has ? $"{options[i].name}\n{options[i].desc}" : "";
            if (choiceButtons[i]) choiceButtons[i].gameObject.SetActive(has);
        }
        choicePanel.SetActive(true);
    }

    void Pick(int idx)
    {
        Hide(choicePanel);
        var cb = pendingPick; pendingPick = null;
        cb?.Invoke(idx);
    }

    void OnEnd()
    {
        Hide(choicePanel);
        var rc = RunController.I;
        if (!resultPanel || !rc) return;
        resultText.text = rc.LastRunWin
            ? $"<color=#7CFC9E>秘境通關！</color>第 {rc.Stage} 夜\n靈玉 +{rc.RunCrystal}・金幣 +{TopBarUI.Num(rc.RunGold)}\n戰利品 {rc.RunLoot.Count} 件已入背包"
            : $"<color=#FF8A8A>歷練失敗…</color>撐到第 {rc.Wave} 波\n安慰獎 靈玉 +{rc.RunCrystal}";
        resultPanel.SetActive(true);
    }

    void Update()
    {
        var rc = RunController.I;
        bool run = rc != null && rc.IsRunning;
        if (heroFill) heroFill.gameObject.SetActive(run);
        if (enemyFill) enemyFill.gameObject.SetActive(run);
        if (buffLabel) buffLabel.text = run && rc.OwnedBuffs.Count > 0
            ? "Buff: " + string.Join("、", System.Linq.Enumerable.Select(rc.OwnedBuffs, b => b.name))
            : "";
        if (!run) return;

        if (heroFill) heroFill.fillAmount = Mathf.Clamp01(rc.HeroHp / Mathf.Max(1f, rc.HeroMaxHp));
        if (enemyFill) enemyFill.fillAmount = Mathf.Clamp01(rc.EnemyHp / Mathf.Max(1f, rc.EnemyMaxHp));
        if (heroLabel) heroLabel.text = $"我方 {(int)Mathf.Max(0f, rc.HeroHp)}/{(int)rc.HeroMaxHp}";
        if (enemyLabel) enemyLabel.text = $"{rc.EnemyName} {(int)Mathf.Max(0f, rc.EnemyHp)}/{(int)rc.EnemyMaxHp}";

        if (rc.LastHitTime != lastHitTime)
        {
            lastHitTime = rc.LastHitTime;
            SpawnFloat((int)rc.LastEnemyHit, new Color(1f, .9f, .3f));
        }
        for (int i = 0; i < floatPool.Count; i++)
        {
            if (!floatPool[i].gameObject.activeSelf) continue;
            floatAge[i] += Time.deltaTime;
            floatPool[i].rectTransform.anchoredPosition += new Vector2(0f, 140f * Time.deltaTime);
            var c = floatPool[i].color; c.a = 1f - floatAge[i]; floatPool[i].color = c;
            if (floatAge[i] >= 1f) floatPool[i].gameObject.SetActive(false);
        }
    }

    void SpawnFloat(int v, Color col)
    {
        for (int i = 0; i < floatPool.Count; i++)
        {
            if (floatPool[i].gameObject.activeSelf) continue;
            floatPool[i].gameObject.SetActive(true);
            floatPool[i].text = v.ToString();
            floatPool[i].color = col;
            floatAge[i] = 0f;
            var rt = floatPool[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(Random.Range(.3f, .7f), Random.Range(.45f, .8f));
            rt.anchoredPosition = Vector2.zero;
            return;
        }
    }
}
