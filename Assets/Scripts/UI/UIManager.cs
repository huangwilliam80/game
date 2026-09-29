using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// ★ 修復 CS0103: The name 'GameEvents' does not exist in the current context
// 以別名直接綁定全域事件总线，確保每個檔案都能解析到 GameEvents（不受專案載入順序影響）。
using GameEvents = global::GameEvents;

public class UIManager : MonoBehaviour
{
    public static UIManager I { get; private set; }
    public string[] panelNames;
    public GameObject[] panels;
    public TMP_Text toastText;
    public CanvasGroup toastGroup;
    private Dictionary<string, GameObject> panelDict = new Dictionary<string, GameObject>();
    private float toastTimer;

    void Awake() {
        I = this;
        for (int i = 0; i < panelNames.Length; i++) {
            panelDict[panelNames[i]] = panels[i];
            panels[i].SetActive(i == 0);
        }
        GameEvents.OnToast += ShowToast;
    }
    void OnDestroy() { GameEvents.OnToast -= ShowToast; }

    // ★ 補齊 CloseAll 方法
    public void CloseAll() {
        foreach (var kvp in panelDict) kvp.Value.SetActive(false);
    }

    public void Open(string name) {
        foreach (var kvp in panelDict) kvp.Value.SetActive(kvp.Key == name);
    }

    void ShowToast(string msg) {
        toastText.text = msg;
        toastGroup.alpha = 1f;
        toastGroup.blocksRaycasts = true;
        toastTimer = 2f;
    }

    void Update() {
        if (toastTimer > 0) {
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0) { toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false; }
        }
    }
}