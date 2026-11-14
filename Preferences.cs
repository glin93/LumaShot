using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LumaShot {
 public class ShortcutBinding {
  public uint Modifiers,KeyCode;public string Text;
  public static ShortcutBinding Parse(string text,string platform){
   if(platform!="windows"&&platform!="macos")throw new IOException("未知平台。");
   string[] parts=(text??"").Split('+').Select(x=>x.Trim()).ToArray();if(parts.Length==0||parts.Any(x=>x.Length==0))throw new IOException("请输入有效的快捷键。");
   uint mods=0;var labels=new List<string>();
   foreach(string part in parts.Take(parts.Length-1)){string p=part.ToLowerInvariant();uint bit=p=="ctrl"||p=="control"?2u:p=="alt"||p=="option"?1u:p=="shift"?4u:p=="win"||p=="cmd"||p=="command"?8u:0u;if(bit==0||(mods&bit)!=0)throw new IOException("快捷键修饰键无效或重复。");if(platform=="windows"&&(p=="cmd"||p=="command"||p=="option"))throw new IOException("Windows 使用 Ctrl、Alt、Shift 或 Win。");if(platform=="macos"&&p=="win")throw new IOException("macOS 使用 Cmd、Ctrl、Option 或 Shift。");mods|=bit;}
   string last=parts.Last();Key key;
   if(last.Length==1&&char.IsDigit(last[0]))key=(Key)Enum.Parse(typeof(Key),"D"+last);else if(!Enum.TryParse<Key>(last,true,out key))throw new IOException("无法识别按键："+last);
   if(key==Key.None||key==Key.System||key==Key.ImeProcessed||key==Key.DeadCharProcessed||key>=Key.LeftShift&&key<=Key.RightAlt||key==Key.LWin||key==Key.RWin||key==Key.Escape||key==Key.Tab)throw new IOException("请选择一个完整的组合键。");
   int vk=KeyInterop.VirtualKeyFromKey(key);if(vk<=0||vk>254||(mods&11)==0&&!(key>=Key.F1&&key<=Key.F24))throw new IOException("请使用 Ctrl、Alt、Win（Mac 为 Cmd/Option），或功能键 F1–F24。");
   if((mods&2)!=0)labels.Add("Ctrl");if((mods&1)!=0)labels.Add(platform=="macos"?"Option":"Alt");if((mods&4)!=0)labels.Add("Shift");if((mods&8)!=0)labels.Add(platform=="macos"?"Cmd":"Win");
   labels.Add(key>=Key.D0&&key<=Key.D9?((int)key-(int)Key.D0).ToString():key.ToString());return new ShortcutBinding{Modifiers=mods,KeyCode=(uint)vk,Text=string.Join("+",labels)};
  }
 }
 public class Preferences {
  public static readonly string[] Actions={"region","window","fullscreen","recordRegion","recordWindow","recordFullscreen","quickHistory","library"};
  public static readonly string[] Names={"自由截图","窗口截图","全屏截图","自由视频录屏","窗口视频录屏","全屏录屏","快速历史","资料库"};
  public static readonly string[] EngineActions={"RectangleRegion","ActiveWindow","PrintScreen","ScreenRecorder","ScreenRecorderActiveWindow","ScreenRecorderCustomRegion"};
  public Dictionary<string,string> Values=new Dictionary<string,string>();readonly string path;
  public Preferences(string root){path=Path.Combine(root,"preferences.json");if(File.Exists(path)){var data=Backend.Json().Deserialize<Dictionary<string,string>>(File.ReadAllText(path));foreach(var p in data){string[] parts=p.Key.Split('.');if(parts.Length==3&&parts[0]=="hotkeys"&&(parts[1]=="windows"||parts[1]=="macos")&&Actions.Contains(parts[2]))Values[p.Key]=ShortcutBinding.Parse(p.Value,parts[1]).Text;else if(IsCaptureKey(p.Key,p.Value))Values[p.Key]=p.Value;}}}
  public static bool IsCaptureKey(string key,string value){return new[]{"recording.microphone","recording.systemAudio"}.Contains(key)&&(value=="true"||value=="false")||new[]{"image","video"}.Any(k=>key=="capture."+k+".copy"&&(value=="true"||value=="false")||key=="capture."+k+".open"&&new[]{"none","library","folder"}.Contains(value));}
  public bool Microphone {get{string value;return Values.TryGetValue("recording.microphone",out value)&&value=="true";}}
  public bool SystemAudio {get{string value;return !Values.TryGetValue("recording.systemAudio",out value)||value=="true";}}
  public bool CaptureCopy(string kind){string value;return Values.TryGetValue("capture."+kind+".copy",out value)?value=="true":kind=="image";}
  public string CaptureOpen(string kind){string value;return Values.TryGetValue("capture."+kind+".open",out value)?value:"none";}
  public static string Default(string platform,string action){int index=Array.IndexOf(Actions,action);if(index<0)throw new IOException("未知快捷键操作。");return (platform=="macos"?"Shift+Cmd+":"Ctrl+Shift+")+new[]{"2","4","3","5","6","7","H","L"}[index];}
  public string Get(string platform,string action){string value;return Values.TryGetValue("hotkeys."+platform+"."+action,out value)?value:Default(platform,action);}
  public Dictionary<string,string> ForPlatform(string platform){return Actions.ToDictionary(a=>a,a=>Get(platform,a));}
  public static Dictionary<string,string> Validate(string platform,Dictionary<string,string> values){var result=Actions.ToDictionary(a=>a,a=>ShortcutBinding.Parse(values[a],platform).Text);if(result.Values.Distinct().Count()!=Actions.Length)throw new IOException("不同操作不能使用相同快捷键。");return result;}
  public void Save(){string tmp=path+".tmp";File.WriteAllText(tmp,Backend.Json().Serialize(Values));if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path);}
 }
 public partial class MainUI {
  Preferences preferences;string shortcutNotice="";
  string Hotkey(string action){return preferences.Get("windows",action);}
  void UnregisterBindings(){if(handle!=IntPtr.Zero)for(int i=1;i<=Preferences.Actions.Length;i++)Native.UnregisterHotKey(handle,i);}
  string RegisterBindings(Dictionary<string,string> values){
   UnregisterBindings();var failed=new List<string>();if(handle==IntPtr.Zero)return "";
   for(int i=0;i<Preferences.Actions.Length;i++){try{var b=ShortcutBinding.Parse(values[Preferences.Actions[i]],"windows");if(!Native.RegisterHotKey(handle,i+1,b.Modifiers|0x4000,b.KeyCode))failed.Add(Preferences.Names[i]+" "+b.Text);}catch(Exception e){failed.Add(e.Message);}}
   return failed.Count==0?"":"快捷键未启用："+string.Join("；",failed);
  }
  void ApplyBindings(){shortcutNotice=RegisterBindings(preferences.ForPlatform("windows"));UpdateTray();Get<Button>("Capture").ToolTip=Hotkey("region");Get<Button>("QuickHistory").ToolTip=Hotkey("quickHistory");Get<TextBlock>("ShortcutHint").Text=Hotkey("region")+"  截图";if(shortcutNotice.Length>0)Say(shortcutNotice);}
  void UpdateTray(){if(tray==null)return;var menu=new System.Windows.Forms.ContextMenuStrip();for(int i=0;i<6;i++){string action=Preferences.Actions[i];menu.Items.Add(Preferences.Names[i]+"   "+Hotkey(action),null,(s,e)=>RunShortcut(action));}menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());menu.Items.Add("快速历史   "+Hotkey("quickHistory"),null,(s,e)=>ToggleQuickHistory());menu.Items.Add("资料库   "+Hotkey("library"),null,(s,e)=>Show());menu.Items.Add("快捷键…",null,(s,e)=>{Show();ShortcutSettings();});menu.Items.Add("退出",null,(s,e)=>Exit());var old=tray.ContextMenuStrip;tray.ContextMenuStrip=menu;if(old!=null)old.Dispose();}
  void ShortcutSettings(){
   var win=new Window{Title="快捷键",Owner=Window,Width=570,Height=690,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=B("#F8F9FC"),FontFamily=Window.FontFamily,Resources=Window.Resources};
   win.MaxHeight=SystemParameters.WorkArea.Height-30;var layout=new DockPanel{Margin=new Thickness(28)};win.Content=layout;var panel=new StackPanel();DockPanel.SetDock(panel,Dock.Top);layout.Children.Add(panel);panel.Children.Add(T("快捷键",22,"#34364C"));panel.Children.Add(Help("同平台设备共用一套快捷键，随配置同步。"));
   var platform=new ComboBox{ItemsSource=new[]{"Windows","macOS"},SelectedIndex=0,Height=38,Margin=new Thickness(0,0,0,14)};panel.Children.Add(platform);var fields=new StackPanel();var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});var message=Help("输入组合键，或点击输入框后直接按键。");footer.Children.Add(message);
   var inputs=new Dictionary<string,TextBox>();string editingPlatform="windows";
   Action render=()=>{fields.Children.Clear();inputs.Clear();editingPlatform=platform.SelectedIndex==0?"windows":"macos";for(int i=0;i<Preferences.Actions.Length;i++){string a=Preferences.Actions[i];var grid=new Grid{Margin=new Thickness(0,0,0,10)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(145)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.Children.Add(T(Preferences.Names[i],13,"#595D75"));var box=new TextBox{Text=preferences.Get(editingPlatform,a),Height=40,Padding=new Thickness(8,3,8,3)};Grid.SetColumn(box,1);grid.Children.Add(box);inputs[a]=box;
     box.GotKeyboardFocus+=(s,e)=>UnregisterBindings();box.LostKeyboardFocus+=(s,e)=>ApplyBindings();
     box.PreviewKeyDown+=(s,e)=>{Key k=e.Key==Key.System?e.SystemKey:e.Key;var mods=Keyboard.Modifiers;if(k==Key.Tab||k==Key.Escape||k==Key.LeftCtrl||k==Key.RightCtrl||k==Key.LeftAlt||k==Key.RightAlt||k==Key.LeftShift||k==Key.RightShift||k==Key.LWin||k==Key.RWin)return;if(mods==ModifierKeys.None&&!(k>=Key.F1&&k<=Key.F24))return;string value=((mods&ModifierKeys.Control)!=0?"Ctrl+":"")+((mods&ModifierKeys.Alt)!=0?(editingPlatform=="macos"?"Option+":"Alt+"):"")+((mods&ModifierKeys.Shift)!=0?"Shift+":"")+((mods&ModifierKeys.Windows)!=0?(editingPlatform=="macos"?"Cmd+":"Win+"):"")+(k>=Key.D0&&k<=Key.D9?((int)k-(int)Key.D0).ToString():k.ToString());box.Text=value;box.CaretIndex=box.Text.Length;e.Handled=true;};fields.Children.Add(grid);}};
   platform.SelectionChanged+=(s,e)=>render();render();var actions=new WrapPanel();actions.Children.Add(Btn("恢复默认",()=>{foreach(var a in Preferences.Actions)inputs[a].Text=Preferences.Default(editingPlatform,a);}));actions.Children.Add(Btn("保存",()=>{
    var next=Preferences.Validate(editingPlatform,inputs.ToDictionary(p=>p.Key,p=>p.Value.Text));var old=new Dictionary<string,string>(preferences.Values);
    if(editingPlatform=="windows"){string error=RegisterBindings(next);if(error.Length>0){ApplyBindings();message.Text=error;return;}}
    try{foreach(var p in next)preferences.Values["hotkeys."+editingPlatform+"."+p.Key]=p.Value;preferences.Save();ApplyBindings();message.Text="已保存。";}catch{preferences.Values=old;ApplyBindings();throw;}
   },true));footer.Children.Add(actions);win.Closed+=(s,e)=>ApplyBindings();win.ShowDialog();
  }
  void CaptureSettings(){
   var win=new Window{Title="截图与录屏",Owner=Window,Width=520,Height=570,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=B("#F8F9FC"),FontFamily=Window.FontFamily,Resources=Window.Resources};var panel=new StackPanel{Margin=new Thickness(28)};win.Content=panel;panel.Children.Add(T("截图与录屏",22,"#34364C"));
   var copies=new Dictionary<string,CheckBox>();var opens=new Dictionary<string,CheckBox>();var targets=new Dictionary<string,ComboBox>();
   foreach(var kind in new[]{"image","video"}){var title=T(kind=="image"?"截图完成后":"录屏完成后",15,"#55516B");title.Margin=new Thickness(0,22,0,10);panel.Children.Add(title);var copy=new CheckBox{Content="自动复制",IsChecked=preferences.CaptureCopy(kind),Margin=new Thickness(0,0,0,10)};panel.Children.Add(copy);copies[kind]=copy;string mode=preferences.CaptureOpen(kind);var row=new StackPanel{Orientation=Orientation.Horizontal};var open=new CheckBox{Content="自动打开",IsChecked=mode!="none",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,15,0)};var target=new ComboBox{ItemsSource=new[]{"资料库","所在文件夹"},SelectedIndex=mode=="folder"?1:0,Width=190,Height=35,IsEnabled=mode!="none"};open.Checked+=(s,e)=>target.IsEnabled=true;open.Unchecked+=(s,e)=>target.IsEnabled=false;row.Children.Add(open);row.Children.Add(target);panel.Children.Add(row);opens[kind]=open;targets[kind]=target;}
   var audioTitle=T("录屏声音",15,"#55516B");audioTitle.Margin=new Thickness(0,22,0,0);panel.Children.Add(audioTitle);var mic=new CheckBox{Content="录制麦克风",IsChecked=preferences.Microphone,Margin=new Thickness(0,12,0,10)};var system=new CheckBox{Content="录制机内声音",IsChecked=preferences.SystemAudio};panel.Children.Add(mic);panel.Children.Add(system);var save=Btn("保存",()=>{preferences.Values["recording.microphone"]=mic.IsChecked==true?"true":"false";preferences.Values["recording.systemAudio"]=system.IsChecked==true?"true":"false";foreach(var kind in new[]{"image","video"}){preferences.Values["capture."+kind+".copy"]=copies[kind].IsChecked==true?"true":"false";preferences.Values["capture."+kind+".open"]=opens[kind].IsChecked!=true?"none":targets[kind].SelectedIndex==1?"folder":"library";}preferences.Save();win.Close();},true);save.Margin=new Thickness(0,25,0,0);panel.Children.Add(save);win.ShowDialog();
  }
 }
}
