using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class UiExt
{
    public static RectTransform rt(this Component c) => (RectTransform)c.transform;
    // ★ 修復 CS1929：CreatePanel/CreateGrid 回傳的是 GameObject，也需要 .rt()
    public static RectTransform rt(this GameObject go) => (RectTransform)go.transform;
}

/// <summary>
/// 場景自動搭建器:用程式碼生成整套豎屏 UI(上半部世界視窗 + 下半部 Dock + 全局遮罩)。
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper I { get; private set; }
    Sprite whiteSq;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // ★ 統一由 ApplyFont() 指定中文字型；這裡只確保自動 sizing 不會干擾版面

        EquipmentDatabase.Init();
        CharacterSystem.Init();
        GameSave.Load();
        foreach (var e in GameSave.Data.bags) e.RegenerateAffixes();
        foreach (var e in GameSave.Data.equipped) if (e != null) e.RegenerateAffixes();

        var (sec, g, e2) = GameSave.SettleOffline();
        BuildUI();

        if (sec > 60) Debug.Log($"離線 {IdleSystem.Fmt(sec)}，已入帳 金幣+{g:N0} 經驗+{e2:N0}");
    }

    void Update()
    {
        autoSave += Time.deltaTime;
        if (autoSave > 30f) { autoSave = 0; GameSave.Save(); }
    }
    float autoSave;

    void OnApplicationPause(bool p) { if (p) GameSave.Save(); }
    void OnApplicationQuit() => GameSave.Save();

    Canvas canvas;
    RectTransform rootRT;

    void BuildUI()
    {
        whiteSq = MakeSquareSprite();

        // ---- Canvas + 豎屏適配 ----
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

        // ---- 背景（★ 修復：必須蓋住整屏，否則 GameView 出現大片白底、文字難以辨識）----
        var bg = CreatePanel("BG", rootRT, new Color(.09f, .10f, .16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.GetComponent<Image>().sprite = whiteSq;
        Stretch(bg.rt());

        // ---- 頂部資源欄 ----
        var topBarGO = CreatePanel("TopBar", rootRT, new Color(.14f, .15f, .22f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, 0));
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

        // ---- 中間容器(TopBar 與 NavBar 之間) ----
        var middle = CreatePanel("Middle", rootRT, new Color(0, 0, 0, 0),
            Vector2.zero, Vector2.one, new Vector2(0, 170f), new Vector2(0, -140f));
        // ★ 注意：這裡不能 Stretch()，否則會把上面 170/-140 的邊距清零，TopBar/NavBar 與面板重疊

        // ==== 上半部:WorldView(永遠顯示的戰鬥演出) ====
        var worldViewGO = CreatePanel("WorldView", middle.transform, new Color(0.05f, 0.08f, 0.12f),
            new Vector2(0, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(worldViewGO.rt());
        var wv = worldViewGO.AddComponent<WorldView>();
        wv.worldArea = worldViewGO.rt();

        // 主角
        var heroImg = CreateImage("Hero", worldViewGO.transform, new Color(0.3f, 0.7f, 1f));
        heroImg.rt().anchorMin = heroImg.rt().anchorMax = new Vector2(0.25f, 0.52f);
        heroImg.rt().sizeDelta = new Vector2(170, 230);
        wv.heroImage = heroImg;

        // 主角血條
        var heroBarBg = CreateImage("HeroHpBarBg", worldViewGO.transform, new Color(0.2f, 0.2f, 0.2f));
        heroBarBg.rt().anchorMin = heroBarBg.rt().anchorMax = new Vector2(0.25f, 0.16f);
        heroBarBg.rt().sizeDelta = new Vector2(180, 18);
        var heroFill = CreateImage("HeroHpFill", heroBarBg.transform, new Color(0.2f, 0.9f, 0.3f));
        var hf = heroFill.GetComponent<Image>();
        hf.type = Image.Type.Filled; hf.fillMethod = Image.FillMethod.Horizontal; hf.fillAmount = 1f;
        Stretch(heroFill.rt());
        wv.heroHpFill = hf;
        wv.heroHpText = CreateText("HeroHpTxt", heroBarBg.transform, 20, Color.white, TextAnchor.MiddleCenter);
        Stretch(wv.heroHpText.rt());

        // 敵人錨點 + 模板
        var enemyAnchor = CreatePanel("EnemyAnchor", worldViewGO.transform, new Color(0, 0, 0, 0),
            new Vector2(0.62f, 0.35f), new Vector2(0.9f, 0.8f), Vector2.zero, Vector2.zero);
        wv.enemyAnchor = enemyAnchor.rt();
        var enemyTpl = CreateImage("EnemyTpl", enemyAnchor.transform, new Color(0.9f, 0.3f, 0.3f));
        enemyTpl.rt().sizeDelta = new Vector2(160, 200);
        enemyTpl.gameObject.SetActive(false);
        wv.enemyPrefab = enemyTpl.gameObject;

        // 敵人血條 + 名稱
        var eBarBg = CreateImage("EnemyHpBarBg", worldViewGO.transform, new Color(0.2f, 0.2f, 0.2f));
        eBarBg.rt().anchorMin = eBarBg.rt().anchorMax = new Vector2(0.76f, 0.16f);
        eBarBg.rt().sizeDelta = new Vector2(180, 18);
        var eFill = CreateImage("EnemyHpFill", eBarBg.transform, new Color(0.9f, 0.3f, 0.3f));
        var ef = eFill.GetComponent<Image>();
        ef.type = Image.Type.Filled; ef.fillMethod = Image.FillMethod.Horizontal; ef.fillAmount = 1f;
        Stretch(eFill.rt());
        wv.enemyHpFill = ef;
        wv.enemyNameText = CreateText("EnemyName", worldViewGO.transform, 22, Color.white, TextAnchor.MiddleCenter);
        wv.enemyNameText.rt().anchorMin = wv.enemyNameText.rt().anchorMax = new Vector2(0.76f, 0.86f);
        wv.enemyNameText.rt().sizeDelta = new Vector2(200, 40);
        wv.enemyHpText = CreateText("EnemyHpTxt", eBarBg.transform, 20, Color.white, TextAnchor.MiddleCenter);
        Stretch(wv.enemyHpText.rt());

        // 傷害數字模板(池)
        var dmgTplGo = new GameObject("DmgTpl"); dmgTplGo.transform.SetParent(worldViewGO.transform, false);
        var dmgTxt = dmgTplGo.AddComponent<TextMeshProUGUI>();
        dmgTxt.fontSize = 36; dmgTxt.alignment = TextAlignmentOptions.Center;
        dmgTxt.text = "999"; dmgTxt.color = Color.white;
        dmgTplGo.AddComponent<CanvasGroup>();
        var dRT = dmgTplGo.AddComponent<RectTransform>(); dRT.sizeDelta = new Vector2(200, 60);
        dmgTplGo.SetActive(false);
        wv.dmgNumberPrefab = dmgTplGo;

        // 波次橫幅（第 X 波 / BOSS）
        var waveBanner = CreateText("WaveBanner", worldViewGO.transform, 30, new Color(.85f, .9f, 1f), TextAnchor.UpperCenter);
        SetRect(waveBanner.rt(), new Vector2(0, 0.9f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        wv.waveBanner = waveBanner;

        // 快速進入秘境的浮動按鈕（UX：一眼看到「現在能做什麼」）
        var quickRunBtn = CreateButton("⚔ 靈玉秘境", worldViewGO.transform, new Color(.55f, .28f, .65f));
        SetRect(quickRunBtn.rt(), new Vector2(.62f, .02f), new Vector2(.98f, .12f), Vector2.zero, Vector2.zero);
        quickRunBtn.onClick.AddListener(() => RunController.I?.StartRun(Mathf.Max(1, GameSave.Data.currentStage)));
        var quickLbl = quickRunBtn.GetComponentInChildren<TMP_Text>();
        if (quickLbl) quickLbl.fontSize = 26;

        // buff 標籤列
        var buffLbl = new GameObject("BuffLbl"); buffLbl.transform.SetParent(worldViewGO.transform, false);
        var bl = buffLbl.AddComponent<TextMeshProUGUI>();
        bl.fontSize = 22; bl.color = new Color(1f, 0.9f, 0.4f); bl.alignment = TextAlignmentOptions.BottomLeft;
        var blRT = buffLbl.AddComponent<RectTransform>();
        blRT.anchorMin = new Vector2(0.05f, 0.02f); blRT.anchorMax = new Vector2(0.95f, 0.08f);
        blRT.offsetMin = blRT.offsetMax = Vector2.zero;
        wv.buffLabel = buffLbl.transform;

        // ==== 下半部:Dock(面板切換區) ====
        var dock = CreatePanel("Dock", middle.transform, new Color(0.11f, 0.12f, 0.18f),
            Vector2.zero, new Vector2(1, 0.45f), Vector2.zero, Vector2.zero);
        Stretch(dock.rt());
        var content = dock.rt();

        // === 面板 1:Main ===
        var main = CreatePanel("P_Main", content, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(main.rt());
        var mp = main.AddComponent<MainPanelUI>();
        // 排版：上半=离线横幅 → 下一目标；下半=三顆行動按鈕（大按钮、拇指可及）
        mp.offlineBanner = CreateText("Offline", main.transform, 30, new Color(1f, .9f, .5f), TextAnchor.MiddleCenter);
        SetRect(mp.offlineBanner.rt(), new Vector2(.05f, .74f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        mp.goalLabel = CreateText("Goal", main.transform, 28, new Color(.95f, .95f, .8f), TextAnchor.UpperCenter);
        SetRect(mp.goalLabel.rt(), new Vector2(.05f, .46f), new Vector2(.95f, .74f), Vector2.zero, Vector2.zero);
        mp.dungeonBtn = CreateButton("⚔ 靈玉秘境（即時戰鬥）", main.transform, new Color(.55f, .28f, .65f));
        SetRect(mp.dungeonBtn.rt(), new Vector2(.06f, .30f), new Vector2(.94f, .43f), Vector2.zero, Vector2.zero);
        mp.claimBtn = CreateButton("領取離線收益", main.transform, new Color(.2f, .5f, .3f));
        SetRect(mp.claimBtn.rt(), new Vector2(.06f, .14f), new Vector2(.48f, .27f), Vector2.zero, Vector2.zero);
        mp.stageBtn = CreateButton("關卡掃蕩", main.transform, new Color(.6f, .4f, .15f));
        SetRect(mp.stageBtn.rt(), new Vector2(.52f, .14f), new Vector2(.94f, .27f), Vector2.zero, Vector2.zero);
        mp.forgeBtn = CreateButton("整理背包", main.transform, new Color(.35f, .3f, .55f));
        SetRect(mp.forgeBtn.rt(), new Vector2(.06f, .0f), new Vector2(.48f, .12f), Vector2.zero, Vector2.zero);
        mp.claimBtn.onClick.AddListener(() => IdleSystem.I?.CollectOfflineNow());
        mp.stageBtn.onClick.AddListener(() => UIManager.I?.Open("Stages"));
        mp.forgeBtn.onClick.AddListener(() => UIManager.I?.Open("Equip"));

        // === 面板 2:Equip ===
        var equip = CreatePanel("P_Equip", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(equip.rt());
        var ep = equip.AddComponent<EquipmentPanelUI>();
        var slotRow = CreateGrid("SlotRow", equip.transform, 3, new Vector2(.03f, .72f), new Vector2(.97f, .97f));
        ep.equipRow = slotRow.transform;
        for (int i = 0; i < 6; i++)
        {
            var b = CreateButton($"槽位{i}", slotRow.transform, new Color(.2f, .22f, .3f));
            int si = i;
            b.onClick.AddListener(() => InventorySystem.SmartEquipBest((EquipSlot)si));
        }
        var bagGrid = CreateGrid("BagGrid", equip.transform, 4, new Vector2(.03f, .30f), new Vector2(.97f, .70f));
        ep.bagGrid = bagGrid.transform;
        var cellTpl = CreateButton("CellTpl", bagGrid.transform, Color.white);
        cellTpl.name = "cellPrefab";
        ep.cellPrefab = cellTpl.gameObject;
        cellTpl.gameObject.SetActive(false);
        ep.detailText = CreateText("Detail", equip.transform, 26, Color.white, TextAnchor.UpperLeft);
        SetRect(ep.detailText.rt(), new Vector2(.03f, .08f), new Vector2(.62f, .29f), Vector2.zero, Vector2.zero);
        string[] ops = { "強化", "洗鍊", "升階", "穿戴", "分解", "一鍵清理" };
        Color[] oc = { new Color(.25f, .45f, .7f), new Color(.55f, .3f, .7f), new Color(.7f, .5f, .2f), new Color(.2f, .55f, .3f), new Color(.6f, .25f, .25f), new Color(.4f, .4f, .45f) };
        var actRow = CreateGrid("Ops", equip.transform, 3, new Vector2(.63f, .06f), new Vector2(.98f, .29f));
        for (int i = 0; i < 6; i++)
        {
            var btn = CreateButton(ops[i], actRow.transform, oc[i]);
            switch (i)
            {
                case 0: ep.btnEnhance = btn; break;
                case 1: ep.btnReforge = btn; break;
                case 2: ep.btnUpgradeQ = btn; break;
                case 3: ep.btnEquipSel = btn; break;
                case 4: ep.btnDecompose = btn; break;
                case 5: ep.btnCleanup = btn; break;
            }
        }

        // === 面板 3:Character ===
        var cha = CreatePanel("P_Char", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(cha.rt());
        var cp = cha.AddComponent<CharacterPanelUI>();
        cp.statText = CreateText("Stats", cha.transform, 30, Color.white, TextAnchor.UpperCenter);
        SetRect(cp.statText.rt(), new Vector2(.05f, .72f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        var gfGrid = CreateGrid("Gongfa", cha.transform, 2, new Vector2(.03f, .06f), new Vector2(.97f, .70f));
        cp.gongfaRow = gfGrid.transform;
        cp.upgradeButtons = new Button[8];
        for (int i = 0; i < 8; i++) cp.upgradeButtons[i] = CreateButton("升級", gfGrid.transform, new Color(.3f, .35f, .5f));

        // === 面板 4:Stages ===
        var stg = CreatePanel("P_Stages", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(stg.rt());
        var sp = stg.AddComponent<StagePanelUI>();
        CreateScrollList("StageList", stg.transform, out var listContent,
            new Vector2(.03f, .34f), new Vector2(.97f, .97f));
        sp.listParent = listContent;
        var itemTpl = CreateButton("第 X 夜", listContent, Color.gray);
        sp.itemPrefab = itemTpl.gameObject;
        itemTpl.gameObject.SetActive(false);
        sp.resultText = CreateText("Result", stg.transform, 26, Color.white, TextAnchor.UpperLeft);
        SetRect(sp.resultText.rt(), new Vector2(.05f, .04f), new Vector2(.55f, .26f), Vector2.zero, Vector2.zero);

        // ★ 秘境（Roguelite）入口：文案直接告訴玩家獎勵與特色
        sp.runHint = CreateText("RunHint", stg.transform, 22, new Color(.9f, .88f, .75f), TextAnchor.UpperLeft);
        SetRect(sp.runHint.rt(), new Vector2(.57f, .04f), new Vector2(.98f, .26f), Vector2.zero, Vector2.zero);
        var runB = CreateButton("進入靈玉秘境（即時戰鬥）", stg.transform, new Color(.62f, .3f, .7f));
        SetRect(runB.rt(), new Vector2(.57f, .27f), new Vector2(.98f, .33f), Vector2.zero, Vector2.zero);
        var runT = runB.GetComponentInChildren<TMP_Text>();
        if (runT) runT.fontSize = 22;
        sp.runBtn = runB;

        // === 面板 5:Forge ===
        var forge = CreatePanel("P_Forge", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(forge.rt());
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

        // ---- UIManager 綁定 Dock 內的面板 ----
        var um = dock.AddComponent<UIManager>();
        um.panelNames = new[] { "Main", "Equip", "Character", "Stages", "Forge" };
        um.panels = new[] { main, equip, cha, stg, forge };
        um.InitPanels();

        // ==== 全局遮罩 1:ChoiceOverlay(三選一) ====
        var choiceGo = CreatePanel("ChoiceOverlay", rootRT, new Color(0, 0, 0, 0.6f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var choiceCG = choiceGo.AddComponent<CanvasGroup>();
        choiceCG.alpha = 0; choiceCG.blocksRaycasts = false;
        Stretch(choiceGo.rt());

        var cardRowGo = CreatePanel("CardRow", choiceGo.transform, new Color(0, 0, 0, 0),
            new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.65f), Vector2.zero, Vector2.zero);
        var hlg = cardRowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 20;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        var co = choiceGo.AddComponent<ChoiceOverlayUI>();
        co.overlayGroup = choiceCG;
        co.cardRow = cardRowGo.transform;

        var cardTpl = new GameObject("CardTpl"); cardTpl.transform.SetParent(cardRowGo.transform, false);
        var cImg = cardTpl.AddComponent<Image>(); cImg.color = new Color(0.3f, 0.3f, 0.4f, 0.95f);
        cardTpl.AddComponent<Button>();
        cardTpl.AddComponent<LayoutElement>().preferredWidth = 260;
        var cn = CreateText("Name", cardTpl.transform, 28, Color.white, TextAnchor.UpperCenter);
        SetRect(cn.rt(), Vector2.zero, Vector2.one, new Vector2(10, -60), new Vector2(-10, -10));
        var cr = CreateText("Rarity", cardTpl.transform, 20, new Color(1f, 0.9f, 0.3f), TextAnchor.UpperRight);
        SetRect(cr.rt(), Vector2.zero, Vector2.one, new Vector2(-20, -40), new Vector2(0, 0));
        var cd = CreateText("Desc", cardTpl.transform, 22, Color.white, TextAnchor.LowerCenter);
        SetRect(cd.rt(), Vector2.zero, Vector2.one, new Vector2(10, 20), new Vector2(-10, 120));
        cardTpl.SetActive(false);
        co.cardPrefab = cardTpl;

        // ==== 全局遮罩 2:RunResult(結算) ====
        var resultGo = CreatePanel("RunResult", rootRT, new Color(0, 0, 0, 0.75f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var resCG = resultGo.AddComponent<CanvasGroup>();
        resCG.alpha = 0; resCG.blocksRaycasts = false;
        Stretch(resultGo.rt());

        var rr = resultGo.AddComponent<RunResultUI>();
        rr.root = resCG;
        rr.title = CreateText("Title", resultGo.transform, 48, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter);
        SetRect(rr.title.rt(), new Vector2(0, 0.78f), new Vector2(1, 0.92f), Vector2.zero, Vector2.zero);
        rr.summaryText = CreateText("Summary", resultGo.transform, 28, Color.white, TextAnchor.MiddleCenter);
        SetRect(rr.summaryText.rt(), new Vector2(0, 0.05f), new Vector2(1, 0.15f), Vector2.zero, Vector2.zero);

        var lootGo = CreatePanel("LootList", resultGo.transform, new Color(0.1f, 0.1f, 0.15f, 0.9f),
            new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.75f), Vector2.zero, Vector2.zero);
        var vlg = lootGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter; vlg.spacing = 10;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        rr.lootList = lootGo.transform;

        var rowTpl = new GameObject("RowTpl"); rowTpl.transform.SetParent(lootGo.transform, false);
        rowTpl.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.9f);
        rowTpl.AddComponent<Button>();
        rowTpl.AddComponent<LayoutElement>().preferredHeight = 80;
        var rn = CreateText("Name", rowTpl.transform, 26, Color.white, TextAnchor.MiddleLeft);
        SetRect(rn.rt(), Vector2.zero, new Vector2(1, 0.5f), new Vector2(20, 0), Vector2.zero);
        var rs = CreateText("Stat", rowTpl.transform, 22, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft);
        SetRect(rs.rt(), Vector2.zero, new Vector2(1, 0.5f), new Vector2(20, -40), Vector2.zero);
        rowTpl.SetActive(false);
        rr.lootRowPrefab = rowTpl;

        var confBtn = CreateButton("確認並結算", resultGo.transform, new Color(0.2f, 0.6f, 0.3f));
        SetRect(confBtn.rt(), new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.13f), Vector2.zero, Vector2.zero);
        rr.confirmBtn = confBtn;

        // ---- Toast（掛在根層，任何面板都能看見）----
        var toastGO = new GameObject("Toast"); toastGO.transform.SetParent(rootRT, false);
        var toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        var uiToast = CreateText("ToastTxt", toastGO.transform, 34, Color.white, TextAnchor.MiddleCenter);
        var tbg = toastGO.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, .7f);
        var trt = toastGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(.1f, .55f); trt.anchorMax = new Vector2(.9f, .65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        SetRect(uiToast.rt(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        um.BindToast(uiToast, toastGroup);   // ★ Toast 交給 UIManager 統一播放（含淡出）

        // ---- 底部導航 ----
        var nav = CreatePanel("NavBar", rootRT, new Color(.13f, .14f, .2f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 170f));
        ((RectTransform)nav.transform).sizeDelta = new Vector2(0, 170);

        string[] tabs = { "掛機", "装备", "角色", "關卡", "鍛造" };
        string[] keys = { "Main", "Equip", "Character", "Stages", "Forge" };
        for (int i = 0; i < 5; i++)
        {
            var b = CreateButton(tabs[i], nav.transform, new Color(.2f, .22f, .3f));
            var rt = b.rt();
            rt.anchorMin = new Vector2(i / 5f + .01f, .1f); rt.anchorMax = new Vector2((i + 1) / 5f - .01f, .9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            string k = keys[i];
            b.onClick.AddListener(() => um.Open(k));
        }

        // ---- 掛機系統 ----
        var idleGO = new GameObject("IdleSystem"); idleGO.transform.SetParent(transform, false);
        idleGO.AddComponent<IdleSystem>();

        // ---- Roguelite Run 控制器 ★ 本次新增 ----
        var runGO = new GameObject("RunController"); runGO.transform.SetParent(transform, false);
        runGO.AddComponent<RunController>();

        ApplyFont();
        top.Refresh();
    }

    /// <summary>把所有文字套上專案內的中文字型（找不到就沿用 TMP 預設，不會報錯）。</summary>
    void ApplyFont()
    {
        var cjk = CJKFont;
        if (cjk == null) return;                    // 已在外層印過警告
        TMPro.TMP_Settings.defaultFontAsset = cjk;  // ★ 之後新建立的 TMP 也自動用中文字型
        var all = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in all)
        {
            t.font = cjk;
            t.enableAutoSizing = false;
            if (!t.enabled) t.enabled = true;      // 只還原原本就該顯示的文字，不誤開隱藏模板
        }
        // 動態生成的傷害數字預製体也要換字型（它在 pool 內）
        if (WorldView.I != null) WorldView.I.ApplyFontToPool(cjk);
    }

    /// <summary>
    /// 中文字型搜尋順序（任何一步失敗都自動退下一步，絕不會拋例外）：
    /// 1) Resources/Fonts/SourceHanSansCN-Regular SDF（自己生成的 TMP 字型资产，效果最好）
    /// 2) Assets/Fonts/*.otf → 用 TMP 內建 CreateFontAsset 在執行期產生「動態 SDF 字型」
    ///    （免開 Font Asset Creator；編輯器與真機都能跑，因為是走 Runtime API）
    /// 3) TMP 內建 LiberationSans SDF（至少英文/數字正常，並在 Console 提示）
    /// </summary>
    static TMP_FontAsset CJKFont
    {
        get
        {
            if (cjkFont != null || cjkTried) return cjkFont;
            cjkTried = true;

            // 1) 先驗「資產名稱 == 檔案名稱」，不符就跳過（避免 Unity 的命名衝突警告與方框字）
            var named = SafeLoad<TMP_FontAsset>("Fonts/SourceHanSansCN-Regular SDF");
            if (IsValid(named) && named.name == "SourceHanSansCN-Regular SDF")
            {
                cjkFont = named;
                Debug.Log("[CJK] 使用已生成的 TMP 字型：Resources/Fonts/SourceHanSansCN-Regular SDF");
                return cjkFont;
            }
            cjkFont = null;

            // ★ 免手動生成步驟：直接把專案內的 .otf/.ttf 轉成「動態 SDF 字型资产」
            foreach (var name in new[] { "SourceHanSansCN-Regular", "SourceHanSansCN-Normal",
                                          "SourceHanSansCN-Medium", "SourceHanSansCN-Bold",
                                          "SourceHanSansCN-Light", "SourceHanSansCN-Heavy" })
            {
                var src = FindFontByName(name);
                if (src == null) continue;
                try
                {
                    var f = TMP_FontAsset.CreateFontAsset(src, 90, 9,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                        AtlasPopulationMode.Dynamic, true);
                    if (IsValid(f))
                    {
                        cjkFont = f;
                        Debug.Log($"[CJK] 已用 {name}.otf 產生動態字型（無需 Font Asset Creator）。");
                        return cjkFont;
                    }
                }
                catch (Exception e) { Debug.LogWarning("[CJK] " + name + " 失敗：" + e.Message); }
            }

            // 最後：TMP 內建西文字型，至少畫面不會壞
            cjkFont = SafeLoad<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (!IsValid(cjkFont)) cjkFont = null;
            if (cjkFont == null)
                Debug.LogWarning("[GameBootstrapper] 找不到可用的中文字型。\n" +
                    "請用 Window→TextMeshPro→Font Asset Creator，來源選 Assets/Fonts/SourceHanSansCN-Regular.otf，" +
                    "存檔到 Assets/Resources/Fonts/SourceHanSansCN-Regular SDF.asset（資產名稱需與檔名一致）。");
            return cjkFont;
        }
    }
    static TMP_FontAsset cjkFont;
    static bool cjkTried;

    /// <summary>
    /// 從 Resources/Fonts/ 抓原始 .otf/.ttf（這些檔已由工具複製到 Resources 底下）。
    /// 刻意不使用 UnityEditor.AssetDatabase —— 它不存在於真機/打包版本。
    /// </summary>
    static Font FindFontByName(string name) => SafeLoad<Font>("Fonts/" + name);

    /// <summary>Resources.Load 但過濾掉「損壞/名稱不符」的资产（Unity 會回傳非 null 但已丟棄的物件）。</summary>
    static T SafeLoad<T>(string path) where T : UnityEngine.Object
    {
        try
        {
            var o = Resources.Load<T>(path);
            return IsValid(o) ? o : null;
        }
        catch (Exception e) { Debug.LogWarning("[SafeLoad] " + path + " → " + e.Message); return null; }
    }

    static bool IsValid(UnityEngine.Object o)
    {
        if (o == null) return false;                 // 已被刪除
        try { _ = o.name; return !string.IsNullOrEmpty(o.name); } // 名稱與檔案不符時 Unity 會在此報錯
        catch { return false; }
    }

    // ================= 小工具 =================
    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.transform.SetParent(rootRT, false);
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && UNITY_6000_0_OR_NEWER
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
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

    TextMeshProUGUI CreateText(string name, Transform parent, int size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = col; t.alignment = (TextAlignmentOptions)anchor;
        t.text = name; t.overflowMode = TextOverflowModes.Ellipsis;
#if UNITY_2022_3_OR_NEWER || UNITY_6000_0_OR_NEWER
        t.textWrappingMode = TextWrappingModes.Normal;
#else
        t.enableWordWrapping = true;
#endif
        return t;
    }

    Button CreateButton(string label, Transform parent, Color col)
    {
        var go = new GameObject("Btn_" + label); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        var b = go.AddComponent<Button>();
        var t = CreateText("T", go.transform, 28, Color.white);
        t.text = label; Stretch(t.rt());
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 150; le.minHeight = 90;
        return b;
    }

    GameObject CreateGrid(string name, Transform parent, int cols, Vector2 aMin, Vector2 aMax)
    {
        var go = CreatePanel(name, parent, new Color(0, 0, 0, 0), aMin, aMax, Vector2.zero, Vector2.zero);
        var g = go.AddComponent<GridLayoutGroup>();
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = cols;
        g.cellSize = new Vector2(200, 110);
        g.spacing = new Vector2(10, 10);
        g.childAlignment = TextAnchor.UpperLeft;
        return go;
    }

    GameObject CreateScrollList(string name, Transform parent, out RectTransform contentRT, Vector2 aMin, Vector2 aMax)
    {
        var sv = CreatePanel(name, parent, new Color(.08f, .08f, .12f, .6f), aMin, aMax, Vector2.zero, Vector2.zero);
        var scroll = sv.AddComponent<ScrollRect>();
        // ★ 修復：viewport 必須是「獨立且帶遮罩」的節點，否則整塊背景會被裁掉、列表也無法捲動
        var vpGO = CreatePanel("Viewport", sv.transform, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        vpGO.AddComponent<RectMask2D>();
        var c = new GameObject("Content"); c.transform.SetParent(vpGO.transform, false);
        contentRT = c.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(.5f, 1); contentRT.sizeDelta = new Vector2(0, 120);
        var vlg = c.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8; vlg.childForceExpandWidth = true; vlg.childControlHeight = false;
        c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRT; scroll.viewport = (RectTransform)vpGO.transform;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40;
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