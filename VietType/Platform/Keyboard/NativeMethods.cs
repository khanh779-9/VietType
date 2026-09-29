using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VietType.Platform.Keyboard;

/// <summary>
/// Win32 declarations phục vụ low-level keyboard hook và SendInput.
/// </summary>
internal static class NativeMethods
{
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;

    internal const uint VK_BACK = 0x08;
    internal const uint VK_RETURN = 0x0D;
    internal const uint VK_SHIFT = 0x10;
    internal const uint VK_CONTROL = 0x11;
    internal const uint VK_MENU = 0x12;
    internal const uint VK_TAB = 0x09;
    internal const uint VK_ESCAPE = 0x1B;
    internal const uint VK_SPACE = 0x20;
    internal const uint VK_LEFT = 0x25;
    internal const uint VK_UP = 0x26;
    internal const uint VK_RIGHT = 0x27;
    internal const uint VK_DOWN = 0x28;
    internal const uint VK_HOME = 0x24;
    internal const uint VK_END = 0x23;
    internal const uint VK_PRIOR = 0x21;
    internal const uint VK_NEXT = 0x22;
    internal const uint VK_DELETE = 0x2E;
    internal const uint VK_LWIN = 0x5B;
    internal const uint VK_RWIN = 0x5C;
    internal const uint VK_OEM_3 = 0xC0;

    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;
    internal const int INPUT_KEYBOARD = 1;

    internal static readonly UIntPtr VietImeMarker = new(0x56494D45u);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        internal uint vkCode;
        internal uint scanCode;
        internal uint flags;
        internal uint time;
        internal IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        internal uint type;
        internal INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] internal KEYBDINPUT ki;
        [FieldOffset(0)] internal MOUSEINPUT mi;
        [FieldOffset(0)] internal HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        internal ushort wVk;
        internal ushort wScan;
        internal uint dwFlags;
        internal uint time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        internal int dx;
        internal int dy;
        internal uint mouseData;
        internal uint dwFlags;
        internal uint time;
        internal UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        internal uint uMsg;
        internal ushort wParamL;
        internal ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out] StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags,
        IntPtr dwhkl);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetKeyboardState(byte[] lpKeyState);

    internal static bool IsKeyPressed(uint vKey) => (GetAsyncKeyState((int)vKey) & 0x8000) != 0;

    internal static bool IsShiftPressed() => IsKeyPressed(VK_SHIFT);
    internal static bool IsCtrlPressed() => IsKeyPressed(VK_CONTROL);
    internal static bool IsAltPressed() => IsKeyPressed(VK_MENU);

    internal static string? GetForegroundProcessName()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0) return null;
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    internal static char? VirtualKeyToChar(uint vkCode, uint scanCode, bool shiftOverride)
    {
        // Letters A-Z
        if (vkCode >= 'A' && vkCode <= 'Z')
        {
            bool caps = (GetKeyState(0x14) & 0x0001) != 0;
            bool upper = caps ^ shiftOverride;
            return upper ? (char)vkCode : (char)(vkCode + 32);
        }

        // Digits 0-9
        if (vkCode >= '0' && vkCode <= '9')
        {
            if (!shiftOverride) return (char)vkCode;
            return vkCode switch
            {
                '0' => ')',
                '1' => '!',
                '2' => '@',
                '3' => '#',
                '4' => '$',
                '5' => '%',
                '6' => '^',
                '7' => '&',
                '8' => '*',
                '9' => '(',
                _ => (char)vkCode
            };
        }

        // Common typing symbols (VIQR, brackets, punctuation)
        return vkCode switch
        {
            0xBA => shiftOverride ? ':' : ';', // VK_OEM_1
            0xBB => shiftOverride ? '+' : '=', // VK_OEM_PLUS
            0xBC => shiftOverride ? '<' : ',', // VK_OEM_COMMA
            0xBD => shiftOverride ? '_' : '-', // VK_OEM_MINUS
            0xBE => shiftOverride ? '>' : '.', // VK_OEM_PERIOD
            0xBF => shiftOverride ? '?' : '/', // VK_OEM_2
            0xC0 => shiftOverride ? '~' : '`', // VK_OEM_3
            0xDB => shiftOverride ? '{' : '[', // VK_OEM_4
            0xDC => shiftOverride ? '|' : '\\', // VK_OEM_5
            0xDD => shiftOverride ? '}' : ']', // VK_OEM_6
            0xDE => shiftOverride ? '"' : '\'', // VK_OEM_7
            _ => null
        };
    }
}
