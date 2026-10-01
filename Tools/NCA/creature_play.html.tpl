<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Swarm Creature Playground</title>
<style>
:root{--bg:#f4f5f9;--fg:#1a2033;--mut:#5a6480;--acc:#0f8a76;--panel:#ffffff;--line:#d5dae6}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--bg:#0b1020;--fg:#dfe6f5;--mut:#8a96b3;--acc:#7fd1c1;--panel:#121a30;--line:#2a3658}}
:root[data-theme="dark"]{--bg:#0b1020;--fg:#dfe6f5;--mut:#8a96b3;--acc:#7fd1c1;--panel:#121a30;--line:#2a3658}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.45 system-ui,-apple-system,Segoe UI,sans-serif}
main{max-width:1100px;margin:0 auto;padding:16px}
h1{font-size:20px;margin:4px 0}.cap{color:var(--mut);margin:4px 0 10px;max-width:80ch}
.row{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin:8px 0}
button{background:var(--panel);color:var(--fg);border:1px solid var(--line);border-radius:6px;padding:6px 10px;cursor:pointer;font:inherit}
button[aria-pressed=true]{border-color:var(--acc);color:var(--acc)}
.stage{position:relative}
canvas{width:100%;aspect-ratio:1.6;background:#060914;border-radius:10px;display:block;touch-action:none;cursor:crosshair}
#hud{position:absolute;left:12px;top:10px;color:#dfe6f5;font:13px/1.4 ui-monospace,Menlo,Consolas,monospace;pointer-events:none;text-shadow:0 1px 2px #000}
#msg{position:absolute;left:0;right:0;bottom:14px;text-align:center;color:#7fd1c1;font-weight:600;pointer-events:none;text-shadow:0 1px 3px #000}
.keys{color:var(--mut);font-size:13px}kbd{border:1px solid var(--line);border-radius:4px;padding:0 5px;font:12px ui-monospace,monospace;background:var(--panel)}
</style></head><body><main>
<h1>Swarm creature playground</h1>
<p class="cap">Live simulation, nothing scripted: field's slot body with the creature's reaction shell, running in your browser. Fly your vessel through it, ram it, hover beside it, or feed on its majority element until it turns into another animal.</p>
<div class="row" id="kinds"></div>
<div class="stage"><canvas id="cv" width="1440" height="900" aria-label="Swarm creature playground"></canvas><div id="hud"></div><div id="msg"></div></div>
<div class="row keys"><span><b>Mouse</b> steers the vessel (it flies to the pointer, in the plane through the creature)</span>
<span><b>Hold left button</b> = RAM (kills what you hit)</span><span><kbd>E</kbd> hold = FEED on the element the animal is made of (its majority)</span>
<span><b>Wheel</b> = nearer / further plane</span><span><b>Right-drag</b> or <kbd>&larr;</kbd><kbd>&rarr;</kbd> = orbit</span><span><kbd>R</kbd> reset</span></div>
<div class="row"><button id="fear" aria-pressed="true">Fear suppresses breeding: on</button><button id="shellb" aria-pressed="true">Reaction shell: on</button><button id="pause" aria-pressed="false">Pause</button></div>
</main><script>
const D=/*DATA*/;
const MAJOR=[1,2,0,3], KINDS=['mass','space','charge','time'], NAMES=['Whale','Jellyfish','Pufferfish','Dragonfly'], ELN=['Charge','Mass','Space','Time'];
const EL=['#e8a93a','#8e6bd8','#3a7bdc','#2fb39a'], DOM=['#1fbfa6','#d04a6a','#d8b040'];
const CAP=280;
// ---------------------------------------------------------------- plans
class Plan{constructor(p){Object.assign(this,p);this.P=p.P.map(a=>Float32Array.from(a));this.fs=6;}
 at(t,sp,sv){const u=t/this.fs,L=this.order.length,fl=Math.floor(u),i=fl%L,j=(i+1)%L,a=u-fl,pi=this.P[this.order[i]],pj=this.P[this.order[j]];
  for(let k=0;k<3*this.n;k++){sp[k]=pi[k]+(pj[k]-pi[k])*a;sv[k]=(pj[k]-pi[k])/this.fs;}}}
const PLANS=D.plans.map(p=>new Plan(p));
const FC={dwell:12,kArrive:.35,accel:.45,sepR:2,sepK:.5,alignR:5,alignK:.15,vmax:[.8,.8,.8,2],reassign:8,sticky:4,layRate:.04,layMax:4,rBud:2.6,
 moltRate:.03,moltSteps:10,morph:60,swirl:.9,sense:2.2,relay:.8,decay:.9,look:10,fleeSwirl:.8,flee:[.6,.5,1.4,2],membrane:80};
const SC={sense:3,look:10,relay:.75,relayR:4.5,decay:.9,flee:[.5,.3,2,2],fleeSwirl:.6,kRet:.1,maxOff:30,jetPeriod:6,jetGain:3,bell:.35,bodyJet:.6,
 inflate:.55,dangerAt:.35,tuck:.35,shield:.25,dart:.8,mob:[0,0,0,1],mobSpeed:1,mobR:1.4,escort:[0,.8,0,.45],escMin:1,escMax:2.4,escR:4,
 tellSteps:36,tellJitter:.7,tellSwirl:.25,preGap:.08,minBody:32,fear:4};
const v3=()=>[0,0,0];
function gauss(){let u=1-Math.random(),v=Math.random();return Math.sqrt(-2*Math.log(u))*Math.cos(6.2832*v);}
// ---------------------------------------------------------------- the creature
class Creature{
 constructor(k){const s=D.seeds[k];this.pos=new Float32Array(CAP*3);this.vel=new Float32Array(CAP*3);this.elem=new Int8Array(CAP);this.dom=new Int8Array(CAP);
  this.home=new Int16Array(CAP).fill(-1);this.moltTo=new Int8Array(CAP);this.molt=new Float32Array(CAP);this.fst=new Float32Array(CAP);this.alive=new Uint8Array(CAP);this.age=new Float32Array(CAP);
  this.off=new Float32Array(CAP*3);this.st=new Float32Array(CAP);this.danger=new Uint8Array(CAP);this.mobbing=new Uint8Array(CAP);this.react=new Float32Array(CAP*3);
  for(let i=0;i<s.elem.length;i++){this.alive[i]=1;for(let d=0;d<3;d++)this.pos[3*i+d]=s.pos[i][d];this.elem[i]=s.elem[i];this.dom[i]=s.dom[i];this.age[i]=30;}
  const c=this.counts(false);this.planIx=MAJOR.indexOf(argmax(c));this.cand=this.planIx;this.candN=0;this.morphFrom=-1;this.morphT=0;this.lastAssign=-1e9;this.clock=0;this.T=0;
  this.anchor=this.centroid();this.perm=[0,1,2];this.permSet=false;this.fThreat=0;
  this.threat=0;this.swell=0;this.tellLeft=0;this.maj=-1;this.t=0;this.tellAmp=0;this.sp=new Float32Array(3*192);this.sv=new Float32Array(3*192);this.fear=true;this.shellOn=true;}
 centroid(){let c=[0,0,0],n=0;for(let i=0;i<CAP;i++)if(this.alive[i]){for(let d=0;d<3;d++)c[d]+=this.pos[3*i+d];n++;}return c.map(x=>x/Math.max(n,1));}
 counts(eff){const c=[0,0,0,0];for(let i=0;i<CAP;i++)if(this.alive[i])c[eff&&this.molt[i]>0?this.moltTo[i]:this.elem[i]]++;return c;}
 kill(i){this.alive[i]=0;this.home[i]=-1;this.st[i]=0;for(let d=0;d<3;d++)this.off[3*i+d]=0;}
 afraid(){return this.fear&&this.shellOn&&Math.random()<Math.min(1,SC.fear*this.threat);}
 step(vessels){this.bodyStep(this.shellOn?vessels:[]);if(this.shellOn)this.shellStep(vessels);this.clock++;}
 // ----- field body (FieldSwarmCore.cs)
 bodyStep(preds){let plan=PLANS[this.planIx];const counts=this.counts(true);const cur=MAJOR[this.planIx],top=argmax(counts),maj=counts[cur]===counts[top]?cur:top;let contested=false;
  if(maj!==cur){const cp=MAJOR.indexOf(maj);if(this.cand===cp)this.candN++;else{this.cand=cp;this.candN=1;}
   if(this.candN>=FC.dwell){this.morphFrom=this.planIx;this.planIx=cp;this.morphT=0;this.lastAssign=-1e9;this.permSet=false;this.molt.fill(0);plan=PLANS[this.planIx];flash(`it becomes the ${NAMES[cp].toLowerCase()}`);}else contested=true;}
  else{this.cand=this.planIx;this.candN=0;}
  if(!this.permSet){this.setPerm(plan);this.permSet=true;}
  this.T+=1;plan.at(this.T,this.sp,this.sv);const sp=this.sp,sv=this.sv;
  const swell=1+(this.planIx===2?.45:0)*this.fThreat;
  if(!contested){if(!this.afraid())this.moltStep(plan,counts);if(!this.afraid())this.lay(plan);}
  for(let i=0;i<CAP;i++)if(this.alive[i]&&this.molt[i]>0){this.molt[i]+=1/FC.moltSteps;if(this.molt[i]>=1){this.elem[i]=this.moltTo[i];this.molt[i]=0;this.lastAssign=-1e9;}}
  if(this.clock-this.lastAssign>=FC.reassign){this.assign(plan);this.lastAssign=this.clock;}
  const u=this.morphFrom>=0?this.morphT/FC.morph:1;if(this.morphFrom>=0&&u>=1)this.morphFrom=-1;const amp=this.morphFrom>=0?FC.swirl*Math.sin(Math.PI*u):0;if(this.morphFrom>=0)this.morphT++;
  let spm=[0,0,0];for(let k=0;k<plan.n;k++)for(let d=0;d<3;d++)spm[d]+=sp[3*k+d];spm=spm.map(x=>x/plan.n);
  const idx=[];for(let i=0;i<CAP;i++)if(this.alive[i])idx.push(i);
  const nv=new Float32Array(CAP*3),ns=new Float32Array(CAP);let stSum=0;const A=this.anchor,P=this.pos,V=this.vel;
  for(const i of idx){const x=[P[3*i],P[3*i+1],P[3*i+2]],v=[V[3*i],V[3*i+1],V[3*i+2]];let des=[0,0,0];
   const h=this.home[i];if(h>=0)for(let d=0;d<3;d++)des[d]=sv[3*h+d]+FC.kArrive*(sp[3*h+d]*swell+A[d]-x[d]);
   if(amp>0){const r=[x[0]-A[0],x[1]-A[1],x[2]-A[2]];const tg=[r[2],0,-r[0]];const tl=Math.hypot(tg[0],tg[2]);if(tl>1e-3){const rl=Math.sqrt(Math.hypot(r[0],r[1],r[2]));for(let d=0;d<3;d++)des[d]+=amp*tg[d]/tl*rl*.3;}}
   let sep=[0,0,0],vs=[0,0,0],cnt=0,st=this.fst[i]*FC.decay,relay=0;
   for(const j of idx){if(j===i)continue;const dx=x[0]-P[3*j],dy=x[1]-P[3*j+1],dz=x[2]-P[3*j+2];const dist=Math.hypot(dx,dy,dz);if(dist>=FC.alignR)continue;
    if(dist<FC.sepR){const f=(FC.sepR-dist)/Math.max(dist,1e-3);sep[0]+=dx*f;sep[1]+=dy*f;sep[2]+=dz*f;}vs[0]+=V[3*j];vs[1]+=V[3*j+1];vs[2]+=V[3*j+2];cnt++;if(this.fst[j]>relay)relay=this.fst[j];}
   const al=cnt?[vs[0]/cnt-v[0],vs[1]/cnt-v[1],vs[2]/cnt-v[2]]:[0,0,0];let flee=[0,0,0];
   for(const p of preds){const rel=[x[0]-p.c[0],x[1]-p.c[1],x[2]-p.c[2]];const dd=Math.hypot(...rel),sense=FC.sense*p.r,spd=Math.max(Math.hypot(...p.v),1e-6);const pvn=p.v.map(a=>a/spd);
    const along=dot(rel,pvn);const lat=rel.map((a,d)=>a-along*pvn[d]);const dl=Math.hypot(...lat);const latn=lat.map(a=>a/Math.max(dl,1e-3));
    const ahead=along>-p.r?clamp01(1-along/(FC.look*spd+p.r)):0;const w=Math.max(clamp01(1-dl/sense)*ahead,clamp01(1-dd/sense));st=Math.max(st,clamp01(1.4*w));
    const rad=rel.map(a=>a/Math.max(dd,1e-3));const sw=cross(pvn,latn);for(let d=0;d<3;d++)flee[d]+=w*FC.flee[this.elem[i]]*(.75*latn[d]+.25*rad[d]+FC.fleeSwirl*.5*sw[d]);}
   st=Math.max(st,relay*FC.relay);const calm=1-.8*st;
   for(let d=0;d<3;d++){const steer=calm*des[d]+FC.sepK*sep[d]+FC.alignK*al[d]+2*flee[d];v[d]=(1-FC.accel)*v[d]+FC.accel*steer;}
   const vm=FC.vmax[this.elem[i]]*(1+.8*st),s=Math.hypot(...v);if(s>vm)for(let d=0;d<3;d++)v[d]*=vm/s;
   for(let d=0;d<3;d++)nv[3*i+d]=v[d];ns[i]=st;stSum+=st;}
  let mean=[0,0,0];for(const i of idx){for(let d=0;d<3;d++){V[3*i+d]=nv[3*i+d];P[3*i+d]+=V[3*i+d];mean[d]+=P[3*i+d];}this.fst[i]=ns[i];
   const r=Math.hypot(P[3*i],P[3*i+1],P[3*i+2]);if(r>FC.membrane)for(let d=0;d<3;d++)P[3*i+d]*=FC.membrane/r;this.age[i]+=1;}
  const n=idx.length;if(n)for(let d=0;d<3;d++)this.anchor[d]=.9*this.anchor[d]+.1*(mean[d]/n-spm[d]);
  this.fThreat=.85*this.fThreat+.15*Math.min(1,3*(n?stSum/n:0));}
 setPerm(plan){const dc=[0,0,0];for(let i=0;i<CAP;i++)if(this.alive[i])dc[this.dom[i]]++;
  const perms=plan.nslots===1?[[0]]:plan.nslots===2?[[0,1],[1,0]]:[[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]];let best=1e9;
  for(const p of perms){let c=0;for(let s=0;s<plan.nslots;s++)c+=Math.abs(dc[p[s]]-plan.slotMix[s]);if(c<best){best=c;this.perm=[p[0],p.length>1?p[1]:1,p.length>2?p[2]:2];}}}
 cost(i,k,plan){const sp=this.sp,A=this.anchor,P=this.pos;const dx=P[3*i]-sp[3*k]-A[0],dy=P[3*i+1]-sp[3*k+1]-A[1],dz=P[3*i+2]-sp[3*k+2]-A[2];
  const d2=(dx*dx+dy*dy+dz*dz)/8;let c=d2<16?d2:8*Math.sqrt(d2)-16;if(this.elem[i]!==plan.elem[k])c+=60;if(this.dom[i]!==this.perm[plan.slot[k]])c+=25;if(this.home[i]===k)c-=FC.sticky;return c;}
 assign(plan){const al=[];for(let i=0;i<CAP;i++)if(this.alive[i])al.push(i);const A=al.length,M=plan.n;const keys=new Float32Array(A*M),ids=new Uint32Array(A*M);
  for(let a=0;a<A;a++)for(let k=0;k<M;k++){keys[a*M+k]=this.cost(al[a],k,plan);ids[a*M+k]=a*M+k;}
  const ord=Array.from(ids).sort((p,q)=>keys[p]-keys[q]);const ru=new Uint8Array(A),cu=new Uint8Array(M),nh=new Int16Array(A).fill(-1);let got=0;const need=Math.min(A,M);
  for(const q of ord){if(got>=need)break;const a=Math.floor(q/M),k=q%M;if(ru[a]||cu[k])continue;ru[a]=cu[k]=1;nh[a]=k;got++;}
  for(let a=0;a<A;a++){if(nh[a]<0){let b=1e9;for(let k=0;k<M;k++){const c=this.cost(al[a],k,plan);if(c<b){b=c;nh[a]=k;}}}this.home[al[a]]=nh[a];}}
 lay(plan){let n=0;for(let i=0;i<CAP;i++)if(this.alive[i])n++;if(n>=plan.n)return;const k=Math.min(FC.layMax,plan.n-n,Math.max(1,Math.ceil(FC.layRate*n)));
  const want=[...Array(4)].map(()=>[0,0,0]),have=[...Array(4)].map(()=>[0,0,0]);for(let s=0;s<plan.n;s++)want[plan.elem[s]][this.perm[plan.slot[s]]]++;
  for(let i=0;i<CAP;i++)if(this.alive[i])have[this.molt[i]>0?this.moltTo[i]:this.elem[i]][this.dom[i]]++;
  const taken=new Set();for(let i=0;i<CAP;i++)if(this.alive[i]&&this.home[i]>=0)taken.add(this.home[i]);const sp=this.sp,A=this.anchor,P=this.pos;
  for(let egg=0;egg<k;egg++){let be=-1,bd=-1,bdef=0;for(let e=0;e<4;e++)for(let d=0;d<3;d++){const def=want[e][d]-have[e][d];if(def<=bdef)continue;let par=false;
    for(let i=0;i<CAP&&!par;i++)par=this.alive[i]&&this.elem[i]===e&&this.dom[i]===d&&this.molt[i]===0;if(par){be=e;bd=d;bdef=def;}}
   if(be<0)return;const free=this.alive.indexOf(0);if(free<0)return;let bp=-1,bs=-1,bdist=1e18;
   for(let s=0;s<plan.n;s++){if(plan.elem[s]!==be||this.perm[plan.slot[s]]!==bd||taken.has(s))continue;const w=[sp[3*s]+A[0],sp[3*s+1]+A[1],sp[3*s+2]+A[2]];
    for(let i=0;i<CAP;i++)if(this.alive[i]&&this.elem[i]===be&&this.dom[i]===bd&&this.molt[i]===0){const d2=(P[3*i]-w[0])**2+(P[3*i+1]-w[1])**2+(P[3*i+2]-w[2])**2;if(d2<bdist){bdist=d2;bp=i;bs=s;}}}
   if(bp<0){for(let i=0;i<CAP;i++)if(this.alive[i]&&this.elem[i]===be&&this.dom[i]===bd){bp=i;break;}}
   let dir=bs>=0?[sp[3*bs]+A[0]-P[3*bp],sp[3*bs+1]+A[1]-P[3*bp+1],sp[3*bs+2]+A[2]-P[3*bp+2]]:[Math.random()-.5,Math.random()-.5,Math.random()-.5];const dl=Math.max(Math.hypot(...dir),1e-6);
   this.alive[free]=1;for(let d=0;d<3;d++){P[3*free+d]=P[3*bp+d]+FC.rBud*dir[d]/dl;this.vel[3*free+d]=this.vel[3*bp+d];this.off[3*free+d]=0;}this.elem[free]=be;this.dom[free]=bd;
   this.home[free]=bs;this.molt[free]=0;this.fst[free]=0;this.st[free]=0;this.age[free]=0;have[be][bd]++;if(bs>=0)taken.add(bs);this.lastAssign=-1e9;}}
 moltStep(plan,counts){const n=counts.reduce((a,b)=>a+b,0),tot=plan.mix.reduce((a,b)=>a+b,0);const sur=[0,1,2,3].map(e=>counts[e]-plan.mix[e]/tot*Math.max(n,1));
  const k=Math.max(1,Math.ceil(FC.moltRate*n));const sp=this.sp,A=this.anchor,P=this.pos;
  for(let q=0;q<k;q++){let ef=0,et=0;for(let e=1;e<4;e++){if(sur[e]>sur[ef])ef=e;if(sur[e]<sur[et])et=e;}if(sur[ef]<1||sur[et]>-1)return;let best=-1,bd=1e18;
   for(let i=0;i<CAP;i++){if(!this.alive[i]||this.elem[i]!==ef||this.molt[i]>0)continue;for(let s=0;s<plan.n;s++)if(plan.elem[s]===et){const d2=(P[3*i]-sp[3*s]-A[0])**2+(P[3*i+1]-sp[3*s+1]-A[1])**2+(P[3*i+2]-sp[3*s+2]-A[2])**2;if(d2<bd){bd=d2;best=i;}}}
   if(best<0)return;this.molt[best]=1e-3;this.moltTo[best]=et;sur[ef]--;sur[et]++;}}
 // ----- the creature shell (CreatureShell.cs)
 shellStep(vessels){const P=this.pos,R=this.react;R.fill(0);this.danger.fill(0);this.mobbing.fill(0);
  let cnt=0,cen=[0,0,0];const counts=[0,0,0,0];for(let i=0;i<CAP;i++)if(this.alive[i]){cnt++;for(let d=0;d<3;d++)cen[d]+=P[3*i+d];counts[this.elem[i]]++;}
  cnt=Math.max(cnt,1);cen=cen.map(x=>x/cnt);const maj=argmax(counts);for(let i=0;i<CAP;i++)this.st[i]*=SC.decay;
  const jetOn=(this.t%SC.jetPeriod)<2?1:0,thr=this.threat;let drift=[0,0,0];
  for(const vs of vessels){const sense=SC.sense*vs.r,spd=Math.hypot(...vs.v),pvn=vs.v.map(a=>a/Math.max(spd,1e-6));const ts=[vs.c[0]-cen[0],vs.c[1]-cen[1],vs.c[2]-cen[2]];
   const tl=Math.max(Math.hypot(...ts),1e-3),tsn=ts.map(a=>a/tl);
   for(let i=0;i<CAP;i++){if(!this.alive[i])continue;const e=this.elem[i];const rel=[P[3*i]-cen[0],P[3*i+1]-cen[1],P[3*i+2]-cen[2]];const rs=[P[3*i]-vs.c[0],P[3*i+1]-vs.c[1],P[3*i+2]-vs.c[2]];
    if(maj===1){if(e!==1)for(let d=0;d<3;d++)R[3*i+d]+=thr*(-SC.tuck*.1*rel[d]);else{const pr=Math.max(0,dot(rel,tsn));for(let d=0;d<3;d++)R[3*i+d]+=thr*SC.shield*.05*tsn[d]*pr;}}
    const dd=Math.max(Math.hypot(...rs),1e-3),along=dot(rs,pvn);const lat=rs.map((a,d)=>a-along*pvn[d]);const dl=Math.max(Math.hypot(...lat),1e-3);let latn=lat.map(a=>a/dl);
    const ahead=clamp01(1-along/(SC.look*spd+vs.r))*(along>-vs.r?1:0);const w=Math.max(clamp01(1-dl/sense)*ahead,clamp01(1-dd/sense));this.st[i]=Math.max(this.st[i],clamp01(1.4*w));
    const rad=rs.map(a=>a/dd);if(spd<1e-3)latn=rad;const sw=cross(pvn,latn);let gain=SC.flee[e];if(e===2)gain*=SC.jetGain*jetOn+.15;
    const fl=[0,1,2].map(d=>w*gain*(.75*latn[d]+.25*rad[d]+SC.fleeSwirl*.5*sw[d]));
    if(e===3){const zig=Math.sin(this.t*1.3+i*2.1),pp=cross(latn,rad);for(let d=0;d<3;d++)fl[d]+=w*SC.dart*zig*pp[d];}
    if(spd<SC.mobSpeed){const mk=SC.mob[e],wm=clamp01(1-dd/(2.5*sense))*mk;let tg=cross([0,1,0],rad);const tn=Math.max(Math.hypot(...tg),1e-3);
     for(let d=0;d<3;d++){R[3*i+d]+=wm*(.4*(SC.mobR*vs.r-dd)*rad[d]+1.5*tg[d]/tn);fl[d]*=(1-mk);}if(wm>.05)this.mobbing[i]=1;}
    for(let d=0;d<3;d++)R[3*i+d]+=fl[d];}
   if(spd>=SC.escMin&&spd<=SC.escMax){let r2=0;for(let i=0;i<CAP;i++)if(this.alive[i])r2+=(P[3*i]-cen[0])**2+(P[3*i+1]-cen[1])**2+(P[3*i+2]-cen[2])**2;
    const rms=Math.sqrt(r2/cnt),hin=Math.max(0,-dot(tsn,pvn));const eg=SC.escort[maj]*clamp01(1-tl/(SC.escR*rms))*(hin<.3?1:0);
    if(eg){for(let i=0;i<CAP;i++)if(this.alive[i])for(let d=0;d<3;d++)P[3*i+d]+=eg*vs.v[d];for(let d=0;d<3;d++){drift[d]+=eg*vs.v[d];this.anchor[d]+=eg*vs.v[d];}}}
   if(maj===2)for(let i=0;i<CAP;i++){if(!this.alive[i])continue;for(let d=0;d<3;d++){const rel=P[3*i+d]-drift[d]-cen[d];R[3*i+d]+=thr*(-tsn[d]*SC.bodyJet*jetOn-SC.bell*.1*rel*jetOn);}}}
  if(this.st.some(x=>x>.02)){const ns=new Float32Array(CAP),r2=SC.relayR**2;
   for(let i=0;i<CAP;i++){if(!this.alive[i])continue;let b=0;for(let j=0;j<CAP;j++){if(!this.alive[j]||this.st[j]<=b)continue;if((P[3*i]-P[3*j])**2+(P[3*i+1]-P[3*j+1])**2+(P[3*i+2]-P[3*j+2])**2<r2)b=this.st[j];}ns[i]=Math.max(this.st[i],b*SC.relay);}
   for(let i=0;i<CAP;i++)this.st[i]=this.alive[i]?ns[i]:0;}
  let sm=0;for(let i=0;i<CAP;i++)if(this.alive[i])sm+=this.st[i];sm/=cnt;this.threat=.85*this.threat+.15*Math.min(3*sm,1);
  const swT=maj===0?SC.inflate*this.threat:0,dsw=swT-this.swell;this.swell=swT;
  for(let i=0;i<CAP;i++)if(this.alive[i])for(let d=0;d<3;d++)R[3*i+d]+=dsw*(P[3*i+d]-drift[d]-cen[d]);
  const changed=this.maj>=0&&maj!==this.maj&&cnt>=SC.minBody;this.tellLeft=changed?SC.tellSteps:Math.max(this.tellLeft-1,0);this.maj=maj;
  const sh=counts.map(c=>c/cnt).sort((a,b)=>b-a),gap=sh[0]-sh[1],pre=clamp01((SC.preGap-gap)/SC.preGap)*(cnt>=SC.minBody?1:0);const amp=Math.max(this.tellLeft/SC.tellSteps,pre);this.tellAmp=amp;
  if(amp>0)for(let i=0;i<CAP;i++){if(!this.alive[i])continue;const rel=[0,1,2].map(d=>P[3*i+d]-drift[d]-cen[d]);const tg=[rel[2],0,-rel[0]];
   for(let d=0;d<3;d++)R[3*i+d]+=amp*(gauss()*SC.tellJitter+SC.tellSwirl*.1*tg[d]);}
  for(let i=0;i<CAP;i++){if(!this.alive[i]){for(let d=0;d<3;d++)this.off[3*i+d]=0;continue;}const calm=1-clamp01(this.st[i]);const nw=[0,1,2].map(d=>this.off[3*i+d]*(1-SC.kRet*calm)+R[3*i+d]);
   const nn=Math.hypot(...nw);if(nn>SC.maxOff)for(let d=0;d<3;d++)nw[d]*=SC.maxOff/nn;for(let d=0;d<3;d++){P[3*i+d]+=nw[d]-this.off[3*i+d];this.off[3*i+d]=nw[d];}
   this.danger[i]=this.elem[i]===0&&this.st[i]>SC.dangerAt?1:0;}
  this.t++;}}
function argmax(a){let b=0;for(let i=1;i<a.length;i++)if(a[i]>a[b])b=i;return b;}
function clamp01(x){return x<0?0:x>1?1:x;}function dot(a,b){return a[0]*b[0]+a[1]*b[1]+a[2]*b[2];}
function cross(a,b){return[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];}
// ---------------------------------------------------------------- game
let kind=0,cr=new Creature(0),ship={p:[0,0,-60],v:[0,0,0],r:6},crystals=[],collected=0,eaten=0,rammed=0;
let mouse=null,ram=false,feed=false,depth=0,yaw=.5,pitch=.3,ctr=[0,0,0],drag=null,paused=false,msgT=0;
const cv=document.getElementById('cv'),g=cv.getContext('2d');
function flash(s){document.getElementById('msg').textContent=s;msgT=60;}
function reset(k){kind=k;cr=new Creature(k);crystals=[];collected=eaten=rammed=0;ship={p:[0,0,-60],v:[0,0,0],r:6};ctr=cr.centroid();for(const b of kd.children)b.setAttribute('aria-pressed',+b.dataset.k===k);
 flash(`a ${NAMES[k].toLowerCase()} seed: 16 tadpoles - watch it grow`);}
const kd=document.getElementById('kinds');NAMES.forEach((n,k)=>{const b=document.createElement('button');b.textContent=`${n} (${ELN[MAJOR[k]]})`;b.dataset.k=k;b.onclick=()=>reset(k);kd.appendChild(b);});
const proj=(x,y,z)=>{x-=ctr[0];y-=ctr[1];z-=ctr[2];const cy=Math.cos(yaw),sy=Math.sin(yaw),cp=Math.cos(pitch),sp=Math.sin(pitch);
 const X=cy*x+sy*z,Z=-sy*x+cy*z,Y=cp*y-sp*Z,Z2=sp*y+cp*Z;const s=1600/(90+Z2);return[cv.width/2+X*s,cv.height/2-Y*s,s,Z2];};
function unproj(sx,sy){const s=1600/(90+depth);const X=(sx-cv.width/2)/s,Y=-(sy-cv.height/2)/s,Z2=depth;const cy=Math.cos(yaw),sy_=Math.sin(yaw),cp=Math.cos(pitch),sp=Math.sin(pitch);
 const y=cp*Y+sp*Z2,Z=-sp*Y+cp*Z2;return[cy*X-sy_*Z+ctr[0],y+ctr[1],sy_*X+cy*Z+ctr[2]];}
cv.addEventListener('contextmenu',e=>e.preventDefault());
cv.onpointermove=e=>{const r=cv.getBoundingClientRect();mouse=[(e.clientX-r.left)*cv.width/r.width,(e.clientY-r.top)*cv.height/r.height];
 if(drag){yaw+=(e.clientX-drag[0])*.008;pitch=Math.max(-1.3,Math.min(1.3,pitch+(e.clientY-drag[1])*.008));drag=[e.clientX,e.clientY];}};
cv.onpointerdown=e=>{if(e.button===2){drag=[e.clientX,e.clientY];cv.setPointerCapture(e.pointerId);}else ram=true;};
cv.onpointerup=e=>{if(e.button===2)drag=null;else ram=false;};cv.onpointerleave=()=>{ram=false;};
cv.onwheel=e=>{e.preventDefault();depth=Math.max(-40,Math.min(40,depth+e.deltaY*.03));};
addEventListener('keydown',e=>{if(e.key==='e'||e.key==='E')feed=true;if(e.key==='r'||e.key==='R')reset(kind);if(e.key==='ArrowLeft')yaw-=.08;if(e.key==='ArrowRight')yaw+=.08;});
addEventListener('keyup',e=>{if(e.key==='e'||e.key==='E')feed=false;});
document.getElementById('fear').onclick=e=>{cr.fear=!cr.fear;e.target.textContent=`Fear suppresses breeding: ${cr.fear?'on':'off'}`;e.target.setAttribute('aria-pressed',cr.fear);};
document.getElementById('shellb').onclick=e=>{cr.shellOn=!cr.shellOn;e.target.textContent=`Reaction shell: ${cr.shellOn?'on':'off'}`;e.target.setAttribute('aria-pressed',cr.shellOn);};
document.getElementById('pause').onclick=e=>{paused=!paused;e.target.textContent=paused?'Resume':'Pause';e.target.setAttribute('aria-pressed',paused);};
function simStep(){
 // ship flies toward the pointer's point in the plane through the creature (max 3.2 voxels/step)
 if(mouse){const tg=unproj(mouse[0],mouse[1]);const d=tg.map((a,i)=>a-ship.p[i]);const dl=Math.hypot(...d);const want=d.map(a=>a/Math.max(dl,1e-6)*Math.min(3.2,dl*.25));
  ship.v=ship.v.map((a,i)=>.7*a+.3*want[i]);ship.p=ship.p.map((a,i)=>a+ship.v[i]);}
 const P=cr.pos;
 if(ram)for(let i=0;i<CAP;i++){if(!cr.alive[i])continue;if((P[3*i]-ship.p[0])**2+(P[3*i+1]-ship.p[1])**2+(P[3*i+2]-ship.p[2])**2<ship.r**2){
  crystals.push({p:[P[3*i],P[3*i+1],P[3*i+2]],v:[(Math.random()-.5)*.6,(Math.random()-.5)*.6,(Math.random()-.5)*.6],e:cr.elem[i]});cr.kill(i);rammed++;}}
 if(feed&&cr.clock%2===0){const maj=MAJOR[cr.planIx];let best=-1,bd=(2.5*ship.r)**2;
  for(let i=0;i<CAP;i++){if(!cr.alive[i]||cr.elem[i]!==maj)continue;const d2=(P[3*i]-ship.p[0])**2+(P[3*i+1]-ship.p[1])**2+(P[3*i+2]-ship.p[2])**2;if(d2<bd){bd=d2;best=i;}}
  if(best>=0){cr.kill(best);eaten++;}}
 cr.step([{c:ship.p,v:ship.v,r:ship.r}]);
 crystals=crystals.filter(k=>{const d=ship.p.map((a,i)=>a-k.p[i]);const dl=Math.hypot(...d);if(dl<ship.r*.9){collected++;return false;}
  k.v=k.v.map(a=>a*.93);if(dl<4*ship.r)k.v=k.v.map((a,i)=>a+d[i]/dl*.35*(1.3-dl/(4*ship.r)));k.p=k.p.map((a,i)=>a+k.v[i]);return true;});}
function draw(){const c=cr.centroid();ctr=ctr.map((a,i)=>a+.06*(c[i]-a));const W=cv.width,H=cv.height;g.fillStyle='#060914';g.fillRect(0,0,W,H);
 if(cr.tellAmp>.02){g.fillStyle=`rgba(127,209,193,${.12*cr.tellAmp})`;g.fillRect(0,0,W,H);}
 const items=[];const P=cr.pos,V=cr.vel;
 for(let i=0;i<CAP;i++){if(!cr.alive[i])continue;const p=proj(P[3*i],P[3*i+1],P[3*i+2]);const vl=Math.max(Math.hypot(V[3*i],V[3*i+1],V[3*i+2]),.05);
  const q=proj(P[3*i]-V[3*i]/vl*2.4,P[3*i+1]-V[3*i+1]/vl*2.4,P[3*i+2]-V[3*i+2]/vl*2.4);items.push([p[3],0,p,q,i]);}
 for(const k of crystals){const p=proj(...k.p);items.push([p[3],1,p]);}
 const sp=proj(...ship.p);items.push([sp[3],2,sp]);items.sort((a,b)=>b[0]-a[0]);
 for(const it of items){const p=it[2];if(p[3]<-85)continue;
  if(it[1]===1){const r=Math.max(2,.5*p[2]);g.globalAlpha=.95;g.fillStyle='#b8f04a';g.beginPath();g.moveTo(p[0],p[1]-r*1.3);g.lineTo(p[0]+r*.8,p[1]);g.lineTo(p[0],p[1]+r*1.3);g.lineTo(p[0]-r*.8,p[1]);g.closePath();g.fill();continue;}
  if(it[1]===2){const R=ship.r*p[2];const gr=g.createRadialGradient(p[0],p[1],0,p[0],p[1],R);gr.addColorStop(0,ram?'#ff9d8acc':feed?'#b8f04acc':'#ffffffcc');gr.addColorStop(1,'#ffffff08');
   g.globalAlpha=1;g.fillStyle=gr;g.beginPath();g.arc(p[0],p[1],R,0,7);g.fill();if(feed){g.strokeStyle='#b8f04a88';g.lineWidth=2;g.beginPath();g.arc(p[0],p[1],R*2.5,0,7);g.stroke();}continue;}
  const i=it[4],q=it[3],e=cr.elem[i],grow=Math.min(1,cr.age[i]/10),r=Math.max(1.3,.62*p[2])*grow,st=cr.st[i],dg=cr.danger[i];
  g.globalAlpha=.55*grow;g.strokeStyle=EL[e];g.lineWidth=Math.max(1,r*.35);g.beginPath();g.moveTo(p[0],p[1]);g.lineTo(q[0],q[1]);g.stroke();
  g.globalAlpha=grow;g.fillStyle=dg?'#ff4d3d':EL[e];g.beginPath();g.arc(p[0],p[1],r,0,7);g.fill();
  if(dg){g.strokeStyle='#ff4d3d';g.lineWidth=1.5;for(let a=0;a<6;a++){const an=a*1.047+cr.t*.2;g.beginPath();g.moveTo(p[0]+Math.cos(an)*r,p[1]+Math.sin(an)*r);g.lineTo(p[0]+Math.cos(an)*r*2.1,p[1]+Math.sin(an)*r*2.1);g.stroke();}}
  g.strokeStyle=DOM[cr.dom[i]];g.lineWidth=Math.max(1,r*.28);g.beginPath();g.arc(p[0],p[1],r*1.28,0,7);g.stroke();
  if(cr.mobbing[i]){g.strokeStyle='#ffffffcc';g.lineWidth=1;g.beginPath();g.arc(p[0],p[1],r*1.8,0,7);g.stroke();}
  if(st>.12||cr.molt[i]>0){g.globalAlpha=Math.min(1,st+(cr.molt[i]>0?.6:0))*.85;g.fillStyle='#fff';g.beginPath();g.arc(p[0],p[1],r*.6,0,7);g.fill();}}
 g.globalAlpha=1;const cnt=cr.counts(false),n=cnt.reduce((a,b)=>a+b,0);
 document.getElementById('hud').innerHTML=`<b>${NAMES[cr.planIx]}</b>  ${n} tadpoles<br>`+cnt.map((c,e)=>`<span style="color:${EL[e]}">${ELN[e].padEnd(6)} ${'#'.repeat(Math.round(30*c/Math.max(n,1))).padEnd(30,'.')} ${c}</span>`).join('<br>')+
  `<br>threat ${'#'.repeat(Math.round(cr.threat*20)).padEnd(20,'.')}  ${cr.tellAmp>.05?'<b style="color:#7fd1c1">shivering</b>':''}<br>rammed ${rammed}  eaten ${eaten}  crystals ${collected}`;
 if(msgT>0&&--msgT===0)document.getElementById('msg').textContent='';}
let last=0,acc=0;function loop(ts){const dt=Math.min(100,ts-last);last=ts;if(!paused){acc+=dt;let k=0;while(acc>40&&k<3){simStep();acc-=40;k++;}if(acc>200)acc=0;}draw();requestAnimationFrame(loop);}
reset(0);requestAnimationFrame(loop);
</script></body></html>
