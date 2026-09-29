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

    public static void Apply(AppTheme theme)
    {
        Current = theme;
        bool dark = theme == AppTheme.Dark || (theme == AppTheme.Auto && IsSystemDark());
        string themeFile = dark ? DarkFile : LightFile;
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"/VietType;component/Themes/{themeFile}", UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;

        // Find and replace the existing theme dictionary (Light or Dark) instead
        // of clearing everything, so control styles remain intact.
        // Compare by filename since URI formats differ between XAML-loaded and
        // code-loaded dictionaries.
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source;
            if (src is null) continue;
            var path = src.OriginalString;
            if (path.EndsWith(LightFile, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(DarkFile, StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }
        merged.Add(dictionary);
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
