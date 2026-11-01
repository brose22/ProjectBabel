using System;
using System.Collections;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

// Streams the Game view (Screen Space Overlay UI included) to the Macro Pad server on
// 127.0.0.1:8765, which relays it to the tablet. Editor-only (this folder is never in a player
// build), view only, and off by default because capturing skews profiler numbers.
// Babel > Stream Game View to Tablet toggles it; it can be flipped during Play mode.
// Per frame: ScreenCapture into a render texture at the end of the frame, a blit down to
// ~800 px wide, an async GPU readback, a JPEG encode, then a background thread POSTs it. The
// sender keeps only the newest frame and stays silent when the server isn't running.
[InitializeOnLoad]
public static class GameViewStreamer
{
    const string MenuPath = "Babel/Stream Game View to Tablet";
    const string PrefKey = "Babel.StreamGameView";
    const string Url = "http://127.0.0.1:8765/api/frame";
    const int Width = 800;
    const int MaxHeight = 600;
    const int JpegQuality = 70;
    const float MaxFps = 60f;
    // Seconds to leave the server alone after a failed send (it isn't running).
    const double RetryDelay = 2;

    static GameObject host;

    static GameViewStreamer()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // A domain reload while already playing (script recompile) loses the host object.
        if (EditorApplication.isPlaying) EditorApplication.delayCall += Sync;
    }

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath, priority = 103)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Sync();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode) Sync();
        else if (change == PlayModeStateChange.ExitingPlayMode) Stop();
    }

    static void Sync()
    {
        if (Enabled && EditorApplication.isPlaying) Start();
        else Stop();
    }

    static void Start()
    {
        if (host != null) return;
        host = new GameObject("GameViewStreamer") { hideFlags = HideFlags.HideAndDontSave };
        host.AddComponent<Capturer>();
    }

    static void Stop()
    {
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
        host = null;
    }

    class Capturer : MonoBehaviour
    {
        static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };

        RenderTexture screen, small;
        // Readbacks land a frame or two late, so keep a few in flight to stay at the game's frame rate.
        int readbacksInFlight;
        float nextCapture;

        // Latest-wins hand-off to the sender thread.
        readonly object gate = new object();
        readonly AutoResetEvent ready = new AutoResetEvent(false);
        byte[] pending;
        Thread sender;
        volatile bool running = true;

        void OnEnable()
        {
            sender = new Thread(SendLoop) { IsBackground = true, Name = "GameViewStreamer" };
            sender.Start();
            StartCoroutine(CaptureLoop());
        }

        void OnDisable()
        {
            running = false;
            ready.Set();
            if (screen != null) screen.Release();
            if (small != null) small.Release();
            DestroyImmediate(screen);
            DestroyImmediate(small);
        }

        IEnumerator CaptureLoop()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                Vector2 gameSize = Handles.GetMainGameViewSize();
                if (readbacksInFlight >= 3 || Time.unscaledTime < nextCapture || gameSize.x < 2 || gameSize.y < 2) continue;
                nextCapture = Time.unscaledTime + 1f / MaxFps;
                Capture(gameSize);
            }
        }

        void Capture(Vector2 size)
        {
            // Match the Game view's aspect; the tablet letterboxes it.
            int width = Width;
            int height = Mathf.Clamp(Mathf.RoundToInt(width * size.y / size.x), 2, MaxHeight);
            if (small == null || small.width != width || small.height != height)
            {
                if (small != null) { small.Release(); DestroyImmediate(small); }
                small = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            }
            if (screen == null || screen.width != (int)size.x || screen.height != (int)size.y)
            {
                if (screen != null) { screen.Release(); DestroyImmediate(screen); }
                screen = new RenderTexture((int)size.x, (int)size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            }

            ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
            // The capture comes out upside down; flip it while shrinking.
            Graphics.Blit(screen, small, new Vector2(1, -1), new Vector2(0, 1));
            readbacksInFlight++;
            AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, OnReadback);
        }

        void OnReadback(AsyncGPUReadbackRequest request)
        {
            readbacksInFlight--;
            if (!running || request.hasError || small == null) return;
            NativeArray<byte> data = request.GetData<byte>();
            byte[] jpeg = ImageConversion.EncodeNativeArrayToJPG(
                data, GraphicsFormat.R8G8B8A8_UNorm, (uint)request.width, (uint)request.height, 0, JpegQuality).ToArray();
            lock (gate) pending = jpeg;
            ready.Set();
        }

        void SendLoop()
        {
            double retryAt = 0;
            while (running)
            {
                ready.WaitOne();
                byte[] jpeg;
                lock (gate) { jpeg = pending; pending = null; }
                if (!running) return;
                if (jpeg == null) continue;
                double now = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
                if (now < retryAt) continue;
                try
                {
                    using (var content = new ByteArrayContent(jpeg))
                    {
                        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                        using (var message = new HttpRequestMessage(HttpMethod.Post, Url) { Content = content })
                        {
                            message.Headers.Add("X-Macropad", "1");
                            using (var response = Client.SendAsync(message).GetAwaiter().GetResult())
                                if (!response.IsSuccessStatusCode) retryAt = now + RetryDelay;
                        }
                    }
                }
                catch (Exception)
                {
                    retryAt = now + RetryDelay;
                }
            }
        }
    }
}
