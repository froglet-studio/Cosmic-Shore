# Handoff prompts — 2026-10-18

Each prompt below is self-contained and has a task on the team board (Docs/TEAM_TASKS.md): paste it as the first message of a new Claude Code session on
`froglet-studio/Cosmic-Shore`. Commit hashes were verified against `origin/bleeding-edge` @ `bfd6347973`
and `origin/Ys-bleeding-edge` @ `b2bd220bb3` on 2026-10-18.

---

## 1. Quick fixes (one session, anyone — base `bleeding-edge`)

Board task: **T-20261018-fde3**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
Five small, independent fixes. Each one exists already as a commit on an old branch that can no
longer be merged as-is. Re-apply each one onto current bleeding-edge as its own commit (conventional commits,
see GIT_RULES.md), then open ONE PR into bleeding-edge. Follow CLAUDE.md: any C#/prefab change passes
/verify-unity before commit (if the CLI isn't available, say so in the PR and add a row to
Docs/UNITY_VERIFICATION_CHECKLIST.md). Never hand-edit a prefab to "apply" a scene override.

1. Time crystal plays through a muted SFX setting.
   Original fix: c30040ec96 (branch claude/sparrow-crystal-sound-bug-bnocj6). On bleeding-edge,
   Assets/_Prefabs/Environment/CrystalTime.prefab still carries two StudioEventEmitter components
   ("Creature colide", fileIDs 3568651701759605074 [TriggerEnter] and -3326657406894871218
   [ObjectDestroy]). They start FMOD events without owning the instance, so per-instance volume
   (the SFX mute) can't reach them. Remove both from the CURRENT prefab (it has changed since:
   71aaf289a1 stripped its AudioSource), check OldCrystalTime.prefab and the other element crystals
   for the same pattern, and record the rule ("a component that starts an FMOD event without owning
   the instance can't honour per-instance volume") in Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md
   (the old commit put it in CLAUDE.md, which has since been split).
   Check: in the editor, mute SFX, collect a Time crystal as the Sparrow — silence.

2. Recycled shielded prisms play the shield-off effect.
   Original fix: bbb47c111b (branch claude/prism-clock-followup-prompts-two-mhvad9).
   Assets/_Scripts/Controller/Toys/Microscene.cs RearrangeIntoAsync still calls
   PrismKinds.Clear(block) (~line 249) BEFORE block.Initialize() (~268), then PrismKinds.Apply
   (~279), so the live shield-disengage runs on every recycled shielded prism. Port the fix: one
   PrismKinds.Retheme(block, lay.Kind) after Initialize (Retheme exists in
   Assets/_Scripts/Controller/Environment/Spawning/PrismKinds.cs), plus the §4.5(b) ordering note
   in Docs/PRISM_ANIMATION.md. Mirror the same change in the Port copy
   (Port/src/CosmicShore.Game/Controller/Toys/Microscene.cs, same three calls at ~158/177/185).
   Check: Wander toy recycle and HexRace — no shatter overlay, no ShieldDeactivate SFX on recycle.

3. The /ab-test skill. Cherry-pick 691cd25d6e (branch cece/upbeat-knuth-f873tn): adds
   .claude/skills/ab-test/SKILL.md, Docs/Studios/ABTests/README.md + stoat-sling/index.html and one
   Docs/claude/DOCUMENTATION_INDEX.md row. Purely additive, applies clean. Also note in the PR an
   unresolved finding from that session: the Stoat prefab turns at 48°/s but StoatFlightStudio.html
   flies it at 120°/s — report which is intended, don't change either.

4. The detect-competing-changes skill. Cherry-pick 7380381338 (branch cece/bold-euler-59ugby):
   .claude/skills/detect-competing-changes, salvaged with four bug fixes. Two new files.

5. The session-branch triage tool. Cherry-pick 13834d5584 (branch cece/wizardly-euler-1ilgln):
   Tools/Build/triage_session_branches.py. Run its own self-test if it has one; if it duplicates
   something already on bleeding-edge, drop it and say so in the PR.

Doc drift to fix in the same PR (docs only):
- Docs/BRANCH_PARTS_STATUS.md says nothing from cece/nice-brahmagupta-ojclij is on bleeding-edge;
  parts A, C and M landed (9d9e713977). Update those rows.
- Docs/ECOSYSTEM_MASTERPLAN.md Phase B item 6 ("UpdateDomain known gap") is done:
  Prism.HandleTeamChangedForCell calls PrismSpatialIndex.UpdateDomain. Mark it done.

Done when: one PR, five fixes + two doc rows, CI green, each in-editor check listed in the PR body
as done or explicitly owed.
```

---

## 2. Yash — the Tether onto Ys-bleeding-edge (team task T-20261017-e64b)

Board task: **T-20261017-e64b (on Ys-bleeding-edge's board)**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
Do team task T-20261017-e64b (Docs/TEAM_TASKS.md on Ys-bleeding-edge) and CONCEPT_FLEET.md §8.1
step (2): bring the Unity Tether prototype on cece/loving-feynman-hlgf18 onto Ys-bleeding-edge.

Why this matters: the Vessel Studio Gibbon (studio-kinds.js `tether` and `web` kinds,
84c9ba3321 / 6e8487298a) ported the Tether's BEHAVIOUR to JavaScript, but the Unity
implementation exists only on that branch (~7,900 lines, none on Ys-bleeding-edge or
bleeding-edge): TetherMath.cs + TetherMathTests.cs (378 lines), TetherExecutor, TetherVisuals,
TetherConfigSO / TetherConfig.asset, TetherLongLineActionSO, LongTetherRope,
TetherVesselTransformer, the VesselPrismController stated-pose trail lay, Tools/Build/tether_harness,
and R_VesselActions/TETHER.md. Its draft PR is #1040 (into bleeding-edge) — leave it open until this
lands, then close it pointing at your merge.

Steps:
- Merge (not rebase — Garrett's branch) cece/loving-feynman-hlgf18 into Ys-bleeding-edge.
  Expect ~12 conflicts: VesselClassType, DefaultNetworkPrefabs, Vessel Prefab Container,
  ToyVesselRoster, VesselTransformer, and an add/add on Tools/Build/check_network_prefab_hashes.py.
  Keep Ys-bleeding-edge's copies of the hash gate and of the two VesselTransformer seams that already
  landed (f091e86a2b: ComputeExternalAcceleration, NoseConvergence); take the branch's third seam
  (AnalogTriggerDrift) and everything Tether-specific.
- Tether = 14 collides with Thresher. Take the next free VesselClassType id — re-check every branch
  before claiming (as of 2026-10-18: 15 Stoat, 16 BrittleStar, 17 Angler, 18 reserved/claimed, 19
  Wrasse/Kite claims; 20 looked free). Update EnumIntegrityTests and TETHER.md §12.
- Run Tools/Build/check_network_prefab_hashes.py, the edit-mode suite, Tools/Build/tether_harness/run.sh,
  and /verify-unity.
- Done when the Tether flies from Toy Box > Vessel Changer on Ys-bleeding-edge with its own id and
  the suite is green (the task's own Done-when).

Reference only (do NOT merge; both are superseded by the Tether, read them for ideas):
- cece/hopeful-bardeen-rmqhmd — the earlier Unity Gibbon spring-tether flyer (TetherSolver,
  GibbonTetherExecutor, GIBBON_TETHER.md). Everything reusable already moved into the Tether.
- claude/spider-momentum-physics-8jlit2 — the original rigid-rope Gibbon, plus the ONLY copy of
  the Canopy Run brachiation race (CanopyRunController, SpawnableCanopyTrack, MinigameCanopyRun.unity,
  author_canopy_run_assets.py, CANOPYRUN.md) and GIBBON.md. Its VesselClassType 13 and GameModes 45
  ids are taken now. If the web-slinger Gibbon gets kept, Canopy Run is the natural first mode for it —
  note that in CONCEPT_FLEET.md §8 so the idea isn't lost.
```

---

## 3. Yash — presence converge releases the lobby it just joined

Board task: **T-20261018-2a60**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
A party/presence race is still live on bleeding-edge. In
Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs, ConvergeToCanonicalAsync
awaits the join, then calls DeleteOwnLobbyQuietlyAsync(), which re-reads _activeLobby and nulls it,
then sets _activeLobby = joined. If JoinOrCreate already moved _activeLobby to the canonical lobby,
the converge releases the lobby it just joined (reconnect churn).

The original fix is 03f6ad7543 on claude/host-connection-refresh-error-85c6a2 — it no longer merges
(conflicts in HostConnectionService, PresenceLobbyService, Docs/PresenceSystem/ARCHITECTURE.md and
BUGS.md) and bleeding-edge has since landed most of the surrounding work independently (BH-2.2
backoff rejoin + watchdog, Reconnecting exit edges, the NotInLobby/B32 fix, the occupied-lobby
defer, single-flight presence:join:{id}; see B31–B33, ca41ed69f3).

Port ONLY the core change: capture the outgoing session/lobby before the join, and release that
captured instance (ReleaseQuietlyAsync(captured)) instead of re-reading _activeLobby after the
await. Bring _membershipBusy across only if the current code still needs it. Mirror in
Port/src/CosmicShore.Game/Controller/Party/Services/PresenceLobbyService.cs if the Port copy has the
same shape. Add a BUGS.md entry. Follow the party/presence threading rules in
Docs/claude/ARCHITECTURE_CORE.md (main-thread affinity). Then run Docs/PresenceSystem/TESTS.md P8 in
MPPM (reconnect churn) and /verify-unity. PR into bleeding-edge.
```

---

## 4. Yash — Rhino crystal-absorption hull morph (spike → keep/drop)

Board task: **T-20261018-f1f5**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
cece/trusting-albattani-p1sd96 has one commit, e9002489c5: VesselCrystalAbsorbMorph (+ tests), a
Rhino.prefab wiring and VESSEL_CRYSTAL_ABSORB.md — a collected elemental crystal morphs onto the
Rhino hull. It was never run in the editor and never PR'd. Since then bleeding-edge moved
ElementalCrystalImpactor.cs to Controller/ImpactEffects/Impactors/, rewrote CrystalMorphMeshBuilder
(~690 lines, 2f6c08d065 adds an EmitCache) and heavily changed Rhino.prefab; a merge conflicts in
Rhino.prefab, CSDebug.cs and ElementalCrystalImpactor.cs.

Load the /vessel skill first. Re-apply the spike on a fresh branch from bleeding-edge against the
new builder and impactor path (don't hand-merge prefab YAML — rewire through the editor or the
asset-surgery tooling). Run /verify-unity, then fly the Rhino, collect crystals and record a short
capture of the landing radius / whiteout using the tuning table in §6 of its doc. Open a DRAFT PR
with the capture and ask Garrett for keep / drop. Don't merge without his keep.
```

---

## 5. Yash — crystal wormhole rebuild + "under 10 s" retune vs the new lens

Board task: **T-20261018-1a4a**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
cece/charming-cerf-alf1j1 (Black Hole cell selector) has two commits that never landed:
2ff034ae2b (the crystal wormhole rebuilt as one geometry — ThroatWarp, CrystalWormholeView, the
CrystalWormholeLens shader and simulator) and 86ce29027d (a playtest-requested retune: "warp less,
smaller poles, stronger suck and spit" — Sparrow sink→source 23.7 s → 6.2 s). The earlier Black Hole
cell + first wormhole DID land (b704b19ef0, eb902588a6 via b9c90205cb).

Since then bleeding-edge rewrote the black-hole lens (da2027490f: "the lens is the Vessel Studio's,
one pass for every hole"), so the branch conflicts in 11 files (CustomCameraController,
BlackHoleLensPass, BlackHoleGravityField, CrystalWormhole, docs). Do NOT merge the branch.

Task: on current bleeding-edge, measure the Sparrow sink→source time through the crystal wormhole.
If it's already under ~10 s and the playtest asks (less warp, smaller poles, stronger suck/spit) are
met by the new lens, write that up and stop — the branch is then obsolete. Otherwise port only the
TUNING from 86ce29027d onto the new lens pass (not the old rebuild), /verify-unity, and PR with
before/after numbers for every ship.
```

---

## 6. Yash — keep landing the project-sprint parts (optional, larger)

Board task: **T-20261018-a251**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
cece/nice-brahmagupta-ojclij holds ~82 commits not on bleeding-edge, grouped into parts B, D–L
(Docs/BRANCH_PARTS_STATUS.md; editor test steps in that branch's
Docs/BRANCH_TEST_PLAN_2026-10-12.md; picker Tools/Build/pick_branch_parts.py exists only on that
branch). Parts A, C, M already landed (9d9e713977). Two weekly scheduled tasks read this branch
("Weekly Prisma AI training", Sundays — until BotProfile.cs reaches bleeding-edge; "Weekly Skim Race
pilot tune", Wednesdays — SKIM_RACE_AI.md §6.13/6.14), so do not delete it.

Start with the cheapest parts: H (Unity-side parity harness: Utility/Replay/*, ParityCapture) and K
(leak fixes: VesselEffectCooldowns and others). For each: run its editor test steps, pick with
`pick_branch_parts.py --base origin/bleeding-edge`, /verify-unity, PR, then update
BRANCH_PARTS_STATUS.md. ~75 of the files the branch touches have changed on bleeding-edge since —
re-validate, don't blind-pick. Blocked on people: B needs the sound designer to save
Cosmic Shore.fspro first; L needs Garrett's sign-off on its new LOCKED ecology rule and on making
worm fission a mutating birth. F (Skim Race I4 retune) must be re-tuned against today's
SkimRaceAIConfig_I4.asset (it gained a TrackFingerprint field), not picked.
```

---

## 7. Garrett (or whoever owns copy) — market analysis docs

Board task: **T-20261018-a464**. Close it with `python3 Tools/Build/team_tasks.py done <id> --note "..."` when the work lands.

```
claude/games-market-analysis-uxjcur has three doc commits never landed (~3,950 lines):
22d2972861 (Docs/MarketAnalysis/MARKET_GAP_PLAN_2026 + index), fdc06af027 (research and audit
evidence digests, SOURCES) and 0209a31fce (Docs/WORLD_AND_COPY.md: "the HyperSea is a sea, and the
product is four places in it"). A merge conflicts only in CLAUDE.md (now an index over Docs/claude/ —
put any index line in Docs/claude/DOCUMENTATION_INDEX.md instead).

Before landing, refresh the facts: the copy says "thirteen modes / eight ships"; today's roster is
~31 launchable games and more hulls (count from Assets/_SO_Assets/Games and VesselClassType). Then
add the seven pitches from that session's last message (session_01GS2mpWTMXV991uMvShUTjk — read
it with the claude-code-remote list_events tool, kinds ["assistant"]) as a "Pitches" section under
the line set in WORLD_AND_COPY.md (the session was waiting on a yes for that). Docs-only PR into bleeding-edge.
```
