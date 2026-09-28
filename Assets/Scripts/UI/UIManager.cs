using System;
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

    [Header("Toast")]
    public TMP_Text toastText;
    public CanvasGroup toastGroup;

    readonly Dictionary<string, GameObject> dic = new Dictionary<string, GameObject>();

    void Awake()
    {
        I = this;
        // ★★★ 修復：AddComponent 會「立刻」觸發 Awake，
        // 此時 GameBootstrapper 還沒賦值 panelNames / panels（是 null），
        // 直接讀取會崩潰並中斷整個 UI 生成（底部導航列因此消失）。
        // 改成：有值才初始化；沒值則等 GameBootstrapper 賦值後手動呼叫 InitPanels()。★★★
        if (panelNames == null || panels == null) return;
        InitPanels();
    }

    /// <summary>建立面板字典並預設顯示 Main（由 GameBootstrapper 在賦值後呼叫）。</summary>
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
}