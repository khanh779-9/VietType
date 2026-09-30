using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VietType.Core.Typing;
using VietType.Core.Models;

namespace VietType.Platform.Keyboard;

/// <summary>
/// Low-level keyboard hook chạy system-wide.
/// Chỉ intercept khi lõi gõ tiếng Việt thực sự cần thay thế ký tự.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly object _stateLock = new();
    private ITextInputEngine? _engine;
    private IntPtr _hookId;
    private bool _disposed;
    private bool _ctrlDown;
    private bool _shiftDown;
    private bool _altDown;
    private bool _winDown;
    private bool _ctrlShiftCandidate;
    private bool _altShiftCandidate;
    private DateTime _lastToggle = DateTime.MinValue;

    private const int ToggleDebounceMs = 300;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public event EventHandler<bool>? EnabledChanged;
    public event EventHandler<string>? Error;
    public event Action<string>? DebugLog;

    public ITextInputEngine? Engine
    {
        get => _engine;
        set
        {
            lock (_stateLock)
            {
                _engine?.Reset();
                _engine = value;
            }
        }
    }

    public bool IsEnabled
    {
        get => _engine?.Enabled ?? false;
        set
        {
            if (_engine is null) return;
            if (_engine.Enabled == value) return;
            _engine.Enabled = value;
            _engine.Reset();
            EnabledChanged?.Invoke(this, value);
        }
    }

    public bool UseCtrlShiftHotkey { get; set; } = true;
    public char AltHotkeyKey { get; set; } = 'Z';
    public bool SupportGames { get; set; } = true;
    public bool SupportMetro { get; set; } = true;
    public bool UseClipboardReplacement { get; set; } = false;

    public HotkeyItem HotkeyToggle { get; set; } = new() { Ctrl = true, Shift = true, Key = "" };
    public HotkeyItem HotkeySwitchMethod { get; set; } = new() { Alt = true, Shift = true, Key = "Z" };
    public HotkeyItem HotkeyRestoreWord { get; set; } = new() { Ctrl = true, Shift = true, Key = "Z" };

    public bool EnableQuickFunctionKeys { get; set; } = true;
    public bool QuickKeysCtrl { get; set; } = true;
    public bool QuickKeysShift { get; set; } = true;
    public bool QuickKeysAlt { get; set; } = true;
    public bool QuickKeysWin { get; set; } = false;

    public bool QuickF1 { get; set; } = true;
    public bool QuickF2 { get; set; } = true;
    public bool QuickF3 { get; set; } = true;
    public bool QuickF4 { get; set; } = true;
    public bool QuickF5 { get; set; } = true;
    public bool QuickF6 { get; set; } = true;
    public bool QuickF7 { get; set; } = true;
    public bool QuickF8 { get; set; } = true;
    public bool QuickF9 { get; set; } = true;
    public bool QuickF12 { get; set; } = true;

    public event Action? SwitchTypingMethodRequested;
    public event Action? SelectUnicodeRequested;
    public event Action? CycleCodeTableRequested;
    public event Action? ToggleSpellCheckRequested;
    public event Action? OpenDashboardRequested;
    public event Action? OpenShortcutsRequested;
    public event Action? ToggleShortcutsRequested;

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        if (module is null)
        {
            Error?.Invoke(this, "Không thể lấy module handle của ứng dụng.");
            return;
        }

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(module.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int code = Marshal.GetLastWin32Error();
            Error?.Invoke(this, $"Không thể cài keyboard hook. Win32 error: {code}");
        }
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode < 0 || _engine is null)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            var key = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int message = wParam.ToInt32();

            if ((ulong)key.dwExtraInfo == NativeMethods.VietImeMarker.ToUInt64())
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            bool isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            bool isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
            if (!isDown && !isUp)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            if (isDown && UpdateModifierState(key.vkCode, true))
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            if (isUp)
            {
                UpdateModifierState(key.vkCode, false);
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (HandleHotkeys(key.vkCode))
                return (IntPtr)1;

            if (!_engine.Enabled)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            // Không can thiệp shortcut của hệ điều hành/app.
            bool ctrlActive = _ctrlDown || NativeMethods.IsCtrlPressed();
            bool altActive = _altDown || NativeMethods.IsAltPressed();
            bool winActive = NativeMethods.IsKeyPressed(NativeMethods.VK_LWIN) || NativeMethods.IsKeyPressed(NativeMethods.VK_RWIN);

            if (winActive || ctrlActive)
            {
                _engine.Reset();
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (altActive && key.vkCode != NativeMethods.VK_SPACE)
            {
                _engine.Reset();
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            switch (key.vkCode)
            {
                case NativeMethods.VK_BACK:
                    return HandleBackspace(nCode, wParam, lParam);

                case NativeMethods.VK_SPACE:
                case NativeMethods.VK_RETURN:
                case NativeMethods.VK_TAB:
                    return HandleBoundary((char)key.vkCode, nCode, wParam, lParam);

                case NativeMethods.VK_ESCAPE:
                case NativeMethods.VK_LEFT:
                case NativeMethods.VK_RIGHT:
                case NativeMethods.VK_UP:
                case NativeMethods.VK_DOWN:
                case NativeMethods.VK_HOME:
                case NativeMethods.VK_END:
                case NativeMethods.VK_PRIOR:
                case NativeMethods.VK_NEXT:
                case NativeMethods.VK_DELETE:
                    _engine.Reset();
                    return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            char? ch = NativeMethods.VirtualKeyToChar(key.vkCode, key.scanCode, _shiftDown);
            if (!ch.HasValue)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            var result = _engine.ProcessKey(ch.Value, _shiftDown);
            DebugLog?.Invoke($"Key '{ch.Value}' → handled={result.Handled}, output='{result.OutputText}', bs={result.BackspaceCount}");

            if (!result.Handled)
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            SendReplacement(result.BackspaceCount, result.OutputText ?? string.Empty);
            return (IntPtr)1;
        }
        catch (Exception ex)
        {
            DebugLog?.Invoke("Hook error: " + ex.Message);
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }

    private bool UpdateModifierState(uint vkCode, bool down)
    {
        switch (vkCode)
        {
            case 0xA2: // VK_LCONTROL
            case 0xA3: // VK_RCONTROL
            case NativeMethods.VK_CONTROL:
                _ctrlDown = down;
                if (down && !_altDown && !_shiftDown && !_winDown)
                    _ctrlShiftCandidate = true;
                if (!down && IsCtrlShiftToggleAllowed() && _ctrlShiftCandidate && !_shiftDown && !_altDown && !_winDown)
                {
                    _ctrlShiftCandidate = false;
                    ToggleEnabled();
                }
                break;

            case 0xA0: // VK_LSHIFT
            case 0xA1: // VK_RSHIFT
            case NativeMethods.VK_SHIFT:
                _shiftDown = down;
                if (down && !_altDown && _ctrlDown && !_winDown)
                    _ctrlShiftCandidate = true;
                if (!down && IsCtrlShiftToggleAllowed() && _ctrlShiftCandidate && !_ctrlDown && !_altDown && !_winDown)
                {
                    _ctrlShiftCandidate = false;
                    ToggleEnabled();
                }

                if (down && _altDown && !_ctrlDown && !_winDown)
                    _altShiftCandidate = true;
                if (!down && IsAltShiftToggleAllowed() && _altShiftCandidate && !_ctrlDown && !_altDown && !_winDown)
                {
                    _altShiftCandidate = false;
                    ToggleEnabled();
                }
                break;

            case 0xA4: // VK_LMENU
            case 0xA5: // VK_RMENU
            case NativeMethods.VK_MENU:
                _altDown = down;
                if (down && !_ctrlDown && !_shiftDown && !_winDown)
                    _altShiftCandidate = true;
                if (!down && IsAltShiftToggleAllowed() && _altShiftCandidate && !_ctrlDown && !_shiftDown && !_winDown)
                {
                    _altShiftCandidate = false;
                    ToggleEnabled();
                }
                if (!down) { _ctrlShiftCandidate = false; _altShiftCandidate = false; }
                break;

            case NativeMethods.VK_LWIN:
            case NativeMethods.VK_RWIN:
                _winDown = down;
                if (!down) { _ctrlShiftCandidate = false; _altShiftCandidate = false; }
                break;

            default:
                if (down && vkCode is not (NativeMethods.VK_BACK or NativeMethods.VK_SPACE))
                {
                    _ctrlShiftCandidate = false;
                    _altShiftCandidate = false;
                }
                break;
        }

        if (!down && !_ctrlDown && !_shiftDown && !_altDown)
        {
            _ctrlShiftCandidate = false;
            _altShiftCandidate = false;
        }

        return vkCode is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN
            or NativeMethods.VK_CONTROL or NativeMethods.VK_SHIFT or NativeMethods.VK_MENU;
    }

    private bool IsCtrlShiftToggleAllowed()
    {
        return HotkeyToggle.Enabled &&
               HotkeyToggle.Ctrl &&
               HotkeyToggle.Shift &&
               !HotkeyToggle.Alt &&
               !HotkeyToggle.Win &&
               string.IsNullOrWhiteSpace(HotkeyToggle.Key);
    }

    private bool IsAltShiftToggleAllowed()
    {
        return HotkeyToggle.Enabled &&
               !HotkeyToggle.Ctrl &&
               HotkeyToggle.Shift &&
               HotkeyToggle.Alt &&
               !HotkeyToggle.Win &&
               string.IsNullOrWhiteSpace(HotkeyToggle.Key);
    }

    private bool HandleHotkeys(uint vkCode)
    {
        bool ctrl = _ctrlDown;
        bool shift = _shiftDown;
        bool alt = _altDown;
        bool win = _winDown || NativeMethods.IsKeyPressed(NativeMethods.VK_LWIN) || NativeMethods.IsKeyPressed(NativeMethods.VK_RWIN);

        // 1. Phím chuyển E/V (chỉ khi có gán phím cụ thể như Z, Space, v.v.)
        if (MatchHotkey(HotkeyToggle, ctrl, shift, alt, win, vkCode))
        {
            ToggleEnabled();
            return true;
        }

        // 2. Phím chuyển kiểu gõ (Telex / VNI / VIQR)
        if (MatchHotkey(HotkeySwitchMethod, ctrl, shift, alt, win, vkCode))
        {
            SwitchTypingMethodRequested?.Invoke();
            return true;
        }

        // 3. Phím phục hồi từ
        if (MatchHotkey(HotkeyRestoreWord, ctrl, shift, alt, win, vkCode))
        {
            var res = _engine?.RestoreWord();
            if (res.HasValue && res.Value.Handled)
            {
                SendReplacement(res.Value.BackspaceCount, res.Value.OutputText ?? string.Empty);
            }
            return true;
        }

        // 4. Tổ hợp phím chức năng nhanh F1 - F12
        if (EnableQuickFunctionKeys &&
            ctrl == QuickKeysCtrl &&
            shift == QuickKeysShift &&
            alt == QuickKeysAlt &&
            win == QuickKeysWin)
        {
            switch (vkCode)
            {
                case 0x70 when QuickF1: // F1: Bật TV
                    if (!IsEnabled) IsEnabled = true;
                    return true;

                case 0x71 when QuickF2: // F2: Tắt TV
                    if (IsEnabled) IsEnabled = false;
                    return true;

                case 0x72 when QuickF3: // F3: Unicode
                    SelectUnicodeRequested?.Invoke();
                    return true;

                case 0x73 when QuickF4: // F4: Chuyển bảng mã kế tiếp
                    CycleCodeTableRequested?.Invoke();
                    return true;

                case 0x74 when QuickF5: // F5: Mở bảng điều khiển
                    OpenDashboardRequested?.Invoke();
                    return true;

                case 0x75 when QuickF6: // F6: Bật / tắt kiểm tra chính tả
                    ToggleSpellCheckRequested?.Invoke();
                    return true;

                case 0x76 when QuickF7: // F7: Chèn ngày hiện tại
                    SendReplacement(0, DateTime.Now.ToString("dd/MM/yyyy"));
                    return true;

                case 0x77 when QuickF8: // F8: Mở bảng gõ tắt
                    OpenShortcutsRequested?.Invoke();
                    return true;

                case 0x78 when QuickF9: // F9: Bật / tắt gõ tắt
                    ToggleShortcutsRequested?.Invoke();
                    return true;

                case 0x7B when QuickF12: // F12: Reset bộ nhớ đệm
                    _engine?.Reset();
                    return true;
            }
        }

        return false;
    }

    private static bool MatchHotkey(HotkeyItem? hk, bool ctrl, bool shift, bool alt, bool win, uint vkCode)
    {
        if (hk is null || !hk.Enabled) return false;
        if (string.IsNullOrWhiteSpace(hk.Key)) return false;
        if (hk.Ctrl != ctrl || hk.Shift != shift || hk.Alt != alt || hk.Win != win) return false;

        uint targetVk = KeyStringToVk(hk.Key);
        return targetVk != 0 && vkCode == targetVk;
    }

    public static uint KeyStringToVk(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return 0;
        string s = key.Trim().ToUpperInvariant();
        return s switch
        {
            "SPACE" or "KHOẢNG TRẮNG" => NativeMethods.VK_SPACE,
            "BACK" or "BACKSPACE" or "XÓA" => NativeMethods.VK_BACK,
            "TAB" => NativeMethods.VK_TAB,
            "ENTER" or "RETURN" => NativeMethods.VK_RETURN,
            "ESC" or "ESCAPE" => NativeMethods.VK_ESCAPE,
            "DELETE" or "DEL" => NativeMethods.VK_DELETE,
            "~" or "`" => NativeMethods.VK_OEM_3,
            ";" => 0xBA,
            "/" => 0xBF,
            "[" => 0xDB,
            "]" => 0xDD,
            "\\" => 0xDC,
            "'" => 0xDE,
            _ when s.StartsWith("F") && int.TryParse(s[1..], out int f) && f is >= 1 and <= 12 => (uint)(0x70 + f - 1),
            _ when s.Length == 1 && s[0] is >= 'A' and <= 'Z' => (uint)s[0],
            _ when s.Length == 1 && s[0] is >= '0' and <= '9' => (uint)s[0],
            _ => 0
        };
    }

    private void ToggleEnabled()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastToggle).TotalMilliseconds < ToggleDebounceMs)
            return;
        _lastToggle = now;
        IsEnabled = !IsEnabled;
    }

    private IntPtr HandleBackspace(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var result = _engine!.ProcessBackspace();
        if (!result.Handled)
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

        SendReplacement(result.BackspaceCount, result.OutputText ?? string.Empty);
        return (IntPtr)1;
    }

    private IntPtr HandleBoundary(char boundary, int nCode, IntPtr wParam, IntPtr lParam)
    {
        var result = _engine!.ProcessBoundary(boundary);
        if (!result.Handled)
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

        SendReplacement(result.BackspaceCount, result.OutputText ?? string.Empty);
        SendVirtualKey((uint)boundary);
        return (IntPtr)1;
    }

    public void TriggerRestoreWord()
    {
        var res = _engine?.RestoreWord();
        if (res.HasValue && res.Value.Handled)
        {
            SendReplacement(res.Value.BackspaceCount, res.Value.OutputText ?? string.Empty);
        }
    }

    internal void SendReplacement(int backspaceCount, string text)
    {
        if (backspaceCount <= 0 && string.IsNullOrEmpty(text)) return;

        // Xử lý gửi phím Backspace với scan code thực tế
        if (backspaceCount > 0)
        {
            var bsInputs = new NativeMethods.INPUT[backspaceCount * 2];
            ushort bsScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_BACK, 0);
            if (bsScan == 0) bsScan = 0x0E;

            int bsIdx = 0;
            for (int i = 0; i < backspaceCount; i++)
            {
                bsInputs[bsIdx++] = KeyInput(NativeMethods.VK_BACK, bsScan, 0);
                bsInputs[bsIdx++] = KeyInput(NativeMethods.VK_BACK, bsScan, NativeMethods.KEYEVENTF_KEYUP);
            }
            NativeMethods.SendInput((uint)bsInputs.Length, bsInputs, Marshal.SizeOf<NativeMethods.INPUT>());

            // Đối với game (DirectInput/RawInput) hoặc Metro/UWP:
            // Cần micro-delay để buffer của game/UWP kịp hoàn tất xóa ký tự trước khi nhận ký tự mới
            if (SupportGames || SupportMetro)
            {
                System.Threading.Thread.Sleep(2);
            }
        }

        // Gửi chuỗi ký tự thay thế (qua Clipboard hoặc SendInput)
        if (!string.IsNullOrEmpty(text))
        {
            if (UseClipboardReplacement)
            {
                SendViaClipboard(text);
            }
            else
            {
                var charInputs = new NativeMethods.INPUT[text.Length * 2];
                int charIdx = 0;
                foreach (char c in text)
                {
                    charInputs[charIdx++] = UnicodeInput(c, 0);
                    charInputs[charIdx++] = UnicodeInput(c, NativeMethods.KEYEVENTF_KEYUP);
                }
                NativeMethods.SendInput((uint)charInputs.Length, charInputs, Marshal.SizeOf<NativeMethods.INPUT>());
            }
        }
    }

    private static void SendViaClipboard(string text)
    {
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                ushort vScan = (ushort)NativeMethods.MapVirtualKey(0x56, 0); // 'V'
                if (vScan == 0) vScan = 0x2F;
                ushort ctrlScan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_CONTROL, 0);
                if (ctrlScan == 0) ctrlScan = 0x1D;

                var inputs = new[]
                {
                    KeyInput(NativeMethods.VK_CONTROL, ctrlScan, 0),
                    KeyInput(0x56, vScan, 0),
                    KeyInput(0x56, vScan, NativeMethods.KEYEVENTF_KEYUP),
                    KeyInput(NativeMethods.VK_CONTROL, ctrlScan, NativeMethods.KEYEVENTF_KEYUP)
                };
                NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
            }
            catch
            {
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join(120);
    }

    private static void SendVirtualKey(uint vk)
    {
        ushort scan = (ushort)NativeMethods.MapVirtualKey(vk, 0);
        var inputs = new[]
        {
            KeyInput(vk, scan, 0),
            KeyInput(vk, scan, NativeMethods.KEYEVENTF_KEYUP)
        };
        NativeMethods.SendInput(2, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT KeyInput(uint vk, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = NativeMethods.VietImeMarker
            }
        }
    };

    private static NativeMethods.INPUT UnicodeInput(char c, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.INPUTUNION
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = 0,
                wScan = c,
                dwFlags = NativeMethods.KEYEVENTF_UNICODE | flags,
                time = 0,
                dwExtraInfo = NativeMethods.VietImeMarker
            }
        }
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Uninstall();
        GC.SuppressFinalize(this);
    }
}
