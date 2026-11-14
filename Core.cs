using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Threading.Tasks;
using D = System.Drawing;
using F = System.Windows.Forms;

namespace LumaShot {
 [DataContract] public class Shot {
  [DataMember] public string Id;
  [DataMember] public string ParentId="";
  [DataMember] public string Title;
  [DataMember] public string Tags="";
  [DataMember] public string Notes="";
  [DataMember] public string Created;
  [DataMember] public string Kind="image";
  [DataMember] public string File;
  [DataMember] public string Original;
  [DataMember] public string Thumb;
  [DataMember] public bool Star;
  [DataMember] public bool Deleted;
  [DataMember] public int Width;
  [DataMember] public int Height;
  [DataMember] public string SourceKey;
  [DataMember] public string OcrText="";
  [DataMember] public string AiHistory="";
  [DataMember] public bool OcrDone;
  public DateTime Date { get { DateTime d; return DateTime.TryParse(Created,out d)?d:DateTime.Now; } }
 }
 public class Library {
  public string Root; public List<Shot> Items=new List<Shot>(); public string Notice="";
  public Library(string root) { Root=Path.GetFullPath(root); Directory.CreateDirectory(Root); Directory.CreateDirectory(Path.Combine(Root,"captures")); Directory.CreateDirectory(Path.Combine(Root,"thumbs")); Load(); }
  public string Full(string relative) { string p=Path.GetFullPath(Path.Combine(Root,relative??"")); if(!p.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("无效的历史文件路径"); return p; }
  void Load() {
   string path=Path.Combine(Root,"library.json"); if(!System.IO.File.Exists(path)) return;
   try { Items=Read(path); } catch { try { Items=Read(path+".bak"); Notice="历史索引已从备份恢复"; } catch { throw new IOException("历史索引与备份均无法读取。原始截图仍在 captures 文件夹；为保护记录，已停止写入。"); } }
  }
  static List<Shot> Read(string path) { using(var s=System.IO.File.OpenRead(path)) return (List<Shot>)new DataContractJsonSerializer(typeof(List<Shot>)).ReadObject(s); }
  public void Save() {
   string p=Path.Combine(Root,"library.json"),tmp=p+".tmp";
   using(var s=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){new DataContractJsonSerializer(typeof(List<Shot>)).WriteObject(s,Items);s.Flush(true);}
   if(System.IO.File.Exists(p)) System.IO.File.Replace(tmp,p,p+".bak"); else System.IO.File.Move(tmp,p);
  }
  public Shot Add(BitmapSource image,string title,string kind) {
   var id=Guid.NewGuid().ToString("N"); var x=new Shot{Id=id,Title=title,Created=DateTime.Now.ToString("o"),Kind=kind,File="captures/"+id+".png",Original="captures/"+id+".png",Thumb="thumbs/"+id+".png",Width=image.PixelWidth,Height=image.PixelHeight};
   Images.Save(image,Full(x.File)); Images.Save(Images.Thumbnail(image,480),Full(x.Thumb)); Items.Insert(0,x);Save();return x;
  }
  public Shot Edit(Shot item,BitmapSource image) {
   var edited=Add(image,item.Title+" · 编辑","image");edited.ParentId=item.Id;edited.Tags=item.Tags;Save();return edited;
  }
  public IEnumerable<Shot> Query(string filter,string search,bool oldest) {
   var q=Items.Where(x=>x.Deleted==(filter=="trash"));
   if(filter=="favorites")q=q.Where(x=>x.Star); if(filter=="image")q=q.Where(x=>x.Kind!="video");if(filter=="video")q=q.Where(x=>x.Kind=="video");
   if(!string.IsNullOrWhiteSpace(search)) foreach(var token in search.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){string t=token;q=q.Where(x=>((x.Title??"")+" "+x.Tags+" "+x.Notes+" "+x.OcrText+" "+x.Created).IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0);}
   return oldest?q.OrderBy(x=>x.Date):q.OrderByDescending(x=>x.Date);
  }
 }
 public static class Images {
  public static BitmapSource Load(string p,int decode=0) { var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;if(decode>0)b.DecodePixelWidth=decode; using(var s=System.IO.File.OpenRead(p)){b.StreamSource=s;b.EndInit();}b.Freeze();return b; }
  public static void Save(BitmapSource b,string p) { var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using(var s=System.IO.File.Create(p))e.Save(s); }
  public static BitmapSource Thumbnail(BitmapSource b,int w) { if(b.PixelWidth<=w)return b;var t=new TransformedBitmap(b,new ScaleTransform((double)w/b.PixelWidth,(double)w/b.PixelWidth));t.Freeze();return t; }
  public static BitmapSource Render(Visual v,int w,int h) { var r=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);r.Render(v);r.Freeze();return r; }
  public static BitmapSource Frame(BitmapSource source) {
   int pad=Math.Max(30,source.PixelWidth/18);var v=new DrawingVisual();using(var d=v.RenderOpen()){d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(211,205,255),Color.FromRgb(169,222,229),45),null,new Rect(0,0,source.PixelWidth+pad*2,source.PixelHeight+pad*2));d.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(38,20,25,45)),null,new Rect(pad+5,pad+8,source.PixelWidth,source.PixelHeight),12,12);d.PushClip(new RectangleGeometry(new Rect(pad,pad,source.PixelWidth,source.PixelHeight),10,10));d.DrawImage(source,new Rect(pad,pad,source.PixelWidth,source.PixelHeight));d.Pop();}return Render(v,source.PixelWidth+2*pad,source.PixelHeight+2*pad);
  }
  public static async Task Copy(BitmapSource b) {var data=new System.Windows.DataObject();data.SetImage(b);await CaptureClipboard.Set(()=>System.Windows.Clipboard.SetDataObject(data,false));}

 }
 public static class Native {
  [DllImport("user32.dll")]public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
  [DllImport("dwmapi.dll")]public static extern int DwmFlush();
 }
 public static class Video {
  public static string Exe {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"engine","ffmpeg.exe");}}
  public static bool Available {get{return System.IO.File.Exists(Exe);}}
  public static Process Start(string arguments) {var p=new Process{StartInfo=new ProcessStartInfo(Exe,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardInput=true}};p.ErrorDataReceived+=(s,e)=>{};p.Start();p.BeginErrorReadLine();return p;}
  public static async Task ConvertGif(string input,string output){var p=Start("-y -i \""+input+"\" -vf \"fps=10,scale=900:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \""+output+"\"");await Task.Run(()=>p.WaitForExit());if(p.ExitCode!=0)throw new IOException("GIF 转换失败");p.Dispose();}
 }
}
