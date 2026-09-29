using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VietType.Controls;

public partial class NavItem : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(NavItem), new PropertyMetadata("Menu", OnTextChanged));

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(NavItem), new PropertyMetadata(null, OnIconChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(NavItem), new PropertyMetadata(false, OnSelectedChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set { SetValue(TextProperty, value); LabelText.Text = value; }
    }

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public event RoutedEventHandler? Click;

    private bool _isHovered;

    public NavItem()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisualState();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavItem item) item.LabelText.Text = e.NewValue?.ToString() ?? string.Empty;
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavItem item) item.Icon.Data = e.NewValue as Geometry;
    }

    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavItem item) item.UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (IsSelected)
        {
            Root.Background = TryFindResource("SidebarHoverBrush") as Brush ?? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            Icon.Fill = TryFindResource("AccentBrush") as Brush ?? Brushes.White;
            LabelText.Foreground = Brushes.White;
            LabelText.FontWeight = FontWeights.SemiBold;
            ActiveIndicator.Visibility = Visibility.Visible;
        }
        else if (_isHovered)
        {
            Root.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            Icon.Fill = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            LabelText.Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            LabelText.FontWeight = FontWeights.Medium;
            ActiveIndicator.Visibility = Visibility.Collapsed;
        }
        else
        {
            Root.Background = Brushes.Transparent;
            Icon.Fill = new SolidColorBrush(Color.FromRgb(142, 158, 180));
            LabelText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            LabelText.FontWeight = FontWeights.Medium;
            ActiveIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void Root_MouseEnter(object sender, MouseEventArgs e)
    {
        _isHovered = true;
        UpdateVisualState();
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        _isHovered = false;
        UpdateVisualState();
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Click?.Invoke(this, new RoutedEventArgs());
}
