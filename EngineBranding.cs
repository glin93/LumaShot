using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace LumaShot {
 // Change only the window presentation of the child we launched. The upstream
 // executable, licenses, About information and copyright notices stay intact.
 sealed class EngineBranding : IDisposable {
  readonly Process process;readonly Icon icon,small;readonly DispatcherTimer timer;readonly HashSet<IntPtr> seen=new HashSet<IntPtr>();
  public EngineBranding(Process child){process=child;icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);small=new Icon(icon,16,16);timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(s,e)=>Update();timer.Start();}
  void Update(){if(process.HasExited){timer.Stop();return;}EnumWindows((window,data)=>{uint pid;GetWindowThreadProcessId(window,out pid);if(pid!=(uint)process.Id||!IsWindowVisible(window)||!seen.Add(window))return true;IntPtr result;SendMessageTimeout(window,0x80,new IntPtr(1),icon.Handle,2,100,out result);SendMessageTimeout(window,0x80,IntPtr.Zero,small.Handle,2,100,out result);var title=new StringBuilder(512);GetWindowText(window,title,title.Capacity);string caption=title.ToString();if(caption.StartsWith("ShareX - ",StringComparison.Ordinal)&&(caption.IndexOf("editor",StringComparison.OrdinalIgnoreCase)>=0||caption.Contains("编辑器")))SendTextMessage(window,0xC,IntPtr.Zero,"LumaShot · "+caption.Substring(9),2,100,out result);return true;},IntPtr.Zero);}
  public void Dispose(){timer.Stop();small.Dispose();icon.Dispose();}
  delegate bool EnumCallback(IntPtr window,IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll",EntryPoint="SendMessageTimeoutW",CharSet=CharSet.Unicode)] static extern IntPtr SendTextMessage(IntPtr window,uint message,IntPtr wp,string text,uint flags,uint timeout,out IntPtr result);
 }
}
