"""Reproducible original vector-style folder mascot; Pillow is only needed to regenerate assets."""
from PIL import Image, ImageDraw
from pathlib import Path
import math
root=Path(__file__).resolve().parents[1]/"assets"
def frame(i=0,icon=False):
    s=3
    im=Image.new("RGBA",(256*s,256*s),(0,0,0,0));d=ImageDraw.Draw(im)
    def rr(box,r,fill,outline=None,width=1):d.rounded_rectangle(tuple(int(v*s) for v in box),radius=r*s,fill=fill,outline=outline,width=width*s)
    def el(box,fill):d.ellipse(tuple(int(v*s) for v in box),fill=fill)
    def line(points,fill,w):d.line([(int(x*s),int(y*s)) for x,y in points],fill=fill,width=w*s)
    if icon:rr((8,8,248,248),56,"#FFF3DA")
    y=0 if icon else round(math.sin(i*math.pi/12)*3)
    el((55,217,201,232),"#EBD8B4")
    rr((62,194+y,94,223+y),13,"#745540");rr((163,194+y,195,223+y),13,"#745540")
    rr((34,56+y,118,118+y),14,"#EAAA40","#B77B35",3)
    rr((32,78+y,224,205+y),22,"#EDB853","#B77B35",3)
    rr((45,86+y,211,180+y),10,"#FFFBE9")
    rr((30,103+y,226,209+y),23,"#FFD47C","#B77B35",3)
    line([(53,114+y),(199,114+y)],"#FFE8AE",4)
    el((58,161+y,87,176+y),"#F29D86");el((168,161+y,197,176+y),"#F29D86")
    if i in (10,11):
        line([(80,149+y),(94,149+y)],"#5B4537",5);line([(162,149+y),(176,149+y)],"#5B4537",5)
    else:
        el((82,137+y,94,155+y),"#5B4537");el((162,137+y,174,155+y),"#5B4537")
        el((84,139+y,88,144+y),"#FFF9E5");el((164,139+y,168,144+y),"#FFF9E5")
    d.arc((112*s,(150+y)*s,144*s,(174+y)*s),0,180,fill="#5B4537",width=4*s)
    # Mint leaf badge makes the mascot recognizable at shortcut sizes.
    el((186,46+y,215,77+y),"#88BDA4");line([(191,71+y),(211,49+y)],"#4D8D79",3)
    return im.resize((256,256),Image.Resampling.LANCZOS)
frame().save(root/"folder.png")
frame(icon=True).save(root/"app.ico",sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
frames=[]
for i in range(24):
    rgba=frame(i);rgb=Image.new("RGB",rgba.size,"#FF00FF");rgb.paste(rgba,mask=rgba.getchannel("A").point(lambda a:255 if a>=128 else 0))
    pal=rgb.quantize(colors=255);palette=pal.getpalette();palette[765:768]=[255,0,255];pal.putpalette(palette)
    mask=rgba.getchannel("A").point(lambda a:255 if a<128 else 0);pal.paste(255,mask=mask);pal.info["transparency"]=255;frames.append(pal)
frames[0].save(root/"folder.gif",save_all=True,append_images=frames[1:],duration=90,loop=0,transparency=255,disposal=2,optimize=False)
