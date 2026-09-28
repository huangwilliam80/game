using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存檔資料結構（全部可 JSON 序列化）。
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

    // ---- 裝備：已穿戴（槽位索引 -> 装备）與背包 ----
    public EquipmentInstance[] equipped = new EquipmentInstance[6];   // 對應 EquipSlot 0..5
    public List<EquipmentInstance> bags = new List<EquipmentInstance>();

    // ---- 功法（Roguelite 永久強化），index 對應 GongfaDef.id ----
    public int[] gongfaLevel = new int[8];

    // ---- 自動戰鬥配置：帶入戰場的「守卫」數量等 ----
    // ★ 補齊 bedCount
    public int bedCount = 1;              // 宿舍床鋪（守衛）數量

    public const string FileName = "savegame.json";
    public static string Path => Application.persistentDataPath + "/" + FileName;
}

/// <summary>
/// 存檔管理器：負責 讀 / 寫 / 計算離線收益。
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

        // 防呆：版本升級或欄位缺失
        if (loaded.equipped == null || loaded.equipped.Length < 6)
        {
            var eq = new EquipmentInstance[6];
            if (loaded.equipped != null) Array.Copy(loaded.equipped, eq, Mathf.Min(loaded.equipped.Length, 6));
            loaded.equipped = eq;
        }
        if (loaded.bags == null) loaded.bags = new List<EquipmentInstance>();
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
        Data.lastUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try { System.IO.File.WriteAllText(SaveData.Path, JsonUtility.ToJson(Data)); }
        catch (Exception e) { Debug.LogError("存檔失敗: " + e.Message); }
    }

    /// <summary>
    /// 計算離線掛機收益並直接入帳（上限 12 小時，避免數值爆炸）。
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