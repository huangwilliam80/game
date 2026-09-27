using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 總管：面板開關、Toast 飘字、底部導航。
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

    readonly System.Collections.Generic.Dictionary<string, GameObject> dic = new Dictionary<string, GameObject>();

    void Awake()
    {
        I = this;
        for (int i = 0; i < panelNames.Length && i < panels.Length; i++)
            dic[panelNames[i]] = panels[i];
        CloseAll("Main");
    }

    void OnEnable() => GameEvents.OnToast += ShowToast;
    void OnDisable() => GameEvents.OnToast -= ShowToast;

    public void Open(string name)
    {
        foreach (var kv in dic) kv.Value.SetActive(kv.Key == name);
    }

    public void CloseAll(string keep = null)
    {
        foreach (var kv in dic) kv.Value.SetActive(kv.Key == keep);
    }

    Coroutine toastCo;
    void ShowToast(string msg)
    {
        if (!toastText) return;
        toastText.text = msg;
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastAnim());
    }

    IEnumerator ToastAnim()
    {
        float t = 0;
        while (t < 2.4f)
        {
            t += Time.unscaledDeltaTime;
            if (toastGroup) toastGroup.alpha = t < .2f ? t / .2f : t > 1.9f ? (2.4f - t) / .5f : 1f;
            yield return null;
        }
        if (toastGroup) toastGroup.alpha = 0;
    }
}
