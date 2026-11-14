using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Threading;
using System.IO.Compression;
using F=System.Windows.Forms;

namespace LumaShot {
 public class Program {
  [STAThread] public static void Main(string[] args) {
   var fixture=args.FirstOrDefault(x=>x.StartsWith("-Lifecycle",StringComparison.Ordinal));if(fixture!=null){LifecycleTests.Fixture(fixture);return;}
   Native.SetProcessDPIAware();
   string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaShot","Library");
   for(int i=0;i<args.Length-1;i++)if(args[i]=="--data")root=Path.GetFullPath(args[i+1]);
   try {
    if(args.Contains("--self-test")){Tests.Run(root);return;}
    if(args.Contains("--lifecycle-test")){LifecycleTests.Run(root);return;}
    if(args.Contains("--resident-test")){ResidentTests.Run(root);return;}
    if(args.Contains("--layout-test")){LayoutTests.Run(root);return;}
    if(args.Contains("--unicode-test")){LayoutTests.CheckUnicode(root);return;}
    if(args.Contains("--engine-test")){Tests.EngineSmoke(root);return;}
    if(args.Contains("--recording-test")){Tests.RecordingSmoke(root);return;}
    if(args.Contains("--surface-test")){Tests.SurfaceSmoke(root);return;}
    bool created;using(var mutex=new Mutex(true,"Local\\LumaShot-"+root.ToLowerInvariant().GetHashCode(),out created)) {
     if(!created){MessageBox.Show("LumaShot 已在运行。点击托盘图标打开快速历史。","LumaShot");return;}
     using(var lifetime=new Mutex(false,"Local\\LumaShot-Running")){
     var app=new Application();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
     app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"LumaShot");e.Handled=true;};
     var lib=new Library(root);if(args.Contains("--demo")&&lib.Items.Count==0)Demo.Seed(lib);
     var ui=new MainUI(lib,args.Contains("--demo"));if(args.Contains("--demo-surfaces"))ui.Window.Loaded+=(s,e)=>ui.PreviewCaptureSurfaces();if(args.Contains("--demo-tray"))ui.Window.Loaded+=(s,e)=>ui.ShowTrayMenu();app.Run(ui.Window);}
    }
   }catch(Exception e){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"startup-error.txt"),e.ToString());if(args.Contains("--self-test")){Environment.ExitCode=1;return;}MessageBox.Show(e.Message,"LumaShot · 启动失败");}
  }
 }
 public partial class MainUI {
  public Window Window;Library lib;CaptureEngine engine;bool demo,oldest,quitting,scanning;string filter="all";Shot selected;int shown=48;
  StackPanel history,inspector;TextBox search;TextBlock status;F.NotifyIcon tray;System.Drawing.Icon appIcon;DispatcherTimer poll;IntPtr handle;HashSet<string> batch=new HashSet<string>();double cardWidth=216;
  public MainUI(Library l,bool isDemo) {
   lib=l;demo=isDemo;aiAvailable=demo;preferences=new Preferences(lib.Root);
   using(var stream=File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Main.xaml")))Window=(Window)XamlReader.Load(stream);
   appIcon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);Window.Icon=Imaging.CreateBitmapSourceFromHIcon(appIcon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
   if(demo)Window.Title="LumaShot · 界面预览（示例资料库）";
   history=Get<StackPanel>("History");inspector=Get<StackPanel>("Inspector");search=Get<TextBox>("Search");status=Get<TextBlock>("Status");
   engine=new CaptureEngine(lib.Root,preferences);
   Click("NavAll",()=>Navigate("all"));Click("NavStar",()=>Navigate("favorites"));Click("NavImage",()=>Navigate("image"));Click("NavVideo",()=>Navigate("video"));Click("NavTrash",()=>Navigate("trash"));
   Click("Capture",()=>Run("RectangleRegion",null));Click("CaptureMenu",ShowCaptureMenu);Click("Import",ImportDialog);Click("Sort",()=>{oldest=!oldest;Get<Button>("Sort").Content=oldest?"↑  最早优先":"↓  最新优先";Refresh();});
   Click("QuickHistory",ToggleQuickHistory);Click("Settings",Settings);Click("Quit",()=>Exit());
   var debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};debounce.Tick+=(s,e)=>{debounce.Stop();shown=48;Refresh();};
   search.TextChanged+=(s,e)=>{Get<TextBlock>("SearchHint").Visibility=search.Text.Length>0?Visibility.Collapsed:Visibility.Visible;debounce.Stop();debounce.Start();};
   Get<ComboBox>("DateFilter").SelectionChanged+=(s,e)=>{shown=48;Refresh();};
   var resize=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(160)};resize.Tick+=(s,e)=>{resize.Stop();double available=Get<ScrollViewer>("HistoryScroll").ActualWidth-72;int cols=Math.Max(1,(int)(available/222));double width=Math.Max(180,Math.Floor(available/cols)-14);if(Math.Abs(cardWidth-width)>3){cardWidth=width;Refresh();}};Window.SizeChanged+=(s,e)=>{resize.Stop();resize.Start();};
   Window.PreviewKeyDown+=(s,e)=>{if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.F){search.Focus();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.C&&!(Keyboard.FocusedElement is TextBox)){CopySelected();e.Handled=true;}else if(e.Key==Key.Delete&&!(Keyboard.FocusedElement is TextBox)){Trash();e.Handled=true;}};
   Window.AllowDrop=true;Window.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};Window.Drop+=async(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))await ImportPaths((string[])e.Data.GetData(DataFormats.FileDrop));};
   Window.SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(Window).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);ApplyBindings();};
   Window.Closing+=(s,e)=>{if(!quitting){e.Cancel=true;Window.Hide();}};
   tray=new F.NotifyIcon{Icon=appIcon,Text="LumaShot",Visible=true};tray.MouseClick+=(s,e)=>{if(e.Button==F.MouseButtons.Left)ToggleQuickHistory();else if(e.Button==F.MouseButtons.Right)ShowTrayMenu();};UpdateTray();
   poll=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};poll.Tick+=async(s,e)=>{await Scan();await IndexText();};poll.Start();StartIntegrations();
   selected=lib.Items.FirstOrDefault(x=>!x.Deleted);Navigate("all");Window.Loaded+=async(s,e)=>{if(!demo)StartCaptureEngine();await RefreshAIAvailability();await Scan();if(!string.IsNullOrEmpty(lib.Notice))Say(lib.Notice);};
  }
  async void StartCaptureEngine(){await engine.WarmUp();}
  T Get<T>(string name)where T:FrameworkElement{return (T)Window.FindName(name);}
  void Click(string name,Action a){Get<Button>(name).Click+=(s,e)=>Safe(a);}
  void Safe(Action a){try{a();}catch(Exception e){MessageBox.Show(Window,e.Message,"LumaShot",MessageBoxButton.OK,MessageBoxImage.Information);}}
  public void Say(string message){status.Text=message;}
  void Show(){HideQuickHistory();Window.Show();Window.WindowState=WindowState.Normal;Window.Activate();}
  void Exit(){if(engine.Busy||capturing||maintenance){MessageBox.Show(Window,"请先完成或关闭正在进行的截图、标注、录制或备份，再退出。","LumaShot");return;}quitting=true;poll.Stop();if(syncTimer!=null)syncTimer.Stop();engine.Dispose();if(backend!=null)backend.Dispose();UnregisterBindings();CloseSurfaces();tray.Dispose();appIcon.Dispose();Window.Close();Application.Current.Shutdown();}
  IntPtr Hook(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x312){int index=wp.ToInt32()-1;if(index>=0&&index<Preferences.Actions.Length){handled=true;RunShortcut(Preferences.Actions[index]);}}return IntPtr.Zero;}
  void RunShortcut(string action){int index=Array.IndexOf(Preferences.Actions,action);if(index>=0&&index<Preferences.EngineActions.Length)Run(Preferences.EngineActions[index],null);else if(action=="quickHistory")ToggleQuickHistory();else if(action=="library")Show();}
  void Navigate(string f){batch.Clear();filter=f;shown=48;selected=null;Get<TextBlock>("Heading").Text=f=="favorites"?"我的收藏":f=="image"?"截图与动图":f=="video"?"屏幕录制":f=="trash"?"回收站":"截图历史";var pairs=new Dictionary<string,string>{{"NavAll","all"},{"NavStar","favorites"},{"NavImage","image"},{"NavVideo","video"},{"NavTrash","trash"}};foreach(var p in pairs){Get<Button>(p.Key).Background=B(p.Value==f?"#353047":"#1B1D2B");Get<Button>(p.Key).Foreground=B(p.Value==f?"#E3DAFF":"#9297AD");}Refresh();}
  List<Shot> Filtered(){var q=lib.Query(filter,search.Text,oldest);int d=Get<ComboBox>("DateFilter").SelectedIndex;if(d>0){DateTime from=DateTime.Today.AddDays(d==1?0:d==2?-6:-29);q=q.Where(x=>x.Date>=from);}return q.ToList();}
  void Refresh(){
   RenderRail();if(quickHistory!=null&&quickHistory.IsVisible)RenderQuickHistory();
   var items=Filtered();batch.IntersectWith(items.Select(x=>x.Id));if(selected!=null&&!items.Contains(selected))selected=null;
   Get<TextBlock>("Total").Text=items.Count.ToString();Get<TextBlock>("LibrarySize").Text=lib.Items.Count(x=>!x.Deleted)+" 条记录 · 不自动清理";
   history.Children.Clear();
   if(items.Count==0){Empty();Details();return;}
   var batchBar=new WrapPanel{Margin=new Thickness(0,0,0,16)};batchBar.Children.Add(Small(batch.Count==0?"选择本页":"已选 "+batch.Count+" 项",()=>{foreach(var x in items.Take(shown))batch.Add(x.Id);Refresh();}));if(batch.Count>0){batchBar.Children.Add(Small("导出所选",ExportBatch));batchBar.Children.Add(Small("收藏",()=>{foreach(var x in lib.Items.Where(x=>batch.Contains(x.Id)))x.Star=true;lib.Save();batch.Clear();Refresh();}));batchBar.Children.Add(Small(filter=="trash"?"恢复所选":"移到回收站",()=>{foreach(var x in lib.Items.Where(x=>batch.Contains(x.Id)))x.Deleted=filter!="trash";lib.Save();batch.Clear();Refresh();}));batchBar.Children.Add(Small("取消选择",()=>{batch.Clear();Refresh();}));}history.Children.Add(batchBar);
   foreach(var group in items.Take(shown).GroupBy(x=>x.Date.Date)){
    string title=group.Key==DateTime.Today?"今天":group.Key==DateTime.Today.AddDays(-1)?"昨天":group.Key.ToString("yyyy 年 M 月 d 日");
    var label=T(title+"    ·    "+group.Count(),12,"#8B8FA5");label.Margin=new Thickness(0,2,0,14);history.Children.Add(label);
    var wrap=new WrapPanel{Margin=new Thickness(0,0,0,12)};foreach(var item in group)wrap.Children.Add(Card(item));history.Children.Add(wrap);
   }
   if(items.Count>shown){var more=Btn("加载更多  ·  还有 "+(items.Count-shown)+" 条",()=>{shown+=48;Refresh();});more.HorizontalAlignment=HorizontalAlignment.Center;history.Children.Add(more);}
   Details();
  }
  FrameworkElement Card(Shot item){
   bool active=selected==item;var border=new Border{Width=cardWidth,Margin=new Thickness(0,0,14,16),Background=Brushes.White,CornerRadius=new CornerRadius(11),BorderBrush=B(active||batch.Contains(item.Id)?"#8A72E8":"#E5E7F0"),BorderThickness=new Thickness(active?2:1),Cursor=Cursors.Hand};
   var stack=new StackPanel();var preview=new Grid{Height=139,Background=B("#EDEEF5"),ClipToBounds=true,Margin=new Thickness(7,7,7,0)};
   try{preview.Children.Add(new Image{Source=Images.Load(lib.Full(item.Thumb),450),Stretch=Stretch.Uniform,Margin=new Thickness(9)});}catch{preview.Children.Add(T("预览暂不可用",12,"#9599AA"));}
   var badge=new Border{Background=B("#EFFFFFFF"),CornerRadius=new CornerRadius(4),Padding=new Thickness(6,3,6,3),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(6)};badge.Child=T(item.Kind=="video"?"▷  VIDEO":item.Kind=="gif"?"GIF":"PNG",8,"#75788E");preview.Children.Add(badge);
   if(item.Star){var star=T("★",15,"#B4935E");star.HorizontalAlignment=HorizontalAlignment.Right;star.Margin=new Thickness(0,5,9,0);preview.Children.Add(star);}
   stack.Children.Add(preview);var title=T(item.Title,12,"#34364C");title.TextTrimming=TextTrimming.CharacterEllipsis;title.Margin=new Thickness(13,12,13,0);title.FontWeight=FontWeights.Medium;stack.Children.Add(title);
   var info=T(item.Date.ToString("HH:mm")+"     "+item.Width+" × "+item.Height+(string.IsNullOrWhiteSpace(item.Tags)?"":"     #"+item.Tags.Split(',')[0]),10,"#969BAE");info.Margin=new Thickness(13,7,10,13);info.TextTrimming=TextTrimming.CharacterEllipsis;stack.Children.Add(info);border.Child=stack;
   border.MouseLeftButtonUp+=(s,e)=>{if(Keyboard.Modifiers==ModifierKeys.Control){if(!batch.Add(item.Id))batch.Remove(item.Id);}selected=item;Refresh();};border.MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2){selected=item;OpenPreview();e.Handled=true;}};
   var menu=new ContextMenu();Add(menu,"复制图片 / 文件",()=>{selected=item;CopySelected();});Add(menu,"编辑标注",()=>{selected=item;Edit();});Add(menu,item.Star?"取消收藏":"加入收藏",()=>{item.Star=!item.Star;lib.Save();Refresh();});Add(menu,item.Deleted?"恢复记录":"移到回收站",()=>{item.Deleted=!item.Deleted;lib.Save();Refresh();});border.ContextMenu=menu;
   return border;
  }
  void Empty(){var panel=new StackPanel{Margin=new Thickness(15,85,15,0),HorizontalAlignment=HorizontalAlignment.Center};var icon=new Border{Width=76,Height=76,Background=B("#ECE7FB"),CornerRadius=new CornerRadius(22),Child=new TextBlock{Text="▧",Foreground=B("#9B85DC"),FontSize=42,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};panel.Children.Add(icon);var title=T(string.IsNullOrEmpty(search.Text)?(filter=="trash"?"回收站很干净":filter=="favorites"?"还没有收藏":filter=="video"?"还没有录屏":"还没有截图"):"没有找到匹配的记录",19,"#4D5065");title.Margin=new Thickness(0,23,0,12);title.TextAlignment=TextAlignment.Center;panel.Children.Add(title);var desc=T(!string.IsNullOrEmpty(search.Text)||Get<ComboBox>("DateFilter").SelectedIndex>0?"试试其他关键词或时间范围。":filter=="favorites"?"收藏的截图和录屏会显示在这里。":filter=="video"?"完成录屏后，会自动保存到这里。":filter=="trash"?"移入回收站的记录可在这里恢复。":"截图后自动保存到历史。",11,"#9599AD");desc.TextAlignment=TextAlignment.Center;desc.TextWrapping=TextWrapping.Wrap;panel.Children.Add(desc);if(filter=="all"&&search.Text==""){var start=Btn("＋  开始第一次截图",()=>Run("RectangleRegion",null),true);start.Margin=new Thickness(0,26,0,0);start.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(start);}history.Children.Add(panel);}
  void Details(){
   if(selected!=null&&ReferenceEquals(inspector.Tag,selected)&&inspector.IsKeyboardFocusWithin&&Keyboard.FocusedElement is TextBox)return;
   inspector.Tag=selected;
   inspector.Children.Clear();if(selected==null){inspector.Children.Add(T("详情",12,"#7D8299"));var icon=T("◈",48,"#DDD8EF");icon.TextAlignment=TextAlignment.Center;icon.Margin=new Thickness(0,100,0,22);inspector.Children.Add(icon);var label=T("选择记录",16,"#757A91");label.TextAlignment=TextAlignment.Center;inspector.Children.Add(label);var help=T("查看、编辑或整理截图。",11,"#A0A5B7");help.TextAlignment=TextAlignment.Center;help.LineHeight=22;help.Margin=new Thickness(0,12,0,0);inspector.Children.Add(help);return;}
   Shot item=selected;inspector.Children.Add(T(item.Deleted?"回收站中的记录":"记录详情",11,"#8D92A7"));
   if(!string.IsNullOrEmpty(item.ParentId)){var parent=lib.Items.FirstOrDefault(x=>x.Id==item.ParentId);if(parent!=null)inspector.Children.Add(Small("查看来源截图",()=>Preview(lib.Full(parent.File),parent.Title)));}
   var thumb=new Border{Background=B("#F1F2F8"),CornerRadius=new CornerRadius(8),Height=145,Margin=new Thickness(0,17,0,17),Cursor=Cursors.Hand};try{thumb.Child=new Image{Source=Images.Load(lib.Full(item.Thumb)),Stretch=Stretch.Uniform,Margin=new Thickness(10)};}catch{}thumb.MouseLeftButtonUp+=(s,e)=>OpenPreview();inspector.Children.Add(thumb);
   var title=new TextBox{Text=item.Title,FontSize=13,FontWeight=FontWeights.SemiBold};title.LostKeyboardFocus+=(s,e)=>{if(!string.IsNullOrWhiteSpace(title.Text)&&title.Text!=item.Title){item.Title=title.Text.Trim();lib.Save();Refresh();}};inspector.Children.Add(title);
   var meta=T(item.Date.ToString("yyyy.MM.dd   HH:mm:ss")+"\n"+item.Width+" × "+item.Height+" px   ·   "+Size(item),10,"#999EB0");meta.LineHeight=22;meta.Margin=new Thickness(0,10,0,15);inspector.Children.Add(meta);
   if(item.Deleted){inspector.Children.Add(Btn("恢复到资料库",()=>{item.Deleted=false;lib.Save();Refresh();},true));inspector.Children.Add(T("原文件仍然保留，可随时恢复。",11,"#969BAE"));return;}
   var row=new UniformGridEx(2);row.Children.Add(Btn("复制",CopySelected,true));row.Children.Add(Btn(item.Kind=="video"?"播放":"编辑标注",()=>{if(item.Kind=="video")OpenPreview();else Edit();}));inspector.Children.Add(row);
   var row2=new UniformGridEx(2);row2.Children.Add(Btn("导出…",Export));row2.Children.Add(Btn(item.Star?"★ 已收藏":"☆ 收藏",()=>{item.Star=!item.Star;lib.Save();Refresh();}));inspector.Children.Add(row2);
   if(item.Kind!="video"){var row3=new UniformGridEx(2);row3.Children.Add(Btn("贴到屏幕",Pin));row3.Children.Add(Btn("提取文字",ExtractText));inspector.Children.Add(row3);}
   else inspector.Children.Add(Btn("导出为 GIF…",ExportGif));
   inspector.Children.Add(Btn("✦  Ask AI",Ask,true));
   AddLabel("标签",18);var tags=new TextBox{Text=item.Tags??"",ToolTip="用英文逗号分隔，如 工作,设计"};tags.LostKeyboardFocus+=(s,e)=>{item.Tags=tags.Text;lib.Save();};inspector.Children.Add(tags);
   AddLabel("备注 · 可搜索",14);var notes=new TextBox{Text=item.Notes??"",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=65,MaxHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};notes.LostKeyboardFocus+=(s,e)=>{item.Notes=notes.Text;lib.Save();};inspector.Children.Add(notes);
   var tools=new WrapPanel{Margin=new Thickness(0,16,0,0)};tools.Children.Add(Small("查看原图",()=>Preview(lib.Full(item.Original),item.Title+" · 原始文件")));tools.Children.Add(Small("文件位置",()=>Process.Start("explorer.exe","/select,\""+lib.Full(item.File)+"\"")));tools.Children.Add(Small("回收站",Trash));inspector.Children.Add(tools);
  }
  string Size(Shot x){try{long b=new FileInfo(lib.Full(x.File)).Length;return b>1048576?(b/1048576.0).ToString("0.0")+" MB":(b/1024.0).ToString("0")+" KB";}catch{return "文件缺失";}}
  void AddLabel(string s,int top){var label=T(s,10,"#9297AA");label.Margin=new Thickness(0,top,0,7);inspector.Children.Add(label);}
  Button Small(string text,Action a){var b=Btn(text,a);b.FontSize=10;b.Padding=new Thickness(7,5,7,5);b.Margin=new Thickness(0,0,5,5);return b;}
  public static SolidColorBrush B(string hex){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(hex);b.Freeze();return b;}
  public static TextBlock T(string text,double size,string color){return new TextBlock{Text=text,FontSize=size,Foreground=B(color)};}
  Button Btn(string text,Action action,bool accent=false){var b=new Button{Content=text,Style=(Style)Window.FindResource(accent?(object)"Accent":typeof(Button)),HorizontalContentAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,6,6)};b.Click+=(s,e)=>Safe(action);return b;}
  void Add(ContextMenu menu,string text,Action a){var m=new MenuItem{Header=text};m.Click+=(s,e)=>Safe(a);menu.Items.Add(m);}
  void ShowCaptureMenu(){var m=new ContextMenu();for(int i=0;i<6;i++){string action=Preferences.Actions[i];if(i==3)m.Items.Add(new Separator());Add(m,Preferences.Names[i]+"     "+Hotkey(action),()=>RunShortcut(action));}m.Items.Add(new Separator());Add(m,"滚动长截图",()=>Run("ScrollingCapture",null));Add(m,"录制动图  GIF",()=>Run("ScreenRecorderGIF",null));Add(m,"延时 3 秒截图",async()=>await CaptureTask("RectangleRegion",null,3000));Add(m,"从剪贴板导入",PasteImport);m.PlacementTarget=Get<Button>("CaptureMenu");m.IsOpen=true;}
  async void Run(string action,string file){
   await CaptureTask(action,file);
  }
  internal async Task CaptureTask(string action,string file=null,int delay=0){
   if(maintenance){Say("正在备份，请稍后截图。");return;}
   if(engine.Busy||capturing){Say("请先结束当前截图、编辑或录制。");return;}
   int count=lib.Items.Count;capturing=true;railArea=PointerWorkArea();
   try{await PrepareCaptureSurfaces();if(delay>0)await Task.Delay(delay);await engine.Run(action,file);await Scan(true);Say(lib.Items.Count>count?"已保存":"已取消");}
   catch(Exception e){Say(e.Message);MessageBox.Show(e.Message,"LumaShot");}
   finally{capturing=false;ResumeSurfaces();}
  }
  async Task Scan(bool fromCapture=false){
   if(scanning||engine.Busy||maintenance||(capturing&&!fromCapture))return;scanning=true;bool changed=false;try{
    foreach(var p in Directory.EnumerateFiles(engine.Inbox,"*",SearchOption.AllDirectories).OrderBy(File.GetLastWriteTimeUtc).ThenBy(x=>x,StringComparer.Ordinal))try{var shot=await engine.Import(lib,p,false);if(shot!=null){selected=shot;changed=true;await AfterCapture(shot);}}catch(IOException){}catch(NotSupportedException){}catch(System.Runtime.InteropServices.COMException){}
    if(changed){Refresh();Say("新记录已自动归档");}
   }finally{scanning=false;}
  }
  async void CopySelected(){if(selected==null)return;try{await CaptureClipboard.Copy(lib,selected);Say("已复制 · 在目标应用按 Ctrl + V 粘贴");}catch(Exception e){Say(e.Message);}}
  async void PasteImport(){try{if(Clipboard.ContainsImage()){selected=lib.Add(Clipboard.GetImage(),"剪贴板 "+DateTime.Now.ToString("MM-dd HH:mm:ss"),"image");Refresh();Say("剪贴板图片已保存");}else if(Clipboard.ContainsFileDropList())await ImportPaths(Clipboard.GetFileDropList().Cast<string>().ToArray());else Say("剪贴板没有可导入的图片。");}catch(Exception e){Say(e.Message);}}
  async void ImportDialog(){var d=new Microsoft.Win32.OpenFileDialog{Filter="图片与视频|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.webm;*.avi",Multiselect=true};if(d.ShowDialog(Window)==true)await ImportPaths(d.FileNames);}
  async Task ImportPaths(string[] paths){int n=0,fail=0;foreach(var p in paths)try{if(Directory.Exists(p))continue;var x=await engine.Import(lib,p,true);if(x!=null){selected=x;n++;}}catch{fail++;}filter="all";Refresh();Say("已导入 "+n+" 项"+(fail>0?"，"+fail+" 项无法读取":""));}
  async void Edit(bool returnToLibrary=true,string previewPath=null){
   if(selected==null)return;if(selected.Kind=="video"){OpenPreview();return;}if(engine.Busy||capturing||maintenance){Say("请先完成当前任务。");return;}
   Shot source=selected;string input=previewPath??lib.Full(source.File);string folder=Path.Combine(lib.Root,"editor");Directory.CreateDirectory(folder);string copy=Path.Combine(folder,"编辑_"+Guid.NewGuid().ToString("N")+Path.GetExtension(input));File.Copy(input,copy);
   var known=new HashSet<string>(lib.Items.Select(x=>x.Id));string before=FileDigest(copy);bool libraryVisible=Window.IsVisible;SuspendSurfaces();
   try{await engine.Run("ImageEditor",copy);await Scan();
    if(File.Exists(copy)&&FileDigest(copy)!=before){selected=await engine.Import(lib,copy,true);selected.SourceKey=null;selected.ParentId=source.Id;selected.Title=source.Title+" · 编辑";selected.Tags=source.Tags;await AfterCapture(selected);}
    foreach(var item in lib.Items.Where(x=>!known.Contains(x.Id)&&!string.IsNullOrEmpty(x.SourceKey)&&x.SourceKey.StartsWith(engine.Inbox,StringComparison.OrdinalIgnoreCase))){item.ParentId=source.Id;item.Title=source.Title+" · 编辑";item.Tags=source.Tags;}
    lib.Save();Refresh();Say(lib.Items.Any(x=>!known.Contains(x.Id))?"编辑已保存为新截图 · 原图保持不变":"未保存新的编辑结果。");
   }catch(Exception e){Say(e.Message);}finally{if(File.Exists(copy))File.Delete(copy);ResumeSurfaces();if(returnToLibrary&&libraryVisible)Show();}
  }
  static string FileDigest(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))return Convert.ToBase64String(sha.ComputeHash(file));}
  void Trash(){if(selected==null)return;selected.Deleted=!selected.Deleted;lib.Save();selected=null;Refresh();Say(filter=="trash"?"已恢复":"已移到回收站 · 原文件保留");}
  void Export(){if(selected==null)return;string ext=Path.GetExtension(selected.File);var d=new Microsoft.Win32.SaveFileDialog{FileName=SafeName(selected.Title)+ext,Filter=ext.Substring(1).ToUpperInvariant()+" 文件|*"+ext};if(d.ShowDialog(Window)==true){File.Copy(lib.Full(selected.File),d.FileName,true);Say("已导出到 "+d.FileName);}}
  static string SafeName(string name){foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');return name;}
  void ExportBatch(){using(var d=new F.FolderBrowserDialog{Description="选择批量导出文件夹"})if(d.ShowDialog()==F.DialogResult.OK){int count=0;foreach(var x in lib.Items.Where(x=>batch.Contains(x.Id))){string name=SafeName(x.Title)+"_"+x.Id.Substring(0,6)+Path.GetExtension(x.File);File.Copy(lib.Full(x.File),Path.Combine(d.SelectedPath,name),true);count++;}Say("已导出 "+count+" 个文件");}}
  async void ExportGif(){if(selected==null)return;var d=new Microsoft.Win32.SaveFileDialog{FileName=SafeName(selected.Title)+".gif",Filter="GIF 动图|*.gif"};if(d.ShowDialog(Window)==true){try{Say("正在生成 GIF…");await Video.ConvertGif(lib.Full(selected.File),d.FileName);Say("GIF 已导出");}catch(Exception e){Say(e.Message);}}}
  void OpenPreview(){if(selected!=null)Preview(lib.Full(selected.File),selected.Title);}
  void Preview(string path,string title,bool pinned=false){
   if(new[]{".mp4",".webm",".avi",".gif"}.Contains(Path.GetExtension(path).ToLowerInvariant())){Process.Start(new ProcessStartInfo(path){UseShellExecute=true});return;}
   var source=Images.Load(path);var area=PointerWorkArea();double maxWidth=Math.Min(pinned?720:1080,area.Width-32),maxHeight=Math.Min(pinned?560:820,area.Height-32);
   double scale=Math.Min(1,Math.Min(Math.Max(1,maxWidth-48)/Math.Max(1,source.PixelWidth),Math.Max(1,maxHeight-94)/Math.Max(1,source.PixelHeight)));
   var win=Surface(title+(pinned?" · 贴图":" · 预览"),true);win.Topmost=pinned;win.Owner=Window;win.Width=Math.Min(maxWidth,Math.Max(320,source.PixelWidth*scale+48));win.Height=Math.Min(maxHeight,Math.Max(220,source.PixelHeight*scale+94));win.Left=area.Left+(area.Width-win.Width)/2;win.Top=area.Top+(area.Height-win.Height)/2;
   var panel=new Grid();panel.RowDefinitions.Add(new RowDefinition{Height=new GridLength(40)});panel.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   var header=new Grid{Background=Brushes.Transparent,Margin=new Thickness(4,0,4,8)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var caption=T(title,12,"#797386");caption.TextTrimming=TextTrimming.CharacterEllipsis;caption.VerticalAlignment=VerticalAlignment.Center;caption.Margin=new Thickness(5,0,14,0);header.Children.Add(caption);
   var record=lib.Items.FirstOrDefault(x=>string.Equals(lib.Full(x.File),path,StringComparison.OrdinalIgnoreCase))??lib.Items.FirstOrDefault(x=>!string.IsNullOrEmpty(x.Original)&&string.Equals(lib.Full(x.Original),path,StringComparison.OrdinalIgnoreCase));
   if(record!=null){var edit=TileButton("编辑","编辑图片",()=>{if(engine.Busy||capturing||maintenance){Say("请先完成当前任务。");return;}selected=record;win.Close();Edit(false,path);},false);edit.Width=64;edit.Height=28;edit.Margin=new Thickness(0,0,10,0);Grid.SetColumn(edit,1);header.Children.Add(edit);}
   var close=TileButton("\uE711","关闭预览 · Esc",()=>win.Close(),true);Grid.SetColumn(close,2);header.Children.Add(close);header.MouseLeftButtonDown+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed){win.DragMove();e.Handled=true;}};
   panel.Children.Add(header);var viewport=new Grid{ClipToBounds=true,Margin=new Thickness(4,0,4,4)};viewport.Children.Add(new Image{Source=source,Stretch=Stretch.Uniform});Grid.SetRow(viewport,1);panel.Children.Add(viewport);win.Content=SurfaceFrame(panel);
   win.KeyDown+=(s,e)=>{if(e.Key==Key.Escape){win.Close();e.Handled=true;}};win.Show();
  }
  void RestoreBackup(){var d=new Microsoft.Win32.OpenFileDialog{Filter="LumaShot 备份|*.zip"};if(d.ShowDialog(Window)!=true)return;try{if(engine.Busy||scanning||syncing||ocrBusy||maintenance)throw new IOException("请先完成当前任务。");int n=BackupRestore.Merge(lib,d.FileName);Refresh();Say("已从备份恢复 "+n+" 条新记录；原有记录未覆盖。");}catch(Exception e){MessageBox.Show(Window,e.Message,"备份恢复失败");}}
  async void Backup(){
   if(maintenance)return;var d=new Microsoft.Win32.SaveFileDialog{FileName="LumaShot-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".zip",Filter="ZIP 备份|*.zip"};if(d.ShowDialog(Window)!=true)return;
   string temp=null;
   try{string outp=Path.GetFullPath(d.FileName);if(outp.StartsWith(lib.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("请将备份保存到资料库文件夹之外。");if(engine.Busy||scanning||syncing||ocrBusy)throw new IOException("请等待当前任务完成后再备份。");maintenance=true;lib.Save();poll.Stop();Window.IsEnabled=false;Say("正在备份…");
    string index=Backend.Json().Serialize(lib.Items);var files=lib.Items.SelectMany(x=>new[]{x.File,x.Original,x.Thumb}).Distinct(StringComparer.OrdinalIgnoreCase).ToList();temp=outp+"."+Guid.NewGuid().ToString("N")+".tmp";string target=temp;
    await Task.Run(()=>{using(var fs=new FileStream(target,FileMode.CreateNew))using(var zip=new ZipArchive(fs,ZipArchiveMode.Create)){using(var writer=new StreamWriter(zip.CreateEntry("library.json").Open(),new System.Text.UTF8Encoding(false)))writer.Write(index);foreach(var relative in files)zip.CreateEntryFromFile(lib.Full(relative),relative.Replace('\\','/'),CompressionLevel.Fastest);}});
    if(File.Exists(outp))File.Replace(temp,outp,null);else File.Move(temp,outp);temp=null;Say("备份完成："+outp);
   }catch(Exception e){Say(e.Message);}finally{if(temp!=null&&File.Exists(temp))File.Delete(temp);maintenance=false;Window.IsEnabled=true;poll.Start();}
  }
 }
 public class UniformGridEx:System.Windows.Controls.Primitives.UniformGrid{public UniformGridEx(int cols){Columns=cols;Margin=new Thickness(0,0,0,7);}protected override Size MeasureOverride(Size size){foreach(FrameworkElement child in Children)child.Margin=new Thickness(0,0,6,0);return base.MeasureOverride(size);}}
}
