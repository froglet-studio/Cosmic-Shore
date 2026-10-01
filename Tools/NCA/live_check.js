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
  await page.mouse.move(box.x + box.width * 0.3, box.y + box.height * 0.5);
  await page.mouse.down();
  for (let k = 0; k <= 20; k++) await page.mouse.move(box.x + box.width * (0.3 + 0.4 * k / 20), box.y + box.height * 0.5, { steps: 1 });
  await page.mouse.up();
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

  // 4. a biased seed: mostly Time -> dragonfly
  await page.selectOption('#lbias', '3');
  await page.click('#bl-seed');
  await page.waitForFunction(() => window.liveSwarm.sw.clock > 200, null, { timeout: 60000 });
  say('phase 5');
  rep.time_seed = await cen();
  await snap('5_time_seed');

  rep.ok = {
    no_errors: errors.length === 0, rate: rep.rate_default >= 10, carve_kills: rep.carve.killed > 0,
    switched: rep.switches.length > 0 && rep.after_switch.plan !== rep.before_eat.plan,
  };
  fs.writeFileSync(path.join(out, 'check.json'), JSON.stringify(rep, null, 1));
  console.log(JSON.stringify(rep.ok), 'rate', rep.rate_default.toFixed(1), rep.rate_max.toFixed(1), 'carve', JSON.stringify(rep.carve),
    'switch', JSON.stringify(rep.switches), 'errors', errors.slice(0, 5));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
