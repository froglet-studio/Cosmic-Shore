#!/usr/bin/env python3
"""Summarise Brood Rush matches played in the REAL Unity game by the Brood audit harness
(Assets/_Scripts/Editor/Launch/BroodAudit/, CosmicShore.Editor.BroodAuditLauncher.Run).

    py -3 Tools/Build/brood_unity_audit/summarize.py <arm>=<results.csv> [...] [--out SUMMARY_TABLES.md]

A hull's BALANCE INDEX is the judge's (Tools/Compute/evaluators/brood_judge.py): the mean over its matches of its
VC + VD divided by the mean VC + VD of every seat in that match. 1.00 is the field; the judge's bar is 0.90-1.10.
Parked hosts are not seats (the harness leaves them out of results.csv). Standard library only.
"""
import argparse
import csv
import statistics as st
from collections import defaultdict


def load(path):
    rows = [r for r in csv.DictReader(open(path, encoding="utf-8"))]
    matches = defaultdict(list)
    for r in rows:
        if r["outcome"] in ("ended", "timeout") and r["vc_vd"]:
            matches[r["match"]].append(r)
    return matches


def f(x, d=0.0):
    try:
        return float(x)
    except (TypeError, ValueError):
        return d


def table(matches):
    per = defaultdict(lambda: defaultdict(list))
    race = []
    for seats in matches.values():
        mean = st.mean(f(s["vc_vd"]) for s in seats)
        race.append(f(seats[0]["race_s"]))
        for s in seats:
            h = per[s["hull"]]
            h["idx"].append(f(s["vc_vd"]) / mean if mean > 0 else 0.0)
            h["vc"].append(f(s["vc"]) / mean if mean > 0 else 0.0)
            h["vd"].append(f(s["vd"]) / mean if mean > 0 else 0.0)
            h["win"].append(1.0 if s["winner"] == s["domain"] else 0.0)
            h["nuc"].append(f(s["nucleus_s"]) / max(1.0, f(s["race_s"])))
            h["speed"].append(f(s["speed"]))
            h["trail"].append(f(s["trail_volume"]))
            h["abil"].append(f(s.get("ability_starts")))
            h["host"].append(1.0 if s["is_host"] == "True" else 0.0)
            h["pilots"].append(s["pilots"])
            h["levels"].append(s["start_levels"])
    return per, race


def render(name, matches):
    per, race = table(matches)
    out = [f"### {name}: {len(matches)} matches, mean match {st.mean(race) if race else 0:.0f} s", "",
           "| Hull | n | Index | sd | VC share | VD share | Win | In nucleus | Speed | Trail vol | Ability starts | Host seats |",
           "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for hull, h in sorted(per.items(), key=lambda kv: -st.mean(kv[1]["idx"])):
        n = len(h["idx"])
        sd = st.stdev(h["idx"]) if n > 1 else 0.0
        out.append(f"| {hull} | {n} | {st.mean(h['idx']):.2f} | {sd:.2f} | {st.mean(h['vc']):.2f} | {st.mean(h['vd']):.2f} | "
                   f"{st.mean(h['win']):.0%} | {st.mean(h['nuc']):.0%} | {st.mean(h['speed']):.0f} | {st.mean(h['trail']):.2f} | "
                   f"{st.mean(h['abil']):.0f} | {int(sum(h['host']))} |")
    inside = [k for k, h in per.items() if 0.9 <= st.mean(h["idx"]) <= 1.1]
    out += ["", f"Within 0.90-1.10 of the field: {len(inside)} of {len(per)} ({', '.join(sorted(inside)) or 'none'}).", ""]
    return "\n".join(out), per


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("arms", nargs="+", help="name=path/to/results.csv")
    ap.add_argument("--out")
    a = ap.parse_args()
    parts = []
    for arm in a.arms:
        name, path = arm.split("=", 1)
        text, _ = render(name, load(path))
        parts.append(text)
    doc = "\n".join(parts)
    print(doc)
    if a.out:
        open(a.out, "w", encoding="utf-8").write(doc + "\n")


if __name__ == "__main__":
    main()
