using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 視覺化戰鬥舞臺：把「掛機波次」以宿舍床鋪 vs 小鬼的動畫呈現。
/// 性能策略：物件全部預製池化（Object Pool），零 Instantiate / 零 Destroy。
/// </summary>
public class DormBattleView : MonoBehaviour
{
    [Header("引用（由 UI 生成器自動指派）")]
    public RectTransform stageArea;       // 戰鬥顯示區域
    public List<RectTransform> bedSlots;  // 最多 6 張床
    public GameObject ghostPrefab;        // 小鬼模板（色塊即可）

    readonly List<GameObject> ghostPool = new List<GameObject>();
    readonly List<float> ghostHp = new List<float>();
    int activeGhosts;
    float animTimer;

    public static DormBattleView I { get; private set; }

    void Awake() { I = this; }

    void OnEnable()
    {
        GameEvents.OnBattleStart += OnWave;
        BuildPool();
        RefreshBeds();
    }
    void OnDisable() { GameEvents.OnBattleStart -= OnWave; }

    void BuildPool()
    {
        if (ghostPool.Count > 0 || ghostPrefab == null || stageArea == null) return;
        for (int i = 0; i < 12; i++)
        {
            var g = Instantiate(ghostPrefab, stageArea);
            g.SetActive(false);
            ghostPool.Add(g);
            ghostHp.Add(0);
        }
    }

    void RefreshBeds()
    {
        int beds = Mathf.Clamp(GameSave.Data.bedCount, 1, bedSlots.Count);
        for (int i = 0; i < bedSlots.Count; i++)
            if (bedSlots[i]) bedSlots[i].gameObject.SetActive(i < beds);
    }

    /// <summary>每完成一波，播放一小段「鬼來→被殲滅」的視覺節拍。</summary>
    void OnWave(int waveNum)
    {
        activeGhosts = Mathf.Clamp(1 + waveNum % 5, 1, ghostPool.Count);
        for (int i = 0; i < ghostPool.Count; i++)
        {
            var go = ghostPool[i];
            bool on = i < activeGhosts;
            go.SetActive(on);
            if (on)
            {
                var rt = go.GetComponent<RectTransform>();
                float x = Mathf.Lerp(-0.8f, 0.8f, i / (float)Mathf.Max(1, activeGhosts - 1));
                rt.anchorMin = rt.anchorMax = new Vector2(.5f + x * .45f, .75f);
                rt.anchoredPosition = Vector2.zero;
                ghostHp[i] = 1f;
            }
        }
        animTimer = 0;
    }

    void Update()
    {
        if (activeGhosts <= 0) return;
        animTimer += Time.deltaTime;
        // 小鬼往下飄、3 秒後消散（純 Transform 動畫 → 不吃 GC）
        for (int i = 0; i < activeGhosts; i++)
        {
            var rt = ghostPool[i].GetComponent<RectTransform>();
            float t = Mathf.Clamp01(animTimer / 3f);
            rt.anchoredPosition = new Vector2(Mathf.Sin((animTimer + i) * 3f) * 20f, -t * 260f);
            var cg = ghostPool[i].GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 1f - t * t;
        }
        if (animTimer >= 3f)
        {
            for (int i = 0; i < activeGhosts; i++) ghostPool[i].SetActive(false);
            activeGhosts = 0;
        }
    }
}
