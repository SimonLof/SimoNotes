using System.Collections.Generic;
using System.Windows.Input;

namespace SimoNotes.Settings;

internal sealed record HotkeyBinding(uint Modifiers, uint VirtualKey)
{
    private const uint SupportedModifiers = 0x0001 | 0x0002 | 0x0004;

    public static HotkeyBinding Default => new(0x0001 | 0x0002, 0x4E);
    public static HotkeyBinding BringAllNotesToFrontDefault => new(0x0001 | 0x0002, 0x46);

    public bool IsValid =>
        (Modifiers & SupportedModifiers) != 0 &&
        (Modifiers & ~SupportedModifiers) == 0 &&
        VirtualKey is > 0 and <= 0xFF;

    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
            if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
            if ((Modifiers & 0x0004) != 0) parts.Add("Shift");

            var keyName = KeyInterop.KeyFromVirtualKey((int)VirtualKey).ToString();
            if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
            {
                keyName = keyName[1..];
            }

            parts.Add(keyName);
            return string.Join("+", parts);
        }
    }
}