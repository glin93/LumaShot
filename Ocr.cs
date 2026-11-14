using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumaShot {
 public static class LocalOcr {
  public static async Task<string> Read(Library lib,Shot item){
   string stem=Path.Combine(lib.Root,"ocr-"+Guid.NewGuid().ToString("N")),png=stem+".png",txt=stem+".txt";
   var img=Images.Load(lib.Full(item.File));double scale=Math.Min(1,2400.0/Math.Max(img.PixelWidth,img.PixelHeight));if(scale<1)img=new TransformedBitmap(img,new ScaleTransform(scale,scale));Images.Save(img,png);
   string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");string script=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LocalOcr.ps1");
   try{var p=new Process{StartInfo=new ProcessStartInfo(exe,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+script+"\" -ImagePath \""+png+"\" -OutputPath \""+txt+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}};
    p.Start();var error=p.StandardError.ReadToEndAsync();var output=p.StandardOutput.ReadToEndAsync();var wait=Task.Run(()=>p.WaitForExit());if(await Task.WhenAny(wait,Task.Delay(30000))!=wait){p.Kill();p.Dispose();throw new IOException("文字识别超时，请缩小截图后重试。");}await wait;string err=await error;await output;int exit=p.ExitCode;p.Dispose();if(exit!=0||!File.Exists(txt))throw new IOException("本地文字识别未完成。请确认 Windows 已安装所需语言。\n"+err);return File.ReadAllText(txt);
   }finally{if(File.Exists(png))File.Delete(png);if(File.Exists(txt))File.Delete(txt);}
  }
 }
}
