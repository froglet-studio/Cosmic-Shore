# Team tasks: archive

Done and archived tasks, moved out of [`TEAM_TASKS.md`](TEAM_TASKS.md) by `team_tasks.py sweep` (a task is archived when nobody takes it for 14 days, or by hand with `team_tasks.py archive`). `team_tasks.py revive <id>` brings an archived task back to the board.

<!-- team-tasks -->

### T-20261015-f4c7 · done · dev · Activation
- **Task:** Reconcile Menu_Main between the first-login guide (merged into Ys-bleeding-edge 2026-10-15) and Menu Redesign B (claude/new-session-76hnyc) before either reaches bleeding-edge: the spotlight must find the restyled ARCADE entry, cards and Settings gear.
- **Done when:** Both are on one branch, the spotlight walks the restyled menu in the editor, and author_first_login_guide.py --check passes.
- **Who can take it:** Shombith (Menu_Main owner), Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Docs/ACTIVATION.md 4; Docs/MENU_REDESIGN_B.md on claude/new-session-76hnyc
- **Progress (2026-10-17, claude/new-session-76hnyc):** both are on one branch now (bleeding-edge merged in), author_first_login_guide.py --check passes, and the guide's targets were checked offline: the ARCADE hub (MenuHubButton, target 10), the Arcade grid's ArcadeExploreView / TryRevealCard, Settings (MenuGuideAlwaysAvailable on the restyled SettingsButton, now guarded by author_menu_home.py), and the Preview step (now spotlights FLY via ModePreviewWindow.guideTarget, because the rev-3 preview is full-screen). Left: the in-editor walk (Docs/UNITY_VERIFICATION_CHECKLIST.md 'Bleeding-edge merge 2026-10-17').
- **Done:** 2026-10-17, Reconciled on claude/new-session-76hnyc after merging bleeding-edge #1189 (Ys-bleeding-edge): each guide step finds its style-B target and each is a generator gate with a planted defect - Home's Arcade hub keeps its MenuHubButton target ARCADE (author_menu_home.py), the Arcade grid (author_menu_arcade.py GameSelectionGrid), the preview's FLY GuideRect (author_menu_configure.py guideTarget), Settings carries MenuGuideAlwaysAvailable (author_menu_home.py); nothing in Menu_Main sorts above the spotlight (32000), so the friends drawer is dimmed too. Editor walk-through still owed: T-20261015-b47d.

### T-20261015-1a5b · done · dev · Performance
- **Task:** check_mathf_params_alloc.py is red on two sites that predate the activation work: BlackHoleWarp.cs:87 (3-value Mathf.Max) and FlipWave.cs:153 (4-value Mathf.Max), each allocating a params array.
- **Done when:** Both use MathfNoAlloc or nested two-value calls and the gate passes.
- **Who can take it:** the owners of the black hole and FlipWave code (check who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by Yash's session (activation) (`Ys-bleeding-edge`)
- **See:** Tools/Build/check_mathf_params_alloc.py

### T-20261015-949d · done · test · diagnostics
- **Task:** Test the Bug Hunt check-in and Team Bug Board in Unity: FrogletTools > Diagnostics > Bug Hunt Check-in, Share now; play with an error; daily sweep picks it up
- **Done when:** A check-in commit with BugLedger/shared/issues + reports lands on the branch, and the next sweep shows the issue on https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno
- **Who can take it:** Yash
- **Added:** 2026-10-15 by a Claude session (`Ys-bleeding-edge`)
- **See:** Docs/DIAGNOSTICS.md#the-bug-hunt-check-in
- **Done:** 2026-10-15, Superseded 2026-10-15: the Bug Ledger window, check-in and Stage & Push were removed; the one 'Send to Claude' question replaces them (new test task added).

### T-20261015-a5a5 · done · decision · Performance
- **Task:** The Charge light now sleeps when a crystal is under 24 px in radius on screen (CrystalChargeArcsProfileSO.sleepBelowScreenRadiusPixels; it rebuilt a mesh per crystal per frame and cost more than every other menu script together in the lava lamp). Check the look at the distance it wakes and pick the number; also worth deciding whether the arcs should rebuild at a lower rate mid-distance.
- **Done when:** Garrett has looked at it in the lava lamp and the field holds his number (generator and asset).
- **Who can take it:** Garrett (owns the crystals)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 2 (10-15 lava lamp row)
- **Done:** 2026-10-17, 2026-10-17, Yash took it over: kept 24 px (a bolt core is ~0.3 px there) and added halfRateBelowScreenRadiusPixels = 64 (alternate-frame rebuild between 24 and 64 px), in the profile, the asset and author_omni_crystal_triangles.py. Garrett may still retune both.

### T-20261015-fafa · done · dev · Performance
- **Task:** Pool the AOE explosions: ExplosionHelper.SpawnAllAndDetonate does Object.Instantiate(prefab) per detonation and AOEExplosion Destroys itself after (Projectiles/AOEExplosion.cs), so every crystal collect and every explodable projectile mints a GameObject tree. Measured in Amoebius (Skim Race I2, 2026-10-15): VesselImpactor.AcceptImpactee 5.6 KB/frame, all of it SquirrelVesselExplosionByCrystalEffect (the Squirrel's boost ring, ~300 KB per omni collect in .NET; the ring's prisms are the design, the un-pooled explosion object is the practice gap). A GenericPoolManager<AOEExplosion> per prefab (like PrismExplosionPoolManager) with Release instead of Destroy at the end of ExplodeAsync; keep the reset-for-replay cleanup. Go/no-go: a Unity Release capture's Instantiate / GC rows on a crystal collect.
- **Done when:** AOE explosions come from a pool; perf_hunt.py amoebius shows VesselImpactor.AcceptImpactee under 1 KB/frame on Skim Race I2 and the baseline is adopted; a Release capture of Skim Race shows no Instantiate on a crystal collect.
- **Who can take it:** Garrett (projectiles / the Squirrel's ring), Yash (offered)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 3.3 (L13) and 2 (10-15 rows)
- **Done:** 2026-10-17, 2026-10-17, Yash's session: AOEExplosionPool (opt-in, on for the Squirrel ring spawner: 1 miss / 7 reuses per 8 collects). The real allocator was CrystalMorphMeshBuilder.TryBuild (5.22 of 5.53 KB/frame); its per-source emit cache brought VesselImpactor.AcceptImpactee 5.53 -> 3.41 KB/frame, under the 3.83 baseline (rest is Amoebius's Mesh.Set* copy). Unity check: T-20261017-c82c / Docs/UNITY_VERIFICATION_CHECKLIST.md 2026-10-17.

### T-20261015-aed4 · done · test · Activation
- **Task:** First-login guide Skip and every-Lesson Skip on claude/new-session-76hnyc: SKIP (and pad Y) ends the quest for good; Lessons offer Skip on a fresh profile
- **Done when:** Steps 1-2 of the checklist entry 'Style-B playtest fixes' pass in the editor
- **Who can take it:** Yash (offered; FTUE owner)
- **Added:** 2026-10-15 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md section 28
- **Done:** 2026-10-15, Superseded: the owner switched the tutorial off (MainQuest questEnabled 0, DrillLibrary tutorialsEnabled 0), MENU_REDESIGN_B section 29

### T-20261016-1ec1 · done · dev · Menu_Main / Hangar
- **Task:** Real ability playback in the Hangar (Sparrow actually fires on RT): host a Barren-cell ModePreviewSession behind the Abilities tab instead of the stand-in lunge/roll/light
- **Done when:** Pressing an ability's control in the Hangar plays the vessel's real ability
- **Who can take it:** Shombith or Yash (decide if wanted)
- **Added:** 2026-10-16 by Claude for Shombith (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md §31, Docs/ModePreview/ARCHITECTURE.md
- **Done:** 2026-10-17, Built on claude/new-session-76hnyc (Docs/MENU_REDESIGN_B.md section 35): TRY IT FOR REAL hosts a Barren-cell ModePreviewSession behind the Abilities tab. The editor test is T-20261016-be3d + the checklist entry 'Hangar TRY'.
- **Done:** 2026-10-17, Done on claude/new-session-76hnyc: TRY (section 35) flies it, and the showcase (section 51, cc061ef9a) plays each real ability under the autopilot; hand test T-20261017-13fb

### T-20261016-7044 · done · test · Menu_Main / Configure
- **Task:** Play-test the rev-3 Configure window (full-window live preview + step cards; FLY / STOP; pad focus; Arena hulls; Maelstrom pool) on PC, pad and a phone
- **Done when:** Docs/UNITY_VERIFICATION_CHECKLIST.md 'Configure rev 3' items 1-5 pass or are filed as bugs
- **Who can take it:** Shombith (Menu_Main); Yash has offered to take tasks
- **Added:** 2026-10-16 by claude/new-session-76hnyc (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md section 33
- **Done:** 2026-10-17, Superseded: the rev-3 Configure (full-window preview + step cards) was replaced by Rev 4 simple on 2026-10-17 (Docs/MENU_REDESIGN_B.md §43); its playtest is T-20261017-1ec4.

### T-20261015-641c · done · decision · Performance
- **Task:** Spindles are the last big GameObject-renderer cost in the lava lamp (lever L1): one Renderer per limb, ~40k enabled in a grown Lattice with ~4.6k visible, +5.3 ms CPU measured in S2 (record 3.3). Every Prism is already on entities (record 1.2); spindles are not prisms. Decide the shape: draw spindles through Entities Graphics like prisms, or merge a plant's spindles into one mesh per plant, keeping the per-spindle wither fade (continuity law). Ecology is LOCKED, so this is yours to call before anyone builds it.
- **Done when:** Garrett has picked an approach (or ruled L1 out) and recorded it in Docs/PERFORMANCE_OPTIMIZATION.md 3.3; a build task follows if he picks one.
- **Who can take it:** Garrett (ecology / flora owner)
- **Added:** 2026-10-15 by the performance session (perf/performance-optimization) (`perf/performance-optimization`)
- **See:** Docs/PERFORMANCE_OPTIMIZATION.md 1.2 and 3.3 (L1); Docs/Reports/PERFORMANCE.md, Waiting on Garrett
- **Done:** 2026-10-17, 2026-10-17, Yash's call: measured before building. A grown Lattice holds 2,898 spindles at 8 min in Amoebius (not ~40k); a sub-pixel render LOD hid 0 of them (and 0 in Garland), so it was removed. No structural build until a Unity S2 renderer census (T-20261015-56dd) shows the count is still large. Docs/PERFORMANCE_OPTIMIZATION.md 3.3 L1.

### T-20261015-6009 · done · test · Regatta AI
- **Task:** Teach the Regatta Squirrel AI from your flying: Third Eye > Record on, fly Regatta as the Squirrel by hand 5 times to the finish, stop Play and Send to Claude; then ask a session to run learn_from_flights.py --write and test the AI
- **Done when:** LEARNED_FROM_FLIGHTS.md lists your races and the Squirrel AI flies the learned line without striking its rail
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Claude for Yash (`Ys-bleeding-edge`)
- **See:** Docs/SKIM_RACE_AI.md §16
- **Done:** 2026-10-16

### T-20261015-1188 · done · test · AI / training
- **Task:** Fly 3-5 Regatta races by hand (Squirrel first) in the Unity EDITOR (training data comes from the editor only, 2026-10-16) (each record is stored by itself when Play stops)
- **Done when:** `python3 Tools/Build/situation_features.py --write` reads them (flight/3) and writes learned_situations.json for the hull
- **Who can take it:** Yash
- **Added:** 2026-10-15 by Claude (Yash's session) (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md
- **Done:** 2026-10-16

### T-20261015-d7f8 · done · dev · Performance
- **Task:** Allocation regression in Amoebius SkimRace|I2|3p: VesselImpactor.AcceptImpactee allocates 3.831 -> 5.578 KB/frame (+46%)
- **Done when:** perf_hunt.py amoebius reads within baseline for that marker
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md
- **Done:** 2026-10-15, Duplicate of T-20261015-fafa: attributed with nested markers to the un-pooled AOE ring explosion on each omni collect (lever L13, Garrett).

### T-20261015-c8e2 · done · dev · Performance
- **Task:** Allocation regression in Amoebius SkimRace|I2|3p: PoolRefill.FastGrowPrism allocates 0.526 -> 0.7 KB/frame (+33%)
- **Done when:** perf_hunt.py amoebius reads within baseline for that marker
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md
- **Done:** 2026-10-17, 2026-10-17: not a code regression. The baseline commit (574e0d39a) itself re-measures 0.766 KB/frame today; the refill tracks boost-prism lays 1:1 at ~20 KB each in .NET (net new ring mass), so it moves with how many rings the AIs lay. Current build 0.693 (repeats to 0.3%); the Skim Race Amoebius baseline was re-adopted from that run with this evidence (Docs/PERFORMANCE_OPTIMIZATION.md 2, 10-17 pool row).

### T-20261015-c7db · done · dev · Performance
- **Task:** Allocation regression in Amoebius SkimRace|I2|3p: new allocating marker PoolRefill.PrismImplosion: 0.122 KB/frame
- **Done when:** perf_hunt.py amoebius reads within baseline for that marker
- **Who can take it:** the owner of the files it names (who_works_where.py); Yash (offered)
- **Added:** 2026-10-15 by perf_hunt sweep (`perf/performance-optimization`)
- **See:** Docs/Reports/PERFORMANCE.md
- **Done:** 2026-10-17, 2026-10-17: fixed. The implosion pool topped its buffer up to 160 although death implosions are batched since D4 and only the Sparrow's ReverseSuction Get()s it; PrismManagers.prefab bufferSizeTarget 160 -> 24. PoolRefill.PrismImplosion 0.157 (baseline commit) / 0.122 -> 0.000 KB/frame. Same prefab: the explosion pool (never Get()) no longer prewarms 1,500 objects at boot.

### T-20261015-7392 · done · dev · AI training
- **Task:** Add the pilot's VIEW to the situation recording: record the camera pose/FOV each sample (V1), compute on-screen / occluded / time-to-contact view features and a coarse view grid (V2), gate the target and skipped-skim labels on what was visible (V3), measure the per-pilot reaction lag (V4)
- **Done when:** flight/3 (or /4) carries the camera; situation_features --self-test passes with the view group; the skim rule is fit only on skims the pilot could see; learn_from_flights reports a measured lag
- **Who can take it:** Yash (his AI-training session); Garrett for V4's handicap numbers
- **Added:** 2026-10-15 by Caleb (`claude/friendly-faraday-qropbs`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §7
- **Done:** 2026-10-16, V1-V4 built on Ys-bleeding-edge 2026-10-16 (Yash's session): camera in flight/3, view group + grid, labels gated on what was seen, measured reaction lag reported by both learners; situation_features --self-test covers each with a negative control. Editor check is T-20261015-0576 (checklist step 2b); V5 not started.

### T-20261016-0eb3 · done · decision · Regatta AI
- **Task:** Two decisions for the Regatta AIs: (1) put the Stoat on the Regatta card (it is one of the roadmap's six but cannot be flown in Regatta, so its AI cannot learn from flights); (2) cece/relaxed-hopper-r62hbd carries a second, older RampBoostAIPolicySO for the Rhino than Ys-bleeding-edge's - pick one before it merges
- **Done when:** Both answered in Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md
- **Who can take it:** Yash with Garrett (Stoat and Rhino owner sessions)
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9
- **Done:** 2026-10-16, (1) Yash put the Stoat on the Regatta card (2026-10-17, author_regatta_assets.py); (2) the Rhino ramp policy question moved to its own task.

### T-20261017-147f · done · test · Menu_Main / Hangar
- **Task:** Play-test the Hangar's TRY IT FOR REAL (Abilities tab): the selected vessel flown for real in open water, its abilities firing on the real controls, the drill coach; START / Esc / STOP back; no screen paging or tab switch while flying
- **Done when:** Docs/UNITY_VERIFICATION_CHECKLIST.md 'Hangar TRY' items 1-4 pass or are filed as bugs
- **Who can take it:** Shombith (Menu_Main); Yash has offered to take tasks
- **Added:** 2026-10-17 by claude/new-session-76hnyc (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md section 35
- **Done:** 2026-10-19, Superseded 2026-10-19: TRY was removed; the owner asked for an AI-only, very low-cost Hangar preview (MENU_REDESIGN_B §59).

### T-20261017-b06e · done · decision · Crew vessels
- **Task:** Decide how the Kite Navigator seat goes online: today the board, motes, AI and every effect run on the hull's local human's machine. A second human on another machine needs the seat replicated, and a choice of which machine decides pops, blasts, Mass clusters, boost and steals (vessel rule 48). Also decide the arena case: the seat's swap uses PilotSwapGesture, which in an arena match also asks the arena to swap hulls while the human flies.
- **Done when:** A written plan in KITE_NAVIGATOR.md that names the deciding machine per effect and the gesture owner in arenas
- **Who can take it:** Garrett with Yash (party / multiplayer)
- **Added:** 2026-10-17 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md (Follow-ups)
- **Done:** 2026-10-21, Decided by Garrett, 2026-10-21: "wrasse and grouper, are two seats. players can select, change seats etc. both can be played on mobile or pc." Two human seats on any mix of phones and PCs: the hull is owned by the pilot seat's machine, the navigator seat is simulated on its holder's machine and sends its decided effects through a server-checked relay; the seat consumes the swap gesture on a crew hull (seats first, then the team ring). Plan and work order: KITE_NAVIGATOR.md "Two humans, two seats (plan, 2026-10-21)".

### T-20261016-92b0 · done · decision · Regatta AI
- **Task:** cece/relaxed-hopper-r62hbd carries a second, older RampBoostAIPolicySO for the Rhino than bleeding-edge's - pick one before it merges (learn_drives_from_flights.py writes only fields both share, so either works)
- **Done when:** The choice is recorded in Docs/AI_SYSTEM/REGATTA_AI_ROADMAP.md and the other branch is reconciled
- **Who can take it:** Yash with Garrett (Rhino owner sessions)
- **Added:** 2026-10-16 by Yash's session (`Ys-bleeding-edge`)
- **See:** Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md §9
- **Done:** 2026-10-16, Yash 2026-10-17: no decision step; the Ys-bleeding-edge RampBoostAIPolicySO was rebuilt (brake on the ramp, corner speed, payback) and took the re-arm + explicit end from the cece/relaxed-hopper-r62hbd cut. When that branch merges, keep this file's states and that branch's executor changes (AI_BOOST.md).
- **Done:** 2026-10-18, merge 5b09c6896 took bleeding-edge's RampBoostAIPolicySO wholesale (a superset of this branch's re-arm + End), Rhino.prefab points at its asset guid ece63fe0fab446188650420e8b018690

### T-20261016-822d · done · dev · Multiplayer
- **Task:** Find and fix what the first real multiplayer logs showed: B31 (the party session refresh is rate-limited about every 10 s while a party exists), B32 (after a guest leaves mid-match, the host's party lobby answers NotInLobby and the host sits in Reconnecting) and B33 (in the editor, a second Play starts inside the first Play's dead party session, because the project skips the domain reload).
- **Done when:** Each of B31-B33 in Docs/PartySystem/BUGS.md is root-caused and fixed, or closed with the reason, and a new two-machine log (net_logs.py show) shows it gone
- **Who can take it:** Yash (party owner)
- **Added:** 2026-10-16 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/PartySystem/BUGS.md B31-B33; Caleb's logs on bugledger/belac (python3 Tools/Build/net_logs.py collect)
- **Done:** 2026-10-17, Root-caused and fixed 2026-10-17 (B31 GET budget, B32 presence leave deleted the shared lobby + stuck Reconnecting, B33 GameDataSO session carried across Plays); edit-mode tests on Amoebius. The proof by log is the retest task.

### T-20261016-c76e · done · test · Multiplayer
- **Task:** Two-machine repro for B32 with both logs: host and guest party up on two PCs, start a Regatta match, the guest leaves mid-race, then the host waits 2 minutes in the menu and stops Play. Both editors send their logs by themselves.
- **Done when:** net_logs.py collect shows ONE test with both machines (host/main and guest/main), and the host's timeline says whether it left Reconnecting
- **Who can take it:** Caleb, Yash (offered)
- **Added:** 2026-10-16 by Yash (`Ys-bleeding-edge`)
- **See:** Docs/PartySystem/BUGS.md B32; Docs/UNITY_VERIFICATION_CHECKLIST.md 'Multiplayer test logs' steps 7-9
- **Done:** 2026-10-17, Folded into the B31-B33 retest task, whose step 3 is this repro with the fix in place.

### T-20261017-13fb · done · test · Menu_Main / Hangar
- **Task:** Play-test the Hangar ability showcase: press an ability and watch the vessel fire each of its real abilities in turn (autopilot), jump with another press, TRY to take the stick, START/Esc to end; list any ability that shows nothing
- **Done when:** All six checklist steps pass, or each failure is reported back with the vessel and ability
- **Who can take it:** Shombith (Menu_Main); Yash (offered)
- **Added:** 2026-10-17 by Claude session (Shombith) (`claude/new-session-76hnyc`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (2026-10-18 Hangar ability showcase); Docs/MENU_REDESIGN_B.md section 51
- **Done:** 2026-10-19, Superseded 2026-10-19: the flown showcase was removed; replaced by the AI demo on the stage (MENU_REDESIGN_B §59).

### T-20261017-9025 · done · test · Vessel models
- **Task:** Open branch ccr-d93a6dfb-ugbxng in the Editor after the seven vessel models were rebuilt in place by /vessel-model (2026-10-17): let the FBX files reimport with no console warning, open the Squirrel / Dolphin / Sparrow / Serpent / Manta (and Falcon, Shrike, Termite) prefabs, run FrogletTools > Vessels > Audits > Audit Vessel Elemental Morphs (four REAL shapes on each live file), Audit Vessel Construction and Audit Vessel Tails and Jets, re-bake Crystal Hull Fusions for the Squirrel, Dolphin and Manta, then fly each hull in freestyle: pitch / yaw / roll / throttle move the whole hull, the domain colour lands on the domain slot, glass and engine mouths read, each element morph is nameable
- **Done when:** (2026-10-18: scope is now the SQUIRREL only; the other six files are the artist's again) the Squirrel checks in Docs/UNITY_VERIFICATION_CHECKLIST.md's 2026-10-18 paragraph (round 8) are ticked - every take moves like the artist's Squirrel (rear engines included, roll banks the whole wing), the spine follows the chevrons, Mass shows - or the failing step is written there
- **Who can take it:** Yash (offered), Andrew, anyone with the Unity Editor; Garrett judges the look
- **Added:** 2026-10-17 by Claude session for Garrett, 2026-10-17 (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md; Docs/VesselModels/README.md; Docs/VesselModels/sheets/ (before/after)
- **Done:** 2026-10-18, Garrett, 2026-10-18: the round-8 Squirrel passes in the Editor (elements upgraded, domain changed, indistinguishable from the original); the other six vessels are the artist's files, unchanged

### T-20261017-3b6b · done · dev · Vessel models
- **Task:** Swap the Urchin and Rhino prefabs from their part-per-mesh placeholders (Urchan_Test.fbx, Rhino_Test.fbx) onto the rebuilt rigs urchan_shapekey_with_animations.fbx and rhino_shapekey_with_animations.fbx with FrogletTools > Vessels > Wiring > Swap Vessel Rig, which moves the geometry, remaps the materials and makes the animation rig-safe (Docs/VESSEL_CONSTRUCTION.md); the rigs now carry four REAL element morphs where the artist rigs had none
- **Done when:** Both prefabs draw one SkinnedMeshRenderer from the rig file, Audit Vessel Elemental Morphs reports four REAL shapes on each, the jets and colliders sit where they did, and a flight in freestyle shows the puppetry (UrchinAnimation / RhinoAnimation resolve their parts by bone name)
- **Who can take it:** Garrett (Urchin and Rhino owner), Yash (offered)
- **Added:** 2026-10-17 by Claude session for Garrett, 2026-10-17 (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/Urchin.md section 8; Docs/VesselModels/Rhino.md section 8; .claude/skills/vessel-model/SKILL.md section 6
- **Done:** 2026-10-18, 2026-10-18 fleet pass: both prefabs now draw the artist rigs with four real morphs (specs/urchin.py, specs/rhino.py), done offline without the swapper; the bones follow the placeholder parts, which keep the colliders. Editor test is the new Urchin/Rhino task.

### T-20261017-eecd · done · decision · Vessel models
- **Task:** Judge the seven rebuilt vessel models against the artist files on the before/after contact sheets in Docs/VesselModels/sheets/ (and in the Editor after the reimport task): for each hull say keep, iterate (name the three biggest faults) or revert the file (git checkout the FBX restores the artist file; the .meta is untouched). Also say whether the C# procedural hulls (Scarab, Stoat, Thresher, Butterfly, Brittle Star HullForm/HullBuilder) should be replaced by /vessel-model specs next
- **Done when:** A line per hull in Docs/VesselModels/README.md or in the session that iterates them
- **Who can take it:** Garrett
- **Added:** 2026-10-17 by Claude session for Garrett, 2026-10-17 (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/sheets/; .claude/skills/vessel-model/references/LESSONS.md
- **Done:** 2026-10-18, Superseded: the 2026-10-17 rebuilds were withdrawn; the fleet pass (2026-10-18) reproduced or replaced every hull; the judging is now the per-vessel flight tasks.

### T-20261018-be55 · done · decision · Vessel colour
- **Task:** Decide whether Jade's ship colours move from azure (hue 209, next to the neutral Blue windows and body at 220-232) to the teal of the palette's other Jade entries (about 177-182), and whether Gold's inverted rim/face (bright edge, dark face; Ruby and Jade run dark edge to bright face) should match the others. The UI reads the same ShipColor1/2, so it is a palette change, not a vessel one.
- **Done when:** OriginalColorSetSO JadeColors/GoldColors updated (or kept) and the decision recorded in Docs/VesselModels/COLOR.md section 6
- **Who can take it:** Garrett (colour language owner)
- **Added:** 2026-10-18 by Garrett's session (`ccr-d93a6dfb-ugbxng`)
- **See:** Docs/VesselModels/COLOR.md section 6
- **Done:** 2026-10-18, Gold fixed 2026-10-18 (rim/face swapped in OriginalColorSetSO and the unused Yellow materials, a data-entry mistake: DomainScorePanel reads ShipColor1 as the muted tone). Jade and Ruby hues deferred by Garrett; reopen as a new task when he takes them up.

### T-20261018-49d0 · done · content · Crew vessels
- **Task:** Give the Wrasse its own HUD. It still wears the Sparrow's (SparrowHUDController + SparrowHUDVariant): the four-icon ability row shows the Sparrow's gun, missile and barrel-roll icons, though the Wrasse has none of those since round 4c. Its four icons should be the kite's four loop effects (Space blast, Mass cluster, Time boost, Charge ram), each with its L5 upgrade (LOCKED four-icon row). Also give it its own ElementalAbilityMap entries (Wrasse.asset is still the Sparrow's map, kept only for the L5 unlock levels).
- **Done when:** The Wrasse's HUD shows its own four icons and no Sparrow ability
- **Who can take it:** Garrett (vessels, icons); any session for the wiring
- **Added:** 2026-10-18 by Claude (cece/compassionate-babbage-gc13io) (`cece/compassionate-babbage-gc13io`)
- **See:** Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md (Round 4c); Docs/claude/VESSEL_HUD_AND_ELEMENTS.md (four-icon row)
- **Done:** 2026-10-18, Round 4d: WrasseHUDController drives the four cards (saved boosts, rams, last blast, Mass flash) and Wrasse.asset has the Wrasse's own cards with no button chips. The icon ART is still the Sparrow's: see the follow-up task.

### T-20261018-7c2d · done · decision · Crew vessels
- **Task:** Decide whether the Pistol's view keeps its flat marks (primed/hot/held rings round prisms, the aim ring and line, the contact dot, the pops) as the view's own markings, or makes them lit volumes like the vortex ring
- **Done when:** Garrett's call is recorded in .claude/skills/duo-vessel/references/designs/goby-and-pistol.md (decisions log) and, if they stay flat, as an owner's-call exception under 'Nothing in the world looks flat' in Docs/claude/SHADERS_AND_PERFORMANCE.md
- **Who can take it:** Garrett (the rule's and the vessel's owner)
- **Added:** 2026-10-18 by Claude session on cece/sharp-tesla-j0kd7z (`cece/sharp-tesla-j0kd7z`)
- **See:** .claude/skills/duo-vessel/references/designs/goby-and-pistol.md; Docs/claude/SHADERS_AND_PERFORMANCE.md
- **Done:** 2026-10-21, Answered by Garrett's LOCKED rule of 2026-10-21 (SHADERS_AND_PERFORMANCE.md 'In the world, not on the screen'): no flat marks. The lab (v14 of the prototype) lights primed / hot / held / ghost-cue prisms and what an aimed fling will break on the prisms themselves, lights the trail tile under a falling Pistol instead of the contact dot, and draws flares as small lit volumes; the aim's dotted arc stays as the aim mark. Unity's PistolWarp still draws the rings until the clone (T-20261018-675f).

### T-20261019-382a · done · test · Crew vessels
- **Task:** On an Android development build of cece/charming-wright-4ayoce, run the 'Phone round' entry in Docs/UNITY_VERIFICATION_CHECKLIST.md: tap-to-press ability cards on Squirrel, Sparrow, Dolphin, Rhino; the Wrasse's kite, tail and motes drawing; double-tap the small view to swap seats; a card tap replicating to a second phone
- **Done when:** All six steps pass on a phone, or each failure is filed as a dev task
- **Who can take it:** Garrett (asked for it); Yash can take it (two phones for step 6)
- **Added:** 2026-10-19 by a Claude session (`cece/charming-wright-4ayoce`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md Phone round; Docs/ABILITY_LOCKUP.md Tap a card
- **Done:** 2026-10-20, Garrett, 2026-10-20: the phone test passed (Phone round, Android development build)

### T-20261020-50d2 · done · decision · Crew vessels
- **Task:** Pick the four-seat crew vessel concept from the Crew of Four Lab round 1 (Galleon, Corsair, Lantern, Ironclad, or a mix) and rule on its open questions: should your own ward block your guns (the shield-drop gamble), should boarders work under the ward, and does the cast-off (a defender takes the bag back and the boarder's own recall fires) count as petals-only under the LOCKED PvP rule.
- **Done when:** A decision is recorded on the lab's Decisions tab (shared log) and copied into Docs/Studios/CREW_OF_FOUR_LAB.md
- **Who can take it:** Garrett (vessels, product decisions)
- **Added:** 2026-10-20 by Claude session cece/happy-noether-m54hz6 (`cece/happy-noether-m54hz6`)
- **See:** https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb, Docs/Studios/CREW_OF_FOUR_LAB.md
- **Done:** 2026-10-20, Garrett chose in round 3 (2026-10-20): one Helm and three unified gunner/skiff seats, always four crew; no Artifact. The ward/boarding/cast-off questions no longer apply. Round 3's open questions are in T-20261020 (new decision task) and Docs/Studios/CREW_OF_FOUR_LAB.md §4.

### T-20261020-83ca · done · decision · vessels
- **Task:** Silence HUD readout: the silenced pilot has no HUD signal today (hits just deflect). The cooldown veil is the wrong read because every ability still works. Pick a look for the ability row or the vessel; VesselSilence.RemainingSeconds / Remaining01 / Changed are ready to drive it
- **Done when:** A readout is chosen and a dev task is filed for it
- **Who can take it:** Garrett (vessel HUD owner)
- **Added:** 2026-10-20 by Garrett (`cece/sleepy-shannon-9qk646`)
- **See:** Docs/ElementalAbilitySystem/SILENCE.md
- **Done:** 2026-10-20, Garrett chose it 2026-10-20: a very subtle onset camera shake plus a subtle danger-coloured vignette for the window, on every hull. Built as SilenceScreenFeedback (cece/sleepy-shannon-9qk646); its editor check is step 6 of T-20261020-1ffa.

### T-20261021-7e2e · done · decision · vessels
- **Task:** Pick the silenced pilot's screen-edge look in the Silence Vignette Lab (https://claude.ai/artifact/Hequw1VLdwXi2ztrDeuXQC): the shipped edge is overtuned on purpose (strength 0.75); compare Ember, Faceted window, Prism rim, Hard frame, Breathing over the Dog Fight scenes, log the pick in Decisions, and the next session writes its To-Unity lines into VesselSilenceConfig.asset
- **Done when:** a pick is logged in the lab's Decisions and VesselSilenceConfig.asset carries it
- **Who can take it:** Garrett (art direction)
- **Added:** 2026-10-21 by Garrett's session (cece/sleepy-shannon-9qk646) (`cece/sleepy-shannon-9qk646`)
- **See:** Docs/Studios/SILENCE_VIGNETTE_LAB.md
- **Done:** 2026-10-21, 2026-10-21, Garrett picked round 4 of the Silence Vignette Lab after trying it ('its great'): the thin pull + danger frame that deepens with the seconds of silence left. It is the shipped VesselSilenceConfig.asset (cece/sleepy-shannon-9qk646).

### T-20261020-a3c2 · done · decision · card-art
- **Task:** Card-art renders draw what a generator emits, not what PrismClearance lays (indistinguishable at card size today). Decide whether the card harness should apply the pass
- **Done when:** Either card_art_harness applies PrismClearance as prism_clip_audit/Audit.cs Hook does, or a note in the /cardart skill says why it does not
- **Who can take it:** Caleb
- **Added:** 2026-10-20 by Garrett (Claude session) (`cece/relaxed-hopper-ns2hdu`)
- **See:** Tools/Build/prism_clip_audit/Audit.cs (ClipAudit.Hook); Tools/Build/card_art_harness/PlatformShim.cs
- **Done:** 2026-10-20, Moot: the runtime PrismClearance pass was removed (Garrett, 2026-10-20: fix clipping at authoring time). The card art draws what the generators emit, which is now what the game lays.

### T-20261020-8f9d · done · decision · ecology
- **Task:** Accept or reject the authoring-time load cost of the flush fits, measured per build in the .NET 8 harness (IL2CPP on a phone will be slower). Atlantis +90 ms (~0.45 s total), Yggdra +50 ms, Caldera 14 -> 40 ms, Zephyr 12.5 -> 20 ms, Hesperides 3.7 -> 13 ms, Daedala and Geode 6.6 -> 13 ms, Cleave +0.4 to +3 ms; the Orrery and Ourobor are unmeasured. The fits run inside the generator once per build, which is not a runtime pass. Baking the fitted lays into an asset would make the cost zero
- **Done when:** A choice is recorded; if baking is chosen, a task exists for it
- **Who can take it:** Garrett
- **Added:** 2026-10-20 by Garrett (Claude session) (`cece/relaxed-hopper-ns2hdu`)
- **See:** Docs/ECOSYSTEM.md §62.3
- **Done:** 2026-10-20, 2026-10-20, Garrett chose baking ("bake the fitted layouts into assets so load cost is zero"). Done on cece/relaxed-hopper-ns2hdu: the 22 cell-environment prefabs load from offline bakes; live generation measured 3.8-289 ms per world (1.09 s across all 22) becomes 0.2-3.4 ms of decoding (22 ms in all). Hand test: the new PrismLayoutBakeTests task. See Docs/ECOSYSTEM.md §62.2b

### T-20261021-ffcf · done · test · ecology
- **Task:** Measure where a world swap's time goes. In the Editor open FrogletTools > Performance > Performance Benchmark, tab Load Time Insights, and arm Record Insight Mode. Enter Play in Menu_Main and use the Cell Selector to swap to the Orrery (then Atlantis, if you can). Each swap now records like a game load: spans for the suction, the release of the old world, the layout bake, the streamed lay and the settle (creation + grow-in), ending when the veil lifts. Press Copy on the report and paste it into a Claude session
- **Done when:** A Load Time Insights report of an Orrery swap is pasted into a session, which names the stage that dominates and fixes that stage
- **Who can take it:** Garrett, or Yash (offered)
- **Added:** 2026-10-21 by Garrett (Claude session) (`cece/relaxed-hopper-ns2hdu`)
- **See:** Docs/ECOSYSTEM.md §62.2b (What the bake does NOT speed up); Cell.SwapCellConfigRoutine
- **Done:** 2026-10-21, 2026-10-21, Garrett's capture: Orrery swap 21.3 s, streamed lay 16.1 s of it with 0.8 s of per-prism work (frame-bound: one 256-prism async clone batch per frame). Fixed on cece/relaxed-hopper-ns2hdu: 2,048-prism batches and a raised integration slice while the gate holds; the re-capture is the next test task on this board

### T-20261021-8321 · done · test · ecology
- **Task:** Re-capture the Orrery swap after the lay fix, exactly as before: Load Time Insights armed, Menu_Main, Cell Selector to the Orrery, Copy the report into a session. The last capture was 21.3 s (lay 16.1 s, settle 2.8 s, release 1.4 s). The lay now clones 2,048 prisms per batch behind the veil, so it should drop by several seconds. Watch for: the settle growing (creation still completes 512 per frame), and the new rows for the clone wait, reused vs new prisms, creation completions, the trail return, and prisms with no render entity at suction. Also say whether the world looks right after the veil lifts
- **Done when:** A new Orrery swap report is in a session; the lay span is clearly shorter than 16.1 s and the world looks as before; the next stage to fix is named from it
- **Who can take it:** Garrett, or Yash (offered)
- **Added:** 2026-10-21 by Garrett (Claude session) (`cece/relaxed-hopper-ns2hdu`)
- **See:** Docs/ECOSYSTEM.md §62.2b (Measured); PrismTrailBuilder.LoadGateCloneBatchSize
- **Done:** 2026-10-21, 2026-10-21, Garrett's second capture: Orrery swap 21.3 -> 13.9 s (lay 16.1 -> 5.7 s). The settle grew to 5.8 s, mostly the last prisms' invisible grow-in (3.7-4.6 s at growthRate 0.5) behind the opaque menu veil; now snapped there (PrismTrailBuilder.LoadGateCovered). Re-capture is the next ecology test task

### T-20261020-bbc3 · done · decision · crew-vessels
- **Task:** Play the Wrasse Lasso Lab on a phone and pick the kite's phone control: the shipped Follow, Sling (thumb on a tether), Plant (the Thresher's anchor), Hook (curled flick) or Well (gravity); also keep or drop snap-shut loops. Round 1's scorecard picks Plant (29 mm of thumb per loop against Follow's 147, 3.3 motes per loop).
- **Done when:** A 'Which control goes to Unity' entry is in the lab's Decisions tab (shared log)
- **Who can take it:** Garrett (Wrasse owner); Yash can play it and record a second opinion
- **Added:** 2026-10-20 by a Claude session (`cece/loving-fermat-ntdnbu`)
- **See:** https://claude.ai/artifact/T5EBvmTvFw4KNRMQtwXDQb, Docs/Studios/WRASSE_LASSO_LAB.md
- **Done:** 2026-10-20, Garrett picked the Sling (2026-10-20: 'Sling is excellent. bring it to unity'); ported as KiteNavigatorCore.Sling on cece/loving-fermat-ntdnbu. Snap-shut loops stay open.

### T-20261021-2d87 · done · dev · vessels
- **Task:** Phone movement bugs: the Urchin flies full reverse whenever no thumb is on the glass (GunVesselTransformer.ReadThrottle reads XDiff 0 as -1; return 0 while InputStatus.Idle), and the Scarab never thrusts on touch (ScarabVesselTransformer.ReadThrottle01 reads only RightTriggerAnalog; treat Touch && !Idle like autopilot as a stopgap)
- **Done when:** On an Android build the Urchin stops (not reverses) when both thumbs lift, and the Scarab flies in Scarab Scramble
- **Who can take it:** Garrett (vessels), any session; Yash (offered) to check on a phone
- **Added:** 2026-10-21 by audit session (`cece/admiring-hamilton-gyzzh8`)
- **See:** Docs/Reports/MOBILE_VESSELS.md §2 rows 1-2
- **Done:** 2026-10-21, Done on cece/admiring-hamilton-gyzzh8: idle parity (both thumbs off reads XDiff 0.5) stops the Urchin reversing; the Scarab reads the touch throttle slider. Phone check is in the new phone-controls test task.

### T-20261021-985e · done · dev · vessels
- **Task:** Touch card fixes: (a) a card whose input does not resolve on touch must not be pressable or claim the touch (AbilityLockupView.SetSlotPressInput vs AbilityCardPress.TryResolveInput); (b) R_VesselActionHandler.StartPressedActions ignores a press of an input already held (a lift + a card started toggles twice)
- **Done when:** Rhino sword, Stoat and Angler Space/Charge cards no longer eat right-thumb touches; the Butterfly Dust card toggles once when the left thumb is also lifted
- **Who can take it:** Garrett (touch input), any session
- **Added:** 2026-10-21 by audit session (`cece/admiring-hamilton-gyzzh8`)
- **See:** Docs/Reports/MOBILE_VESSELS.md §2 rows 3-4
- **Done:** 2026-10-21, Done: AbilityCardPress.RefreshPressable - a card that presses nothing on the device neither presses nor claims. The double start cannot arise any more (lift cards are not buttons on ThumbLifts hulls; lifts raise nothing on TotemButtons hulls).

### T-20261021-bb9f · done · dev · vessels
- **Task:** Touch feel pass: fix the Squirrel/Serpent idle snap (MantaAnimationContoller Idle writes +p,x against puppetry's -2p,2x), ease the stick in StoatAnimation, AnglerAnimation and ButterflyAnimation (rule 53), set touchNoseResponse 9 on Dolphin, Manta, Rhino, Urchin and route it through SingleStickVesselTransformer.RotateShip, and honour Invert Y on touch
- **Done when:** On a phone, lifting the last thumb eases every hull to rest and the dual-stick hulls answer the thumb without the 0.67 s nose lag
- **Who can take it:** Garrett (vessels), any session; Yash (offered) to check on a phone
- **Added:** 2026-10-21 by audit session (`cece/admiring-hamilton-gyzzh8`)
- **See:** Docs/Reports/MOBILE_VESSELS.md §2 rows 5-7, 14
- **Done:** 2026-10-21, Idle snaps fixed (Squirrel, Serpent, Sparrow, Stoat, Angler, Butterfly). The touch nose response and Invert Y on touch were not part of Garrett's list: moved to a new dev task.

### T-20261021-9708 · done · decision · vessels
- **Task:** Mobile vessel decisions D1-D12: lift-to-reach-a-card fires abilities (grace window recommended), card size/placement on touch, gesture hints, two-trigger abilities (Rhino sword, Manta Soar, Shrike, Thresher launch), no trigger depth (hold-time ramp as fleet standard?), Stoat on phones, Angler Squad/Recall with no card, Scarab throttle, Butterfly re-grip Fold, Wrasse on two phones, Termite/Falcon retire, retire phone-flip FlipAction
- **Done when:** Each D row in the report carries a decision (or 'later') and the follow-up dev rows exist
- **Who can take it:** Garrett (vessel owner); Shombith for D2/D3 (UI)
- **Added:** 2026-10-21 by audit session (`cece/admiring-hamilton-gyzzh8`)
- **See:** Docs/Reports/MOBILE_VESSELS.md §3
- **Done:** 2026-10-21, Garrett decided 2026-10-21 (MOBILE_VESSELS.md section 8) and it was built. D9 (Butterfly re-grip Fold), D10 (Wrasse on two phones) and D11 (Termite/Falcon) remain: moved to a new decision task.

### T-20261021-18a1 · done · test · vessels
- **Task:** Fly Marlin and Jacks in the editor (UNITY_VERIFICATION_CHECKLIST 2026-10-21, steps 1-10: the models and the host-client crew included) and the Frenzy shield-food rule
- **Done when:** Every step of the checklist entry passes, or each failure is written up on the PR
- **Who can take it:** Garrett or Yash
- **Added:** 2026-10-21 by Garrett's session (`claude/project-thread-xrej29`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/JACKS.md
- **Done:** 2026-10-21, Superseded 2026-10-21 by the Jacks (cece/lucid-turing-zndmgf): the marlin seat is gone; the herd and Frenzy steps carry into the Jacks test task

### T-20261021-1844 · done · content · vessels
- **Task:** Give Marlin and Jacks its own model, icons and card art (it wears the Rhino's)
- **Done when:** The marlin hull and jacks have /vessel-model bodies and the class card has its own icons
- **Who can take it:** Garrett
- **Added:** 2026-10-21 by Garrett's session (`claude/project-thread-xrej29`)
- **See:** Assets/_Scripts/Controller/Vessel/R_VesselActions/JACKS.md §5
- **Done:** 2026-10-21, Marlin.fbx and Jack.fbx built by /vessel-model (specs/marlin.py, specs/jack.py), drawn by JacksBodies; class card rendered from the model (render_cards.py Marlin)

### T-20261021-cb48 · done · dev · vessels
- **Task:** Touch feel leftovers: set touchNoseResponse on Dolphin, Manta, Rhino, Urchin and route it through SingleStickVesselTransformer.RotateShip (the Angler's 9 is ignored there); honour Invert Y / Invert Throttle in TouchInputStrategy.Reparameterize and the one-thumb Pitch()
- **Done when:** Dual-stick hulls answer the thumb without the 0.67 s nose lag on a phone; the invert settings change touch flight
- **Who can take it:** any session (Garrett's vessels); Yash (offered) to check on a phone
- **Added:** 2026-10-21 by Garrett's session (`cece/admiring-hamilton-gyzzh8`)
- **See:** Docs/Reports/MOBILE_VESSELS.md section 2 rows 7 and 14
- **Done:** 2026-10-21, Done wider than the task (Garrett: 'get rid of all steering lag, and inconsistent y-inversions that are not left to the settings'): no nose lag for any human pilot on any device/hull (NoseFollowFraction), touchNoseResponse retired; one Invert Y rule in every strategy (ApplyInvertSettings), touch reads both invert settings. MOBILE_VESSELS.md section 8, second round.

### T-20261021-249e · done · test · vessels
- **Task:** Fly Marlin and Jacks round 4 on cece/sharp-faraday-gq7m65: the thumb-framed jacks' view on a full-size phone, the faint rings in the marlin's view, the 30-150 herd and its frame time, and the domain hue mark on herded and Angler-tamed creatures (host + client too)
- **Done when:** UNITY_VERIFICATION_CHECKLIST 2026-10-21 Marlin and Jacks steps 11-14 pass, or the failures are dev tasks
- **Who can take it:** Garrett (the look and feel); Yash (offered: editor, phone and host-client steps)
- **Added:** 2026-10-21 by Garrett (`cece/sharp-faraday-gq7m65`)
- **See:** Docs/UNITY_VERIFICATION_CHECKLIST.md (Marlin and Jacks, steps 11-14); R_VesselActions/JACKS.md
- **Done:** 2026-10-21, Superseded 2026-10-21 by the Jacks (cece/lucid-turing-zndmgf): the marlin-view steps are gone; the thumb view, herd size and hue mark carry into the Jacks test task

### T-20261021-2d13 · done · dev · Crew vessels
- **Task:** The Goby & Pistol has no touch path: GobyPistolDuo reads the Goby's primer and flares from keys 3 / 4 and the pad's D-pad up / down only, and the Pistol's dive and launch from the mouse, keyboard and pad (no Touchscreen read anywhere in Crew/GobyPistol). On a phone neither seat's abilities can be used. Give them touch controls (cards that name inputs bound on the hull, as the Wrasse and Orca now do with CrewTriggerActionSO, or the Goby's own touch overrides) in the Goby's next round.
- **Done when:** On a phone, the Goby can fire its primer and flares and the Pistol can dive and launch; author_touch_profiles.py has a Goby row whose glyphs/buttons resolve
- **Who can take it:** Garrett (the Goby's owner) with the session driving the Goby lab
- **Added:** 2026-10-21 by a Claude session (`ccr-e2247aad-06cnzl`)
- **See:** Docs/Reports/MOBILE_VESSELS.md section 8 'Third round'; Assets/_Scripts/Controller/Vessel/Crew/GobyPistol/GobyPistolDuo.cs GobyAbilities / HumanPistol
- **Done:** 2026-10-21, Built on cece/elegant-maxwell-q8vqoh: the Goby's primer/flares are input bindings (CrewButtonActionSO, pad X/Y, keys Q/F), each seat shows its own phone buttons (GobyPistolButtons), the Pistol dives/launches by finger, Goby row in TouchControlProfiles. Phone test: T-20261021-52b0 + the seat-buttons test task.

### T-20261021-cd1e · done · test · Crew vessels
- **Task:** Phone playtest of the Goby & Pistol seat buttons (Android dev build, no pad): Toybox > Vessel Changer > Goby and Pistol. Flying the Goby, two buttons sit round the right thumb: Primer (fires at what is ahead, its count drops) and Flares (fires only once a prime has been set off by the Pistol and blocks are hot); the Squirrel's lifts (left = ring, right = drift) still work. Double tap the small view: now Ghost and Snap sit there instead, dimmed while cooling; a finger on the Pistol's view dives (hold) and launches (drag and let go), also while another finger holds Ghost. On a pad: X primer, Y flares; on a keyboard: Q primer, F flares; the D-pad up/down does nothing.
- **Done when:** Every step behaves as written; buttons sit inside the safe area and don't cover the ability row; misses filed as dev tasks
- **Who can take it:** Yash (offered) or Garrett
- **Added:** 2026-10-21 by a Claude session (`cece/elegant-maxwell-q8vqoh`)
- **See:** .claude/skills/duo-vessel/references/designs/goby-and-pistol.md §11 (2026-10-21)
- **Done:** 2026-10-22, Superseded 2026-10-22: the Goby's Primer and Flares were retired with its new kit (Dart / Lock; Ghost / Snap / Back). The new kit's playtest is its own task.

### T-20261023-7dca · done · dev · Crew vessels
- **Task:** Let a second person ride the Wrasse and the Pistol seats (ICrewStations.HostsRiders is false on KiteNavigator and GobyPistolDuo): run or relay the seat for a rider, as the Orca (seat on the rider's machine) and the Ratel (ICrewRelay) do
- **Done when:** Two people fly the Grouper and the Wrasse (and the Goby and the Pistol) from two machines in freestyle
- **Who can take it:** Garrett with Yash (multiplayer)
- **Added:** 2026-10-23 by Claude (cece/relaxed-tesla-axhxzy) (`cece/relaxed-tesla-axhxzy`)
- **See:** Docs/CREW_STATIONS.md; Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md (Two humans, two seats, steps 1 and 4)
- **Done:** 2026-10-23, Built 2026-10-23 on cece/relaxed-tesla-axhxzy: the Wrasse and the Pistol take a rider (KiteNavigator, GobyPistolDuo ICrewRelay); untested in the Editor - hand tests 9-11 in Docs/CREW_STATIONS.md are on T-20261023-d445

### T-20261022-accb · done · test · Input
- **Task:** Fly two or three hulls (a two-stick hull like the Squirrel, a one-thumb hull like the Sparrow) on a gamepad on this branch and judge whether small corrections feel like the old pad (before 2026-10-21) while the nose still has no lag. Tune Resources/GamepadFlightConfig live in Play: responseExponent (centre softness, 1.5) and steeringSensitivity (full-stick rate, 0.85); 1 / 1 is the uncurved pad.
- **Done when:** Garrett signs off on the values, or new values are committed to Resources/GamepadFlightConfig.asset
- **Who can take it:** Garrett (the feel), Yash (offered)
- **Added:** 2026-10-22 by a Claude session (`cece/serene-galileo-t77xt3`)
- **See:** Docs/claude/ARCHITECTURE_CORE.md (Input Strategy), Assets/_Scripts/ScriptableObjects/GamepadFlightConfigSO.cs
- **Done:** 2026-10-23, Superseded: Garrett flew the response curve and it lost the smoothness; replaced by the gamepad steering smoother (CascadeSmoother), retested under the new task.

### T-20261023-70a9 · done · test · Input
- **Task:** Fly the Squirrel and the Sparrow on a gamepad on cece/serene-galileo-t77xt3: do turns build and settle smoothly (no jolt) while the nose still answers at once? Resources/GamepadFlightConfig now ships legacyNoseLag ON (the old 1.5/s nose lag, exactly) with the smoother off (delay 0): fly it, then untick legacyNoseLag and try the smoother (smoothingStages 6, smoothingDelaySeconds 0.12-0.35 s) to compare; decide which ships. Also run the EditMode CascadeSmootherTests.
- **Done when:** Garrett signs off on the feel and the values (committed to Resources/GamepadFlightConfig.asset), and CascadeSmootherTests pass
- **Who can take it:** Garrett (the feel), Yash (offered, tests)
- **Added:** 2026-10-23 by a Claude session (`cece/serene-galileo-t77xt3`)
- **See:** Docs/claude/ARCHITECTURE_CORE.md (Input Strategy), Assets/_Scripts/Controller/IO/CascadeSmoother.cs, Docs/UNITY_VERIFICATION_CHECKLIST.md
- **Done:** 2026-10-23, Garrett flew the pad 2026-10-23: the old 1.5/s nose lag wins ('responsive, but eases into its target ... keep this on gamepad'); the stage smoother lost (reads as input lag) and was removed. Pad dial now Resources/GamepadFlightConfig.noseFollowPerSecond = 1.5.
