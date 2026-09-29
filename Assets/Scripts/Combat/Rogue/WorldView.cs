using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 上半部世界視窗:左側主角、右側敵人、血條、傷害數字池、當前 buff 列。
/// 沿用現有「物件預製池化、零 Instantiate/Destroy」策略。
/// </summary>
public class WorldView : MonoBehaviour
{
    public static WorldView I { get; private set; }

    [Header("引用(由 GameBootstrapper 指派)")]
    public RectTransform worldArea;        // 上半部容器(錨點約 0.45~1)
    public Image heroImage;                // 主角圖示
    public Image heroHpFill;               // Image.type = Filled, Filled Method = Horizontal
    public TMP_Text heroHpText;
    public RectTransform enemyAnchor;      // 敵人位置(右側)
    public Image enemyHpFill;
    public TMP_Text enemyNameText;
    public TMP_Text enemyHpText;
    public Transform buffLabel;            // 底部 buff 標籤(放一個 TMP 即可)
    public GameObject dmgNumberPrefab;     // 傷害數字模板(TMP,隱藏)
    public GameObject enemyPrefab;         // 敵人圖示模板(隱藏)
    public TMP_Text waveBanner;            // ★ 波次橫幅（第 X 波 / BOSS）

    readonly List<GameObject> dmgPool = new List<GameObject>();
    readonly List<float> dmgTimers = new List<float>();
    GameObject enemyGo;

    float heroMaxHp = 1f, enemyMaxHp = 1f;

    void Awake() { I = this; }

    /// <summary>
    /// ★ 池必須在「世界視窗尺寸已算好」之後才建立。
    /// Unity 在同一幀內 Instantiate 出的 UI，其 RectTransform 尚未完成佈局（尺寸為 0），
    /// 若直接在 Awake / 事件訂閱當下建池，所有飄字與敵人會拿到非法矩形 → NullReferenceException。
    /// 用 Start + 延遲一幀來保證 worldArea.rect 是有效值。
    /// </summary>
    void Start() => StartCoroutine(BuildPoolsNextFrame());

    System.Collections.IEnumerator BuildPoolsNextFrame()
    {
        yield return null;
        BuildPools();
        ShowIdleStage();
    }

    void OnEnable()
    {
        GameEvents.OnRunStart += OnRunStart;
        GameEvents.OnRunEnd += OnRunEnd;
        GameEvents.OnWaveCleared += OnWaveCleared;
        GameEvents.OnStageChanged += ShowIdleStage;
        // ★ 不在這裡 BuildPools()：改由 Start 延遲一幀（見上方註解）
        if (dmgPool.Count > 0) ShowIdleStage();
    }

    void OnDisable()
    {
        GameEvents.OnRunStart -= OnRunStart;
        GameEvents.OnRunEnd -= OnRunEnd;
        GameEvents.OnWaveCleared -= OnWaveCleared;
        GameEvents.OnStageChanged -= ShowIdleStage;
    }

    /// <summary>沒有 run 時，上半部顯示「掛機中：第 X 夜」，讓畫面永遠有語意。</summary>
    void ShowIdleStage()
    {
        if (waveBanner == null) return;
        if (RunController.I != null && RunController.I.IsRunning) return;
        int st = Mathf.Max(1, GameSave.Data.currentStage);
        waveBanner.text = $"🏮 掛機歷練中 · 第 {st} 夜";
    }

    void BuildPools()
    {
        // ★ 防呆：容器還沒佈局完成（rect 寬高為 0）時不要建池，否則子節點尺寸全 0 → 後續空參考
        if (worldArea == null || worldArea.rect.width < 1f || worldArea.rect.height < 1f) return;

        if (dmgPool.Count == 0 && dmgNumberPrefab != null)
        {
            for (int i = 0; i < 20; i++)
            {
                var g = Instantiate(dmgNumberPrefab, worldArea);
                g.SetActive(false);
                dmgPool.Add(g);
                dmgTimers.Add(0f);
            }
        }
        if (enemyPrefab != null && enemyGo == null && enemyAnchor != null)
        {
            enemyGo = Instantiate(enemyPrefab, enemyAnchor);
            enemyGo.SetActive(true);
        }
    }

    // ---- Run 事件 ----
    void OnRunStart()
    {
        if (waveBanner != null) waveBanner.text = "⚔ 靈玉秘境 · 準備戰鬥…";
        var h = CharacterSystem.FinalStats();
        heroMaxHp = Mathf.Max(1f, h.hp);
        SetHeroHp(heroMaxHp);
        if (buffLabel != null)
        {
            var t = buffLabel.GetComponentInChildren<TMP_Text>();
            if (t) t.text = "";
        }
    }

    void OnRunEnd()
    {
        // 清空畫面(可在此播結束動畫)
        SetEnemyHp(0);
        ShowIdleStage();
    }

    void OnWaveCleared(int nextWave)
    {
        if (RunController.I == null) return;
        int stageId = RunController.I.stageId;
        int totalWaves = RunController.I.totalWaves;
        bool boss = nextWave >= totalWaves;
        float eHp = GameMath.MonsterHp(stageId, nextWave) * (boss ? 6f : 1f);
        enemyMaxHp = Mathf.Max(1f, eHp);
        SetEnemyHp(eHp);
        if (enemyNameText != null) enemyNameText.text = boss ? "鬼王" : $"怨鬼 Lv.{nextWave}";
        if (waveBanner != null)
            waveBanner.text = boss ? $"💀 BOSS · 第 {nextWave}/{totalWaves} 波" : $"第 {nextWave}/{totalWaves} 波";
    }

    // ---- 供 RunController.Tick 呼叫 ----
    public void SetHeroHp(float hp)
    {
        float t = Mathf.Clamp01(hp / heroMaxHp);
        if (heroHpFill != null) heroHpFill.fillAmount = t;
        if (heroHpText != null) heroHpText.text = $"{Mathf.FloorToInt(Mathf.Max(0, hp)):N0}";
    }

    public void SetEnemyHp(float hp)
    {
        float t = Mathf.Clamp01(hp / enemyMaxHp);
        if (enemyHpFill != null) enemyHpFill.fillAmount = t;
        if (enemyHpText != null) enemyHpText.text = $"{Mathf.FloorToInt(Mathf.Max(0, hp)):N0}";
    }

    public void FlashHero()
    {
        if (heroImage == null) return;
        heroImage.color = Color.red;
        StartCoroutine(ResetColor(heroImage, Color.white, 0.12f));
    }

    public void FlashEnemy()
    {
        if (enemyGo == null) return;
        var img = enemyGo.GetComponent<Image>();
        if (img == null) return;
        img.color = Color.yellow;
        StartCoroutine(ResetColor(img, Color.white, 0.10f));
    }

    IEnumerator ResetColor(Image img, Color target, float time)
    {
        yield return new WaitForSeconds(time);
        if (img != null) img.color = target;
    }

    /// <summary>在螢幕隨機位置噴一個傷害數字,1 秒後自動回池。</summary>
    public void SpawnDmg(float amount, bool isCrit, bool toHero)
    {
        if (dmgPool.Count == 0) BuildPools();      // 尚未就緒時補建（避免整場 run 沒有飄字）
        if (dmgPool.Count == 0) return;            // 仍失敗就靜默跳過，不影響戰鬥邏輯

        int idx = -1;
        for (int i = 0; i < dmgPool.Count; i++)
            if (!dmgPool[i].activeSelf) { idx = i; break; }
        if (idx < 0) idx = 0; // 池滿時覆蓋最舊(極少發生)

        var go = dmgPool[idx];
        dmgTimers[idx] = 0f;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (t == null) { go.SetActive(true); return; }

        t.text = $"{Mathf.FloorToInt(Mathf.Abs(amount)):N0}";
        t.color = isCrit ? new Color(1f, 0.95f, 0.2f) : (toHero ? new Color(1f, 0.3f, 0.3f) : Color.white);
        t.fontSize = isCrit ? 54 : 36;

        var rt = go.GetComponent<RectTransform>();
        float anchorX = toHero ? 0.30f : 0.70f;
        rt.anchorMin = rt.anchorMax = new Vector2(anchorX + UnityEngine.Random.Range(-0.05f, 0.05f), 0.50f);
        rt.anchoredPosition = Vector2.zero;
        go.SetActive(true);
    }

    /// <summary>更新底部 buff 標籤(用名稱字串顯示即可,進階可改用 Icon Sprite)。</summary>
    public void SetBuffs(List<RogueBuffDef> buffs)
    {
        if (buffLabel == null) return;
        var t = buffLabel.GetComponentInChildren<TMP_Text>();
        if (t == null) return;
        string s = "";
        for (int i = 0; i < buffs.Count; i++)
            s += $"[{buffs[i].name}] ";
        t.text = s;
    }

    /// <summary>由 GameBootstrapper 呼叫：把池裡的傷害數字也換成中文字型（若有中文訊息）。</summary>
    public void ApplyFontToPool(TMPro.TMP_FontAsset font)
    {
        if (font == null) return;
        foreach (var go in dmgPool)
        {
            var t = go != null ? go.GetComponent<TextMeshProUGUI>() : null;
            if (t != null) t.font = font;
        }
        if (dmgNumberPrefab != null)
        {
            var tp = dmgNumberPrefab.GetComponent<TextMeshProUGUI>();
            if (tp != null) tp.font = font;
        }
    }

    void Update()
    {
        // 池若因容器尚未就緒而沒建起來，之後補建（最多試到成功為止，成本極低）
        if (dmgPool.Count == 0) BuildPools();

        for (int i = 0; i < dmgPool.Count; i++)
        {
            if (dmgPool[i] == null || !dmgPool[i].activeSelf) continue;
            dmgTimers[i] += Time.deltaTime;
            float t = dmgTimers[i];
            var rt = dmgPool[i].GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition += new Vector2(0, Time.deltaTime * 70f);
            var cg = dmgPool[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = Mathf.Max(0f, 1f - t);
            if (t > 1f) dmgPool[i].SetActive(false);
        }
    }
}