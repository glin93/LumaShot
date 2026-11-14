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
    foreach(var remote in records){var local=lib.Items.FirstOrDefault(x=>x.Id==remote.Id);if(local==null){lib.Items.Add(remote);count++;}
     else if(before.ContainsKey(local.Id)&&Backend.Json().Serialize(local)==before[local.Id]){remote.SourceKey=local.SourceKey;remote.OcrDone=local.OcrDone||!string.IsNullOrEmpty(remote.OcrText);int index=lib.Items.IndexOf(local);lib.Items[index]=remote;count++;}}
    if(response.ContainsKey("preferences")){var merged=Backend.Json().ConvertToType<Dictionary<string,string>>(response["preferences"]);foreach(var p in merged){string previous,current;bool had=preferencesBefore.TryGetValue(p.Key,out previous),has=preferences.Values.TryGetValue(p.Key,out current);if(had!=has||had&&previous!=current)continue;string[] parts=p.Key.Split('.');if(parts.Length==3&&parts[0]=="hotkeys"&&(parts[1]=="windows"||parts[1]=="macos")&&Preferences.Actions.Contains(parts[2])){try{preferences.Values[p.Key]=ShortcutBinding.Parse(p.Value,parts[1]).Text;}catch{Say("同步的快捷键格式无效。");}}else if(Preferences.IsCaptureKey(p.Key,p.Value))preferences.Values[p.Key]=p.Value;}preferences.Save();ApplyBindings();}
    lib.Save();Refresh();RenderRail();if(quickHistory!=null&&quickHistory.IsVisible)RenderQuickHistory();Say(shortcutNotice.Length>0?shortcutNotice:"同步完成 · "+DateTime.Now.ToString("HH:mm"));
   }catch(Exception e){Say("同步未完成："+e.Message);}finally{syncing=false;}
  }
  Button AsyncButton(string label,Func<Task> action){var b=Btn(label,()=>{});b.Click+=async(s,e)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){MessageBox.Show(connectionsWindow??Window,ex.Message,"LumaShot");}finally{b.IsEnabled=true;}};return b;}
  TextBlock Help(string value){var t=T(value,12,"#83889B");t.TextWrapping=TextWrapping.Wrap;t.LineHeight=21;t.Margin=new Thickness(0,8,0,14);return t;}
  void Connections(){
   if(connectionsWindow!=null){connectionsWindow.Activate();return;}
   var win=new Window{Title="LumaShot · AI 与同步",Owner=Window,Width=720,Height=790,MinWidth=620,MinHeight=600,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#F8F9FC"),FontFamily=Window.FontFamily};win.Resources=Window.Resources;connectionsWindow=win;
   var tabs=new TabControl{Margin=new Thickness(24)};var ai=new StackPanel{Margin=new Thickness(22)};var cloud=new StackPanel{Margin=new Thickness(22)};
   tabs.Items.Add(new TabItem{Header="  Ask AI  ",Content=new ScrollViewer{Content=ai,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});tabs.Items.Add(new TabItem{Header="  云同步  ",Content=new ScrollViewer{Content=cloud,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});win.Content=tabs;
   var list=new StackPanel();var remoteList=new StackPanel();var notice=Help("正在读取配置…");var chatgptControls=new StackPanel{Visibility=Visibility.Collapsed};string running=null;
   Func<Task> refresh=async()=>{
    var state=await Connection().Call("state");UpdateAIAvailability(state);chatgptControls.Visibility=state.ContainsKey("chatgptInstalled")&&Convert.ToBoolean(state["chatgptInstalled"])?Visibility.Visible:Visibility.Collapsed;list.Children.Clear();remoteList.Children.Clear();string activeId=Backend.S(state,"activeAIId");
    foreach(var p in Backend.Rows(state,"profiles")){
     string id=Backend.S(p,"id");bool enabled=id==activeId;var box=new StackPanel{Margin=new Thickness(14)};
     box.Children.Add(T((enabled?"●  ":"○  ")+Backend.S(p,"name"),14,enabled?"#7960CB":"#4B4F66"));box.Children.Add(Help((enabled?"当前启用 · ":"已保存 · ")+(string.IsNullOrEmpty(Backend.S(p,"model"))?"请从下方列表选择模型":Backend.S(p,"model"))+(Backend.S(p,"provider")=="chatgpt"?"\nUsing ChatGPT plan":"")));
     var options=Backend.Rows(p,"models").Select(x=>new ModelOption{Id=Backend.S(x,"id"),Name=Backend.S(x,"name")+"  ·  "+Backend.S(x,"id")}).ToList();
     var picker=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=Backend.S(p,"model"),MinHeight=34,Margin=new Thickness(0,0,0,10),IsEditable=false,ToolTip="从账号可用的图像模型中手动选择"};
     picker.SelectionChanged+=async(s,e)=>{if(picker.SelectedValue==null)return;picker.IsEnabled=false;try{UpdateAIAvailability(await Connection().Call("selectModel",new Dictionary<string,object>{{"id",id},{"model",picker.SelectedValue.ToString()}}));notice.Text="模型已保存。";}catch(Exception ex){notice.Text=ex.Message;}finally{picker.IsEnabled=true;}};box.Children.Add(picker);
     var row=new WrapPanel();var select=AsyncButton(enabled?"已启用":"启用此方案",async()=>{await Connection().Call("select",new Dictionary<string,object>{{"id",id}});await RefreshConnections(list,remoteList,notice);});select.IsEnabled=!enabled;row.Children.Add(select);
     row.Children.Add(AsyncButton("移除",async()=>{await Connection().Call("remove",new Dictionary<string,object>{{"id",id}});await RefreshConnections(list,remoteList,notice);}));box.Children.Add(row);list.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(9),Margin=new Thickness(0,0,0,10),Child=box});
    }
    if(list.Children.Count==0)list.Children.Add(Help("添加方案后，即可对截图提问。"));
    foreach(var p in Backend.Rows(state,"sync")){
     string id=Backend.S(p,"id");bool enabled=Convert.ToBoolean(p["enabled"]);var box=new StackPanel{Margin=new Thickness(12)};box.Children.Add(T((enabled?"●  ":"○  ")+Backend.S(p,"name"),14,"#4B4F66"));if(p.ContainsKey("url"))box.Children.Add(Help(Backend.S(p,"url")));
     var row=new WrapPanel();row.Children.Add(AsyncButton(enabled?"暂停自动同步":"恢复自动同步",async()=>{await Connection().Call("toggleSync",new Dictionary<string,object>{{"id",id}});await RefreshConnections(list,remoteList,notice);}));row.Children.Add(AsyncButton("断开本机连接",async()=>{await Connection().Call("removeSync",new Dictionary<string,object>{{"id",id}});await RefreshConnections(list,remoteList,notice);}));box.Children.Add(row);remoteList.Children.Add(new Border{Background=Brushes.White,CornerRadius=new CornerRadius(9),Margin=new Thickness(0,0,0,10),Child=box});
    }
    notice.Text="Key 安全保存在本机。";
   };
   connectionRefresh=refresh;
   ai.Children.Add(T("AI 服务",22,"#34364C"));ai.Children.Add(Help("保存多个方案，每次使用一个。"));ai.Children.Add(list);
   ai.Children.Add(T("添加 API Key",15,"#4B4F66"));var provider=new ComboBox{ItemsSource=new[]{"OpenAI","Gemini"},SelectedIndex=0,Margin=new Thickness(0,10,0,8),Height=34};var key=new PasswordBox{Height=36,Padding=new Thickness(8),Margin=new Thickness(0,0,0,8)};ai.Children.Add(provider);ai.Children.Add(key);
   ai.Children.Add(AsyncButton("验证 Key 并保存",async()=>{notice.Text="正在读取此账号的官方模型目录…";string value=key.Password;key.Clear();await Connection().Call("addKey",new Dictionary<string,object>{{"provider",provider.SelectedIndex==0?"openai":"gemini"},{"key",value}});await refresh();}));
   ai.Children.Add(Help("填入 Key，再从列表选择模型。API 用量由对应服务计费。"));
   chatgptControls.Children.Add(AsyncButton("Continue with ChatGPT",async()=>{running=Guid.NewGuid().ToString("N");notice.Text="请在系统浏览器完成官方 ChatGPT 授权…";try{await Connection().Call("signIn",null,null,running);await refresh();}finally{running=null;}}));
   chatgptControls.Children.Add(Help("使用 ChatGPT 账号登录 · 非商业实验组件"));ai.Children.Add(chatgptControls);
   var aiTools=new WrapPanel();chatgptControls.Children.Add(AsyncButton("取消登录",async()=>{if(running!=null)await Connection().Cancel(running);}));aiTools.Children.Add(AsyncButton("刷新官方模型目录",async()=>{await Connection().Call("refreshModel");await refresh();}));chatgptControls.Children.Add(Btn("ChatGPT 使用量设置",()=>Process.Start(new ProcessStartInfo("https://chatgpt.com/settings/usage"){UseShellExecute=true})));ai.Children.Add(aiTools);ai.Children.Add(notice);
   cloud.Children.Add(T("云同步",22,"#34364C"));cloud.Children.Add(Help("连接后自动同步截图、视频和历史，每分钟检查一次。编辑会保留为新截图。"));cloud.Children.Add(remoteList);
   cloud.Children.Add(AsyncButton("连接 Google Drive",async()=>{running=Guid.NewGuid().ToString("N");try{await Connection().Call("addDrive",null,null,running);await refresh();await Sync(true);}finally{running=null;}}));cloud.Children.Add(Help("在浏览器中授权，文件保存到 Drive 的 LumaShot 文件夹。"));
   cloud.Children.Add(T("连接 WebDAV",15,"#4B4F66"));cloud.Children.Add(Help("填写服务器地址、用户名和应用密码。"));var url=new TextBox{Height=34,Margin=new Thickness(0,0,0,8),ToolTip="https://服务器/remote.php/dav/files/用户名/"};var user=new TextBox{Height=34,Margin=new Thickness(0,0,0,8),ToolTip="用户名"};var password=new PasswordBox{Height=34,Margin=new Thickness(0,0,0,8),ToolTip="应用密码"};cloud.Children.Add(T("WebDAV 地址",11,"#83889B"));cloud.Children.Add(url);cloud.Children.Add(T("用户名",11,"#83889B"));cloud.Children.Add(user);cloud.Children.Add(T("应用密码",11,"#83889B"));cloud.Children.Add(password);
   cloud.Children.Add(AsyncButton("验证并开启自动同步",async()=>{string secret=password.Password;password.Clear();await Connection().Call("addWebdav",new Dictionary<string,object>{{"url",url.Text.Trim()},{"user",user.Text.Trim()},{"password",secret}});await refresh();await Sync(true);}));
   cloud.Children.Add(AsyncButton("立即同步",async()=>await Sync(true)));cloud.Children.Add(AsyncButton("取消当前授权",async()=>{if(running!=null)await Connection().Cancel(running);}));cloud.Children.Add(Help("首次连接将同步现有历史。断开连接会保留云端文件。"));
   win.Closed+=async(s,e)=>{connectionsWindow=null;connectionRefresh=null;if(running!=null)try{await Connection().Cancel(running);}catch{}};win.Loaded+=async(s,e)=>{try{await refresh();}catch(Exception ex){notice.Text=ex.Message;}};win.Show();
  }
  Func<Task> connectionRefresh;
  Task RefreshConnections(StackPanel a,StackPanel b,TextBlock t){return connectionRefresh==null?Task.FromResult(0):connectionRefresh();}
  async void ExtractText(){if(selected==null||ocrBusy)return;Shot shot=selected;ocrBusy=true;try{Say("正在本地识别文字…");shot.OcrText=await Task.Run(()=>LocalOcr.Read(lib,shot));shot.OcrDone=true;lib.Save();var w=new Window{Title="提取文字 · 可搜索",Owner=Window,Width=650,Height=480,WindowStartupLocation=WindowStartupLocation.CenterOwner};var p=new DockPanel{Margin=new Thickness(22)};var copy=Btn("复制文字",()=>{Clipboard.SetText(shot.OcrText??"");});DockPanel.SetDock(copy,Dock.Bottom);p.Children.Add(copy);p.Children.Add(new TextBox{Text=shot.OcrText,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});w.Content=p;w.Show();Say("文字已收录，可在历史搜索中检索。");}catch(Exception e){Say(e.Message);}finally{ocrBusy=false;}}
  async Task IndexText(){if(ocrBusy||engine.Busy||maintenance)return;var shot=lib.Items.FirstOrDefault(x=>!x.Deleted&&x.Kind!="video"&&!x.OcrDone);if(shot==null)return;ocrBusy=true;try{shot.OcrText=await Task.Run(()=>LocalOcr.Read(lib,shot));shot.OcrDone=true;lib.Save();}catch{shot.OcrDone=true;lib.Save();}finally{ocrBusy=false;}}
  void Pin(){if(selected!=null)Preview(lib.Full(selected.File),selected.Title,true);}
  async void Ask(){
   if(selected==null)return;Shot shot=selected;Dictionary<string,object> state;
   try{state=await Connection().Call("state");UpdateAIAvailability(state);}catch(Exception ex){Say(ex.Message);return;}
   string profileId=Backend.S(state,"activeAIId");var profile=Backend.Rows(state,"profiles").FirstOrDefault(x=>Backend.S(x,"id")==profileId);if(profile==null||string.IsNullOrEmpty(Backend.S(profile,"model"))){Connections();Say("请先添加 AI 方案，并从列表选择模型。");return;}
   var win=new Window{Title="Ask AI · "+shot.Title,Owner=Window,Width=790,Height=780,MinWidth=600,MinHeight=600,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#F8F9FC"),FontFamily=Window.FontFamily};win.Resources=Window.Resources;var dock=new DockPanel{Margin=new Thickness(25)};win.Content=dock;
   var header=new StackPanel();header.Children.Add(T(Backend.S(profile,"name"),18,"#4B4F66"));header.Children.Add(Help(Backend.S(profile,"model")+(Backend.S(profile,"provider")=="chatgpt"?" · Using ChatGPT plan":"")));var media=Help("正在检查所选媒体…");header.Children.Add(media);DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
   var bottom=new StackPanel();var prompt=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=85,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,10,0,10),Text="请总结这张截图的内容。"};if(shot.Kind=="video")prompt.Text="请总结这些视频采样画面，并标出关键时间点。";bottom.Children.Add(prompt);var buttons=new WrapPanel();bottom.Children.Add(buttons);DockPanel.SetDock(bottom,Dock.Bottom);dock.Children.Add(bottom);
   var answer=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(15),FontSize=14,Background=Brushes.White,BorderThickness=new Thickness(0)};dock.Children.Add(answer);
   var messages=new List<Dictionary<string,object>>();try{if(!string.IsNullOrWhiteSpace(shot.AiHistory))messages=Backend.Json().Deserialize<List<Dictionary<string,object>>>(shot.AiHistory);}catch{}
   Action render=()=>{answer.Text=string.Join("\n\n",messages.Select(x=>(Backend.S(x,"role")=="user"?"你":"AI")+" · "+Backend.S(x,"provider")+"\n"+Backend.S(x,"content")));answer.ScrollToEnd();};render();string request=null;bool ready=false;
   var send=Btn("发送",()=>{},true);send.IsEnabled=false;buttons.Children.Add(send);
   send.Click+=async(s,e)=>{
    if(!ready||request!=null||string.IsNullOrWhiteSpace(prompt.Text))return;string question=prompt.Text.Trim();request=Guid.NewGuid().ToString("N");send.IsEnabled=false;string partial="";string previous=answer.Text;answer.Text=previous+"\n\n你\n"+question+"\n\nAI\n";
    try{
     var history=messages.Where(x=>Backend.S(x,"profileId")==profileId).Select(x=>new Dictionary<string,object>{{"role",Backend.S(x,"role")},{"content",Backend.S(x,"content")}}).ToArray();
     var result=await Connection().Call("ask",new Dictionary<string,object>{{"profileId",profileId},{"file",shot.File},{"isVideo",shot.Kind=="video"||shot.Kind=="gif"},{"prompt",question},{"history",history}},delta=>Window.Dispatcher.BeginInvoke(new Action(()=>{partial+=delta;answer.Text=previous+"\n\n你\n"+question+"\n\nAI\n"+partial;answer.ScrollToEnd();})),request);
     foreach(var entry in new[]{new[]{"user",question},new[]{"assistant",Backend.S(result,"text")}})messages.Add(new Dictionary<string,object>{{"role",entry[0]},{"content",entry[1]},{"profileId",profileId},{"provider",Backend.S(profile,"name")},{"model",Backend.S(result,"model")},{"created",DateTime.UtcNow.ToString("o")}});
     shot.AiHistory=Backend.Json().Serialize(messages);var current=lib.Items.FirstOrDefault(x=>x.Id==shot.Id);if(current!=null)current.AiHistory=shot.AiHistory;lib.Save();render();prompt.Clear();
    }catch(Exception ex){answer.AppendText("\n\n[未完成] "+ex.Message);}finally{request=null;send.IsEnabled=ready;}
   };
   buttons.Children.Add(AsyncButton("停止",async()=>{if(request!=null)await Connection().Cancel(request);}));buttons.Children.Add(Btn("复制对话",()=>{if(!string.IsNullOrEmpty(answer.Text))Clipboard.SetText(answer.Text);}));
   win.Closed+=async(s,e)=>{if(request!=null)try{await Connection().Cancel(request);}catch{}};
   win.Show();
   try{
    if(shot.Kind=="video"||shot.Kind=="gif"){var info=await Connection().Call("inspect",new Dictionary<string,object>{{"file",shot.File}});double duration=Convert.ToDouble(info["duration"]);media.Text="视频 "+duration.ToString("0.0")+" 秒 · 原始码率 "+Backend.S(info,"bitrateKbps")+" kb/s\n发送最多 24 张带时间戳的采样画面，不含音频。";ready=duration>0&&duration<=600;if(!ready)media.Text+="\n请先裁剪为 10 分钟以内的片段。";}
    else {media.Text="发送当前截图和本方案下的近期对话，回答保存到截图历史。";ready=true;}
    send.IsEnabled=ready;
   }catch(Exception ex){media.Text=ex.Message;}
  }
 }
 public class ModelOption {public string Id{get;set;} public string Name{get;set;}}
}
