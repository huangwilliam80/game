#if UNITY_EDITOR
using System.Diagnostics;       // Process, ProcessStartInfo
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug; // 解決 CS0104：明確指定使用 UnityEngine.Debug

/// <summary>
/// 選單工具：Tools/Git Sync - Commit & Push
/// 新手友善的一鍵 Git 同步工具，自動加總變更並提交到遠端。
/// </summary>
public static class GitSync
{
    private const string MenuPath = "Tools/Git Sync/Commit && Push";

    [MenuItem(MenuPath)]
    public static void CommitAndPush()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;

        if (!RunGit(projectPath, "rev-parse --is-inside-work-tree", out _))
        {
            EditorUtility.DisplayDialog("Git 未初始化",
                "此專案尚未建立 Git 倉庫。\n請先在終端機執行:\ngit init\ngit remote add origin <你的 repo URL>",
                "好");
            return;
        }

        RunGit(projectPath, "add -A", out _);

        if (!RunGit(projectPath, "status --porcelain", out string status) ||
            string.IsNullOrWhiteSpace(status))
        {
            EditorUtility.DisplayDialog("Git Sync", "沒有需要提交的變更。", "好");
            return;
        }

        string message = $"Update: {DateTime.Now:yyyy-MM-dd HH:mm}";
        if (!RunGit(projectPath, $"commit -m \"{message}\"", out string commitLog))
        {
            Debug.LogError($"[GitSync] 提交失敗:\n{commitLog}");
            return;
        }

        if (RunGit(projectPath, "remote get-url origin", out _) &&
            !RunGit(projectPath, "push origin HEAD", out string pushLog))
        {
            Debug.LogWarning($"[GitSync] 推送失敗(本機提交已完成):\n{pushLog}");
        }
        else
        {
            Debug.Log("[GitSync] 提交並推送完成 ✅");
        }
    }

    [MenuItem("Tools/Git Sync/Status")]
    public static void ShowStatus()
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        RunGit(projectPath, "status -sb", out string output);
        Debug.Log($"[GitSync] 狀態:\n{output}");
    }

    /// <summary>執行 git 命令，回傳是否成功，並取得輸出。</summary>
    private static bool RunGit(string workDir, string arguments, out string output)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "git",
            Arguments              = arguments,
            WorkingDirectory       = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        try
        {
            using var process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            output = stdout + stderr;
            return process.ExitCode == 0;
        }
        catch (Exception e)
        {
            output = e.Message;
            return false;
        }
    }
}
#endif
