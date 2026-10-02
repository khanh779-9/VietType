using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace VietType.Platform;

/// <summary>
/// Hỗ trợ kiểm tra và khởi động ứng dụng với quyền quản trị (Administrator / Elevated).
/// </summary>
public static class ElevationHelper
{
    /// <summary>
    /// Kiểm tra xem tiến trình hiện tại có đang chạy dưới quyền Administrator hay không.
    /// </summary>
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Khởi động lại ứng dụng với quyền Administrator.
    /// Ưu tiên chạy qua Task Scheduler (nếu đã đăng ký) để KHÔNG hiển thị UAC prompt.
    /// Fallback sang UAC prompt (runas) nếu task chưa tồn tại.
    /// </summary>
    /// <returns>True nếu người dùng đồng ý cấp quyền và tiến trình mới đã bắt đầu, False nếu từ chối hoặc lỗi.</returns>
    public static bool RestartAsAdministrator(string? arguments = null)
    {
        // 1. Thử chạy elevated qua Task Scheduler (KHÔNG CẦN UAC PROMPT)
        if (VietTypeStartupTask.IsRegistered() && VietTypeStartupTask.TryRunElevated())
        {
            Application.Current?.Shutdown();
            return true;
        }

        // 2. Chưa có task: gọi runas để người dùng duyệt UAC lần đầu
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            using var cur = Process.GetCurrentProcess();
            processPath = cur.MainModule?.FileName;
        }

        if (string.IsNullOrEmpty(processPath)) return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = arguments ?? string.Empty
        };

        try
        {
            Process.Start(startInfo);
            Application.Current?.Shutdown();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Người dùng bấm "No" hoặc Cancel trên hộp thoại UAC của Windows
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
