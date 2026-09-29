using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 修復 CS0103: The name 'GameEvents' does not exist in the current context
// 以別名直接綁定全域事件总线，確保每個檔案都能解析到 GameEvents（不受專案載入順序影響）。
using GameEvents = global::GameEvents;

public class DormBattleView : MonoBehaviour
{
    public RectTransform stageArea;
    public List<RectTransform> bedSlots;
    public GameObject ghostPrefab;
    private GameObject activeGhost;

    void OnEnable() { GameEvents.OnBattleStart += OnWaveStart; GameEvents.OnBattleEnd += OnWaveEnd; }
    void OnDisable() { GameEvents.OnBattleStart -= OnWaveStart; GameEvents.OnBattleEnd -= OnWaveEnd; }

    void OnWaveStart(int wave) {
        int targetBed = Random.Range(0, bedSlots.Count);
        RectTransform target = bedSlots[targetBed];
        if (activeGhost == null) {
            activeGhost = Instantiate(ghostPrefab, stageArea);
            activeGhost.SetActive(true);
        }
        activeGhost.GetComponent<RectTransform>().anchoredPosition = target.anchoredPosition + new Vector2(0, 50);
        StartCoroutine(AttackAnim(activeGhost.GetComponent<RectTransform>()));
    }

    void OnWaveEnd(bool win, BattleResult result) {
        if (activeGhost != null) { activeGhost.SetActive(false); activeGhost = null; }
    }

    private IEnumerator AttackAnim(RectTransform rt) {
        float duration = 0.5f, elapsed = 0;
        Vector2 startPos = rt.anchoredPosition, endPos = startPos + new Vector2(0, -30);
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }
        rt.anchoredPosition = startPos;
    }
}