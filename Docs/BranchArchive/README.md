# Branch archive — small inactive branches

Snapshot taken **2026-10-08** from `froglet-studio/Cosmic-Shore`. Covers every remote branch whose last commit is
older than 2026-09-08 **and** that carries **1–3 commits not present in `bleeding-edge` or `master`**.

Each branch has its own file under [`small/`](small/). Each one lists those unmerged commits in full (message, files, and the patch for code/doc/text files,
capped at 150 lines per commit). Unity scene/prefab/asset YAML and binaries are listed by name and size only.
The commit SHAs stay recoverable from this doc only while the branch or a tag still points at them —
if a branch here matters, tag it (`git tag archive/<name> origin/<branch>`) before deleting it.

## Status of the inactive-branch cleanup (2026-10-08)

The audit found **505** remote branches; **363** had no commit since 2026-09-08.

| Group | Count | Status |
|---|---|---|
| Already merged (every commit is in `bleeding-edge` or `master`) | 114 | **108 to delete**, script below. The other 6 are kept, see next table. |
| Small unmerged work (1–3 commits) | 141 | Archived in this folder, one file per branch. Not deleted. |
| Medium unmerged work (4–10 commits) | 51 | Not archived yet. Not deleted. |
| Large unmerged work (11+ commits) | 56 | Not archived yet. Not deleted. |
| `master` | 1 | Trunk. Keep. |

**Merged but kept:**

| Branch | Why |
|---|---|
| `development` | Release pipeline: the tester branch (`Docs/BRANCHING_AND_RELEASE.md`). It goes quiet between promotions, so "no commit for a month" is normal for it. |
| `build/android`, `build/windows` | Release pipeline: robot-owned snapshots Unity Build Automation reads, force-moved by `sync-build-branches.yml`. |
| `claude/shape-signs-face-player-INiJp`, `claude/loving-fermi-DVWsY`, `claude/qa-backlog-7mvlsr` | Each still has an **open** pull request (#130, #530, #799). Their work is merged, so the PRs are stale, but deleting the branch closes the PR. Close the PRs first, then delete. |

**Deleting the 108:** [`delete_merged_inactive_branches.sh`](delete_merged_inactive_branches.sh). The cloud session that wrote this archive could
not delete them, because GitHub returned HTTP 403 for every push except to its own branch. Run the script from a clone with push rights. It
re-checks each branch and skips any that has gained new commits since the audit.

## Index


| # | Branch | Last commit | Author | Unmerged commits | Open PR | Last commit message |
|---|---|---|---|---|---|---|
| 1 | [`erwan/firebase`](small/erwan__firebase.md) | 2022-09-14 | Erwan Loisant | 1 | — | Add Firebase |
| 2 | [`code-scraper`](small/code-scraper.md) | 2024-05-20 | Emmanuel Eytan | 3 | — | Added docs. They should have been written earlier. Oops. |
| 3 | [`codex/smooth-camera-follow-for-sparrow.prefab`](small/codex__smooth-camera-follow-for-sparrow.prefab.md) | 2025-07-16 | Shombith03 | 1 | — | feat: adjust camera follow smoothing |
| 4 | [`codex/add-fbx-support-to-blend-shape-system`](small/codex__add-fbx-support-to-blend-shape-system.md) | 2025-08-07 | Garrett Milliron | 3 | #60 | Implement blend shape texture sampling and improve validator |
| 5 | [`scene-maker-tool`](small/scene-maker-tool.md) | 2025-10-17 | Shombith03 | 2 | — | Game Tool Maker Part 2 |
| 6 | [`prism-fade-system`](small/prism-fade-system.md) | 2025-10-24 | Emmanuel | 1 | — | The capsule shows and is at the right place. Weird collision bug. |
| 7 | [`suction-shader`](small/suction-shader.md) | 2026-01-03 | Christopher Stackpole | 2 | — | suction per triangle (unordered) |
| 8 | [`gamemode/Tournament`](small/gamemode__Tournament.md) | 2026-02-13 | Yash Sadhukhan | 2 | — | Merge pull request #68 from YsKhan61/gamemode/Tournament |
| 9 | [`claude/new-squirrel-minigame-jgAWN`](small/claude__new-squirrel-minigame-jgAWN.md) | 2026-02-20 | Claude | 1 | #81 | Add Acorn Hoard minigame for the Squirrel vessel |
| 10 | [`claude/complete-intern-work-lPhVG`](small/claude__complete-intern-work-lPhVG.md) | 2026-02-21 | Claude | 1 | — | Implement all NotImplementedException stubs and incomplete scoring logic |
| 11 | [`claude/fix-ui-haptics-mixing-2laRC`](small/claude__fix-ui-haptics-mixing-2laRC.md) | 2026-02-21 | Garrett Milliron | 3 | #85 | pulling in dev? |
| 12 | [`claude/fix-wildlife-blitz-K6gY9`](small/claude__fix-wildlife-blitz-K6gY9.md) | 2026-02-21 | Claude | 3 | #91 | Add connecting panel animations and pre-game cinematic infrastructure |
| 13 | [`claude/refactor-dependency-injection-Tf5vf`](small/claude__refactor-dependency-injection-Tf5vf.md) | 2026-02-21 | Claude | 1 | — | Refactor 6 singletons to use Reflex dependency injection |
| 14 | [`claude/add-sparrow-gun-sounds-tMnlb`](small/claude__add-sparrow-gun-sounds-tMnlb.md) | 2026-02-23 | Claude | 2 | — | Add Sparrow full-auto gun sounds and fix double-drift release audio |
| 15 | [`claude/add-coding-standards-JX6VT`](small/claude__add-coding-standards-JX6VT.md) | 2026-02-25 | Claude | 1 | — | Add SOLID principles and clean code standards to CLAUDE.md |
| 16 | [`claude/add-menu-scene-tests-43Wzb`](small/claude__add-menu-scene-tests-43Wzb.md) | 2026-02-25 | Claude | 2 | — | Merge remote-tracking branch 'origin/app-shell-polish' into claude/add-menu-scen |
| 17 | [`claude/fix-spawn-shear-prism-SoRfa`](small/claude__fix-spawn-shear-prism-SoRfa.md) | 2026-02-25 | Claude | 3 | — | Fix NullReferenceException when spawnable uses children instead of direct prism  |
| 18 | [`revert-110-claude/bootstrap-scene-setup-Xg8gG`](small/revert-110-claude__bootstrap-scene-setup-Xg8gG.md) | 2026-02-25 | Yash Sadhukhan | 1 | — | Revert "Add bootstrap system with service initialization and lifecycle managemen |
| 19 | [`claude/add-unit-tests-YLJDy`](small/claude__add-unit-tests-YLJDy.md) | 2026-02-26 | Claude | 2 | — | fix(tests): resolve namespace issues in GeometryUtilsTests and XpDataTests |
| 20 | [`claude/fix-unit-tests-SvKvF`](small/claude__fix-unit-tests-SvKvF.md) | 2026-02-26 | Claude | 1 | — | fix(bootstrap): fix unit test failures and add assembly definitions |
| 21 | [`claude/test-hex-race-integration-TK9Nr`](small/claude__test-hex-race-integration-TK9Nr.md) | 2026-02-26 | Claude | 2 | — | Merge remote-tracking branch 'origin/app-shell-polish' into claude/test-hex-race |
| 22 | [`claude/test-multiplayer-freestyle-sync-U5XgI`](small/claude__test-multiplayer-freestyle-sync-U5XgI.md) | 2026-02-26 | Claude | 2 | — | merge: integrate app-shell-polish branch |
| 23 | [`claude/add-mode-toggle-button-gZL4k`](small/claude__add-mode-toggle-button-gZL4k.md) | 2026-02-28 | Claude | 1 | — | feat(ui): add ModeToggleButton for Menu/Freestyle toggle |
| 24 | [`claude/add-party-invite-panel-K5pah`](small/claude__add-party-invite-panel-K5pah.md) | 2026-02-28 | Claude | 1 | — | feat(party): implement network transition for party invite accept flow |
| 25 | [`claude/fix-menu-camera-transition-GZzsT`](small/claude__fix-menu-camera-transition-GZzsT.md) | 2026-02-28 | Claude | 1 | — | fix(menu): smooth camera transitions between menu and gameplay cameras |
| 26 | [`claude/fix-player-instance-2-error-xoo3n`](small/claude__fix-player-instance-2-error-xoo3n.md) | 2026-02-28 | Claude | 1 | — | fix(multiplayer): resolve port conflict and duplicate callback for multi-instanc |
| 27 | [`claude/review-friend-system-nVgua`](small/claude__review-friend-system-nVgua.md) | 2026-02-28 | Claude | 1 | — | feat(party): enable shared Menu_Main scene for party members |
| 28 | [`claude/setup-friends-panel-ui-j3ihi`](small/claude__setup-friends-panel-ui-j3ihi.md) | 2026-02-28 | Claude | 3 | — | fix(editor): consolidate duplicate CreateFriendsPanelPrefab and polish all party |
| 29 | [`claude/spawn-vessel-menu-scene-QIIec`](small/claude__spawn-vessel-menu-scene-QIIec.md) | 2026-02-28 | Claude | 1 | — | fix(menu): ensure vessel spawns as Squirrel in Menu_Main on every host start |
| 30 | [`claude/sync-game-start-lobby-G9Aec`](small/claude__sync-game-start-lobby-G9Aec.md) | 2026-02-28 | Claude | 1 | — | feat(arcade): sync game config to GameDataSO and use dynamic AI backfill from lo |
| 31 | [`claude/add-settings-camera-slider-BnBWv`](small/claude__add-settings-camera-slider-BnBWv.md) | 2026-03-01 | Claude | 1 | — | feat(settings): add camera offset multiplier slider to game settings |
| 32 | [`claude/fix-screenshot-error-jEb91`](small/claude__fix-screenshot-error-jEb91.md) | 2026-03-01 | Claude | 1 | — | fix(screenshot): replace Camera.Render() with URP-compatible screen capture |
| 33 | [`claude/list-bootstrap-scripts-oeeeP`](small/claude__list-bootstrap-scripts-oeeeP.md) | 2026-03-01 | Claude | 2 | — | Merge remote-tracking branch 'origin/app-shell-polish' into claude/list-bootstra |
| 34 | [`claude/list-menu-main-scripts-d6DKD`](small/claude__list-menu-main-scripts-d6DKD.md) | 2026-03-01 | Claude | 1 | — | docs: add complete scene script inventories for Bootstrap and Menu_Main |
| 35 | [`claude/sync-menu-main-dev-0a73y`](small/claude__sync-menu-main-dev-0a73y.md) | 2026-03-01 | Claude | 2 | — | merge(app-shell-polish): integrate click-spam prevention, keep dev sync |
| 36 | [`claude/test-multiplayer-play-mode-9cnAw`](small/claude__test-multiplayer-play-mode-9cnAw.md) | 2026-03-01 | Claude | 1 | — | test(multiplayer): add unit tests for MPPM profile and port helpers |
| 37 | [`claude/unify-crystal-manager-67kN5`](small/claude__unify-crystal-manager-67kN5.md) | 2026-03-01 | Claude | 2 | — | fix(crystals): always unsubscribe in OnDisable regardless of network state |
| 38 | [`claude/add-friday-build-notify-fhh6q`](small/claude__add-friday-build-notify-fhh6q.md) | 2026-03-02 | Claude | 1 | — | Add Friday build & Discord notification workflow |
| 39 | [`claude/restore-block-bandit-J6Pl1`](small/claude__restore-block-bandit-J6Pl1.md) | 2026-03-02 | Garrett Milliron | 2 | — | pushing scene changes like changine to stealing and the extending helix |
| 40 | [`Animation`](small/Animation.md) | 2026-03-03 | Braden Hamilton | 1 | — | Add Missile launch animations to Sparrow ship |
| 41 | [`claude/add-tournament-mode-t0bWK`](small/claude__add-tournament-mode-t0bWK.md) | 2026-03-03 | Claude | 1 | — | feat(tournament): add modular tournament mode architecture |
| 42 | [`claude/fix-double-game-end-78h5q`](small/claude__fix-double-game-end-78h5q.md) | 2026-03-03 | Claude | 2 | — | fix(multiplayer): remove duplicate game-end events from HexRace, DomainGames, an |
| 43 | [`claude/lobby-player-team-selection-9Bzzy`](small/claude__lobby-player-team-selection-9Bzzy.md) | 2026-03-03 | Claude | 1 | — | feat(lobby): add player count stepper (1-12) and team selection (Jade/Ruby/Gold) |
| 44 | [`claude/sync-spawning-algorithm-nLaH5`](small/claude__sync-spawning-algorithm-nLaH5.md) | 2026-03-03 | Claude | 1 | — | refactor(multiplayer): unify AI and human player spawning through same pipeline |
| 45 | [`claude/test-crystal-capture-mode-bnGpP`](small/claude__test-crystal-capture-mode-bnGpP.md) | 2026-03-03 | Claude | 2 | — | docs(arcade): add Crystal Capture game mode technical documentation |
| 46 | [`claude/test-joust-mode-pwmwi`](small/claude__test-joust-mode-pwmwi.md) | 2026-03-03 | Claude | 1 | — | fix(joust): remove double game-end fire and clean up turn monitor |
| 47 | [`claude/add-spawnable-caching-wg2zr`](small/claude__add-spawnable-caching-wg2zr.md) | 2026-03-04 | Claude | 1 | — | Include intensityLevel, domain, and seed in base cache key so spawnable caching  |
| 48 | [`claude/claude-md-mmconidf84v88i8m-BpUSq`](small/claude__claude-md-mmconidf84v88i8m-BpUSq.md) | 2026-03-04 | Claude | 1 | — | docs: add CLAUDE.md with codebase guide for AI assistants |
| 49 | [`claude/investigate-spawning-logic-dMaCu`](small/claude__investigate-spawning-logic-dMaCu.md) | 2026-03-04 | Claude | 3 | — | fix(multiplayer): use LocalClient on clients for DontDestroyOnLoad + move vessel |
| 50 | [`claude/fix-unity-audio-AEjd0`](small/claude__fix-unity-audio-AEjd0.md) | 2026-03-05 | Claude | 1 | — | Fix silent SFX: PlayerPrefs stored audio levels as int but read as float |
| 51 | [`claude/optimize-scene-load-times-5sopf`](small/claude__optimize-scene-load-times-5sopf.md) | 2026-03-05 | Claude | 3 | — | Replace per-prism coroutine with centralized PrismActivationQueue |
| 52 | [`claude/add-prism-activation-queue-CEoJM`](small/claude__add-prism-activation-queue-CEoJM.md) | 2026-03-06 | Claude | 1 | — | Add PrismActivationQueue to eliminate thundering-herd coroutine stalls |
| 53 | [`claude/find-sparrow-prefab-CYGsQ`](small/claude__find-sparrow-prefab-CYGsQ.md) | 2026-03-06 | Claude | 3 | — | Fix animation takeName paths in SparrowModel3.fbx.meta |
| 54 | [`claude/fix-waiting-image-display-dlbhy`](small/claude__fix-waiting-image-display-dlbhy.md) | 2026-03-06 | Claude | 1 | — | fix(multiplayer): show splash overlay on clients during game scene transition |
| 55 | [`claude/adjust-freestyle-shapes-wgdkt`](small/claude__adjust-freestyle-shapes-wgdkt.md) | 2026-03-07 | Claude | 1 | — | Scale shape drawing shapes to 4x size for more maneuvering room |
| 56 | [`claude/explore-ai-menu-vessel-RhZxw`](small/claude__explore-ai-menu-vessel-RhZxw.md) | 2026-03-07 | Claude | 2 | #406 | Refine vessel tutorial: add ActionHandler subscription, idle toggle, and HOME sc |
| 57 | [`claude/optimize-pool-manager-6tOOr`](small/claude__optimize-pool-manager-6tOOr.md) | 2026-03-07 | Claude | 1 | — | Optimize GenericPoolManager for faster scene loading |
| 58 | [`claude/fix-music-stopping-bug-uRH0T`](small/claude__fix-music-stopping-bug-uRH0T.md) | 2026-03-08 | Claude | 1 | — | Fix music stopping bug and add spatial audio for gameplay SFX |
| 59 | [`claude/optimize-menu-performance-5EODy`](small/claude__optimize-menu-performance-5EODy.md) | 2026-03-08 | Claude | 1 | — | Optimize menu UI performance: reduce per-frame waste and navigation spikes |
| 60 | [`claude/update-gitignore-benchmark-2vGf4`](small/claude__update-gitignore-benchmark-2vGf4.md) | 2026-03-08 | Claude | 1 | — | Add BenchmarkReports/ to .gitignore |
| 61 | [`claude/benchmark-mobile-performance-SYydw`](small/claude__benchmark-mobile-performance-SYydw.md) | 2026-03-09 | Claude | 3 | — | Fix CS0019: use Equals() for Color32 comparison instead of != operator |
| 62 | [`claude/optimize-mobile-performance-7uCEG`](small/claude__optimize-mobile-performance-7uCEG.md) | 2026-03-09 | Claude | 3 | — | Eliminate material cloning and cache hot-path GetComponent lookups |
| 63 | [`claude/replace-froglet-games-site-zxfW5`](small/claude__replace-froglet-games-site-zxfW5.md) | 2026-03-09 | Claude | 1 | — | Add patch file for Cosmic-Shore-landing-page repo update |
| 64 | [`claude/explosive-joust-scripts-xLazv`](small/claude__explosive-joust-scripts-xLazv.md) | 2026-03-19 | Shombith03 | 3 | #425 | Add Cinematic Defination |
| 65 | [`claude/fix-volume-scoring-multiplayer-5hBSs`](small/claude__fix-volume-scoring-multiplayer-5hBSs.md) | 2026-03-19 | Claude | 1 | — | Fix multiplayer scoring: use per-player score tracking in BaseScoring |
| 66 | [`claude/add-locust-fauna-XROBc`](small/claude__add-locust-fauna-XROBc.md) | 2026-03-26 | Claude | 1 | — | Add Locust fauna class — swarm-based trail prism consumers |
| 67 | [`claude/fix-freestyle-shape-triggers-duvEz`](small/claude__fix-freestyle-shape-triggers-duvEz.md) | 2026-03-26 | Claude | 1 | — | Fix shape trigger prisms ignoring prefab scale by treating SpawnPoint.Scale as m |
| 68 | [`claude/restore-block-bandit-2ejW2`](small/claude__restore-block-bandit-2ejW2.md) | 2026-03-27 | Claude | 3 | #444 | Add Block Bandit end-game, scoreboard, and HUD following OrganicRematch patterns |
| 69 | [`claude/fix-lobby-rate-limit-W7HA4`](small/claude__fix-lobby-rate-limit-W7HA4.md) | 2026-03-28 | Claude | 1 | — | fix(party): prevent lobby rate-limit errors on invite send |
| 70 | [`claude/fix-player-spawning-k3GR2`](small/claude__fix-player-spawning-k3GR2.md) | 2026-03-31 | Claude | 1 | — | fix(spawning): cap player count at MaxTotalPlayers, resolve Unassigned domains b |
| 71 | [`claude/gyroid-seed-danger-prism-U3MnJ`](small/claude__gyroid-seed-danger-prism-U3MnJ.md) | 2026-04-01 | Claude | 3 | — | fix(gyroid): make FindNearbyAssembledFlora public for cross-class access |
| 72 | [`claude/review-vessel-crystal-mechanics-FfgsO`](small/claude__review-vessel-crystal-mechanics-FfgsO.md) | 2026-04-01 | Claude | 1 | — | feat(crystal): add initial domain field for prefab-driven domain assignment |
| 73 | [`claude/fix-arcade-replay-577tb`](small/claude__fix-arcade-replay-577tb.md) | 2026-04-09 | Claude | 1 | — | fix(arcade): prevent MissingReferenceException on destroyed Player during scene  |
| 74 | [`claude/fix-vessel-ui-bleed-67g3B`](small/claude__fix-vessel-ui-bleed-67g3B.md) | 2026-04-09 | Claude | 1 | — | fix(ui): prevent vessel HUD from briefly bleeding through menu UI |
| 75 | [`claude/optimize-shield-effect-CgpSK`](small/claude__optimize-shield-effect-CgpSK.md) | 2026-04-15 | Claude | 1 | — | feat(prisms): coalesce shield events into single shockwave per wave origin |
| 76 | [`claude/detect-competing-changes-GZ7Lu`](small/claude__detect-competing-changes-GZ7Lu.md) | 2026-04-20 | Claude | 1 | — | feat(skills): add detect-competing-changes skill for git flip analysis |
| 77 | [`claude/emergent-systems-guidance-development`](small/claude__emergent-systems-guidance-development.md) | 2026-04-20 | Claude | 2 | #491 | Refine emergent-systems guidance with canonical fundamentals |
| 78 | [`claude/fix-team-scoreboard-BC9OV`](small/claude__fix-team-scoreboard-BC9OV.md) | 2026-04-21 | Claude | 1 | — | fix(multiplayer): guard server-only timer RPC and unify teammate domain |
| 79 | [`claude/fix-friend-party-invites-1NgTo`](small/claude__fix-friend-party-invites-1NgTo.md) | 2026-04-22 | Claude | 2 | — | fix(party): unstick pending invites and keep clients with the host on start |
| 80 | [`claude/fix-squirrel-jade-effects-QVhqM`](small/claude__fix-squirrel-jade-effects-QVhqM.md) | 2026-04-22 | Claude | 1 | — | fix(vessel): squirrel crystal-hit prisms now render in vessel domain |
| 81 | [`claude/fix-party-lobby-connection-voFVM`](small/claude__fix-party-lobby-connection-voFVM.md) | 2026-04-23 | Claude | 1 | #499 | Document party system audit for pre-Steam launch |
| 82 | [`claude/fix-blue-fauna-spawn-3ov7j`](small/claude__fix-blue-fauna-spawn-3ov7j.md) | 2026-05-03 | Claude | 1 | — | fix(fauna): exclude Blue from cell control + let rabid fauna eat same-domain mas |
| 83 | [`claude/review-optimization-branches-WDr9T`](small/claude__review-optimization-branches-WDr9T.md) | 2026-05-05 | Claude | 2 | — | Merge remote-tracking branch 'origin/bleeding-edge' into claude/review-optimizat |
| 84 | [`claude/add-haptics-for-fx-sznU5`](small/claude__add-haptics-for-fx-sznU5.md) | 2026-05-09 | Claude | 3 | — | diag(haptics): one-shot diagnostic dump + Tools > Test Haptic probe |
| 85 | [`claude/fix-freestyle-loading-screen-mw4kA`](small/claude__fix-freestyle-loading-screen-mw4kA.md) | 2026-05-11 | Claude | 1 | — | fix(loading): prevent stuck loading screen when singleplayer Start() throws |
| 86 | [`claude/fix-ai-domain-placement-CLG5O`](small/claude__fix-ai-domain-placement-CLG5O.md) | 2026-05-12 | Claude | 1 | — | fix(domains): balanced AI tie-break + Random click defers to scene-spawn |
| 87 | [`claude/lucid-keller-0cvVl`](small/claude__lucid-keller-0cvVl.md) | 2026-05-22 | Claude | 2 | — | ci: add GameCI Unity Test Framework pipeline |
| 88 | [`claude/beautiful-dirac-K5720`](small/claude__beautiful-dirac-K5720.md) | 2026-05-26 | Claude | 3 | — | fix(perf): stop ShipAudioController re-searching for nonexistent StudioListener  |
| 89 | [`claude/stoic-dirac-cBNyV`](small/claude__stoic-dirac-cBNyV.md) | 2026-05-30 | Claude | 1 | — | docs(flow): add game flow diagram + multiplayer sync analysis |
| 90 | [`claude/sweet-cray-6gvOC`](small/claude__sweet-cray-6gvOC.md) | 2026-06-05 | Claude | 1 | — | feat(menu): wire the XP progress bar onto the Profile tab |
| 91 | [`claude/optimistic-planck-3f1ztv`](small/claude__optimistic-planck-3f1ztv.md) | 2026-06-08 | Claude | 1 | — | perf(prism): render super-shield as 8 tetra faces instead of 24 |
| 92 | [`claude/friendly-ritchie-e3ccsm`](small/claude__friendly-ritchie-e3ccsm.md) | 2026-06-11 | Claude | 1 | — | fix(presence): self-heal displayName/avatarId lobby properties (B10) |
| 93 | [`claude/kind-meitner-qhbjxo`](small/claude__kind-meitner-qhbjxo.md) | 2026-06-11 | Garrett Milliron | 2 | — | Merge branch 'claude/kind-meitner-qhbjxo' of https://github.com/froglet-studio/S |
| 94 | [`claude/tender-pasteur-mxw3t4`](small/claude__tender-pasteur-mxw3t4.md) | 2026-06-11 | Claude | 1 | — | feat(benchmark): schema v2 — CPU thread breakdown + physics time in captures |
| 95 | [`claude/dreamy-keller-lenwio`](small/claude__dreamy-keller-lenwio.md) | 2026-06-12 | Claude | 2 | — | Merge remote-tracking branch 'origin/bleeding-edge' into claude/dreamy-keller-le |
| 96 | [`claude/explore-cellular-automata-9oWWg`](small/claude__explore-cellular-automata-9oWWg.md) | 2026-06-12 | Claude | 1 | — | Add learned cellular automata and neural boid systems |
| 97 | [`claude/nifty-cori-efpzh2`](small/claude__nifty-cori-efpzh2.md) | 2026-06-12 | Claude | 3 | — | feat(automata): export best-eval checkpoint instead of final training state |
| 98 | [`claude/confident-cerf-qaaq4z`](small/claude__confident-cerf-qaaq4z.md) | 2026-06-14 | Claude | 2 | — | Merge remote-tracking branch 'origin/Ys-bleeding-edge' into claude/confident-cer |
| 99 | [`vignette-testing`](small/vignette-testing.md) | 2026-06-15 | xghest | 3 | — | Update SquirrelVignetteController.cs |
| 100 | [`claude/hopeful-pascal-5131v1`](small/claude__hopeful-pascal-5131v1.md) | 2026-06-16 | Claude | 2 | — | fix(meta): resolve leftover merge conflict markers in ElementShapes folder meta |
| 101 | [`claude/loving-clarke-9xl6he`](small/claude__loving-clarke-9xl6he.md) | 2026-06-17 | Claude | 1 | — | fix(ui): only hoist the local player's vessel HUD into the game canvas |
| 102 | [`claude/beautiful-feynman-gnchcw`](small/claude__beautiful-feynman-gnchcw.md) | 2026-06-19 | Claude | 1 | — | docs(perf): add Docs/PERFORMANCE.md ledger + methodology; correct stale audit st |
| 103 | [`claude/git-contribution-analysis-ghefmu`](small/claude__git-contribution-analysis-ghefmu.md) | 2026-06-19 | Claude | 1 | — | docs: add 12-month contribution summary for Shombith03 |
| 104 | [`claude/laughing-newton-7jq96s`](small/claude__laughing-newton-7jq96s.md) | 2026-06-23 | Claude | 2 | — | fix(arcade): register Sprawl in the live game list (OrganicRematchGames) |
| 105 | [`cece/quirky-gates-1wqijw`](small/cece__quirky-gates-1wqijw.md) | 2026-06-25 | Claude | 1 | — | docs(quest): canonical Quest Track + Breadcrumb activation design + handoff |
| 106 | [`claude/nifty-bohr-61x85r`](small/claude__nifty-bohr-61x85r.md) | 2026-06-25 | Claude | 2 | — | feat(webgl): offline main-menu boot path scaffolding |
| 107 | [`claude/remove-special-chars-03qmyc`](small/claude__remove-special-chars-03qmyc.md) | 2026-06-26 | Claude | 1 | — | style: replace non-ASCII special characters with ASCII across the project |
| 108 | [`claude/toast-system-reintegration-1t5x4f`](small/claude__toast-system-reintegration-1t5x4f.md) | 2026-07-01 | Claude | 3 | — | fix(editor): harden Font Replacer per adversarial review |
| 109 | [`claude/game-codebase-outline-yqkz47`](small/claude__game-codebase-outline-yqkz47.md) | 2026-07-09 | Claude | 2 | — | docs(architecture): add interactive system-constellation map |
| 110 | [`claude/prism-constructs-perception-my7fg7`](small/claude__prism-constructs-perception-my7fg7.md) | 2026-07-09 | Claude | 2 | — | feat(perception): flyable MinigamePrismPerception arcade scene (Squirrel, mode 3 |
| 111 | [`claude/merge-bleeding-edge-conflicts-7iqozq`](small/claude__merge-bleeding-edge-conflicts-7iqozq.md) | 2026-07-16 | Claude | 1 | — | docs(benchmark): align procedure doc with the real tool UI and test inventory |
| 112 | [`claude/audio-matched-haptics-0r6ib8`](small/claude__audio-matched-haptics-0r6ib8.md) | 2026-07-17 | Claude | 3 | — | feat(haptics): playtest feel pass — sparse haptics, hero skim pulse-train + pr |
| 113 | [`claude/toast-prefab-animation-gnzuqk`](small/claude__toast-prefab-animation-gnzuqk.md) | 2026-07-17 | Claude | 1 | #607 | refactor(ui): spawn toasts from authored prefab and animate text |
| 114 | [`claude/tetrahedral-collider-cost-f5bfc5`](small/claude__tetrahedral-collider-cost-f5bfc5.md) | 2026-07-22 | Claude | 3 | — | fix(prisms): purge stale pending shield contacts on Enter-pass and OnDisable |
| 115 | [`claude/rhino-energy-sword-kojj3s`](small/claude__rhino-energy-sword-kojj3s.md) | 2026-07-23 | Claude | 2 | — | fix(rhino): keep sword MaxScale >= BaseScale to avoid resting-clamp inversion |
| 116 | [`claude/vehicle-selection-back-button-o2ukgr`](small/claude__vehicle-selection-back-button-o2ukgr.md) | 2026-07-24 | Claude | 1 | — | fix(ui): make back button work on vessel/team selection screen |
| 117 | [`claude/game-load-time-optimization-b3j58o`](small/claude__game-load-time-optimization-b3j58o.md) | 2026-07-28 | Claude | 2 | #630 | revert(load): back out the load-gate throughput knobs; keep only the density cut |
| 118 | [`claude/load-time-insights-tool-32ncto`](small/claude__load-time-insights-tool-32ncto.md) | 2026-07-28 | Claude | 3 | — | fix(build): add missing LoadInsights using to Prism.cs |
| 119 | [`claude/steam-launch-checklist-qf0tsb`](small/claude__steam-launch-checklist-qf0tsb.md) | 2026-07-28 | Claude | 1 | — | docs: scoped-down Steam Early Access launch checklist |
| 120 | [`UI-Asset-Clean-Up`](small/UI-Asset-Clean-Up.md) | 2026-07-30 | Philip Appoh | 3 | — | Finshed marking all unused sprites |
| 121 | [`claude/prism-grid-explosion-scene-bi74f9`](small/claude__prism-grid-explosion-scene-bi74f9.md) | 2026-08-01 | Claude | 2 | — | fix(tools): report prism-grid readiness by index registration, not instantiation |
| 122 | [`claude/host-connection-refresh-error-85c6a2`](small/claude__host-connection-refresh-error-85c6a2.md) | 2026-08-03 | Claude | 1 | — | fix(party): stop presence converge releasing the lobby it just joined |
| 123 | [`claude/prism-clock-followup-prompts-two-mhvad9`](small/claude__prism-clock-followup-prompts-two-mhvad9.md) | 2026-08-04 | Claude | 1 | — | fix(toys): the conveyor recycle's kind-wipe missed the birth window |
| 124 | [`claude/delete-changeset-bleeding-edge-krwufx`](small/claude__delete-changeset-bleeding-edge-krwufx.md) | 2026-08-06 | Claude | 3 | — | revert(sln): drop accidental "Testing game" solution reorder |
| 125 | [`claude/reorient-55psg4`](small/claude__reorient-55psg4.md) | 2026-08-11 | Claude | 2 | — | docs(qa): mark the backlog stale against a trunk 148 commits ahead |
| 126 | [`claude/dithering-crystal-shepard-tone-rh1x58`](small/claude__dithering-crystal-shepard-tone-rh1x58.md) | 2026-08-12 | Claude | 1 | — | feat(crystal): sell the Shepard tone with a screen door instead of transparency |
| 127 | [`claude/dog-fight-game-mode-it9xgy`](small/claude__dog-fight-game-mode-it9xgy.md) | 2026-08-12 | Claude | 1 | — | fix(dogfight): turret muzzle, AI break-off, target 90, four crystals |
| 128 | [`claude/gamecanvas-prefab-tools-r3ejlh`](small/claude__gamecanvas-prefab-tools-r3ejlh.md) | 2026-08-12 | Claude | 3 | — | Merge remote-tracking branch 'origin/bleeding-edge' into claude/gamecanvas-prefa |
| 129 | [`claude/crystal-destruction-audio-bug-xvqhek`](small/claude__crystal-destruction-audio-bug-xvqhek.md) | 2026-08-13 | Claude | 3 | — | fix(audio): make the burst gate carry blast magnitude, and fix two ordering flaw |
| 130 | [`claude/space-crystal-model-update-wcg14v`](small/claude__space-crystal-model-update-wcg14v.md) | 2026-08-13 | Claude | 3 | — | chore(crystals): TEMP - charge material on the space crystal for geometry check |
| 131 | [`fix/networkmonitor-prefab-dead-component`](small/fix__networkmonitor-prefab-dead-component.md) | 2026-08-13 | jtgjones | 1 | #713 | Remove dead NetworkMonitor MonoBehaviour from NetworkMonitor.prefab |
| 132 | [`claude/time-crystal-face-normals-5z8dg0`](small/claude__time-crystal-face-normals-5z8dg0.md) | 2026-08-14 | Claude | 2 | — | fix(crystals): restore the time crystal's facets, shaded from derived normals |
| 133 | [`claude/untested-backlog-qa-workflow-7a0nb9`](small/claude__untested-backlog-qa-workflow-7a0nb9.md) | 2026-08-14 | FenrysUnchained | 3 | — | Delete Docs/QA/RESULTS/2026-08-14-<caleb>.md |
| 134 | [`claude/managed-callbacks-performance-4g7vhc`](small/claude__managed-callbacks-performance-4g7vhc.md) | 2026-08-20 | Claude | 1 | #763 | perf(editor): cut domain-reload cost at the play-mode boundary |
| 135 | [`claude/unity-cli-pipeline`](small/claude__unity-cli-pipeline.md) | 2026-08-21 | Shombith03 | 1 | — | Add com.unity.pipeline for CLI-driven Editor verification |
| 136 | [`claude/cosmic-shore-ui-audit-r878o2`](small/claude__cosmic-shore-ui-audit-r878o2.md) | 2026-08-22 | Claude | 1 | — | docs(ui): add UI architecture audit for the HUD/menu redesign |
| 137 | [`claude/unity-cli-merge-prep-9cqns6`](small/claude__unity-cli-merge-prep-9cqns6.md) | 2026-08-22 | Claude | 1 | — | chore(skills): add the /verify-unity skill the CLAUDE.md gate references |
| 138 | [`claude/vessel-abilities-elements-bt7bcx`](small/claude__vessel-abilities-elements-bt7bcx.md) | 2026-08-24 | Claude | 1 | — | docs(vessel): add fleet completion push, fix elemental map drift |
| 139 | [`claude/9-slice-sprite-kit-tp0g7t`](small/claude__9-slice-sprite-kit-tp0g7t.md) | 2026-08-25 | Claude | 1 | #796 | feat(ui): 9-slice sprite kit for the corner-sliver shape language (T7) |
| 140 | [`claude/sparrow-crystal-sound-bug-bnocj6`](small/claude__sparrow-crystal-sound-bug-bnocj6.md) | 2026-08-26 | Claude | 1 | — | fix(audio): stop the Time crystal sounding through a muted SFX setting |
| 141 | [`claude/tool-codex-listing-vucv91`](small/claude__tool-codex-listing-vucv91.md) | 2026-08-28 | Claude | 3 | — | docs(codex): finish the Tool→Toy rename in prose |
