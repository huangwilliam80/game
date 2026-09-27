using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;   // Unity 6 預設新輸入系統時才編譯這行
#endif
using UnityEngine.UI;

/// <summary>
/// 場景自動搭建器（新手友善核心！）：
/// 掛在空物體上按 Play，它會用程式碼生成整套豎屏 UI（Canvas、頂欄、五個面板、導航列、戰鬥舞臺），
/// 並自動把引用指派給各面板腳本 —— 你完全不需要手動拖拽。
/// 若想微調外觀，可在 Unity Editor 的 Hierarchy 中直接改生成的物件。
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
        Application.targetFrameRate = 60;          // 流暢幀率保證

        EquipmentDatabase.Init();
        CharacterSystem.Init();
        GameSave.Load();

        // 重構詞綴（存檔只存 seed → 還原 affixes）
        foreach (var e in GameSave.Data.bags) e.RegenerateAffixes();
        foreach (var e in GameSave.Data.equipped) if (e != null) e.RegenerateAffixes();

        var (sec, g, e2) = GameSave.SettleOffline();   // 先結算離線收益入帳

        BuildUI();

        if (sec > 60) Debug.Log($"離線 {IdleSystem.Fmt(sec)}，已入帳 金幣+{g:N0} 經驗+{e2:N0}");
    }

    void Update()
    {
        // 每 30 秒自動存檔（防閃退丟進度）
        autoSave += Time.deltaTime;
        if (autoSave > 30f) { autoSave = 0; GameSave.Save(); }
    }
    float autoSave;

    void OnApplicationPause(bool p) { if (p) GameSave.Save(); }
    void OnApplicationQuit() => GameSave.Save();

    // ==================================================================
    //  UI 生成區（全部程式碼，無 prefab 依賴）
    // ==================================================================
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
        scaler.referenceResolution = new Vector2(1080, 1920);      // 豎屏基準
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        rootRT = canvasGO.GetComponent<RectTransform>();

        EnsureEventSystem();

        // ---- 背景 ----
        var bg = CreateImage("BG", rootRT, new Color(.09f, .10f, .16f));
        Stretch(bg.rectTransform);

        // ---- 頂部資源欄 ----
        var topBarGO = CreatePanel("TopBar", rootRT, new Color(.14f, .15f, .22f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, 140f));
        var top = topBarGO.AddComponent<TopBarUI>();
        top.levelPower = CreateText("LvPow", topBarGO.transform, 34, Color.white, TextAnchor.MiddleLeft);
        SetRect(top.levelPower.rectTransform, new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(20, -70), Vector2.zero);
        top.gold = CreateText("Gold", topBarGO.transform, 30, new Color(1f, .85f, .3f), TextAnchor.MiddleRight);
        SetRect(top.gold.rectTransform, new Vector2(.5f, 1), new Vector2(.75f, 1), new Vector2(-10, -70), Vector2.zero);
        top.crystal = CreateText("Crystal", topBarGO.transform, 30, new Color(.5f, .8f, 1f), TextAnchor.MiddleRight);
        SetRect(top.crystal.rectTransform, new Vector2(.75f, 1), new Vector2(.9f, 1), new Vector2(-10, -70), Vector2.zero);
        top.shard = CreateText("Shard", topBarGO.transform, 30, new Color(.7f, .9f, .6f), TextAnchor.MiddleRight);
        SetRect(top.shard.rectTransform, new Vector2(.9f, 1), new Vector2(1, 1), new Vector2(-20, -70), Vector2.zero);
        top.income = CreateText("Income", topBarGO.transform, 26, new Color(.6f, 1f, .6f), TextAnchor.MiddleLeft);
        SetRect(top.income.rectTransform, new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(20, 0), Vector2.zero);
        top.stageLabel = CreateText("Stage", topBarGO.transform, 26, new Color(.9f, .9f, .9f), TextAnchor.MiddleRight);
        SetRect(top.stageLabel.rectTransform, new Vector2(.5f, 0), new Vector2(1, 1), new Vector2(-20, 0), Vector2.zero);

        // ---- 中央內容區（面板都放這裡）----
        var contentGO = CreatePanel("Content", rootRT, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, -170f));
        var content = contentGO.GetComponent<RectTransform>();

        // === 面板 1：Main（掛機首頁＋戰鬥舞臺）===
        var main = CreatePanel("P_Main", content, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(main.GetComponent<RectTransform>());
        var mp = main.AddComponent<MainPanelUI>();

        // 戰鬥舞臺區域
        var stageArea = CreatePanel("StageArea", main.transform, new Color(.12f, .13f, .2f, .9f),
            new Vector2(.05f, .38f), new Vector2(.95f, .95f), Vector2.zero, Vector2.zero);
        // 六張床
        var beds = new System.Collections.Generic.List<RectTransform>();
        for (int i = 0; i < 6; i++)
        {
            var bed = CreateImage($"Bed{i}", stageArea.transform, new Color(.45f, .3f, .2f));
            var rt = bed.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.13f + i * .148f, .12f);
            rt.sizeDelta = new Vector2(120, 70);
            beds.Add(rt);
            CreateTextIn(bed.transform, "床", 40, Color.white);
        }
        // 小鬼模板（池化用）
        var ghostT = CreateImage("GhostTpl", stageArea.transform, new Color(.6f, .85f, 1f, .9f));
        ghostT.rectTransform.sizeDelta = new Vector2(70, 80);
        ghostT.gameObject.AddComponent<CanvasGroup>();
        CreateTextIn(ghostT.transform, "鬼", 40, new Color(.1f, .1f, .2f));

        var dbv = main.AddComponent<DormBattleView>();
        dbv.stageArea = stageArea.GetComponent<RectTransform>();
        dbv.bedSlots = beds;
        dbv.ghostPrefab = ghostT.gameObject;
        ghostT.gameObject.SetActive(false);

        mp.goalLabel = CreateText("Goal", main.transform, 30, new Color(.95f, .95f, .8f), TextAnchor.MiddleCenter);
        SetRect(mp.goalLabel.rectTransform, new Vector2(.05f, .16f), new Vector2(.95f, .38f), Vector2.zero, Vector2.zero);
        mp.offlineBanner = CreateText("Offline", main.transform, 32, new Color(1f, .9f, .5f), TextAnchor.MiddleCenter);
        SetRect(mp.offlineBanner.rectTransform, new Vector2(.05f, .78f), new Vector2(.95f, .95f), Vector2.zero, Vector2.zero);

        mp.claimBtn = CreateButton("領取離線收益", main.transform, new Color(.2f, .5f, .3f));
        SetRect(mp.claimBtn.rectTransform, new Vector2(.06f, .04f), new Vector2(.48f, .14f), Vector2.zero, Vector2.zero);
        mp.stageBtn = CreateButton("關卡掃蕩", main.transform, new Color(.6f, .4f, .15f));
        SetRect(mp.stageBtn.rectTransform, new Vector2(.52f, .04f), new Vector2(.94f, .14f), Vector2.zero, Vector2.zero);
        mp.forgeBtn = CreateButton("鍛造", main.transform, new Color(.35f, .3f, .55f));
        SetRect(mp.forgeBtn.rectTransform, new Vector2(.52f, .15f), new Vector2(.94f, .24f), Vector2.zero, Vector2.zero);

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
        SetRect(ep.detailText.rectTransform, new Vector2(.03f, .08f), new Vector2(.62f, .29f), Vector2.zero, Vector2.zero);
        string[] ops = { "強化", "洗鍊", "升階", "穿戴", "分解", "一鍵清理" };
        Color[] oc = { new(.25f, .45f, .7f), new(.55f, .3f, .7f), new(.7f, .5f, .2f), new(.2f, .55f, .3f), new(.6f, .25f, .25f), new(.4f, .4f, .45f) };
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
        SetRect(cp.statText.rectTransform, new Vector2(.05f, .72f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
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
        SetRect(sp.resultText.rectTransform, new Vector2(.05f, .05f), new Vector2(.95f, .26f), Vector2.zero, Vector2.zero);

        // === 面板 5：Forge ===
        var forge = CreatePanel("P_Forge", content, new Color(.11f, .12f, .18f, .98f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(forge.GetComponent<RectTransform>());
        var fp = forge.AddComponent<ForgePanelUI>();
        fp.slotButtons = new Button[6];
        var fRow = CreateGrid("Slots", forge.transform, 3, new Vector2(.05f, .6f), new Vector2(.95f, .92f));
        string[] names = { "武器", "頭部", "衣服", "飾品", "法器", "鞋子" };
        for (int i = 0; i < 6; i++) fp.slotButtons[i] = CreateButton(names[i], fRow.transform, new Color(.25f, .28f, .4f));
        fp.infoText = CreateText("Info", forge.transform, 30, Color.white, TextAnchor.MiddleCenter);
        SetRect(fp.infoText.rectTransform, new Vector2(.05f, .35f), new Vector2(.95f, .58f), Vector2.zero, Vector2.zero);
        var doBtn = CreateButton("開始鍛造", forge.transform, new Color(.6f, .35f, .15f));
        SetRect(doBtn.rectTransform, new Vector2(.2f, .15f), new Vector2(.8f, .3f), Vector2.zero, Vector2.zero);
        doBtn.onClick.AddListener(fp.DoForge);

        // ---- Toast ----
        var toastGO = new GameObject("Toast"); toastGO.transform.SetParent(rootRT, false);
        toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        uiToast = CreateText("ToastTxt", toastGO.transform, 34, Color.white, TextAnchor.MiddleCenter);
        var tbg = toastGO.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, .7f);
        var trt = toastGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(.1f, .55f); trt.anchorMax = new Vector2(.9f, .65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        SetRect(uiToast.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ---- 底部導航 ----
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
            var rt = b.rectTransform;
            rt.anchorMin = new Vector2(i / 5f + .01f, .1f); rt.anchorMax = new Vector2((i + 1) / 5f - .01f, .9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            string k = keys[i];
            b.onClick.AddListener(() => um.Open(k));
        }

        // ---- 掛機系統 ----
        var idleGO = new GameObject("IdleSystem"); idleGO.transform.SetParent(transform, false);
        idleGO.AddComponent<IdleSystem>();

        top.Refresh();
    }

    CanvasGroup toastGroup; TMP_Text uiToast;

    // ================= 小工具 =================

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>()) return;
        var es = new GameObject("EventSystem");
        es.transform.SetParent(rootRT, false);
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && UNITY_6000_0_OR_NEWER
        // Unity 6：新輸入系統的 UI 模組（需要 com.unity.inputsystem 套件）
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<StandaloneInputModule>();   // 舊輸入系統退回方案
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
        img.rectTransform.sizeDelta = new Vector2(100, 100);
        return img;
    }

    TMP_Text CreateText(string name, Transform parent, int size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.color = col; t.alignment = anchor;
        t.text = name; t.overflowMode = TextOverflowModes.Ellipsis; t.enableWordWrapping = true;
        return t;
    }

    void CreateTextIn(Transform parent, string s, int size, Color col)
    {
        var t = CreateText("L", parent, size, col);
        t.text = s; Stretch(t.rectTransform);
    }

    Button CreateButton(string label, Transform parent, Color col)
    {
        var go = new GameObject("Btn_" + label); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        var b = go.AddComponent<Button>();
        var t = CreateText("T", go.transform, 28, Color.white);
        t.text = label; Stretch(t.rectTransform);
        go.AddComponent<LayoutElement>().minSize = new Vector2(150, 90);
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
        var vp = sv;                       // 本身當 viewport
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
