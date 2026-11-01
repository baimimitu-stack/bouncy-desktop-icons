using System;
using System.Collections.Generic;
using BouncyIcons;
class CoreTests {
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
    static void Main(){try{Run();}catch(Exception e){Console.WriteLine("FAIL: "+e.Message);Environment.ExitCode=1;}}
    static void Run(){
        Bounds area=new Bounds(-1200,-100,640,400);var foods=new List<Food>();
        PetBrain b=new PetBrain(123,area,new Vec(-4000,9000));
        for(int i=0;i<200000;i++){
            b.Step(.05,foods,false,true);
            Check(b.Position.X>=area.X&&b.Position.X+b.Size<=area.X+area.Width,"x boundary");
            Check(b.Position.Y>=area.Y&&b.Position.Y+b.Size<=area.Y+area.Height,"y boundary");
            Check(b.Hunger>=0&&b.Hunger<=100&&b.Energy>=0&&b.Energy<=100,"needs bounded");
        }
        Vec p=b.Position;for(int i=0;i<100;i++)b.Step(.1,foods,true,true);Check(p.Distance(b.Position)==0,"hover pause");
        b.Reset(new Bounds(0,0,300,300),new Vec(100,100));b.Hunger=90;b.Energy=100;
        foods.Add(new Food("visual-only","folder",new Vec(100,100),true));bool eaten=false;
        for(int i=0;i<50;i++){b.Step(.05,foods,false,true);if(b.ConsumedFoodId=="visual-only"){eaten=true;break;}}
        Check(eaten,"food consumed event");Check(b.Hunger<60,"hunger decreases");
        foods.Clear();b.Energy=0;b.Step(.05,foods,false,true);Check(b.State==PetState.Sleep,"sleep when tired");
        for(int i=0;i<200;i++)b.Step(.05,foods,false,true);Check(b.Energy>50,"sleep recovers");
        b.Reset(new Bounds(0,0,96,96),new Vec(9999,-999));b.Size=96;
        for(int i=0;i<100;i++)b.Step(.1,foods,false,true);Check(b.Position.Distance(new Vec(0,0))==0,"zero movement area");
        b.Reset(new Bounds(0,0,1000,1000),new Vec(0,0));b.Size=80;b.Hunger=90;b.Speed=10;
        foods.Add(new Food("far","grass",new Vec(900,900),false));p=b.Position;b.Step(9000,foods,false,true);Check(p.Distance(b.Position)<=1.01,"suspend dt clamp");
        PetBrain a=new PetBrain(55,area,new Vec(-1000,0)),c=new PetBrain(55,area,new Vec(-1000,0));foods.Clear();
        for(int i=0;i<1000;i++){a.Step(.05,foods,false,false);c.Step(.05,foods,false,false);Check(a.Position.Distance(c.Position)<.00001,"deterministic replay");}
        Console.WriteLine("PASS: 200000 bounded steps, needs, pause, food, sleep, tiny area, suspend, deterministic replay");
    }
}
