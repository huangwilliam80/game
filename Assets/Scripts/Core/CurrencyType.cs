/// <summary>
/// 遊戲內貨幣類型。
/// 獨立成檔：避免與 GameEvents.cs 或其他檔案重複定義（CS0101）。
/// 注意：此檔「只」放 CurrencyType，不要放 BattleResult（BattleResult 已在其他檔案定義）。
/// </summary>
public enum CurrencyType
{
    Gold,           // 金幣
    Exp,            // 經驗
    SpiritCrystal,  // 靈玉
    ForgeShard      // 鍛造碎片
}