using System;
using System.Collections.Generic;

namespace BouncyIcons {
    // No filesystem, UI, clocks or platform calls. Inputs + dt -> state + position.
    public struct Vec {
        public double X, Y;
        public Vec(double x,double y) { X=x;Y=y; }
        public double Distance(Vec b) { return Math.Sqrt((X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y)); }
    }
    public struct Bounds {
        public double X,Y,Width,Height;
        public Bounds(double x,double y,double w,double h) { X=x;Y=y;Width=w;Height=h; }
        public Vec Clamp(Vec p,double size) {
            return new Vec(Math.Max(X,Math.Min(p.X,X+Math.Max(0,Width-size))),Math.Max(Y,Math.Min(p.Y,Y+Math.Max(0,Height-size))));
        }
    }
    public enum PetState { Idle, Wander, SeekFood, Eat, Sleep }
    public sealed class Food {
        public string Id,Label;
        public Vec Position;
        public bool Folder;
        public Food(string id,string label,Vec p,bool folder) { Id=id;Label=label;Position=p;Folder=folder; }
    }
    public sealed class PetBrain {
        readonly Random random;
        Vec target;
        double stateLeft;
        string foodId;
        public Vec Position;
        public Bounds Area;
        public double Size=80,Speed=45,Hunger=35,Energy=100;
        public PetState State=PetState.Idle;
        public string ConsumedFoodId;
        public PetBrain(int seed,Bounds area,Vec start) { random=new Random(seed);Area=area;Position=area.Clamp(start,Size);stateLeft=1; }
        public void Reset(Bounds area,Vec start) { Area=area;Position=area.Clamp(start,Size);State=PetState.Idle;stateLeft=.5;foodId=null; }
        Vec RandomPoint() { return new Vec(Area.X+random.NextDouble()*Math.Max(0,Area.Width-Size),Area.Y+random.NextDouble()*Math.Max(0,Area.Height-Size)); }
        void BeginWander(){State=PetState.Wander;target=RandomPoint();stateLeft=6+random.NextDouble()*5;}
        void Move(Vec goal,double dt) {
            goal=Area.Clamp(goal,Size);double d=Position.Distance(goal),step=Math.Min(d,Math.Max(0,Speed)*dt);
            if(d>.001)Position=new Vec(Position.X+(goal.X-Position.X)*step/d,Position.Y+(goal.Y-Position.Y)*step/d);
            Position=Area.Clamp(Position,Size);
        }
        public void Step(double dt,IList<Food> foods,bool paused,bool professional) {
            ConsumedFoodId=null;
            Position=Area.Clamp(Position,Size);
            if(paused)return;
            dt=Math.Max(0,Math.Min(.1,dt)); // no teleport after suspend or a blocked UI
            Hunger=Math.Min(100,Hunger+dt*1.4);
            stateLeft-=dt;
            if(State==PetState.Sleep){Energy=Math.Min(100,Energy+dt*10);if(stateLeft<=0){State=PetState.Idle;stateLeft=2;}return;}
            Energy=Math.Max(0,Energy-dt*.5);
            if(State==PetState.Eat){
                if(stateLeft<=0){ConsumedFoodId=foodId;foodId=null;Hunger=Math.Max(0,Hunger-40);Energy=Math.Min(100,Energy+12);State=PetState.Idle;stateLeft=2;}
                return;
            }
            if(professional && Energy<15){State=PetState.Sleep;stateLeft=8;return;}
            Food nearest=null;double best=double.MaxValue;
            if(professional && Hunger>=25){foreach(Food f in foods){double d=Position.Distance(Area.Clamp(f.Position,Size));if(d<best){nearest=f;best=d;}}}
            if(nearest!=null){State=PetState.SeekFood;foodId=nearest.Id;target=nearest.Position;}
            else if(State==PetState.SeekFood){State=PetState.Idle;stateLeft=.5;foodId=null;}
            if(State==PetState.SeekFood || State==PetState.Wander){
                Move(target,dt);
                if(Position.Distance(Area.Clamp(target,Size))<4){
                    if(State==PetState.SeekFood){State=PetState.Eat;stateLeft=1.6;}
                    else {State=PetState.Idle;stateLeft=1+random.NextDouble()*3;}
                } else if(State==PetState.Wander&&stateLeft<=0){State=PetState.Idle;stateLeft=1;}
            }else if(stateLeft<=0)BeginWander();
        }
    }
}
