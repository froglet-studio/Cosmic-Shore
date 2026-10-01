"""The research viewer's LIVE swarm section: a swarm you seed and carve, simulated in the browser.

build_viewer.py calls build_live(results_root) and appends the returned (section_html, script, css) after
the swarm gallery. The simulation is swarm_live.js (a line-for-line port of the hgrid grid-morphogen
oracle, verified against the Python scorer by live_export.py; see results/live/NOTE.md). The tadpoles are
drawn by swarm_model.js, the same generator that builds the targets.

Unlike the gallery above it, nothing here is precomputed: every step runs in the page.
"""
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
EL_UI = ["#e8a93a", "#8e6bd8", "#3a7bdc", "#2fb39a"]
NAMES = {"mass": "Whale", "space": "Jellyfish", "charge": "Pufferfish", "time": "Dragonfly"}

MODELS = [
    dict(id="hgrid2", label="Grid morphogen, round 2 (hgrid2)", engine="hgrid2",
         summary=("The round-1 grid plus a fine field at the scale of one tadpole. The most accurate body on the shelf: "
                  "loss 1-3 to its own plan (round 1: 8-18), every standard switch under the bar."),
         about=[
             ("Where a tadpole goes", "Two layers. A coarse 16x16x16 grid (cells of 6 voxels) rides on the swarm and holds the plan's "
              "wanted density for every element and domain: it gets the outline and the composition right. On top, each class "
              "(element + domain) has a fine field: a soft bump for every unit of that class in the plan, minus a bump for every live "
              "tadpole of that class. A tadpole climbs its own class's field, so it slides into the nearest HOLE of its own kind. "
              "Nobody is assigned a site; a site is claimed by being occupied, so two tadpoles wanting one hole jostle and one moves "
              "on. It also moves with its neighbouring units' animation (feed-forward), and a tadpole stranded where its class is "
              "not wanted at all swims straight for the best hole of its class (a migrant)."),
             ("When you kill the majority", "The runner-up element becomes the majority and the plan switches - but a plan, once "
              "committed, holds for 60 steps, so a lead that lasts a moment does not flip it. Then the grid and the fine field "
              "re-sort the survivors into the new creature."),
             ("Does anything die on its own", "Yes. A class the swarm holds more of than the new plan wants STARVES: hunger "
              "builds at a per-tadpole rate and the misfits wither to lime crystals one at a time, never more than the excess "
              "and never an element below the plan's own count of it."),
             ("Watch for", "A loose school that crystallises into a crisp, correctly coloured body as it fills up (the fine "
              "gain grows with how full the body is); the dragonfly at half tempo; after a cull, the lock holding the old "
              "creature for a few seconds before it gives way, then a stream of crystals as the surplus starves."),
         ]),
    dict(id="sort", label="Cell sorting (sort)", engine="sort",
         summary=("No grid and no network: 17 tuned numbers. Accurate (loss 1-4 to its own plan) and lossless - "
                  "nothing ever dies on a clock or of hunger; a misfit changes element instead."),
         about=[
             ("Where a tadpole goes", "Each tadpole knows its TYPE (its element, plus which body region its domain plays in the "
              "current plan), a handful of 'morphogen wells' for that type laid out around the swarm's centre (a French-flag "
              "code), and its neighbours. A newborn commits to the one well its type has fewest tadpoles in, and climbs to it. "
              "Neighbours push apart (collision), unlike types push apart harder than like ones (differential adhesion, so "
              "tissues pack and their borders sharpen), and two touching tadpoles that would each sit better in the other's "
              "spot slide past one another."),
             ("When you kill the majority", "The new majority must lead for 12 steps before the plan switches. Then every "
              "surplus tadpole MOLTS: it re-forms its crystal into an element its region is short of (its domain never "
              "changes), and one whose region is full may transfer to the neediest region. The headcount is capped at the "
              "plan's size, so a big body cut down to a small plan simply re-forms."),
             ("Does anything die on its own", "Never. Only what you kill dies. Mass is conserved: a misfit is re-formed, not "
              "starved, so the crystal counter only moves when you carve, bite, graze or strike."),
             ("Watch for", "Sharp boundaries between colours inside the body; a wound healing exactly where it was cut "
              "(newborns are fated to the emptiest wells, which are the hole); tadpoles flickering to a new element during a "
              "switch instead of crystals appearing; the body re-centring as you carve one side off (the code follows the "
              "centroid)."),
         ]),
    dict(id="grid", label="Grid morphogen, round 1", engine="grid",
         summary="The first grid oracle: right outline and size, but the inside of the body is left to chance (loss 8-18).",
         about=[
             ("Where a tadpole goes", "A coarse 16x16x16 grid rides on the swarm. The majority element picks the plan, and "
              "the grid holds that plan's wanted density for every element and domain, cell by cell. Every tadpole climbs the "
              "gradient of its own class's deficit (wanted minus present) and drifts with the plan's own motion. A cell is 6 "
              "voxels and blurred over ~3, so inside it every class looks alike: the outline is right, which colour sits where "
              "inside the body is left to chance (that is what round 2 adds)."),
             ("When you kill the majority", "The plan flips in the single step another element leads, and the swarm "
              "re-sorts into the new creature. It ignores grazing (it breeds back faster than any steady predator eats) but "
              "one big Bite flips it."),
             ("Does anything die on its own", "Yes. A class the body has no room for, sitting where it is not wanted, starves "
              "and withers to a lime crystal - a whole class can go at once."),
             ("Watch for", "A scattered seed condensing toward its centre and blooming from the inside out at the plan's own "
              "size; Time runners lapping the body; stragglers of unwanted classes withering one after another."),
         ]),
    dict(id="evo", label="Evolved rule (G2 + genome)", engine="evo",
         summary="A learned network in every tadpole, no plan in its head. Organic and always moving, but only suggests the creature (loss 15-25).",
         about=[
             ("Where a tadpole goes", "Every tadpole runs the same small learned network (G2, 232-192-192-35, trained by "
              "backprop) on what it senses within 8 voxels, plus the swarm's headcount and element mix. No grid and no plan "
              "anywhere: where it goes is whatever the network learned."),
             ("When you kill the majority", "A genome found by CMA-ES adds a homeostat: a parent lays more when its element is "
              "short of the share the CURRENT majority's plan wants, and about a quarter of eggs take the element the swarm is "
              "most short of. So after you eat the majority it floods the new majority's element and the cloud slowly re-sorts. "
              "It shrugs off one big bite (it refills its 280 slots in a few steps) but a steady Graze of 4 wears most plans down "
              "in a few hundred steps - the opposite of the grid."),
             ("Does anything die on its own", "Rarely: the network can raise its own death channel, and an egg nobody is near "
              "is lost. Nothing is culled by a rule."),
             ("Watch for", "A loose, always-moving cloud that fills the 280-tadpole budget; Space bodies combing into "
              "near-parallel rods after a switch. Heavier to compute (about 30 steps/s), so keep the speed moderate."),
         ]),
]


def _targets(results_root):
    p = os.path.join(results_root, "live", "targets.json")
    if not os.path.isfile(p):
        import live_export
        os.makedirs(os.path.dirname(p), exist_ok=True)
        json.dump(live_export.targets_json(), open(p, "w"), separators=(",", ":"))
    return open(p).read()


def _evo(results_root):
    p = os.path.join(results_root, "live", "evo_rule.json")
    if not os.path.isfile(p):
        try:
            import live_export
            json.dump(live_export.evo_json(), open(p, "w"), separators=(",", ":"))
        except Exception as e:  # the evolved model is optional: the page still runs the grid
            print("viewer_live: no evolved rule:", e)
            return "null"
    return open(p).read()


def _sort(results_root):
    p = os.path.join(results_root, "live2", "sort_code.json")
    if not os.path.isfile(p):
        try:
            import live_export
            os.makedirs(os.path.dirname(p), exist_ok=True)
            json.dump(live_export.sort_json(), open(p, "w"), separators=(",", ":"))
        except Exception as e:  # optional: the page still runs the other models
            print("viewer_live: no sort code:", e)
            return "null"
    return open(p).read()


def build_live(results_root):
    """(section_html, script, css) for the live swarm. Empty strings if the port is missing."""
    src = os.path.join(HERE, "swarm_live.js")
    if not os.path.isfile(src):
        return "", "", ""
    import prism_render as pr
    pal = np.round(pr.palette_colours()[:, :3], 4).tolist()
    models = "".join(f'<button type="button" class="app" data-lmodel="{m["id"]}" aria-pressed="false">'
                     f'<span class="appl">{m["label"]}</span></button>' for m in MODELS)
    els = "".join(f'<label class="lchip"><input type="radio" name="lfilt" value="{e}"{" checked" if e == -1 else ""}>'
                  f'<span>{"" if e < 0 else f"<i style=background:{EL_UI[e]}></i>"}{n}</span></label>'
                  for e, n in [(-1, "All"), (0, "Charge"), (1, "Mass"), (2, "Space"), (3, "Time")])
    bias = "".join(f'<option value="{v}">{n}</option>' for v, n in
                   [(-1, "Random mix"), (1, "Mostly Mass"), (2, "Mostly Space"), (0, "Mostly Charge"), (3, "Mostly Time")])
    bench = f"""
  <section class="benchp" aria-labelledby="hlive" id="live">
    <div class="h3dhead">
      <div class="eyebrow">Live · seed it, carve it, watch it decide</div>
      <h2 id="hlive">A live swarm you can eat</h2>
      <p class="caption">Everything above was recorded. This one runs in the page, now. Press <strong>New random seed</strong> for a scatter of tadpoles with a random element mix and random domains; the majority element picks the creature. Drag across it to kill every tadpole under the brush (each leaves a lime crystal that fades). Filter the brush to one element and eat the majority: the swarm will change species. Hold Shift (or right-drag, or switch to Orbit) to turn the view; scroll to zoom. <strong>Graze</strong> sets a predator eating the current majority every step; <strong>Bite</strong> takes one big mouthful (just enough that the runner-up leads). One finding to try: a grown body shrugs off grazing - it breeds back faster than you eat - but a single big bite flips it.</p>
      <h3 class="apph">Model</h3>
      <div class="appgrid" id="lmodels">{models}</div>
      <div class="labout" id="labout"></div>
    </div>
    <div class="bench">
      <div class="dish">
        <div class="plate plate3d" id="platel"><canvas id="cvl" width="560" height="560" aria-label="Live tadpole swarm: drag to carve, shift-drag to orbit"></canvas>
          <div class="lbrush" id="lbrush" aria-hidden="true"></div><span class="hint" id="lhint">drag to carve · shift-drag to orbit</span>
          <div class="lflash" id="lflash" aria-live="polite"></div></div>
        <label class="row" for="lspeed">Speed<input id="lspeed" type="range" min="1" max="40" value="12"><output id="o-lspeed">12</output></label>
        <label class="row" for="lbr">Brush<input id="lbr" type="range" min="1.5" max="16" step="0.5" value="5"><output id="o-lbr">5</output></label>
        <div class="lchips" role="radiogroup" aria-label="Brush kills">{els}</div>
      </div>
      <div class="panel">
        <dl class="readouts">
          <div><dt>Step</dt><dd id="rl-step">0</dd></div>
          <div><dt>Tadpoles</dt><dd id="rl-n">0</dd></div>
          <div><dt>Crystals</dt><dd id="rl-cr">0</dd></div>
          <div><dt>Molts</dt><dd id="rl-molt">-</dd></div>
          <div><dt>Steps/s</dt><dd id="rl-rate">0</dd></div>
        </dl>
        <div class="lplan"><span class="lplanl">Becoming</span> <strong id="rl-plan">-</strong> <span class="lmaj" id="rl-maj"></span></div>
        <div class="swmix" id="lmix" aria-label="Element mix, now (top) and the plan's (bottom)"></div>
        <div class="buttons">
          <button type="button" class="btn primary" id="bl-seed">New random seed</button>
          <button type="button" class="btn" id="bl-play">Pause</button>
          <button type="button" class="btn" id="bl-orbit" aria-pressed="false">Orbit</button>
          <button type="button" class="btn" id="bl-spin" aria-pressed="false">Auto-orbit</button>
          <button type="button" class="btn" id="bl-eat">Bite: majority to second place</button>
          <button type="button" class="btn" id="bl-strike">Vessel strike</button>
        </div>
        <label class="row" for="lgraze">Graze<input id="lgraze" type="range" min="0" max="4" step="0.25" value="0"><output id="o-lgraze">0</output></label>
        <label class="row" for="ln">Seed size<input id="ln" type="range" min="8" max="64" value="24"><output id="o-ln">24</output></label>
        <label class="row" for="lbias">Seed bias<select id="lbias">{bias}</select></label>
        <details class="ltune" data-engine="hgrid2"><summary>Tuning (hgrid2; defaults are the verified model)</summary>
          <label class="row" for="lt-hgrid2-G">Grid<select id="lt-hgrid2-G"><option value="16">16 cells (verified)</option><option value="12">12 cells (faster)</option></select></label>
          <label class="row" for="lt-hgrid2-k_fine">Fine sorting<input id="lt-hgrid2-k_fine" data-cfg="k_fine" type="range" min="0" max="4" step="0.25" value="2"><output id="o-lt-hgrid2-k_fine">2</output></label>
          <label class="row" for="lt-hgrid2-k_ff">Feed-forward<input id="lt-hgrid2-k_ff" data-cfg="k_ff" type="range" min="0" max="3" step="0.25" value="1.5"><output id="o-lt-hgrid2-k_ff">1.5</output></label>
          <label class="row" for="lt-hgrid2-lock">Plan lock<input id="lt-hgrid2-lock" data-cfg="lock" type="range" min="0" max="120" step="5" value="60"><output id="o-lt-hgrid2-lock">60</output></label>
          <label class="row" for="lt-hgrid2-p_lay">Regrowth<input id="lt-hgrid2-p_lay" data-cfg="p_lay" type="range" min="0" max="0.3" step="0.01" value="0.1"><output id="o-lt-hgrid2-p_lay">0.1</output></label>
          <label class="row" for="lt-hgrid2-k_mig">Migrants<input id="lt-hgrid2-k_mig" data-cfg="k_mig" type="range" min="0" max="2" step="0.25" value="1"><output id="o-lt-hgrid2-k_mig">1</output></label>
          <label class="row" for="lt-hgrid2-starve">Starvation<input id="lt-hgrid2-starve" data-cfg="starve" type="range" min="0" max="1" step="1" value="1"><output id="o-lt-hgrid2-starve">1</output></label>
          <p class="caption">Fine sorting is the pull of a tadpole into the nearest hole of its own class (0 = round 1 plus the new starvation); feed-forward is how much it moves with its neighbouring units' animation; plan lock is how many steps a new plan holds before another can replace it; regrowth is the laying probability at full deficit; migrants is how fast a stranded tadpole swims for its class's best hole; starvation lets misfits wither. The grid choice re-seeds.</p>
        </details>
        <details class="ltune" data-engine="sort"><summary>Tuning (cell sorting; defaults are the verified model)</summary>
          <label class="row" for="lt-sort-k_well">Well pull<input id="lt-sort-k_well" data-scfg="k_well" type="range" min="0" max="1" step="0.01" value="0.41"><output id="o-lt-sort-k_well">0.41</output></label>
          <label class="row" for="lt-sort-a_other">Tension<input id="lt-sort-a_other" data-scfg="a_other" type="range" min="-0.15" max="0.05" step="0.005" value="-0.065"><output id="o-lt-sort-a_other">-0.065</output></label>
          <label class="row" for="lt-sort-inertia">Inertia<input id="lt-sort-inertia" data-scfg="inertia" type="range" min="0" max="0.9" step="0.01" value="0.69"><output id="o-lt-sort-inertia">0.69</output></label>
          <label class="row" for="lt-sort-dwell">Dwell<input id="lt-sort-dwell" data-scfg="dwell" type="range" min="1" max="40" step="1" value="12"><output id="o-lt-sort-dwell">12</output></label>
          <label class="row" for="lt-sort-molt_rate">Molting<input id="lt-sort-molt_rate" data-scfg="molt_rate" type="range" min="0" max="0.15" step="0.005" value="0.03"><output id="o-lt-sort-molt_rate">0.03</output></label>
          <label class="row" for="lt-sort-lay_rate">Lay rate<input id="lt-sort-lay_rate" data-scfg="lay_rate" type="range" min="0.02" max="0.2" step="0.005" value="0.085"><output id="o-lt-sort-lay_rate">0.085</output></label>
          <label class="row" for="lt-sort-transfer">Region transfer<input id="lt-sort-transfer" data-scfg="transfer" type="range" min="0" max="1" step="1" value="1"><output id="o-lt-sort-transfer">1</output></label>
          <p class="caption">Well pull is how hard a tadpole climbs toward its fated well; tension is how hard UNLIKE neighbours push apart (more negative = sharper tissue borders); inertia is how much velocity carries over; dwell is how many steps a new majority must lead before the plan switches; molting is the per-step chance a surplus tadpole re-forms into a needed element; lay rate is eggs per step as a share of the headcount; region transfer lets a misfit join another body region.</p>
        </details>
        <details class="ltune" data-engine="grid"><summary>Tuning (round-1 grid; defaults are the verified oracle)</summary>
          <label class="row" for="lt-grid-p_lay">Regrowth<input id="lt-grid-p_lay" data-cfg="p_lay" type="range" min="0" max="0.3" step="0.01" value="0.1"><output id="o-lt-grid-p_lay">0.1</output></label>
          <label class="row" for="lt-grid-p_cross">Cross-breed<input id="lt-grid-p_cross" data-cfg="p_cross" type="range" min="0" max="0.6" step="0.05" value="0.25"><output id="o-lt-grid-p_cross">0.25</output></label>
          <label class="row" for="lt-grid-hyst">Hysteresis<input id="lt-grid-hyst" data-cfg="hyst" type="range" min="0" max="0.3" step="0.01" value="0"><output id="o-lt-grid-hyst">0</output></label>
          <label class="row" for="lt-grid-starve">Starvation<input id="lt-grid-starve" data-cfg="starve" type="range" min="0" max="1" step="1" value="1"><output id="o-lt-grid-starve">1</output></label>
          <p class="caption">Regrowth is the laying probability at full deficit; cross-breed is the share of eggs that take the element the parent's domain is most short of (it can feed the majority, which is why grazing alone rarely flips the creature); hysteresis is the lead another element needs, as a share of the headcount, before the plan changes; starvation lets misfits wither. Changes apply to the running swarm.</p>
        </details>
        <p class="census mono" id="rl-seed"></p>
        <ol class="llog mono" id="rl-log" aria-label="What happened"></ol>
      </div>
    </div>
  </section>"""
    data = dict(models=MODELS, names=NAMES)
    script = (SCRIPT.replace("/*SL*/", open(src).read())
              .replace("/*SM*/", open(os.path.join(HERE, "swarm_model.js")).read())
              .replace("/*TARGETS*/", _targets(results_root)).replace("/*EVO*/", _evo(results_root))
              .replace("/*SORT*/", _sort(results_root))
              .replace("/*LDATA*/", json.dumps(data, separators=(",", ":")))
              .replace("/*PALETTE*/", json.dumps(pal)).replace("/*ELUI*/", json.dumps(EL_UI)))
    return bench, script, CSS


CSS = """
.lbrush { position: absolute; border: 1.5px solid var(--accent); border-radius: 50%; pointer-events: none; display: none;
  transform: translate(-50%, -50%); box-shadow: 0 0 0 1px rgba(255,255,255,.25) inset }
.lflash { position: absolute; right: 10px; top: 8px; font: 600 13px var(--mono); color: var(--accent); pointer-events: none;
  opacity: 0; transition: opacity .6s }
.lflash.on { opacity: 1; transition: none }
.lchips { display: flex; flex-wrap: wrap; gap: 6px; font-size: 13px }
.lchip input { position: absolute; opacity: 0 }
.lchip span { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border: 1px solid var(--rule); border-radius: 999px; cursor: pointer }
.lchip i { width: 9px; height: 9px; border-radius: 50% }
.lchip input:checked + span { border-color: var(--accent); box-shadow: inset 0 0 0 1px var(--accent) }
.lchip input:focus-visible + span { outline: 2px solid var(--accent); outline-offset: 2px }
.lplan { font-size: 17px } .lplanl, .lmaj { color: var(--muted); font-size: 14px }
.llog { margin: 0; padding-left: 1.4em; font-size: 12.5px; color: var(--muted); max-height: 9.5em; overflow: auto }
#platel.orbit { cursor: grab }
.labout { max-width: 72ch } .labout p { margin: 0 0 6px } .labout dl { margin: 0; display: grid; gap: 4px }
.labout dt { font-weight: 600; font-size: 14px; margin-top: 4px } .labout dd { margin: 0; color: var(--muted); font-size: 14px; line-height: 1.45 }
.ltune summary { cursor: pointer; font-size: 14px; color: var(--muted) } .ltune { display: grid; gap: 8px }
"""

SCRIPT = r"""<script>
(function () {
  if (typeof THREE === 'undefined') return;
  const SL = (function () { const module = { exports: {} }, exports = module.exports;
/*SL*/
    ; return module.exports; })();
  const SM = (function () { const module = { exports: {} }, exports = module.exports;
/*SM*/
    ; return module.exports; })();
  const TARGETS = /*TARGETS*/, EVO = /*EVO*/, SORT = /*SORT*/, D = /*LDATA*/, PALETTE = /*PALETTE*/, EL_UI = /*ELUI*/;
  const KINDS = SL.KINDS, ELN = ['Charge', 'Mass', 'Space', 'Time'], NAMES = D.names;
  const $ = id => document.getElementById(id), cv = $('cvl'), plate = $('platel');
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---- three.js scene (the gallery's look) -------------------------------------------------
  const renderer = new THREE.WebGLRenderer({ canvas: cv, antialias: true });
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  renderer.outputEncoding = THREE.sRGBEncoding;
  const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(30, 1, 0.5, 1500), world = new THREE.Group();
  scene.add(world);
  scene.add(new THREE.HemisphereLight(0xffffff, 0x445066, 0.8));
  const sun = new THREE.DirectionalLight(0xffffff, 0.75); sun.position.set(30, 60, 40); scene.add(sun);
  function hull(pts) {
    pts = pts.map(p => new THREE.Vector3(...p).normalize());
    const pos = [], eps = 1e-6;
    for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++) for (let k = j + 1; k < pts.length; k++) {
      const n = new THREE.Vector3().subVectors(pts[j], pts[i]).cross(new THREE.Vector3().subVectors(pts[k], pts[i]));
      if (n.lengthSq() < 1e-10) continue;
      let a = 0, b = 0;
      for (const q of pts) { const d = n.dot(new THREE.Vector3().subVectors(q, pts[i])); if (d > eps) a++; else if (d < -eps) b++; if (a && b) break; }
      if (a && b) continue;
      (a ? [pts[i], pts[k], pts[j]] : [pts[i], pts[j], pts[k]]).forEach(v => pos.push(v.x, v.y, v.z));
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.computeVertexNormals(); return g;
  }
  function perms(base, even) {
    const out = [], P3 = even ? [[0, 1, 2], [1, 2, 0], [2, 0, 1]] : [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
    for (const b of base) for (const p of P3) for (let s = 0; s < 8; s++) {
      const v = p.map((ix, i) => b[ix] * ((s >> i) & 1 ? -1 : 1));
      if (!out.some(o => Math.abs(o[0] - v[0]) + Math.abs(o[1] - v[1]) + Math.abs(o[2] - v[2]) < 1e-9)) out.push(v);
    }
    return out;
  }
  const PHI = (1 + Math.sqrt(5)) / 2;
  const GEO = [hull(perms([[0, 1, 3 * PHI], [1, 2 + PHI, 2 * PHI], [PHI, 2, 2 * PHI + 1]], true)), new THREE.IcosahedronGeometry(1, 1), new THREE.DodecahedronGeometry(1, 0), hull(perms([[0, 1, 2]], false))];
  const CAP = 400, MAXS = CAP * 8, MAXD = 900;
  const mat = new THREE.MeshStandardMaterial({ roughness: 0.45, metalness: 0.06, flatShading: true });
  const gem = new THREE.MeshStandardMaterial({ roughness: 0.2, metalness: 0.1, flatShading: true, emissive: 0x2a3446 });
  const inst = (geo, m, n) => { const x = new THREE.InstancedMesh(geo, m, n); x.instanceMatrix.setUsage(THREE.DynamicDrawUsage); x.setColorAt(0, new THREE.Color(1, 1, 1)); x.count = 0; world.add(x); return x; };
  const boxes = inst(new THREE.BoxGeometry(2, 2, 2), mat, CAP), octas = inst(new THREE.OctahedronGeometry(SM.SHIELD), mat, CAP), gems = GEO.map(g => inst(g, gem, CAP));
  const limes = GEO.map(g => inst(g, new THREE.MeshStandardMaterial({ roughness: 0.25, flatShading: true, emissive: 0x2c4a10 }), MAXD));
  const cyl = new THREE.CylinderGeometry(1, 1, 1, 6, 1, true); cyl.translate(0, 0.5, 0);
  const spMesh = new THREE.InstancedMesh(cyl, new THREE.MeshStandardMaterial({ roughness: 0.6, color: new THREE.Color(0.6, 0.72, 0.86).convertSRGBToLinear() }), MAXS);
  spMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); spMesh.count = 0; world.add(spMesh);
  const lin = c => new THREE.Color(...c).convertSRGBToLinear();
  const HEART = lin([0.42, 0.62, 1.0]), LIME = lin([0.62, 0.92, 0.22]), ELC = EL_UI.map(h => new THREE.Color(h).convertSRGBToLinear());
  const PAL = PALETTE.map(d => d.map(lin));
  const M = new THREE.Matrix4(), Q = new THREE.Quaternion(), V = new THREE.Vector3(), Sv = new THREE.Vector3(), UP = new THREE.Vector3(0, 1, 0), Pv = new THREE.Vector3(), TMP = new THREE.Color();
  let BG = new THREE.Color(1, 1, 1);

  // ---- the simulation ----------------------------------------------------------------------
  const pf = new SL.PlanFields(TARGETS, SL.DEF_CFG), pf2 = {};
  function makeSwarm(seed) {
    if (model.engine === 'evo' && EVO) return new SL.EvoSwarm(TARGETS, EVO, { seed });
    if (model.engine === 'sort' && SORT) return new SL.SortSwarm(TARGETS, SORT, { seed });
    if (model.engine === 'hgrid2') {
      const G = +$('lt-hgrid2-G').value, cfg = Object.assign({}, SL.DEF_CFG, SL.HG2_CFG, { G });
      return new SL.Hgrid2Swarm(TARGETS, { seed, cfg, planFields: pf2[G] || (pf2[G] = new SL.PlanFields(TARGETS, cfg)) });
    }
    return new SL.LiveSwarm(TARGETS, { seed, planFields: pf, capacity: CAP });
  }
  let sw = null, model = D.models[0], seedInfo = null, simT = 0, acc = 0, running = !reduce, rate = 0, rateN = 0, rateT = performance.now();
  const P = Object.assign({}, SM.TARGETS.mass.defaults, { slotMap: [0, 1, 2] });
  let geo = [];                                   // per slot: realised geometry relative to the heart at step time
  const dead = [];                                // lime crystals: {p:[sim], e, t0}
  const perp = f => { const a = Math.abs(f[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0]; const d = a[0] * f[0] + a[1] * f[1] + a[2] * f[2];
    const v = [a[0] - d * f[0], a[1] - d * f[1], a[2] - d * f[2]], L = Math.hypot(...v) || 1; return v.map(x => x / L); };
  const U = {};
  function realiseAll() {
    geo = new Array(sw.N);
    for (let i = 0; i < sw.N; i++) {
      if (!sw.active[i]) continue;
      sw.unit(i, U);
      const p = [sw.pos[3 * i], sw.pos[3 * i + 1], sw.pos[3 * i + 2]];
      const u = { p, f: U.f, n: perp(U.f), elem: U.elem, spindle: { len: U.sp[0], bend: U.sp[1], roll: 0, thick: P.spindleThickness },
        prism: { h: U.h.slice(), tier: U.tier, slot: U.dom, roll: 0 } };
      const z = SM.realise(P, u, i);
      geo[i] = { r: z.crystal.r, e: U.elem, q: z.prism, sp: z.spindle, p };
    }
  }
  function drainEvents() {
    const now = performance.now();
    for (const ev of sw.events) {
      if (ev.type === 'kill' || ev.type === 'death') { if (!ev.egg) dead.push({ p: ev.p, e: ev.elem, t0: now }); }
      else if (ev.type === 'switch') log(`step ${ev.step}: ${NAMES[ev.from]} → ${NAMES[ev.to]}`, true);
    }
    sw.events.length = 0;
    while (dead.length > MAXD) dead.shift();
  }
  // GRAZE: a predator that eats this many random tadpoles of the CURRENT majority every step (a fraction is a
  // per-step chance) - attrition, against the swarm's own regrowth
  function graze() {
    const g = +$('lgraze').value; if (!g) return;
    const q = Math.floor(g) + (Math.random() < g % 1 ? 1 : 0); if (!q) return;
    const c = sw.census(); if (c.majority < 0) return;
    const ids = []; for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i] && sw.elem[i] === c.majority) ids.push(i);
    for (let k = 0; k < q && ids.length; k++) { const j = Math.floor(Math.random() * ids.length); sw.kill(ids[j], 'kill'); ids.splice(j, 1); }
    countKills();
  }
  function stepOnce() { graze(); sw.step(); simT = sw.clock; drainEvents(); rateN++; }

  // ---- drawing -----------------------------------------------------------------------------
  let cen = null, az = 0.75, el = 0.35, dist = 150, spin = false, tintHearts = true;
  const smooth = x => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);
  function draw(alpha) {
    let nb = 0, no = 0, ns = 0; const ng = [0, 0, 0, 0], nl = [0, 0, 0, 0];
    const now = sw.clock + alpha;
    // the camera follows the swarm's (interpolated) centroid
    let m = [0, 0, 0], n = 0;
    for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i]) { for (let k = 0; k < 3; k++) m[k] += sw.prev[3 * i + k] + alpha * (sw.pos[3 * i + k] - sw.prev[3 * i + k]); n++; }
    if (n) { m = m.map(v => v / n); cen = cen ? cen.map((v, k) => v + 0.08 * (m[k] - v)) : m; }
    if (cen) world.position.set(-cen[0], -cen[1], -cen[2]);
    for (let i = 0; i < sw.N; i++) {
      const g = geo[i]; if (!g || !sw.active[i]) continue;
      const ox = sw.prev[3 * i] + alpha * (sw.pos[3 * i] - sw.prev[3 * i]) - g.p[0];
      const oy = sw.prev[3 * i + 1] + alpha * (sw.pos[3 * i + 1] - sw.prev[3 * i + 1]) - g.p[1];
      const oz = sw.prev[3 * i + 2] + alpha * (sw.pos[3 * i + 2] - sw.prev[3 * i + 2]) - g.p[2];
      const s = 0.12 + 0.88 * smooth((now - sw.born[i]) / 5);         // bloom in: nothing pops
      const hx = g.p[0] + ox, hy = g.p[1] + oy, hz = g.p[2] + oz;
      const at = (q, k) => (k === 0 ? hx : k === 1 ? hy : hz) + s * (q[k] - g.p[k]);
      const q = g.q, R9 = q.R, h = q.h, oct = q.tier === 2, mm = oct ? octas : boxes, j = oct ? no++ : nb++;
      M.set(R9[0] * h[0] * s, R9[1] * h[1] * s, R9[2] * h[2] * s, at(q.p, 0), R9[3] * h[0] * s, R9[4] * h[1] * s, R9[5] * h[2] * s, at(q.p, 1),
        R9[6] * h[0] * s, R9[7] * h[1] * s, R9[8] * h[2] * s, at(q.p, 2), 0, 0, 0, 1);
      mm.setMatrixAt(j, M); mm.setColorAt(j, PAL[q.dom][q.tier]);
      const e = g.e, jg = ng[e]++, r = g.r * s;
      M.makeScale(r, r, r); M.setPosition(hx, hy, hz); gems[e].setMatrixAt(jg, M); gems[e].setColorAt(jg, tintHearts ? TMP.copy(HEART).lerp(ELC[e], 0.9) : HEART);
      const pts = g.sp.pts;
      for (let k = 0; k + 1 < pts.length && ns < MAXS; k++) {
        const a = pts[k], b = pts[k + 1];
        V.set((b[0] - a[0]) * s, (b[1] - a[1]) * s, (b[2] - a[2]) * s); const L = V.length(); if (L < 1e-6) continue;
        Q.setFromUnitVectors(UP, V.normalize()); Sv.set(g.sp.r * s, L, g.sp.r * s);
        Pv.set(at(a, 0), at(a, 1), at(a, 2)); M.compose(Pv, Q, Sv); spMesh.setMatrixAt(ns++, M);
      }
    }
    const t = performance.now();
    for (let k = dead.length - 1; k >= 0; k--) {
      const c = dead[k], age = (t - c.t0) / 4500;
      if (age >= 1) { dead.splice(k, 1); continue; }
      const e = c.e, j = nl[e]++, sc = 0.9 * (1 - smooth((age - 0.55) / 0.45));
      M.makeScale(sc, sc, sc); M.setPosition(c.p[0], c.p[1], c.p[2]); limes[e].setMatrixAt(j, M);
      limes[e].setColorAt(j, TMP.copy(LIME).lerp(BG, smooth((age - 0.3) / 0.7)));
    }
    boxes.count = nb; octas.count = no; spMesh.count = ns; gems.forEach((x, e) => x.count = ng[e]); limes.forEach((x, e) => x.count = nl[e]);
    [boxes, octas, spMesh, ...gems, ...limes].forEach(x => { x.instanceMatrix.needsUpdate = true; if (x.instanceColor) x.instanceColor.needsUpdate = true; });
    camera.position.set(Math.cos(el) * Math.cos(az) * dist, Math.sin(el) * dist, Math.cos(el) * Math.sin(az) * dist);
    camera.lookAt(0, 0, 0); renderer.render(scene, camera);
  }

  // ---- readouts ----------------------------------------------------------------------------
  const mixBar = (counts, label) => { const tot = counts.reduce((a, b) => a + b, 0) || 1;
    return `<span class="lab">${label}</span><span class="bar">${counts.map((c, e) => `<i style="width:${100 * c / tot}%;background:${EL_UI[e]}" title="${ELN[e]} ${c}"></i>`).join('')}</span>`; };
  let lastPanel = 0;
  function panel() {
    const c = sw.census(), plan = c.plan;
    $('rl-step').textContent = sw.clock; $('rl-n').textContent = c.n + (c.eggs ? ` +${c.eggs}` : '');
    $('rl-cr').textContent = sw.deathsTotal || 0; $('rl-rate').textContent = rate.toFixed(0);
    $('rl-molt').textContent = sw.molts != null ? sw.molts : '-';
    $('rl-plan').textContent = plan ? NAMES[plan] : (c.n ? '-' : 'extinct');
    $('rl-maj').textContent = c.majority >= 0 ? `(${ELN[c.majority]} majority: ${c.elements.map((v, e) => `${ELN[e][0]}${v}`).join(' ')})` : '';
    $('lmix').innerHTML = mixBar(c.elements, 'now') + (plan ? mixBar(TARGETS[plan].mix, NAMES[plan].toLowerCase() + ' plan') : '');
  }
  function log(msg, flash) {
    const li = document.createElement('li'); li.textContent = msg; $('rl-log').prepend(li);
    while ($('rl-log').children.length > 30) $('rl-log').lastChild.remove();
    if (flash) { const f = $('lflash'); f.textContent = msg.replace(/^step \d+: /, ''); f.classList.add('on'); setTimeout(() => f.classList.remove('on'), 1600); }
  }
  function newSeed() {
    const seed = (Math.random() * 2 ** 31) | 0;
    sw = makeSwarm(seed);
    sw.deathsTotal = 0;
    const bias = +$('lbias').value, n = +$('ln').value;
    seedInfo = sw.seedRandom({ n, bias, radius: 10 + n / 4 });
    dead.length = 0; cen = null; acc = 0; $('rl-log').innerHTML = '';
    const em = seedInfo.elements, dm = seedInfo.domains;
    let maj = 0; for (let e = 1; e < 4; e++) if (em[e] > em[maj]) maj = e;
    const tie = em.filter(v => v === em[maj]).length > 1;
    const plan = SL.KINDS[SL.PLAN_OF[maj]];
    if (model.engine === 'evo') { const sp = $('lspeed'); if (+sp.value > 15) { sp.value = 10; sp.dispatchEvent(new Event('input')); } }
    $('rl-seed').textContent = `Seed: ${n} tadpoles. Elements ${em.map((v, e) => `${ELN[e]} ${v}`).join(', ')}; domains ${['Jade', 'Ruby', 'Gold'].map((d, k) => `${d} ${dm[k] || 0}`).join(', ')}. ` +
      `${ELN[maj]} leads${tie ? ' (tied: the first counts)' : ''}, so it becomes the ${NAMES[plan]}.`;
    log(`seed: ${n} tadpoles, ${ELN[maj]} majority → ${NAMES[plan]}`);
    applyTune(); document.querySelectorAll('.ltune').forEach(d => { d.hidden = d.dataset.engine !== model.engine; });
    realiseAll(); panel();
  }
  // a tadpole killed by the player counts toward the crystal total
  function countKills() { let k = 0; for (const ev of sw.events) if ((ev.type === 'kill' || ev.type === 'death') && !ev.egg) k++; sw.deathsTotal += k; }

  // ---- interaction -------------------------------------------------------------------------
  const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
  let mode = 'carve', drag = null, hover = null, carved = 0;
  function brushRadius() { return +$('lbr').value; }
  function filter() { const r = document.querySelector('input[name=lfilt]:checked'); return r ? +r.value : -1; }
  function carveAt(e) {
    const r = cv.getBoundingClientRect();
    ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const o = ray.ray.origin, d = ray.ray.direction, c = cen || [0, 0, 0];
    const k = sw.killRay([o.x + c[0], o.y + c[1], o.z + c[2]], [d.x, d.y, d.z], brushRadius(), filter());
    if (k) { carved += k; countKills(); drainEvents(); }
    return k;
  }
  function placeBrush(e) {
    const r = cv.getBoundingClientRect(), b = $('lbrush');
    if (!e || mode === 'orbit') { b.style.display = 'none'; return; }
    const px = brushRadius() / (dist * Math.tan(camera.fov * Math.PI / 360)) * (r.height / 2);
    b.style.display = 'block'; b.style.width = b.style.height = (2 * px) + 'px';
    b.style.left = (e.clientX - r.left) + 'px'; b.style.top = (e.clientY - r.top) + 'px';
    const f = filter(); b.style.borderColor = f >= 0 ? EL_UI[f] : '';
  }
  cv.addEventListener('contextmenu', e => e.preventDefault());
  cv.addEventListener('pointerdown', e => {
    cv.setPointerCapture(e.pointerId);
    if (mode === 'orbit' || e.button === 2 || e.shiftKey || e.altKey) drag = { orbit: true, x: e.clientX, y: e.clientY, az, el };
    else { drag = { orbit: false }; carved = 0; carveAt(e); }
  });
  cv.addEventListener('pointermove', e => {
    hover = e; placeBrush(drag && drag.orbit ? null : e);
    if (!drag) return;
    if (drag.orbit) { az = drag.az + (e.clientX - drag.x) * 0.01; el = Math.max(-1.5, Math.min(1.5, drag.el + (e.clientY - drag.y) * 0.01)); }
    else carveAt(e);
  });
  const up = () => { if (drag && !drag.orbit && carved) log(`step ${sw.clock}: carved ${carved}${filter() >= 0 ? ' ' + ELN[filter()] : ''}`); drag = null; carved = 0; };
  cv.addEventListener('pointerup', up); cv.addEventListener('pointercancel', up);
  cv.addEventListener('pointerleave', () => { hover = null; placeBrush(null); });
  cv.addEventListener('wheel', e => { e.preventDefault(); dist = Math.max(30, Math.min(600, dist * Math.exp(e.deltaY * 0.001))); placeBrush(hover); }, { passive: false });
  $('bl-seed').onclick = newSeed;
  $('bl-play').onclick = () => { running = !running; $('bl-play').textContent = running ? 'Pause' : 'Play'; };
  $('bl-orbit').onclick = () => { mode = mode === 'orbit' ? 'carve' : 'orbit'; $('bl-orbit').setAttribute('aria-pressed', mode === 'orbit'); plate.classList.toggle('orbit', mode === 'orbit');
    $('lhint').textContent = mode === 'orbit' ? 'drag to orbit' : 'drag to carve · shift-drag to orbit'; placeBrush(null); };
  $('bl-spin').onclick = () => { spin = !spin; $('bl-spin').setAttribute('aria-pressed', spin); };
  $('bl-eat').onclick = () => {
    // the yardstick's cull (swarm_nca.lose_majority "excess"): eat just enough of the majority, plus a random
    // extra, that the runner-up leads by one - a big, sudden bite (grazing alone rarely flips a body)
    const c = sw.census(); if (c.majority < 0) return;
    let to = -1; for (let e = 0; e < 4; e++) if (e !== c.majority && (to < 0 || c.elements[e] > c.elements[to])) to = e;
    const before = c.elements[c.majority], plan = sw.loseMajority(to, 6);
    countKills(); drainEvents();
    log(plan ? `step ${sw.clock}: ate ${before - sw.census().elements[c.majority]} ${ELN[c.majority]}; ${ELN[to]} leads` : `step ${sw.clock}: nothing to eat (no runner-up with 2+)`);
  };
  $('bl-strike').onclick = () => {
    // swarm_probe's strike: everything inside a sphere one RMS radius across, centred one RMS radius off the centroid
    let m = [0, 0, 0], n = 0;
    for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i]) { for (let k = 0; k < 3; k++) m[k] += sw.pos[3 * i + k]; n++; }
    if (n < 4) return; m = m.map(v => v / n);
    let r2 = 0; for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i]) r2 += (sw.pos[3 * i] - m[0]) ** 2 + (sw.pos[3 * i + 1] - m[1]) ** 2 + (sw.pos[3 * i + 2] - m[2]) ** 2;
    const rms = Math.sqrt(r2 / n); let d; do { d = [Math.random() * 2 - 1, Math.random() * 2 - 1, Math.random() * 2 - 1]; } while (Math.hypot(...d) > 1 || Math.hypot(...d) < 0.1);
    const dl = Math.hypot(...d), k = sw.killBall(m.map((v, j) => v + rms * d[j] / dl), rms, -1);
    countKills(); drainEvents(); log(`step ${sw.clock}: vessel strike took ${k} of ${n}`);
  };
  const tune = [...document.querySelectorAll('[data-cfg],[data-scfg]')];
  function applyTune() {
    if (!sw) return;
    tune.forEach(t => { if (t.closest('.ltune').dataset.engine !== model.engine) return;
      if (t.dataset.scfg) sw.scfg[t.dataset.scfg] = +t.value; else sw.cfg[t.dataset.cfg] = +t.value; });
  }
  $('lt-hgrid2-G').addEventListener('change', () => { if (model.engine === 'hgrid2') newSeed(); });
  tune.forEach(t => { const o = $('o-' + t.id); t.addEventListener('input', () => { o.textContent = t.value; applyTune(); }); });
  for (const id of ['lspeed', 'lbr', 'ln', 'lgraze']) { const o = $('o-' + id); const f = () => o.textContent = $(id).value; $(id).addEventListener('input', f); f(); }
  $('lbr').addEventListener('input', () => placeBrush(hover));
  document.querySelectorAll('input[name=lfilt]').forEach(r => r.addEventListener('change', () => placeBrush(hover)));
  function setModel(id) {
    model = D.models.find(m => m.id === id && (m.engine !== 'evo' || EVO) && (m.engine !== 'sort' || SORT)) || D.models[0];
    const esc = t => t.replace(/&/g, '&amp;').replace(/</g, '&lt;');
    $('labout').innerHTML = `<p><strong>${esc(model.label)}.</strong> ${esc(model.summary)}</p><dl>` +
      model.about.map(([k, v]) => `<dt>${esc(k)}</dt><dd>${esc(v)}</dd>`).join('') + '</dl>';
    document.querySelectorAll('#lmodels [data-lmodel]').forEach(b => b.setAttribute('aria-pressed', b.dataset.lmodel === model.id));
  }
  document.querySelectorAll('#lmodels [data-lmodel]').forEach(b => b.onclick = () => { setModel(b.dataset.lmodel); newSeed(); });

  // ---- loop --------------------------------------------------------------------------------
  let visible = true, lastT = performance.now();
  if ('IntersectionObserver' in window) new IntersectionObserver(es => { visible = es[0].isIntersecting; }).observe(cv);
  let simMs = 0, drawMs = 0;
  function frame(t) {
    const dt = Math.min(0.25, (t - lastT) / 1000); lastT = t;
    if (visible) {
      if (running) {
        // the simulation keeps its own clock: a slow frame takes several steps (up to a 40 ms budget)
        acc += dt * +$('lspeed').value;
        let k = 0; const t0 = performance.now();
        while (acc >= 1 && (k === 0 || performance.now() - t0 < 40)) { const before = sw.deaths; stepOnce(); sw.deathsTotal += sw.deaths - before; acc -= 1; k++; }
        if (k) { simMs = 0.9 * simMs + 0.1 * (performance.now() - t0) / k; realiseAll(); }
        if (acc > 2) acc = 1;
      }
      if (spin) az += dt * 0.2;
      const t1 = performance.now();
      draw(running ? Math.min(acc, 1) : 1);
      drawMs = 0.9 * drawMs + 0.1 * (performance.now() - t1);
      if (t - lastPanel > 200) { panel(); lastPanel = t; }
      if (t - rateT > 1000) { rate = rateN * 1000 / (t - rateT); rateN = 0; rateT = t; }
    }
    requestAnimationFrame(frame);
  }
  function resize() { const w = cv.clientWidth || 560, h = cv.clientHeight || 560; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }
  function theme() { BG = new THREE.Color(getComputedStyle(document.documentElement).getPropertyValue('--dish').trim() || '#ffffff'); scene.background = BG.clone(); BG.convertSRGBToLinear(); }
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', theme);
  new MutationObserver(theme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  new ResizeObserver(resize).observe(cv);
  if (!running) $('bl-play').textContent = 'Play';
  const screenOf = i => { const c = cen || [0, 0, 0], r = cv.getBoundingClientRect();
    const v = new THREE.Vector3(sw.pos[3 * i] - c[0], sw.pos[3 * i + 1] - c[1], sw.pos[3 * i + 2] - c[2]).project(camera);
    return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height]; };
  window.liveSwarm = { get sw() { return sw; }, newSeed, setModel, get model() { return model.id; }, screenOf, setRunning: v => { running = v; }, step: stepOnce, realiseAll, carveAt, get rate() { return rate; }, get simMs() { return simMs; }, get drawMs() { return drawMs; },
    camera, get cen() { return cen; }, get dead() { return dead.length; } };
  setModel(D.models[0].id); theme(); resize(); newSeed(); requestAnimationFrame(frame);
})();
</script>"""
