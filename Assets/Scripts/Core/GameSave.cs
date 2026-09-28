using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int version = 1;
    public long lastUnixSec = 0;
    public long gold = 1000; // 新手贈送初始資金
    public long exp = 0;
    public long spiritCrystal = 100;
    public long forgeShard = 50;
    public int heroLevel = 1;
    public int maxStageCleared = 0;
    public int currentStage = 1;
    public EquipmentInstance[] equipped = new EquipmentInstance[6];
    public List<EquipmentInstance> bags = new List<EquipmentInstance>();
    public int[] gongfaLevel = new int[8];
    public const string FileName = "savegame.json";
    public static string Path => Application.persistentDataPath + "/" + FileName;
}

public static class GameSave
{
    public static SaveData Data { get; private set; }

    public static void Load()
    {
        SaveData loaded = null;
        if (System.IO.File.Exists(SaveData.Path))
        {
            try { loaded = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(SaveData.Path)); }
            catch { Debug.LogWarning("存檔損毀，重新開始"); }
        }
        if (loaded == null) loaded = new SaveData();
        if (loaded.equipped == null || loaded.equipped.Length < 6) loaded.equipped = new EquipmentInstance[6];
        if (loaded.bags == null) loaded.bags = new List<EquipmentInstance>();
        if (loaded.gongfaLevel == null || loaded.gongfaLevel.Length < 8) loaded.gongfaLevel = new int[8];
        Data = loaded;
    }

    public static void Save()
    {
        Data.lastUnixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try { System.IO.File.WriteAllText(SaveData.Path, JsonUtility.ToJson(Data)); }
        catch (Exception e) { Debug.LogError("存檔失敗: " + e.Message); }
    }

    public static (int seconds, long gold, long exp) SettleOffline()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Data.lastUnixSec <= 0) { Data.lastUnixSec = now; return (0, 0, 0); }
        long elapsed = Math.Min(now - Data.lastUnixSec, 12 * 3600); // 上限12小時
        if (elapsed <= 0) return (0, 0, 0);

        IdleFormula.IdleRate(out long gps, out long eps, Data.maxStageCleared, Data.heroLevel);
        Data.gold += gps * elapsed;
        Data.exp += eps * elapsed;
        Data.lastUnixSec = now;
        return ((int)elapsed, gps * elapsed, eps * elapsed);
    }
}