using System.Windows;
using System.Windows.Controls;

namespace VietType.Pages;

public partial class InputPage : UserControl
{
    public InputPage()
    {
        InitializeComponent();
    }

    public ComboBox SpellCheck => SpellCheckCombo;
    public CheckBox ModernTone => ModernToneCheck;
    public CheckBox StripToneAuto => StripToneAutoCheck;

    public CheckBox RunAsAdmin => RunAsAdminCheck;
    public CheckBox SupportGames => SupportGamesCheck;
    public CheckBox SupportMetro => SupportMetroCheck;
    public TextBlock AdminBadge => AdminStatusBadge;

    public event SelectionChangedEventHandler? SpellCheckChanged;
    public event RoutedEventHandler? SettingsChanged;
    public event RoutedEventHandler? RunAsAdminChanged;

    private void SpellCheckCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SpellCheckChanged?.Invoke(sender, e);
    private void SettingChanged(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(sender, e);
    private void RunAsAdmin_Click(object sender, RoutedEventArgs e) => RunAsAdminChanged?.Invoke(sender, e);
}
