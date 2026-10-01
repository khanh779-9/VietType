using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VietType.Controls;

/// <summary>
/// Modern seamless chromeless Window using native WindowChrome.
/// Integrates a dedicated top TitleBar (Row 0) and content canvas (Row 1).
/// Template is defined in VietTypeWindow.xaml (loaded as an implicit style).
/// </summary>
public partial class VietTypeWindow : Window
{
    private static readonly Geometry MaximizeGeometry = Geometry.Parse("M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z");
    private static readonly Geometry RestoreGeometry = Geometry.Parse("M2.5,0.5 L9.5,0.5 L9.5,7.5 L7.5,7.5 M0.5,2.5 L7.5,2.5 L7.5,9.5 L0.5,9.5 Z");

    static VietTypeWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(VietTypeWindow),
            new FrameworkPropertyMetadata(typeof(VietTypeWindow)));
    }

    public VietTypeWindow()
    {
        // Explicitly bind Style to the implicit style from VietTypeWindow.xaml
        // in Application.Resources.
        SetResourceReference(StyleProperty, typeof(VietTypeWindow));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Wire up window caption buttons
        if (GetTemplateChild("PART_MinimizeButton") is Button minBtn)
        {
            if (ResizeMode == ResizeMode.NoResize)
            {
                minBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                minBtn.Visibility = Visibility.Visible;
                minBtn.Click += (_, _) => WindowState = WindowState.Minimized;
            }
        }

        if (GetTemplateChild("PART_MaximizeButton") is Button maxBtn)
        {
            if (ResizeMode == ResizeMode.NoResize || ResizeMode == ResizeMode.CanMinimize)
            {
                maxBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                maxBtn.Visibility = Visibility.Visible;
                maxBtn.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        if (GetTemplateChild("PART_CloseButton") is Button closeBtn)
        {
            closeBtn.Click += (_, _) => OnCloseButtonClick();
        }

        UpdateMaximizeButtonIcon();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeButtonIcon();
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (GetTemplateChild("PART_MaximizeIcon") is Path maxIcon &&
            GetTemplateChild("PART_MaximizeButton") is Button maxBtn)
        {
            if (WindowState == WindowState.Maximized)
            {
                maxIcon.Data = RestoreGeometry;
                maxBtn.ToolTip = "Khôi phục";
            }
            else
            {
                maxIcon.Data = MaximizeGeometry;
                maxBtn.ToolTip = "Phóng to";
            }
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        InvalidateVisual();
    }

    protected virtual void OnCloseButtonClick() => Close();
}
