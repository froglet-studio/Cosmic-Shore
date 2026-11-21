/* FTUE Creator preview: the HyperSea behind the page, and a player that plays a phase the way the
   Quest Graph runner would - the menus, the spotlight, the comms window, the flight view - with
   sound. It reads the same phase model the editor writes, so what plays is what exports. */
(function () {
  'use strict';
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const css = n => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
  const rand = (a, b) => a + Math.random() * (b - a);

  // ---------- the sea: drifting faceted prisms, light shafts and plankton ----------
  function Sea(canvas, opts) {
    const ctx = canvas.getContext('2d');
    let w = 0, h = 0, dpr = 1, prisms = [], motes = [], t = 0, raf = 0, alive = true;
    const o = Object.assign({ prisms: 34, motes: 90, speed: 1, vessel: false }, opts || {});
    function resize() {
      dpr = Math.min(2, devicePixelRatio || 1);
      const r = canvas.getBoundingClientRect();
      w = Math.max(1, r.width); h = Math.max(1, r.height);
      canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      if (!prisms.length) seed();
    }
    function seed() {
      prisms = Array.from({ length: o.prisms }, () => ({ x: rand(0, 1), y: rand(0, 1), z: rand(.25, 1), r: rand(0, 6.3), vr: rand(-.3, .3), s: rand(6, 22), hue: Math.random() }));
      motes = Array.from({ length: o.motes }, () => ({ x: rand(0, 1), y: rand(0, 1), z: rand(.2, 1), p: rand(0, 6.3) }));
    }
    const state = { trail: [], skim: 0, boost: 0, sparks: [] };
    function draw(dt) {
      t += dt * o.speed;
      const top = css('--sea-top') || '#001233', bot = css('--sea-bottom') || '#00010A';
      const g = ctx.createLinearGradient(0, 0, 0, h); g.addColorStop(0, top); g.addColorStop(1, bot);
      ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
      // light shafts from the surface
      ctx.save(); ctx.globalCompositeOperation = 'lighter';
      for (let k = 0; k < 4; k++) {
        const x = w * (.15 + k * .25) + Math.sin(t * .15 + k) * 40;
        const sg = ctx.createLinearGradient(x, 0, x + 60, h * .9);
        sg.addColorStop(0, css('--sea-shaft') || 'rgba(0,212,255,.10)'); sg.addColorStop(1, 'rgba(0,0,0,0)');
        ctx.fillStyle = sg; ctx.beginPath(); ctx.moveTo(x - 30, 0); ctx.lineTo(x + 40, 0); ctx.lineTo(x + 160, h); ctx.lineTo(x + 20, h); ctx.closePath(); ctx.fill();
      }
      ctx.restore();
      const cols = [css('--sea-p1') || '#00D4FF', css('--sea-p2') || '#21C0BD', css('--sea-p3') || '#A600FF'];
      const flow = o.vessel ? (1.2 + state.boost * 2.5) : .08;
      for (const p of prisms) {
        p.r += p.vr * dt; p.x -= dt * flow * .05 * p.z; p.y += Math.sin(t * .3 + p.hue * 9) * .0004;
        if (p.x < -.1) { p.x = 1.1; p.y = rand(0, 1); }
        const x = p.x * w, y = p.y * h, s = p.s * p.z * (o.vessel ? 1.3 : 1);
        ctx.save(); ctx.translate(x, y); ctx.rotate(p.r);
        ctx.globalAlpha = .18 + .5 * p.z * (o.vessel ? 1 : .6);
        const c = cols[Math.floor(p.hue * 3)];
        ctx.fillStyle = c + '33'; ctx.strokeStyle = c; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(0, -s); ctx.lineTo(s * .87, s * .5); ctx.lineTo(-s * .87, s * .5); ctx.closePath(); ctx.fill(); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(0, -s); ctx.lineTo(0, s * .15); ctx.lineTo(s * .87, s * .5); ctx.moveTo(0, s * .15); ctx.lineTo(-s * .87, s * .5); ctx.globalAlpha *= .5; ctx.stroke();
        ctx.restore();
      }
      ctx.fillStyle = css('--sea-mote') || 'rgba(230,233,255,.5)';
      for (const m of motes) {
        m.x -= dt * flow * .02 * m.z; if (m.x < 0) m.x = 1;
        ctx.globalAlpha = (.25 + .5 * m.z) * (.6 + .4 * Math.sin(t * 2 + m.p));
        ctx.fillRect(m.x * w, (m.y + Math.sin(t * .4 + m.p) * .01) * h, 1.4 * m.z, 1.4 * m.z);
      }
      ctx.globalAlpha = 1;
      if (o.vessel) drawVessel(dt);
    }
    function drawVessel(dt) {
      const vx = w * .32, vy = h * .52 + Math.sin(t * 1.3) * h * .05;
      state.trail.push({ x: vx - 14, y: vy, a: 1 });
      if (state.trail.length > 70) state.trail.shift();
      ctx.save();
      for (let k = 0; k < state.trail.length; k++) {
        const p = state.trail[k]; p.x -= dt * (90 + state.boost * 220);
        if (k % 4) continue;
        const a = k / state.trail.length;
        ctx.globalAlpha = a * .8; ctx.fillStyle = css('--sea-p2') || '#21C0BD';
        ctx.beginPath(); ctx.moveTo(p.x, p.y - 5); ctx.lineTo(p.x + 6, p.y); ctx.lineTo(p.x, p.y + 5); ctx.lineTo(p.x - 6, p.y); ctx.closePath(); ctx.fill();
      }
      ctx.restore();
      // the vessel: a faceted chevron with a glow
      ctx.save(); ctx.translate(vx, vy); ctx.rotate(Math.cos(t * 1.3) * .12);
      const glow = ctx.createRadialGradient(0, 0, 2, 0, 0, 46); glow.addColorStop(0, 'rgba(153,255,128,.35)'); glow.addColorStop(1, 'rgba(153,255,128,0)');
      ctx.fillStyle = glow; ctx.beginPath(); ctx.arc(0, 0, 46, 0, 6.3); ctx.fill();
      ctx.fillStyle = '#E6E9FF'; ctx.beginPath(); ctx.moveTo(26, 0); ctx.lineTo(-16, -14); ctx.lineTo(-8, 0); ctx.lineTo(-16, 14); ctx.closePath(); ctx.fill();
      ctx.fillStyle = '#99FF80'; ctx.beginPath(); ctx.moveTo(26, 0); ctx.lineTo(-8, 0); ctx.lineTo(-16, -14); ctx.closePath(); ctx.fill();
      ctx.restore();
      state.boost = Math.max(0, state.boost - dt * .6);
      for (const s of state.sparks) { s.x += s.vx * dt; s.y += s.vy * dt; s.a -= dt * 1.6; }
      state.sparks = state.sparks.filter(s => s.a > 0);
      ctx.fillStyle = '#99FF80';
      for (const s of state.sparks) { ctx.globalAlpha = s.a; ctx.fillRect(s.x, s.y, 3, 3); }
      ctx.globalAlpha = 1;
      state.vx = vx; state.vy = vy;
    }
    function burst() { for (let k = 0; k < 14; k++) state.sparks.push({ x: state.vx || w * .3, y: state.vy || h * .5, vx: rand(-160, 60), vy: rand(-120, 120), a: 1 }); state.boost = Math.min(1, state.boost + .35); }
    let last = performance.now();
    function frame(now) {
      if (!alive) return;
      const dt = Math.min(.05, (now - last) / 1000); last = now;
      if (!document.hidden && canvas.offsetParent !== null) draw(dt);
      raf = requestAnimationFrame(frame);
    }
    resize(); addEventListener('resize', resize);
    if ('ResizeObserver' in window) new ResizeObserver(resize).observe(canvas);
    if (reduce) { draw(0); addEventListener('resize', () => draw(0)); } else raf = requestAnimationFrame(frame);
    return { burst, redraw: () => draw(0), set vessel(v) { o.vessel = v; }, stop() { alive = false; cancelAnimationFrame(raf); } };
  }

  // ---------- sound: synthesized, so nothing loads from outside the page ----------
  const Sound = (() => {
    let ac = null, amb = null, on = true;
    const ensure = () => { if (!ac) { try { ac = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { ac = null; } } if (ac && ac.state === 'suspended') ac.resume(); return ac; };
    function tone(freq, dur, type, vol, slide) {
      if (!on || !ensure()) return;
      const t = ac.currentTime, o = ac.createOscillator(), g = ac.createGain();
      o.type = type || 'sine'; o.frequency.setValueAtTime(freq, t); if (slide) o.frequency.exponentialRampToValueAtTime(slide, t + dur);
      g.gain.setValueAtTime(vol || .05, t); g.gain.exponentialRampToValueAtTime(.0001, t + dur);
      o.connect(g).connect(ac.destination); o.start(t); o.stop(t + dur + .02);
    }
    return {
      get on() { return on; }, set on(v) { on = v; if (!v) this.ambient(false); },
      unlock: ensure,
      blip() { tone(rand(1500, 1900), .025, 'square', .012); },
      open() { tone(320, .18, 'sine', .05, 640); tone(640, .22, 'triangle', .025, 960); },
      tap() { tone(880, .06, 'triangle', .05, 1200); },
      success() { [660, 880, 1320].forEach((f, i) => setTimeout(() => tone(f, .16, 'triangle', .045), i * 70)); },
      spot() { tone(220, .35, 'sine', .04, 440); },
      skim() { tone(rand(900, 1300), .05, 'sine', .03, 1800); },
      haptic() { tone(70, .09, 'sine', .12); },
      phase() { [523, 659, 784, 1047].forEach((f, i) => setTimeout(() => tone(f, .3, 'triangle', .045), i * 110)); },
      ambient(start) {
        if (!start) { if (amb) { amb.forEach(n => { try { n.stop(); } catch (e) {} }); amb = null; } return; }
        if (!on || amb || !ensure()) return;
        const g = ac.createGain(); g.gain.value = .018; g.connect(ac.destination);
        const f = ac.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 420; f.connect(g);
        amb = [55, 82.5, 110.3].map((fr, i) => { const o = ac.createOscillator(); o.type = i ? 'sine' : 'triangle'; o.frequency.value = fr; o.connect(f); o.start(); return o; });
        const lfo = ac.createOscillator(), lg = ac.createGain(); lfo.frequency.value = .08; lg.gain.value = 180; lfo.connect(lg).connect(f.frequency); lfo.start(); amb.push(lfo);
      },
    };
  })();

  // ---------- the player ----------
  const MODES_SHOWN = ['SkimRace', 'Scurry', 'Joust', 'Maelstrom', 'Regatta', 'Switchback', 'Hijack', 'Bloomrush'];
  const NAV = [['home', 'Home'], ['arcade', 'Arcade'], ['hangar', 'Hangar'], ['profile', 'Profile'], ['episodes', 'Episodes'], ['store', 'Store']];
  const ACTION_TARGET = { ViewArcadeMenu: 'nav-arcade', ViewArcadeLoadoutMenu: 'nav-arcade', ViewArcadeExploreMenu: 'nav-arcade', ViewStoreMenu: 'nav-store', ClickDailyReward: 'nav-store', ViewHangarMenu: 'nav-hangar', ViewShipManta: 'nav-hangar', ViewShipRhino: 'nav-hangar', ViewShipSquirrel: 'nav-hangar', UnlockVessel: 'nav-hangar', PlayGame: 'nav-arcade', ViewProfileMenu: 'nav-profile', ViewEpisodeMenu: 'nav-episodes' };
  const NAV_TARGET = { Home: 'home', Store: 'store', Port: 'home', Hangar: 'hangar', Profile: 'profile', ArcadeModal: 'arcade' };

  function Player(root, api) {
    // api: { phase(), enumName(en, v), onStep(nodeId), onEdit(), speakers }
    const $ = s => root.querySelector(s);
    const esc = s => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
    root.innerHTML = `
      <div class="device" id="simDevice">
        <canvas class="world" aria-hidden="true"></canvas>
        <div class="shell" data-screen="home">
          <div class="topbar"><span class="logo">COSMIC SHORE</span><span class="crystals" title="Crystals">◆ <b>120</b></span><button class="gear" data-ctl="nav-settings" aria-label="Settings">⚙</button></div>
          <nav class="navcol">${NAV.map(([k, l]) => `<button data-ctl="nav-${k}" data-nav="${k}"><i></i>${l}</button>`).join('')}</nav>
          <div class="screen s-home"><div class="hero"><div class="eyebrow">The HyperSea</div><div class="big">Fly. Skim. Claim the sea.</div><button class="play" data-ctl="nav-arcade">Play</button></div></div>
          <div class="screen s-arcade"><div class="eyebrow">Arcade</div><div class="cards">${MODES_SHOWN.map(m => `<button class="mcard" data-ctl="card-${m}" data-mode="${m}"><span class="art"></span><b>${m}</b><small>I1 · 1-4 pilots</small></button>`).join('')}</div>
            <div class="previewwin" hidden><div class="pw-art"><span>▶</span></div><div><b class="pw-mode"></b><small>Tap the window to fly</small></div></div></div>
          <div class="screen s-profile"><div class="eyebrow">Quest track</div><div class="track">${['Scurry', 'SkimRace', 'Joust', 'Maelstrom', 'Hangar'].map((m, i) => `<button class="qnode ${i ? '' : 'done'}" data-ctl="quest-${m}" data-quest="${m}"><span>${i + 1}</span><b>${m === 'SkimRace' ? 'Hex Race' : m}</b><small>${i ? 'Claim' : 'Claimed'}</small></button>`).join('')}</div></div>
          <div class="screen s-hangar"><div class="eyebrow">Vessel Hangar</div><div class="vessels">${['Squirrel', 'Dolphin', 'Manta', 'Rhino', 'Sparrow', 'Serpent'].map((v, i) => `<button class="vcard ${i ? 'locked' : ''}" data-ctl="vessel-${v}"><span class="vart"></span><b>${v}</b><small>${i ? '◆ 400' : 'Flying'}</small></button>`).join('')}</div></div>
          <div class="screen s-episodes"><div class="eyebrow">Episodes</div><div class="big muted">Voyages begin here.</div></div>
          <div class="screen s-store"><div class="eyebrow">Store</div><div class="big muted">Daily reward ready.</div></div>
        </div>
        <div class="flight" hidden>
          <div class="flighthud"><span class="fmode">Freestyle</span><span class="fcount" hidden></span><button class="fexit" data-ctl="overlay-exit">☰</button></div>
          <div class="inputs" hidden></div>
        </div>
        <div class="dim" hidden><div class="hole"></div></div>
        <div class="instr" hidden><span class="itext" spellcheck="false"></span></div>
        <div class="comms" hidden>
          <div class="cav"><span></span></div>
          <div class="cbody"><div class="ceyebrow">Incoming</div><div class="cwho"></div><div class="ctext" spellcheck="false"></div>
            <div class="cactions"><span class="caction"></span><button class="cnext">Next ▸</button></div></div>
        </div>
        <button class="skip" hidden></button>
        <div class="banner" hidden></div>
        <div class="toastS" hidden></div>
        <div class="haptic" aria-hidden="true"></div>
      </div>
      <div class="simbar">
        <button class="sbtn" data-sim="restart" title="Play the phase from the start">⏮</button>
        <button class="sbtn primary" data-sim="play" title="Play or pause (Space)">▶</button>
        <button class="sbtn" data-sim="step" title="Satisfy this step and go on">⏭</button>
        <div class="scrub" role="list"></div>
        <select class="speed" aria-label="Speed"><option value="1">1×</option><option value="2">2×</option><option value="4">4×</option></select>
        <label class="stog" title="Advance waits and dialogue by itself"><input type="checkbox" class="auto"> Auto</label>
        <label class="stog" title="Sound effects and sea ambience"><input type="checkbox" class="snd" checked> Sound</label>
        <label class="stog vtog" title="Read the lines aloud in a Claude voice" hidden><input type="checkbox" class="voice"> Voice</label>
      </div>
      <div class="simnow"><span class="nowlabel">Press ▶ to play this phase as a player sees it.</span><span class="nowhint">Click the words in the preview to edit them.</span></div>`;

    const device = $('#simDevice'), shell = $('.shell'), flight = $('.flight');
    const world = Sea($('.world'), { prisms: 26, motes: 70 });
    let run = 0, playing = false, idx = 0, resolver = null, speed = 1, tts = null, voiceOn = false;
    window.claude?.use?.('tts').then(t => { tts = t; if (t) $('.vtog').hidden = false; });
    const sim = { navLocked: false, allowed: null, buttons: {}, screen: 'home' };

    const wait = ms => new Promise(r => { const my = run; setTimeout(() => r(my === run), ms / speed); });
    const live = my => my === run;
    function show(el, on) { el.hidden = !on; }
    function screen(name) { sim.screen = name; shell.dataset.screen = name; shell.querySelectorAll('[data-nav]').forEach(b => b.classList.toggle('on', b.dataset.nav === name)); show($('.previewwin'), false); }
    function toFlight(on, label) { show(flight, on); show(shell, !on); world.vessel = on; $('.fmode').textContent = label || 'Freestyle'; if (!on) { show($('.inputs'), false); show($('.fcount'), false); } }
    function applyLocks() {
      shell.querySelectorAll('[data-nav]').forEach(b => b.classList.toggle('locked', sim.navLocked && b.dataset.nav !== 'home'));
      shell.querySelectorAll('.mcard').forEach(c => c.classList.toggle('locked', !!sim.allowed && c.dataset.mode !== sim.allowed));
      const ep = shell.querySelector('[data-nav="episodes"]'); if (sim.buttons.episodes === false) ep.classList.add('locked');
    }
    function toast(msg) { const t = $('.toastS'); t.textContent = msg; show(t, true); clearTimeout(t._h); t._h = setTimeout(() => show(t, false), 1800 / speed); }
    function haptic(kind) { if (!kind) return; const h = $('.haptic'); h.classList.remove('go'); void h.offsetWidth; h.classList.add('go'); Sound.haptic(); }

    // spotlight: dim everything, cut a hole around one control, Settings stays usable
    function spotlight(sel, alpha) {
      const el = sel ? root.querySelector(sel) : null, dim = $('.dim'), hole = $('.hole');
      if (!el || el.offsetParent === null) { show(dim, false); return null; }
      const d = device.getBoundingClientRect(), r = el.getBoundingClientRect();
      Object.assign(hole.style, { left: (r.left - d.left - 6) + 'px', top: (r.top - d.top - 6) + 'px', width: (r.width + 12) + 'px', height: (r.height + 12) + 'px' });
      hole.style.setProperty('--dim', alpha == null ? .7 : alpha);
      show(dim, true); Sound.spot();
      return el;
    }
    function clearSpot() { show($('.dim'), false); }
    // waits for the player to press one control (or the Step button / auto)
    function press(sel, autoMs) {
      return new Promise(res => {
        const my = run;
        const els = Array.from(root.querySelectorAll(sel));
        const done = ok => { els.forEach(e => e.removeEventListener('click', h)); resolver = null; res(ok && live(my)); };
        const h = e => { e.stopPropagation(); Sound.tap(); done(true); };
        els.forEach(e => e.addEventListener('click', h));
        resolver = () => done(true);
        if ($('.auto').checked) setTimeout(() => { if (resolver) resolver(); }, (autoMs || 1600) / speed);
      });
    }
    function bar(n) { const total = (api.phase() ? chainOf() : []).length; $('.nowlabel').innerHTML = n ? `<b>${idx + 1}/${total}</b> ${esc(api.label(n))} · <span>${esc(n.displayName)}</span>` : 'Press ▶ to play this phase as a player sees it.'; }
    function chainOf() { return FTUE.chain(api.phase()).path; }
    function renderScrub(active) {
      const path = api.phase() ? chainOf() : [];
      $('.scrub').innerHTML = path.map((n, i) => `<button role="listitem" class="tick cat-${FTUE.NODE_TYPES[n.type].cat} ${i === active ? 'on' : ''} ${i < active ? 'past' : ''} ${n.enabled ? '' : 'off'}" data-jump="${i}" title="${esc(i + 1 + '. ' + n.displayName)}"></button>`).join('');
    }

    // comms window: typewriter, blips, optional voice, editable text
    async function speak(who, lines, nodeRef, field, action) {
      const c = $('.comms'), txt = $('.ctext'), my = run;
      $('.cwho').textContent = who || 'Captain';
      $('.cav span').textContent = (who || '?').trim()[0] || '?';
      $('.caction').innerHTML = action ? `<kbd>${esc(action.key || '')}</kbd> ${esc(action.label)}` : '';
      if (c.hidden) { show(c, true); c.classList.remove('in'); void c.offsetWidth; c.classList.add('in'); Sound.open(); }
      for (let i = 0; i < lines.length; i++) {
        if (!live(my)) return false;
        const line = lines[i] || '';
        txt.contentEditable = 'false'; txt.textContent = '';
        txt.dataset.idx = i; txt._ref = nodeRef; txt._field = field;
        if (voiceOn && tts) tts.speak(line, { voice: 'airy' }).catch(() => {});
        for (let k = 0; k < line.length; k++) {
          if (!live(my)) return false;
          txt.textContent = line.slice(0, k + 1);
          if (k % 2 === 0 && line[k] !== ' ') Sound.blip();
          await new Promise(r => setTimeout(r, (reduce ? 2 : 22) / speed));
        }
        txt.contentEditable = 'plaintext-only';
        $('.cnext').hidden = !!action;
        if (action) return true;
        const ok = await press('.cnext', 900 + line.length * 45);
        if (!ok) return false;
      }
      return true;
    }
    function closeComms() { show($('.comms'), false); try { tts && tts.stop(); } catch (e) {} }
    function instruction(text, node) {
      const el = $('.instr'), t = $('.itext');
      t.textContent = text; t._ref = node; t._field = 'text'; t.contentEditable = 'plaintext-only';
      show(el, !!text); el.classList.remove('in'); void el.offsetWidth; el.classList.add('in');
    }
    // inline edits write back to the node
    root.addEventListener('input', e => {
      const t = e.target; if (!t._ref) return;
      const n = t._ref;
      if (t._field === 'lines') n.fields.lines[Number(t.dataset.idx)] = t.textContent;
      else n.fields[t._field] = t.textContent;
      api.onEdit(n);
    });
    root.addEventListener('keydown', e => { if (e.target._ref && e.key === 'Enter') { e.preventDefault(); e.target.blur(); } });

    // ---------- one step ----------
    async function play(n) {
      const f = n.fields, E = api.enumName, my = run;
      switch (n.type) {
        case 'QuestDialogueNode': { const ok = await speak(f.speakerName, f.lines.length ? f.lines : ['(no lines)'], n, 'lines'); closeComms(); return ok; }
        case 'QuestShowInstructionNode': {
          instruction(f.text || (f.panelKey ? `[panel: ${f.panelKey}]` : ''), n); haptic(f.haptic);
          const ok = await wait(Math.max(f.minDisplaySeconds * 1000, $('.auto').checked ? 1400 : 600));
          if (f.hideOnAdvance) show($('.instr'), false);
          return ok;
        }
        case 'QuestGuideToMicrogameNode': {
          const who = (api.speakers.find(s => s.ref === f.speaker) || { label: 'Trace' }).label.split(' ')[0];
          const mode = f.source ? E('GameModes', f.mode) : 'SkimRace';
          const card = shell.querySelector(`[data-mode="${mode}"]`) ? `[data-mode="${mode}"]` : '.mcard';
          const skip = $('.skip'); skip.textContent = f.skipLabel ? `${f.skipLabel}${f.skipKeycap ? '  ' + f.skipKeycap : ''}` : ''; show(skip, !!f.skipLabel);
          toFlight(false); screen('home');
          const steps = [
            ['[data-ctl="nav-arcade"].play, .s-home .play', f.arcadeCaption, 'arcadeCaption', f.arcadeAction, () => screen('arcade')],
            [card, f.cardCaption, 'cardCaption', f.cardAction, () => { const pw = $('.previewwin'); $('.pw-mode').textContent = f.source ? mode : 'Game of the Week'; show(pw, true); }],
            ['.previewwin', f.previewCaption, 'previewCaption', f.previewAction, null],
          ];
          for (const [sel, cap, field, act, after] of steps) {
            if (!live(my)) return false;
            spotlight(sel, f.dimAlpha);
            const sp = speak(who, [cap || ''], n, field, { key: f.actionKeycap, label: act });
            const ok = await press(sel, 2200); await sp;
            if (!ok) return false;
            clearSpot(); if (after) after();
            await wait(250);
          }
          closeComms(); show(skip, false); show($('.previewwin'), false);
          toFlight(true, `Lesson · ${f.source ? mode : 'Game of the Week'}`);
          instruction('Lesson: fly through the trail to steal its speed.', null); $('.itext').contentEditable = 'false';
          for (let k = 0; k < 6 && live(my); k++) { await wait(450); world.burst(); Sound.skim(); }
          show($('.instr'), false); toast('Lesson complete'); Sound.success();
          await wait(700); toFlight(false); screen('arcade');
          return live(my);
        }
        case 'QuestEnterFreestyleNode': { toFlight(true, 'Freestyle'); Sound.open(); return wait(900); }
        case 'QuestExitFreestyleNode': {
          if (!f.forceExit) { spotlight('.fexit', .5); const ok = await press('.fexit', 1400); clearSpot(); if (!ok) return false; }
          toFlight(false); screen('home'); return wait(400);
        }
        case 'QuestNavigateNode': { toFlight(false); screen(NAV_TARGET[E('QuestNavTarget', f.target)] || 'home'); toast('Navigated for the player'); return wait(700); }
        case 'QuestLockNavigationNode': { sim.navLocked = !f.unlock; applyLocks(); toast(f.unlock ? 'Navigation unlocked' : 'Navigation locked'); return wait(600); }
        case 'QuestSetArcadeConstraintsNode': { sim.allowed = f.clearConstraints ? null : E('GameModes', f.allowedMode); applyLocks(); toast(f.clearConstraints ? 'Arcade open' : `Arcade: only ${sim.allowed}, intensity ${f.forcedIntensity || 'any'}`); return wait(600); }
        case 'QuestSetButtonInteractableNode': { sim.buttons[f.buttonKey] = !!f.interactable; applyLocks(); if (f.interactable) shell.querySelector(`[data-nav="${f.buttonKey}"]`)?.classList.remove('locked'); toast(`${f.buttonKey}: ${f.interactable ? 'on' : 'off'}`); return wait(600); }
        case 'QuestWaitForInputNode': {
          if (flight.hidden) toFlight(true);
          const box = $('.inputs'); const list = f.acceptedInputs.length ? f.acceptedInputs.map(v => E('InputEvents', v).replace(/Action$/, '')) : ['Any input'];
          box.innerHTML = list.map(l => `<button class="ichip">${esc(l)}</button>`).join(''); show(box, true);
          const ok = await press('.ichip', 1500); show(box, false); haptic(f.successHaptic); if (ok) { world.burst(); Sound.success(); }
          return ok;
        }
        case 'QuestWaitForSkimNode': {
          if (flight.hidden) toFlight(true);
          const cnt = $('.fcount'); show(cnt, true);
          for (let k = 1; k <= f.targetSkims && live(my); k++) { cnt.textContent = `Skims ${k} / ${f.targetSkims}`; world.burst(); Sound.skim(); if (f.perSkimHaptic) haptic(1); await wait(Math.max(120, 2600 / Math.max(1, f.targetSkims))); }
          show(cnt, false); haptic(f.successHaptic); Sound.success(); return live(my);
        }
        case 'QuestWaitForGamePlayedNode': {
          const mode = f.filterByMode ? E('GameModes', f.expectedMode) : 'any game';
          if (!sim.screen || flight.hidden) { screen('arcade'); spotlight(f.filterByMode && shell.querySelector(`[data-mode="${mode}"]`) ? `[data-mode="${mode}"]` : '.mcard', .55); const ok = await press(f.filterByMode && shell.querySelector(`[data-mode="${mode}"]`) ? `[data-mode="${mode}"]` : '.mcard', 1500); clearSpot(); if (!ok) return false; }
          toFlight(true, `${mode}${f.minIntensity ? ' · I' + f.minIntensity : ''}`);
          for (let k = 0; k < 5 && live(my); k++) { await wait(380); world.burst(); Sound.skim(); }
          toFlight(false); screen('home'); banner('Match complete', 'results'); Sound.success(); return wait(900);
        }
        case 'QuestWaitForIntensityNode': { banner(`${E('GameModes', f.mode)} reached intensity ${f.intensityTier}`, 'gate'); Sound.success(); return wait(1100); }
        case 'QuestWaitForModeUnlockedNode': {
          const m = E('GameModes', f.mode); toFlight(false); screen('profile');
          const sel = shell.querySelector(`[data-quest="${m}"]`) ? `[data-quest="${m}"]` : '.qnode:not(.done)';
          spotlight(sel, .6); const ok = await press(sel, 1600); clearSpot();
          if (ok) { const q = shell.querySelector(sel); q.classList.add('done'); q.querySelector('small').textContent = 'Claimed'; Sound.success(); banner(`${m} unlocked`, 'unlock'); await wait(900); }
          return ok;
        }
        case 'QuestWaitForUserActionNode': {
          const a = E('UserActionType', f.action), sel = `[data-ctl="${ACTION_TARGET[a] || 'nav-home'}"]`;
          toFlight(false);
          spotlight(sel, .55); const ok = await press(sel, 1500); clearSpot();
          if (ok) { const nav = root.querySelector(sel)?.dataset.nav; if (nav) screen(nav); if (a === 'UnlockVessel') { screen('hangar'); toast('Vessel unlocked'); } }
          return ok;
        }
        case 'QuestWaitForLessonNode': { toFlight(true, 'Lesson'); for (let k = 0; k < 4 && live(my); k++) { await wait(400); world.burst(); } toFlight(false); toast('Lesson complete'); return live(my); }
        case 'QuestPhaseEndNode': { closeComms(); clearSpot(); banner('Phase complete', 'end'); Sound.phase(); return wait(1400); }
        case 'QuestEndNode': { closeComms(); clearSpot(); banner('FTUE complete', 'end'); Sound.phase(); return wait(1800); }
      }
      return wait(300);
    }
    function banner(text, kind) { const b = $('.banner'); b.textContent = text; b.dataset.kind = kind; show(b, true); b.classList.remove('in'); void b.offsetWidth; b.classList.add('in'); clearTimeout(b._h); b._h = setTimeout(() => show(b, false), 1500 / speed); }

    function reset() {
      run++; resolver = null; closeComms(); clearSpot(); show($('.instr'), false); show($('.banner'), false); show($('.skip'), false);
      Object.assign(sim, { navLocked: false, allowed: null, buttons: {} }); applyLocks(); toFlight(false); screen('home');
      shell.querySelectorAll('.qnode').forEach((q, i) => { q.classList.toggle('done', i === 0); q.querySelector('small').textContent = i ? 'Claim' : 'Claimed'; });
    }
    async function loop(from) {
      reset(); const my = run; playing = true; setBtn();
      Sound.unlock(); Sound.ambient(true);
      const path = chainOf();
      for (idx = from; idx < path.length; idx++) {
        const n = path[idx];
        renderScrub(idx); bar(n); api.onStep(n.nodeId);
        if (!n.enabled) { await wait(150); continue; }
        const ok = await play(n);
        if (!ok || !live(my)) return;
        const e = n.outputs.find(o => o.port === 'next');
        if (e && e.delay) { $('.nowlabel').insertAdjacentHTML('beforeend', ` <em>wait ${e.delay}s</em>`); if (!(await wait(e.delay * 1000))) return; }
      }
      playing = false; setBtn(); renderScrub(path.length); api.onStep(null); Sound.ambient(false);
      $('.nowlabel').textContent = 'Finished. Press ⏮ to play it again.';
    }
    function stop() { run++; resolver = null; playing = false; setBtn(); Sound.ambient(false); try { tts && tts.stop(); } catch (e) {} }
    function setBtn() { const b = root.querySelector('[data-sim="play"]'); b.textContent = playing ? '❚❚' : '▶'; b.title = playing ? 'Stop (Space)' : 'Play (Space)'; }

    root.querySelector('.simbar').addEventListener('click', e => {
      const b = e.target.closest('button'); if (!b) return;
      if (b.dataset.sim === 'play') { if (playing) stop(); else loop(api.startIndex()); }
      if (b.dataset.sim === 'restart') loop(0);
      if (b.dataset.sim === 'step') { if (resolver) resolver(); else if (!playing) loop(api.startIndex()); }
      if (b.dataset.jump) loop(Number(b.dataset.jump));
    });
    $('.speed').addEventListener('change', e => { speed = Number(e.target.value) || 1; });
    $('.snd').addEventListener('change', e => { Sound.on = e.target.checked; if (playing && e.target.checked) Sound.ambient(true); });
    $('.voice').addEventListener('change', e => { voiceOn = e.target.checked; if (!voiceOn) try { tts && tts.stop(); } catch (err) {} });
    root.addEventListener('keydown', e => { if (e.key === ' ' && !e.target._ref && !/INPUT|SELECT|TEXTAREA/.test(e.target.tagName)) { e.preventDefault(); root.querySelector('[data-sim="play"]').click(); } });

    return {
      refresh() { if (!playing) { renderScrub(-1); bar(null); } },
      stop, play: i => loop(i || 0), get playing() { return playing; },
    };
  }

  window.FTUESim = { Sea, Sound, Player };
})();
