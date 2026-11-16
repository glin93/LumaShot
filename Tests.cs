using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumaShot
{
    public static class Demo
    {
        static void Text(
            DrawingContext d,
            string text,
            double x,
            double y,
            double size,
            string color
        )
        {
            d.DrawText(
                new FormattedText(
                    text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    size,
                    MainUI.B(color)
                ),
                new Point(x, y)
            );
        }

        public static BitmapSource Image(int index)
        {
            var v = new DrawingVisual();
            using (var d = v.RenderOpen())
            {
                var colors = new[]
                {
                    "#DFE2F5",
                    "#E1EAF0",
                    "#F2E6DC",
                    "#E7E6F0",
                    "#E4EEE7",
                    "#F0E2EB",
                };
                d.DrawRectangle(MainUI.B(colors[index % 6]), null, new Rect(0, 0, 1000, 650));
                if (index % 3 == 0)
                {
                    d.DrawRoundedRectangle(Brushes.White, null, new Rect(95, 65, 810, 520), 18, 18);
                    d.DrawRoundedRectangle(
                        MainUI.B("#F4F5FA"),
                        null,
                        new Rect(95, 65, 160, 520),
                        18,
                        18
                    );
                    Text(d, "orbit", 123, 95, 28, "#7361D1");
                    for (int y = 172; y < 450; y += 47)
                        d.DrawRoundedRectangle(
                            MainUI.B(y == 172 ? "#E1DCF5" : "#E7E8F0"),
                            null,
                            new Rect(120, y, 106, 12),
                            5,
                            5
                        );
                    Text(d, "Overview", 300, 100, 32, "#35394C");
                    Text(d, "A little progress, every day.", 300, 150, 16, "#989DAF");
                    for (int i = 0; i < 3; i++)
                    {
                        d.DrawRoundedRectangle(
                            MainUI.B("#F6F6FB"),
                            null,
                            new Rect(300 + i * 184, 202, 162, 103),
                            10,
                            10
                        );
                        Text(
                            d,
                            new[] { "12.8k", "84.2%", "+24.6%" }[i],
                            320 + i * 184,
                            225,
                            25,
                            "#7061CC"
                        );
                        Text(
                            d,
                            new[] { "Visitors", "Engagement", "Growth" }[i],
                            320 + i * 184,
                            265,
                            12,
                            "#999DB0"
                        );
                    }
                    for (int i = 0; i < 12; i++)
                        d.DrawRoundedRectangle(
                            MainUI.B(i > 7 ? "#8B76DD" : "#DAD3F4"),
                            null,
                            new Rect(312 + i * 42, 470 - (i * 29 % 125), 23, 50 + (i * 29 % 125)),
                            5,
                            5
                        );
                }
                else if (index % 3 == 1)
                {
                    d.DrawRoundedRectangle(
                        MainUI.B("#242A3F"),
                        null,
                        new Rect(85, 65, 830, 520),
                        18,
                        18
                    );
                    Text(d, "PROJECT / LUMA", 125, 98, 14, "#9CA6C7");
                    Text(d, "Make room for", 125, 210, 60, "#F4F5FA");
                    Text(d, "the good ideas.", 125, 290, 60, "#B7A8F3");
                    Text(d, "Capture. Collect. Create.", 130, 407, 22, "#8994B3");
                    d.DrawRoundedRectangle(
                        MainUI.B("#8D79DC"),
                        null,
                        new Rect(130, 470, 175, 49),
                        9,
                        9
                    );
                    Text(d, "Explore the work", 151, 484, 17, "#FFFFFF");
                    d.DrawEllipse(MainUI.B("#3E405D"), null, new Point(770, 240), 100, 100);
                    d.DrawEllipse(MainUI.B("#B9A4DA"), null, new Point(760, 220), 62, 62);
                }
                else
                {
                    d.DrawRoundedRectangle(
                        Brushes.White,
                        null,
                        new Rect(140, 50, 720, 550),
                        15,
                        15
                    );
                    Text(d, "Notes for a better day", 190, 100, 36, "#4A4141");
                    Text(d, "IDEAS & INSPIRATION", 192, 165, 13, "#B09C8E");
                    for (int i = 0; i < 5; i++)
                    {
                        d.DrawRoundedRectangle(
                            MainUI.B("#F5F0EC"),
                            null,
                            new Rect(190, 220 + i * 58, 620, 40),
                            5,
                            5
                        );
                        Text(
                            d,
                            new[]
                            {
                                "01   Start with something worth keeping.",
                                "02   Give every idea a place.",
                                "03   Make the details feel effortless.",
                                "04   Keep it simple, keep it useful.",
                                "05   Leave room for what comes next.",
                            }[i],
                            205,
                            231 + i * 58,
                            16,
                            "#82726B"
                        );
                    }
                }
            }
            return Images.Render(v, 1000, 650);
        }

        public static void Seed(Library lib)
        {
            string[] names =
            {
                "工作台 · 本周数据概览",
                "Luma 品牌页面参考",
                "产品讨论 · 会议备忘",
                "界面布局与配色参考",
                "灵感收集 · 首页排版",
                "下周待办与想法",
            };
            for (int i = 5; i >= 0; i--)
            {
                var x = lib.Add(Image(i), names[i], "image");
                x.Created = DateTime.Now.AddHours(-i * 5).ToString("o");
                x.Tags = i % 2 == 0 ? "工作,设计" : "灵感,参考";
                x.Notes = "这是一张用于界面预览的示例图片。";
                x.Star = i == 0 || i == 3;
            }
            lib.Save();
        }
    }

    public static class Tests
    {
        public static void SurfaceSmoke(string root)
        {
            Directory.CreateDirectory(root);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var ui = new MainUI(new Library(root), true);
            var results = new List<string>();
            ui.Window.Loaded += async (s, e) =>
            {
                try
                {
                    await ui.CheckCaptureSurfaces(results);
                    File.WriteAllLines(Path.Combine(root, "surface-test-results.txt"), results);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(root, "surface-test-error.txt"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                finally
                {
                    ui.FinishSurfaceCheck();
                }
            };
            app.Run(ui.Window);
        }

        static void Assert(bool b, string m)
        {
            if (!b)
                throw new Exception("FAIL: " + m);
        }

        public static void Run(string root)
        {
            var results = new List<string>();
            var lib = new Library(root);
            var image = Demo.Image(0);
            var a = lib.Add(image, "Alpha 测试截图", "image");
            a.Tags = "design,项目";
            a.Notes = "needle-note";
            a.Star = true;
            lib.Save();
            var reloaded = new Library(root);
            Assert(reloaded.Items.Any(x => x.Id == a.Id && x.Star), "persistence");
            results.Add("PASS persistence and favorites");
            Assert(reloaded.Query("all", "needle-note", false).Any(), "notes search");
            Assert(reloaded.Query("favorites", "项目", false).Any(), "tag search");
            results.Add("PASS title/tag/note search and favorites filter");
            var b = reloaded.Items.First(x => x.Id == a.Id);
            b.Deleted = true;
            reloaded.Save();
            Assert(!reloaded.Query("all", "Alpha", false).Any(), "trash excluded");
            Assert(reloaded.Query("trash", "Alpha", false).Any(), "trash visible");
            Assert(File.Exists(reloaded.Full(b.Original)), "original preserved");
            b.Deleted = false;
            reloaded.Save();
            results.Add("PASS reversible trash and original preservation");
            string original = b.Original;
            var edited = reloaded.Edit(b, Images.Frame(image));
            Assert(
                edited.Id != b.Id
                    && edited.File != b.File
                    && edited.ParentId == b.Id
                    && b.File == original
                    && File.Exists(reloaded.Full(original)),
                "append only edit"
            );
            results.Add(
                "PASS every edit creates a new file and record linked to its immutable original"
            );
            string p = Path.Combine(root, "library.json");
            File.WriteAllText(p, "{broken");
            var recovery = new Library(root);
            Assert(recovery.Items.Count > 0 && recovery.Notice.Length > 0, "backup recovery");
            recovery.Save();
            results.Add("PASS corrupt index recovers from backup");
            bool escaped = false;
            try
            {
                lib.Full("../escape.png");
            }
            catch (IOException)
            {
                escaped = true;
            }
            Assert(escaped, "path containment");
            results.Add("PASS library path traversal rejected");
            Assert(Images.Load(lib.Full(a.Thumb)).PixelWidth <= 480, "thumbnail bound");
            results.Add("PASS bounded thumbnails");
            string zip = Path.Combine(root, "roundtrip.zip");
            var added = new HashSet<string>();
            using (var fs = File.Create(zip))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(Path.Combine(root, "library.json"), "library.json");
                foreach (var item in recovery.Items)
                foreach (var rel in new[] { item.File, item.Original, item.Thumb }.Distinct())
                    if (added.Add(rel))
                        archive.CreateEntryFromFile(recovery.Full(rel), rel);
            }
            var target = new Library(Path.Combine(root, "restored"));
            int restored = BackupRestore.Merge(target, zip);
            Assert(restored == recovery.Items.Count, "backup restore count");
            Assert(BackupRestore.Merge(target, zip) == 0, "idempotent merge");
            results.Add("PASS backup restore and duplicate merge");
            string bad = Path.Combine(root, "bad.zip");
            using (var fs = File.Create(bad))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                using (var sw = new StreamWriter(archive.CreateEntry("../outside.txt").Open()))
                    sw.Write("invalid");
            }
            bool rejected = false;
            try
            {
                BackupRestore.Merge(target, bad);
            }
            catch (IOException)
            {
                rejected = true;
            }
            Assert(rejected, "zip traversal rejected");
            results.Add("PASS unsafe backup paths rejected");
            var prefs = new Preferences(root);
            Assert(
                !prefs.Microphone
                    && prefs.SystemAudio
                    && prefs.CaptureCopy("image")
                    && !prefs.CaptureCopy("video")
                    && prefs.CaptureOpen("image") == "none"
                    && prefs.CaptureOpen("video") == "none",
                "capture defaults"
            );
            results.Add(
                "PASS microphone off, system audio on; image copy on, video copy off; auto-open off"
            );
            Preferences.Validate("windows", prefs.ForPlatform("windows"));
            Preferences.Validate("macos", prefs.ForPlatform("macos"));
            Assert(
                Preferences.EngineActions.Distinct().Count() == 6,
                "six unique engine operations"
            );
            prefs.Values["hotkeys.windows.recordWindow"] = "Ctrl+Alt+9";
            prefs.Values["hotkeys.macos.recordWindow"] = "Shift+Cmd+8";
            prefs.Values["recording.microphone"] = "true";
            prefs.Values["recording.systemAudio"] = "false";
            prefs.Values["capture.video.copy"] = "true";
            prefs.Values["capture.image.open"] = "folder";
            prefs.Save();
            var again = new Preferences(root);
            Assert(
                again.Microphone
                    && !again.SystemAudio
                    && again.CaptureCopy("video")
                    && again.CaptureOpen("image") == "folder"
                    && again.Get("windows", "recordWindow") == "Ctrl+Alt+9"
                    && again.Get("macos", "recordWindow") == "Shift+Cmd+8",
                "settings persistence/platform isolation"
            );
            results.Add(
                "PASS eight unique platform shortcuts and media/audio settings survive restart"
            );
            bool duplicate = false;
            var bindings = again.ForPlatform("windows");
            bindings["window"] = bindings["region"];
            try
            {
                Preferences.Validate("windows", bindings);
            }
            catch (IOException)
            {
                duplicate = true;
            }
            Assert(duplicate, "duplicate shortcut rejected");
            results.Add("PASS duplicate shortcuts rejected before registration");
            File.WriteAllLines(Path.Combine(root, "test-results.txt"), results);
            Environment.ExitCode = 0;
        }

        public static void RecordingSmoke(string root)
        {
            Directory.CreateDirectory(root);
            var app = new Application();
            var engine = new CaptureEngine(root);
            var results = new List<string>();
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            var w = new Window
            {
                Title = "LumaShot recording test fixture",
                WindowStyle = WindowStyle.None,
                Topmost = true,
                WindowState = WindowState.Maximized,
                Content = new Image { Source = Demo.Image(1), Stretch = Stretch.Fill },
            };
            w.Loaded += async (s, e) =>
            {
                try
                {
                    await engine.WarmUp();
                    await Task.Delay(800);
                    foreach (
                        var action in new[]
                        {
                            "ScreenRecorderActiveWindow",
                            "ScreenRecorderCustomRegion",
                        }
                    )
                    {
                        w.Activate();
                        var known = new HashSet<string>(
                            Directory.GetFiles(engine.Inbox, "*.mp4", SearchOption.AllDirectories)
                        );
                        var task = engine.Run(action, null, 2);
                        if (await Task.WhenAny(task, Task.Delay(45000)) != task)
                        {
                            if (engine.Busy)
                                engine.Running.Kill();
                            throw new IOException("Recording did not finish: " + action);
                        }
                        await task;
                        var file = Directory
                            .GetFiles(engine.Inbox, "*.mp4", SearchOption.AllDirectories)
                            .FirstOrDefault(x => !known.Contains(x));
                        Assert(
                            file != null && new FileInfo(file).Length > 1000,
                            "recording saved: " + action
                        );
                        var library = new Library(root);
                        var shot = await engine.Import(library, file, true);
                        Assert(shot.Kind == "video" && shot.Width > 400, "recording import");
                        results.Add(
                            "PASS "
                                + action
                                + " records fixture and creates playable video history (microphone off, system audio on)"
                        );
                    }
                    File.WriteAllLines(Path.Combine(root, "recording-test-results.txt"), results);
                    Environment.ExitCode = 0;
                }
                catch (Exception error)
                {
                    File.WriteAllText(
                        Path.Combine(root, "recording-test-error.txt"),
                        error.ToString()
                    );
                    Environment.ExitCode = 1;
                }
                finally
                {
                    engine.Dispose();
                    w.Close();
                    app.Shutdown();
                }
            };
            app.Run(w);
        }

        public static void EngineSmoke(string root)
        {
            Directory.CreateDirectory(root);
            var app = new Application();
            var results = new List<string>();
            var lib = new Library(root);
            var engine = new CaptureEngine(root);
            var w = new Window
            {
                Title = "LumaShot capture test fixture",
                Width = 850,
                Height = 580,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new Image { Source = Demo.Image(0), Stretch = Stretch.Fill },
            };
            w.Loaded += async (s, e) =>
            {
                try
                {
                    await Task.Delay(1000);
                    w.Activate();
                    await CaptureClipboard.Set(
                        () =>
                            Clipboard.SetDataObject(
                                new DataObject(
                                    DataFormats.UnicodeText,
                                    "LumaShot clipboard preservation test"
                                ),
                                false
                            )
                    );
                    var task = engine.Run("ActiveWindow");
                    if (await Task.WhenAny(task, Task.Delay(35000)) != task)
                    {
                        if (engine.Busy)
                            engine.Running.Kill();
                        throw new Exception("Capture engine did not auto-close in 35 seconds");
                    }
                    await task;
                    string file = Directory
                        .EnumerateFiles(engine.Inbox, "*.png", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    Assert(file != null, "ShareX screenshot saved in inbox");
                    var x = await engine.Import(lib, file, false);
                    Assert(x != null && x.Width > 400, "capture import");
                    results.Add("PASS ShareX ActiveWindow -> inbox -> persistent library");
                    Assert(await engine.Import(lib, file, false) == null, "dedup import");
                    var reload = new Library(root);
                    Assert(await engine.Import(reload, file, false) == null, "dedup after restart");
                    results.Add("PASS import deduplication survives restart");
                    Assert(
                        Clipboard.ContainsText()
                            && Clipboard.GetText() == "LumaShot clipboard preservation test",
                        "engine must not copy unconditionally"
                    );
                    await CaptureClipboard.Copy(lib, x);
                    Assert(Clipboard.ContainsImage(), "capture clipboard image");
                    results.Add(
                        "PASS engine preserves clipboard; app copies image data compatible with Ctrl+V"
                    );
                    string video = Path.Combine(root, "sample.mp4");
                    var p = Video.Start(
                        "-y -f lavfi -i color=c=0x7561df:s=320x240:r=10 -t 1 -c:v libx264 -pix_fmt yuv420p \""
                            + video
                            + "\""
                    );
                    await Task.Run(() => p.WaitForExit());
                    Assert(p.ExitCode == 0, "video encode");
                    p.Dispose();
                    var rec = await engine.Import(lib, video, true);
                    Assert(rec.Kind == "video" && File.Exists(lib.Full(rec.Thumb)), "video import");
                    results.Add("PASS FFmpeg encoding and video history thumbnail");
                    string gif = Path.Combine(root, "sample.gif");
                    await Video.ConvertGif(video, gif);
                    Assert(
                        File.ReadAllBytes(gif).Take(3).SequenceEqual(new byte[] { 71, 73, 70 }),
                        "GIF format"
                    );
                    results.Add("PASS MP4 to GIF export");
                    File.WriteAllLines(Path.Combine(root, "engine-test-results.txt"), results);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(root, "engine-test-error.txt"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                finally
                {
                    engine.Dispose();
                    w.Close();
                    app.Shutdown();
                }
            };
            app.Run(w);
        }
    }
}
