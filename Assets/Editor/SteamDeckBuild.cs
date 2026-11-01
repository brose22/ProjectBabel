using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click Steam Deck build: Babel > Build Steam Deck (Linux) puts the main menu first in Build
// Settings, sets the Linux player up for the Deck (SteamOS is x86-64 Linux,
// Mono scripting so only the Mono Linux module is needed), then builds the enabled scenes into
// Builds/SteamDeck/<product name>.x86_64 (git-ignored) and opens that folder. To run it on the
// Deck, copy the whole folder over and either launch it from Desktop Mode, or in Steam add it as
// a Non-Steam Game (make the file executable first: chmod +x <product name>.x86_64).
public static class SteamDeckBuild
{
    const string OutputFolder = "Builds/SteamDeck";
    const string MatchScenePath = "Assets/Scenes/Match.unity";

    [MenuItem("Babel/Build Steam Deck (Linux)", priority = 102)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play mode before building.");
            return;
        }
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
        {
            Debug.LogError("Linux Build Support isn't installed; add 'Linux Build Support (Mono)' to this editor in Unity Hub (Installs > Add modules).");
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

        ConfigureForSteamDeck();

        string exePath = Path.Combine(OutputFolder, PlayerSettings.productName + ".x86_64");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneLinux64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        });

        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Steam Deck build succeeded in {summary.totalTime.TotalSeconds:0}s " +
                      $"({summary.totalSize / (1024f * 1024f):0.#} MB): {Path.GetFullPath(exePath)}");
            EditorUtility.RevealInFinder(exePath);
        }
        else
        {
            Debug.LogError($"Steam Deck build {summary.result} with {summary.totalErrors} error(s); see the Console for details.");
        }
    }

    // Standalone Player Settings are shared with the Windows build, so only the scripting backend
    // is pinned here (it is already Mono; IL2CPP for Linux would need a second module). The
    // project's fullscreen-window mode already fills the Deck's 1280x800 screen.
    static void ConfigureForSteamDeck()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
    }
}
