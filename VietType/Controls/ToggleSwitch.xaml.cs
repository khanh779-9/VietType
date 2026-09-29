using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VietType.Controls;

public partial class ToggleSwitch : UserControl
{
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(ToggleSwitch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOnChanged));
    public bool IsOn { get => (bool)GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
    public event RoutedEventHandler? Toggled;

    public ToggleSwitch() => InitializeComponent();

    private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToggleSwitch control)
        {
            bool on = (bool)e.NewValue;
            control.Track.Background = on ? control.FindResource("AccentBrush") as Brush : control.FindResource("BorderBrushSoft") as Brush;
            control.Thumb.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            control.Toggled?.Invoke(control, new RoutedEventArgs());
        }
    }

    private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => IsOn = !IsOn;
}
