"""A self-contained showcase page for the `field` swarm: one scripted life per body plan, played back
in the browser (canvas, no libraries). Each life: grow from the 16-tadpole seed, a vessel flies
through (the school parts and re-forms), a vessel strike removes a third of it (it heals), then a
predator eats the majority element and the swarm MORPHS into the new majority's plan.

    python Tools/NCA/field_showcase.py      # -> Tools/NCA/results/field/showcase.html
"""
import base64
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402
import field_probes as fp  # noqa: E402
import swarm_probe  # noqa: E402

EVERY = 3
NAMES = {"mass": "Whale (Mass)", "space": "Jellyfish (Space)", "charge": "Pufferfish (Charge)", "time": "Dragonfly (Time)"}


def life(kind, cfg, seed=3):
    T = sn.load_targets()
    model = fs.FieldSwarm(cfg)
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    frames, preds, events = [], [], []
    t = [0]

    def rec(pred=None):
        if t[0] % EVERY == 0:
            al = (sw.active[0] & sw.hatched[0]).numpy()
            x = sn.decode(sw, 0)
            hat = x["hatched"].numpy()
            p = sw.pos[0].numpy()[al]
            e = sw.elem[0].numpy()[al]; d = sw.dom[0].numpy()[al]
            tier = x["tier"].argmax(1).numpy()[hat]
            h = x["h"].numpy()[hat]; f = x["f"].numpy()[hat]
            st = sw.s[0].numpy()[al][:, fs.STARTLE]
            mol = sw.s[0].numpy()[al][:, fs.MOLT] > 0
            age = sw.s[0].numpy()[al][:, fs.BIRTH]
            q = np.concatenate([p * 10, f * 100, h[:, :1] * 100, e[:, None], d[:, None], tier[:, None], st[:, None] * 100,
                                mol[:, None] * 1, np.minimum(age, 30)[:, None]], 1)
            frames.append(np.round(q).astype("<i2"))
            preds.append(None if pred is None else [round(float(v), 2) for v in pred[0]] + [round(float(pred[1]), 2)])
        t[0] += 1

    def step(n, pred_fn=None):
        nonlocal sw
        for i in range(n):
            pr = pred_fn(i) if pred_fn else None
            model.predators = [] if pr is None else [pr]
            rec(pr)
            sw = model(sw, gen)
        model.predators = []

    events.append([0, "seed: 16 tadpoles"])
    step(220)
    # a vessel flies through the body
    al = (sw.active[0] & sw.hatched[0]).numpy(); p = sw.pos[0].numpy()[al]
    c = p.mean(0); rms = float(np.sqrt(((p - c) ** 2).sum(-1).mean())); rad = 0.6 * rms
    d = np.array([0.8, 0.25, 0.55]); d /= np.linalg.norm(d)
    start = c - d * (2.5 * rms + rad); span = int(2 * (2.5 * rms + rad) / 3.0)
    events.append([t[0], "a vessel flies through"])
    step(span, lambda i: (start + d * 3.0 * i, rad, d * 3.0))
    step(80)
    events.append([t[0], "vessel strike: a third of the body is destroyed"])
    swarm_probe.strike(sw, gen=gen)
    step(140)
    # a ship loiters beside it (the Time tadpoles mob it), then leaves
    al = (sw.active[0] & sw.hatched[0]).numpy(); p = sw.pos[0].numpy()[al]
    c = p.mean(0); rms = float(np.sqrt(((p - c) ** 2).sum(-1).mean()))
    ship = c + np.array([1.3 * rms, 0.2 * rms, 0.0])
    events.append([t[0], "a ship loiters beside it: Time tadpoles mob it"])
    step(80, lambda i: (ship + np.array([0.0, 0.0, 3.0 * np.sin(i / 12)]), 0.3 * rms, np.array([0.0, 0.0, 0.3])))
    step(60)
    to = sn.SWITCH_TO[kind]
    n0 = int((sw.active[0] & sw.hatched[0]).sum())
    if kind == "time":
        fp.strict_cull(sw, 0, to, gen)
        label = "predators eat the Time and Space tadpoles: Mass is the majority"
    else:
        sn.lose_majority(sw, 0, gen, to=to)
        label = f"predators eat {sn.ELEMENTS[sn.MAJOR[kind]]}: {sn.ELEMENTS[to]} is the majority"
    events.append([t[0], f"{label} ({n0 - int((sw.active[0] & sw.hatched[0]).sum())} eaten)"])
    step(300)
    for (st, a, b) in model.mem[0]["switches"]:
        events.append([st, f"commits: {NAMES[a].split()[0]} -> {NAMES[b].split()[0]} (morph)"])
    events.sort(key=lambda e: e[0])
    nmax = max(len(f) for f in frames)
    arr = np.zeros((len(frames), nmax, frames[0].shape[1]), "<i2")
    ns = []
    for i, f in enumerate(frames):
        arr[i, :len(f)] = f; ns.append(len(f))
    return dict(name=NAMES[kind], every=EVERY, shape=list(arr.shape), b64=base64.b64encode(arr.tobytes()).decode(),
                n=ns, pred=preds, events=events)


PAGE = r"""<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Field Swarm Showcase</title>
<style>
:root{--bg:#0b1020;--fg:#dfe6f5;--mut:#8a96b3;--acc:#7fd1c1;--panel:#121a30}
@media (prefers-color-scheme: light){:root:not([data-theme="dark"]){--bg:#0b1020;--fg:#dfe6f5;--mut:#8a96b3;--panel:#121a30}}
body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.45 system-ui,sans-serif}
main{max-width:980px;margin:0 auto;padding:16px}
h1{font-size:20px;margin:4px 0}.cap{color:var(--mut);margin:4px 0 12px}
.row{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin:8px 0}
button{background:var(--panel);color:var(--fg);border:1px solid #2a3658;border-radius:6px;padding:6px 10px;cursor:pointer}
button[aria-pressed=true]{border-color:var(--acc);color:var(--acc)}
canvas{width:100%;max-width:940px;aspect-ratio:1.6;background:#060914;border-radius:10px;display:block;touch-action:none}
input[type=range]{flex:1;min-width:160px}
#ev{color:var(--acc);min-height:1.4em}.leg span{display:inline-block;margin-right:12px}
.dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:4px;vertical-align:middle}
</style></head><body><main>
<h1>Field swarm: designed fields + boids</h1>
<p class="cap">No learned rule. Each tadpole is a boid steering to a slot in its majority element's body plan; the plan is chosen by element ratios. Drag to orbit.</p>
<div class="row" id="kinds"></div>
<canvas id="cv" width="1400" height="875" aria-label="swarm playback"></canvas>
<div class="row"><button id="play" aria-pressed="true">Pause</button><input type="range" id="sl" min="0" value="0"><span id="tt"></span></div>
<div id="ev"></div>
<div class="row leg"><span><i class="dot" style="background:#e8a93a"></i>Charge</span><span><i class="dot" style="background:#8e6bd8"></i>Mass</span><span><i class="dot" style="background:#3a7bdc"></i>Space</span><span><i class="dot" style="background:#2fb39a"></i>Time</span><span>ring = domain (jade / ruby / gold)</span><span>red = danger plates</span><span>white flash = startled / molting</span></div>
</main><script>
const DATA=/*DATA*/;
const EL=['#e8a93a','#8e6bd8','#3a7bdc','#2fb39a'], DOM=['#1fbfa6','#d04a6a','#d8b040'];
const cv=document.getElementById('cv'),g=cv.getContext('2d');let kind=Object.keys(DATA)[0],fi=0,playing=true,yaw=0.6,pitch=0.35,drag=null,cache={};
function dec(k){if(cache[k])return cache[k];const d=DATA[k],b=atob(d.b64),u=new Uint8Array(b.length);for(let i=0;i<b.length;i++)u[i]=b.charCodeAt(i);return cache[k]=new Int16Array(u.buffer);}
const kd=document.getElementById('kinds');for(const k of Object.keys(DATA)){const bt=document.createElement('button');bt.textContent=DATA[k].name;bt.onclick=()=>{kind=k;fi=0;sync();};bt.dataset.k=k;kd.appendChild(bt);}
const sl=document.getElementById('sl');function sync(){for(const b of kd.children)b.setAttribute('aria-pressed',b.dataset.k===kind);sl.max=DATA[kind].shape[0]-1;}
sync();sl.oninput=()=>{fi=+sl.value;};document.getElementById('play').onclick=e=>{playing=!playing;e.target.textContent=playing?'Pause':'Play';e.target.setAttribute('aria-pressed',playing);};
cv.onpointerdown=e=>{drag=[e.clientX,e.clientY];cv.setPointerCapture(e.pointerId)};cv.onpointerup=()=>drag=null;
cv.onpointermove=e=>{if(!drag)return;yaw+=(e.clientX-drag[0])*0.008;pitch=Math.max(-1.4,Math.min(1.4,pitch+(e.clientY-drag[1])*0.008));drag=[e.clientX,e.clientY]};
let ctr=[0,0,0];
function draw(){if(fi>=DATA[kind].shape[0])fi=0;const d=DATA[kind],A=dec(kind),[F,N,C]=d.shape;const n=d.n[fi];const o=fi*N*C;
 let cx=0,cy=0,cz=0;for(let i=0;i<n;i++){cx+=A[o+i*C];cy+=A[o+i*C+1];cz+=A[o+i*C+2];}
 if(n){cx/=n*10;cy/=n*10;cz/=n*10;}ctr=ctr.map((v,i)=>v+0.15*([cx,cy,cz][i]-v));
 const W=cv.width,H=cv.height;g.fillStyle='#060914';g.fillRect(0,0,W,H);
 const cyw=Math.cos(yaw),syw=Math.sin(yaw),cp=Math.cos(pitch),spp=Math.sin(pitch);
 const proj=(x,y,z)=>{x-=ctr[0];y-=ctr[1];z-=ctr[2];const X=cyw*x+syw*z,Z=-syw*x+cyw*z;const Y=cp*y-spp*Z,Z2=spp*y+cp*Z;const s=1500/(85+Z2);return [W/2+X*s,H/2-Y*s,s,Z2];};
 const items=[];for(let i=0;i<n;i++){const b=o+i*C;const x=A[b]/10,y=A[b+1]/10,z=A[b+2]/10;const p=proj(x,y,z);const q=proj(x-A[b+3]/100*2.2,y-A[b+4]/100*2.2,z-A[b+5]/100*2.2);items.push([p[3],p,q,A[b+6]/100,A[b+7],A[b+8],A[b+9],A[b+10]/100,A[b+11],A[b+12]]);}
 items.sort((a,b)=>b[0]-a[0]);
 for(const [zz,p,q,h,e,dm,tier,st,mol,age] of items){const grow=Math.min(1,age/12);const r=Math.max(1.2,0.55*p[2]*(0.6+0.25*h))*grow;
  g.globalAlpha=0.55*grow;g.strokeStyle=EL[e];g.lineWidth=Math.max(1,r*0.35);g.beginPath();g.moveTo(p[0],p[1]);g.lineTo(q[0],q[1]);g.stroke();
  g.globalAlpha=grow;g.fillStyle=tier==2?'#ff4d3d':(tier==1?'#bfe8ff':EL[e]);g.beginPath();g.arc(p[0],p[1],r,0,7);g.fill();
  g.strokeStyle=DOM[dm];g.lineWidth=Math.max(1,r*0.3);g.beginPath();g.arc(p[0],p[1],r*1.25,0,7);g.stroke();
  if(st>0.15||mol){g.globalAlpha=Math.min(1,st+(mol?0.6:0))*0.8;g.fillStyle='#fff';g.beginPath();g.arc(p[0],p[1],r*0.55,0,7);g.fill();}}
 g.globalAlpha=1;const pr=d.pred[fi];if(pr){const p=proj(pr[0],pr[1],pr[2]);g.strokeStyle='#ffffffaa';g.lineWidth=2;g.beginPath();g.arc(p[0],p[1],pr[3]*p[2]*0.9,0,7);g.stroke();g.fillStyle='#ffffff';g.beginPath();g.arc(p[0],p[1],5,0,7);g.fill();}
 const step=fi*d.every;let ev='';for(const [s,t] of d.events)if(s<=step)ev=t;document.getElementById('ev').textContent=ev;
 document.getElementById('tt').textContent=`step ${step}  tadpoles ${n}`;sl.value=fi;}
let last=0;function loop(ts){if(playing&&ts-last>45){fi=(fi+1)%DATA[kind].shape[0];last=ts;}if(!drag&&playing)yaw+=0.002;draw();requestAnimationFrame(loop);}requestAnimationFrame(loop);
</script></body></html>"""


def main():
    torch.set_num_threads(4)
    cfg = fs.FieldCfg(wander=0.25)
    data = {k: life(k, cfg) for k in sn.KINDS}
    out = os.path.join(HERE, "results", "field", "showcase.html")
    open(out, "w").write(PAGE.replace("/*DATA*/", json.dumps(data)))
    print(out, os.path.getsize(out) // 1024, "KB")


if __name__ == "__main__":
    main()
