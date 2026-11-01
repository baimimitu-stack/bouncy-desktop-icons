using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BouncyIcons {
    sealed class SpeechWindow:Form {
        string message="";
        public SpeechWindow(){
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            BackColor=Color.Magenta;TransparencyKey=Color.Magenta;Size=new Size(210,64);
            Font=new Font("Microsoft YaHei",9);DoubleBuffered=true;
        }
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x20|0x08000000|0x80;return p;}}
        public void Follow(PetWindow pet,string text,bool lower){
            if(message!=text){message=text;Invalidate();}TopMost=pet.TopMost;
            Rectangle wa=Screen.FromControl(pet).WorkingArea;
            int y=lower?pet.Bottom+4:pet.Top-Height-4;if(y<wa.Top)y=pet.Bottom+4;
            Location=new Point(Math.Max(wa.Left,Math.Min(pet.Left+pet.Width/2-Width/2,wa.Right-Width)),Math.Max(wa.Top,Math.Min(y,wa.Bottom-Height)));
            if(!Visible)Show(pet);
        }
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=new GraphicsPath()){
                path.AddArc(1,1,20,20,180,90);path.AddArc(Width-22,1,20,20,270,90);
                path.AddArc(Width-22,Height-22,20,20,0,90);path.AddArc(1,Height-22,20,20,90,90);path.CloseFigure();
                using(var fill=new SolidBrush(Color.FromArgb(255,248,229)))e.Graphics.FillPath(fill,path);
                using(var pen=new Pen(Color.FromArgb(213,167,98)))e.Graphics.DrawPath(pen,path);
            }
            TextRenderer.DrawText(e.Graphics,message,Font,new Rectangle(12,8,Width-24,Height-16),Color.FromArgb(94,65,45),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
        }
    }
}
