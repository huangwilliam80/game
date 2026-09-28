using System;
using UnityEngine;

/// <summary>
/// 存檔資料結構（全部可 JSON 序列化）。
/// 只存「必要資訊」，其餘數值一律運行時重算 → 存檔極小、載入極快。
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;

    // ---- 時間（離線掛機的核心）----
    public long lastUnixSec = 0;          // 上次登出時間戳

    // ---- 貨幣／養成 ----
    public long gold = 0;
    public long exp = 0;
    public long spiritCrystal = 0;
    public long forgeShard = 0;
    public int heroLevel = 1;

    // ---- 進度 ----
    public int maxStageCleared = 0;       // 已通過的最大關卡（掛機收益依據）
    public int currentStage = 1;          // 目前選擇的關卡

    // ---- 裝備：已穿戴（槽位索引 -> 装备）----
    public EquipmentInstance[] equipped = new EquipmentInstance[6];   // 對應 EquipSlot 0..5
    
    // 【修正重點】：Unity 內建的 JsonUtility 無法儲存 List，所以這裡用 Array 來存檔。
    // 實際遊戲邏輯中我們還是會用 List 來操作（因為 Add/Remove 比較方便）。
    public EquipmentInstance[] serializedBags = new EquipmentInstance[0]; 
    
    // 加上 [System.NonSerialized] 讓 JsonUtility 忽略這個 List，避免衝突
    [System.NonSerialized]
    public System.Collections.Generic.List<EquipmentInstance> bags = new System.Collections.Generic.List<EquipmentInstance>();

    // ---- 功法（Roguelite 永久強化），index 對應 GongfaDef.id ----
    public int[] gongfaLevel = new int[8];

    // ---- 自動戰鬥配置：帶入戰場的「守卫」數量等 ----
    public int bedCount = 1;              // 宿舍床鋪（守衛）數量

    public const string FileName = "savegame.json";
    public static string Path => UnityEngine.Application.persistentDataPath + "/" + FileName;

    // 【新增】：存檔前呼叫，把 List 轉成陣列準備存起來
    public void PrepareForSave() 
    { 
        serializedBags = bags.ToArray(); 
    }

    // 【新增】：讀檔後呼叫，把讀出來的陣列還原成 List 給程式使用
    public void OnLoad() 
    { 
        if (serializedBags == null) 
        {
            bags = new System.Collections.Generic.List<EquipmentInstance>();
        }
        else
        {
            bags = new System.Collections.Generic.List<EquipmentInstance>(serializedBags);
        }
    }
}

/// <summary>
/// 存檔管理器：負責 讀 / 寫 / 計算離線收益。
/// 用 JsonUtility（Unity 內建、零依賴、速度快）。
/// </summary>
public static class GameSave
{
    public static SaveData Data { get; private set; }

    public static void Load()
    {
        SaveData loaded = null;
        if (System.IO.File.Exists(SaveData.Path))
        {
            try { loaded = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(SaveData.Path)); }
            catch (Exception e) { Debug.LogWarning("存檔損毀，重新開始: " + e.Message); }
        }

        if (loaded == null) loaded = new SaveData();

        // 【修正重點】：讀取完成後，立刻把 Array 轉回 List
        loaded.OnLoad();

        // 防呆：版本升級或欄位缺失
        if (loaded.equipped == null || loaded.equipped.Length < 6)
        {
            var eq = new EquipmentInstance[6];
            if (loaded.equipped != null) Array.Copy(loaded.equipped, eq, Mathf.Min(loaded.equipped.Length, 6));
            loaded.equipped = eq;
        }
        
        if (loaded.gongfaLevel == null || loaded.gongfaLevel.Length < 8)
        {
            var g = new int[8];
            if (loaded.gongfaLevel != null) Array.Copy(loaded.gongfaLevel, g, Mathf.Min(loaded.gongfaLevel.Length, 8));
            loaded.gongfaLevel = g;
        }

        Data = loaded;
    }

    public static void Save()
    {
        // 【修正重點】：存檔前，先把 List 轉成 Array
        Data.PrepareForSave(); 
        
        Data.lastUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try { 
            System.IO.File.WriteAllText(SaveData.Path, JsonUtility.ToJson(Data)); 
        }
        catch (Exception e) { Debug.LogError("存檔失敗: " + e.Message); }
    }

    /// <summary>
    /// 計算離線掛機收益並直接入帳（上限 12 小時，避免數值爆炸）。
    /// 回傳 (秒數, 金幣, 經驗) 給 UI 顯示「歡迎回來」。
    /// </summary>
    public static (int seconds, long gold, long exp) SettleOffline()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Data.lastUnixSec <= 0) { Data.lastUnixSec = now; return (0, 0, 0); }

        long elapsed = now - Data.lastUnixSec;
        if (elapsed <= 0) return (0, 0, 0);
        elapsed = Math.Min(elapsed, 12 * 3600);      // 上限 12 小時

        IdleFormula.IdleRate(out long gps, out long eps, Data.maxStageCleared, Data.heroLevel);
        long g = gps * elapsed;
        long e = eps * elapsed;

        Data.gold += g;
        Data.exp += e;
        Data.lastUnixSec = now;
        return ((int)elapsed, g, e);
    }
}