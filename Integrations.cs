using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace LumaShot {
 public partial class MainUI {
  Backend backend;DispatcherTimer syncTimer;bool syncing,ocrBusy,maintenance;Window connectionsWindow;
  bool aiAvailable;
  internal static bool HasConfiguredAI(Dictionary<string,object> state){string active=Backend.S(state,"activeAIId");return !string.IsNullOrEmpty(active)&&Backend.Rows(state,"profiles").Any(p=>Backend.S(p,"id")==active&&!string.IsNullOrWhiteSpace(Backend.S(p,"model"))&&(!p.ContainsKey("available")||Convert.ToBoolean(p["available"])));}
  void UpdateAIAvailability(Dictionary<string,object> state){bool ready=HasConfiguredAI(state);if(ready==aiAvailable)return;aiAvailable=ready;RenderRail();if(quickHistory!=null&&quickHistory.IsVisible)RenderQuickHistory();}
  async Task RefreshAIAvailability(){if(backend==null&&!File.Exists(Path.Combine(Backend.VaultPath(lib.Root),"settings.dpapi")))return;try{UpdateAIAvailability(await Connection().Call("state"));}catch{aiAvailable=false;RenderRail();}}
  Backend Connection(){if(backend==null){backend=new Backend(lib.Root);backend.Browser+=url=>Window.Dispatcher.BeginInvoke(new Action(()=>{Uri u;if(Uri.TryCreate(url,UriKind.Absolute,out u)&&u.Scheme=="https"&&(u.Host=="auth.openai.com"||u.Host=="chatgpt.com"))Process.Start(new ProcessStartInfo(url){UseShellExecute=true});else Say("登录链接的域名不在允许列表，请检查组件版本。");}));}return backend;}
  void StartIntegrations(){syncTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(60)};syncTimer.Tick+=async(s,e)=>await Sync(false);syncTimer.Start();}
  async Task Sync(bool manual){
   if(syncing||maintenance||scanning||engine.Busy)return;
   if(!manual&&backend==null&&!File.Exists(Path.Combine(Backend.VaultPath(lib.Root),"settings.dpapi")))return;
   syncing=true;
   try{
    var before=lib.Items.ToDictionary(x=>x.Id,x=>Backend.Json().Serialize(x));
    var snapshot=Backend.Json().Deserialize<List<Shot>>(Backend.Json().Serialize(lib.Items));
    if(manual)Say("正在双向合并同步…");
    var preferencesBefore=new Dictionary<string,string>(preferences.Values);
    var response=await Connection().Call("sync",new Dictionary<string,object>{{"records",snapshot},{"preferences",preferencesBefore}});
    if(!response.ContainsKey("connected")||!Convert.ToBoolean(response["connected"])){if(manual)Say("请先添加 Google Drive 或 WebDAV 连接。");return;}
    var records=Backend.Json().ConvertToType<List<Shot>>(response["records"]);int count=0;
    string selectedId=selected==null?null:selected.Id;
    foreach(var remote in records){var local=lib.Items.FirstOrDefault(x=>x.Id==remote.Id);if(local!=null&&local==selected&&inspector.IsKeyboardFocusWithin)continue;if(local==null){lib.Items.Add(remote);count++;}
     else if(before.ContainsKey(local.Id)&&Backend.Json().Serialize(local)==before[local.Id]){remote.SourceKey=local.SourceKey;remote.OcrDone=local.OcrDone||!string.IsNullOrEmpty(remote.OcrText);int index=lib.Items.IndexOf(local);lib.Items[index]=remote;count++;}}
    if(response.ContainsKey("preferences")){var merged=Backend.Json().ConvertToType<Dictionary<string,string>>(response["preferences"]);foreach(var p in merged){string previous,current;bool had=preferencesBefore.TryGetValue(p.Key,out previous),has=preferences.Values.TryGetValue(p.Key,out current);if(had!=has||had&&previous!=current)continue;string[] parts=p.Key.Split('.');if(parts.Length==3&&parts[0]=="hotkeys"&&(parts[1]=="windows"||parts[1]=="macos")&&Preferences.Actions.Contains(parts[2])){try{preferences.Values[p.Key]=ShortcutBinding.Parse(p.Value,parts[1]).Text;}catch{Say("同步的快捷键格式无效。");}}else if(Preferences.IsCaptureKey(p.Key,p.Value))preferences.Values[p.Key]=p.Value;}preferences.Save();ApplyBindings();}
    selected=lib.Items.FirstOrDefault(x=>x.Id==selectedId);lib.Save();Refresh();RenderRail();if(quickHistory!=null&&quickHistory.IsVisible)RenderQuickHistory();Say(shortcutNotice.Length>0?shortcutNotice:"同步完成 · "+DateTime.Now.ToString("HH:mm"));
   }catch(Exception e){Say("同步未完成："+e.Message);}finally{syncing=false;}
  }
  Button AsyncButton(string label,Func<Task> action){var b=Btn(label,()=>{});b.Click+=async(s,e)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){MessageBox.Show(connectionsWindow??Window,ex.Message,"LumaShot");}finally{b.IsEnabled=true;}};return b;}
  TextBlock Help(string value){var t=T(value,12,"#83889B");t.TextWrapping=TextWrapping.Wrap;t.LineHeight=21;t.Margin=new Thickness(0,8,0,14);return t;}
  void Connections(){OpenSettings("ai");}
  internal static string ChatGPTStatus(Dictionary<string,object> state){
   object value;var session=state.TryGetValue("session",out value)?value as Dictionary<string,object>:null;
   string status=Backend.S(session,"status");if(status=="connected"){
    var identity=session.TryGetValue("identity",out value)?value as Dictionary<string,object>:null;string account=Backend.S(identity,"email");if(string.IsNullOrEmpty(account))account=Backend.S(session,"profileLabel");
    return "已登录"+(string.IsNullOrEmpty(account)?"":" · "+account);
   }
   if(status=="reauth_required")return "需要重新登录";
   if(status=="connecting")return "正在登录…";
   return "未登录";
  }
  FrameworkElement ConnectionsPage(Window win,string section){
   connectionsWindow=win;var ai=new StackPanel();var cloud=new StackPanel();
   var list=new StackPanel();var remoteList=new StackPanel();var notice=Help("正在读取配置…");var chatgptControls=new StackPanel{Visibility=Visibility.Collapsed};string running=null;
   var chatStatus=Help("");var chatNotice=Help("");chatNotice.Visibility=Visibility.Collapsed;
   var chatLoginArea=new Expander{IsExpanded=true,Header="登录 ChatGPT",Margin=new Thickness(0,6,0,8)};
   var driveEntry=new Expander{IsExpanded=true,Header="连接 Google Drive",Margin=new Thickness(0,4,0,12)};
   bool? chatConnectedBefore=null,driveConnectedBefore=null;
   Func<Task> refresh=null;refresh=async()=>{
    var state=await Connection().Call("state");UpdateAIAvailability(state);chatgptControls.Visibility=state.ContainsKey("chatgptInstalled")&&Convert.ToBoolean(state["chatgptInstalled"])?Visibility.Visible:Visibility.Collapsed;list.Children.Clear();remoteList.Children.Clear();string activeId=Backend.S(state,"activeAIId");
    chatStatus.Text=ChatGPTStatus(state);bool loggedIn=chatStatus.Text.StartsWith("已登录",StringComparison.Ordinal);chatStatus.Foreground=B(loggedIn?"#47715E":"#83889B");chatLoginArea.Header=loggedIn?"添加其他账号":"登录 ChatGPT";if(chatConnectedBefore!=loggedIn)chatLoginArea.IsExpanded=!loggedIn;chatConnectedBefore=loggedIn;
    bool driveConnected=Backend.Rows(state,"sync").Any(p=>Backend.S(p,"type")=="drive");driveEntry.Header=driveConnected?"添加其他 Google Drive 账号":"连接 Google Drive";if(driveConnectedBefore!=driveConnected)driveEntry.IsExpanded=!driveConnected;driveConnectedBefore=driveConnected;
    foreach(var p in Backend.Rows(state,"profiles")){
     string id=Backend.S(p,"id");bool enabled=id==activeId;var box=new StackPanel{Margin=new Thickness(14)};
     box.Children.Add(T((enabled?"●  ":"○  ")+Backend.S(p,"name"),14,enabled?"#7960CB":"#4B4F66"));box.Children.Add(Help((enabled?"当前启用 · ":"已保存 · ")+(string.IsNullOrEmpty(Backend.S(p,"model"))?"请从下方列表选择模型":Backend.S(p,"model"))+(Backend.S(p,"provider")=="chatgpt"?"\nUsing ChatGPT plan":"")));
     var options=Backend.Rows(p,"models").Select(x=>new ModelOption{Id=Backend.S(x,"id"),Name=Backend.S(x,"name")+"  ·  "+Backend.S(x,"id")}).ToList();
     var picker=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=Backend.S(p,"model"),MinHeight=34,Margin=new Thickness(0,0,0,10),IsEditable=false,ToolTip="从账号可用的图像模型中手动选择"};
     picker.SelectionChanged+=async(s,e)=>{if(picker.SelectedValue==null)return;picker.IsEnabled=false;try{UpdateAIAvailability(await Connection().Call("selectModel",new Dictionary<string,object>{{"id",id},{"model",picker.SelectedValue.ToString()}}));notice.Text="模型已保存。";}catch(Exception ex){notice.Text=ex.Message;}finally{picker.IsEnabled=true;}};box.Children.Add(picker);
     var row=new WrapPanel();var select=AsyncButton(enabled?"已启用":"启用此方案",async()=>{await Connection().Call("select",new Dictionary<string,object>{{"id",id}});await refresh();});select.IsEnabled=!enabled;row.Children.Add(select);
     row.Children.Add(AsyncButton("移除",async()=>{await Connection().Call("remove",new Dictionary<string,object>{{"id",id}});await refresh();}));box.Children.Add(row);list.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(9),Margin=new Thickness(0,0,0,10),Child=box});
    }
    if(list.Children.Count==0)list.Children.Add(Help("添加方案后，即可对截图提问。"));
    foreach(var p in Backend.Rows(state,"sync")){
     string id=Backend.S(p,"id");bool enabled=Convert.ToBoolean(p["enabled"]);var box=new StackPanel{Margin=new Thickness(12)};box.Children.Add(T(Backend.S(p,"name"),14,"#4B4F66"));box.Children.Add(Help(enabled?"已连接 · 自动同步已开启":"已连接 · 自动同步已暂停"));if(p.ContainsKey("url"))box.Children.Add(Help(Backend.S(p,"url")));
     var row=new WrapPanel();row.Children.Add(AsyncButton(enabled?"暂停自动同步":"恢复自动同步",async()=>{await Connection().Call("toggleSync",new Dictionary<string,object>{{"id",id}});await refresh();}));row.Children.Add(AsyncButton("断开本机连接",async()=>{await Connection().Call("removeSync",new Dictionary<string,object>{{"id",id}});await refresh();}));box.Children.Add(row);remoteList.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(9),Margin=new Thickness(0,0,0,10),Child=box});
    }
    notice.Text="Key 安全保存在本机。";
   };
   connectionRefresh=refresh;
   ai.Children.Add(PageHeader("AI 服务","保存多个方案，每次使用一个。"));ai.Children.Add(list);
   var apiCard=Section("使用 API Key","填入 Key，再从官方模型列表中选择。");var provider=new ComboBox{ItemsSource=new[]{"OpenAI","Gemini"},SelectedIndex=0,Height=40,Margin=new Thickness(0,0,0,12)};var key=new PasswordBox{Height=40,Margin=new Thickness(0,5,0,14),ToolTip="API Key"};System.Windows.Automation.AutomationProperties.SetName(key,"API Key");apiCard.Children.Add(provider);apiCard.Children.Add(T("API Key",12,"#626779"));apiCard.Children.Add(key);
   var verify=AsyncButton("验证并保存",async()=>{if(string.IsNullOrWhiteSpace(key.Password)){notice.Text="请输入 API Key";key.Focus();return;}notice.Text="正在读取模型目录…";string value=key.Password;key.Clear();await Connection().Call("addKey",new Dictionary<string,object>{{"provider",provider.SelectedIndex==0?"openai":"gemini"},{"key",value}});await refresh();});verify.Style=(Style)Window.FindResource("Accent");verify.HorizontalAlignment=HorizontalAlignment.Left;apiCard.Children.Add(verify);apiCard.Children.Add(notice);apiCard.Children.Add(Help("Key 保存在本机，API 用量由对应服务计费。"));ai.Children.Add(SettingCard(apiCard));
   var chatCard=Section("ChatGPT 账号","使用 ChatGPT 订阅 · 非商业实验组件");chatCard.Children.Add(chatStatus);var cancelLogin=AsyncButton("取消登录",async()=>{if(running!=null)await Connection().Cancel(running);});cancelLogin.Visibility=Visibility.Collapsed;
   var loginFields=new StackPanel{Margin=new Thickness(0,10,0,0)};loginFields.Children.Add(AsyncButton("Continue with ChatGPT",async()=>{running=Guid.NewGuid().ToString("N");cancelLogin.Visibility=Visibility.Visible;chatNotice.Visibility=Visibility.Visible;chatNotice.Text="请在浏览器中完成登录…";try{await Connection().Call("signIn",null,null,running);chatNotice.Text="登录成功";}catch(Exception ex){chatNotice.Text=ex.Message;}finally{running=null;cancelLogin.Visibility=Visibility.Collapsed;}await refresh();}));loginFields.Children.Add(cancelLogin);loginFields.Children.Add(chatNotice);chatLoginArea.Content=loginFields;chatCard.Children.Add(chatLoginArea);chatCard.Children.Add(Btn("使用量设置",()=>Process.Start(new ProcessStartInfo("https://chatgpt.com/settings/usage"){UseShellExecute=true})));chatgptControls.Children.Add(SettingCard(chatCard));ai.Children.Add(chatgptControls);
   var refreshModels=AsyncButton("刷新模型列表",async()=>{await Connection().Call("refreshModel");await refresh();});refreshModels.HorizontalAlignment=HorizontalAlignment.Left;ai.Children.Add(refreshModels);
   cloud.Children.Add(PageHeader("云同步","自动合并截图、视频、历史和配置。"));var cloudNotice=Help("每分钟自动检查同步");cloud.Children.Add(cloudNotice);cloud.Children.Add(remoteList);
   var driveCard=Section("Google Drive","文件保存到 Drive 的 LumaShot 文件夹。");var cancelDrive=AsyncButton("取消授权",async()=>{if(running!=null)await Connection().Cancel(running);});cancelDrive.Visibility=Visibility.Collapsed;var driveActions=new WrapPanel();driveActions.Children.Add(AsyncButton("连接 Google Drive",async()=>{running=Guid.NewGuid().ToString("N");cancelDrive.Visibility=Visibility.Visible;cloudNotice.Text="请在浏览器中完成授权…";try{await Connection().Call("addDrive",null,null,running);await refresh();await Sync(true);cloudNotice.Text=status.Text;}finally{running=null;cancelDrive.Visibility=Visibility.Collapsed;}}));driveActions.Children.Add(cancelDrive);driveCard.Children.Add(driveActions);driveEntry.Content=SettingCard(driveCard);cloud.Children.Add(driveEntry);
   var dav=Section("WebDAV","连接你的云盘或 NAS。");var url=new TextBox{Height=40,Margin=new Thickness(0,5,0,13),ToolTip="https://服务器/remote.php/dav/files/用户名/"};var user=new TextBox{Height=40,Margin=new Thickness(0,5,0,13)};var password=new PasswordBox{Height=40,Margin=new Thickness(0,5,0,14)};System.Windows.Automation.AutomationProperties.SetName(url,"WebDAV 地址");System.Windows.Automation.AutomationProperties.SetName(user,"用户名");System.Windows.Automation.AutomationProperties.SetName(password,"应用密码");dav.Children.Add(T("服务器地址",12,"#626779"));dav.Children.Add(url);dav.Children.Add(T("用户名",12,"#626779"));dav.Children.Add(user);dav.Children.Add(T("应用密码",12,"#626779"));dav.Children.Add(password);
   var connectDav=AsyncButton("连接 WebDAV",async()=>{if(string.IsNullOrWhiteSpace(url.Text)){cloudNotice.Text="请输入服务器地址";url.Focus();return;}string secret=password.Password;password.Clear();await Connection().Call("addWebdav",new Dictionary<string,object>{{"url",url.Text.Trim()},{"user",user.Text.Trim()},{"password",secret}});await refresh();await Sync(true);cloudNotice.Text=status.Text;});connectDav.HorizontalAlignment=HorizontalAlignment.Left;dav.Children.Add(connectDav);cloud.Children.Add(SettingCard(dav));var sync=AsyncButton("立即同步",async()=>{await Sync(true);cloudNotice.Text=status.Text;});sync.HorizontalAlignment=HorizontalAlignment.Left;cloud.Children.Add(sync);
   win.Closed+=async(s,e)=>{connectionsWindow=null;connectionRefresh=null;if(running!=null)try{await Connection().Cancel(running);}catch{}};win.Dispatcher.BeginInvoke(new Action(async()=>{try{await refresh();}catch(Exception ex){notice.Text=ex.Message;}}));return ScrollPage(section=="cloud"?cloud:ai);
  }
  Func<Task> connectionRefresh;
  Task RefreshConnections(StackPanel a,StackPanel b,TextBlock t){return connectionRefresh==null?Task.FromResult(0):connectionRefresh();}
  async void ExtractText(){if(selected==null||ocrBusy)return;Shot shot=selected;ocrBusy=true;try{Say("正在本地识别文字…");shot.OcrText=await Task.Run(()=>LocalOcr.Read(lib,shot));shot.OcrDone=true;lib.Save();Grid body;var w=DialogShell("提取文字",720,580,out body);var p=new DockPanel{Margin=new Thickness(26,4,26,22)};var heading=PageHeader("提取文字","识别结果已加入历史搜索。");DockPanel.SetDock(heading,Dock.Top);p.Children.Add(heading);var copy=Btn("复制文字",async()=>{try{await CaptureClipboard.CopyText(shot.OcrText??"");}catch(Exception ex){Say(ex.Message);}});DockPanel.SetDock(copy,Dock.Bottom);p.Children.Add(copy);p.Children.Add(new TextBox{Text=shot.OcrText,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(14)});body.Children.Add(p);w.Show();Say("文字已收录，可在历史搜索中检索。");}catch(Exception e){Say(e.Message);}finally{ocrBusy=false;}}
  async Task IndexText(){if(ocrBusy||engine.Busy||maintenance)return;var shot=lib.Items.FirstOrDefault(x=>!x.Deleted&&x.Kind!="video"&&!x.OcrDone);if(shot==null)return;ocrBusy=true;try{shot.OcrText=await Task.Run(()=>LocalOcr.Read(lib,shot));shot.OcrDone=true;lib.Save();}catch{shot.OcrDone=true;lib.Save();}finally{ocrBusy=false;}}
  void Pin(){if(selected!=null)Preview(lib.Full(selected.File),selected.Title,true);}
 }
 public class ModelOption {public string Id{get;set;} public string Name{get;set;}}
}
