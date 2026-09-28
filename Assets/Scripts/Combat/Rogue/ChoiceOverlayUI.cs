using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roguelite 的「三選一 buff」遮罩。
/// 顯示時攔截所有 Raycast,玩家必須選一張才會繼續下一波。
/// </summary>
public class ChoiceOverlayUI : MonoBehaviour
{
    public static ChoiceOverlayUI I { get; private set; }

    [Header("引用(由 GameBootstrapper 指派)")]
    public CanvasGroup overlayGroup;       // 整塊半透明遮罩的根
    public Transform cardRow;              // 放 3 張卡的容器(HorizontalLayoutGroup)
    public GameObject cardPrefab;          // 卡牌模板(預先生成 3 張,隱藏)

    readonly List<GameObject> cards = new List<GameObject>();
    Action<RogueBuffDef> onPick;

    void Awake() { I = this; }

    void OnEnable()
    {
        if (cardPrefab != null && cards.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                var c = Instantiate(cardPrefab, cardRow);
                c.SetActive(false);
                cards.Add(c);
            }
        }
        Hide();
    }

    public void Show(List<RogueBuffDef> options, Action<RogueBuffDef> onPick)
    {
        this.onPick = onPick;
        overlayGroup.alpha = 1f;
        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;

        for (int i = 0; i < cards.Count; i++)
        {
            bool show = i < options.Count;
            cards[i].SetActive(show);
            if (!show) continue;

            var b = options[i];
            var nameT = Find<TMP_Text>(cards[i].transform, "Name");
            var descT = Find<TMP_Text>(cards[i].transform, "Desc");
            var rarT  = Find<TMP_Text>(cards[i].transform, "Rarity");
            var img   = cards[i].GetComponent<Image>();
            if (nameT) nameT.text = b.name;
            if (descT) descT.text = b.desc ?? "";
            if (rarT)  rarT.text  = RarityLabel(b.rarity);
            if (img)   img.color  = RarityBg(b.rarity);

            var btn = cards[i].GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                var pick = b;
                btn.onClick.AddListener(() => Pick(pick));
            }
        }
    }

    void Pick(RogueBuffDef b)
    {
        Hide();
        onPick?.Invoke(b);
    }

    public void Hide()
    {
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;
    }

    static T Find<T>(Transform parent, string name) where T : Component
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<T>() : default;
    }

    static string RarityLabel(int r)
    {
        switch (r)
        {
            case 1: return "普通";
            case 2: return "稀有";
            case 3: return "傳說";
            default: return "?";
        }
    }

    static Color RarityBg(int r)
    {
        switch (r)
        {
            case 1: return new Color(0.35f, 0.38f, 0.45f, 0.95f);
            case 2: return new Color(0.25f, 0.55f, 0.85f, 0.95f);
            case 3: return new Color(0.85f, 0.55f, 0.15f, 0.95f);
            default: return new Color(0.4f, 0.4f, 0.4f, 0.95f);
        }
    }
}