using System;

namespace VietType.Platform;

// ============================================================================
// Tầng ứng dụng VietType — Đăng ký task "VietType" trong Windows Task Scheduler
// với trigger Logon và RunLevel Highest: khởi động cùng Windows với quyền cao nhất
// mà KHÔNG cần UAC prompt lúc đăng nhập (kiểu EVKey).
// Khi đăng ký thất bại thì fallback về Registry Run key ở tầng SettingsRepository.
// ============================================================================

public static class VietTypeStartupTask
{
    private const string TaskName = "VietType";

    /// <summary>Bắn ra khi đăng ký/xóa task thất bại — để tầng UI hiện vào log chẩn đoán.</summary>
    public static event Action<string>? Error;

    private static TaskSchedulerManager? _manager;
    private static TaskSchedulerManager Manager => _manager ??= new TaskSchedulerManager();

    /// <summary>Kiểm tra task khởi động cùng Windows đã đăng ký chưa.</summary>
    public static bool IsRegistered()
    {
        try { return Manager.Exists(TaskName); }
        catch { return false; }
    }

    /// <summary>Kiểm tra task có trigger Logon để khởi động cùng Windows hay không.</summary>
    public static bool HasLogonTrigger()
    {
        try
        {
            dynamic? task = Manager.GetTask(TaskName);
            if (task is null || !(bool)task.Enabled) return false;
            dynamic triggers = task.Definition.Triggers;
            return triggers.Count > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Đăng ký task hỗ trợ quyền Administrator cao nhất (Highest) mà KHÔNG cần UAC prompt.
    /// Có thể cấu hình kèm theo trigger Logon để khởi động cùng Windows elevated không cần UAC.
    /// </summary>
    public static bool Register(bool enableLogonTrigger = true)
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) return false;

            Manager.CreateOrUpdate(TaskName, definition =>
            {
                definition.RegistrationInfo.Description =
                    "Khởi động VietType với quyền cao nhất (Administrator) mà không cần UAC.";

                definition.Principal.RunLevel = (int)TaskRunLevel.Highest;

                // Task mặc định sẽ không chạy trên pin và bị ngắt sau 72 giờ —
                // bỏ cả hai giới hạn vì VietType là ứng dụng chạy thường trú.
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.ExecutionTimeLimit = "PT0S";
                definition.Settings.StartWhenAvailable = true;

                if (enableLogonTrigger)
                {
                    dynamic trigger = definition.Triggers.Create((int)TaskTriggerType.Logon);
                    trigger.Delay = "PT3S";
                    trigger.UserId = $"{Environment.UserDomainName}\\{Environment.UserName}";
                }

                dynamic exec = definition.Actions.Create((int)TaskActionType.Exec);
                exec.Path = exe;
                exec.WorkingDirectory = AppContext.BaseDirectory;
            });

            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Không đăng ký được task khởi động: {ex.Message}");
            return false;
        }
    }

    /// <summary>Xóa task khởi động cùng Windows nếu đang tồn tại.</summary>
    public static void Unregister()
    {
        try
        {
            if (Manager.Exists(TaskName))
                Manager.Delete(TaskName);
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Không xóa được task khởi động: {ex.Message}");
        }
    }

    /// <summary>
    /// Khởi động một instance VietType elevated (RunLevel Highest) bằng cách
    /// chạy task đã đăng ký — KHÔNG hiện UAC prompt.
    /// Dùng thay cho runas khi người dùng cần quyền Admin và task đã tồn tại.
    /// </summary>
    public static bool TryRunElevated()
    {
        try
        {
            return Manager.Run(TaskName) is not null;
        }
        catch
        {
            return false;
        }
    }
}
