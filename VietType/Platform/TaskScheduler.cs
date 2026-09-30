using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VietType.Platform;

// ============================================================================
// Task Scheduler 2.0 — wrapper rút gọn, gói nhiều class trong 1 file.
// Dùng COM IDispatch late-binding (ProgID: Schedule.Service) nên không cần
// NuGet package và không phụ thuộc thứ tự vtable như [ComImport] khai báo tay.
// ============================================================================

/// <summary>Enum Task Creation flags của Task Scheduler 2.0.</summary>
public static class TaskCreation
{
    public const int ValidateOnly = 0x1;
    public const int Create = 0x2;
    public const int Update = 0x4;
    public const int CreateOrUpdate = Create | Update;
    public const int Disable = 0x8;
    public const int DontAddPrincipalAce = 0x10;
    public const int IgnoreRegistrationTriggers = 0x20;
}

/// <summary>Enum Task Logon Type của Task Scheduler 2.0.</summary>
public static class TaskLogonType
{
    public const int None = 0;
    public const int Password = 1;
    public const int S4U = 2;
    public const int InteractiveToken = 3;
    public const int Group = 4;
    public const int ServiceAccount = 5;
    public const int InteractiveTokenOrPassword = 6;
}

/// <summary>Enum Run Level của principal trong task.</summary>
public static class TaskRunLevel
{
    public const int Limited = 0;
    public const int Highest = 1;
}

/// <summary>Enum loại trigger của Task Scheduler 2.0.</summary>
public static class TaskTriggerType
{
    public const int Event = 0;
    public const int Time = 1;
    public const int Daily = 2;
    public const int Weekly = 3;
    public const int Monthly = 4;
    public const int Idle = 6;
    public const int Registration = 7;
    public const int Boot = 8;
    public const int Logon = 9;
    public const int SessionStateChange = 11;
}

/// <summary>Enum loại action của Task Scheduler 2.0.</summary>
public static class TaskActionType
{
    public const int Exec = 0;
    public const int ComHandler = 5;
    public const int SendEmail = 6;
    public const int ShowMessage = 7;
}

/// <summary>Enum trạng thái của task đã đăng ký.</summary>
public static class TaskState
{
    public const int Unknown = 0;
    public const int Disabled = 1;
    public const int Queued = 2;
    public const int Ready = 3;
    public const int Running = 4;
}

/// <summary>
/// Tùy chọn dùng khi đăng ký hoặc cập nhật một task Windows Task Scheduler.
/// </summary>
public sealed class TaskRegistrationOptions
{
    public int Creation { get; init; } = TaskCreation.CreateOrUpdate;

    public int LogonType { get; init; } = TaskLogonType.InteractiveToken;

    /// <summary>
    /// Nếu true, các registration trigger bị bỏ qua lúc đăng ký.
    /// </summary>
    public bool IgnoreRegistrationTriggers { get; init; }

    public int EffectiveCreation =>
        IgnoreRegistrationTriggers
            ? Creation | TaskCreation.IgnoreRegistrationTriggers
            : Creation;
}

/// <summary>Thông tin rút gọn về một task đã đăng ký.</summary>
public sealed record ScheduledTaskInfo(
    string Name,
    string Path,
    bool Enabled,
    int State,
    DateTime LastRunTime,
    DateTime NextRunTime,
    int LastTaskResult
);

/// <summary>Factory tạo COM instance của Task Scheduler 2.0.</summary>
internal static class TaskSchedulerCom
{
    // CLSID của Task Scheduler 2.0 COM server (ProgID: Schedule.Service).
    private static readonly Guid ServiceClsid = new("0F87369F-A4E5-4CFC-BD3E-73E6154572DD");

    public static dynamic CreateService()
    {
        var type = Type.GetTypeFromCLSID(ServiceClsid)
            ?? throw new InvalidOperationException("Task Scheduler COM không khả dụng trên hệ thống này.");

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Không thể tạo instance Task Scheduler COM.");
    }
}

/// <summary>
/// Manager cấp cao cho Windows Task Scheduler 2.0 — bản rút gọn chỉ giữ
/// phần lõi: tra cứu, đăng ký/cập nhật, xóa, bật/tắt, chạy/dừng task.
/// </summary>
public sealed class TaskSchedulerManager : IDisposable
{
    private dynamic? _service;

    public bool Connected
    {
        get
        {
            try { return _service is not null && _service.Connected; }
            catch { return false; }
        }
    }

    private dynamic Service
    {
        get
        {
            if (_service is null)
            {
                _service = TaskSchedulerCom.CreateService();
                // Bắt buộc: COM instance tạo bởi CoCreateInstance CHƯA kết nối —
                // phải gọi Connect() (không tham số = máy cục bộ, người dùng hiện tại)
                // trước khi GetFolder/NewTask/... nếu không sẽ lỗi 0x800704E3.
                _service.Connect();
            }
            return _service;
        }
    }

    // ------------------------------------------------------------
    // Tra cứu task
    // ------------------------------------------------------------

    /// <summary>Lấy task theo đường dẫn. Trả về null nếu không tồn tại.</summary>
    public dynamic? GetTask(string taskPath)
    {
        ValidateTaskPath(taskPath);
        try
        {
            // GetTask thuộc về ITaskFolder, KHÔNG phải ITaskService —
            // phải lấy folder cha rồi gọi folder.GetTask(tên task).
            dynamic folder = GetFolder(GetParentFolderPath(taskPath));
            return folder.GetTask(GetLeafName(taskPath));
        }
        catch
        {
            return null;
        }
    }

    public bool Exists(string taskPath) => GetTask(taskPath) is not null;

    /// <summary>Lấy thông tin rút gọn của task đã đăng ký.</summary>
    public ScheduledTaskInfo GetTaskInfo(string taskPath)
    {
        dynamic? task = GetTask(taskPath)
            ?? throw new FileNotFoundException($"Task not found: {taskPath}");

        return ToInfo(task);
    }

    private static ScheduledTaskInfo ToInfo(dynamic task)
    {
        return new ScheduledTaskInfo(
            (string)task.Name,
            (string)task.Path,
            (bool)task.Enabled,
            (int)task.State,
            (DateTime)task.LastRunTime,
            (DateTime)task.NextRunTime,
            (int)task.LastTaskResult
        );
    }

    // ------------------------------------------------------------
    // Đăng ký / cập nhật / xóa
    // ------------------------------------------------------------

    /// <summary>Tạo TaskDefinition rỗng để cấu hình trực tiếp qua object model.</summary>
    public dynamic CreateDefinition() => Service.NewTask(0);

    /// <summary>
    /// Tạo hoặc cập nhật task bằng một TaskDefinition được cấu hình qua callback.
    /// Tự tạo folder cha nếu chưa tồn tại.
    /// </summary>
    public dynamic CreateOrUpdate(
        string taskPath,
        Action<dynamic> configure,
        TaskRegistrationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ValidateTaskPath(taskPath);
        options ??= new TaskRegistrationOptions();

        dynamic? existing = GetTask(taskPath);
        dynamic definition = existing is not null ? existing.Definition : Service.NewTask(0);

        configure(definition);

        string folderPath = GetParentFolderPath(taskPath);
        string taskName = GetLeafName(taskPath);

        dynamic folder = EnsureFolder(folderPath);

        return folder.RegisterTaskDefinition(
            taskName,
            definition,
            options.EffectiveCreation,
            null,
            null,
            options.LogonType,
            null
        );
    }

    /// <summary>Sửa task hiện có rồi lưu lại.</summary>
    public dynamic Update(string taskPath, Action<dynamic> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        dynamic? task = GetTask(taskPath)
            ?? throw new FileNotFoundException($"Task not found: {taskPath}");

        dynamic definition = task.Definition;
        configure(definition);

        dynamic folder = GetFolder(GetParentFolderPath(task.Path));

        return folder.RegisterTaskDefinition(
            (string)task.Name,
            definition,
            TaskCreation.Update,
            null,
            null,
            (int)definition.Principal.LogonType,
            null
        );
    }

    /// <summary>Xóa task. Không ném lỗi nếu task không tồn tại.</summary>
    public void Delete(string taskPath, bool exceptionOnNotExists = false)
    {
        ValidateTaskPath(taskPath);

        string folderPath = GetParentFolderPath(taskPath);
        string taskName = GetLeafName(taskPath);

        try
        {
            dynamic folder = GetFolder(folderPath);
            folder.DeleteTask(taskName, 0);
        }
        catch
        {
            if (exceptionOnNotExists) throw;
        }
    }

    /// <summary>Bật / tắt task đã đăng ký.</summary>
    public void SetEnabled(string taskPath, bool enabled)
    {
        dynamic? task = GetTask(taskPath)
            ?? throw new FileNotFoundException($"Task not found: {taskPath}");

        task.Enabled = enabled;
    }

    // ------------------------------------------------------------
    // Chạy / dừng
    // ------------------------------------------------------------

    public dynamic? Run(string taskPath, params string[] parameters)
    {
        dynamic? task = GetTask(taskPath)
            ?? throw new FileNotFoundException($"Task not found: {taskPath}");

        // VARIANT params là optional — truyền null thay vì mảng rỗng
        // để Task Scheduler dùng giá trị mặc định.
        return parameters is { Length: > 0 } ? task.Run(parameters) : task.Run(null);
    }

    public void Stop(string taskPath)
    {
        dynamic? task = GetTask(taskPath)
            ?? throw new FileNotFoundException($"Task not found: {taskPath}");

        task.Stop();
    }

    // ------------------------------------------------------------
    // Folder
    // ------------------------------------------------------------

    private dynamic GetFolder(string folderPath)
    {
        folderPath = NormalizeFolderPath(folderPath);
        return Service.GetFolder(folderPath);
    }

    private dynamic EnsureFolder(string folderPath)
    {
        folderPath = NormalizeFolderPath(folderPath);

        if (folderPath == @"\")
            return Service.GetFolder(@"\");

        try
        {
            return GetFolder(folderPath);
        }
        catch
        {
            return Service.GetFolder(@"\").CreateFolder(folderPath.Trim('\\'), null);
        }
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private static void ValidateTaskPath(string taskPath)
    {
        if (string.IsNullOrWhiteSpace(taskPath))
            throw new ArgumentException("Task path cannot be empty.", nameof(taskPath));

        ValidateTaskName(GetLeafName(taskPath));
    }

    private static void ValidateTaskName(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Task name cannot be empty.", nameof(taskName));

        if (taskName.StartsWith(' ') || taskName.EndsWith(' '))
            throw new ArgumentException("Task name cannot start or end with a space.", nameof(taskName));

        if (taskName.Contains('\\'))
            throw new ArgumentException("Task name cannot contain a folder separator.", nameof(taskName));
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return @"\";

        folderPath = folderPath.Replace('/', '\\').Trim();

        if (!folderPath.StartsWith('\\'))
            folderPath = "\\" + folderPath;

        if (folderPath.Length > 1 && folderPath.EndsWith('\\'))
            folderPath = folderPath.TrimEnd('\\');

        return folderPath;
    }

    private static string GetParentFolderPath(string taskPath)
    {
        taskPath = taskPath.Replace('/', '\\').TrimEnd('\\');

        int separator = taskPath.LastIndexOf('\\');

        return separator <= 0 ? @"\" : taskPath[..separator];
    }

    private static string GetLeafName(string path)
    {
        path = path.Replace('/', '\\').TrimEnd('\\');

        int index = path.LastIndexOf('\\');

        return index >= 0 ? path[(index + 1)..] : path;
    }

    public void Dispose()
    {
        if (_service is not null)
        {
            try { Marshal.FinalReleaseComObject(_service); } catch { }
            _service = null;
        }
    }
}

// ============================================================================
// Tầng ứng dụng VietType — tự phát triển thêm.
// Đăng ký task "VietType" với trigger Logon và RunLevel Highest:
// khởi động cùng Windows với quyền cao nhất mà KHÔNG cần UAC prompt
// lúc đăng nhập (kiểu EVKey). Khi đăng ký thất bại thì fallback về
// Registry Run key ở tầng SettingsRepository.
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

    /// <summary>
    /// Đăng ký task khởi động cùng Windows (trigger Logon, RunLevel Highest).
    /// Chỉ nên gọi khi tiến trình đang chạy với quyền Administrator.
    /// </summary>
    public static bool Register()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe)) return false;

            Manager.CreateOrUpdate(TaskName, definition =>
            {
                definition.RegistrationInfo.Description =
                    "Khởi động VietType cùng Windows với quyền cao nhất.";

                definition.Principal.RunLevel = TaskRunLevel.Highest;

                // Task mặc định sẽ không chạy trên pin và bị ngắt sau 72 giờ —
                // bỏ cả hai giới hạn vì VietType là ứng dụng chạy thường trú.
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.ExecutionTimeLimit = "PT0S";
                definition.Settings.StartWhenAvailable = true;

                dynamic trigger = definition.Triggers.Create(TaskTriggerType.Logon);
                trigger.Delay = "PT3S";
                trigger.UserId = $"{Environment.UserDomainName}\\{Environment.UserName}";

                // IActionCollection.Create tự thêm action mới vào collection và trả về action đó.
                dynamic exec = definition.Actions.Create(TaskActionType.Exec);
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
