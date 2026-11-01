using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// The audio library's inspector: every key with a preview button, warnings for bad keys, and the
// button that regenerates AudioKeys so code can't mistype a key.
[CustomEditor(typeof(AudioLibrary))]
public class AudioLibraryEditor : Editor
{
    const string KeysPath = "Assets/Scripts/Core/AudioKeys.cs";
    const string AssetPath = "Assets/Resources/AudioLibrary.asset";

    AudioSource previewSource;

    [MenuItem("Babel/Data/Audio Library", priority = 30)]
    static void SelectLibrary()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    void OnDisable()
    {
        if (previewSource != null) DestroyImmediate(previewSource.gameObject);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AudioLibrary.mixer)));

        SerializedProperty entries = serializedObject.FindProperty(nameof(AudioLibrary.entries));
        for (int i = 0; i < entries.arraySize; i++)
            if (!DrawEntry(entries, i)) break;

        if (GUILayout.Button("Add key")) AddEntry(entries);
        serializedObject.ApplyModifiedProperties();

        var library = (AudioLibrary)target;
        DrawKeyProblems(library);
        DrawKeysFile(library);
    }

    // False when the entry was removed, since the indexes after it have moved.
    bool DrawEntry(SerializedProperty entries, int index)
    {
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        string key = entry.FindPropertyRelative(nameof(AudioEntry.key)).stringValue;

        using (new EditorGUILayout.HorizontalScope())
        {
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, string.IsNullOrEmpty(key) ? "(no key)" : key, true);
            if (GUILayout.Button("Preview", GUILayout.Width(60f))) Preview(((AudioLibrary)target).entries[index]);
            if (GUILayout.Button("Remove", GUILayout.Width(60f)))
            {
                entries.DeleteArrayElementAtIndex(index);
                return false;
            }
        }
        if (!entry.isExpanded) return true;

        EditorGUI.indentLevel++;
        foreach (string field in new[]
        {
            nameof(AudioEntry.key), nameof(AudioEntry.channel), nameof(AudioEntry.clips),
            nameof(AudioEntry.volume), nameof(AudioEntry.pitchVariance), nameof(AudioEntry.cooldown),
        })
            EditorGUILayout.PropertyField(entry.FindPropertyRelative(field));
        EditorGUI.indentLevel--;
        return true;
    }

    static void AddEntry(SerializedProperty entries)
    {
        entries.arraySize++;
        SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        entry.FindPropertyRelative(nameof(AudioEntry.key)).stringValue = "";
        entry.FindPropertyRelative(nameof(AudioEntry.channel)).enumValueIndex = (int)AudioChannel.Sfx;
        entry.FindPropertyRelative(nameof(AudioEntry.clips)).arraySize = 0;
        entry.FindPropertyRelative(nameof(AudioEntry.volume)).floatValue = 1f;
        entry.FindPropertyRelative(nameof(AudioEntry.pitchVariance)).floatValue = 0f;
        entry.FindPropertyRelative(nameof(AudioEntry.cooldown)).floatValue = 0f;
        entry.isExpanded = true;
    }

    // Ignores the mixer, whose groups only sound in Play mode.
    void Preview(AudioEntry entry)
    {
        AudioClip[] clips = entry.clips?.Where(clip => clip != null).ToArray();
        if (clips == null || clips.Length == 0) return;

        if (previewSource == null)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags("Audio preview", HideFlags.HideAndDontSave, typeof(AudioSource));
            previewSource = go.GetComponent<AudioSource>();
        }
        previewSource.Stop();
        previewSource.clip = clips[Random.Range(0, clips.Length)];
        previewSource.volume = entry.volume;
        previewSource.pitch = 1f + Random.Range(-entry.pitchVariance, entry.pitchVariance);
        previewSource.Play();
    }

    static void DrawKeyProblems(AudioLibrary library)
    {
        var names = new HashSet<string>();
        foreach (AudioEntry entry in library.entries)
        {
            string name = AudioRules.KeyToIdentifier(entry.key);
            if (name.Length == 0)
                EditorGUILayout.HelpBox($"'{entry.key}' can't become a key name.", MessageType.Error);
            else if (!names.Add(name))
                EditorGUILayout.HelpBox($"'{entry.key}' clashes with another key as AudioKeys.{name}.", MessageType.Error);
        }
    }

    static void DrawKeysFile(AudioLibrary library)
    {
        string source = KeysSource(library);
        bool current = File.Exists(KeysPath) && File.ReadAllText(KeysPath).Replace("\r\n", "\n") == source;
        if (current) return;

        EditorGUILayout.HelpBox("AudioKeys doesn't match this library.", MessageType.Warning);
        if (!GUILayout.Button("Generate AudioKeys")) return;
        File.WriteAllText(KeysPath, source);
        AssetDatabase.ImportAsset(KeysPath);
    }

    static string KeysSource(AudioLibrary library)
    {
        var source = new StringBuilder();
        source.Append("// Generated from AudioLibrary by its inspector. Don't edit by hand.\n");
        source.Append("public static class AudioKeys\n{\n");
        var names = new HashSet<string>();
        foreach (AudioEntry entry in library.entries)
        {
            string name = AudioRules.KeyToIdentifier(entry.key);
            if (name.Length > 0 && names.Add(name)) source.Append($"    public const string {name} = \"{entry.key}\";\n");
        }
        source.Append("}\n");
        return source.ToString();
    }
}
