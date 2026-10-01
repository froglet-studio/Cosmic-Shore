<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Swarm Creature Showcase</title>
<style>
:root{--bg:#f4f5f9;--fg:#1a2033;--mut:#5a6480;--acc:#0f8a76;--panel:#ffffff;--line:#d5dae6;--stage:#060914}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--bg:#0b1020;--fg:#dfe6f5;--mut:#8a96b3;--acc:#7fd1c1;--panel:#121a30;--line:#2a3658}}
:root[data-theme="dark"]{--bg:#0b1020;--fg:#dfe6f5;--mut:#8a96b3;--acc:#7fd1c1;--panel:#121a30;--line:#2a3658}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.45 system-ui,-apple-system,Segoe UI,sans-serif}
main{max-width:1000px;margin:0 auto;padding:16px}
h1{font-size:20px;margin:4px 0}.cap{color:var(--mut);margin:4px 0 12px;max-width:72ch}
.row{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin:8px 0}
button{background:var(--panel);color:var(--fg);border:1px solid var(--line);border-radius:6px;padding:6px 10px;cursor:pointer;font:inherit}
button[aria-pressed=true]{border-color:var(--acc);color:var(--acc)}
canvas{width:100%;aspect-ratio:1.6;background:var(--stage);border-radius:10px;display:block;touch-action:none}
input[type=range]{flex:1;min-width:140px}
#ev{color:var(--acc);min-height:2.8em;font-weight:500}
.leg{color:var(--mut);font-size:13px}.leg span{display:inline-flex;align-items:center;margin-right:12px}
.dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:5px}
#evlist{font-size:13px;color:var(--mut);padding-left:18px}#evlist li{cursor:pointer}#evlist li.on{color:var(--fg)}
</style></head><body><main>
<h1>The swarm creature</h1>
<p class="cap">A learned body (the evolved G2 rule grows, colours and re-forms it) wearing a designed reaction shell: tadpoles startle, flee, inflate, jet, mob and heal around a vessel. Drag to orbit; click an event to jump to it.</p>
<div class="row" id="kinds"></div>
<canvas id="cv" width="1440" height="900" aria-label="Swarm creature playback"></canvas>
<div class="row"><button id="play" aria-pressed="true">Pause</button><button id="spd">1x</button><input type="range" id="sl" min="0" value="0" aria-label="time"><span id="tt"></span></div>
<div id="ev"></div>
<ol id="evlist"></ol>
<div class="row leg"><span><i class="dot" style="background:#e8a93a"></i>Charge</span><span><i class="dot" style="background:#8e6bd8"></i>Mass</span><span><i class="dot" style="background:#3a7bdc"></i>Space</span><span><i class="dot" style="background:#2fb39a"></i>Time</span><span>ring = domain</span><span>red spikes = danger</span><span>white = startled</span><span>lime diamonds = crystals</span><span>white sphere = vessel</span></div>
</main><script>
const DATA=/*DATA*/, SC=/*SCALE*/;
const EL=['#e8a93a','#8e6bd8','#3a7bdc','#2fb39a'], DOM=['#1fbfa6','#d04a6a','#d8b040'];
const cv=document.getElementById('cv'),g=cv.getContext('2d');let kind=Object.keys(DATA)[0],fi=0,playing=true,speed=1,yaw=0.6,pitch=0.32,drag=null,cache={},trail=[];
function dec(k,key,T){const id=k+key;if(cache[id])return cache[id];const s=key?DATA[k][key].b64:DATA[k].b64;const b=atob(s),u=new Uint8Array(b.length);for(let i=0;i<b.length;i++)u[i]=b.charCodeAt(i);return cache[id]=new T(u.buffer);}
const kd=document.getElementById('kinds');for(const k of Object.keys(DATA)){const bt=document.createElement('button');bt.textContent=DATA[k].name;bt.onclick=()=>{kind=k;fi=0;trail=[];sync();};bt.dataset.k=k;kd.appendChild(bt);}
const sl=document.getElementById('sl'),evl=document.getElementById('evlist');
function sync(){for(const b of kd.children)b.setAttribute('aria-pressed',b.dataset.k===kind);sl.max=DATA[kind].shape[0]-1;evl.innerHTML='';for(const [s,t] of DATA[kind].events){const li=document.createElement('li');li.textContent=`step ${s}: ${t}`;li.dataset.s=s;li.onclick=()=>{fi=Math.floor(s/DATA[kind].every);trail=[];};evl.appendChild(li);}}
sync();sl.oninput=()=>{fi=+sl.value;trail=[];};
document.getElementById('play').onclick=e=>{playing=!playing;e.target.textContent=playing?'Pause':'Play';e.target.setAttribute('aria-pressed',playing);};
document.getElementById('spd').onclick=e=>{speed=speed===1?2:(speed===2?0.5:1);e.target.textContent=speed+'x';};
cv.onpointerdown=e=>{drag=[e.clientX,e.clientY];cv.setPointerCapture(e.pointerId)};cv.onpointerup=()=>drag=null;
cv.onpointermove=e=>{if(!drag)return;yaw+=(e.clientX-drag[0])*0.008;pitch=Math.max(-1.4,Math.min(1.4,pitch+(e.clientY-drag[1])*0.008));drag=[e.clientX,e.clientY]};
let ctr=null;
function draw(){const d=DATA[kind];if(fi>=d.shape[0])fi=0;const A=dec(kind,'',Int16Array),FL=dec(kind,'flags',Int8Array);const [F,N,C]=d.shape;const n=d.n[fi];const o=fi*N*C,of=fi*N*3;
 let cx=0,cy=0,cz=0;for(let i=0;i<n;i++){cx+=A[o+i*C];cy+=A[o+i*C+1];cz+=A[o+i*C+2];}
 if(n){cx/=n*SC[0];cy/=n*SC[0];cz/=n*SC[0];}if(!ctr)ctr=[cx,cy,cz];ctr=ctr.map((v,i)=>v+0.08*([cx,cy,cz][i]-v));
 const W=cv.width,H=cv.height;const tell=d.tell[fi]||0;
 g.fillStyle='#060914';g.fillRect(0,0,W,H);
 if(tell>0){g.fillStyle=`rgba(127,209,193,${0.10*tell})`;g.fillRect(0,0,W,H);}
 const cyw=Math.cos(yaw),syw=Math.sin(yaw),cp=Math.cos(pitch),spp=Math.sin(pitch);
 const proj=(x,y,z)=>{x-=ctr[0];y-=ctr[1];z-=ctr[2];const X=cyw*x+syw*z,Z=-syw*x+cyw*z;const Y=cp*y-spp*Z,Z2=spp*y+cp*Z;const s=1700/(95+Z2);return [W/2+X*s,H/2-Y*s,s,Z2];};
 const items=[];
 for(let i=0;i<n;i++){const b=o+i*C;const x=A[b]/SC[0],y=A[b+1]/SC[0],z=A[b+2]/SC[0];const fx=A[b+6]/1000,fy=A[b+7]/1000,fz=A[b+8]/1000,sl_=1;
  const p=proj(x,y,z),q=proj(x-fx*2.4*sl_,y-fy*2.4*sl_,z-fz*2.4*sl_);items.push([p[3],0,p,q,A[b+3],A[b+4],A[b+5],FL[of+i*3]/100,FL[of+i*3+1],FL[of+i*3+2]]);}
 for(const c of (d.crystal_track[fi]||[])){const p=proj(c[0],c[1],c[2]);items.push([p[3],1,p]);}
 items.sort((a,b)=>b[0]-a[0]);
 for(const it of items){const p=it[2];
  if(it[1]===1){const r=Math.max(2,0.5*p[2]);g.globalAlpha=0.95;g.fillStyle='#b8f04a';g.beginPath();g.moveTo(p[0],p[1]-r*1.3);g.lineTo(p[0]+r*0.8,p[1]);g.lineTo(p[0],p[1]+r*1.3);g.lineTo(p[0]-r*0.8,p[1]);g.closePath();g.fill();continue;}
  const [,,,q,e,dm,tier,st,dg,mb]=it;const r=Math.max(1.3,0.62*p[2]);
  g.globalAlpha=0.55;g.strokeStyle=EL[e];g.lineWidth=Math.max(1,r*0.35);g.beginPath();g.moveTo(p[0],p[1]);g.lineTo(q[0],q[1]);g.stroke();
  g.globalAlpha=1;g.fillStyle=dg?'#ff4d3d':EL[e];g.beginPath();g.arc(p[0],p[1],r,0,7);g.fill();
  if(dg){g.strokeStyle='#ff4d3d';g.lineWidth=1.5;for(let a=0;a<6;a++){const an=a*1.047+fi*0.2;g.beginPath();g.moveTo(p[0]+Math.cos(an)*r,p[1]+Math.sin(an)*r);g.lineTo(p[0]+Math.cos(an)*r*2.1,p[1]+Math.sin(an)*r*2.1);g.stroke();}}
  g.strokeStyle=DOM[dm]||'#999';g.lineWidth=Math.max(1,r*0.28);g.beginPath();g.arc(p[0],p[1],r*1.28,0,7);g.stroke();
  if(mb){g.strokeStyle='#ffffffcc';g.lineWidth=1;g.beginPath();g.arc(p[0],p[1],r*1.8,0,7);g.stroke();}
  if(st>0.12){g.globalAlpha=Math.min(1,st)*0.85;g.fillStyle='#fff';g.beginPath();g.arc(p[0],p[1],r*0.6,0,7);g.fill();}}
 g.globalAlpha=1;const v=d.vessel[fi];
 if(v){trail.push(v.slice(0,3));if(trail.length>30)trail.shift();}else if(trail.length)trail.shift();
 if(trail.length>1){g.strokeStyle='#ffffff55';g.lineWidth=3;g.beginPath();trail.forEach((t,i)=>{const p=proj(t[0],t[1],t[2]);i?g.lineTo(p[0],p[1]):g.moveTo(p[0],p[1]);});g.stroke();}
 if(v){const p=proj(v[0],v[1],v[2]);const gr=g.createRadialGradient(p[0],p[1],0,p[0],p[1],v[3]*p[2]);gr.addColorStop(0,'#ffffffcc');gr.addColorStop(1,'#ffffff10');g.fillStyle=gr;g.beginPath();g.arc(p[0],p[1],v[3]*p[2],0,7);g.fill();}
 const step=fi*d.every;let ev='';let on=-1;d.events.forEach(([s,t],i)=>{if(s<=step){ev=t;on=i;}});document.getElementById('ev').textContent=ev;
 [...evl.children].forEach((li,i)=>li.className=i===on?'on':'');
 document.getElementById('tt').textContent=`step ${step}  tadpoles ${n}`;sl.value=fi;}
let last=0,acc=0;function loop(ts){const dt=ts-last;last=ts;if(playing){acc+=dt*speed;while(acc>50){fi=(fi+1)%DATA[kind].shape[0];acc-=50;if(fi===0)trail=[];}}if(!drag&&playing)yaw+=0.0015;draw();requestAnimationFrame(loop);}requestAnimationFrame(loop);
</script></body></html>
