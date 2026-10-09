using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LumenDeck;

internal static partial class Program
{
    private static void CheckSettingsStorage(string directory)
    {
        string work = Path.Combine(directory, "settings-storage");
        Directory.CreateDirectory(work);
        string path = Path.Combine(work, "settings.json");
        var settings = AppSettings.Load(path);
        settings.StartMinimised = true;
        settings.SetScreenBlank("example", "Fictitious monitor", true, 42, 107);
        Assert(settings.Save() && settings.LastSaveResult == SettingsSaveResult.Saved,
            "settings first creation confirms the captured preference and recovery record");
        Assert(AppSettings.Load(path).ScreenBlankActiveFor("example"), "recovery marker survives actual disk reload");
        Assert(!File.ReadAllText(path).Contains("LastSaveResult") && !File.ReadAllText(path).Contains("IsSavePending"),
            "settings save diagnostics and admission are not serialized as preferences");
        settings.StartMinimised = false;
        Assert(settings.Save() && !AppSettings.Load(path).StartMinimised, "same writer advances its source after a replacement");

        byte[] original = File.ReadAllBytes(path);
        byte[] replacement = Encoding.UTF8.GetBytes("{\"Monitors\":[],\"StartMinimised\":true}");
        var stagedFailure = new SettingsFileStore(path, original, stage =>
        {
            if (stage == SettingsWriteStage.Staged) throw new IOException("injected after flush");
        });
        Assert(stagedFailure.Save(replacement) == SettingsSaveResult.Failed && File.ReadAllBytes(path).SequenceEqual(original),
            "staging failure preserves byte-exact predecessor settings");
        Assert(Directory.GetFiles(work, ".settings-*.tmp").Length == 0, "failed writer removes only its own staged file");
        string unrelated = Path.Combine(work, ".settings-unrelated.tmp");
        File.WriteAllText(unrelated, "another writer owns this file");
        Assert(stagedFailure.Save(replacement) == SettingsSaveResult.Failed && File.Exists(unrelated),
            "cleanup never sweeps another writer's temporary file");

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert(new SettingsFileStore(path, original).Save(replacement) == SettingsSaveResult.Failed,
                "locked destination reports unconfirmed replacement");
        Assert(File.ReadAllBytes(path).SequenceEqual(original), "locked replacement leaves predecessor bytes intact");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try { Assert(new SettingsFileStore(path, original).Save(replacement) == SettingsSaveResult.Failed,
            "read-only destination refuses settings replacement"); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }

        var changed = new SettingsFileStore(path, original, stage =>
        {
            if (stage == SettingsWriteStage.Staged) File.WriteAllText(path, "{\"external\":true}");
        });
        Assert(changed.Save(replacement) == SettingsSaveResult.SourceChanged && File.ReadAllText(path) == "{\"external\":true}",
            "changed source after staging is preserved instead of overwritten");
        Assert(Directory.GetFiles(work, ".settings-*.tmp").SequenceEqual(new[] { unrelated }),
            "source conflict cleans owned staging while retaining unrelated temporary content");

        var failedAcknowledgement = new SettingsFileStore(path, File.ReadAllBytes(path), stage =>
        {
            if (stage == SettingsWriteStage.Committed) throw new IOException("injected lost result");
        });
        Assert(failedAcknowledgement.Save(replacement) == SettingsSaveResult.Failed && File.ReadAllBytes(path).SequenceEqual(replacement),
            "failed acknowledgement is not a promise of unchanged disk bytes");
        Assert(new SettingsFileStore(path, null).Save(original) == SettingsSaveResult.SourceChanged,
            "a missing-source writer cannot overwrite a file created since loading");
        var deleted = AppSettings.Load(path);
        File.Delete(path);
        Assert(!deleted.Save() && deleted.LastSaveResult == SettingsSaveResult.SourceChanged && !File.Exists(path),
            "stale writer does not recreate a source removed outside the app");

        foreach (string damaged in new[] { "{broken", "null", "[]" })
        {
            File.WriteAllText(path, damaged);
            var recovered = AppSettings.Load(path);
            recovered.StartMinimised = true;
            Assert(recovered.Monitors.Count == 0 && !recovered.Save() && recovered.LastSaveResult == SettingsSaveResult.Unavailable
                && File.ReadAllText(path) == damaged, "fallback session does not replace an invalid settings source");
        }
        using (var unreadable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var recovered = AppSettings.Load(path);
            Assert(!recovered.Save() && recovered.LastSaveResult == SettingsSaveResult.Unavailable,
                "unreadable settings source is retained when using session defaults");
        }
        foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32 })
        {
            string json = "{\"Monitors\":[{\"Key\":\"bom\",\"LastSeenName\":\"架空ディスプレイ\"}],\"StartMinimised\":true}";
            File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(json)).ToArray());
            var withBom = AppSettings.Load(path);
            Assert(withBom.StartMinimised && withBom.Find("bom")?.LastSeenName == "架空ディスプレイ" && withBom.Save(),
                $"legacy BOM text settings load and replace at {encoding.WebName}");
        }
        File.WriteAllText(path, "{\"Monitors\":null,\"FutureRoot\":{\"version\":2}}");
        var shape = AppSettings.Load(path);
        Assert(shape.Monitors.Count == 0 && shape.Save(), "repairable null monitor-list shape retains existing launch compatibility");
        File.WriteAllText(path, "{\"Monitors\":[{\"Key\":\"example\",\"FutureControl\":{\"値\":7}}],\"FutureRoot\":[1,2]}");
        var future = AppSettings.Load(path);
        future.StartMinimised = true;
        Assert(future.Save(), "known preference changes save alongside future fields");
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
            Assert(document.RootElement.GetProperty("FutureRoot")[1].GetInt32() == 2 &&
                document.RootElement.GetProperty("Monitors")[0].GetProperty("FutureControl").GetProperty("値").GetInt32() == 7,
                "unknown root and per-monitor fields survive settings round trip");

        var holder = new SettingsFileStore(path, File.ReadAllBytes(path));
        using (var mutex = new Mutex(false, holder.MutexName))
        {
            var owner = new Thread(() => mutex.WaitOne());
            owner.Start();
            Assert(owner.Join(2000), "isolated mutex owner terminates for abandoned ownership check");
            Assert(holder.Save(File.ReadAllBytes(path)) == SettingsSaveResult.Saved,
                "abandoned settings mutex still validates and commits the source");
        }
        CheckSettingsPeer(work, path, busy: true);
        CheckSettingsPeer(work, path, busy: false);
        var cliSettings = AppSettings.Load(path);
        File.AppendAllText(path, " ");
        using var output = new StringWriter();
        using var errors = new StringWriter();
        Assert(Cli.FinishAdjustment(cliSettings, Cli.ExitOk, ["brightness 42"], ["Fictitious display"], output, errors) == Cli.ExitWriteRefused
            && output.ToString().StartsWith("Applied") && errors.ToString().Contains("Settings save not confirmed")
            && !errors.ToString().Contains("monitor refused"),
            "CLI settings conflict returns exit 2 without inventing a refused monitor request");
        output.GetStringBuilder().Clear();
        errors.GetStringBuilder().Clear();
        Assert(Cli.FinishAdjustment(AppSettings.Load(path), Cli.ExitWriteRefused, ["brightness 42"], ["Fictitious display"], output, errors)
            == Cli.ExitWriteRefused && output.ToString().StartsWith("Requested") && errors.ToString().Contains("monitor refused"),
            "CLI monitor refusal remains distinct from successful settings persistence");
        CheckCapturedSettings(work);
    }

    private static void CheckSettingsPeer(string work, string path, bool busy)
    {
        var initial = AppSettings.Load(path);
        initial.SetScreenBlank("protected", "Fictitious protected display", false, null);
        Assert(initial.Save(), "peer baseline starts without the winning recovery marker");
        string token = Guid.NewGuid().ToString("N");
        string ready = Path.Combine(work, token + ".ready");
        string go = Path.Combine(work, token + ".go");
        string result = Path.Combine(work, token + ".result");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "LumenDeck.ControlTests.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true };
        foreach (string argument in new[] { "--settings-peer", path, ready, go, result }) start.ArgumentList.Add(argument);
        using var peer = Process.Start(start);
        try
        {
            Assert(SpinWait.SpinUntil(() => File.Exists(ready) || peer.HasExited, 5000) && File.Exists(ready),
                "isolated settings peer captures its own source before the competing save");
            byte[] source = File.ReadAllBytes(path);
            var winner = AppSettings.Load(path);
            winner.SetScreenBlank("protected", "Fictitious protected display", true, 55, 140);
            if (busy)
            {
                var store = new SettingsFileStore(path, source, stage =>
                {
                    if (stage != SettingsWriteStage.Staged) return;
                    File.WriteAllText(go, "go");
                    bool exited = peer.WaitForExit(5000);
                    string outcome = File.Exists(result) ? File.ReadAllText(result) : "no result";
                    Assert(exited && peer.ExitCode == 0 && outcome == "Busy",
                        $"cross-process contender reports busy while the writer owns the named mutex (exited={exited}, result={outcome})");
                });
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(winner);
                Assert(store.Save(payload) == SettingsSaveResult.Saved, "mutex owner publishes its own complete snapshot");
            }
            else
            {
                Assert(winner.Save(), "winner commits a new recovery marker before stale peer runs");
                File.WriteAllText(go, "go");
                Assert(peer.WaitForExit(5000) && peer.ExitCode == 0 && File.ReadAllText(result) == "SourceChanged",
                    "cross-process stale snapshot is refused instead of deleting a newer recovery marker");
            }
            Assert(AppSettings.Load(path).ScreenBlankActiveFor("protected"),
                "competing settings writer cannot erase the winning blackout recovery record");
        }
        finally
        {
            if (!peer.HasExited) { peer.Kill(entireProcessTree: true); peer.WaitForExit(2000); }
        }
    }

    private static void RunSettingsPeer(string path, string ready, string go, string result)
    {
        var settings = AppSettings.Load(path);
        settings.MinimiseToTray = !settings.MinimiseToTray;
        File.WriteAllText(ready, "ready");
        if (!SpinWait.SpinUntil(() => File.Exists(go), 10000)) { Environment.ExitCode = 3; return; }
        settings.Save();
        File.WriteAllText(result, settings.LastSaveResult.ToString());
    }
}
