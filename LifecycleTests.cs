using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using F = System.Windows.Forms;

namespace LumaShot
{
    public static class LifecycleTests
    {
        // A separate child with the same host lifetime as ShareX: closing its
        // untitled selector leaves the hidden host alive until the wrapper closes it.
        public static void Fixture(string mode)
        {
            if (mode == "-LifecycleError")
            {
                Environment.ExitCode = 42;
                return;
            }
            var host = new F.Form { Text = "ShareX - Hotkey host", ShowInTaskbar = false };
            var selector = new F.Form
            {
                Text = "",
                Width = 360,
                Height = 180,
                ShowInTaskbar = false,
            };
            selector.Controls.Add(
                new F.Label { Text = "LumaShot lifecycle test fixture", Dock = F.DockStyle.Fill }
            );
            var timer = new F.Timer
            {
                Interval =
                    mode == "-LifecycleActive" ? 6500
                    : mode == "-LifecycleFast" ? 10
                    : 500,
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                selector.Close();
            };
            host.Shown += (s, e) =>
            {
                host.Hide();
                selector.Show();
                timer.Start();
            };
            F.Application.Run(host);
            timer.Dispose();
            selector.Dispose();
            host.Dispose();
        }

        static void Check(bool ok, string text)
        {
            if (!ok)
                throw new Exception(text);
        }

        public static void Run(string root)
        {
            Directory.CreateDirectory(root);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Startup += async (s, e) =>
            {
                var results = new List<string>();
                var engine = new CaptureEngine(root);
                engine.Exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var task = engine.Run("LifecycleCancel");
                        Check(
                            await Task.WhenAny(task, Task.Delay(12000)) == task,
                            "Cancel left child running"
                        );
                        await task;
                        Check(
                            !engine.Busy && engine.Running == null,
                            "Cancel did not release state"
                        );
                    }
                    results.Add(
                        "PASS three consecutive untitled-selector cancellations close the idle host and release capture state"
                    );
                    var active = engine.Run("LifecycleActive");
                    await Task.Delay(5500);
                    Check(
                        engine.Busy && !active.IsCompleted,
                        "Visible untitled selector was closed"
                    );
                    Check(
                        await Task.WhenAny(active, Task.Delay(10000)) == active,
                        "Closed selector did not recover"
                    );
                    await active;
                    results.Add(
                        "PASS untitled visible selector stays open; closing it then releases the host"
                    );
                    var fast = engine.Run("LifecycleFast");
                    Check(
                        await Task.WhenAny(fast, Task.Delay(30000)) == fast,
                        "Fast cancellation did not recover"
                    );
                    await fast;
                    results.Add(
                        "PASS cancellation before the first window poll recovers after startup grace"
                    );
                    bool failed = false;
                    try
                    {
                        await engine.Run("LifecycleError");
                    }
                    catch (IOException)
                    {
                        failed = true;
                    }
                    Check(
                        failed && !engine.Busy && engine.Running == null,
                        "Failed child retained capture state"
                    );
                    await engine.Run("LifecycleCancel");
                    results.Add(
                        "PASS nonzero engine exit releases state and the next task succeeds"
                    );
                    File.WriteAllLines(Path.Combine(root, "lifecycle-test-results.txt"), results);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(
                        Path.Combine(root, "lifecycle-test-error.txt"),
                        ex.ToString()
                    );
                    Environment.ExitCode = 1;
                    if (engine.Busy)
                        engine.Running.Kill();
                }
                finally
                {
                    app.Shutdown();
                }
            };
            app.Run();
        }
    }
}
