using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CardGame.EditorTools
{
    /// <summary>
    /// 出 Windows x64 包。菜单：Build → Build Windows x64
    /// 命令行：Tuanjie.exe -batchmode -quit -projectPath . -executeMethod CardGame.EditorTools.BuildWindows.BuildFromCommandLine
    /// </summary>
    public static class BuildWindows
    {
        const string OutDir = "Build/Windows";
        const string OutExe = "Build/Windows/CardGame.exe";

        [MenuItem("Build/Build Windows x64")]
        public static void BuildFromMenu()
        {
            bool ok = Run();
            if (!ok)
                EditorUtility.DisplayDialog("构建失败", "看 Console 里的红字。", "好");
        }

        public static void BuildFromCommandLine()
        {
            bool ok = Run();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Run()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] Build Settings 里没有启用的场景。");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutExe));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log("[Build] 场景 " + scenes.Length + " 个 → " + OutExe);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary sum = report.summary;

            Debug.Log(string.Format("[Build] 结果 {0}  大小 {1:N1} MB  耗时 {2:F1}s  错误 {3}",
                sum.result, sum.totalSize / 1048576.0, sum.totalTime.TotalSeconds, sum.totalErrors));

            return sum.result == BuildResult.Succeeded;
        }
    }
}
