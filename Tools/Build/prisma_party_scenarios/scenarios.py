#!/usr/bin/env python3
"""The party layer's Block 3 scenarios, run against five live instances of the game.

    python3 scenarios.py --instances instances.json --out results.json

Called by run.sh, which builds the port, launches the five instances and writes instances.json
({label: {port, log, pid}}). Each scenario names the ticket it closes
(Docs/prompts/MULTIPLAYER_HARDENING_PROMPT.md Block 3; Docs/PartySystem/BUGS.md). The scenarios
run as ONE continuous session because the party they build is the state the next one needs - so
the first failure stops the run and every later scenario is reported "not run", never "passed".

What a pass here proves, and what it does not, is in README.md next to this file.
"""
import argparse
import datetime
import json
import os
import signal
import subprocess
import sys
import time
import traceback

from driver import (Inst, boot_to_menu, console, double_tap, is_solo, party, run_together,
                    score_rows, until, vessels, wait_party)

MATCH_MODE = "Bloomrush"
MATCH_SCENE = "MinigameBloomrush"


def members(n):
    return f"{n}/4"


def seated_in(host_state):
    return lambda s: s.get("role") == "client" and s.get("session") == host_state.get("session")


def host_at(n):
    return lambda s: s.get("role") == "host" and s.get("members") == members(n) and s.get("conns") == str(n)


def wait_joinable(inst, host, timeout_s=180):
    """Wait until `inst`'s online row for `host` shows the host's REAL member count - what a person
    reads on the row before pressing Join. Polled presence lags the roster by up to two refresh
    intervals, and the pre-flight refuses on the stale number (a 2/4 party read as "full" right
    after a kick and a leave failed T2 once), so racing before the row agrees tests the lag, not
    the race."""
    count = party(host)["members"]
    until(lambda: f"{host.name}(joinable {count})" in (console(inst, "party online") or ""), timeout_s,
          f"{inst} to see {host.name} as joinable at {count}", step=2)


def count(lines, needle):
    return sum(1 for l in lines if needle in l)


class Session:
    def __init__(self, insts):
        self.i = insts
        self.A, self.B, self.C, self.D, self.E = (insts[k] for k in "ABCDE")
        self.winner = self.loser = None

    # ── T1 ────────────────────────────────────────────────────────────────────
    def t1_accept(self):
        B, C = self.B, self.C
        console(B, f"party invite {C.name}")
        wait_party(C, lambda s: s.get("invite") == B.name, "the invite to arrive")
        console(C, "party accept")
        b = wait_party(B, host_at(2), "the host to count 2")
        c = wait_party(C, lambda s: seated_in(b)(s) and s.get("members") == members(2), "the guest to be seated")
        agree = set(b["names"].split(",")) == set(c["names"].split(","))
        return agree, {"host": b, "guest": c, "rosters_agree": agree}

    # ── T5 (accept) ───────────────────────────────────────────────────────────
    def t5_double_accept(self):
        B, D = self.B, self.D
        console(B, f"party invite {D.name}")
        wait_party(D, lambda s: s.get("invite") == B.name, "the invite to arrive")
        mark = D.log_mark()
        double_tap(D, "party accept")
        b = wait_party(B, host_at(3), "the host to count 3")
        wait_party(D, lambda s: seated_in(b)(s) and s.get("members") == members(3), "the guest to be seated")
        log = D.log_since(mark)
        flows = count(log, "Starting direct-join accept flow")
        stopped_by = ("controller (_transitioning)" if count(log, "ignoring duplicate accept")
                      else "console (invite already consumed)" if count(log, "no pending invite") else "unknown")
        return flows == 1, {"accept_flows_started": flows, "second_tap_stopped_by": stopped_by, "host": b}

    # ── T2b / B25 ─────────────────────────────────────────────────────────────
    def t2b_race_for_last_seat(self):
        A, B, E = self.A, self.B, self.E
        for i in (A, E):
            wait_joinable(i, B)
        marks = {i.label: i.log_mark() for i in (A, E)}
        skew = run_together([A, E], f"party join {B.name}")
        b = wait_party(B, host_at(4), "the host to count 4", 180)
        states = {}
        for i in (A, E):
            # Both start solo, so "solo" means nothing until the join itself has returned:
            # PartyConsoleCommand logs "<what> finished" (or "failed") when the task ends.
            until(lambda: any(f"join {B.name} finished" in l or f"join {B.name} failed" in l
                              for l in i.log_since(marks[i.label])), 180, f"{i}'s join to return")
            states[i.label] = wait_party(i, lambda s: seated_in(b)(s) or is_solo(s), f"{i} to settle", 180)
        seated = [i for i in (A, E) if seated_in(b)(states[i.label])]
        bounced = [i for i in (A, E) if is_solo(states[i.label])]
        ok = len(seated) == 1 and len(bounced) == 1
        detail = {"press_skew_ms": skew, "host": b, "A": states["A"], "E": states["E"]}
        if ok:
            self.winner, self.loser = seated[0], bounced[0]
            log = self.loser.log_since(marks[self.loser.label])
            toast = count(log, "That party is full.") > 0
            # Starting the join means the loser PASSED the pre-flight - both saw a 3/4 party - so the
            # seat was refused by the session's own MaxPlayers: the race B25 is about.
            race = count(log, "Starting direct-join") > 0
            detail.update(winner=self.winner.name, loser=self.loser.name, full_toast=toast,
                          refused_by="session MaxPlayers (race exercised)" if race else "pre-flight (race NOT exercised)")
            ok = toast
        return ok, detail

    # ── kick ──────────────────────────────────────────────────────────────────
    def kick(self):
        B, W = self.B, self.winner
        console(B, f"party kick {W.name}")
        w = wait_party(W, is_solo, "the kicked player to stand in their own menu")
        b = wait_party(B, host_at(3), "the host to count 3")
        return True, {"kicked": w, "host": b}

    # ── leave ─────────────────────────────────────────────────────────────────
    def leave(self):
        B, D = self.B, self.D
        console(D, "party leave")
        d = wait_party(D, is_solo, "the leaver to stand in their own menu")
        b = wait_party(B, host_at(2), "the host to count 2")
        return True, {"left": d, "host": b}

    # ── T2 ────────────────────────────────────────────────────────────────────
    def t2_simultaneous_join_with_room(self):
        A, B, E = self.A, self.B, self.E
        for i in (A, E):
            wait_joinable(i, B)
        skew = run_together([A, E], f"party join {B.name}")
        b = wait_party(B, host_at(4), "the host to count 4", 180)
        states = {i.label: wait_party(i, lambda s: seated_in(b)(s) and s.get("members") == members(4),
                                      f"{i} to be seated with a full roster", 180) for i in (A, E)}
        return True, {"press_skew_ms": skew, "host": b, **states}

    # ── T5 (join) ─────────────────────────────────────────────────────────────
    def t5_double_join(self):
        B, D, E = self.B, self.D, self.E
        console(E, "party leave")
        wait_party(E, is_solo, "E to stand in their own menu")
        wait_party(B, host_at(3), "the host to count 3")
        wait_joinable(D, B)
        mark = D.log_mark()
        double_tap(D, f"party join {B.name}")
        b = wait_party(B, host_at(4), "the host to count 4", 180)
        wait_party(D, seated_in(b), "D to be seated", 180)
        log = D.log_since(mark)
        starts = count(log, "Starting direct-join")
        ran = count(log, f"[DiagnosticsHUD] party join {B.name} ")
        # The invariant is ONE join reaching UGS. Which layer stops the second press is timing: the
        # controller's _transitioning guard if it lands first, or the transition fade (which covers
        # the screen and blocks raycasts the moment the first join starts) if it lands after.
        stopped_by = ("controller (_transitioning)" if count(log, "Already transitioning")
                      else "transition overlay (the second press never ran)" if ran == 1 else "unknown")
        return starts == 1 and stopped_by != "unknown", {
            "direct_joins_started": starts, "presses_that_ran": ran, "second_tap_stopped_by": stopped_by, "host": b}

    # ── launch ────────────────────────────────────────────────────────────────
    def launch_match(self):
        crew = [self.B, self.C, self.A, self.D]
        self.B.do(f"arcade {MATCH_MODE}")
        self.B.wait(60)
        time.sleep(3)
        for i in crew:
            i.do("arcade start")
        scenes = until(lambda: (lambda sc: sc if all(v == MATCH_SCENE for v in sc.values()) else None)(
            {i.label: i.state().get("scene") for i in crew}), 240, f"all four in {MATCH_SCENE}", step=2)
        return True, {"scenes": scenes}

    # ── T4 / B20 ──────────────────────────────────────────────────────────────
    def t4_leaver_at_ready_gate(self):
        B, C, A, D = self.B, self.C, self.A, self.D
        mark = B.log_mark()
        for i in (B, C, A):
            # The HUD shows Ready only once this pilot's Player and vessel are up
            # (MiniGameHUD unlocks it); pressing the controller before that is not something a
            # person can do, and NREs on the still-null LocalPlayer. So: wait for the button, then
            # click the button itself, so its own wiring is part of what is tested.
            until(lambda: i.rect("ReadyButton"), 180, f"{i}'s Ready button to show")
            i.click("ReadyButton")
            i.wait(10)
        until(lambda: count(B.log_since(mark), "pressed Ready): 3/4"), 120, "the host to count three Ready presses")
        console(D, "party leave")
        until(lambda: count(B.log_since(mark), "All players ready - starting countdown"), 120,
              "the countdown to start without the leaver", step=1)
        gate = [l for l in B.log_since(mark) if "Ready gate" in l or "starting countdown" in l]
        d = wait_party(D, is_solo, "the leaver to stand in their own menu")
        return True, {"host_gate_log": [l.split("] ", 2)[-1] for l in gate], "leaver": d}

    # ── T3 / B21 ──────────────────────────────────────────────────────────────
    def t3_leaver_mid_match(self):
        B, A = self.B, self.A
        B.wait(600)
        before = vessels(B)
        mark = B.log_mark()
        console(A, "party leave")
        wait_party(A, is_solo, "the leaver to stand in their own menu")
        until(lambda: count(B.log_since(mark), f"handing '{A.name}' to the AI"), 120, "the host to hand the ship to the AI")
        B.wait(300)
        after1 = vessels(B)
        B.wait(300)
        after2 = vessels(B)
        flipped = [n for n, (owner, _) in before.items() if owner != 0 and n in after1 and after1[n][0] == 0]
        moving = [n for n in flipped if n in after2 and after1[n][1] != after2[n][1]]
        scores = score_rows(B)
        ok = len(flipped) == 1 and moving == flipped and A.name in scores
        return ok, {"ship_handed_to_ai": flipped, "still_flying": moving, "score_row": scores.get(A.name),
                    "vessels_before": len(before), "vessels_after": len(after2)}

    # ── T6 / spectator ────────────────────────────────────────────────────────
    def t6_spectator(self):
        B, E = self.B, self.E
        before = vessels(B)
        wait_joinable(E, B)
        console(E, f"party spectate {B.name}")
        e = wait_party(E, lambda s: s.get("spectating") == "1" and s.get("role") == "client"
                       and s.get("scene") == MATCH_SCENE, "the spectator to be watching", 240)
        b = wait_party(B, lambda s: s.get("spectators") == "1", "the host to count the spectator")
        after = vessels(B)
        ok = b.get("humans") == "2" and b.get("conns") == "3" and len(after) == len(before)
        return ok, {"spectator": e, "host": b, "vessels_before": len(before), "vessels_after": len(after)}

    # ── Block 1: the session record knows who it is and what happened ─────────
    def net_record(self):
        # The one moment all three roles exist at once: B hosts, C is a member, E spectates.
        roles = {}
        for i, want in ((self.B, "host"), (self.C, "client"), (self.E, "spectator")):
            line = console(i, "net") or ""
            fields = [f.strip() for f in line.split("|")]
            roles[i.label] = {"want": want, "got": fields[1] if len(fields) > 1 else None, "line": line}
        dumped = console(self.B, "net dump") or ""
        path = dumped.split("wrote ", 1)[1].strip() if "wrote " in dumped else None
        events = set()
        if path and os.path.exists(path):
            events = {e.get("e") for e in json.load(open(path)).get("lifecycle", [])}
        # What this run has put the host through by now: party transitions, approvals, leavers,
        # the ready gate passing (T4) and a ship handed to the AI (T3).
        need = {"party", "clientApproved", "clientLeft", "readyGate", "leaverToAI"}
        ok = all(r["got"] == r["want"] for r in roles.values()) and need <= events
        return ok, {"roles": roles, "host_record": path, "missing_events": sorted(need - events),
                    "host_events": sorted(e for e in events if e)}

    # ── T7 / B10 ──────────────────────────────────────────────────────────────
    def t7_host_drop(self):
        B, C, E = self.B, self.C, self.E
        t0 = time.time()
        os.kill(B.pid, signal.SIGKILL)
        out = {}
        for i in (C, E):
            s = wait_party(i, is_solo, f"{i} to stand in their own menu", 300)
            out[i.label] = {"wall_s": round(time.time() - t0, 1), "state": s}
        return True, out

    # ── T4-lobby / B20 (the launch lobby's gate) ──────────────────────────────
    def t4_leaver_at_launch_lobby(self):
        # Everyone is solo after T7. C hosts a new party of four, then one member walks out of
        # the arcade card lobby after the other three pressed Start.
        C, A, D, E = self.C, self.A, self.D, self.E
        for g in (A, D, E):
            sent, gsent = C.log_mark(), g.log_mark()
            console(C, f"party invite {g.name}")
            try:
                wait_party(g, lambda s: s.get("invite") == C.name, f"{g}'s invite")
                console(g, "party accept")
                wait_party(g, lambda s: s.get("role") == "client", f"{g} seated")
            except TimeoutError as e:
                # Defect 5 (2026-10-09 diagnosis): the INVITEE's Accept pre-flight refused a fresh
                # invite because the new host's partySession advertisement lagged the invite (B29,
                # the Accept case). The host's "RemoveExpired - 1 expired" that used to name this
                # failure is a symptom: an invite nobody accepted outlives its 60 s lifetime during
                # the 240 s wait. Read the invitee's log first.
                if count(g.log_since(gsent), "Join pre-flight refused (SessionChanged)"):
                    raise TimeoutError(
                        f"B29 (Accept): {g}'s pre-flight refused a fresh invite on {C}'s stale partySession "
                        "advertisement (Docs/PartySystem/BUGS.md B29). " + str(e)) from None
                if count(C.log_since(sent), "(reason: timeout)"):
                    raise TimeoutError(
                        f"{C}'s invite to {g} expired unaccepted - a symptom; read {g}'s log for why "
                        "the Accept did not seat it. " + str(e)) from None
                raise
        wait_party(C, host_at(4), "the new host to count 4")
        mark = C.log_mark()
        C.do(f"arcade {MATCH_MODE}")
        C.wait(60)
        time.sleep(3)
        for i in (C, A, D):
            i.do("arcade start")
            i.wait(10)
        time.sleep(3)
        console(E, "party leave")
        until(lambda: count(C.log_since(mark), "left) - launching game"), 120,
              "the lobby to launch without the leaver", step=1)
        scenes = until(lambda: (lambda sc: sc if all(v == MATCH_SCENE for v in sc.values()) else None)(
            {i.label: i.state().get("scene") for i in (C, A, D)}), 240, f"the three in {MATCH_SCENE}", step=2)
        gate = [l.split("] ", 2)[-1] for l in C.log_since(mark) if "launching game" in l]
        return True, {"host_gate_log": gate, "scenes": scenes}


SCENARIOS = [
    ("T1", "B2/T1", "Accept: guest seated, both rosters agree", Session.t1_accept),
    ("T5-accept", "T3 (single-flight)", "Double-tap Accept starts one accept flow", Session.t5_double_accept),
    ("T2b", "B25", "Two Joins at once on a 3/4 party: 4/4, loser told 'That party is full.'", Session.t2b_race_for_last_seat),
    ("kick", "-", "Host kicks a member: member back in their own menu", Session.kick),
    ("leave", "-", "Member leaves: back in their own menu, host recounts", Session.leave),
    ("T2", "B5/T2", "Two Joins at once with room for both: both seated", Session.t2_simultaneous_join_with_room),
    ("T5-join", "T3 (single-flight)", "Double-tap Join starts one direct join", Session.t5_double_join),
    ("launch", "-", f"Party of four launches {MATCH_MODE}", Session.launch_match),
    ("T4", "B20", "A leaver at the ready gate does not strand the other three", Session.t4_leaver_at_ready_gate),
    ("T3", "B21", "A leaver mid-match: ship flies on under AI, score row survives", Session.t3_leaver_mid_match),
    ("T6", "SPECTATOR §6", "Spectator: no Player/vessel, not a human, counted as a spectator", Session.t6_spectator),
    ("net", "Block 1", "Session record: each peer names its role; the host's timeline holds the lifecycle", Session.net_record),
    ("T7", "B10", "Host killed: each remaining peer lands in its own working menu", Session.t7_host_drop),
    ("T4-lobby", "B20", "A leaver at the arcade launch lobby does not strand the other three", Session.t4_leaver_at_launch_lobby),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--instances", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--repo", default=os.getcwd())
    args = ap.parse_args()

    spec = json.load(open(args.instances))
    insts = {k: Inst(k, v["port"], v["log"], v["pid"]) for k, v in spec.items()}
    head = subprocess.run(["git", "-C", args.repo, "rev-parse", "--short", "HEAD"], capture_output=True, text=True).stdout.strip()
    dirty = bool(subprocess.run(["git", "-C", args.repo, "status", "--porcelain", "--", "Assets/_Scripts"],
                                capture_output=True, text=True).stdout.strip())
    report = {"date": datetime.datetime.now().isoformat(timespec="seconds"), "commit": head,
              "uncommitted_scripts": dirty, "engine": "Prisma (Port/) - see README.md for what this does not prove",
              "scenarios": []}

    t0 = time.time()
    for k, inst in insts.items():
        boot_to_menu(inst)
    report["boot_wall_s"] = round(time.time() - t0, 1)
    # What each pilot's network was, as the pilot itself logged it at boot (transport, simulated line, relay).
    report["network"] = {k: [l for l in inst.log_lines() if l.startswith(("[net] transport:", "[netsim] starting", "[relay] sessions"))][:3]
                         for k, inst in insts.items()}

    session, failed = Session(insts), False
    for sid, ticket, title, fn in SCENARIOS:
        row = {"id": sid, "ticket": ticket, "title": title}
        if failed:
            row["result"] = "not run"
        else:
            t = time.time()
            try:
                ok, detail = fn(session)
                row.update(result="pass" if ok else "FAIL", detail=detail)
            except Exception as e:
                row.update(result="FAIL", error=f"{type(e).__name__}: {e}", trace=traceback.format_exc(limit=3))
            row["wall_s"] = round(time.time() - t, 1)
            failed = row["result"] != "pass"
        report["scenarios"].append(row)
        print(f"  {row['result']:8} {sid:10} {ticket:20} {title}" + (f"  ({row['wall_s']}s)" if "wall_s" in row else ""), flush=True)
        if row.get("error"):
            print(f"           {row['error']}", flush=True)

    with open(args.out, "w") as f:
        json.dump(report, f, indent=2)
    passed = sum(1 for r in report["scenarios"] if r["result"] == "pass")
    print(f"prisma_party_scenarios: {passed}/{len(SCENARIOS)} passed - {args.out}")
    return 0 if passed == len(SCENARIOS) else 1


if __name__ == "__main__":
    sys.exit(main())
