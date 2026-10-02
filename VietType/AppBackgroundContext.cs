using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using VietType.Core.Models;
using VietType.Core.Typing;
using VietType.Infrastructure;
using VietType.Platform;
using VietType.Platform.Keyboard;
using VietType.Themes;
using Forms = System.Windows.Forms;

namespace VietType;

public sealed class AppBackgroundContext : IDisposable
{
    public SettingsRepository Settings { get; } = new();
    public TextInputEngine Engine { get; } = new(CharacterTables.Unicode);
    public KeyboardHook KeyboardHook { get; }
    public ObservableCollection<ShortcutDefinition> Shortcuts { get; } = new();
    public List<string[]> EncodingTables { get; } = new();

    public Forms.NotifyIcon? NotifyIcon { get; private set; }
    private System.Drawing.Icon? _trayIconOn;
    private System.Drawing.Icon? _trayIconOff;
    private System.Windows.Controls.ContextMenu? _trayContextMenu;
    private HwndSource? _messageSink;
    private MainWindow? _mainWindow;
    private AboutWindow? _aboutWindow;
    private bool _suppressEnabledFeedback;
    private bool _isDisposed;

    public event Action<bool>? EnabledChanged;
    public event Action<int>? TypingMethodChanged;
    public event Action<int>? CodeTableChanged;
    public event Action<int>? SpellCheckChanged;
    public event Action<bool>? ModernToneChanged;
    public event Action<bool>? ShortcutsChanged;

    public AppBackgroundContext()
    {
        KeyboardHook = new KeyboardHook { Engine = Engine };
    }

    public void Initialize()
    {
        var settings = Settings.Load();
        ThemeManager.Apply(ParseTheme(settings.Theme));

        LoadEncodingTables();
        LoadShortcuts();

        ApplySettingsToEngine(settings);

        KeyboardHook.EnabledChanged += OnHookEnabledChanged;
        KeyboardHook.Error += OnHookError;

        KeyboardHook.SwitchTypingMethodRequested += () => Application.Current.Dispatcher.Invoke(CycleTypingMethod);
        KeyboardHook.SelectUnicodeRequested += () => Application.Current.Dispatcher.Invoke(SelectUnicodeTable);
        KeyboardHook.CycleCodeTableRequested += () => Application.Current.Dispatcher.Invoke(CycleCodeTable);
        KeyboardHook.ToggleSpellCheckRequested += () => Application.Current.Dispatcher.Invoke(ToggleSpellCheck);
        KeyboardHook.OpenDashboardRequested += () => Application.Current.Dispatcher.Invoke(() => ShowMainWindow());
        KeyboardHook.OpenShortcutsRequested += () => Application.Current.Dispatcher.Invoke(() => ShowMainWindow(3));
        KeyboardHook.ToggleShortcutsRequested += () => Application.Current.Dispatcher.Invoke(ToggleShortcutsState);

        _suppressEnabledFeedback = true;
        KeyboardHook.IsEnabled = settings.Enabled;
        _suppressEnabledFeedback = false;
        KeyboardHook.Install();

        // Tự động duy trì Task Scheduler khi đang chạy quyền Administrator:
        // Đảm bảo task luôn tồn tại để các lần sau hoặc khi cần elevate sẽ KHÔNG hiện UAC prompt.
        if (ElevationHelper.IsAdministrator())
        {
            bool startWithWin = Settings.IsStartWithWindows();
            VietTypeStartupTask.Register(enableLogonTrigger: startWithWin);
        }

        BuildTrayIcon();
        UpdateTrayStatus(settings.Enabled);

        // Chỉ hiển thị cửa sổ chính khi cấu hình yêu cầu
        if (settings.ShowWindowAtStartup)
        {
            ShowMainWindow();
        }
    }

    public void LoadEncodingTables()
    {
        EncodingTables.Clear();
        string baseDir = Path.Combine(AppContext.BaseDirectory, "Data", "EncodingTables");
        for (int i = 0; i < CharacterTables.PredefinedTableFiles.Length; i++)
        {
            string fileName = CharacterTables.PredefinedTableFiles[i] + ".txt";
            string path = Path.Combine(baseDir, fileName);
            if (File.Exists(path))
            {
                try
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length >= 146)
                    {
                        EncodingTables.Add(lines);
                        continue;
                    }
                }
                catch { }
            }
            EncodingTables.Add(CharacterTables.Unicode);
        }
    }

    public void LoadShortcuts()
    {
        Shortcuts.Clear();
        foreach (var item in Settings.LoadShortcuts())
            Shortcuts.Add(item);
        Engine.SetShortcuts(Shortcuts);
    }

    public void ApplySettingsToEngine(AppConfiguration settings)
    {
        Engine.ModernToneRemoval = settings.ModernToneRemoval;
        Engine.SpellCheckLevel = Math.Clamp(settings.SpellCheckLevel, 0, 2);
        Engine.ShortcutsEnabled = settings.EnableShortcuts;
        Engine.ShortcutWithoutSpace = settings.ShortcutWithoutSpace;
        Engine.ShortcutsWhenDisabled = settings.ShortcutsWhenVietnameseOff;
        Engine.SetShortcuts(Shortcuts);

        Engine.SetTypingMethod(settings.TypingMethod);
        if (settings.CodeTable >= 0 && settings.CodeTable < EncodingTables.Count)
        {
            try { Engine.SetCodeTable(EncodingTables[settings.CodeTable]); } catch { }
        }

        KeyboardHook.SupportGames = settings.SupportGames;
        KeyboardHook.SupportMetro = settings.SupportMetro;
        KeyboardHook.UseClipboardReplacement = settings.UseClipboardReplacement;
        SoundFeedback.Enabled = settings.SoundFeedback;

        KeyboardHook.HotkeyToggle = settings.HotkeyToggle;
        KeyboardHook.HotkeySwitchMethod = settings.HotkeySwitchMethod;
        KeyboardHook.HotkeyRestoreWord = settings.HotkeyRestoreWord;

        KeyboardHook.EnableQuickFunctionKeys = settings.EnableQuickFunctionKeys;
        KeyboardHook.QuickKeysCtrl = settings.QuickKeysCtrl;
        KeyboardHook.QuickKeysShift = settings.QuickKeysShift;
        KeyboardHook.QuickKeysAlt = settings.QuickKeysAlt;
        KeyboardHook.QuickKeysWin = settings.QuickKeysWin;

        KeyboardHook.QuickF1 = settings.QuickF1;
        KeyboardHook.QuickF2 = settings.QuickF2;
        KeyboardHook.QuickF3 = settings.QuickF3;
        KeyboardHook.QuickF4 = settings.QuickF4;
        KeyboardHook.QuickF5 = settings.QuickF5;
        KeyboardHook.QuickF6 = settings.QuickF6;
        KeyboardHook.QuickF7 = settings.QuickF7;
        KeyboardHook.QuickF8 = settings.QuickF8;
        KeyboardHook.QuickF9 = settings.QuickF9;
        KeyboardHook.QuickF12 = settings.QuickF12;
    }

    public void SaveConfiguration(AppConfiguration settings)
    {
        Settings.Save(settings);
        Settings.SetStartWithWindows(settings.StartWithWindows);
        ApplySettingsToEngine(settings);
    }

    public void ShowMainWindow(int pageIndex = -1)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_mainWindow is null || !_mainWindow.IsLoaded)
            {
                _mainWindow = new MainWindow(this);
                _mainWindow.Closed += (_, _) => _mainWindow = null;
            }

            if (pageIndex >= 0)
                _mainWindow.SelectPage(pageIndex);

            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    public void OnMainWindowClosed()
    {
        _mainWindow = null;
    }

    public void ToggleTypingEnabled()
    {
        KeyboardHook.ToggleEnabled();
    }

    private void OnHookEnabledChanged(object? sender, bool enabled)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            UpdateTrayStatus(enabled);
            EnabledChanged?.Invoke(enabled);

            if (_suppressEnabledFeedback) return;
            if (enabled) SoundFeedback.PlayToggleOn(); else SoundFeedback.PlayToggleOff();
            ShowBalloonNotification("VietType - Bộ gõ", enabled ? "Đã BẬT bộ gõ tiếng Việt [V]" : "Đã TẮT bộ gõ tiếng Việt [E]");
        });
    }

    private void OnHookError(object? sender, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ShowBalloonNotification("VietType - Lỗi", message);
        });
    }

    public void UpdateTrayStatus(bool enabled)
    {
        if (NotifyIcon is not null)
        {
            NotifyIcon.Icon = enabled ? _trayIconOn : _trayIconOff;
            NotifyIcon.Text = enabled ? "VietType: Tiếng Việt [V] (Bật)" : "VietType: English [E] (Tắt)";
        }
    }

    private void BuildTrayIcon()
    {
        _trayIconOn = CreateBadgeIcon("V", System.Drawing.Color.FromArgb(220, 38, 38), System.Drawing.Color.White);
        _trayIconOff = CreateBadgeIcon("E", System.Drawing.Color.FromArgb(37, 99, 235), System.Drawing.Color.White);

        NotifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconOn,
            Visible = true,
            Text = "VietType • Tiếng Việt [V]"
        };

        NotifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                Application.Current.Dispatcher.Invoke(ToggleTypingEnabled);
            else if (e.Button == Forms.MouseButtons.Right)
                Application.Current.Dispatcher.Invoke(ShowTrayContextMenu);
        };
        NotifyIcon.DoubleClick += (_, _) => Application.Current.Dispatcher.Invoke(() => ShowMainWindow());
    }

    private void EnsureMessageSink()
    {
        if (_messageSink is null)
        {
            var parameters = new HwndSourceParameters("VietTypeTraySink")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0
            };
            _messageSink = new HwndSource(parameters);
        }
    }

    private void ShowTrayContextMenu()
    {
        EnsureMessageSink();
        _trayContextMenu = CreateTrayContextMenu();
        UpdateTrayContextMenu(_trayContextMenu);

        if (_messageSink is not null)
            NativeMethods.SetForegroundWindow(_messageSink.Handle);

        _trayContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _trayContextMenu.IsOpen = true;
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var itemConvert = new System.Windows.Controls.MenuItem { Header = "Chuyển mã ..." };
        itemConvert.Click += (_, _) => ShowMainWindow(1);
        menu.Items.Add(itemConvert);

        var itemShortcuts = new System.Windows.Controls.MenuItem { Header = "Gõ tắt ..." };
        itemShortcuts.Click += (_, _) => ShowMainWindow(3);
        menu.Items.Add(itemShortcuts);

        var itemSuggest = new System.Windows.Controls.MenuItem { Header = "Gợi ý từ (Auto Complete)", Tag = "opt_suggest" };
        itemSuggest.Click += (_, _) => ToggleOptionSuggest();
        menu.Items.Add(itemSuggest);

        menu.Items.Add(new Separator());

        // Kiểu gõ
        var itemMethod = new System.Windows.Controls.MenuItem { Header = "Kiểu gõ" };
        var mTelex = new System.Windows.Controls.MenuItem { Header = "Telex", Tag = "method_0" };
        mTelex.Click += (_, _) => SetTypingMethod(0);
        itemMethod.Items.Add(mTelex);

        var mVni = new System.Windows.Controls.MenuItem { Header = "VNI", Tag = "method_1" };
        mVni.Click += (_, _) => SetTypingMethod(1);
        itemMethod.Items.Add(mVni);

        var mViqr = new System.Windows.Controls.MenuItem { Header = "VIQR", Tag = "method_2" };
        mViqr.Click += (_, _) => SetTypingMethod(2);
        itemMethod.Items.Add(mViqr);

        var mTelexExt = new System.Windows.Controls.MenuItem { Header = "Telex mở rộng", Tag = "method_3" };
        mTelexExt.Click += (_, _) => SetTypingMethod(3);
        itemMethod.Items.Add(mTelexExt);
        menu.Items.Add(itemMethod);

        // Bảng mã
        var itemTable = new System.Windows.Controls.MenuItem { Header = "Bảng mã" };
        for (int i = 0; i < CharacterTables.PredefinedTableNames.Length; i++)
        {
            int index = i;
            var mi = new System.Windows.Controls.MenuItem
            {
                Header = CharacterTables.PredefinedTableNames[i],
                Tag = $"table_{i}"
            };
            mi.Click += (_, _) => SetCodeTable(index);
            itemTable.Items.Add(mi);
        }
        menu.Items.Add(itemTable);

        menu.Items.Add(new Separator());

        // Tùy chọn khác
        var itemOtherOptions = new System.Windows.Controls.MenuItem { Header = "Tùy chọn khác" };

        var optShortcuts = new System.Windows.Controls.MenuItem { Header = "Bật tính năng gõ tắt", Tag = "opt_shortcuts" };
        optShortcuts.Click += (_, _) => ToggleOptionShortcuts();
        itemOtherOptions.Items.Add(optShortcuts);

        var optSpell = new System.Windows.Controls.MenuItem { Header = "Kiểm tra chính tả", Tag = "opt_spell" };
        optSpell.Click += (_, _) => ToggleOptionSpell();
        itemOtherOptions.Items.Add(optSpell);

        var optRestore = new System.Windows.Controls.MenuItem { Header = "Phục hồi từ gốc" };
        optRestore.Click += (_, _) => TriggerRestoreWord();
        itemOtherOptions.Items.Add(optRestore);

        var optModernTone = new System.Windows.Controls.MenuItem { Header = "Cho phép kiểu gõ hiện đại (hoà, thuỷ, khoẻ...)", Tag = "opt_moderntone" };
        optModernTone.Click += (_, _) => ToggleOptionModernTone();
        itemOtherOptions.Items.Add(optModernTone);

        var optStartup = new System.Windows.Controls.MenuItem { Header = "Khởi động cùng Windows", Tag = "opt_startup" };
        optStartup.Click += (_, _) => ToggleStartupOption();
        itemOtherOptions.Items.Add(optStartup);

        menu.Items.Add(itemOtherOptions);

        var itemAdmin = new System.Windows.Controls.MenuItem { Header = "Khởi động lại với quyền Admin", Tag = "admin" };
        itemAdmin.Click += (_, _) => RestartApplication(true);
        menu.Items.Add(itemAdmin);

        var itemDashboard = new System.Windows.Controls.MenuItem
        {
            Header = "Hiện cửa sổ chính",
            FontWeight = FontWeights.Bold
        };
        itemDashboard.Click += (_, _) => ShowMainWindow();
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
        var cfg = Settings.Load();
        int curMethod = Engine.TypingMethodIndex;
        int curTable = cfg.CodeTable;
        bool isAdmin = Platform.ElevationHelper.IsAdministrator();

        void UpdateItem(System.Windows.Controls.MenuItem item)
        {
            if (item.Tag is string tag)
            {
                if (tag.StartsWith("method_") && int.TryParse(tag[7..], out int mIdx))
                {
                    item.IsChecked = mIdx == curMethod;
                }
                else if (tag.StartsWith("table_") && int.TryParse(tag[6..], out int tIdx))
                {
                    item.IsChecked = tIdx == curTable;
                }
                else if (tag == "opt_shortcuts")
                {
                    item.IsChecked = Engine.ShortcutsEnabled;
                }
                else if (tag == "opt_spell")
                {
                    item.IsChecked = Engine.SpellCheckLevel > 0;
                }
                else if (tag == "opt_suggest")
                {
                    item.IsChecked = cfg.StripToneInAutocomplete;
                }
                else if (tag == "opt_moderntone")
                {
                    item.IsChecked = Engine.ModernToneRemoval;
                }
                else if (tag == "opt_startup")
                {
                    item.IsChecked = Settings.IsStartWithWindows();
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

    public void CycleTypingMethod()
    {
        SoundFeedback.PlaySwitch();
        int next = (Engine.TypingMethodIndex + 1) % 4;
        SetTypingMethod(next);
        string name = next switch { 0 => "Telex", 1 => "VNI", 2 => "VIQR", _ => "Telex mở rộng" };
        ShowBalloonNotification("VietType - Kiểu gõ", $"Đã chuyển sang kiểu gõ: {name}");
    }

    public void SetTypingMethod(int index)
    {
        Engine.SetTypingMethod(index);
        var cfg = Settings.Load();
        cfg.TypingMethod = index;
        Settings.Save(cfg);
        TypingMethodChanged?.Invoke(index);
    }

    public void SelectUnicodeTable()
    {
        SoundFeedback.PlaySwitch();
        SetCodeTable(0);
        ShowBalloonNotification("VietType - Bảng mã", "Đã chọn Bảng mã Unicode dựng sẵn.");
    }

    public void CycleCodeTable()
    {
        SoundFeedback.PlaySwitch();
        if (EncodingTables.Count == 0) return;
        var cfg = Settings.Load();
        int next = (Math.Max(0, cfg.CodeTable) + 1) % EncodingTables.Count;
        SetCodeTable(next);
        string name = next < CharacterTables.PredefinedTableNames.Length ? CharacterTables.PredefinedTableNames[next] : "Unicode";
        ShowBalloonNotification("VietType - Bảng mã", $"Đã chuyển sang bảng mã: {name}");
    }

    public void SetCodeTable(int index)
    {
        if (index < 0 || index >= EncodingTables.Count) return;
        try
        {
            Engine.SetCodeTable(EncodingTables[index]);
            var cfg = Settings.Load();
            cfg.CodeTable = index;
            cfg.CodeTableName = index < CharacterTables.PredefinedTableNames.Length ? CharacterTables.PredefinedTableNames[index] : "Unicode";
            Settings.Save(cfg);
            CodeTableChanged?.Invoke(index);
        }
        catch (Exception ex)
        {
            ShowBalloonNotification("VietType - Lỗi", $"Không tải được bảng mã: {ex.Message}");
        }
    }

    public void ToggleSpellCheck()
    {
        SoundFeedback.PlaySwitch();
        int next = Engine.SpellCheckLevel > 0 ? 0 : 1;
        Engine.SpellCheckLevel = next;
        var cfg = Settings.Load();
        cfg.SpellCheckLevel = next;
        Settings.Save(cfg);
        SpellCheckChanged?.Invoke(next);
        string name = next switch { 1 => "Cơ bản", 2 => "Nghiêm ngặt", _ => "Không kiểm tra" };
        ShowBalloonNotification("VietType - Chính tả", $"Mức kiểm tra chính tả: {name}");
    }

    public void ToggleShortcutsState()
    {
        SoundFeedback.PlaySwitch();
        bool next = !Engine.ShortcutsEnabled;
        Engine.ShortcutsEnabled = next;
        var cfg = Settings.Load();
        cfg.EnableShortcuts = next;
        Settings.Save(cfg);
        ShortcutsChanged?.Invoke(next);
        ShowBalloonNotification("VietType - Gõ tắt", next ? "Đã BẬT tính năng gõ tắt." : "Đã TẮT tính năng gõ tắt.");
    }

    public void ToggleOptionShortcuts() => ToggleShortcutsState();

    public void ToggleOptionSpell() => ToggleSpellCheck();

    public void ToggleOptionSuggest()
    {
        var cfg = Settings.Load();
        cfg.StripToneInAutocomplete = !cfg.StripToneInAutocomplete;
        Settings.Save(cfg);
        ShowBalloonNotification("VietType - Gợi ý từ", cfg.StripToneInAutocomplete ? "Đã bật gợi ý từ" : "Đã tắt gợi ý từ");
    }

    public void ToggleOptionModernTone()
    {
        bool next = !Engine.ModernToneRemoval;
        Engine.ModernToneRemoval = next;
        var cfg = Settings.Load();
        cfg.ModernToneRemoval = next;
        Settings.Save(cfg);
        ModernToneChanged?.Invoke(next);
        ShowBalloonNotification("VietType - Kiểu dấu", next ? "Đã bật: Gõ 'oà', 'uý' thay vì 'òa', 'úy'" : "Đã tắt: Gõ 'oà', 'uý' thay vì 'òa', 'úy'");
    }

    public void ToggleStartupOption()
    {
        bool newVal = !Settings.IsStartWithWindows();
        Settings.SetStartWithWindows(newVal);
        var cfg = Settings.Load();
        cfg.StartWithWindows = newVal;
        Settings.Save(cfg);
        ShowBalloonNotification("VietType - Khởi động", newVal ? "Đã bật khởi động cùng Windows" : "Đã tắt khởi động cùng Windows");
    }

    public void TriggerRestoreWord()
    {
        KeyboardHook.TriggerRestoreWord();
    }

    public void OpenAboutWindow()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_aboutWindow is null || !_aboutWindow.IsLoaded)
            {
                _aboutWindow = new AboutWindow();
                _aboutWindow.Closed += (_, _) => _aboutWindow = null;
            }
            _aboutWindow.Show();
            _aboutWindow.Activate();
        });
    }

    public void RestartApplication(bool asAdmin)
    {
        if (asAdmin)
        {
            if (ElevationHelper.RestartAsAdministrator())
                ExitApplication();
        }
        else
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
            ExitApplication();
        }
    }

    public void ExitApplication()
    {
        Dispose();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Application.Current.Shutdown();
        });
    }

    public void ShowBalloonNotification(string title, string text)
    {
        if (NotifyIcon is not null)
        {
            NotifyIcon.BalloonTipTitle = title;
            NotifyIcon.BalloonTipText = text;
            NotifyIcon.ShowBalloonTip(1500);
        }
    }

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

                using var bgBrush = new SolidBrush(bgColor);
                g.FillPath(bgBrush, path);
            }

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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        KeyboardHook.Dispose();

        if (NotifyIcon is not null)
        {
            NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
            NotifyIcon = null;
        }

        _trayIconOn?.Dispose();
        _trayIconOff?.Dispose();
        _messageSink?.Dispose();

        if (_mainWindow is not null)
        {
            try { _mainWindow.Close(); } catch { }
            _mainWindow = null;
        }

        if (_aboutWindow is not null)
        {
            try { _aboutWindow.Close(); } catch { }
            _aboutWindow = null;
        }
    }
}
