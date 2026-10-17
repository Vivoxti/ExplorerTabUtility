using System.IO;
using System.Text.Json;
using System.Windows;
using ExplorerTabUtility.Managers;
using ExplorerTabUtility.Models;
using Xunit;

namespace ExplorerTabUtility.Tests;

public sealed class SettingsStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ExplorerTabUtility.Tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "settings.json");
    private SettingsStorage<AppSettings> Storage => new(FilePath);

    private static AppSettings HiddenSettings() => new()
    {
        IsTrayIconHidden = true,
        IsFirstRun = false,
        AutoUpdate = true,
        HotKeyProfiles = "[{\"Name\":\"Show settings\",\"HotKeys\":[17,18,84],\"Action\":\"ToggleVisibility\",\"IsEnabled\":true}]",
        FormSize = new Size(900, 500),
        ClosedWindows = [new WindowRecord("C:/Example", 42)]
    };

    [Fact]
    public void SaveAndReloadPreservesHiddenIconAndHotkey()
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        var loaded = Storage.Load();

        AssertHiddenSettings(loaded, settings);
        Assert.Equal(settings.FormSize, loaded.FormSize);
        Assert.Equal(settings.ClosedWindows![0].Handle, loaded.ClosedWindows![0].Handle);
    }

    [Fact]
    public void ExistingSettingsWithoutBackupRemainCompatible()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(HiddenSettings()));

        Assert.True(Storage.Load().IsTrayIconHidden);
        Assert.False(File.Exists(FilePath + ".bak"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"IsTrayIconHidden\":")]
    [InlineData("null")]
    public void DamagedPrimaryRecoversHiddenIconAndHotkeyFromBackup(string damagedJson)
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        Storage.Save(settings);
        File.WriteAllText(FilePath, damagedJson);

        AssertHiddenSettings(Storage.Load(), settings);
    }

    [Fact]
    public void MissingPrimaryRecoversFromBackup()
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        Storage.Save(settings);
        File.Delete(FilePath);

        AssertHiddenSettings(Storage.Load(), settings);
    }

    [Fact]
    public void SavingRecoveredSettingsDoesNotReplaceGoodBackupWithDamagedJson()
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        Storage.Save(settings);
        File.WriteAllText(FilePath, "interrupted old write");
        var recovered = Storage.Load();
        Storage.Save(recovered);
        File.WriteAllText(FilePath, "another damaged write");

        AssertHiddenSettings(Storage.Load(), settings);
    }

    [Fact]
    public void InterruptedTemporaryWriteDoesNotDamageSavedSettings()
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        File.WriteAllText(FilePath + ".interrupted.tmp", "{\"IsTrayIconHidden\":");

        AssertHiddenSettings(Storage.Load(), settings);
    }

    [Fact]
    public void FailedReplacementLeavesOriginalAndBackupIntactAndCleansTemporaryFile()
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        Storage.Save(settings);
        var original = File.ReadAllText(FilePath);
        var backup = File.ReadAllText(FilePath + ".bak");
        using (File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => Storage.Save(new AppSettings()));

        Assert.Equal(original, File.ReadAllText(FilePath));
        Assert.Equal(backup, File.ReadAllText(FilePath + ".bak"));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void SerializationFailureLeavesSavedSettingsIntact()
    {
        var settings = HiddenSettings();
        Storage.Save(settings);
        var original = File.ReadAllText(FilePath);
        settings.FormSize = new Size(double.NaN, 500);

        Assert.ThrowsAny<ArgumentException>(() => Storage.Save(settings));
        Assert.Equal(original, File.ReadAllText(FilePath));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void ConcurrentSavesLeaveCompleteSettingsAndAValidBackup()
    {
        var storage = Storage;
        Parallel.For(0, 100, i =>
        {
            var settings = HiddenSettings();
            settings.FormSize = new Size(900 + i, 500);
            storage.Save(settings);
        });

        AssertHiddenSettings(storage.Load(), HiddenSettings());
        AssertHiddenSettings(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath + ".bak"))!, HiddenSettings());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void FreshInstallationUsesDefaultsWithoutCreatingSettingsFiles()
    {
        Assert.False(Storage.Load().IsTrayIconHidden);
        Assert.False(Directory.Exists(_directory));
    }

    private static void AssertHiddenSettings(AppSettings actual, AppSettings expected)
    {
        Assert.True(actual.IsTrayIconHidden);
        Assert.False(actual.IsFirstRun);
        Assert.True(actual.AutoUpdate);
        Assert.Equal(expected.HotKeyProfiles, actual.HotKeyProfiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
