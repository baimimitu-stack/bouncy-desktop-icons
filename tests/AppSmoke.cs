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
        TestMigration(Path.Combine(Program.DataDir,"migration-test"));
        Program.Wake=new EventWaitHandle(false,EventResetMode.AutoReset,"BouncyIconsSmoke-"+Guid.NewGuid());
        LauncherContext c=new LauncherContext();Application.DoEvents();
        string gif=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","folder.gif");
        c.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"",gif);
        if(c.Pets.Count!=2)throw new Exception("multiple entries");
        foreach(PetWindow w in c.Pets.Values){w.Config.Size=128;w.ApplySize();w.Save();if(w.Width!=140)throw new Exception("resize");w.LoadGif();w.Rescue();TestDrag(w);TestDoubleClick(w);}
        foreach(Form f in Application.OpenForms){if(f is ManagerWindow){
            if(!f.Text.Contains("管理器"))throw new Exception("edition name missing");
            using(Bitmap bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"manager-preview.png"));}
        }}
        TestInteractions(c,args[0]);TestReactions(c,args[0]);
        Entry added=c.Entries[1];string realTarget=c.Pets[added.Id].Config.Target;c.Remove(added);
        if(c.Pets.Count!=1||!Directory.Exists(realTarget))throw new Exception("removing entry touched target");
        foreach(PetWindow w in c.Pets.Values){w.OpenSettings();}
        Application.DoEvents();foreach(Form f in Application.OpenForms){if(f.Text.EndsWith(" · 设置"))using(Bitmap bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"settings-preview.png"));}}
        using(ManualWindow manual=new ManualWindow()){
            manual.Show();Application.DoEvents();
            if(!manual.Controls[0].Text.Contains("双击")||!manual.Controls[0].Text.Contains("简易 / 标准 / 进阶"))throw new Exception("manual content");
            using(Bitmap bmp=new Bitmap(manual.Width,manual.Height)){manual.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(args[0],"manual-preview.png"));}
        }
        foreach(string mode in new[]{"pro","lite","standard","lite"}){
            PetWindow pet=c.Pets[c.Entries[0].Id];string target=pet.Config.Target,image=pet.Config.Gif;int size=pet.Config.Size;
            c.SetMode(mode);Application.DoEvents();Program.LoadMode();
            if(Program.Edition!=mode||pet.Config.Target!=target||pet.Config.Gif!=image||pet.Config.Size!=size||c.Pets.Count!=1)throw new Exception("mode switch lost settings");
            if(pet.Moving!=(mode!="lite")||pet.Pro!=(mode=="pro"))throw new Exception("mode behavior");
        }
        c.Shutdown();Program.Wake.Dispose();Console.WriteLine("PASS: multi-GIF import/persistence/random choice/preview/restore; single-click reaction; double-click and drag cancellation; social proximity, toggles, cooldown; edition names; embedded manual; actual double-click launch; single-click/drag do not launch; two targets; GIF; resize; drag persistence; range; safe removal; shutdown");
    }
    static void TestReactions(LauncherContext c,string output){
        c.SetMode("pro");var pet=c.Pets[c.Entries[0].Id];pet.ClearInteraction();
        string original=pet.Config.Gif,target=pet.Config.Target,source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","folder.gif");
        var first=pet.Reactions.Add(source,"Happy");var second=pet.Reactions.Add(source,"Happy");first.Text="自定义开心";first.Seconds=1;pet.Reactions.Save();
        var reload=new ReactionLibrary(Path.Combine(Program.DataDir,pet.Entry.Id));if(reload.Clips.Count!=2||reload.Clips[0].Text!="自定义开心")throw new Exception("reaction persistence");
        var chosen=new System.Collections.Generic.HashSet<string>();for(int i=0;i<100;i++)chosen.Add(reload.Pick("Happy").Id);if(chosen.Count!=2)throw new Exception("multiple reaction selection");
        bool rejected=false;try{pet.Reactions.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"BouncyIcons.exe"),"Happy");}catch{rejected=true;}if(!rejected||pet.Reactions.Clips.Count!=2)throw new Exception("invalid GIF import");
        pet.Social.React("Happy","hello");pet.SyncReaction(0);if(pet.ActiveReactionId==null)throw new Exception("mood did not play GIF");
        pet.PreviewReaction(first);if(pet.ActiveReactionId!=first.Id||pet.Social.Speech!="自定义开心")throw new Exception("reaction preview or custom words");
        for(int i=0;i<12;i++)pet.SyncReaction(.1);
        if(pet.ActiveReactionId!=null||pet.Config.Gif!=original||pet.Config.Target!=target)throw new Exception("reaction restoration");
        using(var window=new ReactionWindow(pet)){window.Show();Application.DoEvents();using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"reactions-preview.png"));}}
        using(var chat=new ChatWindow(pet)){chat.Show();Application.DoEvents();using(var image=new Bitmap(chat.Width,chat.Height)){chat.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"chat-preview.png"));}}
        pet.PreviewReaction(first);c.SetMode("lite");if(pet.ActiveReactionId!=null)throw new Exception("mode switch did not restore GIF");
        pet.Reactions.Remove(first);if(File.Exists(pet.Reactions.FileFor(first))||!File.Exists(source))throw new Exception("remove reaction touched source");
        File.Delete(pet.Reactions.FileFor(second));if(pet.Reactions.Pick("Happy")!=null)throw new Exception("missing GIF fallback");
        pet.Reactions.Remove(second);c.SetMode("pro");
        string damaged=Path.Combine(Program.DataDir,"damaged");Directory.CreateDirectory(Path.Combine(damaged,"reactions"));string index=Path.Combine(damaged,"reactions","library.json");File.WriteAllText(index,"broken");var invalid=new ReactionLibrary(damaged);if(invalid.LoadError==null||invalid.Pick("Happy")!=null)throw new Exception("damaged library isolation");
    }
    static void TestInteractions(LauncherContext c,string output){
        c.SetMode("pro");PetWindow a=c.Pets[c.Entries[0].Id],b=c.Pets[c.Entries[1].Id];
        a.Config.Roaming=b.Config.Roaming=false;a.ClearInteraction();b.ClearInteraction();
        var up=typeof(PetWindow).GetMethod("Up",BindingFlags.Instance|BindingFlags.NonPublic);
        Point pointer=new Point(a.Left+20,a.Top+20);
        a.BeginDrag(pointer,new Point(20,20));up.Invoke(a,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,20,20,0)});
        int count=a.Social.ClickCount;a.ProcessPendingClick(0);if(a.Social.ClickCount!=count)throw new Exception("click not deferred");
        a.ProcessPendingClick(double.MaxValue);if(a.Social.ClickCount!=count+1)throw new Exception("single click reaction");
        a.ClearInteraction();a.BeginDrag(pointer,new Point(20,20));up.Invoke(a,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,20,20,0)});
        a.BeginDrag(pointer,new Point(20,20));if(!a.AcceptDoubleClick())throw new Exception("double click recognition");
        a.ProcessPendingClick(double.MaxValue);if(a.Social.ClickCount!=count+1)throw new Exception("double click reacted");
        a.BeginDrag(pointer,new Point(20,20));a.UpdateDrag(new Point(pointer.X+40,pointer.Y+40));up.Invoke(a,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,60,60,0)});
        a.ProcessPendingClick(double.MaxValue);if(a.Social.ClickCount!=count+1)throw new Exception("drag reacted");
        a.ClearInteraction();b.ClearInteraction();b.Location=new Point(a.Left+500,a.Top);if(a.Meet(b))throw new Exception("distant encounter");
        b.Location=new Point(a.Right+10,a.Top);b.Config.Social=false;if(a.Meet(b))throw new Exception("disabled social");b.Config.Social=true;
        if(!a.Meet(b)||a.Social.Speech==""||b.Social.Speech=="")throw new Exception("nearby encounter");
        if(a.Meet(b))throw new Exception("encounter spam");
        var tick=typeof(PetWindow).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic);tick.Invoke(a,new object[]{null,EventArgs.Empty});tick.Invoke(b,new object[]{null,EventArgs.Empty});
        foreach(Form f in Application.OpenForms){if(f is SpeechWindow)using(Bitmap image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"speech-preview.png"));break;}}
        c.SetMode("lite");if(a.Social.Speech!=""||a.Meet(b))throw new Exception("mode did not stop social");
        c.SetMode("pro");a.Config.Interactions=false;a.BeginDrag(pointer,new Point(20,20));up.Invoke(a,new object[]{null,new MouseEventArgs(MouseButtons.Left,1,20,20,0)});a.ProcessPendingClick(double.MaxValue);
        if(a.Social.ClickCount!=count+1)throw new Exception("disabled clicks");a.Config.Interactions=true;
    }
    static void TestMigration(string root){
        var json=new System.Web.Script.Serialization.JavaScriptSerializer();string id=new string('a',32);
        foreach(string mode in new[]{"lite","pro"}){
            string old=Path.Combine(root,mode),profile=Path.Combine(old,id);Directory.CreateDirectory(profile);
            string gif=Path.Combine(profile,"custom.gif");File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","folder.gif"),gif);
            File.WriteAllText(Path.Combine(old,"entries.json"),json.Serialize(new[]{new Entry{Id=id,Name=mode}}));
            File.WriteAllText(Path.Combine(profile,"settings.json"),json.Serialize(new Preferences{Gif=gif,Target="test-target",Size=144}));
        }
        string shared=SharedData.Prepare(root);
        var entries=json.Deserialize<Entry[]>(File.ReadAllText(Path.Combine(shared,"entries.json")));
        if(entries.Length!=2||entries[0].Id==entries[1].Id)throw new Exception("migration collision");
        foreach(Entry e in entries){var cfg=json.Deserialize<Preferences>(File.ReadAllText(Path.Combine(shared,e.Id,"settings.json")));
            if(cfg.Target!="test-target"||cfg.Size!=144||!File.Exists(cfg.Gif)||!cfg.Gif.StartsWith(shared))throw new Exception("migration content");}
        string before=File.ReadAllText(Path.Combine(shared,"entries.json"));SharedData.Prepare(root);
        if(before!=File.ReadAllText(Path.Combine(shared,"entries.json"))||!Directory.Exists(Path.Combine(root,"lite")))throw new Exception("migration repeat or original deleted");
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
    static int LaunchCount(string file){for(int i=0;i<100;i++){try{return File.Exists(file)?File.ReadAllLines(file).Length:0;}catch(IOException){Thread.Sleep(10);}}throw new IOException("launch probe stayed locked");}
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
