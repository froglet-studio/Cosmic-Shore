# Ark escort: pacing and plates

Read-only research on `D:\Voyage\wt-c` (bleeding-edge). Line numbers are from that checkout. Paths are under
`Assets/_Scripts/` unless stated.

## 1. How the Ark moves today

**Path.** The corridor (`Controller/Toys/CellConveyor.cs`) stands cells centre-to-centre `CellSpacing` apart along a
heading that may bend up to `MaxTurnDegrees` per hop (`NextCentreFrom`, 501-515, floor `Mathf.Max(2000f, CellSpacing)`).
The first centre is `hostRadius + CellSpacing * 0.5` out (219). The Ark's destination is always the current target
cell's CENTRE (`ArkwayRun.AimArk`, `Controller/Toys/ArkwayRun.cs:683-690`); it "arrives" within
`CellConveyor.ArriveDistance = 140f` (CellConveyor.cs:48, checked at ArkwayRun.cs:596) and `AdvancePastTarget`
(CellConveyor.cs:252-274) immediately retargets the next centre, 3200 u away.

**Speed.** `Ark.SetSpeedProfile(approach, cruise, slowRadius)` (`Controller/Environment/Ark.cs:755-760`) and
`Ark.Update` (800-824): speed is a pure function of distance to the destination,
`t = clamp01(dist / slowRadius); speed = lerp(approach, cruise, smoothstep(t))` (814-815). `slowRadius` is the target
cell's `MembraneRadius` or the fallback 1300 (`CellConveyor.CurrentCellRadius`, 105-113). Turn rate 40 deg/s
(`TurnDegreesPerSecond`, Ark.cs:75). No acceleration state.

- `ArkwayConfig.ArkSpeed = 18` u/s (approach, "pace of the slow pass"), `ArkCruiseSpeedFactor = 4` → cruise 72 u/s,
  `CellSpacing = 3200` (ArkwayRun.cs:21-55; authored in `_SO_Assets/Toys/Wander_WithArk.asset:17-27`).
- Per difficulty, `ArkEscortRun.SetSail` does `_cfg.ArkSpeed *= tier.ArkSpeed`
  (`Controller/Voyages/ArkEscortRun.cs:126`), tiers 1.3 (d1) → 1.0 (d4) → 0.75 (d9) (`_SO_Assets/Toys/ArkEscort.asset:59-104`).
  Because cruise = approach x 4, the tier scales BOTH: d1 cruises at 93.6 u/s, d4 at 72, d9 at 54. Vessel cruise is
  ~60-68 u/s (`SwitchbackPar.DolphinCruiseSpeed = 68`, `WrasseRunCourse.CruiseSpeed = 60`), so at d1-d5 the Ark leaving
  a cell outruns the squad: that is the Squirrel chase.

**Where the time goes (d4, R = 1300, spacing 3200).** Leaving a core, the next centre is 3200 u away, so the Ark is at
full cruise from the first frame: it crosses the EXIT half of the cell (1300 u) at 72 u/s in ~18 s, the open water
(~600 u) in ~8 s, then brakes over the entry membrane band: ~28 s from membrane to core, of which ~22 s is the inner
650 u (speed is still 45 u/s at half radius). Per leg ~54 s; ~26 s of each cell's ~46 s is spent above 45 u/s. The slow
part the escort was designed around (the last few hundred units) is ~10 s. Nothing in the profile knows about the
cell it is LEAVING, which is why the exit is a sprint.

## 2. Code plan: slow on entry, crawl the cytoplasm, short gaps

Goal: cruise only in open water; brake crossing the membrane; crawl (tier-scaled) from membrane to core and on to
the far membrane; then accelerate. Gap time comes from shorter spacing, not from a faster Ark.

**Ark.cs**
1. Replace the two-speed profile with a struct and a departure anchor:
   ```csharp
   [System.Serializable] public struct ArkSpeedProfile { public float Cruise, Entry, Crawl, MembraneBand; }
   public void SetSpeedProfile(in ArkSpeedProfile p)                       // replaces SetSpeedProfile(approach, cruise, slowRadius) at 755
   public void SetDestination(Vector3 centre, float membraneRadius)         // already exists at 790; keep
   public void SetDeparture(Vector3 centre, float membraneRadius)           // NEW: the cell just left
   ```
   Keep the old `SetSpeedProfile(float, float, float)` as a thin wrapper (Entry = Crawl = approach, band = slowRadius)
   so Wander_WithArk keeps its feel.
2. In `Update` (807-822) compute speed from TWO ranges:
   - `dIn  = dist(pos, dest) - destRadius` (negative once inside the destination membrane)
   - `dOut = dist(pos, departure) - departureRadius` (negative while still inside the departed cell)
   ```
   if (dOut < hullHalfLength)                   speed = Crawl                       // still in the cell we are leaving
   else if (dIn > MembraneBand)                 speed = Cruise                       // open water
   else if (dIn > 0)                            speed = lerp(Entry, Cruise, smoothstep(dIn / MembraneBand))   // braking onto the membrane
   else                                         speed = lerp(Crawl, Entry, smoothstep(clamp01(-dIn / MembraneBand)))  // inside: ease to crawl
   ```
   Still a pure function of position (the doc comment at 743-754 stays true). `hullHalfLength` = `ArkHullLength * 0.5`
   passed through `LayHullAsync` (231) or the profile struct.
3. Expose `public bool InsideCell` (dIn < 0) and `public float DistanceToDestination` for the run's metrics (§4).

**ArkwayRun.cs / ArkwayConfig**
4. New config fields (ArkwayRun.cs:20-27) and matching `[SerializeField]`s on `ScriptableObjects/Toys/ArkwaySettingsSO.cs`
   (40-55, copied into the config at 161-163): `ArkCruiseSpeed = 55` (absolute, below vessel cruise, replaces the x4
   factor), `ArkEntrySpeed = 24`, `ArkCrawlSpeed = 14`, `ArkMembraneBand = 400`. Keep `ArkSpeed` only as the legacy
   wrapper input.
5. `AimArk` (683-690): build the profile, call `SetDestination(CurrentTargetCentre, CurrentCellRadius)` and
   `SetDeparture(_conveyor.PreviousCentre, _conveyor.PreviousCellRadius)`. Add those two getters to CellConveyor next to
   `CurrentTargetCentre` (98-113): `_cells[_targetIndex - 1]` when `_targetIndex > 0`, else the host cell's centre/radius.
6. Spacing: `Wander_WithArk.asset cellSpacing 3200 → ~2800` for the escort (two membranes of ~1200-1300 plus a 200-400 u
   gap). `ArkwaySettingsSO.cs:94` has `[Min(2000f)]` and the tooltip rule "must exceed two membrane radii"; the escort can
   also override `_cfg.CellSpacing` in `ArkEscortRun.SetSail` after `BuildConfig` (124) via a new `ArkEscortSO` field
   `cellSpacing` (default 2800). Gap at 55 u/s ≈ 5-8 s.
7. Arrival still fires at the core (ArriveDistance 140); the retarget no longer changes speed because `dOut` holds
   Crawl until the stern clears the departed membrane.

**ArkEscortSO / ArkEscortRun**
8. `Tier.ArkSpeed` (ArkEscortSO.cs:64-65) becomes a multiplier on CRAWL (and Entry) only; at ArkEscortRun.cs:126 set
   `_cfg.ArkCrawlSpeed *= tier.ArkSpeed; _cfg.ArkEntrySpeed *= tier.ArkSpeed;` and leave cruise alone. Retitle the
   tooltip. Optional: `Tier.CruiseSpeed` absolute if a tier wants to vary the gap too.

**Expected feel (R 1300, crawl 14, d4).** Membrane-to-membrane 2600 u at ~14-18 u/s ≈ 2.5-3 min per cell; gap ≈ 6 s;
run ≈ 13-15 min (today ≈ 4.5 min). If that is long, raise crawl to 20 (≈ 2 min per cell). The sim's
`RunTimeoutSeconds = 1800` (`ArkEscortSim.cs:27`) is real seconds at time scale 3, so it still fits; at time scale 2
tighten the plan or raise the timeout. `ArkEscortPilot.StationRadius 260 / GuardRadius 900` need no change;
`WingmateAbeam 140` placements (ArkEscortRun.cs:30, 454) are fine at crawl.

## 3. Shielded / super-shielded plates: why fauna never eat them, and how they could

**How plates get their tier.** The model lays one prism per socket of the rig table with `socket.Kind`
(`ArkModel.BuildLays`, `Controller/Environment/ArkModel.cs:245-276`; `ArkRigTable.ParseKind` 118-124 maps
"Shielded"/"SuperShielded"/"Danger"/Plain from `_Models/Vessel Models/ArkReverse.rig.json`). The lay applies the kind
through `PrismKinds.Apply(prism, kind)` which sets `prismProperties.IsShielded / IsSuperShielded`
(`Controller/Environment/PrismProperties.cs:16-17`). The reactor regrows a slot from the same `target.Lay`
(`Ark.RegrowOne`, Ark.cs:514), so a regrown plate comes back armoured.

**Three independent gates keep fauna off them.**
1. Steering: `Cell.AddBlock` only files a prism into the density grids when `!IsInedibleShield(block)` (Cell.cs:3322;
   re-filed on shield change at 3409-3418). Fauna aim at `GetDensestRegion*` (Boid.cs:220), so armoured plates are not
   even attractors. `Cell.IsInedibleShield` (3433-3439): super → always inedible; shielded → inedible unless
   `ShieldedMassIsFood` (= `phase == CellPhase.Frenzy`, 3426).
2. Diet: every grazer's edibility predicate routes through `Fauna.IsInedibleShield` (Fauna.cs:891-898; Boid
   `IsEdibleForForager` 712, LightFauna `IsEdibleForHerbivore` 843, intruder test Cell.cs:655). Super is never food;
   shielded only in a Frenzy or when `IFaunaHerd.EatsShields` (only `JacksExecutor.cs:1318`).
3. The prism itself: `Prism.Consume` (`Controller/Vessel/Prism.cs:1943-1962`) returns at `AbsorbSuperShieldHit(0f)`
   (1950, 2000-2005) for super, and for shielded without `devastate` only `DeactivateShields()` (1955-1956).

So the ~20% armoured sockets are an untouchable floor: `Ark.HealthFraction` (157-158) bottoms near 0.2 and the
`BreachHull` line (ArkEscortSO.cs:67-70, `ArkEscortRun.CheckBreach` 330-345) was added to fake a loss.

**Plan: armour that wears under sustained grazing, per species.**
1. `Prism`: add a wear counter and one entry point, next to `Consume`:
   ```csharp
   float _armourWear;                                    // reset in the pool-reuse block at ~1002
   /// A creature bite on an armoured prism: wears the armour; true when it just cracked (caller then Consume()s).
   public bool GnawArmour(float bite, string eaterName)  // bite in "armour points"; shielded cracks at 1, super at SuperArmour (2-3)
   {
       if (destroyed || prismProperties is not { IsShielded: true } and not { IsSuperShielded: true }) return false;
       _armourWear += bite;
       StampDeflection(0f);                              // the existing wobble reads as "being gnawed"
       float need = prismProperties.IsSuperShielded ? SuperArmour : 1f;
       if (_armourWear < need) return false;
       DeactivateShields();                              // the sanctioned break sequence (doc at 1980-1983)
       return true;
   }
   ```
   `AbsorbSuperShieldHit` is untouched: vessels, explosions and abilities still bounce. Only creature bites wear.
2. Per-species rate: `LightFaunaDataSO` (`Controller/Environment/FloraAndFauna/LightFaunaDataSO.cs`, fields at 11-87):
   `[Min(0f)] public float shieldBite = 0f;` and `[Min(0f)] public float superShieldBite = 0f;` (armour points per
   mouthful; 0 = this species never gnaws). Same two `[SerializeField]`s on `Boid.cs` beside `maxClusterBites` (113), and
   on `WormColonyConfigSO` / substrate configs for species that use `Fauna.Consume` paths of their own. Author e.g.
   sharks 0.5/0.25 (two bites for a shield, four for super), brittlestars 0.2/0, scavengers 0.34/0.17, quadfish 0/0.
3. Diet: `Fauna.IsInedibleShield` (891-898) gains `protected virtual float ShieldBite(bool super)`; return false when the
   species' rate for that tier is > 0 (super no longer unconditional). In `ConsumeMouthful` (Boid.cs:796-833,
   LightFauna.cs:934-966) replace each `prism.Consume(...)` with a Fauna helper:
   ```csharp
   protected bool Bite(Prism p, Domains d, string eater, bool devastate)
   {
       if (IsShieldedMass(p) && !p.GnawArmour(ShieldBite(p.prismProperties.IsSuperShielded), eater)) return false; // still armoured: keep the feed hold, come back
       p.Consume(transform, d, eater, true, true); return true;
   }
   ```
   The feed-hold loop (`_feedTarget`, `consumeHoldSeconds`) already returns the creature to the same prism, so wear
   accumulates one hold at a time: a shark strips a super plate in ~4 holds, exactly the "eats through the armour"
   read. `NotifyFed` only on a real consume (the stomach stays conserved).
4. Steering: the grids must file armoured plates for species that can eat them. Cheapest honest rule: a Cell flag
   `public bool ArmourIsFood { get; set; }` folded into `Cell.IsInedibleShield` (3433) the way `ShieldedMassIsFood` is,
   set by `ArkEscortRun.PrepareCell` (178-196) for every escort cell (the Ark is the only armoured mass in a traversal
   cell). Call `SyncShieldFoodFiling`-style refile when it flips (3448-3461). Side effect: Cell.cs:655 then counts
   plates as intruders → pens go Frenzy → shielded already food; acceptable, note it in the doc.
5. Attribution and loss: `Prism.Implode` stamps `DestroyerName`, `DestroyedByCreature`, `DestroyedAt` (Prism.cs:55-61,
   1693-1695). `Ark.ScanHullIntegrity` (882-898) sees the plate gone, `HealthFraction` falls for real, the reactor
   regrows it armoured. Drop `BreachHull` to 0 in the tiers (keep the field as an optional line) once the sim shows real
   falls; retune `RegrowSeconds` since regrowth now races plate loss.
6. Networking: wear runs only where `Fauna.IsSimAuthority` (Fauna.cs:1472) is true; the crack goes through
   `DeactivateShields` → `PrismStateManager.SyncAOERegistryShieldState` (the funnel named at Cell.cs:3404), so clients
   see the shield drop before the consume replicates. Do not replicate `_armourWear` itself.

## 4. New per-run metrics for the escort JSON

`ArkEscortReport` (`Controller/Voyages/ArkEscortReport.cs`) is `JsonUtility`-serialised: new fields must be public
`[Serializable]` classes/lists, no dictionaries. `Tools/Build/ark_escort_report.py` reads `LegReports[*]` by name;
extend its columns accordingly.

| Metric | Fields (LegReport unless noted) | Hook |
|---|---|---|
| Escort proximity per seat | `List<SeatNear> Seats { Seat, Vessel, PlayerName, Near300, Near900, Samples }` | `ArkEscortRun.Sample` (357-388), 1 Hz; it already loops local + `_wingmates` against `GuardRadius` (366-377): add a 300 u ring (`ArkEscortPilot.StationRadius`-ish) and bucket per seat via `SquadDeployment`'s seat / `IVesselStatus.PlayerName` (IVesselStatus.cs:58). |
| Kills on Ark-attackers vs other | `KillsOnArk`, `KillsElsewhere`; `List<SeatKills> { PlayerName, Kills, KillsOnArk }` | Today `CellRuntimeDataSO.OnFaunaKilled` carries only the killer string (Fauna.ReportKill 1727-1739). Add `public static event Action<Fauna,string> Fauna.Killed` raised in `Die` (1634-1660) beside `ReportKill`. In the run keep `HashSet<Fauna> _threatened` filled by `CountThreats` (392-416, already enumerates threats within 900 u every pilot retarget) with a last-seen stamp; on `Killed`, membership (seen < 10 s ago) → on-Ark. Killer string = `PlayerName` (Skimmer/Explosion wither SOs: `SkimmerWitherLifeformByCrystalEffectSO.cs:78`, `ExplosionWitherLifeformByCrystalEffectSO.cs:92`) → seat. |
| Plate damage per world | `PlatesLost`, `PlatesLostShielded`, `PlatesLostSuper`, `PlatesByCreature`, `PlatesRegrown`, `List<KillerCount> PlateKillers` | Add `public event Action<Prism> HullPrismLost` to `Ark`, raised from `ScanHullIntegrity` (882-898) by diffing a `_wasAlive` bitset per slot (read `DestroyerName`, `DestroyedByCreature`, `prismProperties.IsShielded/IsSuperShielded`). `Ark.HullPrismRegrown` (218, raised at 535) already exists. Subscribe in `OnUnderway` / `Finish`. |
| Time inside vs outside cells, Ark speed | `InsideSeconds`, `OutsideSeconds`, `MeanSpeed`, `Sample.Speed`, `Sample.Inside` | `Sample`: `Ark.Speed` (177) and the new `Ark.InsideCell` (§2.3), or `dist(ark, _corridor.CurrentTargetCentre) < CurrentCellRadius`. |
| Per-vessel kills / mass / territory | `List<SeatStats> { PlayerName, LifeformsKilled, VolumeCreated, BlocksCreated, HostileVolumeDestroyed }` per leg (delta) and run | `gameData.TryGetRoundStats(playerName, out IRoundStats)` (`Controller/Managers/StatsManager.cs:117,209`; fields `Data/Enums/IRoundStats.cs:74-111`). Snapshot at `TrackLeg` entry (313-327) and `CloseLeg` (347-355); server-only stats, fine for the host/sim. |
| Territory per world | `SquadVolumeAtExit`, `HostileVolumeAtExit`, `ControllerAtExit` | `CloseLeg`: `cell.GetDomainVolume(domain)` (Cell.cs:1008), `GetTeamVolume` (3583), `cell.DominantDomain`; cell from `_legOf` inverse (keep a `Cell[] _cellOfLeg`). |
| Hull breakdown at entry/exit | `ArmouredAlive`, `PlainAlive` | Add `Ark.CountAlive(out plain, out shielded, out super)` beside `AliveCount` (154). |

`ArkEscortSim.Write` (JsonUtility.ToJson) needs no change. Add the new columns to `ark_escort_report.py` (per-world
plates lost / kills-on-Ark / seat proximity means).

## 5. Risks

- **Run length / GPU.** A crawling Ark triples time per cell, so peak-population scenes last 3x longer. The D3D12
  device loss (tuning log pass 6, d9 at 2.6x population, editor) happened under sustained high prism counts;
  `maxPopulation` is now 2 (ArkEscort.asset:105), d9 1.4x. Keep that cap, run the sim at `-arkEscortTimeScale 2` rather
  than 3 (time scale multiplies sim steps per rendered frame), and watch `LogCensus`'s creation backlog warning
  (ArkwayRun.cs:653-664, threshold 3000). Background: `Docs/PRISM_ECS_MIGRATION.md §8`, `Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md:33`.
- **Perf of the plan itself.** Armour wear is one float per prism and a `StampDeflection`; filing ~30 extra plates in
  the grids is nothing. The `Fauna.Killed` static event must be unsubscribed in `Finish`/`OnDestroy` (the run already
  does this for `OnFaunaKilled`, 484-486). `HullPrismLost` diffing is over ~150 slots every 0.5 s.
- **Fauna mobbing a slow Ark.** With plates as food and a 2.5 min dwell, the Ark becomes the cell's densest edible
  region; `Ark.CellRebindSeconds = 2.5` (74) keeps the grid honest. Expect the tiers to need a full re-tune (pass 10):
  `RegrowSeconds` now fights real loss; `BreachHull` may go to 0.
- **Membrane overlap.** Shorter spacing must stay above two membrane radii (`ArkwaySettingsSO.cs:94-96`,
  `CellConveyor.NextCentreFrom` floor 2000). `MembraneRadius` reads 0 until the membrane spawns (CellConveyor comment
  213-217); the departure radius must use the same fallback as `CurrentCellRadius`.
- **Networking / ownership.** `Ark` has no NetworkVariables: each peer runs its own `ArkwayRun`/`Ark`; the pure
  position-function profile keeps peers deterministic only if destinations agree. Wingmate spawning is server-gated
  (`ArkEscortRun.Deploy`, 208). Kill attribution is already single-authority (`Fauna.ReportKill`, 1733); plate wear and
  the `Killed` event must follow the same `IsSimAuthority` gate or clients double count. Shield drops replicate through
  the existing shield-state funnel; `_armourWear` must not.
- **Side effects of `ArmourIsFood`.** Cell.cs:655 counts edible intruder mass for `ContainmentIntruderFrenzy`; armoured
  plates now trigger Frenzy in penned cells (The Swarm). Probably desired (the pen "sees" the Ark) but it changes pass-9
  numbers for that world.
- **Other armoured mass.** The flag is per-cell and set only on escort cells, so Skim Race tracks, Cleave arenas and
  boost rings keep their immunity; a non-zero `superShieldBite` on a species still only matters where `ArmourIsFood`
  files the plates, except via the feed-cluster query (`QuerySphere` in `ConsumeMouthful`), which can reach an armoured
  prism next to plain food. Gate `Bite` on `cell.ArmourIsFood` too if that worries the arenas.
