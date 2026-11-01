using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Plays sounds by key from the AudioLibrary: AudioManager.Play(AudioKeys.EnemyKilled). Creates
// itself before the first scene and survives scene loads, so music carries across match reloads.
// A missing key or empty entry warns once and stays silent, so calls can land before their sounds.
public class AudioManager : MonoBehaviour
{
    // Sounds past this many at once are dropped, not queued.
    const int VoiceCount = 24;
    const float MusicFadeSeconds = 1f;

    static AudioManager instance;

    readonly Dictionary<string, AudioEntry> entries = new();
    readonly Dictionary<AudioChannel, AudioMixerGroup> groups = new();
    readonly Dictionary<string, float> lastPlayed = new();
    readonly HashSet<string> warned = new();
    readonly List<AudioSource> voices = new();
    // Its own generator, so audio never shifts gameplay's UnityEngine.Random sequence.
    readonly System.Random random = new();

    AudioMixer mixer;
    // The two swap on each change of track: one fades in as the other fades out.
    AudioSource musicIn;
    AudioSource musicOut;
    float musicTarget;
    string musicKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject(nameof(AudioManager));
        DontDestroyOnLoad(go);
        // The scenes' cameras have none.
        go.AddComponent<AudioListener>();
        instance = go.AddComponent<AudioManager>();
    }

    public static void Play(string key)
    {
        if (instance) instance.PlayVoice(key);
    }

    // Crossfades to the key's track. Asking for the track already playing changes nothing.
    public static void PlayMusic(string key)
    {
        if (instance) instance.SwitchMusic(key);
    }

    public static void StopMusic()
    {
        if (instance) instance.SwitchMusic(null);
    }

    // Pushes the saved channel volumes to the mixer.
    public static void ApplyVolumes()
    {
        if (!instance || !instance.mixer) return;
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            instance.mixer.SetFloat(channel + "Volume", AudioRules.VolumeToDecibels(GameSettings.ChannelVolume(channel)));
    }

    void Awake()
    {
        var library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
        if (library == null)
        {
            Debug.LogWarning($"AudioManager: no AudioLibrary at Resources/{AudioLibrary.ResourcePath}, the game will be silent.");
            return;
        }

        foreach (AudioEntry entry in library.entries)
            if (!string.IsNullOrEmpty(entry.key)) entries[entry.key] = entry;

        mixer = library.mixer;
        if (mixer != null)
            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                AudioMixerGroup[] matches = mixer.FindMatchingGroups(channel.ToString());
                if (matches.Length > 0) groups[channel] = matches[0];
            }

        for (int i = 0; i < VoiceCount; i++) voices.Add(NewSource());
        musicIn = NewSource();
        musicOut = NewSource();
        musicIn.loop = musicOut.loop = true;
        AudioEvents.Subscribe();
    }

    // The mixer ignores SetFloat during Awake, so the saved volumes wait until here.
    void Start() => ApplyVolumes();

    AudioSource NewSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    void Update()
    {
        if (musicIn == null) return;
        float step = Time.unscaledDeltaTime / MusicFadeSeconds;
        musicIn.volume = Mathf.MoveTowards(musicIn.volume, musicTarget, step);
        musicOut.volume = Mathf.MoveTowards(musicOut.volume, 0f, step);
        if (musicOut.isPlaying && musicOut.volume <= 0f) musicOut.Stop();
        if (musicTarget <= 0f && musicIn.isPlaying && musicIn.volume <= 0f) musicIn.Stop();
    }

    void PlayVoice(string key)
    {
        if (!TryGetClip(key, out AudioEntry entry, out AudioClip clip)) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(key, out float last) && !AudioRules.CooldownOver(last, now, entry.cooldown)) return;

        AudioSource voice = voices.Find(source => !source.isPlaying);
        if (voice == null) return;

        lastPlayed[key] = now;
        Route(voice, entry.channel);
        voice.clip = clip;
        voice.volume = entry.volume;
        voice.pitch = 1f + ((float)random.NextDouble() * 2f - 1f) * entry.pitchVariance;
        voice.Play();
    }

    void SwitchMusic(string key)
    {
        if (key == musicKey || musicIn == null) return;

        AudioEntry entry = null;
        AudioClip clip = null;
        if (key != null && !TryGetClip(key, out entry, out clip)) return;

        musicKey = key;
        (musicIn, musicOut) = (musicOut, musicIn);
        musicTarget = entry?.volume ?? 0f;
        if (clip == null) return;

        Route(musicIn, entry.channel);
        musicIn.clip = clip;
        musicIn.volume = 0f;
        musicIn.Play();
    }

    bool TryGetClip(string key, out AudioEntry entry, out AudioClip clip)
    {
        clip = null;
        if (key == null || !entries.TryGetValue(key, out entry))
        {
            entry = null;
            WarnOnce(key, $"AudioManager: no entry for key '{key}'.");
            return false;
        }
        if (entry.clips != null && entry.clips.Length > 0) clip = entry.clips[random.Next(entry.clips.Length)];
        if (clip != null) return true;

        WarnOnce(key, $"AudioManager: key '{key}' has no clip assigned.");
        return false;
    }

    void WarnOnce(string key, string message)
    {
        if (warned.Add(key ?? "")) Debug.LogWarning(message);
    }

    void Route(AudioSource source, AudioChannel channel) =>
        source.outputAudioMixerGroup = groups.TryGetValue(channel, out AudioMixerGroup group) ? group : null;
}
