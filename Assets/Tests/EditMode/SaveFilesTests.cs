using System;
using System.IO;
using NUnit.Framework;

public class SaveFilesTests
{
    string folder;
    string path;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "babel-save-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, "save.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    // A save is usable when it starts with "ok"; "throw" stands for text the parser chokes on.
    static string Parse(string text)
    {
        if (text.StartsWith("throw")) throw new FormatException(text);
        return text.StartsWith("ok") ? text : null;
    }

    [Test]
    public void Write_CreatesTheFileWithNoBackupOrTempLeftOver()
    {
        SaveFiles.Write(path, "ok 1");
        Assert.That(File.ReadAllText(path), Is.EqualTo("ok 1"));
        Assert.That(File.Exists(SaveFiles.BackupPath(path)), Is.False);
        Assert.That(File.Exists(SaveFiles.TempPath(path)), Is.False);
    }

    [Test]
    public void Write_KeepsThePreviousFileAsTheBackup()
    {
        SaveFiles.Write(path, "ok 1");
        SaveFiles.Write(path, "ok 2");
        SaveFiles.Write(path, "ok 3");
        Assert.That(File.ReadAllText(path), Is.EqualTo("ok 3"));
        Assert.That(File.ReadAllText(SaveFiles.BackupPath(path)), Is.EqualTo("ok 2"));
        Assert.That(File.Exists(SaveFiles.TempPath(path)), Is.False);
    }

    [Test]
    public void Write_ReplacesATempFileLeftByAnInterruptedWrite()
    {
        SaveFiles.Write(path, "ok 1");
        File.WriteAllText(SaveFiles.TempPath(path), "half a sa");
        SaveFiles.Write(path, "ok 2");
        Assert.That(File.ReadAllText(path), Is.EqualTo("ok 2"));
        Assert.That(File.Exists(SaveFiles.TempPath(path)), Is.False);
    }

    [Test]
    public void Load_PrefersTheMainFile()
    {
        SaveFiles.Write(path, "ok old");
        SaveFiles.Write(path, "ok new");
        Assert.That(SaveFiles.Load(path, Parse, out string value), Is.EqualTo(SaveSource.Main));
        Assert.That(value, Is.EqualTo("ok new"));
    }

    [TestCase("bad")]
    [TestCase("throw")]
    [TestCase("")]
    public void Load_FallsBackToTheBackupWhenTheMainFileIsUnusable(string broken)
    {
        SaveFiles.Write(path, "ok old");
        SaveFiles.Write(path, "ok new");
        File.WriteAllText(path, broken);
        Assert.That(SaveFiles.Load(path, Parse, out string value), Is.EqualTo(SaveSource.Backup));
        Assert.That(value, Is.EqualTo("ok old"));
    }

    [Test]
    public void Load_FallsBackToTheBackupWhenTheMainFileIsMissing()
    {
        SaveFiles.Write(path, "ok old");
        SaveFiles.Write(path, "ok new");
        File.Delete(path);
        Assert.That(SaveFiles.Exists(path), Is.True);
        Assert.That(SaveFiles.Load(path, Parse, out string value), Is.EqualTo(SaveSource.Backup));
        Assert.That(value, Is.EqualTo("ok old"));
    }

    [Test]
    public void Load_FindsNothingWhenBothFilesAreUnusable()
    {
        File.WriteAllText(path, "bad");
        File.WriteAllText(SaveFiles.BackupPath(path), "throw");
        Assert.That(SaveFiles.Load(path, Parse, out string value), Is.EqualTo(SaveSource.None));
        Assert.That(value, Is.Null);
    }

    [Test]
    public void Load_FindsNothingWhenThereIsNoSave()
    {
        Assert.That(SaveFiles.Exists(path), Is.False);
        Assert.That(SaveFiles.Load(path, Parse, out string value), Is.EqualTo(SaveSource.None));
        Assert.That(value, Is.Null);
    }

    [Test]
    public void Delete_RemovesTheMainFileTheBackupAndTheTempFile()
    {
        SaveFiles.Write(path, "ok 1");
        SaveFiles.Write(path, "ok 2");
        File.WriteAllText(SaveFiles.TempPath(path), "half");
        SaveFiles.Delete(path);
        Assert.That(SaveFiles.Exists(path), Is.False);
        Assert.That(Directory.GetFiles(folder), Is.Empty);
    }
}
