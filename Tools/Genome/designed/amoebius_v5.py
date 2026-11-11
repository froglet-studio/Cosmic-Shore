import sys
sys.path.insert(0, __import__('os').path.join(__import__('os').path.dirname(__file__), '..'))
from agenome.genome import Genome
from agenome.handmade import _named as N
def build(mode="skim", seed=5, hud=True):
    g=Genome(seed=seed,meta={"lineage":"designed","name":"amoebius_v5_"+mode})
    w=g.add("tex.perlin",**N("tex.perlin",freq=3,oct=4,persist=0.5,seed=3,gamma=1.0))
    b=g.add("tex.perlin",**N("tex.perlin",freq=5,oct=6,persist=0.6,seed=11,gamma=2.2,ah=0.68,as_=0.95,av=0.012,bh=0.63,bs=0.8,bv=0.2))
    d=g.addv("tex.distort",b,w,amount=0.3)
    c=g.add("tex.cell",**N("tex.cell",count=64,seed=7,mode=2,gamma=1.0,ah=0.63,as_=0.85,av=0.09,bh=0.62,bs=0.6,bv=0.01))
    sky=g.addv("tex.blend",d,c,mode=0,amount=0.55)
    if mode=="skim":
        co=g.addv("ag.course",kind=0,radius=700,gates=8,perturb=0,mouth=24,seed=0)
        a=g.addv("ag.rail",co,lanes=1,offset=0,twist=0,sx=10,sy=1,sz=3,spacing=12,state=2,colour=0,marker=2,emit=0.3,h=0.64,s=0.6,v=1.2)
        a=g.addv("ag.crystal",a,jitter=35,radius=24,inset=26,lift=0,emit=1.5)
    else:
        co=g.addv("ag.course",kind=2,radius=820,gates=8,perturb=200,mouth=88,seed=3)
        a=g.addv("ag.rail",co,lanes=3,offset=22,twist=1,sx=6,sy=6,sz=8,spacing=8,state=2,colour=1,marker=1,emit=0.3,h=0.64,s=0.6,v=1.2)
        a=g.addv("ag.gate",a,emit=1.5,h=0.62,s=0.7,v=1.0)
    a=g.addv("ag.vessel",a,count=3,domains=3,cruise=60,minspeed=0,turn=120,roll=130,rotthr=0,follow=1.5,accel=1.5,boostmax=5,decay=0.3,ha=0.58,hb=0.795,hc=0.10,sat=1.0,val=1.4)
    a=g.addv("ag.pilot",a,law=1,look=0.85,lookmin=40,lookmax=455,lead=2.5,maxlead=23,gain=0.084,height=4.4,thrlo=0.2,thrhi=1.0,bend=300,bank=2.0)
    a=g.addv("ag.skim",a,radius=7.5,add=0.1,pickup=0.8)
    a=g.addv("ag.lay",a,wave=7,sx=20,sy=0.75,sz=5.5,gap=18.5,offset=0,delay=2,grow=1.25,emit=0.5)
    a=g.addv("ag.shards",a,spacing=120,radius=1200,scale=10,emit=0.15,h=0.67,s=0.25,v=0.45)
    a=g.addv("ag.body",a,shape=2,sx=3.2,sy=2.2,sz=4,emit=0.8,colormode=1,edge=0.6,h=0.6,s=0.1,v=1.6)
    cam=g.addv("cam.attach",a,agent=0,dist=17,height=0,fov=90)
    cam=g.addv("cam.tunnel",cam,lo=70,hi=280,fovdrop=25,panini=0.7,pdrop=0.5)
    post=g.add("post.grade",**N("post.grade",exposure=1.0,thresh=0.7,bloom=0.35,bloomrad=2.0,fh=0.68,fs=0.8,fv=0.03,fog=0.00002,vignette=0.2,sat=1.2,contrast=1.1,feedback=0.0,grain=0.0,aberr=0.0))
    h=None
    if hud:
        h=g.add("hud.text",**N("hud.text",anchor=0,x=0.02,y=0.03,size=0.012,chars=18,rows=1,seed=4,h=0.0,s=0.0,v=0.9,alpha=0.8))
        h=g.add("hud.ring",h,**N("hud.ring",anchor=2,x=-0.07,y=0.09,r=0.028,thick=0.005,arc=1.0,h=0.55,s=0.3,v=1.0,alpha=0.9))
        h=g.add("hud.bar",h,**N("hud.bar",anchor=8,x=-0.20,y=-0.17,w=0.36,hh=0.075,segs=5,fill=1.0,h=0.64,s=0.5,v=0.25,alpha=0.8))
        h=g.add("hud.bar",h,**N("hud.bar",anchor=8,x=-0.20,y=-0.08,w=0.36,hh=0.075,segs=5,fill=1.0,h=0.64,s=0.5,v=0.25,alpha=0.8))
    g.add("out",None,a,cam,post,sky,h,**N("out",sky=1.0,steps=16,far=4000,ltheta=0.9,lphi=0.7,ambient=0.3,lh=0.6,ls=0.15,lv=1.0,warmup=20))
    return g.prune().repair()
if __name__=="__main__":
    g=build(sys.argv[2] if len(sys.argv)>2 else "skim"); g.save(sys.argv[1]); print(g.size(), len(g.nodes))
