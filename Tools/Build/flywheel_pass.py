#!/usr/bin/env python3
"""flywheel_pass.py - one pass of the Regatta flywheel as one command per box (Plan step: time-arena-course).

A pass re-races the best courses the course search has found with the FIELD AS SHIPPED on one bleeding-edge commit,
picks the best shuffled pool per start condition, and writes a report with the deltas from the previous pass. It
replaces the hand-run w3/rejudge/rejudge.py + report_all.py and the hand-off notes between them.

    # on every box (K of N), on the commit the pass is named for (resumable: re-run the same line after a stop)
    python3 -u Tools/Build/flywheel_pass.py run --name p3 --sha fbbd97003 --shard 0/3
    # anywhere, once the shards are in (or part way, for a look): folds every shard and writes the report
    python3 Tools/Build/flywheel_pass.py report --name p3            # --prev p2 (default: the latest other pass)
    python3 Tools/Build/flywheel_pass.py status --name p3
    python3 Tools/Build/flywheel_pass.py --self-test

1. COURSES. The first runner of a pass pins the course list in passes/<name>/courses.json, so every shard races the
   same courses even while the search keeps writing: per start (card, L5) the N best records by search loss, deduped,
   from every archive*/shard_<start>_*/records.jsonl that timed all eight archive hulls.
2. RACES. Each hull alone, shipped AIs, no overrides (Garrett 2026-11-16: judged with AND without the L5 start). A
   runner takes the courses whose rank % N == K and appends one line per (start, rank, hull, rep) to
   races.s<K>of<N>.jsonl, skipping lines already there. --repeats Dolphin=3 races a noisy hull on more seeds (median).
3. POOL. Per start, the 4-8 courses (--pool-min/--pool-max) whose per-hull geometric-mean time is the most even: the
   pool value is slowest / fastest of those means, and it passes at --cancel (1.10x, the plan's bar) when no hull is
   fastest on every course of it. Exhaustive when the combinations are few, a seeded swap search otherwise.
4. REPORT. passes/<name>/REPORT.md and summary.json: standings (time over the field's geometric mean, geometric mean
   over courses; 1.00 = mid-field), best and median course spread, the best pool vs 1.10x and who sets its ends, DNFs,
   and every number's change from the previous pass.

A DNF counts as --dnf seconds (400). The judge needs `apt-get install -y dotnet-sdk-10.0`; delete the judge binary
(REGATTA_JUDGE_BIN) after any C# change. At most 4 game processes per 16 GB box: the judge's default workers keep to it.
"""
import argparse
import glob
import itertools
import json
import math
import os
import random
import statistics
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
FLY = "/mnt/project-files/regatta-flywheel"
H8 = ["Dolphin", "Manta", "Rhino", "Serpent", "Sparrow", "Squirrel", "Stoat", "Urchin"]   # timed in every archive
HULLS = H8 + ["Scarab"]                                                                   # the field as shipped
STARTS = ("card", "L5")


# ---------------------------------------------------------------- paths and small helpers

def pass_dir(root, name):
    return os.path.join(root, "passes", name)


def read_jsonl(path):
    out = []
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                try:
                    out.append(json.loads(line))
                except ValueError:
                    pass                      # a half-written last line on the network mount
    except FileNotFoundError:
        pass
    return out


def write_json_once(path, obj):
    """Write path only if nobody has: the first runner pins it, the rest read what it pinned."""
    tmp = f"{path}.{os.getpid()}.tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(obj, fh, indent=1)
    try:
        os.link(tmp, path)                    # atomic: fails if the file exists
    except FileExistsError:
        pass
    except OSError:                           # a mount without hard links
        if not os.path.exists(path):
            os.replace(tmp, path)
    if os.path.exists(tmp):
        os.remove(tmp)
    return json.load(open(path, encoding="utf-8"))


def gm(xs):
    return math.exp(sum(math.log(x) for x in xs) / len(xs))


def head_sha():
    try:
        return subprocess.run(["git", "-C", REPO, "rev-parse", "HEAD"], capture_output=True, text=True,
                              check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return ""


# ---------------------------------------------------------------- 1. courses

def genome_key(g):
    return json.dumps(g, sort_keys=True)


def pick_courses(root, n, archives, rank_hulls=None):
    """{start: [{"rank", "loss", "game_genome", "archive_seconds", "source"}]}, the n best per start. rank_hulls: rank on
    the archive's slowest / fastest over those hulls alone (archives searched on different fields compare), else on the
    archive's own loss."""
    need = rank_hulls or H8
    out = {}
    for st in STARTS:
        files = sorted(f for a in archives for f in glob.glob(os.path.join(root, a, f"shard_{st}_*", "records.jsonl")))
        seen, recs = set(), []
        for f in files:
            for r in read_jsonl(f):
                secs = r.get("seconds") or {}
                if not (r.get("valid") and r.get("game_genome") and all(secs.get(h) for h in need)):
                    continue
                k = genome_key(r["game_genome"])
                if k in seen:
                    continue
                seen.add(k)
                key = (max(secs[h] for h in need) / min(secs[h] for h in need)) if rank_hulls else r["loss"]["loss"]
                recs.append((key, os.path.relpath(f, root), r))
        recs.sort(key=lambda t: t[0])
        out[st] = [{"rank": i, "loss": loss, "game_genome": r["game_genome"], "archive_seconds": r["seconds"],
                    "source": src} for i, (loss, src, r) in enumerate(recs[:n])]
    return out


# ---------------------------------------------------------------- 2. races

def run(a, judge=None):
    if judge is None:
        sys.path.insert(0, os.path.join(REPO, "Tools", "Compute", "evaluators"))
        import regatta_judge
        judge = regatta_judge.judge
    d = pass_dir(a.root, a.name)
    os.makedirs(d, exist_ok=True)
    have = head_sha()
    if a.sha and not have.startswith(a.sha) and not a.any_sha:
        raise SystemExit(f"this checkout is at {have[:10] or '?'}, the pass is for {a.sha}: check it out "
                         "(or pass --any-sha to race anyway)")
    meta = write_json_once(os.path.join(d, "pass.json"), {
        "name": a.name, "sha": a.sha or have, "n": a.n, "archives": a.archives, "repeats": a.repeats,
        "hulls": HULLS, "rank_hulls": a.rank_hulls or None, "dnf_s": a.dnf, "started": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())})
    if meta["sha"] and a.sha and not meta["sha"].startswith(a.sha) and not meta["sha"].startswith(have[:len(a.sha)]):
        raise SystemExit(f"pass {a.name} was pinned to {meta['sha']}, not {a.sha}: name a new pass")
    courses = write_json_once(os.path.join(d, "courses.json"), pick_courses(a.root, meta["n"], meta["archives"],
                                                                                  meta.get("rank_hulls")))
    K, N = (int(x) for x in a.shard.split("/"))
    out = os.path.join(d, f"races.s{K}of{N}.jsonl")
    done = {(r["start"], r["rank"], r["hull"], r["rep"]) for r in read_jsonl(out)}
    if os.path.exists(out) and os.path.getsize(out):
        with open(out, "rb") as fh:
            fh.seek(-1, 2)
            torn = fh.read(1) != b"\n"
        if torn:                              # a stop mid-line: end it, so the next line is not glued onto it
            with open(out, "a", encoding="utf-8") as fh:
                fh.write("\n")
    repeats = meta["repeats"] or {}
    raced = 0
    for st in STARTS:
        for c in courses.get(st, []):
            if c["rank"] % N != K:
                continue
            calls = []
            first = [h for h in HULLS if (st, c["rank"], h, 0) not in done]
            if first:
                calls.append((first, [1]))
            for h, reps in repeats.items():
                more = [s for s in range(2, int(reps) + 1) if (st, c["rank"], h, s - 1) not in done]
                if more:
                    calls.append(([h], more))
            for hs, seeds in calls:
                t0 = time.time()
                res = judge(c["game_genome"], hs, seeds, start=None if st == "card" else st)
                with open(out, "a", encoding="utf-8") as fh:
                    for race in res["races"]:
                        for s in race["seats"]:
                            rep = int(race.get("seed", 1)) - 1
                            fh.write(json.dumps({
                                "start": st, "rank": c["rank"], "hull": s["hull"], "rep": rep, "seed": rep + 1,
                                "finish_s": s.get("finish_s"), "dnf": s.get("dnf_reason"),
                                "prism_hits": s.get("prism_hits"), "notes": res.get("notes"),
                                "wall_s": round(time.time() - t0, 1)}) + "\n")
                raced += 1
                print(f"[{a.name} {K}/{N}] {st} rank {c['rank']} {','.join(hs)} seeds {seeds} "
                      f"{time.time() - t0:.0f} s", flush=True)
    print(f"[{a.name} {K}/{N}] done: {raced} judge calls this run, results in {out}")
    return raced


# ---------------------------------------------------------------- 3. pool

def pool_value(T, idx, hulls):
    m = {h: gm([T[i][h] for i in idx]) for h in hulls}
    return max(m.values()) / min(m.values()), m


def one_leader(T, idx, hulls):
    """The hull fastest on every course of the pool, or None."""
    leaders = {min(hulls, key=lambda h: T[i][h]) for i in idx}
    return leaders.pop() if len(leaders) == 1 else None


def best_pool(T, hulls, kmin=4, kmax=8, budget=200000, seed=1):
    """(value, means, indices) of the most even pool of kmin..kmax courses; a pool where one hull leads every course
    is taken only when no other exists."""
    n = len(T)
    if n == 0:
        return None
    rng = random.Random(seed)
    best = None

    def consider(idx):
        nonlocal best
        v, m = pool_value(T, idx, hulls)
        key = (one_leader(T, idx, hulls) is not None, v)
        if best is None or key < best[0]:
            best = (key, v, m, tuple(sorted(idx)))

    for k in range(min(kmin, n), min(kmax, n) + 1):
        if math.comb(n, k) <= budget:
            for idx in itertools.combinations(range(n), k):
                consider(idx)
            continue
        starts = []
        for i in range(n):                                    # greedy from every course, then seeded random starts
            cur = [i]
            while len(cur) < k:
                cur.append(min((j for j in range(n) if j not in cur), key=lambda j: pool_value(T, cur + [j], hulls)[0]))
            starts.append(cur)
        starts += [rng.sample(range(n), k) for _ in range(12)]
        for cur in starts:                                    # each refined by a swap search
            cv = pool_value(T, cur, hulls)[0]
            improved = True
            while improved:
                improved = False
                for pos in range(k):
                    for j in range(n):
                        if j in cur:
                            continue
                        cand = cur[:pos] + [j] + cur[pos + 1:]
                        v = pool_value(T, cand, hulls)[0]
                        if v < cv - 1e-12:
                            cur, cv, improved = cand, v, True
            consider(cur)
    _, v, m, idx = best
    return v, m, idx


# ---------------------------------------------------------------- 4. report

def fold(d, dnf_s):
    """{start: {"T": [{hull: s}], "ranks": [...], "dnf": {hull: n}, "archive": [{hull: s}]}} over complete courses."""
    rows = [r for f in sorted(glob.glob(os.path.join(d, "races.s*of*.jsonl"))) for r in read_jsonl(f)]
    courses = json.load(open(os.path.join(d, "courses.json"), encoding="utf-8"))
    seen, by = set(), {}
    for r in rows:
        k = (r["start"], r["rank"], r["hull"], r["rep"])
        if k in seen:
            continue
        seen.add(k)
        by.setdefault(r["start"], {}).setdefault(r["rank"], {}).setdefault(r["hull"], []).append(r)
    out = {}
    for st in STARTS:
        arch = {c["rank"]: c["archive_seconds"] for c in courses.get(st, [])}
        T, ranks, A, dnf = [], [], [], {h: 0 for h in HULLS}
        for rank, hs in sorted((by.get(st) or {}).items()):
            if not all(h in hs for h in HULLS):
                continue
            t = {}
            for h in HULLS:
                xs = [x["finish_s"] if x.get("finish_s") else dnf_s for x in hs[h]]
                dnf[h] += sum(1 for x in hs[h] if not x.get("finish_s"))
                t[h] = statistics.median(xs)
            T.append(t)
            ranks.append(rank)
            A.append(arch.get(rank))
        out[st] = {"T": T, "ranks": ranks, "dnf": dnf, "archive": A, "courses": len(courses.get(st, []))}
    return out


def summarise(F, kmin, kmax, cancel, hulls=None):
    """Over hulls (default the whole field): a sub-field's numbers come from the same races, its other hulls left out."""
    hulls = hulls or HULLS
    S = {}
    for st, f in F.items():
        T = [{h: t[h] for h in hulls} for t in f["T"]]
        if not T:
            S[st] = {"courses": 0, "of": f["courses"]}
            continue
        stand = {h: gm([t[h] / gm([t[x] for x in hulls]) for t in T]) for h in hulls}
        spreads = sorted(max(t.values()) / min(t.values()) for t in T)
        v, m, idx = best_pool(T, hulls, kmin, kmax)
        lead = one_leader(T, idx, hulls)
        S[st] = {"courses": len(T), "of": f["courses"], "standing": stand, "best_course": spreads[0],
                 "median_course": spreads[len(spreads) // 2], "pool": v, "pool_means": m,
                 "pool_ranks": [f["ranks"][i] for i in idx], "pool_passes": v <= cancel and lead is None,
                 "pool_one_leader": lead, "slowest": max(m, key=m.get), "fastest": min(m, key=m.get),
                 "dnf": {h: n for h, n in f["dnf"].items() if n and h in hulls}}
    return S


def previous(root, name):
    """The latest other pass with a summary, by its start time."""
    best = None
    for p in glob.glob(os.path.join(root, "passes", "*", "summary.json")):
        nm = os.path.basename(os.path.dirname(p))
        if nm == name:
            continue
        meta = os.path.join(os.path.dirname(p), "pass.json")
        t = json.load(open(meta)).get("started", "") if os.path.exists(meta) else ""
        if best is None or t > best[0]:
            best = (t, nm)
    return best[1] if best else None


def delta(new, old, fmt="{:+.2f}"):
    return "" if old is None else " (" + fmt.format(new - old) + ")"


def report(a):
    d = pass_dir(a.root, a.name)
    meta = json.load(open(os.path.join(d, "pass.json"), encoding="utf-8"))
    F = fold(d, meta.get("dnf_s", a.dnf))
    S = summarise(F, a.pool_min, a.pool_max, a.cancel)
    sub = meta.get("rank_hulls")
    if sub:                                   # the field the courses were ranked on, from the same races
        S["sub"] = {"hulls": sub, **summarise(F, a.pool_min, a.pool_max, a.cancel, sub)}
    prev = a.prev or previous(a.root, a.name)
    P = json.load(open(os.path.join(pass_dir(a.root, prev), "summary.json"))) if prev else {}
    rank_on = f"ranked on {', '.join(sub)}'s slowest / fastest" if sub else "ranked on each archive's loss"
    L = [f"# Regatta flywheel pass {a.name}", "",
         f"Bleeding-edge `{meta['sha'][:10]}`, the field as shipped ({', '.join(HULLS)}), each hull alone. The "
         f"{meta['n']} best search courses per start from {', '.join(meta['archives'])}, {rank_on}. A DNF counts as "
         f"{meta.get('dnf_s', a.dnf):.0f} s. Standing = time over the field's geometric mean on a course, geometric mean "
         f"over courses (1.00 = mid-field). Pool: {a.pool_min}-{a.pool_max} courses, value = slowest / fastest hull "
         f"mean, passing at {a.cancel:.2f}x with no hull fastest on every course.",
         "", f"Deltas in brackets are from pass {prev}." if prev else "No earlier pass to compare with.", ""]
    groups = [("the full field", HULLS, S, P)]
    if sub:
        psub = P.get("sub") if (P.get("sub") or {}).get("hulls") == sub else {}
        groups.insert(0, (", ".join(sub), sub, S["sub"], psub or {}))
    for label, hulls, G, PG in groups:
        for st in STARTS:
            s, p = G[st], (PG.get(st) or {})
            L.append(f"## {label[0].upper() + label[1:]}, start {st} ({s['courses']} of {s['of']} courses complete)")
            L.append("")
            if not s["courses"]:
                L += ["No complete course yet.", ""]
                continue
            ps = p.get("standing") or {}
            L.append("| Hull | Standing | Pool mean (s) |")
            L.append("|---|---|---|")
            for h in sorted(hulls, key=s["standing"].get):
                L.append(f"| {h} | {s['standing'][h]:.2f}{delta(s['standing'][h], ps.get(h))} | "
                         f"{s['pool_means'][h]:.0f}{delta(s['pool_means'][h], (p.get('pool_means') or {}).get(h), '{:+.0f}')} |")
            L.append("")
            verdict = "PASSES" if s["pool_passes"] else ("fails: " + (f"{s['pool_one_leader']} leads every course"
                                                                     if s["pool_one_leader"] else f"over {a.cancel:.2f}x"))
            L.append(f"- Best pool **{s['pool']:.3f}x**{delta(s['pool'], p.get('pool'), '{:+.3f}')} ({verdict}), courses "
                     f"{', '.join(map(str, s['pool_ranks']))}: slowest {s['slowest']}, fastest {s['fastest']}.")
            L.append(f"- Best single course {s['best_course']:.2f}x{delta(s['best_course'], p.get('best_course'))}, "
                     f"median {s['median_course']:.2f}x{delta(s['median_course'], p.get('median_course'))}.")
            L.append("- DNFs: " + (", ".join(f"{h} {n}" for h, n in s["dnf"].items()) or "none") + ".")
            L.append("")
    json.dump(S, open(os.path.join(d, "summary.json"), "w"), indent=1)
    open(os.path.join(d, "REPORT.md"), "w", encoding="utf-8").write("\n".join(L) + "\n")
    print("\n".join(L))
    return S


def status(a):
    d = pass_dir(a.root, a.name)
    courses = json.load(open(os.path.join(d, "courses.json"), encoding="utf-8"))
    rows = {(r["start"], r["rank"], r["hull"], r["rep"])
            for f in glob.glob(os.path.join(d, "races.s*of*.jsonl")) for r in read_jsonl(f)}
    for st in STARTS:
        n = len(courses.get(st, []))
        full = sum(1 for c in courses.get(st, []) if all((st, c["rank"], h, 0) in rows for h in HULLS))
        print(f"{a.name} {st}: {full}/{n} courses complete, {sum(1 for r in rows if r[0] == st)} races")


# ---------------------------------------------------------------- self-test

def self_test():
    import tempfile
    root = tempfile.mkdtemp(prefix="flywheel_pass_")
    rng = random.Random(7)
    for st in STARTS:                                     # a fake archive: 12 courses per start, 2 shards, one dupe
        for sh in (0, 1):
            p = os.path.join(root, "archiveX", f"shard_{st}_{sh}")
            os.makedirs(p)
            with open(os.path.join(p, "records.jsonl"), "w") as fh:
                for i in range(6):
                    g = {"seed": sh * 6 + i, "start": st}
                    fh.write(json.dumps({"valid": True, "game_genome": g, "genome": g, "loss": {"loss": rng.random()},
                                         "seconds": {h: 50 + rng.random() * 50 for h in H8}}) + "\n")
                fh.write(json.dumps({"valid": True, "game_genome": {"seed": 0, "start": st}, "genome": {},
                                     "loss": {"loss": -1}, "seconds": {h: 1 for h in H8}}) + "\n" if sh == 1 else "")
                fh.write(json.dumps({"valid": False, "game_genome": {"seed": 99}, "loss": {"loss": -5},
                                     "seconds": {}}) + "\n")
    calls = []

    def fake_judge(genome, hulls, seeds, start=None):
        calls.append((genome["seed"], tuple(hulls), tuple(seeds)))
        races = []
        for sd in seeds:
            seats = []
            for h in hulls:
                base = 60 + 7 * HULLS.index(h) + 9 * math.sin(genome["seed"] * (1 + HULLS.index(h)))
                dnf = h == "Stoat" and genome["seed"] == 3
                seats.append({"hull": h, "finish_s": None if dnf else round(base + sd, 3),
                              "dnf_reason": "stopped" if dnf else None})
            races.append({"seed": sd, "seats": seats})
        return {"races": races, "notes": ["fake"]}

    def args(**kw):
        ns = argparse.Namespace(root=root, name="t1", sha="", any_sha=True, n=8, archives=["archiveX"],
                                repeats={"Dolphin": 3}, dnf=400.0, shard="0/1", prev=None, pool_min=4, pool_max=6,
                                cancel=1.10, rank_hulls=None)
        ns.__dict__.update(kw)
        return ns

    courses = pick_courses(root, 8, ["archiveX"])
    # ranked on a sub-field: the archive's own loss is ignored, the sub-field's slowest / fastest decides
    six = ["Dolphin", "Sparrow", "Squirrel", "Stoat", "Urchin", "Manta"]
    sub = pick_courses(root, 8, ["archiveX"], six)["card"]
    sp = [max(c["archive_seconds"][h] for h in six) / min(c["archive_seconds"][h] for h in six) for c in sub]
    assert sp == sorted(sp), ("ranked on the six's spread", sp[:3])
    assert all(len(courses[st]) == 8 for st in STARTS), "n best per start"
    assert courses["card"][0]["loss"] < courses["card"][1]["loss"], "ranked by loss"
    assert len({genome_key(c["game_genome"]) for c in courses["L5"]}) == 8, "deduped"
    # sharded: two runners split the courses and together race every hull, Dolphin on 3 seeds
    run(args(shard="0/2"), fake_judge)
    n0 = len(calls)
    run(args(shard="1/2"), fake_judge)
    assert len(calls) == 2 * 8 * 2, f"two calls per course (field, then Dolphin's extra seeds), got {len(calls)}"
    assert n0 == len(calls) - n0, "shards split evenly"
    run(args(shard="0/2"), fake_judge)
    run(args(shard="1/2"), fake_judge)
    assert len(calls) == 32, "a re-run races nothing (resumable)"
    # a half-raced course resumes with only what it lacks
    d = pass_dir(root, "t1")
    f = os.path.join(d, "races.s0of2.jsonl")
    lines = open(f).read().splitlines()
    open(f, "w").write("\n".join(l for l in lines if '"hull": "Urchin"' not in l) + "\n{\"half")
    run(args(shard="0/2"), fake_judge)
    assert all(c[1] == ("Urchin",) for c in calls[32:]) and len(calls) == 32 + 8, "resume re-races the missing hull only"
    # the courses pinned by the first runner stay, even when the archive grows
    with open(os.path.join(root, "archiveX", "shard_card_0", "records.jsonl"), "a") as fh:
        fh.write(json.dumps({"valid": True, "game_genome": {"seed": 77}, "genome": {"seed": 77}, "loss": {"loss": -9},
                             "seconds": {h: 1 for h in H8}}) + "\n")
    run(args(shard="0/2"), fake_judge)
    assert len(calls) == 40, "pinned course list"
    S = report(args())
    assert S["card"]["courses"] == 8 and S["card"]["dnf"].get("Stoat", 0) >= 0
    assert 4 <= len(S["card"]["pool_ranks"]) <= 6
    T = fold(d, 400)["card"]["T"]
    v_ex = min(pool_value(T, idx, HULLS)[0] for k in range(4, 7) for idx in itertools.combinations(range(len(T)), k)
               if one_leader(T, idx, HULLS) is None)
    assert abs(S["card"]["pool"] - v_ex) < 1e-9, "the pool is the most even one"
    # the swap search finds a planted perfect pool among many courses
    rng2 = random.Random(3)
    T2 = [{h: 50 + rng2.random() * 60 for h in HULLS} for _ in range(40)]
    for i, j in ((5, 0), (17, 1), (23, 2), (31, 3)):
        T2[i] = {h: 80 * (1.02 if HULLS.index(h) % 4 == j else 1.0) for h in HULLS}
    v2, _, idx2 = best_pool(T2, HULLS, 4, 4, budget=10)
    assert v2 <= 1.0201 and set(idx2) == {5, 17, 23, 31}, (v2, idx2)
    # a second pass reports deltas from the first
    run(args(name="t2", shard="0/1"), lambda g, h, s, start=None: fake_judge(dict(g, seed=g["seed"] + 1), h, s))
    S2 = report(args(name="t2"))
    rep = open(os.path.join(pass_dir(root, "t2"), "REPORT.md")).read()
    assert "Deltas in brackets are from pass t1" in rep and "(+" in rep or "(-" in rep, "deltas shown"
    assert previous(root, "t2") == "t1"
    run(args(name="t4", shard="0/1", rank_hulls=six), fake_judge)
    S3 = report(args(name="t4"))
    assert S3["sub"]["hulls"] == six and set(S3["sub"]["card"]["standing"]) == set(six), "the sub-field is reported"
    assert set(S3["card"]["standing"]) == set(HULLS), "and the full field beside it"
    # a checkout on the wrong commit refuses
    try:
        run(args(name="t3", sha="0000000", any_sha=False), fake_judge)
        raise AssertionError("wrong sha accepted")
    except SystemExit as e:
        assert "check it out" in str(e)
    print(f"flywheel_pass --self-test: OK ({len(calls)} fake judge calls, pools {S['card']['pool']:.3f}x / "
          f"{S2['card']['pool']:.3f}x)")
    return 0


# ---------------------------------------------------------------- main

def main():
    if "--self-test" in sys.argv:
        return self_test()
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--root", default=FLY, help="the flywheel folder (archives in, passes/ out)")
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run", help="race this box's share of a pass (resumable)")
    r.add_argument("--name", required=True)
    r.add_argument("--sha", required=True, help="the bleeding-edge commit the pass is for (this checkout must be on it)")
    r.add_argument("--any-sha", action="store_true", help="race even when the checkout is on another commit")
    r.add_argument("--shard", default="0/1", help="K/N: this runner takes the courses whose rank %% N == K")
    r.add_argument("--n", type=int, default=30, help="courses per start (pinned by the pass's first runner)")
    r.add_argument("--archives", nargs="*", default=["archive2", "archive3"])
    r.add_argument("--repeats", default="Dolphin=3", help="HULL=SEEDS,... a noisy hull raced on more seeds (median)")
    r.add_argument("--dnf", type=float, default=400.0)
    r.add_argument("--rank-hulls", type=lambda x: [h for h in x.split(",") if h], default=None,
                   help="rank the courses on these hulls' archive spread (and report them as a sub-field too)")
    for name in ("report", "status"):
        p = sub.add_parser(name)
        p.add_argument("--name", required=True)
        if name == "report":
            p.add_argument("--prev", help="the pass to compare with (default: the latest other pass)")
            p.add_argument("--pool-min", type=int, default=4)
            p.add_argument("--pool-max", type=int, default=8)
            p.add_argument("--cancel", type=float, default=1.10)
            p.add_argument("--dnf", type=float, default=400.0)
    a = ap.parse_args()
    if a.cmd == "run":
        a.repeats = {k: int(v) for k, v in (x.split("=") for x in a.repeats.split(",") if x)} if a.repeats else {}
        run(a)
    elif a.cmd == "report":
        report(a)
    else:
        status(a)
    return 0


if __name__ == "__main__":
    sys.exit(main())
