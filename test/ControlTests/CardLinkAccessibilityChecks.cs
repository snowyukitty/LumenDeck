using LumenDeck;
using Monitor = LumenDeck.Monitor;

internal static partial class Program
{
    private static void CheckCardLinkAccessibility()
    {
        var profileNames = new HashSet<string>();
        foreach (string position in new[] { "left", "right", "" })
        {
            using var monitor = Display("card-links-" + position);
            monitor.Description = "Studio & Desk";
            monitor.PositionLabel = position;
            // No native handle exists. The presentation flag is cleared in finally
            // before Monitor.Dispose; all queued I/O uses the fake transport.
            var transport = new FakeDdcTransport();
            using var worker = new DdcWorker(transport);
            using var form = new PreviewForm { ClientSize = new Size(600, 1000) };
            int helpCalls = 0, profileCalls = 0, featureCalls = 0;
            try
            {
                monitor.HasPhysicalHandle = true;
                using var card = new MonitorCard(monitor, new AppSettings(), worker, null, null,
                    null, null, _ => featureCalls++, () => helpCalls++, _ => profileCalls++);
                card.SetWidth(570);
                form.Controls.Add(card);
                form.Show();
                string context = position.Length == 0 ? monitor.DisplayName : $"{monitor.DisplayName} ({position})";
                var profile = Descendants(card).OfType<LinkLabel>().Single(c => c.Name == "brightness-profile");
                var connection = Descendants(card).OfType<LinkLabel>().Single(c => c.Name == "connection-help");
                var disclosure = Descendants(card).OfType<LinkLabel>().Single(c => c.Name == "other-controls");
                var notice = Descendants(card).OfType<AdjustmentNotice>().Single();
                var help = notice.Controls.OfType<LinkLabel>().Single(c => c.Text == "Connection help");
                var dismiss = notice.Controls.OfType<LinkLabel>().Single(c => c.Text == "Dismiss notice");
                Assert(profile.AccessibilityObject.Name == context + ": Edit brightness estimate profile",
                    "profile navigation exposes its display and action to native accessibility");
                Assert(profileNames.Add(profile.AccessibilityObject.Name),
                    "same-model display links remain distinguishable by position or its absence");
                foreach (var link in new[] { profile, connection, disclosure, help, dismiss })
                {
                    Assert(link.AccessibilityObject.Name.StartsWith(context + ": "),
                        "card links expose the literal display context independently of their parent");
                    Assert(string.IsNullOrEmpty(link.AccessibilityObject.KeyboardShortcut),
                        "display ampersands do not invent link keyboard shortcuts");
                }
                Assert(helpCalls == 0 && profileCalls == 0 && featureCalls == 0 && transport.Writes.IsEmpty,
                    "reading native card link properties dispatches no navigation, scan or write");
                InvokeLink(profile);
                InvokeLink(connection);
                InvokeLink(disclosure);
                Assert(profileCalls == 1 && helpCalls == 1 && featureCalls == 1,
                    "contextual link names retain the original navigation callbacks");
                card.BeginFeatureLoad();
                Assert(!disclosure.Enabled && disclosure.AccessibilityObject.Name == context + ": " + disclosure.Text &&
                    disclosure.AccessibilityObject.Name.Contains("Reading"),
                    "loading other controls updates the accessible action without starting another scan");
                monitor.FeaturesLoaded = true;
                card.PopulateFeatures();
                Assert(!disclosure.Enabled && disclosure.AccessibilityObject.Name == context + ": " + disclosure.Text &&
                    disclosure.AccessibilityObject.Name.Contains("no other"),
                    "empty capability result replaces the loading accessible name");
                monitor.Features.Add(new VcpFeature { Definition = VcpCatalog.Find(0x62), Current = 20, Max = 100 });
                card.PopulateFeatures();
                Assert(disclosure.Enabled && disclosure.AccessibilityObject.Name == context + ": " + disclosure.Text &&
                    disclosure.AccessibilityObject.Name.Contains("Show 1 more"),
                    "populated capability result exposes the current contextual action");
                InvokeLink(disclosure);
                Assert(disclosure.AccessibilityObject.Name == context + ": Hide the other controls",
                    "expanded other controls expose their live hide action");
                InvokeLink(disclosure);
                Assert(disclosure.AccessibilityObject.Name == context + ": " + disclosure.Text &&
                    disclosure.AccessibilityObject.Name.Contains("Show 1 more"),
                    "collapsed other controls restore their live show action");
                card.ShowWarmthFailure();
                Assert(notice.AccessibilityObject.Name == context + ": " + notice.Message &&
                    notice.Controls.OfType<Label>().Single(c => c is not LinkLabel).AccessibilityObject.Name == notice.AccessibilityObject.Name,
                    "failure group and message identify their display while retaining the warning");
                InvokeLink(help);
                Assert(helpCalls == 2 && notice.Visible, "contextual failure help leaves its warning visible");
                InvokeLink(dismiss);
                Assert(!notice.Visible && notice.AccessibilityObject.Name == "" &&
                    notice.Controls.OfType<Label>().Single(c => c is not LinkLabel).AccessibilityObject.Name == "",
                    "dismissal clears hidden warning accessibility text");
                Assert(profileCalls == 1 && featureCalls == 1 && worker.Idle && transport.Writes.IsEmpty,
                    "context, disclosure and notice interactions issue no fake transport writes");
                monitor.HasPhysicalHandle = false;
                // Clear capabilities so the disconnected presentation has no label.
                monitor.Features.Clear();
                card.PopulateFeatures();
                Assert(disclosure.Text == "" && disclosure.AccessibilityObject.Name == "",
                    "an absent disclosure does not retain a stale contextual action");
            }
            finally { monitor.HasPhysicalHandle = false; }
        }
    }
}
