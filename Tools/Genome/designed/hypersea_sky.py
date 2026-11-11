import sys,os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
os.environ.setdefault("KERAS_BACKEND","jax")
import colorsys, math, numpy as np
from agenome.genome import Genome
from agenome.ops import OPS
from agenome.handmade import _named as N
def lin(c): return [((x+0.055)/1.055)**2.4 if x>0.04045 else x/12.92 for x in c]
def col(pre,c,k=1.0,linear=True):
    c=lin(c) if linear else list(c); c=[x*k for x in c]
    h,s,v=colorsys.rgb_to_hsv(*[x/max(max(c),1e-9) for x in c]); v=max(c)
    return {pre+"h":h,pre+"s":s,pre+"v":v}
def sph(v):
    v=np.array(v,float); v/=np.linalg.norm(v); return math.acos(v[1]), math.atan2(v[2],v[0])%(2*math.pi)
def sky(g, linear=True):
    nt,nph=sph([0.1,1,0.15]); ct,cp=sph([1,-0.1,0.3]); at,ap=sph([-0.5,0.35,0.8])
    C=lambda p,c,k=1.0: col(p,c,k,linear)
    H=lambda p,c,k=1.0: col(p,c,k,False)  # [HDR] properties reach the shader as stored
    a=g.addv("sky.base",**C("d",[0.008,0.005,0.035]),**C("a",[0.03,0.025,0.08],0.14),ntheta=nt,nphi=nph,width=0.047,drift=0.1)
    a=g.addv("sky.band",a,**C("c",[0.3773585,0.26409683,0.11510615],0.43),**H("e",[0.4,0.48,0.72],0.43),nscale=13.4,nstr=0.876)
    a=g.addv("sky.stars",a,density=67.3,bright=0.24,base=0,boost=0.089,conc=2)
    a=g.addv("sky.nebula",a,scale=2.5,**H("a",[0.023529422,0.027056675,0.5176471],0.4),**H("b",[0.04,0.25,0.55],0.4),**H("c",[0.2126609,0.078431375,0.5019608],0.4))
    a=g.addv("sky.dust",a,scale=4.99,strength=0.945)
    a=g.addv("sky.core",a,theta=ct,phi=cp,**H("c",[0.9811321,0.76181436,0.39800632],1.29),**H("h",[0.754717,0.53432167,0.32514533],1.29*0.25),size=0.029,halo=0.303)
    a=g.addv("sky.galaxy",a,theta=at,phi=ap,**H("c",[0.78616345,0.62676924,0.16563806],0.15),**H("n",[0.78616345,0.4853058,0.2892487],0.15),size=0.4)
    a=g.addv("sky.web",a,scale=12.1,sharp=18.95,interior=0.15,**C("",[0,0.050716996,0.70440245],0.229))
    a=g.addv("sky.haze",a,**C("",[0,0.02679205,1],0.02),height=0.698,falloff=8)
    return a
def build(linear=True, post=None):
    g=Genome(seed=1,meta={"lineage":"designed","name":"hypersea_sky"})
    s=sky(g,linear)
    cam=g.addv("cam.tunnel",g.addv("cam.attach",None,agent=0,dist=17,height=0,fov=90),lo=70,hi=280,fovdrop=25,panini=0.7,pdrop=0.5)
    pp=dict(exposure=1.0,thresh=0.7,bloom=0.35,bloomrad=2.0,fh=0.68,fs=0.8,fv=0.03,fog=0.00001,vignette=0.0,sat=1.0,contrast=1.0,feedback=0.0,grain=0.0,aberr=0.0)
    pp.update(post if isinstance(post,dict) else {})
    p=g.add("post.grade",**N("post.grade",**pp)) if post!="urp" else g.addv("post.urp",thresh=0.2,intensity=2.5,scatter=0.68,exposure=1.0)
    g.add("out",None,None,cam,p,s,None,**N("out",sky=1.0,steps=16,far=4000,ltheta=0.9,lphi=0.7,ambient=0.3,lh=0.6,ls=0.15,lv=1.0,warmup=0))
    return g
if __name__=="__main__":
    from agenome.render import Renderer
    from agenome.replay import Session
    from agenome.paired import look_loss
    from PIL import Image
    S=Session('/mnt/project-files/amoebius-genome/targets/sessions/switchback_i2_s7',frames=[0,337,674])
    real=S.real("_sky")
    R=Renderer(w=256,h=144,ss=int(os.environ.get("SS","5")))
    rows=[np.concatenate(list(real),1)]
    for linear in (True,):
        g=build(linear,"urp"); print(g.size())
        fr=R.render(g,3,replay=S)
        print(linear,[round(look_loss(a,b)[0],4) for a,b in zip(fr,real)])
        rows.append(np.concatenate(list(fr),1))
    Image.fromarray(np.concatenate(rows,0)).resize((768*2,432*2)).save('/home/user/v/sky1.jpg',quality=88)
