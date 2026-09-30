using VietType.Themes;
using System;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;

namespace VietType;

public partial class App : Application
{
    private const string MutexName = "VietType.SingleInstance.Mutex.8";
    private const string ShowEventName = "VietType.SingleInstance.ShowEvent.8";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Kiểm tra nếu người dùng đã chọn chạy quyền Admin mà hiện tại chưa có quyền
        try
        {
            var repo = new Infrastructure.SettingsRepository();
            var cfg = repo.Load();
            if (cfg.RunAsAdmin && !Platform.ElevationHelper.IsAdministrator())
            {
                // Đã có task "VietType" (RunLevel Highest) → khởi động instance
                // elevated qua Task Scheduler, KHÔNG cần UAC prompt.
                if (Platform.VietTypeStartupTask.IsRegistered() && Platform.VietTypeStartupTask.TryRunElevated())
                {
                    Shutdown();
                    return;
                }

                // Fallback: chưa có task → UAC prompt như cũ;
                // instance admin mới sẽ đăng ký task lúc nạp cấu hình.
                if (Platform.ElevationHelper.RestartAsAdministrator())
                {
                    Shutdown();
                    return;
                }
            }
        }
        catch { }

        ThemeManager.Initialize();

        bool createdNew = false;
        try
        {
            _instanceMutex = new Mutex(true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            // Another instance is already running. Signal it to bring its window to front.
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var handle))
                {
                    handle.Set();
                    handle.Dispose();
                }
            }
            catch { }

            Shutdown();
            return;
        }

        // Setup background listener to show window when subsequent instances start
        try
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (_showEvent?.WaitOne() == true)
                {
                    Current?.Dispatcher.Invoke(() =>
                    {
                        if (Current.MainWindow is MainWindow mw)
                        {
                            mw.ShowFromTray();
                        }
                    });
                }
            });
        }
        catch { }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.Shutdown();
        _showEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
