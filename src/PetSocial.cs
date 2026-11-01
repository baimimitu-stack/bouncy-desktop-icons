using System;

namespace BouncyIcons {
    public enum PetMood { Calm, Happy, Shy, Curious, Grumpy }
    // Pure local simulation: no app inspection, network, or real file interaction.
    public sealed class PetSocial {
        readonly Random random;
        double moodLeft,clickHeat,cooldown;
        public PetMood Mood=PetMood.Calm;
        public string Speech="";
        public string ReactionKind="Calm";
        public int Revision {get;private set;}
        public void OverrideSpeech(string text){if(!String.IsNullOrWhiteSpace(text)){Speech=text;SpeechLeft=3.5;}}
        public void React(string kind,string text){PetMood mood;if(!Enum.TryParse<PetMood>(kind,out mood))mood=PetMood.Curious;if(kind=="Argument")mood=PetMood.Grumpy;if(kind=="Greeting")mood=PetMood.Happy;Say(text,mood);ReactionKind=kind;}
        public double SpeechLeft {get;private set;}
        public int ClickCount {get;private set;}
        public bool Ready {get{return cooldown<=0&&SpeechLeft<=0;}}
        public string MoodName {get {switch(Mood){case PetMood.Happy:return "开心";case PetMood.Shy:return "害羞";case PetMood.Curious:return "好奇";case PetMood.Grumpy:return "小脾气";default:return "悠闲";}}}
        public PetSocial(int seed){random=new Random(seed);}
        public void Step(double dt){
            dt=Math.Max(0,Math.Min(.1,dt));clickHeat=Math.Max(0,clickHeat-dt*.32);
            cooldown=Math.Max(0,cooldown-dt);SpeechLeft=Math.Max(0,SpeechLeft-dt);moodLeft-=dt;
            if(moodLeft<=0&&Mood!=PetMood.Calm){Mood=PetMood.Calm;ReactionKind="Calm";Revision++;}if(SpeechLeft<=0)Speech="";
        }
        void Say(string text,PetMood mood){Speech=text;SpeechLeft=3.5;Mood=mood;moodLeft=18;ReactionKind=mood.ToString();Revision++;}
        public void Click(){Click(null,null);}
        public void Click(string kind,string text){
            ClickCount++;clickHeat+=1;cooldown=6;
            if(clickHeat>=4){Say("戳太多啦，歇一下嘛！",PetMood.Grumpy);return;}
            if(kind!=null){Say(text,PetMood.Curious);ReactionKind=kind;return;}
            switch(random.Next(5)){
                case 0:Say("嘿嘿，摸摸收到！",PetMood.Happy);break;
                case 1:Say("诶？是在叫我吗？",PetMood.Curious);break;
                case 2:Say("有一点点害羞…",PetMood.Shy);break;
                case 3:Say("今天也想和你玩！",PetMood.Happy);break;
                default:Say("痒痒！被你发现啦～",PetMood.Shy);break;
            }
        }
        public bool Meet(PetSocial other){
            if(other==null||ReferenceEquals(this,other)||!Ready||!other.Ready)return false;
            if(Mood==PetMood.Grumpy||other.Mood==PetMood.Grumpy||random.Next(4)==0){
                Say("这块草地我先发现的！",PetMood.Grumpy);other.Say("哼，分你一半还不行嘛！",PetMood.Grumpy);
            }else if(random.Next(2)==0){
                Say("嗨！一起散步吗？",PetMood.Happy);other.Say("好呀，等等我～",PetMood.Happy);
            }else{
                Say("你今天看起来很精神！",PetMood.Curious);other.Say("嘿嘿，你也是呀。",PetMood.Shy);
            }
            ReactionKind=other.ReactionKind=(Mood==PetMood.Grumpy?"Argument":"Greeting");
            cooldown=other.cooldown=22;return true;
        }
        public void Clear(){ReactionKind="Calm";Speech="";SpeechLeft=0;clickHeat=0;cooldown=0;Mood=PetMood.Calm;moodLeft=0;}
    }
}
