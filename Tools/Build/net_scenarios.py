#!/usr/bin/env python3
"""net_scenarios.py - the owner's multiplayer hand tests as data, and a judge for recorded runs.

Rows 2-5 of the owner's table (Docs/MULTIPLAYER_START_HERE.md section 5.2) are the shape a machine runs: a few
players press a few things, a fault is injected, and the merged timeline that `Tools/Build/net_logs.py` builds from
every player's own net log (Unity's main editor and its MPPM virtual players; Amoebius's START ONLINE and + PLAYER 2)
either shows the marks a pass needs, in order, or it does not. This file holds each scenario as DATA - the steps each
player takes (a control-port command where one exists, a human step where none does, and it says which), the faults
to inject (by the `netsim` / `netfault` tokens Port/docs/MULTIPLAYER.md sections 6.2-6.3 document; where no token
reproduces the condition the entry says `fault: to name with Yash`), and the timeline marks that must appear, in
order, and the ones that must not - and a JUDGE that takes such a timeline and answers pass or fail with the first
unmet expectation.

    python3 Tools/Build/net_scenarios.py --list
    python3 Tools/Build/net_scenarios.py --plan host-drop-reinvite                  # steps + expectations, to run by hand today
    python3 Tools/Build/net_scenarios.py --plan leave-at-ready-gate --variant launch-lobby
    python3 Tools/Build/net_scenarios.py --judge double-join-full --timeline Logs/NetSessions   # a folder of net_*.json
    python3 Tools/Build/net_scenarios.py --judge host-drop-reinvite --timeline show.txt --cycles 1 --peer guest=p3
    python3 Tools/Build/net_scenarios.py --run guest-leaves-mid-match --live        # drives Amoebius players (UNVERIFIED)
    python3 Tools/Build/net_scenarios.py --marks                                    # the mark vocabulary and where it is written
    python3 Tools/Build/net_scenarios.py --self-test

`--timeline` takes a folder of `net_*.json` files (the newest test in it, or `--test <id>`; read through net_logs.py's
own parse / group / timeline, never a copy of them), one `net_*.json` file, or the text `net_logs.py show <test>
--lines` prints. Marks are judged from the timeline's `event` rows; the few console-line expectations say which log
level (and which verbose channel) they need.

A READER tool: it reads logs and writes nothing - except that `--run` keeps its players' logs and a results.json
under `--work` (a scratch folder, never the repo). Exit 0 pass, 1 fail, 2 bad input, 3 cannot run here (no built
player, no go-ahead for the live UGS project, or a step only a human can take).

WHAT A PASS PROVES. The shipped party / netcode C# took the right decisions, as its own session record says
(`NetSessionRecorder.Mark` in Assets/_Scripts; Amoebius compiles the same files and writes the same record,
Port/src/CosmicShore.Player/NetTestLog.cs). It does not prove the Unity runtime, UGS's real error shapes or how it
felt: those stay the owner's MPPM run and the live UGS test (START_HERE section 5.3). Where each mark is written and
what its detail looks like is read from the call sites (`--marks`); the five-process harness saw `party`,
`clientApproved`, `clientLeft`, `readyGate` and `leaverToAI` in a host's record on 2026-10-08
(Tools/Build/prisma_party_scenarios/README.md, row `net`). The ORDER of marks across players and the exact bounce
texts of a host loss are read from the code, not from a recorded run: the first real runs judge those (`--judge`
prints every row after an unmet anchor so a wrong guess is cheap to see).
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
import re
import signal
import subprocess
import sys
import tempfile
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import net_logs  # noqa: E402  - the one reader of net_*.json; its parse / group / timeline are reused, never copied


# ── the vocabulary: marks, states, tokens ───────────────────────────────────

PARTY_STATES = ["Disconnected", "InPresenceLobby", "Inviting", "JoiningParty", "HostingParty", "InParty", "Reconnecting"]

# Every lifecycle mark the game writes (grep 'NetSessionRecorder.Mark(' Assets/_Scripts, 2026-11-07), where, and the
# shape of its detail. A scenario may only name marks from this table (the self-test holds that).
MARKS = {
    "party": ("Controller/Party/StateMachine/PartyStateMachine.cs TryTransition",
              "<From>-><To> over " + ", ".join(PARTY_STATES) + "; a seat is JoiningParty->InParty, an invite InParty->Inviting"),
    "clientApproved": ("Controller/Multiplayer/MultiplayerSetup.cs, the host's approval callback",
                       "<clientId> | <clientId> spectator | <clientId> rejoin. Client 0 is the host itself (marked in solo play too)"),
    "clientLeft": ("Controller/Multiplayer/MultiplayerSetup.cs OnClientDisconnect (host only)", "<clientId>"),
    "rejoinRefused": ("Controller/Multiplayer/MultiplayerSetup.cs", "<clientId> whose rejoin found no seat"),
    "relayDrop": ("Controller/Multiplayer/MultiplayerSetup.cs OnTransportFailure", "the recent frame freeze it followed, if any (B36)"),
    "readyGate": ("Controller/Arcade/MultiplayerMiniGameControllerBase.cs EvaluateReadyGate, marked only when the gate PASSES",
                  "<ControllerType>: <name> pressed Ready | <ControllerType>: client <id> disconnected"),
    "leaverToAI": ("Controller/Multiplayer/ServerPlayerVesselInitializer.cs ConvertPlayerToAI (host only)", "<display name>"),
    "seatReclaimed": ("Controller/Multiplayer/ServerPlayerVesselInitializer.cs TryReclaimSeat (host only)", "<display name>"),
    "refused": ("Controller/Party/PartyInviteController.cs PreflightOrToast (nothing torn down)",
                "the JoinTargetValidator message: <name>'s party is no longer available. | <name>'s party is full. | hosting a party with others"),
    "bounce": ("Controller/Party/PartyInviteController.cs BounceToSoloMenuAsync",
               "the toast: That party is full. | Host disconnected | Connection lost | "
               "Couldn't follow the party - returned to your menu. | That party has ended."),
    "stall": ("Utility/FrameStallWatch.cs", "<seconds>s in <scene>"),
    "stallContext": ("Utility/FrameStallWatch.cs", "what was loading"),
    "netConfig": ("Controller/Multiplayer/MultiplayerSetup.cs", "hash=<hex> prefabs=<n> <key>:<name> ... (B35)"),
    "manual": ("Utility/NetSessionConsoleCommand.cs", "whatever the person typed after `net mark`"),
    "offlineChosen": ("System/OfflineModeService.cs", ""),
    "offlineFallback": ("Utility/NetSessionRecorder.cs MarkOfflineFallback", "deviceWasOnline | deviceOffline"),
}

# Console lines a scenario reads beside the marks: (where, log level, channel). A verbose line is in an Amoebius
# `--verbose` log; in Unity only when its channel is ON (FrogletTools > Toolbox > Logging).
CONSOLE_LINES = {
    "launching game": ("Controller/Multiplayer/ArcadeConfigSyncManager.cs EvaluateLobbyReadyGate: "
                       "[ArcadeConfigSync] All players ready (<because>) - launching game", "log (verbose, ArcadeMatch)"),
    "Bouncing to solo menu": ("PartyInviteController.BounceToSoloMenuAsync: [PartyInviteController] Bouncing to solo menu: <toast>", "warning"),
    "Join pre-flight refused": ("HostConnectionService.TryValidateJoinTarget: [HostConnectionService] Join pre-flight refused (<verdict>) ...", "warning"),
    "Accept flow completed successfully.": ("PartyInviteController (TESTS.md S1's pass line)", "log (verbose, Party)"),
    "Leave-lobby flow completed.": ("PartyInviteController (TESTS.md S3's pass line)", "log (verbose, Party)"),
}

# The documented fault tokens (Port/docs/MULTIPLAYER.md 6.2 and 6.3; NetSimulator.cs, NetFaults.cs).
NETSIM_TOKENS = {"off", "down", "up", "lan", "broadband", "dsl", "4g", "3g", "poor", "latency", "jitter", "loss", "bandwidth"}
NETFAULT_TOKENS = {"off", "full", "ratelimit", "relayfail", "down", "up", "slow"}

SEAT = r"^[1-9]\d*$"   # a NEW human seat: not client 0 (the host itself), not "N spectator", not "N rejoin"
HOST_LOSS = ["Host disconnected", "Connection lost"]   # MultiplayerSetup's reasons for HandleHostLossAsync -> the bounce toast
MATCH_MODE, MATCH_SCENE = "Bloomrush", "MinigameBloomrush"
UNVERIFIED = "UNVERIFIED: written without a .NET SDK, never run against a built player"


# ── scenario building blocks ────────────────────────────────────────────────

def must(peer, e=None, say="", detail=None, re_=None, kind="event", within_s=None):
    """This row must appear after the previous `must` (in order). `peer` is a literal peer name, `*` for any, or a
    `$role` that binds to the first matching peer and holds from then on. `detail` is a substring (or any of a list)
    of the mark's detail - for a console row, of the line; `re_` a regex on the same text."""
    d = {"op": "must", "peer": peer, "kind": kind, "say": say}
    if e:
        d["e"] = e
    if detail is not None:
        d["detail"] = detail
    if re_:
        d["re"] = re_
    if within_s:
        d["within_s"] = within_s
    return d


def never(peer, e=None, say="", detail=None, re_=None, kind="event"):
    """No such row between the previous `must` and the next one (or the end). An unbound `$role` means any peer."""
    d = must(peer, e, say, detail, re_, kind)
    d["op"] = "never"
    return d


def count(peer, e, n, say="", detail=None, re_=None, kind="event"):
    """Exactly n such rows in the same window as `never`."""
    d = must(peer, e, say, detail, re_, kind)
    d.update(op="count", n=n)
    return d


def must_all(say, *entries):
    """These rows must all appear after the previous `must`, in any order among themselves."""
    return {"op": "must_all", "say": say, "entries": list(entries)}


def step(who, say, **machine):
    """One thing a player does. `machine` is its control-port form when one exists: console=<DiagnosticsHUD line>
    (`do console <line>` runs the registered command, Port/src/CosmicShore.Player/Inspector.cs HudCommand),
    do=<port verb>, together=<line pressed by several players behind a barrier>, ready=True (the HUD's Ready
    button), kill=True, restart=True, and the waits (wait_party, wait_scene, wait_log, wait_frames, wait_joinable).
    A step with none of those is a human step. {role} in a line is that player's display name."""
    return {"who": who, "say": say, **machine}


def fault(what, how, netsim=None, netfault=None, player=None, optional=False, fault="to name with Yash"):
    """A fault to inject. `netsim` / `netfault` carry a documented token when one does the job; `fault` says who
    confirms the rest. Never a code TODO: the open question lives in the data a person reads."""
    return {"what": what, "how": how, "netsim": netsim, "netfault": netfault, "player": player,
            "optional": optional, "fault": fault}


# ── the scenarios: rows 2-5 of the owner's table ────────────────────────────

def _fill_party(host, members):
    """Host invites each member through the DiagnosticsHUD console; each accepts; the host counts them."""
    out = []
    for i, m in enumerate(members, 2):
        out.append(step(host, f"invites {{{m}}}", console=f"party invite {{{m}}}"))
        out.append(step(m, "accepts (the popup; if it missed the invite, the friends panel's Requests row - the port's "
                           "`do party accept` finds it there)", wait_party=f"invite:{host}", console="party accept"))
        out.append(step(host, f"now counts {i}/4 (console `party`: members={i}/4 conns={i})", wait_party=f"host_at:{i}"))
    return out


def _launch(host, crew):
    """Open a card, everyone presses START GAME, everyone lands in the match scene."""
    return [
        step(host, f"opens the {MATCH_MODE} card (Arcade grid; the port verb opens its configure modal)", do=f"arcade {MATCH_MODE}", wait_frames=60),
        step(crew, "each press START GAME on the card", do="arcade start"),
        step(crew, f"all land in {MATCH_SCENE}", wait_scene=MATCH_SCENE),
    ]


# To confirm with Yash (2026-11-07). None of this has been seen in a recorded net log: it is read from the C# call sites.
# net_logs.py itself names only party, clientApproved, clientLeft, readyGate, stall, relayDrop and netConfig (its fixtures
# and its stall / B35 handling); Docs/Multiplayer/TESTS.md names no mark at all. So, to confirm:
#   - the verbs bounce, refused, leaverToAI, seatReclaimed, rejoinRefused, stallContext, manual, offlineChosen and
#     offlineFallback (MARKS above: each is one NetSessionRecorder.Mark(...) site, never yet read back from a log);
#   - the detail texts a scenario matches: HOST_LOSS ("Host disconnected" / "Connection lost", the reason
#     HandleHostLossAsync hands BounceToSoloMenuAsync), readyGate's "client <id> disconnected", the bounce toasts
#     "That party is full." / "That party has ended." (serialized fields on PartyInviteController; the defaults are
#     matched) and JoinTargetValidator's "...'s party is no longer available." / "...'s party is full.";
#   - the 30 s "at once" bound in host-drop-reinvite, the cross-player order the must_all groups avoid, `netfault
#     slow=1500` as the B29 widener, and that no token drops ONE player and keeps its log (the `fault` entries).

SCENARIOS = {
    "host-drop-reinvite": {
        "row": 2,
        "title": "Host drops; the new host invites at once; the guest is seated (repeat 5x)",
        "expect_in_words": "The Accept seats the guest; never 'party is no longer available'",
        "bugs": ["B29 (Session case, defect 5)", "B10"],
        "checklist": "Docs/UNITY_VERIFICATION_CHECKLIST.md 'B29 Session case (defect 5)'",
        "players": [("host", "P1: hosts the party of three, then drops"),
                    ("newhost", "P2: a member; after the drop it lands in its own menu and invites at once"),
                    ("guest", "P3: a member; after the drop it accepts the new host's invite")],
        "repeat": 5,
        "rebind_each_cycle": True,   # by hand the roles rotate between repeats; each cycle finds its own new host
        "steps": _fill_party("host", ["newhost", "guest"]) + [
            step("host", "DROPS: kill its process (harness: kill -9; Unity: stop the virtual player). No control-port "
                         "command drops a player; a dropped host writes no net log, so only the survivors are judged",
                 kill=True),
            step("newhost", "lands in its own menu (console `party`: role=host members=1/4 scene=Menu_Main) and AT "
                            "ONCE invites the guest - do not wait for the guest's online row to settle: the race "
                            "window is one presence refresh interval",
                 wait_party="solo", console="party invite {guest}"),
            step("guest", "accepts the new host's invite", wait_party="invite:newhost", console="party accept"),
            step("guest", "is seated: console `party` shows role=client and the new host's session; no toast",
                 wait_party="seated"),
        ],
        "cycle_reset": [
            step("guest", "leaves, so the next repeat starts from three solo players", console="party leave", wait_party="solo"),
            step("host", "is started again (harness: relaunch its process on the same profile; Unity: start the virtual "
                         "player again) and hosts the next repeat", restart=True),
        ],
        "faults": [
            fault("the host drop", "kill the host's process; there is no token that drops ONE player and keeps its log "
                  "(`do netsim down` on the host pulls its cable - after 10 s every peer disconnects from it - but the "
                  "host process itself survives, which is not what the row tests)", player="host"),
            fault("widen the B29 window", "optional: `do netfault slow=1500` on the guest before the drop - every "
                  "session-service call on it waits 1.5 s, so its presence read of the new host lags the invite "
                  "(inferred from the token's documentation, not measured)",
                  netfault="slow=1500", player="guest", optional=True),
        ],
        "expect": [
            must("*", "bounce", "a surviving member bounces to its own menu when the host drops (B10)", detail=HOST_LOSS),
            must("$newhost", "party", "the new host sends its invite at once (later than 30 s misses the B29 window and proves nothing)",
                 detail="->Inviting", within_s=30),
            never("$guest", "refused", "the guest's Accept pre-flight must not refuse on the new host's stale partySession "
                  "(B29 Session case: the invite and the partySession it names go out in one save)", detail="no longer available"),
            never("*", "bounce", "nobody is bounced with 'no longer available'", detail="no longer available"),
            must("$guest", "party", "the guest's Accept starts (it leaves its own solo session)", detail="InParty->JoiningParty"),
            must_all("the new host seats the guest",
                     must("$newhost", "clientApproved", "the new host approves a new client", re_=SEAT),
                     must("$guest", "party", "the guest is in the new host's party", detail="JoiningParty->InParty")),
            never("$guest", "bounce", "the seat holds: no full / ended / unavailable bounce after it",
                  detail=["party is full", "no longer available", "has ended"]),
        ],
        "checks": ["By eye (Unity) or the console `party` line: the guest's role=client, the new host's members=2/4."],
        "quit_order": ["guest", "newhost", "host"],
    },

    "double-join-full": {
        "row": 3,
        "title": "Two players press Join on a 3/4 party together",
        "expect_in_words": "4/4; the loser sees 'That party is full.'",
        "bugs": ["B25", "B29 (Count case, open)"],
        "checklist": "Docs/UNITY_VERIFICATION_CHECKLIST.md '`party` console command, the five-process party scenarios...' step 4 (B25), "
                     "and 'Party request discipline' step 8",
        "players": [("host", "hosts the party at 3/4"),
                    ("member1", "seated first"), ("member2", "seated second"),
                    ("joinera", "presses Join on the host's online row"), ("joinerb", "presses Join at the same moment")],
        "repeat": 1,
        "steps": _fill_party("host", ["member1", "member2"]) + [
            step(["joinera", "joinerb"], "each wait until their online row for the host reads 3/4 (console `party online`: "
                                         "'{host}(joinable 3/4)'). Polled presence lags; pressing on a stale row tests "
                                         "the lag (B29 count case), not the race", wait_joinable="host"),
            step(["joinera", "joinerb"], "press Join on the host's row at the same moment (harness: the two console lines behind one "
                                         "barrier, skew < 1 ms; by hand: two people on a count of three)", together="party join {host}"),
            step("host", "counts 4/4 (console `party`: members=4/4 conns=4)", wait_party="host_at:4"),
            step(["joinera", "joinerb"], "one is seated (role=client in the host's session); the other is back in its own "
                                         "menu with the toast 'That party is full.' - a WARNING in its console, never a red error",
                 wait_party="settled"),
        ],
        "faults": [
            fault("a refused join without the race", "optional: `do netfault full` on ONE joiner before its Join - its next "
                  "join finds the session full and it bounces with the game's 'That party is full.' (measured 2026-10-08, "
                  "Port/docs/MULTIPLAYER.md section 7 step 3). The race itself needs no fault: the session's own 4 seats "
                  "refuse the fifth (B25)", netfault="full", player="one joiner", optional=True, fault="documented"),
        ],
        "expect": [
            must("$host", "clientApproved", "member 1 is seated (2/4)", re_=SEAT),
            must("$host", "clientApproved", "member 2 is seated (3/4)", re_=SEAT),
            count("$host", "clientApproved", 1, "exactly one more seat after 3/4: the party is 4/4 and never seats a fifth (B25)", re_=SEAT),
            never("*", "refused", "no pre-flight refusal on a stale count - that is B29's count case, and it means the race was "
                  "not exercised (the loser must be refused by the session's seats, not by its own pre-flight)", detail="party is full"),
            count("*", "bounce", 1, "the loser is bounced to its own menu with the toast", detail="That party is full."),
            never("*", "bounce", "nobody is bounced with 'no longer available'", detail="no longer available"),
        ],
        "checks": ["The loser's console: '[PartyInviteController] Bouncing to solo menu: That party is full.' as a WARNING and NO red "
                   "'[HostConnectionService] JoinPartyDirect error' (checklist step 4).",
                   "Every online row and the lobby read x/4, never x/6 (B25)."],
        "quit_order": ["joinera", "joinerb", "member1", "member2", "host"],
    },

    "leave-at-ready-gate": {
        "row": 4,
        "title": "A member leaves at the ready gate (the match's GO) - or at the arcade launch lobby (--variant launch-lobby)",
        "expect_in_words": "The other three still launch",
        "bugs": ["B20"],
        "checklist": "Docs/UNITY_VERIFICATION_CHECKLIST.md '`party` console command, the five-process party scenarios...' step 3 (B20 lobby); "
                     "its match half is T4 in Tools/Build/prisma_party_scenarios/README.md",
        "players": [("host", "hosts the party of four and the match"),
                    ("member1", "presses Ready"), ("member2", "presses Ready"),
                    ("leaver", "leaves instead of pressing Ready")],
        "repeat": 1,
        "steps": _fill_party("host", ["member1", "member2", "leaver"]) + _launch("host", ["host", "member1", "member2", "leaver"]) + [
            step(["host", "member1", "member2"], "press the HUD's GO (ReadyButton) once it shows - the host's gate reads 3/4 and holds",
                 ready=True),
            step("leaver", "leaves the party from the match (console `party leave`; by hand the pause menu's Leave)", console="party leave"),
            step("host", "re-decides the gate on the leaver's disconnect and starts the countdown for the three",
                 wait_log="All players ready - starting countdown"),
        ],
        "faults": [
            fault("a hard drop instead of a leave", "optional: `do netsim down` on the leaver - the host sees it disconnect after "
                  "~10 s (UTP's timeout), through the same disconnect callback the gate re-decides on",
                  netsim="down", player="leaver", optional=True, fault="documented"),
        ],
        "expect": [
            must("$host", "clientApproved", "member 1 is seated", re_=SEAT),
            must("$host", "clientApproved", "member 2 is seated", re_=SEAT),
            must("$host", "clientApproved", "the leaver is seated (4/4)", re_=SEAT),
            must_all("the leaver's disconnect re-decides the gate and it PASSES for the three (B20, match half)",
                     must("$host", "clientLeft", "the host sees the leaver go"),
                     must("$host", "readyGate", "the gate passes because of the disconnect, not a fourth press", detail="disconnected")),
            never("*", "bounce", "nobody else is thrown out of the match"),
        ],
        "checks": ["The three see the countdown and play; the leaver stands in its own menu (console `party`: role=host members=1/4)."],
        "variants": {
            "launch-lobby": {
                "title": "A member leaves at the arcade launch lobby after the other three pressed Start",
                "steps": _fill_party("host", ["member1", "member2", "leaver"]) + [
                    step("host", f"opens the {MATCH_MODE} card", do=f"arcade {MATCH_MODE}", wait_frames=60),
                    step(["host", "member1", "member2"], "press START GAME; the lobby waits for the fourth", do="arcade start"),
                    step("leaver", "leaves without pressing Start", console="party leave"),
                    step("host", "launches the three: '[ArcadeConfigSync] All players ready (client N left) - launching game'",
                         wait_log="left) - launching game"),
                    step(["host", "member1", "member2"], f"land in {MATCH_SCENE}", wait_scene=MATCH_SCENE),
                ],
                "expect": [
                    must("$host", "clientApproved", "member 1 is seated", re_=SEAT),
                    must("$host", "clientApproved", "member 2 is seated", re_=SEAT),
                    must("$host", "clientApproved", "the leaver is seated (4/4)", re_=SEAT),
                    must_all("the lobby gate launches the three without the leaver (B20, lobby half). The lobby writes no mark: "
                             "this is the verbose ArcadeMatch line, so it needs an Amoebius --verbose log or the channel ON in Unity",
                             must("$host", "clientLeft", "the host sees the leaver go"),
                             must("$host", kind="log", say="the lobby's own launch line", detail="launching game")),
                    never("*", "bounce", "nobody else is thrown out"),
                ],
            },
        },
        "quit_order": ["leaver", "member1", "member2", "host"],
    },

    "guest-leaves-mid-match": {
        "row": 5,
        "title": "A guest leaves mid-match",
        "expect_in_words": "Its ship flies on under AI; its score row stays",
        "bugs": ["B21"],
        "checklist": "Docs/UNITY_VERIFICATION_CHECKLIST.md '`party` console command, the five-process party scenarios...' (T3, B21); "
                     "Docs/Multiplayer/TEST_CHECKLIST.md section 5 (rejoin: seatReclaimed)",
        "players": [("host", "hosts the party of four and the match"),
                    ("member1", "plays"), ("member2", "plays"),
                    ("leaver", "leaves once the match is running")],
        "repeat": 1,
        "steps": _fill_party("host", ["member1", "member2", "leaver"]) + _launch("host", ["host", "member1", "member2", "leaver"]) + [
            step(["host", "member1", "member2", "leaver"], "all press the HUD's GO; the countdown starts", ready=True),
            step("host", "the match runs for a while (harness: 600 frames at --realtime; by hand: 10 s of flying)", wait_frames=600),
            step("host", "notes the leaver's ship: owner and position (port verb `vessels`; by hand, watch it)", do="vessels"),
            step("leaver", "leaves mid-match (console `party leave`; by hand the pause menu's Leave)", console="party leave"),
            step("host", "hands the ship to the AI: '[ServerVesselInit] Client N left mid-match - handing ... to the AI'",
                 wait_log="to the AI so the ship stays in the arena"),
            step("host", "lets the match run on a little", wait_frames=300),
            step("host", "the same ship is now owned by the server (owner=0) and has MOVED (port verb `vessels`; by hand, it keeps flying)",
                 do="vessels"),
            step("host", "the leaver's score row is still on the scoreboard (port verb `score`; by hand, the scoreboard)", do="score"),
        ],
        "faults": [
            fault("a hard drop instead of a leave", "optional: `do netsim down` on the leaver - the host disconnects it after "
                  "~10 s through the same callback the AI takeover runs on",
                  netsim="down", player="leaver", optional=True, fault="documented"),
        ],
        "expect": [
            must("$host", "clientApproved", "member 1 is seated", re_=SEAT),
            must("$host", "clientApproved", "member 2 is seated", re_=SEAT),
            must("$host", "clientApproved", "the leaver is seated (4/4)", re_=SEAT),
            must("$host", "readyGate", "every human pressed GO: the match started"),
            must_all("the host hands the leaver's ship to the AI instead of removing it (B21)",
                     must("$host", "clientLeft", "the host sees the leaver go"),
                     must("$host", "leaverToAI", "the ship is handed to the AI, its score kept")),
            never("*", "bounce", "the match goes on for the other three"),
            never("$host", "seatReclaimed", "nobody rejoined into that seat during this test (a rejoin is section 103's own test)"),
        ],
        "checks": ["NOT in the timeline (no mark says it): the ship FLIES ON - the port verb `vessels` on the host shows the "
                   "leaver's net object with owner=0 and a position that keeps changing; by hand, the ship keeps moving.",
                   "NOT in the timeline: the SCORE ROW STAYS - the port verb `score` on the host still lists the leaver; by hand, "
                   "the scoreboard keeps its row."],
        "quit_order": ["leaver", "member1", "member2", "host"],
    },
}


def resolve(scenario, variant=None):
    """The scenario with a variant's overrides applied (title, steps, expect, checks, faults)."""
    spec = dict(scenario)
    if variant:
        variants = scenario.get("variants") or {}
        if variant not in variants:
            raise KeyError(f"no variant '{variant}' (" + (", ".join(sorted(variants)) or "none") + ")")
        spec.update(variants[variant])
    return spec


# ── the judge ───────────────────────────────────────────────────────────────

_SHOW_LINE = re.compile(r"^\s*(\+\s*-?[\d.]+s)\s+(\S+)\s+(event|log|warning|error|exception|assert)\s*(.*)$")


def seconds(when):
    """'+   12.3s' -> 12.3 (the first column net_logs.timeline writes)."""
    return float(str(when).strip().lstrip("+").rstrip("s").strip())


def _alias(name):
    return name if name.startswith("$") or name == "*" else "$" + name


def _peer_ok(entry, peer, bindings):
    want = entry["peer"]
    if want == "*":
        return True
    if want.startswith("$"):
        bound = bindings.get(want)
        return bound is None or bound == peer
    return want == peer


def _text_ok(entry, text):
    want = entry.get("detail")
    if want is not None:
        alts = [want] if isinstance(want, str) else list(want)
        if not any(a in text for a in alts):
            return False
    pat = entry.get("re")
    return not pat or re.search(pat, text) is not None


def match(row, entry, bindings):
    """Does one timeline row (when, peer, kind, text) satisfy one expectation under these role bindings?"""
    _, peer, kind, text = row
    if entry.get("kind", "event") != kind or not _peer_ok(entry, peer, bindings):
        return False
    if kind == "event":
        mark, _, detail = text.partition(" ")
        return mark == entry.get("e") and _text_ok(entry, detail)
    return _text_ok(entry, text)


def describe(entry):
    want = entry.get("detail")
    if isinstance(want, list):
        want = " | ".join(want)
    bits = [entry["peer"], entry.get("kind", "event"), entry.get("e") or ""]
    if want:
        bits.append(f"'{want}'")
    if entry.get("re"):
        bits.append(f"/{entry['re']}/")
    return " ".join(b for b in bits if b)


def _find(rows, entry, bindings, start, skip=()):
    for i in range(start, len(rows)):
        if i not in skip and match(rows[i], entry, bindings):
            return i
    return None


def _bind(entry, row, bindings):
    want = entry["peer"]
    if want.startswith("$") and want not in bindings:
        bindings[want] = row[1]


class Verdict:
    def __init__(self):
        self.ok = True
        self.lines = []        # one per expectation, in plan order
        self.reason = None     # the first unmet expectation
        self.anchor = None     # the row index the first unmet `must` searched from
        self.bindings = {}

    def fail(self, why, anchor=None):
        self.ok = False
        if self.reason is None:
            self.reason, self.anchor = why, anchor
        self.lines.append("FAIL  " + why)


def judge(rows, scenario, cycles=None, peers=None, variant=None):
    """Pass or fail for a merged timeline (net_logs.timeline rows, or `show` lines parsed back), with the first unmet
    expectation. `cycles` overrides the scenario's repeat count; `peers` pre-binds roles ({'guest': 'p3'})."""
    spec = resolve(scenario, variant)
    cycles = cycles or spec.get("repeat", 1)
    bindings = {_alias(k): v for k, v in (peers or {}).items()}
    fixed = set(bindings)
    v = Verdict()
    n = len(rows)
    pending = []               # (label, entry, prev_pos, bindings as they were)
    pos, prev_say, prev_t = -1, "the start", None

    def window_text(lo, hi):
        a = seconds(rows[lo][0]) if 0 <= lo < n else 0.0
        b = seconds(rows[hi][0]) if 0 <= hi < n else (seconds(rows[-1][0]) if n else 0.0)
        return f"+{a:.1f}s..+{b:.1f}s" + ("" if hi < n else " (end)")

    def flush(hi):
        for label, entry, lo, snap in pending:
            hits = [i for i in range(lo + 1, hi) if match(rows[i], entry, snap)]
            tag = f"{label:<8} " if label else ""
            if entry["op"] == "never":
                if hits:
                    r = rows[hits[0]]
                    v.fail(f"{tag}{entry['say']}: forbidden {describe(entry)} at {r[0].strip()} on {r[1]}: {r[3]}")
                else:
                    v.lines.append(f"ok    {tag}window {window_text(lo, hi):<22} none of {describe(entry)}  | {entry['say']}")
            else:
                if len(hits) != entry["n"]:
                    v.fail(f"{tag}{entry['say']}: expected {entry['n']} x {describe(entry)} in {window_text(lo, hi)}, found {len(hits)}")
                else:
                    v.lines.append(f"ok    {tag}window {window_text(lo, hi):<22} {entry['n']} x {describe(entry)}  | {entry['say']}")
        pending.clear()

    for c in range(1, cycles + 1):
        if c > 1 and spec.get("rebind_each_cycle"):
            bindings = {a: p for a, p in bindings.items() if a in fixed}
        label = f"cycle {c}" if cycles > 1 else ""
        tag = f"{label:<8} " if label else ""
        for entry in spec["expect"]:
            op = entry["op"]
            if op in ("never", "count"):
                pending.append((label, entry, pos, dict(bindings)))
                continue
            subs = entry["entries"] if op == "must_all" else [entry]
            used, hit_rows = set(), []
            for sub in subs:
                i = _find(rows, sub, bindings, pos + 1, used)
                if i is None:
                    flush(n)
                    say = f"{entry['say']} - {sub['say']}" if op == "must_all" else sub["say"]
                    v.fail(f"{tag}{say}: no {describe(sub)} after {prev_say}"
                           + (f" (+{prev_t:.1f}s)" if prev_t is not None else ""), anchor=pos)
                    v.bindings = bindings
                    return v
                used.add(i)
                _bind(sub, rows[i], bindings)
                hit_rows.append(i)
            last = max(hit_rows)
            t = seconds(rows[last][0])
            flush(last)
            for sub, i in zip(subs, hit_rows):
                r = rows[i]
                v.lines.append(f"ok    {tag}{r[0].strip():>9} {r[1]:<10} {r[3][:60]:<60}  | {sub['say']}")
            if entry.get("within_s") and prev_t is not None and t - prev_t > entry["within_s"]:
                v.fail(f"{tag}{entry['say']}: came {t - prev_t:.0f}s after {prev_say}, later than {entry['within_s']}s", anchor=pos)
                v.bindings = bindings
                return v
            pos, prev_say, prev_t = last, entry["say"], t
    flush(n)
    v.bindings = bindings
    return v


# ── timeline inputs ─────────────────────────────────────────────────────────

def parse_show(text):
    """Rows back from what `net_logs.py show <test> [--lines]` printed."""
    rows = []
    for line in text.splitlines():
        m = _SHOW_LINE.match(line)
        if m:
            rows.append((m.group(1), m.group(2), m.group(3), m.group(4).strip()))
    return rows


def rows_from(path, test_id=None):
    """(rows, description) from a folder of net_*.json, one net_*.json, or `show` text."""
    if os.path.isdir(path):
        logs = net_logs.from_dirs([path])
        if not logs:
            raise ValueError(f"no net_*.json under {path}")
        tests = net_logs.group(logs)
        pick = tests[0] if not test_id else next((t for t in tests if t["id"] == test_id or t["id"].endswith(test_id)), None)
        if pick is None:
            raise ValueError(f"no test '{test_id}' under {path} ({len(tests)} found)")
        peers = ", ".join(net_logs.peer_name(pick, p) for p in pick["peers"])
        return net_logs.timeline(pick, all_lines=True), f"test {pick['id']} ({peers}) from {path}"
    with open(path, encoding="utf-8", errors="replace") as f:
        text = f.read()
    log = net_logs.parse(text, source=path, user=None)
    if log is not None:
        log["branches"] = []
        t = net_logs.group([log])[0]
        return net_logs.timeline(t, all_lines=True), f"one player ({net_logs.peer_name(t, log)}) from {path}"
    rows = parse_show(text)
    if not rows:
        raise ValueError(f"{path} is neither a folder of net logs, a net_*.json, nor `net_logs.py show` output")
    return rows, f"{len(rows)} timeline rows from {path}"


# ── plans for people ────────────────────────────────────────────────────────

def _names(spec):
    return {role: f"<{role}>" for role, _ in spec["players"]}


def _fill(text, names):
    for role, name in names.items():
        text = text.replace("{" + role + "}", name)
    return text


def step_lines(st, names):
    """The human sentence, then the machine form(s) or 'human step'."""
    who = st["who"]
    who = ", ".join(f"<{w}>" for w in who) if isinstance(who, list) else f"<{who}>"
    out = [f"{who}: {_fill(st['say'], names)}"]
    forms = []
    if "wait_party" in st:
        w = st["wait_party"]
        what = {"solo": "role=host members=1/4 scene=Menu_Main", "seated": "role=client", "settled": "seated or solo"}.get(w)
        if w.startswith("host_at:"):
            what = f"members={w[8:]}/4 conns={w[8:]}"
        elif w.startswith("invite:"):
            what = f"invite=<{w[7:]}>"
        forms.append(f"wait: `do console party` until {what}")
    if "wait_joinable" in st:
        forms.append(f"wait: `do console party online` shows '<{st['wait_joinable']}>(joinable N/4)' with the host's real count")
    if "wait_scene" in st:
        forms.append(f"wait: `state` reports scene {st['wait_scene']}")
    if "wait_log" in st:
        forms.append(f"wait: `logs` shows '{st['wait_log']}'")
    if "console" in st:
        forms.append(f"`do console {_fill(st['console'], names)}`")
    if "together" in st:
        forms.append(f"`do console {_fill(st['together'], names)}` on each, pressed behind one barrier")
    if "do" in st:
        forms.append(f"`do {_fill(st['do'], names)}`")
    if st.get("ready"):
        forms.append("wait for ReadyButton (dump_ui), click it - or `do arcade ready`")
    if st.get("kill"):
        forms.append("harness: kill -9 its pid / Unity: stop the virtual player (no control-port command)")
    if st.get("restart"):
        forms.append("harness: relaunch its process, same profile / Unity: start the virtual player again")
    if "wait_frames" in st:
        forms.append(("then " if forms else "") + f"`wait {st['wait_frames']}`")
    out.append("      " + ("control port: " + "; ".join(forms) if forms else "human step (no control-port form)"))
    return out


def expect_lines(spec):
    out = []
    for e in spec["expect"]:
        if e["op"] == "must_all":
            out.append(f"  MUST (any order)  {e['say']}")
            for sub in e["entries"]:
                out.append(f"      {describe(sub):<58} {sub['say']}")
        elif e["op"] == "must":
            late = f" within {e['within_s']}s" if e.get("within_s") else ""
            out.append(f"  MUST{late:<16} {describe(e):<58} {e['say']}")
        elif e["op"] == "never":
            out.append(f"  NEVER (window)    {describe(e):<58} {e['say']}")
        else:
            out.append(f"  EXACTLY {e['n']}         {describe(e):<58} {e['say']}")
    return out


def plan_text(name, variant=None):
    scenario = SCENARIOS[name]
    spec = resolve(scenario, variant)
    names = _names(spec)
    out = [f"{name}" + (f" --variant {variant}" if variant else "") + f"  (row {spec['row']} of the owner's table, START_HERE section 5.2)",
           f"  {spec['title']}",
           f"  Expect, in the owner's words: {spec['expect_in_words']}",
           f"  Bugs: {', '.join(spec['bugs'])}.  Checklist: {spec['checklist']}",
           "",
           "Players (launch order; in Unity, the main editor and tagged MPPM virtual players):"]
    out += [f"  <{role}>: {what}" for role, what in spec["players"]]
    rep = spec.get("repeat", 1)
    out += ["", "Steps" + (f" (repeat {rep}x; the race window is one presence refresh interval)" if rep > 1 else "") + ":"]
    for i, st in enumerate(spec["steps"], 1):
        first, *rest = step_lines(st, names)
        out.append(f"  {i:2}. {first}")
        out += ["    " + r for r in rest]
    if rep > 1 and spec.get("cycle_reset"):
        out.append("  Between repeats:")
        for st in spec["cycle_reset"]:
            first, *rest = step_lines(st, names)
            out.append(f"   - {first}")
            out += ["    " + r for r in rest]
    out += ["", "Faults to inject:"]
    for f in spec["faults"]:
        tok = " ".join(x for x in (f"netsim={f['netsim']}" if f["netsim"] else "", f"netfault={f['netfault']}" if f["netfault"] else "") if x)
        out.append(f"  - {f['what']}" + (f" on <{f['player']}>" if f["player"] else "") + (" (optional)" if f["optional"] else "")
                   + (f"  [{tok}]" if tok else "  [no token]") + f"  fault: {f['fault']}")
        out.append(f"      {f['how']}")
    out += ["", "Expected timeline (net_logs.py marks, judged in this order; a window runs from the previous MUST to the next):"]
    out += expect_lines(spec)
    if spec.get("checks"):
        out += ["", "Also check (not in the timeline, or not judged by it):"] + [f"  - {c}" for c in spec["checks"]]
    out += ["", "Afterwards: the logs collect themselves when each player stops (close a window, never kill it: a killed player writes no log).",
            "  python3 Tools/Build/net_logs.py show 1 --lines > run.txt      # or point --timeline at the folder of net_*.json",
            f"  python3 Tools/Build/net_scenarios.py --judge {name}" + (f" --variant {variant}" if variant else "")
            + " --timeline run.txt" + (" --cycles N" if rep > 1 else "") + "   # --peer role=peer pins a role to a player"]
    return "\n".join(out)


def list_text():
    out = []
    for name, s in SCENARIOS.items():
        rep = f"  repeat {s['repeat']}x" if s.get("repeat", 1) > 1 else ""
        out.append(f"{name:<24} row {s['row']}  {len(s['players'])} players{rep}  {s['title']}")
        for vname, v in (s.get("variants") or {}).items():
            out.append(f"{'':<24} --variant {vname}: {v['title']}")
    return "\n".join(out)


def marks_text():
    out = ["Lifecycle marks (NetSessionRecorder.Mark in Assets/_Scripts; Amoebius writes the same record):"]
    for e, (where, shape) in MARKS.items():
        out.append(f"  {e:<16} {where}")
        if shape:
            out.append(f"  {'':<16}   detail: {shape}")
    out += ["", "Console lines a scenario reads beside the marks:"]
    for text, (where, level) in CONSOLE_LINES.items():
        out.append(f"  '{text}'  [{level}]")
        out.append(f"      {where}")
    out += ["", "netsim tokens (Port/docs/MULTIPLAYER.md 6.2): " + ", ".join(sorted(NETSIM_TOKENS)),
            "netfault tokens (6.3): " + ", ".join(sorted(NETFAULT_TOKENS))]
    return "\n".join(out)


# ── --run: drive Amoebius players through their control ports (UNVERIFIED) ──

NO_PLAYER = """no built Amoebius player found. Build it (the .NET 10 SDK; in a cloud container:
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /opt/dotnet
  export PATH=/opt/dotnet:$PATH DOTNET_ROOT=/opt/dotnet), then
  dotnet build Port/src/CosmicShore.Player -c Debug
or pass --player <path to CosmicShore.dll or the CosmicShore apphost>."""

UGS_ONLY = """Amoebius multiplayer is UGS only (Docs/MULTIPLAYER_START_HERE.md 5.1): two players meet only through
the LIVE UGS project (a sign-in creates a player there, so it needs the owner's go-ahead - then pass --live; the
`amoebius` environment must exist, 5.3 step 0) or through UGS stand-ins (set COSMIC_SHORE_UGS_LOBBY_URL and
COSMIC_SHORE_UGS_AUTH_URL to running stand-ins; the five-process harness's shared-folder sessions are gone and its
stand-in rewrite is not done - Port/docs/MULTIPLAYER.md section 5)."""


def find_player(explicit=None):
    """(argv prefix to launch the player, description) or None."""
    dotnet = _dotnet()
    cands = [explicit] if explicit else []
    bin_dir = os.path.join(REPO, "Port", "src", "CosmicShore.Player", "bin", "Debug", "net10.0")
    cands += [os.path.join(bin_dir, "CosmicShore.dll"), os.path.join(bin_dir, "CosmicShore"),
              os.path.join(os.environ.get("PRISMA_PARTY_WORK") or os.path.join(tempfile.gettempdir(), "prisma_party_scenarios"),
                           "player", "CosmicShore.dll")]
    for c in cands:
        if not c or not os.path.isfile(c):
            continue
        if c.endswith(".dll"):
            if dotnet:
                return [dotnet, c], c
            continue
        if os.access(c, os.X_OK):
            return [c], c
    return None


def _dotnet():
    for root in (os.environ.get("DOTNET10_ROOT"), os.environ.get("DOTNET_ROOT"), "/opt/dotnet", os.path.expanduser("~/.dotnet10")):
        if root and os.access(os.path.join(root, "dotnet"), os.X_OK):
            return os.path.join(root, "dotnet")
    for d in os.environ.get("PATH", "").split(os.pathsep):
        p = os.path.join(d, "dotnet")
        if os.access(p, os.X_OK):
            return p
    return None


def load_driver():
    """Tools/Build/prisma_party_scenarios/driver.py: Inst (one control port), boot_to_menu, party, wait_party, is_solo, until."""
    path = os.path.join(HERE, "prisma_party_scenarios", "driver.py")
    spec = importlib.util.spec_from_file_location("prisma_party_driver", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def machine_form(st):
    return any(k in st for k in ("console", "do", "together", "ready", "kill", "restart",
                                 "wait_party", "wait_scene", "wait_log", "wait_frames", "wait_joinable"))


class Runner:
    """Launches one Amoebius player per role and plays a scenario's steps through the control ports.

    UNVERIFIED: this session had no .NET SDK, so none of it has run against a built player. The launch line copies
    Tools/Build/prisma_party_scenarios/run.sh (one HOME and profile per player, --headless --realtime --verbose, a
    control port each) plus the two variables the launcher gives START ONLINE so each player writes its net log
    (COSMIC_SHORE_NET_LOG_DIR, COSMIC_SHORE_NET_PEER = the role). Net peers are the roles, so the judge pins each
    role to its player.
    """

    def __init__(self, spec, launch, work, base_port, driver, netsim=None, netfault=None):
        self.spec, self.launch, self.work, self.driver = spec, launch, work, driver
        self.base_port, self.netsim, self.netfault = base_port, netsim, netfault
        self.insts, self.procs, self.ports = {}, {}, {}
        os.makedirs(os.path.join(work, "net"), exist_ok=True)
        os.makedirs(os.path.join(work, "logs"), exist_ok=True)

    def start(self, role):
        port = self.ports.setdefault(role, self.base_port + len(self.ports) + 1)
        home = os.path.join(self.work, "home", role)
        os.makedirs(os.path.join(home, ".local", "share"), exist_ok=True)
        env = dict(os.environ)
        env.update(HOME=home, XDG_DATA_HOME=os.path.join(home, ".local", "share"), COSMIC_SHORE_PROJECT=REPO,
                   COSMIC_SHORE_PROFILE=f"net-{role}", COSMIC_SHORE_AUDIO="off",
                   COSMIC_SHORE_NET_LOG_DIR=os.path.join(self.work, "net"), COSMIC_SHORE_NET_PEER=role,
                   DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        env.pop("COSMIC_SHORE_NET", None)   # online: never "off"
        if self.netsim:
            env["COSMIC_SHORE_NET_SIM"] = self.netsim
        if self.netfault:
            env["COSMIC_SHORE_NET_FAULT"] = self.netfault
        log_path = os.path.join(self.work, "logs", f"{role}.log")
        log = open(log_path, "ab")
        proc = subprocess.Popen(self.launch + ["--headless", "--realtime", "--verbose", "--control-port", str(port)],
                                cwd=self.work, env=env, stdout=log, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL)
        self.procs[role] = proc
        inst = self.driver.Inst(role.capitalize(), port, log_path, proc.pid)
        self.insts[role] = inst
        print(f"  {role}: pid {proc.pid}, control port {port}, log {log_path}", flush=True)
        self.driver.boot_to_menu(inst)
        return inst

    def name(self, role):
        return self.insts[role].name

    def fill(self, text):
        for role in self.insts:
            text = text.replace("{" + role + "}", self.name(role))
        return text

    def wait_party(self, role, what, timeout=240):
        d, inst = self.driver, self.insts[role]
        if what == "solo":
            return d.wait_party(inst, d.is_solo, "its own menu", timeout)
        if what == "seated":
            return d.wait_party(inst, lambda s: s.get("role") == "client", "to be seated", timeout)
        if what == "settled":
            return d.wait_party(inst, lambda s: s.get("role") == "client" or d.is_solo(s), "to settle", timeout)
        if what.startswith("host_at:"):
            k = what[8:]
            return d.wait_party(inst, lambda s: s.get("role") == "host" and s.get("members") == f"{k}/4" and s.get("conns") == k,
                                f"the host to count {k}", timeout)
        if what.startswith("invite:"):
            frm = self.name(what[7:])
            return d.wait_party(inst, lambda s: s.get("invite") == frm, f"the invite from {frm}", timeout)
        raise ValueError(f"unknown wait_party '{what}'")

    def run_step(self, st):
        roles = st["who"] if isinstance(st["who"], list) else [st["who"]]
        d = self.driver
        print(f"  - {', '.join(roles)}: {self.fill(st['say'])[:110]}", flush=True)
        for role in roles:
            inst = self.insts[role]
            if "wait_party" in st:
                self.wait_party(role, st["wait_party"])
            if "wait_joinable" in st:
                host = self.insts[st["wait_joinable"]]
                cnt = d.party(host)["members"]
                d.until(lambda: f"{host.name}(joinable {cnt})" in (self.console(inst, "party online") or ""),
                        180, f"{inst} to see {host.name} joinable at {cnt}", step=2)
            if "wait_scene" in st:
                d.until(lambda: inst.state().get("scene") == st["wait_scene"], 240, f"{inst} in {st['wait_scene']}", step=2)
            if "wait_log" in st:
                d.until(lambda: st["wait_log"] in inst.logs(600), 180, f"{inst} to log '{st['wait_log']}'", step=1)
            if "console" in st:
                inst.do("console " + self.fill(st["console"]))
                inst.wait(10)
            if "do" in st:
                inst.do(self.fill(st["do"]))
                inst.wait(10)
            if st.get("ready"):
                d.until(lambda: inst.rect("ReadyButton"), 180, f"{inst}'s Ready button")
                inst.click("ReadyButton")
                inst.wait(10)
            if st.get("kill"):
                os.kill(inst.pid, signal.SIGKILL)
                self.procs[role].wait(timeout=30)
            if st.get("restart"):
                if self.procs[role].poll() is None:
                    raise RuntimeError(f"{role} is still running; restart is for a killed player")
                self.start(role)
            if "wait_frames" in st:
                inst.wait(st["wait_frames"])
        if "together" in st:
            line = "console " + self.fill(st["together"])
            bar, stamps = threading.Barrier(len(roles)), {}

            def go(role):
                bar.wait()
                stamps[role] = time.time()
                self.insts[role].do(line)
            threads = [threading.Thread(target=go, args=(r,)) for r in roles]
            [t.start() for t in threads]
            [t.join() for t in threads]
            print(f"    press skew {round((max(stamps.values()) - min(stamps.values())) * 1000, 2)} ms", flush=True)

    @staticmethod
    def console(inst, line):
        """Run a DiagnosticsHUD console line through the port's `do console` verb; the result it logged, or None."""
        inst.do("console " + line)
        inst.wait(5)
        tag = f"[console] {line} -> "
        for l in reversed(inst.logs(200).splitlines()):
            if tag in l:
                return l.split(tag, 1)[1]
        return None

    def quit_all(self):
        """Guests first, the host last, so the teardown bounces nobody; a quit (never a kill) writes the net log."""
        order = [r for r in self.spec.get("quit_order", []) if r in self.insts] + [r for r in self.insts if r not in self.spec.get("quit_order", [])]
        for role in order:
            proc = self.procs.get(role)
            if proc is None or proc.poll() is not None:
                continue
            try:
                self.insts[role].cmd("quit", timeout=10)
            except Exception:
                pass
            try:
                proc.wait(timeout=90)
            except subprocess.TimeoutExpired:
                proc.kill()

    def kill_all(self):
        for proc in self.procs.values():
            if proc.poll() is None:
                proc.kill()


def cmd_run(args):
    scenario = SCENARIOS[args.run]
    spec = resolve(scenario, args.variant)
    print(f"--run {args.run}: {UNVERIFIED}")
    player = find_player(args.player)
    if player is None:
        print(NO_PLAYER)
        return 3
    standins = os.environ.get("COSMIC_SHORE_UGS_LOBBY_URL") and os.environ.get("COSMIC_SHORE_UGS_AUTH_URL")
    if not args.live and not standins:
        print(UGS_ONLY)
        return 3
    human = [st for st in spec["steps"] + spec.get("cycle_reset", []) if not machine_form(st)]
    if human:
        print("cannot run unattended; a human step: " + human[0]["say"])
        return 3
    launch, desc = player
    print(f"player: {desc}" + ("  (LIVE UGS project, with the owner's go-ahead)" if args.live else "  (UGS stand-ins from the environment)"))
    work = args.work or os.path.join(tempfile.gettempdir(), "net_scenarios", time.strftime("%Y%m%d_%H%M%S") + "_" + args.run)
    os.makedirs(work, exist_ok=True)
    driver = load_driver()
    runner = Runner(spec, launch, work, args.base_port, driver, netsim=args.netsim, netfault=args.netfault)
    cycles = args.cycles or spec.get("repeat", 1)
    try:
        print("launching players:")
        for role, _ in spec["players"]:
            runner.start(role)
        for c in range(1, cycles + 1):
            print(f"cycle {c}/{cycles}:")
            if c > 1:
                for st in spec.get("cycle_reset", []):
                    runner.run_step(st)
            for st in spec["steps"]:
                runner.run_step(st)
        runner.quit_all()
    finally:
        runner.kill_all()
    rows, desc = rows_from(os.path.join(work, "net"))
    verdict = judge(rows, scenario, cycles=cycles, variant=args.variant, peers={role: role for role, _ in spec["players"]})
    print(f"judging {desc}")
    print("\n".join(verdict.lines))
    with open(os.path.join(work, "results.json"), "w", encoding="utf-8") as f:
        json.dump({"scenario": args.run, "variant": args.variant, "cycles": cycles, "pass": verdict.ok, "reason": verdict.reason,
                   "rows": [list(r) for r in rows], "lines": verdict.lines, "work": work, "unverified": UNVERIFIED}, f, indent=1)
    print(f"verdict: {'PASS' if verdict.ok else 'FAIL - ' + verdict.reason}   (logs and results.json under {work})")
    return 0 if verdict.ok else 1


# ── commands ────────────────────────────────────────────────────────────────

def cmd_judge(args):
    scenario = SCENARIOS[args.judge]
    try:
        rows, desc = rows_from(args.timeline, args.test)
    except ValueError as e:
        print(f"cannot read the timeline: {e}")
        return 2
    peers = dict(kv.split("=", 1) for kv in (args.peer or []) if "=" in kv)
    verdict = judge(rows, scenario, cycles=args.cycles, peers=peers, variant=args.variant)
    print(f"{args.judge}" + (f" --variant {args.variant}" if args.variant else "") + f"  judged from {desc}, {len(rows)} rows"
          + (f", {args.cycles} cycle(s)" if args.cycles else ""))
    print("\n".join(verdict.lines))
    if verdict.bindings:
        print("roles: " + ", ".join(f"{a[1:]}={p}" for a, p in sorted(verdict.bindings.items())))
    if not verdict.ok:
        print(f"verdict: FAIL - {verdict.reason}")
        if verdict.anchor is not None:
            after = rows[verdict.anchor + 1:verdict.anchor + 9]
            if after:
                print("  rows after the anchor:")
                for r in after:
                    print(f"    {r[0].strip():>9}  {r[1]:<10} {r[2]:<8} {r[3][:100]}")
        return 1
    print("verdict: PASS")
    return 0


# ── self-test ───────────────────────────────────────────────────────────────

def synthetic_log(peer, start, role, events, lines=(), user="tester"):
    """One net log in the schema NetTestLogFormat writes (what net_logs.parse reads)."""
    return {"schema": net_logs.SCHEMA, "engine": "amoebius", "peer": peer, "isVirtualPlayer": False, "why": "test", "user": user,
            "branch": "Ys-bleeding-edge", "commit": "abc1234", "playStartedUtc": start, "playEndedUtc": start, "exit": "quit",
            "record": {"schema": "cosmicshore.netsession.v1", "startedUtc": start, "role": role, "counters": {}, "verdict": {},
                       "lifecycle": [{"t": t, "e": e, "detail": d} for t, e, d in events]},
            "console": {"errors": 0, "warnings": sum(1 for l in lines if l[1] == "warning"), "dropped": 0,
                        "lines": [{"t": t, "type": k, "msg": m} for t, k, m in lines]}}


def rows_of(players):
    """players: {peer: (start_iso, role, events, lines)} -> the merged timeline, through net_logs' own grouping."""
    logs = []
    for peer, (start, role, events, lines) in players.items():
        log = net_logs.parse(json.dumps(synthetic_log(peer, start, role, events, lines)), source=f"synthetic:{peer}", user="tester")
        log["branches"] = []
        logs.append(log)
    tests = net_logs.group(logs)
    assert len(tests) == 1, [t["id"] for t in tests]
    return net_logs.timeline(tests[0], all_lines=True)


T0 = "2026-11-07T10:00:00Z"


def fixture_host_drop():
    """One cycle: main hosts p2 and p3, drops; p2 invites p3 at once; p3 is seated."""
    return {
        "main": (T0, "host", [(1, "clientApproved", "0"), (5, "party", "InParty->Inviting"), (10, "clientApproved", "1"), (15, "clientApproved", "2")], []),
        "p2": (T0, "client", [(1, "clientApproved", "0"), (8, "party", "InParty->JoiningParty"), (12, "party", "JoiningParty->InParty"),
                              (40, "bounce", "Host disconnected"), (41, "party", "InParty->Disconnected"), (43, "party", "Disconnected->InPresenceLobby"),
                              (44, "party", "InPresenceLobby->HostingParty"), (46, "party", "HostingParty->InParty"), (46, "clientApproved", "0"),
                              (47, "party", "InParty->Inviting"), (60, "clientApproved", "1"), (60, "party", "Inviting->InParty")],
               [(40, "warning", "[PartyInviteController] Bouncing to solo menu: Host disconnected")]),
        "p3": (T0, "client", [(1, "clientApproved", "0"), (16, "party", "InParty->JoiningParty"), (20, "party", "JoiningParty->InParty"),
                              (40.5, "bounce", "Connection lost"), (45, "party", "HostingParty->InParty"), (45, "clientApproved", "0"),
                              (55, "party", "InParty->JoiningParty"), (61, "party", "JoiningParty->InParty")], []),
    }


def fixture_host_drop_two_cycles():
    """Cycle 1 as above; then p2 (the host now) drops, p3 invites the restarted main (a second 'main' log), which is seated."""
    f = fixture_host_drop()
    f["p3"][2].extend([(80, "bounce", "Host disconnected"), (85, "party", "HostingParty->InParty"), (85, "clientApproved", "0"),
                       (86, "party", "InParty->Inviting"), (95, "clientApproved", "1")])
    f["main-again"] = ("2026-11-07T10:01:10Z", "client", [(1, "clientApproved", "0"), (20, "party", "InParty->JoiningParty"),
                                                          (26, "party", "JoiningParty->InParty")], [])
    return f


def fixture_double_join():
    return {
        "main": (T0, "host", [(1, "clientApproved", "0"), (10, "clientApproved", "1"), (20, "clientApproved", "2"), (31, "clientApproved", "3")], []),
        "p2": (T0, "client", [(8, "party", "InParty->JoiningParty"), (11, "party", "JoiningParty->InParty")], []),
        "p3": (T0, "client", [(18, "party", "InParty->JoiningParty"), (21, "party", "JoiningParty->InParty")], []),
        "p4": (T0, "client", [(30.0, "party", "InParty->JoiningParty"), (32, "party", "JoiningParty->InParty")], []),
        "p5": (T0, "host", [(30.0, "party", "InParty->JoiningParty"), (33, "bounce", "That party is full."), (34, "party", "JoiningParty->HostingParty"),
                            (35, "party", "HostingParty->InParty"), (35, "clientApproved", "0")],
               [(33, "warning", "[PartyInviteController] Bouncing to solo menu: That party is full.")]),
    }


def fixture_ready_gate(lobby=False):
    host = [(1, "clientApproved", "0"), (10, "clientApproved", "1"), (20, "clientApproved", "2"), (30, "clientApproved", "3")]
    lines = []
    if lobby:
        host += [(60, "clientLeft", "3")]
        lines = [(60.2, "log", "[ArcadeConfigSync] All players ready (client 3 left) - launching game")]
    else:
        host += [(80, "clientLeft", "3"), (80.1, "readyGate", "BloomrushController: client 3 disconnected")]
    return {
        "main": (T0, "host", host, lines),
        "p2": (T0, "client", [(8, "party", "InParty->JoiningParty"), (11, "party", "JoiningParty->InParty")], []),
        "p3": (T0, "client", [(18, "party", "InParty->JoiningParty"), (21, "party", "JoiningParty->InParty")], []),
        "p4": (T0, "client", [(28, "party", "InParty->JoiningParty"), (31, "party", "JoiningParty->InParty"), (81, "party", "HostingParty->InParty")],
               [(81, "log", "[PartyInviteController] Leave-lobby flow completed.")]),
    }


def fixture_mid_match():
    f = fixture_ready_gate()
    f["main"] = (T0, "host", [(1, "clientApproved", "0"), (10, "clientApproved", "1"), (20, "clientApproved", "2"), (30, "clientApproved", "3"),
                              (50, "readyGate", "BloomrushController: PilotD pressed Ready"), (120, "clientLeft", "2"), (120.1, "leaverToAI", "PilotC")], [])
    return f


def self_test():
    ok = True

    def check(cond, what):
        nonlocal ok
        print(("ok    " if cond else "FAIL  ") + what)
        ok = ok and cond

    # The vocabulary: a scenario names only marks the game writes and tokens the docs name.
    def entries(spec):
        for e in spec["expect"]:
            yield from (e["entries"] if e["op"] == "must_all" else [e])
    for name, s in SCENARIOS.items():
        for variant in [None] + sorted(s.get("variants") or {}):
            spec = resolve(s, variant)
            bad = [e for e in entries(spec) if e.get("kind", "event") == "event" and e.get("e") not in MARKS]
            check(not bad, f"{name}{' --variant ' + variant if variant else ''}: every mark named is one the game writes" + (f" (not: {bad})" if bad else ""))
            lines = [e for e in entries(spec) if e.get("kind", "event") != "event"]
            check(all(any(k in str(e.get("detail")) for k in CONSOLE_LINES) for e in lines), f"{name}: every console line named is catalogued")
        for f in s["faults"]:
            for key, table in (("netsim", NETSIM_TOKENS), ("netfault", NETFAULT_TOKENS)):
                tok = f[key]
                if tok:
                    check(tok.split("=")[0].split()[0] in table, f"{name}: fault token '{tok}' is documented ({key})")
            check(f["fault"] in ("documented", "to name with Yash"), f"{name}: the fault entry says who names it")
            check("TODO" not in json.dumps(f), f"{name}: no code TODO in the fault data")
        roles = {r for r, _ in s["players"]}
        for st in s["steps"] + s.get("cycle_reset", []):
            who = st["who"] if isinstance(st["who"], list) else [st["who"]]
            check(set(who) <= roles, f"{name}: step names only its players ({who})")
    check(all(SCENARIOS[n]["row"] == r for n, r in (("host-drop-reinvite", 2), ("double-join-full", 3), ("leave-at-ready-gate", 4), ("guest-leaves-mid-match", 5))),
          "the four scenarios are rows 2-5 of the owner's table")

    # Row 2: host drop, re-invite at once. One cycle passes; a missing seat and a B29 refusal fail.
    hd = SCENARIOS["host-drop-reinvite"]
    v = judge(rows_of(fixture_host_drop()), hd, cycles=1)
    check(v.ok, f"host-drop-reinvite: a clean cycle passes ({v.reason})")
    check(v.bindings.get("$newhost") == "p2" and v.bindings.get("$guest") == "p3", f"...and binds newhost=p2 guest=p3 ({v.bindings})")
    f = fixture_host_drop()
    f["p3"][2].remove((61, "party", "JoiningParty->InParty"))
    v = judge(rows_of(f), hd, cycles=1)
    check(not v.ok and "guest is in the new host's party" in v.reason, f"negative control: the guest never seated -> FAIL naming the seat ({v.reason})")
    f = fixture_host_drop()
    f["p3"][2].append((50, "refused", "PilotB's party is no longer available."))
    v = judge(rows_of(f), hd, cycles=1)
    check(not v.ok and "B29" in v.reason and "no longer available" in v.reason, f"negative control: a B29 refusal -> FAIL naming B29 ({v.reason})")
    f = fixture_host_drop()
    f["p2"][2].remove((47, "party", "InParty->Inviting"))
    f["p2"][2].append((90, "party", "InParty->Inviting"))
    v = judge(rows_of(f), hd, cycles=1)
    check(not v.ok and "later than 30s" in v.reason, f"negative control: an invite 50 s after the drop misses the window -> FAIL ({v.reason})")
    v = judge(rows_of(fixture_host_drop_two_cycles()), hd, cycles=2)
    check(v.ok, f"two cycles with rotated roles pass (roles re-bind each cycle) ({v.reason})")
    check(v.bindings.get("$newhost") == "p3" and v.bindings.get("$guest") == "main-again",
          f"...cycle 2 bound newhost=p3 and guest=the restarted host ({v.bindings})")
    v = judge(rows_of(fixture_host_drop_two_cycles()), hd, cycles=3)
    check(not v.ok and v.reason.startswith("cycle 3"), f"...and asking for a third cycle fails in cycle 3 ({v.reason})")
    v = judge(rows_of(fixture_host_drop_two_cycles()), hd, cycles=2, peers={"newhost": "p2"})
    check(not v.ok, "a role pinned with --peer holds across cycles: newhost=p2 cannot be cycle 2's host (negative control)")

    # Row 3: two joins on a 3/4 party.
    dj = SCENARIOS["double-join-full"]
    v = judge(rows_of(fixture_double_join()), dj)
    check(v.ok, f"double-join-full: 4/4 and one 'That party is full.' bounce passes ({v.reason})")
    f = fixture_double_join()
    f["main"][2].append((34, "clientApproved", "4"))
    v = judge(rows_of(f), dj)
    check(not v.ok and "B25" in v.reason and "found 2" in v.reason, f"negative control: a fifth seat -> FAIL naming B25 ({v.reason})")
    f = fixture_double_join()
    f["p5"][2].remove((33, "bounce", "That party is full."))
    f["p5"][2].append((30.5, "refused", "PilotA's party is full."))
    v = judge(rows_of(f), dj)
    check(not v.ok and "race was" in v.reason, f"negative control: a pre-flight refusal -> FAIL: the race was not exercised ({v.reason})")
    f = fixture_double_join()
    f["main"][2].remove((20, "clientApproved", "2"))
    v = judge(rows_of(f), dj)
    # Ids are not roles: with one approval missing, the winner's approval reads as the third seat and the fourth never comes.
    check(not v.ok and "B25" in v.reason and "found 0" in v.reason, f"negative control: one seat fewer -> FAIL: no fourth seat ({v.reason})")

    # Row 4: a leaver at the ready gate, and at the launch lobby.
    rg = SCENARIOS["leave-at-ready-gate"]
    v = judge(rows_of(fixture_ready_gate()), rg)
    check(v.ok, f"leave-at-ready-gate: clientLeft + readyGate 'disconnected' passes ({v.reason})")
    f = fixture_ready_gate()
    f["main"][2].remove((80.1, "readyGate", "BloomrushController: client 3 disconnected"))
    v = judge(rows_of(f), rg)
    check(not v.ok and "fourth press" in v.reason, f"negative control: the gate never passed -> FAIL naming the gate ({v.reason})")
    f = fixture_ready_gate()
    f["p2"][2].append((85, "bounce", "Host disconnected"))
    v = judge(rows_of(f), rg)
    check(not v.ok and "thrown out" in v.reason, f"negative control: another member bounced -> FAIL ({v.reason})")
    v = judge(rows_of(fixture_ready_gate(lobby=True)), rg, variant="launch-lobby")
    check(v.ok, f"--variant launch-lobby: the lobby's 'launching game' log line passes ({v.reason})")
    f = fixture_ready_gate(lobby=True)
    f["main"] = (f["main"][0], f["main"][1], f["main"][2], [])
    v = judge(rows_of(f), rg, variant="launch-lobby")
    check(not v.ok and "launch line" in v.reason, f"negative control: no launch line -> FAIL ({v.reason})")
    try:
        resolve(rg, "nope")
        check(False, "an unknown variant is refused")
    except KeyError as e:
        check("launch-lobby" in str(e), "an unknown variant is refused and the real ones are named")

    # Row 5: a guest leaves mid-match.
    mm = SCENARIOS["guest-leaves-mid-match"]
    v = judge(rows_of(fixture_mid_match()), mm)
    check(v.ok, f"guest-leaves-mid-match: readyGate then clientLeft + leaverToAI passes ({v.reason})")
    f = fixture_mid_match()
    f["main"][2].remove((120.1, "leaverToAI", "PilotC"))
    v = judge(rows_of(f), mm)
    check(not v.ok and "B21" in v.reason, f"negative control: no AI hand-over -> FAIL naming B21 ({v.reason})")
    f = fixture_mid_match()
    f["p3"][2].append((125, "bounce", "Connection lost"))
    v = judge(rows_of(f), mm)
    check(not v.ok and "goes on" in v.reason, f"negative control: another guest bounced -> FAIL ({v.reason})")
    f = fixture_mid_match()
    f["main"][2].remove((50, "readyGate", "BloomrushController: PilotD pressed Ready"))
    v = judge(rows_of(f), mm)
    check(not v.ok and "match started" in v.reason, f"negative control: the match never started -> FAIL ({v.reason})")

    # The seat regex: client 0 (the host itself), spectators and rejoins are not new seats.
    check(all(re.search(SEAT, d) is None for d in ("0", "1 spectator", "2 rejoin")) and re.search(SEAT, "3"), "SEAT matches a plain client id >= 1 only")

    # `show` text round-trips into the same verdict.
    rows = rows_of(fixture_double_join())
    width = max(len(r[1]) for r in rows)
    shown = "tester-20261107_100000  amoebius  started 2026-11-07 10:00:00 UTC\n  main       host       CLEAN errors 0\n" + \
        "\n".join(f"{when}  {peer:<{width}} {kind:<9} {text}" for when, peer, kind, text in rows)
    back = parse_show(shown)
    check(len(back) == len(rows) and judge(back, dj).ok, "the text `net_logs.py show` prints parses back and judges the same")

    # rows_from: a folder of net logs (newest test, --test id) and a single file.
    with tempfile.TemporaryDirectory() as tmp:
        for peer, (start, role, events, lines) in fixture_mid_match().items():
            with open(os.path.join(tmp, f"net_20261107_100000_{peer}.json"), "w", encoding="utf-8") as f:
                json.dump(synthetic_log(peer, start, role, events, lines), f)
        rows, desc = rows_from(tmp)
        check(judge(rows, mm).ok and "test tester-20261107_100000" in desc, f"a folder of net_*.json is read through net_logs ({desc})")
        rows, desc = rows_from(os.path.join(tmp, "net_20261107_100000_main.json"))
        check(judge(rows, mm).ok and "one player" in desc, "one net_*.json (the host's alone) is enough for a host-side verdict")
        try:
            rows_from(tmp, "nope")
            check(False, "an unknown --test id is refused")
        except ValueError:
            check(True, "an unknown --test id is refused")
        with open(os.path.join(tmp, "garbage.txt"), "w") as f:
            f.write("not a timeline\n")
        try:
            rows_from(os.path.join(tmp, "garbage.txt"))
            check(False, "a file that is nothing we read is refused")
        except ValueError:
            check(True, "a file that is nothing we read is refused")

    # Plans render for every scenario and variant, with every step carrying a machine form or saying it is a human step.
    for name, s in SCENARIOS.items():
        for variant in [None] + sorted(s.get("variants") or {}):
            text = plan_text(name, variant)
            check("control port:" in text and "Expected timeline" in text, f"--plan {name}{' --variant ' + variant if variant else ''} renders")
    check(all(machine_form(st) for s in SCENARIOS.values() for st in s["steps"] + s.get("cycle_reset", [])),
          "every step of the four scenarios has a control-port form (so --run needs no human)")
    check("host-drop-reinvite" in list_text() and "launch-lobby" in list_text(), "--list names the scenarios and their variants")
    check("clientApproved" in marks_text(), "--marks prints the vocabulary")

    # --run refuses cleanly where it cannot run: no player here, or no go-ahead.
    check(find_player("/nonexistent/CosmicShore.dll") is None or True, "find_player tolerates a missing path")
    check("dotnet build Port/src/CosmicShore.Player" in NO_PLAYER and "--live" in UGS_ONLY, "the exit-3 messages say what to build and what to get")

    print("net_scenarios: self-test " + ("OK" if ok else "FAILED"))
    return 0 if ok else 1


# ── main ────────────────────────────────────────────────────────────────────

def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0], formatter_class=argparse.RawDescriptionHelpFormatter,
                                 epilog="scenarios: " + ", ".join(SCENARIOS))
    ap.add_argument("--list", action="store_true", help="the scenarios, one line each")
    ap.add_argument("--plan", metavar="SCENARIO", help="print the steps and expectations, to run by hand")
    ap.add_argument("--judge", metavar="SCENARIO", help="judge a recorded run (needs --timeline)")
    ap.add_argument("--run", metavar="SCENARIO", help=f"drive Amoebius players through their control ports ({UNVERIFIED})")
    ap.add_argument("--marks", action="store_true", help="the mark vocabulary and where the game writes it")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--variant", help="a scenario variant (--list shows them)")
    ap.add_argument("--timeline", help="a folder of net_*.json, one net_*.json, or `net_logs.py show` output")
    ap.add_argument("--test", help="with a folder: the test id to judge (default: the newest)")
    ap.add_argument("--cycles", type=int, help="how many repeats the run holds (default: the scenario's)")
    ap.add_argument("--peer", action="append", metavar="ROLE=PEER", help="pin a role to a player, e.g. guest=p3 (repeatable)")
    ap.add_argument("--player", help="--run: the CosmicShore.dll or apphost to launch (default: Port/src/CosmicShore.Player/bin/Debug/net10.0)")
    ap.add_argument("--live", action="store_true", help="--run: play through the LIVE UGS project (only with the owner's go-ahead)")
    ap.add_argument("--work", help="--run: the scratch folder for logs and results.json")
    ap.add_argument("--base-port", type=int, default=47900, help="--run: control ports are base+1, base+2, ...")
    ap.add_argument("--netsim", help="--run: COSMIC_SHORE_NET_SIM for every player, e.g. 4g")
    ap.add_argument("--netfault", help="--run: COSMIC_SHORE_NET_FAULT for every player, e.g. slow=500")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if args.list:
        print(list_text())
        return 0
    if args.marks:
        print(marks_text())
        return 0
    for name in (args.plan, args.judge, args.run):
        if name and name not in SCENARIOS:
            print(f"no scenario '{name}' (" + ", ".join(SCENARIOS) + ")")
            return 2
    try:
        if args.plan:
            print(plan_text(args.plan, args.variant))
            return 0
        if args.judge:
            if not args.timeline:
                print("--judge needs --timeline <folder of net_*.json | net_*.json | show output>")
                return 2
            return cmd_judge(args)
        if args.run:
            return cmd_run(args)
    except KeyError as e:
        print(str(e).strip('"\''))
        return 2
    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
