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
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(NavItem), new PropertyMetadata(false));

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

    public NavItem()
    {
        InitializeComponent();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavItem item) item.LabelText.Text = e.NewValue?.ToString() ?? string.Empty;
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavItem item) item.Icon.Data = e.NewValue as Geometry;
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Click?.Invoke(this, new RoutedEventArgs());
}
