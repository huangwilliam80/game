using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        // 關閉垂直同步以允許手動限制幀率
        QualitySettings.vSyncCount = 0;
        // 鎖定 60 FPS 確保放置塔防戰鬥流暢
        Application.targetFrameRate = 60;
    }
}