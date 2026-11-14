using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Text;
using System.Runtime.InteropServices;

namespace LumaShot {
 public class CaptureEngine {
  public string Exe,Folder,Inbox; public Process Running;readonly Preferences preferences;readonly string config;
  public CaptureEngine(string root,Preferences options=null) {
   preferences=options??new Preferences(root);
   Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"engine");Exe=Path.Combine(Folder,"ShareX.exe");Inbox=Path.Combine(root,"inbox");Directory.CreateDirectory(Inbox);
   string settings=Path.Combine(Folder,"ShareX");Directory.CreateDirectory(settings);
   // Dedicated portable engine; never touches an installed ShareX profile.
   config=Path.Combine(settings,"ApplicationConfig.json");
  }
  void Configure(int duration){
   var screen=System.Windows.Forms.SystemInformation.VirtualScreen;
   var settings=new {ShowStartScreen=false,ShowIconPackPrompt=false,FirstTimeMinimizeToTray=false,ShowTray=true,SilentRun=true,AutoCheckUpdate=false,DisableUpload=true,DisableHotkeys=true,UseAlternativeClipboardCopyImage=true,UseCustomScreenshotsPath=true,CustomScreenshotsPath=Inbox,SaveImageSubFolderPattern="",SaveImageSubFolderPatternWindow="",HistorySaveTasks=true,
    DefaultTaskSettings=new {AfterCaptureJob="SaveImageToFile",AfterUploadJob="None",GeneralSettings=new {PlaySoundAfterCapture=false,PlaySoundAfterUpload=false,ShowToastNotificationAfterTaskCompleted=false},
     CaptureSettings=new {ScreenRecordUseNative=true,ScreenRecordShowBar=duration==0,ScreenRecordFixedDuration=duration>0,ScreenRecordDuration=duration,ScreenRecordFPS=30,ScreenRecordRequireHardwareEncoder=false,ScreenRecordSystemAudio=preferences.SystemAudio,ScreenRecordMicrophone=preferences.Microphone,ScreenRecordCamera=false,CaptureCustomRegion=string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0}, {1}, {2}, {3}",screen.X,screen.Y,screen.Width,screen.Height)},
     UploadSettings=new {NameFormatPattern="Luma_%y-%mo-%d_%h-%mi-%s_%ra{6}",NameFormatPatternActiveWindow="Luma_%y-%mo-%d_%h-%mi-%s_%ra{6}"}}};
   File.WriteAllText(config,Backend.Json().Serialize(settings),Encoding.UTF8);
  }
  void RememberRecordingAudio(bool initialMic,bool initialSystem){
   var app=Backend.Json().Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(config));
   var task=(System.Collections.Generic.Dictionary<string,object>)app["DefaultTaskSettings"];var capture=(System.Collections.Generic.Dictionary<string,object>)task["CaptureSettings"];
   bool mic=Convert.ToBoolean(capture["ScreenRecordMicrophone"]),system=Convert.ToBoolean(capture["ScreenRecordSystemAudio"]);
   // Only changes made in the recording bar override settings updated during a recording.
   if(mic!=initialMic)preferences.Values["recording.microphone"]=mic?"true":"false";
   if(system!=initialSystem)preferences.Values["recording.systemAudio"]=system?"true":"false";
   if(mic!=initialMic||system!=initialSystem)preferences.Save();
  }
  public bool Busy { get {return Running!=null&&!Running.HasExited;} }
  public async Task Run(string action,string file=null,int duration=0) {
   if(Busy)throw new InvalidOperationException("请先完成或取消当前的截图 / 编辑任务。");
   if(!File.Exists(Exe))throw new FileNotFoundException("找不到 ShareX 引擎，请保留完整的 engine 文件夹。");
   bool mic=preferences.Microphone,system=preferences.SystemAudio;Configure(duration);
   var si=new ProcessStartInfo(Exe,"-portable -multi -silent -nohotkeys -autoclose -"+action+(file==null?"":" \""+file+"\"")){UseShellExecute=false,WorkingDirectory=Folder};
   Running=Process.Start(si);using(var branding=new EngineBranding(Running)){if(action=="ImageEditor")await WaitEditorExit(Running);else await Task.Run(()=>Running.WaitForExit());}
   int exit=Running.ExitCode;Running.Dispose();Running=null;if(exit!=0)throw new IOException("截图引擎退出，代码："+exit+"。可以重试，或检查 engine/ShareX/Logs。");
   if(action.StartsWith("ScreenRecorder",StringComparison.Ordinal)&&duration==0)RememberRecordingAudio(mic,system);
  }
  // ShareX 22.4 leaves its host running when the standalone editor is cancelled
  // or saves directly. Close only this dedicated child's idle host, via its
  // normal shutdown message, after all editor/dialog windows have disappeared.
  async Task WaitEditorExit(Process process){
   bool seen=false;int idle=0;TimeSpan cpu=process.TotalProcessorTime;
   while(!process.HasExited){
    bool visible=false;IntPtr host=IntPtr.Zero;
    EnumWindows((window,data)=>{uint pid;GetWindowThreadProcessId(window,out pid);if(pid!=(uint)process.Id)return true;var title=new StringBuilder(256);GetWindowText(window,title,title.Capacity);if(title.ToString()=="ShareX - Hotkey host")host=window;else if(IsWindowVisible(window)&&title.Length>0)visible=true;return true;},IntPtr.Zero);
    if(visible){seen=true;idle=0;}else if(seen){var next=process.TotalProcessorTime;idle=(next-cpu).TotalMilliseconds<20?idle+1:0;cpu=next;if(idle>=10&&host!=IntPtr.Zero){PostMessage(host,0x10,IntPtr.Zero,IntPtr.Zero);await Task.Run(()=>process.WaitForExit());return;}}
    await Task.Delay(500);
   }
  }
  delegate bool EnumWindowCallback(IntPtr window,IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowCallback callback,IntPtr data);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
  public async Task<Shot> Import(Library lib,string file,bool manual) {
   var fi=new FileInfo(file);if(!fi.Exists||fi.Length==0)return null;
   string key=fi.FullName+"|"+fi.Length+"|"+fi.LastWriteTimeUtc.Ticks;
   if(!manual&&lib.Items.Any(x=>x.SourceKey==key))return null;
   string ext=fi.Extension.ToLowerInvariant();Shot shot;
   if(ext==".mp4"||ext==".webm"||ext==".avi"){
    string temp=Path.Combine(lib.Root,"thumb-"+Guid.NewGuid().ToString("N")+".png");
    var p=Video.Start("-y -i \""+file+"\" -frames:v 1 \""+temp+"\"");await Task.Run(()=>p.WaitForExit());int result=p.ExitCode;p.Dispose();if(result!=0||!File.Exists(temp))throw new IOException("无法读取视频缩略图");
    BitmapSource image=Images.Load(temp);File.Delete(temp);shot=lib.Add(image,Path.GetFileNameWithoutExtension(file),"video");shot.File="captures/"+shot.Id+ext;File.Copy(file,lib.Full(shot.File));shot.Original=shot.File;
   }else if(new[]{".png",".jpg",".jpeg",".bmp",".gif"}.Contains(ext)){
    shot=lib.Add(Images.Load(file),Path.GetFileNameWithoutExtension(file),ext==".gif"?"gif":"image");
    if(ext==".gif"){shot.File="captures/"+shot.Id+".gif";File.Copy(file,lib.Full(shot.File));shot.Original=shot.File;}
   }else return null;
   if(!manual)shot.Created=fi.LastWriteTime.ToString("o");shot.SourceKey=key;lib.Save();return shot;
  }
 }
}
