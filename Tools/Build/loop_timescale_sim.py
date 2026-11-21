#!/usr/bin/env python3
"""Loop timescale simulation: how differently built players spend their time and crystals in the loop model.

The loop model (Docs/LoopAtlas/model.json) says how long every loop takes. Segments
(Docs/LoopAtlas/sim/segments.yaml) say how long different cultures and demographics like a sitting, a finishable
unit and a reset rhythm to be. Personas (sim/personas.yaml) are particular people drawn from those segments. This
tool puts the two side by side three ways:

  fit       the timescale fit: for every segment and place, how often one finishable unit fits a sitting, and
            whether the game has a reset rhythm the segment answers to.
  simulate  a seeded day-by-day simulation of every persona for one Tide (42 days) in three scenarios that follow
            the atlas layers: today (Now: the master unlock is on), planned (Planned: the gate flipped, the quest
            chain and the planned voyage), proposed (Planned plus the Proposed-layer fixes this file names). Each
            sitting is filled with real loops from the model; each round pays from the economy table.
  report    writes sim/out/journeys.json (one representative run per persona and scenario, as steps the journey
            animation can play), sim/out/summary.json (a Monte Carlo over many seeds) and sim/REPORT.md.

  --check       rebuild everything in memory and fail if the committed outputs differ (the drift gate)
  self-test     negative controls: the fit and the simulation must refuse what they are meant to refuse

The numbers are priors, not measurements (segments.yaml says how sure each one is). The point is the comparison
between scenarios and between personas, and the holes it finds, which go to the atlas's Proposed layer.

Usage: python3 Tools/Build/loop_timescale_sim.py [report|--check|self-test|fit] [--runs N]
"""
from __future__ import annotations

import argparse
import copy
import json
import math
import random
import statistics
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
ATLAS = ROOT / "Docs" / "LoopAtlas"
SIM = ATLAS / "sim"
OUT = SIM / "out"
PLACES = ["toybox", "arcade", "arena", "voyages"]
SCENARIOS = ["today", "planned", "proposed"]
LAYER = {"today": "now", "planned": "plan", "proposed": "prop"}
DAYS = 42
RUNS = 200
SEED = 20261121

# The Proposed-layer fixes the `proposed` scenario switches on, each with the proposal or hole it comes from.
FIXES = {
    "tide": "P6 The Tide: a daily job, a weekly card, a six-week season (era.tide, build queued 2026-11-20)",
    "one_currency": "P10 One currency: a clean voyage pays about three arcade wins in crystals",
    "sinks": "P7 More to spend crystals on: Ark refits, pilots, colour sets",
    "chapters": "P13 Voyage chapters: dock at any location reached and resume next sitting",
    "puzzle_door": "P14 A puzzle-first door: the Ratel's board is offered on Home and in the first-login guide",
    "crew_seat": "P15 Every crew vessel seats an arena card and a voyage squad, so two people share one seat",
    "quest_rung": "arc.unlock hole: a quest clears at I2, matching ECONOMY_TABLES' ~3 games to the Hangar",
    "make_pays": "P16 Making pays: a finished painting pays a little and can be shared, so the Toybox feeds the long game",
    "breath": "P5 The breath: after each result one offered next step, so a sitting crosses places",
}


# ----------------------------------------------------------------------------------------------------------- load

def load(atlas: Path = ATLAS):
    model = json.loads((atlas / "model.json").read_text())
    sim = atlas / "sim"
    eco = yaml.safe_load((sim / "economy.yaml").read_text())
    segs = {s["id"]: s for s in yaml.safe_load((sim / "segments.yaml").read_text())["segments"]}
    personas = yaml.safe_load((sim / "personas.yaml").read_text())["personas"]
    return model, eco, segs, personas


def load_expectations(atlas: Path = ATLAS):
    path = atlas / "sim" / "expectations.yaml"
    return yaml.safe_load(path.read_text())["expectations"] if path.exists() else []


def metric_of(s, key):
    v = s
    for part in key.split("."):
        if not isinstance(v, dict) or part not in v:
            raise SystemExit(f"loop_timescale_sim: expectation metric '{key}' names nothing in a summary")
        v = v[part]
    return v


def judge(expectations, summary):
    """Every expectation against every persona it covers, per scenario: met, missed, or n/a (no value)."""
    rows = []
    for e in expectations:
        who = list(summary["personas"]) if e["who"] == "all" else e["who"]
        for pid in who:
            if pid not in summary["personas"]:
                raise SystemExit(f"loop_timescale_sim: expectation {e['id']} names unknown persona {pid}")
            res = {}
            for sc in SCENARIOS:
                v = metric_of(summary["personas"][pid]["scenarios"][sc], e["metric"])
                if v is None:
                    res[sc] = "n/a"
                else:
                    ok = v >= e["value"] if e["op"] == ">=" else v <= e["value"]
                    res[sc] = "met" if ok else "missed"
            rows.append({"id": e["id"], "persona": pid, **res})
    return rows


def nodes_by_id(model):
    return {n["id"]: n for n in model["nodes"]}


def children(model, pid):
    return [n for n in model["nodes"] if n.get("parent") == pid]


def validate(model, eco, segs, personas):
    """Fail loudly on a persona or segment the simulation cannot read."""
    errs = []
    ids = nodes_by_id(model)
    for p in PLACES:
        if p not in ids:
            errs.append(f"model has no place '{p}'")
    need = ["sittings", "minutes", "hours", "unit", "reset", "patience", "habit", "social", "wants", "sure"]
    drivers = {d["id"] for d in model["drivers"]}
    for s in segs.values():
        for k in need:
            if k not in s:
                errs.append(f"segment {s['id']}: missing {k}")
        for k in s.get("wants", {}):
            if k not in drivers:
                errs.append(f"segment {s['id']}: wants unknown driver '{k}'")
        if len(s.get("sittings", [])) != 4 or len(s.get("minutes", [])) != 4:
            errs.append(f"segment {s['id']}: sittings and minutes take four numbers")
    seen = set()
    for p in personas:
        if p["id"] in seen:
            errs.append(f"persona {p['id']}: duplicate id")
        seen.add(p["id"])
        if p.get("segment") not in segs:
            errs.append(f"persona {p['id']}: unknown segment {p.get('segment')}")
        for k in p.get("places", {}):
            if k not in PLACES:
                errs.append(f"persona {p['id']}: unknown place {k}")
        for t in p.get("triggers", []):
            if t.get("place") not in PLACES:
                errs.append(f"persona {p['id']}: trigger names unknown place {t.get('place')}")
    for p in personas:
        if p.get("with") and p["with"] not in seen:
            errs.append(f"persona {p['id']}: with names unknown persona {p['with']}")
    if errs:
        raise SystemExit("loop_timescale_sim: " + "; ".join(errs))


def segment_of(p, segs):
    s = copy.deepcopy(segs[p["segment"]])
    s.update(p.get("override", {}))
    return s


# ------------------------------------------------------------------------------------------------------------ fit

def unit_span(place, model, eco, scenario):
    """Seconds of the smallest thing a place lets you finish, as [lo, hi]."""
    ids = nodes_by_id(model)
    if place == "voyages":
        u = eco["units"]["voyage"]
        if scenario == "today":
            return u["now"]
        if scenario == "planned":
            return u["plan"]
        return u["chapter"]
    kids = [c for c in children(model, place) if c["id"].startswith(("round.", "toy."))
            and "x" not in str(c.get("in", ""))]
    if place == "toybox":
        kids = [c for c in kids if c["id"] == "toy.fly"]  # free flight can be stopped at any point
    lo = min(c["t"][0] for c in kids)
    hi = max(c["t"][1] for c in kids)
    return [lo, hi] if kids else ids[place]["t"]


def overlap(a, b):
    lo, hi = max(a[0], b[0]), min(a[1], b[1])
    return max(0.0, hi - lo) / max(1e-9, a[1] - a[0])


def fit_table(model, eco, segs):
    """For every segment, place and scenario: the share of sittings one unit fits in, and the unit-length match."""
    rng = random.Random(SEED)
    table = {}
    for sid, s in segs.items():
        row = {}
        for sc in SCENARIOS:
            for place in PLACES:
                u = unit_span(place, model, eco, sc)
                fits = 0
                n = 400
                for i in range(n):
                    weekend = i % 7 >= 5
                    m = s["minutes"][2:] if weekend else s["minutes"][:2]
                    sit = rng.uniform(*m) * 60
                    if rng.uniform(*u) <= sit:
                        fits += 1
                pref = [s["unit"][0] * 60, s["unit"][1] * 60]
                row[f"{sc}:{place}"] = {
                    "fits": round(fits / n, 2),
                    "match": round(overlap(u, pref) if u[1] > u[0] else 0.0, 2),
                    "unit_min": [round(u[0] / 60, 1), round(u[1] / 60, 1)],
                }
        rhythm_now = 0  # nothing in the build changes on a schedule the player sees (era.now)
        row["reset"] = {"wants": s["reset"], "today": rhythm_now, "planned": rhythm_now,
                        "proposed": s["reset"] if s["reset"] in (1, 7, 42) else 0}
        table[sid] = row
    return table


def fit_flags(table, segs):
    """The mismatches worth a line in the report: a place whose unit rarely fits, or a rhythm that is missing."""
    flags = []
    for sid, row in table.items():
        for sc in SCENARIOS:
            for place in PLACES:
                c = row[f"{sc}:{place}"]
                if c["fits"] < 0.5:
                    flags.append({"segment": sid, "scenario": sc, "place": place, "kind": "unit-too-long",
                                  "fits": c["fits"], "unit_min": c["unit_min"]})
        if segs[sid]["reset"] and row["reset"]["planned"] == 0:
            flags.append({"segment": sid, "scenario": "planned", "place": "era", "kind": "no-reset-rhythm",
                          "wants": segs[sid]["reset"]})
    return flags


# ------------------------------------------------------------------------------------------------------ simulate

def want_match(place, seg, model):
    """How much of what a segment wants a place offers: the share of its want weight the place's drivers carry."""
    drivers = set(nodes_by_id(model)[place]["drivers"])
    for c in children(model, place):
        if "x" not in str(c.get("in", "")):
            drivers |= set(c["drivers"])
    total = sum(seg["wants"].values()) or 1
    return sum(w for k, w in seg["wants"].items() if k in drivers) / total


class Run:
    """One persona through one scenario, one seed."""

    def __init__(self, p, seg, model, eco, scenario, seed, partner=None, log=True):
        self.p, self.seg, self.model, self.eco, self.sc = p, seg, model, eco, scenario
        self.rng = random.Random(seed)
        self.log = log
        self.partner = partner  # the persona whose sittings this one joins, by id
        self.fix = set(FIXES) if scenario == "proposed" else set()
        self.skill = p["skill"]
        self.fly_skill = p.get("fly_skill", p["skill"] if p.get("seat") != "puzzle" else 0.12)
        self.e = seg["habit"]
        self.churned_on = None
        self.crystals = 0
        self.earned = 0
        self.spent = 0
        self.vessels = 0
        self.sinks = 0
        self.quest = 0
        self.rung = 0
        self.i3_plays = 0
        self.hangar = scenario == "today"
        self.found_seat = p.get("seat") != "puzzle" or "puzzle_door" in self.fix
        self.saga_chapters = 12
        self.saga_done = 0
        self.chapter_progress = 0.0
        self.voyage_len = 0.0
        self.wins_since_goal = 0
        self.rounds = 0
        self.steps = []
        self.first_sitting_finished = None
        self.days = []
        self.milestones = {}
        self.played = set()
        self.stats = {"sittings": 0, "minutes": 0.0, "lost_hauls": 0, "voyage_starts": 0, "benched": 0,
                      "matches": 0, "no_seat": 0, "flew_unwanted": 0, "crossings": 0,
                      "arcade_rounds": 0, "arcade_wins": 0, "arena_rounds": 0, "arena_wins": 0, "first3_wins": 0,
                      "first3_rounds": 0}
        self.want = {pl: want_match(pl, seg, model) for pl in PLACES}

    # -- helpers
    def mark(self, name, day):
        self.milestones.setdefault(name, day)

    def step(self, day, minute, loop, dur, parent, cr=0, event=None, with_=None):
        if self.log:
            s = {"day": day, "minute": round(minute, 2), "loop": loop, "dur_s": int(dur), "parent": parent,
                 "crystals": cr, "layer": LAYER[self.sc]}
            if event:
                s["event"] = event
            if with_:
                s["with"] = with_
            self.steps.append(s)

    def sinks_left(self):
        if self.sc == "today":
            return []  # the master unlock owns every vessel; crystals buy nothing
        left = []
        if self.vessels < self.eco["prices"]["vessels_for_sale"]:
            left.append(("vessel", self.eco["prices"]["vessel"]))
        if "sinks" in self.fix:
            used = self.sinks
            for name, s in self.eco["prices"]["prop_sinks"].items():
                if used < s["count"]:
                    left.append((name, s["price"]))
                    break
                used -= s["count"]
        return left

    def next_goal_wins(self):
        """Wins the next thing to earn is away, for the grind feeling."""
        if not self.hangar:
            need = self.eco["quests"]["rung_to_clear"]["prop" if "quest_rung" in self.fix else "now"]
            return max(1, need - 1 - self.rung)
        left = self.sinks_left()
        if not left:
            return 0
        price = min(c for _, c in left)
        return max(0, math.ceil((price - self.crystals) / self.eco["payout"]["place"][0]))

    def place_payout(self, win_p):
        r = self.rng.random()
        pay = self.eco["payout"]["place"]
        if r < win_p:
            return 0
        if r < win_p + (1 - win_p) * 0.45:
            return 1
        return 2

    def win_prob(self, skill, opp, seats=3):
        """Chance of first place. Equal skill wins 1 in `seats`; the gap moves it along a logistic curve."""
        x = 1 / (1 + math.exp(-(skill - opp) * 6))
        return min(0.95, x * 2 / seats)

    def learn(self):
        self.skill += self.p["learns"] / 10 * (1 - self.skill)
        self.fly_skill += self.p["learns"] / 20 * (1 - self.fly_skill)

    # -- the places
    def open_places(self):
        if self.hangar:
            return list(PLACES)
        return list(self.eco["gates"]["open_from_start"])

    def choose_place(self, remaining, weekend, joined):
        if joined:
            return joined
        w = dict(self.p["places"])
        if weekend and self.p.get("weekend_places"):
            w = dict(self.p["weekend_places"])
        opts = []
        for pl in self.open_places():
            weight = w.get(pl, 0)
            if weight <= 0:
                continue
            if pl == "voyages" and self.saga_done >= self.saga_chapters:
                weight *= 0.4  # the authored sagas are spent; voyages lose their story
            u = unit_span(pl, self.model, self.eco, self.sc)
            if u[0] > remaining:
                weight *= 0.15  # people mostly do not start what cannot fit
            opts.append((pl, weight))
        if not opts:
            return None
        tot = sum(x for _, x in opts)
        r = self.rng.uniform(0, tot)
        for pl, x in opts:
            r -= x
            if r <= 0:
                return pl
        return opts[-1][0]

    def arcade_round(self, day, minute, joined_with):
        kinds = [("round.time", 3), ("round.charge", 2), ("round.ms", 1.5), ("round.party", 1)]
        loop = self.rng.choices([k for k, _ in kinds], [w for _, w in kinds])[0]
        t = nodes_by_id(self.model)[loop]["t"]
        dur = self.rng.uniform(*t)
        puzzle = self.p.get("seat") == "puzzle"
        seat_ok = self.found_seat and (self.hangar or "puzzle_door" in self.fix)
        if puzzle and not seat_ok:
            # The quest chain's cards are flying races, and the Honeyguide's own cards open only with the chain.
            # A puzzle player sent into flying races. In today's build the Ratel is reached only through the Toybox.
            skill = self.fly_skill
            self.stats["flew_unwanted"] += 1
        else:
            skill = self.skill
        opp = self.eco["ladder"]["ai_strength"][min(self.rung, 3)]
        if self.hangar and self.sc != "today" or self.sc == "today":
            # Past the chain a player picks the rung that is a fair fight for them (payout does not scale with it).
            opp = max(opp, min(0.85, skill + 0.05)) if self.rung >= 1 else opp
        place = self.place_payout(self.win_prob(skill, opp))
        cr = self.eco["payout"]["place"][place]
        self.stats["arcade_rounds"] += 1
        if self.stats["arcade_rounds"] <= 10:
            self.stats["first3_rounds"] += 1
            self.stats["first3_wins"] += place == 0
        self.stats["arcade_wins"] += place == 0
        ev = None
        if place == 0:
            self.wins_since_goal += 1
            self.mark("first_win", day)
            if self.rung < 2:
                self.rung += 1
            elif self.rung == 2:
                self.i3_plays += 1
                if self.i3_plays >= self.eco["ladder"]["plays_at_i3_for_i4"]:
                    self.rung = 3
            else:
                self.rung = 3
        elif self.rung == 2:
            self.i3_plays += 1
        if not self.hangar:
            need = self.eco["quests"]["rung_to_clear"]["prop" if "quest_rung" in self.fix else "now"]
            if self.rung + 1 >= need and place == 0:
                self.quest += 1
                ev = f"quest {self.eco['quests']['chain'][self.quest - 1]} claimed"
                self.mark(f"quest_{self.quest}", day)
                self.rung, self.i3_plays = 0, 0
                if self.quest >= len(self.eco["quests"]["chain"]):
                    self.hangar = True
                    ev = "Hangar opens"
                    self.mark("hangar", day)
        elif self.rung == 3 and place == 0 and self.rng.random() < 0.3:
            self.rung, self.i3_plays = 0, 0  # moves on to a new card
        return loop, dur, cr, ev

    def arena_round(self, day, joined_with):
        kinds = [("round.regatta", 2), ("round.broadside", 2), ("round.broodrush", 2), ("round.astro", 1)]
        loop = self.rng.choices([k for k, _ in kinds], [w for _, w in kinds])[0]
        dur = self.rng.uniform(*nodes_by_id(self.model)[loop]["t"])
        people = self.p.get("people", 1)
        benched = False
        team = loop in ("round.broadside", "round.broodrush", "round.astro")
        if people > self.eco["social"]["squad_seats"] and team:
            # Four people, three seats: one sits out unless a crew vessel can take two of them into the arena.
            if "crew_seat" not in self.fix:
                benched = True
        skill = self.skill if (self.p.get("seat") != "puzzle" or self.found_seat) else self.fly_skill
        opp = 0.5
        place = self.place_payout(self.win_prob(skill, opp))
        cr = self.eco["payout"]["place"][place]
        self.stats["arena_rounds"] += 1
        self.stats["arena_wins"] += place == 0
        if place == 0:
            self.wins_since_goal += 1
            self.mark("first_arena_win", day)
        return loop, dur, cr, benched

    def spend(self, day):
        ev = []
        while True:
            left = self.sinks_left()
            if not left or left[0][1] > self.crystals:
                break  # saves for the vessel before anything cheaper
            name, cost = left[0]
            self.crystals -= cost
            self.spent += cost
            self.wins_since_goal = 0
            if name == "vessel":
                self.vessels += 1
                self.mark("first_vessel", day)
                if self.vessels == self.eco["prices"]["vessels_for_sale"]:
                    self.mark("fleet_owned", day)
                ev.append(f"buys vessel {self.vessels}")
            else:
                self.sinks += 1
                ev.append(f"buys {name.replace('_', ' ')}")
        if self.sc != "today" and self.hangar and not self.sinks_left():
            self.mark("nothing_to_buy", day)
        return ev

    # -- one sitting
    def sitting(self, day, start_min, length_min, joined=None, joined_with=None):
        sat = 0.0
        t = 0.0
        L = length_min * 60
        earned = 0
        unlocks = 0
        places_seen = []
        self.step(day, start_min, "sitting", 0, "era", event="opens the game", with_=joined_with)
        if self.sc == "today" and self.stats["sittings"] < 2:
            sat -= 0.1  # every lock open, no guide: the first sittings land on a full menu
        place = None
        while t < L - 45:
            nxt = self.choose_place(L - t, day % 7 >= 5, joined if not places_seen else None)
            if nxt is None:
                break
            if place is not None and nxt != place:
                if "breath" not in self.fix and self.rng.random() < 0.6:
                    break  # nothing offers the crossing; most people close the game instead
                self.stats["crossings"] += 1
            place = nxt
            places_seen.append(place)
            if place == "arena" and self.p.get("seat") == "puzzle":
                if "crew_seat" not in self.fix:
                    # The Honeyguide seats no arena card: no seat for the Ratel in her son's squad.
                    self.stats["no_seat"] += 1
                    self.step(day, start_min + t / 60, "arena", 30, "sitting",
                              event="no seat: the Honeyguide has no arena card", with_=joined_with)
                    sat -= 0.3
                    t += 30
                    joined = None
                    continue
            group_note = None
            if place == "voyages" and self.p.get("people", 1) > 1:
                if self.sc == "today":
                    group_note = "voyages are single-device today: the group splits into solo voyages"
                    sat -= 0.25
                elif self.p["people"] > self.eco["social"]["squad_seats"] and "crew_seat" not in self.fix:
                    group_note = "a voyage party is three: one of the group sits out"
                    self.stats["benched"] += 1
                    sat -= 0.1
                if group_note:
                    self.step(day, start_min + t / 60, "voyages", 0, "sitting", event=group_note,
                              with_=joined_with)
            if place == "voyages" and self.p.get("seat") == "puzzle" and self.sc == "today":
                self.stats["no_seat"] += 1
                self.step(day, start_min + t / 60, "voyages", 30, "sitting",
                          event="no seat: voyages are single-device today", with_=joined_with)
                sat -= 0.3
                t += 30
                joined = None
                continue
            block_end = t + self.rng.uniform(0.35, 1.0) * (L - t)
            if place == "toybox":
                if self.p.get("seat") == "puzzle" and not self.found_seat and self.rng.random() < 0.2:
                    self.found_seat = True
                    self.mark("found_ratel", day)
                    self.step(day, start_min + t / 60, "toy.changers", 40, "toybox",
                              event="finds the Honeyguide and Ratel in the Vessel Changer")
                    t += 40
                while t < block_end:
                    loop = self.rng.choices(["toy.fly", "toy.paint", "toy.wander", "toy.changers"],
                                            [2, 2, 1, 1])[0]
                    dur = min(self.rng.uniform(*nodes_by_id(self.model)[loop]["t"]), L - t)
                    cr, ev = 0, None
                    if loop == "toy.paint" and "make_pays" in self.fix:
                        cr = self.eco["payout"]["painting"]
                        if self.rng.random() < 0.3:
                            ev = "a shared painting is liked"
                            sat += 0.1
                    self.crystals += cr
                    earned += cr
                    self.step(day, start_min + t / 60, loop, dur, "toybox", cr, ev, joined_with)
                    t += dur
                sat += 0.15 * self.want["toybox"]
            elif place == "arcade":
                while t < block_end:
                    loop, dur, cr, ev = self.arcade_round(day, start_min + t / 60, joined_with)
                    if t + dur > L + 60:
                        break
                    self.rounds += 1
                    self.stats["matches"] += 1
                    self.learn()
                    self.crystals += cr
                    earned += cr
                    if ev:
                        unlocks += 1
                    self.step(day, start_min + t / 60, loop, dur, "arcade", cr, ev, joined_with)
                    t += dur + 20
                    for e in self.spend(day):
                        unlocks += 1
                        self.step(day, start_min + t / 60, "arc.fleet", 15, "era", 0, e)
                        t += 15
                    self.mark("first_round", day)
                sat += 0.2 * self.want["arcade"]
            elif place == "arena":
                while t < block_end:
                    loop, dur, cr, benched = self.arena_round(day, joined_with)
                    if t + dur > L + 60:
                        break
                    self.rounds += 1
                    self.stats["matches"] += 1
                    self.learn()
                    ev = None
                    if benched:
                        self.stats["benched"] += 1
                        ev = "one of four sits out: three seats"
                        sat -= 0.04
                    self.crystals += cr
                    earned += cr
                    self.step(day, start_min + t / 60, loop, dur, "arena", cr, ev, joined_with)
                    t += dur + 20
                    for e in self.spend(day):
                        unlocks += 1
                        self.step(day, start_min + t / 60, "arc.fleet", 15, "era", 0, e)
                        t += 15
                    self.mark("first_arena", day)
                sat += 0.25 * self.want["arena"]
            elif place == "voyages":
                u = unit_span("voyages", self.model, self.eco, self.sc)
                dur = self.rng.uniform(*u)
                self.stats["voyage_starts"] += 1
                self.mark("first_voyage", day)
                left = L - t
                clean = self.rng.random() < self.win_prob(self.skill, 0.45)
                if "chapters" in self.fix:
                    # One planned-length voyage, flown in as many sittings as it takes; leaving at a location
                    # keeps the haul aboard for next time.
                    if self.chapter_progress == 0:
                        self.voyage_len = self.rng.uniform(*self.eco["units"]["voyage"]["plan"])
                    remaining = (1 - self.chapter_progress) * self.voyage_len
                    take = min(remaining, max(left, min(dur, left)))
                    self.chapter_progress += take / self.voyage_len
                    done = self.chapter_progress >= 0.999
                    phase_loops = ["voy.port", "voy.leg", "voy.place"]
                    self.step(day, start_min + t / 60, self.rng.choice(phase_loops), take, "voyages", 0,
                              "docks at a location; the voyage waits" if not done else None, joined_with)
                    t += take
                    if done:
                        self.chapter_progress = 0
                        pay = self.eco["payout"]["voyage_clean"] if clean else self.eco["payout"]["voyage_failed"]
                        self.crystals += pay
                        earned += pay
                        self.step(day, start_min + t / 60, "voy.home", 60, "voyages", pay,
                                  "docks home" + (", saga chapter" if clean else ", job failed"), joined_with)
                        t += 60
                        if clean and self.saga_done < self.saga_chapters:
                            self.saga_done += 1
                            unlocks += 1
                            if self.saga_done == self.saga_chapters:
                                self.mark("sagas_spent", day)
                        self.mark("first_dock", day)
                        for e in self.spend(day):
                            unlocks += 1
                            self.step(day, start_min + t / 60, "arc.fleet", 15, "era", 0, e)
                    sat += 0.3 * self.want["voyages"]
                elif dur > left:
                    # Started what does not fit: quitting before the dock loses the haul.
                    self.stats["lost_hauls"] += 1
                    self.step(day, start_min + t / 60, "voy.leg", left, "voyages", 0,
                              "quits before docking: the haul is lost", joined_with)
                    t = L
                    sat -= 0.35
                else:
                    pay = 0
                    if "one_currency" in self.fix:
                        pay = self.eco["payout"]["voyage_clean"] if clean else 0
                    self.step(day, start_min + t / 60, "voy.port", dur * 0.15, "voyages", 0, None, joined_with)
                    self.step(day, start_min + t / 60 + dur * 0.15 / 60, "voy.leg", dur * 0.6, "voyages", 0,
                              None, joined_with)
                    self.step(day, start_min + t / 60 + dur * 0.75 / 60, "voy.home", dur * 0.25, "voyages", pay,
                              "docks" + (", saga chapter" if clean and self.saga_done < self.saga_chapters else ""),
                              joined_with)
                    t += dur
                    self.crystals += pay
                    earned += pay
                    self.mark("first_dock", day)
                    if clean and self.saga_done < self.saga_chapters:
                        self.saga_done += 1
                        unlocks += 1
                        if self.saga_done == self.saga_chapters:
                            self.mark("sagas_spent", day)
                    for e in self.spend(day):
                        unlocks += 1
                        self.step(day, start_min + t / 60, "arc.fleet", 15, "era", 0, e)
                    sat += 0.3 * self.want["voyages"]
            joined = None
        self.step(day, start_min + t / 60, "sitting", 0, "era", event="closes the game", with_=joined_with)
        if self.stats["sittings"] == 0:
            self.first_sitting_finished = (self.rounds > 0 or "first_dock" in self.milestones
                                           or "toybox" in places_seen)
        self.stats["sittings"] += 1
        self.stats["minutes"] += t / 60
        self.earned += earned
        # How the sitting felt.
        sat += 0.25 * min(1.0, earned / 200)
        sat += min(0.4, 0.2 * unlocks)
        if joined_with:
            sat += 0.2 if self.seg["social"] != "solo" or self.p.get("with") else 0.1
        if self.p.get("seat") == "puzzle" and not (self.found_seat and (self.hangar or "puzzle_door" in self.fix)):
            sat -= 0.2
        g = self.next_goal_wins()
        if g > self.seg["patience"]:
            sat -= 0.2 * min(1.0, (g - self.seg["patience"]) / self.seg["patience"])
        if self.sc != "today" and self.hangar and not self.sinks_left() and self.seg["wants"].get("own", 0) > 0:
            sat -= 0.1  # nothing left to earn toward
        self.e = max(0.0, min(0.95, self.e + 0.10 * (sat - 0.3)))

    # -- days
    def scheduled(self, day):
        s = self.seg
        weekend = day % 7 >= 5
        lo, hi = (s["sittings"][2], s["sittings"][3]) if weekend else (s["sittings"][0], s["sittings"][1])
        n = self.rng.uniform(lo, hi)
        k = int(n) + (1 if self.rng.random() < n - int(n) else 0)
        return k

    def pull(self, day):
        """What brings them back today, beyond the habit."""
        bonus = 0.0
        if "tide" in self.fix:
            if self.seg["reset"] == 1:
                bonus += 0.12  # a daily job that expires
            if self.seg["reset"] == 7 and day % 7 == 0:
                bonus += 0.15  # the card of the week
            if day % 7 == 0:
                bonus += 0.04
        g = self.next_goal_wins()
        if 0 < g <= max(2, self.seg["patience"] // 3):
            bonus += 0.08  # something is close
        return bonus

    def day(self, d, joins):
        if self.churned_on is not None:
            back = 0.002
            if "tide" in self.fix and d % 7 == 0:
                back = 0.06 if self.seg["reset"] else 0.03  # news from the sea: this week's card, a new Tide
            if d % 42 == 0 and d > 0 and "tide" in self.fix:
                back = 0.25
            if self.rng.random() < back:
                self.churned_on = None
                self.e = self.seg["habit"] * 0.8
                self.mark("came_back", d)
                self.step(d, 12 * 60, "era.tide" if "tide" in self.fix else "era", 0, "era",
                          event="comes back for the news")
            else:
                self.days.append({"day": d, "active": False, "crystals": self.crystals, "vessels": self.vessels})
                return
        n = self.scheduled(d)
        hours = sorted(self.rng.sample(self.seg["hours"], min(n, len(self.seg["hours"])))) if n else []
        played = False
        join = joins.get(d)
        comes = self.rng.random() < min(1.0, self.e + self.pull(d))
        if join and hours:
            comes = comes or self.rng.random() < 0.9  # someone asked them
        for i, h in enumerate(hours):
            if not comes or (i > 0 and self.rng.random() > 0.75):
                continue
            weekend = d % 7 >= 5
            m = self.seg["minutes"][2:] if weekend else self.seg["minutes"][:2]
            length = self.rng.uniform(*m)
            if join and i == len(hours) - 1:
                length = max(length, 30)
                self.sitting(d, h * 60 + self.rng.uniform(0, 40), length, join["place"], join["with"])
            else:
                self.sitting(d, h * 60 + self.rng.uniform(0, 40), length)
            played = True
        self.e = max(0.0, self.e - 0.02)  # novelty fades unless the sittings earn it back
        if played:
            self.played.add(d)
        if self.e < 0.15 and self.churned_on is None:
            self.churned_on = d
            self.mark("lapsed", d)
        self.days.append({"day": d, "active": played, "crystals": self.crystals, "vessels": self.vessels})

    def run(self, joins):
        self.mark("install", 0)
        if self.sc == "today":
            self.mark("hangar", 0)  # the master unlock: open at install, every vessel owned
            self.mark("nothing_to_buy", 0)
        for d in range(DAYS):
            self.day(d, joins)
        return self


def joins_for(p, personas):
    """The days someone asks this persona in, from its triggers."""
    out = {}
    for t in p.get("triggers", []):
        if t["event"] == "join":
            # A trigger repeats on the same weekday after it first fires, while both keep playing.
            for d in range(t["day"], DAYS, 7):
                out[d] = {"place": t["place"], "with": p.get("with")}
    return out


def simulate(p, seg, model, eco, sc, seed, log=True):
    return Run(p, seg, model, eco, sc, seed, log=log).run(joins_for(p, None))


# ------------------------------------------------------------------------------------------------------- summary

def summarize(runs):
    def rate(f):
        return round(sum(1 for r in runs if f(r)) / len(runs), 2)

    def med(key):
        xs = [r.milestones[key] for r in runs if key in r.milestones]
        return {"median_day": statistics.median(xs) if xs else None, "share": round(len(xs) / len(runs), 2)}

    hours = [r.stats["minutes"] / 60 for r in runs]
    earned = [r.earned for r in runs]
    unspent = [r.crystals for r in runs]
    per_hour = [r.earned / (r.stats["minutes"] / 60) for r in runs if r.stats["minutes"] > 30]
    lost = sum(r.stats["lost_hauls"] for r in runs)
    vstarts = sum(r.stats["voyage_starts"] for r in runs)
    matches = sum(r.stats["matches"] for r in runs)
    return {
        "d1": rate(lambda r: 1 in r.played),
        "d7": rate(lambda r: 7 in r.played),
        "d30_window": rate(lambda r: any(28 <= d <= 34 for d in r.played)),
        "active_day_41": rate(lambda r: any(d >= 35 for d in r.played)),
        "hours_42d": round(statistics.mean(hours), 1),
        "crystals_per_hour": round(statistics.mean(per_hour)) if per_hour else 0,
        "earned_42d": round(statistics.mean(earned)),
        "unspent_42d": round(statistics.mean(unspent)),
        "milestones": {k: med(k) for k in ["first_round", "first_win", "found_ratel", "hangar", "first_arena",
                                           "first_voyage", "first_dock", "first_vessel", "fleet_owned",
                                           "nothing_to_buy", "sagas_spent", "lapsed", "came_back"]},
        "lost_haul_share": round(lost / vstarts, 2) if vstarts else None,
        "benched_share": round(sum(r.stats["benched"] for r in runs) / matches, 2) if matches else None,
        "no_seat_per_run": round(sum(r.stats["no_seat"] for r in runs) / len(runs), 1),
        "first_sitting_finished": round(sum(1 for r in runs if r.first_sitting_finished)
                                        / max(1, sum(1 for r in runs if r.first_sitting_finished is not None)), 2),
        "arcade_win_rate": round(sum(r.stats["arcade_wins"] for r in runs)
                                 / max(1, sum(r.stats["arcade_rounds"] for r in runs)), 2),
        "first_ten_rounds_win_rate": round(sum(r.stats["first3_wins"] for r in runs)
                                           / max(1, sum(r.stats["first3_rounds"] for r in runs)), 2),
        "arena_win_rate": round(sum(r.stats["arena_wins"] for r in runs)
                                / max(1, sum(r.stats["arena_rounds"] for r in runs)), 2),
        "flew_unwanted_share": round(sum(r.stats["flew_unwanted"] for r in runs)
                                     / max(1, sum(r.stats["arcade_rounds"] for r in runs)), 2),
    }


def build(atlas: Path = ATLAS, runs: int = RUNS):
    model, eco, segs, personas = load(atlas)
    validate(model, eco, segs, personas)
    table = fit_table(model, eco, segs)
    flags = fit_flags(table, segs)
    journeys = {"meta": {
        "what": "One representative simulated run per persona and scenario, as steps on a time axis.",
        "step": {"day": "0-based day since install", "minute": "minute of that day the step starts",
                 "loop": "loop id in Docs/LoopAtlas/model.json", "dur_s": "seconds", "parent": "the place or tier",
                 "crystals": "crystals earned by the step", "event": "what happened, when anything did",
                 "with": "the persona played with, if any", "layer": "now | plan | prop"},
        "scenarios": {sc: LAYER[sc] for sc in SCENARIOS}, "days": DAYS, "seed": SEED,
        "fixes": FIXES, "generator": "Tools/Build/loop_timescale_sim.py"}, "personas": {}}
    summary = {"meta": {"runs": runs, "days": DAYS, "seed": SEED, "fixes": FIXES}, "fit": table, "flags": flags,
               "personas": {}}
    for i, p in enumerate(personas):
        seg = segment_of(p, segs)
        jp = {"name": p["name"], "segment": p["segment"], "is": p["is"], "people": p.get("people", 1),
              "with": p.get("with"), "challenge": p.get("challenge", ""), "scenarios": {}}
        sp = {"name": p["name"], "segment": p["segment"], "scenarios": {}}
        for j, sc in enumerate(SCENARIOS):
            all_runs = [simulate(p, seg, model, eco, sc, SEED + 1000 * i + k, log=False) for k in range(runs)]
            # The representative run: the seed whose D7 and hours sit closest to the population's.
            s = summarize(all_runs)
            target_h = s["hours_42d"]
            best = min(range(runs), key=lambda k: abs(all_runs[k].stats["minutes"] / 60 - target_h)
                       + (0 if (7 in all_runs[k].played) == (s["d7"] >= 0.5) else 5))
            rep = simulate(p, seg, model, eco, sc, SEED + 1000 * i + best, log=True)
            jp["scenarios"][LAYER[sc]] = {"seed": SEED + 1000 * i + best, "steps": rep.steps,
                                          "days": rep.days, "milestones": rep.milestones}
            sp["scenarios"][sc] = s
        journeys["personas"][p["id"]] = jp
        summary["personas"][p["id"]] = sp
    summary["expectations"] = judge(load_expectations(atlas), summary)
    report = render_report(summary, segs, personas, eco, load_expectations(atlas))
    return journeys, summary, report


# -------------------------------------------------------------------------------------------------------- report

def fmt_day(m):
    if not m or m["median_day"] is None:
        return "never"
    d = m["median_day"]
    share = int(round(m["share"] * 100))
    return f"day {d:g} ({share}%)"


def render_report(summary, segs, personas, eco, expectations=()):
    L = []
    w = L.append
    w("# Loop timescales across player cultures: simulation report")
    w("")
    w("Generated by `python3 Tools/Build/loop_timescale_sim.py report` from the loop model and `Docs/LoopAtlas/sim/`."
      " Do not edit by hand. The segment numbers are priors, not measurements (each says how sure it is in"
      " `segments.yaml`): read the comparisons between scenarios and personas, not the absolute values.")
    w("")
    w(f"Each persona is simulated {summary['meta']['runs']} times for {summary['meta']['days']} days (one Tide) in"
      " three scenarios that follow the atlas layers:")
    w("")
    w("- **today** (Now): the master developer unlock is on, so every place and vessel is open, no guide runs, and"
      " crystals buy nothing.")
    w("- **planned** (Planned): the gate flipped. The quest chain (I4 on four modes) opens the Hangar, Arena and"
      " Voyages; a voyage is the planned 20 to 40 minutes; vessels cost 4,000 and only placing pays.")
    w("- **proposed** (Proposed): planned plus these fixes:")
    for k, v in summary["meta"]["fixes"].items():
        w(f"  - `{k}`: {v}")
    w("")
    w("## 1. Retention and activation by persona")
    w("")
    w("D1 and D7 are the share of runs that played on exactly that day; the paid-EA gate is D1 35% and D7 15%"
      " (`Docs/ACTIVATION.md`). D30 is any play in days 28 to 34. Day 1 and day 7 are weekdays"
      " here (day 0 is a Monday), so a weekend-only player scores low on D1 and D7 by design; read D30 for them.")
    w("")
    w("| Persona | Segment | Scenario | D1 | D7 | D30 | Hours in 42 days | Hangar | First vessel | First dock |")
    w("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for pid, sp in summary["personas"].items():
        for sc in SCENARIOS:
            s = sp["scenarios"][sc]
            m = s["milestones"]
            w(f"| {sp['name']} | {sp['segment']} | {sc} | {s['d1']:.0%} | {s['d7']:.0%} | {s['d30_window']:.0%} |"
              f" {s['hours_42d']} | {fmt_day(m['hangar'])} | {fmt_day(m['first_vessel'])} |"
              f" {fmt_day(m['first_dock'])} |")
    w("")
    w("A milestone reads `day N (S%)`: the median day among the runs that reached it, and the share that did.")
    w("")
    w("## 2. The economy")
    w("")
    w("| Persona | Scenario | Crystals per hour | Earned | Unspent at day 42 | Fleet owned | Nothing left to buy |"
      " Lost voyage hauls | Benched matches |")
    w("| --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for pid, sp in summary["personas"].items():
        for sc in SCENARIOS:
            s = sp["scenarios"][sc]
            m = s["milestones"]
            lh = "-" if s["lost_haul_share"] is None else f"{s['lost_haul_share']:.0%}"
            bs = "-" if not s["benched_share"] else f"{s['benched_share']:.0%}"
            w(f"| {sp['name']} | {sc} | {s['crystals_per_hour']} | {s['earned_42d']} | {s['unspent_42d']} |"
              f" {fmt_day(m['fleet_owned'])} | {fmt_day(m['nothing_to_buy'])} | {lh} | {bs} |")
    w("")
    w("## 2a. Activation paths")
    w("")
    w("The order each persona meets the game's milestones in, by median day (planned, then proposed). A milestone"
      " fewer than half the runs reach is left out. First-ten-rounds win rate is how often they took first place in"
      " their first ten arcade rounds, the number the I1 AI decides.")
    w("")
    order = ["first_round", "first_win", "found_ratel", "hangar", "first_arena", "first_voyage", "first_dock",
             "first_vessel", "fleet_owned", "sagas_spent", "nothing_to_buy", "lapsed", "came_back"]
    words = {"first_round": "first round", "first_win": "first win", "found_ratel": "finds the Ratel",
             "hangar": "Hangar", "first_arena": "first arena", "first_voyage": "first voyage",
             "first_dock": "first dock", "first_vessel": "first vessel", "fleet_owned": "fleet owned",
             "sagas_spent": "sagas spent", "nothing_to_buy": "nothing left to buy", "lapsed": "lapses",
             "came_back": "comes back"}
    w("| Persona | Scenario | Path (median day) | First-ten-rounds win rate |")
    w("| --- | --- | --- | --- |")
    for pid, sp in summary["personas"].items():
        for sc in ("planned", "proposed"):
            s = sp["scenarios"][sc]
            ms = [(k, s["milestones"][k]) for k in order if s["milestones"][k]["share"] >= 0.5]
            ms.sort(key=lambda kv: kv[1]["median_day"])
            path = " > ".join(f"{words[k]} {m['median_day']:g}" for k, m in ms) or "-"
            w(f"| {sp['name']} | {sc} | {path} | {s['first_ten_rounds_win_rate']:.0%} |")
    w("")
    w("## 3. Timescale fit: does one finishable unit fit a sitting?")
    w("")
    w("The share of a segment's sittings that one unit of the place fits into (a round in the Arcade and Arena, a"
      " toy in the Toybox, a voyage, or in `proposed` a voyage chapter).")
    w("")
    w("| Segment | Sitting (min, weekday / weekend) | Toybox | Arcade | Arena | Voyage today | Voyage planned |"
      " Voyage chapter | Reset it answers to |")
    w("| --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for sid, row in summary["fit"].items():
        s = segs[sid]
        rs = {0: "none", 1: "daily", 7: "weekly", 42: "seasonal"}.get(s["reset"], str(s["reset"]))
        w(f"| {s['name']} | {s['minutes'][0]}-{s['minutes'][1]} / {s['minutes'][2]}-{s['minutes'][3]} |"
          f" {row['planned:toybox']['fits']:.0%} | {row['planned:arcade']['fits']:.0%} |"
          f" {row['planned:arena']['fits']:.0%} | {row['today:voyages']['fits']:.0%} |"
          f" {row['planned:voyages']['fits']:.0%} | {row['proposed:voyages']['fits']:.0%} | {rs} |")
    w("")
    w("Nothing in the build changes on a schedule the player sees (the long game's last phase is empty), so every"
      " segment that answers to a daily or weekly reset finds none today or in the plan; The Tide (P6) is the"
      " only one proposed.")
    w("")
    w("## 4. What each persona's path shows")
    w("")
    for p in personas:
        sp = summary["personas"][p["id"]]
        t, pl, pr = (sp["scenarios"][sc] for sc in SCENARIOS)
        w(f"### {p['name']} ({p['segment']})")
        w("")
        w(p["is"].strip())
        w("")
        w(f"Challenge: {p.get('challenge', '').strip()}")
        w("")
        w(f"- D7 {t['d7']:.0%} today, {pl['d7']:.0%} planned, {pr['d7']:.0%} proposed; D30 {t['d30_window']:.0%},"
          f" {pl['d30_window']:.0%}, {pr['d30_window']:.0%}.")
        w(f"- Hangar: {fmt_day(pl['milestones']['hangar'])} planned, {fmt_day(pr['milestones']['hangar'])}"
          f" proposed. First vessel: {fmt_day(pl['milestones']['first_vessel'])} planned,"
          f" {fmt_day(pr['milestones']['first_vessel'])} proposed.")
        if p.get("seat") == "puzzle":
            w(f"- Found the Ratel: {fmt_day(t['milestones']['found_ratel'])} today. Asked into a squad with no"
              f" seat for her: {t['no_seat_per_run']} times today, {pl['no_seat_per_run']} planned,"
              f" {pr['no_seat_per_run']} proposed. Share of her rounds flown when she wanted the puzzle:"
              f" {t['flew_unwanted_share'] or 0:.0%} today, {pl['flew_unwanted_share'] or 0:.0%} planned.")
        if pl["lost_haul_share"]:
            w(f"- Voyages started and abandoned before the dock (haul lost): {pl['lost_haul_share']:.0%} planned,"
              f" {t['lost_haul_share'] or 0:.0%} today, {pr['lost_haul_share'] or 0:.0%} proposed (chapters).")
        if pl["benched_share"]:
            w(f"- Team matches with one of the group sitting out: {pl['benched_share']:.0%} planned,"
              f" {pr['benched_share'] or 0:.0%} proposed.")
        sg = pl["milestones"]["sagas_spent"]
        if sg["share"]:
            w(f"- The four authored sagas (12 chapters) were spent by {fmt_day(sg)} planned.")
        w("")
    w("## 5. Activation expectations")
    w("")
    w("Proposed targets from `sim/expectations.yaml`, judged on the simulated runs. A missed line in `proposed` is a"
      " hole the fixes do not close yet.")
    w("")
    w("| Expectation | Met today | Met planned | Met proposed | Missed in proposed by |")
    w("| --- | --- | --- | --- | --- |")
    names = {pid: sp["name"] for pid, sp in summary["personas"].items()}
    for e in expectations:
        rows = [r for r in summary["expectations"] if r["id"] == e["id"]]

        def count(sc):
            judged = [r for r in rows if r[sc] != "n/a"]
            return f"{sum(r[sc] == 'met' for r in judged)} of {len(judged)}"
        missed = ", ".join(names[r["persona"]] for r in rows if r["proposed"] == "missed") or "-"
        w(f"| {e['id']} {e['why']} | {count('today')} | {count('planned')} | {count('proposed')} | {missed} |")
    w("")
    w("## 6. Mismatch flags")
    w("")
    w("Every place whose unit fits fewer than half of a segment's sittings, and every reset rhythm a segment wants"
      " that the game lacks.")
    w("")
    for f in summary["flags"]:
        if f["kind"] == "unit-too-long":
            w(f"- {segs[f['segment']]['name']}, {f['scenario']}: a {f['place']} unit"
              f" ({f['unit_min'][0]}-{f['unit_min'][1]} min) fits {f['fits']:.0%} of sittings.")
        else:
            rs = {1: "daily", 7: "weekly", 42: "seasonal"}.get(f["wants"], str(f["wants"]))
            w(f"- {segs[f['segment']]['name']}: answers to a {rs} reset; the game has none.")
    w("")
    return "\n".join(L) + "\n"


# --------------------------------------------------------------------------------------------------------- main

def dump(obj):
    return json.dumps(obj, indent=1, sort_keys=False) + "\n"


def write(atlas=ATLAS, runs=RUNS):
    journeys, summary, report = build(atlas, runs)
    out = atlas / "sim" / "out"
    out.mkdir(parents=True, exist_ok=True)
    (out / "journeys.json").write_text(json.dumps(journeys, separators=(",", ":")) + "\n")
    (out / "summary.json").write_text(dump(summary))
    (atlas / "sim" / "REPORT.md").write_text(report)
    print(f"loop_timescale_sim: wrote {out / 'journeys.json'}, {out / 'summary.json'}, {atlas / 'sim' / 'REPORT.md'}")


def check(atlas=ATLAS, runs=RUNS):
    journeys, summary, report = build(atlas, runs)
    out = atlas / "sim" / "out"
    bad = []
    pairs = [(out / "journeys.json", json.dumps(journeys, separators=(",", ":")) + "\n"),
             (out / "summary.json", dump(summary)), (atlas / "sim" / "REPORT.md", report)]
    for path, text in pairs:
        if not path.exists() or path.read_text() != text:
            bad.append(str(path.relative_to(ROOT)))
    if bad:
        print("loop_timescale_sim: out of date, run `python3 Tools/Build/loop_timescale_sim.py report`: "
              + ", ".join(bad))
        return 1
    print("loop_timescale_sim: outputs up to date")
    return 0


def self_test():
    """Negative controls: each check must refuse the thing it exists to refuse."""
    model, eco, segs, personas = load()
    failures = []
    # 1. A segment whose sittings are shorter than any round must be flagged for the Arcade.
    tiny = copy.deepcopy(segs)
    tiny["x-tiny"] = dict(copy.deepcopy(next(iter(segs.values()))), id="x-tiny", minutes=[0.5, 0.8, 0.5, 0.8])
    flags = fit_flags(fit_table(model, eco, tiny), tiny)
    if not any(f["segment"] == "x-tiny" and f["place"] == "arcade" for f in flags):
        failures.append("fit did not flag a sitting shorter than every round")
    # 2. A commuter's sitting cannot hold a planned voyage.
    seg = segment_of(next(p for p in personas if p["id"] == "commuter"), segs)
    t = fit_table(model, eco, {"c": seg})
    if t["c"]["planned:voyages"]["fits"] > 0.05:
        failures.append("fit says a 20-40 minute voyage fits a 5-7 minute sitting")
    # 3. A persona with no habit and no patience must lapse.
    p = copy.deepcopy(personas[0])
    s = dict(segment_of(p, segs), habit=0.0, patience=1)
    r = simulate(p, s, model, eco, "planned", 1, log=False)
    if "lapsed" not in r.milestones:
        failures.append("a persona with no habit never lapsed")
    # 4. Today crystals buy nothing; planned they buy vessels.
    jonah = next(p for p in personas if p["id"] == "jonah")
    sj = segment_of(jonah, segs)
    rt = simulate(jonah, sj, model, eco, "today", 3, log=False)
    if rt.spent:
        failures.append("today's run spent crystals although the master unlock owns every vessel")
    # 5. Validation refuses an unknown segment.
    bad = copy.deepcopy(personas)
    bad[0]["segment"] = "nobody"
    try:
        validate(model, eco, segs, bad)
        failures.append("validate accepted an unknown segment")
    except SystemExit:
        pass
    # 6b. An expectation naming a metric that does not exist is refused.
    try:
        judge([{"id": "X", "metric": "nope", "op": ">=", "value": 1, "who": "all"}],
              {"personas": {"a": {"scenarios": {sc: {} for sc in SCENARIOS}}}})
        failures.append("judge accepted an expectation on a metric that does not exist")
    except SystemExit:
        pass
    # 6. The simulation is deterministic for a seed.
    a = simulate(jonah, sj, model, eco, "proposed", 7, log=True).steps
    b = simulate(jonah, sj, model, eco, "proposed", 7, log=True).steps
    if a != b:
        failures.append("the same seed gave two different runs")
    if failures:
        print("loop_timescale_sim self-test FAILED: " + "; ".join(failures))
        return 1
    print("loop_timescale_sim self-test: 7 negative controls refused as they should")
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", nargs="?", default="report", choices=["report", "fit", "self-test"])
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--runs", type=int, default=RUNS)
    a = ap.parse_args(argv)
    if a.check:
        return check(runs=a.runs)
    if a.cmd == "self-test":
        return self_test()
    if a.cmd == "fit":
        model, eco, segs, personas = load()
        validate(model, eco, segs, personas)
        for f in fit_flags(fit_table(model, eco, segs), segs):
            print(json.dumps(f))
        return 0
    write(runs=a.runs)
    return 0


if __name__ == "__main__":
    sys.exit(main())
