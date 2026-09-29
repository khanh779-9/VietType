using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VietType.Controls;
using VietType.Infrastructure;
using VietType.Platform;

namespace VietType;

public partial class AboutWindow : VietTypeWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        bool isAdmin = ElevationHelper.IsAdministrator();
        AdminTagText.Text = isAdmin ? "Administrator" : "Standard User";
        AdminTag.Background = (System.Windows.Media.Brush)FindResource(isAdmin ? "SuccessBrush" : "AccentSoftBrush");
        AdminTagText.Foreground = (System.Windows.Media.Brush)FindResource(isAdmin ? "InverseTextBrush" : "AccentBrush");
    }

    protected override void OnCloseButtonClick() => Close();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Guide_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Documentation", "UsageGuide.html");
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("Không tìm thấy tệp tài liệu hướng dẫn.", "VietType", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở tài liệu: {ex.Message}", "VietType", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietType");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở thư mục dữ liệu: {ex.Message}", "VietType", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
