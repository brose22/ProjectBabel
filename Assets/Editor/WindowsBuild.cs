using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click Windows build: Babel > Build Windows Player builds the enabled scenes in Build
// Settings into Builds/Windows/<product name>.exe (git-ignored), then opens that folder.
public static class WindowsBuild
{
    const string OutputFolder = "Builds/Windows";

    [MenuItem("Babel/Build Windows Player", priority = 100)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play mode before building.");
            return;
        }

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("No enabled scenes in Build Settings; run Babel > Build Grey-Box Match Scene first.");
            return;
        }

        string exePath = Path.Combine(OutputFolder, PlayerSettings.productName + ".exe");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Windows build succeeded in {summary.totalTime.TotalSeconds:0}s " +
                      $"({summary.totalSize / (1024f * 1024f):0.#} MB): {Path.GetFullPath(exePath)}");
            EditorUtility.RevealInFinder(exePath);
        }
        else
        {
            Debug.LogError($"Windows build {summary.result} with {summary.totalErrors} error(s); see the Console for details.");
        }
    }
}
