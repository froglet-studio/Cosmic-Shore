"""Compare human ratings exported from the viewer with the probe and the authored labels.

    python ratings.py emotion_ratings.json [more.json ...]

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
            probe = max(r["probe"], key=r["probe"].get) if r.get("probe") else None
            rows.append((label, h, r.get("truth"), probe))
        pooled += rows
        lab = [x for x in rows if x[2]]
        print(f"{p}: {len(rows)} rated | human = authored {sum(h == t for _, h, t, _ in lab)}/{len(lab)} | "
              f"probe = human {sum(h == q for _, h, _, q in rows)}/{len(rows)}")
    conf = Counter((t or "game", h) for _, h, t, _ in pooled)
    print("authored/source -> human:", dict(conf))


if __name__ == "__main__":
    main(sys.argv[1:])
