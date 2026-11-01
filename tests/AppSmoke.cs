using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
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
        foreach(PetWindow w in c.Pets.Values){w.Config.Size=128;w.ApplySize();w.Save();if(w.Width!=140)throw new Exception("resize");w.LoadGif();w.Rescue();}
        foreach(Form f in Application.OpenForms){if(f.Text=="蹦蹦图标 · 所有应用"){
            using(Bitmap bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"manager-preview.png"));}
        }}
        Entry added=c.Entries[1];string realTarget=c.Pets[added.Id].Config.Target;c.Remove(added);
        if(c.Pets.Count!=1||!Directory.Exists(realTarget))throw new Exception("removing entry touched target");
        foreach(PetWindow w in c.Pets.Values){w.OpenSettings();}
        Application.DoEvents();foreach(Form f in Application.OpenForms){if(f.Text.EndsWith(" · 设置"))using(Bitmap bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"settings-preview.png"));}}
        c.Shutdown();Program.Wake.Dispose();Console.WriteLine("PASS: native app initialization; two independent targets; GIF import; resize/save; manager/settings render; safe entry removal; shutdown");
    }
}
