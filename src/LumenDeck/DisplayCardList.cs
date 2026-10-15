using System.Runtime.InteropServices;

namespace LumenDeck;

/// <summary>Keeps native scrollbars in sync after autosized card content changes.</summary>
internal sealed class DisplayCardList : FlowLayoutPanel
{
    private bool _syncingScroll;

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        // FlowLayout can clear the managed horizontal extent while leaving
        // WS_HSCROLL behind after a hidden card or observation label reflows.
        // Only remove that stale native bar; retain real overflow and the
        // current vertical scroll position. The native resize can reenter layout.
        if (_syncingScroll || !AutoScroll || !IsHandleCreated || HorizontalScroll.Visible ||
            (GetWindowLong(Handle, -16) & 0x00100000) == 0) return;
        _syncingScroll = true;
        try { ShowScrollBar(Handle, 0, false); }
        finally { _syncingScroll = false; }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowScrollBar(IntPtr window, int bar, [MarshalAs(UnmanagedType.Bool)] bool show);
}
