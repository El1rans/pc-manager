import sys, pathlib
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
out = pathlib.Path(sys.argv[1]); out.mkdir(exist_ok=True)
NAVY=(0x1E,0x2A,0x44); AMBER=(0xF5,0xB9,0x42); FLAME=(0xFF,0xF6,0xDE); FIRE=(0xE8,0x6A,0x17)
SS=8  # supersampling

def cubic(p0,p1,p2,p3,n=24):
    return [tuple((1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t**2*c+t**3*d for a,b,c,d in zip(p0,p1,p2,p3)) for t in [i/n for i in range(n+1)]]

def flame_poly(cx, top, bottom_r, bottom_cy):
    # teardrop: tip at (cx,top), round bottom circle radius r centered (cx,bottom_cy)
    import math
    r=bottom_r; pts=[]
    # right side curve tip -> right of circle
    pts += cubic((cx,top),(cx+r*0.75,top+ (bottom_cy-top)*0.45),(cx+r,bottom_cy-r*0.5),(cx+r,bottom_cy))
    for i in range(1,25):
        a=math.pi*i/24; pts.append((cx+r*math.cos(a), bottom_cy+r*math.sin(a)))
    pts += cubic((cx-r,bottom_cy),(cx-r,bottom_cy-r*0.5),(cx-r*0.75,top+(bottom_cy-top)*0.45),(cx,top))
    return pts

def overlay(img, fn, alpha):
    layer=Image.new('RGBA', img.size, (0,0,0,0)); d=ImageDraw.Draw(layer); fn(d)
    if alpha<1:
        a=layer.getchannel('A').point(lambda v:int(v*alpha)); layer.putalpha(a)
    img.alpha_composite(layer)

def glow(img, cx, cy, r, alpha):
    layer=Image.new('RGBA', img.size, (0,0,0,0)); d=ImageDraw.Draw(layer)
    d.ellipse([cx-r,cy-r,cx+r,cy+r], fill=AMBER+(int(255*alpha),))
    layer=layer.filter(ImageFilter.GaussianBlur(r*0.45)); img.alpha_composite(layer)

def fire(P):
    pts=[]
    pts+=cubic((33.2,26.2),(35.8,29.6),(36.2,32.4),(36.2,35.6))
    import math
    for i in range(1,25):
        a=math.pi*i/24; pts.append((32+4.2*math.cos(a), 35.6+4.2*math.sin(a)))
    pts+=cubic((27.8,35.6),(27.8,32.4),(30,30.6),(30.9,28.6))
    pts+=cubic((30.9,28.6),(31.6,30.2),(32.9,28.8),(33.2,26.2))
    return [P(*q) for q in pts]

def lantern(img, ox, oy, k, glow_on=True, glow_scale=1.0):
    P=lambda x,y:(ox+x*k, oy+y*k)
    if glow_on:
        cx,cy=P(32,34); glow(img,cx,cy,24*k*glow_scale,.55)
    d=ImageDraw.Draw(img)
    d.line([P(32,9.5),P(32,16.5)], fill=AMBER, width=max(1,int(3*k)))
    for y in (9.5,16.5): d.ellipse([*P(32-1.5,y-1.5),*P(32+1.5,y+1.5)], fill=AMBER)
    d.polygon([P(25,16.5),P(39,16.5),P(42.2,22),P(21.8,22)], fill=AMBER)
    d.rounded_rectangle([*P(23,22),*P(41,43.5)], radius=4.5*k, fill=AMBER)
    d.rounded_rectangle([*P(26.2,24.8),*P(37.8,40.8)], radius=3*k, fill=FLAME)
    d.polygon(fire(P), fill=FIRE)
    d.rounded_rectangle([*P(26,45),*P(38,48.6)], radius=1.8*k, fill=AMBER)

def icon(size, glow=True):
    S=size*SS; k=S/64
    img=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(img)
    d.rounded_rectangle([0,0,S-1,S-1], radius=14*k, fill=NAVY)
    lantern(img,0,0,k,glow)
    mask=Image.new('L',(S,S),0); ImageDraw.Draw(mask).rounded_rectangle([0,0,S-1,S-1], radius=14*k, fill=255)
    img.putalpha(ImageChops.multiply(img.getchannel('A'), mask))
    return img.resize((size,size), Image.LANCZOS)

def icon_small(size):
    # pixel-tuned 16-unit design
    S=size*SS; k=S/16
    img=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(img); P=lambda x,y:(x*k,y*k)
    d.rounded_rectangle([0,0,S-1,S-1], radius=3.5*k, fill=NAVY)
    d.polygon([P(5.2,3.5),P(10.8,3.5),P(12.1,5.7),P(3.9,5.7)], fill=AMBER)
    d.rounded_rectangle([*P(4,5.7),*P(12,13)], radius=1.6*k, fill=AMBER)
    d.rounded_rectangle([*P(5.1,6.8),*P(10.9,12)], radius=.9*k, fill=FLAME); d.polygon([P(*p) for p in flame_poly(8,7.6,1.35,10.3)], fill=FIRE)
    d.rounded_rectangle([*P(5.8,13.4),*P(10.2,14.7)], radius=.65*k, fill=AMBER)
    return img.resize((size,size), Image.LANCZOS)

sizes={}
for s in (256,128,64,48,40,32):
    sizes[s]=icon(s, glow=s>=32); sizes[s].save(out/f'icon-{s}.png')
for s in (24,20,16):
    sizes[s]=icon_small(s); sizes[s].save(out/f'icon-{s}.png')
ico_order=[256,128,64,48,40,32,24,20,16]
sizes[256].save(out/'porchlight.ico', format='ICO', sizes=[(s,s) for s in ico_order], append_images=[sizes[s] for s in ico_order[1:]])
# The PIL ICO writer resizes from the first image; write our own to keep hand-tuned small sizes
import io, struct
entries=[]; blobs=[]
for s in ico_order:
    b=io.BytesIO(); sizes[s].save(b,'PNG'); blobs.append(b.getvalue())
off=6+16*len(ico_order); hdr=struct.pack('<HHH',0,1,len(ico_order)); dirs=b''
for s,blob in zip(ico_order,blobs):
    dirs+=struct.pack('<BBBBHHII', s%256, s%256, 0,0,1,32,len(blob),off); off+=len(blob)
(out/'porchlight.ico').write_bytes(hdr+dirs+b''.join(blobs))

# Logos (2x) with Segoe UI
SEMI=r'C:\Windows\Fonts\seguisb.ttf'; REG=r'C:\Windows\Fonts\segoeui.ttf'
def logo(mode, scale=2):
    W,H=520*scale,128*scale; img=Image.new('RGBA',(W*SS//4,H*SS//4),(0,0,0,0))
    f=SS//4; ic=icon(128*scale).resize((128*scale*f,128*scale*f), Image.LANCZOS)
    img.alpha_composite(ic,(0,0))
    d=ImageDraw.Draw(img)
    txt,sub=((0x1E,0x2A,0x44),(0x5B,0x62,0x70)) if mode=='light' else ((0xF3,0xF4,0xF6),(0xA3,0xA9,0xB5))
    F1=ImageFont.truetype(SEMI, int(58*scale*f)); F2=ImageFont.truetype(REG, int(21*scale*f))
    d.text((156*scale*f,72*scale*f), 'Porchlight', font=F1, fill=txt, anchor='ls')
    d.text((158*scale*f,106*scale*f), 'We leave the light on for you.', font=F2, fill=sub, anchor='ls')
    return img.resize((W,H), Image.LANCZOS)
logo('light').save(out/'logo-light.png'); logo('dark').save(out/'logo-dark.png')

# Inno Setup wizard images (BMP, 100% and 200%)
def wizard(scale):
    W,H=164*scale,314*scale; f=4; S=(W*f,H*f)
    img=Image.new('RGBA',S,NAVY+(255,))
    k=1.5*scale*f
    glow(img, 82*scale*f, 118*scale*f, 95*scale*f, .18)
    lantern(img, 34*scale*f, 58*scale*f, k, True)
    d=ImageDraw.Draw(img)
    d.text((82*scale*f,222*scale*f),'Porchlight',font=ImageFont.truetype(SEMI,int(25*scale*f)),fill=(0xF3,0xF4,0xF6),anchor='ms')
    d.text((82*scale*f,246*scale*f),'We leave the light on for you.',font=ImageFont.truetype(REG,int(10.5*scale*f)),fill=(0xC9,0xCE,0xD8),anchor='ms')
    return img.resize((W,H),Image.LANCZOS).convert('RGB')
for sc in (1,2): wizard(sc).save(out/f'wizard-{sc}00.bmp')
def wizsmall(px):
    bg=Image.new('RGB',(px,px),(255,255,255)); ic=icon(px); bg.paste(ic,(0,0),ic); return bg
for sc,px in ((1,55),(2,110)): wizsmall(px).save(out/f'wizard-small-{sc}00.bmp')

# Preview sheet
sheet=Image.new('RGB',(1000,620),(246,247,249)); d=ImageDraw.Draw(sheet)
x=20
for sz in (256,64,48,32,24,20,16):
    sheet.paste(sizes[sz],(x,20),sizes[sz]); x+=sz+24
bar=Image.new('RGB',(560,48),(32,32,32)); x=16
for sz in (32,24,20,16): bar.paste(sizes[sz],(x,(48-sz)//2),sizes[sz]); x+=sz+22
sheet.paste(bar,(310,120))
ll=Image.open(out/'logo-light.png').resize((520,128),Image.LANCZOS); sheet.paste(ll,(20,300),ll)
dark=Image.new('RGB',(580,160),(21,23,27)); ldk=Image.open(out/'logo-dark.png').resize((520,128),Image.LANCZOS); dark.paste(ldk,(20,16),ldk); sheet.paste(dark,(0,450))
wz=Image.open(out/'wizard-100.bmp'); sheet.paste(wz,(760,290)); ws=Image.open(out/'wizard-small-100.bmp'); sheet.paste(ws,(680,290))
sheet.save(out/'preview.png'); print('ok')
