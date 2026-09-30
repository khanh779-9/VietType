using VietType.Controls;
using VietType.Core.Typing;
using VietType.Core.Models;
using VietType.Platform.Keyboard;
using VietType.Pages;
using VietType.Infrastructure;
using VietType.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace VietType;

public partial class MainWindow : VietTypeWindow
{
    private readonly SettingsRepository _settings = new();
    private readonly TextInputEngine _engine = new(CharacterTables.Unicode);
    private readonly KeyboardHook _keyboardHook;
    private readonly ObservableCollection<ShortcutDefinition> _shortcuts = new();
    private readonly List<string[]> _encodingTables = new();
    private Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIconOn;
    private System.Drawing.Icon? _trayIconOff;
    private bool _loading;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _keyboardHook = new KeyboardHook { Engine = _engine };
        _keyboardHook.EnabledChanged += OnEnabledChanged;
        _keyboardHook.Error += OnHookError;
        _keyboardHook.DebugLog += OnDebugLog;

        ShortcutsPage.Grid.ItemsSource = _shortcuts;
        WirePageEvents();
        BuildTrayIcon();
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

        _keyboardHook.SwitchTypingMethodRequested += () => Dispatcher.Invoke(CycleTypingMethod);
        _keyboardHook.SelectUnicodeRequested += () => Dispatcher.Invoke(SelectUnicodeTable);
        _keyboardHook.CycleCodeTableRequested += () => Dispatcher.Invoke(CycleCodeTable);
        _keyboardHook.ToggleSpellCheckRequested += () => Dispatcher.Invoke(ToggleSpellCheck);
        _keyboardHook.OpenDashboardRequested += () => Dispatcher.Invoke(ShowFromTray);
        _keyboardHook.OpenShortcutsRequested += () => Dispatcher.Invoke(() => { ShowFromTray(); SelectPage(3); });
        _keyboardHook.ToggleShortcutsRequested += () => Dispatcher.Invoke(ToggleShortcutsState);

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
            var settings = _settings.Load();
            ThemeManager.Apply(ParseTheme(settings.Theme));
            LoadEncodingTables();
            LoadShortcuts();

            ApplyConfigurationToUI(settings);
            ApplySettingsToEngine();
            _suppressEnabledFeedback = true;
            _keyboardHook.IsEnabled = settings.Enabled;
            _suppressEnabledFeedback = false;
            _keyboardHook.Install();
            UpdateStatusVisuals();
            UpdatePreview();
            SelectPage(0);
            _loading = false;
            if (!settings.ShowWindowAtStartup) Hide();
        }
        catch (Exception ex)
        {
            _loading = false;
            OnHookError(this, $"Không tải được cấu hình: {ex.Message}");
        }
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

        bool isAdmin = Platform.ElevationHelper.IsAdministrator();
        InputPage.RunAsAdmin.IsChecked = isAdmin || settings.RunAsAdmin;
        InputPage.AdminBadge.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        InputPage.SupportGames.IsChecked = settings.SupportGames;
        InputPage.SupportMetro.IsChecked = settings.SupportMetro;

        _keyboardHook.SupportGames = settings.SupportGames;
        _keyboardHook.SupportMetro = settings.SupportMetro;
        _keyboardHook.UseClipboardReplacement = settings.UseClipboardReplacement;

        // Đang chạy elevated và người dùng muốn quyền Admin → đảm bảo task "VietType"
        // (RunLevel Highest) đã đăng ký: các lần khởi động/restart sau sẽ gọi task
        // này thay vì UAC prompt.
        if (settings.RunAsAdmin && Platform.ElevationHelper.IsAdministrator())
            Platform.VietTypeStartupTask.Register();

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

    private static System.Drawing.Icon CreateBadgeIcon(string letter, System.Drawing.Color bgColor, System.Drawing.Color fgColor)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(System.Drawing.Color.Transparent);

            // Nền bo góc mềm mại, phủ rộng để nét to rõ
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                float radius = 6.5f;
                float diameter = radius * 2f;
                var bounds = new System.Drawing.RectangleF(0.5f, 0.5f, size - 1f, size - 1f);
                path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();

                using var brush = new SolidBrush(bgColor);
                g.FillPath(brush, path);

                // Đường viền mảnh định hình góc cạnh rõ ràng trên mọi hình nền taskbar
                using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(50, 255, 255, 255), 1.2f);
                g.DrawPath(borderPen, path);
            }

            // Ký tự V hoặc E in cực đậm to rõ nét (Arial Bold 22px)
            using var font = new Font("Arial", 22f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(fgColor);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var textRect = new System.Drawing.RectangleF(0, 0.5f, size, size);
            g.DrawString(letter, font, textBrush, textRect, sf);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = System.Drawing.Icon.FromHandle(hIcon);
            return (System.Drawing.Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private System.Windows.Controls.ContextMenu? _trayContextMenu;
    private AboutWindow? _aboutWindow;

    private void BuildTrayIcon()
    {
        // Chữ V màu đỏ đậm (chuẩn tiếng Việt), chữ E màu xanh dương (chuẩn quốc tế) cực kỳ dễ nhìn
        _trayIconOn = CreateBadgeIcon("V", System.Drawing.Color.FromArgb(220, 38, 38), System.Drawing.Color.White);
        _trayIconOff = CreateBadgeIcon("E", System.Drawing.Color.FromArgb(37, 99, 235), System.Drawing.Color.White);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconOn,
            Visible = true,
            Text = "VietType • Tiếng Việt [V]"
        };

        // Click chuột trái vào icon khay hệ thống để bật/tắt nhanh tiếng Việt (V <-> E)
        // Click chuột phải hiển thị Custom WPF ContextMenu tương thích theme
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                Dispatcher.Invoke(ToggleTypingEnabled);
            else if (e.Button == Forms.MouseButtons.Right)
                Dispatcher.Invoke(ShowTrayContextMenu);
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void ShowTrayContextMenu()
    {
        _trayContextMenu = CreateTrayContextMenu();
        UpdateTrayContextMenu(_trayContextMenu);

        // Đảm bảo menu nhận focus để tự động đóng khi click ra ngoài
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        NativeMethods.SetForegroundWindow(helper.EnsureHandle());

        _trayContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _trayContextMenu.IsOpen = true;
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // 1. Nhóm thao tác chuyển mã & gõ tắt
        var itemConvert = new System.Windows.Controls.MenuItem { Header = "Chuyển mã ..." };
        itemConvert.Click += (_, _) => { ShowFromTray(); SelectPage(1); };
        menu.Items.Add(itemConvert);

        var itemShortcuts = new System.Windows.Controls.MenuItem { Header = "Gõ tắt ..." };
        itemShortcuts.Click += (_, _) => { ShowFromTray(); SelectPage(3); };
        menu.Items.Add(itemShortcuts);

        // 2. Gợi ý từ
        var itemSuggest = new System.Windows.Controls.MenuItem { Header = "Gợi ý từ (Auto Complete)", Tag = "opt_suggest" };
        itemSuggest.Click += (_, _) => ToggleOptionSuggest();
        menu.Items.Add(itemSuggest);

        // 3. Kiểu gõ > (Submenu)
        var itemMethods = new System.Windows.Controls.MenuItem { Header = "Kiểu gõ" };
        var mTelex = new System.Windows.Controls.MenuItem { Header = "Telex", Tag = "method_0" };
        mTelex.Click += (_, _) => SetTypingMethodFromTray(0);
        itemMethods.Items.Add(mTelex);

        var mVni = new System.Windows.Controls.MenuItem { Header = "VNI", Tag = "method_1" };
        mVni.Click += (_, _) => SetTypingMethodFromTray(1);
        itemMethods.Items.Add(mVni);

        var mViqr = new System.Windows.Controls.MenuItem { Header = "VIQR", Tag = "method_2" };
        mViqr.Click += (_, _) => SetTypingMethodFromTray(2);
        itemMethods.Items.Add(mViqr);

        var mTelexExt = new System.Windows.Controls.MenuItem { Header = "Telex mở rộng", Tag = "method_3" };
        mTelexExt.Click += (_, _) => SetTypingMethodFromTray(3);
        itemMethods.Items.Add(mTelexExt);
        menu.Items.Add(itemMethods);

        // 4. BẢNG MÃ > (Submenu chứa 17 bảng mã như yêu cầu)
        var itemTables = new System.Windows.Controls.MenuItem { Header = "Bảng mã" };
        for (int i = 0; i < HomePage.CodeTable.Items.Count; i++)
        {
            int idx = i;
            string name = (HomePage.CodeTable.Items[i] as ComboBoxItem)?.Content?.ToString() ?? $"Bảng mã {i + 1}";
            var subItem = new System.Windows.Controls.MenuItem { Header = name, Tag = $"table_{idx}" };
            subItem.Click += (_, _) => SetCodeTableFromTray(idx);
            itemTables.Items.Add(subItem);
        }
        menu.Items.Add(itemTables);

        // 5. Các lựa chọn khác > (Submenu)
        var itemOtherOptions = new System.Windows.Controls.MenuItem { Header = "Các lựa chọn khác" };

        var optShortcuts = new System.Windows.Controls.MenuItem { Header = "Bật tính năng gõ tắt", Tag = "opt_shortcuts" };
        optShortcuts.Click += (_, _) => ToggleOptionShortcuts();
        itemOtherOptions.Items.Add(optShortcuts);

        var optSpell = new System.Windows.Controls.MenuItem { Header = "Kiểm tra chính tả", Tag = "opt_spell" };
        optSpell.Click += (_, _) => ToggleOptionSpell();
        itemOtherOptions.Items.Add(optSpell);

        var optRestore = new System.Windows.Controls.MenuItem { Header = "Phục hồi từ gốc" };
        optRestore.Click += (_, _) => _keyboardHook.TriggerRestoreWord();
        itemOtherOptions.Items.Add(optRestore);

        var optStartup = new System.Windows.Controls.MenuItem { Header = "Khởi động cùng Windows", Tag = "opt_startup" };
        optStartup.Click += (_, _) => ToggleStartupOption();
        itemOtherOptions.Items.Add(optStartup);

        menu.Items.Add(itemOtherOptions);

        // 6. Quản trị & Điều khiển
        var itemAdmin = new System.Windows.Controls.MenuItem { Header = "Khởi động lại với quyền Admin", Tag = "admin" };
        itemAdmin.Click += (_, _) => RestartApplication(true);
        menu.Items.Add(itemAdmin);

        var itemDashboard = new System.Windows.Controls.MenuItem
        {
            Header = "Hiện cửa sổ chính",
            FontWeight = FontWeights.Bold
        };
        itemDashboard.Click += (_, _) => ShowFromTray();
        menu.Items.Add(itemDashboard);

        var itemAbout = new System.Windows.Controls.MenuItem { Header = "About VietType..." };
        itemAbout.Click += (_, _) => OpenAboutWindow();
        menu.Items.Add(itemAbout);

        var itemExit = new System.Windows.Controls.MenuItem { Header = "Thoát" };
        itemExit.Click += (_, _) => ExitApplication();
        menu.Items.Add(itemExit);

        return menu;
    }

    private void UpdateTrayContextMenu(System.Windows.Controls.ContextMenu menu)
    {
        int curMethod = HomePage.TypingMethod.SelectedIndex;
        int curTable = HomePage.CodeTable.SelectedIndex;
        bool isAdmin = Platform.ElevationHelper.IsAdministrator();

        void UpdateItem(System.Windows.Controls.MenuItem item)
        {
            if (item.Tag is string tag)
            {
                if (tag.StartsWith("method_") && int.TryParse(tag[7..], out int mIdx))
                {
                    item.IsChecked = curMethod == mIdx;
                }
                else if (tag.StartsWith("table_") && int.TryParse(tag[6..], out int tIdx))
                {
                    item.IsChecked = curTable == tIdx;
                }
                else if (tag == "opt_shortcuts")
                {
                    item.IsChecked = ShortcutsPage.Enabled.IsChecked == true;
                }
                else if (tag == "opt_spell")
                {
                    item.IsChecked = InputPage.SpellCheck.SelectedIndex > 0;
                }
                else if (tag == "opt_suggest")
                {
                    item.IsChecked = InputPage.StripToneAuto.IsChecked == true;
                }
                else if (tag == "opt_startup")
                {
                    item.IsChecked = HomePage.StartWithWindows.IsChecked == true;
                }
                else if (tag == "admin")
                {
                    item.Header = isAdmin ? "Đang chạy quyền Admin ✓" : "Khởi động lại với quyền Admin";
                    item.IsEnabled = !isAdmin;
                }
            }

            if (item.HasItems)
            {
                foreach (var child in item.Items)
                {
                    if (child is System.Windows.Controls.MenuItem sub)
                        UpdateItem(sub);
                }
            }
        }

        foreach (var item in menu.Items)
        {
            if (item is System.Windows.Controls.MenuItem mi)
                UpdateItem(mi);
        }
    }

    private void ConvertClipboardContent()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrEmpty(text) && HomePage.CodeTable.SelectedIndex >= 0 && HomePage.CodeTable.SelectedIndex < _encodingTables.Count)
                {
                    var targetTable = _encodingTables[HomePage.CodeTable.SelectedIndex];
                    var unicodeTable = CharacterTables.Unicode;
                    if (targetTable != unicodeTable)
                    {
                        var converted = CharacterTables.Convert(text, unicodeTable, targetTable);
                        System.Windows.Clipboard.SetText(converted);
                    }
                }
                ShowBalloonNotification("VietType - Clipboard", "Đã xử lý chuyển mã nhanh nội dung Clipboard!");
            }
            else
            {
                ShowBalloonNotification("VietType - Clipboard", "Clipboard hiện không có văn bản.");
            }
        }
        catch (Exception ex)
        {
            ShowBalloonNotification("VietType - Lỗi chuyển mã", ex.Message);
        }
    }

    private void ToggleOptionSuggest()
    {
        InputPage.StripToneAuto.IsChecked = !(InputPage.StripToneAuto.IsChecked == true);
        SaveCurrentSettings();
        ShowBalloonNotification("VietType - Gợi ý từ", InputPage.StripToneAuto.IsChecked == true ? "Đã bật gợi ý từ" : "Đã tắt gợi ý từ");
    }

    private void ToggleStartupOption()
    {
        bool newVal = !(HomePage.StartWithWindows.IsChecked == true);
        HomePage.StartWithWindows.IsChecked = newVal;
        AdvancedPage.StartWithWindows.IsChecked = newVal;
        _settings.SetStartWithWindows(newVal);
        ShowBalloonNotification("VietType - Khởi động", newVal ? "Đã bật khởi động cùng Windows" : "Đã tắt khởi động cùng Windows");
    }

    private void ToggleOptionShortcuts()
    {
        ShortcutsPage.Enabled.IsChecked = !(ShortcutsPage.Enabled.IsChecked == true);
        SaveCurrentSettings();
        ShowBalloonNotification("VietType - Gõ tắt", ShortcutsPage.Enabled.IsChecked == true ? "Đã bật tính năng gõ tắt" : "Đã tắt tính năng gõ tắt");
    }

    private void ToggleOptionSpell()
    {
        InputPage.SpellCheck.SelectedIndex = InputPage.SpellCheck.SelectedIndex > 0 ? 0 : 1;
        SaveCurrentSettings();
        ShowBalloonNotification("VietType - Chính tả", InputPage.SpellCheck.SelectedIndex > 0 ? "Đã bật kiểm tra chính tả" : "Đã tắt kiểm tra chính tả");
    }

    private void SetTypingMethodFromTray(int index)
    {
        SoundFeedback.PlaySwitch();
        HomePage.TypingMethod.SelectedIndex = index;
        string name = (HomePage.TypingMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Telex";
        ShowBalloonNotification("VietType - Kiểu gõ", $"Đã chọn kiểu gõ: {name}");
    }

    private void SetCodeTableFromTray(int index)
    {
        SoundFeedback.PlaySwitch();
        HomePage.CodeTable.SelectedIndex = index;
        string name = (HomePage.CodeTable.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Unicode";
        ShowBalloonNotification("VietType - Bảng mã", $"Đã chọn bảng mã: {name}");
    }

    private void RestartApplication(bool asAdmin)
    {
        SaveCurrentSettings();
        if (asAdmin)
        {
            Platform.ElevationHelper.RestartAsAdministrator();
        }
        else
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            Application.Current?.Shutdown();
        }
    }

    public void OpenAboutWindow()
    {
        if (_aboutWindow is not null && _aboutWindow.IsLoaded)
        {
            _aboutWindow.Activate();
            _aboutWindow.Focus();
            return;
        }

        _aboutWindow = new AboutWindow();
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            _aboutWindow.Owner = this;
        }
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private void ToggleTypingEnabled()
    {
        HomePage.Toggle.IsOn = !HomePage.Toggle.IsOn;
    }

    public void ExitApplication()
    {
        _allowClose = true;
        Close();
        Application.Current?.Shutdown();
    }

    protected override void OnCloseButtonClick() => Hide();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose) { e.Cancel = true; Hide(); return; }
        SaveCurrentSettings();
        _keyboardHook.Dispose();
        if (_notifyIcon is not null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); }
        _trayIconOn?.Dispose();
        _trayIconOff?.Dispose();
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e) => SelectPage(0);
    private void NavTyping_Click(object sender, RoutedEventArgs e) => SelectPage(1);
    private void NavHotkeys_Click(object sender, RoutedEventArgs e) => SelectPage(2);
    private void NavShortcuts_Click(object sender, RoutedEventArgs e) => SelectPage(3);
    private void NavAdvanced_Click(object sender, RoutedEventArgs e) => SelectPage(4);
    private void NavAbout_Click(object sender, RoutedEventArgs e) => OpenAboutWindow();

    private void SelectPage(int index)
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

    private bool _suppressEnabledFeedback;

    private void OnEnabledChanged(object? sender, bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            _loading = true;
            HomePage.Toggle.IsOn = enabled;
            _loading = false;
            UpdateStatusVisuals();

            if (_suppressEnabledFeedback) return;
            if (enabled) SoundFeedback.PlayToggleOn(); else SoundFeedback.PlayToggleOff();
            ShowBalloonNotification("VietType - Bộ gõ", enabled ? "Đã BẬT bộ gõ tiếng Việt" : "Đã TẮT bộ gõ tiếng Việt");
        });
    }

    private void UpdateStatusVisuals()
    {
        bool enabled = HomePage.Toggle.IsOn;
        var successBrush = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        var dangerBrush = (System.Windows.Media.Brush)FindResource("DangerBrush");
        var accentBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var mutedBrush = (System.Windows.Media.Brush)FindResource("MutedTextBrush");

        SidebarStatusText.Text = enabled ? "Đang bật (V)" : "Đang tắt (E)";
        StatusDot.Fill = enabled ? successBrush : dangerBrush;

        // Cập nhật logo trạng thái V / E
        AppLogoBadge.Background = enabled ? accentBrush : mutedBrush;
        AppLogoPath.Data = (System.Windows.Media.Geometry)FindResource(enabled ? "IconStateOn" : "IconStateOff");

        // Cập nhật icon và tooltip ở khay hệ thống (System Tray)
        if (_notifyIcon is not null)
        {
            _notifyIcon.Icon = enabled ? _trayIconOn : _trayIconOff;
            _notifyIcon.Text = enabled ? "VietType: Tiếng Việt [V] (Bật)" : "VietType: English [E] (Tắt)";
        }
    }

    private void TypingMethodCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || HomePage.TypingMethod.SelectedIndex < 0) return;
        _engine.SetTypingMethod(HomePage.TypingMethod.SelectedIndex);
        UpdateStatusVisuals(); UpdatePreview(); SaveCurrentSettings();
    }

    private void CodeTableCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || HomePage.CodeTable.SelectedIndex < 0 || HomePage.CodeTable.SelectedIndex >= _encodingTables.Count) return;
        try { _engine.SetCodeTable(_encodingTables[HomePage.CodeTable.SelectedIndex]); UpdateStatusVisuals(); UpdatePreview(); SaveCurrentSettings(); }
        catch (Exception ex) { OnHookError(this, $"Không tải được bảng mã: {ex.Message}"); }
    }

    private void SpellCheckCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || InputPage.SpellCheck.SelectedIndex < 0) return;
        _engine.SpellCheckLevel = InputPage.SpellCheck.SelectedIndex; SaveCurrentSettings();
    }

    private void SettingCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            ApplySettingsToEngine();
            SaveCurrentSettings();
        }
    }

    private void PreviewInput_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void UpdatePreview() { try { HomePage.PreviewResult.Text = _engine.PreviewText(HomePage.PreviewEditor.Text); } catch { HomePage.PreviewResult.Text = HomePage.PreviewEditor.Text; } }

    private void SettingCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            ApplySettingsToEngine();
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

    private void CycleTypingMethod()
    {
        SoundFeedback.PlaySwitch();
        int next = (HomePage.TypingMethod.SelectedIndex + 1) % 4;
        HomePage.TypingMethod.SelectedIndex = next;
        string name = next switch { 0 => "Telex", 1 => "VNI", 2 => "VIQR", _ => "Telex mở rộng" };
        ShowBalloonNotification("VietType - Kiểu gõ", $"Đã chuyển sang kiểu gõ: {name}");
    }

    private void SelectUnicodeTable()
    {
        SoundFeedback.PlaySwitch();
        for (int i = 0; i < HomePage.CodeTable.Items.Count; i++)
        {
            if ((HomePage.CodeTable.Items[i] as ComboBoxItem)?.Content?.ToString() == "Unicode")
            {
                HomePage.CodeTable.SelectedIndex = i;
                break;
            }
        }
        ShowBalloonNotification("VietType - Bảng mã", "Đã chọn Bảng mã Unicode dựng sẵn.");
    }

    private void CycleCodeTable()
    {
        SoundFeedback.PlaySwitch();
        if (HomePage.CodeTable.Items.Count == 0) return;
        int next = (Math.Max(0, HomePage.CodeTable.SelectedIndex) + 1) % HomePage.CodeTable.Items.Count;
        HomePage.CodeTable.SelectedIndex = next;
        string name = (HomePage.CodeTable.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Unicode";
        ShowBalloonNotification("VietType - Bảng mã", $"Đã chuyển sang bảng mã: {name}");
    }

    private void ToggleSpellCheck()
    {
        SoundFeedback.PlaySwitch();
        int next = InputPage.SpellCheck.SelectedIndex > 0 ? 0 : 1;
        InputPage.SpellCheck.SelectedIndex = next;
        string name = next switch { 1 => "Cơ bản", 2 => "Nghiêm ngặt", _ => "Không kiểm tra" };
        ShowBalloonNotification("VietType - Chính tả", $"Mức kiểm tra chính tả: {name}");
    }

    private void ToggleShortcutsState()
    {
        SoundFeedback.PlaySwitch();
        ShortcutsPage.Enabled.IsChecked = ShortcutsPage.Enabled.IsChecked != true;
        bool state = ShortcutsPage.Enabled.IsChecked == true;
        ShowBalloonNotification("VietType - Gõ tắt", state ? "Đã BẬT tính năng gõ tắt." : "Đã TẮT tính năng gõ tắt.");
    }

    private void ShowBalloonNotification(string title, string text)
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.ShowBalloonTip(1500);
        }
    }

    private void RunAsAdmin_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool requestAdmin = InputPage.RunAsAdmin.IsChecked == true;
        bool isCurrentlyAdmin = Platform.ElevationHelper.IsAdministrator();

        if (requestAdmin && !isCurrentlyAdmin)
        {
            // Đã có task "VietType" (RunLevel Highest) → khởi động instance elevated
            // qua Task Scheduler, KHÔNG cần UAC prompt. Instance hiện tại tự thoát.
            if (Platform.VietTypeStartupTask.IsRegistered() && Platform.VietTypeStartupTask.TryRunElevated())
            {
                SaveCurrentSettings();
                ExitApplication();
                return;
            }

            var result = MessageBox.Show(
                "Để kích hoạt quyền quản trị (Administrator), VietType cần khởi động lại.\n\nBạn có muốn khởi động lại ứng dụng ngay bây giờ?",
                "Khởi động quyền quản trị - VietType",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveCurrentSettings();
                if (!Platform.ElevationHelper.RestartAsAdministrator())
                {
                    // Người dùng hủy UAC prompt
                    _loading = true;
                    InputPage.RunAsAdmin.IsChecked = false;
                    _loading = false;
                    SaveCurrentSettings();
                }
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

    private void ApplySettingsToEngine()
    {
        _engine.ModernToneRemoval = InputPage.ModernTone.IsChecked == true;
        _engine.SpellCheckLevel = Clamp(InputPage.SpellCheck.SelectedIndex < 0 ? 1 : InputPage.SpellCheck.SelectedIndex, 0, 2);
        _engine.ShortcutsEnabled = ShortcutsPage.Enabled.IsChecked == true;
        _engine.ShortcutWithoutSpace = ShortcutsPage.WithoutSpace.IsChecked == true;
        _engine.ShortcutsWhenDisabled = ShortcutsPage.WhenOff.IsChecked == true;
        _engine.SetShortcuts(_shortcuts);

        _keyboardHook.SupportGames = InputPage.SupportGames.IsChecked == true;
        _keyboardHook.SupportMetro = InputPage.SupportMetro.IsChecked == true;
        _keyboardHook.UseClipboardReplacement = AdvancedPage.UseClipboard.IsChecked == true;
        SoundFeedback.Enabled = AdvancedPage.SoundFeedback.IsChecked == true;

        _keyboardHook.HotkeyToggle = new HotkeyItem
        {
            Enabled = true,
            Ctrl = HotkeysPage.ToggleCtrl.IsChecked == true,
            Shift = HotkeysPage.ToggleShift.IsChecked == true,
            Alt = HotkeysPage.ToggleAlt.IsChecked == true,
            Win = HotkeysPage.ToggleWin.IsChecked == true,
            Key = GetSelectedKey(HotkeysPage.ToggleKey, "(Không dùng phím)")
        };

        _keyboardHook.HotkeySwitchMethod = new HotkeyItem
        {
            Enabled = !string.IsNullOrEmpty(GetSelectedKey(HotkeysPage.MethodKey, "(Tắt)")),
            Ctrl = HotkeysPage.MethodCtrl.IsChecked == true,
            Shift = HotkeysPage.MethodShift.IsChecked == true,
            Alt = HotkeysPage.MethodAlt.IsChecked == true,
            Win = HotkeysPage.MethodWin.IsChecked == true,
            Key = GetSelectedKey(HotkeysPage.MethodKey, "(Tắt)")
        };

        _keyboardHook.HotkeyRestoreWord = new HotkeyItem
        {
            Enabled = !string.IsNullOrEmpty(GetSelectedKey(HotkeysPage.RestoreKey, "(Tắt)")),
            Ctrl = HotkeysPage.RestoreCtrl.IsChecked == true,
            Shift = HotkeysPage.RestoreShift.IsChecked == true,
            Alt = HotkeysPage.RestoreAlt.IsChecked == true,
            Win = HotkeysPage.RestoreWin.IsChecked == true,
            Key = GetSelectedKey(HotkeysPage.RestoreKey, "(Tắt)")
        };

        _keyboardHook.EnableQuickFunctionKeys = HotkeysPage.QuickEnable.IsChecked == true;
        _keyboardHook.QuickKeysCtrl = HotkeysPage.QuickCtrl.IsChecked == true;
        _keyboardHook.QuickKeysShift = HotkeysPage.QuickShift.IsChecked == true;
        _keyboardHook.QuickKeysAlt = HotkeysPage.QuickAlt.IsChecked == true;
        _keyboardHook.QuickKeysWin = HotkeysPage.QuickWin.IsChecked == true;

        _keyboardHook.QuickF1 = HotkeysPage.QuickF1.IsChecked == true;
        _keyboardHook.QuickF2 = HotkeysPage.QuickF2.IsChecked == true;
        _keyboardHook.QuickF3 = HotkeysPage.QuickF3.IsChecked == true;
        _keyboardHook.QuickF4 = HotkeysPage.QuickF4.IsChecked == true;
        _keyboardHook.QuickF5 = HotkeysPage.QuickF5.IsChecked == true;
        _keyboardHook.QuickF6 = HotkeysPage.QuickF6.IsChecked == true;
        _keyboardHook.QuickF7 = HotkeysPage.QuickF7.IsChecked == true;
        _keyboardHook.QuickF8 = HotkeysPage.QuickF8.IsChecked == true;
        _keyboardHook.QuickF9 = HotkeysPage.QuickF9.IsChecked == true;
        _keyboardHook.QuickF12 = HotkeysPage.QuickF12.IsChecked == true;
    }

    private void SaveCurrentSettings()
    {
        if (_loading) return;
        try
        {
            bool startWithWin = HomePage.StartWithWindows.IsChecked == true || AdvancedPage.StartWithWindows.IsChecked == true;
            _settings.SetStartWithWindows(startWithWin);

            bool showAtStartup = HomePage.ShowWindowAtStartup.IsChecked == true || AdvancedPage.ShowWindowAtStartup.IsChecked == true;

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
            _settings.Save(settings);
        }
        catch (Exception ex) { OnHookError(this, $"Không lưu được cấu hình: {ex.Message}"); }
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
            _settings.SetStartWithWindows(false);
            _loading = false;

            ApplySettingsToEngine();
            SaveCurrentSettings();
            UpdateStatusVisuals();
            UpdatePreview();

            ShowBalloonNotification("VietType", "Đã khôi phục toàn bộ thiết lập về mặc định!");
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
            "Bạn đang sử dụng phiên bản VietType mới nhất (v1.1.0).\n\nKhông có bản cập nhật mới nào tại thời điểm này.",
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
            _trayContextMenu = null;
            SaveCurrentSettings();
        }
    }

    private void LoadShortcuts()
    {
        _shortcuts.Clear();
        foreach (var item in _settings.LoadShortcuts()) _shortcuts.Add(item);
        ApplySettingsToEngine();
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
        string trigger = ShortcutsPage.TriggerBox.Text.Trim(), replacement = ShortcutsPage.ReplacementBox.Text;
        if (trigger.Length == 0 || replacement.Length == 0) { MessageBox.Show("Hãy nhập cả từ khóa và nội dung thay thế.", "Gõ tắt", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (_shortcuts.Any(x => string.Equals(x.Trigger, trigger, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("Từ khóa này đã tồn tại.", "Gõ tắt", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var item = new ShortcutDefinition { Trigger = trigger, Replacement = replacement }; _shortcuts.Add(item); PersistShortcuts(item);
    }

    private void SaveShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsPage.Grid.SelectedItem is not ShortcutDefinition item) { AddShortcut_Click(sender, e); return; }
        string trigger = ShortcutsPage.TriggerBox.Text.Trim(), replacement = ShortcutsPage.ReplacementBox.Text;
        if (trigger.Length == 0 || replacement.Length == 0) return;
        if (_shortcuts.Any(x => !ReferenceEquals(x, item) && string.Equals(x.Trigger, trigger, StringComparison.OrdinalIgnoreCase))) return;
        item.Trigger = trigger; item.Replacement = replacement; ShortcutsPage.Grid.Items.Refresh(); PersistShortcuts(item);
    }

    private void DeleteShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsPage.Grid.SelectedItem is not ShortcutDefinition item) return;
        _shortcuts.Remove(item); _settings.SaveShortcuts(_shortcuts); ApplySettingsToEngine(); ShortcutsPage.TriggerBox.Clear(); ShortcutsPage.ReplacementBox.Clear();
    }

    private void PersistShortcuts(ShortcutDefinition item) { ShortcutsPage.Grid.SelectedItem = item; ShortcutsPage.Grid.ScrollIntoView(item); _settings.SaveShortcuts(_shortcuts); ApplySettingsToEngine(); }

    private void LoadEncodingTables()
    {
        _encodingTables.Clear();
        HomePage.CodeTable.Items.Clear();

        string folder = Path.Combine(AppContext.BaseDirectory, "Data", "EncodingTables");
        if (!Directory.Exists(folder))
        {
            string devFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "EncodingTables");
            if (Directory.Exists(devFolder))
                folder = Path.GetFullPath(devFolder);
        }

        // Tải 17 bảng mã tiêu chuẩn theo đúng thứ tự tham chiếu
        foreach (string name in CharacterTables.PredefinedTableNames)
        {
            string file = Path.Combine(folder, $"{name}.txt");
            if (File.Exists(file))
            {
                try
                {
                    string[] table = File.ReadAllLines(file);
                    if (table.Length >= 146)
                    {
                        _encodingTables.Add(table);
                        HomePage.CodeTable.Items.Add(new ComboBoxItem { Content = name });
                        continue;
                    }
                }
                catch { }
            }

            if (name == "Unicode")
            {
                _encodingTables.Add(CharacterTables.Unicode);
                HomePage.CodeTable.Items.Add(new ComboBoxItem { Content = name });
            }
        }

        // Tải thêm bất kỳ bảng mã mở rộng nào khác trong thư mục nếu người dùng tự thêm
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder, "*.txt").OrderBy(Path.GetFileName))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (CharacterTables.PredefinedTableNames.Contains(name))
                    continue;

                try
                {
                    string[] table = File.ReadAllLines(file);
                    if (table.Length >= 146)
                    {
                        _encodingTables.Add(table);
                        HomePage.CodeTable.Items.Add(new ComboBoxItem { Content = name });
                    }
                }
                catch { }
            }
        }

        if (_encodingTables.Count == 0)
        {
            _encodingTables.Add(CharacterTables.Unicode);
            HomePage.CodeTable.Items.Add(new ComboBoxItem { Content = "Unicode" });
        }
    }

    private void OnDebugLog(string message)
    {
        if (AdvancedPage.DebugTracking.IsChecked != true) return;
        Dispatcher.BeginInvoke(() => { AdvancedPage.DebugLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); AdvancedPage.DebugLog.ScrollToEnd(); });
    }

    private void OnHookError(object? sender, string message) => Dispatcher.BeginInvoke(() => { AdvancedPage.DebugLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}{Environment.NewLine}"); AdvancedPage.DebugLog.ScrollToEnd(); });
    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenPath(_settings.AppDirectory);
    private void OpenGuide_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(AppContext.BaseDirectory, "Resources", "Guide", "UsageGuide.html"));
    private void OpenEncodingTables_Click(object sender, RoutedEventArgs e) => OpenPath(Path.Combine(AppContext.BaseDirectory, "Data", "EncodingTables"));

    private static void OpenPath(string path)
    {
        try { if (File.Exists(path) || Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); else MessageBox.Show("Không tìm thấy tài nguyên.", "VietType", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "VietType", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
