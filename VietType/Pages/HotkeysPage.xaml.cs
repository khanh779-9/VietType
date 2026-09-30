using System.Windows;
using System.Windows.Controls;

namespace VietType.Pages;

public partial class HotkeysPage : UserControl
{
    public HotkeysPage()
    {
        InitializeComponent();
        PopulateKeyCombos();
    }

    private void PopulateKeyCombos()
    {
        string[] toggleKeys = ["(Không dùng phím)", "Z", "Space", "~", "A", "S", "V", "F", "W"];
        foreach (var k in toggleKeys) HkToggleKey.Items.Add(k);

        string[] methodKeys = ["(Tắt)", "Z", "F", "J", "Space", "M", "W"];
        foreach (var k in methodKeys) HkMethodKey.Items.Add(k);

        string[] restoreKeys = ["(Tắt)", "Z", "Backspace", "Space", "R", "U"];
        foreach (var k in restoreKeys) HkRestoreKey.Items.Add(k);
    }

    // Phím chuyển E/V
    public CheckBox ToggleCtrl => HkToggleCtrl;
    public CheckBox ToggleShift => HkToggleShift;
    public CheckBox ToggleAlt => HkToggleAlt;
    public CheckBox ToggleWin => HkToggleWin;
    public ComboBox ToggleKey => HkToggleKey;

    // Phím chuyển cơ chế
    public CheckBox MethodCtrl => HkMethodCtrl;
    public CheckBox MethodShift => HkMethodShift;
    public CheckBox MethodAlt => HkMethodAlt;
    public CheckBox MethodWin => HkMethodWin;
    public ComboBox MethodKey => HkMethodKey;

    // Phím phục hồi từ
    public CheckBox RestoreCtrl => HkRestoreCtrl;
    public CheckBox RestoreShift => HkRestoreShift;
    public CheckBox RestoreAlt => HkRestoreAlt;
    public CheckBox RestoreWin => HkRestoreWin;
    public ComboBox RestoreKey => HkRestoreKey;

    // Phím chức năng nhanh F1 - F12
    public CheckBox QuickEnable => HkQuickEnable;
    public CheckBox QuickCtrl => HkQuickCtrl;
    public CheckBox QuickShift => HkQuickShift;
    public CheckBox QuickAlt => HkQuickAlt;
    public CheckBox QuickWin => HkQuickWin;

    public CheckBox QuickF1 => HkQuickF1;
    public CheckBox QuickF2 => HkQuickF2;
    public CheckBox QuickF3 => HkQuickF3;
    public CheckBox QuickF4 => HkQuickF4;
    public CheckBox QuickF5 => HkQuickF5;
    public CheckBox QuickF6 => HkQuickF6;
    public CheckBox QuickF7 => HkQuickF7;
    public CheckBox QuickF8 => HkQuickF8;
    public CheckBox QuickF9 => HkQuickF9;
    public CheckBox QuickF12 => HkQuickF12;

    public event RoutedEventHandler? SettingsChanged;
    public event SelectionChangedEventHandler? SettingComboChanged;

    private void SettingChanged(object sender, RoutedEventArgs e) => SettingsChanged?.Invoke(sender, e);
    private void SettingCombo_Changed(object sender, SelectionChangedEventArgs e) => SettingComboChanged?.Invoke(sender, e);
}
