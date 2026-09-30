using System.Windows;
using System.Windows.Controls;

namespace VietType.Pages;

public partial class AdvancedPage : UserControl
{
    public AdvancedPage() => InitializeComponent();
    public ComboBox Theme => ThemeCombo;
    public CheckBox StartWithWindows => StartWithWindowsCheck;
    public CheckBox ShowWindowAtStartup => ShowWindowAtStartupCheck;
    public CheckBox UseClipboard => UseClipboardCheck;
    public CheckBox SoundFeedback => SoundFeedbackCheck;
    public CheckBox DebugTracking => DebugTrackingCheck;
    public TextBox DebugLog => DebugLogBox;

    public event SelectionChangedEventHandler? ThemeChanged;
    public event RoutedEventHandler? SettingsChanged;
    public event RoutedEventHandler? ResetDefaultsRequested;
    public event RoutedEventHandler? CheckUpdatesRequested;
    public event RoutedEventHandler? DataFolderRequested;
    public event RoutedEventHandler? GuideRequested;
    public event RoutedEventHandler? EncodingTablesRequested;

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ThemeChanged?.Invoke(sender, e);
    private void SettingChanged(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(sender, e);
    private void ResetDefaults_Click(object sender, RoutedEventArgs e) => ResetDefaultsRequested?.Invoke(sender, e);
    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => CheckUpdatesRequested?.Invoke(sender, e);
    private void DataFolder_Click(object sender, RoutedEventArgs e) => DataFolderRequested?.Invoke(sender, e);
    private void Guide_Click(object sender, RoutedEventArgs e) => GuideRequested?.Invoke(sender, e);
    private void EncodingTables_Click(object sender, RoutedEventArgs e) => EncodingTablesRequested?.Invoke(sender, e);
}
