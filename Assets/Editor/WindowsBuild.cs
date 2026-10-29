using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click Windows build: Babel > Build Windows Player puts the main menu first in Build
// Settings (the player starts there), then builds the enabled scenes
// into Builds/Windows/<product name>.exe (git-ignored) and opens that folder.
public static class WindowsBuild
{
    const string OutputFolder = "Builds/Windows";
    const string MatchScenePath = "Assets/Scenes/Match.unity";

    [MenuItem("Babel/Build Windows Player", priority = 100)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play mode before building.");
            return;
        }

        // The player starts on the first scene, which must be the main menu.
        if (!MainMenuBuilder.PutMenuFirst())
        {
            Debug.LogError("No main menu scene; run Babel > Build Main Menu Scene first.");
            return;
        }
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (!scenes.Contains(MatchScenePath))
        {
            Debug.LogError("The match scene isn't enabled in Build Settings; run Babel > Build Grey-Box Match Scene first.");
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
