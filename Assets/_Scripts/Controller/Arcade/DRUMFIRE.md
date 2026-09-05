# Drumfire — the Dolphin's rhythm range

`GameModes.Drumfire = 45` · scene `MinigameDrumfire` · `DrumfireController`
· **Dolphin only** · 2–4 pilots · 2–3 domains · 4 intensities

> **The one-line pitch.** A great porous drum of prisms hangs in the middle of the cell. Your own
> line of crystals runs **past** it, not at it — so the target is always off to one side, and every
> shot needs a deliberate turn off your flight vector. Drift to hold the line, swing the nose onto
> the drum, take the next crystal to let the jaws go. **Fly, aim, shoot, repeat.** Most volume torn
> out when the clock stops wins.

Drumfire is the platform's first mode built to **teach a hull** rather than to test one. Everything
below follows from that: it has no race target (a pilot who is losing is still practising), no
opposing fire (nothing to react to except your own line), and one target that everybody shares.

---

## 1 · Why the Dolphin needs this

The Dolphin has essentially one offensive act, and it is a three-step loop the vessel never
explains:

1. **Bank energy by SKIMMING** — the conic blast's gape is the energy you banked
   (`MaxScale = lerp(400, 2080, energy)`), and the only way to bank it is to fly close to mass.
2. **Fire by touching a CRYSTAL** — there is no trigger. The crystal *is* the trigger.
3. **Aim by pointing the nose** — the blast leaves the jaws, and a drifting Dolphin's nose and
   course are different directions.

Rampage pays you for aiming that cone at a forest; The Bends pays you for catching a rival in it.
Both assume you already know the loop. Drumfire is where you learn it, and the geometry is what
teaches it — there is no tutorial text and no scripted beat.

---

## 2 · The lane — the whole lesson, expressed as geometry

Each pilot is given **their own straight line of crystals**, struck through **their own spawn
slot**. `CrystalManager.CrystalPlacementMode.ApproachLanes` is the platform capability this mode
added; `ApproachLaneGeometry` is the pure math behind it (unit-tested in
`ApproachLaneGeometryTests`).

| Authored on the scene's `NetworkCrystalManager` | Value |
|---|---|
| `laneRingRadius` | **1120** |
| `laneOffsetFromCenter` | **420** |
| `laneLeadDistance` | **640** |
| `laneLength` | **800** |
| `laneFormation` | `Symmetric` |

**The standoff is the mechanism.** A lane leaves a point on the spawn sphere and is tilted
`theta` off the straight-in direction, so its closest approach to the centre is
`ringRadius · sin(theta)`. Solving for the authored standoff gives

```
sin(theta) = laneOffsetFromCenter / laneRingRadius = 420 / 1120 = 0.375   ->   theta = 22.0 deg
```

so a pilot flying their own crystals passes the drum at **420u** from its centre — **100u clear of
its 320u skin** — instead of flying into it. Because the lane never points at the target, the pilot
cannot aim by steering: they have to hold the line and turn the nose. That is the drift.

### 2.1 · The crystal band is centred on the pass, and getting that wrong killed the first design

The lane's closest approach sits **1038u** along its 1120u+ run. The band therefore runs
`640 … 1440`, straddling it: the pilot shoots on the way in, at the pass, and on the way out.

The first cut ran the band `300 … 1800` — starting at the pass and running outward. It did not
work, and the reason generalises:

> **Blast yield falls as the square of range.** Measured single-shot yields across that band ran
> from **6,678** panes (slot 1, full energy, at the pass) down to **926** (slot 3, empty). The
> first volley of four shots destroyed **31.7 – 52.5 %** of the drum and the second pass added
> **0.0 %** — the arena was gone before the rhythm had a second bar.

The geometric floor is
`f = omega·(d² + R²) / (2·pi·R²)`, minimum **3.68 %** of the drum per full-energy shot, and it is
**scale-invariant** — an R-sweep over 320 / 500 / 600 / 700 / 850 confirms a bigger drum does not
help, because the cone grows with the range it is fired from. The fix was not a bigger target; it
was **centring the band on the pass and shortening it**, plus inverting the intensity ladder so a
harder intensity means *more, tighter* beats rather than more reach.

`Tools/Build/drumfire_arena.py` now asserts **end crystals are worth at most 2.0× middle
crystals**, which is the assertion that caught the wrong lead distance before it shipped.

### 2.2 · The beat

| Intensity | Crystals per lane | Spacing | Beat @ 40 u/s | Beat @ 68 u/s (max cruise) |
|---|---|---|---|---|
| 1 | 5 | 200u | 5.0 s | 2.9 s |
| 2 | 6 | 160u | 4.0 s | 2.4 s |
| 3 | 7 | 133u | 3.3 s | 2.0 s |
| 4 | 8 | 114u | 2.9 s | 1.7 s |

A full pass is **36 s at 40 u/s**, so the 75 s clock is **~2.1 passes**. That is the brief answered
literally: *only give them enough time to hit all their crystals while moving on the slower side.*
Fly faster and you get a third pass, at a beat too tight to aim in. The arena model asserts the
pass count stays inside **1.2 – 3.5** and the beat stays at or above **2.5 s at 40 u/s**.

There is **722u of run-out** past the last crystal before the membrane, so the final shot is never
taken into a boundary.

### 2.3 · Lane ownership is emergent

Nothing assigns a pilot to a lane. `CellSpawnFormation` places one player per direction and lane
*k* is struck through spawn slot *k*, so the pilot who spawns on slot *k* is standing at the mouth
of lane *k*. Two things make that safe and both are asserted by
`Tools/Build/author_drumfire_assets.py`:

- `spawnRingRadiusFloor == laneRingRadius` (1120)
- `spawnFormation == laneFormation` (`Symmetric`)

Change one without the other and pilots spawn off the mouths of their own lanes.

**The index mapping is lane-MAJOR** (`lane = index / slotsPerLane`), not slot-major.
`NetworkCrystalManager` grows its slot list as players arrive and fills only the entries that are
still empty; a slot-major mapping would change which lane every *existing* crystal belonged to
every time somebody joined, stranding all of them until next collected. Appending whole lanes
cannot disturb the lanes already laid.

A collected crystal **reloads at its own slot** (the lane early-return in
`CrystalManager.CalculateNewSpawnPos`), so a pilot's second pass runs the same line as their first.

---

## 3 · The drum

`SpawnableDrum` (`Assets/_Prefabs/Spawnables/SpawnableDrum.prefab`, seed 45) is the whole of the
cell's environment. It borrows the Orrery's sun-shell vocabulary — a phyllotaxis point set per
sphere, panes laid tangent to the surface, value-noise gaps punched through — scaled from a 46u
ornament to a 320u arena feature and stacked into concentric shells.

| Shell | Radius | Points | Kept | Gap % |
|---|---|---|---|---|
| 0 | 320 | 14,074 | 12,815 | 8.9 |
| 1 | 256 | 9,007 | 8,114 | 9.9 |
| 2 | 192 | 5,067 | 4,460 | 12.0 |
| 3 | 128 | 2,252 | 2,103 | 6.6 |
| 4 | 64 | 563 | 511 | 9.2 |

| Family | Kind | Count | Volume |
|---|---|---|---|
| Shells (skin) | Plain | 28,003 | 1,304,716 |
| Ribs | Shielded | 216 | 36,288 |
| Core cage | SuperShielded | 24 | 5,832 |
| Danger studs | Danger | 107 | 26,215 |
| **Total** | | **28,350** | **1,373,051** |

Point counts fall as `r²` so every shell is covered to the same fraction and the panes stay one
size throughout. **A shot fired across the ball passes through several skins; a shot fired at its
middle punches one small hole.** That difference is the aiming lesson, and it is geometry rather
than a rule.

**Everything is `Domains.Blue`, deliberately.** `StatsManager.IsFriendlyEnvironmentPrism` counts a
prism as friendly only when it wears the attacker's own colour, so a Blue drum is hostile to every
domain and each pilot is shooting at exactly the same target. Painting it in the three playable
colours would have made a third of the ball worthless to whichever team drew that colour, decided
by a spawn slot nobody picked.

**What is not plain skin:**

- **Shielded meridian ribs** — two passes to break and worth more volume, so they are the structure
  worth aiming at. Laid at `outerRadius + ribPaneSize.z * 1.5` because a shield's octahedron reaches
  **1.5 × leafSize** from the prism centre (`Docs/ECOSYSTEM.md §35`); any closer and the armour
  fuses into the skin it braces.
- **A super-shielded core cage** that no blast can touch, so the drum always leaves a landmark and
  the arena can never be scored down to an empty sphere.
- **Danger studs on the outer skin only** — which is what makes flying in close to graze the drum
  for a wider jaw a real risk rather than a free upgrade. A stud is never placed in a noise gap
  (`BuildStuds` re-runs the same `IsGap` test the skin uses).

### 3.1 · Collider budget

**240 always-on mesh colliders** (216 shielded ribs + 24 core panes). The other **28,110** are
plain/danger `BoxCollider`s, which ride `PrismColliderLodManager` and so are bounded by phase LOD
rather than by population — exactly like the freestyle cell environments.
`drumfire_arena.py` fails the build if the always-on count leaves the shipped band (≤ 400).

### 3.2 · The cell

`Drumfire Cell Config` authors **no `NucleusPrefab`**. The nucleus is the platform's crystal
respawn volume (`Docs/ECOSYSTEM.md §27`, a LOCKED rule) and this mode's crystals live on lanes
1120u out, so a nucleus would be a marker pointing at nothing. PeelTheCage and the Boneyard are the
documented precedents. The spawn ring is covered by `spawnRingRadiusFloor` instead, and
`Cell.SpawnVisuals` already guards a null nucleus.

Its `SpawnProfile` carries **no flora and no fauna**: every prism a pilot destroys should be the
drum. The phase ladder is authored above the measured baseline anyway (Restless 1,384,251 volume /
Frenzy 1,430,651) so the cell reads as Calm throughout — a phase change gates production, and this
cell produces nothing.

---

## 4 · Scoring — time ends it, volume scores it

`DrumfireScoringRuleSO` (`ScoringMetric.VolumeDestroyed`, `golfRules: 0`) is the only shipping rule
whose **objective is never reached**: `IsObjectiveReached` always answers `false` and `TargetCount`
is 0, because only `DrumfireTimeTurnMonitor` may end a Drumfire turn. A rule that ever answered
true would race the clock and hand the win to whoever crossed an invented threshold first.

**VOLUME rather than a prism COUNT** because the drum is built of panes of one size but braced with
heavier structure: a shot that takes out a rib is worth more than the same number of skin panes,
which puts the aiming lesson in the score.

`ScoringMetric.VolumeDestroyed` is the platform's **first float-backed metric**. It is rounded
**once**, in `ScoringMetrics.Read`, so every downstream consumer — the per-domain sum, the HUD
column, the goal row, the scoreboard secondary — keeps the single `int` contract the other nine
metrics share. `IRoundStats.HostileVolumeDestroyed` was already being credited by
`StatsManager.RecordPrismDestruction` and already travels on the existing
`Player.ReportEnvironmentPrismDestroyed_ServerRpc` round trip, so the metric needed **no new
networking**.

The winner is the **domain with the largest sum**, so teammates combine; ties break by
`ActiveDomains` order (Jade → Ruby → Gold) so every peer resolves identically. Individual scores
are the raw metric — no golf sentinel — so two teammates are separated on the scoreboard by their
own contribution.

**Match length** lives in `EndConditionOverridesSO.drumfireSeconds` (**75 s**, authored through
*FrogletTools > Game Modes > End Game Conditions*), never a per-scene field. The monitor replicates
it, because each peer runs its own copy of the elapsed-time loop and two peers reading different
durations would disagree about when their local monitors stop; the turn END stays server-gated
either way.

`TimeBasedTurnMonitor.PublishesSecondsRemaining` stays true, so the top bar draws a **clock row**
(m:ss, no target, no bar) rather than reading the payload as an objective count
(`Docs/GAME_MODE_TOPBAR.md`).

---

## 5 · Comeback

`ElementalComebackSystem` gained `ScoreDifferenceSource.VolumeDestroyed` (appended last, index 7 —
never inserted, because the enum is serialized on every `SO_ArcadeGame`). `ComebackRatePerScoreDeficit`
is **2.7e-05**: a quarter-of-target deficit must buy at least one whole element level, and this
mode's "target" is a full drum. That is the fourth outing of the trap Dog Fight, The Bends and
Wildlife Liberation each record — **a comeback rate is a function of the target**, so re-targeting a
mode silently kills it.

---

## 6 · AI

**No hook is installed, deliberately.** The platform pilot already does exactly what this mode asks:
it seeks the nearest collectible cell item (here, the next crystal on its lane) and, once its course
is committed, **drifts** and swings its nose onto the densest cluster of hostile mass it can find
(`Cell.GetExplosionTarget`) — which in this arena is the drum.

Installing `AIPilot.SetExternalTargetProvider` would **override crystal seeking outright** and
disarm every AI Dolphin, which is the rule `RAMPAGE.md` records. A `SetDriftLookTargetProvider`
override is unnecessary because the default already points at the target. The Dolphin's authored
`approachRunSeconds` of 2.5 s (it is the one vessel that aims on the way in) covers the break-off.

---

## 7 · Files

| Role | Path |
|---|---|
| Controller | `_Scripts/Controller/Arcade/DrumfireController.cs` |
| Scoring rule | `_Scripts/Controller/Arcade/Scoring/DrumfireScoringRuleSO.cs` |
| Turn monitor | `_Scripts/Controller/Arcade/TurnMonitors/DrumfireTimeTurnMonitor.cs` |
| Arena | `_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs` |
| Lane geometry (pure) | `_Scripts/Utility/ApproachLaneGeometry.cs` |
| Lane placement mode | `_Scripts/Controller/Environment/FlowField/CrystalManager.cs` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameDrumfire.unity` |
| Arcade card | `_SO_Assets/Games/ArcadeGameDrumfire.asset` |
| Cell + profile | `_SO_Assets/Cell Configs/Drumfire Cell/` |
| Arena model (measurement) | `Tools/Build/drumfire_arena.py` |
| Asset generator | `Tools/Build/author_drumfire_assets.py` (`--check`) |
| Tests | `_Scripts/Tests/Editor/ApproachLaneGeometryTests.cs`, `DrumfireScoringTests.cs` |

**Assets are the build, the generator is the source.** Re-run
`python3 Tools/Build/author_drumfire_assets.py` rather than hand-editing the scene, the cell config,
the arcade card or the scoring rule; `--check` fails if any of them has drifted off what the
generator would write, and it cross-validates its lane constants against `drumfire_arena.py`.

---

## 8 · Tuning, in the order to reach for it

1. **Too easy / too hard to finish a lane** → `EndConditionOverridesSO.drumfireSeconds` (75).
2. **Beat too tight or too loose** → `laneLength` on the scene's `NetworkCrystalManager`, or the
   per-intensity slot table (5/6/7/8). Re-run `drumfire_arena.py`; it asserts the beat and pass count.
3. **Shots feel weak / the drum survives untouched** → `laneOffsetFromCenter` (420). Lowering it
   raises yield quadratically and eats the standoff the lesson depends on; the model asserts the
   lane still clears the 320u skin.
4. **The drum evaporates in one pass** → this is §2.1. Do **not** enlarge the drum (yield is
   scale-invariant); move the band, or shorten it.
5. **Frame cost** → `outerShellPoints` (14,074) and `shellCount` (5). Re-measure the cell's phase
   ladder afterwards with *FrogletTools > Ecology > Measure Cell Environment Baselines*.

Never retune by adding decay, a prism cap, or a respawning drum. Mass is conserved: the drum is
removed only by the active force of a pilot's blast, which is the entire score.

---

## 9 · Verification status

Everything below was measured or executed offline. **Nothing has been opened in Unity** — no Editor
was available in the session that authored this.

| Check | Result |
|---|---|
| `Tools/Build/drumfire_arena.py` | all arena assertions pass (28,350 prisms / 1,373,051 volume / 240 always-on colliders) |
| `Tools/Build/author_drumfire_assets.py --check` | clean, idempotent, 21 files |
| `Tools/Build/check_conditional_compilation.py` | OK |
| `ApproachLaneGeometryTests` | compiled against the shipped source and run outside Unity — **37 cases, 0 failures**, negative-controlled (a slot-major mapping raises 18 failures; a swapped heading raises 44) |
| `DrumfireScoringTests` | compiled against the real scoring sources and the shipped asset — **12 tests, 0 failures**, negative-controlled (a rule that races the clock raises 2) |
| `CrystalManager` lane refactor | proven bit-identical to the pre-refactor inline math over 1,257 placements |
| Scene clone | 82 documents, matching the donor, zero new dangling fileIDs |

**Still to confirm in the Editor** (the human is the gate):

1. Open `MinigameDrumfire` and let the drum build. Run *FrogletTools > Ecology > Measure Cell
   Environment Baselines* and check the drum against the simulated **28,350 count / 1,373,051
   volume**. A mismatch means the C# and the model have diverged.
2. Play a 2-pilot match. Confirm each Dolphin spawns at the mouth of its own lane and that the
   crystal line runs past the drum rather than into it.
3. Watch one full pass and confirm the drum is still substantially standing — §2.1 is the failure
   to look for, and the arena model's consumption table is an *upper* bound under idealised aim
   (43.5 % at full energy over two passes).
4. Tune `EndConditionOverridesSO.drumfireSeconds` and the lane band from there.
