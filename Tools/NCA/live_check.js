// Headless check of the gallery's LIVE swarm section (viewer_live.py + swarm_live.js).
//   node live_check.js <viewer.html> <outdir> [three.min.js]
// Loads the page in Chromium (Playwright), scrolls to the live section, and verifies: no page errors, the
// simulation steps at >= 10 steps/s while rendering, a drag across the swarm kills tadpoles (and leaves
// lime crystals), and an element-filtered carve of the majority makes the swarm switch plan. Screenshots
// and a JSON report go to <outdir>.
const path = require('path'), fs = require('fs');
let pw; try { pw = require('playwright'); } catch (e) { pw = require(path.join(process.env.NODE_PATH || '/opt/node-tools/node_modules', 'playwright')); }
(async () => {
  const [page_, out, three] = process.argv.slice(2);
  fs.mkdirSync(out, { recursive: true });
  const browser = await pw.chromium.launch({ args: ['--use-gl=swiftshader', '--enable-webgl', '--ignore-gpu-blocklist'] });
  const ctx = await browser.newContext({ viewport: { width: 1400, height: 1000 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(String(e)));
  page.on('console', m => { if (m.type() === 'error' && !/Failed to load resource/.test(m.text())) errors.push('console: ' + m.text()); });
  const netErrors = []; page.on('requestfailed', r => netErrors.push(r.url().slice(0, 80)));
  if (three) await page.route(/three\.min\.js$/, r => r.fulfill({ body: fs.readFileSync(three), contentType: 'application/javascript' }));
  await page.goto('file://' + path.resolve(page_), { waitUntil: 'load' });
  await page.waitForFunction(() => window.liveSwarm && window.liveSwarm.sw, null, { timeout: 30000 });
  await page.locator('#live').scrollIntoViewIfNeeded();
  await page.evaluate(() => document.getElementById('cvl').scrollIntoView({ block: 'center' }));
  const rep = { errors, netErrors };
  const T0 = Date.now(), say = m => console.log(((Date.now() - T0) / 1000).toFixed(1) + 's', m);
  const snap = async name => { await page.locator('#platel').screenshot({ path: path.join(out, name + '.png') }); };
  const cen = () => page.evaluate(() => window.liveSwarm.sw.census());

  // 1. it runs: measured rate at the default speed (12/s) and at full speed (40/s, the slider max)
  await page.waitForTimeout(4000);
  rep.rate_default = await page.evaluate(() => window.liveSwarm.rate); say('rate ' + rep.rate_default);
  await page.evaluate(() => { const s = document.getElementById('lspeed'); s.value = 40; s.dispatchEvent(new Event('input')); });
  await page.waitForTimeout(3000);
  rep.rate_max = await page.evaluate(() => window.liveSwarm.rate);
  rep.sim_ms = await page.evaluate(() => window.liveSwarm.simMs); rep.draw_ms = await page.evaluate(() => window.liveSwarm.drawMs); say(`max ${rep.rate_max} sim ${rep.sim_ms}ms draw ${rep.draw_ms}ms`);
  await page.evaluate(() => { const s = document.getElementById('lspeed'); s.value = 12; s.dispatchEvent(new Event('input')); });
  rep.grow = await cen();
  rep.step_after_grow = await page.evaluate(() => window.liveSwarm.sw.clock);
  // let it finish growing
  await page.waitForFunction(() => window.liveSwarm.sw.clock > 260, null, { timeout: 60000 });
  say('phase 1');
  rep.grown = await cen();
  await snap('1_grown');

  // 2. carve: drag straight across the middle of the canvas, all elements, brush 8
  await page.evaluate(() => { const s = document.getElementById('lbr'); s.value = 8; s.dispatchEvent(new Event('input')); });
  const box = await page.locator('#cvl').boundingBox();
  const n0 = (await cen()).n;
  // aim at the live tadpole nearest the swarm's centroid, and drag through it
  const [ax, ay] = await page.evaluate(() => { const L = window.liveSwarm, sw = L.sw; let m = [0, 0, 0], n = 0;
    for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i]) { for (let k = 0; k < 3; k++) m[k] += sw.pos[3 * i + k]; n++; }
    m = m.map(v => v / n); let best = -1, bd = 1e9;
    for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i]) { const d = Math.hypot(sw.pos[3 * i] - m[0], sw.pos[3 * i + 1] - m[1], sw.pos[3 * i + 2] - m[2]); if (d < bd) { bd = d; best = i; } }
    L.setRunning(false); return L.screenOf(best); });
  await page.mouse.move(ax - 80, ay);
  await page.mouse.down();
  for (let k = 0; k <= 20; k++) await page.mouse.move(ax - 80 + 8 * k, ay, { steps: 1 });
  await page.mouse.up();
  await page.evaluate(() => window.liveSwarm.setRunning(true));
  const n1 = (await cen()).n;
  say('phase 2');
  rep.carve = { before: n0, after: n1, killed: n0 - n1, crystals_visible: await page.evaluate(() => window.liveSwarm.dead) };
  await page.waitForTimeout(300);
  await snap('2_carved');

  // 3. eat the majority with the element filter until another element leads; watch the plan switch
  const c0 = await cen();
  rep.before_eat = c0;
  await page.evaluate(m => { document.querySelector(`input[name=lfilt][value="${m}"]`).click(); }, c0.majority);
  await page.evaluate(() => { const s = document.getElementById('lbr'); s.value = 16; s.dispatchEvent(new Event('input')); });
  let passes = 0;
  for (; passes < 30; passes++) {
    const c = await cen();
    const lead = c.elements.indexOf(Math.max(...c.elements));
    if (lead !== c0.majority) break;
    const y = 0.2 + 0.6 * ((passes * 0.37) % 1);
    await page.mouse.move(box.x + box.width * 0.15, box.y + box.height * y);
    await page.mouse.down();
    for (let k = 0; k <= 16; k++) await page.mouse.move(box.x + box.width * (0.15 + 0.7 * k / 16), box.y + box.height * y);
    await page.mouse.up();
    await page.waitForTimeout(150);
  }
  say('phase 3');
  rep.eat_passes = passes;
  rep.after_eat = await cen();
  await snap('3_eaten');
  await page.waitForFunction(p => window.liveSwarm.sw.switches.length > 0, null, { timeout: 20000 }).catch(() => {});
  rep.switches = await page.evaluate(() => window.liveSwarm.sw.switches);
  const t0 = await page.evaluate(() => window.liveSwarm.sw.clock);
  await page.waitForFunction(t => window.liveSwarm.sw.clock > t + 240, t0, { timeout: 60000 });
  say('phase 4');
  rep.after_switch = await cen();
  rep.log = await page.evaluate(() => [...document.querySelectorAll('#rl-log li')].map(l => l.textContent));
  await snap('4_switched');
  await page.locator('#live').screenshot({ path: path.join(out, 'section.png') });

  // 3b. graze + strike + tuning run without errors
  await page.evaluate(() => { const g = document.getElementById('lgraze'); g.value = 2; g.dispatchEvent(new Event('input'));
    const t = document.getElementById('lt-hgrid2-p_lay'); t.value = 0.2; t.dispatchEvent(new Event('input')); });
  const g0 = await cen(); await page.waitForTimeout(3000); const g1 = await cen();
  rep.graze = { before: g0, after: g1, deaths: await page.evaluate(() => window.liveSwarm.sw.deathsTotal) };
  await page.evaluate(() => { const g = document.getElementById('lgraze'); g.value = 0; g.dispatchEvent(new Event('input'));
    const t = document.getElementById('lt-hgrid2-p_lay'); t.value = 0.1; t.dispatchEvent(new Event('input')); });
  const s0 = (await cen()).n; await page.click('#bl-strike'); const s1 = (await cen()).n;
  rep.strike = { before: s0, after: s1 };
  await page.waitForTimeout(200); await snap('3b_strike');
  say('phase 3b ' + JSON.stringify(rep.strike));

  rep.default_model = await page.evaluate(() => window.liveSwarm.model);

  // 3c. every new model on its own: a mostly-Mass seed grows a whale; step rate at the grown whale (speed slider
  // at its max 40, and the sim's own ms/step); an element-filtered carve of the majority flips the plan.
  const setSpeed = v => page.evaluate(v => { const s = document.getElementById('lspeed'); s.value = v; s.dispatchEvent(new Event('input')); }, v);
  async function eatMajority() {
    const c0 = await cen();
    await page.evaluate(m => { document.querySelector(`input[name=lfilt][value="${m}"]`).click(); }, c0.majority);
    await page.evaluate(() => { const s = document.getElementById('lbr'); s.value = 16; s.dispatchEvent(new Event('input')); });
    let p = 0;
    for (; p < 40; p++) {
      const c = await cen(); if (c.elements.indexOf(Math.max(...c.elements)) !== c0.majority) break;
      const y = 0.2 + 0.6 * ((p * 0.37) % 1);
      await page.mouse.move(box.x + box.width * 0.15, box.y + box.height * y); await page.mouse.down();
      for (let k = 0; k <= 16; k++) await page.mouse.move(box.x + box.width * (0.15 + 0.7 * k / 16), box.y + box.height * y);
      await page.mouse.up(); await page.waitForTimeout(120);
    }
    await page.evaluate(() => document.querySelector('input[name=lfilt][value="-1"]').click());
    return { before: c0, passes: p, after: await cen() };
  }
  rep.models = {};
  for (const [id, G] of [['hgrid2', 16], ['hgrid2', 12], ['sort', 0], ['grid', 0]]) {
    const tag = id + (G ? G : '');
    await page.evaluate(([id, G]) => { if (G) document.getElementById('lt-hgrid2-G').value = String(G); window.liveSwarm.setModel(id); }, [id, G]);
    await page.selectOption('#lbias', '1');
    await page.evaluate(() => window.liveSwarm.newSeed());
    await setSpeed(40);
    await page.waitForFunction(() => window.liveSwarm.sw.clock > 240, null, { timeout: 120000 });
    await page.waitForTimeout(3000);
    const r = { model: await page.evaluate(() => window.liveSwarm.model), grown: await cen(), rate_max: await page.evaluate(() => window.liveSwarm.rate),
      sim_ms: await page.evaluate(() => window.liveSwarm.simMs), draw_ms: await page.evaluate(() => window.liveSwarm.drawMs),
      about: await page.evaluate(() => document.getElementById('labout').textContent.slice(0, 120)),
      tune_visible: await page.evaluate(() => [...document.querySelectorAll('.ltune')].filter(d => !d.hidden).map(d => d.dataset.engine)) };
    await snap('m_' + tag + '_grown');
    await setSpeed(12);
    r.eat = await eatMajority();
    const tc = await page.evaluate(() => window.liveSwarm.sw.clock);
    await page.waitForFunction(() => window.liveSwarm.sw.switches.length > 0, null, { timeout: 30000 }).catch(() => {});
    await setSpeed(40);
    await page.waitForFunction(t => window.liveSwarm.sw.clock > t + 240, tc, { timeout: 120000 });
    r.switches = await page.evaluate(() => window.liveSwarm.sw.switches); r.after = await cen();
    r.molts = await page.evaluate(() => window.liveSwarm.sw.molts == null ? null : window.liveSwarm.sw.molts);
    r.crystals = await page.evaluate(() => window.liveSwarm.sw.deathsTotal);
    // graze + strike + bite run on every model
    const s0 = (await cen()).n; await page.click('#bl-strike'); r.strike = { before: s0, after: (await cen()).n };
    await page.evaluate(() => { const g = document.getElementById('lgraze'); g.value = 2; g.dispatchEvent(new Event('input')); });
    await page.waitForTimeout(1500);
    await page.evaluate(() => { const g = document.getElementById('lgraze'); g.value = 0; g.dispatchEvent(new Event('input')); });
    await page.click('#bl-eat'); r.bite = await cen();
    await snap('m_' + tag + '_switched');
    rep.models[tag] = r;
    say(`${tag}: grown ${JSON.stringify(r.grown.elements)} ${r.grown.plan} rate ${r.rate_max.toFixed(1)} sim ${r.sim_ms.toFixed(2)}ms; eat ${r.eat.passes} passes -> ${r.after.plan} switches ${JSON.stringify(r.switches)}`);
  }
  await page.evaluate(() => { document.getElementById('lt-hgrid2-G').value = '16'; });
  await setSpeed(12);

  // 4. a biased seed: mostly Time -> dragonfly
  await page.selectOption('#lbias', '3');
  await page.evaluate(() => window.liveSwarm.setModel('hgrid2'));
  await page.click('#bl-seed');
  await page.waitForFunction(() => window.liveSwarm.sw.clock > 200, null, { timeout: 60000 });
  say('phase 5');
  rep.time_seed = await cen();
  await snap('5_time_seed');

  // 5. the evolved rule: grows, renders, and the 'eat 2/3 of the majority' button makes it switch
  await page.evaluate(() => { window.liveSwarm.setModel('evo'); window.liveSwarm.newSeed(); });
  await page.selectOption('#lbias', '1');
  await page.evaluate(() => window.liveSwarm.newSeed());
  await page.evaluate(() => { const s = document.getElementById('lspeed'); s.value = 12; s.dispatchEvent(new Event('input')); });
  await page.waitForTimeout(4000);
  rep.evo_rate = await page.evaluate(() => window.liveSwarm.rate); rep.evo_sim_ms = await page.evaluate(() => window.liveSwarm.simMs);
  say('evo rate ' + rep.evo_rate + ' sim ' + rep.evo_sim_ms);
  await page.waitForFunction(() => window.liveSwarm.sw.clock > 240, null, { timeout: 120000 });
  rep.evo_grown = await cen();
  await snap('6_evo_grown');
  await page.click('#bl-eat');
  rep.evo_after_eat = await cen();
  const t1 = await page.evaluate(() => window.liveSwarm.sw.clock);
  await page.waitForFunction(t => window.liveSwarm.sw.clock > t + 240, t1, { timeout: 120000 });
  rep.evo_after = await cen(); rep.evo_switches = await page.evaluate(() => window.liveSwarm.sw.switches);
  await snap('7_evo_after_eat');
  say('phase 6');

  rep.ok = {
    no_errors: errors.length === 0, rate: rep.rate_default >= 10, carve_kills: rep.carve.killed > 0,
    switched: rep.switches.length > 0 && rep.after_switch.plan !== rep.before_eat.plan,
    evo_runs: rep.evo_grown.n > 32, strike_kills: rep.strike.after < rep.strike.before, evo_switched: rep.evo_after.plan !== rep.evo_grown.plan,
  };
  for (const [tag, r] of Object.entries(rep.models)) {
    rep.ok[tag + '_whale'] = r.grown.plan === 'mass';
    rep.ok[tag + '_switched'] = r.switches.length > 0 && r.after.plan !== 'mass';
    rep.ok[tag + '_interactive'] = r.rate_max >= 20;
  }
  fs.writeFileSync(path.join(out, 'check.json'), JSON.stringify(rep, null, 1));
  console.log(JSON.stringify(rep.ok), 'rate', rep.rate_default.toFixed(1), rep.rate_max.toFixed(1), 'carve', JSON.stringify(rep.carve),
    'switch', JSON.stringify(rep.switches), 'evo', JSON.stringify(rep.evo_switches), rep.evo_rate, 'errors', errors.slice(0, 5));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
