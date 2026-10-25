# Team tasks

The team's hand-off tickets live on **GitHub Issues** (label `team-task`) since 2026-10-24; this file no longer holds
them. Rules: [`Docs/TEAM.md`](TEAM.md) rule 9.

- **The board:** the Froglet Tasks page, https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f
- **From a session:** the `/team-tasks` skill, or `python3 Tools/Build/team_tasks.py list`
- **On GitHub:** issues labelled [`team-task`](https://github.com/froglet-studio/Cosmic-Shore/issues?q=label%3Ateam-task)

A task a branch still writes below the marker in the old format is filed as a ticket by the next sweep
(`.github/workflows/team-tasks.yml`); prefer `/team-tasks add`.

<!-- team-tasks -->

### T-20261015-d4f5 · done · test · ui
- **Task:** Profiler capture on claude/new-session-76hnyc: Menu_Main Home (not flying) and the lava lamp. Canvas.BuildBatch / SendWillRenderCanvases should not fire every frame from closed windows (style-B ambient loops now sleep while hidden); compare frame time with bleeding-edge
- **Done when:** A shared capture shows no per-frame canvas rebuild from hidden menu windows and Home/lava-lamp frame time at or better than bleeding-edge
- **Who can take it:** Yash or Shombith
- **Added:** 2026-10-15 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md section 24
- **Done:** 2026-10-24, Closed without being run (Yash, 2026-10-24): the test named claude/new-session-76hnyc, which merged into bleeding-edge as #1153 on 2026-10-19 and went through Rev 4-10 after this was written. Re-test the merged menu on bleeding-edge as a new task if needed.

### T-20261015-e890 · done · test · ui
- **Task:** Boot flow on claude/new-session-76hnyc: from Bootstrap through sign-in, Menu_Main shows only after Garland is built (black + small LOADING bar, then one fade); no GROWING GARLAND headline; game scenes' connecting screen unchanged; Toy Box Arkway/microscene loads use the subtle readout
- **Done when:** All four checks pass in the editor, and the boot cover time is noted
- **Who can take it:** Yash
- **Added:** 2026-10-15 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md section 25
- **Done:** 2026-10-24, Closed without being run (Yash, 2026-10-24): the test named claude/new-session-76hnyc, which merged into bleeding-edge as #1153 on 2026-10-19 and went through Rev 4-10 after this was written. Re-test the merged menu on bleeding-edge as a new task if needed.

### T-20261015-e4dc · done · test · ui
- **Task:** Style-B start flow on claude/new-session-76hnyc: Bootstrap splash (dim, LOADING bar, no spinner), Connection lost window + RETRY on A, age gate and consent (B/Esc declines), sign-in auth panel and pilot-name panel (rule ticks, count, single press on A), splash holds until Garland is built
- **Done when:** All nine steps of the checklist entry 'Style-B start flow' pass in the editor, with a pad and with a keyboard
- **Who can take it:** Yash
- **Added:** 2026-10-15 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md, Docs/MENU_REDESIGN_B.md section 26
- **Done:** 2026-10-24, Closed without being run (Yash, 2026-10-24): the test named claude/new-session-76hnyc, which merged into bleeding-edge as #1153 on 2026-10-19 and went through Rev 4-10 after this was written. Re-test the merged menu on bleeding-edge as a new task if needed.

### T-20261015-ef54 · done · test · ui
- **Task:** Style-B Maelstrom game UI on claude/new-session-76hnyc: hub (chips, side-by-side round cards with the upcoming card outlined, Race to N bars, Pilots ready faces, READY on A, LB/RB scroll), stats screen (NEXT on A), summary (winner line, domain chips, ranked cards, A/Y/B)
- **Done when:** All six steps of the checklist entry 'Style-B Maelstrom game UI' pass in the editor, solo with AI and in a two-player party
- **Who can take it:** Yash
- **Added:** 2026-10-15 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md, Docs/MENU_REDESIGN_B.md section 27
- **Done:** 2026-10-24, Closed without being run (Yash, 2026-10-24): the test named claude/new-session-76hnyc, which merged into bleeding-edge as #1153 on 2026-10-19 and went through Rev 4-10 after this was written. Re-test the merged menu on bleeding-edge as a new task if needed.

### T-20261017-3d13 · open · test · performance
- **Task:** Re-capture Menu_Main with the Stoat for 10 minutes in a Release editor run, with the Third Eye CLOSED (it is a second camera and taints the frame time). The vessel-registry fix should keep frames flat; the diag now names each Driver.LateUpdate and the BlackHole.* rows.
- **Done when:** A bugledger/yskha capture from after this fix shows no slide from minute 1 to minute 10, and its diag has the BlackHoleRegistry.LateUpdate row
- **Who can take it:** Yash
- **Added:** 2026-10-17 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md §2 (10-17 Stoat slowdown); Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-17 Stoat entry

### T-20261017-b227 · open · test · Vessel Studio
- **Task:** Playtest the ten concept vessels in the Vessel Studio artifact (hub row 'New vessels - the concept fleet'): fly each on phone and PC, watch its AI, and Record Keep / Drop with a line of why on each page's Decisions tab; then pick five, then three (Docs/Studios/CONCEPT_FLEET.md section 1).
- **Done when:** Every one of the ten pages has a Keep or Drop decision in the shared log and the owner has named the five and the three.
- **Who can take it:** Yash (asked for them; offered playtests), Garrett (product call on the three)
- **Added:** 2026-10-17 by Claude (concept fleet session) (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md; https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa

### T-20261017-3244 · open · dev · Vessel Studio
- **Task:** Give the picked concept vessels a clean AI difficulty ladder (Hard fastest on every course, D24): the shared belief mistakes barely cost these fast movers and the lab-only press delay punishes tempo mechanics (the Octopus's Easy DNFs; the Albatross's Easy loses the updraft). One AI round per kept vessel, scorecard before and after.
- **Done when:** Each kept vessel's Scorecard shows Hard < Medium < Easy on all four courses, recorded in CONCEPT_FLEET.md section 4.
- **Who can take it:** Yash (offered: follow-up fixes), any Claude session with /vessel-studio
- **Added:** 2026-10-17 by Claude (concept fleet session) (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 4; studio-kinds.js ai() per kind

### T-20261017-b999 · open · decision · Vessel Studio
- **Task:** Decide the concept fleet's three open design questions before any port: the Albatross's own gravity (a vessel that falls in a HyperSea without gravity), the Remora's compounding slipstream (two drafters ratchet each other to the cap) and whether a tow may take petals, and the Dragonfly / Marlin second input strategy (left stick = translation, RT = throttle).
- **Done when:** A decision per question recorded in the artifact's decision log and CONCEPT_FLEET.md section 6.
- **Who can take it:** Garrett (vessels, product); Yash
- **Added:** 2026-10-17 by Claude (concept fleet session) (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 6

### T-20261017-479c · open · test · Refactor
- **Task:** Settle whether Unity 6 binds a MonoBehaviour whose class name differs from its file name: open Assets/_Prefabs/Spacevessels/Squirrel.prefab (any vessel) and check the R_ShipElementStatsHandler component (file R_VesselElementStatsHandler.cs) shows its script and serialized values, not 'Missing script'. VesselStatus GetOrAdd()s it at runtime, which would hide a failed binding. 19 such mismatches exist, 13 of them on assets (Docs/REFACTOR_BACKLOG.md section 0, batch 2, M5).
- **Done when:** Result (binds / missing) recorded in Docs/REFACTOR_BACKLOG.md M5; if missing, a dev task to rename file or class is added.
- **Who can take it:** Yash (offered), anyone with the Unity Editor
- **Added:** 2026-10-17 by Yash (session) (`Ys-bleeding-edge`)
- **See:** Docs/REFACTOR_BACKLOG.md section 0 batch 2

### T-20261017-7915 · open · dev · Stoat
- **Task:** The Stoat's AI is paused while you configure the Stoat (Yash, 2026-10-17). When your configuration lands, say so here; the next AI session re-checks the autopilot distances against Regatta's race line, flies it and learns it (REGATTA_AI_ROADMAP.md §3.1 has where the AI stands)
- **Done when:** Garrett notes his Stoat configuration is done, and the AI follow-up is picked up
- **Who can take it:** Garrett (Stoat configuration)
- **Added:** 2026-10-17 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md §3.1

### T-20261017-0b70 · open · decision · Butterfly
- **Task:** Butterfly AI audit (BUTTERFLY.md §8a), two calls in your modes: (1) the AI cruises at 0.6 throttle (~45 u/s vs a person's 67) in Dustup, Sirocco and Tapestry - give those modes a full-throttle floor like Regatta and Waystation? (2) in Dustup and Sirocco the AI never fetches crystals, so its Space and Charge stay at level 0 while people level up - add Tapestry's crystal detour?
- **Done when:** Garrett decides both; the chosen change is made
- **Who can take it:** Garrett (Butterfly modes owner); Yash can make the change
- **Added:** 2026-10-17 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/BUTTERFLY.md §8a

### T-20261017-5cf2 · open · test · Butterfly
- **Task:** Play Waystation with AI Butterflies (Third Eye on one): each fold lands just short of a ring and threads it; the AI flies ~67 u/s; your own hull gains no AIPilot component; run WaystationFoldTests
- **Done when:** tests pass and the folds land in the rings
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-17 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (The AI Butterfly's fold lands in the ring)

### T-20261017-3c5d · open · dev · Vessel Studio / concept fleet
- **Task:** Round 2 of the concept fleet: one vessel per session in the owner's order (CONCEPT_FLEET.md section 8); the Gibbon's session merges the Tether prototype (section 8.1)
- **Done when:** each picked vessel has a studio round the owner recorded Keep on, and a kept vessel is ported with /artifact-to-unity
- **Who can take it:** Yash (sessions), Garrett (element sign-off)
- **Added:** 2026-10-17 by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 8

### T-20261017-e64b · open · dev · Vessel / Tether
- **Task:** Bring cece/loving-feynman-hlgf18 (the Tether prototype) onto Ys-bleeding-edge: Tether = 14 collides with Thresher = 14; take the next free id (re-check before claiming: 17 went to the Angler and the Kite's branch claims 19, so 18 as of 2026-10-18; 18 then went to the Goby and 19-23 are taken: 24 as of 2026-10-24), rewrite TETHER.md section 12, run check_network_prefab_hashes.py and the edit-mode suite
- **Done when:** the Tether flies from Toy Box > Vessel Changer on Ys-bleeding-edge with its own id and the suite green
- **Who can take it:** Yash or the Gibbon session; Garrett merged the branch's family
- **Added:** 2026-10-17 by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/TETHER.md on that branch; CONCEPT_FLEET.md section 8.1

### T-20261017-a640 · open · dev · Vessel Studio / Gibbon
- **Task:** Gibbon (+ the Tether; the notes' "Given" is the Gibbon): two modes from cece/loving-feynman-hlgf18: time mode = the branch's auto-tethers, space mode = two sticks + two triggers (one pair per tether), slicing by what the tethers cut, movement by 0 / 1 / 2 tethers attached; the time <-> space transition first
- **Done when:** Garrett flies the transition and calls it natural
- **Who can take it:** Yash (session 1 of round 2)
- **Added:** 2026-10-17 by Garrett's review 2026-10-17, recorded by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 9

### T-20261017-929b · open · dev · Vessel Studio / Flea
- **Task:** Give Garrett a branch with the Flea; if good, bring it into the game as a playable vessel, then a small racing game, then push to bleeding-edge
- **Done when:** Garrett has flown it from a branch; the Unity Flea flies from the Vessel Changer
- **Who can take it:** Yash
- **Added:** 2026-10-17 by Garrett's review 2026-10-17, recorded by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 9

### T-20261017-ba54 · open · dev · Vessel Studio / Albatross
- **Task:** Up/down from the nearest point on the nucleus (cytoplasm and nucleus), small strong thermals with an explosion, a membrane-crossing consequence that grows faster than linear with time avoided; qualitative before numbers
- **Done when:** Garrett flies it and the frame reads as diving toward / falling away from the nucleus
- **Who can take it:** Yash
- **Added:** 2026-10-17 by Garrett's review 2026-10-17, recorded by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 9

### T-20261017-3cc7 · open · dev · Vessel Studio / Dragonfly
- **Task:** Control swap (LT held: look stick becomes roll / elevation, no A/B), long-cooldown stored boosts along the current motion, and a precision kit: grasping joust that steals an element, and geometry mines (triangle / square / kite / pentagon / rhombus)
- **Done when:** Garrett flies the swap and one mine shape
- **Who can take it:** Yash
- **Added:** 2026-10-17 by Garrett's review 2026-10-17, recorded by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 9

### T-20261017-1bc9 · open · dev · Vessel Studio / Marlin
- **Task:** Pure drifter: never gets grip, less drift assist, the kit built around drifting; tune for fun
- **Done when:** Garrett flies it and still wants the character
- **Who can take it:** Yash
- **Added:** 2026-10-17 by Garrett's review 2026-10-17, recorded by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 9

### T-20261017-b900 · open · dev · Code review
- **Task:** Review Joseph Zhou's pull request when it lands (Unity crashed while he prepared it); approval is required before merge
- **Done when:** the PR is approved or sent back with notes
- **Who can take it:** Yash
- **Added:** 2026-10-17 by Garrett's review 2026-10-17, recorded by the concept-fleet session (`Ys-bleeding-edge`)
- **See:** Docs/Studios/CONCEPT_FLEET.md section 9

### T-20261017-0406 · open · decision · Amoebius
- **Task:** Decide PR #1045 (Prisma Unity Relay transport, 9 Oct, 1 commit, 817 behind bleeding-edge). bleeding-edge already has its core, built differently: Unity Relay protocol (Wire/Relay), UGS sign-in with per-profile session token (Wire/Ugs), START ONLINE through UGS Lobby + Relay (Shombith, #1089), one NetStack. #1045 is built on the shared-folder sessions and LAN fallback Shombith removed on 14 Oct. Only three things in it are missing on bleeding-edge: automatic region pick by UGS QoS (bleeding-edge only pins COSMIC_SHORE_RELAY_REGION), DTLS 1.2 via BouncyCastle (bleeding-edge uses plain UDP on purpose; a new dependency), and the 9 Oct LIVE run against real UGS Relay (2/2 passed, ~60 ms RTT, spawns/RPCs/NetVars synced; bleeding-edge's docs say nothing has talked to UGS's relay yet). Proposed plan: merge bleeding-edge into prisma-relay-transport taking Shombith's side everywhere, drop CosmicShore.Online / LAN fallback / --relay-* flags / RELAY.md, then port only the QoS region pick into Wire/Relay and record the live results in Port/docs/MULTIPLAYER.md; leave DTLS out. Or close #1045 as superseded.
- **Done when:** Yash and Shombith choose port-the-gaps or close; #1045 is then either a small PR on Shombith's design (QoS region pick + live evidence, engine tests and unity_isolation_check green) or closed with a comment naming what bleeding-edge replaced it with.
- **Who can take it:** Yash (author), Shombith (owns Amoebius networking)
- **Added:** 2026-10-17 by Yash's session (work report) (`Ys-bleeding-edge`)
- **See:** PR #1045 (prisma-relay-transport); Port/docs/MULTIPLAYER.md 6.7-6.10; Docs/TOOLING_DECISIONS.md 3

### T-20261017-d3b8 · open · decision · Tools
- **Task:** Decide PR #1030 (branch hygiene, 8 Oct, 9 commits, 1,105 behind bleeding-edge). None of it is on bleeding-edge and nobody else has built it: the Branch cleanup workflow (.github/workflows/branch-cleanup.yml + Tools/BranchJanitor/branch_cleanup.py, report / dry-run / archive / delete, tags archive/<branch> before deleting), policy.json, the browser Branch Janitor page, Docs/BranchArchive/ (one evidence doc per inactive branch) and a new Amoebius launcher BRANCHES page. The archive lists are stale (505 branches then, 545 on 2026-10-17), and a new launcher page is the growth Docs/TOOLING_DECISIONS.md asks to hold until the Amoebius decision. Options: (a) merge bleeding-edge in, keep the workflow, script, policy and docs, drop the BRANCHES page, regenerate the archive for today's branches; (b) keep it as is; (c) close it.
- **Done when:** Yash picks a, b or c; for (a) the PR is refreshed, the archive regenerated, and the workflow's report mode run once on the real repo.
- **Who can take it:** Yash (author); the core four for the launcher page
- **Added:** 2026-10-17 by Yash's session (work report) (`Ys-bleeding-edge`)
- **See:** PR #1030 (claude/trusting-curie-bn6rrn); Docs/TOOLING_DECISIONS.md 3

### T-20261024-bd25 · open · test · Vessel Studio / Flea
- **Task:** Fly the Unity Flea from branch claude/flea-vessel: Menu_Main freestyle, Toy Box > Vessel Changer > Flea; RT (Right Shift / LMB) timed to a prism contact is a perfect bounce, LT (Left Shift / RMB) crouches and hops
- **Done when:** The six steps in Docs/UNITY_VERIFICATION_CHECKLIST.md 'The Flea (vessel 24)' pass in the editor, or what failed is written down; then decide: racing game next, or tune first
- **Who can take it:** Garrett (Yash can run the checklist first)
- **Added:** 2026-10-24 by a Claude session (`Ys-bleeding-edge`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/FLEA.md on claude/flea-vessel; T-20261017-929b

### T-20261021-a2b3 · done · dev · Performance
- **Task:** Prism creation is per-prism polling. Every laid prism starts its own CreateBlockCoroutine, and while the per-frame budget is spent, each waiting prism is resumed every frame only to yield again. Under the menu veil the budget is a fixed 512 completions a frame. Measured in the second Orrery capture: 34,770 completions at 0.07 ms each (2.5 s of real work), metered over ~68+ frames. Measure first (the third capture, T-20261021-a96c, shows the settle with the grow-in snapped). If the creation queue is then the floor, replace the polling with one queue the gate drains on a time slice, so a waiting prism costs nothing per frame
- **Done when:** A capture shows the creation queue's share of a swap, and if it is the floor, a change that drains it from one queue on a time slice lands with a before/after capture
- **Who can take it:** Yash (handed over by Garrett, 2026-10-21)
- **Added:** 2026-10-21 by Garrett (Claude session) (`cece/relaxed-hopper-ns2hdu`)
- **See:** Docs/ECOSYSTEM.md §62.2b; Prism.CreateBlockCoroutine; Prism.MaxCreationCompletionsPerFrame / LoadGateCreationCompletionsPerFrame
- **Done:** 2026-10-24, Prism.CreationQueue replaces per-prism polling (Bloomrush: 56k coroutines/frame, 166 ms). Unity after-capture owed (Bloomrush, Release).

### T-20261024-803f · open · test · Performance
- **Task:** Re-capture Menu_Main after the QuestTrackView fix (Ys-bleeding-edge): Release, Third Eye closed, play Menu_Main (freestyle or the hub) past the 4-minute auto-capture and stop Play. Compare ClipperRegistry.Cull with the 10-19..23 captures (3.1-7.7 ms/frame; 0.26 ms on 10-16).
- **Done when:** A Menu_Main capture after the fix shows ClipperRegistry.Cull and CPU busy back near the 10-16 baseline, or names what still costs
- **Who can take it:** Yash, Caleb or Andrew (anyone with the Unity Editor)
- **Added:** 2026-10-24 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md §2 10-24

### T-20261024-1a3f · open · dev · Menu_Main UI
- **Task:** Menu_Main UI clipping cost since style-B (#1153): ClipperRegistry.Cull 3.1-7.7 ms/frame in every Menu_Main capture 10-19..23 (0.26 ms on 10-16); RectMask2D count 18 -> 59. Hidden screens and modals stay active (alpha 0), so any graphic that moves under a mask is re-clipped every frame. QuestTrackView's parallax was one such mover (fixed 10-24); find any other with a Release capture after that fix, and consider deactivating or disabling the masks of screens nobody can see (closed modals, off-strip screens, freestyle).
- **Done when:** Menu_Main CPU busy back near the 10-16 baseline (3.8 ms) in a Release capture
- **Who can take it:** Shombith (style-B menu)
- **Added:** 2026-10-24 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/MENU_REDESIGN_B.md §24; Docs/PERFORMANCE_OPTIMIZATION.md §2 10-24

### T-20261024-3084 · open · decision · Ecology
- **Task:** Bloomrush (and Rampage, same cactus forest) grows its ~480-plant arena DURING play: about 56,000 prisms wait for creation at the 90 s mark in both of Andrew's 10-20 captures, and at the gameplay budget (6 completions a frame for laid-world mass) the forest takes ~2.6 min to finish appearing. The per-frame cost of the wait is fixed (Prism.CreationQueue, 10-24); the growth rate is a design call: build the forest under the load gate (512/frame behind the connecting screen), or give environment mass a time slice in play.
- **Done when:** A decision recorded, and a Bloomrush capture shows the forest's backlog drained or bounded
- **Who can take it:** Garrett (ecology, arenas)
- **Added:** 2026-10-24 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md §2 10-24

### T-20261024-0212 · open · test · Performance
- **Task:** Re-capture Bloomrush after the prism creation queue (Ys-bleeding-edge): Release, Third Eye closed, Arcade > Bloomrush, play past the 90 s auto-capture, stop Play. Compare with the 10-20 captures (CPU busy 130-221 ms, Prism.CreateBlockCoroutine x56k/frame, CoroutinesDelayedCalls 139 ms).
- **Done when:** A Bloomrush capture after the fix with CoroutinesDelayedCalls near zero
- **Who can take it:** Andrew (has the Bloomrush captures), Yash, anyone with the Unity Editor
- **Added:** 2026-10-24 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-24 prism creation queue

### T-20261024-b06b · open · test · Multiplayer
- **Task:** Retest B35 (joins refused with 'NetworkConfig mismatch') on two machines on Ys-bleeding-edge: one hosts a party, the other direct-joins, then once more by accepting an invite. Then press Send to Claude (automatic) and run net_logs.py show 1.
- **Done when:** Both joins land in the party, the host logs no 'NetworkConfig mismatch', and net_logs.py show prints no 'NetworkConfig differs between players'.
- **Who can take it:** Caleb, Gradi, Yash (offered)
- **Added:** 2026-10-24 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/PartySystem/BUGS.md B35; Docs/UNITY_VERIFICATION_CHECKLIST.md 'Party: B35 fixed'

### T-20261024-7309 · open · dev · Amoebius
- **Task:** Amoebius's edit-mode test build (Tools/Build/prisma_edit_mode_tests) needed test-only gap-fills for engine APIs new game code uses: URP VolumeFrameworkUpdateMode + UniversalAdditionalCameraData.volumeStack/requiresVolumeFrameworkUpdate (OrthographicLens), TouchControl.startTime (PilotSwapGesture), Vector3.RotateTowards (PistolBodyMath), GravitySensor (TouchTilt), NetworkConfig.GetConfig + NetworkPrefabs.NetworkPrefabOverrideLinks (MultiplayerSetup, B35). The live engine build likely lacks them too; add them to the engine or confirm the gap-fill is enough.
- **Done when:** The engine declares these, or its owner decides the gap-fill stays; gapfill.py then reports them as present.
- **Who can take it:** Shombith (engine)
- **Added:** 2026-10-24 by a Claude session (`Ys-bleeding-edge`)
- **See:** Tools/Build/prisma_edit_mode_tests/gapfill.py

### T-20261024-2979 · open · test · Regatta AI
- **Task:** Race an AI Rhino in Regatta I1 and I3 (Third Eye on it) with the corner speed 333 and line 0.676 set from your six Rhino races: it should brake to about 330 u/s before sharp rings, fly about two thirds of the way out, and scrape rails less; note its times (before: about 45 s I1, 50 s I3; the offline model predicts slower)
- **Done when:** The UNITY_VERIFICATION_CHECKLIST entry for 2026-10-24 Rhino corner speed and line is ticked, with the I1 and I3 times
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-24 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Rhino corner speed and line, 2026-10-24); Assets/_Scripts/Controller/AI/AI_BOOST.md (Ramp)

### T-20261024-3e3c · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part A steps 3 and 9 on GitHub Actions: run Unity CI (tier static) on Ys-bleeding-edge and confirm the skimmer audit, AI/replay gate and generator loop ran green; then check the first Bleeding-edge guard run after A landed is green at every static step
- **Done when:** Both runs green with those steps present and not skipped; result noted in BRANCH_PARTS_STATUS.md
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-faca · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part A steps 4-6 in the Unity editor: Audit Vessel Skimmers vs audit_vessel_skimmers.py (6 faults), Validate Lifeform Crystals vs audit_lifeform_crystals.py, and an Inspector edit to SkimRaceAIConfig_I4.asset turning author_skimrace_ai_config.py --check to DRIFT (then git checkout the asset)
- **Done when:** All three steps match their PASS lines; mismatches pasted into DT-002
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-3d2f · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part E (AI training fixes, deployed tournament genome): run its editor steps, then push it onto Ys-bleeding-edge with pick_branch_parts.py build E
- **Done when:** Editor steps pass and E is pushed; discard any Archive/SessionState asset changes from the test session
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-e80a · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part F (Skim Race AI intensity-4 policy v2-i4): run its editor steps, then push it with pick_branch_parts.py build F
- **Done when:** Editor steps pass and F is pushed; Skim Race AI config assets not left modified
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-e462 · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part G (load-time target, unattended load sweep R7): after E has landed, run its editor steps, then push it with pick_branch_parts.py build G
- **Done when:** Editor steps pass and G is pushed (E and M already in)
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-d76a · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part H (parity harness, Unity side): run its editor steps (needs a throwaway returning-user account named exactly 'parity'), push H, then push part I (Port/ only) after it
- **Done when:** H's editor steps pass, H then I pushed as separate pushes
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-e520 · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part J (multiplayer QoL: Add Friend, recently played with, ready lights, dropping out, invite inbox): run its two-account editor steps, then push it with pick_branch_parts.py build J
- **Done when:** Two-account steps pass (or are marked 'needs two accounts'), J pushed
- **Who can take it:** Yash; a second UGS account / machine (Caleb or Andrew)
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-5cad · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part K (leak and lifetime fixes: effect cooldowns, score trackers, delayed tasks): run its editor steps, then push it with pick_branch_parts.py build K
- **Done when:** Editor steps pass and K is pushed
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-7c3d · open · test · Branch parts (nice-brahmagupta)
- **Task:** Part L (heritable genome, off by default): run its editor steps and get Garrett's sign-off on the LOCKED rule 'the genome is a lean, not a level' and on worm fission as a mutating birth; then push L
- **Done when:** Steps pass, Garrett's sign-off recorded, L pushed
- **Who can take it:** Yash (tests); Garrett (sign-off)
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-a306 · open · setup · Branch parts (nice-brahmagupta)
- **Task:** Part B (untrack FMOD's cache, PR template): ask the sound designer to open Cosmic Shore.fspro, settle the 'Squirel boost' track volumes, save, build and commit Metadata/ and Build/; then push B
- **Done when:** The sound designer's commit is on Ys-bleeding-edge and B is pushed
- **Who can take it:** Yash (push); the sound designer (FMOD save)
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-a0c4 · open · docs · Branch parts (nice-brahmagupta)
- **Task:** Part C step 10: get Shombith and Garrett to read the Steam Playtest runbook (A4 step 6, branch model, upload block, the name step, A6 template) and confirm they can execute each step
- **Done when:** Both confirm, or their corrections are filed
- **Who can take it:** Yash (chase); Shombith and Garrett (read)
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-eb42 · open · docs · Branch parts (nice-brahmagupta)
- **Task:** Part D (plans and docs) last: push it once the parts its QA items describe have landed, then run /qa-backlog once on bleeding-edge; when every part has landed or been dropped, delete the test plan, pick_branch_parts.py and BRANCH_PARTS_STATUS.md
- **Done when:** D pushed, QA backlog refreshed, the one-off plan files deleted
- **Who can take it:** Yash
- **Added:** 2026-10-24 by Claude session for Yash (`Ys-bleeding-edge`)
- **See:** Docs/BRANCH_PARTS_STATUS.md; Docs/BRANCH_TEST_PLAN_2026-10-12.md on cece/nice-brahmagupta-ojclij (part's steps block); push with pick_branch_parts.py from that branch, --base origin/Ys-bleeding-edge

### T-20261024-c9ba · open · test · Regatta AI
- **Task:** Race an AI Dolphin in Regatta I1 and I3 (Third Eye, trails on) with the line 0.785 set from your Dolphin race: it should fly about three quarters of the way out, never cross a rail, and still thread every ring after a drift; note its times
- **Done when:** The UNITY_VERIFICATION_CHECKLIST entry for 2026-10-24 Dolphin line is ticked, with the I1 and I3 times
- **Who can take it:** Yash (has offered to take tasks)
- **Added:** 2026-10-24 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Dolphin line, 2026-10-24); Assets/_Scripts/Controller/Arcade/REGATTA.md 6.2
