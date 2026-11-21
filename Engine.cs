using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumaShot
{
    public class CaptureEngine : IDisposable
    {
        ResidentCapture resident,
            standby;
        bool active,
            disposed,
            warmEnabled;
        readonly string residentProfile;
        int slot,
            operation;
        internal ResidentCapture Resident
        {
            get { return resident; }
        }
        internal bool VerifyResident;
        public string Exe,
            Folder,
            Inbox;
        public Process Running;
        readonly Preferences preferences;
        readonly string config;

        public CaptureEngine(string root, Preferences options = null)
        {
            preferences = options ?? new Preferences(root);
            Folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine");
            Exe = Path.Combine(Folder, "ShareX.exe");
            Inbox = Path.Combine(root, "inbox");
            Directory.CreateDirectory(Inbox);
            string settings = Path.Combine(Folder, "ShareX");
            Directory.CreateDirectory(settings);
            // Dedicated portable engine; never touches an installed ShareX profile.
            config = Path.Combine(settings, "ApplicationConfig.json");
            residentProfile = Path.Combine(root, "capture-engine");
        }

        void Configure(int duration, string destination = null)
        {
            var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
            var settings = new
            {
                ShowStartScreen = false,
                ShowIconPackPrompt = false,
                FirstTimeMinimizeToTray = false,
                ShowTray = true,
                SilentRun = true,
                AutoCheckUpdate = false,
                DisableUpload = true,
                DisableHotkeys = true,
                UseAlternativeClipboardCopyImage = true,
                UseCustomScreenshotsPath = true,
                CustomScreenshotsPath = Inbox,
                SaveImageSubFolderPattern = "",
                SaveImageSubFolderPatternWindow = "",
                HistorySaveTasks = destination == null,
                RecentTasksSave = false,
                SaveSettingsAfterTaskCompleted = false,
                DefaultTaskSettings = new
                {
                    AfterCaptureJob = "SaveImageToFile",
                    AfterUploadJob = "None",
                    GeneralSettings = new
                    {
                        PlaySoundAfterCapture = false,
                        PlaySoundAfterUpload = false,
                        ShowToastNotificationAfterTaskCompleted = false,
                    },
                    CaptureSettings = new
                    {
                        ShowCursor = false,
                        ScreenRecordUseNative = true,
                        ScreenRecordShowCursor = true,
                        ScreenRecordShowBar = duration == 0,
                        ScreenRecordFixedDuration = duration > 0,
                        ScreenRecordDuration = duration,
                        ScreenRecordFPS = 30,
                        ScreenRecordRequireHardwareEncoder = false,
                        ScreenRecordSystemAudio = preferences.SystemAudio,
                        ScreenRecordMicrophone = preferences.Microphone,
                        ScreenRecordCamera = false,
                        CaptureCustomRegion = string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "{0}, {1}, {2}, {3}",
                            screen.X,
                            screen.Y,
                            screen.Width,
                            screen.Height
                        ),
                    },
                    UploadSettings = new
                    {
                        NameFormatPattern = "Luma_%y-%mo-%d_%h-%mi-%s_%ra{6}",
                        NameFormatPatternActiveWindow = "Luma_%y-%mo-%d_%h-%mi-%s_%ra{6}",
                    },
                },
            };
            string path = destination ?? config;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Backend.Json().Serialize(settings), Encoding.UTF8);
        }

        void RememberRecordingAudio(bool initialMic, bool initialSystem)
        {
            var app = Backend
                .Json()
                .Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                    File.ReadAllText(config)
                );
            var task = (System.Collections.Generic.Dictionary<string, object>)
                app["DefaultTaskSettings"];
            var capture = (System.Collections.Generic.Dictionary<string, object>)
                task["CaptureSettings"];
            bool mic = Convert.ToBoolean(capture["ScreenRecordMicrophone"]),
                system = Convert.ToBoolean(capture["ScreenRecordSystemAudio"]);
            // Only changes made in the recording bar override settings updated during a recording.
            if (mic != initialMic)
                preferences.Values["recording.microphone"] = mic ? "true" : "false";
            if (system != initialSystem)
                preferences.Values["recording.systemAudio"] = system ? "true" : "false";
            if (mic != initialMic || system != initialSystem)
                preferences.Save();
        }

        bool SupportsResident
        {
            get
            {
                return Path.GetFileName(Exe) == "ShareX.exe"
                    && File.Exists(Path.Combine(Folder, "LumaShot.Capture.exe"));
            }
        }

        public async Task WarmUp()
        {
            warmEnabled = true;
            try
            {
                await EnsureResident();
            }
            catch { }
        }

        ResidentCapture CreateResident()
        {
            string profile = Path.Combine(residentProfile, (slot++ % 2) == 0 ? "a" : "b");
            Configure(0, Path.Combine(profile, "ApplicationConfig.json"));
            return new ResidentCapture(
                Path.Combine(Folder, "LumaShot.Capture.exe"),
                profile,
                VerifyResident
            );
        }

        async Task EnsureResident()
        {
            if (disposed)
                throw new ObjectDisposedException("CaptureEngine");
            if (!SupportsResident)
                return;
            if (resident == null || !resident.Alive)
            {
                if (resident != null)
                    resident.Dispose();
                resident = CreateResident();
            }
            await resident.Ready();
        }

        public void Dispose()
        {
            disposed = true;
            operation++;
            if (standby != null)
            {
                standby.Dispose();
                standby = null;
            }
            if (resident != null)
            {
                resident.Dispose();
                resident = null;
            }
        }

        // Some native renderer allocations outlive a closed ShareX selector. Recycle
        // only after a quiet period and only above the private-byte budget. Prepare
        // the replacement first so captures can continue using the current host.
        async void ReclaimIdleHost(int version, ResidentCapture previous)
        {
            try
            {
                await Task.Delay(20000);
                if (
                    disposed
                    || active
                    || operation != version
                    || resident != previous
                    || !previous.Alive
                    || standby != null
                )
                    return;
                previous.Process.Refresh();
                if (previous.Process.PrivateMemorySize64 <= 192 * 1048576L)
                    return;
                var replacement = CreateResident();
                standby = replacement;
                try
                {
                    await replacement.Ready();
                    if (active || disposed || resident != previous)
                    {
                        replacement.Dispose();
                        return;
                    }
                    resident = replacement;
                    previous.Dispose();
                }
                finally
                {
                    if (ReferenceEquals(standby, replacement))
                        standby = null;
                }
            }
            catch
            {
                if (standby != null)
                {
                    standby.Dispose();
                    standby = null;
                }
            }
        }

        public bool Busy
        {
            get { return active || (Running != null && !Running.HasExited); }
        }

        public void CancelActiveCapture()
        {
            var host = resident;
            if (host != null && host.Alive)
                host.Cancel();
        }

        public async Task Run(string action, string file = null, int duration = 0)
        {
            if (Busy)
                throw new InvalidOperationException("请先完成或取消当前的截图 / 编辑任务。");
            if (disposed)
                throw new ObjectDisposedException("CaptureEngine");
            if (!File.Exists(Exe))
                throw new FileNotFoundException("找不到 ShareX 引擎，请保留完整的 engine 文件夹。");
            active = true;
            int version = ++operation;
            try
            {
                if (
                    SupportsResident
                    && file == null
                    && (
                        action == "RectangleRegion"
                        || action == "ActiveWindow"
                        || action == "PrintScreen"
                        || (VerifyResident && action == "VerifyCancel")
                    )
                )
                {
                    await EnsureResident();
                    Running = resident.Process;
                    try
                    {
                        Native.AllowSetForegroundWindow(Running.Id);
                        using (var branding = new EngineBranding(Running))
                            await resident.Capture(action);
                    }
                    finally
                    {
                        Running = null;
                    }
                }
                else
                {
                    if (resident != null)
                    {
                        resident.Dispose();
                        resident = null;
                    }
                    await RunOnce(action, file, duration);
                }
            }
            finally
            {
                active = false;
                if (!disposed && resident != null && resident.Alive)
                    ReclaimIdleHost(version, resident);
            }
            if (warmEnabled && resident == null && !disposed)
                await WarmUp();
        }

        async Task RunOnce(string action, string file, int duration)
        {
            bool mic = preferences.Microphone,
                system = preferences.SystemAudio;
            Configure(duration);
            var si = new ProcessStartInfo(
                Exe,
                "-portable -multi -silent -nohotkeys -autoclose -"
                    + action
                    + (file == null ? "" : " \"" + file + "\"")
            )
            {
                UseShellExecute = false,
                WorkingDirectory = Folder,
            };
            var process = Process.Start(si);
            Native.AllowSetForegroundWindow(process.Id);
            Running = process;
            try
            {
                using (var branding = new EngineBranding(process))
                    await WaitTaskExit(process);
                int exit = process.ExitCode;
                if (exit != 0)
                    throw new IOException(
                        "截图引擎退出，代码：" + exit + "。可以重试，或检查 engine/ShareX/Logs。"
                    );
            }
            finally
            {
                if (ReferenceEquals(Running, process))
                    Running = null;
                process.Dispose();
            }
            if (action.StartsWith("ScreenRecorder", StringComparison.Ordinal) && duration == 0)
                RememberRecordingAudio(mic, system);
        }

        // ShareX 22.4 only auto-closes after an upload task completes. Cancelling a
        // region, recording setup or editor creates no such task. Observe ALL visible
        // windows (region selectors have no caption), then close this child's idle
        // host normally. Never time out a visible selector, editor or recording bar.
        internal async Task WaitTaskExit(Process process)
        {
            bool seen = false;
            int idle = 0;
            TimeSpan cpu = process.TotalProcessorTime;
            var age = Stopwatch.StartNew();
            bool closeRequested = false;
            while (!process.HasExited)
            {
                bool visible = false;
                IntPtr host = IntPtr.Zero;
                EnumWindows(
                    (window, data) =>
                    {
                        uint pid;
                        GetWindowThreadProcessId(window, out pid);
                        if (pid != (uint)process.Id)
                            return true;
                        var title = new StringBuilder(256);
                        GetWindowText(window, title, title.Capacity);
                        if (title.ToString() == "ShareX - Hotkey host")
                            host = window;
                        else if (IsWindowVisible(window))
                            visible = true;
                        return true;
                    },
                    IntPtr.Zero
                );
                if (process.HasExited)
                    break;
                var next = process.TotalProcessorTime;
                bool working = (next - cpu).TotalMilliseconds >= 20;
                cpu = next;
                if (visible)
                {
                    seen = true;
                    idle = 0;
                    closeRequested = false;
                }
                else if (!working && (seen || age.Elapsed.TotalSeconds >= 20))
                    idle++;
                else
                    idle = 0;
                if (idle >= 40 && host != IntPtr.Zero && !closeRequested)
                {
                    PostMessage(host, 0x10, IntPtr.Zero, IntPtr.Zero);
                    closeRequested = true;
                }
                await Task.Delay(100);
            }
        }

        delegate bool EnumWindowCallback(IntPtr window, IntPtr data);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowCallback callback, IntPtr data);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr window, uint message, IntPtr wp, IntPtr lp);

        public async Task<Shot> Import(Library lib, string file, bool manual)
        {
            var fi = new FileInfo(file);
            if (!fi.Exists || fi.Length == 0)
                return null;
            string key = fi.FullName + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            if (!manual)
            {
                var existing = lib.Items.FirstOrDefault(x => x.SourceKey == key);
                if (existing != null)
                {
                    StorageMaintenance.RemoveImportedInboxFile(lib, Inbox, fi.FullName, existing);
                    return null;
                }
            }
            string ext = fi.Extension.ToLowerInvariant();
            BitmapSource preview;
            string kind;
            if (ext == ".mp4" || ext == ".webm" || ext == ".avi")
            {
                kind = "video";
                string temp = Path.Combine(
                    lib.Root,
                    "thumb-" + Guid.NewGuid().ToString("N") + ".png"
                );
                try
                {
                    using (
                        var process = Video.Start(
                            "-y -i \"" + file + "\" -frames:v 1 \"" + temp + "\""
                        )
                    )
                    {
                        await Task.Run(() => process.WaitForExit());
                        if (process.ExitCode != 0 || !File.Exists(temp))
                            throw new IOException("无法读取视频缩略图");
                    }
                    preview = Images.Load(temp);
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
            }
            else if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(ext))
            {
                kind = ext == ".gif" ? "gif" : "image";
                preview = Images.Load(file);
            }
            else
                return null;
            var shot = lib.ImportMedia(
                file,
                preview,
                kind,
                key,
                (manual ? DateTime.Now : fi.LastWriteTime).ToString("o")
            );
            if (!manual)
                StorageMaintenance.RemoveImportedInboxFile(lib, Inbox, fi.FullName, shot);
            return shot;
        }
    }
}
