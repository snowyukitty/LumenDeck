namespace LumenDeck;

/// <summary>A normalised RegisterHotKey chord.</summary>
internal readonly record struct ScreenBlankHotkey(uint Modifiers, uint VirtualKey, string Text)
{
    public static bool TryParse(string text, out ScreenBlankHotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        uint modifiers = 0;
        Keys key = Keys.None;
        foreach (string raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string token = raw.Trim();
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifiers |= Native.MOD_CONTROL;
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= Native.MOD_ALT;
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= Native.MOD_SHIFT;
            else if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= Native.MOD_WIN;
            else if (key == Keys.None && TryParseKey(token, out Keys parsed))
                key = parsed;
            else
                return false;
        }

        if (modifiers == 0 || key == Keys.None || IsModifierKey(key)) return false;
        hotkey = Create(modifiers, key);
        return true;
    }

    public static bool TryFromKeyData(Keys keyData, out ScreenBlankHotkey hotkey)
    {
        uint modifiers = 0;
        if ((keyData & Keys.Control) != 0) modifiers |= Native.MOD_CONTROL;
        if ((keyData & Keys.Alt) != 0) modifiers |= Native.MOD_ALT;
        if ((keyData & Keys.Shift) != 0) modifiers |= Native.MOD_SHIFT;

        Keys key = keyData & Keys.KeyCode;
        if (modifiers == 0 || key == Keys.None || IsModifierKey(key))
        {
            hotkey = default;
            return false;
        }

        hotkey = Create(modifiers, key);
        return true;
    }

    private static ScreenBlankHotkey Create(uint modifiers, Keys key)
    {
        var parts = new List<string>();
        if ((modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Native.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyName(key));
        return new ScreenBlankHotkey(modifiers, (uint)key, string.Join("+", parts));
    }

    private static bool TryParseKey(string token, out Keys key)
    {
        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                key = Keys.A + (c - 'A');
                return true;
            }
            if (c is >= '0' and <= '9')
            {
                key = Keys.D0 + (c - '0');
                return true;
            }
        }

        return Enum.TryParse(token, true, out key) && key != Keys.None;
    }

    private static string KeyName(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z) return ((char)('A' + key - Keys.A)).ToString();
        if (key is >= Keys.D0 and <= Keys.D9) return ((char)('0' + key - Keys.D0)).ToString();
        return key.ToString();
    }

    private static bool IsModifierKey(Keys key) => key is
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.LWin or Keys.RWin;
}

/// <summary>Small key-capture dialog for one monitor's blackout shortcut.</summary>
internal sealed class ScreenBlankHotkeyDialog : Form
{
    private readonly Label _captured;
    private readonly Button _save;

    public string SelectedShortcut { get; private set; }

    public ScreenBlankHotkeyDialog(string monitorName, string current)
    {
        Text = "Screen blank shortcut";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        ClientSize = new Size(430, 184);
        BackColor = Theme.Base;
        ForeColor = Theme.Ink;
        Font = Theme.Body;

        Controls.Add(new Label
        {
            Text = $"Press a shortcut for {monitorName}.",
            AutoSize = true,
            Location = new Point(18, 18),
            ForeColor = Theme.Ink,
        });
        Controls.Add(new Label
        {
            Text = "It toggles a reversible black overlay and restores the saved brightness.",
            AutoSize = false,
            Bounds = new Rectangle(18, 42, 394, 34),
            ForeColor = Theme.InkMuted,
        });

        _captured = new Label
        {
            Text = string.IsNullOrEmpty(current) ? "Press Ctrl/Alt/Shift plus a key" : current,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(18, 76, 394, 34),
            BackColor = Theme.Sunken,
            ForeColor = string.IsNullOrEmpty(current) ? Theme.InkFaint : Theme.AmberLight,
            Font = Theme.Value,
        };
        Controls.Add(_captured);

        _save = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(236, 132, 82, 30),
            Enabled = !string.IsNullOrEmpty(current),
        };
        _save.Click += (_, _) => SelectedShortcut ??= current;

        var clear = new Button { Text = "Clear", Bounds = new Rectangle(18, 132, 82, 30) };
        clear.Click += (_, _) =>
        {
            SelectedShortcut = "";
            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(330, 132, 82, 30),
        };

        Controls.Add(clear);
        Controls.Add(_save);
        Controls.Add(cancel);
        AcceptButton = _save;
        CancelButton = cancel;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            base.OnKeyDown(e);
            return;
        }

        if (ScreenBlankHotkey.TryFromKeyData(e.KeyData, out var hotkey))
        {
            SelectedShortcut = hotkey.Text;
            _captured.Text = hotkey.Text;
            _captured.ForeColor = Theme.AmberLight;
            _save.Enabled = true;
        }
        else
        {
            _captured.Text = "Include Ctrl, Alt or Shift plus one other key";
            _captured.ForeColor = Theme.Warn;
            _save.Enabled = false;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }
}
