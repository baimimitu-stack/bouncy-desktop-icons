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
            pet=p;Text="反应素材库 · "+p.Entry.Name;Icon=Program.AppIcon;ClientSize=new Size(620,535);
            Font=new Font("Microsoft YaHei",10);BackColor=Color.FromArgb(248,244,236);StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            LabelAt("每种反应可添加多张 GIF，触发时随机选一张",20,18,570,28);
            kind.SetBounds(20,55,200,30);Controls.Add(kind);
            clips.SetBounds(20,100,580,180);Controls.Add(clips);
            LabelAt("气泡台词（可留空，使用原台词）",20,292,400,26);words.SetBounds(20,320,580,28);Controls.Add(words);
            LabelAt("播放秒数",20,364,90,28);seconds.SetBounds(115,360,70,28);Controls.Add(seconds);
            ButtonAt("保存台词 / 时长",200,358,150,()=>UpdateClip());
            ButtonAt("预览",360,358,100,()=>{var clip=Selected();if(clip!=null){UpdateClip();pet.PreviewReaction(clip);}});
            ButtonAt("移除素材",470,358,130,()=>{var clip=Selected();if(clip!=null)Try(()=>{pet.StopReaction();pet.Reactions.Remove(clip);RefreshClips();status.Text="已移除本机导入副本，原文件保留。";});});
            status.SetBounds(20,407,580,45);status.Text=pet.Reactions.LoadError??"本地复制保存，不上传网络。关闭素材库后恢复日常 GIF。";Controls.Add(status);
            kind.SelectedIndexChanged+=(s,e)=>RefreshClips();clips.SelectedIndexChanged+=(s,e)=>{var clip=Selected();words.Text=clip==null?"":clip.Text;seconds.Value=clip==null?4:clip.Seconds;};
            foreach(Control control in Controls)if(control.Top>=100)control.Top+=70;
            ButtonAt("新增心情",235,53,110,()=>EditMood(null));
            ButtonAt("编辑心情",350,53,110,()=>EditMood(CurrentMood()));
            ButtonAt("删除心情",465,53,135,()=>DeleteMood());
            ButtonAt("批量添加 GIF…",20,105,200,()=>Import());
            ButtonAt("触发这个心情",235,105,170,()=>{var mood=CurrentMood();if(mood!=null){pet.Social.React(mood.Id,String.IsNullOrWhiteSpace(mood.Text)?mood.Name+"～":mood.Text);pet.SyncReaction(0);}});
            ReloadMoods(null);FormClosed+=(s,e)=>pet.ClearInteraction();
        }
        CustomMood CurrentMood(){return kind.SelectedItem as CustomMood;}
        void ReloadMoods(string selected){kind.Items.Clear();foreach(var mood in pet.Reactions.Moods)kind.Items.Add(mood);kind.SelectedItem=pet.Reactions.Moods.FirstOrDefault(m=>m.Id==selected)??pet.Reactions.Moods.First();}
        void EditMood(CustomMood mood){using(var dialog=new MoodEditor(mood)){if(dialog.ShowDialog(this)!=DialogResult.OK)return;Try(()=>{var saved=pet.Reactions.SaveMood(mood,dialog.MoodName,dialog.Words,dialog.IncludeInClicks);ReloadMoods(saved.Id);status.Text="心情已保存，可添加多张 GIF。";});}}
        void DeleteMood(){var mood=CurrentMood();if(mood==null)return;
            if(ReactionLibrary.Keys.Contains(mood.Id)){status.Text="内置心情保留自动行为，可改名；自定义心情可删除。";return;}
            if(MessageBox.Show(this,"删除心情“"+mood.Name+"”？已有 GIF 将移到悠闲分类，不会删素材。","删除自定义心情",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            Try(()=>{pet.ClearInteraction();pet.Reactions.DeleteMood(mood);ReloadMoods("Calm");status.Text="心情已删除，素材已保留到悠闲分类。";});
        }
        ReactionClip Selected(){return clips.SelectedItem as ReactionClip;}
        void RefreshClips(){clips.Items.Clear();if(CurrentMood()==null)return;foreach(var c in pet.Reactions.Clips.Where(c=>c.Kind==CurrentMood().Id))clips.Items.Add(c);if(clips.Items.Count>0)clips.SelectedIndex=0;else{words.Text="";seconds.Value=4;}}
        void Import(){using(var dialog=new OpenFileDialog{Filter="GIF 动图|*.gif",Multiselect=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            int added=0;var failures=new List<string>();foreach(string file in dialog.FileNames){try{pet.Reactions.Add(file,CurrentMood().Id);added++;}catch(Exception){failures.Add(System.IO.Path.GetFileName(file));}}
            RefreshClips();status.Text="已添加 "+added+" 张"+(failures.Count==0?"。":"；未导入 "+failures.Count+" 张（格式、大小或文件访问问题）。");
        }}
        void UpdateClip(){var clip=Selected();if(clip==null)return;Try(()=>{clip.Text=words.Text.Trim();clip.Seconds=(int)seconds.Value;pet.Reactions.Save();int i=clips.SelectedIndex;clips.Items[i]=clip;status.Text="已保存；相同分类的多张 GIF 会随机播放。";});}
        void Try(Action action){try{action();}catch(Exception ex){status.Text=ex is ArgumentException?ex.Message:"保存失败，请检查素材文件或目录是否可写。";}}
        void LabelAt(string text,int x,int y,int w,int h){Controls.Add(new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h)});}
        void ButtonAt(string text,int x,int y,int width,Action action){var button=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,34)};button.Click+=(s,e)=>action();Controls.Add(button);}
    }
    sealed class MoodEditor:Form {
        readonly TextBox name=new TextBox{MaxLength=20},words=new TextBox{MaxLength=80};
        readonly CheckBox click=new CheckBox{Text="参与单击随机反应",AutoSize=true};
        public string MoodName {get{return name.Text;}}
        public string Words {get{return words.Text;}}
        public bool IncludeInClicks {get{return click.Checked;}}
        public MoodEditor(CustomMood mood){Text=mood==null?"新增心情":"编辑心情";ClientSize=new Size(440,245);Font=new Font("Microsoft YaHei",10);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            Controls.Add(new Label{Text="心情名称（例如：委屈、傲娇、困困）",Location=new Point(20,15),AutoSize=true});name.SetBounds(20,42,400,28);Controls.Add(name);
            Controls.Add(new Label{Text="默认台词（单张 GIF 的台词优先）",Location=new Point(20,82),AutoSize=true});words.SetBounds(20,110,400,28);Controls.Add(words);
            click.Location=new Point(20,155);click.Checked=mood==null||mood.Click;click.Enabled=mood==null||!ReactionLibrary.Keys.Contains(mood.Id);Controls.Add(click);
            if(mood!=null){name.Text=mood.Name;words.Text=mood.Text;}
            var save=new Button{Text="保存",Location=new Point(300,195),Size=new Size(120,32)};save.Click+=(s,e)=>{if(String.IsNullOrWhiteSpace(name.Text)){MessageBox.Show("先给心情起个名字吧。");return;}DialogResult=DialogResult.OK;Close();};Controls.Add(save);AcceptButton=save;
        }
    }

}
