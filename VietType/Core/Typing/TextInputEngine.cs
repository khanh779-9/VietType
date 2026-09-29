using System;
using System.Collections.Generic;
using System.Linq;
using VietType.Core.Models;

namespace VietType.Core.Typing;

/// <summary>
/// Adapter hiện đại hóa lõi VietnameseProcessor cũ: giữ thuật toán VNI/Telex/VIQR
/// nhưng cung cấp API đơn giản cho global keyboard hook.
/// </summary>
public sealed class TextInputEngine : ITextInputEngine
{
    private readonly object _sync = new();
    private readonly VietnameseProcessor _core;
    private Dictionary<string, string> _shortcuts = new(StringComparer.OrdinalIgnoreCase);
    private string _rawBuffer = string.Empty;
    private string _screenBuffer = string.Empty;
    private string[] _codeTable;

    private static readonly char[] Vni = ['0', '1', '2', '4', '3', '5', '6', ' ', ' ', ' ', ' ', ' ', '7', '8', '9'];
    private static readonly char[] Telex = ['z', 's', 'f', 'x', 'r', 'j', ' ', 'a', 'e', 'o', ' ', 'w', ' ', ' ', 'd'];
    private static readonly char[] TelexExtended = ['z', 's', 'f', 'x', 'r', 'j', ' ', 'a', 'e', 'o', 'w', ' ', ' ', ' ', 'd'];
    private static readonly char[] Viqr = ['0', '\'', '`', '~', '?', '.', '^', ' ', ' ', ' ', ' ', ' ', '+', '(', 'd'];

    public TextInputEngine(string[] unicodeCodeTable)
    {
        if (unicodeCodeTable is null || unicodeCodeTable.Length < 146)
            throw new ArgumentException("Bảng mã Unicode không hợp lệ.", nameof(unicodeCodeTable));

        _codeTable = unicodeCodeTable;
        _core = new VietnameseProcessor(_codeTable, Telex);
        _core.ktChinhTa = 1;
    }

    public bool Enabled { get; set; } = true;
    public bool ModernToneRemoval
    {
        get => _core.boDauKieuMoi;
        set => _core.boDauKieuMoi = value;
    }

    public int SpellCheckLevel
    {
        get => _core.ktChinhTa;
        set => _core.ktChinhTa = Math.Clamp(value, 0, 2);
    }

    public bool ShortcutsEnabled { get; set; }
    public bool ShortcutWithoutSpace { get; set; }
    public bool ShortcutsWhenDisabled { get; set; }

    public IReadOnlyDictionary<string, string> Shortcuts => _shortcuts;

    public void SetShortcuts(IEnumerable<ShortcutDefinition> items)
    {
        lock (_sync)
        {
            _shortcuts = items
                .Where(x => !string.IsNullOrWhiteSpace(x.Trigger))
                .GroupBy(x => x.Trigger.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last().Replacement ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SetTypingMethod(int index)
    {
        lock (_sync)
        {
            _core.kieuGo = index switch
            {
                0 => Vni,
                1 => Telex,
                2 => TelexExtended,
                3 => Viqr,
                _ => Telex
            };
            ResetInternal();
        }
    }

    public int TypingMethodIndex
    {
        get
        {
            lock (_sync)
            {
                if (ReferenceEquals(_core.kieuGo, Vni)) return 0;
                if (ReferenceEquals(_core.kieuGo, Telex)) return 1;
                if (ReferenceEquals(_core.kieuGo, TelexExtended)) return 2;
                if (ReferenceEquals(_core.kieuGo, Viqr)) return 3;
                return 1;
            }
        }
    }

    public string PreviewText(string text)
    {
        lock (_sync)
        {
            var preview = new VietnameseProcessor(_codeTable, GetTypingMap())
            {
                boDauKieuMoi = ModernToneRemoval,
                ktChinhTa = SpellCheckLevel
            };
            return preview.Convert(text) ?? text;
        }
    }

    private char[] GetTypingMap() => TypingMethodIndex switch
    {
        0 => Vni,
        1 => Telex,
        2 => TelexExtended,
        3 => Viqr,
        _ => Telex
    };

    public string[] GetCodeTable() => _codeTable.ToArray();

    public void SetCodeTable(string[] table)
    {
        if (table is null || table.Length < 146)
            throw new ArgumentException("Bảng mã phải có ít nhất 146 ký tự.", nameof(table));

        lock (_sync)
        {
            _codeTable = table;
            _core.bangMa = table;
            ResetInternal();
        }
    }

    public string GetBuffer()
    {
        lock (_sync) return _screenBuffer;
    }

    public InputTransformResult ProcessKey(char c, bool isShift)
    {
        lock (_sync)
        {
            bool shortcutsOnly = !Enabled;
            if (shortcutsOnly && !(ShortcutsEnabled && ShortcutsWhenDisabled))
                return InputTransformResult.PassThrough();

            if (!IsTypingChar(c))
            {
                ResetInternal();
                return InputTransformResult.PassThrough();
            }

            _rawBuffer += c;
            if (_rawBuffer.Length > 100)
                _rawBuffer = _rawBuffer[^100..];

            _core.Reset();
            string converted = shortcutsOnly ? _rawBuffer : (_core.Convert(_rawBuffer) ?? _rawBuffer);

            // Shortcuts without space
            if (ShortcutsEnabled && (shortcutsOnly ? ShortcutsWhenDisabled : true) &&
                ShortcutWithoutSpace && _shortcuts.TryGetValue(_rawBuffer, out var shortcutReplacement))
            {
                string shortcutOutput = ApplyShortcutCase(_rawBuffer, shortcutReplacement);
                int bsCount = _screenBuffer.Length;
                ResetInternal();
                return InputTransformResult.Replace(shortcutOutput, bsCount);
            }

            // Normal typing pass-through: if the new character simply appends to screen without modifying previous characters
            if (converted.Length == _screenBuffer.Length + 1 &&
                converted.StartsWith(_screenBuffer, StringComparison.Ordinal) &&
                converted[^1] == c)
            {
                _screenBuffer = converted;
                return InputTransformResult.PassThrough();
            }

            if (string.Equals(converted, _screenBuffer, StringComparison.Ordinal))
            {
                return InputTransformResult.PassThrough();
            }

            // A transformation occurred (e.g. aa -> â, as -> á, viets -> viết).
            // Calculate common prefix to minimize the number of backspaces needed!
            int commonPrefix = 0;
            int maxCommon = Math.Min(_screenBuffer.Length, converted.Length);
            while (commonPrefix < maxCommon && _screenBuffer[commonPrefix] == converted[commonPrefix])
            {
                commonPrefix++;
            }

            int backspaceCount = _screenBuffer.Length - commonPrefix;
            string textToSend = converted[commonPrefix..];
            _screenBuffer = converted;

            return InputTransformResult.Replace(textToSend, backspaceCount);
        }
    }

    public InputTransformResult ProcessBackspace()
    {
        lock (_sync)
        {
            if (_rawBuffer.Length == 0)
                return InputTransformResult.PassThrough();

            _rawBuffer = _rawBuffer[..^1];
            _core.Reset();
            string converted = _rawBuffer.Length == 0 ? string.Empty : (_core.Convert(_rawBuffer) ?? _rawBuffer);

            // If the deletion was simply a single normal character, let OS handle backspace natively
            if (_screenBuffer.Length > 0 && converted.Length == _screenBuffer.Length - 1 &&
                _screenBuffer.StartsWith(converted, StringComparison.Ordinal))
            {
                _screenBuffer = converted;
                return InputTransformResult.PassThrough();
            }

            int backspaceCount = _screenBuffer.Length;
            _screenBuffer = converted;
            return InputTransformResult.Replace(converted, backspaceCount);
        }
    }

    public InputTransformResult ProcessBoundary(char boundary)
    {
        lock (_sync)
        {
            if (_rawBuffer.Length == 0)
            {
                ResetInternal();
                return InputTransformResult.PassThrough();
            }

            string output = _screenBuffer;
            bool useShortcut = ShortcutsEnabled && (Enabled || ShortcutsWhenDisabled);
            if (useShortcut && _shortcuts.TryGetValue(_rawBuffer, out var replacement))
                output = ApplyShortcutCase(_rawBuffer, replacement);
            else
            {
                _core.Reset();
                output = _core.Convert(_rawBuffer) ?? _screenBuffer;
            }

            if (string.Equals(output, _screenBuffer, StringComparison.Ordinal))
            {
                ResetInternal();
                return InputTransformResult.PassThrough();
            }

            int backspaceCount = _screenBuffer.Length;
            ResetInternal();
            return InputTransformResult.Replace(output, backspaceCount);
        }
    }

    public InputTransformResult RestoreWord()
    {
        lock (_sync)
        {
            if (_screenBuffer.Length == 0 || _rawBuffer.Length == 0 || string.Equals(_screenBuffer, _rawBuffer, StringComparison.Ordinal))
                return InputTransformResult.PassThrough();

            int bsCount = _screenBuffer.Length;
            string restored = _rawBuffer;
            _screenBuffer = _rawBuffer;
            return InputTransformResult.Replace(restored, bsCount);
        }
    }

    public void Reset()
    {
        lock (_sync) ResetInternal();
    }

    private void ResetInternal()
    {
        _rawBuffer = string.Empty;
        _screenBuffer = string.Empty;
        _core.Reset();
    }

    private static bool IsTypingChar(char c)
    {
        if (char.IsLetterOrDigit(c)) return true;
        return c is '~' or '`' or '\'' or '?' or '.' or '^' or '+' or '(' or ')' or 'w' or 'W';
    }

    private static string ApplyShortcutCase(string trigger, string replacement)
    {
        if (trigger.Length == 0) return replacement;
        if (trigger.All(char.IsUpper)) return replacement.ToUpperInvariant();
        if (trigger.All(c => !char.IsLetter(c) || char.IsLower(c))) return replacement;
        return replacement.Length == 0 ? replacement : char.ToUpper(trigger[0]) + replacement[1..];
    }
}
