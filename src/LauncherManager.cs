using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Linq;

namespace BouncyIcons {
    public sealed class Entry {
        public string Id,Name;
        public Entry(){}
        public Entry(string name){Id=Guid.NewGuid().ToString("N");Name=name;}
    }
    public sealed class LauncherContext:ApplicationContext {
        public static LauncherContext Current;
        public readonly List<Entry> Entries=new List<Entry>();
        public readonly Dictionary<string,PetWindow> Pets=new Dictionary<string,PetWindow>();
        readonly NotifyIcon tray;
        readonly System.Windows.Forms.Timer wakeTimer=new System.Windows.Forms.Timer();
        ManagerWindow manager;
        readonly string index=Path.Combine(Program.DataDir,"entries.json");
        bool exiting;
        public LauncherContext(){
            Current=this;
            try{Entries.AddRange(new JavaScriptSerializer().Deserialize<List<Entry>>(File.ReadAllText(index))??new List<Entry>());}catch{}
            // Only generated identifiers are valid local profile directories.
            Entries.RemoveAll(e=>e==null||e.Id==null||!System.Text.RegularExpressions.Regex.IsMatch(e.Id,"^[a-f0-9]{32}$"));
            bool first=Entries.Count==0;
            if(first)Entries.Add(new Entry("我的桌面"));
            foreach(Entry e in Entries){PetWindow w=new PetWindow(e);Pets[e.Id]=w;if(first){w.Config.Target=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);w.Save();}w.Show();}
            Save();
            ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("管理所有应用挂件…",null,(s,e)=>ShowManager());
            menu.Items.Add("找回全部挂件",null,(s,e)=>{foreach(PetWindow w in Pets.Values)w.Rescue();});
            menu.Items.Add("使用说明书",null,(s,e)=>Program.ShowManual());
            menu.Items.Add("退出所有挂件",null,(s,e)=>Shutdown());
            tray=new NotifyIcon{Icon=SystemIcons.Application,Text="蹦蹦图标 · "+Program.EditionName,ContextMenuStrip=menu,Visible=true};
            tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left)ShowManager();};
            wakeTimer.Interval=500;wakeTimer.Tick+=(s,e)=>{if(Program.Wake.WaitOne(0))ShowManager();};wakeTimer.Start();
            if(first)ShowManager();
        }
        public void SetMode(string mode){
            if(mode!="lite"&&mode!="standard"&&mode!="pro")throw new ArgumentException("mode");
            SharedData.AtomicWrite(Path.Combine(Program.DataDir,"mode.txt"),mode);
            if(Program.Edition==mode)return;Program.Edition=mode;
            foreach(PetWindow pet in Pets.Values)pet.ApplyMode();
            tray.Text="蹦蹦图标 · "+Program.EditionName;if(manager!=null)manager.RefreshMode();
        }
        public void Save(){try{File.WriteAllText(index,new JavaScriptSerializer().Serialize(Entries));}catch(Exception e){Program.Log(e);}}
        public void ShowManager(){if(manager!=null&&!manager.IsDisposed){manager.Show();manager.Activate();return;}manager=new ManagerWindow(this);manager.Show();}
        public void Add(string target,string picture,string gif){
            string name=String.IsNullOrEmpty(target)?"新挂件":Path.GetFileNameWithoutExtension(target.TrimEnd(Path.DirectorySeparatorChar));
            Entry e=new Entry(name);PetWindow w=new PetWindow(e);w.Config.Target=target??"";if(!String.IsNullOrEmpty(gif))w.ImportGif(gif);else if(!String.IsNullOrEmpty(picture))w.ImportImage(picture);
            w.Save();Entries.Add(e);Pets[e.Id]=w;Save();w.Show();if(manager!=null)manager.RefreshList();
        }
        public void Remove(Entry e){PetWindow w;if(Pets.TryGetValue(e.Id,out w)){w.Quit();Pets.Remove(e.Id);}Entries.Remove(e);Save();if(manager!=null)manager.RefreshList();}
        public void Shutdown(){if(exiting)return;exiting=true;wakeTimer.Stop();foreach(PetWindow w in Pets.Values.ToList())w.Quit();Save();tray.Visible=false;tray.Dispose();wakeTimer.Dispose();if(manager!=null)manager.Dispose();ExitThread();}
    }
    sealed class ManagerWindow:Form {
        readonly LauncherContext ctx;
        readonly ListView list=new ListView();
        readonly ComboBox modes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(310,25),Width=180};
        readonly Label summary=new Label{Location=new Point(22,65),AutoSize=true};
        public ManagerWindow(LauncherContext c){ctx=c;Text="蹦蹦图标管理器";ClientSize=new Size(700,460);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei",10);BackColor=Color.FromArgb(248,244,236);AllowDrop=true;
            Controls.Add(new Label{Text="蹦蹦图标管理器",Font=new Font(Font.FontFamily,17,FontStyle.Bold),Location=new Point(22,20),AutoSize=true});
            Controls.Add(summary);modes.Items.AddRange(new object[]{"简易","标准","进阶"});Controls.Add(modes);RefreshMode();modes.SelectedIndexChanged+=(s,e)=>ctx.SetMode(new[]{"lite","standard","pro"}[modes.SelectedIndex]);
            AddButton("使用说明书",536,20,()=>Program.ShowManual());
            list.Location=new Point(22,105);list.Size=new Size(656,265);list.View=View.Details;list.FullRowSelect=true;list.MultiSelect=false;list.Columns.Add("挂件",145);list.Columns.Add("双击打开",390);list.Columns.Add("尺寸",80);Controls.Add(list);
            AddButton("添加应用挂件",22,390,()=>AddEntry(null));AddButton("设置 / 换 GIF",180,390,()=>{Entry e=Selected();if(e!=null)ctx.Pets[e.Id].OpenSettings();});
            AddButton("找回",338,390,()=>{Entry e=Selected();if(e!=null)ctx.Pets[e.Id].Rescue();});
            AddButton("移除挂件",496,390,()=>{Entry e=Selected();if(e!=null&&MessageBox.Show("只移除这个挂件，不删除对应应用或文件。","移除挂件",MessageBoxButtons.OKCancel)==DialogResult.OK)ctx.Remove(e);});
            list.DoubleClick+=(s,e)=>{Entry item=Selected();if(item!=null)ctx.Pets[item.Id].OpenSettings();};
            Activated+=(s,e)=>RefreshList();
            DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
            DragDrop+=(s,e)=>{string[] paths=(string[])e.Data.GetData(DataFormats.FileDrop);if(paths!=null&&paths.Length>0)AddEntry(paths[0]);};
            FormClosing+=(s,e)=>{e.Cancel=true;Hide();};RefreshList();
        }
        public void RefreshMode(){modes.SelectedIndex=Program.Edition=="lite"?0:Program.Edition=="standard"?1:2;summary.Text=Program.EditionSummary+"。所有模式共用应用绑定和 GIF。";}
        void AddButton(string name,int x,int y,Action f){Button b=new Button{Text=name,Location=new Point(x,y),Size=new Size(142,36)};b.Click+=(s,e)=>f();Controls.Add(b);}
        Entry Selected(){return list.SelectedItems.Count==0?null:(Entry)list.SelectedItems[0].Tag;}
        public void RefreshList(){string id=Selected()==null?null:Selected().Id;list.Items.Clear();foreach(Entry e in ctx.Entries){PetWindow w=ctx.Pets[e.Id];ListViewItem item=new ListViewItem(e.Name);item.SubItems.Add(String.IsNullOrEmpty(w.Config.Target)?"未绑定 · 双击打开设置":w.Config.Target);item.SubItems.Add(w.Config.Size.ToString());item.Tag=e;list.Items.Add(item);if(id==e.Id)item.Selected=true;}}
        void AddEntry(string input){using(AddEntryWindow d=new AddEntryWindow(input)){if(d.ShowDialog(this)==DialogResult.OK)ctx.Add(d.Target,d.Picture,d.Gif);}}
    }
    sealed class ManualWindow:Form {
        public ManualWindow(){
            Text="蹦蹦图标 · 使用说明书";ClientSize=new Size(680,620);MinimumSize=new Size(460,360);StartPosition=FormStartPosition.CenterScreen;
            TextBox text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,ReadOnly=true,BackColor=Color.FromArgb(255,253,248),BorderStyle=BorderStyle.None,Font=new Font("Microsoft YaHei",11)};
            using(Stream stream=typeof(ManualWindow).Assembly.GetManifestResourceStream("BouncyIcons.Manual.txt")){
                if(stream==null)throw new InvalidOperationException("使用说明书资源缺失");
                using(StreamReader reader=new StreamReader(stream,System.Text.Encoding.UTF8))text.Text=reader.ReadToEnd().Replace("\r\n","\n").Replace("\n",Environment.NewLine);
            }
            text.SelectionStart=0;Padding=new Padding(22);Controls.Add(text);
        }
    }
    sealed class AddEntryWindow:Form {
        public string Target="",Picture="",Gif="";
        readonly TextBox target=new TextBox(),image=new TextBox(),gif=new TextBox();
        public AddEntryWindow(string input){Text="添加应用挂件";ClientSize=new Size(610,330);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            Controls.Add(new Label{Text="一个入口，对应一个 app 和一张动图",Location=new Point(20,20),AutoSize=true});
            Row("快捷方式 / 程序",target,60,()=>{using(OpenFileDialog d=new OpenFileDialog()){d.DereferenceLinks=false;d.Filter="快捷方式和程序|*.lnk;*.url;*.exe|所有文件|*.*";if(d.ShowDialog()==DialogResult.OK)target.Text=d.FileName;}});
            Row("静态图片（可选）",image,120,()=>{using(OpenFileDialog d=new OpenFileDialog()){d.Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.ico";if(d.ShowDialog()==DialogResult.OK)image.Text=d.FileName;}});
            Row("匹配 GIF（可选）",gif,180,()=>{using(OpenFileDialog d=new OpenFileDialog()){d.Filter="GIF 动图|*.gif";if(d.ShowDialog()==DialogResult.OK)gif.Text=d.FileName;}});
            Button folder=new Button{Text="绑定文件夹",Location=new Point(20,255),Size=new Size(130,34)};folder.Click+=(s,e)=>{using(FolderBrowserDialog d=new FolderBrowserDialog()){if(d.ShowDialog()==DialogResult.OK)target.Text=d.SelectedPath;}};Controls.Add(folder);
            Button save=new Button{Text="添加",Location=new Point(460,255),Size=new Size(130,34)};save.Click+=(s,e)=>{foreach(string p in new[]{target.Text,image.Text,gif.Text}){if(!String.IsNullOrWhiteSpace(p)&&!File.Exists(p)&&!Directory.Exists(p)){MessageBox.Show("找不到文件："+p);return;}}Target=target.Text;Picture=image.Text;Gif=gif.Text;DialogResult=DialogResult.OK;Close();};Controls.Add(save);
            if(!String.IsNullOrEmpty(input)){string ext=Path.GetExtension(input).ToLowerInvariant();if(ext==".gif")gif.Text=input;else if(new[]{".png",".jpg",".jpeg",".bmp",".ico"}.Contains(ext))image.Text=input;else target.Text=input;}
        }
        void Row(string label,TextBox box,int y,Action browse){Controls.Add(new Label{Text=label,Location=new Point(20,y),AutoSize=true});box.Location=new Point(20,y+23);box.Size=new Size(475,25);Controls.Add(box);Button b=new Button{Text="选择…",Location=new Point(505,y+21),Size=new Size(85,29)};b.Click+=(s,e)=>browse();Controls.Add(b);}
    }
}
