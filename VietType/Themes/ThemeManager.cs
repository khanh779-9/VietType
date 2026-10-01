using Microsoft.Win32;
using System;
using System.Windows;

namespace VietType.Themes;

public enum AppTheme
{
    Light,
    Dark,
    Auto
}

public static class ThemeManager
{
    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static void Initialize()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Shutdown() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private const string LightFile = "Light.xaml";
    private const string DarkFile = "Dark.xaml";

    public static AppTheme Parse(string? value) =>
        Enum.TryParse<AppTheme>(value, true, out var theme) ? theme : AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        Current = theme;
        bool dark = theme == AppTheme.Dark || (theme == AppTheme.Auto && IsSystemDark());
        string themeFile = dark ? DarkFile : LightFile;

        var merged = Application.Current?.Resources?.MergedDictionaries;
        if (merged is null) return;

        int existingIndex = -1;
        bool alreadyMatches = false;
        for (int i = 0; i < merged.Count; i++)
        {
            var src = merged[i].Source?.OriginalString;
            if (src is null) continue;
            if (src.EndsWith(themeFile, StringComparison.OrdinalIgnoreCase))
            {
                alreadyMatches = true;
                existingIndex = i;
                break;
            }
            if (src.EndsWith(LightFile, StringComparison.OrdinalIgnoreCase) ||
                src.EndsWith(DarkFile, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        if (alreadyMatches) return;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"/VietType;component/Themes/{themeFile}", UriKind.Relative)
        };

        if (existingIndex >= 0)
        {
            merged.RemoveAt(existingIndex);
            merged.Insert(existingIndex, dictionary);
        }
        else
        {
            merged.Add(dictionary);
        }
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (Current != AppTheme.Auto || Application.Current is null) return;
        Application.Current.Dispatcher.BeginInvoke(() => Apply(AppTheme.Auto));
    }
}
