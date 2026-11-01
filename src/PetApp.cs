using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Threading;
using Microsoft.Win32;

namespace BouncyIcons {
    public sealed class Preferences {
        public int Size=80,Speed=45,X=int.MinValue,Y=int.MinValue;
        public int AreaX,AreaY,AreaW,AreaH;
        public bool TopMost=true,Bounce=true,Roaming=true;
        public string Gif="",Target="";
    }
    static class Program {
        public static string Edition,DataDir;
        public static EventWaitHandle Wake;
        [STAThread] static void Main(string[] args) {
            Edition=Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLowerInvariant().Contains("lite")?"lite":(Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLowerInvariant().Contains("standard")?"standard":"pro");
            DataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BouncyIcons",Edition);
            Directory.CreateDirectory(DataDir);
            bool created;
            using(Mutex mutex=new Mutex(true,"Local\\BouncyIcons-"+Edition,out created)){
                Wake=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\BouncyIconsWake-"+Edition);
                if(!created){Wake.Set();Wake.Dispose();return;}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException+=(s,e)=>Log(e.Exception);
                try{LauncherContext context=new LauncherContext();if(Array.IndexOf(args,"--manage")>=0)context.ShowManager();Application.Run(context);}catch(Exception e){Log(e);MessageBox.Show(e.Message,"启动失败");}
                Wake.Dispose();
            }
        }
        public static void Log(Exception e){try{File.AppendAllText(Path.Combine(DataDir,"error.log"),DateTime.Now+" "+e+Environment.NewLine);}catch{}}
    }
    public sealed class PetWindow:Form {
        public Preferences Config;
        readonly string configPath;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        double last,elapsed,grassIn=3;
        Image gif;
        MemoryStream imageStream;
        int frameCount=1,frameIndex;double frameElapsed;int[] frameDelays=new int[]{100};
        NotifyIcon tray;
        SettingsWindow settings;
        PetBrain brain;
        Rectangle area;
        bool dragging,menuOpen,quitting;
        Point dragOffset,dragStart;
        Point dragWindowStart;
        Rectangle dragAreaStart;
        bool dragMoved;
        readonly List<Food> foods=new List<Food>();
        readonly Dictionary<string,FoodWindow> foodWindows=new Dictionary<string,FoodWindow>();
        readonly Random random=new Random();
        public bool Pro {get{return Program.Edition=="pro";}}
        public bool Moving {get{return Program.Edition!="lite";}}
        public readonly Entry Entry;
        readonly string profileDir;
        public PetWindow(Entry entry){
            Entry=entry;profileDir=Path.Combine(Program.DataDir,entry.Id);Directory.CreateDirectory(profileDir);
            configPath=Path.Combine(profileDir,"settings.json");
            try{Config=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(configPath))??new Preferences();}catch{Config=new Preferences();}
            Config.Size=Math.Max(32,Math.Min(256,Config.Size));Config.Speed=Math.Max(10,Math.Min(120,Config.Speed));
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;TopMost=Config.TopMost;
            DoubleBuffered=true;Text=Pro?"蹦蹦图标 · 专业版":(Moving?"蹦蹦图标 · 中等版":"蹦蹦图标 · 低配版");StartPosition=FormStartPosition.Manual;AllowDrop=true;
            ApplyArea();
            Vec start=new Vec(Config.X==int.MinValue?area.Right-Config.Size:Config.X,Config.Y==int.MinValue?area.Bottom-Config.Size-24:Config.Y);
            brain=new PetBrain(Environment.TickCount,ToBounds(area),start);ApplySize();
            LoadGif();BuildMenu();
            tray=new NotifyIcon{Icon=SystemIcons.Application,Text=Text,Visible=false,ContextMenuStrip=ContextMenuStrip};
            tray.MouseClick+=(s,e)=>{if(e.Button==MouseButtons.Left)Rescue();};
            timer.Interval=Moving?50:100;timer.Tick+=Tick;timer.Start();
            Cursor=Cursors.Hand;
            MouseDown+=Down;MouseMove+=MoveMouse;MouseUp+=Up;
            MouseCaptureChanged+=(s,e)=>{if(dragging&&!Capture)EndDrag(false);};
            DragEnter+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};
            DragDrop+=(s,e)=>{string[] paths=(string[])e.Data.GetData(DataFormats.FileDrop);if(paths==null||paths.Length==0)return;
                if(Path.GetExtension(paths[0]).Equals(".gif",StringComparison.OrdinalIgnoreCase))ImportGif(paths[0]);
                else if(Pro&&Directory.Exists(paths[0]))Feed(Path.GetFileName(paths[0]),true);
            };
            SystemEvents.DisplaySettingsChanged+=DisplayChanged;
            FormClosing+=(s,e)=>{if(!quitting){e.Cancel=true;Rescue();}};
            FormClosed+=(s,e)=>{timer.Stop();timer.Dispose();SystemEvents.DisplaySettingsChanged-=DisplayChanged;Save();ClearFood();DisposeImage();tray.Visible=false;tray.Dispose();};
        }
        static Bounds ToBounds(Rectangle a){return new Bounds(a.X,a.Y,a.Width,a.Height);}
        void DisplayChanged(object s,EventArgs e){if(!IsDisposed)BeginInvoke((Action)(()=>{ApplyArea();brain.Reset(ToBounds(area),brain.Position);ApplySize();ClearFood();}));}
        void ApplyArea(){
            Rectangle wa=Screen.PrimaryScreen.WorkingArea;
            if(!Moving){area=wa;return;}
            int w=Config.AreaW>0?Config.AreaW:Math.Min(640,wa.Width),h=Config.AreaH>0?Config.AreaH:Math.Min(420,wa.Height);
            w=Math.Min(wa.Width,Math.Max(Config.Size+36,w));h=Math.Min(wa.Height,Math.Max(Config.Size+36,h));
            int x=Config.AreaW>0?Config.AreaX:wa.Right-w-8,y=Config.AreaH>0?Config.AreaY:wa.Bottom-h-8;
            area=new Rectangle(Math.Max(wa.Left,Math.Min(x,wa.Right-w)),Math.Max(wa.Top,Math.Min(y,wa.Bottom-h)),w,h);
        }
        public void ApplySize(){
            Config.Size=Math.Max(32,Math.Min(256,Config.Size));Size=new Size(Config.Size+12,Config.Size+36);
            ApplyArea();brain.Size=Config.Size+36;brain.Speed=Config.Speed;brain.Area=ToBounds(area);brain.Position=brain.Area.Clamp(brain.Position,brain.Size);
            Location=new Point((int)brain.Position.X,(int)brain.Position.Y);TopMost=Config.TopMost;Invalidate();
        }
        public void Save(){
            if(brain!=null){Config.X=(int)brain.Position.X;Config.Y=(int)brain.Position.Y;}
            try{string temp=configPath+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(Config));
                if(File.Exists(configPath))File.Replace(temp,configPath,null);else File.Move(temp,configPath);
            }catch(Exception e){Program.Log(e);}
        }
        public void ImportImage(string file){ try{ string target=Path.Combine(profileDir,"picture.png");using(Image im=Image.FromFile(file)){if(im.Width>4096||im.Height>4096)throw new Exception("图片尺寸过大");im.Save(target,ImageFormat.Png);}Config.Gif=target;LoadGif();Save();}catch(Exception e){MessageBox.Show(e.Message);} }
        void DisposeImage(){if(gif!=null){gif.Dispose();gif=null;}if(imageStream!=null){imageStream.Dispose();imageStream=null;}}
        public void LoadGif(){
            DisposeImage();string file=File.Exists(Config.Gif)?Config.Gif:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","folder.gif");
            try{imageStream=new MemoryStream(File.ReadAllBytes(file));gif=Image.FromStream(imageStream);frameCount=1;frameIndex=0;frameElapsed=0;
                foreach(Guid dim in gif.FrameDimensionsList)if(dim==FrameDimension.Time.Guid)frameCount=gif.GetFrameCount(FrameDimension.Time);
                frameDelays=new int[frameCount];for(int i=0;i<frameCount;i++)frameDelays[i]=100;
                try{byte[] delays=gif.GetPropertyItem(0x5100).Value;for(int i=0;i<frameCount&&i*4+4<=delays.Length;i++)frameDelays[i]=Math.Max(50,BitConverter.ToInt32(delays,i*4)*10);}catch(ArgumentException){}}catch(Exception e){Program.Log(e);}
            Invalidate();
        }
        void FrameChanged(object s,EventArgs e){if(IsHandleCreated&&!IsDisposed){try{BeginInvoke((Action)(()=>Invalidate()));}catch(InvalidOperationException){}}}
        public void ImportGif(string file){
            try{
                FileInfo info=new FileInfo(file);if(info.Length>20*1024*1024)throw new Exception("GIF 最大 20 MB");
                byte[] bytes=File.ReadAllBytes(file);using(MemoryStream ms=new MemoryStream(bytes))using(Image check=Image.FromStream(ms)){
                    if(check.RawFormat.Guid!=ImageFormat.Gif.Guid||check.Width>4096||check.Height>4096)throw new Exception("请选择尺寸不超过 4096 的有效 GIF");
                }
                string target=Path.Combine(profileDir,"custom.gif");File.WriteAllBytes(target,bytes);Config.Gif=target;LoadGif();Save();
            }catch(Exception e){MessageBox.Show(e.Message,"无法读取 GIF");}
        }
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);
            int bob=Config.Bounce?(int)(Math.Sin(elapsed*5)*3):0;
            if(Pro&&brain.State==PetState.Eat)bob=(int)(Math.Sin(elapsed*15)*4);
            Rectangle box=new Rectangle(6,5+bob,Config.Size,Config.Size);
            if(gif!=null){double scale=Math.Min((double)box.Width/gif.Width,(double)box.Height/gif.Height);int w=(int)(gif.Width*scale),h=(int)(gif.Height*scale);
                e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(gif,new Rectangle(box.X+(box.Width-w)/2,box.Y+(box.Height-h)/2,w,h));
            }else{using(Brush b=new SolidBrush(Color.Goldenrod))e.Graphics.FillRectangle(b,box);}
            string label=Pro?StateLabel():Entry.Name;
            TextRenderer.DrawText(e.Graphics,label,Font,new Rectangle(0,Config.Size+10,Width,24),Color.White,Color.Transparent,TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPadding);
        }
        string StateLabel(){switch(brain.State){case PetState.SeekFood:return "找吃的…";case PetState.Eat:return "嚼嚼嚼";case PetState.Sleep:return "z z z";case PetState.Wander:return "散步中";default:return "歇一会儿";}}
        void Tick(object s,EventArgs e){
            double now=clock.Elapsed.TotalSeconds,dt=now-last;last=now;elapsed=now;
            bool changed=false;frameElapsed+=Math.Min(.1,dt)*1000;if(gif!=null&&frameCount>1&&frameElapsed>=frameDelays[frameIndex]){frameElapsed=0;frameIndex=(frameIndex+1)%frameCount;gif.SelectActiveFrame(FrameDimension.Time,frameIndex);changed=true;}

            bool near=new Rectangle(Left-28,Top-28,Width+56,Height+56).Contains(Cursor.Position);
            bool paused=dragging||menuOpen||settings!=null||near;
            if(Moving&&!dragging){brain.Step(dt,foods,paused||!Config.Roaming,Pro);Location=new Point((int)Math.Round(brain.Position.X),(int)Math.Round(brain.Position.Y));
                if(brain.ConsumedFoodId!=null)RemoveFood(brain.ConsumedFoodId);
                if(Pro&&!paused&&Config.Roaming){grassIn-=Math.Min(.1,dt);if(grassIn<=0){grassIn=18+random.NextDouble()*12;if(foods.Count<3)Feed("草",false);}}
            }
            if(Config.Bounce||Moving||changed)Invalidate();
        }
        void BuildMenu(){
            ContextMenuStrip m=new ContextMenuStrip();m.Items.Add("管理所有应用挂件…",null,(s,e)=>LauncherContext.Current.ShowManager());m.Items.Add("设置 / 换 GIF / 大小",null,(s,e)=>OpenSettings());
            m.Items.Add("选择点击后打开的文件夹…",null,(s,e)=>{using(FolderBrowserDialog d=new FolderBrowserDialog()){if(d.ShowDialog()==DialogResult.OK){Config.Target=d.SelectedPath;Save();}}});
            m.Items.Add("选择点击后打开的文件或快捷方式…",null,(s,e)=>{using(OpenFileDialog d=new OpenFileDialog()){d.Title="选择打开目标";d.DereferenceLinks=false;if(d.ShowDialog()==DialogResult.OK){Config.Target=d.FileName;Save();}}});
            if(Pro){m.Items.Add("投喂草",null,(s,e)=>Feed("草",false));m.Items.Add("投喂文件夹（仅动画）…",null,(s,e)=>{using(FolderBrowserDialog d=new FolderBrowserDialog()){if(d.ShowDialog()==DialogResult.OK)Feed(Path.GetFileName(d.SelectedPath),true);}});}
            m.Items.Add("找回 / 重置位置",null,(s,e)=>Rescue());m.Items.Add("退出所有挂件",null,(s,e)=>LauncherContext.Current.Shutdown());
            m.Opening+=(s,e)=>menuOpen=true;m.Closed+=(s,e)=>menuOpen=false;ContextMenuStrip=m;
        }
        public void OpenSettings(){if(settings!=null){settings.Activate();return;}settings=new SettingsWindow(this);settings.FormClosed+=(s,e)=>{settings=null;Save();};settings.Show();}
        public void Rescue(){Config.AreaW=0;Config.AreaH=0;ApplyArea();brain.Reset(ToBounds(area),new Vec(area.Right-Width-12,area.Bottom-Height-12));ApplySize();TopMost=true;Config.TopMost=true;Show();ClearFood();Save();}
        public void SelectArea(){using(RangePicker p=new RangePicker()){if(p.ShowDialog()==DialogResult.OK){Rectangle r=p.Selected;Config.AreaX=r.X;Config.AreaY=r.Y;Config.AreaW=r.Width;Config.AreaH=r.Height;ApplyArea();brain.Reset(ToBounds(area),new Vec(area.X,area.Y));ApplySize();ClearFood();Save();}}}
        public void Quit(){quitting=true;if(settings!=null)settings.Close();Close();}
        // Coordinate inputs keep gesture logic testable without moving the system pointer.
        internal void BeginDrag(Point pointer,Point offset){
            dragging=true;dragMoved=false;dragOffset=offset;dragStart=pointer;
            dragWindowStart=Location;dragAreaStart=area;Capture=true;
        }
        internal void UpdateDrag(Point pointer){
            if(!dragging)return;
            Size threshold=SystemInformation.DragSize;
            if(!dragMoved&&Math.Abs(pointer.X-dragStart.X)<=threshold.Width/2&&Math.Abs(pointer.Y-dragStart.Y)<=threshold.Height/2)return;
            dragMoved=true;Cursor=Cursors.SizeAll;
            Rectangle wa=Screen.PrimaryScreen.WorkingArea;
            Vec p=ToBounds(wa).Clamp(new Vec(pointer.X-dragOffset.X,pointer.Y-dragOffset.Y),Math.Max(Width,Height));
            brain.Position=p;Location=new Point((int)p.X,(int)p.Y);
        }
        internal bool EndDrag(bool allowClick){
            if(!dragging)return false;
            dragging=false;Capture=false;Cursor=Cursors.Hand;
            if(!dragMoved)return allowClick;
            if(Moving){
                Config.AreaX=dragAreaStart.X+Left-dragWindowStart.X;
                Config.AreaY=dragAreaStart.Y+Top-dragWindowStart.Y;
                Config.AreaW=dragAreaStart.Width;Config.AreaH=dragAreaStart.Height;
                ApplyArea();ClearFood();
            }
            brain.Reset(ToBounds(area),brain.Position);
            Location=new Point((int)brain.Position.X,(int)brain.Position.Y);
            Save();return false;
        }
        void Down(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left)BeginDrag(Cursor.Position,e.Location);}
        void MoveMouse(object s,MouseEventArgs e){UpdateDrag(Cursor.Position);}
        void Up(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left||!EndDrag(true))return;
            if(String.IsNullOrWhiteSpace(Config.Target))OpenSettings();else try{Process.Start(new ProcessStartInfo(Config.Target){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,"打开失败");}
        }
        void Feed(string label,bool folder){
            if(!Pro||foods.Count>=8)return;
            Vec p=new Vec(area.X+random.NextDouble()*Math.Max(0,area.Width-brain.Size),area.Y+random.NextDouble()*Math.Max(0,area.Height-brain.Size));
            Food f=new Food(Guid.NewGuid().ToString(),label,p,folder);foods.Add(f);FoodWindow fw=new FoodWindow(f);foodWindows.Add(f.Id,fw);fw.Show();brain.Hunger=Math.Max(30,brain.Hunger);
        }
        void RemoveFood(string id){FoodWindow w;if(foodWindows.TryGetValue(id,out w)){w.Close();w.Dispose();foodWindows.Remove(id);}foods.RemoveAll(f=>f.Id==id);}
        void ClearFood(){foreach(FoodWindow w in foodWindows.Values){w.Close();w.Dispose();}foodWindows.Clear();foods.Clear();}
        public bool IsStartup(){try{using(RegistryKey key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){return key!=null&&key.GetValue("BouncyIcons-"+Program.Edition)!=null;}}catch(System.Security.SecurityException){return false;}}
        public void SetStartup(bool enabled){using(RegistryKey key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(enabled)key.SetValue("BouncyIcons-"+Program.Edition,"\""+Application.ExecutablePath+"\"");else key.DeleteValue("BouncyIcons-"+Program.Edition,false);}}
    }
    sealed class FoodWindow:Form {
        readonly Food food;
        public FoodWindow(Food f){food=f;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;Size=new Size(70,55);StartPosition=FormStartPosition.Manual;Location=new Point((int)f.Position.X,(int)f.Position.Y);DoubleBuffered=true;}
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{CreateParams cp=base.CreateParams;cp.ExStyle|=0x20|0x08000000;return cp;}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(food.Folder){e.Graphics.FillRectangle(Brushes.Goldenrod,8,12,42,25);e.Graphics.FillRectangle(Brushes.Goldenrod,8,6,18,10);}else{using(Pen p=new Pen(Color.ForestGreen,3)){for(int i=0;i<5;i++)e.Graphics.DrawLine(p,30,38,12+i*8,10+(i%2)*8);}}
            TextRenderer.DrawText(e.Graphics,food.Folder?"文件夹零食":"草",Font,new Rectangle(0,38,70,17),Color.White,TextFormatFlags.HorizontalCenter);}
    }
    sealed class RangePicker:Form {
        Point start;Rectangle box;bool drawing;
        public Rectangle Selected;
        public RangePicker(){FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=Screen.PrimaryScreen.WorkingArea;BackColor=Color.Black;Opacity=.55;TopMost=true;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;Text="拖出活动范围 · Esc 取消";
            MouseDown+=(s,e)=>{start=e.Location;drawing=true;Capture=true;};MouseMove+=(s,e)=>{if(drawing){box=new Rectangle(Math.Min(start.X,e.X),Math.Min(start.Y,e.Y),Math.Abs(start.X-e.X),Math.Abs(start.Y-e.Y));Invalidate();}};
            MouseUp+=(s,e)=>{drawing=false;Capture=false;if(box.Width>=100&&box.Height>=100){Selected=new Rectangle(Left+box.X,Top+box.Y,box.Width,box.Height);DialogResult=DialogResult.OK;Close();}};
            KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.DrawString("拖出活动范围，松开保存 · Esc 取消",Font,Brushes.White,20,20);using(Pen p=new Pen(Color.Gold,3))e.Graphics.DrawRectangle(p,box);}
    }
    sealed class SettingsWindow:Form {
        readonly PetWindow pet;
        public SettingsWindow(PetWindow p){pet=p;Text=p.Text+" · 设置";ClientSize=new Size(430,490);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei",10);BackColor=Color.FromArgb(248,244,236);
            Label title=new Label{Text=p.Pro?"专业版 · 游走与投喂":(p.Moving?"中等版 · 随机游走":"低配版 · 原地 GIF"),Font=new Font(Font.FontFamily,16,FontStyle.Bold),AutoSize=true,Location=new Point(22,20)};Controls.Add(title);
            Button gif=ButtonAt("选择 GIF…",22,70,()=>{using(OpenFileDialog d=new OpenFileDialog()){d.Filter="GIF 动图|*.gif";if(d.ShowDialog()==DialogResult.OK)pet.ImportGif(d.FileName);}});
            ButtonAt("恢复默认",172,70,()=>{pet.Config.Gif="";pet.LoadGif();pet.Save();});
            Controls.Add(new Label{Text="大小",Location=new Point(22,125),AutoSize=true});NumericUpDown size=new NumericUpDown{Minimum=32,Maximum=256,Value=pet.Config.Size,Increment=8,Location=new Point(130,121),Width=100};size.ValueChanged+=(s,e)=>{pet.Config.Size=(int)size.Value;pet.ApplySize();pet.Save();};Controls.Add(size);
            CheckBox bounce=CheckAt("原地弹动",22,168,pet.Config.Bounce);bounce.CheckedChanged+=(s,e)=>{pet.Config.Bounce=bounce.Checked;pet.Invalidate();pet.Save();};
            CheckBox top=CheckAt("保持置顶",220,168,pet.Config.TopMost);top.CheckedChanged+=(s,e)=>{pet.Config.TopMost=top.Checked;pet.TopMost=top.Checked;pet.Save();};
            CheckBox startup=CheckAt("登录 Windows 后自动出现",22,205,pet.IsStartup());startup.CheckedChanged+=(s,e)=>{try{pet.SetStartup(startup.Checked);}catch(Exception ex){MessageBox.Show(ex.Message);}};
            if(p.Moving){CheckBox roam=CheckAt(p.Pro?"随机游走、觅食和休息":"在范围内随机游走",22,245,pet.Config.Roaming);roam.CheckedChanged+=(s,e)=>{pet.Config.Roaming=roam.Checked;pet.Save();};
                Controls.Add(new Label{Text="速度",Location=new Point(22,290),AutoSize=true});NumericUpDown speed=new NumericUpDown{Minimum=10,Maximum=120,Value=pet.Config.Speed,Location=new Point(130,286),Width=100};speed.ValueChanged+=(s,e)=>{pet.Config.Speed=(int)speed.Value;pet.ApplySize();pet.Save();};Controls.Add(speed);
                ButtonAt("框选活动范围",22,330,()=>{Hide();pet.SelectArea();Show();});ButtonAt("找回挂件",172,330,()=>pet.Rescue());
            }
            Controls.Add(new Label{Text=p.Pro?"拖入文件夹可投喂动画副本，不改动真实文件。\n拖动挂件可换位置，右键可设置点击打开目标。":"不打开主界面也能动，但仍需一个轻量进程。\n不想弹动可关掉；GIF 本身仍会播放。",Location=new Point(22,390),Size=new Size(390,60)});
            ButtonAt("完成",285,450,()=>Close());
        }
        Button ButtonAt(string text,int x,int y,Action action){Button b=new Button{Text=text,Location=new Point(x,y),Size=new Size(135,32)};b.Click+=(s,e)=>action();Controls.Add(b);return b;}
        CheckBox CheckAt(string text,int x,int y,bool value){CheckBox c=new CheckBox{Text=text,Checked=value,AutoSize=true,Location=new Point(x,y)};Controls.Add(c);return c;}
    }
}
