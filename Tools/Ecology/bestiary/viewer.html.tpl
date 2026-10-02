<title>Hypersea Bestiary</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@500;700&family=Barlow:wght@400;500&family=JetBrains+Mono:wght@400;600&display=swap">
<style>
/* layout: full-bleed 3D tank; a specimen ledger docked right (stacks under the tank on phones). Single dark look on purpose: it is a deep-sea tank. */
:root{
  --abyss:#070a12; --panel:#0d1220; --line:#1d2742; --ink:#dfe5f6; --mute:#8590b3;
  --sonar:#59d6c4; --warn:#ff7a59; --good:#9fe07a;
  --f-display:"Chakra Petch",ui-sans-serif,system-ui,sans-serif;
  --f-body:"Barlow",ui-sans-serif,system-ui,sans-serif;
  --f-data:"JetBrains Mono",ui-monospace,Menlo,monospace;
  color-scheme:dark;
}
html,body{height:100%}
body{background:var(--abyss);color:var(--ink);font:15px/1.5 var(--f-body);overflow:hidden}
#app{display:grid;grid-template-columns:minmax(0,1fr) 400px;height:100%}
#tank{position:relative;min-width:0}
#tank canvas{display:block;width:100%;height:100%}
#hud{position:absolute;left:16px;right:16px;top:calc(12px + env(safe-area-inset-top,0px));display:flex;flex-wrap:wrap;gap:8px;align-items:center}
#hud button,#hud select{background:rgba(13,18,32,.85);color:var(--ink);border:1px solid var(--line);border-radius:4px;padding:5px 10px;font:500 13px var(--f-body);cursor:pointer}
#hud button:focus-visible,#hud input:focus-visible,.sp:focus-visible{outline:2px solid var(--sonar);outline-offset:2px}
#hud button[aria-pressed="true"]{border-color:var(--sonar);color:var(--sonar)}
#scrub{flex:1;min-width:120px;accent-color:var(--sonar)}
#clock{font:13px var(--f-data);font-variant-numeric:tabular-nums;color:var(--mute);min-width:9ch}
#flash{position:absolute;left:16px;bottom:calc(16px + env(safe-area-inset-bottom,0px));font:600 13px var(--f-data);color:var(--warn);letter-spacing:.08em;text-transform:uppercase}
#legend{position:absolute;right:16px;bottom:calc(16px + env(safe-area-inset-bottom,0px));font:12px var(--f-data);color:var(--mute);text-align:right}
aside{background:var(--panel);border-left:1px solid var(--line);overflow-y:auto;padding-block:20px;padding-inline:20px;display:flex;flex-direction:column;gap:18px;min-width:0}
h1{font:700 22px/1.1 var(--f-display);letter-spacing:.06em;text-transform:uppercase;margin:0;text-wrap:balance}
.sub{color:var(--mute);font-size:13px;margin:4px 0 0}
#list{display:grid;grid-template-columns:1fr 1fr;gap:6px}
.sp{background:transparent;border:1px solid var(--line);border-radius:4px;color:var(--ink);text-align:left;padding:7px 9px;cursor:pointer;font:500 13px var(--f-body)}
.sp b{display:block;font:700 13px var(--f-display);letter-spacing:.05em;text-transform:uppercase}
.sp span{color:var(--mute);font-size:12px}
.sp[aria-current="true"]{border-color:var(--sonar);background:rgba(89,214,196,.08)}
h2{font:700 18px var(--f-display);letter-spacing:.05em;text-transform:uppercase;margin:0}
.emo{color:var(--sonar);font-size:14px;margin:2px 0 0}
.lbl{font:600 11px var(--f-data);letter-spacing:.12em;text-transform:uppercase;color:var(--mute);margin:0 0 6px}
table{width:100%;border-collapse:collapse;font:13px var(--f-data);font-variant-numeric:tabular-nums}
td{padding:3px 0;border-bottom:1px solid var(--line)}
td:last-child{text-align:right}
.ok{color:var(--good)} .no{color:var(--warn)}
#doc{white-space:pre-wrap;font:12.5px/1.55 var(--f-data);color:#b6bfdc;max-width:65ch;margin:0}
.counter{font-size:14px;margin:0}
@media (max-width:820px){
  body{overflow:auto}
  #app{grid-template-columns:1fr;grid-template-rows:62vh auto;height:auto}
  aside{border-left:0;border-top:1px solid var(--line)}
}
@media (prefers-reduced-motion:reduce){*{scroll-behavior:auto}}
</style>
<div id="app">
  <div id="tank">
    <div id="hud">
      <button id="play" aria-pressed="false">Pause</button>
      <button id="follow" aria-pressed="false">Chase cam</button>
      <select id="speed" aria-label="Playback speed"><option value="0.5">0.5x</option><option value="1" selected>1x</option><option value="2">2x</option><option value="4">4x</option></select>
      <input id="scrub" type="range" min="0" value="0" aria-label="Timeline">
      <span id="clock">t 0.0 s</span>
    </div>
    <div id="flash" aria-live="polite"></div>
    <div id="legend">white diamond = pilot · thin line = its trail<br>dim points = plants (mass) · drag to orbit, scroll to zoom</div>
  </div>
  <aside>
    <div><h1>Hypersea Bestiary</h1><p class="sub">Eight threat fauna run on local rules only, each recorded against the pilot that shows it best. Seed 7, 45 s.</p></div>
    <div id="list" role="list"></div>
    <div><h2 id="name"></h2><p class="emo" id="emo"></p></div>
    <div><p class="lbl">Counterplay</p><p class="counter" id="counter"></p></div>
    <div><p class="lbl">Scorecard (4 pilots x 3 seeds, 90 s)</p><table id="card"></table></div>
    <div><p class="lbl">Outside emotion read (Direction C probe)</p><table id="probe"></table></div>
    <div><p class="lbl">Rules</p><p id="doc"></p></div>
  </aside>
</div>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
const RUNS = __DATA__;
const EL = [[0.35,0.55,0.7],[0.7,0.45,0.2],[0.5,0.35,0.7],[0.35,0.7,0.4]];
function dec(b64, T){const s=atob(b64);const u=new Uint8Array(s.length);for(let i=0;i<s.length;i++)u[i]=s.charCodeAt(i);return new T(u.buffer)}
const tank=document.getElementById('tank');
const scene=new THREE.Scene(), cam=new THREE.PerspectiveCamera(55,1,1,20000);
const ren=new THREE.WebGLRenderer({antialias:true}); ren.setPixelRatio(Math.min(2,devicePixelRatio)); tank.prepend(ren.domElement);
scene.background=new THREE.Color(0x070a12);
const ctl=new THREE.OrbitControls(cam,ren.domElement); ctl.enableDamping=true;
function resize(){const w=tank.clientWidth,h=tank.clientHeight;cam.aspect=w/h;cam.updateProjectionMatrix();ren.setSize(w,h)}
addEventListener('resize',resize);
scene.add(new THREE.Mesh(new THREE.SphereGeometry(1200,48,24),new THREE.MeshBasicMaterial({color:0x2a4a7a,wireframe:true,transparent:true,opacity:0.05})));
function ptsMat(glow){return new THREE.ShaderMaterial({transparent:true,depthWrite:false,blending:THREE.AdditiveBlending,
 vertexShader:`attribute float size;attribute vec3 col;varying vec3 vC;void main(){vC=col;vec4 mv=modelViewMatrix*vec4(position,1.);gl_PointSize=clamp(size*${glow?'1100.':'700.'}/-mv.z,${glow?'2.5':'1.0'},96.);gl_Position=projectionMatrix*mv;}`,
 fragmentShader:`varying vec3 vC;void main(){vec2 d=gl_PointCoord-.5;float r=dot(d,d);if(r>.25)discard;gl_FragColor=vec4(vC*(1.15-2.6*r),1.);}`})}
function cloud(n,glow){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(n*3),3));
 g.setAttribute('col',new THREE.BufferAttribute(new Float32Array(n*3),3));g.setAttribute('size',new THREE.BufferAttribute(new Float32Array(n),1));
 const p=new THREE.Points(g,ptsMat(glow));p.frustumCulled=false;scene.add(p);return p}
let R=null,k=0,ft=0,playing=true,follow=false,objs=[],pilotTrail;
function load(i){
  for(const o of objs)scene.remove(o); objs=[];
  const r=RUNS[i]; R={...r};
  R.counts=dec(r.counts,Uint16Array); R.pos=dec(r.pos,Int16Array); R.col=dec(r.col,Uint8Array); R.size=dec(r.size,Uint8Array);
  R.pil=dec(r.pilots,Int16Array); R.mpos=dec(r.mpos,Int16Array); R.melem=dec(r.melem,Uint8Array); R.mdeath=dec(r.mdeath,Uint16Array);
  R.tpos=dec(r.tpos,Int16Array); R.tborn=dec(r.tborn,Uint16Array); R.tdeath=dec(r.tdeath,Uint16Array);
  R.off=new Uint32Array(r.nf+1); for(let f=0;f<r.nf;f++)R.off[f+1]=R.off[f]+R.counts[f];
  let maxA=1; for(const c of R.counts)maxA=Math.max(maxA,c);
  const nm=R.melem.length; R.mass=cloud(nm,false);
  {const P=R.mass.geometry.attributes.position.array,C=R.mass.geometry.attributes.col.array;
   for(let j=0;j<nm;j++){P[j*3]=R.mpos[j*3];P[j*3+1]=R.mpos[j*3+1];P[j*3+2]=R.mpos[j*3+2];const c=EL[R.melem[j]%4];C[j*3]=c[0];C[j*3+1]=c[1];C[j*3+2]=c[2]}
   R.mass.geometry.attributes.position.needsUpdate=true;R.mass.geometry.attributes.col.needsUpdate=true}
  const nt=R.tborn.length; R.trail=cloud(Math.max(nt,1),false);
  {const P=R.trail.geometry.attributes.position.array,C=R.trail.geometry.attributes.col.array;
   for(let j=0;j<nt;j++){P.set([R.tpos[j*3],R.tpos[j*3+1],R.tpos[j*3+2]],j*3);C.set([0.75,0.8,0.95],j*3)}
   R.trail.geometry.attributes.position.needsUpdate=true;R.trail.geometry.attributes.col.needsUpdate=true}
  R.ag=cloud(maxA,true); R.pl=cloud(1,true);
  objs=[R.mass,R.trail,R.ag,R.pl];
  const p0=[R.pil[0],R.pil[1],R.pil[2]];
  cam.position.set(p0[0]+500,p0[1]+350,p0[2]+700); ctl.target.set(p0[0],p0[1],p0[2]);
  scrub.max=r.nf-1; k=0; ft=0;
  R.hitF=new Map(); for(const [f,kind] of r.hits) R.hitF.set(f,(R.hitF.get(f)||[]).concat(kind));
  panel(r);
  document.querySelectorAll('.sp').forEach((b,j)=>b.setAttribute('aria-current',j===i?'true':'false'));
}
const fmt=v=>v===null||v===undefined?'n/a':(typeof v==='number'?(Math.round(v*100)/100).toString():v);
function row(t,a,b,cls){return `<tr><td>${a}</td><td class="${cls||''}">${b}</td></tr>`}
function panel(r){
  document.getElementById('name').textContent=r.key;
  document.getElementById('emo').textContent=r.emotion;
  document.getElementById('counter').textContent=r.counter;
  document.getElementById('doc').textContent=r.doc;
  const c=r.card||{}, v=c.verdict||{};
  const ok=b=>b?'ok':'no';
  document.getElementById('card').innerHTML=
    row(0,'telegraph, first strike (s)',fmt(c.telegraph_first_s),ok(v.telegraph))+
    row(0,'telegraph, shared median (s)',fmt(c.telegraph_s))+
    row(0,'hits/min: wanderer · evader · skimmer',`${fmt(c.hits_per_min_wander)} · ${fmt(c.hits_per_min_evader)} · ${fmt(c.hits_per_min_skimmer)}`)+
    row(0,'counterplay (evader / wanderer)',fmt(c.counterplay),ok(v.counterplay))+
    row(0,'counterplay, aware same-speed pilot',fmt(c.counterplay_aware))+
    row(0,'payoff: kills/min for a hunter',fmt(c.payoff_per_min),ok(v.payoff))+
    row(0,'variety',fmt(c.variety),ok(v.variety))+
    row(0,'cost: us per agent-step (numpy)',fmt(c.us_per_agent_step));
  const p=r.probe||{}; let h='';
  for(const vw of ['hover','cruise','evade']) if(p[vw]) h+=row(0,vw+' viewer',`${p[vw].top} ${Math.round(100*Math.max(...Object.values(p[vw].p)))}%`);
  document.getElementById('probe').innerHTML=h||row(0,'not measured','');
}
function show(fr,a){
  const f=Math.min(fr,R.nf-1), g=Math.min(f+1,R.nf-1);
  const n=R.counts[f], same=R.counts[g]===n;
  const P=R.ag.geometry.attributes.position.array,C=R.ag.geometry.attributes.col.array,Z=R.ag.geometry.attributes.size.array;
  const o=R.off[f],o2=R.off[g];
  for(let j=0;j<n;j++){for(let q=0;q<3;q++){const x=R.pos[(o+j)*3+q];P[j*3+q]=same?x+(R.pos[(o2+j)*3+q]-x)*a:x;C[j*3+q]=R.col[(o+j)*3+q]/255}Z[j]=R.size[o+j]/4}
  for(let j=n;j<Z.length;j++)Z[j]=0;
  for(const at of ['position','col','size'])R.ag.geometry.attributes[at].needsUpdate=true;
  const MZ=R.mass.geometry.attributes.size.array; for(let j=0;j<MZ.length;j++)MZ[j]=R.mdeath[j]>f?5:0; R.mass.geometry.attributes.size.needsUpdate=true;
  const TZ=R.trail.geometry.attributes.size.array; for(let j=0;j<R.tborn.length;j++)TZ[j]=(R.tborn[j]<=f&&R.tdeath[j]>f)?3:0; R.trail.geometry.attributes.size.needsUpdate=true;
  const pp=[0,1,2].map(q=>R.pil[f*3+q]+(R.pil[g*3+q]-R.pil[f*3+q])*a);
  R.pl.geometry.attributes.position.array.set(pp);R.pl.geometry.attributes.col.array.set([1,1,1]);R.pl.geometry.attributes.size.array[0]=16;
  for(const at of ['position','col','size'])R.pl.geometry.attributes[at].needsUpdate=true;
  if(follow){const q=Math.max(0,f-5);const dir=[pp[0]-R.pil[q*3],pp[1]-R.pil[q*3+1],pp[2]-R.pil[q*3+2]];const l=Math.hypot(...dir)||1;
    cam.position.lerp(new THREE.Vector3(pp[0]-dir[0]/l*160,pp[1]-dir[1]/l*160+60,pp[2]-dir[2]/l*160),0.15);ctl.target.set(...pp)}
  document.getElementById('clock').textContent='t '+(f*R.dt).toFixed(1)+' s';
  let msg='';for(let b=Math.max(0,f-6);b<=f;b++){const h=R.hitF.get(b);if(h)msg=h[h.length-1]}
  document.getElementById('flash').textContent=msg?('hit: '+msg):'';
}
const list=document.getElementById('list');
RUNS.forEach((r,i)=>{const b=document.createElement('button');b.className='sp';b.setAttribute('role','listitem');
  b.innerHTML=`<b>${r.key}</b><span>${r.emotion.split('(')[0].split('->')[0].split(' with')[0]}</span>`;b.onclick=()=>load(i);list.appendChild(b)});
const scrub=document.getElementById('scrub'), playB=document.getElementById('play'), folB=document.getElementById('follow');
scrub.oninput=()=>{k=+scrub.value;ft=0};
playB.onclick=()=>{playing=!playing;playB.textContent=playing?'Pause':'Play';playB.setAttribute('aria-pressed',String(!playing))};
folB.onclick=()=>{follow=!follow;folB.setAttribute('aria-pressed',String(follow))};
addEventListener('keydown',e=>{if(e.code==='Space'&&e.target.tagName!=='BUTTON'){e.preventDefault();playB.click()}});
resize(); load(0); let last=performance.now();
(function loop(now){requestAnimationFrame(loop);const dt=Math.min(0.1,(now-last)/1000);last=now;
  if(playing){ft+=dt*(+document.getElementById('speed').value)/R.dt;while(ft>=1){ft-=1;k=(k+1)%R.nf}scrub.value=k}
  show(k,playing?ft:0);ctl.update();ren.render(scene,cam)})(performance.now());
</script>
