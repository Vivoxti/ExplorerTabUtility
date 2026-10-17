using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ExplorerTabUtility.Managers;

internal sealed class SettingsStorage<T>(string filePath) where T : class, new()
{
    private readonly object _saveLock = new();
    private readonly string _backupPath = filePath + ".bak";

    public T Load() => TryLoad(filePath) ?? TryLoad(_backupPath) ?? new T();

    private static T? TryLoad(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Failed to load settings from {path}: {ex.Message}");
            return null;
        }
    }

    public void Save(T settings)
    {
        // History updates and UI changes can save concurrently. Serialize inside
        // the lock so an older snapshot cannot replace a newer save.
        lock (_saveLock)
        {
            var json = JsonSerializer.Serialize(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
                        writer.Write(json);

                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(filePath))
                {
                    // Do not replace a good backup with a damaged primary file
                    // after recovering settings from that backup.
                    var backupPath = TryLoad(filePath) != null ? _backupPath : null;
                    File.Replace(temporaryPath, filePath, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
