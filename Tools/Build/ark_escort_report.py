#!/usr/bin/env python3
"""ark_escort_report.py - read the escort sim's JSON lines (ArkEscortSim) and print how each difficulty played.

    python3 Tools/Build/ark_escort_report.py path/to/escort.jsonl [--since N]   # N: skip the first N lines

One row per difficulty: runs, survived, mean lowest hull, the world the Ark fell in most, and each world's mean hull
loss (entry minus lowest), so a tuning session sees where the damage happens. Docs/Voyages/ARK_ESCORT.md, "Tuning".

Lines written since the cell-aware pace and plate eating also carry: time inside/outside cells and the Ark's mean
speed, kills on the Ark against elsewhere, plates lost per world (shielded / super), each seat's share of samples
within 300 u and 900 u of the Ark, per-seat round stats, and the named overrides (-arkEscortParams) the run applied.
Pacification (escort.pacifyShare): per world, the share of runs in which the squad pacified it and how long after the
Ark entered. Older lines lack those fields; their columns print blank.
"""
import argparse, collections, json, statistics, sys

OUTCOME = {0: "none", 1: "survived", 2: "fallen", 3: "abandoned"}


def mean(v):
    return statistics.mean(v) if v else None


def fmt(v, spec, width):
    return format(v, spec).rjust(width) if v is not None else " " * width


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--since", type=int, default=0)
    a = ap.parse_args()
    runs = []
    with open(a.path, encoding="utf-8") as f:
        for i, line in enumerate(f):
            if i < a.since or not line.strip():
                continue
            runs.append(json.loads(line))
    by = collections.defaultdict(list)
    for r in runs:
        if r.get("Result", 0) in (1, 2):
            by[r["Difficulty"]].append(r)
    names = []
    for r in runs:
        for l in r.get("LegReports", []):
            if l["Name"] not in names:
                names.append(l["Name"])

    # ── Hull loss per world (the original table) ──
    print(f"{'d':>2} {'runs':>4} {'won':>4} {'low hull':>8}  fell in          " + "  ".join(n[:10].rjust(10) for n in names))
    for d in sorted(by):
        rs = by[d]
        won = sum(1 for r in rs if r["Result"] == 1)
        low = statistics.mean(r["HullMinFraction"] for r in rs)
        fell = collections.Counter(r["LegReports"][r["FallenLeg"]]["Name"] for r in rs if r["Result"] == 2)
        loss = []
        for n in names:
            v = [l["HullAtEntry"] - l["HullMin"] for r in rs for l in r["LegReports"] if l["Name"] == n and l["Reached"]]
            loss.append(f"{statistics.mean(v):10.0f}" if v else " " * 10)
        print(f"{d:>2} {len(rs):>4} {won:>4} {low:8.0%}  {(fell.most_common(1)[0][0] if fell else '-'):16} " + "  ".join(loss))

    # ── Pace, kills, plates (new fields; blank for old lines) ──
    print()
    print(f"{'d':>2} {'in s':>6} {'out s':>6} {'in%':>5} {'u/s':>5} {'k@ark':>6} {'k else':>6} {'plates':>6} "
          f"{'shld':>5} {'super':>5} {'regrow':>6} {'ff':>5}   plates lost per world: " + "  ".join(n[:10].rjust(10) for n in names))
    for d in sorted(by):
        rs = by[d]
        ins = mean([r["InsideSeconds"] for r in rs if "InsideSeconds" in r])
        outs = mean([r["OutsideSeconds"] for r in rs if "OutsideSeconds" in r])
        share = ins / (ins + outs) if ins is not None and outs is not None and ins + outs > 0 else None
        cols = [
            fmt(ins, ".0f", 6), fmt(outs, ".0f", 6), fmt(share, ".0%", 5),
            fmt(mean([r["MeanArkSpeed"] for r in rs if "MeanArkSpeed" in r]), ".1f", 5),
            fmt(mean([r["KillsOnArk"] for r in rs if "KillsOnArk" in r]), ".1f", 6),
            fmt(mean([r["KillsElsewhere"] for r in rs if "KillsElsewhere" in r]), ".1f", 6),
            fmt(mean([r["PlatesLost"] for r in rs if "PlatesLost" in r]), ".1f", 6),
            fmt(mean([r["PlatesLostShielded"] for r in rs if "PlatesLostShielded" in r]), ".1f", 5),
            fmt(mean([r["PlatesLostSuper"] for r in rs if "PlatesLostSuper" in r]), ".1f", 5),
            fmt(mean([r["PlatesRegrown"] for r in rs if "PlatesRegrown" in r]), ".1f", 6),
            fmt(mean([r["FriendlyFire"] for r in rs if "FriendlyFire" in r]), ".1f", 5),
        ]
        per = []
        for n in names:
            v = [l["PlatesLost"] for r in rs for l in r["LegReports"] if l["Name"] == n and l["Reached"] and "PlatesLost" in l]
            per.append(fmt(mean(v), ".1f", 10))
        print(f"{d:>2} " + " ".join(cols) + "                          " + "  ".join(per))

    # ── Pacification per world: share of runs pacified (mean seconds after entry; 'a' = some pacified ahead) ──
    if any("PacifiedLegs" in r for r in runs):
        print()
        print(f"{'d':>2} {'pacified':>8}   per world: " + "  ".join(n[:10].rjust(10) for n in names))
        for d in sorted(by):
            rs = by[d]
            tot = mean([r.get("PacifiedLegs", 0) for r in rs if "PacifiedLegs" in r])
            per = []
            for n in names:
                ls = [l for r in rs for l in r["LegReports"] if l["Name"] == n and "Pacified" in l]
                if not ls:
                    per.append(" " * 10)
                    continue
                share = sum(1 for l in ls if l["Pacified"]) / len(ls)
                after = [l["PacifiedAt"] - l["EnteredAt"] for l in ls if l["Pacified"] and l.get("EnteredAt", 0) > 0]
                ahead = any(l.get("PacifiedAhead") for l in ls if l["Pacified"])
                cell = f"{share:.0%}" + (f" {mean(after):.0f}s" if after else "") + (" a" if ahead else "")
                per.append(cell.rjust(10))
            print(f"{d:>2} {fmt(tot, '.1f', 8)}              " + "  ".join(per))

    # ── Escort proximity and round stats per seat ──
    seat_rows = collections.defaultdict(lambda: collections.defaultdict(list))
    for d in sorted(by):
        for r in by[d]:
            near = collections.defaultdict(lambda: [0, 0, 0])
            for l in r.get("LegReports", []):
                for s in l.get("Seats", []):
                    k = (s.get("Seat", -1), s.get("Vessel", "?"))
                    near[k][0] += s.get("Near300", 0)
                    near[k][1] += s.get("Near900", 0)
                    near[k][2] += s.get("Samples", 0)
            stats = {(s.get("Seat", -1), s.get("Vessel", "?")): s for s in r.get("RunSeats", [])}
            for k in set(near) | set(stats):
                row = seat_rows[d][k]
                n300, n900, n = near.get(k, [0, 0, 0])
                if n:
                    row.append(("near300", n300 / n))
                    row.append(("near900", n900 / n))
                st = stats.get(k)
                if st:
                    for f in ("Kills", "KillsOnArk", "LifeformsKilled", "VolumeCreated", "HostileVolumeDestroyed"):
                        row.append((f, st.get(f, 0)))
    if seat_rows:
        print()
        print(f"{'d':>2} {'seat':>4} {'vessel':10} {'<300':>5} {'<900':>5} {'kills':>6} {'@ark':>5} {'lf':>5} "
              f"{'vol made':>9} {'hostile':>9}")
        for d in sorted(seat_rows):
            for (seat, vessel), row in sorted(seat_rows[d].items()):
                vals = collections.defaultdict(list)
                for f, v in row:
                    vals[f].append(v)
                print(f"{d:>2} {seat:>4} {vessel[:10]:10} {fmt(mean(vals['near300']), '.0%', 5)} "
                      f"{fmt(mean(vals['near900']), '.0%', 5)} {fmt(mean(vals['Kills']), '.1f', 6)} "
                      f"{fmt(mean(vals['KillsOnArk']), '.1f', 5)} {fmt(mean(vals['LifeformsKilled']), '.1f', 5)} "
                      f"{fmt(mean(vals['VolumeCreated']), '.0f', 9)} {fmt(mean(vals['HostileVolumeDestroyed']), '.0f', 9)}")

    # ── Overrides the runs applied ──
    applied = collections.Counter()
    refused = collections.Counter()
    for r in runs:
        for p in r.get("Params", []):
            (applied if p.get("Applied") else refused)[f"{p['Key']}={p['Value']:g}"] += 1
    if applied or refused:
        print()
        print("params applied: " + (", ".join(f"{k} (x{n})" for k, n in applied.items()) or "-"))
        if refused:
            print("params NOT applied: " + ", ".join(f"{k} (x{n})" for k, n in refused.items()))

    bad = [r for r in runs if r.get("Result", 0) not in (1, 2)]
    if bad:
        print(f"{len(bad)} runs did not finish: " + ", ".join(f"d{r['Difficulty']} {OUTCOME.get(r.get('Result', 0))}" for r in bad))


if __name__ == "__main__":
    sys.exit(main())
