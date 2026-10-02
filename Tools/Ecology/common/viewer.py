"""Build a self-contained playback viewer from one or more Recorder JSONs (common/arena.py).

    python Tools/Ecology/common/viewer.py out.html run1.json [run2.json ...] --title "Pack hunters"

One HTML file, three.js + OrbitControls from jsDelivr (the artifact CSP allows it), data inlined. Mass prisms
draw as dim points tinted by element (or bright in their DOMAIN colour once owned, orange when dangerous, and
they MOVE when a species moves them - Recorder per-frame deltas), species agents as bright points sized by body radius, pilots as white
diamonds with a short trail. A dropdown switches runs; space pauses; the slider scrubs. Playback runs on the recording's own
clock (each frame's `t`) at a chosen speed, interpolating positions between recorded frames, so a sparse
recording plays smoothly instead of jumping once per sample. A run whose meta carries `focus: [x, y, z, distance]` opens
framed on that point.
"""
from __future__ import annotations

import argparse
import json
import os

TPL = r"""<meta charset="utf-8"><title>__TITLE__</title>
<style>
:root{--bg:#0b0d14;--fg:#d8dcf0;--mute:#7c84a8;--acc:#7fd1ff}
@media (prefers-color-scheme: light){:root:not([data-theme="dark"]){--bg:#0b0d14;--fg:#d8dcf0;--mute:#7c84a8;--acc:#7fd1ff}}
body{background:var(--bg);color:var(--fg);font:13px/1.4 system-ui,sans-serif;height:100%;overflow:hidden}
html{height:100%}
#ui{position:fixed;left:12px;top:12px;right:12px;display:flex;gap:10px;flex-wrap:wrap;align-items:center;z-index:2}
#ui select,#ui input,#ui button{background:#151a28;color:var(--fg);border:1px solid #2a3150;border-radius:6px;padding:4px 8px}
#info{position:fixed;left:12px;bottom:12px;right:12px;color:var(--mute);max-width:70ch;z-index:2}
#t{min-width:14ch;font-variant-numeric:tabular-nums}
canvas{display:block}
</style>
<div id="ui"><select id="run"></select><button id="play">pause</button><select id="speed" title="playback speed"><option value="0.25">0.25x</option><option value="0.5">0.5x</option><option value="1" selected>1x</option><option value="2">2x</option><option value="4">4x</option></select><input id="scrub" type="range" min="0" value="0" style="flex:1;min-width:120px"><span id="t"></span></div>
<div id="info"></div>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const RUNS = __DATA__;
const EL = [[0.55,0.85,1.0],[1.0,0.62,0.25],[0.75,0.5,1.0],[0.5,1.0,0.6]];
const scene = new THREE.Scene(), cam = new THREE.PerspectiveCamera(55, innerWidth/innerHeight, 1, 20000);
const ren = new THREE.WebGLRenderer({antialias:true}); ren.setPixelRatio(Math.min(2,devicePixelRatio));
ren.setSize(innerWidth, innerHeight); document.body.appendChild(ren.domElement);
cam.position.set(0, 900, 2400); const ctl = new THREE.OrbitControls(cam, ren.domElement);
addEventListener('resize', ()=>{cam.aspect=innerWidth/innerHeight;cam.updateProjectionMatrix();ren.setSize(innerWidth,innerHeight)});
const shell = new THREE.Mesh(new THREE.SphereGeometry(1200,48,24), new THREE.MeshBasicMaterial({color:0x2a3a6a,wireframe:true,transparent:true,opacity:0.08}));
scene.add(shell);
function ptsMat(solid){return new THREE.ShaderMaterial(Object.assign(solid?{transparent:false,depthWrite:true,depthTest:true}:{transparent:true,depthWrite:false,blending:THREE.AdditiveBlending},{
  vertexShader:`attribute float size;attribute vec3 col;varying vec3 vC;void main(){vC=col;vec4 mv=modelViewMatrix*vec4(position,1.);gl_PointSize=clamp(size*900./-mv.z,1.5,64.);gl_Position=projectionMatrix*mv;}`,
  fragmentShader:`varying vec3 vC;void main(){vec2 d=gl_PointCoord-.5;float r=dot(d,d);if(r>.25)discard;gl_FragColor=vec4(vC*(1.2-2.*r),1.);}`}))}
function cloud(n,solid){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(n*3),3));
  g.setAttribute('col',new THREE.BufferAttribute(new Float32Array(n*3),3));g.setAttribute('size',new THREE.BufferAttribute(new Float32Array(n),1));
  const p=new THREE.Points(g,ptsMat(solid));p.frustumCulled=false;scene.add(p);return p}
let mass=null, agents=null, pil=null, run=null, k=0, playing=true, tp=0, aliveK=-1;
// pilotNow: the INTERPOLATED position of pilot 0 this frame (a follow camera reads it, never a raw sample)
let pilotNow=null;
function load(i){
  run=RUNS[i]; k=0; tp=run.frames[0].t; aliveK=-1; pilotNow=null; for(const o of [mass,agents,pil]) if(o) scene.remove(o);
  const m=run.mass0; const n=m.pos.length; mass=cloud(n);
  const P=mass.geometry.attributes.position.array, C=mass.geometry.attributes.col.array, S=mass.geometry.attributes.size.array;
  for(let j=0;j<n;j++){P.set(m.pos[j],j*3);const c=EL[m.elem[j]%4];C.set([c[0]*.25,c[1]*.25,c[2]*.25],j*3);S[j]=4}
  mass.geometry.attributes.position.needsUpdate=true; let maxA=1;
  for(const f of run.frames) {let a=0; for(const s in f.species) a+=f.species[s].pos.length; maxA=Math.max(maxA,a)}
  agents=cloud(maxA); pil=cloud(16); scrub.max=run.frames.length-1;
  document.getElementById('info').textContent=(run.meta.note||'')+'  ·  drag to orbit, scroll to zoom, space to pause';
}
function hex2bits(h){const b=new Uint8Array(h.length/2);for(let i=0;i<b.length;i++)b[i]=parseInt(h.substr(i*2,2),16);return b}
function lerp3(out,o,a,b,u){out[o]=a[0]+(b[0]-a[0])*u;out[o+1]=a[1]+(b[1]-a[1])*u;out[o+2]=a[2]+(b[2]-a[2])*u}
// Draw the state BETWEEN two recorded frames. Positions are interpolated wherever an index means the same
// agent in both frames (same species, same count); anything else (a birth, a death) shows frame A as-is.
function show(fa,fb,u){
  if(aliveK!==k){const bits=hex2bits(fa.alive), S=mass.geometry.attributes.size.array;
    for(let j=0;j<S.length;j++) S[j]=(bits[j>>3]>>(7-(j&7)))&1?4:0; mass.geometry.attributes.size.needsUpdate=true; aliveK=k}
  const P=agents.geometry.attributes.position.array, C=agents.geometry.attributes.col.array, Z=agents.geometry.attributes.size.array; let q=0;
  for(const s in fa.species){const a=fa.species[s], b=fb.species[s], same=b&&b.pos.length===a.pos.length;
    for(let j=0;j<a.pos.length;j++){if(same) lerp3(P,q*3,a.pos[j],b.pos[j],u); else P.set(a.pos[j],q*3);
      C.set(a.col[j],q*3);Z[q]=same?a.size[j]+(b.size[j]-a.size[j])*u:a.size[j];q++}}
  for(let j=q;j<Z.length;j++)Z[j]=0;
  for(const a of ['position','col','size']) agents.geometry.attributes[a].needsUpdate=true;
  const PP=pil.geometry.attributes.position.array, PC=pil.geometry.attributes.col.array, PZ=pil.geometry.attributes.size.array;
  const sameP=fb.pilots.length===fa.pilots.length;
  for(let j=0;j<16;j++){if(j<fa.pilots.length){if(sameP) lerp3(PP,j*3,fa.pilots[j],fb.pilots[j],u); else PP.set(fa.pilots[j],j*3);
    PC.set([1,1,1],j*3);PZ[j]=22}else PZ[j]=0}
  for(const a of ['position','col','size']) pil.geometry.attributes[a].needsUpdate=true;
  pilotNow=fa.pilots.length?[PP[0],PP[1],PP[2]]:null;
  document.getElementById('t').textContent='t '+(fa.t+(fb.t-fa.t)*u).toFixed(1)+' s';
}
const sel=document.getElementById('run'), scrub=document.getElementById('scrub'), spd=document.getElementById('speed');
RUNS.forEach((r,i)=>{const o=document.createElement('option');o.value=i;o.textContent=r.meta.label||('run '+i);sel.appendChild(o)});
sel.onchange=()=>load(+sel.value); scrub.oninput=()=>{k=+scrub.value;tp=run.frames[k].t};
document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'pause':'play'};
addEventListener('keydown',e=>{if(e.code==='Space'&&e.target.tagName!=='SELECT'){e.preventDefault();document.getElementById('play').click()}});
load(0); let last=performance.now();
// playback runs on the RECORDING's own clock (frame.t), at the chosen speed - never one frame per tick
(function loop(now){requestAnimationFrame(loop);const dt=Math.min(0.1,(now-last)/1000);last=now;
  const F=run.frames, n=F.length;
  if(playing&&n>1){tp+=dt*(+spd.value||1); if(tp>=F[n-1].t){tp=F[0].t;k=0}
    while(k<n-2&&F[k+1].t<=tp)k++; scrub.value=k}
  const fa=F[k], fb=F[Math.min(k+1,n-1)], span=fb.t-fa.t;
  show(fa,fb,span>0?Math.min(1,Math.max(0,(tp-fa.t)/span)):0);ctl.update();ren.render(scene,cam)})(performance.now());
</script>
"""


def build(out: str, paths: list[str], title: str):
    runs = [json.load(open(p)) for p in paths]
    with open(out, "w") as fh:
        fh.write(TPL.replace("__TITLE__", title).replace("__DATA__", json.dumps(runs, separators=(",", ":"))))
    return os.path.getsize(out)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("runs", nargs="+"); ap.add_argument("--title", default="Ecology run")
    a = ap.parse_args()
    print(build(a.out, a.runs, a.title), "bytes")
