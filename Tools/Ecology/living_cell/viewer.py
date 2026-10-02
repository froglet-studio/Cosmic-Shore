"""Build living_cell/viewer.html: a whole living cell with a pilot touring it.

    python3 -m living_cell.viewer [seed] [minutes_recorded]

One self-contained HTML (three.js 0.128 + OrbitControls from jsDelivr, like common/viewer.py), data inlined as
base64 Int16 arrays. What it shows:
  * every species in its own colour (bright = in its striking state), macro cohorts as faint region clouds
    (the far, collapsed populations), prisms by kind (flora green, trail in its pilot's domain colour,
    skeleton grey, hoard gold, fortress wall rose, physarum tube amber, trap body olive, danger orange)
  * the explorer pilot (white, with a short wake) and the wanderer (grey); camera can follow the explorer
  * a timeline under the scene: the explorer's ENCOUNTERS (a tick per encounter in the species' colour,
    filled when it turned active), HITS, and the EMOTION probe's top label per 8 s window with the threat
    curve - scrub it to jump.
"""
from __future__ import annotations

import base64
import json
import os
import sys

import numpy as np

from .cell import Cell
from .metrics import Flight

HERE = os.path.dirname(os.path.abspath(__file__))
SPECIES = ["grazer", "locust", "pack", "thief", "lurker", "fortress", "snaptrap", "physarum"]
COL = {"grazer": [0.45, 0.95, 1.0], "locust": [0.45, 0.95, 0.35], "pack": [0.95, 0.3, 0.25], "thief": [0.7, 0.7, 1.0],
       "lurker": [0.8, 1.0, 0.5], "fortress": [0.95, 0.4, 0.55], "snaptrap": [0.55, 0.8, 0.3], "physarum": [1.0, 0.7, 0.15]}
ACT = {"grazer": [0.7, 1.0, 1.0], "locust": [1.0, 0.82, 0.1], "pack": [1.0, 0.05, 0.05], "thief": [1.0, 0.9, 0.35],
       "lurker": [1.0, 0.1, 0.15], "fortress": [1.0, 0.1, 0.3], "snaptrap": [1.0, 0.9, 0.2], "physarum": [1.0, 1.0, 0.5]}


def b64(a, dt):
    return base64.b64encode(np.ascontiguousarray(a, dt).tobytes()).decode()


def record(seed=1, cfg=None, burn=600.0, seconds=300.0, every=0.5, dt=0.1):
    c = Cell(seed=seed, cfg=cfg, pilots=("explore", "wander"))
    while c.w.t < burn:
        c.step(dt)
    fl = Flight(c, 0, t0=c.w.t, seconds=seconds)
    frames, keys = [], []
    nxt, nkey = c.w.t, c.w.t
    while not fl.done():
        c.step(dt); fl.update()
        w = c.w
        if w.t + 1e-9 >= nxt:
            nxt += every
            sets = c.threat_sets()
            P, S, A = [], [], []
            for si, sp in enumerate(SPECIES):
                if sp in ("snaptrap", "physarum", "fortress") and sp in sets:
                    pts, act = sets[sp]
                    if sp == "fortress":
                        pts, act = pts[1:], act[1:]
                elif sp in c.guilds:
                    pts, _, _, act = c.guilds[sp].threat_agents()
                else:
                    continue
                if sp == "pack":
                    act = c.guilds["pack"].intent[c.guilds["pack"].alive] > 0.3
                P.append(pts); S.append(np.full(len(pts), si)); A.append(act)
            P = np.concatenate(P) if P else np.zeros((0, 3)); S = np.concatenate(S) if S else np.zeros(0)
            A = np.concatenate(A) if A else np.zeros(0)
            imp = []
            for si, sp in enumerate(SPECIES[:5]):
                if sp in c.guilds:
                    cen, cnt = c.guilds[sp].macro_render()
                    imp += [(x[0], x[1], x[2], si, n) for x, n in zip(cen, cnt)]
            imp = np.array(imp) if imp else np.zeros((0, 5))
            frames.append(dict(t=round(w.t - fl.t0, 1), p=[np.round(p.pos, 1).tolist() for p in w.pilots],
                               a=b64(np.round(P * 4), np.int16), s=b64(S + 8 * A, np.uint8),
                               i=b64(np.round(imp[:, :3] * 4), np.int16), ic=b64(np.c_[imp[:, 3], np.minimum(imp[:, 4], 255)], np.uint8)))
        if w.t + 1e-9 >= nkey:
            nkey += 10.0
            n = w.n
            idx = np.flatnonzero(w.alive[:n])
            if len(idx) > 20000:
                idx = np.sort(w.rng.choice(idx, 20000, replace=False))
            k = w.kind[idx].astype(np.int64)
            style = np.where(k == 1, 7 + np.clip(w.dom[idx], 1, 3), k)          # trail -> 8,9,10 by domain
            style = np.where(w.danger[idx], 11, style)
            keys.append(dict(t=round(w.t - fl.t0, 1), m=b64(np.round(w.pos[idx] * 4), np.int16), k=b64(style, np.uint8)))
    summ = fl.summary(emo=True)
    hits = [dict(t=round(e[0] - fl.t0, 1), sp=e[2], kind=e[3]) for e in c.w.events if e[1] == 0 and fl.t0 <= e[0] <= fl.t1]
    meta = dict(seed=seed, cfg=cfg or {}, every=every, summary={k: v for k, v in summ.items() if k not in ("events", "seq", "emotion_series")},
                encounters=summ["events"], emotion=summ.get("emotion_series", []), hits=hits, species=SPECIES,
                col=[COL[s] for s in SPECIES], act=[ACT[s] for s in SPECIES], R=c.w.R, nucleus=c.w.nucleus, L=c.w.L)
    return dict(meta=meta, frames=frames, keys=keys)


TPL = r"""<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Living Cell</title>
<style>
:root{--bg:#0a0c13;--fg:#dde2f5;--mute:#8a92b6;--line:#262c45;--panel:#11152299}
:root[data-theme="light"]{--bg:#0a0c13}
body{margin:0;background:var(--bg);color:var(--fg);font:13px/1.4 system-ui,sans-serif;overflow:hidden}
#ui{position:fixed;left:12px;top:10px;right:12px;display:flex;gap:8px;flex-wrap:wrap;align-items:center;z-index:2}
button,select{background:#151a2a;color:var(--fg);border:1px solid #2c3354;border-radius:6px;padding:4px 9px;font:inherit}
#leg{display:flex;gap:6px;flex-wrap:wrap}#leg span{display:inline-flex;align-items:center;gap:4px;padding:2px 6px;border-radius:5px;cursor:pointer;border:1px solid #2c3354}
#leg i{width:10px;height:10px;border-radius:50%;display:inline-block}
#tl{position:fixed;left:0;right:0;bottom:0;height:150px;background:var(--panel);border-top:1px solid var(--line);z-index:2}
#tl canvas{width:100%;height:100%;display:block;cursor:pointer}
#sum{position:fixed;right:12px;top:48px;max-width:330px;background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:8px 10px;font-size:12px;z-index:2;color:var(--mute)}
#sum b{color:var(--fg)}
#t{font-variant-numeric:tabular-nums;min-width:9ch}
@media (max-width:700px){#sum{display:none}}
</style>
<div id="ui"><button id="play">pause</button><button id="follow">follow pilot: on</button><span id="t"></span><div id="leg"></div></div>
<div id="sum"></div>
<div id="tl"><canvas id="tlc"></canvas></div>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const D=__DATA__, M=D.meta;
function i16(s){const b=atob(s),a=new Uint8Array(b.length);for(let i=0;i<b.length;i++)a[i]=b.charCodeAt(i);return new Int16Array(a.buffer)}
function u8(s){const b=atob(s),a=new Uint8Array(b.length);for(let i=0;i<b.length;i++)a[i]=b.charCodeAt(i);return a}
const KC=[[.25,.55,.25],[.5,.5,.5],[.45,.45,.45],[.95,.75,.2],[.85,.4,.5],[.85,.6,.15],[.45,.5,.25],[.5,.5,.5],[.1,.75,.7],[1,.3,.4],[1,.8,.25],[1,.45,.1]];
const KS=[3,3,3,4,5,5,5,3,3,3,3,5];
const scene=new THREE.Scene(), cam=new THREE.PerspectiveCamera(55,innerWidth/(innerHeight-150),1,20000);
const ren=new THREE.WebGLRenderer({antialias:true});ren.setPixelRatio(Math.min(2,devicePixelRatio));ren.setSize(innerWidth,innerHeight-150);document.body.appendChild(ren.domElement);
cam.position.set(0,900,2600);const ctl=new THREE.OrbitControls(cam,ren.domElement);
addEventListener('resize',()=>{cam.aspect=innerWidth/(innerHeight-150);cam.updateProjectionMatrix();ren.setSize(innerWidth,innerHeight-150);drawTL()});
scene.add(new THREE.Mesh(new THREE.SphereGeometry(M.R,48,24),new THREE.MeshBasicMaterial({color:0x2a3a6a,wireframe:true,transparent:true,opacity:0.06})));
scene.add(new THREE.Mesh(new THREE.SphereGeometry(M.nucleus,24,12),new THREE.MeshBasicMaterial({color:0x5060a0,wireframe:true,transparent:true,opacity:0.12})));
function mat(add){return new THREE.ShaderMaterial(Object.assign(add?{transparent:true,depthWrite:false,blending:THREE.AdditiveBlending}:{},{
 vertexShader:`attribute float size;attribute vec3 col;varying vec3 vC;void main(){vC=col;vec4 mv=modelViewMatrix*vec4(position,1.);gl_PointSize=clamp(size*900./-mv.z,1.2,60.);gl_Position=projectionMatrix*mv;}`,
 fragmentShader:`varying vec3 vC;void main(){vec2 d=gl_PointCoord-.5;float r=dot(d,d);if(r>.25)discard;gl_FragColor=vec4(vC*(1.15-2.*r),1.);}`}))}
function cloud(n,add){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(n*3),3));
 g.setAttribute('col',new THREE.BufferAttribute(new Float32Array(n*3),3));g.setAttribute('size',new THREE.BufferAttribute(new Float32Array(n),1));const p=new THREE.Points(g,mat(add));p.frustumCulled=false;scene.add(p);return p}
let maxA=1,maxI=1,maxM=1;for(const f of D.frames){maxA=Math.max(maxA,i16(f.a).length/3);maxI=Math.max(maxI,i16(f.i).length/3)}for(const k of D.keys)maxM=Math.max(maxM,i16(k.m).length/3);
const mass=cloud(maxM,false), ag=cloud(maxA,false), imp=cloud(maxI,true), pil=cloud(64,false);
const hidden=new Set();
const leg=document.getElementById('leg');M.species.forEach((s,i)=>{const e=document.createElement('span');const c=M.col[i];e.innerHTML=`<i style="background:rgb(${c.map(x=>x*255|0)})"></i>${s}`;e.onclick=()=>{hidden.has(i)?hidden.delete(i):hidden.add(i);e.style.opacity=hidden.has(i)?.35:1};leg.appendChild(e)});
let k=0,playing=true,follow=true,trailP=[],lastKey=-1;
function setKey(t){let j=0;for(let q=0;q<D.keys.length;q++)if(D.keys[q].t<=t)j=q;if(j===lastKey)return;lastKey=j;
 const m=i16(D.keys[j].m),ks=u8(D.keys[j].k),P=mass.geometry.attributes.position.array,C=mass.geometry.attributes.col.array,Z=mass.geometry.attributes.size.array;
 const n=ks.length;for(let q=0;q<Z.length;q++){if(q<n){P[q*3]=m[q*3]/4;P[q*3+1]=m[q*3+1]/4;P[q*3+2]=m[q*3+2]/4;C.set(KC[ks[q]]||KC[0],q*3);Z[q]=KS[ks[q]]||3}else Z[q]=0}
 for(const a of['position','col','size'])mass.geometry.attributes[a].needsUpdate=true}
function show(kk){const f=D.frames[kk];setKey(f.t);
 const a=i16(f.a),s=u8(f.s),P=ag.geometry.attributes.position.array,C=ag.geometry.attributes.col.array,Z=ag.geometry.attributes.size.array;const n=s.length;
 const SZ=[3,3,11,3,5,3,14,6];
 for(let q=0;q<Z.length;q++){if(q<n){const sp=s[q]&7,act=s[q]>>3;if(hidden.has(sp)){Z[q]=0;continue}P[q*3]=a[q*3]/4;P[q*3+1]=a[q*3+1]/4;P[q*3+2]=a[q*3+2]/4;C.set(act?M.act[sp]:M.col[sp],q*3);Z[q]=SZ[sp]*(act?1.5:1)}else Z[q]=0}
 for(const x of['position','col','size'])ag.geometry.attributes[x].needsUpdate=true;
 const ip=i16(f.i),ic=u8(f.ic),IP=imp.geometry.attributes.position.array,IC=imp.geometry.attributes.col.array,IZ=imp.geometry.attributes.size.array;const ni=ic.length/2;
 for(let q=0;q<IZ.length;q++){if(q<ni){const sp=ic[q*2];if(hidden.has(sp)){IZ[q]=0;continue}IP[q*3]=ip[q*3]/4;IP[q*3+1]=ip[q*3+1]/4;IP[q*3+2]=ip[q*3+2]/4;IC.set(M.col[sp].map(x=>x*.22),q*3);IZ[q]=14+12*Math.cbrt(ic[q*2+1])}else IZ[q]=0}
 for(const x of['position','col','size'])imp.geometry.attributes[x].needsUpdate=true;
 const PP=pil.geometry.attributes.position.array,PC=pil.geometry.attributes.col.array,PZ=pil.geometry.attributes.size.array;
 trailP=D.frames.slice(Math.max(0,kk-40),kk+1).map(x=>x.p[0]);
 for(let q=0;q<64;q++)PZ[q]=0;
 f.p.forEach((p,j)=>{PP.set(p,j*3);PC.set(j?[.6,.6,.6]:[1,1,1],j*3);PZ[j]=26});
 trailP.forEach((p,j)=>{const q=4+j;if(q<64){PP.set(p,q*3);PC.set([.8,.85,1],q*3);PZ[q]=6}});
 for(const x of['position','col','size'])pil.geometry.attributes[x].needsUpdate=true;
 if(follow){const p=f.p[0];const t=new THREE.Vector3(p[0],p[1],p[2]);const off=cam.position.clone().sub(ctl.target);if(off.length()>900)off.setLength(520);ctl.target.lerp(t,.25);cam.position.copy(ctl.target.clone().add(off))}
 document.getElementById('t').textContent='t '+f.t.toFixed(1)+' s';drawTL()}
// ---- timeline
const tlc=document.getElementById('tlc'),ctx=tlc.getContext('2d');const T=D.frames[D.frames.length-1].t||1;
const EMO={cute:'#7fe0ff',playful:'#9dff7a',eerie:'#b48cff',majestic:'#ffd27a',menacing:'#ff8a5c',terrifying:'#ff3b5c',neutral:'#3a405c'};
function drawTL(){const W=tlc.width=tlc.clientWidth*devicePixelRatio,H=tlc.height=tlc.clientHeight*devicePixelRatio;const s=devicePixelRatio;ctx.clearRect(0,0,W,H);
 const x0=90*s,x1=W-12*s,X=t=>x0+(x1-x0)*t/T;ctx.font=`${11*s}px system-ui`;ctx.fillStyle='#8a92b6';
 ctx.fillText('encounters',8*s,24*s);ctx.fillText('hits',8*s,52*s);ctx.fillText('emotion',8*s,82*s);ctx.fillText('threat',8*s,122*s);
 for(const e of M.encounters){const i=M.species.indexOf(e.species);const c=M.col[i].map(x=>x*255|0);ctx.strokeStyle=ctx.fillStyle=`rgb(${c})`;const x=X(e.t);ctx.lineWidth=2*s;
  ctx.beginPath();ctx.arc(x,22*s,5*s,0,7);e.active?ctx.fill():ctx.stroke()}
 for(const h of M.hits){const i=M.species.indexOf(h.sp);const c=(i>=0?M.col[i]:[1,1,1]).map(x=>x*255|0);ctx.fillStyle=`rgb(${c})`;ctx.fillRect(X(h.t)-1*s,44*s,2*s,14*s)}
 const ws=M.emotion,stp=8;for(let i=0;i<ws.length;i++){const w=ws[i];ctx.fillStyle=EMO[w.top]||'#555';const xa=X(w.t),xb=X(Math.min(T,w.t+2));ctx.fillRect(xa,70*s,Math.max(1,xb-xa),18*s)}
 ctx.strokeStyle='#ff5c7a';ctx.lineWidth=1.5*s;ctx.beginPath();ws.forEach((w,i)=>{const x=X(w.t+4),y=140*s-w.threat*36*s;i?ctx.lineTo(x,y):ctx.moveTo(x,y)});ctx.stroke();
 let lx=x0;for(const e in EMO){ctx.fillStyle=EMO[e];ctx.fillRect(lx,92*s,9*s,9*s);ctx.fillStyle='#8a92b6';ctx.fillText(e,lx+12*s,100*s);lx+=ctx.measureText(e).width+26*s}
 const cx=X(D.frames[k].t);ctx.strokeStyle='#fff';ctx.lineWidth=1*s;ctx.beginPath();ctx.moveTo(cx,6*s);ctx.lineTo(cx,H-4*s);ctx.stroke()}
tlc.onclick=e=>{const r=tlc.getBoundingClientRect();const t=(e.clientX-r.left-90)/(r.width-102)*T;let best=0;D.frames.forEach((f,i)=>{if(Math.abs(f.t-t)<Math.abs(D.frames[best].t-t))best=i});k=best};
const S=M.summary;document.getElementById('sum').innerHTML=`<b>A 5-minute flight through a living cell</b><br>seed ${M.seed} · explorer pilot (white)<br>
encounters <b>${S.encounters}</b> (${S.enc_per_min}/min) · species met <b>${S.variety}</b><br>quiet <b>${Math.round(S.quiet_frac*100)}%</b> of the flight · hits <b>${S.hits_per_min}</b>/min · steals ${S.steals_per_min}/min<br>
emotions felt <b>${S.emotion_distinct}</b> · threat range ${S.threat_range}<br><span>faint clouds = far populations held as cohorts; they become individuals near a pilot</span>`;
document.getElementById('play').onclick=()=>{playing=!playing;play.textContent=playing?'pause':'play'};
document.getElementById('follow').onclick=()=>{follow=!follow;document.getElementById('follow').textContent='follow pilot: '+(follow?'on':'off')};
addEventListener('keydown',e=>{if(e.code==='Space'){e.preventDefault();document.getElementById('play').click()}});
let acc=0,last=performance.now();
(function loop(now){requestAnimationFrame(loop);acc+=(now-last)/1000;last=now;if(playing&&acc>M.every/2){acc=0;k=(k+1)%D.frames.length}show(k);ctl.update();ren.render(scene,cam)})(performance.now());
</script>
"""


def build(seed=2, cfg=None, burn=600.0, seconds=300.0, out=None):
    d = record(seed=seed, cfg=cfg, burn=burn, seconds=seconds)
    out = out or os.path.join(HERE, "viewer.html")
    with open(out, "w") as fh:
        fh.write(TPL.replace("__DATA__", json.dumps(d, separators=(",", ":"))))
    return os.path.getsize(out), d["meta"]["summary"]


if __name__ == "__main__":
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 2
    print(build(seed=seed))
