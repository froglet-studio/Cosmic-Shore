#!/usr/bin/env python3
"""The ROSTER: plan the new pilots that let every player own a unique crew, and deal crews out of the pool.

Garrett, 2026-11-18: "start making new images that support more combinations of the existing clades. I want to make
unique pilot sets and start issuing them to each of our players. So each player of our game has a unique crew of
pilots." A crew is one pilot for each of the 18 callsigns, and no pilot is in two crews.

    python3 Tools/Build/pilot_art/roster.py status                 # crews the pool makes, what each callsign lacks
    python3 Tools/Build/pilot_art/roster.py plan --crews 7         # roll the new pilots that reach 7 full crews
    python3 Tools/Build/pilot_art/roster.py plan --crews 7 --write # ... and record them in roster/plan.json
    python3 Tools/Build/pilot_art/roster.py deal players.txt out.json  # one crew per player id, in order
    python3 Tools/Build/pilot_art/roster.py --self-test

A new pilot is the person the game mints for a seed (creator.roll, bit for bit), kept only when its two animals are
a PAIR NO PILOT HAS YET (in either order), its name is new, and neither animal is already carried by more pilots than
an even spread allows. Seeds come from the callsign and a counter, so a plan is the same on every machine.

The pool is the painted pilots (scenes/people.json) plus the plan's pilots marked painted. PRIVACY: the repository
is public, so `deal` writes player ids only where it is told (a scratch or project folder), never into the repo.
"""
import argparse
import collections
import hashlib
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief    # noqa: E402
import creator  # noqa: E402

ROOT = brief.ROOT
PEOPLE = os.path.join(ROOT, "Docs/Pilots/art/scenes/people.json")
PLAN = os.path.join(ROOT, "Docs/Pilots/art/roster/plan.json")
CALLSIGNS = ["ENSIGN", "MANGER", "ANVIL", "VOLT", "ZODIAC", "SUNDIAL", "OPAL", "RAMPART", "CITADEL", "HORNET",
             "IVY", "TALON", "DOE", "WINGMAN", "BANDIT", "NEEDLE", "GALLEON", "FLUME"]


def painted():
    """The painted pilots: word -> {callsign, species, name}."""
    people = json.load(open(PEOPLE, encoding="utf-8"))["people"]
    return {w: dict(callsign=p["callsign"], species=list(p["species"]),
                    name=f'{p.get("given", "")} {p.get("family", "")}'.strip()) for w, p in people.items()}


def load_plan():
    if os.path.exists(PLAN):
        return json.load(open(PLAN, encoding="utf-8"))
    return {"about": ("New pilots planned by Tools/Build/pilot_art/roster.py so every player can own a unique crew "
                      "(Garrett, 2026-11-18). Each is the person creator.roll mints for its seed. state: planned, "
                      "designed (a scene record exists), painted (its scene is captured)."), "pilots": []}


def pool(plan=None, painted_only=True):
    """Every pilot that can go in a crew: word -> callsign."""
    out = {w: p["callsign"] for w, p in painted().items()}
    for p in (plan or load_plan())["pilots"]:
        if not painted_only or p.get("state") == "painted":
            out[p["word"]] = p["callsign"]
    return out


def crews_possible(by_callsign):
    return min(by_callsign.get(c, 0) for c in CALLSIGNS)


def seed_for(callsign, n):
    h = hashlib.sha256(f"cosmic-shore/roster/{callsign}/{n}".encode()).hexdigest()
    return int(h[:16], 16) or 1


def plan(crews, existing_plan=None):
    """Roll the new pilots that bring every callsign to `crews` pilots. Returns the new plan entries."""
    existing_plan = existing_plan or load_plan()
    known = painted()
    pairs = {frozenset(p["species"]) for p in known.values()}
    names = {p["name"].lower() for p in known.values()}
    words = set(known)
    use = collections.Counter()   # the planned pilots' animals: the even spread is over the new pilots
    counts = collections.Counter(p["callsign"] for p in known.values())
    for p in existing_plan["pilots"]:
        pairs.add(frozenset(p["heritage"]))
        names.add(p["person"].lower())
        words.add(p["word"])
        use.update(p["heritage"])
        counts[p["callsign"]] += 1
    need = [(c, crews - counts[c]) for c in CALLSIGNS if counts[c] < crews]
    total_new = sum(n for _, n in need)
    animals = brief.rollable_animals()
    # an even spread: no animal carried by more than its share of the planned pilots' animals, plus one
    cap = math.ceil(2 * (total_new + sum(1 for p in existing_plan["pilots"])) / len(animals)) + 1
    out = []
    counter = collections.Counter({p["callsign"]: p["n"] + 1 for p in existing_plan["pilots"]})
    # interleave callsigns so a short run still spreads the animals across them
    queue = []
    for i in range(max((n for _, n in need), default=0)):
        queue += [c for c, n in need if n > i]
    for c in queue:
        for _ in range(100000):
            n = counter[c]
            counter[c] += 1
            r = creator.roll(c, seed_for(c, n))
            pair = frozenset(r["heritage"])
            given = r["person"].split()[0]
            if (pair in pairs or r["person"].lower() in names or given in words
                    or any(use[a] >= cap for a in r["heritage"])):
                continue
            pairs.add(pair)
            names.add(r["person"].lower())
            words.add(given)
            use.update(r["heritage"])
            out.append(dict(word=given, callsign=c, n=n, seed=r["seed"], person=r["person"],
                            heritage=list(r["heritage"]), faction=r["faction"], faction_name=r["faction_name"],
                            state="planned"))
            break
        else:
            raise SystemExit(f"no new pair of animals left for {c}: widen the spread or the animals")
    return out


def status(text=True):
    pl = load_plan()
    by = collections.Counter(pool(pl).values())
    planned = collections.Counter(p["callsign"] for p in pl["pilots"])
    lines = [f"painted pool: {sum(by.values())} pilots, {crews_possible(by)} full crews; "
             f"planned: {len(pl['pilots'])} ({sum(1 for p in pl['pilots'] if p.get('state') == 'painted')} painted)"]
    for c in CALLSIGNS:
        lines.append(f"  {c:8} painted {by.get(c, 0):3}  planned {planned.get(c, 0):3}")
    if text:
        print("\n".join(lines))
    return by


def deal(player_ids, plan_=None, painted_only=True):
    """One crew per player, in the order given: for each callsign the next unused pilot. Players past the pool's
    crews get None. The order of the pool is fixed (painted first, then the plan's order), so dealing again to the
    same players in the same order gives the same crews; a new player only ever takes from what is left."""
    pl = plan_ or load_plan()
    by = collections.defaultdict(list)
    for w, c in pool(pl, painted_only).items():
        by[c].append(w)
    out = {}
    for i, pid in enumerate(player_ids):
        if all(len(by[c]) > i for c in CALLSIGNS):
            out[pid] = {c: by[c][i] for c in CALLSIGNS}
        else:
            out[pid] = None
    return out


def random_crew(player_id, plan_=None, painted_only=True):
    """A crew for a player outside the test pool (Garrett, 2026-11-18: "all other players can grab a random set from
    that pool"): one pilot per callsign, picked by a hash of the player's id, so it is the same every time for that
    player and may share pilots with anyone else's."""
    pl = plan_ or load_plan()
    by = collections.defaultdict(list)
    for w, c in pool(pl, painted_only).items():
        by[c].append(w)
    out = {}
    for c in CALLSIGNS:
        h = int(hashlib.sha256(f"cosmic-shore/crew/{player_id}/{c}".encode()).hexdigest()[:12], 16)
        out[c] = by[c][h % len(by[c])]
    return out


def self_test():
    pl = {"pilots": []}
    a = plan(4, pl)
    b = plan(4, pl)
    assert a == b, "a plan must be the same every time"
    known = painted()
    pairs = [frozenset(p["species"]) for p in known.values()] + [frozenset(p["heritage"]) for p in a]
    assert len(pairs) == len(set(pairs)), "every pilot has a pair of animals of their own"
    by = collections.Counter(p["callsign"] for p in known.values())
    by.update(p["callsign"] for p in a)
    assert crews_possible(by) >= 4, by
    for p in a:
        assert p["heritage"][0] != p["heritage"][1]
        assert creator.roll(p["callsign"], int(p["seed"], 16))["person"] == p["person"]
    pl2 = {"pilots": [dict(p, state="painted") for p in a]}
    crews = deal(["p1", "p2", "p3", "p4", "p5"], pl2)
    used = [w for c in crews.values() if c for w in c.values()]
    assert len(used) == len(set(used)), "no pilot is in two crews"
    assert all(crews[p] for p in ("p1", "p2", "p3", "p4"))
    r = random_crew("someone", pl2)
    assert r == random_crew("someone", pl2) and set(r) == set(CALLSIGNS)
    print(f"roster self-test ok: {len(a)} new pilots reach 4 crews; deal gives 4 unique crews")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", nargs="?", default="status", choices=["status", "plan", "deal"])
    ap.add_argument("args", nargs="*")
    ap.add_argument("--crews", type=int, default=7)
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--planned", action="store_true", help="deal: count planned pilots too, not only painted ones")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.cmd == "status":
        return status()
    if a.cmd == "plan":
        pl = load_plan()
        new = plan(a.crews, pl)
        for p in new:
            print(f'{p["word"]:12} {p["callsign"]:8} {p["person"]:26} {" + ".join(p["heritage"]):24} '
                  f'{p["faction"]} of {p["faction_name"]}')
        print(f"{len(new)} new pilots")
        if a.write and new:
            pl["pilots"] += new
            os.makedirs(os.path.dirname(PLAN), exist_ok=True)
            json.dump(pl, open(PLAN, "w", encoding="utf-8"), indent=1)
            print(f"written {os.path.relpath(PLAN, ROOT)}")
        return
    if a.cmd == "deal":
        src, out = a.args
        ids = [l.strip() for l in open(src, encoding="utf-8") if l.strip()]
        if os.path.abspath(out).startswith(os.path.abspath(ROOT)):
            sys.exit("deal writes player ids: give an output path outside the repository")
        json.dump(deal(ids, painted_only=not a.planned), open(out, "w", encoding="utf-8"), indent=1)
        print(f"{len(ids)} players dealt into {out}")


if __name__ == "__main__":
    main()
