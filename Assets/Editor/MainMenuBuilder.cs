using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Babel > Build Main Menu Scene creates the menu scene (a camera and the MainMenu component, which
// builds its UI at runtime) and puts it first in Build Settings, so players start on it. In the
// editor, Play also starts from the menu while Babel > Play From Main Menu is ticked.
[InitializeOnLoad]
public static class MainMenuBuilder
{
    public const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string PlayFromMenuKey = "Babel.PlayFromMainMenu";
    const string PlayFromMenuItem = "Babel/Play From Main Menu";

    static MainMenuBuilder()
    {
        EditorApplication.delayCall += ApplyPlayModeStartScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // A play-mode test run enters Play from a bootstrap scene the Test Runner has just opened
    // (Assets/InitTestScene<guid>.unity). Starting from the menu instead would leave the run
    // waiting forever for tests that never begin, so the start scene steps aside for it.
    static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode && SceneManager.GetActiveScene().name.StartsWith("InitTestScene"))
            EditorSceneManager.playModeStartScene = null;
        else if (change == PlayModeStateChange.EnteredEditMode)
            ApplyPlayModeStartScene();
    }

    [MenuItem("Babel/Build Main Menu Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);

        var menu = new SerializedObject(new GameObject("Main Menu").AddComponent<MainMenu>());
        menu.FindProperty("data").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MatchData>("Assets/Data/GreyBoxMatch.asset");
        menu.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutMenuFirst();
        ApplyPlayModeStartScene();
    }

    // Keeps the menu scene at the top of Build Settings, adding it if it exists but isn't listed.
    // Returns false when there's no menu scene yet.
    public static bool PutMenuFirst()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return false;
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return true;
    }

    static bool PlayFromMenu => EditorPrefs.GetBool(PlayFromMenuKey, true);

    [MenuItem(PlayFromMenuItem, priority = 50)]
    static void TogglePlayFromMenu()
    {
        EditorPrefs.SetBool(PlayFromMenuKey, !PlayFromMenu);
        ApplyPlayModeStartScene();
    }

    [MenuItem(PlayFromMenuItem, true)]
    static bool TogglePlayFromMenuValidate()
    {
        Menu.SetChecked(PlayFromMenuItem, PlayFromMenu);
        return true;
    }

    static void ApplyPlayModeStartScene() =>
        EditorSceneManager.playModeStartScene = PlayFromMenu ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
}
