"""Update AI: a vessel's trained genomes from Froglet Compute into the game's archive, on the ai branch.

    python3 Tools/Compute/archive_update.py status [--vessel Squirrel]
    python3 Tools/Compute/archive_update.py update --vessel Squirrel --by Shombith [--push]
    python3 Tools/Compute/archive_update.py --self-test

The game already deploys trained AIs: `TrainingDeploymentService` installs the genome `Assets/_SO_Assets/AI
Training/Archive.asset` holds for a (vessel, mode, intensity) on every AI seat of a normal match (intensity 4 is the
trained vector; 1-3 dither it, or fly their own entry when `UseStoredGenomeForLowerIntensity`). A Unity trainer run
on a team PC (README section 16) writes its best genome into the COPY's archive, which nobody ships. This is the
button that ships it (Shombith, 2026-11-01: "each vessel has an update AI button which can push its AI according to
difficulty levels it trained on"):

- `status`: for every vessel, every (mode, intensity) a compute run trained, the best genome across every machine and
  epoch (`jobs/unity-train/<epoch>/files/<unit>/genome.json`, its `status.json` for the fitness), against what the
  archive on `origin/ai` holds for that key.
- `update --vessel X --by NAME --push`: one commit on `origin/ai` (plumbing: a blob, a tree, `commit-tree`, a
  force-with-lease push; no checkout touched, so it runs on the VM's bare clone) that upserts the vessel's entries,
  one per (mode, intensity), with `Approved-by: NAME` and an `Affects:` line. NAME must be one of the core four
  (`Docs/TEAM.json` `core`, README section 8): a promotion is a person's yes.

The YAML is edited as text, entry by entry, in the exact shape Unity writes (so Unity reads it and rewrites nothing
but what changed); `--self-test` round-trips entries and refuses a vessel with nothing trained.
"""
import argparse
import datetime
import json
import os
import re
import sys

import compute_lib as L

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ARCHIVE = "Assets/_SO_Assets/AI Training/Archive.asset"
JOB = "unity-train"
GENOME_KEYS = ("geneNames", "geneValues", "enabledModules")
ENUMS = {"GameModes": "Assets/_Scripts/Data/Enums/GameModes.cs", "VesselClassType": "Assets/_Scripts/Data/Enums/VesselClassType.cs"}


# ── names and numbers ──
def enum_values(name, ref=None, cwd=None):
    """{member: value} of a game enum, from the ai branch (the game the archive belongs to) or the checkout."""
    text = L.git_ok("show", f"{ref or L.AI_REF}:{ENUMS[name]}", cwd=cwd) if ref != "" else None
    if not text:
        try:
            with open(os.path.join(L.REPO, ENUMS[name]), encoding="utf-8") as fh:
                text = fh.read()
        except OSError:
            text = ""
    m = re.search(rf"enum {name}\s*\{{(.*?)\}}", text, re.S)
    body = m.group(1) if m else ""
    return {k: int(v) for k, v in re.findall(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,?", body, re.M)}


def approvers():
    try:
        with open(os.path.join(L.REPO, "Docs", "TEAM.json"), encoding="utf-8") as fh:
            return list(json.load(fh).get("core") or [])
    except Exception:
        return ["Garrett", "Caleb", "Shombith", "Yash"]


def split_unit(unit_id):
    """("skimrace", "squirrel", 4) from "skimrace-squirrel-i4"; None when it is not a trainer unit."""
    m = re.fullmatch(r"([a-z0-9]+)-([a-z0-9]+)-i(\d)", str(unit_id))
    return (m.group(1), m.group(2), int(m.group(3))) if m else None


# ── what the runs trained ──
def trained(machines=None, cwd=None):
    """{(mode_lower, vessel_lower, intensity): {fitness, episodes, machine, epoch, unit, updated, genome, seed}}: the
    best genome a compute run published for each key, across every machine and epoch."""
    machines = L.gather(cwd=cwd) if machines is None else machines
    best = {}
    for m in machines:
        ref = m.get("ref")
        if not ref:
            continue
        listing = L.git_ok("ls-tree", "-r", "--name-only", ref, "jobs/", cwd=cwd) or ""
        for path in listing.splitlines():
            if not path.endswith("/genome.json"):
                continue
            parts = path.split("/")                      # jobs, unity-train or unity-train~<request>, <epoch>, files, <unit>, genome.json
            if len(parts) != 6 or parts[3] != "files" or parts[1].split("~")[0] != JOB:
                continue
            job_dir, epoch, unit = parts[1], parts[2], parts[4]
            key = split_unit(unit)
            if not key:
                continue
            try:
                st = json.loads(L.git_ok("show", f"{ref}:jobs/{job_dir}/{epoch}/files/{unit}/status.json", cwd=cwd) or "{}")
            except Exception:
                st = {}
            fit = st.get("best_fitness")
            if not L.finite(fit):
                continue
            genome = L.git_ok("show", f"{ref}:{path}", cwd=cwd)
            if not genome:
                continue
            row = {"fitness": float(fit), "episodes": int(st.get("episodes_total") or 0), "machine": f"{m.get('user')}/{m.get('host')}",
                   "epoch": epoch, "job": job_dir, "unit": unit, "updated": st.get("updated"), "genome": genome, "seed": st.get("seed") or ""}
            if key not in best or row["fitness"] > best[key]["fitness"]:
                best[key] = row
    return best


# ── the archive's text ──
ENTRY_RE = re.compile(r"^  - Vessel: (\d+)\n    GameMode: (\d+)\n    Intensity: (\d+)\n", re.M)


def entries(text):
    """[{vessel, mode, intensity, fitness, utc, start, end}] for every entry block in the asset's text."""
    out = []
    heads = list(ENTRY_RE.finditer(text))
    for i, h in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
        block = text[h.start():end]
        f = re.search(r"^    Fitness: (\S+)", block, re.M)
        u = re.search(r"^    TrainedUtc: (\S*)", block, re.M)
        out.append({"vessel": int(h.group(1)), "mode": int(h.group(2)), "intensity": int(h.group(3)),
                    "fitness": float(f.group(1)) if f else None, "utc": u.group(1) if u else "", "start": h.start(), "end": end})
    return out


def _f(v):
    s = f"{float(v):.8g}"
    return s if s not in ("inf", "-inf", "nan") else "0"


def render_entry(vessel_id, mode_id, intensity, genome, fitness, generation, utc, notes):
    """One entry in the shape Unity writes (2026-09-29's Archive.asset is the reference)."""
    g = json.loads(genome) if isinstance(genome, str) else genome
    def lst(key, items, fmt):
        return f"      {key}:" + ("".join(f"\n      - {fmt(x)}" for x in items) if items else " []") + "\n"
    s = (f"  - Vessel: {int(vessel_id)}\n    GameMode: {int(mode_id)}\n    Intensity: {int(intensity)}\n    Genome:\n"
         + lst("geneNames", g.get("geneNames") or [], str) + lst("geneValues", g.get("geneValues") or [], _f)
         + lst("enabledModules", g.get("enabledModules") or [], str)
         + f"      GenerationBorn: {int(g.get('GenerationBorn') or 0)}\n      EvaluationCount: {int(g.get('EvaluationCount') or 0)}\n"
         + f"      Fitness: {_f(g.get('Fitness') or 0)}\n      NoveltyScore: {_f(g.get('NoveltyScore') or 0)}\n      Lineage: {g.get('Lineage') or ''}\n"
         + f"    Fitness: {_f(fitness)}\n    Generation: {int(generation)}\n    TrainedUtc: {utc}\n    Notes: {notes}\n")
    return s


def upsert(text, new_entries):
    """`text` with each rendered entry replacing the one of its key, or appended to Entries."""
    for ne in new_entries:
        head = ENTRY_RE.match(ne)
        key = tuple(int(x) for x in head.groups())
        cur = entries(text)
        hit = next((e for e in cur if (e["vessel"], e["mode"], e["intensity"]) == key), None)
        if hit:
            text = text[:hit["start"]] + ne + text[hit["end"]:]
        elif cur:
            text = text[:cur[-1]["end"]].rstrip("\n") + "\n" + ne
        elif "  Entries: []" in text:
            text = text.replace("  Entries: []", "  Entries:\n" + ne.rstrip("\n"), 1) + ("" if text.endswith("\n") else "\n")
        else:
            text = text.rstrip("\n") + "\n" + ne
    if not text.endswith("\n"):
        text += "\n"
    return text


# ── status and update ──
def status(machines=None, cwd=None, ref=None):
    """Per vessel: the trained keys against the archive on the ai branch. [{vessel, rows: [...]}, ...]"""
    ref = ref or L.AI_REF
    modes, vessels = enum_values("GameModes", ref, cwd), enum_values("VesselClassType", ref, cwd)
    lmode = {k.lower(): k for k in modes}
    lvessel = {k.lower(): k for k in vessels}
    text = L.git_ok("show", f"{ref}:{ARCHIVE}", cwd=cwd) or ""
    arch = {(e["vessel"], e["mode"], e["intensity"]): e for e in entries(text)}
    by_vessel = {}
    for (ml, vl, inten), row in trained(machines, cwd).items():
        mode, vessel = lmode.get(ml), lvessel.get(vl)
        if not (mode and vessel):
            continue
        a = arch.get((vessels[vessel], modes[mode], inten))
        by_vessel.setdefault(vessel, []).append({
            "mode": mode, "intensity": inten, "fitness": row["fitness"], "episodes": row["episodes"], "machine": row["machine"],
            "epoch": row["epoch"], "unit": row["unit"], "updated": row["updated"], "seed": row["seed"],
            "archive_fitness": a["fitness"] if a else None, "archive_utc": a["utc"] if a else None,
            "newer": a is None or (a["fitness"] is None) or row["fitness"] > a["fitness"]})
    out = []
    for vessel in sorted(by_vessel):
        rows = sorted(by_vessel[vessel], key=lambda r: (r["mode"], r["intensity"]))
        out.append({"vessel": vessel, "rows": rows, "newer": sum(1 for r in rows if r["newer"])})
    return out


def update(vessel, by, machines=None, cwd=None, push=True, remote="origin", only_newer=True):
    """The vessel's trained genomes into the archive on the ai branch: {ok, sha, entries, note}."""
    if by not in approvers():
        return {"ok": False, "note": f"{by!r} is not one of the core four ({', '.join(approvers())}): a promotion is their yes"}
    if push:
        L.fetch(["ai"], results=False, cwd=cwd)
    ref = f"{remote}/ai"
    modes, vessels = enum_values("GameModes", ref, cwd), enum_values("VesselClassType", ref, cwd)
    if vessel not in vessels:
        return {"ok": False, "note": f"{vessel} is not a VesselClassType"}
    rows = next((v["rows"] for v in status(machines, cwd, ref) if v["vessel"] == vessel), [])
    rows = [r for r in rows if r["newer"]] if only_newer else rows
    if not rows:
        return {"ok": False, "note": f"nothing trained for {vessel} that the archive does not already hold"}
    best = trained(machines, cwd)
    text = L.git_ok("show", f"{ref}:{ARCHIVE}", cwd=cwd)
    if not text:
        return {"ok": False, "note": f"{ARCHIVE} is not on {ref}"}
    utc = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.0000000Z")
    rendered = []
    for r in rows:
        key = (r["mode"].lower(), vessel.lower(), r["intensity"])
        row = best[key]
        rendered.append(render_entry(vessels[vessel], modes[r["mode"]], r["intensity"], row["genome"], r["fitness"],
                                     json.loads(row["genome"]).get("GenerationBorn") or 0, utc,
                                     f"Froglet Compute: {r['unit']} on {r['machine']} ({r['episodes']} episodes), approved by {by}"))
    new_text = upsert(text, rendered)
    import ai_branch as B
    ai_sha = L.rev(ref, cwd=cwd)
    tree = L.git("rev-parse", f"{ai_sha}^{{tree}}", cwd=cwd).strip()
    blob = B._git("hash-object", "-w", "--stdin", cwd=cwd, input=new_text).strip()
    new_tree = B._with_entries(tree, {ARCHIVE: ("100644", blob)}, cwd=cwd)
    what = ", ".join(f"{r['mode']} I{r['intensity']} ({r['fitness']:.3g})" for r in rows)
    msg = (f"chore(ai): {vessel}'s AI from Froglet Compute: {what}\n\n"
           f"The trainer's best genome per (mode, intensity) written into {ARCHIVE} (the archive normal play deploys), "
           f"from the runs published on the compute branches. Tool: Tools/Compute/archive_update.py.\n\n"
           f"Approved-by: {by}\nAffects: the AI trainer's owners (studyholic, Garrett, Shombith) and Yash's AI system: the shipped "
           f"{vessel} AI changes for these pairs.\n")
    sha = B._commit(new_tree, [ai_sha], msg, cwd=cwd)
    if push:
        B._git("push", "--quiet", f"--force-with-lease=refs/heads/ai:{ai_sha}", remote, f"{sha}:refs/heads/ai", cwd=cwd)
        B._git("update-ref", f"refs/remotes/{remote}/ai", sha, cwd=cwd)
    return {"ok": True, "sha": sha, "entries": [{"mode": r["mode"], "intensity": r["intensity"], "fitness": r["fitness"]} for r in rows],
            "note": f"{vessel}: {len(rows)} entr{'y' if len(rows) == 1 else 'ies'} on ai ({sha[:9]}{'' if push else ', not pushed'})"}


# ── self-test ──
HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_Script: {fileID: 11500000, guid: 0bbe8ceb59a24c89b539dfba256eaafb, type: 3}
  m_Name: Archive
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Utility.AITraining.TrainingArchiveSO
  Entries:
"""


def self_test():
    bad = []
    g = json.dumps({"geneNames": ["throttle.base", "boost.min_charge"], "geneValues": [0.8, 0.6], "enabledModules": ["Throttle"],
                    "GenerationBorn": 3, "EvaluationCount": 12, "Fitness": 0, "NoveltyScore": 0, "Lineage": "x"})
    e1 = render_entry(6, 33, 4, g, 277.09552, 0, "2026-09-29T15:08:33.0020190Z", "first")
    text = HEADER + e1
    got = entries(text)
    if len(got) != 1 or (got[0]["vessel"], got[0]["mode"], got[0]["intensity"]) != (6, 33, 4) or abs(got[0]["fitness"] - 277.09552) > 1e-4:
        bad.append(f"one rendered entry did not parse back: {got}")
    e2 = render_entry(6, 33, 4, g, 300.5, 2, "2026-11-01T00:00:00.0000000Z", "second")
    e3 = render_entry(7, 73, 1, g, 12.25, 1, "2026-11-01T00:00:00.0000000Z", "third")
    text2 = upsert(text, [e2, e3])
    got2 = entries(text2)
    if len(got2) != 2:
        bad.append(f"upsert did not replace one and append one: {len(got2)} entries")
    else:
        if abs(got2[0]["fitness"] - 300.5) > 1e-6 or "first" in text2:
            bad.append("the same key was not replaced")
        if (got2[1]["vessel"], got2[1]["mode"], got2[1]["intensity"]) != (7, 73, 1):
            bad.append("the new key was not appended")
    if upsert(HEADER.replace("  Entries:\n", "  Entries: []\n"), [e3]).count("  - Vessel: 7") != 1:
        bad.append("an empty archive did not take its first entry")
    if "      enabledModules:\n      - Throttle\n" not in text2 or "      - 0.8\n" not in text2:
        bad.append("the entry's lists are not in Unity's shape")
    if upsert(text2, []) != text2:
        bad.append("an upsert of nothing changed the text")
    # the unit name and the enums
    if split_unit("skimrace-squirrel-i4") != ("skimrace", "squirrel", 4) or split_unit("toy~x") is not None:
        bad.append("unit names are not parsed")
    ev = enum_values("VesselClassType", "")
    if ev.get("Squirrel") != 6 or ev.get("Serpent") != 7:
        bad.append(f"the vessel enum did not parse: {dict(list(ev.items())[:4])}")
    # a fake machine's results: the best of two epochs wins, a run without a fitness is skipped
    fake = {}
    def show(*args, cwd=None):
        a = " ".join(args)
        if a.startswith("ls-tree"):
            return "\n".join(fake)
        for p, v in fake.items():
            if a.endswith(":" + p):
                return v
        return None
    fake.update({"jobs/unity-train/e1/files/skimrace-squirrel-i4/genome.json": g,
                 "jobs/unity-train/e1/files/skimrace-squirrel-i4/status.json": json.dumps({"best_fitness": 10, "episodes_total": 5}),
                 "jobs/unity-train/e2/files/skimrace-squirrel-i4/genome.json": g,
                 "jobs/unity-train/e2/files/skimrace-squirrel-i4/status.json": json.dumps({"best_fitness": 40, "episodes_total": 9, "seed": "3 genes"}),
                 "jobs/unity-train~r20261028-205229-b55f/e3/files/skimrace-squirrel-i3/genome.json": g,
                 "jobs/unity-train~r20261028-205229-b55f/e3/files/skimrace-squirrel-i3/status.json": json.dumps({"best_fitness": 7, "episodes_total": 2}),
                 "jobs/drive-x/e9/files/skimrace-squirrel-i1/genome.json": g,
                 "jobs/drive-x/e9/files/skimrace-squirrel-i1/status.json": json.dumps({"best_fitness": 99}),
                 "jobs/unity-train/e2/files/skimrace-squirrel-i2/genome.json": g,
                 "jobs/unity-train/e2/files/skimrace-squirrel-i2/status.json": json.dumps({"best_fitness": "nan"})})
    real = L.git_ok
    L.git_ok = show
    try:
        t = trained([{"ref": "origin/compute/a/b", "user": "a", "host": "b"}])
    finally:
        L.git_ok = real
    if sorted(t) != [("skimrace", "squirrel", 3), ("skimrace", "squirrel", 4)] or t[("skimrace", "squirrel", 4)]["fitness"] != 40 or t[("skimrace", "squirrel", 4)]["seed"] != "3 genes":
        bad.append(f"the best genome across epochs was not picked (a manual run's unit counted, another job's and the nan skipped): {sorted(t)}")
    if update("Squirrel", "Nobody", machines=[], push=False)["ok"]:
        bad.append("a name outside the core four was allowed to promote")
    return bad


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    s = sub.add_parser("status"); s.add_argument("--vessel"); s.add_argument("--json", action="store_true")
    u = sub.add_parser("update"); u.add_argument("--vessel", required=True); u.add_argument("--by", required=True)
    u.add_argument("--push", action="store_true"); u.add_argument("--all", action="store_true", help="every trained key, not only the ones newer than the archive")
    a = ap.parse_args(argv)
    if a.self_test:
        bad = self_test()
        print("archive_update self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
        return 1 if bad else 0
    if a.cmd == "status":
        rows = status()
        if a.vessel:
            rows = [v for v in rows if v["vessel"] == a.vessel]
        if a.json:
            print(json.dumps(rows, indent=1))
        elif not rows:
            print("no vessel has a trained genome on the compute branches yet")
        for v in rows if not a.json else []:
            print(f"{v['vessel']}: {len(v['rows'])} trained pair(s), {v['newer']} newer than the archive")
            for r in v["rows"]:
                arch = f"archive {r['archive_fitness']:.4g} ({r['archive_utc'][:10]})" if r["archive_fitness"] is not None else "not in the archive"
                print(f"  {r['mode']:<22} I{r['intensity']}  best {r['fitness']:.4g} after {r['episodes']} episodes on {r['machine']}  |  {arch}{'  <- newer' if r['newer'] else ''}")
        return 0
    if a.cmd == "update":
        res = update(a.vessel, a.by, push=a.push, only_newer=not a.all)
        print(res["note"])
        return 0 if res["ok"] else 1
    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
