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
    public sealed class CustomMood {
        public string Id,Name,Text="";
        public bool Click=true;
        public override string ToString(){return Name;}
    }
    public sealed class ReactionData {
        public List<CustomMood> Moods=new List<CustomMood>();
        public List<ReactionClip> Clips=new List<ReactionClip>();
    }
    public sealed class ReactionLibrary {
        public static readonly string[] Keys={"Happy","Shy","Curious","Grumpy","Greeting","Argument","Calm"};
        public static readonly string[] Names={"开心","害羞","好奇","小脾气","打招呼","拌嘴","悠闲"};
        public readonly List<CustomMood> Moods=new List<CustomMood>();
        public readonly List<ReactionClip> Clips=new List<ReactionClip>();
        readonly string directory,index;
        public string LoadError {get;private set;}
        readonly Random random=new Random();
        public ReactionLibrary(string profile){
            directory=Path.Combine(profile,"reactions");index=Path.Combine(directory,"library.json");
            try{if(File.Exists(index)){
                string raw=File.ReadAllText(index);var json=new JavaScriptSerializer();
                if(raw.TrimStart().StartsWith("["))Clips.AddRange(json.Deserialize<List<ReactionClip>>(raw)??new List<ReactionClip>());
                else {var data=json.Deserialize<ReactionData>(raw);if(data==null)throw new IOException("empty library");Clips.AddRange(data.Clips??new List<ReactionClip>());Moods.AddRange(data.Moods??new List<CustomMood>());}
                Moods.RemoveAll(m=>m==null||String.IsNullOrWhiteSpace(m.Name)||!(Keys.Contains(m.Id)||ValidId(m.Id)));
                var seen=new HashSet<string>();Moods.RemoveAll(m=>!seen.Add(m.Id));
                Clips.RemoveAll(c=>c==null||!ValidId(c.Id));
                foreach(var c in Clips)if(!Keys.Contains(c.Kind)&&!Moods.Any(m=>m.Id==c.Kind))c.Kind="Calm";
                foreach(var c in Clips)c.Seconds=Math.Max(1,Math.Min(30,c.Seconds));
            }}catch{LoadError="素材列表无法读取，原文件已保留。请先恢复 library.json 后重试。";Clips.Clear();Moods.Clear();}
            for(int i=0;i<Keys.Length;i++)if(!Moods.Any(m=>m.Id==Keys[i]))Moods.Add(new CustomMood{Id=Keys[i],Name=Names[i],Click=false});
        }
        static bool ValidId(string id){return id!=null&&Regex.IsMatch(id,"^[a-f0-9]{32}$");}
        public string FileFor(ReactionClip clip){if(clip==null||!ValidId(clip.Id))throw new ArgumentException("无效素材");return Path.Combine(directory,clip.Id+".gif");}
        public ReactionClip Add(string source,string kind){
            if(LoadError!=null)throw new IOException(LoadError);
            if(!Moods.Any(m=>m.Id==kind))throw new ArgumentException("未知反应分类");
            if(new FileInfo(source).Length>20*1024*1024)throw new IOException("每张 GIF 最大 20 MB");
            byte[] bytes=File.ReadAllBytes(source);
            using(var stream=new MemoryStream(bytes))using(Image image=Image.FromStream(stream)){
                if(image.RawFormat.Guid!=ImageFormat.Gif.Guid||image.Width>4096||image.Height>4096)throw new IOException("请选择有效 GIF，宽高不超过 4096");
            }
            var clip=new ReactionClip{Id=Guid.NewGuid().ToString("N"),Name=Path.GetFileNameWithoutExtension(source),Kind=kind};
            Directory.CreateDirectory(directory);File.WriteAllBytes(FileFor(clip),bytes);Clips.Add(clip);
            try{Save();}catch{Clips.Remove(clip);File.Delete(FileFor(clip));throw;}return clip;
        }
        public void Save(){if(LoadError!=null)throw new IOException(LoadError);Directory.CreateDirectory(directory);SharedData.AtomicWrite(index,new JavaScriptSerializer().Serialize(new ReactionData{Moods=Moods,Clips=Clips}));}
        public void Remove(ReactionClip clip){
            if(!Clips.Contains(clip))return;int at=Clips.IndexOf(clip);Clips.Remove(clip);
            try{Save();}catch{Clips.Insert(at,clip);throw;}File.Delete(FileFor(clip));
        }
        public string MoodText(string id){var mood=Moods.FirstOrDefault(m=>m.Id==id);return mood==null?null:mood.Text;}
        public string MoodName(string id){var mood=Moods.FirstOrDefault(m=>m.Id==id);return mood==null?null:mood.Name;}
        public CustomMood ChooseClick(){var choices=Moods.Where(m=>!Keys.Contains(m.Id)&&m.Click).ToList();int pick=random.Next(choices.Count+5);return pick<choices.Count?choices[pick]:null;}
        public CustomMood SaveMood(CustomMood old,string name,string text,bool click){
            name=(name??"").Trim();if(name.Length==0||name.Length>20)throw new ArgumentException("心情名称需要 1–20 个字。");
            if(Moods.Any(m=>m!=old&&String.Equals(m.Name,name,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("这个心情名称已经存在。");
            var mood=old??new CustomMood{Id=Guid.NewGuid().ToString("N")};string prevName=mood.Name,prevText=mood.Text;bool prevClick=mood.Click;
            mood.Name=name;mood.Text=text??"";mood.Click=click;if(old==null)Moods.Add(mood);
            try{Save();}catch{if(old==null)Moods.Remove(mood);else{mood.Name=prevName;mood.Text=prevText;mood.Click=prevClick;}throw;}return mood;
        }
        public void DeleteMood(CustomMood mood){
            if(Keys.Contains(mood.Id))throw new ArgumentException("内置心情关联自动行为，可以改名；自定义心情可以删除。");
            int at=Moods.IndexOf(mood);if(at<0)return;var affected=Clips.Where(c=>c.Kind==mood.Id).ToList();Moods.RemoveAt(at);foreach(var clip in affected)clip.Kind="Calm";
            try{Save();}catch{Moods.Insert(at,mood);foreach(var clip in affected)clip.Kind=mood.Id;throw;}
        }
        public ReactionClip Pick(string kind){var choices=Clips.Where(c=>c.Kind==kind&&File.Exists(FileFor(c))).ToList();return choices.Count==0?null:choices[random.Next(choices.Count)];}
    }
}
