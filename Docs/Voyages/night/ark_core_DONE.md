# Ark core: pacing, plate eating, escort metrics, params hook (DONE)

Branch `claude/voyage-ark-core` in `D:\Voyage\wt-a`. Paths under `Assets/_Scripts/`.

## What changed

1. **Ark pacing** (`Controller/Environment/Ark.cs`, `Controller/Toys/ArkwayRun.cs`, `Controller/Toys/CellConveyor.cs`)
   - New `ArkSpeedProfile` struct {Cruise, Entry, Crawl, MembraneBand, HullHalfLength, Ceiling} with a pure
     `SpeedAt(dIn, dOut)`: min of an exit ramp (crawl until the stern clears the departed membrane, then ramp to cruise
     over one band) and an entry ramp (cruise outside the band, Entry at the membrane, Crawl one band inside). Continuous;
     every speed clamped to `Ceiling`.
   - `Ark.SetSpeedProfile(in ArkSpeedProfile)`, `Ark.SetDeparture(centre, radius)`, `Ark.ClearDeparture()`,
     `Ark.InsideCell`, `Ark.DistanceToDestination`. Legacy `SetSpeedProfile(float,float,float)` unchanged (Arkway toy).
   - `ArkwayConfig.ArkPacing` (nullable): when set, `AimArk` uses the profile + destination radius + departure anchor.
   - `CellConveyor.HasDeparture / DepartureCentre / DepartureRadius`: the cell whose core the Ark last reached (no
     anchor at voyage start: the Ark cruises out of the host instead of crawling through it). Brief's host-anchor idea
     was dropped because the Ark starts wherever the player stood in the host.
   - Escort: `ArkEscortSO` gains `arkPacing` (default Cruise 45, Entry 22, Crawl 14, Band 400, HullHalf 60, Ceiling 50),
     `cellSpacing` 2800 (overrides Wander_WithArk's 3200; 0 keeps it), `platesAreFood` true. `Tier.ArkSpeed` now scales
     Crawl and Entry only (cruise untouched). `ArkEscortRun.ApplyPacing`.
2. **Plate eating** (`Prism.cs`, `Fauna.cs`, `Boid.cs`, `LightFauna.cs`, `LightFaunaDataSO.cs`, `Cell.cs`)
   - `Prism.GnawArmour(bite)`: wear counter (reset on pool reuse, never replicated); shield cracks at 1 point, super at
     `Prism.SuperArmourPoints` = 2, via `DeactivateShields` (the sanctioned break); non-cracking bites wobble.
     `AbsorbSuperShieldHit` untouched: vessels/explosions/abilities still bounce.
   - `Fauna.ArmourBite(bool super)` (virtual, default 0), `GnawsArmourOf`, `BiteMass(...)` -> None/Gnawed/Ate;
     `IsInedibleShield` treats gnawable armour as food. Wear only where `IsSimAuthority` (puppets never crack).
   - `Boid` fields `shieldBite`, `superShieldBite` (default 0, foragers only); `LightFaunaDataSO.shieldBite /
     superShieldBite`. Boid and LightFauna mouthfuls go through `BiteMass`; a gnawed plate stays the feed target.
   - `Cell.ArmourIsFood` (refiles armoured blocks on change; `Cell.IsInedibleShield` returns false for armour there).
     Set only by `ArkEscortRun.PrepareCellMetrics` (escort worlds), cleared at finish. Not Ark-specific: no Ark weighting.
   - Authored bites: MassShark 0.5/0.25, MassBrittleStar 0.2/0, Clawfish 0.34/0.17 (QuadFish 0/0).
   - Reactor regrowth already re-lays from the slot's `PrismLay` (kind included) -> plates come back armoured (verified).
   - `ArkEscort.asset`: every tier's `BreachHull` -> 0 (field kept, tooltip says optional).
   - Carve-out recorded in `Docs/ECOSYSTEM.md` (section "0.x") and `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`.
3. **Metrics** (`Controller/Voyages/ArkEscortReport.cs`, new `ArkEscortRun.Metrics.cs` partial; `ArkEscortRun.cs`
   touched only with one-line hook calls, Deploy/BringWingmates untouched)
   - `Fauna.Killed(Fauna, killer)` static event (raised in `ReportKill`, authority only, same set as OnFaunaKilled);
     `Fauna.HomeCell`.
   - `Ark.HullPrismLost(Prism, PrismKind)` (slot-diffed, also checked before a regrow reuses the slot; hold-taken
     plates are not losses), `Ark.CountAlive(out plain, out shielded, out super)`.
4. **Params hook** (new `Controller/Voyages/ArkEscortParams.cs`, `ArkEscortSim.cs`)
   - `-arkEscortParams path.json` (flat JSON, number/true/false values) -> `ArkEscortParams.Pending`; each run
     `Instantiate`s the escort SO and applies the overrides to that copy (never the asset).
   - Registry of setters by dot-pattern with one `{placeholder}` segment: `ArkEscortParams.Register(pattern, setter)`.
     `ai.*` keys route to `ArkEscortParams.ExternalSetter` (`Func<string,float,bool>`, for `EscortGeneRegistry.TrySet`).
     Unknown/refused keys are logged and echoed with `Applied:false` and a `Note`.
5. `Tools/Build/ark_escort_report.py`: original table unchanged; new tables for pace/kills/plates, per-seat
   proximity and round stats, and applied/refused params. Old lines print blanks.
6. Tests: new `Tests/Editor/ArkPacingTests.cs` (profile shape, ceiling/continuity, params parse/apply, ai hook).

## Verification

Compile clean (0 `error CS`, D:\VoyageLogs\compile_wt-a.log). EditMode ArkModelTests 13, ArkFallTests 11,
ArkPacingTests 6: 30/30 passed (D:\VoyageLogs	ests_wt-a.xml). Pushed as 4 commits (aa188f9dc..0c63a2e40). Unity's import
touched some .mat / ProjectSettings / packages-lock files in the worktree; those are left uncommitted.

## New JSON keys (ArkEscortReport)

Run: `KillsOnArk, KillsElsewhere, PlatesLost, PlatesLostShielded, PlatesLostSuper, PlatesByCreature, PlatesRegrown,
InsideSeconds, OutsideSeconds, MeanArkSpeed, BreachHull, RunSeats[], Params[{Key,Value,Applied,Note}], ParamKeys[]`.
Leg: `KillsOnArk, KillsElsewhere, PlatesLost, PlatesLostPlain, PlatesLostShielded, PlatesLostSuper, PlatesByCreature,
PlatesRegrown, PlateKillers[{Name,Count}], PlainAtExit, ShieldedAtExit, SuperAtExit, InsideSeconds, OutsideSeconds,
MeanSpeed, SpeedSum, SpeedSamples, Seats[{Seat,Vessel,PlayerName,Near300,Near900,Samples}],
SeatStats[{Seat,Vessel,PlayerName,Kills,KillsOnArk,LifeformsKilled,VolumeCreated,BlocksCreated,HostileVolumeDestroyed}]`
(leg deltas), `ControllerAtExit, Territory[{Domain,Volume,ControlVolume}]`. Sample: `Speed, Inside`.
"On Ark" = the creature was within 900 u of the Ark in a 1 Hz sample in the last 10 s, or is at death.

## Params registry keys

`ark.cruiseSpeed, ark.entrySpeed, ark.crawlSpeed, ark.membraneBand, ark.hullHalfLength, ark.speedCeiling,
ark.cellSpacing, ark.platesAreFood (0/1), escort.maxPopulation, tier.{d}.population, tier.{d}.regrowSeconds,
tier.{d}.hostileShare, tier.{d}.arkSpeed, tier.{d}.breachHull` ({d} = 1-9 or `all`), `ai.*` (external).
Example: `{"ark.crawlSpeed": 18, "tier.all.regrowSeconds": 1.5, "tier.4.population": 0.7}`.

## Left undone / caveats

- `fauna.<configName>.<field>` keys not implemented (needs per-run copies of fauna config SOs referenced from
  prefabs); the registry is ready for them (`Register("fauna.{name}.floor", ...)`).
- Armour gnawing covers Boid and LightFauna eaters only; Swarm/Substrate/Worm/Nca/Builder consumers keep the old rule
  (ArmourBite 0). Boid prefabs author no bite yet (fields default 0).
- Spacing 2800 assumes escort membranes about 1300 or less; a bigger world would overlap. Not measured in-sim.
- No sim run done here: tiers (RegrowSeconds especially) need a re-tune now that plates are real food and BreachHull is 0.
- Run time triples (about 2.5-3 min per cell at crawl 14); the sim's 1800 s real-time timeout fits at time scale 2-3.
