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
    public static RectTransform rt(this Component c) => c.GetComponent<RectTransform>();
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

        // ---- 背景 ----
        var bg = CreateImage("BG", rootRT, new Color(.09f, .10f, .16f));
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
        Stretch(middle.rt());

        // ==== 上半部:WorldView(永遠顯示的戰鬥演出) ====
        var worldViewGO = CreatePanel("WorldView", middle.transform, new Color(0.05f, 0.08f, 0.12f),
            new Vector2(0, 0.4f), Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(worldViewGO.rt());
        var wv = worldViewGO.AddComponent<WorldView>();
        wv.worldArea = worldViewGO.rt();

        // 主角
        var heroImg = CreateImage("Hero", worldViewGO.transform, new Color(0.3f, 0.7f, 1f));
        heroImg.rt().anchorMin = heroImg.rt().anchorMax = new Vector2(0.25f, 0.5f);
        heroImg.rt().sizeDelta = new Vector2(180, 240);
        wv.heroImage = heroImg;

        // 主角血條
        var heroBarBg = CreateImage("HeroHpBarBg", worldViewGO.transform, new Color(0.2f, 0.2f, 0.2f));
        heroBarBg.rt().anchorMin = heroBarBg.rt().anchorMax = new Vector2(0.25f, 0.15f);
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
            new Vector2(0.65f, 0.4f), new Vector2(0.85f, 0.8f), Vector2.zero, Vector2.zero);
        wv.enemyAnchor = enemyAnchor.rt();
        var enemyTpl = CreateImage("EnemyTpl", enemyAnchor.transform, new Color(0.9f, 0.3f, 0.3f));
        enemyTpl.rt().sizeDelta = new Vector2(160, 200);
        enemyTpl.gameObject.SetActive(false);
        wv.enemyPrefab = enemyTpl.gameObject;

        // 敵人血條 + 名稱
        var eBarBg = CreateImage("EnemyHpBarBg", worldViewGO.transform, new Color(0.2f, 0.2f, 0.2f));
        eBarBg.rt().anchorMin = eBarBg.rt().anchorMax = new Vector2(0.75f, 0.15f);
        eBarBg.rt().sizeDelta = new Vector2(180, 18);
        var eFill = CreateImage("EnemyHpFill", eBarBg.transform, new Color(0.9f, 0.3f, 0.3f));
        var ef = eFill.GetComponent<Image>();
        ef.type = Image.Type.Filled; ef.fillMethod = Image.FillMethod.Horizontal; ef.fillAmount = 1f;
        Stretch(eFill.rt());
        wv.enemyHpFill = ef;
        wv.enemyNameText = CreateText("EnemyName", worldViewGO.transform, 22, Color.white, TextAnchor.MiddleCenter);
        wv.enemyNameText.rt().anchorMin = wv.enemyNameText.rt().anchorMax = new Vector2(0.75f, 0.85f);
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

        // buff 標籤列
        var buffLbl = new GameObject("BuffLbl"); buffLbl.transform.SetParent(worldViewGO.transform, false);
        var bl = buffLbl.AddComponent<TextMeshProUGUI>();
        bl.fontSize = 22; bl.color = new Color(1f, 0.9f, 0.4f); bl.alignment = TextAlignmentOptions.LowerLeft;
        var blRT = buffLbl.AddComponent<RectTransform>();
        blRT.anchorMin = new Vector2(0.05f, 0.02f); blRT.anchorMax = new Vector2(0.95f, 0.08f);
        blRT.offsetMin = blRT.offsetMax = Vector2.zero;
        wv.buffLabel = buffLbl.transform;

        // ==== 下半部:Dock(面板切換區) ====
        var dock = CreatePanel("Dock", middle.transform, new Color(0.11f, 0.12f, 0.18f),
            Vector2.zero, new Vector2(1, 0.4f), Vector2.zero, Vector2.zero);
        Stretch(dock.rt());
        var content = dock.rt();

        // === 面板 1:Main ===
        var main = CreatePanel("P_Main", content, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(main.rt());
        var mp = main.AddComponent<MainPanelUI>();
        mp.goalLabel = CreateText("Goal", main.transform, 30, new Color(.95f, .95f, .8f), TextAnchor.MiddleCenter);
        SetRect(mp.goalLabel.rt(), new Vector2(.05f, .16f), new Vector2(.95f, .38f), Vector2.zero, Vector2.zero);
        mp.offlineBanner = CreateText("Offline", main.transform, 32, new Color(1f, .9f, .5f), TextAnchor.MiddleCenter);
        SetRect(mp.offlineBanner.rt(), new Vector2(.05f, .78f), new Vector2(.95f, .95f), Vector2.zero, Vector2.zero);
        mp.claimBtn = CreateButton("領取離線收益", main.transform, new Color(.2f, .5f, .3f));
        SetRect(mp.claimBtn.rt(), new Vector2(.06f, .04f), new Vector2(.48f, .14f), Vector2.zero, Vector2.zero);
        mp.stageBtn = CreateButton("關卡掃蕩", main.transform, new Color(.6f, .4f, .15f));
        SetRect(mp.stageBtn.rt(), new Vector2(.52f, .04f), new Vector2(.94f, .14f), Vector2.zero, Vector2.zero);
        mp.forgeBtn = CreateButton("鍛造", main.transform, new Color(.35f, .3f, .55f));
        SetRect(mp.forgeBtn.rt(), new Vector2(.52f, .15f), new Vector2(.94f, .24f), Vector2.zero, Vector2.zero);
        mp.claimBtn.onClick.AddListener(() => IdleSystem.I?.CollectOfflineNow());
        mp.stageBtn.onClick.AddListener(() => UIManager.I?.Open("Stages"));
        mp.forgeBtn.onClick.AddListener(() => UIManager.I?.Open("Forge"));

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
            new Vector2(.03f, .28f), new Vector2(.97f, .97f));
        sp.listParent = listContent;
        var itemTpl = CreateButton("第 X 夜", listContent, Color.gray);
        sp.itemPrefab = itemTpl.gameObject;
        itemTpl.gameObject.SetActive(false);
        sp.resultText = CreateText("Result", stg.transform, 28, Color.white, TextAnchor.UpperCenter);
        SetRect(sp.resultText.rt(), new Vector2(.05f, .05f), new Vector2(.95f, .26f), Vector2.zero, Vector2.zero);

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

        // ---- Toast ----
        var toastGO = new GameObject("Toast"); toastGO.transform.SetParent(rootRT, false);
        var toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        var uiToast = CreateText("ToastTxt", toastGO.transform, 34, Color.white, TextAnchor.MiddleCenter);
        var tbg = toastGO.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, .7f);
        var trt = toastGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(.1f, .55f); trt.anchorMax = new Vector2(.9f, .65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        SetRect(uiToast.rt(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameEvents.OnToast += msg =>
        {
            if (uiToast) uiToast.text = msg;
            if (toastGroup) StartCoroutine(FadeToast(toastGroup));
        };

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

        top.Refresh();
    }

    System.Collections.IEnumerator FadeToast(CanvasGroup cg)
    {
        yield return new WaitForSeconds(1.5f);
        float t = 1f;
        while (t > 0)
        {
            t -= Time.deltaTime;
            cg.alpha = t;
            yield return null;
        }
        cg.alpha = 0;
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
        var vp = sv;
        var c = new GameObject("Content"); c.transform.SetParent(sv.transform, false);
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