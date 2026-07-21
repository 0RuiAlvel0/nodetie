using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace NodeTie.Infrastructure.Hotkeys;

public sealed record HotkeyBinding(HotkeyModifiers Modifiers, Keys Key)
{
    public static HotkeyBinding Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.L);

    public static bool TryParse(string? value, out HotkeyBinding binding)
    {
        binding = Default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] tokens = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            string token = tokens[i];
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || token.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Control;
                continue;
            }

            if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Shift;
                continue;
            }

            if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Alt;
                continue;
            }

            if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) || token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Win;
                continue;
            }

            return false;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            return false;
        }

        string keyToken = tokens[^1];
        if (!Enum.TryParse<Keys>(keyToken, ignoreCase: true, out var key))
        {
            return false;
        }

        if (IsModifierKey(key))
        {
            return false;
        }

        binding = new HotkeyBinding(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        List<string> segments = new();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            segments.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            segments.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            segments.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            segments.Add("Win");
        }

        segments.Add(Key.ToString());
        return string.Join('+', segments);
    }

    public static IReadOnlyList<Keys> GetSupportedKeys()
    {
        List<Keys> keys = new();

        keys.AddRange(Enumerable.Range(0, 10).Select(i => Keys.D0 + i));
        keys.AddRange(Enumerable.Range(0, 26).Select(i => Keys.A + i));
        keys.AddRange(Enumerable.Range(1, 12).Select(i => Keys.F1 + (i - 1)));

        return keys;
    }

    private static bool IsModifierKey(Keys key)
    {
        return key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin;
    }
}
