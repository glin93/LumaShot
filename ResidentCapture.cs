using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace LumaShot
{
    // One private child per application. A warm process is not an active capture.
    internal sealed class ResidentCapture : IDisposable
    {
        readonly object gate = new object();
        readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
        TaskCompletionSource<bool> pending;
        bool disposed;
        public readonly Process Process;
        internal long LastDispatchMilliseconds,
            LastCaptureMilliseconds,
            LastVisibleMilliseconds;
        Stopwatch request;
        internal string Diagnostic = "";

        internal ResidentCapture(string executable, string profile, bool verify = false)
        {
            Process = new Process
            {
                StartInfo = new ProcessStartInfo(
                    executable,
                    "\""
                        + profile
                        + "\" "
                        + System.Diagnostics.Process.GetCurrentProcess().Id
                        + (verify ? " --verify" : "")
                )
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
                EnableRaisingEvents = true,
            };
            Process.OutputDataReceived += (s, e) => Receive(e.Data);
            Process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (gate)
                    {
                        Diagnostic = Diagnostic + e.Data + Environment.NewLine;
                        if (Diagnostic.Length > 4096)
                            Diagnostic = Diagnostic.Substring(Diagnostic.Length - 4096);
                    }
                }
            };
            Process.Exited += (s, e) => Fail(new IOException("截图引擎已退出，请重新截图。"));
            try
            {
                Process.Start();
                Process.BeginOutputReadLine();
                Process.BeginErrorReadLine();
            }
            catch
            {
                Process.Dispose();
                throw;
            }
        }

        internal bool Alive
        {
            get
            {
                try
                {
                    return !disposed && !Process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal async Task Ready()
        {
            if (await Task.WhenAny(ready.Task, Task.Delay(20000)) != ready.Task)
            {
                Dispose();
                throw new IOException("截图引擎启动超时，请重试。");
            }
            await ready.Task;
        }

        internal async Task Capture(string action)
        {
            await Ready();
            TaskCompletionSource<bool> completion;
            lock (gate)
            {
                if (disposed || !Alive)
                    throw new IOException("截图引擎已退出，请重试。");
                if (pending != null)
                    throw new InvalidOperationException("请先完成或取消当前截图。");
                completion = pending = new TaskCompletionSource<bool>();
                request = Stopwatch.StartNew();
            }
            try
            {
                Process.StandardInput.WriteLine(action);
                Process.StandardInput.Flush();
                await completion.Task;
            }
            finally
            {
                lock (gate)
                {
                    if (ReferenceEquals(pending, completion))
                        pending = null;
                }
            }
        }

        void Receive(string line)
        {
            if (line == null || !line.StartsWith("@LUMA ", StringComparison.Ordinal))
                return;
            line = line.Substring(6);
            lock (gate)
            {
                if (line == "READY")
                    ready.TrySetResult(true);
                else if (line.StartsWith("DISPATCH "))
                {
                    LastDispatchMilliseconds = request == null ? 0 : request.ElapsedMilliseconds;
                }
                else if (line.StartsWith("VISIBLE "))
                {
                    long.TryParse(line.Substring(8), out LastVisibleMilliseconds);
                }
                else if (line.StartsWith("DONE "))
                {
                    long.TryParse(line.Substring(5), out LastCaptureMilliseconds);
                    if (pending != null)
                        pending.TrySetResult(true);
                }
                else if (line.StartsWith("ERROR "))
                {
                    var error = new IOException(
                        "截图没有完成，请重试。" + " (" + line.Substring(6) + ")"
                    );
                    if (pending != null)
                        pending.TrySetException(error);
                    else
                        ready.TrySetException(error);
                }
            }
        }

        void Fail(Exception error)
        {
            lock (gate)
            {
                ready.TrySetException(error);
                if (pending != null)
                    pending.TrySetException(error);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
            }
            Fail(new ObjectDisposedException("Capture engine"));
            try
            {
                Process.StandardInput.WriteLine("QUIT");
                Process.StandardInput.Close();
            }
            catch { }
            // Normal shutdown first; the bounded fallback targets only this owned child.
            Task.Run(() =>
            {
                try
                {
                    if (!Process.WaitForExit(3000))
                        Process.Kill();
                    Process.WaitForExit(2000);
                }
                catch { }
                finally
                {
                    Process.Dispose();
                }
            });
        }
    }
}
