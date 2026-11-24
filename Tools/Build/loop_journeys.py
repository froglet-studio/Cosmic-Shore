#!/usr/bin/env python3
"""Player journeys through the Loop Atlas: expand Docs/LoopAtlas/personas/journeys.yaml into timelines.

Garrett, 2026-11-21: model how different players move through the loops, and animate them side by side.

  python3 Tools/Build/loop_journeys.py build            # writes personas/journeys.json and personas/JOURNEYS.md
  python3 Tools/Build/loop_journeys.py build --html DIR # also writes the page and its data into DIR to publish
  python3 Tools/Build/loop_journeys.py --check          # what CI runs: valid, and the generated files are current
  python3 Tools/Build/loop_journeys.py self-test        # proves each check refuses what it must

journeys.json is read by the journeys page and by the economy and timescale simulation. Each step carries
{day, minute, t, loop, dur_s, parent, crystals, event, ...}, the same shape as Docs/LoopAtlas/sim/out/journeys.json.
It reads model.json (run loop_atlas.py build first) and needs PyYAML.
"""
from __future__ import annotations

import argparse
import copy
import json
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2] / "Docs" / "LoopAtlas"
MODEL = ROOT / "model.json"
SRC = ROOT / "personas" / "journeys.yaml"
OUT = ROOT / "personas" / "journeys.json"
REPORT = ROOT / "personas" / "JOURNEYS.md"
TEMPLATE = ROOT / "personas" / "page" / "journeys.template.html"
DOW = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"]
LAYERS = ("now", "prop")
VESSEL_PRICE = 4000
WIN_PAY = 200
PLACES = ("toybox", "arcade", "arena", "voyages")
STEP_KEYS = {"l", "m", "in", "via", "n", "hole", "risk", "ev", "won"}
KINDS = {"find", "own", "crew", "loss"}


class JourneyError(Exception):
    pass


def load_model() -> dict:
    model = json.loads(MODEL.read_text(encoding="utf-8"))
    return {n["id"]: n for n in model["nodes"]}, {h["id"] for h in model["handoffs"]}, model


def chain(nodes: dict, loop: str) -> list[str]:
    out, seen = [], set()
    while loop and loop not in seen:
        seen.add(loop)
        out.append(loop)
        loop = nodes[loop].get("parent")
    return out[::-1]


def hhmm(text: str, where: str) -> int:
    m = re.fullmatch(r"(\d\d):(\d\d)", str(text))
    if not m or int(m[1]) > 23 or int(m[2]) > 59:
        raise JourneyError(f"{where}: at must be HH:MM, got {text!r}")
    return int(m[1]) * 60 + int(m[2])


def days_of(sitting: dict, horizon: int, start: int, where: str) -> list[int]:
    if "day" in sitting:
        d = sitting["day"]
        if not isinstance(d, int) or not 1 <= d <= horizon:
            raise JourneyError(f"{where}: day {d!r} is outside 1..{horizon}")
        return [d]
    every = sitting.get("every")
    if not every:
        raise JourneyError(f"{where}: a sitting needs day or every")
    bad = [x for x in every if x not in DOW]
    if bad:
        raise JourneyError(f"{where}: unknown weekday {bad}")
    lo, hi = sitting.get("from", 1), sitting.get("to", horizon)
    return [d for d in range(lo, min(hi, horizon) + 1) if DOW[(start + d - 1) % 7] in every]


def expand(doc: dict, nodes: dict, handoffs: set) -> dict:
    horizon = doc["horizon"]
    start = DOW.index(doc.get("start", "mon"))
    ids = [p["id"] for p in doc["personas"]]
    if len(ids) != len(set(ids)):
        raise JourneyError("persona ids repeat")
    personas = []
    for p in doc["personas"]:
        out = {k: p[k] for k in ("id", "name", "from", "who", "challenge", "device", "group", "drivers", "timescale", "win")}
        out["layers"] = {}
        for layer in LAYERS:
            lay = p["layers"].get(layer)
            if lay is None:
                raise JourneyError(f"{p['id']}: missing layer {layer}")
            fate = lay.get("fate") or {}
            last = fate.get("day", horizon)
            steps, sittings = [], []
            # one-off sittings first; a weekly pattern skips a day where a one-off sitting takes its slot
            order = sorted(range(len(lay["sittings"])), key=lambda i: "day" not in lay["sittings"][i])
            taken = []
            for si in order:
                s = lay["sittings"][si]
                where = f"{p['id']}.{layer}.sittings[{si}]"
                at = hhmm(s["at"], where)
                if s.get("with") and s["with"] not in ids:
                    raise JourneyError(f"{where}: with {s['with']!r} is not a persona")
                for d in days_of(s, horizon, start, where):
                    if d > last:
                        continue
                    t = (d - 1) * 1440 + at
                    span = (t, t + sum(st.get("m", 0) for st in s["steps"]))
                    if "every" in s and any(a < span[1] and span[0] < b for a, b in taken):
                        continue
                    if "day" in s:
                        taken.append(span)
                    t0 = t
                    for st in s["steps"]:
                        extra = set(st) - STEP_KEYS
                        if extra:
                            raise JourneyError(f"{where}: unknown step fields {sorted(extra)}")
                        for key in ("l", "in"):
                            if key in st and st[key] not in nodes:
                                raise JourneyError(f"{where}: loop {st[key]!r} is not in the atlas")
                        if "via" in st and st["via"] not in handoffs:
                            raise JourneyError(f"{where}: handoff {st['via']!r} is not in vocabulary.yaml")
                        if not isinstance(st.get("m"), (int, float)) or st["m"] <= 0:
                            raise JourneyError(f"{where}: step {st.get('l')} needs minutes m > 0")
                        if not 0 <= st.get("risk", 0) <= 1:
                            raise JourneyError(f"{where}: risk must be 0..1")
                        node = nodes[st["l"]]
                        lo, hi = node["t"]
                        dur = round(st["m"] * 60)
                        path = chain(nodes, st["l"])
                        if "in" in st:
                            path = [x for x in chain(nodes, st["in"]) if x not in path] + path
                        won = st.get("won", 0)
                        steps.append({
                            "day": d, "minute": t - (d - 1) * 1440, "t": t, "loop": st["l"], "dur_s": dur,
                            "parent": st.get("in") or node.get("parent"), "path": path,
                            "laps": round(dur / ((lo + hi) / 2), 3), "via": st.get("via"), "note": st.get("n"),
                            "hole": st.get("hole"), "risk": st.get("risk", 0), "event": st.get("ev"),
                            "won": won, "crystals": won * WIN_PAY, "with": s.get("with"), "sit": len(sittings),
                        })
                        t += st["m"]
                    sittings.append({"day": d, "t0": t0, "t1": t, "with": s.get("with")})
            steps.sort(key=lambda x: x["t"])
            sittings.sort(key=lambda x: x["t0"])
            for a, b in zip(sittings, sittings[1:]):
                if b["t0"] < a["t1"]:
                    raise JourneyError(f"{p['id']}.{layer}: sittings overlap on day {b['day']}")
            # renumber sittings in time order
            remap, k = {}, 0
            for st in steps:
                if st["sit"] not in remap:
                    remap[st["sit"]] = k
                    k += 1
            for st in steps:
                st["sit"] = remap[st["sit"]]
            events = []
            for e in lay.get("events", []):
                if e.get("kind") not in KINDS:
                    raise JourneyError(f"{p['id']}.{layer}: event kind must be one of {sorted(KINDS)}")
                events.append({"day": e["day"], "t": (e["day"] - 1) * 1440 + hhmm(e["at"], p["id"]),
                               "kind": e["kind"], "note": e["n"]})
            bank, bought = 0, 0
            for st in steps:
                bank += st["crystals"]
                while bank >= VESSEL_PRICE * (bought + 1):
                    bought += 1
                    events.append({"day": st["day"], "t": st["t"] + st["dur_s"] // 60, "kind": "own", "auto": True,
                                   "note": f"Can afford vessel {bought} ({VESSEL_PRICE * bought} crystals earned)."})
            events.sort(key=lambda e: e["t"])
            for u in lay.get("uses", []):
                if u not in nodes:
                    raise JourneyError(f"{p['id']}.{layer}: uses {u!r} is not in the atlas")
            out["layers"][layer] = {
                "fate": fate, "uses": lay.get("uses", []), "steps": steps, "sittings": sittings, "events": events,
                "summary": summarise(steps, sittings, fate, horizon, nodes),
            }
        personas.append(out)
    loops = sorted(nodes)  # every loop, so the page can also place the simulation's steps
    return {
        "horizon": horizon, "start": doc.get("start", "mon"), "price": VESSEL_PRICE, "pay": WIN_PAY,
        "loops": {i: {k: nodes[i].get(k) for k in ("name", "tier", "t", "parent", "kind", "in", "drivers")}
                  | {"phases": [ph["n"] for ph in nodes[i].get("phases", [])]} for i in loops},
        "personas": personas,
    }


def summarise(steps: list, sittings: list, fate: dict, horizon: int, nodes: dict) -> dict:
    span = fate.get("day", horizon)
    weeks = max(span, 1) / 7
    tiers = {}
    places = {k: 0 for k in PLACES + ("other",)}
    first, seen = [], set()
    for st in steps:
        tier = nodes[st["loop"]]["tier"]
        tiers[tier] = tiers.get(tier, 0) + st["dur_s"] / 60
        place = next((x for x in st["path"] if x in PLACES), "other")
        places[place] += st["dur_s"] / 60
        if st["loop"] not in seen:
            seen.add(st["loop"])
            first.append({"loop": st["loop"], "day": st["day"]})
    minutes = sum(s["t1"] - s["t0"] for s in sittings)
    return {
        "days_played": len({s["day"] for s in sittings}), "sittings": len(sittings),
        "sittings_week": round(len(sittings) / weeks, 1), "minutes_week": round(minutes / weeks),
        "sitting_min": round(minutes / len(sittings), 1) if sittings else 0,
        "crystals": sum(st["crystals"] for st in steps),
        "minutes_by_tier": {str(k): round(v) for k, v in sorted(tiers.items())},
        "minutes_by_place": {k: round(v) for k, v in places.items()},
        "holes": sorted({st["hole"] for st in steps if st["hole"]}),
        "first_visits": first, "left_on": fate.get("day"),
    }


def report(data: dict) -> str:
    lines = ["# Player journeys", "",
             "Generated by `Tools/Build/loop_journeys.py build` from `journeys.yaml`; never edit by hand. Every persona is",
             "a Proposed-layer hypothesis, nothing here is measured. `now` is the game today (master unlock on); `prop` is",
             "the same person with the Proposed layer's fixes.", "",
             "| Persona | Layer | Left on day | Sittings a week | Minutes a sitting | Minutes a week | Crystals | Holes hit |",
             "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    for p in data["personas"]:
        for layer in LAYERS:
            s = p["layers"][layer]["summary"]
            left = s["left_on"] or f"still playing at {data['horizon']}"
            lines.append(f"| {p['name']} | {layer} | {left} | {s['sittings_week']} | {s['sitting_min']} | "
                         f"{s['minutes_week']} | {s['crystals']} | {len(s['holes'])} |")
    lines += [""]
    for p in data["personas"]:
        lines += [f"## {p['name']}", "", p["who"], "", f"**Challenge.** {p['challenge']}", ""]
        for layer in LAYERS:
            lay = p["layers"][layer]
            path = " > ".join(f"{data['loops'][f['loop']]['name']} (d{f['day']})" for f in lay["summary"]["first_visits"][:10])
            lines += [f"**{layer}**: {lay['fate'].get('why', '')}", "", f"Activation path: {path}", ""]
            for h in lay["summary"]["holes"]:
                lines.append(f"- hole: {h}")
            lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def render(out_dir: Path, sim: str | None = None) -> None:
    """The page is the template as it is; it fetches journeys.json, and sim.json when present, from beside itself.

    sim is the economy and timescale simulation's output (Docs/LoopAtlas/sim/out/journeys.json); the page shows its
    scenarios as three more layers for the personas both files share."""
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "index.html").write_text(TEMPLATE.read_text(encoding="utf-8"), encoding="utf-8")
    (out_dir / "journeys.json").write_text(OUT.read_text(encoding="utf-8"), encoding="utf-8")
    if sim:
        (out_dir / "sim.json").write_text(Path(sim).read_text(encoding="utf-8"), encoding="utf-8")


def build(write: bool) -> list[str]:
    nodes, handoffs, _ = load_model()
    doc = yaml.safe_load(SRC.read_text(encoding="utf-8"))
    data = expand(doc, nodes, handoffs)
    want = {OUT: json.dumps(data, separators=(",", ":"), ensure_ascii=False) + "\n", REPORT: report(data)}
    stale = [p.name for p, body in want.items() if not p.exists() or p.read_text(encoding="utf-8") != body]
    if write:
        for p, body in want.items():
            p.write_text(body, encoding="utf-8")
    return stale, data


def self_test() -> None:
    nodes, handoffs, _ = load_model()
    good = yaml.safe_load(SRC.read_text(encoding="utf-8"))
    expand(copy.deepcopy(good), nodes, handoffs)

    def refuses(name, mutate):
        doc = copy.deepcopy(good)
        mutate(doc)
        try:
            expand(doc, nodes, handoffs)
        except JourneyError:
            print(f"  refuses {name}")
            return
        raise SystemExit(f"self-test: accepted {name}")

    s0 = lambda d: d["personas"][0]["layers"]["now"]["sittings"][0]
    refuses("an unknown loop", lambda d: s0(d)["steps"][0].update(l="nope"))
    refuses("an unknown handoff", lambda d: s0(d)["steps"][0].update(via="teleport"))
    refuses("zero minutes", lambda d: s0(d)["steps"][0].update(m=0))
    refuses("a bad time", lambda d: s0(d).update(at="25:00"))
    refuses("an unknown field", lambda d: s0(d)["steps"][0].update(colour="red"))
    refuses("a day past the horizon", lambda d: s0(d).update(day=999))
    refuses("overlapping sittings", lambda d: d["personas"][0]["layers"]["now"]["sittings"].append(copy.deepcopy(s0(d))))
    refuses("an unknown co-player", lambda d: s0(d).update(**{"with": "ghost"}))
    refuses("a missing layer", lambda d: d["personas"][0]["layers"].pop("prop"))
    print("loop_journeys: self-test passed")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", nargs="?", default="build", choices=["build", "self-test"])
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--html")
    ap.add_argument("--sim", help="the simulation's journeys.json, published beside the page as sim.json")
    a = ap.parse_args()
    try:
        if a.cmd == "self-test":
            self_test()
            return 0
        stale, data = build(write=not a.check)
        if a.check:
            if stale:
                print(f"loop_journeys: stale {stale}; run python3 Tools/Build/loop_journeys.py build")
                return 1
            print(f"loop_journeys: {len(data['personas'])} personas valid and journeys.json up to date")
            return 0
        print(f"wrote {OUT.relative_to(ROOT.parents[1])} and {REPORT.name}")
        if a.html:
            render(Path(a.html), a.sim)
            print(f"wrote {a.html}/index.html and journeys.json (publish journeys.json beside the page)")
    except JourneyError as e:
        print(f"loop_journeys: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
