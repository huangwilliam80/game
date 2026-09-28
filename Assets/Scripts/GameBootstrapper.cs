using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper I { get; private set; }
    Sprite whiteSq;
    
    // ★★★ 新增：全域字體變數 ★★★
    TMP_FontAsset gameFont; 

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // ★★★ 新增：從 Resources 資料夾載入思源黑體 ★★★
        // 假設你的 Font Asset 名稱為 "SourceHanSansCN SDF"
        gameFont = Resources.Load<TMP_FontAsset>("Fonts/SourceHanSansCN SDF"); 
        if (gameFont != null && TMP_Settings.instance != null)
        {
            TMP_Settings.defaultFontAsset = gameFont;
        }

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

        var topBarGO = CreatePanel("TopBar", rootRT, new Color(.14f, .15f, .22f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, 140f));
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

        var contentGO = CreatePanel("Content", rootRT, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, -170f));
        var content = contentGO.GetComponent<RectTransform>();

        // === 面板 1：Main ===
        var main = CreatePanel("P_Main", content, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(main.GetComponent<RectTransform>());
        var mp = main.AddComponent<MainPanelUI>();

        var stageArea = CreatePanel("StageArea", main.transform, new Color(.12f, .13f, .2f, .9f),
            new Vector2(.05f, .38f), new Vector2(.95f, .95f), Vector2.zero, Vector2.zero);
            
        var beds = new List<RectTransform>();
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

        // === 面板 2：Equip ===
        var equip = CreatePanel("P_Equip", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(equip.GetComponent<RectTransform>());
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

        // === 面板 3：Character ===
        var cha = CreatePanel("P_Char", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(cha.GetComponent<RectTransform>());
        var cp = cha.AddComponent<CharacterPanelUI>();
        cp.statText = CreateText("Stats", cha.transform, 30, Color.white, TextAnchor.UpperCenter);
        SetRect(cp.statText.rt(), new Vector2(.05f, .72f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        var gfGrid = CreateGrid("Gongfa", cha.transform, 2, new Vector2(.03f, .06f), new Vector2(.97f, .70f));
        cp.gongfaRow = gfGrid.transform;
        cp.upgradeButtons = new Button[8];
        for (int i = 0; i < 8; i++) cp.upgradeButtons[i] = CreateButton("升級", gfGrid.transform, new Color(.3f, .35f, .5f));

        // === 面板 4：Stages ===
        var stg = CreatePanel("P_Stages", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(stg.GetComponent<RectTransform>());
        var sp = stg.AddComponent<StagePanelUI>();
        CreateScrollList("StageList", stg.transform, out var listContent,
            new Vector2(.03f, .28f), new Vector2(.97f, .97f));
        sp.listParent = listContent;
        var itemTpl = CreateButton("第 X 夜", listContent, Color.gray);
        sp.itemPrefab = itemTpl.gameObject;
        itemTpl.gameObject.SetActive(false);
        sp.resultText = CreateText("Result", stg.transform, 28, Color.white, TextAnchor.UpperCenter);
        SetRect(sp.resultText.rt(), new Vector2(.05f, .05f), new Vector2(.95f, .26f), Vector2.zero, Vector2.zero);

        // === 面板 5：Forge ===
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

        // ---- Toast & NavBar ----
        var toastGO = new GameObject("Toast"); toastGO.transform.SetParent(rootRT, false);
        toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        uiToast = CreateText("ToastTxt", toastGO.transform, 34, Color.white, TextAnchor.MiddleCenter);
        var tbg = toastGO.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, .7f);
        var trt = toastGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(.1f, .55f); trt.anchorMax = new Vector2(.9f, .65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        SetRect(uiToast.rt(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var nav = CreatePanel("NavBar", rootRT, new Color(.13f, .14f, .2f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 170f), new Vector2(0, 0));
        ((RectTransform)nav.transform).sizeDelta = new Vector2(0, 170);
        var um = gameObject.AddComponent<UIManager>();
        um.panelNames = new[] { "Main", "Equip", "Character", "Stages", "Forge" };
        um.panels = new[] { main, equip, cha, stg, forge };
        um.toastText = uiToast; um.toastGroup = toastGroup;

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

        // ===== ★ 新增：秘境控制器 + 上半部視圖接線 ★ =====
        var rcGO = new GameObject("RunController"); rcGO.transform.SetParent(transform, false);
        rcGO.AddComponent<RunController>();

        var rv = main.AddComponent<RunViewUI>();
        rv.heroFill  = CreateBar("HeroBar",  stageArea, new Color(.3f,.8f,.4f), new Vector2(.08f,.06f), new Vector2(.48f,.13f));
        rv.enemyFill = CreateBar("EnemyBar", stageArea, new Color(.85f,.3f,.3f), new Vector2(.52f,.06f), new Vector2(.92f,.13f));
        rv.heroLabel  = CreateText("HeroHp",  stageArea.transform, 26, Color.white, TextAnchor.MiddleLeft);
        SetRect(rv.heroLabel.rt(),  new Vector2(.08f,.13f), new Vector2(.48f,.21f), Vector2.zero, Vector2.zero);
        rv.enemyLabel = CreateText("EnemyHp", stageArea.transform, 26, Color.white, TextAnchor.MiddleRight);
        SetRect(rv.enemyLabel.rt(), new Vector2(.52f,.13f), new Vector2(.92f,.21f), Vector2.zero, Vector2.zero);
        rv.waveLabel  = CreateText("Wave",    stageArea.transform, 30, new Color(1f,.9f,.5f), TextAnchor.MiddleCenter);
        SetRect(rv.waveLabel.rt(),  new Vector2(.35f,.9f),  new Vector2(.65f,.99f), Vector2.zero, Vector2.zero);
        
        var froot = new GameObject("FloatRoot", typeof(RectTransform));
        froot.transform.SetParent(stageArea, false); Stretch(froot.rt());
        rv.floatRoot = froot.rt();

        rv.choicePanel = CreatePanel("Choice", stageArea, new Color(0,0,0,.78f), new Vector2(.05f,.25f), new Vector2(.95f,.75f), Vector2.zero, Vector2.zero);
        var cGrid = CreateGrid("ChoiceGrid", rv.choicePanel.transform, 3, Vector2.zero, Vector2.one);
        rv.choiceButtons = new Button[3];
        rv.choiceTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            rv.choiceButtons[i] = CreateButton("", cGrid.transform, new Color(.25f,.3f,.55f));
            rv.choiceTexts[i] = rv.choiceButtons[i].GetComponentInChildren<TMP_Text>();
            if(rv.choiceTexts[i] != null) rv.choiceTexts[i].fontSize = 30;
        }
        
        rv.resultPanel = CreatePanel("RunResult", stageArea, new Color(0,0,0,.85f), new Vector2(.1f,.3f), new Vector2(.9f,.72f), Vector2.zero, Vector2.zero);
        rv.resultText = CreateText("RunResultTxt", rv.resultPanel.transform, 32, Color.white, TextAnchor.MiddleCenter);
        SetRect(rv.resultText.rt(), new Vector2(0,.3f), Vector2.one, Vector2.zero, Vector2.zero);
        rv.resultCloseBtn = CreateButton("領取獎勵", rv.resultPanel.transform, new Color(.2f,.5f,.3f));
        SetRect(rv.resultCloseBtn.rt(), new Vector2(.25f,.08f), new Vector2(.75f,.26f), Vector2.zero, Vector2.zero);

        mp.dungeonBtn = CreateButton("靈玉秘境", main.transform, new Color(.55f,.2f,.4f));
        SetRect(mp.dungeonBtn.rt(), new Vector2(.06f,.15f), new Vector2(.48f,.24f), Vector2.zero, Vector2.zero);
        
        sp.runBtn = CreateButton("進入秘境", stg.transform, new Color(.55f,.2f,.4f));
        SetRect(sp.runBtn.rt(), new Vector2(.5f,.27f), new Vector2(.97f,.34f), Vector2.zero, Vector2.zero);
        sp.runHint = CreateText("RunHint", stg.transform, 24, new Color(.8f,.8f,.9f), TextAnchor.UpperLeft);
        SetRect(sp.runHint.rt(), new Vector2(.03f,.27f), new Vector2(.5f,.34f), Vector2.zero, Vector2.zero);
        // ================================================

        var idleGO = new GameObject("IdleSystem"); idleGO.transform.SetParent(transform, false);
        idleGO.AddComponent<IdleSystem>();
        top.Refresh();
    }

    // ★ 新增：創建血條 Helper ★
    Image CreateBar(string name, RectTransform parent, Color col, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = col; 
        img.type = Image.Type.Filled; 
        img.fillMethod = Image.FillMethod.Horizontal;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    // ★★★ 重要：請確保你原本的 CreateText 方法有加上這行 ★★★
    // if (gameFont != null) tmp.font = gameFont;
    
    // ... (保留你原本的 CreateText, CreateButton, CreatePanel, SetRect 等 Helper 方法) ...
}