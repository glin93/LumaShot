// SPDX-License-Identifier: GPL-3.0-or-later
// Adapter for the pinned, unmodified ShareX 22.4 assemblies. Commands travel
// only through inherited anonymous pipes; no listening socket or public pipe.
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

internal static class CaptureHost
{
    const BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly sharex;
    static object dispatcher,
        priority;
    static MethodInfo post;
    static readonly object stateGate = new object();
    static int generation,
        activeOperation,
        cancelledOperation;
    static bool verify;
    static readonly System.Collections.Generic.List<WeakReference> selectors =
        new System.Collections.Generic.List<WeakReference>();

    static Type Type(string name)
    {
        return sharex.GetType("ShareX." + name, true);
    }

    static object Get(object target, string name)
    {
        var t = target as Type ?? target.GetType();
        var p = t.GetProperty(name, All);
        return p != null
            ? p.GetValue(target is Type ? null : target, null)
            : t.GetField(name, All).GetValue(target is Type ? null : target);
    }

    static void Set(object target, string name, object value)
    {
        var t = target as Type ?? target.GetType();
        var p = t.GetProperty(name, All);
        if (p != null)
            p.SetValue(target is Type ? null : target, value, null);
        else
            t.GetField(name, All).SetValue(target is Type ? null : target, value);
    }

    static object Call(object target, string name, params object[] args)
    {
        return (target as Type ?? target.GetType())
            .GetMethod(name, All)
            .Invoke(target is Type ? null : target, args);
    }

    static void Reply(string value)
    {
        Console.Out.WriteLine("@LUMA " + value);
        Console.Out.Flush();
    }

    static Task UI(Func<Task> action)
    {
        var done = new TaskCompletionSource<bool>();
        Action run = async () =>
        {
            try
            {
                await action();
                done.SetResult(true);
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        };
        post.Invoke(dispatcher, new object[] { run, priority });
        return done.Task;
    }

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            sharex = Assembly.LoadFrom(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShareX.dll")
            );
            if (args.Length < 2 || args.Length > 3 || !Path.IsPathRooted(args[0]))
                throw new ArgumentException("A private capture profile and parent are required.");
            verify = args.Length == 3 && args[2] == "--verify";
            var parent = Process.GetProcessById(int.Parse(args[1]));
            new Thread(() =>
            {
                try
                {
                    parent.WaitForExit();
                }
                catch { }
                Environment.Exit(0);
            })
            {
                IsBackground = true,
                Name = "LumaShot parent lifetime",
            }.Start();
            // Same startup sequence as upstream Program.Main, with a private profile
            // and no global single-instance IPC. The shipped ShareX files stay intact.
            var options = new[] { "-portable", "-multi", "-silent", "-nohotkeys" };
            Call(Type("ApplicationDiagnostics"), "Initialize");
            Set(Type("Program"), "_startTimer", Stopwatch.StartNew());
            Call(Type("ApplicationCommandLine"), "Initialize", (object)options);
            Call(Type("SystemOptions"), "UpdateSystemOptions");
            Call(Type("PersonalPathManager"), "Initialize");
            Set(Type("PersonalPathManager"), "CustomPersonalPath", args[0]);
            Set(Type("StartupOptions"), "MultiInstance", true);
            var reader = new Thread(Control)
            {
                IsBackground = true,
                Name = "LumaShot capture commands",
            };
            reader.Start();
            Call(Type("Program"), "Run", (object)options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
    }

    static void Control()
    {
        try
        {
            var started = Stopwatch.StartNew();
            while (!(bool)Get(Type("SingleInstanceCommandRouter"), "_hasBeenReady"))
            {
                if (started.Elapsed.TotalSeconds > 20)
                    throw new TimeoutException("Capture engine startup timed out.");
                Thread.Sleep(20);
            }
            var dispatchType = System.Type.GetType(
                "Avalonia.Threading.Dispatcher, Avalonia.Base",
                true
            );
            dispatcher = dispatchType.GetProperty("UIThread").GetValue(null, null);
            var priorityType = System.Type.GetType(
                "Avalonia.Threading.DispatcherPriority, Avalonia.Base",
                true
            );
            priority = Get(priorityType, "Normal");
            post = dispatchType.GetMethod("Post", new[] { typeof(Action), priorityType });
            UI(() =>
                {
                    Set(
                        Get(Get(Type("ApplicationLifecycle"), "_hotkeyHost"), "TrayIconService"),
                        "Visible",
                        false
                    );
                    return Task.FromResult(true);
                })
                .GetAwaiter()
                .GetResult();
            Reply("READY");
            string command;
            while ((command = Console.ReadLine()) != null)
            {
                if (command == "QUIT")
                    break;
                if (command == "CANCEL")
                {
                    int target;
                    lock (stateGate)
                    {
                        target = activeOperation;
                        if (target != 0)
                            cancelledOperation = target;
                    }
                    if (target != 0)
                    {
                        UI(() =>
                        {
                            CancelActiveSelector(false);
                            return Task.FromResult(true);
                        });
                    }
                    continue;
                }
                bool cancelFixture = verify && command == "VerifyCancel";
                string capture =
                    command == "RectangleRegion" || cancelFixture ? "CaptureRegion"
                    : command == "ActiveWindow" ? "CaptureActiveWindow"
                    : command == "PrintScreen" ? "CaptureFullscreen"
                    : null;
                if (capture == null)
                {
                    Reply("ERROR unsupported-command");
                    continue;
                }
                int operation;
                lock (stateGate)
                {
                    if (activeOperation != 0)
                    {
                        Reply("ERROR capture-busy");
                        continue;
                    }
                    operation = ++generation;
                    activeOperation = operation;
                    cancelledOperation = 0;
                }
                UI(async () =>
                {
                    try
                    {
                        var clock = Stopwatch.StartNew();
                        object settings = Call(Type("TaskSettings"), "GetDefaultTaskSettings");
                        object captureSettings = Get(settings, "CaptureSettings");
                        if (captureSettings != null)
                            Set(captureSettings, "ShowCursor", false);
                        var instance = Activator.CreateInstance(Type(capture));
                        Reply("DISPATCH " + clock.ElapsedMilliseconds);
                        if (capture == "CaptureRegion")
                            ObserveSelector(clock, operation, cancelFixture);
                        await (Task)
                            Type("CaptureBase")
                                .GetMethod("CaptureAsync", All)
                                .Invoke(instance, new[] { settings, (object)false });
                        while ((bool)Get(Type("TaskManager"), "IsBusy"))
                            await Task.Delay(10);
                        var tasks = (IList)Get(Type("TaskManager"), "Tasks");
                        bool failed = false;
                        for (int i = tasks.Count - 1; i >= 0; i--)
                        {
                            object task = tasks[i];
                            if (Get(task, "Status").ToString() == "Failed")
                                failed = true;
                            Call(Type("TaskManager"), "Remove", task);
                        }
                        Call(Get(Type("TaskManager"), "RecentManager"), "Clear");
                        Reply(
                            failed
                                ? "ERROR capture-save-failed"
                                : "DONE " + clock.ElapsedMilliseconds
                        );
                        ReclaimWhenIdle(operation);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                        Reply("ERROR capture-failed");
                    }
                    finally
                    {
                        lock (stateGate)
                        {
                            if (activeOperation == operation)
                                activeOperation = 0;
                        }
                    }
                });
            }
            UI(() =>
                {
                    CancelActiveSelector(true);
                    Call(Type("ApplicationLifecycle"), "Exit");
                    return Task.FromResult(true);
                })
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Reply("ERROR host-failed");
            Environment.Exit(1);
        }
    }

    static object FindRegionSelector()
    {
        var bootstrap = System.Type.GetType(
            "ShareX.AvaloniaUI.Integration.AvaloniaBootstrapper, ShareX.Avalonia",
            false
        );
        if (bootstrap == null)
            return null;
        var desktop = Get(bootstrap, "_desktopLifetime");
        if (desktop == null)
            return null;
        var windows = (IEnumerable)Get(desktop, "Windows");
        if (windows == null)
            return null;
        foreach (var window in windows)
            if (window != null && window.GetType().Name == "RegionCaptureWindow")
                return window;
        return null;
    }

    static void CancelActiveSelector(bool force)
    {
        try
        {
            var selector = FindRegionSelector();
            if (selector == null)
                return;
            if (!force)
            {
                bool regionActive = true;
                try
                {
                    regionActive = (bool)Get(selector, "_regionToolActive");
                }
                catch { }
                if (!regionActive)
                {
                    try
                    {
                        var workspace = Get(selector, "_editorWorkspace");
                        if (
                            workspace != null
                            && (bool)Call(workspace, "CancelActiveInteractionOrSelection")
                        )
                            return;
                    }
                    catch { }
                }
            }
            Call(selector, "CancelCapture");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    // Measure first visible selector without retaining any screenshot pixels.
    static async void ObserveSelector(Stopwatch clock, int operation, bool cancelFixture)
    {
        int pid = Process.GetCurrentProcess().Id;
        try
        {
            for (int i = 0; i < 400 && operation == generation; i++)
            {
                IntPtr found = IntPtr.Zero;
                EnumWindows(
                    (window, data) =>
                    {
                        uint owner;
                        GetWindowThreadProcessId(window, out owner);
                        if (owner == (uint)pid && IsWindowVisible(window))
                        {
                            found = window;
                            return false;
                        }
                        return true;
                    },
                    IntPtr.Zero
                );
                if (found != IntPtr.Zero)
                {
                    SetForegroundWindow(found);
                    Reply("VISIBLE " + clock.ElapsedMilliseconds);
                    bool shouldCancel;
                    lock (stateGate)
                        shouldCancel = cancelledOperation == operation;
                    if (shouldCancel)
                    {
                        await UI(() =>
                        {
                            CancelActiveSelector(true);
                            return Task.FromResult(true);
                        });
                        return;
                    }
                    await UI(() =>
                    {
                        var selector = FindRegionSelector();
                        if (selector != null)
                        {
                            try
                            {
                                Call(selector, "Activate");
                            }
                            catch { }
                        }
                        return Task.FromResult(true);
                    });
                    if (cancelFixture)
                    {
                        await Task.Delay(80);
                        await UI(() =>
                        {
                            var selector = FindRegionSelector();
                            if (selector != null)
                            {
                                selectors.Add(new WeakReference(selector));
                                Call(selector, "CancelCapture");
                            }
                            return Task.FromResult(true);
                        });
                        return;
                    }
                    return;
                }
                await Task.Delay(5);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    static async void ReclaimWhenIdle(int operation)
    {
        await Task.Delay(3000);
        if (operation != generation || (bool)Get(Type("TaskManager"), "IsBusy"))
            return;
        // Free transient managed pixel buffers after a burst, never trim working sets
        // or keep a full-screen bitmap alive just to make the next capture warm.
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        if (verify)
        {
            int alive = 0;
            foreach (var weak in selectors)
                if (weak.IsAlive)
                    alive++;
            Console.Error.WriteLine(
                "Idle managed MB="
                    + (GC.GetTotalMemory(false) / 1048576)
                    + "; retained closed selectors="
                    + alive
                    + "/"
                    + selectors.Count
            );
        }
    }

    delegate bool EnumCallback(IntPtr window, IntPtr data);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumCallback callback, IntPtr data);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr window);
}
