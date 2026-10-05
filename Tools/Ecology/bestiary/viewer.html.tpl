<!-- Bestiary ledger + hooks, spliced into the shared 3D viewer (common/viewer.py TPL) in place of its <div id="info">.
     The runs are the compact binary format of build_viewer.py; HOOKS.decode turns one into the shared viewer's frames. -->
<style>
#led{position:fixed;right:12px;top:58px;bottom:186px;width:330px;background:var(--panel);border:1px solid var(--line);border-radius:8px;
  padding:12px 14px;z-index:3;overflow-y:auto;display:flex;flex-direction:column;gap:12px;--sonar:#59d6c4;--warn:#ff7a59;--good:#9fe07a}
#ledtog{display:none;position:fixed;right:12px;z-index:4;background:#151a28;color:var(--fg);border:1px solid #2a3150;border-radius:6px;padding:5px 10px;font:inherit}
#led h1{font:700 15px/1.15 system-ui,sans-serif;letter-spacing:.08em;text-transform:uppercase;margin:0}
#led .sub{color:var(--mute);font-size:12px;margin:3px 0 0}
#list{display:grid;grid-template-columns:1fr 1fr;gap:5px}
.sp{background:transparent;border:1px solid var(--line);border-radius:5px;color:var(--fg);text-align:left;padding:5px 8px;cursor:pointer;font:12px system-ui,sans-serif}
.sp b{display:block;font-weight:700;letter-spacing:.05em;text-transform:uppercase;font-size:12px}
.sp span{color:var(--mute);font-size:11px}
.sp[aria-current="true"]{border-color:var(--sonar);background:rgba(89,214,196,.09)}
#led h2{font:700 14px system-ui,sans-serif;letter-spacing:.06em;text-transform:uppercase;margin:0}
.emo{color:var(--sonar);font-size:13px;margin:2px 0 0}
.lbl{font:600 10.5px ui-monospace,Menlo,monospace;letter-spacing:.12em;text-transform:uppercase;color:var(--mute);margin:0 0 4px}
#led table{width:100%;border-collapse:collapse;font:12px ui-monospace,Menlo,monospace;font-variant-numeric:tabular-nums}
#led td{padding:2px 0;border-bottom:1px solid var(--line)} #led td:last-child{text-align:right}
.ok{color:var(--good)} .no{color:var(--warn)}
#doc{white-space:pre-wrap;font:11.5px/1.5 ui-monospace,Menlo,monospace;color:#b6bfdc;margin:0}
.counter{font-size:13px;margin:0}
#hitmsg{position:fixed;left:50%;top:64px;transform:translateX(-50%);font:600 13px ui-monospace,Menlo,monospace;color:#ff7a59;letter-spacing:.1em;text-transform:uppercase;z-index:3;pointer-events:none;text-shadow:0 0 8px #000}
#hitv{position:fixed;inset:0;pointer-events:none;background:radial-gradient(transparent 50%,#ff3a1a 150%);opacity:0;transition:opacity .2s;z-index:2}
@media (max-width:820px){#led{width:280px}}
@media (max-width:560px){#ledtog{display:block;top:auto;bottom:132px}#led{left:12px;right:12px;width:auto;top:auto;bottom:170px;max-height:46vh}#led.shut{display:none}#hitmsg{top:auto;bottom:240px}}
</style>
<div id="hitv"></div><div id="hitmsg" aria-live="polite"></div>
<button id="ledtog">specimen ledger</button>
<aside id="led">
  <div><h1>Hypersea Bestiary</h1><p class="sub">Eight threat fauna on local rules only, each recorded against the pilot that shows it best. Seed 7, 45 s.</p></div>
  <div id="list" role="list"></div>
  <div><h2 id="name"></h2><p class="emo" id="emo"></p></div>
  <div><p class="lbl">Counterplay</p><p class="counter" id="counter"></p></div>
  <div><p class="lbl">Scorecard (4 pilots x 3 seeds, 90 s)</p><table id="card"></table></div>
  <div><p class="lbl">Outside emotion read (Direction C probe)</p><table id="probe"></table></div>
  <div><p class="lbl">Rules</p><p id="doc"></p></div>
</aside>
<div id="info"></div>
<script>
(function(){
function dec(b64,T){const s=atob(b64);const u=new Uint8Array(s.length);for(let i=0;i<s.length;i++)u[i]=s.charCodeAt(i);return new T(u.buffer)}
const fmt=v=>v===null||v===undefined?'n/a':(typeof v==='number'?(Math.round(v*100)/100).toString():v);
const row=(a,b,cls)=>`<tr><td>${a}</td><td class="${cls||''}">${b}</td></tr>`;
function panel(r){
  document.getElementById('name').textContent=r.key+'  (vs '+r.pilot+' pilot)';
  document.getElementById('emo').textContent=r.emotion;
  document.getElementById('counter').textContent=r.counter;
  document.getElementById('doc').textContent=r.doc;
  const c=r.card||{}, v=c.verdict||{}, ok=b=>b?'ok':'no';
  document.getElementById('card').innerHTML=
    row('telegraph, first strike (s)',fmt(c.telegraph_first_s),ok(v.telegraph))+
    row('telegraph, shared median (s)',fmt(c.telegraph_s))+
    row('hits/min: wanderer · evader · skimmer',`${fmt(c.hits_per_min_wander)} · ${fmt(c.hits_per_min_evader)} · ${fmt(c.hits_per_min_skimmer)}`)+
    row('counterplay (evader / wanderer)',fmt(c.counterplay),ok(v.counterplay))+
    row('counterplay, aware same-speed pilot',fmt(c.counterplay_aware))+
    row('payoff: kills/min for a hunter',fmt(c.payoff_per_min),ok(v.payoff))+
    row('variety',fmt(c.variety),ok(v.variety))+
    row('cost: us per agent-step (numpy)',fmt(c.us_per_agent_step));
  const p=r.probe||{}; let h='';
  for(const vw of ['hover','cruise','evade']) if(p[vw]) h+=row(vw+' viewer',`${p[vw].top} ${Math.round(100*Math.max(...Object.values(p[vw].p)))}%`);
  document.getElementById('probe').innerHTML=h||row('not measured','');
}
let hitF=null, hitLast=-1;
window.HOOKS={
  // compact binary run (build_viewer.py) -> the shared viewer's frames: flat typed species blocks, alive bitmask per frame
  decode(r){
    const counts=dec(r.counts,Uint16Array),pos=dec(r.pos,Int16Array),col=dec(r.col,Uint8Array),size=dec(r.size,Uint8Array),pil=dec(r.pilots,Int16Array),
      mpos=dec(r.mpos,Int16Array),melem=dec(r.melem,Uint8Array),mdeath=dec(r.mdeath,Uint16Array),tpos=dec(r.tpos,Int16Array),tborn=dec(r.tborn,Uint16Array),tdeath=dec(r.tdeath,Uint16Array);
    const nm=melem.length,nt=tborn.length,N=nm+nt,NB=Math.ceil(N/8)||1, mass0={pos:[],style:[]};
    for(let j=0;j<nm;j++){mass0.pos.push([mpos[j*3],mpos[j*3+1],mpos[j*3+2]]);mass0.style.push(melem[j]%4)}
    for(let j=0;j<nt;j++){mass0.pos.push([tpos[j*3],tpos[j*3+1],tpos[j*3+2]]);mass0.style.push(4)}   // the pilot's trail prisms: jade
    const frames=[]; let o=0;
    for(let f=0;f<r.nf;f++){const n=counts[f],P=new Float32Array(n*3),C=new Float32Array(n*3),S=new Float32Array(n);
      for(let i=0;i<n*3;i++){P[i]=pos[o*3+i];C[i]=col[o*3+i]/255} for(let j=0;j<n;j++)S[j]=size[o+j]/4; o+=n;
      const al=new Uint8Array(NB);
      for(let j=0;j<nm;j++)if(mdeath[j]>f)al[j>>3]|=128>>(j&7);
      for(let j=0;j<nt;j++){const q=nm+j;if(tborn[j]<=f&&tdeath[j]>f)al[q>>3]|=128>>(q&7)}
      frames.push({t:+(f*r.dt).toFixed(3),pilots:[[pil[f*3],pil[f*3+1],pil[f*3+2]]],alive:al,species:{[r.key]:{n,pos:P,col:C,size:S}}})}
    return {meta:{label:r.label,note:r.key+': '+r.emotion,mass_n_max:N,R:1200},mass0,frames,raw:r};
  },
  load(run,i){panel(run.raw); hitF=new Map(); for(const [f,kind] of run.raw.hits)hitF.set(f,(hitF.get(f)||[]).concat(kind)); hitLast=-1;
    document.querySelectorAll('.sp').forEach((b,j)=>b.setAttribute('aria-current',j===i?'true':'false'))},
  frame(fa,fb,u,k){if(k===hitLast)return; hitLast=k; let msg='',fresh=false;
    for(let b=Math.max(0,k-6);b<=k;b++){const h=hitF&&hitF.get(b);if(h){msg=h[h.length-1];fresh=b>=k-1}}
    document.getElementById('hitmsg').textContent=msg?('hit: '+msg):''; if(fresh)document.getElementById('hitv').style.opacity=0.85; else document.getElementById('hitv').style.opacity=0},
  hud(){const r=run&&run.raw;if(!r)return '';let n=0;for(const [f] of r.hits)if(f<=k)n++;return `<br>hits so far <b>${n}</b> / ${r.hits.length}`}
};
addEventListener('DOMContentLoaded',()=>{
  const list=document.getElementById('list'), sel=document.getElementById('run');
  RUNS.forEach((r,i)=>{const b=document.createElement('button');b.className='sp';b.setAttribute('role','listitem');
    b.innerHTML=`<b>${r.key}</b><span>${r.emotion.split('(')[0].split('->')[0].split(' with')[0]}</span>`;
    b.onclick=()=>{sel.value=i;load(i)};list.appendChild(b)});
  document.querySelectorAll('.sp').forEach((b,j)=>b.setAttribute('aria-current',j===+sel.value?'true':'false'));
  const led=document.getElementById('led'); if(innerWidth<=560)led.classList.add('shut');
  document.getElementById('ledtog').onclick=()=>led.classList.toggle('shut');
});
})();
</script>
