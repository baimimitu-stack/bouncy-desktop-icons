using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Reflection;
using BouncyIcons;
class AppSmoke {
    [STAThread] static void Main(string[] args){try{Run(args);}catch(Exception e){Console.WriteLine("FAIL: "+e.Message);Environment.ExitCode=1;}}
    static void Run(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Program.Edition=args.Length>1?args[1]:"lite";Program.DataDir=Path.Combine(Path.GetFullPath(args[0]),"smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Program.DataDir);
        Program.Wake=new EventWaitHandle(false,EventResetMode.AutoReset,"BouncyIconsSmoke-"+Guid.NewGuid());
        LauncherContext c=new LauncherContext();Application.DoEvents();
        string gif=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","folder.gif");
        c.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"",gif);
        if(c.Pets.Count!=2)throw new Exception("multiple entries");
        foreach(PetWindow w in c.Pets.Values){w.Config.Size=128;w.ApplySize();w.Save();if(w.Width!=140)throw new Exception("resize");w.LoadGif();w.Rescue();TestDrag(w);TestDoubleClick(w);}
        foreach(Form f in Application.OpenForms){if(f is ManagerWindow){
            if(!f.Text.Contains(Program.EditionName))throw new Exception("edition name missing");
            using(Bitmap bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"manager-preview.png"));}
        }}
        Entry added=c.Entries[1];string realTarget=c.Pets[added.Id].Config.Target;c.Remove(added);
        if(c.Pets.Count!=1||!Directory.Exists(realTarget))throw new Exception("removing entry touched target");
        foreach(PetWindow w in c.Pets.Values){w.OpenSettings();}
        Application.DoEvents();foreach(Form f in Application.OpenForms){if(f.Text.EndsWith(" · 设置"))using(Bitmap bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"settings-preview.png"));}}
        using(ManualWindow manual=new ManualWindow()){
            manual.Show();Application.DoEvents();
            if(!manual.Controls[0].Text.Contains("双击")||!manual.Controls[0].Text.Contains("简易 / 标准 / 进阶"))throw new Exception("manual content");
            using(Bitmap bmp=new Bitmap(manual.Width,manual.Height)){manual.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"manual-preview.png"));}
        }
        c.Shutdown();Program.Wake.Dispose();Console.WriteLine("PASS: edition names; embedded manual; actual double-click launch; single-click/drag do not launch; two targets; GIF; resize; drag persistence; range; safe removal; shutdown");
    }
    static void TestDrag(PetWindow w){
        Rectangle screen=Screen.PrimaryScreen.WorkingArea;
        if(w.Moving){w.Config.AreaX=screen.Right-450;w.Config.AreaY=screen.Bottom-350;w.Config.AreaW=400;w.Config.AreaH=300;w.ApplySize();}
        Point origin=w.Location,offset=new Point(20,20);
        Point pointer=new Point(origin.X+20,origin.Y+20);
        w.BeginDrag(pointer,offset);w.UpdateDrag(new Point(pointer.X+1,pointer.Y+1));
        if(w.Location!=origin||!w.EndDrag(true))throw new Exception("tiny movement must remain click");
        Point drop=new Point(screen.Left+40,screen.Top+40);
        w.BeginDrag(pointer,offset);w.UpdateDrag(new Point(drop.X+20,drop.Y+20));
        if(w.Location!=drop)throw new Exception("drag must reach outside original range");
        typeof(PetWindow).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,new object[]{null,EventArgs.Empty});
        if(w.Location!=drop)throw new Exception("timer snapped drag to old range");
        if(w.EndDrag(true))throw new Exception("drag must never open target");
        if(w.Location!=drop)throw new Exception("drop snapped");
        if(w.Moving&&(w.Config.AreaW!=400||w.Config.AreaH!=300))throw new Exception("drag resized activity area");
        using(PetWindow reloaded=new PetWindow(w.Entry)){
            if(reloaded.Location!=drop)throw new Exception("drag position not restored");
            reloaded.Quit();
        }
        pointer=new Point(w.Left+20,w.Top+20);w.BeginDrag(pointer,offset);
        if(w.EndDrag(false))throw new Exception("capture loss must not open target");
    }
    static int LaunchCount(string file){return File.Exists(file)?File.ReadAllLines(file).Length:0;}
    static void TestDoubleClick(PetWindow w){
        string previous=w.Config.Target,marker=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"launch-probe-count.txt");
        w.Config.Target=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LaunchProbe.exe");
        Point offset=new Point(20,20),pointer=new Point(w.Left+20,w.Top+20);
        MethodInfo up=typeof(PetWindow).GetMethod("Up",BindingFlags.Instance|BindingFlags.NonPublic);
        MethodInfo dbl=typeof(Control).GetMethod("OnMouseDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic);
        int before=LaunchCount(marker);
        w.BeginDrag(pointer,offset);up.Invoke(w,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,20,20,0)});
        if(LaunchCount(marker)!=before)throw new Exception("single click launched target");
        w.BeginDrag(pointer,offset);dbl.Invoke(w,new object[]{new MouseEventArgs(MouseButtons.Left,2,20,20,0)});
        DateTime until=DateTime.UtcNow.AddSeconds(5);while(LaunchCount(marker)==before&&DateTime.UtcNow<until)Thread.Sleep(25);
        if(LaunchCount(marker)!=before+1)throw new Exception("double click did not launch target exactly once");
        up.Invoke(w,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,20,20,0)});
        pointer=new Point(w.Left+20,w.Top+20);w.BeginDrag(pointer,offset);w.UpdateDrag(new Point(pointer.X+30,pointer.Y+30));up.Invoke(w,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,50,50,0)});
        pointer=new Point(w.Left+20,w.Top+20);w.BeginDrag(pointer,offset);dbl.Invoke(w,new object[]{new MouseEventArgs(MouseButtons.Left,2,20,20,0)});w.EndDrag(false);
        if(LaunchCount(marker)!=before+1)throw new Exception("drag armed an app launch");
        w.Config.Target=previous;w.Save();
    }
}
