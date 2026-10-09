using System.Reflection;
using System.Windows.Forms.Automation;

namespace LumenDeck;

/// <summary>Offline product reference. Reads GUI assembly metadata only.</summary>
internal class AboutForm : ReferenceForm
{
    internal static string VersionText => typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    internal static string BuildText => typeof(MainForm).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { Length: > 0 } build
            ? build : "not recorded";

    public AboutForm(Action<string> copy = null) : base("About LumenDeck",
        $"Version {VersionText}\nBuild: {BuildText}",
    [
        new("Start with your display", "Choose a display card and adjust brightness gradually. Available controls depend on the display and connection. Day, Evening and Night request preset levels; Custom requests your saved levels. Check the screen and any card warnings after an adjustment."),
        new("Keyboard controls", "Press F1 in the main window, or choose Settings > Keyboard controls. That offline guide covers display navigation, slider adjustments, exact values and screen blackout shortcuts."),
        new("Connection help and support", "Choose Connection help for offline checks when a display or control is missing. View support report opens a preview you can review before copying it into a bug report. Opening these references does not adjust a display."),
        new("Portable package", "The ZIP includes QUICKSTART.txt and LICENSE.txt beside LumenDeck.exe. Settings are stored separately in your Windows application-data folder. Use Exit from the notification-area menu before replacing app files."),
        new("MIT licence", "Copyright © 2026 LumenDeck contributors. LumenDeck is distributed under the MIT licence. See LICENSE.txt in the portable package, or LICENSE in the source, for the full terms."),
    ])
    {
        var writeClipboard = copy ?? Clipboard.SetText;
        var status = new Label { Name = "copy-version-status", AutoSize = true, UseMnemonic = false,
            ForeColor = Theme.InkMuted, Visible = false, AccessibleRole = AccessibleRole.StatusBar };
        var copyButton = new Button { Name = "copy-version", Text = "Copy version", AutoSize = true,
            MinimumSize = new Size(110, 32),
            AccessibleDescription = "Copy only LumenDeck version and build details to the clipboard." };
        copyButton.Click += (_, _) =>
        {
            bool copied = false;
            try
            {
                writeClipboard($"LumenDeck {VersionText}{Environment.NewLine}Build: {BuildText}");
                copied = true;
            }
            catch (Exception)
            {
                // Clipboard failures can include private system/application paths.
            }
            // Clipboard callbacks can close the window before returning.
            if (IsDisposed || Disposing || status.IsDisposed) return;
            status.Text = copied ? "Version details copied."
                : "Could not copy. Version and build remain shown above.";
            status.Visible = true;
            status.AccessibilityObject.RaiseAutomationNotification(
                copied ? AutomationNotificationKind.ActionCompleted : AutomationNotificationKind.ActionAborted,
                AutomationNotificationProcessing.ImportantMostRecent,
                status.Text);
        };
        Footer.Controls.Add(copyButton);
        Footer.SetFlowBreak(copyButton, true);
        Footer.Controls.Add(status);
        void FitStatus() => status.MaximumSize = new Size(Math.Max(1, Footer.ClientSize.Width -
            Footer.Padding.Horizontal - status.Margin.Horizontal), 0);
        Footer.SizeChanged += (_, _) => FitStatus();
        FitStatus();
    }
}
