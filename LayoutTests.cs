using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace LumaShot {
 public static class LayoutTests {
  static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static void Arrange(FrameworkElement element,double width,double height){element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();}
  public static void Run(string output){
   Directory.CreateDirectory(output);var results=new List<string>();
   try{
    Window window;using(var file=File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Main.xaml")))window=(Window)XamlReader.Load(file);
    foreach(double height in new[]{33.0,34.0,40.0,44.0}){
     var input=new TextBox{Resources=window.Resources,Text="截图 Search gyp",FontSize=13,Height=height,Width=300};Arrange(input,300,height);
     var host=(ScrollViewer)input.Template.FindName("PART_ContentHost",input);var text=input.GetRectFromCharacterIndex(0);
     Check(host.ViewportHeight>=text.Height&&text.Height>=13,"Text is clipped at height "+height);
     Check(host.Margin==new Thickness(0),"Padding applied twice");
    }
    results.Add("PASS Chinese/Latin text fits library, quick history and WebDAV input heights (33/34/40/44 DIP)");
    var connected=Backend.Json().Deserialize<Dictionary<string,object>>("{\"session\":{\"status\":\"connected\",\"identity\":{\"email\":\"sample@example.test\"}}}");Check(MainUI.ChatGPTStatus(connected)=="已登录 · sample@example.test","Connected account label");
    Check(MainUI.ChatGPTStatus(new Dictionary<string,object>())=="未登录","Missing session label");
    var expired=Backend.Json().Deserialize<Dictionary<string,object>>("{\"session\":{\"status\":\"reauth_required\"}}");Check(MainUI.ChatGPTStatus(expired)=="需要重新登录","Expired session label");results.Add("PASS ChatGPT status distinguishes connected account, signed-out state and reauthentication");
    var entry=new Expander{Resources=window.Resources,Header="添加其他账号",IsExpanded=false,Content=new TextBlock{Text="Continue with ChatGPT"}};Arrange(entry,500,150);var body=(ContentPresenter)entry.Template.FindName("Body",entry);Check(body.Visibility==Visibility.Collapsed,"Connected account login is collapsed");entry.IsExpanded=true;Arrange(entry,500,150);Check(body.Visibility==Visibility.Visible,"Additional account action expands");results.Add("PASS additional account entry is collapsed until explicitly expanded");
    var secret=new PasswordBox{Resources=window.Resources,Password="synthetic-password",Height=34,Width=300};Arrange(secret,300,34);
    var passwordHost=(ScrollViewer)secret.Template.FindName("PART_ContentHost",secret);Check(passwordHost.ViewportHeight>=14,"Password bullets clipped");results.Add("PASS password content has visible line height");
    var model=new ComboBox{Resources=window.Resources,ItemsSource=new[]{new ModelOption{Id="test-model",Name="Official model · test-model"}},DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue="test-model",Width=400,Height=38};Arrange(model,400,38);
    Check(Descendants(model).OfType<TextBlock>().Any(x=>x.Text=="Official model · test-model"),"Selected model must display its name");results.Add("PASS selected model label uses DisplayMemberPath");
    var scroll=new ScrollViewer{Resources=window.Resources,Content=new Border{Height=1000,Width=100},VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Arrange(scroll,220,200);scroll.ScrollToVerticalOffset(140);Arrange(scroll,220,200);Check(scroll.VerticalOffset>0,"Styled scrollbar must scroll content");results.Add("PASS styled ScrollViewer retains scrolling");
    File.WriteAllLines(Path.Combine(output,"layout-test-results.txt"),results);
   }catch(Exception ex){File.WriteAllText(Path.Combine(output,"layout-test-error.txt"),ex.ToString());Environment.ExitCode=1;}
  }
  public static void CheckUnicode(string output){
   Directory.CreateDirectory(output);try{string expected="中文提问 · 标签、备注与 AI 回答 café 日本語 😀";
    using(var backend=new Backend(output)){
     var record=new Dictionary<string,object>{{"Title",expected},{"Notes",expected},{"AiHistory",expected}};
     var response=backend.Call("sync",new Dictionary<string,object>{{"records",new[]{record}}}).GetAwaiter().GetResult();
     var actual=Backend.Rows(response,"records").Single();Check(Backend.S(actual,"Title")==expected&&Backend.S(actual,"Notes")==expected&&Backend.S(actual,"AiHistory")==expected,"Unicode corrupted across C# / Node IPC");
    }
    File.WriteAllText(Path.Combine(output,"unicode-test-results.txt"),"PASS real C# → Node → C# transport preserves Chinese, accented text, emoji and AI history; no account or network request\n");
   }catch(Exception ex){File.WriteAllText(Path.Combine(output,"unicode-test-error.txt"),ex.ToString());Environment.ExitCode=1;}
  }
 }
}
