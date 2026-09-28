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
    public static RectTransform rt(this Component c)
        => c == null ? null : c.transform as RectTransform;   // ★ 用 as 而非強制轉換，壞了也只是 null（下游已防呆）
    // ★ 修復 CS1929：CreatePanel/CreateGrid 回傳的是 GameObject，也需要 .rt()
    public static RectTransform rt(this GameObject go)
        => go == null ? null : go.transform as RectTransform;
}

/// <summary>
/// 場景自動搭建器:用程式碼生成整套豎屏 UI(上半部世界視窗 + 下半部 Dock + 全局遮罩)。
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper I { get; private set; }
    Sprite whiteSq;

    /// <summary>
    /// 場景自舉：不管這個腳本有沒有掛在場景上，Play／打包都會自動建立一份。
    /// （新手常不小心把 GameBootstrapper 從場景刪掉 → 畫面全黑，這裡徹底防掉這個坑。）
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (I != null) return;                                   // 已存在（場景有掛 or 之前建過）
        var go = new GameObject("[GameBootstrapper]");
        go.AddComponent<GameBootstrapper>();                     // AddComponent 同幀執行 Awake
    }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // ★★ 順序至關重要：一定要在「建立任何 TMP 元件」之前決定字型。
        //    TMP 的預設 LiberationSans SDF 沒有中文字形，若先建中文文字、後換字型，
        //    TMP 内部會殘留損毀的字符資訊 → 之後讀取 RectTransform / 佈局時就可能拋 NullReferenceException。
        if (!TMPReady())
            Debug.LogError("[Boot] TextMeshPro 尚未匯入核心資源！請選單 Window → TextMeshPro → Import TMP Essential Resources，" +
                           "否則所有文字都不會顯示（遊戲邏輯仍會正常跑）。");
        ApplyFontEarly();

        // ---- 系統層先就緒（全部 Init 完成後才碰 UI，避免任何「靜態單體尚未建立」的空參考）----
        try
        {
            Log("EquipmentDatabase.Init");   EquipmentDatabase.Init();
            Log("CharacterSystem.Init");     CharacterSystem.Init();
            Log("GameSave.Load");            GameSave.Load();
            Log("RegenerateAffixes");
            foreach (var e in GameSave.Data.bags) e.RegenerateAffixes();
            foreach (var e in GameSave.Data.equipped) if (e != null) e.RegenerateAffixes();

            Log("IdleSystem");
            var idleGO = new GameObject("IdleSystem"); idleGO.transform.SetParent(transform, false);
            idleGO.AddComponent<IdleSystem>();          // Awake 同步執行 → IdleSystem.I 可用
            Log("RunController");
            var runGO = new GameObject("RunController"); runGO.transform.SetParent(transform, false);
            runGO.AddComponent<RunController>();        // Awake 同步執行 → RunController.I 可用

            Log("SettleOffline");
            var (sec, g, e2) = GameSave.SettleOffline();
            Log("BuildUI");
            BuildUI();                                  // 內部另有 try/catch + 逐步防呆
            Log("啟動完成 ✔");

            if (sec > 60) Debug.Log($"離線 {IdleSystem.Fmt(sec)}，已入帳 金幣+{g:N0} 經驗+{e2:N0}");
        }
        catch (System.Exception ex)   // ★ 出錯時 Console 會直接告訴你是哪一步、為什麼，而不是丟回 Unity 內部堆疊
        {
            Debug.LogError("[GameBootstrapper] 啟動失敗：" + ex);
            ShowFatalError(ex.Message);
        }
    }

    /// <summary>最後手段：用 Unity 內建 IMGUI 顯示錯誤（不依賴 TMP／畫布），讓你看得到問題。</summary>
    void ShowFatalError(string msg) { fatal = msg; }
    string fatal;

    /// <summary>TMP 是否已匯入內建資源（沒匯入時 defaultFontAsset 為 null，文字全消失）。</summary>
    static bool TMPReady()
    {
        try { return TMPro.TMP_Settings.defaultFontAsset != null || SafeLoad<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") != null; }
        catch { return false; }
    }

    /// <summary>啟動階段印出「做到哪一步」；若再報錯，Console 最後一行就是罪魁禍首。</summary>
    static void Log(string step) => Debug.Log("[Boot] " + step);
    void OnGUI()
    {
        if (string.IsNullOrEmpty(fatal)) return;
        GUI.Label(new Rect(20, 20, 1000, 300), "启动失败（详见 Console）：\n" + fatal);
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
    UIManager um;                 // ★ 修復 CS0103：Toast／導航在另一個 Step 段落裡，必須用欄位共用

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
        if (bg != null) bg.GetComponent<Image>().sprite = whiteSq;
        Stretch(bg.rt());

        // ---- 頂部資源欄 ----
        var topBarGO = CreatePanel("TopBar", rootRT, new Color(.14f, .15f, .22f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140f), new Vector2(0, 0));
        var top = topBarGO.AddComponent<TopBarUI>();
        // ★ 統一用 Place(...)：CreateText 萬一回傳 null（TMP 沒字型時）也不會連鎖拋 NRE
        top.levelPower = Place(CreateText("LvPow", topBarGO.transform, 34, Color.white, TextAnchor.MiddleLeft),
            new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(20, -70), Vector2.zero);
        top.gold = Place(CreateText("Gold", topBarGO.transform, 30, new Color(1f, .85f, .3f), TextAnchor.MiddleRight),
            new Vector2(.5f, 1), new Vector2(.75f, 1), new Vector2(-10, -70), Vector2.zero);
        top.crystal = Place(CreateText("Crystal", topBarGO.transform, 30, new Color(.5f, .8f, 1f), TextAnchor.MiddleRight),
            new Vector2(.75f, 1), new Vector2(.9f, 1), new Vector2(-10, -70), Vector2.zero);
        top.shard = Place(CreateText("Shard", topBarGO.transform, 30, new Color(.7f, .9f, .6f), TextAnchor.MiddleRight),
            new Vector2(.9f, 1), new Vector2(1, 1), new Vector2(-20, -70), Vector2.zero);
        top.income = Place(CreateText("Income", topBarGO.transform, 26, new Color(.6f, 1f, .6f), TextAnchor.MiddleLeft),
            new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(20, 0), Vector2.zero);
        top.stageLabel = Place(CreateText("Stage", topBarGO.transform, 26, new Color(.9f, .9f, .9f), TextAnchor.MiddleRight),
            new Vector2(.5f, 0), new Vector2(1, 1), new Vector2(-20, 0), Vector2.zero);

        // ---- 中間容器(TopBar 與 NavBar 之間) ----
        var middle = CreatePanel("Middle", rootRT, new Color(0, 0, 0, 0),
            Vector2.zero, Vector2.one, new Vector2(0, 170f), new Vector2(0, -140f));
        if (middle != null) { var mi = middle.GetComponent<Image>(); if (mi) mi.raycastTarget = false; }
        // ★ 注意：這裡不能 Stretch()，否則會把上面 170/-140 的邊距清零，TopBar/NavBar 與面板重疊

        Step("上半部:WorldView", () =>
        {
        // ==== 上半部:WorldView(永遠顯示的戰鬥演出) ====
        var worldViewGO = CreatePanel("WorldView", middle != null ? middle.transform : rootRT, new Color(0.05f, 0.08f, 0.12f),
            new Vector2(0, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);
        if (worldViewGO == null) return;
        // ★ 修復 NullReferenceException：CreatePanel 已用 aMin/aMax 排好位置，
        //   這裡若再 Stretch() 會被「錨點歸零 + offset 歸零」壓成 0x0，
        //   之後 Instantiate(dmgTpl, worldArea) 與所有子節點都會得到非法矩形 → 空參考連鎖。
        var wv = worldViewGO.AddComponent<WorldView>();
        wv.worldArea = worldViewGO.rt();

        // 主角
        var heroImg = CreateImage("Hero", worldViewGO.transform, new Color(0.3f, 0.7f, 1f));
        if (heroImg != null) SetCenter(heroImg.rt(), new Vector2(0.25f, 0.52f), new Vector2(170, 230));
        wv.heroImage = heroImg;

        // 主角血條
        var heroBarBg = CreateImage("HeroHpBarBg", worldViewGO.transform, new Color(0.2f, 0.2f, 0.2f));
        if (heroBarBg != null) SetCenter(heroBarBg.rt(), new Vector2(0.25f, 0.16f), new Vector2(180, 18));
        var heroFill = CreateImage("HeroHpFill", heroBarBg.transform, new Color(0.2f, 0.9f, 0.3f));
        var hf = heroFill != null ? heroFill.GetComponent<Image>() : null;
        if (hf) { hf.type = Image.Type.Filled; hf.fillMethod = Image.FillMethod.Horizontal; hf.fillAmount = 1f; }
        if (heroFill != null) Stretch(heroFill.rt());
        wv.heroHpFill = hf;
        wv.heroHpText = CreateText("HeroHpTxt", heroBarBg.transform, 20, Color.white, TextAnchor.MiddleCenter);
        if (wv.heroHpText != null) Stretch(wv.heroHpText.rt());

        // 敵人錨點 + 模板
        var enemyAnchor = CreatePanel("EnemyAnchor", worldViewGO.transform, new Color(0, 0, 0, 0),
            new Vector2(0.62f, 0.35f), new Vector2(0.9f, 0.8f), Vector2.zero, Vector2.zero);
        wv.enemyAnchor = enemyAnchor != null ? enemyAnchor.rt() : null;
        var enemyTpl = CreateImage("EnemyTpl", enemyAnchor.transform, new Color(0.9f, 0.3f, 0.3f));
        if (enemyTpl != null) enemyTpl.rt().sizeDelta = new Vector2(160, 200);
        if (enemyTpl != null) { enemyTpl.gameObject.SetActive(false); wv.enemyPrefab = enemyTpl.gameObject; }

        // 敵人血條 + 名稱
        var eBarBg = CreateImage("EnemyHpBarBg", worldViewGO.transform, new Color(0.2f, 0.2f, 0.2f));
        if (eBarBg != null) SetCenter(eBarBg.rt(), new Vector2(0.76f, 0.16f), new Vector2(180, 18));
        var eFill = CreateImage("EnemyHpFill", eBarBg.transform, new Color(0.9f, 0.3f, 0.3f));
        var ef = eFill != null ? eFill.GetComponent<Image>() : null;
        if (ef) { ef.type = Image.Type.Filled; ef.fillMethod = Image.FillMethod.Horizontal; ef.fillAmount = 1f; }
        if (eFill != null) Stretch(eFill.rt());
        wv.enemyHpFill = ef;
        wv.enemyNameText = CreateText("EnemyName", worldViewGO.transform, 22, Color.white, TextAnchor.MiddleCenter);
        if (wv.enemyNameText != null) SetCenter(((Component)wv.enemyNameText).rt(), new Vector2(0.76f, 0.86f), new Vector2(200, 40));
        wv.enemyHpText = CreateText("EnemyHpTxt", eBarBg.transform, 20, Color.white, TextAnchor.MiddleCenter);
        if (wv.enemyHpText != null) Stretch(wv.enemyHpText.rt());

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
        Place(waveBanner, new Vector2(0, 0.9f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        wv.waveBanner = waveBanner;

        // ==== 掛機小怪層（DormBattleView）：與 RunController 的即時戰鬥互不干擾 ====
        // ★ 修復：這個元件從未被 AddComponent → 掛機時上半部完全沒有「鬼來襲」的視覺節拍。
        var dormGo = CreatePanel("DormLayer", worldViewGO.transform, new Color(0, 0, 0, 0),
            new Vector2(0.03f, 0.20f), new Vector2(0.55f, 0.42f), Vector2.zero, Vector2.zero);
        if (dormGo != null)
        {
            var dv = dormGo.AddComponent<DormBattleView>();
            dv.stageArea = dormGo.rt();
            // 6 個床鋪位（依 bedCount 顯示；未解鎖的床直接隱藏）
            for (int i = 0; i < 6; i++)
            {
                var bed = CreateImage($"Bed{i}", dormGo.transform, new Color(.25f, .35f, .5f));
                if (bed != null) SetCenter(bed.rt(), new Vector2(0.08f + i * 0.17f, 0.25f), new Vector2(90, 46));
                if (bed != null) dv.bedSlots.Add(bed.rt());
            }
            // 小鬼模板（池化：只 Instantiate 一次模板，之後重複使用）
            var ghostTpl = CreateImage("GhostTpl", dormGo.transform, new Color(.75f, .45f, .95f));
            if (ghostTpl != null)
            {
                ghostTpl.rt().sizeDelta = new Vector2(70, 70);
                ghostTpl.gameObject.AddComponent<CanvasGroup>();
                ghostTpl.gameObject.SetActive(false);
                dv.ghostPrefab = ghostTpl.gameObject;
            }
        }

        // 快速進入秘境的浮動按鈕（UX：一眼看到「現在能做什麼」）
        var quickRunBtn = CreateButton("⚔ 靈玉秘境", worldViewGO.transform, new Color(.55f, .28f, .65f));
        if (quickRunBtn != null) SetRect(quickRunBtn.rt(), new Vector2(.62f, .02f), new Vector2(.98f, .12f), Vector2.zero, Vector2.zero);
        if (quickRunBtn) quickRunBtn.onClick.AddListener(() => RunController.I?.StartRun(Mathf.Max(1, GameSave.Data.currentStage)));
        var quickLbl = quickRunBtn != null ? quickRunBtn.GetComponentInChildren<TMP_Text>() : null;
        if (quickLbl) quickLbl.fontSize = 26;

        // buff 標籤列
        var buffLbl = new GameObject("BuffLbl"); buffLbl.transform.SetParent(worldViewGO.transform, false);
        var bl = buffLbl.AddComponent<TextMeshProUGUI>();
        bl.fontSize = 22; bl.color = new Color(1f, 0.9f, 0.4f); bl.alignment = TextAlignmentOptions.BottomLeft;
        var blRT = buffLbl.AddComponent<RectTransform>();
        blRT.anchorMin = new Vector2(0.05f, 0.02f); blRT.anchorMax = new Vector2(0.95f, 0.08f);
        blRT.offsetMin = blRT.offsetMax = Vector2.zero;
        wv.buffLabel = buffLbl.transform;
        });

        Step("下半部:Dock+面板", () =>
        {
        // ==== 下半部:Dock(面板切換區) ====
        var dock = CreatePanel("Dock", middle != null ? middle.transform : rootRT, new Color(0.11f, 0.12f, 0.18f),
            Vector2.zero, new Vector2(1, 0.45f), Vector2.zero, Vector2.zero);
        if (dock == null) return;
        // ★ 同 WorldView：不可再 Stretch()，否則會把 0~0.45 的錨點清掉、與上半部重疊
        var content = dock.rt();
        if (content == null) return;

        // === 面板 1:Main ===
        // ★★ 關鍵修復：五個分頁面板必須是「rootRT 下彼此獨立、同範圍」的節點。
        //    之前掛在 Dock 裡並用 Stretch() 把 Dock 撐滿 → 面板與 Dock 底圖／其他面板重疊，
        //    結果就是「按鈕看不到、點了沒反應」。現在統一：parent = rootRT，
        //    範圍 = 頂欄下方 ~ 導航列上方（offset 0,170 / 0,-140）。
        // 分頁統一範圍：頂欄下方 170px ~ 導航列上方 140px
        Vector2 pOffMin = new Vector2(0, 170f), pOffMax = new Vector2(0, -140f);
        var main = CreateLayer("P_Main", rootRT, Vector2.zero, Vector2.one, pOffMin, pOffMax);
        if (main == null) { Debug.LogWarning("[BuildUI] 面板建立失敗：P_Main"); return; }
        AddPanelBg(main, new Color(.10f, .11f, .16f, .98f));
        var mp = main.AddComponent<MainPanelUI>();
        // 排版：上半=离线横幅 → 下一目标；下半=三顆行動按鈕（大按钮、拇指可及）
        mp.offlineBanner = CreateText("Offline", main.transform, 30, new Color(1f, .9f, .5f), TextAnchor.MiddleCenter);
        Place(mp.offlineBanner, new Vector2(.05f, .74f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        mp.goalLabel = CreateText("Goal", main.transform, 28, new Color(.95f, .95f, .8f), TextAnchor.UpperCenter);
        Place(mp.goalLabel, new Vector2(.05f, .46f), new Vector2(.95f, .74f), Vector2.zero, Vector2.zero);
        mp.dungeonBtn = CreateButton("⚔ 靈玉秘境（即時戰鬥）", main.transform, new Color(.55f, .28f, .65f));
        Place(mp.dungeonBtn, new Vector2(.06f, .30f), new Vector2(.94f, .43f), Vector2.zero, Vector2.zero);
        if (mp.dungeonBtn) mp.dungeonBtn.onClick.AddListener(() => RunController.I?.StartRun(Mathf.Max(1, GameSave.Data.currentStage)));
        mp.claimBtn = CreateButton("領取離線收益", main.transform, new Color(.2f, .5f, .3f));
        Place(mp.claimBtn, new Vector2(.06f, .14f), new Vector2(.48f, .27f), Vector2.zero, Vector2.zero);
        mp.stageBtn = CreateButton("關卡掃蕩", main.transform, new Color(.6f, .4f, .15f));
        Place(mp.stageBtn, new Vector2(.52f, .14f), new Vector2(.94f, .27f), Vector2.zero, Vector2.zero);
        mp.forgeBtn = CreateButton("整理背包", main.transform, new Color(.35f, .3f, .55f));
        Place(mp.forgeBtn, new Vector2(.06f, .0f), new Vector2(.48f, .12f), Vector2.zero, Vector2.zero);
        if (mp.claimBtn) mp.claimBtn.onClick.AddListener(() => IdleSystem.I?.CollectOfflineNow());
        if (mp.stageBtn) mp.stageBtn.onClick.AddListener(() => UIManager.I?.Open("Stages"));
        if (mp.forgeBtn) mp.forgeBtn.onClick.AddListener(() => UIManager.I?.Open("Equip"));

        // === 面板 2:Equip ===
        var equip = CreateLayer("P_Equip", rootRT, Vector2.zero, Vector2.one, pOffMin, pOffMax);
        if (equip == null) { Debug.LogWarning("[BuildUI] 面板建立失敗：P_Equip"); return; }
        AddPanelBg(equip, new Color(.11f, .12f, .18f, .98f));
        var ep = equip.AddComponent<EquipmentPanelUI>();
        var slotRow = CreateGrid("SlotRow", equip.transform, 3, new Vector2(.03f, .72f), new Vector2(.97f, .97f));
        if (slotRow != null)
        {
            ep.equipRow = slotRow.transform;
            string[] slotNames = { "武器", "頭部", "衣服", "飾品", "法器", "鞋子" };
            for (int i = 0; i < 6; i++)
            {
                var b = CreateButton(slotNames[i], slotRow.transform, new Color(.2f, .22f, .3f));
                int si = i;
                if (b) b.onClick.AddListener(() => InventorySystem.SmartEquipBest((EquipSlot)si));
            }
        }
        var bagGrid = CreateGrid("BagGrid", equip.transform, 4, new Vector2(.03f, .30f), new Vector2(.97f, .70f));
        if (bagGrid != null)
        {
            ep.bagGrid = bagGrid.transform;
            var cellTpl = CreateButton("CellTpl", bagGrid.transform, Color.white);
            ep.cellPrefab = cellTpl != null ? cellTpl.gameObject : null;
            if (cellTpl != null) cellTpl.gameObject.SetActive(false);
        }
        ep.detailText = CreateText("Detail", equip.transform, 26, Color.white, TextAnchor.UpperLeft);
        Place(ep.detailText, new Vector2(.03f, .08f), new Vector2(.62f, .29f), Vector2.zero, Vector2.zero);
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
        var cha = CreateLayer("P_Char", rootRT, Vector2.zero, Vector2.one, pOffMin, pOffMax);
        if (cha == null) { Debug.LogWarning("[BuildUI] 面板建立失敗：P_Char"); return; }
        AddPanelBg(cha, new Color(.11f, .12f, .18f, .98f));
        var cp = cha.AddComponent<CharacterPanelUI>();
        cp.statText = CreateText("Stats", cha.transform, 30, Color.white, TextAnchor.UpperCenter);
        Place(cp.statText, new Vector2(.05f, .72f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
        var gfGrid = CreateGrid("Gongfa", cha.transform, 2, new Vector2(.03f, .06f), new Vector2(.97f, .70f));
        if (gfGrid != null)
        {
            cp.gongfaRow = gfGrid.transform;
            cp.upgradeButtons = new Button[8];
            for (int i = 0; i < 8; i++) cp.upgradeButtons[i] = CreateButton("升級", gfGrid.transform, new Color(.3f, .35f, .5f));
        }

        // === 面板 4:Stages ===
        var stg = CreateLayer("P_Stages", rootRT, Vector2.zero, Vector2.one, pOffMin, pOffMax);
        if (stg == null) { Debug.LogWarning("[BuildUI] 面板建立失敗：P_Stages"); return; }
        AddPanelBg(stg, new Color(.11f, .12f, .18f, .98f));
        var sp = stg.AddComponent<StagePanelUI>();
        CreateScrollList("StageList", stg.transform, out var listContent,
            new Vector2(.03f, .34f), new Vector2(.97f, .97f));
        if (listContent != null)
        {
            sp.listParent = listContent;
            var itemTpl = CreateButton("第 X 夜", listContent, Color.gray);
            sp.itemPrefab = itemTpl != null ? itemTpl.gameObject : null;
            if (itemTpl != null) itemTpl.gameObject.SetActive(false);
        }
        sp.resultText = CreateText("Result", stg.transform, 26, Color.white, TextAnchor.UpperLeft);
        Place(sp.resultText, new Vector2(.05f, .04f), new Vector2(.55f, .26f), Vector2.zero, Vector2.zero);

        // ★ 秘境（Roguelite）入口：文案直接告訴玩家獎勵與特色
        sp.runHint = CreateText("RunHint", stg.transform, 22, new Color(.9f, .88f, .75f), TextAnchor.UpperLeft);
        Place(sp.runHint, new Vector2(.57f, .04f), new Vector2(.98f, .26f), Vector2.zero, Vector2.zero);
        var runB = CreateButton("進入靈玉秘境（即時戰鬥）", stg.transform, new Color(.62f, .3f, .7f));
        if (runB != null) SetRect(runB.rt(), new Vector2(.57f, .27f), new Vector2(.98f, .33f), Vector2.zero, Vector2.zero);
        var runT = runB != null ? runB.GetComponentInChildren<TMP_Text>() : null;
        if (runT) runT.fontSize = 22;
        sp.runBtn = runB;

        // === 面板 5:Forge ===
        var forge = CreateLayer("P_Forge", rootRT, Vector2.zero, Vector2.one, pOffMin, pOffMax);
        if (forge == null) { Debug.LogWarning("[BuildUI] 面板建立失敗：P_Forge"); return; }
        AddPanelBg(forge, new Color(.11f, .12f, .18f, .98f));
        var fp = forge.AddComponent<ForgePanelUI>();
        fp.slotButtons = new Button[6];
        var fRow = CreateGrid("Slots", forge.transform, 3, new Vector2(.05f, .6f), new Vector2(.95f, .92f));
        string[] names = { "武器", "頭部", "衣服", "飾品", "法器", "鞋子" };
        if (fRow != null)
            for (int i = 0; i < 6; i++) fp.slotButtons[i] = CreateButton(names[i], fRow.transform, new Color(.25f, .28f, .4f));
        fp.infoText = CreateText("Info", forge.transform, 30, Color.white, TextAnchor.MiddleCenter);
        Place(fp.infoText, new Vector2(.05f, .35f), new Vector2(.95f, .58f), Vector2.zero, Vector2.zero);
        var doBtn = CreateButton("開始鍛造", forge.transform, new Color(.6f, .35f, .15f));
        if (doBtn != null) { SetRect(doBtn.rt(), new Vector2(.2f, .15f), new Vector2(.8f, .3f), Vector2.zero, Vector2.zero); doBtn.onClick.AddListener(fp.DoForge); }

        // ---- UIManager 綁定 Dock 內的面板 ----
        um = dock.AddComponent<UIManager>();
        um.panelNames = new[] { "Main", "Equip", "Character", "Stages", "Forge" };
        um.panels = new[] { main, equip, cha, stg, forge };
        um.InitPanels();
        });

        Step("全局遮罩", () =>
        {
        // ==== 全局遮罩 1:ChoiceOverlay(三選一) ====
        var choiceGo = CreatePanel("ChoiceOverlay", rootRT, new Color(0, 0, 0, 0.6f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        if (choiceGo == null) return;
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
        Place(cn, Vector2.zero, Vector2.one, new Vector2(10, -60), new Vector2(-10, -10));
        var cr = CreateText("Rarity", cardTpl.transform, 20, new Color(1f, 0.9f, 0.3f), TextAnchor.UpperRight);
        Place(cr, Vector2.zero, Vector2.one, new Vector2(-20, -40), new Vector2(0, 0));
        var cd = CreateText("Desc", cardTpl.transform, 22, Color.white, TextAnchor.LowerCenter);
        Place(cd, Vector2.zero, Vector2.one, new Vector2(10, 20), new Vector2(-10, 120));
        if (cardTpl != null) cardTpl.SetActive(false);
        co.cardPrefab = cardTpl;

        // ==== 全局遮罩 2:RunResult(結算) ====
        var resultGo = CreatePanel("RunResult", rootRT, new Color(0, 0, 0, 0.75f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        if (resultGo == null) return;
        var resCG = resultGo.AddComponent<CanvasGroup>();
        resCG.alpha = 0; resCG.blocksRaycasts = false;
        Stretch(resultGo.rt());

        var rr = resultGo.AddComponent<RunResultUI>();
        rr.root = resCG;
        rr.title = CreateText("Title", resultGo.transform, 48, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter);
        Place(rr.title, new Vector2(0, 0.78f), new Vector2(1, 0.92f), Vector2.zero, Vector2.zero);
        rr.summaryText = CreateText("Summary", resultGo.transform, 28, Color.white, TextAnchor.MiddleCenter);
        Place(rr.summaryText, new Vector2(0, 0.05f), new Vector2(1, 0.15f), Vector2.zero, Vector2.zero);

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
        Place(rn, Vector2.zero, new Vector2(1, 0.5f), new Vector2(20, 0), Vector2.zero);
        var rs = CreateText("Stat", rowTpl.transform, 22, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft);
        Place(rs, Vector2.zero, new Vector2(1, 0.5f), new Vector2(20, -40), Vector2.zero);
        if (rowTpl != null) rowTpl.SetActive(false);
        rr.lootRowPrefab = rowTpl;

        var confBtn = CreateButton("確認並結算", resultGo.transform, new Color(0.2f, 0.6f, 0.3f));
        if (confBtn != null) SetRect(confBtn.rt(), new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.13f), Vector2.zero, Vector2.zero);
        rr.confirmBtn = confBtn;
        });

        Step("Toast+導航", () =>
        {
        // ---- Toast（掛在根層，任何面板都能看見）----
        var toastGO = new GameObject("Toast"); toastGO.transform.SetParent(rootRT, false);
        var toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        var tbg = toastGO.AddComponent<Image>(); tbg.color = new Color(0, 0, 0, .7f);
        tbg.raycastTarget = false;           // ★ 重要：Toast 是「提示」，绝不能挡住下面的点击
        var trt = toastGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(.1f, .55f); trt.anchorMax = new Vector2(.9f, .65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var uiToast = CreateText("ToastTxt", toastGO.transform, 34, Color.white, TextAnchor.MiddleCenter);
        if (uiToast != null) { uiToast.raycastTarget = false; Place(uiToast, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); }
        if (um != null) um.BindToast(uiToast, toastGroup);   // ★ Toast 交给 UIManager 统一播放（含淡出）

        // ---- 底部導航 ----
        // ★ 修復：底部導航列用「錨點(0,0)-(1,0) + offsetMin(0,0)/offsetMax(0,170)」才有 170 高；
        //   之前多了一行 sizeDelta=(0,170) 會把 offsetMax.y 蓋掉 → 高度變 0，整條 NavBar 消失。
        var nav = CreatePanel("NavBar", rootRT, new Color(.13f, .14f, .2f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 170f));

        string[] tabs = { "掛機", "装备", "角色", "關卡", "鍛造" };
        string[] keys = { "Main", "Equip", "Character", "Stages", "Forge" };
        for (int i = 0; i < 5; i++)
        {
            var b = CreateButton(tabs[i], nav.transform, new Color(.2f, .22f, .3f));
            if (b == null) continue;
            var rt = b.rt();
            rt.anchorMin = new Vector2(i / 5f + .01f, .1f); rt.anchorMax = new Vector2((i + 1) / 5f - .01f, .9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            string k = keys[i];
            b.onClick.AddListener(() => { if (um != null) um.Open(k); });
        }
        });

        // （IdleSystem / RunController 已在 Awake 早期建立，這裡不再重複創建）
        ApplyFont();
        top.Refresh();
    }

    /// <summary>
    /// ★ 在任何 TMP 元件诞生之前，先把「TMP 預設字型」定成中文字型。
    /// 這樣所有 CreateText／動態生成的文字一開始就用正確字型，
    /// 不會出現「先用無中文的字型建好、事後才換字型」導致的損毀字符 → NullReferenceException。
    /// </summary>
    void ApplyFontEarly()
    {
        var f = CJKFont;
        if (f != null)
        {
            TMPro.TMP_Settings.defaultFontAsset = f;
            Debug.Log("[Boot] 預設字型＝" + f.name);
        }
    }

    /// <summary>把所有文字套上專案內的中文字型（找不到就沿用 TMP 預設，不會報錯）。</summary>
    void ApplyFont()
    {
        var cjk = CJKFont;
        if (cjk == null) return;                    // 已在外層印過警告（畫面仍可用西文正常運作）
        TMPro.TMP_Settings.defaultFontAsset = cjk;
        if (canvas == null) return;
        var all = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            if (t.font != cjk) { t.font = cjk; t.SetAllDirty(); }   // ★ 換字型後務必標脏，讓 TMP 重建字符
            t.enableAutoSizing = false;
            t.enabled = true;                // ★ 還原被「無字型」保護性關閉的文字
        }
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

            // 1) 先驗「資產名稱 == 檔案名稱」，不符就跳過
            //    （你上次出現 Main Object Name '...Bold SDF' does not match filename '...Regular SDF'，
            //     這種资产在 Unity 內部已被標記為丟棄，讀它任何欄位都可能回傳 null → NRE，必須直接略過。）
            var named = SafeLoad<TMP_FontAsset>("Fonts/SourceHanSansCN-Regular SDF");
            if (named != null && named.name == "SourceHanSansCN-Regular SDF")
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

    /// <summary>
    /// Resources.Load 的安全版：過濾掉「損壞 / 被 Unity 丟棄 / 名稱與檔名不符」的资产。
    /// 這類对象 Resources.Load 仍會回傳非 null，但存取其內容就會拋例外或回傳 null，
    /// 之後任何 RectTransform／佈局操作都可能連鎖變成 NullReferenceException。
    /// </summary>
    static T SafeLoad<T>(string path) where T : UnityEngine.Object
    {
        try
        {
            var o = Resources.Load<T>(path);
            if (o == null) return null;                       // 不存在或已被銷毀
            // 名稱與「檔案末段」不一致 → Unity 已將該物件標記為擲棄，一律不用
            int slash = path.LastIndexOf('/');
            string file = slash >= 0 ? path.Substring(slash + 1) : path;
            if (!string.Equals(o.name, file, System.StringComparison.Ordinal))
            {
                Debug.LogWarning($"[SafeLoad] 略過「{path}」：資產名稱({o.name}) ≠ 檔名({file})。\n" +
                    "→ 請重新產生此 TMP 字型资产（Window→TextMeshPro→Font Asset Creator），存檔時名稱要與檔名一致。");
                return null;
            }
            return o;
        }
        catch (Exception e) { Debug.LogWarning("[SafeLoad] " + path + " → " + e.Message); return null; }
    }

    static bool IsValid(UnityEngine.Object o)
    {
        if (o == null) return false;                 // 已被刪除
        try { return !string.IsNullOrEmpty(o.name); }
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

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;                     // ★ 防呆：上游元件缺失時靜默略過，不打斷整條 BuildUI
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        if (rt == null) return;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    /// <summary>以「中心錨點 + 尺寸」定位（避免 anchorMin/Max 連續賦值在元件為 null 時崩潰）。</summary>
    static void SetCenter(RectTransform rt, Vector2 anchor, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 「定位」通用版：文字／按鈕都能用（★ 修復 CS1503：原本只接受 TextMeshProUGUI，
    /// 但面板欄位宣告的是 TMP_Text、有時傳入的是 Button，型別不符就編譯不過）。
    /// </summary>
    static T Place<T>(T t, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax) where T : Component
    {
        if (t == null) return null;
        var rt = t.transform as RectTransform;
        if (rt != null) { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax; }
        return t;
    }

    /// <summary>GameObject 版定位（面板／容器常用）。</summary>
    static GameObject Place(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        if (go == null) return null;
        SetRect(go.transform as RectTransform, aMin, aMax, offMin, offMax);
        return go;
    }

    /// <summary>把一大段 UI 組裝包起來：單段失敗只印錯誤、不影響其他面板（避免半套畫面）。</summary>
    void Step(string name, System.Action build)
    {
        try { Log("BuildUI/" + name); build(); }
        catch (System.Exception e) { Debug.LogError("[BuildUI] 「" + name + "」組裝失敗：" + e); }
    }

    GameObject CreatePanel(string name, Transform parent, Color col, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        if (parent == null) { Debug.LogWarning("[BuildUI] 遺漏父節點：" + name); return null; }
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        return go;
    }

    Image CreateImage(string name, Transform parent, Color col)
    {
        if (parent == null) { Debug.LogWarning("[BuildUI] 遺漏父節點：" + name); return null; }
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.sprite = whiteSq; img.color = col;
        img.rt().sizeDelta = new Vector2(100, 100);
        return img;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, int size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        // ★ 防呆：父節點若已被銷毀（前一個步驟失敗的連鎖反應），這裡直接回傳 null，
        //   而不是讓整條 BuildUI 崩在中間、只剩半套畫面。所有呼叫端都有 if(x) 檢查。
        if (parent == null) { Debug.LogWarning("[BuildUI] 遺漏父節點：" + name); return null; }
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        // ★ TMP 若連 defaultFontAsset 都拿不到（專案沒跑過 Import TMP Essentials）會內部空參考，
        //   這裡先補一層保護：沒有字型就暫時關掉元件，等 ApplyFont() 再打開。
        if (TMPro.TMP_Settings.defaultFontAsset == null) t.enabled = false;
        t.fontSize = size; t.color = col; t.alignment = (TextAlignmentOptions)anchor;
        t.text = name; t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;      // ★ 純顯示文字不吃點擊，避免擋住按鈕／背包格子
#if UNITY_2022_3_OR_NEWER || UNITY_6000_0_OR_NEWER
        t.textWrappingMode = TextWrappingModes.Normal;
#else
        t.enableWordWrapping = true;
#endif
        return t;
    }

    Button CreateButton(string label, Transform parent, Color col)
    {
        if (parent == null) { Debug.LogWarning("[BuildUI] 遺漏父節點：Btn_" + label); return null; }
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
        if (go == null) return null;
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
        if (sv == null) { contentRT = null; return null; }
        var scroll = sv.AddComponent<ScrollRect>();
        // ★ 修復：viewport 必須是「獨立且帶遮罩」的節點，否則整塊背景會被裁掉、列表也無法捲動
        var vpGO = CreatePanel("Viewport", sv.transform, new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        vpGO.AddComponent<RectMask2D>();
        var c = new GameObject("Content"); c.transform.SetParent(vpGO.transform, false);
        contentRT = c.AddComponent<RectTransform>();   // ★ 手動建的 GameObject 需自己加 RectTransform
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

    // ================= 面板分頁工具（★ 缺失定義補齊：原本 BuildUI 呼叫了卻沒實作 → CS0103 編譯不過）=================

    /// <summary>
    /// 建立一個「分頁層級」容器：透明底、不吃點擊、可帶圓角風格的背景由 AddPanelBg 另外疊。
    /// 回傳 GameObject（已附 RectTransform），失敗回傳 null 並由呼叫端防呆。
    /// </summary>
    static GameObject CreateLayer(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        if (parent == null) { Debug.LogWarning("[BuildUI] 遺漏父節點：Layer " + name); return null; }
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = I != null ? I.whiteSq : null;
        img.color = new Color(0, 0, 0, 0);      // 容器本身透明
        img.raycastTarget = false;              // ★ 空檔不要擋住下層按鈕
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        return go;
    }

    /// <summary>幫分頁補一層不透明底色（放在最底層，避免蓋住子元素）。</summary>
    static void AddPanelBg(GameObject panel, Color col)
    {
        if (panel == null) return;
        var bg = CreatePanel("PanelBg", panel.transform, col, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        if (bg == null) return;
        var img = bg.GetComponent<Image>();
        if (img != null) { img.sprite = I != null ? I.whiteSq : null; img.raycastTarget = true; }
        bg.transform.SetAsFirstSibling();       // ★ 底圖必須在第一個 sibling，否則會蓋住內容
    }
}