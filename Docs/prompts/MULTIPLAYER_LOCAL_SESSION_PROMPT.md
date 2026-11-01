# Multiplayer: the local Unity session (hand-off prompt)

Paste everything below the line into a **local** Claude Code session started in your Cosmic-Shore clone, with the
Unity Editor open on that project. It takes over all multiplayer work and testing from the cloud session that ran
the hourly sweep (2026-10-25 to 2026-11-01). Written 2026-11-01.

---

You are taking over the Cosmic Shore multiplayer work, as a LOCAL session on my machine with the Unity Editor open.
The point of being local: you drive Unity yourself (compile, Play mode, Multiplayer Play Mode virtual players, the test
runner, the console) and test everything by yourself, instead of handing me "go and check" steps. Work on
`Ys-bleeding-edge`; merge, never rebase or force-push; never push to `bleeding-edge`; never open a pull request unless I
ask; never touch the live UGS project (anything on UGS or the backend goes through Shombith).

## 0. Start

1. `git fetch origin`, check out `Ys-bleeding-edge`, merge `origin/Ys-bleeding-edge`.
2. Read, in this order: `CLAUDE.md`; `Docs/MULTIPLAYER_START_HERE.md` (all of it: rules §2, the sweep §3.1, state §4,
   hand tests §5, next steps §6); `Docs/PartySystem/BUGS.md` (index table, then B36, B39-B47);
   `Docs/PartySystem/SWEEP_LOG.md` (newest rows); `Docs/Multiplayer/README.md` (the doc index);
   `Docs/Multiplayer/TEST_CHECKLIST.md` (the hand bug-hunt checklist and the Ctrl+Shift+M overlay);
   `Docs/unity-cli-setup.md`.
3. Get the Unity CLI working against the open Editor (`unity --help` for the installed version is the authority; never
   assume flags). Prove it once: a compile, entering and leaving Play mode, reading the console. If it cannot connect,
   fix the setup per `Docs/unity-cli-setup.md`; tell me only if you are truly blocked.
4. `python3 Tools/Build/who_works_where.py query <paths>` before changing anything; name who else works there.

## 1. How you verify (replaces what the cloud session could not do)

- Every C# change: `bash Tools/Build/unity_refcompile/run.sh` (player and editor), `bash .claude/skills/ship/gates.sh`,
  AND a real compile in the open Editor through the CLI, with the console read for new errors. Run the affected
  EditMode tests in Unity's Test Runner (the Amoebius edit-mode suite no longer builds since 29 Oct: 102 engine APIs
  missing in newly merged code; do not fix that harness here, it is Shombith's).
- Multiplayer behaviour: test it in Play mode with **Multiplayer Play Mode** virtual players (2-3 instances, each its
  own UGS account), the Party / ArcadeMatch log channels on, and the overlay (Ctrl+Shift+M) open. Read every
  instance's console and its net test log (written at Play stop; `python3 Tools/Build/net_logs.py collect` / `show`).
- When a hand test passes, mark its `Docs/UNITY_VERIFICATION_CHECKLIST.md` entry verified with the date and what you
  saw. Never claim a check that did not run. What only two real machines on two networks can show (Relay over the
  internet, NAT), say so and leave it for me.

## 2. The work, in order

1. **Clear the verification backlog with MPPM.** Every multiplayer entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`
   marked NOT EDITOR-VERIFIED since 2026-10-17, newest first: Block 5 steps 1-9 (push events, two absent reads, the 20 s
   safety poll, leave on quit/background, ExitingPlayMode leave), B31-B35, B39-B44 (recovery from a dropped host),
   B46 (`MiniGameHUD.ResolveAIAvatarSprite` null name), the Cell swap trail return (B36, `Cell.ReturnRetiredTrailToPool`),
   the boot-load timing (`LoadInsights.BeginBootLoad`), and the 1 Nov batch (8 s watched poll, second converge at +7 s,
   Friends Offline on quit, arcade lobby AI roster resync, RTT badge, `HitReportGuard`, vessel transform tuning).
   Then run `Docs/Multiplayer/TEST_CHECKLIST.md` end to end. Each failure becomes a fix (below) or a
   BUGS.md entry.
2. **B47**: a mid-match rejoin reclaims the seat but the rejoiner logs `Double initialization not allowed!`
   (`VesselController.Initialize`), and other clients see the seat with no name. Reproduce with MPPM (drop a client
   mid Serpent/Thresher time trial, REJOIN), root-cause, fix, prove.
3. **B36 / B45, the long freezes that drop the Relay.** Garrett's machine froze 10-62 s in single frames (boot menu
   load, Maelstrom cell swap, menu after Regatta); four boots dropped the Relay. Two things wait on people: the boot
   owner decides whether the Relay host starts after the menu is up (do not change the boot order without that
   decision), and a Profiler capture on Garrett's machine. On MY machine: profile a cold and a warm boot and a big
   Cell swap in Play mode (Profiler + Load Time Insights), measure the worst frames, and report what the frame is spent
   on. Fix what the measurement names if it is in multiplayer or the swap; otherwise write it into B36/B45.
4. **The Unity track in `Docs/MULTIPLAYER_START_HERE.md` §6**: U1 (my MPPM runs: now yours), U3 (reconnection grace:
   a 15 s network pull mid-match hands the vessel back with no menu), then the rest in that table. Measure Block 5's
   target too: lobby GETs at rest per client per minute and invite latency, in a 30-minute 3-instance MPPM run.
5. **The multiplayer sweep, as a local routine.** Real tests from the team still land on `bugledger/<user>` branches.
   Once a day (or when I say "sweep"), run `Docs/MULTIPLAYER_START_HERE.md` §3.1 exactly: `net_logs.py collect`,
   compare with the newest `SWEEP_LOG.md` row, `show` each new test, match findings to BUGS.md, fix and verify here
   (now with Unity), add the SWEEP_LOG row, tell me in a few lines. Check the Bug Hunt sweep's recent commits first.
   The cloud session's hourly trigger ("Multiplayer sweep") is being retired with that session.

## 3. Standing rules (from the cloud session; keep them)

- One commit per fix with its proof; docs in the same change (BUGS.md row and section, the checklist entry, SWEEP_LOG,
  MULTIPLAYER_START_HERE §4/§6 when a step lands). Claim bug numbers after checking every origin branch's BUGS.md
  (`git branch -r` + `git show <b>:Docs/PartySystem/BUGS.md`); B47 is the highest as of 2026-11-01.
- Port (Amoebius engine) commits are separate and Port-only. No Unity or package version changes. No raw `Debug.Log`
  (CSDebug channels; `check_console_logging.py`). Player-facing text is an authored field. No model ids in code,
  commits or docs. "Affects: <person>'s <area>" in commits that touch someone else's area.
- Froglet Tasks: suggest only, never file unapproved, never name or assign a person.
- Never ask me to run a command, a learner or a test: run it yourself. Ask me only for what needs a second real
  machine, another person, or a decision that is someone else's.
- Plain-language replies: what you tested, what passed, what failed, what you fixed, what waits on whom.
