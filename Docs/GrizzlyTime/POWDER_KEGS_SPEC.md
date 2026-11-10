# Grizzly Time: Powder Kegs, the final build spec

**What this is.** An upgrade of `GameModes.GrizzlyTime = 63`. It keeps the same scene, generator and HeadlongCircuit solver, and 2 laps x 8 rings = 16 gates.

**What it is built from.**
- The base is the judges' winner, Powder Kegs (2 of 3 judges picked it).
- Grafted from Brave Line: the AI safety rules, the silence-aware rivalry, and the mouth ladder.
- Grafted from Demolition Circuit: crown and finale feedback, cut down so it adds no prism mass near the racing line.

**Checked this session (read-only, `git fetch origin` run, 2026-11-11).**
- Toast ids:
  - origin/bleeding-edge has `ShatterplaneHint = 165` and `HarvestHint = 163`. 164 is not on bleeding-edge: it is `GlasshouseHint`, in flight on origin/claude/peaceful-cerf-vpl3x8.
  - Scanning GameToastSituation.cs on the 200 most recent origin refs found nothing at 166-179.
  - The local checkout is stale and shows 161.
- `GrizzlyTimeController.cs` on origin is 38 lines and overrides only `ModeName` and `CreateCourseSource`. Its summary at :10-17 is stale.
- The only in-flight branch touching Grizzly Time paths is origin/grizzly-v3 (2026-10-25), on `GrizzlyTimeCourse.cs` and `GrizzlyTimeCourseTests.cs`.
- APIs:
  - `ThresherTimeNest.PassesNear(Vector3 centre, Vector3 prev, Vector3 cur, float radius)` at ThresherTimeNest.cs:95, and `Build` at :104.
  - `Prism.ActivateShield()` at Prism.cs:2142.
  - `PrismProperties.IsShielded` at PrismProperties.cs:16.
  - `Prism.DeflectsSilenced` at Prism.cs:2095, called from `Damage` at :1873.
  - `RaceCourseSource.TryStartLine` at RaceCourseSource.cs:142, and `LeadInGates` at :111.
  - Serpent Time's `ThreadsGate`, `OnLocalNextGateChanged` and `LightCluster` at SerpentTimeController.cs:709/718/724.
  - Thresher's `NestOwnerId`, `Holds()` and `HullWithin` at ThresherTimeController.cs:75/158/226.
- Line numbers on GateRaceController (:331 AICommitFromApproachPoint, :363 ThreadsGate, :374 OnLocalNextGateChanged, :442 TryOverrideAim, :870 OnCourseRaised, :949-959 the -1 at finish, :1260 OnRacerArmed, :1289 AINoseLeadTable) and VesselTransformer.cs:1628 (the clamp) come from the judges' checks; I did not re-open them.

**Nothing below has been flown, run in Unity or run in a harness.** Every time and percentage is code reading plus arithmetic.

---

## 1. Pitch and core loop

**Pitch.** Every gate has a powder keg beside it. Freeze a bomb in the keg, get round to the keg's far side, and blow it. The keg bursts, the gate is yours, and the blast throws you down the next leg. At the hard levels you also choose which keg each corner wants. A pilot silenced by a rival's blast cannot open kegs, so for those 8 s they have to thread ring mouths instead.

**The kit fact everything rests on.** The bomb's self-launch pushes the hull AWAY from the bomb: direction = hull - blast (GrizzlyTriggerBombExecutor.cs:476-503, judge-verified).
- The push is world-space: 300 u/s ceiling for 1 s, about 260-300 u of carry.
- Cruise is 50 u/s at 95 deg/s, a 30 u turning circle.
- A full blast reaches 100 u; a tap reaches 25 u.
- So **where you stand when you blow the bomb is your steering**. The keg gives the bomb a target and gives that steering a purpose.

**One leg (legs are 475-610 u):**
1. **Key.** From cruise, fire a full bomb at the keg. The lime next ring frames the gate (LightLocalNextGate). Freeze the bomb within 100 u of the keg's core.
   - The key must be fired from cruise: a bomb fired mid-launch leaves at about 140 u/s against the hull's about 350 and drops behind.
2. **Swing.** Use the 30 u pivot to put the hull on the keg's exit side (keg -> next gate), within 100 u. From I2, chalk rails show that lane in the world.
3. **Blow.** One detonation does three things: it opens the keg, it credits the gate (the hull passes within the credit radius of an open keg), and it throws the hull down ExitDir.
4. **Carry (optional).** From the next stretch of cruise, fire, freeze and ride a second bomb. Riding it sums under the 300 clamp.

**The safe fallback.** The ring mouth still credits (`base.ThreadsGate`). Kegs sit 140 u off the ring centre, so the old line through the mouth meets no new mass.

**Mastery:**
- **Carve:** blow the keg while the previous launch is still live. The vectors sum and clamp at 300, so the carry bends instead of dropping to cruise. Nothing is coded for this.
- Pick your keg at I4.
- **Draft:** a rival's blast opens your local keg for burst time plus the regrow time.
- **Rival play:** a Grizzly blast that catches a rival is a MissileBlast hit, not the "Strike" the designs called it. `VesselCombatHitByGrizzlyBlast` has hitClass 3 = MissileBlast (CombatHitClass.cs:54), worth 20 points (CombatHitDrain.cs:88) and 8 s of silence, the cap (SILENCE.md). A silenced pilot's blasts and rams deflect off every Blue prism (Prism.cs:1873). Pinning the leader therefore sends them to the ring mouths for about 2 gates. PvP stays petals-only and uses the shipped fleet rule.

## 2. The I1-I4 ladder (one new demand per level)

All numbers live in a new pure static `GrizzlyTimeKegs` (section 6, step 2), except the mouth radii.

Shared by every level:
- Mouths **96 / 84 / 74 / 66 u** (shipped: 96/72/58/46). No step is larger than 12.5%.
- Keg: 3 core blocks (Blue) plus a 12-slab Plain hoop at radius 16 u = 15 prisms. Its centre is at `KegOffset = 140 u` from the ring centre, in the ring plane, toward the corner's inside.
  - Nearest keg prism to the ring centre: 124 u, at least 28 u outside the widest mouth.
  - A full blast at the ring centre (100 u) cannot open it.
- The course profile is today's `GrizzlyTimeCourse.ForIntensity(i)` (AngularSpread {1.2, 4, 2, 4}, :201). Only RingRadius changes.

| Level | New demand | Core kind | Credit radius | Regrow | Kegs per gate | Chalk |
|---|---|---|---|---|---|---|
| I1 KEGS | Hit a target with your bomb: one bomb is both key and engine | Plain (one blast or one ram opens it) | 160 u | 3.0 s | 1 (inside) | none |
| I2 BANK SHOT | Blow the keg from its EXIT side so the push points at the next gate. On the 110 deg corner a straight blow throws you about 300 u wrong | Plain | 140 u | 3.0 s | 1 | 2 rails x 6 Plain dots at +/-70 u, from 120 to 260 u along ExitDir |
| I3 TWO KEYS | Spend the carry bomb as a stripping key: strip from range with LT (no backward push when blown from more than 100 u out), then blow from the exit with RT | Shielded (the first hit sheds, the second breaks; a ram also strips) | 120 u | 2.5 s; a stripped core re-shields 2.5 s after its last hit when no hull is within 110 u | 1 | yes |
| I4 PICK YOUR KEG | Choose the inside or the outside keg at each corner | Shielded | 100 u | 2.0 s | 2 twins at +/-140 u (280 u apart, more than two full reaches, so one blast cannot open both) | yes, per keg |

**Ammo, stated honestly.**
- Key plus carry is 0.70 a gate. At I3, strip plus blow is also 0.70: the stripping bomb replaces the carry bomb, which keeps I3 ammo-neutral.
- The pool regenerates 0.15/s, so a full-bomb pilot is ammo-bound at about 4.7 s a gate, about 75 s for 16 gates. That sits under the ~90 s target and above the old "50-56 s" claim, which ignored ammo.
- A gamepad can tap its carry bomb (0.08 ammo, about 187 u). That is a vessel-side device gap. It already exists today, and the kegs make it no worse because the KEY must be full (100 u reach against 25 u). The harness measures the gap and reports it to the vessel owner; the design does not claim it away.

**Dropped:** the take-off rings (the hull clips them on the axis), the I4 StartingElements Time 0.5 (it rescales per mode, which is borderline against the locked rule), Time crystals (Time only cheapens Rush, and the AI never Rushes), the 260 u credit radius (chasers would coast), and Super-shielded chalk (it truncates the blast and sits in the exit lane).

## 3. Course features

### 3a. Powder kegs (I1-I4; twins at I4)

**Reused, all paths verified on origin:**
- the prefab `Assets/_Prefabs/Trails/SpawnablePrism.prefab`, fileID 4563009547826722997 (guid via `lib.existing_guid`);
- `PrismTrailBuilder.LayOne` + `PrismLay(SpawnPoint, Domains.Blue, kind)` (Environment/Spawning/PrismTrailBuilder.cs:57);
- the Part shape and `PassesNear` from `Assets/_Scripts/Controller/Arcade/ThresherTime/ThresherTimeNest.cs:27/95`;
- the burst, regrow and Holds tick from ThresherTimeController.cs:158-250;
- `EnvironmentPrismPool.TryRelease` (Utility/PoolsAndBuffers/EnvironmentPrismPool.cs:41).

**Placement.** This is a pure function of the broadcast `RaceGate` list plus Intensity, with zero System.Random. For ring k with prev p, gate g and next n (positions):
- `bis = normalize(normalize(p-g) + normalize(n-g))`;
- `n_in = normalize(bis - Dot(bis, g.Axis) * g.Axis)`.
- If the deflection is under 10 deg, `n_in = normalize(Cross(g.Axis, Vector3.up))`, with its sign taken from `Hash01(k)` (`Hash01(int)` copied from the Jackstraw lesson).
- Keg centre: `g.Position + 140 * n_in`. At I4, a second keg at `g.Position - 140 * n_in` (the outside keg).
- `ExitDir(keg) = normalize(n.Position - keg.Centre)`. The hoop is laid in the plane perpendicular to ExitDir, so it reads as a barrel pointing down the lane.
- Kegs are keyed by ringIndex 0..7. Rings are laid once per lap, and the race maps threads to rings with `RingIndexFor` (GateRaceController.cs:244).

**Every peer builds them the same way:**
- In `OnCourseRaised` (GateRaceController.cs:870, which runs on every peer after `SyncCourse_ClientRpc`), call `LayOne(kegPrefab, ..., _kegRoot, keg.Trail, KegOwnerId = "GrizzlyTimeKeg", watchForReveal: true)`.
- Nothing new is replicated. Each peer simulates every bomb from its replicated press and breaks its own kegs. The owner's `ThreadsGate` decides credit and the server records it, as Thresher Time does.

**Tick, in `LateUpdate` only** (a subclass must not declare `Update`):
- A core that is not `Holds()` (`alive && ownerID == KegOwnerId`) bursts the keg: the remaining parts are devastated outward at `kegBurstPerFrame` 24 per frame, and 48 on the final lap for heat.
- Regrow happens after RegrowSeconds once no hull is within 110 u (reach + 10). It re-lays with `watchForReveal:false` and `CompletesAsLiveRibbon = true`.
- At I3 and above: a standing core with `!IsShielded` past its re-shield time calls `prism.ActivateShield()` (Prism.cs:2142). The accessor path from Prism to `PrismProperties.IsShielded` is unverified; name it in the editor.

**Credit:** `ThreadsGate(ring, prev, cur) => base.ThreadsGate(ring, prev, cur) || AnyOpenKeg(ring, k => ThresherTimeNest.PassesNear(k.Centre, prev, cur, CreditRadius[i]))`.

**Breakage (research reading, unverified in play):**
- Blue is never a player domain (Domains.cs:6-8), so every Grizzly blast damages kegs (PrismSpatialIndex.cs ~3405-3470).
- `AOEGrizzlyExplosion` has devastating 0, so a Shielded core only sheds its shield (Prism.cs:1886-1887). A second, separate blast is needed because each AOE hits a prism only once.

**Budget:** 15 prisms per keg -> 120 per lap at I1-I3, 240 at I4.

### 3b. Exit chalk (I2+)

- **Layout:** `GrizzlyTimeKegs.Chalk(keg)` returns 2 rails x 6 dots of Plain Blue SpawnablePrism, scale 5 u, at +/-70 u lateral (in the plane of keg, ExitDir and n_in), running from 120 to 260 u along ExitDir.
- **Laying and regrow:** laid in the same `OnCourseRaised` (the DecoyRunController.OnCourseRaised:74 precedent). They are Plain, so blasts clear them, and they sit on the keg regrow loop: each dot is re-laid 3 s after it is lost, once no hull is within 60 u.
- **Reach:** they start at 120 u, beyond the 100 u full reach of a blast at the core, so a keg blast never touches them.
- **Lane:** the lane is 140 u wide around the exit line.
- **Budget:** 12 dots per keg -> 96 per lap at I2-I3, 192 at I4. The I4 total of 432 sets `MaxPrismsPerLap = 450`.

### 3c. Crown bursts (I1-I4, feedback, no standing mass)

- In `OnLocalNextGateChanged(previousRing, nextRing)` (:374), lay 16 Plain Blue prisms at mouth + 14 u in the previous ring's plane with `watchForReveal:false`, and devastate them outward on the next LateUpdate.
- Debris only: the prisms never stand, so nothing new can be hit. Local only.
- If the spawn-then-burst look is wrong, cut it. It is a stretch item, and the look is unverified.

### 3d. Finish demolition wave (stretch)

- On `OnLocalNextGateChanged(prev, -1)` (:949-959 passes -1 at finish), schedule every standing mode prism (kegs and chalk) to burst at `t0 + dist(prism, finishRing)/600`, capped at 60 per frame.
- Local only. Non-finishers do not get it in v1: the match-end hook is unverified.

### 3e. Race-first track changes

- `GrizzlyTimeCourse.cs:207` RingRadius {96, 72, 58, 46} -> {96, 84, 74, 66}. This is a one-line diff and it collides with grizzly-v3.
- Start line: the harness prints the per-pad gate-0 presentation spread. Only if it exceeds 30 deg, override `TryStartLine` (RaceCourseSource.cs:142) in `GrizzlyTimeCourseSource`.
- No lead-in gate: the target of 16 and its registrations stay unchanged.

## 4. Feedback

**In the world, in 3D, on every peer:**
- the keg burst (Thresher burst plus prism debris);
- the keg regrowing as a live ribbon in front of a trailing pilot;
- the chalk lane;
- the lime next ring framing the gate (shipped);
- the bomb's domain-lit wake (GrizzlyBombVisual.cs:28-33);
- crown debris on each thread;
- doubled burst speed on the final lap;
- the finale wave (stretch).

No HUD rings and no 2D markers.

**Toasts.** New `Assets/_SO_Assets/Game Toasts/GameToastConfig_GrizzlyTime.asset` (gameMode 63), written with `lib.toast` (arcade_mode_lib.py:310) and registered with `g.register_toast_config` (:474), copying hull_time_race.py:247-256.
- Shared beats, which need no new id (already ticked at GateRaceController.cs:1001-1009 but authored nowhere for this mode): 129 Half, 130 LeadChanged, 131 HomeStretch, 132 FinalLap, 30 ComebackActivated.
- New ids:
  - `GrizzlyTimeHint = 166`: posted once, locally, at turn start (SerpentTimeController.cs:794-799 pattern). Template: "Freeze a bomb in the keg, get on its far side, blow it - the blast throws you away from the bomb."
  - `GrizzlyTimeBankShot = 167`: template "Bank shot ({count})", filled by `TemplateText.Fill`, posted on the 1st bank shot and then every 4th.
    - Detection is local: a keg breaks while the local hull is within 100 u of its core, and within the next 0.15 s the local `VesselTransformer.VelocityShift` (public getter, :357) is within 30 deg of that keg's ExitDir.
- **Id rule:** claim 166-167 only. 164 (Glasshouse, in flight) and 165 (Shatterplane, on origin) are taken. Re-scan origin/bleeding-edge and every origin ref at commit, never the local checkout.
- No per-gate toast.

**FMOD.** New `[SerializeField] EventReference` fields on GrizzlyTimeController, all shipped EMPTY and said so in the PR. They play through `AudioSystem.Instance.PlaySFXEvent(ref, pos)`.
- `kegBurstEvent`: 3D at the keg, every peer.
- `kegStrippedEvent`: I3+, 3D.
- `bankShotEvent`: local.
- `crownBurstEvent`: local, at the ring.
- `finaleWaveEvent`: local, at the finish ring.
- The base `gateThreadedEvent` / `courseFinishedEvent` (GateRaceController.cs:86/90) get explicit, still-empty field lines in the scene through the generator.
- Ask for a `/sound-brief` for all seven slots. The bomb's own fire and detonate slots are the vessel's and are not touched.

## 5. AI (input only; no vessel file touched in v1)

Plan step: `time-arcade` (judge-verified in ai_plan.json), named in the commit. No Froglet Compute: it is paused.

1. **Regatta steering.** `protected override bool AICommitFromApproachPoint => true;` in GrizzlyTimeController.
   - Grizzly Time is the only lapped circuit without it. The scene's lead of 420 sits outside its commit radius of 380, the documented orbit shape.
   - Redline went from 10/24 to 24/24 offline finishes with this one change.
2. **`TryOverrideAim` (:442), server only, with the AI never needing two hits:**
   - At **I1-I2** (Plain cores), while ring k's keg stands and the AI is within 600 u of it, aim at `keg.Centre + ExitDir*40`. The AI then passes beside the core, not through it, which avoids the hull-deviation spin from ramming the cage.
     - The shipped AutopilotDrive fires a full bomb about every 2.3 s and detonates it 10 u behind the hull, so a blast lands within 100 u of the core on most passes.
     - Fallback: if the keg is still standing when the AI is inside `CommitDistanceFor` of it, return false, which hands the AI back to the ring mouth (180-260 u away, reachable).
   - At **I3-I4** (Shielded), aim at a keg ONLY if the server's copy is already open or stripped (a rival or the AI itself cracked it). Otherwise there is no override, and the AI threads the mouth, which shrinks only to 66 u.
   - At I4, choose the keg with the pure `GrizzlyTimeKegs.ChooseKeg(prev, gate, next, intensity)`, the same function the harness measures.
3. **Stall rescue** (from Brave Line).
   - Each server tick (`OnServerTick`, :1025), compare every AI's `RoundStats.SwitchesThreaded` with the last value and stamp the time when it rises.
   - If an AI has gone 18 s without threading (about 3x the measured 6.2-7.8 s a gate), aim at `ring.Position - ring.Axis*300` until it is within 60 u, then hand back.
4. **AINoseLeadTable row (:1289):** ship one only if the harness AI-replica sweep shows it improves finishes or median time. Otherwise leave it null.
5. **Difficulty:** Easy and Medium come from the existing `GateRaceHandicap` (Controller/AI/GateRaceHandicap.cs:29). No new difficulty code.

**Inertness:** every piece above is a GateRaceController virtual on this mode's controller, so it is inert everywhere else by construction. **No vessel code changes.**

**Owner proposals, NOT built.** These are vessel changes. Ask Garrett, who merged the bombs (#1018) and the hull (#1593), plus jtgjones for grizzly-v3. TEAM.json names no Grizzly owner.
- (P1) The inert `aiFireStickBand`: AutopilotDrive's gate reads `input.XSum/YSum` (executor :578), which AIPilot never writes for single-stick hulls (AIPilot.cs:1272-1275). The AI therefore fires mid-corner.
- (P2) `IGrizzlyRaceCoach { GrizzlyBombCall Call(GrizzlyTriggerBombExecutor bomb, int slot); }`, returning Default / Hold / FreezeNow / DetonateNow, consulted in the Idle / InFlight / Frozen cases (:590-618).
  - Null means today's AI. The controller would set it in OnRacerArmed (:1260) and clear it with ReferenceEquals, following the IOrcaRaceCoach / IJacksRaceCoach shape.
  - The decision would be the pure `GrizzlyTimeKegs.BombCall`.
  - This is what would let the AI bank-shot and open I3-I4 kegs.
- (P3) An element that scales the trigger bomb, so the 0.45 comeback buys pace.
- (P4) `GrizzlyHUDController.cs:147-151` hardcodes the weapon labels, which breaks the authored-text rule.
- (P5) The stale comments in GrizzlyCycleWeaponActionSO.cs:7-8 and GrizzlyModeSwitchingFireSO.cs:9, and "B / R" in GRIZZLY_TRIGGER_BOMBS.md (the key is T).
- (P6) The dead WhileBoosting immunity on the prefab.

## 6. Files, in order

0. **Before starting:**
   - `git fetch origin`.
   - `py -3 Tools/Build/who_works_where.py query Assets/_Scripts/Controller/Arcade/GrizzlyTime Tools/Build/author_grizzly_time_assets.py "Assets/_Scenes/Multiplayer Scenes/MinigameGrizzlyTime.unity"`.
   - Branch from **origin/bleeding-edge**, never the local checkout (22+ commits behind).
   - Read `git diff origin/bleeding-edge...origin/grizzly-v3 -- Assets/_Scripts/Controller/Arcade/GrizzlyTime Assets/_Scripts/Tests/Editor/GrizzlyTimeCourseTests.cs`.
   - Re-scan the toast ids.
1. **`Assets/_Scripts/Controller/Arcade/GrizzlyTime/GrizzlyTimeCourse.cs`:** the :207 RingRadius change only. Keep every new constant out of this file to limit the grizzly-v3 conflict to one line.
2. **NEW `GrizzlyTime/GrizzlyTimeKegs.cs`:** pure static, no `UnityEngine.Object`, zero random draws apart from `Hash01`.
   - Structs `Keg {Ring, Centre, ExitDir, Inside}` and `Part {Position, Rotation, Scale, Kind, Core}`.
   - Ladder arrays: `CreditRadius {160,140,120,100}`, `RegrowSeconds {3,3,2.5,2}`, `ReshieldSeconds 2.5`, `CoreKind {Plain,Plain,Shielded,Shielded}`, `Twins {false,false,false,true}`, `HasChalk {false,true,true,true}`.
   - Geometry constants: `KegOffset 140`, `HoopRadius 16`, `HoopSlabs 12`, `CoreBlocks 3`, `ChalkFrom 120`, `ChalkTo 260`, `ChalkHalfGap 70`, `ChalkDots 6`, `HullClear 110`, `MaxPrismsPerLap 450`.
   - Reach constants that read GrizzlyTimeCourse so they re-derive under v3: `FullReach => GrizzlyTimeCourse.MaxBlastScale*0.5f` (100) and `TapReach` (25).
   - AI constants: `AIStallSeconds 18`, `AIKegAimRange 600`, `AIKegPassOffset 40`.
   - Functions:
     - `KegsFor(IReadOnlyList<RaceGate> lap, int intensity, List<Keg>)`, `Build(in Keg, int intensity, List<Part>)`, `Chalk(in Keg, List<Part>)`;
     - `InsideNormal(prev, at, next, ring)`, `ExitDirection(keg, next)`;
     - `ChooseKeg(...)`, `IsBankShot(kegPos, hullPos, shiftDir, exitDir, maxDeg = 30)`;
     - `BombCall(...)` for the proposed coach (pure; unused in v1);
     - `PrismsPerLap(intensity)`.
3. **`GrizzlyTimeController.cs`:**
   - Make it `partial`.
   - Add the `AICommitFromApproachPoint` override.
   - Rewrite the stale summary at :10-17: launch away from the bomb, 300 ceiling, about 350 u/s, a 211 u circle while launched; Rush is about 75-80 u.
4. **NEW `GrizzlyTime/GrizzlyTimeController.Kegs.cs`,** shaped like HeadlongController.Stampede.cs and ThresherTimeController:
   - `[Header]/[Tooltip]` fields:
     - `kegPrefab`, `kegBurstPerFrame 24`, `kegBurstSpeed`, `crownPrisms 16`;
     - the five empty EventReferences;
     - `GameToastSituation hintToast = GrizzlyTimeHint`, `bankShotToast = GrizzlyTimeBankShot`, `bankShotEveryN 4`.
   - Overrides: `OnCourseRaised`, `ThreadsGate`, `TryOverrideAim`, `OnLocalNextGateChanged`, `OnServerTick` (stall stamps), `OnRacerArmed` (per-pilot state), and `OnNetworkDespawn` (release everything; Thresher ClearNests shape :160-174).
   - `LateUpdate`: TickKegs, TickChalk, the re-shield step, the crown burst and the finale.
   - Logging: verbose traces on a `CSLogChannel` (add a `GrizzlyTime` member with `[CSLogChannelLabel]`, or reuse `ArcadeMatch`). A warning only for a missing prefab. Nothing per frame.
5. **`Assets/_Scripts/Data/Enums/GameToastSituation.cs`:** `GrizzlyTimeHint = 166` and `GrizzlyTimeBankShot = 167`, with explicit values and a comment each.
6. **NEW `Assets/_Scripts/Tests/Editor/GrizzlyTimeKegsTests.cs`** (section 7), and extend `GrizzlyTimeCourseTests.cs`:
   - the mouth expectations;
   - read back `DefaultMinimumSpeed 0`, ammo regen 0.15, costs 0.08/0.35 and `projectileSpeed 90` from Grizzly.prefab and GrizzlyTriggerBombConfig.asset.
7. **NEW `Tools/Build/grizzly_time_course_harness/`** (run.sh, NUnitShim.cs, Runner.cs), copied from `headlong_course_harness/run.sh`.
   - It compiles `race_course_source_harness/UnityMath.cs`, HeadlongCircuit, RaceCourseGeometry, RaceGate, GrizzlyTimeCourse.cs, GrizzlyTimeKegs.cs, ThresherTimeNest.cs (for PassesNear; check it compiles standalone), and both test files, using .NET SDK 8.0.403 Roslyn. headlong's harness passed 9/9 in 16.7 s on this box.
   - Modes: `--sweep`, `--export`, `--model` (section 7), `--self-test`.
8. **`Tools/Build/author_grizzly_time_assets.py`:**
   - SCRIPT_PATHS (:52-56) += GrizzlyTimeKegs.cs and GrizzlyTimeController.Kegs.cs.
   - NEW_FIELDS (:185-200) += the `kegPrefab` line (fileID 4563009547826722997, guid from `lib.existing_guid('Assets/_Prefabs/Trails/SpawnablePrism.prefab')`), the burst and toast fields, and seven empty EventReference blocks.
   - A one-shot migration of the committed scene's controller block that STANDS DOWN once applied (hull_time_race.py:209-212 pattern), never an assert above validation, with a negative control.
   - Emit and register `GameToastConfig_GrizzlyTime.asset` (129-132, 30, 166, 167).
   - Card Description: new words, avoiding ': ' and ' #' (`plain_scalar_errors`). Preview ObjectiveText becomes "Blow the kegs, ride the blasts".
   - Validation: the mirrored C# constants (mouths and ladder), the keg prefab guid in the scene, and the toast ids.
   - Fix the docstring at :22 (3 laps -> 2).
   - Run `--check`, confirm it names MinigameGrizzlyTime.unity, then write.
9. **Card art:**
   - `render_card_backgrounds.py` SOURCES (:120) += GrizzlyTimeKegs.cs (and ThresherTimeNest.cs if it is referenced). Without it the harness fails with CS0246.
   - `card_art_harness/Program.cs`: a "grizzlytime" staging block drawing I2 kegs and chalk, beside perchrun :167 and breach :194.
   - Re-render through `/cardart`; then `render_card_backgrounds.py --check` and `bake_prism_layouts --check`.
10. **Docs:**
    - `Assets/_Scripts/Controller/Arcade/GRIZZLYTIME.md`: the section 8 fixes, plus a "Powder Kegs" section with the measured sweep.
    - `Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md`: a new round row, marked as an estimate. Proposed: Verb 9, Flow 7, Ladder 8, Feedback 5, Rivals 4, Fairness 5, Fun about 6.3.
    - GAME_TOASTS.md (166, 167).
    - Docs/claude/GAME_MODES_AND_CONTROLLERS.md: the mode paragraph.
    - A `Docs/UNITY_VERIFICATION_CHECKLIST.md` entry.
    - The design brief.
11. **Gates** (`py -3`, TMPDIR = scratchpad):
    - the harness, plus `--self-test`;
    - `check_switch_label_collisions`, `check_enum_member_references`, `check_using_directives`, `check_console_logging`, `check_conditional_compilation`;
    - `author_preview_spawns --check`, `author_grizzly_time_assets.py --check`, `render_card_backgrounds --check`, `bake_prism_layouts --check`.
    - The verify rule: `unity_refcompile` cannot run on Windows. The editor was open (PID 13564, lockfile), so use the `unity` CLI against it; batchmode `-runTests -testFilter GrizzlyTime` is possible only once the editor is closed. If neither runs, say so in the commit/PR and add the checklist entry.
    - Commit as `feat(grizzly-time): ...` with `Plan step: time-arcade` and the Affects lines, then push. Do not file a ticket without approval.

**Cut line if the session runs short:** steps 1-8 with I1-I2 kegs, the toasts and the AI changes are the minimum shippable unit. I3 re-shield, I4 twins, crowns and the finale come after.

## 7. Tests and offline measurement

**EditMode tests in `GrizzlyTimeKegsTests.cs`.** Every test runs 400 seeds x I1-I4 through `HeadlongCourseSource.BuildCircuit(GrizzlyTimeCourse.ForIntensity(i), ...)`:
- `Kegs_are_a_pure_function_of_the_course`: build twice, compare bitwise.
- `No_keg_or_chalk_prism_is_within_the_mouth_plus_25u_of_any_ring_centre`: protects the safe line.
- `A_full_blast_at_the_ring_centre_cannot_open_a_keg`: `KegOffset - HoopRadius > FullReach`.
- `Twin_kegs_are_more_than_two_full_reaches_apart` (I4).
- `Every_chalk_rail_starts_beyond_one_full_blast_of_its_keg`, and `Every_chalk_rail_clears_the_exit_line_by_at_least_60u`.
- `Every_keg_is_reachable_from_its_exit_side_inside_its_credit_radius`.
- `Every_corner_over_60deg_punishes_the_straight_blow_and_leaves_a_bank_shot`: the straight blow ends at least 150 u farther from the next gate than the bank shot, using `GrizzlyTimeCourse.CarryAfterLaunch`.
- `Neither_twin_habit_wins_more_than_75pct_of_I4_gates` (via `ChooseKeg` rollouts).
- `Prism_budget_holds`: at most `MaxPrismsPerLap` (450).
- `Ladder_adds_one_demand_per_level`, `Mouth_ladder_has_no_step_over_15pct`, `Credit_radius_shrinks_monotonically`.
- `Toast_ids_are_166_and_167`: read back from the enum.

**`--self-test` negative controls.** Each sed mutation must FAIL:
- `KegOffset 140 -> 90`;
- `ChalkFrom 120 -> 90`;
- the I4 twin offset collapsed to 60;
- `CoreKind` Shielded -> Plain at I3 (caught by the ladder test);
- I4 RingRadius 66 -> 46 (caught by the mouth-step test).

**Race model (`Runner --model`).**
- A kinematic Grizzly built from the constants the tests read back:
  - cruise 50, 95 deg/s;
  - `LaunchImpulse`/`ImpulseWeight` with falloff 1 -> 0.5;
  - live launches summed as vectors and clamped to 300;
  - bombs at 90 + Course speed (a bomb fired mid-launch falls behind);
  - ammo 0.15/s, 0.08 / 0.35;
  - keg rules as built, credit on PassesNear;
  - 0.3 s reaction lag.
- Pilots:
  - (A) ring-threader;
  - (B) rammer;
  - (C) straight-blower;
  - (D) always-inside and (E) always-outside, at I4;
  - (R) reader: bank shot, carve, ChooseKeg by rollout, in a full-only variant and a tap-capable variant;
  - (AI) AI-replica: a line-for-line port of AutopilotDrive (:572-627) INCLUDING the inert stick band, plus the section 5 aim rules and the stall rescue.
- Bars:
  - **AI-replica finishes 400/400 at every intensity** within 240 s, and its median is at most 2x the reader's. Any I3/I4 stall means: shorten the re-shield, or Plain cores at I3. Never a vessel change.
  - STRICT gain = (best fixed habit - R) / best fixed habit. I1 0-3%; I2-I4 5-15%. If I4 is under 3%, the twin choice is fake: drop the twins and make I4's demand the 100 u credit plus 2 s regrow only, and say so.
  - Reader median I4 time 70-95 s (the ammo-bound expectation is about 75 s). Laps stay at 2.
  - Full-only R against tap-capable R: report the gap; do not gate on it. It goes to the vessel owner.
  - The bank shot's value: (C) - (R) at corners over 60 deg.
  - Gate-0 presentation spread across pads: over 30 deg means apply the TryStartLine override.

**After merge:** compare recorded flights (HumanFlightRecorder / BugHuntAutoSend; the sweep runs on its own) on the orbit metric, the share of race time within 400 u of the next ring without credit. Today it is 68-69% for the AI and 75-125 s single-ring stalls for humans. Target: under 15%, and no gate over 25 s.

## 8. Stale facts to correct

- **GRIZZLYTIME.md:4, :123-124, :132:** 3 laps / 24 gates / comeback 0.3. Shipped: 2 / 16 / 0.45 (scene :825, EndConditionOverrides.asset:64 and :130, generator :95 and :117, ArcadeGameGrizzlyTime.asset:37; changed by 01b435541a).
- **GRIZZLYTIME.md:188** "No toasts of its own ... as Redline": wrong. Redline authors 129-132 + 30, and no stat toast watches SwitchesThreaded. It becomes the new config.
- **GRIZZLYTIME.md §4:**
  - "AI throttle 0.7 -> 1.0 so it cruises at 50" is wrong: SingleStickVesselTransformer ignores throttle (:71-72).
  - "fires while the stick is straight" is inert for the AI (executor :578 vs AIPilot.cs:1272-1275).
- **GRIZZLYTIME.md §2:** "Rush is a burst onto a straight" overstates it: about 75-80 u, Energy starts at 0, the first Rush comes at about 5 s.
- **GRIZZLYTIME.md:35-38** "pace is ammo-bound": in recorded flights pace is control-bound (meter 0.66-0.81). With kegs, ammo binds at reader pace (about 4.7 s a gate). State both.
- **GRIZZLYTIME.md:181** names RingRadius as the dial: update it to 96/84/74/66.
- **The doc's race-length model:** about 15 k u / 85 s was for 3 laps. At 2 laps it is about 9.4-10.4 k u.
- **GrizzlyTimeController.cs:10-17** (verified on origin): "launched along its nose ... 100 u/s ceiling ... 150 u/s on a 90 u circle". Actual: away from the bomb, 300 ceiling, about 350 u/s, 211 u circle.
- **author_grizzly_time_assets.py:22** docstring says 3 laps.
- **GrizzlyTimeCourseTests.cs:79-98:** does not read back DefaultMinimumSpeed, ammo or projectileSpeed (added in step 6).
- **Research and design docs** call the Grizzly blast a "Strike". It is MissileBlast, hitClass 3: 20 points, 8 s silence.
- **VESSEL_MODE_CHART.md:122** AI row: mostly human rivals. `vessel_mode_chart.py:166-173` must count only rivals with ai bit 64. This is a tooling change, so ask Shombith.
- **ARCADE_MATCH_LENGTH.md:31** "unfinishable": refresh after the sweep.
- **GrizzlyTimeCourse constants:** all other constants match the shipped assets (`--check` OK, 19 files). Only RingRadius changes, by design.

## 9. Only the Unity editor can verify

- That GrizzlyTimeController + Kegs.cs compile. The Roslyn harness checks only the pure files.
- That a Grizzly blast destroys Blue Plain cores and only sheds Blue Shielded ones.
- That one AOE hits a prism once.
- That a ram strips or opens.
- That a silenced pilot's blast deflects off kegs.
- The `IsShielded` accessor path and how `ActivateShield()` looks on re-shield.
- Keg, hoop, chalk and crown look and readability; whether the lime ring frames a keg 140 u off-centre; the bank shot's feel.
- Tadpole and Brittlestar fauna eating keg or chalk prisms (regrow masks it).
- Burst and debris cost on phones.
- Per-peer divergence: a remote tap sampled smaller can open a different keg. Credit is owner-side.
- Toasts showing.
- The real AI racing and finishing in the scene.
- The stall rescue firing.
- Silence griefing at the start (AIs blast every ~2.3 s and bombs freeze on rival hulls).
- Final look, feel and audio. Use a `/unity-look` still per VIDEOS_AND_PREVIEWS.md.

## 10. Owners for Affects lines

- **Yash:** Grizzly Time's main driver (who_works_where: Yash 19, Garrett 6, jtgjones 2, Shombith 1). This is the mode area.
- **Garrett:** merged the trigger bombs (#1018) and the hull (#1593). He is read-only here, and is the person to ask for P1-P6. He ordered the Time-game fun rounds. The Headlong and Thresher recipes are copied, not changed.
- **jtgjones:** origin/grizzly-v3 edits GrizzlyTimeCourse.cs and its tests (a certain one-line conflict at :207). If v3's nose-following launch merges, the bank shot, the carve and the I2/I4 demands lose their point, and kegs degrade to key plus engine. Garrett and jtgjones decide that; the mode cannot. Flag it in the PR, and get their call before relying on I2/I4.
- **Shombith:** tooling (the vessel_mode_chart.py fix, any harness conventions) and the locked "a mode never changes the vessel" rule. This build stays inside it: track, environment, AI virtuals, authored toasts and empty slots only.
- **Suggested commit line:** "Affects: Garrett's Grizzly kit (read-only, proposals P1-P6 attached); jtgjones's grizzly-v3 (GrizzlyTimeCourse.cs:207 RingRadius); Thresher Time's PassesNear and Headlong's lay/regrow recipe reused unchanged; Plan step: time-arcade".
