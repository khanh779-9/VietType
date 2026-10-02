using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VietType.Core.Models;
using VietType.Platform;
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

    /// <summary>
    /// Đã bật khởi động cùng Windows nếu có Registry Run key
    /// hoặc task "VietType" trong Task Scheduler đang kích hoạt trigger Logon.
    /// </summary>
    public bool IsStartWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
        return key?.GetValue(RunValueName) is string || (ElevationHelper.IsAdministrator() && VietTypeStartupTask.HasLogonTrigger());
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (ElevationHelper.IsAdministrator())
        {
            if (enabled)
            {
                // Khi chạy quyền Admin: Đăng ký task có trigger Logon để Windows tự khởi động
                // với quyền cao nhất (Highest) mà KHÔNG cần UAC prompt lúc đăng nhập.
                VietTypeStartupTask.Register(enableLogonTrigger: true);
                SetRunKey(false);
            }
            else
            {
                // Tắt khởi động cùng Windows: Tắt trigger Logon nhưng VẪN GIỮ task trong Task Scheduler
                // để hỗ trợ chạy Administrator không cần UAC prompt!
                VietTypeStartupTask.Register(enableLogonTrigger: false);
                SetRunKey(false);
            }
        }
        else
        {
            // Khi chạy quyền người dùng thông thường: Khởi động qua Registry Run
            SetRunKey(enabled);
        }
    }

    private void SetRunKey(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunRegistryKey, writable: true);
        if (key is null) return;

        if (enabled)
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
                key.SetValue(RunValueName, $"\"{exe}\"");
        }
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
