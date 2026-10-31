#!/usr/bin/env python3
"""coevo_report.py - a markdown checkpoint of a coevo session: fronts, the best escort's genes, the fauna per
difficulty, win rates per difficulty, and how the objectives moved.

    python Tools/SimFarm/coevo_report.py --root D:/VoyageFarm --session s1 [--out D:/Voyage/briefs/coevo_progress.md] [--append]
"""
from __future__ import annotations

import argparse
import json
import statistics
import time
from pathlib import Path


def load(p: Path):
    return json.loads(p.read_text(encoding="utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default="D:/VoyageFarm")
    ap.add_argument("--session", required=True)
    ap.add_argument("--out", default=None)
    ap.add_argument("--append", action="store_true")
    ap.add_argument("--title", default=None)
    a = ap.parse_args()
    d = Path(a.root) / "coevo" / a.session
    st = load(d / "state.json")
    space = load(Path(st["spacePath"])) if st.get("spacePath") else {"vessel": {"genes": {}}, "fauna": {"genes": {}}}
    g = st["genomes"]
    lines = [f"## {a.title or a.session} - checkpoint {time.strftime('%Y-%m-%d %H:%M')}", ""]
    gens = sorted(d.glob("gen_*.json"))
    runs = [json.loads(l) for l in (d / "runs.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()] \
        if (d / "runs.jsonl").exists() else []
    lines.append(f"Generations done: {st['gen']}; runs scored: {len(runs)}; session folder `{d}`.")
    lines.append("")

    # Win rates per difficulty, per generation block (non-idle escorts), and the idle escort's.
    idle = st.get("idle")
    by = {}
    for r in runs:
        c = r.get("coevo", {})
        key = "idle" if c.get("vessel") == idle else "escorts"
        by.setdefault((key, r["Difficulty"]), []).append(r)
    diffs = sorted({k[1] for k in by})
    lines.append("| who | " + " | ".join(f"d{x}" for x in diffs) + " |")
    lines.append("|---|" + "---|" * len(diffs))
    for who in ("escorts", "idle"):
        cells = []
        for x in diffs:
            rs = by.get((who, x), [])
            fin = [r for r in rs if r.get("Result") in (1, 2)]
            if not fin:
                cells.append("-")
                continue
            won = sum(1 for r in fin if r["Result"] == 1)
            hull = statistics.mean(r.get("HullMinFraction", 0) for r in fin)
            cells.append(f"{won}/{len(fin)} won, hull {hull:.0%}")
        lines.append(f"| {who} | " + " | ".join(cells) + " |")
    lines.append(f"\nTarget win rate for the best escorts: " + ", ".join(f"d{x} {max(0, 100 - 8 * x)}%" for x in diffs))
    lines.append("")

    # Objectives over generations.
    lines.append("| gen | evolving | regime | runs | vessel best fit | vessel best div | fauna best fit | fauna best div |")
    lines.append("|---|---|---|---|---|---|---|---|")
    for p in gens:
        r = load(p)
        cells = []
        for sd in ("vessel", "fauna"):
            arch = r["sides"][sd]["archive"]
            fits = [o[0] for o in arch.values()]
            divs = [o[1] for o in arch.values()]
            cells += [f"{max(fits):.3f}" if fits else "-", f"{max(divs):.3f}" if divs else "-"]
        lines.append(f"| {r['gen']} | {r['evolving']} | {r['regime']} | {r['runs']} | " + " | ".join(cells) + " |")
    lines.append("")

    best = load(d / "best" / "index.json") if (d / "best" / "index.json").exists() else {}
    vf = best.get("vessel_fit")
    if vf:
        genes = g[vf["gid"]]["genes"]
        vs = space["vessel"]["genes"]
        lines.append(f"**Best escort** `{vf['gid']}` (fit {vf['objectives'][0]:.3f}, diversity {vf['objectives'][1]:.3f}, "
                     f"{len(g[vf['gid']]['runs'])} runs). Genes off their default:")
        lines.append("")
        lines.append("| gene | value | default |")
        lines.append("|---|---|---|")
        for k, v in genes.items():
            if k.startswith("__"):
                continue
            dflt = vs.get(k, {}).get("default")
            if dflt is None or abs(float(v) - float(dflt)) > 1e-6:
                lines.append(f"| {k} | {v} | {dflt} |")
        mine = [r for r in g[vf["gid"]]["runs"]]
        if mine:
            per = {}
            for r in mine:
                per.setdefault(r["d"], []).append(r)
            lines.append("")
            lines.append("Its runs: " + "; ".join(
                f"d{x} {sum(1 for r in rs if r['win'])}/{len(rs)} won, kills on Ark "
                f"{sum(r.get('kos', 0) for r in rs) / max(1, sum(r.get('kills', 0) for r in rs)):.0%}, "
                f"FF {statistics.mean(r.get('ff', 0) for r in rs):.1f}" for x, rs in sorted(per.items())))
        lines.append("")
    fd = best.get("fauna_by_difficulty", {})
    if fd:
        lines.append("**Fauna per difficulty** (genome whose strong-escort win rate is nearest the target):")
        lines.append("")
        lines.append("| d | genome | escort win | target | runs | params file | genes off default |")
        lines.append("|---|---|---|---|---|---|---|")
        fs = space["fauna"]["genes"]
        for x, b in fd.items():
            genes = g[b["gid"]]["genes"]
            off = ", ".join(f"{k}={v}" for k, v in genes.items()
                            if fs.get(k, {}).get("default") is not None and abs(float(v) - float(fs[k]["default"])) > 1e-6)
            lines.append(f"| {x} | `{b['gid']}` | {b['win']:.0%} | {b['target']:.0%} | {b['runs']} | "
                         f"`best/fauna_d{x}.json` | {off or '(defaults)'} |")
        lines.append("")
    for sd in ("vessel", "fauna"):
        p = d / f"archive_{sd}.json"
        if p.exists():
            arch = load(p)
            lines.append(f"{sd} front ({len(arch)}): " + ", ".join(
                f"`{e['gid']}` ({e['objectives'][0]:.2f}, {e['objectives'][1]:.2f}; {e['runs']} runs)" for e in arch))
            lines.append("")
    text = "\n".join(lines) + "\n"
    if a.out:
        out = Path(a.out)
        if a.append and out.exists():
            out.write_text(out.read_text(encoding="utf-8") + "\n" + text, encoding="utf-8")
        else:
            out.write_text(text, encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
