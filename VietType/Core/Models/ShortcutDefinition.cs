namespace VietType.Core.Models;

public sealed class ShortcutDefinition
{
    public string Trigger { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;

    public ShortcutDefinition Clone() => new() { Trigger = Trigger, Replacement = Replacement };
}
