using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VietType.Controls;

public partial class PathIcon : UserControl
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(PathIcon), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(Brush), typeof(PathIcon), new PropertyMetadata(null));

    public Geometry? Data { get => (Geometry?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public Brush? IconBrush { get => (Brush?)GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }

    public PathIcon()
    {
        InitializeComponent();
        DataContext = this;
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PathIcon icon) icon.IconPath.Data = e.NewValue as Geometry;
    }
}
