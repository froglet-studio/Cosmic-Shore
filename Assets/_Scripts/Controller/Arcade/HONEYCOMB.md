# Honeycomb (GameModes.Honeycomb = 109)

The Honeyguide's **Mass** game (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 19, hint toast 184).

## The loop
A **timed** building war (default 90 s, `honeycombRoundSeconds` in End Game Conditions) on the bare Barren cell.
The score is the prism volume a domain has standing in its own colour at the whistle
(`ScoringMetric.VolumeRemaining`, a live stock, so what rivals break counts against you). An omni crystal is a hive:
a comb of super-shielded prisms behind the hull while the Ratel plays the hive puzzle, and a human's hull also
plants a Mass hive every 45 s. Elemental crystals are scattered peer-locally at the countdown (24/16/10/6 by
intensity, radius 900, seed 109). The lead is announced after 20 s, at most every 10 s.

## Reused vs owned
- **Reused:** the Barren cell (Tapestry's), the timed turn monitor (`NetworkTimeBasedTurnMonitor`), the
  Tapestry/Glasshouse controller shape, `DomainRaceLeadChanged` toast, comeback toast,
  `RampageObjectiveProvider` on the HUD.
- **Owned:** `HoneycombController`, `HoneycombSettingsSO`, `HoneycombScoringRuleSO` (authored templates through
  `TemplateText`), `HoneycombTimeTurnMonitor`, the hint toast.
- **Not changed:** the vessel.

## The comeback rate
Sized against a volume, so the generator reads the Honeyguide's trail off the shipped prefab (10 volume a prism at
5 a second, 50 a second; reference 4500). The rate 0.001333 buys 1.5 element levels a quarter of the reference
behind. A vessel retune moves it.

## AI
No mode hooks: the AI runs the platform's crystal chase.

## Assets
`Tools/Build/author_honeycomb_assets.py` (`--check`).

## Verification status
Authored headless and passing every generator gate. **Not compiled in Unity and not run in the editor.** Card art
is the placeholder until `/cardart` renders it.

## Known limitations
- An AI crew plants no Mass hive (only a human hull does), so humans get extra hives. That is a vessel question for
  the Honeyguide's owner, not something a mode may change.
