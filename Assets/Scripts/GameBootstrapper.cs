using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// ★ 修復：添加擴展方法，讓 GameObject / Component 都能用 .rt() 獲取 RectTransform
public static class UIExt
{
    public static RectTransform rt(this GameObject go) => go.GetComponent<RectTransform>();
    public static RectTransform rt(this Component c) => c.GetComponent<RectTransform>();
}

public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper I { get; private set; }

    Sprite whiteSq;
    float autoSave;
    Text uiToast;
    CanvasGroup toastGroup;

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

        if (sec > 60)
            Debug.Log($"離線 {IdleSystem.Fmt(sec)}，已入帳 金幣+{g:N0} 經驗+{e2:N0}");
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

    void BuildUI()
    {
        whiteSq = MakeSquareSprite();

        // ---- Canvas ----
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

        // ---- Toast ----
        var toastGO = CreateImage("Toast", rootRT, new Color(0, 0, 0, .75f));
        var toastRT = toastGO.rt();
        Stretch(toastRT);
        toastRT.sizeDelta = new Vector2(0, 80);
        toastRT.anchorMin = new Vector2(0, .45f);
        toastRT.anchorMax = new Vector2(1, .45f);
        toastRT.anchoredPosition = Vector2.zero;
        toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        uiToast = CreateText("ToastTxt", toastGO.transform, 30, Color.white, TextAnchor.MiddleCenter);
        Stretch(uiToast.rectTransform);

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

        // ---- 中央內容區 ----
        var contentGO = CreatePanel("Content", rootRT, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, -170f));
        var content = contentGO.GetComponent<RectTransform>();

        // ═══════ 面板 1：Main ═══════
        var main = CreatePanel("P_Main", content, new Color(0, 0, 0, 0),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(main.rt());
        main.SetActive(false);

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
        ghostT.AddComponent<CanvasGroup>();
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
        SetRect(mp.claimBtn.rt(), new Vector2(.06f, .04f), new Vector2(.48f, .14f), Vector2.zero, Vector2.zero);
        mp.stageBtn = CreateButton("關卡掃蕩", main.transform, new Color(.6f, .4f, .15f));
        SetRect(mp.stageBtn.rt(), new Vector2(.52f, .04f), new Vector2(.94f, .14f), Vector2.zero, Vector2.zero);
        mp.forgeBtn = CreateButton("鍛造", main.transform, new Color(.35f, .3f, .55f));
        SetRect(mp.forgeBtn.rt(), new Vector2(.52f, .15f), new Vector2(.94f, .24f), Vector2.zero, Vector2.zero);

        main.SetActive(true);

        // ═══════ 面板 2：Equip ═══════
        var equip = CreatePanel("P_Equip", content, new Color(.11f, .12f, .18f, .98f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(equip.rt());
        equip.SetActive(false);

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
        Color[] oc = {
            new Color(.25f,.45f,.7f), new Color(.55f,.3f,.7f), new Color(.7f,.5f,.2f),
            new Color(.2f,.55f,.3f),  new Color(.6f,.25f,.25f), new Color(.4f,.4f,.45f)
        };
        var actRow = CreateGrid("Ops", equip.transform, 3, new Vector2(.63f, .06f), new Vector2(.98f, .29f));
        for (int i = 0; i < 6; i++)
        {
            var btn = CreateButton(ops[i], actRow.transform, oc[i]);
            switch (i)
            {
                case 0: ep.btnEnhance = btn; btn.onClick.AddListener(ep.OnEnhance); break;
                case 1: ep.btnReforge = btn; btn.onClick.AddListener(ep.OnReforge); break;
                case 2: ep.btnUpgradeQ = btn; btn.onClick.AddListener(ep.OnUpgradeQuality); break;
                case 3: ep.btnEquipSel = btn; btn.onClick.AddListener(ep.OnEquipSelected); break;
                case 4: ep.btnDecompose = btn; btn.onClick.AddListener(ep.OnDecompose); break;
                case 5: ep.btnCleanup = btn; btn.onClick.AddListener(ep.OnCleanup); break;
            }
        }
        equip.SetActive(true);

        // ═══════ 面板 3：Character ═══════
        var cha = CreatePanel("P_Char", content, new Color(.11f, .12f, .18f, .98f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(cha.rt());
        cha.SetActive(false);

        var cp = cha.AddComponent<CharacterPanelUI>();
        cp.statText = CreateText("Stats", cha.transform, 30, Color.white, TextAnchor.UpperCenter);
        SetRect(cp.statText.rectTransform, new Vector2(.05f, .72f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        var gfGrid = CreateGrid("Gongfa", cha.transform, 2, new Vector2(.03f, .06f), new Vector2(.97f, .70f));
        cp.gongfaRow = gfGrid.transform;
        cp.upgradeButtons = new Button[8];
        for (int i = 0; i < 8; i++)
            cp.upgradeButtons[i] = CreateButton("升級", gfGrid.transform, new Color(.3f, .35f, .5f));
        cha.SetActive(true);

        // ═══════ 面板 4：Stages ═══════
        var stg = CreatePanel("P_Stages", content, new Color(.11f, .12f, .18f, .98f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(stg.rt());
        stg.SetActive(false);

        var sp = stg.AddComponent<StagePanelUI>();
        CreateScrollList("StageList", stg.transform, out var listContent,
            new Vector2(.03f, .28f), new Vector2(.97f, .97f));
        sp.listParent = listContent;
        var itemTpl = CreateButton("第 X 夜", listContent, Color.gray);
        sp.itemPrefab = itemTpl.gameObject;
        itemTpl.gameObject.SetActive(false);
        sp.resultText = CreateText("Result", stg.transform, 28, Color.white, TextAnchor.UpperCenter);
        SetRect(sp.resultText.rectTransform, new Vector2(.05f, .05f), new Vector2(.95f, .26f), Vector2.zero, Vector2.zero);
        stg.SetActive(true);

        // ═══════ 面板 5：Forge ═══════
        var forge = CreatePanel("P_Forge", content, new Color(.11f, .12f, .18f, .98f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(forge.rt());
        forge.SetActive(false);

        var fp = forge.AddComponent<ForgePanelUI>();
        fp.slotButtons = new Button[6];
        var fRow = CreateGrid("Slots", forge.transform, 3, new Vector2(.05f, .6f), new Vector2(.95f, .92f));
        string[] names = { "武器", "頭部", "衣服", "飾品", "法器", "鞋子" };
        for (int i = 0; i < 6; i++)
            fp.slotButtons[i] = CreateButton(names[i], fRow.transform, new Color(.25f, .28f, .4f));
        fp.infoText = CreateText("Info", forge.transform, 30, Color.white, TextAnchor.MiddleCenter);
        SetRect(fp.infoText.rectTransform, new Vector2(.05f, .35f), new Vector2(.95f, .58f), Vector2.zero, Vector2.zero);
        var doBtn = CreateButton("開始鍛造", forge.transform, new Color(.6f, .35f, .15f));
        SetRect(doBtn.rt(), new Vector2(.2f, .15f), new Vector2(.8f, .3f), Vector2.zero, Vector2.zero);
        doBtn.onClick.AddListener(fp.DoForge);
        forge.SetActive(true);

        // ═══════ 底部導航 ═══════
        var nav = CreatePanel("NavBar", rootRT, new Color(.13f, .14f, .2f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 170f), new Vector2(0, 0));
        nav.rt().sizeDelta = new Vector2(0, 170);

        var um = gameObject.AddComponent<UIManager>();
        um.panelNames = new[] { "Main", "Equip", "Character", "Stages", "Forge" };
        um.panels = new[] { main, equip, cha, stg, forge };
        um.toastText = uiToast;
        um.toastGroup = toastGroup;

        string[] tabs = { "掛機", "裝備", "角色", "關卡", "鍛造" };
        string[] keys = { "Main", "Equip", "Character", "Stages", "Forge" };
        for (int i = 0; i < 5; i++)
        {
            var b = CreateButton(tabs[i], nav.transform, new Color(.2f, .22f, .3f));
            var rt = b.rt();
            rt.anchorMin = new Vector2(i / 5f + .01f, .1f);
            rt.anchorMax = new Vector2((i + 1) / 5f - .01f, .9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            string k = keys[i];
            b.onClick.AddListener(() => um.Open(k));
        }

        var idleGO = new GameObject("IdleSystem");
        idleGO.transform.SetParent(transform, false);
        idleGO.AddComponent<IdleSystem>();

        top.Refresh();
    }

    // ══════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════

    Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(2, 2);
        tex.SetPixel(0, 0, Color.white);
        tex.SetPixel(1, 0, Color.white);
        tex.SetPixel(0, 1, Color.white);
        tex.SetPixel(1, 1, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 100);
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }

    GameObject CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = whiteSq;
        img.color = color;
        return go;
    }

    Text CreateText(string name, Transform parent, int fontSize, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    void CreateTextIn(Transform parent, string content, int fontSize, Color color)
    {
        var txt = CreateText("Label", parent, fontSize, color, TextAnchor.MiddleCenter);
        txt.text = content;
        Stretch(txt.rectTransform);
    }

    GameObject CreatePanel(string name, Transform parent, Color color,
        Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = CreateImage(name, parent, color);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return go;
    }

    Button CreateButton(string name, Transform parent, Color color)
    {
        var go = CreateImage(name, parent, color);
        var btn = go.AddComponent<Button>();
        var txt = CreateText("Text", go.transform, 28, Color.white, TextAnchor.MiddleCenter);
        txt.text = name;
        Stretch(txt.rectTransform);
        return btn;
    }

    GameObject CreateGrid(string name, Transform parent, int columns, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var grid = go.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.spacing = new Vector2(10, 10);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.cellSize = new Vector2(200, 80);
        return go;
    }

    void CreateScrollList(string name, Transform parent, out RectTransform contentRT,
        Vector2 aMin, Vector2 aMax)
    {
        var scrollGO = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Mask), typeof(Image));
        scrollGO.transform.SetParent(parent, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = aMin;
        scrollRT.anchorMax = aMax;
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGO.transform, false);
        var vpRT = viewport.GetComponent<RectTransform>();
        Stretch(vpRT);
        var vpImg = viewport.GetComponent<Image>();
        vpImg.color = new Color(0, 0, 0, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewport.transform, false);
        contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(.5f, 1);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRT;
        scroll.content = contentRT;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }
}