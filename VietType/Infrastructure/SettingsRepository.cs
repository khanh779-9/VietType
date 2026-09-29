using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VietType.Core.Models;
using Microsoft.Win32;

namespace VietType.Infrastructure;

public sealed class SettingsRepository
{
    private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "VietType";

    public string AppDirectory { get; }
    public string SettingsPath { get; }
    public string ShortcutPath { get; }

    public SettingsRepository()
    {
        AppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietType");
        Directory.CreateDirectory(AppDirectory);
        SettingsPath = Path.Combine(AppDirectory, "settings.json");
        ShortcutPath = Path.Combine(AppDirectory, "GoTat.dat");
    }

    public AppConfiguration Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration();
            }
        }
        catch
        {
            // Trả về cấu hình mặc định nếu file JSON lỗi.
        }

        return new AppConfiguration();
    }

    public void Save(AppConfiguration settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, options));
    }

    public List<ShortcutDefinition> LoadShortcuts()
    {
        try
        {
            if (!File.Exists(ShortcutPath))
                File.WriteAllText(ShortcutPath, "chxh\tCộng hòa xã hội chủ nghĩa Việt Nam\r\ndltd\tĐộc lập - Tự do - Hạnh phúc");

            return File.ReadAllLines(ShortcutPath)
                .Select(ParseShortcut)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();
        }
        catch
        {
            return new List<ShortcutDefinition>();
        }
    }

    public void SaveShortcuts(IEnumerable<ShortcutDefinition> items)
    {
        var lines = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Trigger))
            .Select(x => $"{x.Trigger.Trim()}\t{x.Replacement ?? string.Empty}");
        File.WriteAllLines(ShortcutPath, lines);
    }

    public bool IsStartWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
        return key?.GetValue(RunValueName) is string;
    }

    public void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunRegistryKey, writable: true);
        if (key is null) return;

        if (enabled)
            key.SetValue(RunValueName, Environment.ProcessPath ?? string.Empty);
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }



    private static ShortcutDefinition? ParseShortcut(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        int tab = line.IndexOf('\t');
        if (tab <= 0) return null;
        return new ShortcutDefinition
        {
            Trigger = line[..tab].Trim(),
            Replacement = line[(tab + 1)..]
        };
    }
}
