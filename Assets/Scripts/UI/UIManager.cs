using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 總管：面板開關、Toast 飄字、底部導航。
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager I { get; private set; }

    [Header("面板（名稱 → GameObject）")]
    public string[] panelNames = { "Main", "Equip", "Character", "Stages", "Forge" };
    public GameObject[] panels;

    [Header("Toast（留空則自動使用場景中的 Toast）")]
    public TMP_Text toastText;
    public CanvasGroup toastGroup;

    readonly Dictionary<string, GameObject> dic = new Dictionary<string, GameObject>();

    void Awake()
    {
        I = this;
        if (panelNames != null && panels != null) InitPanels();
    }

    /// <summary>由 GameBootstrapper 呼叫：把全域 Toast 元件接進來。</summary>
    public void BindToast(TMP_Text text, CanvasGroup group)
    {
        toastText = text;
        toastGroup = group;
    }

    void OnEnable()
    {
        // ★ 修復：訂閱全域 Toast 事件
        GameEvents.OnToast += ShowToast;
    }

    void OnDisable()
    {
        GameEvents.OnToast -= ShowToast;
    }

    public void InitPanels()
    {
        dic.Clear();
        if (panelNames == null || panels == null) return;
        for (int i = 0; i < panelNames.Length && i < panels.Length; i++)
            dic[panelNames[i]] = panels[i];
        CloseAll("Main");
    }

    public void Open(string name)
    {
        foreach (var kv in dic) kv.Value.SetActive(kv.Key == name);
    }

    public void CloseAll(string keep = null)
    {
        foreach (var kv in dic) kv.Value.SetActive(kv.Key == keep);
    }

    // ================= Toast 飄字邏輯 =================

    void ShowToast(string msg)
    {
        // 停止舊的動畫，開始新的
        StopAllCoroutines();
        StartCoroutine(ToastRoutine(msg));
    }

    IEnumerator ToastRoutine(string msg)
    {
        if (toastText == null || toastGroup == null) yield break;

        // 1. 顯示文字並淡入
        toastText.text = msg;
        toastGroup.alpha = 1f;

        // 2. 停留 2 秒讓玩家看清楚
        yield return new WaitForSeconds(2f);

        // 3. 花 0.5 秒淡出
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f; // 2f 代表 0.5秒完成 (1/0.5)
            toastGroup.alpha = 1f - t;
            yield return null;
        }
        toastGroup.alpha = 0f;
    }
}