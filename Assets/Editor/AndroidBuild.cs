using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click Android build for sideloading on an Amazon Fire tablet: Babel > Build Android APK
// puts the main menu first in Build Settings, sets the player up for Fire OS (plain APK rather
// than an app bundle, 32- and 64-bit ARM, landscape only), then builds the enabled scenes into
// Builds/Android/<product name>.apk (git-ignored) and opens that folder. Fire tablets have no
// Play Store, so the APK is signed with the debug keystore unless one is set in Player Settings.
public static class AndroidBuild
{
    const string OutputFolder = "Builds/Android";
    const string MatchScenePath = "Assets/Scenes/Match.unity";

    [MenuItem("Babel/Build Android APK (Fire Tablet)", priority = 101)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play mode before building.");
            return;
        }
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("Android Build Support isn't installed; add it to this editor in Unity Hub (Installs > Add modules).");
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

        ConfigureForFireTablet();

        string apkPath = Path.Combine(OutputFolder, PlayerSettings.productName + ".apk");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        });

        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Android build succeeded in {summary.totalTime.TotalSeconds:0}s " +
                      $"({summary.totalSize / (1024f * 1024f):0.#} MB): {Path.GetFullPath(apkPath)}");
            EditorUtility.RevealInFinder(apkPath);
        }
        else
        {
            Debug.LogError($"Android build {summary.result} with {summary.totalErrors} error(s); see the Console for details.");
        }
    }

    // These are Android-only Player Settings, so they don't affect the Windows build.
    static void ConfigureForFireTablet()
    {
        NamedBuildTarget android = NamedBuildTarget.Android;

        // Sideloading needs an .apk; app bundles (.aab) are for store upload only.
        EditorUserBuildSettings.buildAppBundle = false;

        // Older Fire tablets run a 32-bit OS, newer ones 64-bit; ARM64 needs IL2CPP.
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

        if (string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(android)))
            PlayerSettings.SetApplicationIdentifier(android, "com.DefaultCompany.ProjectBabel");

        // The match is laid out for a wide screen: turn with the tablet, but never to portrait.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
    }
}
