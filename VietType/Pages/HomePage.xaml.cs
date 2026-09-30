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

    public event RoutedEventHandler? SettingsChanged;
    public event RoutedEventHandler? EnabledChanged;
    public event SelectionChangedEventHandler? TypingMethodChanged;
    public event SelectionChangedEventHandler? CodeTableChanged;
    public event TextChangedEventHandler? PreviewChanged;

    private void ToggleEnabled_Toggled(object sender, RoutedEventArgs e) => EnabledChanged?.Invoke(sender, e);
    private void TypingMethodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => TypingMethodChanged?.Invoke(sender, e);
    private void CodeTableCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => CodeTableChanged?.Invoke(sender, e);
    private void PreviewInput_TextChanged(object sender, TextChangedEventArgs e) => PreviewChanged?.Invoke(sender, e);
    private void SettingChanged(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(sender, e);
}
