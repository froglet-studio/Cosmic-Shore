#!/usr/bin/env python3
"""Make Prisma's headless player keep game time on the wall clock - in a THROWAWAY worktree only.

    python3 Tools/Build/prisma_party_scenarios/pace_headless.py <worktree-root>

WHY. `--headless` ticks a fixed 1/60 s as fast as the CPU allows (Port/CLAUDE.md), so an idle menu
runs its game clock ~200x faster than the wall. One process alone cannot tell. Five processes
talking through a session directory and TCP can, because those run on the wall clock. Every
game-time timer then fires early against them. The first run that showed it: an invite's 60 s
lifetime lapsed before the other process had polled the invite line, so the guest's Accept met a
withdrawn invite. Unity has no such mode: its timers follow real time. So the scenarios run with
the game clock paced to the wall.

HOW. A frame that finishes early sleeps out the rest of its 1/60 s. A frame that runs late is
caught up by not sleeping. The debt is capped at a quarter second, so a long load (boot, a scene
change) is not followed by a burst of compressed time - the same reason Unity caps deltaTime.
Gated on COSMIC_SHORE_HEADLESS_REALTIME=1, so the patched build behaves as before without it.

Port/CLAUDE.md: the engine changes only in a port session. This edit is made to the temporary
worktree run.sh deletes afterwards, the same way ../prisma_edit_mode_tests/gapfill.py fills API
gaps. Idempotent; if the port gains pacing of its own, retire this.
"""
import pathlib
import sys

p = pathlib.Path(sys.argv[1]) / "Port" / "src" / "CosmicShore.Player" / "Program.cs"
t = p.read_text(encoding="utf-8-sig")
MARK = "COSMIC_SHORE_HEADLESS_REALTIME"
if MARK in t:
    print("  present  Program.cs: headless wall-clock pacing")
    sys.exit(0)

before_loop = "            int frameNow = 0;\n"
after_tick = "                SessionReport.SimTime(tickMs);\n"
assert t.count(before_loop) == 1, "anchor 'int frameNow = 0;' not unique/found - the port's headless loop changed"
assert t.count(after_tick) == 1, "anchor 'SessionReport.SimTime(tickMs);' not unique/found - the port's headless loop changed"

t = t.replace(before_loop, before_loop +
    "            // prisma_party_scenarios/pace_headless.py (throwaway worktree only): game time on the wall clock.\n"
    "            bool paceRealtime = System.Environment.GetEnvironmentVariable(\"" + MARK + "\") == \"1\";\n"
    "            long paceOrigin = System.Diagnostics.Stopwatch.GetTimestamp();\n"
    "            double paceFrames = 0;\n", 1)
t = t.replace(after_tick, after_tick +
    "                if (paceRealtime)\n"
    "                {\n"
    "                    paceFrames++;\n"
    "                    double ahead = paceFrames * (1000.0 / 60.0) - System.Diagnostics.Stopwatch.GetElapsedTime(paceOrigin).TotalMilliseconds;\n"
    "                    if (ahead > 1) System.Threading.Thread.Sleep((int)ahead);\n"
    "                    else if (ahead < -250) paceFrames += (-250 - ahead) / (1000.0 / 60.0); // drop debt past 1/4 s\n"
    "                }\n", 1)
p.write_text(t, encoding="utf-8")
print("  patched  Program.cs: headless wall-clock pacing (COSMIC_SHORE_HEADLESS_REALTIME=1)")
