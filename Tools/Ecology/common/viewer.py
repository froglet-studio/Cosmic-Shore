"""Build a self-contained 3D playback viewer from one or more Recorder JSONs (common/arena.py).

    python Tools/Ecology/common/viewer.py out.html run1.json [run2.json ...] --title "Pack hunters"
    python Tools/Ecology/common/viewer.py --retemplate built.html     # re-render an old viewer's data with this template

One HTML file, three.js r128 from jsDelivr (the artifact CSP allows it), data inlined.

What the viewer is for: judging how a species FEELS to a pilot, so it is drawn as a scene, not a plot.
  depth        lit instanced solids (prisms are boxes, creatures are bodies that face their velocity), distance fog,
               a parallax starfield, a faint equator disc with drop lines from the pilot and nearby creatures (a
               radar-scope depth cue), a fresnel membrane with latitude/longitude rings
  orientation  the pilot is a shaped vessel that faces its velocity and banks into turns, with a fading ribbon of
               where it has just been; a HUD reads speed, heading, pitch and height above the equator; a minimap
               shows the cell from above
  control      camera modes: PILOT VIEW (over the pilot's shoulder at the creatures it faces, default), CHASE
               (behind the followed body), ORBIT (free), FLY (WASD + drag to look,
               R/F up/down, Shift fast). Click any creature to follow it; Esc returns to the pilot.
  playback     runs on the recording's own clock (each frame's `t`) at a chosen speed, interpolating positions
               between recorded frames, so a sparse recording plays smoothly instead of jumping once per sample.
Data contract (unchanged): frames carry t, pilots, alive (hex bits), optional md (mass deltas: index, x, y, z,
style), species {name: {pos, col, size}}; meta may carry label, note, focus [x, y, z, distance], hud (per frame).
Mass style: 0-3 element (neutral, dim), 4-6 domain jade/ruby/gold, 7 danger, 8 shielded.
"""
from __future__ import annotations

import argparse
import json
import os
import re

TPL = r"""<meta charset="utf-8"><title>__TITLE__</title>
<style>
:root{--bg:#05070d;--fg:#d8dcf0;--mute:#8a92b6;--acc:#7fd1ff;--panel:#0f1422e6;--line:#26304c;color-scheme:dark}
body{background:var(--bg);color:var(--fg);font:13px/1.4 system-ui,sans-serif;height:100%;overflow:hidden;margin:0}
html{height:100%}
#ui{position:fixed;left:12px;top:12px;right:12px;display:flex;gap:8px;flex-wrap:wrap;align-items:center;z-index:4}
#ui select,#ui input,#ui button{background:#151a28;color:var(--fg);border:1px solid #2a3150;border-radius:6px;padding:4px 8px;font:inherit}
#info{position:fixed;left:12px;bottom:12px;right:190px;color:var(--mute);max-width:70ch;z-index:3}
#t{min-width:14ch;font-variant-numeric:tabular-nums}
#hudbox{position:fixed;left:12px;top:58px;z-index:3;background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:6px 8px;
  font:12px/1.35 ui-monospace,SFMono-Regular,Menlo,monospace;font-variant-numeric:tabular-nums;color:var(--fg);pointer-events:none}
#hudbox b{color:var(--acc);font-weight:600}
#att{display:block;margin-top:4px}
#mini{position:fixed;right:12px;bottom:12px;z-index:3;border-radius:50%;background:#0a0f1ccc;border:1px solid var(--line)}
#help{color:var(--mute)}
canvas{display:block}
@media (max-width:560px){#mini{width:110px;height:110px}#info{right:130px;max-height:4.2em;overflow:hidden}#help{display:none}#hudbox{font-size:11px}#att{display:none}}
</style>
<div id="ui"><select id="run"></select><button id="play">pause</button><select id="speed" title="playback speed"><option value="0.25">0.25x</option><option value="0.5">0.5x</option><option value="1" selected>1x</option><option value="2">2x</option><option value="4">4x</option></select><select id="cammode" title="camera (1-4)"><option value="watch">pilot view</option><option value="chase">chase cam</option><option value="orbit">orbit cam</option><option value="fly">fly cam</option></select><input id="scrub" type="range" min="0" value="0" style="flex:1;min-width:120px"><span id="t"></span></div>
<div id="hudbox"><div id="hudtxt"></div><canvas id="att" width="120" height="60"></canvas></div>
<canvas id="mini" width="160" height="160"></canvas>
<div id="info"></div>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const RUNS = __DATA__;
const R_CELL=1200, BG=0x05070d;
const scene=new THREE.Scene(); scene.background=new THREE.Color(BG); scene.fog=new THREE.FogExp2(BG,0.00042);
const cam=new THREE.PerspectiveCamera(60,innerWidth/innerHeight,6,30000);
const ren=new THREE.WebGLRenderer({antialias:true}); ren.setPixelRatio(Math.min(2,devicePixelRatio)); ren.setSize(innerWidth,innerHeight);
document.body.appendChild(ren.domElement);
const ctl=new THREE.OrbitControls(cam,ren.domElement); ctl.enableDamping=true; ctl.dampingFactor=0.08;
addEventListener('resize',()=>{cam.aspect=innerWidth/innerHeight;cam.updateProjectionMatrix();ren.setSize(innerWidth,innerHeight)});

// ---- lights + world references ----------------------------------------------------------------------------
scene.add(new THREE.HemisphereLight(0x9fc4ff,0x2a1a10,0.75));
const key=new THREE.DirectionalLight(0xfff1dc,1.05); key.position.set(0.5,1,0.35); scene.add(key);
const rim=new THREE.DirectionalLight(0x6fa8ff,0.45); rim.position.set(-0.6,-0.3,-0.8); scene.add(rim);
(function stars(){const n=2600,g=new THREE.BufferGeometry(),p=new Float32Array(n*3),c=new Float32Array(n*3);
  for(let i=0;i<n;i++){let x=Math.random()*2-1,y=Math.random()*2-1,z=Math.random()*2-1,l=Math.hypot(x,y,z)||1,r=9000+Math.random()*6000;
    p.set([x/l*r,y/l*r,z/l*r],i*3);const b=0.4+Math.random()*0.6;c.set([b*0.85,b*0.9,b],i*3)}
  g.setAttribute('position',new THREE.BufferAttribute(p,3));g.setAttribute('color',new THREE.BufferAttribute(c,3));
  const m=new THREE.PointsMaterial({size:1.6,sizeAttenuation:false,vertexColors:true,fog:false});scene.add(new THREE.Points(g,m))})();
const membrane=new THREE.Mesh(new THREE.SphereGeometry(R_CELL,64,32),new THREE.ShaderMaterial({transparent:true,depthWrite:false,side:THREE.DoubleSide,
  uniforms:{c:{value:new THREE.Color(0x3d6bd6)}},
  vertexShader:`varying vec3 vN;varying vec3 vV;void main(){vec4 mv=modelViewMatrix*vec4(position,1.);vN=normalize(normalMatrix*normal);vV=normalize(-mv.xyz);gl_Position=projectionMatrix*mv;}`,
  fragmentShader:`uniform vec3 c;varying vec3 vN;varying vec3 vV;void main(){float f=pow(1.-abs(dot(vN,vV)),3.);gl_FragColor=vec4(c,0.02+0.32*f);}`}));
scene.add(membrane);
const grat=new THREE.Group();
for(let i=1;i<6;i++){const lat=-Math.PI/2+i*Math.PI/6,r=R_CELL*Math.cos(lat),y=R_CELL*Math.sin(lat),pts=[];
  for(let a=0;a<=96;a++)pts.push(new THREE.Vector3(r*Math.cos(a/96*Math.PI*2),y,r*Math.sin(a/96*Math.PI*2)));
  grat.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),new THREE.LineBasicMaterial({color:0x34508f,transparent:true,opacity:i===3?0.35:0.14})))}
for(let j=0;j<12;j++){const pts=[],ph=j/12*Math.PI*2;
  for(let a=0;a<=64;a++){const t=-Math.PI/2+a/64*Math.PI;pts.push(new THREE.Vector3(R_CELL*Math.cos(t)*Math.cos(ph),R_CELL*Math.sin(t),R_CELL*Math.cos(t)*Math.sin(ph)))}
  grat.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),new THREE.LineBasicMaterial({color:0x34508f,transparent:true,opacity:0.1})))}
scene.add(grat);
// equator disc: concentric rings + spokes, the floor the drop lines land on
const disc=new THREE.Group();
for(let r=200;r<=R_CELL;r+=200){const pts=[];for(let a=0;a<=128;a++)pts.push(new THREE.Vector3(r*Math.cos(a/128*Math.PI*2),0,r*Math.sin(a/128*Math.PI*2)));
  disc.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),new THREE.LineBasicMaterial({color:0x2c5a7a,transparent:true,opacity:0.22})))}
for(let j=0;j<16;j++){const a=j/16*Math.PI*2;disc.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0,0,0),new THREE.Vector3(R_CELL*Math.cos(a),0,R_CELL*Math.sin(a))]),new THREE.LineBasicMaterial({color:0x2c5a7a,transparent:true,opacity:0.12})))}
scene.add(disc);
const DROPMAX=48; const drops=new THREE.LineSegments(new THREE.BufferGeometry(),new THREE.LineBasicMaterial({vertexColors:true,transparent:true,opacity:0.4}));
drops.geometry.setAttribute('position',new THREE.BufferAttribute(new Float32Array(DROPMAX*6),3));drops.geometry.setAttribute('color',new THREE.BufferAttribute(new Float32Array(DROPMAX*6),3));
drops.frustumCulled=false;scene.add(drops);

const GLOW=(function(){const c=document.createElement('canvas');c.width=c.height=64;const g=c.getContext('2d'),r=g.createRadialGradient(32,32,0,32,32,32);
  r.addColorStop(0,'rgba(255,255,255,1)');r.addColorStop(0.25,'rgba(255,255,255,.55)');r.addColorStop(1,'rgba(255,255,255,0)');g.fillStyle=r;g.fillRect(0,0,64,64);
  return new THREE.CanvasTexture(c)})();
// ---- meshes ---------------------------------------------------------------------------------------------
// style: 0-3 element (neutral, dim), 4-6 domain jade/ruby/gold, 7 danger, 8 shielded
const ST=[[.30,.42,.50],[.50,.34,.16],[.40,.28,.52],[.26,.50,.32],[.1,.85,.78],[1,.3,.4],[1,.82,.3],[1,.45,.12],[.62,.74,1]];
const STS=[1,1,1,1,1.25,1.25,1.25,1.4,1.25];
const prismGeo=new THREE.BoxGeometry(3.2,3.2,8);
const bodyGeo=(function(){const g=new THREE.ConeGeometry(0.55,2.2,7);g.rotateX(Math.PI/2);return g})();   // nose along +Z
const lam=()=>new THREE.MeshLambertMaterial({vertexColors:false});
let mass=null, agents=null, run=null, k=0, playing=true, tp=0, cur=-1, aliveK=-1, nMass=0, maxA=1;
let massSt=null, massPos=null, massRot=null, massAlive=null;
let aPos=null, aDir=null, aCol=null, aSize=null, aSpec=null, aCount=0;
const tmpM=new THREE.Matrix4(), tmpQ=new THREE.Quaternion(), tmpS=new THREE.Vector3(), tmpP=new THREE.Vector3(), Zp=new THREE.Vector3(0,0,1), tmpV=new THREE.Vector3(), tmpC=new THREE.Color();
function hashRot(j,q){const s=Math.sin(j*12.9898)*43758.5453;const u=s-Math.floor(s),v=(s*7.13)%1,w=(s*3.71)%1;
  q.setFromEuler(new THREE.Euler(u*6.28,Math.abs(v)*6.28,Math.abs(w)*6.28));return q}
// vessel: a dart with swept wings, a fin and a glowing engine; faces +Z
function makeVessel(){const g=new THREE.Group();
  const hull=new THREE.Mesh(new THREE.ConeGeometry(2.2,13,4),new THREE.MeshLambertMaterial({color:0xe8eefc}));hull.rotation.x=Math.PI/2;hull.rotation.y=Math.PI/4;g.add(hull);
  const wing=new THREE.Mesh(new THREE.BoxGeometry(14,0.5,4.5),new THREE.MeshLambertMaterial({color:0xb9c7e8}));wing.position.set(0,0,-2.5);g.add(wing);
  const fin=new THREE.Mesh(new THREE.BoxGeometry(0.5,4,3.5),new THREE.MeshLambertMaterial({color:0xb9c7e8}));fin.position.set(0,2,-4);g.add(fin);
  const eng=new THREE.Mesh(new THREE.SphereGeometry(1.4,10,8),new THREE.MeshBasicMaterial({color:0x7fd1ff}));eng.position.set(0,0,-6.6);g.add(eng);
  const halo=new THREE.Sprite(new THREE.SpriteMaterial({map:GLOW,color:0x7fd1ff,transparent:true,opacity:0.55,depthWrite:false,blending:THREE.AdditiveBlending}));halo.scale.set(26,26,1);g.add(halo);
  g.scale.setScalar(1.6);scene.add(g);return g}
const TRAILN=140; function makeTrail(){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(TRAILN*3),3));
  g.setAttribute('color',new THREE.BufferAttribute(new Float32Array(TRAILN*3),3));const l=new THREE.Line(g,new THREE.LineBasicMaterial({vertexColors:true,transparent:true,opacity:0.9}));l.frustumCulled=false;scene.add(l);return l}
let vessels=[], trails=[], trailBuf=[], vState=[];
const glowMat=new THREE.ShaderMaterial({transparent:true,depthWrite:false,blending:THREE.AdditiveBlending,uniforms:{map:{value:GLOW},h:{value:innerHeight}},
  vertexShader:`attribute float size;attribute vec3 col;varying vec3 vC;uniform float h;void main(){vC=col;vec4 mv=modelViewMatrix*vec4(position,1.);gl_PointSize=clamp(size*7.*h/(-mv.z*1.15),3.,46.);gl_Position=projectionMatrix*mv;}`,
  fragmentShader:`uniform sampler2D map;varying vec3 vC;void main(){float a=texture2D(map,gl_PointCoord).a;gl_FragColor=vec4(vC*a*0.75,1.);}`});
addEventListener('resize',()=>{glowMat.uniforms.h.value=innerHeight});
let glow=null;

function load(i){
  run=RUNS[i]; k=0; tp=run.frames[0].t; cur=-1; aliveK=-1;
  if(mass){scene.remove(mass);mass.geometry.dispose()} if(agents){scene.remove(agents)}
  for(const v of vessels)scene.remove(v); for(const t of trails)scene.remove(t); vessels=[];trails=[];trailBuf=[];vState=[];
  nMass=Math.max(run.meta.mass_n_max||0,run.mass0.pos.length);
  mass=new THREE.InstancedMesh(prismGeo,lam(),Math.max(1,nMass)); mass.instanceMatrix.setUsage(THREE.DynamicDrawUsage); scene.add(mass);
  massSt=new Int8Array(nMass); massPos=new Float32Array(nMass*3); massRot=[]; massAlive=new Uint8Array(nMass);
  for(let j=0;j<nMass;j++){massRot.push(hashRot(j,new THREE.Quaternion()));mass.setColorAt(j,tmpC.setRGB(0,0,0))}
  maxA=1; for(const f of run.frames){let a=0;for(const s in f.species)a+=f.species[s].pos.length;maxA=Math.max(maxA,a)}
  agents=new THREE.InstancedMesh(bodyGeo,lam(),maxA); agents.instanceMatrix.setUsage(THREE.DynamicDrawUsage); scene.add(agents);
  if(glow){scene.remove(glow);glow.geometry.dispose()} {const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(maxA*3),3));
    g.setAttribute('col',new THREE.BufferAttribute(new Float32Array(maxA*3),3));g.setAttribute('size',new THREE.BufferAttribute(new Float32Array(maxA),1));glow=new THREE.Points(g,glowMat);glow.frustumCulled=false;scene.add(glow)}
  for(let j=0;j<maxA;j++)agents.setColorAt(j,tmpC.setRGB(1,1,1));
  aPos=new Float32Array(maxA*3);aDir=new Float32Array(maxA*3);aCol=new Float32Array(maxA*3);aSize=new Float32Array(maxA);aSpec=new Array(maxA);
  const np=Math.max(...run.frames.map(f=>f.pilots.length),0);
  for(let j=0;j<np;j++){vessels.push(makeVessel());trails.push(makeTrail());trailBuf.push([]);vState.push({q:new THREE.Quaternion(),fwd:new THREE.Vector3(0,0,1),roll:0,speed:0,prev:null})}
  scrub.max=run.frames.length-1; follow=null; const hasA=run.frames.some(f=>Object.values(f.species).some(b=>b.pos.length)); setMode(np?(hasA?'watch':'chase'):'orbit',true);
  document.getElementById('info').textContent=(run.meta.note||'');
}
function setMass(j,x,y,z,st){massPos[j*3]=x;massPos[j*3+1]=y;massPos[j*3+2]=z;massSt[j]=st;
  const c=ST[st]||ST[0];mass.setColorAt(j,tmpC.setRGB(c[0],c[1],c[2]));writeMass(j)}
function writeMass(j){const s=massAlive[j]?(STS[massSt[j]]||1):0;tmpS.set(s,s,s);tmpP.set(massPos[j*3],massPos[j*3+1],massPos[j*3+2]);
  tmpM.compose(tmpP,massRot[j],tmpS);mass.setMatrixAt(j,tmpM)}
function reset(){const m=run.mass0,n=m.pos.length;massAlive.fill(0);
  for(let j=0;j<n;j++){const p=m.pos[j];massAlive[j]=1;setMass(j,p[0],p[1],p[2],m.style?m.style[j]:m.elem[j]%4)}
  for(let j=n;j<nMass;j++)writeMass(j); cur=-1; aliveK=-1}
function seek(kk){if(kk<cur||cur<0)reset();
  for(let q=cur+1;q<=kk;q++){const md=run.frames[q].md;if(md)for(const d of md)setMass(d[0],d[1],d[2],d[3],d[4])}
  cur=kk; mass.instanceMatrix.needsUpdate=true; if(mass.instanceColor)mass.instanceColor.needsUpdate=true}
function hex2bits(h){const b=new Uint8Array(h.length/2);for(let i=0;i<b.length;i++)b[i]=parseInt(h.substr(i*2,2),16);return b}
function applyAlive(f){const bits=hex2bits(f.alive);
  for(let j=0;j<nMass;j++){const a=(j<bits.length*8&&((bits[j>>3]>>(7-(j&7)))&1))?1:0;if(a!==massAlive[j]){massAlive[j]=a;writeMass(j)}}
  mass.instanceMatrix.needsUpdate=true}

// ---- per-frame state between two recorded frames ----------------------------------------------------------
let pilotNow=null;            // interpolated pilot 0 (kept for panels that read it)
function show(fa,fb,u,dt){
  if(k!==cur)seek(k); if(aliveK!==k){applyAlive(fa);aliveK=k}
  let q=0; const GP=glow.geometry.attributes.position.array,GC=glow.geometry.attributes.col.array,GS=glow.geometry.attributes.size.array;
  for(const s in fa.species){const a=fa.species[s],b=fb.species[s],same=b&&b.pos.length===a.pos.length;
    for(let j=0;j<a.pos.length&&q<maxA;j++){const pa=a.pos[j];let x=pa[0],y=pa[1],z=pa[2],dx=0,dy=0,dz=0;
      if(same){const pb=b.pos[j];x+=(pb[0]-pa[0])*u;y+=(pb[1]-pa[1])*u;z+=(pb[2]-pa[2])*u;dx=pb[0]-pa[0];dy=pb[1]-pa[1];dz=pb[2]-pa[2]}
      aPos[q*3]=x;aPos[q*3+1]=y;aPos[q*3+2]=z; const L=Math.hypot(dx,dy,dz);
      if(L>1e-3){aDir[q*3]=dx/L;aDir[q*3+1]=dy/L;aDir[q*3+2]=dz/L}else if(!aDir[q*3]&&!aDir[q*3+1]&&!aDir[q*3+2])aDir[q*3+2]=1;
      const sz=same?a.size[j]+(b.size[j]-a.size[j])*u:a.size[j]; aSize[q]=sz; aSpec[q]=s; const c=a.col[j];
      tmpP.set(x,y,z); tmpV.set(aDir[q*3],aDir[q*3+1],aDir[q*3+2]); tmpQ.setFromUnitVectors(Zp,tmpV);
      const w=Math.max(0.6,sz); tmpS.set(w,w,w*1.15); tmpM.compose(tmpP,tmpQ,tmpS); agents.setMatrixAt(q,tmpM);
      agents.setColorAt(q,tmpC.setRGB(Math.min(1,c[0]*1.1+0.05),Math.min(1,c[1]*1.1+0.05),Math.min(1,c[2]*1.1+0.05)));
      GP[q*3]=x;GP[q*3+1]=y;GP[q*3+2]=z;GC[q*3]=c[0];GC[q*3+1]=c[1];GC[q*3+2]=c[2];GS[q]=sz; q++}}
  aCount=q; for(let j=q;j<maxA;j++){tmpM.makeScale(0,0,0);agents.setMatrixAt(j,tmpM);GS[j]=0}
  for(const a of ['position','col','size'])glow.geometry.attributes[a].needsUpdate=true;
  agents.instanceMatrix.needsUpdate=true; if(agents.instanceColor)agents.instanceColor.needsUpdate=true;
  const sameP=fb.pilots.length===fa.pilots.length;
  for(let j=0;j<vessels.length;j++){const v=vessels[j],st=vState[j];
    if(j>=fa.pilots.length){v.visible=false;trails[j].visible=false;continue} v.visible=true;trails[j].visible=true;
    const pa=fa.pilots[j],pb=sameP?fb.pilots[j]:pa; const p=new THREE.Vector3(pa[0]+(pb[0]-pa[0])*u,pa[1]+(pb[1]-pa[1])*u,pa[2]+(pb[2]-pa[2])*u);
    const span=Math.max(1e-3,fb.t-fa.t), vel=new THREE.Vector3(pb[0]-pa[0],pb[1]-pa[1],pb[2]-pa[2]).divideScalar(span);
    if(st.prev&&st.prev.distanceTo(p)>250){trailBuf[j].length=0}       // a loop/seek: never draw a streak across the cell
    const sp=vel.length(); st.speed=sp;
    if(sp>1e-3){const f=vel.clone().normalize();
      const yawRate=(dt>0&&st.fwd)?Math.atan2(new THREE.Vector3().crossVectors(st.fwd,f).y,st.fwd.dot(f))/Math.max(dt,1e-3):0;
      st.roll+=(THREE.MathUtils.clamp(-yawRate*0.55,-1.1,1.1)-st.roll)*(1-Math.exp(-dt*4)); st.fwd.lerp(f,1-Math.exp(-dt*8)).normalize()}
    const m=new THREE.Matrix4().lookAt(new THREE.Vector3(0,0,0),st.fwd.clone().negate(),new THREE.Vector3(0,1,0));
    st.q.setFromRotationMatrix(m).multiply(new THREE.Quaternion().setFromAxisAngle(Zp,st.roll));
    v.position.copy(p);v.quaternion.copy(st.q); st.prev=p.clone();
    const tb=trailBuf[j]; if(!tb.length||tb[tb.length-1].distanceTo(p)>2.5){tb.push(p.clone());if(tb.length>TRAILN)tb.shift()}
    const P=trails[j].geometry.attributes.position.array,C=trails[j].geometry.attributes.color.array;
    for(let i=0;i<TRAILN;i++){const e=tb[Math.max(0,tb.length-1-i)]||p;P.set([e.x,e.y,e.z],i*3);const a=(1-i/TRAILN)*(tb.length>i?1:0);C.set([0.5*a,0.82*a,a],i*3)}
    trails[j].geometry.attributes.position.needsUpdate=true;trails[j].geometry.attributes.color.needsUpdate=true;
    if(j===0)pilotNow=[p.x,p.y,p.z]}
  if(!fa.pilots.length)pilotNow=null;
  // drop lines: the followed body + the pilot + the creatures nearest the camera target
  const DP=drops.geometry.attributes.position.array,DC=drops.geometry.attributes.color.array;let d=0;
  const addDrop=(x,y,z,r,g,b)=>{if(d>=DROPMAX)return;DP.set([x,y,z,x,0,z],d*6);DC.set([r,g,b,r*0.3,g*0.3,b*0.3],d*6);d++};
  for(const v of vessels)if(v.visible)addDrop(v.position.x,v.position.y,v.position.z,0.6,0.85,1);
  const tgt=camTarget(); const near=[];
  for(let j=0;j<aCount;j++){const dx=aPos[j*3]-tgt.x,dy=aPos[j*3+1]-tgt.y,dz=aPos[j*3+2]-tgt.z,dd=dx*dx+dy*dy+dz*dz;if(dd<450*450)near.push([dd,j])}
  near.sort((a,b)=>a[0]-b[0]); for(const [,j] of near.slice(0,DROPMAX-vessels.length)){const c=agents.instanceColor?agents.instanceColor.array:null;
    addDrop(aPos[j*3],aPos[j*3+1],aPos[j*3+2],c?c[j*3]*0.7:0.6,c?c[j*3+1]*0.7:0.6,c?c[j*3+2]*0.7:0.6)}
  for(let j=d;j<DROPMAX;j++)DP.fill(0,j*6,j*6+6); drops.geometry.attributes.position.needsUpdate=true;drops.geometry.attributes.color.needsUpdate=true;
  document.getElementById('t').textContent='t '+(fa.t+(fb.t-fa.t)*u).toFixed(1)+' s'+(run.meta.hud?'  '+(run.meta.hud[k]||''):'');
}

// ---- cameras ----------------------------------------------------------------------------------------------
let mode='chase', follow=null;   // follow: null = pilot 0, else {spec, pos:Vector3} tracked by continuity
const camSel=document.getElementById('cammode');
function setMode(m,frame){mode=m;camSel.value=m;ctl.enabled=(m==='orbit');
  if(m==='orbit'&&frame){const fo=run.meta.focus;if(fo){ctl.target.set(fo[0],fo[1],fo[2]);cam.position.set(fo[0]+fo[3]*0.35,fo[1]+fo[3]*0.35,fo[2]+fo[3])}
    else{ctl.target.set(0,0,0);cam.position.set(0,1100,2600)}}
  if(m==='orbit'&&!frame){ctl.target.copy(camTarget())}
  if(m==='fly'){flyYaw=Math.atan2(-camFwd().x,-camFwd().z);flyPitch=Math.asin(THREE.MathUtils.clamp(camFwd().y,-1,1))}}
camSel.onchange=()=>setMode(camSel.value,false);
function camFwd(){return new THREE.Vector3(0,0,-1).applyQuaternion(cam.quaternion)}
function followed(){ // returns {pos, fwd, speed}
  if(follow&&aCount){let best=-1,bd=1e18;for(let j=0;j<aCount;j++){if(aSpec[j]!==follow.spec)continue;const dx=aPos[j*3]-follow.pos.x,dy=aPos[j*3+1]-follow.pos.y,dz=aPos[j*3+2]-follow.pos.z,dd=dx*dx+dy*dy+dz*dz;if(dd<bd){bd=dd;best=j}}
    if(best>=0&&bd<200*200){follow.pos.set(aPos[best*3],aPos[best*3+1],aPos[best*3+2]);follow.fwd.lerp(new THREE.Vector3(aDir[best*3],aDir[best*3+1],aDir[best*3+2]),0.08).normalize();
      return {pos:follow.pos,fwd:follow.fwd,speed:0,size:aSize[best]}}
    follow=null}
  if(vessels.length&&vessels[0].visible){const st=vState[0];return {pos:vessels[0].position,fwd:st.fwd,speed:st.speed,size:6}}
  return {pos:new THREE.Vector3(),fwd:new THREE.Vector3(0,0,1),speed:0,size:6}}
function camTarget(){return mode==='orbit'?ctl.target:(mode==='watch'&&watchT?watchT:followed().pos)}
// the creatures the pilot is facing off with: centroid of the nearest few within 700 u (null if none)
let watchT=null, watchSpec='', watchSpread=0;
function nearestGroup(p){const near=[];for(let j=0;j<aCount;j++){const dx=aPos[j*3]-p.x,dy=aPos[j*3+1]-p.y,dz=aPos[j*3+2]-p.z,dd=dx*dx+dy*dy+dz*dz;if(dd<700*700)near.push([dd,j])}
  if(!near.length)return null;near.sort((a,b)=>a[0]-b[0]);const c=new THREE.Vector3();const m=Math.min(8,near.length);
  for(let i=0;i<m;i++){const j=near[i][1];c.x+=aPos[j*3];c.y+=aPos[j*3+1];c.z+=aPos[j*3+2]}c.divideScalar(m);
  let s2=0;for(let i=0;i<m;i++){const j=near[i][1];s2+=(aPos[j*3]-c.x)**2+(aPos[j*3+1]-c.y)**2+(aPos[j*3+2]-c.z)**2;s2+=0}
  watchSpread=Math.sqrt(s2/m)+(m>1?0:aSize[near[0][1]]*4);watchSpec=aSpec[near[0][1]];return c}
const chase={pos:null,look:null};
const keys={}; addEventListener('keydown',e=>{keys[e.code]=true;
  if(e.target.tagName==='SELECT'||e.target.tagName==='INPUT')return;
  if(e.code==='Space'){e.preventDefault();document.getElementById('play').click()}
  if(e.code==='Digit1')setMode('watch');if(e.code==='Digit2')setMode('chase');if(e.code==='Digit3')setMode('orbit');if(e.code==='Digit4')setMode('fly');
  if(e.code==='Escape'){follow=null}});
addEventListener('keyup',e=>{keys[e.code]=false});
let flyYaw=0,flyPitch=0,drag=null;
ren.domElement.addEventListener('pointerdown',e=>{drag={x:e.clientX,y:e.clientY,moved:false}});
addEventListener('pointermove',e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y;if(Math.abs(dx)+Math.abs(dy)>3)drag.moved=true;
  if(mode==='fly'&&drag.moved){flyYaw-=dx*0.004;flyPitch=THREE.MathUtils.clamp(flyPitch-dy*0.004,-1.5,1.5);drag.x=e.clientX;drag.y=e.clientY}});
addEventListener('pointerup',e=>{if(drag&&!drag.moved)pick(e);drag=null});
const ray=new THREE.Raycaster();
function pick(e){const r=ren.domElement.getBoundingClientRect();ray.setFromCamera({x:(e.clientX-r.left)/r.width*2-1,y:-(e.clientY-r.top)/r.height*2+1},cam);
  // pick the creature nearest the ray (generous: bodies are small at range)
  let best=-1,bd=1e18;const o=ray.ray.origin,dv=ray.ray.direction;
  for(let j=0;j<aCount;j++){tmpV.set(aPos[j*3]-o.x,aPos[j*3+1]-o.y,aPos[j*3+2]-o.z);const t=tmpV.dot(dv);if(t<0)continue;
    const perp=tmpV.lengthSq()-t*t,tol=Math.max(aSize[j]*1.5,t*0.02);if(perp<tol*tol&&t<bd){bd=t;best=j}}
  if(best>=0){follow={spec:aSpec[best],pos:new THREE.Vector3(aPos[best*3],aPos[best*3+1],aPos[best*3+2]),fwd:new THREE.Vector3(aDir[best*3],aDir[best*3+1],aDir[best*3+2])};
    if(mode==='orbit')ctl.target.copy(follow.pos);if(mode==='fly'||mode==='watch')setMode('chase')}}
function updateCamera(dt){
  if(mode==='watch'){const f=followed(),g=nearestGroup(f.pos);
    if(!g){watchT=null}else{watchT=watchT?watchT.lerp(g,1-Math.exp(-dt*3)):g.clone();
      const toT=watchT.clone().sub(f.pos),d=Math.max(1,toT.length()),dir=toT.divideScalar(d),R=THREE.MathUtils.clamp(Math.max(d*0.55,watchSpread*1.8),80,420);
      const side=new THREE.Vector3().crossVectors(dir,new THREE.Vector3(0,1,0)).normalize();
      const want=f.pos.clone().addScaledVector(dir,-R).add(new THREE.Vector3(0,R*0.35,0)).addScaledVector(side,R*0.25),look=f.pos.clone().lerp(watchT,0.6);
      if(!chase.pos||chase.pos.distanceTo(want)>800){chase.pos=want.clone();chase.look=look.clone()}
      chase.pos.lerp(want,1-Math.exp(-dt*2.5));chase.look.lerp(look,1-Math.exp(-dt*4));cam.position.copy(chase.pos);cam.lookAt(chase.look);return}}
  if(mode==='chase'||mode==='watch'){const f=followed(),back=Math.max(70,f.size*14),up=Math.max(24,f.size*4.5);
    const want=f.pos.clone().addScaledVector(f.fwd,-back).add(new THREE.Vector3(0,up,0)),look=f.pos.clone().addScaledVector(f.fwd,back*0.6);
    if(!chase.pos||chase.pos.distanceTo(want)>800){chase.pos=want.clone();chase.look=look.clone()}
    chase.pos.lerp(want,1-Math.exp(-dt*3.2));chase.look.lerp(look,1-Math.exp(-dt*5));cam.position.copy(chase.pos);cam.lookAt(chase.look)}
  else if(mode==='orbit'){if(follow){const f=followed();const d=f.pos.clone().sub(ctl.target);ctl.target.add(d);cam.position.add(d)}ctl.update()}
  else{const fwd=new THREE.Vector3(-Math.sin(flyYaw)*Math.cos(flyPitch),Math.sin(flyPitch),-Math.cos(flyYaw)*Math.cos(flyPitch)),right=new THREE.Vector3().crossVectors(fwd,new THREE.Vector3(0,1,0)).normalize();
    const s=(keys.ShiftLeft||keys.ShiftRight?900:280)*dt,mv=new THREE.Vector3();
    if(keys.KeyW)mv.add(fwd);if(keys.KeyS)mv.sub(fwd);if(keys.KeyD)mv.add(right);if(keys.KeyA)mv.sub(right);if(keys.KeyR)mv.y+=1;if(keys.KeyF)mv.y-=1;
    cam.position.addScaledVector(mv,s);cam.lookAt(cam.position.clone().add(fwd))}}

// ---- HUD: readout, attitude, minimap --------------------------------------------------------------------------
const hudtxt=document.getElementById('hudtxt'),att=document.getElementById('att').getContext('2d'),mini=document.getElementById('mini'),mc=mini.getContext('2d');
function hud(){const f=followed(),fw=f.fwd,head=(Math.atan2(fw.x,fw.z)*180/Math.PI+360)%360,pitch=Math.asin(THREE.MathUtils.clamp(fw.y,-1,1))*180/Math.PI;
  const who=follow?('following '+follow.spec):(mode==='watch'&&watchT?('pilot view: '+watchSpec+' '+watchT.distanceTo(f.pos).toFixed(0)+' u away'):(vessels.length?'following pilot':'no pilot'));
  hudtxt.innerHTML=`<b>${who}</b><br>speed ${f.speed?f.speed.toFixed(0)+' u/s':'-'}<br>heading ${head.toFixed(0)}&deg; pitch ${pitch>=0?'+':''}${pitch.toFixed(0)}&deg;<br>height ${f.pos.y>=0?'+':''}${f.pos.y.toFixed(0)} r ${f.pos.length().toFixed(0)}`;
  const W=120,H=60;att.clearRect(0,0,W,H);att.save();att.translate(W/2,H/2);const roll=(follow||!vState.length)?0:vState[0].roll;att.rotate(roll);
  const py=THREE.MathUtils.clamp(pitch,-60,60)*0.6;att.fillStyle='#16304e';att.fillRect(-W,-H*2+py,W*2,H*2);att.fillStyle='#3a2a1a';att.fillRect(-W,py,W*2,H*2);
  att.strokeStyle='#9fc4ff';att.lineWidth=1;att.beginPath();att.moveTo(-W,py);att.lineTo(W,py);att.stroke();att.restore();
  att.strokeStyle='#ffd27f';att.lineWidth=2;att.beginPath();att.moveTo(W/2-22,H/2);att.lineTo(W/2-7,H/2);att.lineTo(W/2,H/2+5);att.lineTo(W/2+7,H/2);att.lineTo(W/2+22,H/2);att.stroke()}
function minimap(){const S=mini.width,c=S/2,sc=(S/2-6)/R_CELL;mc.clearRect(0,0,S,S);
  mc.strokeStyle='#34508f';mc.lineWidth=1;mc.beginPath();mc.arc(c,c,R_CELL*sc,0,Math.PI*2);mc.stroke();
  mc.fillStyle='rgba(120,150,190,.35)';const st=Math.max(1,Math.floor(nMass/1500));for(let j=0;j<nMass;j+=st)if(massAlive[j])mc.fillRect(c+massPos[j*3]*sc,c+massPos[j*3+2]*sc,1,1);
  const col=agents.instanceColor?agents.instanceColor.array:null;for(let j=0;j<aCount;j++){mc.fillStyle=col?`rgb(${col[j*3]*255|0},${col[j*3+1]*255|0},${col[j*3+2]*255|0})`:'#fff';mc.fillRect(c+aPos[j*3]*sc-1,c+aPos[j*3+2]*sc-1,2,2)}
  for(let j=0;j<vessels.length;j++){if(!vessels[j].visible)continue;const p=vessels[j].position,fw=vState[j].fwd,a=Math.atan2(fw.z,fw.x);
    mc.save();mc.translate(c+p.x*sc,c+p.z*sc);mc.rotate(a);mc.fillStyle='#fff';mc.beginPath();mc.moveTo(7,0);mc.lineTo(-4,4);mc.lineTo(-2,0);mc.lineTo(-4,-4);mc.closePath();mc.fill();mc.restore()}
  const cp=cam.position,cf=camFwd();mc.strokeStyle='#ffd27f';mc.beginPath();mc.moveTo(c+cp.x*sc,c+cp.z*sc);mc.lineTo(c+(cp.x+cf.x*160)*sc,c+(cp.z+cf.z*160)*sc);mc.stroke()}

// ---- playback ---------------------------------------------------------------------------------------------
const sel=document.getElementById('run'),scrub=document.getElementById('scrub'),spd=document.getElementById('speed');
RUNS.forEach((r,i)=>{const o=document.createElement('option');o.value=i;o.textContent=r.meta.label||('run '+i);sel.appendChild(o)});
sel.onchange=()=>load(+sel.value); scrub.oninput=()=>{k=+scrub.value;tp=run.frames[k].t};
document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'pause':'play'};
const uiBar=document.getElementById('ui'),hudBox=document.getElementById('hudbox');
function placeHud(){hudBox.style.top=(uiBar.getBoundingClientRect().bottom+8)+'px'} addEventListener('resize',placeHud);
load(0); placeHud(); let last=performance.now(), hudT=0;
document.getElementById('info').insertAdjacentHTML('beforeend','<div id="help">1 pilot view · 2 chase · 3 orbit · 4 fly (WASD, drag to look, R/F up/down, Shift fast) · click a creature to follow it, Esc back to the pilot · space pause</div>');
(function loop(now){requestAnimationFrame(loop);const dt=Math.min(0.1,(now-last)/1000);last=now;
  const F=run.frames,n=F.length;
  if(playing&&n>1){tp+=dt*(+spd.value||1);if(tp>=F[n-1].t){tp=F[0].t;k=0}while(k<n-2&&F[k+1].t<=tp)k++;scrub.value=k}
  const fa=F[k],fb=F[Math.min(k+1,n-1)],span=fb.t-fa.t;
  show(fa,fb,span>0?Math.min(1,Math.max(0,(tp-fa.t)/span)):0,dt); updateCamera(dt);
  hudT+=dt;if(hudT>0.1){hudT=0;placeHud();hud();minimap()} ren.render(scene,cam)})(performance.now());
</script>
"""


def build(out: str, paths: list[str], title: str):
    runs = [json.load(open(p)) for p in paths]
    with open(out, "w") as fh:
        fh.write(TPL.replace("__TITLE__", title).replace("__DATA__", json.dumps(runs, separators=(",", ":"))))
    return os.path.getsize(out)


def extract(html: str):
    """(title, runs-json-text, extra) from a viewer built with ANY version of this template - lets an old
    viewer's recorded data be re-rendered with the current template without re-running its sims."""
    title = re.search(r"<title>(.*?)</title>", html, re.S).group(1)
    m = re.search(r"const RUNS = (\[.*?\]);\n", html, re.S)
    return title, m.group(1)


def retemplate(path: str, out: str | None = None):
    title, data = extract(open(path, encoding="utf-8").read())
    out = out or path
    with open(out, "w", encoding="utf-8") as fh:
        fh.write(TPL.replace("__TITLE__", title).replace("__DATA__", data))
    return os.path.getsize(out)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("out", nargs="?"); ap.add_argument("runs", nargs="*"); ap.add_argument("--title", default="Ecology run")
    ap.add_argument("--retemplate", nargs="+", help="re-render existing viewer HTML files in place with this template")
    a = ap.parse_args()
    if a.retemplate:
        for p in a.retemplate:
            print(p, retemplate(p), "bytes")
    else:
        print(build(a.out, a.runs, a.title), "bytes")
