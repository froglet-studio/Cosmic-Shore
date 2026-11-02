#!/usr/bin/env python3
"""Run an automatic multiplayer test with built DEVELOPMENT players - no Editor, no MPPM.

One player hosts (-csMpAutoHost), the others join it (-csMpAutoJoin), each on its own UGS auth profile
(-csMpProfile), and each writes a result JSON (MultiplayerAutoPilot.cs). This script launches them, waits, and
prints one verdict line per player. Exit code 0 when every player passed.

    python3 Tools/Build/mp_auto.py run --player "C:/Builds/Dev/CosmicShore.exe"               # host + 1 joiner
    python3 Tools/Build/mp_auto.py run --player <exe> --players 4 --timeout 300
    python3 Tools/Build/mp_auto.py run --player <exe> --out C:/mp-run --extra "-csmloadinsights"
    python3 Tools/Build/mp_auto.py --self-test

Why built players: two MPPM Editors used ~24 GB with their import workers on the test PC and ran the clone at
1-10 fps (multiplayer test of 2026-11-02). A built player needs a fraction of that, so four fit on one machine.

Each run signs anonymous players in to the UGS project the build points at. Point the build at a development
environment, never the live one. The same profiles are reused between runs (mpauto-host, mpauto-join1..3), so a
run does not create new UGS players every time.

Logs and results land in --out (default: a fresh folder under the system temp directory); the folder is printed.
"""
import argparse
import json
import os
import shlex
import subprocess
import sys
import tempfile
import time

PARTY_MAX = 4  # a party is 4, never 6 (Docs/MULTIPLAYER_START_HERE.md section 2)


def profiles(players):
    """The auth profile of each player: the host first, then joiners."""
    return ["mpauto-host"] + [f"mpauto-join{i}" for i in range(1, players)]


def build_commands(player, players, out, timeout, extra, windowed=True):
    """The (name, argv, result path) of every player. The host waits for all of them."""
    rendezvous = os.path.join(out, "rendezvous.json")
    common = []
    if windowed:
        common += ["-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540"]
    common += ["-csMpTimeout", str(timeout), "-csMpQuit"]
    common += shlex.split(extra) if extra else []
    cmds = []
    for i, profile in enumerate(profiles(players)):
        name = "host" if i == 0 else f"join{i}"
        result = os.path.join(out, f"{name}.json")
        argv = [player, "-csMpProfile", profile]
        if i == 0:
            argv += ["-csMpAutoHost", rendezvous, "-csMpExpect", str(players)]
        else:
            argv += ["-csMpAutoJoin", rendezvous]
        argv += ["-csMpResult", result, "-logFile", os.path.join(out, f"{name}.log")] + common
        cmds.append((name, argv, result))
    return cmds


def read_result(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def verdict_line(name, result, exit_code):
    if result is None:
        return f"FAIL  {name}: no result file (exit code {exit_code})"
    state = "PASS" if result.get("ok") else "FAIL"
    tail = "" if result.get("ok") else f" - {result.get('reason')}"
    return f"{state}  {name}: {result.get('seconds', 0):.1f}s{tail}"


def run(args):
    if not os.path.isfile(args.player):
        print(f"no player at {args.player}", file=sys.stderr)
        return 2
    if not 2 <= args.players <= PARTY_MAX:
        print(f"--players must be 2-{PARTY_MAX}", file=sys.stderr)
        return 2
    out = args.out or tempfile.mkdtemp(prefix="cs-mp-auto-")
    os.makedirs(out, exist_ok=True)
    for stale in ("rendezvous.json",):
        try:
            os.remove(os.path.join(out, stale))
        except FileNotFoundError:
            pass

    cmds = build_commands(args.player, args.players, out, args.timeout, args.extra, windowed=not args.fullscreen)
    procs = []
    for i, (name, argv, result) in enumerate(cmds):
        try:
            os.remove(result)
        except FileNotFoundError:
            pass
        procs.append((name, subprocess.Popen(argv), result))
        if i == 0:
            time.sleep(args.stagger)  # let the host start writing its rendezvous before the joiners boot

    deadline = time.time() + args.timeout + 60
    for name, proc, _ in procs:
        try:
            proc.wait(timeout=max(1, deadline - time.time()))
        except subprocess.TimeoutExpired:
            proc.kill()

    ok = True
    for name, proc, result_path in procs:
        result = read_result(result_path)
        line = verdict_line(name, result, proc.returncode)
        ok &= line.startswith("PASS")
        print(line)
        if result and not result.get("ok"):
            for step in result.get("steps", []):
                print(f"        {step}")
    print(f"logs and results: {out}")
    return 0 if ok else 1


def self_test():
    cmds = build_commands("game.exe", 3, "out", 200, "-csmloadinsights")
    assert [c[0] for c in cmds] == ["host", "join1", "join2"], cmds
    host, join = cmds[0][1], cmds[1][1]
    assert host[host.index("-csMpProfile") + 1] == "mpauto-host"
    assert host[host.index("-csMpExpect") + 1] == "3"
    assert "-csMpAutoJoin" not in host and "-csMpAutoHost" in host
    assert join[join.index("-csMpProfile") + 1] == "mpauto-join1"
    assert join[join.index("-csMpAutoJoin") + 1] == host[host.index("-csMpAutoHost") + 1], "joiners read the host's file"
    assert "-csMpQuit" in join and "-csmloadinsights" in join
    assert len(set(p for p in profiles(4))) == 4, "every player needs its own UGS profile"
    assert verdict_line("host", {"ok": True, "seconds": 12.0}, 0).startswith("PASS")
    assert verdict_line("join1", {"ok": False, "reason": "x", "seconds": 1}, 1).startswith("FAIL")
    assert verdict_line("join1", None, 1).startswith("FAIL")
    print("mp_auto self-test: OK")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    r = sub.add_parser("run", help="launch the players and report")
    r.add_argument("--player", required=True, help="the built development player's executable")
    r.add_argument("--players", type=int, default=2, help="2-4, the host included (default 2)")
    r.add_argument("--timeout", type=int, default=240, help="seconds each player may take (default 240)")
    r.add_argument("--out", help="folder for logs and results (default: a new temp folder)")
    r.add_argument("--extra", default="", help="more arguments for every player, as one string")
    r.add_argument("--stagger", type=float, default=3.0, help="seconds between the host and the joiners starting")
    r.add_argument("--fullscreen", action="store_true", help="do not force 960x540 windows")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.cmd == "run":
        return run(args)
    ap.print_help()
    return 2


if __name__ == "__main__":
    sys.exit(main())
