using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VietType.Controls;
using VietType.Core.Models;
using VietType.Core.Typing;
using VietType.Infrastructure;
using VietType.Pages;
using VietType.Platform;
using VietType.Platform.Keyboard;
using VietType.Themes;

namespace VietType;

public partial class MainWindow : VietTypeWindow
{
    private readonly AppBackgroundContext _context;
    private readonly SettingsRepository _settings;
    private readonly TextInputEngine _engine;
    private readonly KeyboardHook _keyboardHook;
    private readonly ObservableCollection<ShortcutDefinition> _shortcuts;
    private readonly List<string[]> _encodingTables;
    private bool _loading;

    static MainWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MainWindow),
            new FrameworkPropertyMetadata(typeof(VietTypeWindow)));
    }

    public MainWindow() : this(App.BackgroundContext)
    {
    }

    public MainWindow(AppBackgroundContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _settings = _context.Settings;
        _engine = _context.Engine;
        _keyboardHook = _context.KeyboardHook;
        _shortcuts = _context.Shortcuts;
        _encodingTables = _context.EncodingTables;

        InitializeComponent();

        _keyboardHook.DebugLog += OnDebugLog;
        _context.EnabledChanged += OnContextEnabledChanged;
        _context.TypingMethodChanged += OnContextTypingMethodChanged;
        _context.CodeTableChanged += OnContextCodeTableChanged;
        _context.SpellCheckChanged += OnContextSpellCheckChanged;
        _context.ModernToneChanged += OnContextModernToneChanged;
        _context.ShortcutsChanged += OnContextShortcutsChanged;

        ShortcutsPage.Grid.ItemsSource = _shortcuts;
        WirePageEvents();
    }

    private void WirePageEvents()
    {
        HomePage.EnabledChanged += ToggleEnabled_Changed;
        HomePage.TypingMethodChanged += TypingMethodCombo_Changed;
        HomePage.CodeTableChanged += CodeTableCombo_Changed;
        HomePage.PreviewChanged += PreviewInput_TextChanged;
        HomePage.SettingsChanged += SettingCheck_Changed;

        InputPage.SpellCheckChanged += SpellCheckCombo_Changed;
        InputPage.SettingsChanged += SettingCheck_Changed;
        InputPage.RunAsAdminChanged += RunAsAdmin_Changed;

        HotkeysPage.SettingComboChanged += SettingCombo_Changed;
        HotkeysPage.SettingsChanged += SettingCheck_Changed;

        ShortcutsPage.ShortcutSelected += ShortcutGrid_SelectionChanged;
        ShortcutsPage.AddRequested += AddShortcut_Click;
        ShortcutsPage.SaveRequested += SaveShortcut_Click;
        ShortcutsPage.DeleteRequested += DeleteShortcut_Click;
        ShortcutsPage.SettingsChanged += SettingCheck_Changed;

        AdvancedPage.ThemeChanged += ThemeCombo_Changed;
        AdvancedPage.SettingsChanged += SettingCheck_Changed;
        AdvancedPage.ResetDefaultsRequested += (_, _) => ResetToDefaults();
        AdvancedPage.CheckUpdatesRequested += (_, _) => CheckForUpdates();
        AdvancedPage.DataFolderRequested += OpenDataFolder_Click;
        AdvancedPage.GuideRequested += OpenGuide_Click;
        AdvancedPage.EncodingTablesRequested += OpenEncodingTables_Click;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _loading = true;
            PopulateEncodingTablesCombo();

            var settings = _settings.Load();
            ApplyConfigurationToUI(settings);
            UpdateStatusVisuals();
            UpdatePreview();

            if (PageTabs.SelectedIndex < 0)
                SelectPage(0);

            _loading = false;
        }
        catch (Exception ex)
        {
            _loading = false;
            OnHookError(this, $"Không tải được cấu hình: {ex.Message}");
        }
    }

    private void PopulateEncodingTablesCombo()
    {
        HomePage.CodeTable.Items.Clear();
        for (int i = 0; i < CharacterTables.PredefinedTableNames.Length; i++)
        {
            HomePage.CodeTable.Items.Add(new ComboBoxItem { Content = CharacterTables.PredefinedTableNames[i] });
        }
        if (HomePage.CodeTable.Items.Count == 0)
            HomePage.CodeTable.Items.Add(new ComboBoxItem { Content = "Unicode" });
    }

    private void ApplyConfigurationToUI(AppConfiguration settings)
    {
        HomePage.TypingMethod.SelectedIndex = Clamp(settings.TypingMethod, 0, 3);
        int targetTableIndex = -1;
        if (!string.IsNullOrEmpty(settings.CodeTableName))
        {
            for (int i = 0; i < HomePage.CodeTable.Items.Count; i++)
            {
                if ((HomePage.CodeTable.Items[i] as ComboBoxItem)?.Content?.ToString() == settings.CodeTableName)
                {
                    targetTableIndex = i;
                    break;
                }
            }
        }
        if (targetTableIndex < 0)
        {
            if (settings.CodeTable == 0 && string.IsNullOrEmpty(settings.CodeTableName))
            {
                for (int i = 0; i < HomePage.CodeTable.Items.Count; i++)
                {
                    if ((HomePage.CodeTable.Items[i] as ComboBoxItem)?.Content?.ToString() == "Unicode")
                    {
                        targetTableIndex = i;
                        break;
                    }
                }
            }
            if (targetTableIndex < 0)
                targetTableIndex = Clamp(settings.CodeTable, 0, Math.Max(0, HomePage.CodeTable.Items.Count - 1));
        }
        HomePage.CodeTable.SelectedIndex = targetTableIndex;
        InputPage.SpellCheck.SelectedIndex = Clamp(settings.SpellCheckLevel, 0, 2);
        HomePage.Toggle.IsOn = settings.Enabled;
        InputPage.ModernTone.IsChecked = settings.ModernToneRemoval;
        InputPage.StripToneAuto.IsChecked = settings.StripToneInAutocomplete;
        HomePage.ShowWindowAtStartup.IsChecked = settings.ShowWindowAtStartup;
        AdvancedPage.ShowWindowAtStartup.IsChecked = settings.ShowWindowAtStartup;

        bool isWinStart = _settings.IsStartWithWindows();
        HomePage.StartWithWindows.IsChecked = isWinStart;
        AdvancedPage.StartWithWindows.IsChecked = isWinStart;
        AdvancedPage.UseClipboard.IsChecked = settings.UseClipboardReplacement;
        AdvancedPage.SoundFeedback.IsChecked = settings.SoundFeedback;

        ShortcutsPage.Enabled.IsChecked = settings.EnableShortcuts;
        ShortcutsPage.WithoutSpace.IsChecked = settings.ShortcutWithoutSpace;
        ShortcutsPage.WhenOff.IsChecked = settings.ShortcutsWhenVietnameseOff;
        AdvancedPage.DebugTracking.IsChecked = settings.DebugTracking;
        AdvancedPage.Theme.SelectedIndex = ParseTheme(settings.Theme) switch { AppTheme.Light => 0, AppTheme.Dark => 1, _ => 2 };

        bool isAdmin = ElevationHelper.IsAdministrator();
        InputPage.RunAsAdmin.IsChecked = isAdmin || settings.RunAsAdmin;
        InputPage.AdminBadge.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        InputPage.SupportGames.IsChecked = settings.SupportGames;
        InputPage.SupportMetro.IsChecked = settings.SupportMetro;

        // Nạp cấu hình phím tắt chi tiết
        HotkeysPage.ToggleCtrl.IsChecked = settings.HotkeyToggle.Ctrl;
        HotkeysPage.ToggleShift.IsChecked = settings.HotkeyToggle.Shift;
        HotkeysPage.ToggleAlt.IsChecked = settings.HotkeyToggle.Alt;
        HotkeysPage.ToggleWin.IsChecked = settings.HotkeyToggle.Win;
        SetComboSelection(HotkeysPage.ToggleKey, string.IsNullOrEmpty(settings.HotkeyToggle.Key) ? "(Không dùng phím)" : settings.HotkeyToggle.Key);

        HotkeysPage.MethodCtrl.IsChecked = settings.HotkeySwitchMethod.Ctrl;
        HotkeysPage.MethodShift.IsChecked = settings.HotkeySwitchMethod.Shift;
        HotkeysPage.MethodAlt.IsChecked = settings.HotkeySwitchMethod.Alt;
        HotkeysPage.MethodWin.IsChecked = settings.HotkeySwitchMethod.Win;
        SetComboSelection(HotkeysPage.MethodKey, string.IsNullOrEmpty(settings.HotkeySwitchMethod.Key) ? "(Tắt)" : settings.HotkeySwitchMethod.Key);

        HotkeysPage.RestoreCtrl.IsChecked = settings.HotkeyRestoreWord.Ctrl;
        HotkeysPage.RestoreShift.IsChecked = settings.HotkeyRestoreWord.Shift;
        HotkeysPage.RestoreAlt.IsChecked = settings.HotkeyRestoreWord.Alt;
        HotkeysPage.RestoreWin.IsChecked = settings.HotkeyRestoreWord.Win;
        SetComboSelection(HotkeysPage.RestoreKey, string.IsNullOrEmpty(settings.HotkeyRestoreWord.Key) ? "(Tắt)" : settings.HotkeyRestoreWord.Key);

        HotkeysPage.QuickEnable.IsChecked = settings.EnableQuickFunctionKeys;
        HotkeysPage.QuickCtrl.IsChecked = settings.QuickKeysCtrl;
        HotkeysPage.QuickShift.IsChecked = settings.QuickKeysShift;
        HotkeysPage.QuickAlt.IsChecked = settings.QuickKeysAlt;
        HotkeysPage.QuickWin.IsChecked = settings.QuickKeysWin;

        HotkeysPage.QuickF1.IsChecked = settings.QuickF1;
        HotkeysPage.QuickF2.IsChecked = settings.QuickF2;
        HotkeysPage.QuickF3.IsChecked = settings.QuickF3;
        HotkeysPage.QuickF4.IsChecked = settings.QuickF4;
        HotkeysPage.QuickF5.IsChecked = settings.QuickF5;
        HotkeysPage.QuickF6.IsChecked = settings.QuickF6;
        HotkeysPage.QuickF7.IsChecked = settings.QuickF7;
        HotkeysPage.QuickF8.IsChecked = settings.QuickF8;
        HotkeysPage.QuickF9.IsChecked = settings.QuickF9;
        HotkeysPage.QuickF12.IsChecked = settings.QuickF12;
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
    private static AppTheme ParseTheme(string? value) => Enum.TryParse<AppTheme>(value, true, out var theme) ? theme : AppTheme.Light;

    public void SelectPage(int index)
    {
        PageTabs.SelectedIndex = index;
        var items = new[] { NavDashboard, NavTyping, NavHotkeys, NavShortcuts, NavAdvanced };
        for (int i = 0; i < items.Length; i++) items[i].IsSelected = i == index;
        NavAbout.IsSelected = false;
    }

    private void ToggleEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _keyboardHook.IsEnabled = HomePage.Toggle.IsOn;
        UpdateStatusVisuals();
        SaveCurrentSettings();
    }

    private void OnContextEnabledChanged(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            HomePage.Toggle.IsOn = enabled;
            _loading = false;
            UpdateStatusVisuals();
        });
    }

    private void OnContextTypingMethodChanged(int index)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            HomePage.TypingMethod.SelectedIndex = index;
            _loading = false;
            UpdateStatusVisuals();
            UpdatePreview();
        });
    }

    private void OnContextCodeTableChanged(int index)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            HomePage.CodeTable.SelectedIndex = index;
            _loading = false;
            UpdateStatusVisuals();
            UpdatePreview();
        });
    }

    private void OnContextSpellCheckChanged(int level)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            InputPage.SpellCheck.SelectedIndex = level;
            _loading = false;
        });
    }

    private void OnContextModernToneChanged(bool modernTone)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            InputPage.ModernTone.IsChecked = modernTone;
            _loading = false;
            UpdatePreview();
        });
    }

    private void OnContextShortcutsChanged(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            ShortcutsPage.Enabled.IsChecked = enabled;
            _loading = false;
        });
    }

    private void UpdateStatusVisuals()
    {
        bool enabled = HomePage.Toggle.IsOn;
        var successBrush = (Brush)FindResource("SuccessBrush");
        var dangerBrush = (Brush)FindResource("DangerBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var mutedBrush = (Brush)FindResource("MutedTextBrush");

        SidebarStatusText.Text = enabled ? "Đang bật (V)" : "Đang tắt (E)";
        StatusDot.Fill = enabled ? successBrush : dangerBrush;

        AppLogoBadge.Background = enabled ? accentBrush : mutedBrush;
        AppLogoPath.Data = (Geometry)FindResource(enabled ? "IconStateOn" : "IconStateOff");
    }

    private void TypingMethodCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || HomePage.TypingMethod.SelectedIndex < 0) return;
        _engine.SetTypingMethod(HomePage.TypingMethod.SelectedIndex);
        UpdateStatusVisuals();
        UpdatePreview();
        SaveCurrentSettings();
    }

    private void CodeTableCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || HomePage.CodeTable.SelectedIndex < 0 || HomePage.CodeTable.SelectedIndex >= _encodingTables.Count) return;
        try
        {
            _engine.SetCodeTable(_encodingTables[HomePage.CodeTable.SelectedIndex]);
            UpdateStatusVisuals();
            UpdatePreview();
            SaveCurrentSettings();
        }
        catch (Exception ex)
        {
            OnHookError(this, $"Không tải được bảng mã: {ex.Message}");
        }
    }

    private void SpellCheckCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || InputPage.SpellCheck.SelectedIndex < 0) return;
        _engine.SpellCheckLevel = InputPage.SpellCheck.SelectedIndex;
        SaveCurrentSettings();
    }

    private bool _syncingCheck;

    private void SettingCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _syncingCheck) return;

        _syncingCheck = true;
        try
        {
            if (ReferenceEquals(sender, HomePage.StartWithWindows))
            {
                AdvancedPage.StartWithWindows.IsChecked = HomePage.StartWithWindows.IsChecked;
            }
            else if (ReferenceEquals(sender, AdvancedPage.StartWithWindows))
            {
                HomePage.StartWithWindows.IsChecked = AdvancedPage.StartWithWindows.IsChecked;
            }
            else if (ReferenceEquals(sender, HomePage.ShowWindowAtStartup))
            {
                AdvancedPage.ShowWindowAtStartup.IsChecked = HomePage.ShowWindowAtStartup.IsChecked;
            }
            else if (ReferenceEquals(sender, AdvancedPage.ShowWindowAtStartup))
            {
                HomePage.ShowWindowAtStartup.IsChecked = AdvancedPage.ShowWindowAtStartup.IsChecked;
            }

            SaveCurrentSettings();
        }
        finally
        {
            _syncingCheck = false;
        }
    }

    private void PreviewInput_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void UpdatePreview()
    {
        try { HomePage.PreviewResult.Text = _engine.PreviewText(HomePage.PreviewEditor.Text); }
        catch { HomePage.PreviewResult.Text = HomePage.PreviewEditor.Text; }
    }

    private void SettingCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            SaveCurrentSettings();
        }
    }

    private static void SetComboSelection(ComboBox combo, string text)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i]?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string GetSelectedKey(ComboBox combo, string ignoreValue)
    {
        string? val = combo.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(val) || val.Equals(ignoreValue, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return val;
    }

    private void RunAsAdmin_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool requestAdmin = InputPage.RunAsAdmin.IsChecked == true;
        bool isCurrentlyAdmin = ElevationHelper.IsAdministrator();

        if (requestAdmin && !isCurrentlyAdmin)
        {
            var result = MessageBox.Show(
                "Để kích hoạt quyền quản trị (Administrator), VietType cần khởi động lại.\n\nBạn có muốn khởi động lại ứng dụng ngay bây giờ?",
                "Khởi động quyền quản trị - VietType",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveCurrentSettings();
                _context.RestartApplication(true);
            }
            else
            {
                _loading = true;
                InputPage.RunAsAdmin.IsChecked = false;
                _loading = false;
            }
        }
        else
        {
            SaveCurrentSettings();
        }
    }

    private void SaveCurrentSettings()
    {
        if (_loading) return;
        try
        {
            bool startWithWin = HomePage.StartWithWindows.IsChecked == true;
            bool showAtStartup = HomePage.ShowWindowAtStartup.IsChecked == true;

            var settings = new AppConfiguration
            {
                Enabled = HomePage.Toggle.IsOn,
                TypingMethod = Math.Max(0, HomePage.TypingMethod.SelectedIndex),
                CodeTable = Math.Max(0, HomePage.CodeTable.SelectedIndex),
                CodeTableName = (HomePage.CodeTable.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Unicode",
                ModernToneRemoval = InputPage.ModernTone.IsChecked == true,
                ShowWindowAtStartup = showAtStartup,
                SpellCheckLevel = Math.Max(0, InputPage.SpellCheck.SelectedIndex),
                EnableShortcuts = ShortcutsPage.Enabled.IsChecked == true,
                ShortcutWithoutSpace = ShortcutsPage.WithoutSpace.IsChecked == true,
                ShortcutsWhenVietnameseOff = ShortcutsPage.WhenOff.IsChecked == true,
                StripToneInAutocomplete = InputPage.StripToneAuto.IsChecked == true,
                DebugTracking = AdvancedPage.DebugTracking.IsChecked == true,
                StartWithWindows = startWithWin,
                Theme = GetSelectedTheme().ToString(),
                RunAsAdmin = InputPage.RunAsAdmin.IsChecked == true,
                SupportGames = InputPage.SupportGames.IsChecked == true,
                SupportMetro = InputPage.SupportMetro.IsChecked == true,
                UseClipboardReplacement = AdvancedPage.UseClipboard.IsChecked == true,
                SoundFeedback = AdvancedPage.SoundFeedback.IsChecked == true,

                HotkeyToggle = new HotkeyItem
                {
                    Enabled = true,
                    Ctrl = HotkeysPage.ToggleCtrl.IsChecked == true,
                    Shift = HotkeysPage.ToggleShift.IsChecked == true,
                    Alt = HotkeysPage.ToggleAlt.IsChecked == true,
                    Win = HotkeysPage.ToggleWin.IsChecked == true,
                    Key = GetSelectedKey(HotkeysPage.ToggleKey, "(Không dùng phím)")
                },

                HotkeySwitchMethod = new HotkeyItem
                {
                    Enabled = !string.IsNullOrEmpty(GetSelectedKey(HotkeysPage.MethodKey, "(Tắt)")),
                    Ctrl = HotkeysPage.MethodCtrl.IsChecked == true,
                    Shift = HotkeysPage.MethodShift.IsChecked == true,
                    Alt = HotkeysPage.MethodAlt.IsChecked == true,
                    Win = HotkeysPage.MethodWin.IsChecked == true,
                    Key = GetSelectedKey(HotkeysPage.MethodKey, "(Tắt)")
                },

                HotkeyRestoreWord = new HotkeyItem
                {
                    Enabled = !string.IsNullOrEmpty(GetSelectedKey(HotkeysPage.RestoreKey, "(Tắt)")),
                    Ctrl = HotkeysPage.RestoreCtrl.IsChecked == true,
                    Shift = HotkeysPage.RestoreShift.IsChecked == true,
                    Alt = HotkeysPage.RestoreAlt.IsChecked == true,
                    Win = HotkeysPage.RestoreWin.IsChecked == true,
                    Key = GetSelectedKey(HotkeysPage.RestoreKey, "(Tắt)")
                },

                EnableQuickFunctionKeys = HotkeysPage.QuickEnable.IsChecked == true,
                QuickKeysCtrl = HotkeysPage.QuickCtrl.IsChecked == true,
                QuickKeysShift = HotkeysPage.QuickShift.IsChecked == true,
                QuickKeysAlt = HotkeysPage.QuickAlt.IsChecked == true,
                QuickKeysWin = HotkeysPage.QuickWin.IsChecked == true,

                QuickF1 = HotkeysPage.QuickF1.IsChecked == true,
                QuickF2 = HotkeysPage.QuickF2.IsChecked == true,
                QuickF3 = HotkeysPage.QuickF3.IsChecked == true,
                QuickF4 = HotkeysPage.QuickF4.IsChecked == true,
                QuickF5 = HotkeysPage.QuickF5.IsChecked == true,
                QuickF6 = HotkeysPage.QuickF6.IsChecked == true,
                QuickF7 = HotkeysPage.QuickF7.IsChecked == true,
                QuickF8 = HotkeysPage.QuickF8.IsChecked == true,
                QuickF9 = HotkeysPage.QuickF9.IsChecked == true,
                QuickF12 = HotkeysPage.QuickF12.IsChecked == true
            };

            _context.SaveConfiguration(settings);
        }
        catch (Exception ex)
        {
            OnHookError(this, $"Không lưu được cấu hình: {ex.Message}");
        }
    }

    private void ResetToDefaults()
    {
        var result = MessageBox.Show(
            "Bạn có chắc chắn muốn khôi phục toàn bộ thiết lập VietType về mặc định ban đầu không?",
            "VietType - Khôi phục mặc định",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _loading = true;
            var defaults = new AppConfiguration();
            ApplyConfigurationToUI(defaults);
            _loading = false;

            _context.SaveConfiguration(defaults);
            UpdateStatusVisuals();
            UpdatePreview();

            _context.ShowBalloonNotification("VietType", "Đã khôi phục toàn bộ thiết lập về mặc định!");
        }
        catch (Exception ex)
        {
            _loading = false;
            MessageBox.Show($"Lỗi khi khôi phục thiết lập: {ex.Message}", "VietType", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CheckForUpdates()
    {
        MessageBox.Show(
            "Bạn đang sử dụng phiên bản VietType mới nhất (v1.3.0).\n\nKhông có bản cập nhật mới nào tại thời điểm này.",
            "VietType - Kiểm tra cập nhật",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private AppTheme GetSelectedTheme() => AdvancedPage.Theme.SelectedIndex switch { 1 => AppTheme.Dark, 2 => AppTheme.Auto, _ => AppTheme.Light };
    private void ThemeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && IsLoaded)
        {
            ThemeManager.Apply(GetSelectedTheme());
            SaveCurrentSettings();
        }
    }

    private void ShortcutGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShortcutsPage.Grid.SelectedItem is ShortcutDefinition item)
        {
            ShortcutsPage.TriggerBox.Text = item.Trigger;
            ShortcutsPage.ReplacementBox.Text = item.Replacement;
        }
    }

    private void AddShortcut_Click(object sender, RoutedEventArgs e)
    {
        string trigger = ShortcutsPage.TriggerBox.Text.Trim();
        string replacement = ShortcutsPage.ReplacementBox.Text;
        if (trigger.Length == 0 || replacement.Length == 0)
        {
            MessageBox.Show("Hãy nhập cả từ khóa và nội dung thay thế.", "Gõ tắt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_shortcuts.Any(x => string.Equals(x.Trigger, trigger, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Từ khóa này đã tồn tại.", "Gõ tắt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var item = new ShortcutDefinition { Trigger = trigger, Replacement = replacement };
        _shortcuts.Add(item);
        PersistShortcuts(item);
    }

    private void SaveShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsPage.Grid.SelectedItem is not ShortcutDefinition item)
        {
            AddShortcut_Click(sender, e);
            return;
        }
        string trigger = ShortcutsPage.TriggerBox.Text.Trim();
        string replacement = ShortcutsPage.ReplacementBox.Text;
        if (trigger.Length == 0 || replacement.Length == 0) return;
        if (_shortcuts.Any(x => !ReferenceEquals(x, item) && string.Equals(x.Trigger, trigger, StringComparison.OrdinalIgnoreCase))) return;
        item.Trigger = trigger;
        item.Replacement = replacement;
        ShortcutsPage.Grid.Items.Refresh();
        PersistShortcuts(item);
    }

    private void DeleteShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsPage.Grid.SelectedItem is not ShortcutDefinition item) return;
        _shortcuts.Remove(item);
        _settings.SaveShortcuts(_shortcuts);
        _engine.SetShortcuts(_shortcuts);
        ShortcutsPage.TriggerBox.Clear();
        ShortcutsPage.ReplacementBox.Clear();
    }

    private void PersistShortcuts(ShortcutDefinition item)
    {
        ShortcutsPage.Grid.SelectedItem = item;
        ShortcutsPage.Grid.ScrollIntoView(item);
        _settings.SaveShortcuts(_shortcuts);
        _engine.SetShortcuts(_shortcuts);
    }

    private void OnDebugLog(string message)
    {
        if (AdvancedPage.DebugTracking.IsChecked != true) return;
        Dispatcher.BeginInvoke(() =>
        {
            AdvancedPage.DebugLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            AdvancedPage.DebugLog.ScrollToEnd();
        });
    }

    private void OnHookError(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            AdvancedPage.DebugLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}{Environment.NewLine}");
            AdvancedPage.DebugLog.ScrollToEnd();
        });
    }

    // Bấm nút [X] đóng cửa sổ giao diện mà không tắt tiến trình nền (tiến trình nền do AppBackgroundContext duy trì).
    protected override void OnCloseButtonClick() => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveCurrentSettings();

        _keyboardHook.DebugLog -= OnDebugLog;
        _context.EnabledChanged -= OnContextEnabledChanged;
        _context.TypingMethodChanged -= OnContextTypingMethodChanged;
        _context.CodeTableChanged -= OnContextCodeTableChanged;
        _context.SpellCheckChanged -= OnContextSpellCheckChanged;
        _context.ModernToneChanged -= OnContextModernToneChanged;
        _context.ShortcutsChanged -= OnContextShortcutsChanged;

        _context.OnMainWindowClosed();
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e) => SelectPage(0);
    private void NavTyping_Click(object sender, RoutedEventArgs e) => SelectPage(1);
    private void NavHotkeys_Click(object sender, RoutedEventArgs e) => SelectPage(2);
    private void NavShortcuts_Click(object sender, RoutedEventArgs e) => SelectPage(3);
    private void NavAdvanced_Click(object sender, RoutedEventArgs e) => SelectPage(4);
    private void NavAbout_Click(object sender, RoutedEventArgs e) => _context.OpenAboutWindow();

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenPath(_settings.AppDirectory);
    private void OpenGuide_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(AppContext.BaseDirectory, "Resources", "Documentation", "UsageGuide.html"));
    private void OpenEncodingTables_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(AppContext.BaseDirectory, "Data", "EncodingTables"));

    private static void OpenPath(string path)
    {
        try
        {
            if (File.Exists(path) || Directory.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                MessageBox.Show("Không tìm thấy tài nguyên.", "VietType", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "VietType", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
