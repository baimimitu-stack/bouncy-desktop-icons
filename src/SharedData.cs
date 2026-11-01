using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace BouncyIcons {
    // Migration copies user content first. Legacy data is never deleted by the app.
    public static class SharedData {
        public static string Prepare(string root) {
            string shared=Path.Combine(root,"shared"),index=Path.Combine(shared,"entries.json");
            Directory.CreateDirectory(shared);
            if(File.Exists(index))return shared;
            var json=new JavaScriptSerializer();var entries=new List<Entry>();var ids=new HashSet<string>();
            var receipts=new List<object>();
            foreach(string mode in new[]{"lite","standard","pro"}) {
                string old=Path.Combine(root,mode),oldIndex=Path.Combine(old,"entries.json");
                if(!File.Exists(oldIndex))continue;
                var source=json.Deserialize<List<Entry>>(File.ReadAllText(oldIndex));
                if(source==null)throw new IOException("旧挂件列表无法读取："+mode);
                foreach(Entry entry in source) {
                    if(entry==null||entry.Id==null||!Regex.IsMatch(entry.Id,"^[a-f0-9]{32}$"))throw new IOException("旧挂件标识不完整："+mode);
                    string id=entry.Id;if(!ids.Add(id)){id=Guid.NewGuid().ToString("N");ids.Add(id);}
                    string from=Path.Combine(old,entry.Id),to=Path.Combine(shared,id);Directory.CreateDirectory(to);
                    string settings=Path.Combine(from,"settings.json");
                    if(!File.Exists(settings))throw new IOException("旧挂件设置缺失："+entry.Name);
                    Preferences cfg=json.Deserialize<Preferences>(File.ReadAllText(settings));
                    if(cfg==null)throw new IOException("旧挂件设置无法读取："+entry.Name);
                    foreach(string image in Directory.GetFiles(from)) {
                        if(Regex.IsMatch(Path.GetExtension(image),"^\\.(gif|png|jpg|jpeg|bmp|ico)$",RegexOptions.IgnoreCase))File.Copy(image,Path.Combine(to,Path.GetFileName(image)),true);
                    }
                    if(!String.IsNullOrEmpty(cfg.Gif)&&File.Exists(cfg.Gif)) {
                        string dest=Path.Combine(to,Path.GetFileName(cfg.Gif));File.Copy(cfg.Gif,dest,true);cfg.Gif=dest;
                    }
                    AtomicWrite(Path.Combine(to,"settings.json"),json.Serialize(cfg));
                    entries.Add(new Entry{Name=entry.Name,Id=id});
                    receipts.Add(new {Mode=mode,OldId=entry.Id,NewId=id});
                }
            }
            AtomicWrite(Path.Combine(shared,"migration.json"),json.Serialize(receipts));
            AtomicWrite(index,json.Serialize(entries));
            return shared;
        }
        public static void AtomicWrite(string path,string text) {
            string tmp=path+".tmp";File.WriteAllText(tmp,text);
            if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
        }
    }
}
