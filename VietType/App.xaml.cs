using VietType.Themes;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace VietType;

public partial class App : Application
{
    private const string MutexName = "VietType.SingleInstance.Mutex.8";
    private const string ShowEventName = "VietType.SingleInstance.ShowEvent.8";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;

    public App()
    {
        // Khắc phục triệt để lỗi màn hình đen (Black Screen) trên Intel Iris Xe Graphics và driver GPU.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }

    public static AppBackgroundContext BackgroundContext { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        ThemeManager.Initialize();

        // Kiểm tra nếu người dùng đã chọn chạy quyền Admin mà hiện tại chưa có quyền
        try
        {
            var repo = new Infrastructure.SettingsRepository();
            var cfg = repo.Load();
            ThemeManager.Apply(ThemeManager.Parse(cfg.Theme));

            if (cfg.RunAsAdmin && !Platform.ElevationHelper.IsAdministrator())
            {
                if (Platform.ElevationHelper.RestartAsAdministrator())
                {
                    Shutdown();
                    return;
                }
            }
        }
        catch { }

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

        BackgroundContext = new AppBackgroundContext();
        BackgroundContext.Initialize();

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
                        BackgroundContext.ShowMainWindow();
                    });
                }
            });
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.Shutdown();
        BackgroundContext?.Dispose();
        _showEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
