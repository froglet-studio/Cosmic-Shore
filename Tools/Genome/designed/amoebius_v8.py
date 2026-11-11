import sys
sys.path.insert(0,'/home/user/g')
from agenome.genome import Genome
from agenome.handmade import _named as N
import sky1
def build(mode="skim", seed=5, hud=True):
    g=Genome(seed=seed,meta={"lineage":"designed","name":"amoebius_v8_"+mode})
    sky=sky1.sky(g)
    if mode=="skim":
        co=g.addv("ag.course",kind=1,radius=700,gates=8,perturb=247,mouth=20,seed=0)
        a=g.addv("ag.rail",co,lanes=1,offset=0,twist=0,sx=30.75,sy=3.075,sz=9.225,spacing=12.2,state=2,colour=0,marker=2,emit=0.3,h=0.6587,s=0.88,v=1.54)
        a=g.addv("ag.crystal",a,jitter=35,radius=20,inset=26,lift=0,emit=1.5,ring=3.0)
    else:
        co=g.addv("ag.course",kind=2,radius=820,gates=8,perturb=200,mouth=88,seed=3)
        a=g.addv("ag.rail",co,lanes=3,offset=22,twist=1,sx=18.45,sy=18.45,sz=24.4,spacing=8,state=2,colour=1,marker=1,emit=0.3,h=0.64,s=0.6,v=1.2)
    a=g.addv("ag.vessel",a,count=3,domains=(1 if mode=='skim' else 3),cruise=60,minspeed=0,turn=120,roll=130,rotthr=0,follow=1.5,accel=1.5,boostmax=5,decay=0.3,ha=0.628,hb=(0.795 if mode=='skim' else 0.684),hc=(0.10 if mode=='skim' else 0.023),sat=1.0,val=(1.322 if mode=='skim' else 1.6))
    a=g.addv("ag.pilot",a,law=1,look=0.85,lookmin=40,lookmax=455,lead=2.5,maxlead=23,gain=0.084,height=4.4,thrlo=0.2,thrhi=1.0,bend=300,bank=2.0)
    a=g.addv("ag.skim",a,radius=7.5,add=0.1,pickup=0.8)
    a=g.addv("ag.lay",a,wave=8.4,sx=20,sy=0.83,sz=6.08,gap=18.34,offset=0,delay=2,grow=1.25,emit=0.5)
    a=g.addv("ag.impact",a,hull=1.5,slow=0.5,dur=1.0,fresh=1.5,wobble=0,period=1.0)
    a=g.addv("ag.shards",a,spacing=120,radius=1200,scale=10,emit=0.15,h=0.67,s=0.25,v=0.45)
    a=g.addv("ag.jet",a,secs=0.5,width=0.3,x=1.45,z=-0.5,ramp=0,alpha=1,soft=1,vmax=2000,domain=1,h=0.55,s=0.9,v=1.1)
    a=g.addv("ag.jet",a,secs=4,width=1.0,x=4,z=-12,ramp=0.5,alpha=0.42,soft=0,vmax=2000,domain=1,h=0.503,s=1,v=0.75)
    a=g.addv("ag.body",a,shape=2,sx=4.5,sy=2.2,sz=6.7,emit=0.3,colormode=1,edge=0.2,h=0.62,s=0.4,v=0.5)
    cam=g.addv("cam.attach",a,agent=0,dist=17,height=0,fov=90)
    cam=g.addv("cam.tunnel",cam,lo=70,hi=280,fovdrop=25,panini=0.7,pdrop=0.5)
    post=g.addv("post.urp",thresh=0.2,intensity=2.5,scatter=0.7,exposure=1.0)
    h=None
    if hud:
        h=g.add("hud.text",**N("hud.text",anchor=0,x=0.02,y=0.03,size=0.012,chars=18,rows=1,seed=4,h=0.0,s=0.0,v=0.9,alpha=0.8))
        h=g.add("hud.ring",h,**N("hud.ring",anchor=2,x=-0.07,y=0.09,r=0.028,thick=0.005,arc=1.0,h=0.55,s=0.3,v=1.0,alpha=0.9))
        h=g.add("hud.bar",h,**N("hud.bar",anchor=8,x=-0.20,y=-0.17,w=0.36,hh=0.075,segs=5,fill=1.0,h=0.64,s=0.5,v=0.25,alpha=0.8))
        h=g.add("hud.bar",h,**N("hud.bar",anchor=8,x=-0.20,y=-0.08,w=0.36,hh=0.075,segs=5,fill=1.0,h=0.64,s=0.5,v=0.25,alpha=0.8))
    g.add("out",None,a,cam,post,sky,h,**N("out",sky=1.0,steps=16,far=4000,ltheta=0.9,lphi=0.7,ambient=0.3,lh=0.6,ls=0.15,lv=1.0,warmup=0))
    return g.prune().repair()
if __name__=="__main__":
    g=build(sys.argv[2] if len(sys.argv)>2 else "skim"); g.save(sys.argv[1]); print(g.size(), len(g.nodes))
