"""Build a self-contained playback viewer from one or more Recorder JSONs (common/arena.py).

    python Tools/Ecology/common/viewer.py out.html run1.json [run2.json ...] --title "Pack hunters"

One HTML file, three.js + OrbitControls from jsDelivr (the artifact CSP allows it), data inlined. Mass prisms
draw as dim points tinted by element, species agents as bright points sized by body radius, pilots as white
diamonds with a short trail. A dropdown switches runs; space pauses; the slider scrubs.
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
<div id="ui"><select id="run"></select><button id="play">pause</button><input id="scrub" type="range" min="0" value="0" style="flex:1;min-width:120px"><span id="t"></span></div>
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
function ptsMat(){return new THREE.ShaderMaterial({transparent:true,depthWrite:false,blending:THREE.AdditiveBlending,
  vertexShader:`attribute float size;attribute vec3 col;varying vec3 vC;void main(){vC=col;vec4 mv=modelViewMatrix*vec4(position,1.);gl_PointSize=clamp(size*900./-mv.z,1.5,64.);gl_Position=projectionMatrix*mv;}`,
  fragmentShader:`varying vec3 vC;void main(){vec2 d=gl_PointCoord-.5;float r=dot(d,d);if(r>.25)discard;gl_FragColor=vec4(vC*(1.2-2.*r),1.);}`})}
function cloud(n){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(n*3),3));
  g.setAttribute('col',new THREE.BufferAttribute(new Float32Array(n*3),3));g.setAttribute('size',new THREE.BufferAttribute(new Float32Array(n),1));
  const p=new THREE.Points(g,ptsMat());p.frustumCulled=false;scene.add(p);return p}
let mass=null, agents=null, pil=null, run=null, k=0, playing=true, massBits=null;
function load(i){
  run=RUNS[i]; k=0; for(const o of [mass,agents,pil]) if(o) scene.remove(o);
  const m=run.mass0; const n=m.pos.length; mass=cloud(n);
  const P=mass.geometry.attributes.position.array, C=mass.geometry.attributes.col.array, S=mass.geometry.attributes.size.array;
  for(let j=0;j<n;j++){P.set(m.pos[j],j*3);const c=EL[m.elem[j]%4];C.set([c[0]*.25,c[1]*.25,c[2]*.25],j*3);S[j]=4}
  mass.geometry.attributes.position.needsUpdate=true; let maxA=1;
  for(const f of run.frames) {let a=0; for(const s in f.species) a+=f.species[s].pos.length; maxA=Math.max(maxA,a)}
  agents=cloud(maxA); pil=cloud(16); scrub.max=run.frames.length-1;
  document.getElementById('info').textContent=(run.meta.note||'')+'  ·  drag to orbit, scroll to zoom, space to pause';
}
function hex2bits(h){const b=new Uint8Array(h.length/2);for(let i=0;i<b.length;i++)b[i]=parseInt(h.substr(i*2,2),16);return b}
function show(f){
  const bits=hex2bits(f.alive), S=mass.geometry.attributes.size.array;
  for(let j=0;j<S.length;j++) S[j]=(bits[j>>3]>>(7-(j&7)))&1?4:0; mass.geometry.attributes.size.needsUpdate=true;
  const P=agents.geometry.attributes.position.array, C=agents.geometry.attributes.col.array, Z=agents.geometry.attributes.size.array; let q=0;
  for(const s in f.species){const b=f.species[s];for(let j=0;j<b.pos.length;j++){P.set(b.pos[j],q*3);C.set(b.col[j],q*3);Z[q]=b.size[j];q++}}
  for(let j=q;j<Z.length;j++)Z[j]=0;
  for(const a of ['position','col','size']) agents.geometry.attributes[a].needsUpdate=true;
  const PP=pil.geometry.attributes.position.array, PC=pil.geometry.attributes.col.array, PZ=pil.geometry.attributes.size.array;
  for(let j=0;j<16;j++){if(j<f.pilots.length){PP.set(f.pilots[j],j*3);PC.set([1,1,1],j*3);PZ[j]=22}else PZ[j]=0}
  for(const a of ['position','col','size']) pil.geometry.attributes[a].needsUpdate=true;
  document.getElementById('t').textContent='t '+f.t.toFixed(1)+' s';
}
const sel=document.getElementById('run'), scrub=document.getElementById('scrub');
RUNS.forEach((r,i)=>{const o=document.createElement('option');o.value=i;o.textContent=r.meta.label||('run '+i);sel.appendChild(o)});
sel.onchange=()=>load(+sel.value); scrub.oninput=()=>{k=+scrub.value};
document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'pause':'play'};
addEventListener('keydown',e=>{if(e.code==='Space'){e.preventDefault();document.getElementById('play').click()}});
load(0); let acc=0, last=performance.now();
(function loop(now){requestAnimationFrame(loop);acc+=(now-last)/1000;last=now;
  if(playing&&acc>1/20){acc=0;k=(k+1)%run.frames.length;scrub.value=k}
  show(run.frames[k]);ctl.update();ren.render(scene,cam)})(performance.now());
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
