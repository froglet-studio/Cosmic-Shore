#!/usr/bin/env node
// Render Frogma boards (.dc.html) locally for a look before publishing (/frogma §5, "render the board locally").
// The canvas's own runtime (support.js) is not in the repo, so this injects a minimal stand-in that does what a board
// needs to be SEEN: {{dotted.holes}} in text and attributes, <sc-for>, <sc-if>, on* handlers bound to renderVals()
// functions, setState re-rendering, data-props defaults and the <helmet> styles. /_blob/ images do not load (they live
// in the canvas's asset store), so they show as broken images. This is a look, never the gate: `frogma.py check` is.
//
//   node .claude/skills/frogma/render_local.cjs <dir with the .dc.html files> <out dir> <file> [<file> ...]
//   CLICK='R51A.dc.html@<css selector>@<ms to wait>|...' SUFFIX=_after node ...   # press things, then shoot
//
// Written for Rev 51 (2026-11-05); measured on its nine boards, it matches what the canvas showed for layout and
// colour. Not covered: <dc-import>, links between boards, the editor's tweak panel.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const os = require('os');

const SHIM = `(function(){
class DCLogic{constructor(p){this.props=p||{};this.state={};}setState(s){Object.assign(this.state,typeof s==='function'?s(this.state):s);window.__rerender&&window.__rerender();}forceUpdate(){window.__rerender&&window.__rerender();}componentDidMount(){}}
window.DCLogic=DCLogic;
function look(scope,p){p=p.trim();if(p==='true')return true;if(p==='false')return false;if(/^-?\\d+(\\.\\d+)?$/.test(p))return +p;let v=scope;for(const k of p.split('.')){if(v==null)return undefined;v=v[k];}return v;}
function interp(str,scope){const m=str.match(/^\\s*\\{\\{([^}]+)\\}\\}\\s*$/);if(m)return look(scope,m[1]);return str.replace(/\\{\\{([^}]+)\\}\\}/g,(_,p)=>{const v=look(scope,p);return v==null?'':v;});}
function proc(node,scope){
 if(node.nodeType===3){if(node.textContent.includes('{{'))node.textContent=interp(node.textContent,scope)+'';return;}
 if(node.nodeType!==1)return;
 const tag=node.tagName.toLowerCase();
 if(tag==='sc-for'){const list=look(scope,node.getAttribute('list').replace(/[{}]/g,''))||[];const as=node.getAttribute('as');const frag=document.createDocumentFragment();
   list.forEach((it,i)=>{for(const ch of node.__tpl){const c=ch.cloneNode(true);prep(c);proc(c,Object.assign(Object.create(scope),{[as]:it,$index:i}));frag.appendChild(c);}});
   node.replaceWith(frag);return;}
 if(tag==='sc-if'){if(!look(scope,node.getAttribute('value').replace(/[{}]/g,''))){node.remove();return;}for(const k of [...node.childNodes])proc(k,scope);node.replaceWith(...node.childNodes);return;}
 for(const a of [...node.attributes]){if(!a.value.includes('{{'))continue;const v=interp(a.value,scope);
   if(/^on/i.test(a.name)&&typeof v==='function'){node.removeAttribute(a.name);node.addEventListener(a.name.slice(2).toLowerCase(),v);}
   else node.setAttribute(a.name,v==null?'':v);}
 for(const k of [...node.childNodes])proc(k,scope);
}
function prep(root){if(!root.querySelectorAll)return;for(const f of [root,...root.querySelectorAll('sc-for')])if(f.tagName&&f.tagName.toLowerCase()==='sc-for'&&!f.__tpl)f.__tpl=[...f.childNodes].map(c=>c.cloneNode(true));}
window.addEventListener('DOMContentLoaded',()=>{
 const x=document.querySelector('x-dc');const helmet=x.querySelector('helmet');if(helmet){document.head.insertAdjacentHTML('beforeend',helmet.innerHTML);helmet.remove();}
 const tpl=x.innerHTML;const sc=document.querySelector('script[data-dc-script]');const props={};
 try{const p=JSON.parse(sc.getAttribute('data-props')||'{}');for(const k in p)if(p[k]&&'default' in p[k])props[k]=p[k].default;}catch(e){}
 const Component=(new Function('DCLogic',sc.textContent+';return Component;'))(DCLogic);const comp=new Component(props);
 window.__rerender=()=>{x.innerHTML=tpl;prep(x);proc(x,comp.renderVals());};window.__rerender();
});})();`;

(async () => {
  const [dir, out, ...files] = process.argv.slice(2);
  if (!dir || !out || !files.length) { console.error('usage: render_local.cjs <dir> <out> <file.dc.html>...'); process.exit(2); }
  fs.mkdirSync(out, { recursive: true });
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'frogma-render-'));
  fs.writeFileSync(path.join(tmp, 'shim.js'), SHIM);
  const clicks = (process.env.CLICK || '').split('|').filter(Boolean).map(c => c.split('@'));
  const browser = await chromium.launch();
  let failed = 0;
  for (const f of files) {
    const html = fs.readFileSync(path.join(dir, f), 'utf8').replace('<script src="./support.js"></script>', '<script src="shim.js"></script>');
    fs.writeFileSync(path.join(tmp, f), html);
    const m = html.match(/"\$preview":\s*\{"width":\s*(\d+),\s*"height":\s*(\d+)\}/);
    const [w, h] = m ? [+m[1], +m[2]] : [1920, 1080];
    const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
    page.on('pageerror', e => { failed++; console.log(`${f}: page error: ${e.message}`); });
    await page.goto('file://' + path.join(tmp, f));
    await page.waitForTimeout(900);
    for (const [file, sel, wait] of clicks) if (file === f) { await page.click(sel); await page.waitForTimeout(+wait || 300); }
    const shot = path.join(out, f.replace('.dc.html', (process.env.SUFFIX || '') + '.png'));
    await page.screenshot({ path: shot });
    console.log(`${f} -> ${shot}`);
    await page.close();
  }
  await browser.close();
  fs.rmSync(tmp, { recursive: true, force: true });
  process.exit(failed ? 1 : 0);
})();
