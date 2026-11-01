using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace BouncyIcons {
    public sealed class ReactionClip {
        public string Id,Name,Kind,Text="";
        public int Seconds=4;
        public override string ToString(){return Name+" · "+Seconds+" 秒";}
    }
    public sealed class ReactionLibrary {
        public static readonly string[] Keys={"Happy","Shy","Curious","Grumpy","Greeting","Argument","Calm"};
        public static readonly string[] Names={"开心","害羞","好奇","小脾气","打招呼","拌嘴","悠闲"};
        public readonly List<ReactionClip> Clips=new List<ReactionClip>();
        readonly string directory,index;
        public string LoadError {get;private set;}
        readonly Random random=new Random();
        public ReactionLibrary(string profile){
            directory=Path.Combine(profile,"reactions");index=Path.Combine(directory,"library.json");
            try{if(File.Exists(index)){
                Clips.AddRange(new JavaScriptSerializer().Deserialize<List<ReactionClip>>(File.ReadAllText(index))??new List<ReactionClip>());
                Clips.RemoveAll(c=>c==null||!ValidId(c.Id)||!Keys.Contains(c.Kind));
                foreach(var c in Clips)c.Seconds=Math.Max(1,Math.Min(30,c.Seconds));
            }}catch{LoadError="素材列表无法读取，原文件已保留。请先恢复 library.json 后重试。";Clips.Clear();}
        }
        static bool ValidId(string id){return id!=null&&Regex.IsMatch(id,"^[a-f0-9]{32}$");}
        public string FileFor(ReactionClip clip){if(clip==null||!ValidId(clip.Id))throw new ArgumentException("无效素材");return Path.Combine(directory,clip.Id+".gif");}
        public ReactionClip Add(string source,string kind){
            if(LoadError!=null)throw new IOException(LoadError);
            if(!Keys.Contains(kind))throw new ArgumentException("未知反应分类");
            if(new FileInfo(source).Length>20*1024*1024)throw new IOException("每张 GIF 最大 20 MB");
            byte[] bytes=File.ReadAllBytes(source);
            using(var stream=new MemoryStream(bytes))using(Image image=Image.FromStream(stream)){
                if(image.RawFormat.Guid!=ImageFormat.Gif.Guid||image.Width>4096||image.Height>4096)throw new IOException("请选择有效 GIF，宽高不超过 4096");
            }
            var clip=new ReactionClip{Id=Guid.NewGuid().ToString("N"),Name=Path.GetFileNameWithoutExtension(source),Kind=kind};
            Directory.CreateDirectory(directory);File.WriteAllBytes(FileFor(clip),bytes);Clips.Add(clip);
            try{Save();}catch{Clips.Remove(clip);File.Delete(FileFor(clip));throw;}return clip;
        }
        public void Save(){if(LoadError!=null)throw new IOException(LoadError);Directory.CreateDirectory(directory);SharedData.AtomicWrite(index,new JavaScriptSerializer().Serialize(Clips));}
        public void Remove(ReactionClip clip){
            if(!Clips.Contains(clip))return;int at=Clips.IndexOf(clip);Clips.Remove(clip);
            try{Save();}catch{Clips.Insert(at,clip);throw;}File.Delete(FileFor(clip));
        }
        public ReactionClip Pick(string kind){var choices=Clips.Where(c=>c.Kind==kind&&File.Exists(FileFor(c))).ToList();return choices.Count==0?null:choices[random.Next(choices.Count)];}
    }
}
