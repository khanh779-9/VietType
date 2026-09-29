namespace VietType.Core.Models;

public sealed class AppConfiguration
{
    public bool Enabled { get; set; } = true;
    public int TypingMethod { get; set; }
    public int CodeTable { get; set; }
    public string? CodeTableName { get; set; }
    public bool ModernToneRemoval { get; set; }
    public bool ShowWindowAtStartup { get; set; } = true;
    public int SpellCheckLevel { get; set; } = 1;
    public bool EnableShortcuts { get; set; }
    public bool ShortcutWithoutSpace { get; set; }
    public bool ShortcutsWhenVietnameseOff { get; set; }
    public bool StripToneInAutocomplete { get; set; }
    public bool DebugTracking { get; set; }
    public bool StartWithWindows { get; set; }
    public bool UseCtrlShiftHotkey { get; set; } = true;
    public string AltHotkeyKey { get; set; } = "Z";
    public string Theme { get; set; } = "Light";
    public bool RunAsAdmin { get; set; }
    public bool SupportGames { get; set; } = true;
    public bool SupportMetro { get; set; } = true;
    public bool UseClipboardReplacement { get; set; } = false;

    // Chi tiết thiết lập phím tắt
    public HotkeyItem HotkeyToggle { get; set; } = new() { Ctrl = true, Shift = true, Key = "" };
    public HotkeyItem HotkeySwitchMethod { get; set; } = new() { Alt = true, Shift = true, Key = "Z" };
    public HotkeyItem HotkeyRestoreWord { get; set; } = new() { Ctrl = true, Shift = true, Key = "Z" };

    // Phím chức năng nhanh (F1 - F12)
    public bool EnableQuickFunctionKeys { get; set; } = true;
    public bool QuickKeysCtrl { get; set; } = true;
    public bool QuickKeysShift { get; set; } = true;
    public bool QuickKeysAlt { get; set; } = true;
    public bool QuickKeysWin { get; set; } = false;

    public bool QuickF1 { get; set; } = true;
    public bool QuickF2 { get; set; } = true;
    public bool QuickF3 { get; set; } = true;
    public bool QuickF5 { get; set; } = true;
    public bool QuickF8 { get; set; } = true;
    public bool QuickF9 { get; set; } = true;
    public bool QuickF12 { get; set; } = true;
}
