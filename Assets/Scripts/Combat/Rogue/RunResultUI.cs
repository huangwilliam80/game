using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一場 Roguelite 結束後的結算畫面:
/// 顯示勝敗、列出戰利品、玩家點切換「保留 / 分解」、確認後回調 RunController。
/// </summary>
public class RunResultUI : MonoBehaviour
{
    public static RunResultUI I { get; private set; }

    [Header("引用(由 GameBootstrapper 指派)")]
    public CanvasGroup root;
    public TMP_Text title;
    public TMP_Text summaryText;
    public Transform lootList;             // VerticalLayoutGroup,放 loot rows
    public GameObject lootRowPrefab;       // 戰利品行模板
    public Button confirmBtn;

    readonly List<GameObject> rows = new List<GameObject>();
    readonly List<bool> keepFlags = new List<bool>();

    List<EquipmentInstance> loot;
    Action<List<EquipmentInstance>, long> onConfirm;

    void Awake() { I = this; }

    void OnEnable()
    {
        if (lootRowPrefab != null && rows.Count == 0)
        {
            for (int i = 0; i < 8; i++)
            {
                var r = Instantiate(lootRowPrefab, lootList);
                r.SetActive(false);
                rows.Add(r);
            }
        }
        Hide();
    }

    /// <summary>
    /// 開啟結算畫面。
    /// onConfirm 回調簽名: (保留的裝備列表, 分解可獲得的金幣總額)
    /// </summary>
    public void Show(bool win, List<EquipmentInstance> loot,
                     Action<List<EquipmentInstance>, long> onConfirm)
    {
        this.loot = loot ?? new List<EquipmentInstance>();
        this.onConfirm = onConfirm;
        root.alpha = 1f;
        root.blocksRaycasts = true;
        root.interactable = true;

        if (title != null)
        {
            title.text = win ? "✦ 勝利 ✦" : "✗ 敗北 ✗";
            title.color = win ? new Color(1f, 0.9f, 0.3f) : Color.gray;
        }

        keepFlags.Clear();
        for (int i = 0; i < this.loot.Count; i++) keepFlags.Add(true);
        RefreshAll();

        if (confirmBtn != null)
        {
            confirmBtn.onClick.RemoveAllListeners();
            confirmBtn.onClick.AddListener(Confirm);
        }
    }

    void RefreshAll()
    {
        long sellGold = 0;
        int kept = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < loot.Count;
            rows[i].SetActive(show);
            if (!show) continue;

            var eq = loot[i];
            var nameT = Find<TMP_Text>(rows[i].transform, "Name");
            var statT = Find<TMP_Text>(rows[i].transform, "Stat");
            var btn = rows[i].GetComponentInChildren<Button>();

            // 名稱（含品質色）＋ 保留/分解標記
            var st = eq != null ? eq.FinalStats() : new StatBlock();
            if (nameT != null)
            {
                string mark = keepFlags[i] ? "<color=#8CFF9E>✓ 保留</color>" : "<color=#BFBFBF>✗ 分解</color>";
                nameT.text = $"<color=#{ColorUtility.ToHtmlStringRGB(EquipDisplayUtil.QualityColor(eq.quality))}>[{EquipDisplayUtil.QualityName(eq.quality)}]</color> " +
                             $"{EquipDisplayUtil.Name(eq)} Lv.{eq.level}{(eq.plus > 0 ? "+" + eq.plus : "")}  {mark}";
            }
            // 數值摘要（让玩家 3 秒內判斷要不要留）
            if (statT != null)
            {
                statT.text = $"戰力 {eq.Power:N0}｜攻 {st.atk:N0} 防 {st.def:N0} 命 {st.hp:N0}" +
                             $"｜分解 +{TopBarUI.Num(eq.SellPrice)} 金";
            }
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int idx = i;
                btn.onClick.AddListener(() => { keepFlags[idx] = !keepFlags[idx]; RefreshAll(); });
            }

            if (keepFlags[i]) kept++;
            else sellGold += SellOf(eq);
        }
        if (summaryText != null)
            summaryText.text = $"保留 {kept} 件  |  分解換金幣 +{sellGold:N0}";
    }

    void Confirm()
    {
        var keep = new List<EquipmentInstance>();
        long sellGold = 0;
        for (int i = 0; i < loot.Count; i++)
        {
            if (keepFlags[i]) keep.Add(loot[i]);
            else sellGold += SellOf(loot[i]);
        }
        Hide();
        onConfirm?.Invoke(keep, sellGold);
    }

    public void Hide()
    {
        root.alpha = 0f;
        root.blocksRaycasts = false;
        root.interactable = false;
    }

    /// <summary>分解可換金幣（統一走 EquipmentInstance.SellPrice）。</summary>
    static long SellOf(EquipmentInstance eq) => eq != null ? eq.SellPrice : 0;

    static T Find<T>(Transform parent, string name) where T : Component
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<T>() : default;
    }
}
