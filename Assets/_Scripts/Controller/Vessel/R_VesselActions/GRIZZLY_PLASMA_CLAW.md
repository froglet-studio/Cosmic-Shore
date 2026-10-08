# Grizzly — Plasma Claw & the Burn System (third weapon; Mass 5 upgrades the burn)

Design source: `ClassGrizzly.md`. Element link: **Mass** — the weapon ships
unlocked and Mass 5 upgrades its burn from destroy to steal. (The doc's original
open question was resolved as a single unlock covering both; that was split on
2026-08-27 so the weapon is always usable and only the theft is earned.)

The Grizzly is the ONLY vessel that can light an area — and enemy trails — on
fire with a spreading burn. This file also documents the burn system itself,
since it is net-new (no burn/ignite/DoT existed in the codebase before).

## Plasma claw (GrizzlyFlamethrowerActionSO/Executor)

- Third slot in the weapon cycle, **available from level 1** — `GrizzlyWeaponModeExecutor`
  cycles Explosives -> Sniper -> Flamethrower unconditionally.
- **Mass 5 gates what the burn DOES, not whether the weapon exists.** Below it a
  burnout DESTROYS the prism; at Mass 5 it STEALS it (`convert` is sampled at ignite
  time and carried on the burn, so a later relock cannot retroactively change a burn
  already lit). Changed 2026-08-27: gating the whole weapon mode put a third of the
  vessel’s offence behind a level most matches never reach, and the interesting half
  of the unlock was always the theft rather than the weapon.
- **Costs no energy** (per the doc) — the tradeoff is range and time-to-kill.
- While held: scans a forward cone (`QuerySphere` + angle filter,
  `IgniteTicksPerSecond`, `IgnitesPerTick` budget) and ignites enemy prisms.
- Mass-5 state is sampled at IGNITE time: burns started under the upgrade
  convert on burnout even if the level drops mid-burn.

## Burn system (PrismBurnManager — Controller/Managers/)

- Self-creating singleton (`EnsureInstance`), zero scene wiring.
- Per-prism state: igniter, domain, extinguish time, next spread roll,
  convert-on-burnout.
- **Spread:** every `spreadInterval`, each burning prism rolls `spreadChance`
  against neighbors within `spreadRadius` (PrismSpatialIndex.QuerySphere).
  Enemy trails burn through the same path — trail prisms are prisms.
- **Burnout:** destroyed (`Prism.Damage`) — or STOLEN (`Prism.Steal`) when
  convert-on-burnout: fire → theft → the enemy's volume becomes yours
  (mass production through arson). Shielded prisms decay their shield first
  (Steal's own semantics — a natural two-stage burn); super-shielded prisms
  cannot burn at all.
- **Visual v1:** the Danger prism state (`MakeDangerous`). A dedicated Burning
  material state needs ThemeManagerDataContainer assets — deferred to art.
- **Perf:** ~4 Hz tick, concurrent-burning cap (192), burnouts budgeted per tick
  (8) because each Steal/Damage triggers material/animation work. `ResetAll()`
  clears everything (call on turn end / scene transitions).
