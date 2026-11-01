using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;

namespace BouncyIcons {
    sealed class ReactionWindow:Form {
        readonly PetWindow pet;
        readonly ComboBox kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
        readonly ListBox clips=new ListBox();
        readonly TextBox words=new TextBox{MaxLength=80};
        readonly NumericUpDown seconds=new NumericUpDown{Minimum=1,Maximum=30,Value=4};
        readonly Label status=new Label();
        public ReactionWindow(PetWindow p){
            pet=p;Text="反应素材库 · "+p.Entry.Name;Icon=Program.AppIcon;ClientSize=new Size(620,465);
            Font=new Font("Microsoft YaHei",10);BackColor=Color.FromArgb(248,244,236);StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            LabelAt("每种反应可添加多张 GIF，触发时随机选一张",20,18,570,28);
            kind.Items.AddRange(ReactionLibrary.Names);kind.SetBounds(20,55,200,30);Controls.Add(kind);
            ButtonAt("批量添加 GIF…",235,53,170,()=>Import());
            clips.SetBounds(20,100,580,180);Controls.Add(clips);
            LabelAt("气泡台词（可留空，使用原台词）",20,292,400,26);words.SetBounds(20,320,580,28);Controls.Add(words);
            LabelAt("播放秒数",20,364,90,28);seconds.SetBounds(115,360,70,28);Controls.Add(seconds);
            ButtonAt("保存台词 / 时长",200,358,150,()=>UpdateClip());
            ButtonAt("预览",360,358,100,()=>{var clip=Selected();if(clip!=null){UpdateClip();pet.PreviewReaction(clip);}});
            ButtonAt("移除素材",470,358,130,()=>{var clip=Selected();if(clip!=null)Try(()=>{pet.StopReaction();pet.Reactions.Remove(clip);RefreshClips();status.Text="已移除本机导入副本，原文件保留。";});});
            status.SetBounds(20,407,580,45);status.Text=pet.Reactions.LoadError??"本地复制保存，不上传网络。关闭素材库后恢复日常 GIF。";Controls.Add(status);
            kind.SelectedIndexChanged+=(s,e)=>RefreshClips();clips.SelectedIndexChanged+=(s,e)=>{var clip=Selected();words.Text=clip==null?"":clip.Text;seconds.Value=clip==null?4:clip.Seconds;};
            kind.SelectedIndex=0;FormClosed+=(s,e)=>pet.ClearInteraction();
        }
        ReactionClip Selected(){return clips.SelectedItem as ReactionClip;}
        void RefreshClips(){clips.Items.Clear();foreach(var c in pet.Reactions.Clips.Where(c=>c.Kind==ReactionLibrary.Keys[kind.SelectedIndex]))clips.Items.Add(c);if(clips.Items.Count>0)clips.SelectedIndex=0;else{words.Text="";seconds.Value=4;}}
        void Import(){using(var dialog=new OpenFileDialog{Filter="GIF 动图|*.gif",Multiselect=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            int added=0;var failures=new List<string>();foreach(string file in dialog.FileNames){try{pet.Reactions.Add(file,ReactionLibrary.Keys[kind.SelectedIndex]);added++;}catch(Exception){failures.Add(System.IO.Path.GetFileName(file));}}
            RefreshClips();status.Text="已添加 "+added+" 张"+(failures.Count==0?"。":"；未导入 "+failures.Count+" 张（格式、大小或文件访问问题）。");
        }}
        void UpdateClip(){var clip=Selected();if(clip==null)return;Try(()=>{clip.Text=words.Text.Trim();clip.Seconds=(int)seconds.Value;pet.Reactions.Save();int i=clips.SelectedIndex;clips.Items[i]=clip;status.Text="已保存；相同分类的多张 GIF 会随机播放。";});}
        void Try(Action action){try{action();}catch(Exception){status.Text="保存失败，请检查素材文件或目录是否可写。";}}
        void LabelAt(string text,int x,int y,int w,int h){Controls.Add(new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h)});}
        void ButtonAt(string text,int x,int y,int width,Action action){var button=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,34)};button.Click+=(s,e)=>action();Controls.Add(button);}
    }
}
