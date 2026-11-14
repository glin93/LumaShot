// Documentation artwork rendered from the application's WPF controls and synthetic data.
// No desktop capture, global shortcuts, account connections or clipboard writes.
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace LumaShot {
 public static class ShowcaseProgram {
  [STAThread] public static void Main(string[] args) {
   try {
    Directory.CreateDirectory(args[0]);Directory.CreateDirectory(args[1]);
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    var library=new Library(args[0]);Demo.Seed(library);
    int i=0;foreach(var shot in library.Items){shot.Created=DateTime.Today.AddHours(14).AddMinutes(-i++*15).ToString("o");shot.Notes="本周产品与设计资料，留作复盘参考。";}
    library.Save();var ui=new MainUI(library,true);ui.ExportShowcase(args[1]);
   }catch(Exception error){File.WriteAllText(Path.Combine(args[0],"showcase-error.txt"),error.ToString());Environment.ExitCode=1;}
  }
 }
 public partial class MainUI {
  static void Place(Canvas canvas,UIElement element,double x,double y){Canvas.SetLeft(element,x);Canvas.SetTop(element,y);canvas.Children.Add(element);}
  static Canvas Board(string title,double height){
   var canvas=new Canvas{Width=1600,Height=height,Background=new LinearGradientBrush(Color.FromRgb(245,244,252),Color.FromRgb(232,235,245),90)};
   TextElement.SetFontFamily(canvas,new FontFamily("Segoe UI, Microsoft YaHei UI"));
   var name=T("LUMASHOT",14,"#7964C6");name.FontWeight=FontWeights.SemiBold;Place(canvas,name,72,38);
   var heading=T(title,32,"#252738");heading.FontWeight=FontWeights.SemiBold;Place(canvas,heading,72,67);
   return canvas;
  }
  static Border Frame(UIElement content,double width,double height){
   var inner=new Grid{Clip=new RectangleGeometry(new Rect(0,0,width-4,height-4),14,14)};inner.Children.Add(content);
   return new Border{Width=width,Height=height,Background=Brushes.White,CornerRadius=new CornerRadius(16),Padding=new Thickness(1),BorderBrush=B("#DDDDE9"),BorderThickness=new Thickness(1),Child=inner,Effect=new DropShadowEffect{Color=Color.FromRgb(34,30,64),Opacity=.13,BlurRadius=30,ShadowDepth=10}};
  }
  static BitmapSource Snapshot(FrameworkElement element,double width,double height){
   element.Width=width;element.Height=height;element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)(width*2),(int)(height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(element);bitmap.Freeze();return bitmap;
  }
  static void SaveBoard(Canvas board,string path){Images.Save(Snapshot(board,board.Width,board.Height),path);}
  internal void ExportShowcase(string output){
   poll.Stop();syncTimer.Stop();tray.Visible=false;tray.Dispose();
   selected=lib.Items[0];Refresh();
   var content=(FrameworkElement)Window.Content;Window.Content=null;content.Resources=Window.Resources;TextElement.SetFontFamily(content,Window.FontFamily);TextElement.SetFontSize(content,13);TextElement.SetForeground(content,Window.Foreground);((Panel)content).Background=Window.Background;
   var library=Snapshot(content,1280,800);
   var first=Board("Library / 资料库",1080);
   Place(first,Frame(new Image{Source=library,Stretch=Stretch.Uniform},1456,910),72,138);
   SaveBoard(first,Path.Combine(output,"library.png"));

   var floating=Board("Floating previews / 截后浮窗",1000);
   var workspace=new DockPanel();var bar=new Border{Height=38,Background=B("#F8F8FB"),Padding=new Thickness(18,10,0,0),Child=T("Workspace · Product overview",12,"#838394")};DockPanel.SetDock(bar,Dock.Top);workspace.Children.Add(bar);workspace.Children.Add(new Image{Source=Demo.Image(0),Stretch=Stretch.Uniform});
   Place(floating,Frame(workspace,1080,740),430,165);
   aiAvailable=true;
   for(int n=0;n<5;n++){
    var tile=(Border)CaptureTile(lib.Items[n],false);tile.Margin=new Thickness(0);
    if(n==1){var grid=(Grid)tile.Child;((Grid)grid.Children[1]).Visibility=Visibility.Visible;((Image)grid.Children[0]).Effect=new BlurEffect{Radius=4};}
    Place(floating,tile,100,136+n*163);
   }
   SaveBoard(floating,Path.Combine(output,"floating-previews.png"));

   EnsureQuickHistory();quickSelectionId=lib.Items[1].Id;RenderQuickHistory();
   var quick=(FrameworkElement)quickHistory.Content;quickHistory.Content=null;quick.Resources=Window.Resources;TextElement.SetFontFamily(quick,Window.FontFamily);TextElement.SetFontSize(quick,13);
   quick.Clip=new RectangleGeometry(new Rect(7,7,1306,276),20,20);var historyImage=Snapshot(quick,1320,290);
   var historyBoard=Board("Quick history / 快速历史",850);
   Place(historyBoard,Frame(new Image{Source=Demo.Image(1),Stretch=Stretch.Uniform},1040,625),280,172);
   Place(historyBoard,new Image{Source=historyImage,Width=1456,Height=320,Stretch=Stretch.Fill},72,142);
   SaveBoard(historyBoard,Path.Combine(output,"quick-history.png"));

   var settingsLayout=new Grid{Background=B("#F6F7FB"),Resources=Window.Resources};TextElement.SetFontFamily(settingsLayout,Window.FontFamily);
   settingsLayout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});settingsLayout.ColumnDefinitions.Add(new ColumnDefinition());
   var navigation=new StackPanel{Margin=new Thickness(20,26,16,20)};navigation.Children.Add(PageHeader("设置",null));
   foreach(string label in new[]{"截图与录屏","快捷键","AI 服务","云同步","资料库","关于"}){var item=new Border{CornerRadius=new CornerRadius(8),Background=B(label=="截图与录屏"?"#EAE6F8":"#F6F7FB"),Padding=new Thickness(16,14,16,14),Margin=new Thickness(0,0,0,6),Child=T(label,14,label=="截图与录屏"?"#7057CF":"#55596B")};navigation.Children.Add(item);}settingsLayout.Children.Add(navigation);
   var settingsContent=new Border{Padding=new Thickness(24,26,28,20),Child=CapturePage()};Grid.SetColumn(settingsContent,1);settingsLayout.Children.Add(settingsContent);
   var settingsBoard=Board("Settings / 设置",1000);Place(settingsBoard,Frame(new Image{Source=Snapshot(settingsLayout,980,780),Stretch=Stretch.Uniform},1050,836),275,130);SaveBoard(settingsBoard,Path.Combine(output,"settings.png"));

   var conversation=new DockPanel{Background=B("#F6F7FB"),Margin=new Thickness(24),Resources=Window.Resources};TextElement.SetFontFamily(conversation,Window.FontFamily);
   var askHeading=PageHeader("Ask AI","工作台 · 本周数据概览");DockPanel.SetDock(askHeading,Dock.Top);conversation.Children.Add(askHeading);
   var composer=new Border{CornerRadius=new CornerRadius(13),Background=Brushes.White,BorderBrush=B("#DCDDE8"),BorderThickness=new Thickness(1),Padding=new Thickness(18),Height=120,Margin=new Thickness(0,12,0,0)};var composerBody=new DockPanel();var send=Btn("发送  ↑",()=>{},true);send.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(send,Dock.Bottom);composerBody.Children.Add(send);composerBody.Children.Add(T("继续追问…",14,"#9398AA"));composer.Child=composerBody;DockPanel.SetDock(composer,Dock.Bottom);conversation.Children.Add(composer);
   var answer=new StackPanel();answer.Children.Add(ChatMessage("user","你","帮我总结这个页面，有哪些值得保留的设计？"));answer.Children.Add(ChatMessage("assistant","AI","### 清晰的信息层级\n\n重点数据和图表一目了然。\n\n- **信息层级**：标题、关键数据和图表各有明确的位置。\n- **颜色使用**：紫色用于强调重点，其余区域保持简洁。\n- **布局节奏**：卡片间距一致，阅读顺畅。\n\n建议补充数据的时间范围，方便比较不同周期。"));conversation.Children.Add(answer);
   var askBoard=Board("Ask AI / 截图对话",1000);Place(askBoard,Frame(new Image{Source=Snapshot(new Border{Child=conversation},820,740),Stretch=Stretch.Uniform},930,840),335,130);SaveBoard(askBoard,Path.Combine(output,"ask-ai.png"));
   appIcon.Dispose();quitting=true;quickHistory.Close();Window.Close();Application.Current.Shutdown();
  }
 }
}
