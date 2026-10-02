"""Record a hierarchical run around pilots and build a self-contained viewer of expand / collapse.

    python -m hierarchy.viewer [--T 90] [--pilots 2] [--out results/hierarchy_viewer.html]

What you see:
  dim green/red puffs   IMPOSTORS: each region's macro population drawn as a few representatives, each sized by
                        the headcount it stands for (drawn volume = bodies it stands for)
  bright points         AGENTS: individuals simulated near a pilot; a fresh one is drawn sliding out of the
                        representative it came from (emerge), a newborn grows from nothing (bloom)
  white diamonds        pilots (with a forward cone of what they can SEE)
  faint boxes           HOT regions (expanded)
  floor tint            flora per region (the shared field)
Frames are packed binary (int16 positions, base64) and agents are matched by id between frames and lerped, so the
playback is smooth at a 5 fps record rate. Space pauses, F toggles follow-cam, I/A/B toggle impostors/agents/boxes.
"""
from __future__ import annotations

import argparse
import base64
import json
import os

import numpy as np

from .params import Params
from .sim import HierSim, Pilot

OUT = os.path.join(os.path.dirname(__file__), "results")


def b64(a):
    return base64.b64encode(np.ascontiguousarray(a).tobytes()).decode()


def record(T=60.0, pilots=2, seed=11, every=4, imp_every=5, n_herb=45000, n_pred=3000, P=None):
    P = P or Params()
    sim = HierSim(P, seed=seed)
    sim.populate(n_herb, n_pred)
    for i in range(pilots):
        p = sim.add_pilot(Pilot.wanderer(speed=110.0 + 40 * i))
        p.turn = 0.8
    q = 32767.0 / sim.W.R
    frames = []
    stats = []
    pop_metric = []                 # drawn body volume near each pilot, per frame (continuity check)
    for i in range(int(T / P.dt_micro)):
        sim.step()
        if i % every:
            continue
        out = {}
        sim.render(out)
        A = sim.A
        ag = out.get("agents")
        n = A.n
        f = dict(t=round(sim.t, 2))
        if ag is not None and n:
            f["aid"] = b64(A.view("id").astype(np.uint32))
            f["apos"] = b64(np.round(ag["pos"] * q).astype(np.int16))
            f["asp"] = b64(A.view("sp").astype(np.uint8))
            f["asz"] = b64(np.round(np.asarray(ag["size"]) * 10).astype(np.uint8))
        if len(frames) % imp_every == 0:        # impostors drift slowly: keyframe them
            imp = []
            for k, name in enumerate(("impostor_herb", "impostor_pred")):
                b = out.get(name)
                if b is None:
                    imp.append(("", "")); continue
                imp.append((b64(np.round(b["pos"] * q).astype(np.int16)),
                            b64(np.clip(np.round(np.asarray(b["size"]) * 4), 0, 65535).astype(np.uint16))))
            f["imp"] = imp
        f["pil"] = [np.round(p.pos, 1).tolist() + np.round(p.vel / max(np.linalg.norm(p.vel), 1e-9), 3).tolist()
                    for p in sim.arena.pilots]
        f["hot"] = b64(np.packbits(sim.hot))
        fl = (sim.W.F.sum(1) / max(sim.W.vox_ok.sum(1).max(), 1) / P.flora_cap * 255)
        f["flora"] = b64(np.clip(fl, 0, 255).astype(np.uint8))
        frames.append(f)
        s = sim.summary()
        stats.append(dict(t=round(sim.t, 1), herb=s["herb"]["count"], pred=s["pred"]["count"], agents=int(A.n),
                          hot=int(sim.hot.sum()), expand=int(sim.events["expand"]), absorb=int(sim.events["absorb"])))
        # continuity metric: drawn volume (size^3) within 450 u of each pilot, agents + impostors
        row = []
        for p in sim.arena.pilots:
            tot = 0.0
            for b in out.values():
                d = np.linalg.norm(np.asarray(b["pos"]) - p.pos, axis=1)
                tot += float((np.asarray(b["size"]) ** 3 * (d < 450)).sum())
            row.append(tot)
        pop_metric.append(row)
    meta = dict(R=sim.W.R, L=P.L, centers=np.round(sim.W.centers, 1).tolist(), q=q, every_s=every * P.dt_micro,
                stats=stats, events=dict((k, float(v)) for k, v in sim.events.items()))
    return meta, frames, np.array(pop_metric)


TPL = r"""<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Hierarchical Ecology Viewer</title>
<style>
:root{--bg:#0a0c13;--fg:#d9ddef;--mute:#8088ab;--panel:#141a29;--line:#29314d;--acc:#7fd1ff}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--bg:#0a0c13}}
:root[data-theme="dark"]{--bg:#0a0c13}
html,body{margin:0;height:100%;background:var(--bg);color:var(--fg);font:13px/1.45 system-ui,sans-serif;overflow:hidden}
#ui{position:fixed;left:12px;right:12px;top:12px;display:flex;flex-wrap:wrap;gap:8px;align-items:center;z-index:2}
#ui button,#ui input{background:var(--panel);color:var(--fg);border:1px solid var(--line);border-radius:6px;padding:4px 9px}
#ui button.on{border-color:var(--acc);color:var(--acc)}
#hud{position:fixed;right:12px;top:52px;background:color-mix(in srgb,var(--panel) 88%,transparent);border:1px solid var(--line);border-radius:8px;padding:8px 10px;min-width:220px;z-index:2;font-variant-numeric:tabular-nums}
#hud b{color:var(--acc);font-weight:600}
#leg{position:fixed;left:12px;bottom:12px;max-width:62ch;color:var(--mute);z-index:2}
#leg span{display:inline-block;width:9px;height:9px;border-radius:50%;margin:0 4px 0 10px;vertical-align:middle}
canvas{display:block}
@media (max-width:600px){#hud{top:auto;bottom:70px;right:12px;left:12px}#leg{font-size:11px}}
</style></head><body>
<div id="ui"><button id="play">pause</button><input id="scrub" type="range" min="0" value="0" style="flex:1;min-width:120px">
<button id="fol" class="on">follow pilot</button><button id="bimp" class="on">impostors</button><button id="bag" class="on">agents</button><button id="bhot" class="on">hot regions</button></div>
<div id="hud"></div>
<div id="leg"><span style="background:#58d070"></span>grazers <span style="background:#ff5a4a"></span>predators (bright = agents, dim puffs = macro impostors sized by headcount)
<span style="background:#fff"></span>pilots. Boxes = expanded regions. Drag to orbit, scroll to zoom.</div>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const D = __DATA__;
const M = D.meta, F = D.frames, q = M.q;
function dec(s, T){ if(!s) return new T(0); const b=atob(s); const u=new Uint8Array(b.length); for(let i=0;i<b.length;i++)u[i]=b.charCodeAt(i); return new T(u.buffer); }
const scene=new THREE.Scene(), cam=new THREE.PerspectiveCamera(55,innerWidth/innerHeight,1,20000);
const ren=new THREE.WebGLRenderer({antialias:true}); ren.setPixelRatio(Math.min(2,devicePixelRatio)); ren.setSize(innerWidth,innerHeight); document.body.appendChild(ren.domElement);
cam.position.set(0,1100,2600); const ctl=new THREE.OrbitControls(cam,ren.domElement);
addEventListener('resize',()=>{cam.aspect=innerWidth/innerHeight;cam.updateProjectionMatrix();ren.setSize(innerWidth,innerHeight)});
scene.add(new THREE.Mesh(new THREE.SphereGeometry(M.R,48,24),new THREE.MeshBasicMaterial({color:0x2a3a6a,wireframe:true,transparent:true,opacity:0.06})));
function mat(add){return new THREE.ShaderMaterial({transparent:true,depthWrite:false,blending:add?THREE.AdditiveBlending:THREE.NormalBlending,
 vertexShader:`attribute float size;attribute vec3 col;attribute float alpha;varying vec3 vC;varying float vA;void main(){vC=col;vA=alpha;vec4 mv=modelViewMatrix*vec4(position,1.);gl_PointSize=clamp(size*900./-mv.z,1.2,90.);gl_Position=projectionMatrix*mv;}`,
 fragmentShader:`varying vec3 vC;varying float vA;void main(){vec2 d=gl_PointCoord-.5;float r=dot(d,d);if(r>.25)discard;gl_FragColor=vec4(vC*(1.25-2.4*r),vA*(1.-3.*r));}`})}
function cloud(n,add){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(n*3),3));
 g.setAttribute('col',new THREE.BufferAttribute(new Float32Array(n*3),3));g.setAttribute('size',new THREE.BufferAttribute(new Float32Array(n),1));
 g.setAttribute('alpha',new THREE.BufferAttribute(new Float32Array(n),1));const p=new THREE.Points(g,mat(add));p.frustumCulled=false;scene.add(p);return p}
let maxA=1,maxI=1; for(const f of F){ if(f.aid) maxA=Math.max(maxA,atob(f.aid).length/4); if(f.imp){let s=0; for(const im of f.imp) s+=atob(im[1]||'').length/2; maxI=Math.max(maxI,s)}}
const ag=cloud(maxA,true), im=cloud(maxI,false), pil=cloud(8,true), flo=cloud(M.centers.length,false);
const C=M.centers; {const P=flo.geometry.attributes.position.array; for(let i=0;i<C.length;i++)P.set(C[i],i*3); flo.geometry.attributes.position.needsUpdate=true}
// hot boxes
const boxG=new THREE.BoxGeometry(M.L,M.L,M.L), boxE=new THREE.EdgesGeometry(boxG);
const boxes=C.map(c=>{const l=new THREE.LineSegments(boxE,new THREE.LineBasicMaterial({color:0x7fd1ff,transparent:true,opacity:0.18}));l.position.set(c[0],c[1],c[2]);l.visible=false;scene.add(l);return l});
// decode frames lazily, cache
const cache=new Map();
function fr(i){ if(cache.has(i)) return cache.get(i); const f=F[i]; const o={t:f.t,id:dec(f.aid,Uint32Array),pos:dec(f.apos,Int16Array),sp:dec(f.asp,Uint8Array),sz:dec(f.asz,Uint8Array),
  imp:(()=>{let j=i; while(!F[j].imp) j--; return F[j].imp.map(x=>[dec(x[0],Int16Array),dec(x[1],Uint16Array)])})(),pil:f.pil,hot:dec(f.hot,Uint8Array),flora:dec(f.flora,Uint8Array)};
  const m=new Map(); for(let j=0;j<o.id.length;j++) m.set(o.id[j],j); o.map=m; cache.set(i,o); if(cache.size>40){cache.delete(cache.keys().next().value)} return o }
let k=0,u=0,playing=true,follow=true,showI=true,showA=true,showH=true;
const scrub=document.getElementById('scrub'); scrub.max=F.length-1;
function tog(id,fn){const b=document.getElementById(id);b.onclick=()=>{const v=fn();b.classList.toggle('on',v)}}
tog('fol',()=>follow=!follow); tog('bimp',()=>showI=!showI); tog('bag',()=>showA=!showA); tog('bhot',()=>showH=!showH);
document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'pause':'play'};
scrub.oninput=()=>{k=+scrub.value;u=0};
addEventListener('keydown',e=>{if(e.code==='Space'){e.preventDefault();document.getElementById('play').click()} if(e.key==='f')document.getElementById('fol').click();
 if(e.key==='i')document.getElementById('bimp').click(); if(e.key==='a')document.getElementById('bag').click(); if(e.key==='b')document.getElementById('bhot').click()});
const HC=[[0.36,1.0,0.48],[1.0,0.36,0.3]], IC=[[0.22,0.55,0.3],[0.6,0.22,0.18]];
function draw(){
 const a=fr(k), b=fr(Math.min(k+1,F.length-1)), s=1/q;
 // agents lerp by id
 const P=ag.geometry.attributes.position.array, Cc=ag.geometry.attributes.col.array, Z=ag.geometry.attributes.size.array, Al=ag.geometry.attributes.alpha.array;
 let n=0; if(showA){ for(let j=0;j<a.id.length;j++){ const jb=b.map.get(a.id[j]); let x=a.pos[j*3]*s,y=a.pos[j*3+1]*s,z=a.pos[j*3+2]*s;
   if(jb!==undefined){x+=(b.pos[jb*3]*s-x)*u;y+=(b.pos[jb*3+1]*s-y)*u;z+=(b.pos[jb*3+2]*s-z)*u}
   P[n*3]=x;P[n*3+1]=y;P[n*3+2]=z; Cc.set(HC[a.sp[j]],n*3); Z[n]=a.sz[j]/10*2.2; Al[n]=1; n++ } }
 for(let j=n;j<Z.length;j++){Z[j]=0;Al[j]=0}
 for(const t of ['position','col','size','alpha']) ag.geometry.attributes[t].needsUpdate=true;
 // impostors (no id: draw frame a)
 const IP=im.geometry.attributes.position.array, ICc=im.geometry.attributes.col.array, IZ=im.geometry.attributes.size.array, IA=im.geometry.attributes.alpha.array; let m=0;
 if(showI) for(let sp=0;sp<2;sp++){const [pp,ss]=a.imp[sp]; for(let j=0;j<ss.length;j++){IP[m*3]=pp[j*3]*s;IP[m*3+1]=pp[j*3+1]*s;IP[m*3+2]=pp[j*3+2]*s;ICc.set(IC[sp],m*3);IZ[m]=ss[j]/4*1.4;IA[m]=0.55;m++}}
 for(let j=m;j<IZ.length;j++){IZ[j]=0;IA[j]=0}
 for(const t of ['position','col','size','alpha']) im.geometry.attributes[t].needsUpdate=true;
 // flora
 const FC=flo.geometry.attributes.col.array, FZ=flo.geometry.attributes.size.array, FA=flo.geometry.attributes.alpha.array;
 for(let j=0;j<C.length;j++){const v=a.flora[j]/255;FC.set([0.12+0.1*v,0.22+0.35*v,0.12],j*3);FZ[j]=60+140*v;FA[j]=0.10+0.18*v}
 for(const t of ['col','size','alpha']) flo.geometry.attributes[t].needsUpdate=true;
 // hot boxes
 for(let j=0;j<boxes.length;j++) boxes[j].visible=showH && ((a.hot[j>>3]>>(7-(j&7)))&1)===1;
 // pilots
 const PP=pil.geometry.attributes.position.array, PC=pil.geometry.attributes.col.array, PZ=pil.geometry.attributes.size.array, PA=pil.geometry.attributes.alpha.array;
 for(let j=0;j<8;j++){ if(j<a.pil.length){const p0=a.pil[j],p1=(b.pil[j]||p0); PP.set([p0[0]+(p1[0]-p0[0])*u,p0[1]+(p1[1]-p0[1])*u,p0[2]+(p1[2]-p0[2])*u],j*3);PC.set([1,1,1],j*3);PZ[j]=26;PA[j]=1}else{PZ[j]=0;PA[j]=0} }
 for(const t of ['position','col','size','alpha']) pil.geometry.attributes[t].needsUpdate=true;
 if(follow&&a.pil.length){const p=a.pil[0]; const tgt=new THREE.Vector3(PP[0],PP[1],PP[2]); const back=new THREE.Vector3(p[3],p[4],p[5]).multiplyScalar(-260).add(new THREE.Vector3(0,110,0));
   ctl.target.lerp(tgt,0.15); cam.position.lerp(tgt.clone().add(back),0.06)}
 const st=M.stats[k]; document.getElementById('hud').innerHTML=`t <b>${st.t.toFixed(1)} s</b><br>grazers <b>${st.herb}</b> · predators <b>${st.pred}</b><br>agents (individuals) <b>${st.agents}</b><br>macro (in impostors) <b>${st.herb+st.pred-st.agents}</b><br>hot regions <b>${st.hot}</b> / ${C.length}<br>expanded ${st.expand} · absorbed ${st.absorb}`;
}
let last=performance.now();
(function loop(now){requestAnimationFrame(loop);const dt=(now-last)/1000;last=now;
 if(playing){u+=dt/M.every_s;while(u>=1){u-=1;k=(k+1)%F.length;scrub.value=k}}
 draw();ctl.update();ren.render(scene,cam)})(performance.now());
</script></body></html>"""


def build(out, meta, frames):
    with open(out, "w") as fh:
        fh.write(TPL.replace("__DATA__", json.dumps(dict(meta=meta, frames=frames), separators=(",", ":"))))
    return os.path.getsize(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--T", type=float, default=60.0)
    ap.add_argument("--pilots", type=int, default=2)
    ap.add_argument("--out", default=os.path.join(OUT, "hierarchy_viewer.html"))
    a = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    meta, frames, popm = record(a.T, a.pilots)
    meta["drawn_volume_near_pilot"] = popm[:, 0].round(1).tolist()
    print(build(a.out, meta, frames), "bytes ->", a.out)
    d = np.abs(np.diff(popm, axis=0)) / np.maximum(popm[:-1], 1)
    print("drawn-volume near pilot: max frame-to-frame relative change", float(d.max()), "p99", float(np.quantile(d, 0.99)))


if __name__ == "__main__":
    main()
