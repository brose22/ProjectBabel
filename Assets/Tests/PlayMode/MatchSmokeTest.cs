using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// One smoke test: load the game's first scene, let the AI player start a run and play until the
// first wave ends, and fail if anything logged an error or threw on the way.
//
// The game scripts have no asmdef, so they compile into Assembly-CSharp, which a test assembly
// can't reference. The few game members the test needs are therefore reached by reflection
// (see Game below); a rename there fails the test with a message naming the missing member.
public class MatchSmokeTest
{
    const float TimeoutSeconds = 60f;
    const float AiSpeed = 32f;
    const int Seed = 12345;
    const string MatchesStartedKey = "settings.matchesStarted";

    readonly List<string> errors = new();
    int wavesEnded;
    Action<int> onWaveEnded;

    string saveFolder;
    float timeScale;
    int targetFrameRate;
    int vSyncCount;
    bool runInBackground;
    bool hadMatchesStarted;
    int matchesStarted;
    bool writeCsv;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        errors.Clear();
        wavesEnded = 0;

        timeScale = Time.timeScale;
        targetFrameRate = Application.targetFrameRate;
        vSyncCount = QualitySettings.vSyncCount;
        runInBackground = Application.runInBackground;
        hadMatchesStarted = PlayerPrefs.HasKey(MatchesStartedKey);
        matchesStarted = PlayerPrefs.GetInt(MatchesStartedKey, 0);
        writeCsv = (bool)Game.GetStatic("MatchStats", "WriteCsvEnabled");

        // Uncapped frames, so 32x game speed isn't held back by vsync or a frame cap.
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Application.runInBackground = true;

        // Saves go to a scratch folder from before the first scene loads until the last one is gone.
        saveFolder = Path.Combine(Application.temporaryCachePath, "playmode-smoke");
        if (Directory.Exists(saveFolder)) Directory.Delete(saveFolder, true);
        Directory.CreateDirectory(saveFolder);
        RedirectSaves();
        Game.CallStatic("RunState", "Clear");
        Game.CallStatic("Profile", "Clear");

        Application.logMessageReceived += OnLog;
        onWaveEnded = _ => wavesEnded++;
        Game.Event("GameEvents", "WaveEnded").AddEventHandler(null, onWaveEnded);
        yield break;
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator FirstSceneLoadsAndTheAiPlaysAWave()
    {
        // The test reports logged errors itself, with every message and stack trace, instead of
        // the runner stopping at the first one. (The runner resets this for each test, so it is
        // set here and not in SetUp.)
        LogAssert.ignoreFailingMessages = true;

        string firstScene = SceneUtility.GetScenePathByBuildIndex(0);
        Assert.IsNotEmpty(firstScene, "No scene is enabled in the build settings.");
        yield return SceneManager.LoadSceneAsync(0);
        for (int i = 0; i < 5; i++) yield return null;
        AssertNoErrors($"loading {firstScene}");

        // The same call the menu's AI Run screen makes: it loads the Match scene on a fresh run,
        // which the AI then plays. It redirects the saves itself, to the ai folder its batches use.
        object options = Activator.CreateInstance(Game.Type("AiOptions"));
        Game.SetField(options, "runs", 1);
        Game.SetField(options, "speed", AiSpeed);
        Game.SetField(options, "seed", Seed);
        Game.CallStatic("AiPlayer", "Begin", options);

        float start = Time.realtimeSinceStartup;
        while (wavesEnded == 0 && errors.Count == 0 && Time.realtimeSinceStartup - start < TimeoutSeconds)
            yield return null;
        float took = Time.realtimeSinceStartup - start;

        AssertNoErrors("the AI playing its first wave");
        Assert.Greater(wavesEnded, 0,
            $"No wave ended within {TimeoutSeconds:0}s (scene {SceneManager.GetActiveScene().name}, time scale {Time.timeScale}). " +
            "An unfocused editor throttles play mode; run with the editor focused or headless.");
        Debug.Log($"[Smoke] First wave ended after {took:0.0}s.");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Destroying the AI player ends its save redirection, so point the saves back at the
        // scratch folder until the match is unloaded and can't write any more.
        var ai = Game.GetStatic("AiPlayer", "Instance") as Component;
        if (ai != null) UnityEngine.Object.Destroy(ai.gameObject);
        yield return null;
        RedirectSaves();
        Game.SetStatic("MatchStats", "WriteCsvEnabled", false);

        Scene empty = SceneManager.CreateScene("SmokeTestEmpty");
        SceneManager.SetActiveScene(empty);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        yield return null;

        Game.Event("GameEvents", "WaveEnded").RemoveEventHandler(null, onWaveEnded);
        Application.logMessageReceived -= OnLog;
        LogAssert.ignoreFailingMessages = false;

        Game.CallStatic("RunState", "Clear");
        Game.CallStatic("Profile", "Clear");
        Game.SetStatic("RunSave", "PathOverride", null);
        Game.SetStatic("ProfileSave", "PathOverride", null);
        Game.SetStatic("MatchStats", "WriteCsvEnabled", writeCsv);

        Time.timeScale = timeScale;
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = vSyncCount;
        Application.runInBackground = runInBackground;
        if (hadMatchesStarted) PlayerPrefs.SetInt(MatchesStartedKey, matchesStarted);
        else PlayerPrefs.DeleteKey(MatchesStartedKey);

        if (Directory.Exists(saveFolder)) Directory.Delete(saveFolder, true);
    }

    void RedirectSaves()
    {
        Game.SetStatic("RunSave", "PathOverride", Path.Combine(saveFolder, "run.json"));
        Game.SetStatic("ProfileSave", "PathOverride", Path.Combine(saveFolder, "profile.json"));
    }

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
            errors.Add($"{type}: {message}\n{stackTrace}");
    }

    void AssertNoErrors(string during)
    {
        if (errors.Count > 0)
            Assert.Fail($"{errors.Count} error(s) logged during {during}:\n{string.Join("\n", errors)}");
    }

    // The game's types, looked up by name in Assembly-CSharp.
    static class Game
    {
        const BindingFlags Statics = BindingFlags.Public | BindingFlags.Static;

        public static Type Type(string name)
        {
            Type type = System.Type.GetType(name + ", Assembly-CSharp");
            Assert.IsNotNull(type, $"The game has no type called {name}; update MatchSmokeTest.");
            return type;
        }

        public static object GetStatic(string type, string property) => Property(type, property).GetValue(null);

        public static void SetStatic(string type, string property, object value) => Property(type, property).SetValue(null, value);

        public static void CallStatic(string type, string method, params object[] args)
        {
            MethodInfo info = Type(type).GetMethod(method, Statics);
            Assert.IsNotNull(info, $"{type} has no public static method {method}; update MatchSmokeTest.");
            info.Invoke(null, args);
        }

        public static EventInfo Event(string type, string name)
        {
            EventInfo info = Type(type).GetEvent(name, Statics);
            Assert.IsNotNull(info, $"{type} has no public static event {name}; update MatchSmokeTest.");
            return info;
        }

        public static void SetField(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no public field {field}; update MatchSmokeTest.");
            info.SetValue(target, value);
        }

        static PropertyInfo Property(string type, string property)
        {
            PropertyInfo info = Type(type).GetProperty(property, Statics);
            Assert.IsNotNull(info, $"{type} has no public static property {property}; update MatchSmokeTest.");
            return info;
        }
    }
}
