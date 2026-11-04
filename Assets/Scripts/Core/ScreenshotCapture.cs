using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// F11 saves a PNG of the game view to a Screenshots folder (next to Assets in the editor, in the
// persistent data folder in a build). Creates itself before the first scene and survives scene loads.
public class ScreenshotCapture : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject(nameof(ScreenshotCapture));
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotCapture>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f11Key.wasPressedThisFrame) Capture();
    }

    static void Capture()
    {
#if UNITY_EDITOR
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
#else
        string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
#endif
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"Screenshot saved: {path}");
    }
}
