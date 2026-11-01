using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Mixer groups share these names, and each group's exposed volume is the name plus "Volume".
public enum AudioChannel { Sfx, Ui, Music }

[Serializable]
public class AudioEntry
{
    public string key;
    public AudioChannel channel;
    [Tooltip("One is picked at random on each play.")]
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Random pitch change either way, so repeats don't grate. 0.1 is up to 10%.")]
    [Range(0f, 0.5f)] public float pitchVariance;
    [Tooltip("Seconds that must pass before this key plays again. Unscaled, so pauses count.")]
    [Min(0f)] public float cooldown;
}

// Every sound in the game by key. Lives in Resources so AudioManager can find it without a scene
// reference; after adding or renaming a key, regenerate AudioKeys from this asset's inspector.
[CreateAssetMenu(menuName = "Babel/Audio Library", fileName = "AudioLibrary")]
public class AudioLibrary : ScriptableObject
{
    public const string ResourcePath = "AudioLibrary";

    public AudioMixer mixer;
    public List<AudioEntry> entries = new();
}
