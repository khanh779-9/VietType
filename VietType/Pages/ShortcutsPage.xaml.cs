using System.Windows;
using System.Windows.Controls;

namespace VietType.Pages;

public partial class ShortcutsPage : UserControl
{
    public ShortcutsPage()
    {
        InitializeComponent();
    }

    // Bảng gõ tắt
    public DataGrid Grid => ShortcutGrid;
    public TextBox TriggerBox => ShortcutTriggerBox;
    public TextBox ReplacementBox => ShortcutReplacementBox;
    public CheckBox Enabled => EnableShortcutsCheck;
    public CheckBox WithoutSpace => ShortcutWithoutSpaceCheck;
    public CheckBox WhenOff => ShortcutWhenOffCheck;

    public event SelectionChangedEventHandler? ShortcutSelected;
    public event RoutedEventHandler? AddRequested;
    public event RoutedEventHandler? SaveRequested;
    public event RoutedEventHandler? DeleteRequested;
    public event RoutedEventHandler? SettingsChanged;

    private void ShortcutGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShortcutSelected?.Invoke(sender, e);
    private void Add_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke(sender, e);
    private void Save_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(sender, e);
    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(sender, e);
    private void SettingChanged(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(sender, e);
}
