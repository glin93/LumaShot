using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;

namespace LumaShot {
 public static class BackupRestore {
  public static int Merge(Library lib,string backup) {
   using(var z=ZipFile.OpenRead(backup)){
    if(z.Entries.Count>100000||z.Entries.Sum(x=>x.Length)>10L*1024*1024*1024)throw new IOException("备份过大，请手动解压并检查。");
    foreach(var e in z.Entries)if(!string.IsNullOrEmpty(e.Name))lib.Full(e.FullName.Replace('/',Path.DirectorySeparatorChar));
    var index=z.GetEntry("library.json");if(index==null||index.Length>50*1024*1024)throw new IOException("这不是有效的 LumaShot 资料库备份。");
    List<Shot> records;using(var s=index.Open())records=(List<Shot>)new DataContractJsonSerializer(typeof(List<Shot>)).ReadObject(s);
    var additions=records.Where(x=>!lib.Items.Any(y=>x.Id==y.Id)).ToList();
    foreach(var x in additions){Guid id;if(!Guid.TryParseExact(x.Id,"N",out id))throw new IOException("备份中的记录编号无效。");foreach(var path in new[]{x.File,x.Original,x.Thumb}.Distinct()){lib.Full(path);string normalized=path.Replace('\\','/');if(!normalized.StartsWith("captures/")&&!normalized.StartsWith("thumbs/"))throw new IOException("备份中包含异常文件路径。");if(!z.Entries.Any(e=>e.FullName.Replace('\\','/')==normalized))throw new IOException("备份缺少截图文件："+path);}}
    var paths=additions.SelectMany(x=>new[]{x.File,x.Original,x.Thumb}).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    foreach(var path in paths)if(File.Exists(lib.Full(path)))throw new IOException("发现文件冲突，原有文件未覆盖："+path);
    var copied=new List<string>();try{foreach(var path in paths){var e=z.Entries.First(v=>v.FullName.Replace('\\','/')==path.Replace('\\','/'));string dest=lib.Full(path);Directory.CreateDirectory(Path.GetDirectoryName(dest));copied.Add(dest);e.ExtractToFile(dest,false);}lib.Items.AddRange(additions);lib.Save();return additions.Count;}
    catch{foreach(var x in additions)lib.Items.Remove(x);foreach(var file in copied)if(File.Exists(file))File.Delete(file);throw;}
   }
  }
 }
}
