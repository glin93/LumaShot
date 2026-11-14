using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LumaShot {
 public class Backend:IDisposable {
  public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=100};}
  class Pending { public TaskCompletionSource<Dictionary<string,object>> Task=new TaskCompletionSource<Dictionary<string,object>>(); public Action<string> Delta; }
  Process process;StreamWriter input;Dictionary<string,Pending> pending=new Dictionary<string,Pending>();object gate=new object();public event Action<string> Browser;
  public string Vault;
  public static string VaultPath(string root){
   string id;using(var hash=SHA256.Create())id=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToLowerInvariant()))).Replace("-","").Substring(0,20);
   return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaShot","Connections",id);
  }
  public Backend(string root){
   Vault=VaultPath(root);
   string app=AppDomain.CurrentDomain.BaseDirectory;
   process=new Process{StartInfo=new ProcessStartInfo(Path.Combine(app,"runtime","node.exe"),"\""+Path.Combine(app,"backend","main.mjs")+"\" \""+root+"\" \""+Vault+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8},EnableRaisingEvents=true};
   process.StartInfo.EnvironmentVariables.Remove("OPENAI_LOG");process.StartInfo.EnvironmentVariables.Remove("OPENAI_CUSTOM_HEADERS");
   process.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Receive(e.Data);};process.ErrorDataReceived+=(s,e)=>{};
   process.Exited+=(s,e)=>FailAll("后台连接已退出，请重新启动应用。");
   process.Start();input=new StreamWriter(process.StandardInput.BaseStream,new UTF8Encoding(false)){NewLine="\n",AutoFlush=true};process.BeginOutputReadLine();process.BeginErrorReadLine();
  }
  void Receive(string line){try{
   var m=Json().Deserialize<Dictionary<string,object>>(line);
   if(S(m,"event")=="browser"){if(Browser!=null)Browser(S(m,"url"));return;}
   string id=S(m,"requestId");Pending p;lock(gate){if(!pending.TryGetValue(id,out p))return;if(S(m,"event")!="delta")pending.Remove(id);}
   if(S(m,"event")=="delta"){if(p.Delta!=null)p.Delta(S(m,"text"));}
   else if(m.ContainsKey("error"))p.Task.TrySetException(new IOException(S(m,"error")));
   else p.Task.TrySetResult(m["result"] as Dictionary<string,object> ?? new Dictionary<string,object>());
  }catch{FailAll("后台响应格式异常，请重新启动应用。");}}
  void FailAll(string message){lock(gate){foreach(var p in pending.Values)p.Task.TrySetException(new IOException(message));pending.Clear();}}
  public Task<Dictionary<string,object>> Call(string method,Dictionary<string,object> args=null,Action<string> delta=null,string requestId=null){
   var data=args??new Dictionary<string,object>();string id=requestId??Guid.NewGuid().ToString("N");data["method"]=method;data["requestId"]=id;var p=new Pending{Delta=delta};
   lock(gate){if(process.HasExited)throw new IOException("后台连接已退出，请重新启动应用。");pending.Add(id,p);try{input.WriteLine(Json().Serialize(data));}catch{pending.Remove(id);throw;}}return p.Task.Task;
  }
  public async Task Cancel(string id){await Call("cancel",new Dictionary<string,object>{{"target",id}});}
  public static string S(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)&&v!=null?Convert.ToString(v):"";}
  public static List<Dictionary<string,object>> Rows(Dictionary<string,object> d,string key){object v;if(!d.TryGetValue(key,out v)||v==null)return new List<Dictionary<string,object>>();return Json().ConvertToType<List<Dictionary<string,object>>>(v);}
  public void Dispose(){try{input.Close();if(!process.WaitForExit(2000))process.Kill();process.Dispose();}catch{}FailAll("应用已退出。");}
 }
}
