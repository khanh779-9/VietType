using System.Windows;
using System.Windows.Controls;

namespace VietType.Controls;

/// <summary>
/// A card container with a title and content area.
/// Template is defined in SettingCard.xaml (loaded as an implicit style via App.xaml).
/// Uses DefaultStyleKey so the template does not create a separate namescope —
/// child elements with x:Name in consuming pages register normally.
/// </summary>
public class SettingCard : ContentControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingCard), new PropertyMetadata("Settings"));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    static SettingCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingCard),
            new FrameworkPropertyMetadata(typeof(SettingCard)));
    }

    public SettingCard()
    {
        SetResourceReference(StyleProperty, typeof(SettingCard));
    }
}
