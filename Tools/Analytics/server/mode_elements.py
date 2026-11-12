#!/usr/bin/env python3
"""Each game mode's genre element(s), read from the game's own files, for Froglet Analytics (Shombith, 2026-11-06:
"the matches over time should also have element support").

    python3 mode_elements.py              write mode_elements.json beside this file
    python3 mode_elements.py --check      exit 1 when mode_elements.json is not what the game's files say now
    python3 mode_elements.py --self-test  known answers and a negative control

The game answers "what kind of game is this mode" in ModeGenre.TryElementsFor (Assets/_Scripts/Data/ModeGenre.cs):
the mode's own row wins (Scurry, Tollway, Scarab Scramble, Brood Rush), else the fallback by the mode's scoring metric
(ModeGenre.TryElementForMetric). The metric is the one the arcade grid reads (ArcadeGridOrder.TryGenres): the mode's
preview definition in Assets/Resources/ModePreviewLibrary.asset (its Definitions, first per mode wins; Maelstrom and
Random have none by design), whose ObjectiveMetric is a ScoringMetric. The arcade cards (SO_ArcadeGame, under
Assets/_SO_Assets/Games/) carry the Mode but no metric; they are listed as `cards` so a reader can see which modes ship.

The server reads only the json (the VM's checkout has no Assets/), and uses it for matches that did not send their own
element: everything before 2026-11-06, the PostHog history included. Keys are GameModes names, the string the game
sends as game_mode (AnalyticsServiceFacade: GameMode.ToString()). Standard library only.
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(HERE, "mode_elements.json")
ELEMENTS = ("Charge", "Mass", "Space", "Time", "Omni")

PATHS = {
    "mode_genre": "Assets/_Scripts/Data/ModeGenre.cs",
    "game_modes": "Assets/_Scripts/Data/Enums/GameModes.cs",
    "scoring_metric": "Assets/_Scripts/Data/Enums/ScoringMetric.cs",
    "element": "Assets/_Scripts/Data/Enums/Element.cs",
    "preview_library": "Assets/Resources/ModePreviewLibrary.asset",
    "cards": "Assets/_SO_Assets/Games",
}


def _read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8") as handle:
        return handle.read()


def _strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def parse_enum(text, name):
    """{member: value} of `enum <name>` in C# source (every member carries an explicit value in this project)."""
    body = re.search(r"\benum\s+" + name + r"\b[^{]*\{(.*?)\}", _strip_comments(text), re.S)
    if not body:
        raise ValueError(f"enum {name} not found")
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"\b([A-Za-z_]\w*)\s*=\s*(-?\d+)", body.group(1))}


def _switch_rows(block, case_kind, assign):
    """Rows of a C# switch whose cases fall through to assignments: [(labels, {variable: Element})]."""
    rows, labels = [], []
    for line in block.splitlines():
        for label in re.findall(case_kind + r"\.(\w+)\s*:", line):
            labels.append(label)
        for var, element in re.findall(r"\b(" + assign + r")\s*=\s*Element\.(\w+)\s*;", line):
            if not labels:
                continue
            if rows and rows[-1][0] is labels:
                rows[-1][1][var] = element
            else:
                rows.append((labels, {var: element}))
        if re.search(r"\breturn\b", line) and labels:
            labels = []
    return [(list(ls), vals) for ls, vals in rows]


def _method_body(text, name):
    start = re.search(r"\bbool\s+" + name + r"\s*\(", text)       # the definition, not a call
    if not start:
        raise ValueError(f"method {name} not found")
    i = text.index("{", start.end())
    depth = 0
    for j in range(i, len(text)):
        depth += {"{": 1, "}": -1}.get(text[j], 0)
        if depth == 0:
            return text[i + 1:j]
    raise ValueError(f"method {name} has no end")


def parse_mode_genre(text):
    """(mode rows {mode: (primary, secondary|None)}, metric rows {metric: element}) from ModeGenre.cs."""
    code = _strip_comments(text)
    modes = {}
    for labels, vals in _switch_rows(_method_body(code, "TryElementsFor"), "GameModes", "primary|secondary"):
        if "primary" not in vals:
            continue
        for label in labels:
            modes[label] = (vals["primary"], vals.get("secondary"))
    metrics = {}
    for labels, vals in _switch_rows(_method_body(code, "TryElementForMetric"), "ScoringMetric", "element"):
        for label in labels:
            if vals.get("element") and vals["element"] != "None":
                metrics[label] = vals["element"]
    if not modes or not metrics:
        raise ValueError("ModeGenre.cs: no mode rows or no metric rows found; the parser needs updating")
    return modes, metrics


def _guid_index(folders):
    index = {}
    for folder in folders:
        for root, _, files in os.walk(os.path.join(REPO, folder)):
            for name in files:
                if name.endswith(".asset.meta"):
                    with open(os.path.join(root, name), encoding="utf-8", errors="replace") as handle:
                        m = re.search(r"^guid:\s*([0-9a-f]{32})", handle.read(), re.M)
                    if m:
                        index[m.group(1)] = os.path.join(root, name[:-5])
    return index


def preview_metrics(library_text, mode_names, metric_names):
    """{mode name: metric name} from the preview library's Definitions, in order, first per mode wins, as
    ModePreviewLibrarySO.Resolve does. A definition that omits ObjectiveMetric has the C# default, Crystals (0)."""
    guids = re.findall(r"\{fileID:\s*11400000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*2\}",
                       library_text.split("Definitions:", 1)[1] if "Definitions:" in library_text else "")
    index = _guid_index(["Assets/_SO_Assets", "Assets/Resources"])
    by_value = {v: k for k, v in mode_names.items()}
    metric_by_value = {v: k for k, v in metric_names.items()}
    out = {}
    for guid in guids:
        path = index.get(guid)
        if not path:
            continue
        with open(path, encoding="utf-8", errors="replace") as handle:
            asset = handle.read()
        mode = re.search(r"^\s*Mode:\s*(-?\d+)", asset, re.M)
        metric = re.search(r"^\s*ObjectiveMetric:\s*(-?\d+)", asset, re.M)
        name = by_value.get(int(mode.group(1))) if mode else None
        if not name or name in ("Maelstrom", "Random") or name in out:    # IsPreviewable; first entry wins
            continue
        out[name] = metric_by_value.get(int(metric.group(1)) if metric else 0)
    return out


def card_modes(mode_names):
    by_value = {v: k for k, v in mode_names.items()}
    found = set()
    for root, _, files in os.walk(os.path.join(REPO, PATHS["cards"])):
        for name in files:
            if not name.endswith(".asset"):
                continue
            with open(os.path.join(root, name), encoding="utf-8", errors="replace") as handle:
                text = handle.read()
            if "m_Script" not in text:
                continue
            m = re.search(r"^  Mode:\s*(-?\d+)", text, re.M)
            if m and int(m.group(1)) in by_value:
                found.add(by_value[int(m.group(1))])
    return sorted(found)


def elements_for(mode, metric, mode_rows, metric_rows):
    """ModeGenre.TryElementsFor: the mode's own row, else its metric's element, else none."""
    if mode in mode_rows:
        return list(mode_rows[mode])
    if metric and metric in metric_rows:
        return [metric_rows[metric], None]
    return None


def build():
    game_modes = parse_enum(_read(PATHS["game_modes"]), "GameModes")
    metrics = parse_enum(_read(PATHS["scoring_metric"]), "ScoringMetric")
    elements = parse_enum(_read(PATHS["element"]), "Element")
    mode_rows, metric_rows = parse_mode_genre(_read(PATHS["mode_genre"]))
    for name, pair in list(mode_rows.items()) + [(k, (v, None)) for k, v in metric_rows.items()]:
        for e in pair:
            if e is not None and e not in elements:
                raise ValueError(f"{name}: Element.{e} is not in Element.cs")
    unknown = [m for m in mode_rows if m not in game_modes] + [m for m in metric_rows if m not in metrics]
    if unknown:
        raise ValueError(f"ModeGenre.cs names members the enums do not have: {unknown}")
    metric_of = preview_metrics(_read(PATHS["preview_library"]), game_modes, metrics)
    modes, none = {}, []
    for name in sorted(game_modes, key=lambda n: game_modes[n]):
        answer = elements_for(name, metric_of.get(name), mode_rows, metric_rows)
        if answer:
            modes[name] = answer
        else:
            none.append(name)
    return {
        "$comment": "GENERATED by Tools/Analytics/server/mode_elements.py from the game's ModeGenre.cs, GameModes.cs and "
                    "ModePreviewLibrary.asset; do not edit. game_mode -> [primary element, secondary element or null]. "
                    "The analytics server uses it for matches that did not send their own element (before 2026-11-06, "
                    "the PostHog history included).",
        "sources": dict(PATHS),
        "modes": dict(sorted(modes.items())),
        "no_element": sorted(none),
        "metric": dict(sorted(metric_of.items())),
        "mode_rows": sorted(mode_rows),
        "cards": card_modes(game_modes),
    }


def render(data):
    return json.dumps(data, indent=2, sort_keys=False) + "\n"


def load(path=OUT):
    """{game_mode: (primary, secondary|None)} from the committed json; {} when it is missing or unreadable."""
    try:
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError):
        return {}
    out = {}
    for mode, pair in (data.get("modes") or {}).items():
        if isinstance(pair, list) and pair and pair[0] in ELEMENTS:
            out[str(mode)] = (pair[0], pair[1] if len(pair) > 1 and pair[1] in ELEMENTS else None)
    return out


def have_sources():
    return all(os.path.exists(os.path.join(REPO, p)) for p in PATHS.values())


def check():
    if not have_sources():
        print("mode_elements: no Unity sources in this checkout; nothing to check")
        return 0
    want = render(build())
    try:
        with open(OUT, encoding="utf-8") as handle:
            have = handle.read()
    except OSError:
        have = ""
    if have != want:
        print("mode_elements.json is stale: run python3 Tools/Analytics/server/mode_elements.py")
        return 1
    print("mode_elements.json is current")
    return 0


def self_test():
    """Known answers from ModeGenre.cs's own rows and comments, checked from the code, and a negative control: a
    ModeGenre with Tollway's row removed must fall back to its metric (Goals -> Time), so the parser is reading rows."""
    failures = []
    sample = """
    public static bool TryElementsFor(GameModes mode, ScoringMetric? metric, out Element primary, out Element secondary)
    {   secondary = Element.None;
        switch (mode)
        {   // ACQUISITION, not a race.
            case GameModes.Scurry:
            case GameModes.Tollway:
                primary = Element.Mass;
                return true;
            case GameModes.BroodRush:
                primary = Element.Mass;
                secondary = Element.Space;
                return true;
        }
        if (metric.HasValue) return TryElementForMetric(metric.Value, out primary);
        primary = Element.None; return false; }
    public static bool TryElementForMetric(ScoringMetric metric, out Element element)
    {   switch (metric)
        {   case ScoringMetric.Goals: case ScoringMetric.Crystals:
                element = Element.Time; return true;
            case ScoringMetric.Jousts:
                element = Element.Charge; return true;
            default: element = Element.None; return false; } }
    """
    rows, metric_rows = parse_mode_genre(sample)
    expect = lambda got, want, what: failures.append(f"{what}: {got} != {want}") if got != want else None
    expect(rows.get("Scurry"), ("Mass", None), "sample Scurry")
    expect(rows.get("BroodRush"), ("Mass", "Space"), "sample BroodRush")
    expect(metric_rows, {"Goals": "Time", "Crystals": "Time", "Jousts": "Charge"}, "sample metrics")
    without = sample.replace("case GameModes.Tollway:", "")
    rows2, _ = parse_mode_genre(without)
    expect(elements_for("Tollway", "Goals", rows2, metric_rows), ["Time", None], "negative control: Tollway unrowed")
    expect(elements_for("Tollway", "Goals", rows, metric_rows), ["Mass", None], "Tollway with its row")
    if have_sources():
        data = build()
        modes = data["modes"]
        for mode, want in (("Scurry", ["Mass", None]), ("Tollway", ["Mass", None]), ("ScarabScramble", ["Mass", None]),
                           ("BroodRush", ["Mass", "Space"])):
            expect(modes.get(mode), want, f"game {mode}")
        expect(data["metric"].get("Tollway"), "Goals", "Tollway's preview metric (its row is what makes it Mass)")
        expect(data["metric"].get("Scurry"), "Crystals", "Scurry's preview metric")
        _, game_metric_rows = parse_mode_genre(_read(PATHS["mode_genre"]))
        if "Joust" in data["metric"]:
            expect(modes.get("Joust"), [game_metric_rows.get(data["metric"]["Joust"]), None], "Joust by its metric")
        expect("Maelstrom" in data["no_element"], True, "Maelstrom draws other modes: no element")
        expect("SkimRace" in modes, "SkimRace" in data["metric"], "SkimRace answers exactly when it has a metric")
    for line in failures:
        print("FAIL", line)
    print("mode_elements self-test:", "ok" if not failures else f"{len(failures)} failed")
    return 1 if failures else 0


def main(argv):
    if argv[:1] == ["--check"]:
        return check()
    if argv[:1] == ["--self-test"]:
        return self_test()
    if argv:
        print(__doc__)
        return 2
    if not have_sources():
        print("mode_elements: the game's files are not in this checkout")
        return 1
    data = build()
    with open(OUT, "w", encoding="utf-8") as handle:
        handle.write(render(data))
    print(f"wrote {OUT}: {len(data['modes'])} modes with an element, {len(data['no_element'])} without")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
