using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Build de desenvolvimento para Windows, para testar a LAN com várias janelas ou com amigos.
    /// Batchmode: -executeMethod Game.EditorTools.DevBuild.BuildWindows -buildPath &lt;pasta&gt;
    /// </summary>
    public static class DevBuild
    {
        [MenuItem("Game/Build/Development Build (Windows)")]
        public static void BuildWindows()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildPath");
            string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/Dev";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ArenaSceneSetup.ArenaScenePath },
                locationPathName = System.IO.Path.Combine(folder, "Joiigo.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[DevBuild] {report.summary.result} em {options.locationPathName} ({report.summary.totalErrors} erros)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
