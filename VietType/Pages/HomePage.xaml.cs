using System.Windows;
using System.Windows.Controls;

using VietType.Controls;

namespace VietType.Pages;

public partial class HomePage : UserControl
{
    public HomePage() => InitializeComponent();

    public ToggleSwitch Toggle => ToggleEnabled;
    public ComboBox TypingMethod => TypingMethodCombo;
    public ComboBox CodeTable => CodeTableCombo;
    public TextBox PreviewEditor => PreviewInput;
    public CheckBox ShowWindowAtStartup => ShowWindowAtStartupCheck;
    public CheckBox StartWithWindows => StartWithWindowsCheck;
    public TextBlock PreviewResult => PreviewOutput;

    public ComboBox QuickToggleCombo => QuickToggleHotkeyCombo;
    public ComboBox QuickMethodCombo => QuickMethodHotkeyCombo;
    public ComboBox QuickRestoreCombo => QuickRestoreHotkeyCombo;

    public event RoutedEventHandler? SettingsChanged;
    public event RoutedEventHandler? EnabledChanged;
    public event SelectionChangedEventHandler? TypingMethodChanged;
    public event SelectionChangedEventHandler? CodeTableChanged;
    public event TextChangedEventHandler? PreviewChanged;

    public event SelectionChangedEventHandler? QuickToggleChanged;
    public event SelectionChangedEventHandler? QuickMethodChanged;
    public event SelectionChangedEventHandler? QuickRestoreChanged;
    public event RoutedEventHandler? CustomizeHotkeysRequested;

    private void ToggleEnabled_Toggled(object sender, RoutedEventArgs e) => EnabledChanged?.Invoke(sender, e);
    private void TypingMethodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => TypingMethodChanged?.Invoke(sender, e);
    private void CodeTableCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => CodeTableChanged?.Invoke(sender, e);
    private void PreviewInput_TextChanged(object sender, TextChangedEventArgs e) => PreviewChanged?.Invoke(sender, e);
    private void SettingChanged(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(sender, e);

    private void QuickToggleCombo_Changed(object sender, SelectionChangedEventArgs e) => QuickToggleChanged?.Invoke(sender, e);
    private void QuickMethodCombo_Changed(object sender, SelectionChangedEventArgs e) => QuickMethodChanged?.Invoke(sender, e);
    private void QuickRestoreCombo_Changed(object sender, SelectionChangedEventArgs e) => QuickRestoreChanged?.Invoke(sender, e);
    private void CustomizeHotkeys_Click(object sender, RoutedEventArgs e) => CustomizeHotkeysRequested?.Invoke(sender, e);
}
