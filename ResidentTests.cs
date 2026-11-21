using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LumaShot
{
    public static class ResidentTests
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        static async Task Bounded(Task task)
        {
            Check(await Task.WhenAny(task, Task.Delay(15000)) == task, "Capture task timed out");
            await task;
        }

        static string Memory(Process process)
        {
            process.Refresh();
            return "private="
                + (process.PrivateMemorySize64 / 1048576)
                + " MB, working="
                + (process.WorkingSet64 / 1048576)
                + " MB";
        }

        public static void Run(string root)
        {
            Directory.CreateDirectory(root);
            var app = new Application();
            var window = new Window
            {
                Title = "LumaShot resident capture test",
                Width = 720,
                Height = 480,
                Background = Brushes.AliceBlue,
                Content = new TextBlock
                {
                    Text = "Resident capture fixture · 连续截图验证",
                    FontSize = 25,
                    Margin = new Thickness(32),
                },
            };
            var engine = new CaptureEngine(root) { VerifyResident = true };
            var results = new List<string>();
            window.Loaded += async (s, e) =>
            {
                try
                {
                    var clock = Stopwatch.StartNew();
                    await engine.WarmUp();
                    Check(engine.Resident != null && engine.Resident.Alive, "Warmup failed");
                    var process = engine.Resident.Process;
                    int pid = process.Id;
                    results.Add(
                        "Warm startup: " + clock.ElapsedMilliseconds + " ms; " + Memory(process)
                    );
                    Check(!engine.Busy, "Warm idle engine reported busy");
                    for (int i = 0; i < 4; i++)
                    {
                        await Bounded(engine.Run("VerifyCancel"));
                        Check(!engine.Busy && engine.Running == null, "Cancel retained busy state");
                        Check(engine.Resident.Process.Id == pid, "Cancel restarted engine");
                        Check(
                            engine.Resident.LastVisibleMilliseconds > 0,
                            "Selector never became visible"
                        );
                        results.Add(
                            "PASS region cancel "
                                + (i + 1)
                                + ": selector visible in "
                                + engine.Resident.LastVisibleMilliseconds
                                + " ms; task completed in "
                                + engine.Resident.LastCaptureMilliseconds
                                + " ms"
                        );
                    }
                    int beforeEsc = Directory
                        .GetFiles(engine.Inbox, "*.png", SearchOption.AllDirectories)
                        .Length;
                    var escCapture = engine.Run("RectangleRegion");
                    await Task.Delay(350);
                    engine.CancelActiveCapture();
                    await Bounded(escCapture);
                    Check(
                        !engine.Busy
                            && engine.Running == null
                            && engine.Resident.Process.Id == pid
                            && Directory
                                .GetFiles(engine.Inbox, "*.png", SearchOption.AllDirectories)
                                .Length == beforeEsc,
                        "Esc cancel pipe did not cleanly close region selector"
                    );
                    results.Add(
                        "PASS ESC/CANCEL command cleanly cancels active RectangleRegion without restarting resident host"
                    );
                    int beforeBurst = Directory
                        .GetFiles(engine.Inbox, "*.png", SearchOption.AllDirectories)
                        .Length;
                    for (int i = 0; i < 20; i++)
                    {
                        window.Activate();
                        await Bounded(engine.Run(i % 5 == 4 ? "PrintScreen" : "ActiveWindow"));
                        Check(
                            !engine.Busy && engine.Resident.Process.Id == pid,
                            "Capture restarted or retained busy state"
                        );
                        if (i == 0 || i % 5 == 4)
                            results.Add(
                                "Capture "
                                    + (i + 1)
                                    + ": dispatch="
                                    + engine.Resident.LastDispatchMilliseconds
                                    + " ms, save="
                                    + engine.Resident.LastCaptureMilliseconds
                                    + " ms; "
                                    + Memory(process)
                            );
                    }
                    Check(
                        Directory
                            .GetFiles(engine.Inbox, "*.png", SearchOption.AllDirectories)
                            .Length
                            == beforeBurst + 20,
                        "Missing or duplicated captures"
                    );
                    await Task.Delay(4500);
                    results.Add("PASS 20 captures saved by one process; idle " + Memory(process));
                    for (int i = 0; i < 10; i++)
                    {
                        await Bounded(engine.Run("VerifyCancel"));
                        window.Activate();
                        await Bounded(engine.Run("ActiveWindow"));
                    }
                    await Task.Delay(4500);
                    results.Add(
                        "PASS second burst (10 cancellations + 10 captures), same process; idle "
                            + Memory(process)
                    );
                    results.Add(engine.Resident.Diagnostic);
                    await Task.Delay(22000);
                    Check(
                        engine.Resident != null && engine.Resident.Alive && !engine.Busy,
                        "Idle replacement was not ready"
                    );
                    Check(
                        engine.Resident.Process.Id != pid,
                        "Oversized idle host was not recycled"
                    );
                    process = engine.Resident.Process;
                    pid = process.Id;
                    results.Add("PASS idle replacement already warm; " + Memory(process));
                    Check(
                        process.PrivateMemorySize64 < 192 * 1048576L,
                        "Idle replacement exceeded memory budget"
                    );
                    window.Activate();
                    await Bounded(engine.Run("ActiveWindow"));
                    results.Add(
                        "PASS next capture dispatch after recycle: "
                            + engine.Resident.LastDispatchMilliseconds
                            + " ms"
                    );
                    process.Refresh();
                    var cpu = process.TotalProcessorTime;
                    await Task.Delay(2000);
                    process.Refresh();
                    results.Add(
                        "Idle CPU time over 2 s: "
                            + (process.TotalProcessorTime - cpu).TotalMilliseconds
                            + " ms"
                    );
                    var active = engine.Run("RectangleRegion");
                    await Task.Delay(350);
                    bool rejected = false;
                    try
                    {
                        await engine.Run("PrintScreen");
                    }
                    catch (InvalidOperationException)
                    {
                        rejected = true;
                    }
                    Check(rejected, "Concurrent capture accepted");
                    process.Kill();
                    bool failed = false;
                    try
                    {
                        await Bounded(active);
                    }
                    catch (IOException)
                    {
                        failed = true;
                    }
                    Check(
                        failed && !engine.Busy && engine.Running == null,
                        "Crash retained busy state"
                    );
                    window.Activate();
                    await Bounded(engine.Run("ActiveWindow"));
                    Check(engine.Resident.Process.Id != pid, "Dead process reused");
                    results.Add(
                        "PASS concurrent capture rejected; active-process crash releases state and next capture succeeds"
                    );
                    var last = engine.Resident.Process;
                    int lastPid = last.Id;
                    engine.Dispose();
                    await Task.Delay(4000);
                    bool gone = false;
                    try
                    {
                        using (var check = Process.GetProcessById(lastPid))
                        {
                            gone = check.HasExited;
                        }
                    }
                    catch (ArgumentException)
                    {
                        gone = true;
                    }
                    Check(gone, "Disposed engine remained running");
                    results.Add("PASS application shutdown removes resident child");
                    File.WriteAllLines(Path.Combine(root, "resident-test-results.txt"), results);
                }
                catch (Exception ex)
                {
                    File.WriteAllLines(Path.Combine(root, "resident-test-partial.txt"), results);
                    File.WriteAllText(
                        Path.Combine(root, "resident-test-error.txt"),
                        ex.ToString()
                            + Environment.NewLine
                            + (engine.Resident == null ? "" : engine.Resident.Diagnostic)
                    );
                    Environment.ExitCode = 1;
                }
                finally
                {
                    engine.Dispose();
                    window.Close();
                }
            };
            app.Run(window);
        }
    }
}
