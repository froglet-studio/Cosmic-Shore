# Bug Hunt — where it stands (2026-11-21)

**Owner:** Yash. **Branch:** fixes land on `Ys-bleeding-edge` first and reach `bleeding-edge` with each merge.
**Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Seven machines send errors from Unity**, each to its own `bugledger/<user>` branch: Garrett (`gradi`), Shombith,
  Andrew (`owner`), jtgjones (`mrglo`), Yash (`yskha`), Caleb (`belac`), Will (`will`).
- **Ledger on 2026-11-21 (03:57 UTC sweep, 182 branches): 386 open, 35 being checked after a fix** (421 issues).
  The routine's first run with the repository attached: its push to `Ys-bleeding-edge` worked. Eight issues were new
  or seen again since the 11-20 report. One was code and is fixed (`65ffb622dd`, section 3); the rest are not code or
  already routed: the Unity profiler's own "Allocating too large buffer" assertion (E-ffb0263d28, no project frame),
  four failures of Claude's editor commands (`/api/exec` E-e5bc8c31c5 and E-270a8af1ef, `capture_game_view`
  E-d286e5f8e2, `list` E-fa89fca07b), the Rider folder on Yash's machine again (E-15034bdc73, 50 sightings) and the
  `weekly_challenge` board serving last week's row again (E-c8e2f49696, six machines, Shombith's backend, #1585).
  No new phone sessions were ingested; `check_gamelist_scenes.py` is clean.
- **Ledger on 2026-11-20: 383 open, 34 being checked after a fix** (417 issues; it was 406 open and 11 this morning).
  Every one of the 417 has an outcome, listed by id in section 5.
- **Why nothing was fixed from 2026-11-17 to -20:** the daily sweep is a cloud routine ("Bug Hunt daily sweep", 03:54
  UTC). It was recreated on 2026-11-16 with no repository attached, so every run stopped at its first push: the git
  proxy answered 403, "not in this session's authorized repository set". The routine now has
  `froglet-studio/Cosmic-Shore` as its source. A check run on 2026-11-20 15:39 UTC cloned the repository, and its
  push dry-runs to `Ys-bleeding-edge` and `bugledger/yskha` were no longer refused; the first real push is the
  2026-11-21 03:54 UTC run. The board's database writes work from that routine without an approval (its 11-20 run
  wrote `runs/2026-11-20`).
- **Fixed on 2026-11-20, nine commits on `Ys-bleeding-edge`** (section 3). None was run in the Unity editor: the
  editor was in Play mode for a multiplayer test all session. Each was compiled with Unity's own compiler (0 errors);
  the hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` § "Bug ledger fixes (2026-11-20)".
- **Already fixed, handed back on 2026-11-20:** 15 issues whose fix was in `bleeding-edge` but never marked on the
  sender's branch (section 3).
- **Three of the five "regressed" issues were never regressions.** The destroyed `VesselController` and destroyed
  `Player` errors (E-17461650a8, E-edc6a81429) carry line numbers from files older than their fix (`5787029688`,
  2026-09-21). The MPPM reflection error (E-1f1ff22829, 414 sightings) carries the package hash of Multiplayer Play
  Mode 1.6.1 in every sighting; the project has been on 2.0.2 since `8bcdde930`.
- **Not code, on Yash's machine:** Unity's external script editor points at
  `A:\Installed Apps\Rider\JetBrains Rider 2025.1.4`, a folder with no `plugins/rider-unity` in it, so the Rider
  package throws on every script reload (E-15034bdc73, 48 sightings). Pick the installed editor in
  Preferences > External Tools, or reinstall Rider.

## 1a. Errors from a build's console that the ledger never had (2026-11-20)

A development build showed "Scene 'MinigameElimination' couldn't be loaded" and the same for `MinigameMazeRun`. Neither
was on any ledger branch. Why:

1. **The ledger hears the editor only.** That console is a development build's. Nothing of it is in this PC's editor
   log or Windows player log, so it came from a phone or another machine's build. A phone's sessions reach Git only
   through a USB ingest (last one 2026-11-02) and land on `bugledger-android/<user>`, which the sweep did not read. A
   Windows development build sends nothing at all.
2. **The static check that exists for dead cards looked at rosters only.** `check_gamelist_scenes.py` read every
   `SO_GameList`. The vessel class assets' `Games` lists and the training games' `Game` field are not rosters, so the
   23 cards archived on 2026-10-13 stayed linked from there. On Windows the check also failed every card (backslash
   paths), so here it read as noise.

What was dead: 12 card entries in ten vessel class assets, the eight training entries of four of them, and **all four** entries of `TrainingGames`, the daily
challenge's whole pool (Block Bandit, Dolphin Darts, Maze Run, Slip and Stride), each leading to a deleted scene.

Fixed: the dead entries are gone and the four training games are in `Games/_Archive/`; the check now fails on any live
asset that points at a dead archived asset, and runs on Windows; the sweep reads phone sessions and runs the check
(`/bug-ledger` section 6). **Left for a decision:** `TrainingGames` is now empty, so the daily challenge reports itself
unavailable and the Hangar's training modal does not open, instead of loading a scene that is not there. Which live
games the daily challenge should offer is its owner's call.

## 2. What is blocking it

1. **Testers on old checkouts.** Each tester pulls `bleeding-edge`; their editor then takes the fixes and closes them
   after two clean play tests. Today's nine fixes are on `Ys-bleeding-edge` only until its next merge.
2. **Three fixes cannot be marked**, because their error text is shared by other causes and a mark would reopen as a
   false regression (`/bug-ledger` section 3). They stay open on purpose:
   - E-c703a8f80b "Some objects were not cleaned up when closing the scene". The `Cell0_RetiringWorld` cause (Caleb,
     Garrett, Shombith) is fixed in `d391dc761d`. The same text also covers `[PrismDebris]` (Andrew, 10-27),
     `[PrismRenderVisibilityFlush]` (Yash, 11-10) and `[PrismTimerManager]` (Will, 09-25), which were not looked at.
     A sighting after 2026-11-20 that names `Cell0_RetiringWorld` means the fix did not hold.
   - E-46ca22c3ef, a NullReferenceException whose signature is only the log filter's frame. Its 11-11 sighting is the
     conveyor's re-parent at scene close, fixed by the same commit.
   - E-b616bf230d "Rpc methods can only be invoked after starting the NetworkManager!". The crystal-explode caller is
     fixed in `e56a9f1d5d`; any other RPC sent with no session logs the same line.
3. **The Relay marks are already right.** The five Relay errors no longer read "validating `24ba982c8`" on Yash's
   branch: they are open with no fix commit, and belong to the multiplayer work (section 4). The one issue still
   carrying that commit is E-f4286d786f, Unity's own `Library/StateCache` file locked by a second editor or an MPPM
   player: a machine condition, never a code fix.

## 3. Fixed

### 2026-11-21 (not run in the editor; `unity_refcompile` OK)

| Error | Machines | Cause | Fix |
|---|---|---|---|
| "Local keyword _MW_STANDIN comes from a different shader" in `SolitonTunnelLensPass.SetStandin` (E-8ddade9b04) | one (Yash) | the keyword was cached once, and a shader edit or reimport during Play replaced the shader's keyword space | `65ffb622dd`: made from the current shader each call |

### 2026-11-20 (not run in the editor; compiled with Unity's compiler)

| Error | Machines | Cause | Fix |
|---|---|---|---|
| "Some objects were not cleaned up when closing the scene": `Cell0_RetiringWorld` (E-c703a8f80b, E-46ca22c3ef) | six | `CellConveyor.OnDestroy` struck its cells while the scene closed, and a strike makes a new GameObject | `d391dc761d` |
| "Trying to remove Slice (TextMeshProUGUI) from rebuild list" (E-e061201544) | five | the Voyages edge strip switched its label on and off inside the canvas rebuild | `e357fbad6c` |
| `[PrismOcclusion]` cannot measure the Serpent / the Jacks (E-5f2ee86d9b, E-d6b3d6f496) | two | the hull was measured once, at bind, while still hidden | `ba3ad0569a` |
| Destroyed `Material` in `UIThemeLive.SwapSea` (E-0f0eb8a59f) | three | a TMP sub-mesh's `material` getter clones a material that was destroyed | `c0ef0c8281` |
| "Destroying components immediately is not permitted" from `MantaDecoyCrystal` (E-3e99fba873) | two | the decoy was stripped inside `OnTriggerEnter`, so it kept its gameplay parts | `dedc985d2a` |
| "Rpc methods can only be invoked after starting the NetworkManager!" from a crystal (E-b616bf230d) | three | the explode RPC was sent with no session | `e56a9f1d5d` |
| NullReferenceException in `UrchinAnimation.Update` (E-75a12f0559, #1773) | three | the pods read the vessel before the hull was bound | `d8a7763cd0` |
| NullReferenceException in `HangarUpgradesPage.DrawFocused` (E-111d87fec0) | three | a redraw with no ability entry | `79f1ff888b` |
| Destroyed `ToastNotificationItem` (E-245165ae01) | four | the pool handed out an item destroyed with its scene | `540bac6a24` |

Their owners were told through `/approve`: Shombith, Garrett, Caleb (`Docs/Approvals/`).

### Already fixed, marked on 2026-11-20

| Error | Fix | Marked for |
|---|---|---|
| MPPM `ProjectDataStore` reflection error (E-1f1ff22829) | MPPM 2.0.2, `8bcdde930` | Garrett, Will |
| Destroyed `VesselController` / `Player` (E-17461650a8, E-edc6a81429) | `5787029688` | Garrett, Will, Andrew |
| Voyage scoreboard NullReferenceException (E-6b08617cca) | `37aac575d8` | Shombith |
| `ResolveAIAvatarSprite` null key (E-376e5a6f8d) | `be6f1058e4` | Garrett |
| Dolphin / Squirrel / Rampage assets did not parse (seven issues) | `8df5d3359c`, `5846aaab12`, `a227e1e2bb` | Will, Shombith |
| Urchin occlusion radius, Urchin skimmer container (E-5103c76904, E-5a15f5e924) | `f3b155dd30` | Will |
| Party recovery "Recovery failed (ArgumentNullException)" (E-0de9ec6c70) | `93765d6aa8` | Garrett, Will |

Earlier fixes are in each issue's ledger note and in `git log --grep "bugledger"`.

## 4. Left, and for whom

- **With the multiplayer work (81 issues):** Relay, transport, lobby, session and party errors, including today's
  two-machine test (E-0a098d80df, E-e5ff2abb8c, E-e6054bc064, E-fda866d511, E-ced2f8d740, E-cd8ad9dbfe,
  E-abc13c880b, E-b2c9301f4c, E-f4fd02cb76, E-afa8c57ab8) and the "address already in use" family on Yash's and
  Garrett's machines. One has a known cause nobody has fixed: `NetworkSceneObjectGuard.NeutralizeStray` strips a
  stray NetworkObject with `DestroyImmediate` inside a physics callback, which Unity refuses, so the stray stays
  (E-6bc7e07601, five machines; the B16 guard).
- **Shombith, backend (23):** Vivox is not set up for this UGS project (`'server' is null or empty`, 403 on the
  credentials), 401 / token refresh failures, two Cloud Save writes that failed after four attempts, and the
  `weekly_challenge` board with no weekly reset (#1585).
- **Charles, FMOD content (3):** Manta Boost Activate loop (E-cdf0c783c1, T-20261015-4996) and the two Morningstar
  events missing from the built banks (E-b6c18a87a2, E-eee0bbbdf4).
- **Owners' systems (14), not committed here:**
  - Garrett: the gyroid colony's seed-handoff check fails (E-faa24ff5b6, E-0221e91bca, three machines); the
    chain-fire prism effect fires a gun whose projectile pool was destroyed (E-debe17fb43, E-11a0f573e2, #1774);
    the Scarab's crystal morph needs `Solid.056 (EdgeArcs)` Read/Write enabled (E-f876d5f10a); the Honeyguide's
    impactor container has an empty crystal-effect slot (E-f8152d07a5).
  - Shombith: destroyed mask / RectTransform in a uGUI rebuild (E-4ae8006406, E-3233be5687, E-9d5b82005a,
    E-cbdc6be185; T-20261025-0c78, needs a capture); Block shown before the friends service starts (E-5bea1a27c8);
    the menu spotlight runs on after its target is destroyed (E-15b4eb64f0, with Caleb); Serpentine's card lists no
    vessel the player owns (E-1f8f2b4960).
  - Arcade preview: "no gameplay camera to lend" (E-1eea72bc60, four machines), a deliberate diagnostic.
- **Needs a capture (23):** errors with no project frame, or seen once on code rewritten since. Among them Unity's
  `t.GetParent() == nullptr` assertion (E-73d255442d, three machines), a uGUI mesh over 65,000 vertices on
  Shombith's machine (E-281310eb70), the wormhole's "Screen position out of view frustum" (E-fcf50f15ac, not seen
  since the wormhole was rebuilt on 11-19), the `[AppManager] ... not found at injection time` family in edit mode,
  and Skein's "Turn started with no course" in Play (E-598aa7898c).
- **Silent for a month or more (57):** last seen before 2026-10-21 on machines that still send. Not looked at
  one by one; a new sighting brings one back to the top of the next sweep.
- **Not code (179):** machine environment (142: Garrett's video codecs, `Library` files locked by a second editor,
  Unity account and network errors, a build profile without the Authentication scene, the Rider folder above),
  Unity editor internals with no project frame (24), Claude's own editor commands failing (12:
  `capture_game_view`, `/api/exec`, the AI assistant's `connection.state_change`), and the ledger's own test (1).

## 5. Every issue's outcome (2026-11-20)

Ids without their `E-` prefix. FIXED: fixed today. ALREADY: fixed earlier, marked today. VALIDATING: being proven
since before today. MP: with the multiplayer work. ENV, EDITOR, TOOL, TEST: not code. BACKEND: Shombith's. FMOD:
Charles's. OWNER: section 4. CAPTURE: needs a capture. STALE: silent for a month or more.

- FIXED (11):
  0f0eb8a59f 111d87fec0 245165ae01 3e99fba873 46ca22c3ef 5f2ee86d9b 75a12f0559 b616bf230d c703a8f80b d6b3d6f496
  e061201544
- ALREADY (15):
  17461650a8 1f1ff22829 2f25b16a12 376e5a6f8d 4065bcae2a 4ad9e5ca21 5103c76904 5713660fbd 5a15f5e924 6020da4524
  6b08617cca 8780898fab cb11787d50 edc6a81429
  0de9ec6c70 (party recovery after Stop, `93765d6aa8`)
- VALIDATING (11):
  14bef8c883 251530c87b 3455204aad 603d7f129d 76248f9266 76d7341dae 79b40242f7 7a0e239d28 95af7aa88a c52c39070d
  f3658978b8
- MP (81):
  002734c541 0125d8211e 0a098d80df 0a427c50aa 0edf9dfeaa 0ef8eba542 183fb7b4d5 187231bd6b 1a188c52a2 1fd0145e5d
  2171c71550 3cf2e96718 3e1ec07e1f 3e9077e16b 401a4f6ec5 4559c252c9 4c0f5f8d33 4d0d17e44b 4e2cacc9d8 50d1b641dc
  554961be6d 55c19c8f2b 5692ad45af 5b3fdac4a4 5fa838820d 625609559e 62ac69194a 69d8f59062 6b79cd07f1 6bc7e07601
  6f8a396c25 71cedd035d 71e3ad3027 756b8af285 7d8ce3b9c2 81100c14a5 8186f3205f 849115e303 853e9f9090 8ab5bbf2a3
  908f18b36f 90ae02dce6 9a3e719250 9befe1d612 9e181c28f7 a22e91689e a2a3f23a3c a9ca990531 abc13c880b abcd20023a
  ad793f31fe add091a12b afa8c57ab8 b2c9301f4c b50054597d c223e6e8e9 c2a4d444b8 c407a02dbc c4b8b2d53e c523697cae
  c93e2a9474 cd8ad9dbfe ced2f8d740 d038e41c9d d0e25fe166 dc45515190 e0844806c9 e0d6e82a03 e3eed3e493 e5ff2abb8c
  e6054bc064 f18360ba1d f1dc9191b6 f4fd02cb76 f900f1f48d fa0982802a fa3a8bf2c0 fae86e6882 fda866d511 fdf6dcdfc7
  ff763c29c3
- BACKEND (23):
  078446dd03 2716e55218 33876f4909 4463d4fc1c 5c27bd1550 742744315c 8a0a0c4bb4 8e979cd5be 9b3f50bb34 9f61a934b0
  a8cb6be7ff aa2bfdf370 ac1a17e953 ae8d2e4fa2 af714de326 b389bd51d7 b7e16abd76 be1ab728fa c8e2f49696 ca127debd0
  e5c44d7377 e7bdcce56e fffbda0c5b
- FMOD (3):
  b6c18a87a2 cdf0c783c1 eee0bbbdf4
- OWNER (14):
  0221e91bca 11a0f573e2 15b4eb64f0 1eea72bc60 1f8f2b4960 3233be5687 4ae8006406 5bea1a27c8 9d5b82005a cbdc6be185
  debe17fb43 f8152d07a5 f876d5f10a faa24ff5b6
- CAPTURE (23):
  05e4645aad 1721f99ea8 1b416ae026 1e7b21606a 22344fbacd 2367abc157 281310eb70 41c5e7a809 4445f3521c 57b430e550
  598aa7898c 73d255442d 78e5f3122b 80426dcfb6 8a31922102 93bb3fc04a 99d4d23e58 9fce069a8c b872184a91 bb0801d0dc
  f19547a0fb f6825303a7 fcf50f15ac
- STALE (57):
  003c05338a 0063ca5460 0084b9a5a6 0febd0fa90 18bfa45b5a 1a37dc048f 1bac401028 1ea298a4bc 1f2b9b867b 20f939a2fa
  2294d7e48b 24400ce3c5 28083172e6 2fdd4bd1ee 32addbd21c 3c585ab5f3 54be168eb9 58954f7f1c 5bf4159451 6bde731994
  768535ee5a 79952bd865 7c9f07bec9 7eb0b887f2 818bd55cce 84237fd8d2 89bf9930c1 9ed5f7c109 9f08f3d857 9fe3e1b54c
  a717aff812 a9dd76681a aaff34252c ae3f066ffb b1dc953670 b23157117d b8c609adec c06496382a c11906bacf c752caf6be
  c928286e9f ca00d5ef36 d7cf8db622 db8386c321 df6378a670 e5defe493b e65af9197f ee5d3d0eab efc775ff71 efda4b2237
  f1681b036f f3a44fed1c f617c39622 fa9f643cd7 fc9c4c779c fcf19bd06c febb83dcaf
- ENV (142):
  0061e6c5f7 011f8d520d 01b3e5fd59 01cf00ddb4 0397b44bed 046076db36 0507a960f6 05438e1d61 084a85b821 0a832d3776
  0ac0d710cc 0adce869a9 0c7c8b3d0f 0e219b2c5d 102354e1f2 15034bdc73 15885ab856 15e3c23f46 16885c8baa 19b5e2e213
  1d865224da 1e1a6dabc9 1fbcdd5e4d 208ecf1d88 21903a14f4 21b9961e80 247fc88ce0 25b4ba509d 273c2f99c5 2a061815b6
  2be77d57a6 2cd72006ea 2eb26581a5 2f27c26cfe 33f6725d3d 3b7cdfcf7d 3cf50682ff 43613e6a88 46e1d3f89c 492b512064
  4a47abc679 4add71f9f5 4bd74219e7 4c62e1381c 4ca5823107 4cd7f6d77b 4f543b3fb8 516daa47ca 54b04ca859 54eba394a6
  5704e118df 57800baab0 57a239cbed 5b908ffe22 652847526d 65638634f3 66c1bee5bf 69c55a96e3 6a2fe16801 6c5e0609b0
  707d3265ce 70e05e3f21 7229201e16 74aef63b3d 7712c9fd63 7799a58803 789aa7a820 792cc7aad1 7d2fae3ddf 7fa56946fe
  8162d08fd4 818ec86bd7 82eb7b2aa2 843006179c 85c9d9ad01 86049370b8 888f918ae4 892cf10314 8d31fa49f2 8f8126a76c
  907adc2b84 967cc14e66 972e307f5e 97c743d574 98a4a68b3c 9ac7e65c2e 9adaaaec66 9f61829c75 a09b67493a a0bd740b03
  a2f5ccdc6a a37b5d90cb a5c45a9a45 a6f683ebb4 a771e8a767 aa2f68903d ac39b2bbe4 acd6b94d3e b02e474cf6 b260a49c69
  b2929e9de8 b2d3e3baa6 b3d043826e b55bf6c7c8 b72940176d b8568409d5 bc503bb960 bd62debc1c c021090fec c04371ccbc
  c04e06d361 c1dffb3937 c72b4a2f90 c758aee5a9 cb6faeeaa6 cde434cc0a ce4f856488 cea5aa7fe7 d257533757 da61ed0d72
  dbe3705efe dd951e4e48 ddabadd8ad ddb09125ab e0969253ac e2135e0133 e349862f50 e4bb9080ed eb8720ddfc ece1330de5
  ef80d03980 f0f2e21850 f2200431ab f4286d786f f42fdb54dd f89b2c13ae fc9629a670 fd64b43840 fe131fc992 ff51e7bd22
  ffa14aaf30 ffb4a34861
- EDITOR (24):
  01a3ab701c 05bd04a49e 19fa85f54b 1aef017e7a 1e767a3761 31bed942ff 3424ad79f9 3a31ac8c46 3a3f07efa7 3f1dc6d742
  3fc7c12978 41e1c36fe8 472f1ed686 5079fe4e71 6b4acf8b65 8326779b3c 8bc1875f13 9dca713157 a574f06fb0 b846bc69af
  c6177045e1 d6a7052ce2 df312d6546 e476e11261
- TOOL (12):
  15940b78c5 1f8c574eac 3c306eac58 49e7cdae67 61bdbed499 6c008f062d 8909c619e5 d286e5f8e2 d8093775c7 e339480c7b
  e5bc8c31c5 f668e8c5a5
- TEST (1):
  8cd155dc89

## 6. Performance (`perf_hunt.py status --refresh`, 2026-10-25)

Gates all OK. **Baselines are per machine since 2026-10-25** (`scene|label|user`; each tester's first valid capture of a
scenario is their baseline, and the 17 older baselines went to the tester whose capture they were). Only each
machine's NEWEST capture of a scenario counts. The flagged regressions fell from 184 to 35; most of the 184 were Caleb's
and Andrew's captures judged against Yash's faster machine. What still stands:
- Grizzly Charge (jtgjones, 10-25): `LateBehaviourUpdate` 0.40 -> 2.44 ms, CPU +35% (T-20261017-19f0).
- Garland (Yash, 10-26): `UGUI.Rendering.UpdateBatches` 0.82 -> 4.65 ms.
- Menu_Main S2 (Yash): `BlackHole.Tick` / `BlackHole.Field` ~0.01 -> 2 ms (T-20261017-9990).
- Regatta (Caleb, 10-16): GC per frame 47 -> 89 KB (T-20261017-2129).
- Andrew's Garland and Bloomrush: 19 lines against his own first capture; check one more capture before acting.

T-20261017-1809 closed: Caleb's next capture read its two markers within his baseline. Open capture tasks:
T-20261015-56dd, -3e0b, -3972, -1551, -7edd.

## 7. Latest activity (generated)

This block is refreshed by `python3 Tools/Build/team_report.py area bug-hunt --write`.

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-21 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-11-21 `abd59fc71` bugledger: E-8ddade9b04 fixed in 65ffb622dd — on another branch
- 2026-11-21 `1d6969e80` bugledger: 81 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-20 `3f070b974` bugledger: 123 open error(s) from Shombith's play tests, 12 report file(s) — on another branch
- 2026-11-20 `eb61686fb` chore(tooling): retire Froglet Tasks for Swim Lanes — on bleeding-edge
- 2026-11-20 `fbb1961a4` docs(bug-hunt): why a build's scene errors never reached the ledger, and what the sweep reads now — on Ys-bleeding-edge
- 2026-11-20 `1052af91a` bugledger: 80 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-20 `bcd3f0f58` bugledger: E-0de9ec6c70 fixed in 93765d6aa8 — on another branch
- 2026-11-20 `a41069da5` bugledger: E-245165ae01 fixed in 540bac6a24 — on another branch
- 2026-11-20 `8dfdf286b` bugledger: E-111d87fec0 fixed in 79f1ff888b — on another branch
- 2026-11-20 `8f66c0b78` bugledger: E-75a12f0559 fixed in d8a7763cd0 — on another branch
- 2026-11-20 `dbcabd178` bugledger: E-3e99fba873 fixed in dedc985d2a — on another branch
- 2026-11-20 `9b2037358` bugledger: E-0f0eb8a59f fixed in c0ef0c8281 — on another branch
- 2026-11-20 `b32a4b54a` bugledger: E-d6b3d6f496 fixed in ba3ad0569a — on another branch
- 2026-11-20 `514077b93` bugledger: E-5f2ee86d9b fixed in ba3ad0569a — on another branch
- 2026-11-20 `239e3b0d2` bugledger: E-e061201544 fixed in e357fbad6c — on another branch
- 2026-11-20 `ed026cec8` bugledger: E-5a15f5e924 fixed in f3b155dd30 — on another branch
- 2026-11-20 `3edbebf4e` bugledger: E-5103c76904 fixed in f3b155dd30 — on another branch
- 2026-11-20 `199ee36a3` bugledger: E-6020da4524 fixed in a227e1e2bb — on another branch
- 2026-11-20 `982bfb749` bugledger: E-4065bcae2a fixed in a227e1e2bb — on another branch
- 2026-11-20 `e6d12f1c2` bugledger: E-cb11787d50 fixed in 5846aaab12 — on another branch
- 2026-11-20 `baf7f83fa` bugledger: E-2f25b16a12 fixed in 5846aaab12 — on another branch
- 2026-11-20 `283ae157d` bugledger: E-8780898fab fixed in 5846aaab12 — on another branch
- 2026-11-20 `d81969b29` bugledger: E-5713660fbd fixed in 8df5d3359c — on another branch
- 2026-11-20 `d05cfe7ab` bugledger: E-4ad9e5ca21 fixed in 8df5d3359c — on another branch
- 2026-11-20 `7d8dceecd` bugledger: E-376e5a6f8d fixed in be6f1058e4 — on another branch

### Also in flight on other branches
- `bugledger/yskha` (Yash, 2026-11-21): bugledger: E-8ddade9b04 fixed in 65ffb622dd
- `bugledger/shombith` (Shombith, Yash, 2026-11-21): bugledger: 123 open error(s) from Shombith's play tests, 12 report file(s)
- `bugledger/will` (Yash, ownerbynight, 2026-11-20): bugledger: E-0de9ec6c70 fixed in 93765d6aa8
- `bugledger/gradi` (Garrett, Yash, 2026-11-20): bugledger: E-0de9ec6c70 fixed in 93765d6aa8
- `bugledger/owner` (Andrew, Yash, 2026-11-20): bugledger: E-245165ae01 fixed in 540bac6a24
- `bugledger/belac` (Caleb, Yash, 2026-11-20): bugledger: E-245165ae01 fixed in 540bac6a24
- `claude/project-thread-jw78fe` (Garrett, 2026-11-18): test(proven-win): vessel hull off-screen skinning rejected (~3 us per hull)

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->
