using System;
using System.IO;
using System.Text;

// Where a loaded save came from.
public enum SaveSource
{
    None,
    Main,
    Backup,
}

// Reads and writes a save file so a crash mid-write can't destroy it. A save is written to a temp
// file beside the real one and then swapped in, and the file it replaces is kept as a backup.
// Loading falls back to the backup when the main file is missing or can't be used.
public static class SaveFiles
{
    public const string BackupSuffix = ".bak";
    public const string TempSuffix = ".tmp";

    public static string BackupPath(string path) => path + BackupSuffix;
    public static string TempPath(string path) => path + TempSuffix;

    // True if the main file or its backup is there.
    public static bool Exists(string path) => File.Exists(path) || File.Exists(BackupPath(path));

    // Writes the whole of contents to a temp file, flushed to disk, then swaps it in for the file
    // at path. The file it replaces becomes the backup. Throws what System.IO throws; the file at
    // path is then still the old one.
    public static void Write(string path, string contents)
    {
        string temp = TempPath(path);
        byte[] bytes = new UTF8Encoding(false).GetBytes(contents ?? "");
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, BackupPath(path));
        else File.Move(temp, path);
    }

    // Loads the main file, or the backup if the main file is missing or unusable. parse turns a
    // file's text into the save, and returns null or throws if the text can't be used. Returns
    // which file the value came from, or None with a null value if neither could be used.
    public static SaveSource Load<T>(string path, Func<string, T> parse, out T value) where T : class
    {
        if (TryRead(path, parse, out value)) return SaveSource.Main;
        if (TryRead(BackupPath(path), parse, out value)) return SaveSource.Backup;
        return SaveSource.None;
    }

    // Deletes the main file, its backup and any temp file left by an interrupted write.
    public static void Delete(string path)
    {
        foreach (string file in new[] { path, BackupPath(path), TempPath(path) })
            if (File.Exists(file)) File.Delete(file);
    }

    static bool TryRead<T>(string path, Func<string, T> parse, out T value) where T : class
    {
        value = null;
        if (!File.Exists(path)) return false;
        try
        {
            value = parse(File.ReadAllText(path));
        }
        catch (Exception)
        {
            value = null;
        }
        return value != null;
    }
}
