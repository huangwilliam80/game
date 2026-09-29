using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


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