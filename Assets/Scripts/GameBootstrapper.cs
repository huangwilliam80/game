using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public static class UiExt
{
    public static RectTransform rt(this Component c) => c.GetComponent<RectTransform>();
}

public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper I { get; private set; }
    Sprite whiteSq;
    float autoSave;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        RogueBuffPool.Init();
        TryLoadChineseFont();
        EquipmentDatabase.Init();
        CharacterSystem.Init();
        GameSave.Load();

        foreach (var e in GameSave.Data.bags) e.RegenerateAffixes();
        foreach (var e in GameSave.Data.equipped) if (e != null) e.RegenerateAffixes();

        var (sec, g, e2) = GameSave.SettleOffline();
        BuildUI();
        
        var runGO = new GameObject("RunController"); 
        runGO.transform.SetParent(transform, false);
        runGO.AddComponent<RunController>();

        if (sec > 60) Debug.Log($"離線 {IdleSystem.Fmt(sec)}，已入帳 金幣+{g:N0} 經驗+{e2:N0}");
    }

    void Update()
    {
        autoSave += Time.deltaTime;
        if (autoSave > 30f) { autoSave = 0; GameSave.Save(); }
    }

    void OnApplicationPause(bool p) { if (p) GameSave.Save(); }
    void OnApplicationQuit() => GameSave.Save();

    Canvas canvas;
    RectTransform rootRT;
    TMPro.TMP_FontAsset defaultFont;

    void BuildUI()
    {
        whiteSq = MakeSquareSprite();
        var canvasGO = new GameObject("Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        rootRT = canvasGO.GetComponent<RectTransform>();
        EnsureEventSystem();

        var bg = CreateImage("BG", rootRT, new Color(.09f, .10f, .16f));
        Stretch(bg.rt());

        // ==========================================
        // 1. 頂部資源欄 (修正：錨點在螢幕最上方，不再切半)
        // ==========================================
        var topBarGO = CreatePanel("TopBar", rootRT, new Color(.14f, .15f, .22f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140f), Vector2.zero);
        var top = topBarGO.AddComponent<TopBarUI>();
        top.levelPower = CreateText("LvPow", topBarGO.transform, 34, Color.white, TextAnchor.MiddleLeft);
        SetRect(top.levelPower.rt(), new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(20, -70), Vector2.zero);
        top.gold = CreateText("Gold", topBarGO.transform, 30, new Color(1f, .85f, .3f), TextAnchor.MiddleRight);
        SetRect(top.gold.rt(), new Vector2(.5f, 1), new Vector2(.75f, 1), new Vector2(-10, -70), Vector2.zero);
        top.crystal = CreateText("Crystal", topBarGO.transform, 30, new Color(.5f, .8f, 1f), TextAnchor.MiddleRight);
        SetRect(top.crystal.rt(), new Vector2(.75f, 1), new Vector2(.9f, 1), new Vector2(-10, -70), Vector2.zero);
        top.shard = CreateText("Shard", topBarGO.transform, 30, new Color(.7f, .9f, .6f), TextAnchor.MiddleRight);
        SetRect(top.shard.rt(), new Vector2(.9f, 1), new Vector2(1, 1), new Vector2(-20, -70), Vector2.zero);
        top.income = CreateText("Income", topBarGO.transform, 26, new Color(.6f, 1f, .6f), TextAnchor.MiddleLeft);
        SetRect(top.income.rt(), new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(20, 0), Vector2.zero);
        top.stageLabel = CreateText("Stage", topBarGO.transform, 26, new Color(.9f, .9f, .9f), TextAnchor.MiddleRight);
        SetRect(top.stageLabel.rt(), new Vector2(.5f, 0), new Vector2(1, 1), new Vector2(-20, 0), Vector2.zero);

        // ==========================================
        // 2. 底部導航列 (固定在螢幕最下方)
        // ==========================================
        var nav = CreatePanel("NavBar", rootRT, new Color(.13f, .14f, .2f),
            new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 170f));
        
        // ==========================================
        // 3. 內容區域 (自動填滿 TopBar 與 NavBar 之間)
        // ==========================================
        var contentGO = CreatePanel("Content", rootRT, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 170f), new Vector2(0, -140f));
        var content = contentGO.GetComponent<RectTransform>();

        // ==========================================
        // ★ 4. 主面板：上戰鬥 / 中按鈕 / 下裝備 (完美分割)
        // ==========================================
        var main = CreatePanel("P_Main", content, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(main.GetComponent<RectTransform>());
        var mp = main.AddComponent<MainPanelUI>();
        var ep = main.AddComponent<EquipmentPanelUI>(); 

        // -- [上半部] Roguelite 戰鬥區 (Y: 0.45 ~ 0.88) --
        var stageArea = CreatePanel("StageArea", main.transform, new Color(.12f, .13f, .2f, .9f),
            new Vector2(.03f, .45f), new Vector2(.97f, .88f), Vector2.zero, Vector2.zero);
        var beds = new System.Collections.Generic.List<RectTransform>();
        for (int i = 0; i < 6; i++)
        {
            var bed = CreateImage($"Bed{i}", stageArea.transform, new Color(.45f, .3f, .2f));
            var rt = bed.rt();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.13f + i * .148f, .12f);
            rt.sizeDelta = new Vector2(120, 70);
            beds.Add(rt);
            CreateTextIn(bed.transform, "床", 40, Color.white);
        }
        var ghostT = CreateImage("GhostTpl", stageArea.transform, new Color(.6f, .85f, 1f, .9f));
        ghostT.rt().sizeDelta = new Vector2(70, 80);
        ghostT.gameObject.AddComponent<CanvasGroup>();
        CreateTextIn(ghostT.transform, "鬼", 40, new Color(.1f, .1f, .2f));

        var dbv = main.AddComponent<DormBattleView>();
        dbv.stageArea = stageArea.GetComponent<RectTransform>();
        dbv.bedSlots = beds;
        dbv.ghostPrefab = ghostT.gameObject;
        ghostT.gameObject.SetActive(false);

        BuildRunView(main.transform); // 生成血條與波次

        // 離線收益提示條 (移到戰鬥區上方邊緣)
        mp.offlineBanner = CreateText("Offline", main.transform, 28, new Color(1f, .9f, .5f), TextAnchor.MiddleCenter);
        SetRect(mp.offlineBanner.rt(), new Vector2(.05f, .88f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);

        // -- [中半部] 目標與行動按鈕 (Y: 0.26 ~ 0.44) --
        mp.goalLabel = CreateText("Goal", main.transform, 26, new Color(.95f, .95f, .8f), TextAnchor.MiddleCenter);
        SetRect(mp.goalLabel.rt(), new Vector2(.03f, .36f), new Vector2(.97f, .44f), Vector2.zero, Vector2.zero);

        mp.dungeonBtn = CreateButton("🔥 秘境", main.transform, new Color(.6f, .2f, .5f));
        SetRect(mp.dungeonBtn.rt(), new Vector2(.03f, .26f), new Vector2(.24f, .35f), Vector2.zero, Vector2.zero);

        mp.stageBtn = CreateButton("關卡", main.transform, new Color(.6f, .4f, .15f));
        SetRect(mp.stageBtn.rt(), new Vector2(.26f, .26f), new Vector2(.48f, .35f), Vector2.zero, Vector2.zero);

        mp.claimBtn = CreateButton("領離線", main.transform, new Color(.2f, .5f, .3f));
        SetRect(mp.claimBtn.rt(), new Vector2(.50f, .26f), new Vector2(.72f, .35f), Vector2.zero, Vector2.zero);

        mp.forgeBtn = CreateButton("鍛造", main.transform, new Color(.4f, .3f, .15f)); // 補上缺失的鍛造按鈕
        SetRect(mp.forgeBtn.rt(), new Vector2(.74f, .26f), new Vector2(.97f, .35f), Vector2.zero, Vector2.zero);


        // -- [下半部] 裝備資訊區 (Y: 0.00 ~ 0.25) --
        var slotRow = CreateGrid("SlotRow", main.transform, 6, new Vector2(.03f, .19f), new Vector2(.97f, .25f));
        ep.equipRow = slotRow.transform;
        for (int i = 0; i < 6; i++)
        {
            var b = CreateButton($"槽位{i}", slotRow.transform, new Color(.2f, .22f, .3f));
            int si = i;
            b.onClick.AddListener(() => InventorySystem.SmartEquipBest((EquipSlot)si));
        }

        // 背包格子調小尺寸，並限制在左半邊
        var bagGrid = CreateGrid("BagGrid", main.transform, 4, new Vector2(.03f, .04f), new Vector2(.65f, .18f), new Vector2(140, 80));
        bagGrid.gameObject.AddComponent<RectMask2D>(); // 防止格子溢出
        ep.bagGrid = bagGrid.transform;
        var cellTpl = CreateButton("CellTpl", bagGrid.transform, Color.white);
        cellTpl.name = "cellPrefab";
        ep.cellPrefab = cellTpl.gameObject;
        cellTpl.gameObject.SetActive(false);

        ep.detailText = CreateText("Detail", main.transform, 22, Color.white, TextAnchor.UpperLeft);
        SetRect(ep.detailText.rt(), new Vector2(.03f, .00f), new Vector2(.65f, .04f), Vector2.zero, Vector2.zero);

        string[] ops = { "強化", "洗鍊", "穿戴", "分解", "清理" };
        Color[] oc = { new(.25f, .45f, .7f), new(.55f, .3f, .7f), new(.2f, .55f, .3f), new(.6f, .25f, .25f), new(.4f, .4f, .45f) };
        var actRow = CreateGrid("Ops", main.transform, 2, new Vector2(.66f, .02f), new Vector2(.97f, .18f), new Vector2(140, 70));
        for (int i = 0; i < 5; i++)
        {
            var btn = CreateButton(ops[i], actRow.transform, oc[i]);
            switch (i)
            {
                case 0: ep.btnEnhance = btn; break;
                case 1: ep.btnReforge = btn; break;
                case 2: ep.btnEquipSel = btn; break;
                case 3: ep.btnDecompose = btn; break;
                case 4: ep.btnCleanup = btn; break;
            }
        }

        // ==========================================
        // 5. 其他 Tab 面板 (角色/關卡/鍛造)
        // ==========================================
        var cha = CreatePanel("P_Char", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(cha.GetComponent<RectTransform>());
        var cp = cha.AddComponent<CharacterPanelUI>();
        cp.statText = CreateText("Stats", cha.transform, 30, Color.white, TextAnchor.UpperCenter);
        SetRect(cp.statText.rt(), new Vector2(.05f, .72f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        var gfGrid = CreateGrid("Gongfa", cha.transform, 2, new Vector2(.03f, .06f), new Vector2(.97f, .70f));
        cp.gongfaRow = gfGrid.transform;
        cp.upgradeButtons = new Button[8];
        for (int i = 0; i < 8; i++) cp.upgradeButtons[i] = CreateButton("升級", gfGrid.transform, new Color(.3f, .35f, .5f));

        var stg = CreatePanel("P_Stages", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(stg.GetComponent<RectTransform>());
        var sp = stg.AddComponent<StagePanelUI>();
        CreateScrollList("StageList", stg.transform, out var listContent, new Vector2(.03f, .28f), new Vector2(.97f, .97f));
        sp.listParent = listContent;
        var itemTpl = CreateButton("第 X 夜", listContent, Color.gray);
        sp.itemPrefab = itemTpl.gameObject;
        itemTpl.gameObject.SetActive(false);
        sp.resultText = CreateText("Result", stg.transform, 28, Color.white, TextAnchor.UpperCenter);
        SetRect(sp.resultText.rt(), new Vector2(.05f, .05f), new Vector2(.95f, .26f), Vector2.zero, Vector2.zero);
        sp.runBtn = CreateButton("進入秘境挑戰", stg.transform, new Color(.6f, .2f, .5f));
        SetRect(sp.runBtn.rt(), new Vector2(.1f, .1f), new Vector2(.9f, .22f), Vector2.zero, Vector2.zero);

        var forge = CreatePanel("P_Forge", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(forge.GetComponent<RectTransform>());
        var fp = forge.AddComponent<ForgePanelUI>();
        fp.slotButtons = new Button[6];
        var fRow = CreateGrid("Slots", forge.transform, 3, new Vector2(.05f, .6f), new Vector2(.95f, .92f));
        string[] names = { "武器", "頭部", "衣服", "飾品", "法器", "鞋子" };
        for (int i = 0; i < 6; i++) fp.slotButtons[i] = CreateButton(names[i], fRow.transform, new Color(.25f, .28f, .4f));
        fp.infoText = CreateText("Info", forge.transform, 30, Color.white, TextAnchor.MiddleCenter);
        SetRect(fp.infoText.rt(), new Vector2(.05f, .35f), new Vector2(.95f, .58f), Vector2.zero, Vector2.zero);
        var doBtn = CreateButton("開始鍛造", forge.transform, new Color(.6f, .35f, .15f));
        SetRect(doBtn.rt(), new Vector2(.2f, .15f), new Vector2(.8f, .3f), Vector2.zero, Vector2.zero);
        doBtn.onClick.AddListener(fp.DoForge);

        // Toast
        var toastGO = new GameObject("Toast"); toastGO.transform.SetParent(rootRT, false);
        CanvasGroup toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        TMP_Text uiToast = CreateText("ToastTxt", toastGO.transform, 34, Color.white, TextAnchor.MiddleCenter);
        var tbg = toastGO.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, .7f);
        var trt = toastGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(.1f, .55f); trt.anchorMax = new Vector2(.9f, .65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        SetRect(uiToast.rt(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // NavBar 按鈕生成
        var um = gameObject.AddComponent<UIManager>();
        um.panelNames = new[] { "Main", "Character", "Stages", "Forge" };
        um.panels = new[] { main, cha, stg, forge };
        um.toastText = uiToast; um.toastGroup = toastGroup;

        string[] tabs = { "主頁/裝備", "角色", "關卡", "鍛造" };
        string[] keys = { "Main", "Character", "Stages", "Forge" };
        for (int i = 0; i < 4; i++)
        {
            var b = CreateButton(tabs[i], nav.transform, new Color(.2f, .22f, .3f));
            var rtb = b.rt();
            rtb.anchorMin = new Vector2(i / 4f + .01f, .1f); rtb.anchorMax = new Vector2((i + 1) / 4f - .01f, .9f);
            rtb.offsetMin = rtb.offsetMax = Vector2.zero;
            string k = keys[i];
            b.onClick.AddListener(() => um.Open(k));
        }

        var idleGO = new GameObject("IdleSystem"); idleGO.transform.SetParent(transform, false);
        idleGO.AddComponent<IdleSystem>();
        top.Refresh();
    }

    void TryLoadChineseFont()
    {
        try
        {
            defaultFont = Resources.Load<TMPro.TMP_FontAsset>("Fonts/SourceHanSansCN-Bold SDF");
            if (defaultFont == null)
                defaultFont = Resources.Load<TMPro.TMP_FontAsset>("SourceHanSansCN-Bold SDF");
        }
        catch { defaultFont = null; }
    }

    void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.transform.SetParent(rootRT, false);
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && UNITY_6000_0_OR_NEWER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    RunViewUI runView;
    void BuildRunView(Transform mainT)
    {
        // 戰鬥視覺容器 (對齊上方的 StageArea)
        var vroot = CreatePanel("RunView", mainT, new Color(0, 0, 0, 0),
            new Vector2(.03f, .45f), new Vector2(.97f, .88f), Vector2.zero, Vector2.zero);

        var view = vroot.AddComponent<RunViewUI>();

        view.heroFill = CreateBar("HeroBar", vroot.transform, new Color(.3f, .8f, .4f),
            new Vector2(.05f, .80f), new Vector2(.45f, .90f));
        view.enemyFill = CreateBar("EnemyBar", vroot.transform, new Color(.85f, .3f, .35f),
            new Vector2(.55f, .80f), new Vector2(.95f, .90f));
        view.heroLabel = CreateText("HeroTxt", vroot.transform, 22, Color.white);
        SetRect(view.heroLabel.rt(), new Vector2(.05f, .70f), new Vector2(.45f, .80f), Vector2.zero, Vector2.zero);
        view.enemyLabel = CreateText("EnemyTxt", vroot.transform, 22, Color.white);
        SetRect(view.enemyLabel.rt(), new Vector2(.55f, .70f), new Vector2(.95f, .80f), Vector2.zero, Vector2.zero);
        view.waveLabel = CreateText("WaveTxt", vroot.transform, 26, new Color(1f, .9f, .5f));
        SetRect(view.waveLabel.rt(), new Vector2(.3f, .55f), new Vector2(.7f, .70f), Vector2.zero, Vector2.zero);
        view.buffLabel = CreateText("BuffTxt", vroot.transform, 20, new Color(.7f, .85f, 1f));
        SetRect(view.buffLabel.rt(), new Vector2(.05f, .45f), new Vector2(.95f, .55f), Vector2.zero, Vector2.zero);

        var froots = CreatePanel("FloatRoot", vroot.transform, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, .45f), Vector2.zero, Vector2.zero);
        view.floatRoot = froots.GetComponent<RectTransform>();

        var choice = CreatePanel("ChoicePanel", mainT, new Color(0, 0, 0, .75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(choice.GetComponent<RectTransform>());
        var title = CreateText("Title", choice.transform, 40, new Color(1f, .9f, .5f));
        SetRect(title.rt(), new Vector2(.1f, .68f), new Vector2(.9f, .78f), Vector2.zero, Vector2.zero);
        title.text = "⚔ 選擇一張增益卡";
        view.choicePanel = choice;
        for (int i = 0; i < 3; i++)
        {
            float x0 = .06f + i * .30f;
            var b = CreateButton("卡" + (i + 1), choice.transform, new Color(.25f, .3f, .5f));
            var rt = b.rt();
            rt.anchorMin = new Vector2(x0, .3f); rt.anchorMax = new Vector2(x0 + .28f, .64f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            view.choiceButtons[i] = b;
            var t = b.GetComponentInChildren<TMP_Text>();
            t.fontSize = 26; t.text = "";
            view.choiceTexts[i] = t;
        }

        var result = CreatePanel("ResultPanel", mainT, new Color(0, 0, 0, .8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(result.GetComponent<RectTransform>());
        view.resultPanel = result;
        view.resultText = CreateText("ResultTxt", result.transform, 36, Color.white);
        SetRect(view.resultText.rt(), new Vector2(.1f, .45f), new Vector2(.9f, .8f), Vector2.zero, Vector2.zero);
        var close = CreateButton("返回", result.transform, new Color(.3f, .45f, .3f));
        SetRect(close.rt(), new Vector2(.3f, .25f), new Vector2(.7f, .37f), Vector2.zero, Vector2.zero);
        view.resultCloseBtn = close;

        runView = view;

        var wvGO = new GameObject("WorldView"); wvGO.transform.SetParent(vroot.transform, false);
        var wv = wvGO.AddComponent<WorldView>();
        wv.worldArea = froots.GetComponent<RectTransform>();
        wv.heroHpFill = view.heroFill;
        wv.enemyHpFill = view.enemyFill;
        wv.heroHpText = view.heroLabel;
        wv.enemyNameText = view.enemyLabel;
        wv.waveBanner = view.waveLabel;
        wv.buffLabel = view.buffLabel != null ? view.buffLabel.gameObject.transform : null;
        var dmgTpl = new GameObject("DmgTpl", typeof(RectTransform));
        dmgTpl.transform.SetParent(froots.transform, false);
        var dt = dmgTpl.AddComponent<TextMeshProUGUI>();
        dt.fontSize = 36; dt.alignment = TextAlignmentOptions.Center;
        dmgTpl.AddComponent<CanvasGroup>();
        dmgTpl.SetActive(false);
        wv.dmgNumberPrefab = dmgTpl;
        wv.ApplyFontToPool(defaultFont);
    }

    Image CreateBar(string name, Transform parent, Color fillCol, Vector2 aMin, Vector2 aMax)
    {
        var back = CreatePanel(name + "Bg", parent, new Color(.15f, .15f, .2f), aMin, aMax, Vector2.zero, Vector2.zero);
        var fg = new GameObject(name); fg.transform.SetParent(back.transform, false);
        var img = fg.AddComponent<Image>(); img.sprite = whiteSq; img.color = fillCol;
        img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillAmount = 1f;
        Stretch(fg.GetComponent<RectTransform>());
        return img;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax; }

    GameObject CreatePanel(string name, Transform parent, Color col, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        return go;
    }

    Image CreateImage(string name, Transform parent, Color col)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        img.rt().sizeDelta = new Vector2(100, 100);
        return img;
    }

    TMP_Text CreateText(string name, Transform parent, int size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (defaultFont != null) t.font = defaultFont;
        t.fontSize = size; t.color = col; t.alignment = AnchorToTMP(anchor);
        t.text = name; t.overflowMode = TextOverflowModes.Ellipsis;
#if UNITY_2022_3_OR_NEWER || UNITY_6000_0_OR_NEWER
        t.textWrappingMode = TextWrappingModes.Normal;
#else
        t.enableWordWrapping = true;
#endif
        return t;
    }

    static TMPro.TextAlignmentOptions AnchorToTMP(TextAnchor a)
    {
        switch (a)
        {
            case TextAnchor.UpperLeft:    return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:  return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:   return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:   return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:  return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:    return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:  return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:   return TextAlignmentOptions.BottomRight;
            default:                      return TextAlignmentOptions.Center;
        }
    }

    void CreateTextIn(Transform parent, string s, int size, Color col)
    {
        var t = CreateText("L", parent, size, col);
        t.text = s; Stretch(t.rt());
    }

    Button CreateButton(string label, Transform parent, Color col)
    {
        var go = new GameObject("Btn_" + label); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        var b = go.AddComponent<Button>();
        var t = CreateText("T", go.transform, 28, Color.white);
        t.text = label; Stretch(t.rt());
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 100; le.minHeight = 60; // 稍微縮小 minWidth 讓按鈕能擠進一排
        return b;
    }

    // ★ 新增了 cellSize 參數，方便動態調整網格大小
    GameObject CreateGrid(string name, Transform parent, int cols, Vector2 aMin, Vector2 aMax, Vector2? cellSize = null)
    {
        var go = CreatePanel(name, parent, new Color(0, 0, 0, 0), aMin, aMax, Vector2.zero, Vector2.zero);
        var g = go.AddComponent<GridLayoutGroup>();
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = cols;
        g.cellSize = cellSize ?? new Vector2(180, 100);
        g.spacing = new Vector2(10, 10);
        g.childAlignment = TextAnchor.UpperLeft;
        return go;
    }

    GameObject CreateScrollList(string name, Transform parent, out RectTransform contentRT, Vector2 aMin, Vector2 aMax)
    {
        var sv = CreatePanel(name, parent, new Color(.08f, .08f, .12f, .6f), aMin, aMax, Vector2.zero, Vector2.zero);
        var scroll = sv.AddComponent<ScrollRect>();
        var vp = sv;
        var c = new GameObject("Content", typeof(RectTransform)); c.transform.SetParent(sv.transform, false);
        contentRT = c.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(.5f, 1); contentRT.sizeDelta = new Vector2(0, 120);
        var vlg = c.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.childForceExpandWidth = true; vlg.childControlHeight = false;
        c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRT; scroll.viewport = vp.GetComponent<RectTransform>();
        scroll.horizontal = false;
        return sv;
    }

    Sprite MakeSquareSprite()
    {
        var t = new Texture2D(4, 4);
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
    }
}