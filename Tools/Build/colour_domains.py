#!/usr/bin/env python3
"""The six colour domains (Fire, Gold, Lime, Jade, Blue, Ruby): which family a colour belongs to, and the forbidden band.

THE SOURCE OF TRUTH is Docs/VesselModels/colour_domains.json (Garrett's "shaped by eye", adopted 2026-10-31, shaped in
the Colour Domains artifact https://claude.ai/artifact/QT7nMWURMQgznfAnAg2FXh). Docs/VesselModels/COLOR_DOMAINS.md
explains it. This tool READS it and writes nothing.

THE RULE (OKLab, x = [L, a, b]): each family f has a score A_f(x) = n_f . x + c_f. A colour belongs to the family that
scores highest, unless it is within margin/2 of that family's border with any other family g, measured as the OKLab
distance to the plane A_f = A_g: (A_f - A_g) / |n_f - n_g| < margin/2. Then it is in the forbidden band. The cells are
convex and fill OKLab with no gaps, and the band is the same thickness on every border.

    python3 Tools/Build/colour_domains.py classify '#00D4FF' '#99FF80'   # which family, how far inside its border
    python3 Tools/Build/colour_domains.py --report                       # every anchor, the element shades, the shares
    python3 Tools/Build/colour_domains.py --check                        # exit 1 on any problem below
    python3 Tools/Build/colour_domains.py --self-test                    # proves --check fails when it should

--check fails when:
  - an anchor's colour in the game (read live from the palette, the HUD config, the vessel scheme, the kite view) no
    longer matches the definitions, naming the asset to look at;
  - an anchor does not rest in its own family, clear of the band (except the listed exceptions, each with its reason);
  - a UI theme colour named for a family (UITheme.asset cta, danger, controlActive, purchase, tooltip, selection) is
    not in that family, clear of the band (except the listed exceptions);
  - one of the four Lime element shades (OriginalColorSetSO ElementalCrystalColors) is not in Lime, clear of the band;
  - the measured shares written in the definitions are stale (more than 0.1 point off a fresh measurement);
  - the Colour Domains lab (Docs/Studios/ColourDomainsLab.html, the artifact's source) embeds a different truth.
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "vessel_model"))
sys.path.insert(0, os.path.join(REPO, ".claude", "skills", "vessel-studio"))
import domain_colors  # noqa: E402  (the domains' UI colours, read the way the studios read them)
import kite_mote_colors as kite  # noqa: E402  (oklab, the palette/HUD readers, the kite view's lime)
import vessel_colors  # noqa: E402  (the vessel body, glass and engine)

DEFS = os.path.join(REPO, "Docs", "VesselModels", "colour_domains.json")
UI_THEME = os.path.join(REPO, "Assets", "_SO_Assets", "UI", "UITheme.asset")
LAB = os.path.join(REPO, "Docs", "Studios", "ColourDomainsLab.html")   # the shaping page; it embeds the same truth
FAMILIES = ("fire", "gold", "lime", "jade", "blue", "ruby")
ANCHOR_TOL = 0.0006        # sRGB per channel: the definitions store 4 decimals
SHARE_TOL = 0.1            # percentage points
GRID = 24                  # the shares are measured on a (GRID+1)^3 sRGB grid


def load(path=DEFS):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


# ----------------------------------------------------------------------------- the rule
def classify(defs, rgb):
    """(family or None for the band, the family that scores highest, OKLab distance to its nearest border)."""
    x = kite.oklab(rgb)
    cells = defs["cells"]
    score = {f: sum(n * v for n, v in zip(cells[f]["n"], x)) + cells[f]["c"] for f in FAMILIES}
    top = max(FAMILIES, key=lambda f: score[f])
    dist = math.inf
    for g in FAMILIES:
        if g == top:
            continue
        dn = [a - b for a, b in zip(cells[top]["n"], cells[g]["n"])]
        dist = min(dist, (score[top] - score[g]) / math.hypot(*dn))
    return (top if dist >= defs["margin"] / 2 else None), top, dist


def shares(defs):
    count = {f: 0 for f in FAMILIES}
    band = 0
    n = GRID + 1
    for r in range(n):
        for g in range(n):
            for b in range(n):
                fam, _, _ = classify(defs, (r / GRID, g / GRID, b / GRID))
                if fam:
                    count[fam] += 1
                else:
                    band += 1
    total = n ** 3
    return {f: round(count[f] / total * 100, 2) for f in FAMILIES}, round(band / total * 100, 2)


# ----------------------------------------------------------------------------- the game's colours, read live
def live_anchors():
    """Each anchor id -> (its colour in the game now, where it is read from)."""
    palette, ladder, view = (kite.read(p) for p in (kite.PALETTE, kite.LADDER, kite.VIEW))
    dom = domain_colors.asset_colors(palette)
    m = re.search(r"static readonly Color Lime = new\(%sf, %sf, %sf" % (kite.NUM, kite.NUM, kite.NUM), view)
    if not m:
        raise SystemExit("colour_domains: KiteNavigatorView's Lime constant not found")
    s = vessel_colors.SCHEME
    pal, lad, scheme, dc = "OriginalColorSetSO.asset", "ElementalBarsConfig.asset", "vessel_colors.py SCHEME", "OriginalColorSetSO.asset (domain_colors)"
    return {
        "fire-0": (kite.peak1(kite.yaml_color(palette, "Danger")), pal + " Danger"),
        "fire-1": (kite.yaml_color(ladder, "fireColor"), lad + " fireColor"),
        "fire-2": (s["engine"]["rim"], scheme + " engine rim"),
        "fire-3": (s["engine"]["face"], scheme + " engine face"),
        "gold-0": (dom["gold"], dc + " GoldColors.TrailHighlightColor"),
        "lime-0": (kite.peak1(kite.yaml_color(palette, "DarkCTA")), pal + " DarkCTA"),
        "lime-1": (kite.yaml_color(palette, "BrightCTA"), pal + " BrightCTA"),
        "lime-2": (tuple(float(v) for v in m.groups()), "KiteNavigatorView.cs Lime"),
        "jade-0": (dom["jade"], dc + " JadeColors.TrailHighlightColor"),
        "jade-1": (kite.peak1(dom["jade"]), dc + " JadeColors.TrailHighlightColor, brightest channel 1"),
        "blue-0": (dom["blue"], dc + " BlueColors.TrailHighlightColor"),
        "blue-1": (kite.yaml_color(ladder, "blueColor"), lad + " blueColor"),
        "blue-2": (s["body"]["rim"], scheme + " body rim"),
        "blue-3": (s["body"]["face"], scheme + " body face"),
        "blue-4": (s["window"]["rim"], scheme + " window rim"),
        "blue-5": (s["window"]["face"], scheme + " window face"),
        "blue-6": ((0.0, 0.0, 0.0), "black (every grey is Blue's)"),
        "blue-7": ((1.0, 1.0, 1.0), "white (every grey is Blue's)"),
        "ruby-0": (dom["ruby"], dc + " RubyColors.TrailHighlightColor"),
        "ruby-1": (kite.peak1(dom["ruby"]), dc + " RubyColors.TrailHighlightColor, brightest channel 1"),
    }


def ui_tokens(defs):
    """(token, the family it stands for, its colour in UITheme.asset)."""
    spec = defs.get("ui_tokens", {}).get("tokens", {})
    if not spec:
        return []
    text = kite.read(UI_THEME)
    return [(tok, fam, kite.yaml_color(text, tok)) for tok, fam in spec.items()]


def element_shades():
    text = kite.read(kite.PALETTE)
    m = kite.PALETTE_RE.search(text)
    if not m:
        raise SystemExit("colour_domains: OriginalColorSetSO.asset has no ElementalCrystalColors")
    cols = re.findall(r"\{r: %s, g: %s, b: %s" % (kite.NUM, kite.NUM, kite.NUM), m.group(0))
    return [(name, tuple(float(v) for v in c)) for name, c in zip(kite.ELEMENTS, cols)]


# ----------------------------------------------------------------------------- the lab's copy
def lab_truth(path=LAB):
    """The truth the shaping page opens on (its ADOPTED_RAW), or None when the page is absent."""
    if not os.path.exists(path):
        return None
    m = re.search(r"const ADOPTED_RAW = (\{.*?\});\n", kite.read(path))
    if not m:
        raise SystemExit("colour_domains: %s has no ADOPTED_RAW" % os.path.relpath(path, REPO))
    return json.loads(m.group(1))


def lab_problems(defs, lab):
    errs = []
    cells = lab.get("cells") or []
    want = [v for f in FAMILIES for v in defs["cells"][f]["n"] + [defs["cells"][f]["c"]]]
    if len(cells) != 24 or max(abs(a - b) for a, b in zip(cells, want)) > 1e-6:
        errs.append("the lab's frozen cells differ from colour_domains.json (Docs/Studios/ColourDomainsLab.html ADOPTED_RAW.cells)")
    if abs(lab.get("margin", -1) - defs["margin"]) > 1e-9:
        errs.append("the lab's margin %s differs from colour_domains.json's %s" % (lab.get("margin"), defs["margin"]))
    for f in FAMILIES:
        mine = {a["id"]: a["rgb"] for a in defs["families"][f]["anchors"]}
        theirs = {a["id"]: a["c"] for a in lab.get("fams", {}).get(f, {}).get("anchors", [])}
        if sorted(mine) != sorted(theirs):
            errs.append("the lab's %s anchors (%s) differ from colour_domains.json's (%s)" % (f, ", ".join(sorted(theirs)), ", ".join(sorted(mine))))
            continue
        for k in mine:
            if max(abs(a - b) for a, b in zip(mine[k], theirs[k])) > ANCHOR_TOL:
                errs.append("the lab's anchor %s is %s, colour_domains.json says %s" % (k, kite.hexof(theirs[k]).upper(), kite.hexof(mine[k]).upper()))
    return errs


# ----------------------------------------------------------------------------- the check
def problems(defs, live=None, shades=None, measure=True, lab=False, ui=None):
    errs = []
    if sorted(defs.get("cells", {})) != sorted(FAMILIES) or sorted(defs.get("families", {})) != sorted(FAMILIES):
        return ["colour_domains.json must define cells and families for exactly %s" % ", ".join(FAMILIES)]
    for f in FAMILIES:
        c = defs["cells"][f]
        if len(c.get("n", [])) != 3 or not isinstance(c.get("c"), (int, float)):
            errs.append("cells.%s needs n (3 numbers) and c" % f)
    if errs:
        return errs
    live = live_anchors() if live is None else live
    exceptions = {e["anchor"]: e["reason"] for e in defs.get("exceptions", [])}
    seen = set()
    for f in FAMILIES:
        for a in defs["families"][f]["anchors"]:
            seen.add(a["id"])
            if a["id"] not in live:
                errs.append("%s anchor %s (%s) has no live source in this tool" % (f, a["id"], a["name"]))
                continue
            game, where = live[a["id"]]
            if max(abs(x - y) for x, y in zip(game, a["rgb"])) > ANCHOR_TOL:
                errs.append("%s anchor “%s” is %s in the definitions but %s in the game (%s): change one to match"
                            % (f, a["name"], a["hex"], kite.hexof(game).upper(), where))
                continue
            fam, top, dist = classify(defs, a["rgb"])
            if fam != f and a["id"] not in exceptions:
                errs.append("%s anchor “%s” %s is %s" % (f, a["name"], a["hex"],
                            "in %s" % top if top != f else "in the band (%.4f from its border, needs %.4f)" % (dist, defs["margin"] / 2)))
            elif fam == f and a["id"] in exceptions:
                print("colour_domains: note: %s is now clear of the band; its exception can go" % a["id"])
    for anchor_id in live:
        if anchor_id not in seen:
            errs.append("anchor %s is read from the game but missing from colour_domains.json" % anchor_id)
    for tok, fam_want, rgb in (ui_tokens(defs) if ui is None else ui):
        fam, top, dist = classify(defs, rgb)
        key = "ui:" + tok
        if fam != fam_want and key not in exceptions:
            errs.append("UI theme colour %s %s should be %s but is %s (UITheme.asset)" % (tok, kite.hexof(rgb).upper(), fam_want,
                        "in %s" % top if top != fam_want else "in the band (%.4f from its border)" % dist))
        elif fam == fam_want and key in exceptions:
            print("colour_domains: note: %s is now inside %s; its exception can go" % (key, fam_want))
    for name, rgb in (element_shades() if shades is None else shades):
        fam, top, dist = classify(defs, rgb)
        if fam != "lime":
            errs.append("Lime element shade %s %s is %s" % (name, kite.hexof(rgb).upper(),
                        "in %s" % top if top != "lime" else "in the band"))
    lab = lab_truth() if lab is False else lab
    if lab is not None:
        errs += lab_problems(defs, lab)
    if measure:
        got, band = shares(defs)
        want = defs.get("measured", {})
        for f in FAMILIES:
            if abs(got[f] - want.get("shares", {}).get(f, -99)) > SHARE_TOL:
                errs.append("measured.shares.%s says %s%% but the cells give %.2f%% (re-run --report and update it)"
                            % (f, want.get("shares", {}).get(f), got[f]))
        if abs(band - want.get("band", -99)) > SHARE_TOL:
            errs.append("measured.band says %s%% but the cells give %.2f%%" % (want.get("band"), band))
    return errs


def report(defs):
    live = live_anchors()
    for f in FAMILIES:
        for a in defs["families"][f]["anchors"]:
            fam, top, dist = classify(defs, a["rgb"])
            state = ("clear, %.1f JND" % ((dist - defs["margin"] / 2) / 0.02)) if fam == f else ("BAND" if top == f else "IN " + top.upper())
            game = kite.hexof(live[a["id"]][0]).upper() if a["id"] in live else "?"
            print("  %-5s %-24s %s  game %s  %s" % (f, a["name"], a["hex"], game, state))
    for tok, fam_want, rgb in ui_tokens(defs):
        fam, top, dist = classify(defs, rgb)
        print("  ui %-14s %s  %s" % (tok, kite.hexof(rgb).upper(), ("%s, clear %.1f JND" % (fam, (dist - defs["margin"] / 2) / 0.02)) if fam == fam_want
                                     else "%s? %s (%.4f from its border)" % (fam_want, ("IN " + top.upper()) if top != fam_want else "BAND", dist)))
    for name, rgb in element_shades():
        fam, top, dist = classify(defs, rgb)
        print("  lime element %-6s %s  %s" % (name, kite.hexof(rgb).upper(), "clear, %.1f JND" % ((dist - defs["margin"] / 2) / 0.02) if fam == "lime" else "NOT LIME"))
    got, band = shares(defs)
    print("  shares of the sRGB gamut (%d^3 grid): %s, band %.2f%%" % (GRID + 1, ", ".join("%s %.2f%%" % (f, got[f]) for f in FAMILIES), band))
    print('  as JSON for "measured": ' + json.dumps({"shares": got, "band": band}))


def self_test():
    """Each failure --check exists to catch, planted in memory; every one must be named."""
    defs = load()
    live, shades = live_anchors(), element_shades()
    assert not problems(defs, live, shades, measure=False), "the shipped definitions must pass first"
    cases = []
    d = json.loads(json.dumps(defs)); d["families"]["jade"]["anchors"][0]["rgb"] = [0.0, 0.83, 1.0]; d["families"]["jade"]["anchors"][0]["hex"] = "#00D4FF"
    cases.append(("an anchor drifted from the game", d, live, shades, "in the game"))
    lv = dict(live); lv["gold-0"] = ((1.0, 0.5, 0.0), "planted")
    cases.append(("the game's colour moved", defs, lv, shades, "Gold UI"))
    d = json.loads(json.dumps(defs)); d["cells"]["ruby"]["c"] += 0.5
    cases.append(("a cell swallowed its neighbours' anchors", d, live, shades, "is in ruby"))
    sh = list(shades); sh[0] = ("Charge", (1.0, 0.85, 0.0))
    cases.append(("an element shade left Lime", defs, live, sh, "Lime element shade Charge"))
    d = json.loads(json.dumps(defs)); d["measured"]["shares"]["fire"] += 1.0
    cases.append(("stale measured shares", d, live, shades, "measured.shares.fire"))
    ui = ui_tokens(defs); ui2 = [(t, f, (0.0, 0.83, 1.0) if t == "purchase" else c) for t, f, c in ui]
    cases.append(("a UI family colour left its family", defs, live, shades, "UI theme colour purchase"))
    lab = lab_truth()
    if lab is not None:
        lb = json.loads(json.dumps(lab)); lb["cells"][3] += 0.01
        cases.append(("the lab embeds a different truth", defs, live, shades, "lab's frozen cells"))
    bad = 0
    for label, dd, lv, sh, needle in cases:
        errs = problems(dd, lv, sh, measure=(label == "stale measured shares"),
                        lab=(lb if label.startswith("the lab") else None), ui=(ui2 if label.startswith("a UI") else None))
        hit = any(needle in e for e in errs)
        print("  %-45s %s" % (label, "caught" if hit else "MISSED"))
        bad += not hit
    return bad


def main(argv):
    defs = load()
    if argv[:1] == ["classify"]:
        for h in argv[1:]:
            h = h if h.startswith("#") else "#" + h
            rgb = tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))
            fam, top, dist = classify(defs, rgb)
            print("%s  %s" % (h.upper(), ("%s, %.1f JND inside its border" % (fam, (dist - defs["margin"] / 2) / 0.02)) if fam
                                       else "forbidden band (nearest: %s, %.4f from its border)" % (top, dist)))
        return 0
    if "--report" in argv:
        report(defs)
        return 0
    if "--self-test" in argv:
        bad = self_test()
        print("colour_domains --self-test:", "ok" if not bad else "%d planted problem(s) missed" % bad)
        return 1 if bad else 0
    if "--check" in argv:
        errs = problems(defs)
        for e in errs:
            print("  " + e)
        print("colour_domains --check:", "ok" if not errs else "%d problem(s)" % len(errs))
        return 1 if errs else 0
    print(__doc__)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
