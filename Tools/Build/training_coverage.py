#!/usr/bin/env python3
"""training_coverage.py - what the microgame training system covers, card by card (TRAINING_PLAN phase 6).

A READER: it writes one generated report, never an asset.

For every card on the Arcade and Arena screens (ArcadeGames.asset, ArenaGames.asset) it reads:
  - PREVIEW   the card's ModePreviewDefinitionSO (Assets/_SO_Assets/Mode Previews) and whether it can
              be flown (CanTestFlight: a PreviewCell, or a cell per intensity).
  - LESSON    per hull the card seats: the hull's ElementalAbilityMap (Resources/ElementalAbilityMaps),
              which the Lesson's ability steps and the Mentor's derived ability tips read. The Lesson
              template itself is chosen by flight scheme, so every hull has one.
  - MENTOR    authored tips the Mentor would say on that card: a mode list (DrillLibrary.modeTips) or
              a metric list (DrillLibrary.metricTips, keyed on the preview's ObjectiveMetric, which is
              what ModePreviewSession hands the drill). Without either the Mentor says only the
              derived ability tips.
  - GOTW      whether the card is in the Game of the Week rotation (Resources/GameOfTheWeek.asset):
              first login lands on that card, so it must be fully covered.

    python3 Tools/Build/training_coverage.py            print the summary
    python3 Tools/Build/training_coverage.py --write    regenerate Docs/ModePreview/TRAINING_COVERAGE.md
    python3 Tools/Build/training_coverage.py --check    fail if the report is stale, or if a Game of
                                                        the Week card is not fully covered
    python3 Tools/Build/training_coverage.py --self-test   negative controls
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REPORT = "Docs/ModePreview/TRAINING_COVERAGE.md"
LISTS = [("Arcade", "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"),
         ("Arena", "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset")]
PREVIEWS = "Assets/_SO_Assets/Mode Previews"
LIBRARY = "Assets/Resources/DrillLibrary.asset"
GOTW = "Assets/Resources/GameOfTheWeek.asset"
ABILITY_MAPS = "Assets/Resources/ElementalAbilityMaps"
CLASSES = "Assets/_SO_Assets/Classes"


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()


def enum_names(rel, enum):
    src = read(rel)
    body = src[src.index("enum " + enum):]
    body = body[:body.index("}")]
    return {int(v): n for n, v in re.findall(r"^\s*(\w+)\s*=\s*(-?\d+)", body, re.M)}


def guid_index():
    """guid -> asset path, for every .asset under Assets/_SO_Assets and Assets/Resources."""
    out = {}
    for base in ("Assets/_SO_Assets", "Assets/Resources"):
        for d, _, files in os.walk(os.path.join(ROOT, base)):
            for f in files:
                if f.endswith(".asset.meta"):
                    m = re.search(r"^guid: (\w+)", open(os.path.join(d, f), encoding="utf-8").read(), re.M)
                    if m:
                        out[m.group(1)] = os.path.relpath(os.path.join(d, f[:-5]), ROOT)
    return out


def field(text, name, default=None):
    m = re.search(rf"^  {name}: ?(.*)$", text, re.M)
    return m.group(1).strip() if m else default


def guid_list(text, name):
    m = re.search(rf"^  {name}:\n((?:  - .*\n)*)", text, re.M)
    return re.findall(r"guid: (\w+)", m.group(1)) if m else []


def int_list(text, name):
    m = re.search(rf"^  {name}:\n((?:  - .*\n)*)", text, re.M)
    return [int(v) for v in re.findall(r"-\s*(-?\d+)", m.group(1))] if m else []


def library_keys(text, block, key):
    """The Mode / Metric of each row of a DrillLibrary mapping list whose tip list is assigned."""
    m = re.search(rf"^  {block}:\n((?:  - .*\n(?:    .*\n)*)*)", text, re.M)
    if not m:
        return set()
    keys = set()
    for row in re.split(r"^  - ", m.group(1), flags=re.M)[1:]:
        k = re.search(rf"^(?:    )?{key}: (-?\d+)", row, re.M)
        tips = re.search(r"guid: (\w+)", row)
        if k and tips:
            keys.add(int(k.group(1)))
    return keys


def collect():
    modes = enum_names("Assets/_Scripts/Data/Enums/GameModes.cs", "GameModes")
    metrics = enum_names("Assets/_Scripts/Data/Enums/ScoringMetric.cs", "ScoringMetric")
    idx = guid_index()

    previews = {}
    for f in sorted(os.listdir(os.path.join(ROOT, PREVIEWS))):
        if f.endswith(".asset"):
            t = read(f"{PREVIEWS}/{f}")
            previews[int(field(t, "Mode", "-1"))] = {
                "flyable": "guid:" in (field(t, "PreviewCell") or "").replace(" ", "")
                           or bool(guid_list(t, "PreviewCellsByIntensity")),
                "metric": int(field(t, "ObjectiveMetric", "-1")),
                "vessel": int(field(t, "Vessel", "0")),
            }

    lib = read(LIBRARY)
    mode_tips = library_keys(lib, "modeTips", "Mode")
    metric_tips = library_keys(lib, "metricTips", "Metric")
    gotw = set(int_list(read(GOTW), "rotation")) | {int(field(read(GOTW), "fallback", "-1"))}
    maps = {f[:-6] for f in os.listdir(os.path.join(ROOT, ABILITY_MAPS)) if f.endswith(".asset")}

    cards = []
    for screen, rel in LISTS:
        for g in guid_list(read(rel), "Games"):
            path = idx.get(g)
            if not path:
                cards.append({"screen": screen, "name": f"(missing asset {g})", "mode": -1, "error": True})
                continue
            t = read(path)
            mode = int(field(t, "Mode", "-1"))
            hulls = []
            for vg in guid_list(t, "Vessels"):
                vt = read(idx[vg]) if vg in idx else ""
                hulls.append(field(vt, "Name") or "?")
            p = previews.get(mode)
            metric = p["metric"] if p else None
            cards.append({
                "screen": screen,
                "name": (field(t, "DisplayName") or "").strip("'\""),
                "mode": mode,
                "mode_name": modes.get(mode, str(mode)),
                "hulls": hulls,
                "hulls_no_map": [h for h in hulls if h not in maps],
                "preview": bool(p),
                "flyable": bool(p and p["flyable"]),
                "mode_tips": mode in mode_tips,
                "metric": metrics.get(metric, "-") if metric is not None else "-",
                "metric_tips": metric in metric_tips,
                "gotw": mode in gotw,
            })
    return cards


def covered(c):
    return c.get("flyable") and not c.get("hulls_no_map") and (c.get("mode_tips") or c.get("metric_tips"))


def problems(cards):
    """Hard failures: a Game of the Week card that is not fully covered, or a list naming no asset."""
    out = []
    for c in cards:
        if c.get("error"):
            out.append(f"{c['screen']}: {c['name']}")
        elif c["gotw"] and not covered(c):
            out.append(f"{c['name']} is in the Game of the Week rotation but is not fully covered "
                       f"(preview flyable: {c['flyable']}, hulls without an ability map: "
                       f"{c['hulls_no_map'] or 'none'}, authored Mentor tips: "
                       f"{c['mode_tips'] or c['metric_tips']})")
    return out


def render(cards):
    yes = lambda b: "yes" if b else "**no**"
    full = sum(1 for c in cards if not c.get("error") and covered(c))
    lines = [
        "# Training coverage (generated)",
        "",
        "Generated by `python3 Tools/Build/training_coverage.py --write`; `--check` fails when this file is stale or",
        "when a Game of the Week card is not fully covered. Do not edit by hand. What each column means:",
        "the script's docstring. The plan it measures: `Docs/ModePreview/TRAINING_PLAN.md` phases 5 and 6.",
        "",
        f"**{full} of {len(cards)} cards are fully covered** (a flyable preview, an ability map for every hull, "
        "and authored Mentor tips).",
        "",
        "| Screen | Card | Mode | GotW | Preview flies | Hulls | Hulls without an ability map | Mentor tips (mode) | Metric | Mentor tips (metric) |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for c in sorted(cards, key=lambda c: (c["screen"], not c.get("gotw"), c["name"])):
        if c.get("error"):
            lines.append(f"| {c['screen']} | {c['name']} | | | | | | | | |")
            continue
        lines.append(
            f"| {c['screen']} | {c['name']} | {c['mode_name']} | {'yes' if c['gotw'] else ''} | "
            f"{yes(c['flyable'])} | {', '.join(c['hulls']) or '-'} | {', '.join(c['hulls_no_map']) or ''} | "
            f"{'yes' if c['mode_tips'] else ''} | {c['metric']} | {'yes' if c['metric_tips'] else ''} |")
    return "\n".join(lines) + "\n"


def self_test():
    base = {"screen": "Arcade", "name": "X", "mode": 1, "mode_name": "X", "hulls": ["A"], "hulls_no_map": [],
            "preview": True, "flyable": True, "mode_tips": True, "metric": "M", "metric_tips": False, "gotw": True}
    fired = 0
    assert not problems([dict(base)]), "a clean card must pass"
    for mutation in ({"flyable": False}, {"hulls_no_map": ["A"]}, {"mode_tips": False}):
        if problems([dict(base, **mutation)]):
            fired += 1
        else:
            print(f"negative control did not fire: {mutation}")
            return 1
    if not problems([{"screen": "Arcade", "name": "(missing asset x)", "mode": -1, "error": True}]):
        print("negative control did not fire: missing card asset")
        return 1
    fired += 1
    assert not problems([dict(base, gotw=False, flyable=False)]), "an uncovered card off the rotation is a gap, not a failure"
    print(f"self-test: clean card passes; {fired} negative controls fire")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    cards = collect()
    text = render(cards)
    if "--write" in argv:
        with open(os.path.join(ROOT, REPORT), "w", encoding="utf-8") as f:
            f.write(text)
        print(f"wrote {REPORT}")
    errs = problems(cards)
    if "--check" in argv:
        path = os.path.join(ROOT, REPORT)
        if not os.path.exists(path) or open(path, encoding="utf-8").read() != text:
            errs.append(f"{REPORT} is stale: run python3 Tools/Build/training_coverage.py --write")
    full = sum(1 for c in cards if not c.get("error") and covered(c))
    for e in errs:
        print("FAIL:", e)
    print(f"training coverage: {full} of {len(cards)} cards fully covered; "
          f"{'ok' if not errs else str(len(errs)) + ' problem(s)'}")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
