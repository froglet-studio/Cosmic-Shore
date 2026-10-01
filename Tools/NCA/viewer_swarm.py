"""The research viewer's swarm section: one rule, four body plans, played back from rollouts.

build_viewer.py calls build_swarm(results_root). It reads results/swarm_coevo/{rollout,summary}.json
(written by `swarm_nca.py rollout`) and the training log, and returns (section_html, script). The
recorded units are realised in the browser by swarm_model.js, the same generator that builds the
targets, so the grown swarm and its target are drawn by identical code.
"""
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
KINDS = ("mass", "space", "charge", "time")
NAMES = {"mass": "Whale", "space": "Jellyfish", "charge": "Pufferfish", "time": "Dragonfly"}
ELEMENTS = ("Charge", "Mass", "Space", "Time")
MAJOR = {"mass": "Mass", "space": "Space", "charge": "Charge", "time": "Time"}
EL_UI = ["#e8a93a", "#8e6bd8", "#3a7bdc", "#2fb39a"]


def _curve(log_path):
    """Inline SVG: the summed four-plan loss over training (50-point moving mean)."""
    if not os.path.isfile(log_path):
        return ""
    rows = [json.loads(l) for l in open(log_path) if l.strip()]
    if len(rows) < 3:
        return ""
    st = np.array([r["step"] for r in rows], float)
    per = {k: np.array([r[k]["sink"] for r in rows], float) for k in KINDS if k in rows[0]}   # a specialist logs one plan
    tot = sum(per.values())
    W, H, L, R, T, B = 640, 220, 48, 16, 14, 30
    k = max(1, min(25, len(tot) // 8))
    sm = lambda a: np.convolve(a, np.ones(k) / k, mode="valid")
    xs = st[k - 1:]
    hi = float(np.ceil(sm(tot).max() / 50) * 50) or 1.0
    sx = lambda s: L + (W - L - R) * s / max(1, st[-1])
    sy = lambda v: T + (H - T - B) * (1 - v / hi)
    out = [f'<svg viewBox="0 0 {W} {H}" role="img" aria-label="Summed four-plan loss over training">']
    for v in np.linspace(0, hi, 5):
        out.append(f'<line x1="{L}" x2="{W-R}" y1="{sy(v):.1f}" y2="{sy(v):.1f}" class="grid"/>'
                   f'<text x="{L-8}" y="{sy(v)+4:.1f}" class="tick" text-anchor="end">{v:.0f}</text>')
    for s in np.linspace(0, st[-1], 5):
        out.append(f'<text x="{sx(s):.1f}" y="{H-10}" class="tick" text-anchor="middle">{int(s)}</text>')
    pts = " ".join(f"{sx(x):.1f},{sy(y):.1f}" for x, y in zip(xs, sm(tot)))
    out.append(f'<polyline points="{pts}" class="series" style="stroke:var(--ink)"/>')
    for kk, c in zip(KINDS, (EL_UI[1], EL_UI[2], EL_UI[0], EL_UI[3])):
        if kk not in per:
            continue
        pts = " ".join(f"{sx(x):.1f},{sy(y):.1f}" for x, y in zip(xs, sm(per[kk])))
        out.append(f'<polyline points="{pts}" class="series" style="stroke:{c};stroke-width:1.4;opacity:.85"/>')
    out.append("</svg>")
    return "".join(out)


def _eval16(d):
    """The tiered all-transition evaluation (swarm_eval: 4 own plans + 12 switches), if it was run."""
    p = os.path.join(d, "eval16.json")
    if os.path.isfile(p):
        return json.load(open(p))
    return json.load(open(os.path.join(d, "summary.json"))).get("eval16")


def _rank(d):
    """Runs scored on all 16 transitions first (most passed), then the original eight tests; ties by
    the lowest divergence over the original eight."""
    sm = json.load(open(os.path.join(d, "summary.json")))
    import swarm_nca
    correct, close = swarm_nca.tests_passed(sm)
    ev = _eval16(d)
    if not ev:
        return (2, -correct, close)
    # runs scored at the absolute loss-8 bar rank above runs scored only on "closest of four"
    return (0 if ev.get("max_loss") is not None else 1, -ev["passed"] / max(1, ev.get("feasible", 16)), close)


def _is_run(d):
    """A result folder the gallery can show: a swarm rollout plus a rollout summary."""
    try:
        return (os.path.isfile(os.path.join(d, "rollout.json")) and "cross" in json.load(open(os.path.join(d, "summary.json"))))
    except Exception:
        return False


LABELS = {"field": "Designed field + flocking", "hgrid/oracle": "Grid morphogen (designed)", "hgrid/hybrid_g2": "Learned rule G2 + grid morphogen", "hgrid": "Grid morphogen (learned)",
          "colony": "Colony brain", "evo": "Evolved rule", "hgrid2": "Grid morphogen 2 (fine, local)", "evo/compact": "Evolved compact rule (no neural net)", "play": "Strike-hardened rule", "meta": "Metamorphosis", "sort": "Emergent cell sorting", "posinfo": "Learned rule + positional information", "distill": "Local rule distilled from field", "meta/oracle": "Learned rule + designed metamorph", "hgrid2/round_a": "Grid morphogen 2 (round A)", "creature": "Creature shell (evo body + reaction shell)", "hgrid2/evo_grid": "Evolved body + grid steering", "sort/v1_typelook": "Emergent cell sorting (v1)", "evo16": "Evolved rule, hardened (evo16)", "zoo": "Zoo of personalities (field + behaviours)", "combo": "Lossless grid morphogen (combo, G8)", "combo/g16": "Lossless grid morphogen (combo, G16)", "evofate": "Evolved rule + fate", "evofate/c1_cma": "Evolved rule + fate (CMA point)", "sortfeel": "Emergent cell sorting, organic", "posinfo2": "Learned rule, lossless (posinfo2)", "evofate/c2": "Evolved rule + fate (C2)"}


def _label(d, summ):
    n = os.path.basename(d)
    rel = os.path.relpath(d, os.path.dirname(os.path.dirname(d))) if os.path.basename(os.path.dirname(d)) != "results" else n
    lab = summ.get("meta", {}).get("label") or summ.get("label") or LABELS.get(rel) or LABELS.get(n)
    if lab:
        return lab
    if n == "swarm_coevo":
        return "Baseline rule (try6)"
    if n.startswith("swarm_coevo_"):
        return "Learned rule " + n[len("swarm_coevo_"):].upper()
    return n


def _note(summ, passed, ev=None):
    meta = summ.get("meta", {})
    ov = meta.get("overrides", {})
    fixes = [lab for key, lab in (("sticky_plan", "sticky switch labels"), ("learned_lay", "a learned laying gate"), ("learned_egg", "parents choosing some eggs' element"),
                                  ("w_con", "a contrastive loss"), ("scale_inv", "a scale-invariant loss"),
                                  ("w_over", "an overflow penalty"), ("min_body", "a body floor")) if str(ov.get(key, "0")) not in ("0", "0.0")]
    bar = (f"under the absolute bar (a test fails if its divergence to the wanted plan is over {ev['max_loss']:g})" if ev and ev.get("max_loss") is not None
           else "on the OLD bar only (closest of the four plans, no absolute divergence limit; not yet rescored at the loss-8 bar)")
    head = (f"{ev['passed']} of {ev['feasible']} feasible transitions pass on the 16-test yardstick {bar} (all four own plans and all 12 switches, fair cull, "
            f"3 samples each); the rollout shown is the older 8-test one ({passed} of 8). " if ev else
            f"{passed} of 8 tests pass (each seeding closest to its own plan, and each switched swarm closest to its new plan). ")
    return (head
            + (f"Trained with {', '.join(fixes)}. " if fixes else "")
            + ("Divergences are scale-invariant (the swarm is rescaled to the plan's size before matching), so they "
               "are lower than in runs without that option and not directly comparable. " if summ.get("scale_inv") else "")
            + meta.get("note", ""))


def _payload(d):
    import swarm_nca
    summ = json.load(open(os.path.join(d, "summary.json")))
    passed, _ = swarm_nca.tests_passed(summ)
    probe = None
    if os.path.isfile(os.path.join(d, "probe.json")):
        try:
            probe = json.load(open(os.path.join(d, "probe.json")))
            if isinstance(probe, dict) and "strike" in probe:      # richer probe files keep the strike under "strike"
                probe = probe["strike"]
        except Exception:
            probe = None
    ev = _eval16(d)
    if ev:                                     # keep only the rates: the full file carries per-sample detail
        ev = dict(passed=ev["passed"], feasible=ev["feasible"], na=ev.get("na", []), samples=ev.get("samples"), max_loss=ev.get("max_loss"),
                  own={k: (v["rate"] if isinstance(v, dict) else v) for k, v in ev["own"].items()},
                  switch={k: (v["rate"] if isinstance(v, dict) else v) for k, v in ev["switch"].items()})
    return dict(eval16=ev, id=os.path.relpath(d, os.path.dirname(d) if os.path.basename(os.path.dirname(d)) == "results" else
                                    os.path.dirname(os.path.dirname(d))).replace("/", "-"), label=_label(d, summ), passed=passed, note=_note(summ, passed, ev),
                roll=json.load(open(os.path.join(d, "rollout.json"))), cross=summ["cross"],
                census={k: summ["census"][k] for k in KINDS if k in summ.get("census", {})},
                switch=summ.get("switch"), probe=probe, curve=_curve(os.path.join(d, "log.jsonl")))


# What each approach IS and how it differs, shown when it is picked. Keyed by run id (prefix match).
ABOUT = {
    "evo-compact": dict(
        what="A small hand-written swarm rule with no neural net: 31 numbers (attractions, speeds, laying and egg-choice gains, a desired-mix table) found by evolution (CMA-ES).",
        body="Each tadpole is pulled toward a few Gaussian anchors for its element's group in the current plan; the plan is the majority element.",
        sorting="Coarse: an element goes to its group's anchors, not to individual places in the body.",
        switching="Laying and egg choice steer the mix toward the new plan's; the anchors move to the new plan.",
        watch="Motion is jerky, and crystal clusters tend to form flat planar sheets: the anchors are too simple to hold a rounded body.",
        bar="Fails tier 1 under the loss-8 bar: own-plan losses 21-30. Every body fills the 280 cap."),
    "evo": dict(
        what="The learned G2 rule (one small neural net per tadpole, trained by backprop) left unchanged, plus a 95-number behaviour genome found by evolution: a laying homeostat (lay more when your element is short of the plan's share) and egg choice (some eggs take the most-wanted element).",
        body="Emergent: each tadpole only perceives its neighbours, and the outline grows from those local interactions. Nothing places tadpoles.",
        sorting="None designed. Elements end up roughly, not exactly, where the plan has them - the main remaining error.",
        switching="When the majority is eaten, the homeostat breeds the new majority's mix and the body re-forms around it.",
        watch="It always reads as a swarm reaching for a shape: organic, imperfect, alive.",
        bar="Fails tier 1 under the loss-8 bar: own-plan losses 15-25, almost all of it from element and domain placement (the outline alone is 2-4.5)."),
    "field": dict(
        what="Entirely designed, no learning: every unit of the plan is a slot, tadpoles are assigned to slots (matching element and domain) and fly there as boids; a field of attraction covers the gaps.",
        body="Global: a plan-sized set of slots is assigned to tadpoles by a sort every few steps.",
        sorting="Exact: assignment puts every element and domain in its place - why it is the only tier-1 pass.",
        switching="Census of the majority with a 12-step delay, then a 60-step vortex morph to the new plan's slots; surplus elements molt into wanted ones.",
        watch="Crisp, readable transitions and vessel reactions (startle wave, mobbing, pufferfish inflation) - but its imperfections look like bugs, not life.",
        bar="Passes tier 1 (own-plan losses 3.1 / 1.6 / 1.4 / 4.6); 9 of 13 feasible switches (four switched bodies land at 9-12)."),
    "hgrid2": dict(
        what="The grid morphogen, second round: the coarse class-deficit grid plus a FINE per-class morphogen (each element-and-domain class climbs a smooth field built from its own units in the plan), a continuous target, and feed-forward of the plan's own motion.",
        body="Local: every tadpole reads only the fields at its own position; nothing assigns it a place.",
        sorting="Local and fine enough to tell classes apart inside one grid cell - which is what the first grid lacked.",
        switching="As the first grid: the plan follows the majority (with a lock), laying follows class deficits, misplaced surplus withers to crystals.",
        watch="Whether it keeps the first grid's organic motion now that it is accurate (the feel metrics in results/hgrid2/feel.json).",
        bar="16 of 16 under the loss-8 bar at seed 7 (own 1.2 / 2.0 / 2.9 / 3.0), held-out seeds 1000 / 2000 15 of 15 feasible; in the organic feel band. Not lossless: it corrects composition by starving misplaced surplus on a timer."),
    "sort": dict(
        what="No network: a tadpole's TYPE is (element, body region). Each type climbs a few Gaussian morphogen wells in body coordinates (positional information), a newborn commits to the well its type under-occupies (fate), unlike types push apart harder than like types (differential adhesion), and a joint composition homeostat decides what is laid. 17 numbers found by CMA-ES.",
        body="Local sorting around a census and the body's centre; nothing assigns a tadpole a place.",
        sorting="Emergent tissue sorting (Steinberg): like cells pack together, boundaries sharpen. Adding the element term costs this rule 0.0-0.9, against 4-10 for the learned rule.",
        switching="Majority plan with a 12-step dwell; surplus tadpoles MOLT into a deficit element of their own domain, a tadpole with no region may transfer. Nothing dies on a clock.",
        watch="Sorting in motion: a mixed knot separating into tissues. The dragonfly's three-region domain split is its weakest part.",
        bar="12 of 13 feasible under the loss-8 bar at seed 7 (own 1.4 / 2.4 / 1.05 / 5.0); held-out 13 / 13 / 13 / 12. Lossless by design."),
    "posinfo": dict(
        what="The learned G2 rule given body-frame positional inputs (where am I in the body) and trained by backprop, plus a designed (element, domain) production homeostat that lays and molts toward the plan's mix.",
        body="Emergent from local perception plus a body frame.",
        sorting="Learned, with the position input the plain learned rule lacked.",
        switching="The homeostat re-forms surplus members into deficit elements; the switches were not trained when this was published.",
        watch="Whether it keeps the learned rule's organic swarming while the body is accurate.",
        bar="Passes tier 1 under the bar (3.2 / 6.9 / 4.4 / 6.7); switches not yet."),
    "meta": dict(
        what="The learned rule with a designed metamorphosis homeostat ON during training: tadpoles of the element most over the plan re-form, over 12 slowed steps and at most 8% at a time, into the element most under it; the network only learned shape.",
        body="Emergent from local perception.",
        sorting="Learned (none designed).",
        switching="Metamorphosis carries composition across a switch; every passing switch truly reshapes.",
        watch="Members visibly changing element after a cull.",
        bar="7/8 on the old 8-test yardstick on five seeds; own plans 11.8-14.8 fail the loss-8 bar."),
    "combo": dict(
        what="The grid morphogen (hgrid2) made LOSSLESS: its starvation of misplaced surplus is replaced by molting (a surplus tadpole re-forms its crystal into the element its own team is most short of), a laying cap (no egg while the body holds its headcount), ratio targets for an overfull team, and orphan steering. Coarse grid 8 cells a side.",
        body="Local: every tadpole reads the coarse class-deficit grid and a fine per-class morphogen at its own position, plus a census.",
        sorting="The fine morphogen and migrants (a tadpole where its class is barely wanted heads for the nearest site where it is missing).",
        switching="Majority plan with a lock; surplus MOLTS into the new body's missing elements. Nothing dies on its own.",
        watch="After a switch the old majority's leftovers changing colour into the new animal's missing parts instead of withering.",
        bar="All four scorecard axes: 16 / 15 / 15 of 16 at seeds 7 / 23 / 41 under the loss-8 bar, 0 self-inflicted deaths, in the organic band, cheaper than hgrid2."),
    "posinfo2": dict(
        what="A LEARNED rule (the round-1 network G2 plus inputs that tell each tadpole where it sits in the body's own frame, trained end to end) wrapped in a DESIGNED controller that decides only who is laid and who molts into what. The network decides where every tadpole swims; it can no longer decide to die.",
        body="Learned: the network reads its neighbours, which side of the body it is on and the element census, and steers.",
        sorting="Learned from positional information - the network puts each element and team where its plan wants them.",
        switching="Designed: a quota scaled so every living team fits the new plan, surplus MOLTS (never dies), and a majority guard stops a slim new majority from being flipped back by its own laying.",
        watch="A learned body that is accurate: the tightest own-plan shapes of any model, and nothing withers.",
        bar="51 of 52 feasible transitions over seeds 7 / 23 / 41 / 1000 under the loss-8 bar (own plans 1.6-2.7), 0 self-inflicted deaths, in the organic band."),
    "sortfeel": dict(
        what="Emergent cell sorting (sort) made organic. Its flat sheets turned out to be COMPRESSION, not adhesion: every tadpole was pulled to the centre of its well. Wells are now flat-bottomed (a tadpole fills its well as a liquid) and every tadpole wanders slightly on its own clock.",
        body="Positional wells per (element, region) and differential adhesion; a liquid inside each well.",
        sorting="Fate: a newborn commits to the well its type under-occupies.",
        switching="Molting and region transfer; nothing dies on its own.",
        watch="Tissues that fill their wells and shimmer instead of packing into sheets.",
        bar="12 / 11 / 13 / 13 of 13 feasible at seeds 7 / 23 / 41 / 101 under the loss-8 bar, 0 self-inflicted deaths, planar excess 0.27 -> 0.05 (in the organic band). The lightest accurate model: 6.3 ms/step in Python."),
    "evofate": dict(
        what="The round-1 EVOLVED rule (the learned G2 network + the evolved behaviour genome, unchanged, its learned death switched off) given a FATE: each tadpole commits to one of a few positional wells for its element, and a small pull with a dead zone keeps it there. Inside its well a tadpole moves exactly as the evolved rule does.",
        body="Emergent from the evolved rule's local motion; the fate pull only acts outside the dead zone.",
        sorting="Fate: a newborn picks the well its type under-occupies - the actuator the learned rule never had.",
        switching="Designed composition (lay homeostat, egg choice, molting); majority plan with a dwell.",
        watch="Whether it still feels like the evolved rule (the lead: beautifully organic) now that its bodies pass the bar.",
        bar="12 / 11 / 12 of 13 feasible at seeds 7 / 23 / 41 under the loss-8 bar, tier 1 at every seed, 0 self-inflicted deaths, in the organic band (at its planar edge at seed 23)."),
    "hgrid2-evo_grid": dict(
        what="The round-1 EVOLVED rule (the body the lead called beautifully organic) moving, hatching and looking exactly as it shipped, with the grid morphogen adding only composition (what is laid, misplaced surplus withering), a plan lock, and a gentle steer (coarse class gradient + fine morphogen + migrants).",
        body="Emergent: the evolved rule's own local motion; the grid nudges.",
        sorting="The grid's steer and its migrants (a tadpole where its class is barely wanted heads for the nearest site where it is missing).",
        switching="Majority plan with a 60-step lock; laying follows class deficits; surplus withers to crystals.",
        watch="Whether it keeps evo's swarming texture now that the body is accurate.",
        bar="13 of 16 under the loss-8 bar (own 2.2 / 3.2 / 1.9 / 4.9; only the three into-dragonfly switches miss at 8.3-9.8); held-out 12/15 on two seeds; in the organic band. Not lossless."),
    "evo16": dict(
        what="The evolved rule with its behaviour genome re-searched for held-out robustness and healing.",
        body="Emergent from local perception (G2) plus the evolved lay homeostat and egg choice.",
        sorting="None designed: the outline is right, the elements are not where the plan puts them.",
        switching="Lay homeostat + egg choice.",
        watch="The organic texture of the evolved family.",
        bar="16/16 on the OLD bar and 128/128 over held-out seeds; own-plan losses 13-26, so it fails tier 1 at the loss-8 bar."),
    "zoo": dict(
        what="A zoo of personalities: the designed field model plus nine switchable behaviours (breathe, travelling wave, jitter, orbit, switch burst, curiosity, hunting, rush-breeding after losses, bristling danger), searched with MAP-Elites over how a creature reacts, how lively it is and how dramatic its switches are.",
        body="Global, as field: slots assigned by a sort.",
        sorting="Global slot assignment.",
        switching="Molting + census with dwell, plus a radial burst in some personalities.",
        watch="Seven named creatures in results/zoo/elites/, each with its own playback page.",
        bar="Seven named creatures, each 13 of 13 feasible on the old bar (field's own-plan losses, ~1-3); not yet rescored at the loss-8 bar."),
    "meta-oracle": dict(
        what="The frozen learned G2 rule with a DESIGNED conformity metamorph and no training at all: each step, tadpoles of the element most over the majority's plan start re-forming into the element most under it (12 slowed steps, at most 8% at once).",
        body="Emergent from local perception.",
        sorting="Learned (none designed).",
        switching="Designed metamorphosis: the clearest evidence that composition should be designed and shape learned.",
        watch="Switched bodies truly reshape (jelly to puffer, puffer to dragonfly).",
        bar="7/8 on the old 8-test yardstick with zero training; fails the loss-8 bar."),
    "colony": dict(
        what="The learned rule plus one shared colony state (a small recurrent net) that reads the swarm's summary, votes the plan and biases which elements breed.",
        body="Emergent from local perception, steered by the colony's vote.",
        sorting="Learned.",
        switching="The vote flips 20-60 steps after a cull, cleanly and with hysteresis, but the refill burst comes first.",
        watch="The plan vote flipping (results/colony/vote_trace.json).",
        bar="5.0 / 8 mean over seeds 7-10 (control without the colony 4.25); fails the loss-8 bar."),
    "distill": dict(
        what="A local network student taught by the designed field model (behaviour cloning + DAgger): every tadpole sees only its neighbours and the population census.",
        body="Emergent from local perception; the plan exists only in the weights.",
        sorting="Learned from field's labels.",
        switching="A per-tadpole census belief with a dwell.",
        watch="Blurrier bodies than field and no healing: the measured price of giving up the global frame.",
        bar="7 of 16 on the full yardstick under the old bar; own-plan divergence ~25-45, fails the loss-8 bar."),
    "hgrid-oracle": dict(
        what="Two levels: a coarse 3D grid (a cellular automaton over space) holds, per element and slot, how many tadpoles are WANTED there versus present; boids below follow the gradient of their own class's shortage.",
        body="The grid carries the plan; the boids only read their local cell, so the body assembles from local deficits.",
        sorting="Partial and local: each class flows to where its class is missing - between field's exact assignment and none.",
        switching="The grid switches to the new majority's plan; laying is regulated by the grid's class totals and misplaced surplus tadpoles starve to crystals (loot during a switch).",
        watch="Accurate and organic at once: the body is right, and the motion stays alive.",
        bar="Just over the loss-8 bar: own-plan losses 8-17 (1 of 4 own plans passes)."),
    "hgrid-hybrid_g2": dict(
        what="The learned G2 rule for motion and look, with the grid layer controlling only composition (which element is laid where).",
        body="Emergent outline from G2; the grid adds where each class is wanted.",
        sorting="Partial, via the grid's class deficits on laying and starving.",
        switching="As the grid oracle: the grid's plan changes, composition follows.",
        watch="G2's organic motion with a better-composed body.",
        bar="Just over the loss-8 bar: own-plan losses 7-17 (1 of 4 own plans passes)."),
    "swarm_coevo_g2": dict(
        what="The canonical learned rule: one small neural net per tadpole, trained by backprop through time on all four plans at once, with a learned laying gate and a body floor.",
        body="Emergent from local perception only.",
        sorting="None designed - element placement is the main error.",
        switching="Rarely: the bodies sit at the 280 cap and keep their old shape at the new mix.",
        watch="The raw learned behaviour every evolved and hybrid approach builds on.",
        bar="Fails tier 1: own-plan losses 15-23."),
    "swarm_coevo": dict(
        what="A learned rule from the gradient lineage (backprop through time), with the training options listed in the note.",
        body="Emergent from local perception only.",
        sorting="None designed.",
        switching="Rarely.",
        watch="An intermediate stage of the learned family.",
        bar="Fails tier 1 under the loss-8 bar."),
}


def _about(pid):
    for k in sorted(ABOUT, key=len, reverse=True):
        if pid == k or pid.startswith(k + "_") or pid.startswith(k):
            return ABOUT[k]
    return None


def build_swarm(results_root, gallery_dir=None):
    """The swarm section. The top-ranked run is embedded in the page; with gallery_dir, every other
    run is written there as <id>.json, and the page fetches it (relative URL) when it is picked, so
    the page stays under the artifact size limit however many approaches there are."""
    cands = []
    for n in sorted(os.listdir(results_root)):                 # a result folder, or one level of sub-runs
        d = os.path.join(results_root, n)
        if not os.path.isdir(d):
            continue
        cands += ([d] if _is_run(d) else []) + [os.path.join(d, m) for m in sorted(os.listdir(d))
                                                 if os.path.isdir(os.path.join(d, m)) and _is_run(os.path.join(d, m))]
    cands = sorted(cands, key=_rank)
    if not cands:
        return "", ""
    import prism_render as pr
    first = _payload(cands[0])
    tag = lambda p: (f"{p['eval16']['passed']}/{p['eval16'].get('feasible', 16)}" + ("" if p['eval16'].get('feasible', 16) == 16 else " feasible") + ("" if p['eval16'].get('max_loss') is not None else " old bar")) if p.get("eval16") else f"{p['passed']}/8"
    manifest = [dict(id=first["id"], label=first["label"], passed=tag(first), about=_about(first["id"]), file=None)]
    if gallery_dir:
        os.makedirs(gallery_dir, exist_ok=True)
        for d in cands[1:]:
            p = _payload(d)
            fn = f"{p['id']}.json"
            json.dump(p, open(os.path.join(gallery_dir, fn), "w"), separators=(",", ":"))
            manifest.append(dict(id=p["id"], label=p["label"], passed=tag(p), about=_about(p["id"]), file=f"{os.path.basename(gallery_dir)}/{fn}"))
    apps = "".join(f'<button type="button" class="app" data-app="{m["id"]}" aria-pressed="false">'
                   f'<span class="appl">{m["label"]}</span><span class="apps mono">{m["passed"]}</span></button>' for m in manifest)
    tabs = "".join(
        f'<button type="button" role="tab" data-swk="{k}" aria-selected="false">'
        f'<span class="swatch" style="background:{EL_UI[ELEMENTS.index(MAJOR[k])]}"></span>{MAJOR[k]} seed</button>' for k in KINDS)
    bench = f"""
  <section class="benchp" aria-labelledby="hswarm" id="swarm">
    <div class="h3dhead">
      <div class="eyebrow">Latest · one rule, four seedings</div>
      <h2 id="hswarm">One rule grows four different creatures</h2>
      <p class="caption">Each swarm is scored against one plan only: the plan of its current majority element. The total is the sum over the four seedings. Every particle is a whole tadpole fauna: a heart crystal, a spindle and a prism. Its <strong>element</strong> and <strong>domain</strong> are fixed at birth in most approaches: an egg is its parent's domain, and its parent's element but for a rare mutation. The rule decides where each tadpole swims, whether an egg hatches, how its prism and spindle are shaped (inside its element's identity), and whether a Charge tadpole is plain, danger or shielded. A tadpole that dies leaves a lime crystal. The same rule is seeded four ways, sixteen tadpoles at each target's element mix. Pick an approach and a seed to watch it grow; drag to orbit.</p>
      <h3 class="apph">Approaches</h3>
      <p class="caption">Each is a different way of building the creature, ranked by strict tests passed. The best is loaded first; the others load when picked.</p>
      <div class="appgrid" id="swapps">{apps}</div>
      <dl class="about" id="swabout"></dl>
      <p class="caption" id="swnote"></p>
    </div>
    <div class="bench">
      <div class="dish">
        <div class="tabs swtabs" role="tablist" aria-label="Seed">{tabs}</div>
        <div class="plate plate3d" id="platesw"><canvas id="cvsw" width="560" height="560" aria-label="Grown tadpole swarm, drag to orbit"></canvas><span class="hint">drag to orbit</span></div>
        <label class="row" for="swt">Growth step<input id="swt" type="range" min="0" max="1" value="1"><output id="o-swt">0</output></label>
      </div>
      <div class="panel">
        <dl class="readouts">
          <div><dt>Step</dt><dd id="rsw-step">0</dd></div>
          <div><dt>Tadpoles</dt><dd id="rsw-n">0</dd></div>
          <div><dt>Crystals</dt><dd id="rsw-cr">0</dd></div>
          <div><dt>Shields</dt><dd id="rsw-sh">0</dd></div>
        </dl>
        <div class="buttons">
          <button type="button" class="btn primary" id="bsw-play">Pause</button>
          <button type="button" class="btn" id="bsw-target" aria-pressed="false">Show target</button>
          <button type="button" class="btn" id="bsw-spin" aria-pressed="true">Auto-orbit</button>
        </div>
        <div class="swmix" id="swmix" aria-label="Element mix, grown (top) and target (bottom)"></div>
        <p class="census mono" id="rsw-census" aria-live="off"></p>
      </div>
    </div>
    <div class="record" id="swtables"></div>
  </section>"""
    data = {"first": first, "manifest": manifest, "names": NAMES, "major": MAJOR}
    pal = np.round(pr.palette_colours()[:, :3], 4).tolist()
    script = SCRIPTSWARM.replace("/*SWARM_MODEL*/", open(os.path.join(HERE, "swarm_model.js")).read()) \
        .replace("/*SWDATA*/", json.dumps(data, separators=(",", ":"))).replace("/*PALETTE*/", json.dumps(pal)) \
        .replace("/*ELUI*/", json.dumps(EL_UI))
    return bench, script


CSS = """
.swtabs { grid-template-columns: repeat(4, minmax(0, 1fr)) !important; margin-bottom: 10px }
.swtabs button { border-bottom: 0 !important; border-right: 1px solid var(--rule) !important }
.swtabs button:last-child { border-right: 0 !important }
.swmix { display: grid; gap: 4px; margin: 12px 0 }
.swmix .bar { display: flex; height: 12px; border-radius: 6px; overflow: hidden; background: var(--rule) }
.swmix .bar i { display: block; height: 100% }
.swmix .lab { font: 12px var(--mono); color: var(--muted) }
table.swcross td.win { font-weight: 700; color: var(--accent) }
table.swcross td.diag { text-decoration: underline; text-underline-offset: 3px }
.apph { margin: 18px 0 4px }
.appgrid { display: flex; flex-wrap: wrap; gap: 8px; margin: 10px 0 4px }
.appgrid .app { display: inline-flex; align-items: baseline; gap: 10px; padding: 7px 12px; border: 1px solid var(--rule);
  border-radius: 8px; background: transparent; color: var(--ink); font: inherit; cursor: pointer; min-width: 0 }
.appgrid .app[aria-pressed="true"] { border-color: var(--accent); box-shadow: inset 0 0 0 1px var(--accent) }
.appgrid .app .apps { color: var(--muted); font-size: 12px }
.appgrid .app[aria-busy="true"] { opacity: .6 }
.about { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 10px 22px; margin: 12px 0 6px; max-width: 980px }
.about div { min-width: 0 }
.about dt { font-size: 11px; letter-spacing: .06em; text-transform: uppercase; color: var(--muted); margin-bottom: 2px }
.about dd { margin: 0; font-size: 13.5px; line-height: 1.45 }
"""

SCRIPTSWARM = r"""<script>
(function () {
  if (typeof THREE === 'undefined') return;
  const exports = {}, module = { exports };
  /*SWARM_MODEL*/
  const SM = module.exports && module.exports.realise ? module.exports : window.SwarmModel;
  const D = /*SWDATA*/, PALETTE = /*PALETTE*/, EL_UI = /*ELUI*/, KINDS = ['mass', 'space', 'charge', 'time'];
  const $ = id => document.getElementById(id), cv = $('cvsw');
  const renderer = new THREE.WebGLRenderer({ canvas: cv, antialias: true });
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  renderer.outputEncoding = THREE.sRGBEncoding;
  const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(30, 1, 0.5, 1500);
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
  const MAXP = 600, MAXS = 600 * 8;
  const mat = new THREE.MeshStandardMaterial({ roughness: 0.45, metalness: 0.06, flatShading: true });
  const gem = new THREE.MeshStandardMaterial({ roughness: 0.2, metalness: 0.1, flatShading: true, emissive: 0x3a4a66 });
  const inst = (geo, m, n) => { const x = new THREE.InstancedMesh(geo, m, n); x.instanceMatrix.setUsage(THREE.DynamicDrawUsage); x.setColorAt(0, new THREE.Color(1, 1, 1)); x.count = 0; scene.add(x); return x; };
  const boxes = inst(new THREE.BoxGeometry(2, 2, 2), mat, MAXP), octas = inst(new THREE.OctahedronGeometry(SM.SHIELD), mat, MAXP), gems = GEO.map(g => inst(g, gem, MAXP));
  const limes = GEO.map(g => inst(g, new THREE.MeshStandardMaterial({ roughness: 0.25, flatShading: true, emissive: 0x2c4a10 }), MAXP));
  const cyl = new THREE.CylinderGeometry(1, 1, 1, 6, 1, true); cyl.translate(0, 0.5, 0);
  const spMesh = new THREE.InstancedMesh(cyl, new THREE.MeshStandardMaterial({ roughness: 0.6, color: new THREE.Color(0.6, 0.72, 0.86).convertSRGBToLinear() }), MAXS);
  spMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); spMesh.count = 0; scene.add(spMesh);
  const lin = c => new THREE.Color(...c).convertSRGBToLinear();
  const HEART = lin([0.42, 0.62, 1.0]), LIME = lin([0.62, 0.92, 0.22]);
  const M = new THREE.Matrix4(), Q = new THREE.Quaternion(), V = new THREE.Vector3(), Sv = new THREE.Vector3(), UP = new THREE.Vector3(0, 1, 0);

  // decode rollouts (one approach at a time; others are fetched when picked)
  let R = {}, SC = null, RUN = null;
  const every = 5, CACHE = {};
  function decodeRoll(roll) {
    const out = {};
    for (const k of KINDS) {
      const r = roll[k], bin = atob(r.b64), buf = new ArrayBuffer(bin.length), u8 = new Uint8Array(buf);
      for (let i = 0; i < bin.length; i++) u8[i] = bin.charCodeAt(i);
      out[k] = { a: new Int16Array(buf), shape: r.shape, n: r.n, crystals: r.crystals, switched_at: r.switched_at };
    }
    return out;
  }
  const NAMES = D.names, MAJOR = D.major, ELN = ['Charge', 'Mass', 'Space', 'Time'];
  const best = row => Object.keys(row).reduce((a, b) => row[a] <= row[b] ? a : b);
  const cells = (row, mark) => { const w = best(row); return KINDS.map(k2 => `<td class="${k2 === w ? 'win' : ''}${k2 === mark ? ' diag' : ''}">${row[k2].toFixed(1)}</td>`).join(''); };
  function tables(p) {
    const head = KINDS.map(k => `<th scope="col">${NAMES[k]}</th>`).join('');
    let own = 0;
    const rows = KINDS.map(k => { const c = p.census[k] || {}; if (best(p.cross[k]) === k) own++;
      return `<tr><th scope="row">Seeded mostly ${MAJOR[k]}</th>${cells(p.cross[k], k)}<td>${c.n ?? ''}/${c.target_n ?? ''}</td><td>${c.deaths ?? ''}</td><td>${c.mutants ?? ''}</td></tr>`; }).join('');
    let h = `<h3>Does the seed choose the body?</h3><p class="caption">Each grown swarm (rows) scored against every target (columns): Sinkhorn divergence on position, element, domain region, prism, tier, facing and spindle, under the best assignment of domains to regions. Lower is closer; the lowest in each row is marked. ${own} of 4 seedings are closest to their own body plan.</p>`
      + `<div class="tablewrap"><table class="swcross"><thead><tr><th scope="col">Grown from</th>${head}<th scope="col">Tadpoles</th><th scope="col">Crystals</th><th scope="col">Mutant eggs</th></tr></thead><tbody>${rows}</tbody></table></div>`;
    if (p.switch) {
      let ok = 0;
      const sr = KINDS.map(k => { const v = p.switch[k]; if (best(v.cross) === v.to) ok++;
        return `<tr><th scope="row">${NAMES[k]} loses its ${MAJOR[k]}: ${MAJOR[v.to]} majority</th>${cells(v.cross, v.to)}<td>${NAMES[v.majority] || v.majority}</td></tr>`; }).join('');
      h += `<h3>Losing the majority</h3><p class="caption">The same swarms after another 240 steps, once enough of their majority element was removed (as if eaten) that another element took over. The underlined column is the new majority's plan; ${ok} of 4 end closest to it. Play a seed past the marked step to watch it.</p>`
        + `<div class="tablewrap"><table class="swcross"><thead><tr><th scope="col">Swarm</th>${head}<th scope="col">Majority now</th></tr></thead><tbody>${sr}</tbody></table></div>`;
    }
    if (p.eval16) {
      const e = p.eval16, cell = (r, own) => r == null ? '<td>n/a</td>' : `<td class="${r > 0.5 ? 'win' : ''}${own ? ' diag' : ''}">${Math.round(r * 100)}%</td>`;
      const rows = KINDS.map(k => `<tr><th scope="row">${NAMES[k]}</th>` + KINDS.map(t => t === k ? cell(e.own[k], true) : cell(e.switch[`${k}->${t}`], false)).join('') + '</tr>').join('');
      h += `<h3>Every transition</h3><p class="caption">All 16 tests: each plan grown from its own seed (diagonal) and each of the 12 possible switches (a grown body loses its majority to each other element). Each test is run ${e.samples || 3} times; the cell is the share of runs that end closest to the wanted plan with at least 32 tadpoles${e.max_loss != null ? ` and a divergence of at most ${e.max_loss}` : ' (old bar: no divergence limit)'}, and a test passes on a majority. ${e.passed} of ${e.feasible} feasible tests pass${e.na && e.na.length ? ` (n/a: a body that holds too little of that element to be taken over: ${e.na.join(', ')})` : ''}.</p>`
        + `<div class="tablewrap"><table class="swcross"><thead><tr><th scope="col">Grown as \\ ends as</th>${head}</tr></thead><tbody>${rows}</tbody></table></div>`;
    }
    if (p.probe) {
      const pr = KINDS.filter(k => p.probe[k]).map(k => { const q = p.probe[k];
        return `<tr><th scope="row">${NAMES[k]}</th><td>${q.before}</td><td>${q.killed}</td><td>${q.cut}</td><td>${q.recovered}</td><td>${q.heal == null ? '–' : q.heal}</td></tr>`; }).join('');
      h += `<h3>A vessel flies through it</h3><p class="caption">Every tadpole inside a sphere one swarm radius across, off-centre, is removed; the swarm is scored against its own plan before, right after, and 120 steps later. Heal 1 means back to the pre-strike score.</p>`
        + `<div class="tablewrap"><table class="swcross"><thead><tr><th scope="col">Plan</th><th scope="col">Before</th><th scope="col">Killed</th><th scope="col">Right after</th><th scope="col">120 steps later</th><th scope="col">Heal</th></tr></thead><tbody>${pr}</tbody></table></div>`;
    }
    if (p.curve) h += `<figure class="fig"><figcaption>Training loss: the sum over the four seedings (black) and each plan</figcaption><div class="chart">${p.curve}</div></figure>`;
    return h;
  }
  function setRun(p) {
    RUN = p; CACHE[p.id] = p; R = decodeRoll(p.roll); SC = p.roll.scale;
    $('swnote').textContent = p.label + ': ' + p.note;
    const ab = (D.manifest.find(x => x.id === p.id) || {}).about;
    $('swabout').innerHTML = ab ? [['What it is', ab.what], ['How the body forms', ab.body], ['Where elements go', ab.sorting], ['How it switches', ab.switching], ['What to watch for', ab.watch], ['Under the loss-8 bar', ab.bar]]
      .map(([t, v]) => `<div><dt>${t}</dt><dd>${v}</dd></div>`).join('') : '';
    $('swtables').innerHTML = tables(p);
    document.querySelectorAll('#swapps [data-app]').forEach(b => b.setAttribute('aria-pressed', b.dataset.app === p.id));
    load(kind);
  }
  function pick(id) {
    if (CACHE[id]) return setRun(CACHE[id]);
    const m = D.manifest.find(x => x.id === id), b = document.querySelector(`#swapps [data-app="${id}"]`);
    if (!m || !m.file) return;
    b.setAttribute('aria-busy', 'true');
    fetch(m.file).then(r => { if (!r.ok) throw new Error(r.status); return r.json(); })
      .then(p => { b.removeAttribute('aria-busy'); setRun(p); })
      .catch(() => { b.removeAttribute('aria-busy'); $('swnote').textContent = m.label + ' could not be loaded here (its data is published beside the page).'; });
  }
  const perp = f => { const a = Math.abs(f[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0]; const d = a[0] * f[0] + a[1] * f[1] + a[2] * f[2];
    const v = [a[0] - d * f[0], a[1] - d * f[1], a[2] - d * f[2]], L = Math.hypot(...v) || 1; return v.map(x => x / L); };
  function swarmFrame(k, fi) {
    const r = R[k], [F, N, W] = r.shape, P = Object.assign({}, SM.TARGETS[k].defaults, { slotMap: [0, 1, 2] });
    const out = { crystals: [], prisms: [], spindles: [], shields: 0 }, n = r.n[fi];
    for (let i = 0; i < n; i++) {
      const o = (fi * N + i) * W, g = j => r.a[o + j] / SC[j];
      let f = [g(9), g(10), g(11)]; const L = Math.hypot(...f); f = L > 1e-3 ? f.map(x => x / L) : [1, 0, 0];
      const u = { p: [g(0), g(1), g(2)], f, n: perp(f), elem: g(3), spindle: { len: g(12), bend: g(13), roll: 0, thick: P.spindleThickness },
        prism: { h: [g(5), g(6), g(7)], tier: g(8), slot: g(4), roll: 0 } };
      const z = SM.realise(P, u, i); out.crystals.push(z.crystal); out.prisms.push(z.prism); out.spindles.push(z.spindle);
      if (z.prism.tier === 2) out.shields++;
    }
    return out;
  }
  const TG = {};
  const target = k => TG[k] || (TG[k] = SM.report(k, { slotMap: [0, 1, 2] }));

  function draw(g, off, dead) {
    let nb = 0, no = 0, ns = 0; const ng = [0, 0, 0, 0], nl = [0, 0, 0, 0];
    g.prisms.forEach(q => {
      const R9 = q.R, h = q.h, oct = q.tier === 2, m = oct ? octas : boxes, j = oct ? no++ : nb++;
      M.set(R9[0] * h[0], R9[1] * h[1], R9[2] * h[2], q.p[0] + off[0], R9[3] * h[0], R9[4] * h[1], R9[5] * h[2], q.p[1] + off[1], R9[6] * h[0], R9[7] * h[1], R9[8] * h[2], q.p[2] + off[2], 0, 0, 0, 1);
      m.setMatrixAt(j, M); m.setColorAt(j, lin(PALETTE[q.dom][q.tier]));
    });
    g.crystals.forEach(c => { const e = c.elem, j = ng[e]++; M.makeScale(c.r, c.r, c.r); M.setPosition(c.p[0] + off[0], c.p[1] + off[1], c.p[2] + off[2]); gems[e].setMatrixAt(j, M); gems[e].setColorAt(j, HEART); });
    (dead || []).forEach(c => { const e = c[3], j = nl[e]++; M.makeScale(0.9, 0.9, 0.9); M.setPosition(c[0] + off[0], c[1] + off[1], c[2] + off[2]); limes[e].setMatrixAt(j, M); limes[e].setColorAt(j, LIME); });
    g.spindles.forEach(sp => {
      for (let i = 0; i + 1 < sp.pts.length && ns < MAXS; i++) {
        const a = sp.pts[i], b = sp.pts[i + 1];
        V.set(b[0] - a[0], b[1] - a[1], b[2] - a[2]); const L = V.length(); if (L < 1e-6) continue;
        Q.setFromUnitVectors(UP, V.normalize()); Sv.set(sp.r, L, sp.r);
        M.compose(new THREE.Vector3(a[0] + off[0], a[1] + off[1], a[2] + off[2]), Q, Sv); spMesh.setMatrixAt(ns++, M);
      }
    });
    boxes.count = nb; octas.count = no; spMesh.count = ns; gems.forEach((m, e) => m.count = ng[e]); limes.forEach((m, e) => m.count = nl[e]);
    [boxes, octas, spMesh, ...gems, ...limes].forEach(m => { m.instanceMatrix.needsUpdate = true; if (m.instanceColor) m.instanceColor.needsUpdate = true; });
  }
  let cen = null, kind = 'mass', fi = 0, showT = false, playing = !matchMedia('(prefers-reduced-motion: reduce)').matches, spin = playing, az = 0.75, el = 0.35, dist = 120, last = 0, tk = 0;
  function mixBar(counts, label) {
    const tot = counts.reduce((a, b) => a + b, 0) || 1;
    return `<span class="lab">${label}</span><span class="bar">${counts.map((c, e) => `<i style="width:${100 * c / tot}%;background:${EL_UI[e]}" title="${['Charge', 'Mass', 'Space', 'Time'][e]} ${c}"></i>`).join('')}</span>`;
  }
  function render() {
    const r = R[kind], t = target(kind);
    let g, off = [0, 0, 0], dead = [];
    if (showT) { g = t.frames[tk % t.frames.length]; off = t.offset; }
    else {
      g = swarmFrame(kind, fi); const step = fi * every; dead = r.crystals.filter(c => c[4] <= step);
      // a body plan is a shape, not a place (the loss is translation-invariant): follow the swarm's centroid
      const m = [0, 0, 0], n = g.crystals.length || 1;
      g.crystals.forEach(c => { m[0] += c.p[0] / n; m[1] += c.p[1] / n; m[2] += c.p[2] / n; });
      const a = cen ? 0.35 : 1; cen = (cen || m).map((v, i) => v + a * (m[i] - v));
      off = cen.map(v => -v);
    }
    draw(g, off, dead);
    camera.position.set(Math.cos(el) * Math.cos(az) * dist, Math.sin(el) * dist, Math.cos(el) * Math.sin(az) * dist);
    camera.lookAt(0, 0, 0); renderer.render(scene, camera);
    const sa = R[kind].switched_at;
    $('rsw-step').textContent = showT ? 'target' : (fi * every) + (sa != null && fi * every >= sa ? ' (after the loss)' : '');
    $('rsw-n').textContent = showT ? t.units : r.n[fi] + ' / ' + t.units;
    $('rsw-cr').textContent = showT ? '–' : dead.length;
    $('rsw-sh').textContent = showT ? (t.states ? t.states[2] / t.frames.length | 0 : '–') : g.shields;
    $('o-swt').textContent = fi * every;
  }
  function mix() {
    const r = R[kind], [F, N, W] = r.shape, f = F - 1, c = [0, 0, 0, 0];
    for (let i = 0; i < r.n[f]; i++) c[Math.round(r.a[(f * N + i) * W + 3])]++;
    $('swmix').innerHTML = mixBar(c, 'grown') + mixBar(target(kind).elements, 'target');
    const row = RUN.cross[kind], w = best(row);
    $('rsw-census').textContent = `${MAJOR[kind]} seed grows closest to the ${NAMES[w]} (${row[w].toFixed(1)}; its own target ${row[kind].toFixed(1)}).`;
  }
  function load(k) {
    kind = k; fi = R[k].shape[0] - 1; tk = 0; cen = null;
    document.querySelectorAll('.swtabs [data-swk]').forEach(b => b.setAttribute('aria-selected', b.dataset.swk === k));
    const t = target(k); dist = Math.max(70, 1.6 * Math.max(...t.size));
    $('swt').max = R[k].shape[0] - 1; $('swt').value = fi; mix(); render();
  }
  function resize() { const w = cv.clientWidth || 560, h = cv.clientHeight || 560; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); render(); }
  function loop(t) {
    if (t - last > 120) {
      last = t;
      if (playing) { if (showT) tk++; else fi = (fi + 1) % R[kind].shape[0]; $('swt').value = fi; }
      if (spin) az += 0.012;
      if (playing || spin) render();
    }
    requestAnimationFrame(loop);
  }
  let drag = null;
  cv.addEventListener('pointerdown', e => { drag = [e.clientX, e.clientY, az, el]; cv.setPointerCapture(e.pointerId); });
  cv.addEventListener('pointermove', e => { if (!drag) return; az = drag[2] + (e.clientX - drag[0]) * 0.01; el = Math.max(-1.5, Math.min(1.5, drag[3] + (e.clientY - drag[1]) * 0.01)); render(); });
  cv.addEventListener('pointerup', () => { drag = null; });
  cv.addEventListener('wheel', e => { e.preventDefault(); dist = Math.max(20, Math.min(500, dist * Math.exp(e.deltaY * 0.001))); render(); }, { passive: false });
  document.querySelectorAll('.swtabs [data-swk]').forEach(b => b.onclick = () => load(b.dataset.swk));
  $('swt').addEventListener('input', e => { fi = +e.target.value; cen = null; playing = false; $('bsw-play').textContent = 'Play'; showT = false; $('bsw-target').setAttribute('aria-pressed', false); render(); });
  $('bsw-play').onclick = () => { playing = !playing; $('bsw-play').textContent = playing ? 'Pause' : 'Play'; };
  if (!playing) $('bsw-play').textContent = 'Play';
  $('bsw-target').onclick = () => { showT = !showT; $('bsw-target').setAttribute('aria-pressed', showT); $('bsw-target').textContent = showT ? 'Show swarm' : 'Show target'; render(); };
  $('bsw-spin').onclick = () => { spin = !spin; $('bsw-spin').setAttribute('aria-pressed', spin); };
  $('bsw-spin').setAttribute('aria-pressed', spin);
  function theme() { scene.background = new THREE.Color(getComputedStyle(document.documentElement).getPropertyValue('--dish').trim() || '#e8eef2'); render(); }
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', theme);
  new MutationObserver(theme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  new ResizeObserver(resize).observe(cv);
  document.querySelectorAll('#swapps [data-app]').forEach(b => b.onclick = () => pick(b.dataset.app));
  setRun(D.first); theme(); resize(); requestAnimationFrame(loop);
})();
</script>"""
