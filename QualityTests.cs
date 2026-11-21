using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LumaShot
{
    public static class QualityTests
    {
        public static void Run(string root)
        {
            Directory.CreateDirectory(root);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var results = new List<string>();
            var library = new Library(root);
            Demo.Seed(library);
            var ui = new MainUI(library, true);
            app.Dispatcher.BeginInvoke(
                new Action(async () =>
                {
                    try
                    {
                        await ui.CheckQuality(results);
                    }
                    catch (Exception error)
                    {
                        results.Add("FAIL " + error);
                        Environment.ExitCode = 1;
                    }
                    finally
                    {
                        File.WriteAllLines(Path.Combine(root, "quality-results.txt"), results);
                        ui.StopQuality();
                        app.Shutdown();
                    }
                })
            );
            app.Run();
        }
    }

    public partial class MainUI
    {
        internal void StopQuality()
        {
            poll.Stop();
            syncTimer.Stop();
            engine.Dispose();
            tray.Dispose();
            if (backend != null)
                backend.Dispose();
        }

        internal async Task CheckQuality(List<string> results)
        {
            poll.Stop();
            syncTimer.Stop();
            var before = new Shot
            {
                Id = "test",
                Title = "old",
                Notes = "old notes",
                Tags = "old tags",
            };
            var local = new Shot
            {
                Id = "test",
                Title = "old",
                Notes = "local notes",
                Tags = "old tags",
            };
            var remote = new Shot
            {
                Id = "test",
                Title = "remote",
                Notes = "remote notes",
                Tags = "remote tags",
            };
            MergeRecord(local, before, remote);
            SurfaceChecks.Require(
                local.Title == "remote"
                    && local.Notes == "local notes"
                    && local.Tags == "remote tags",
                "per-field merge"
            );
            results.Add(
                "PASS sync preserves edits made in flight and accepts unrelated remote fields"
            );

            selected = lib.Items[0];
            Refresh();
            var oldCard = cardBorders[selected.Id];
            var first = selected;
            recordEditors["Notes"].Text = "typed immediately before switching";
            SelectCard(lib.Items[1]);
            SurfaceChecks.Require(
                new Library(lib.Root).Items.First(x => x.Id == first.Id).Notes
                    == "typed immediately before switching",
                "immediate save on selection"
            );
            SurfaceChecks.Require(
                ReferenceEquals(cardBorders[first.Id], oldCard),
                "selection recreated card"
            );
            results.Add("PASS switching records flushes edits and reuses grid controls");

            var tags = (StackPanel)TagEditor(first);
            var draft = (TextBox)((Grid)tags.Children[1]).Children[0];
            draft.Text = "新增标签，工作";
            FlushEdits();
            SurfaceChecks.Require(
                new Library(lib.Root).Items.First(x => x.Id == first.Id).Tags.Contains("新增标签"),
                "pending tag not saved"
            );
            results.Add("PASS pending tag input is committed before leaving the record");
            var savedRecords = lib.Items.ToList();
            while (lib.Items.Count < 60)
            {
                var copy = new Shot();
                foreach (var field in typeof(Shot).GetFields())
                    field.SetValue(copy, field.GetValue(first));
                copy.Id = Guid.NewGuid().ToString("N");
                lib.Items.Add(copy);
            }
            shown = 48;
            Refresh();
            SurfaceChecks.Require(cardBorders.Count == 48, "first page not bounded");
            shown = 96;
            Refresh();
            SurfaceChecks.Require(cardBorders.Count == 12, "second page retained old controls");
            lib.Items.Clear();
            lib.Items.AddRange(savedRecords);
            shown = 48;
            Refresh();
            results.Add(
                "PASS 60-record library renders 48 then 12 cards without retaining the previous page"
            );
            var toggle = Switch(true, "test");
            toggle.ApplyTemplate();
            var knob = (FrameworkElement)toggle.Template.FindName("Knob", toggle);
            SurfaceChecks.Require(
                ((System.Windows.Media.TranslateTransform)knob.RenderTransform).X == 18,
                "initial enabled switch thumb"
            );
            results.Add("PASS enabled switch is visually on before animation or first input");

            var message = ChatMessage("assistant", "Model", "start");
            var panel = (StackPanel)message.Child;
            var header = panel.Children[0];
            UpdateChatMessage(message, "partial answer", false);
            var body = ((ChatMessageState)message.Tag).Body.Content;
            UpdateChatMessage(message, "longer answer", false);
            SurfaceChecks.Require(
                ReferenceEquals(header, panel.Children[0])
                    && ReferenceEquals(body, ((ChatMessageState)message.Tag).Body.Content),
                "stream replaces header or body"
            );
            UpdateChatMessage(message, "stopped answer", true);
            SurfaceChecks.Require(
                ReferenceEquals(header, panel.Children[0])
                    && ((ChatMessageState)message.Tag).Text == "stopped answer",
                "stopped answer copy state"
            );
            results.Add(
                "PASS AI streaming keeps message header/copy action and reuses text control; stopped answer remains copyable"
            );

            string thumb = lib.Full(first.Thumb);
            var a = Images.Load(thumb, 480);
            var b = Images.Load(thumb, 480);
            SurfaceChecks.Require(
                ReferenceEquals(a, b) && ThumbnailCache.Bytes <= 32 * 1024 * 1024,
                "thumbnail cache"
            );
            results.Add("PASS repeated thumbnail decode uses bounded 32 MiB cache");

            string incoming = Path.Combine(engine.Inbox, "quality-image.png");
            Images.Save(Demo.Image(0), incoming);
            var imported = await engine.Import(lib, incoming, false);
            SurfaceChecks.Require(
                !File.Exists(incoming) && File.Exists(lib.Full(imported.File)),
                "inbox not reclaimed"
            );
            string manual = Path.Combine(lib.Root, "manual.png");
            Images.Save(Demo.Image(1), manual);
            var manualShot = await engine.Import(lib, manual, true);
            StorageMaintenance.RemoveImportedInboxFile(lib, engine.Inbox, manual, manualShot);
            SurfaceChecks.Require(File.Exists(manual), "manual source removed");
            results.Add(
                "PASS verified inbox source is removed after commit; manual source is retained"
            );
            string text = await LocalOcr.Read(lib, imported);
            SurfaceChecks.Require(
                !string.IsNullOrWhiteSpace(text),
                "in-process OCR returned empty"
            );
            results.Add("PASS Windows OCR runs in process and recognizes synthetic screenshot");
            var rect = DisplayPlacement.LogicalArea(
                new System.Drawing.Rectangle(-1920, 0, 1920, 1080),
                1.5
            );
            SurfaceChecks.Require(
                rect.Left == -1280 && rect.Width == 1280 && rect.Height == 720,
                "mixed DPI coordinates"
            );
            results.Add(
                "PASS monitor-specific conversion handles negative origins and 150% scaling (physical multi-monitor not tested)"
            );
            var poster = lib.Add(Images.Load(manual), "legacy video", "video");
            string oldPoster = poster.File;
            poster.File = "captures/" + poster.Id + ".mp4";
            poster.Original = poster.File;
            File.WriteAllText(lib.Full(poster.File), "synthetic video fixture");
            lib.Save();
            StorageMaintenance.CleanRedundantFiles(lib, engine.Inbox);
            SurfaceChecks.Require(
                File.Exists(lib.Full(oldPoster)),
                "backup referenced poster deleted"
            );
            lib.Save();
            StorageMaintenance.CleanRedundantFiles(lib, engine.Inbox);
            SurfaceChecks.Require(
                !File.Exists(lib.Full(oldPoster)) && File.Exists(lib.Full(poster.File)),
                "legacy poster cleanup"
            );
            results.Add(
                "PASS storage cleanup removes verified legacy poster, preserves original and backup references"
            );
            var previewLife = new CapturePreviewLifetime();
            previewLife.Add("hover-card", 0);
            previewLife.ExtendAtLeast(new[] { "hover-card" }, 17.5);
            SurfaceChecks.Require(
                !previewLife.IsExpired("hover-card", 16)
                    && previewLife.IsExpired("hover-card", 17.5),
                "hover extension did not defer expiry"
            );
            results.Add("PASS floating preview interaction defers 15-second expiry cleanly");
        }
    }
}
