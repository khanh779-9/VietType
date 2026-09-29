namespace VietType.Core.Models;

public sealed class HotkeyItem
{
    public bool Enabled { get; set; } = true;
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = string.Empty;
}
