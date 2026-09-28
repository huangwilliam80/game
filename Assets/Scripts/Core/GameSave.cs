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
    public long lastUnixSec = 0;
    public long gold = 0;
    public long exp = 0;
    public long spiritCrystal = 0;
    public long forgeShard = 0;
    public int heroLevel = 1;
    public int maxStageCleared = 0;
    public int currentStage = 1;
    public EquipmentInstance[] equipped = new EquipmentInstance[6];
    public EquipmentInstance[] serializedBags = new EquipmentInstance[0];
    
    [System.NonSerialized]
    public System.Collections.Generic.List<EquipmentInstance> bags = new System.Collections.Generic.List<EquipmentInstance>();
    
    public int[] gongfaLevel = new int[8];
    public int bedCount = 1;

    public const string FileName = "savegame.json";
    public static string Path => UnityEngine.Application.persistentDataPath + "/" + FileName;

    public void PrepareForSave()
    {
        serializedBags = bags.ToArray();
    }

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

        loaded.OnLoad();

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

        // ★★★ 核心修復：自動清理無效的裝備數據（防止舊存檔崩潰）★★★
        CleanInvalidEquipData(loaded);

        Data = loaded;
    }

    /// <summary>自動剔除找不到模板的髒數據</summary>
    static void CleanInvalidEquipData(SaveData d)
    {
        // 清理背包
        for (int i = d.bags.Count - 1; i >= 0; i--)
        {
            if (d.bags[i] == null || EquipmentDatabase.Get(d.bags[i].defId) == null)
            {
                d.bags.RemoveAt(i);
            }
        }
        // 清理已穿戴
        for (int i = 0; i < d.equipped.Length; i++)
        {
            if (d.equipped[i] != null && EquipmentDatabase.Get(d.equipped[i].defId) == null)
            {
                d.equipped[i] = null;
            }
        }
    }

    public static void Save()
    {
        Data.PrepareForSave();
        Data.lastUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            System.IO.File.WriteAllText(SaveData.Path, JsonUtility.ToJson(Data));
        }
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

        elapsed = Math.Min(elapsed, 12 * 3600);
        IdleFormula.IdleRate(out long gps, out long eps, Data.maxStageCleared, Data.heroLevel);

        long g = gps * elapsed;
        long e = eps * elapsed;

        Data.gold += g;
        Data.exp += e;
        Data.lastUnixSec = now;

        return ((int)elapsed, g, e);
    }
}