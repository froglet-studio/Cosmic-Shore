# Build prompt: the payload, pilot vision and artifacts

Paste everything below the line into a new Claude Code session on `froglet-studio/cosmic-shore`. Written by
`cece/sweet-mendel-yoq3vo` (Garrett, 2026-10-27) once the designs were decided.

---

You are building the squad layer Garrett designed on 2026-10-27, which turns a squad into a **12-card deck**: three
vessels, each with four cards (vessel, element, pilot, artifact). The kit and elements already exist. You build the
other two cards, plus the payload they turn on. Work autonomously through the phases below; stop only at the
decision gates named here.

## Read first (all of it, before any code)

1. `CLAUDE.md`, then `Docs/TEAM.md`. Run `git fetch origin` and
   `python3 Tools/Build/who_works_where.py query Docs/Pilots Assets/_Scripts/Controller/Environment/Ark.cs Assets/_Scripts/Controller/IO`.
2. **`Docs/Pilots/CALLSIGNS.md`**, the pilot concept. A pilot grants vision: hold **LB** and one of 17 callsigns lights
   what the pilot sees while everything else is suppressed.
3. **`Docs/Pilots/ARTIFACTS.md`**, the artifact concept.
   - A **payload** is a parcel of the squad's Ark hull. Any vessel grabs it with **RB** and fires it with RB to crack
     super-shielded and shielded prisms.
   - An **artifact** works on any vessel and transforms the payload.
   - Upgrades are paid in elemental crystals on four element tracks.
   - Theft means taking down a squad's Ark, which gives you every artifact aboard. One Ark per squad of three.
   - An Ark felled by fauna spills its artifacts for anyone; any left uncollected are lost when the instance expires.
4. The precedents you reuse, not rebuild:
   - the Dolphin's Echo Sight (`EchoSightActionExecutor`, `EchoSightVesselHighlighter`, `EchoSightHalo.shader`) and
     `Docs/LIT.md` (`PrismLit`), for vision;
   - `SniperCrackSuperShieldPrismEffectSO`, for the crack;
   - `Ark` and `Docs/ECOSYSTEM.md` §41 (the Arkway, the Wander toy's "With Ark"), for the Ark;
   - `Docs/ElementalAbilitySystem/SILENCE.md`;
   - the LOCKED "PvP is petals only" rule in `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`.
5. **Shombith's in-flight pilot branch `claude/pensive-dijkstra-qxoxo0`** (and `claude/modest-sagan-0ybcgo` under it).
   It holds the data types this is built on: `PilotSO`, `ArtifactSO`, `PilotArtifactCatalogSO`, the saved squads
   (`SquadBook` / `SQUAD_DATA`), `PilotLoadoutStore`, `VesselPilotLoadout`, `author_pilot_assets.py`, and the
   Hangar PILOTS / ARTIFACTS tabs. Its *design* (pilots rewrite one ability; artifacts are a number) is superseded by
   the two docs above. Its *types* are not.

## Gate 0, before any code: the foundation

Check whether `claude/pensive-dijkstra-qxoxo0` has merged into `bleeding-edge`
(`git merge-base --is-ancestor origin/claude/pensive-dijkstra-qxoxo0 origin/bleeding-edge`).
- **Merged:** build on its types.
- **Not merged:** ask Garrett, in one question, whether to merge that branch into yours and build on it, or wait.
  Never write a second `PilotSO`, `ArtifactSO` or squad store beside it. Shombith heads tooling and the backend.
  Anything that changes his roster, his Hangar tabs or the Tomahawk throw goes through him; say so in the commit
  ("Affects: Shombith's pilots").

## Phases (in this order, each one shippable)

**1. The plain payload.** Build it where an Ark already exists: the Wander toy's "With Ark", `ArkwayRun`.
- `ArkHold` on `Ark` hands out a parcel of real hull prisms. Mass is conserved: move existing prisms, never mint.
- `PayloadCarrier` on every vessel: **RB** grabs within reach of your squad's Ark, the parcel rides visibly on the
  hull, and **RB** again releases it.
- The crack runs as a prism impact effect on the same route as `SniperCrackSuperShieldPrismEffectSO`. It cracks
  super-shielded and shielded prisms in a radius, then the parcel is spent.
- Silence makes a carrier's payload harmless.
- RB is free on every vessel: the three abilities that used it moved to Y on 2026-10-27. Give the payload an honestly
  named `InputEvents` member (fixed value; claim it only after checking `bleeding-edge` and the in-flight branches)
  rather than reusing the legacy `FlipAction`. `Docs/REFACTOR_BACKLOG.md` S73 has the evidence.
- Authored text and empty FMOD `EventReference`s, per the two LOCKED rules.

**2. Pilot vision on LB.**
- A `PilotVision` enum with fixed values 1-17 in `CALLSIGNS.md` §2's order, `PilotSO.vision`, and a `CallsignSetSO`
  in `Resources/Pilots/`.
- A `PilotVision` component on every vessel with one provider per sight: a prism predicate (ENSIGN, RAMPART, CITADEL,
  HORNET), a volume (MANGER, from `Cell.GetDensestRegionAnyDomain`), and target sets (crystals by `Element`, plants
  by `Flora`, fauna by `FaunaDiet`, vessels by side, switches, Arks).
- The suppression is a post-processing volume weighted by LB.
- It never takes slots in Lit's peer bank. Give its globals new names (`Docs/LIT.md`'s bank-rename lesson). It stays
  local; only the hull's callsign plate is shared.
- Measure the cost of marking hundreds of fauna, plants and crystals before choosing instanced versus shader-side
  (`Docs/claude/SHADERS_AND_PERFORMANCE.md`).
- Start with the four cheapest sights (ENSIGN, CITADEL, BANDIT, VOLT) and prove the look before adding the rest.

**3. Two artifacts on any vessel.** Javelin (offence) and Aegis (defence).
- Reshape `ArtifactSO` to a payload transformation with four element tracks (0-10, a twist at 5).
- No vessel class in any artifact.
- The squad slot's artifact loads the payloads that player grabs.

**4. The Ark's fall.**
- The credited squad (whoever destroyed the last hull prism) gets the whole hold, server-side, in one step.
- An Ark felled by fauna spills `ArtifactPickup`s that anyone grabs with RB and carries home. The instance deletes
  uncollected ones when it ends.

**5. Upgrades.** Per-element crystal balances and artifact track levels on the player profile. **That is backend work
and goes through Shombith.** Today there is one crystal balance (`CrystalCurrencyDisplay`), and the Elemental
Converter is animation only. Build the Ark-screen panel against a device-local stand-in until his backend lands, as
`PilotLoadoutStore` does.

**6. Voyages.** Vaults (super-shielded shells with an artifact inside), the hold and extraction belong to Voyages,
the fourth game location after Toybox, Arcade and Arena (Garrett, 2026-10-28; `Docs/Pilots/ARTIFACTS.md` §8.5). They are
not an Arkway mode: the Arkway is a toy, a taste of a freestyle voyage. Voyages need their own plan before any build.

## Rules that are not negotiable

- **`/verify-unity` before every C# commit.** In a cloud session with no editor, say so in the commit, file hand
  tests in `Docs/UNITY_VERIFICATION_CHECKLIST.md`, and run `bash Tools/Build/unity_refcompile/run.sh` (player and
  `--config editor`) when a .NET SDK is available.
- **Ownership and networking.**
  - The server arbitrates every grab, transfer and spill.
  - Nothing reads a vision light to decide an outcome.
  - PvP moves petals only, through `CombatHitDrain.TryAdmit`.
- **Config and data.**
  - Config goes in ScriptableObjects.
  - A generator that writes assets owns them: `--check` diffs disk, with negative controls.
  - Editor tools live under `FrogletTools/` and record their output.
- **Never assign tickets.** Suggest hand-offs through `/team-tasks` only with the person's approval.

## Decisions still open (ask; do not invent)

| Decision | Who decides |
|---|---|
| ENSIGN shows your own mass or the rivals' | Garrett |
| Singularity moving super-shielded mass, an exception to *Prism movement* | Garrett |
| BANDIT on the Dolphin (it may make the Charge level-5 upgrade redundant) | the Dolphin's owner |
| Where each hull's callsign plate sits | `/vessel-model` |
| Thieves hoarding artifacts | the ecology owner |
| An artifact sight among the callsigns | Garrett |
| What becomes of the 40 ability-rewrite pilots and 45 numeric artifacts | Garrett and Shombith |

Finish with `/ship`.
