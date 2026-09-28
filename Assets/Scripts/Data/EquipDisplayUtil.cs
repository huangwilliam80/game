using UnityEngine;

public static class EquipDisplayUtil
{
    public static Color QualityColor(int quality)
    {
        switch ((Quality)quality) {
            case Quality.Common: return new Color(.8f,.8f,.8f); 
            case Quality.Fine: return new Color(.55f,.9f,.5f);
            case Quality.Rare: return new Color(.4f,.7f,1f); 
            case Quality.Epic: return new Color(.75f,.45f,1f);
            case Quality.Legend: return new Color(1f,.75f,.25f); 
            default: return Color.white;
        }
    }

    public static string QualityName(int quality)
    {
        switch ((Quality)quality) {
            case Quality.Common: return "普通"; 
            case Quality.Fine: return "精良";
            case Quality.Rare: return "稀有"; 
            case Quality.Epic: return "史詩";
            case Quality.Legend: return "傳說"; 
            default: return "普通";
        }
    }

    public static string Name(EquipmentInstance eq)
    {
        var def = EquipmentDatabase.Get(eq.defId);
        return def != null ? def.name : "未知裝備";
    }
}