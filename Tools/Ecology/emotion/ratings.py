"""Compare human ratings exported from the viewer with the probe and the authored labels.

    python ratings.py emotion_ratings.json [more.json ...]

Also reads the flyable ecology's export (flight/index.html: rate panel -> export JSON). Its runs carry probe = null
(the page rates live encounters, not probe-scored clips), so only human-vs-authored agreement applies, and its
"encounters" list adds the 1-5 threat / readability / fun scales, summarised per species.

Reports, per rater and pooled: agreement of the human with the authored label (are our archetypes what we
said they are?), agreement of the probe with the human (does the probe measure what people feel?), and a
confusion table. With one rater this is anecdote; it becomes evidence at ~5+ raters per run.
"""
import json
import sys
from collections import Counter

EMO = ("cute", "playful", "eerie", "majestic", "menacing", "terrifying")


def main(paths):
    pooled = []
    for p in paths:
        d = json.load(open(p)); runs = {r["label"]: r for r in d["runs"]}
        rows = []
        for label, h in d["ratings"].items():
            r = runs.get(label)
            if not r:
                continue
            probe = max(r["probe"], key=r["probe"].get) if r.get("probe") else None   # null for flight-page encounters
            rows.append((label, h, r.get("truth"), probe))
        pooled += rows
        lab = [x for x in rows if x[2]]
        print(f"{p}: {len(rows)} rated | human = authored {sum(h == t for _, h, t, _ in lab)}/{len(lab)} | "
              f"probe = human {sum(h == q for _, h, _, q in rows if q)}/{sum(1 for x in rows if x[3])}")
    conf = Counter((t or "game", h) for _, h, t, _ in pooled)
    print("authored/source -> human:", dict(conf))
    scales = {}
    for p in paths:
        for e in json.load(open(p)).get("encounters", []):
            for k in ("threat", "readability", "fun"):
                if isinstance(e.get(k), (int, float)):
                    scales.setdefault(e.get("species", "?"), {}).setdefault(k, []).append(e[k])
    for sp, d in sorted(scales.items()):
        print(f"  {sp:10s} " + "  ".join(f"{k} {sum(v) / len(v):.1f} (n={len(v)})" for k, v in d.items()))


if __name__ == "__main__":
    main(sys.argv[1:])
