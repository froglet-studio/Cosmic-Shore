# Prompt: a Space game for a vessel

Paste everything below the line into a new session. Replace `<VESSEL>` with the hull's name (the Thresher or the
Brittle Star for now) and paste its seed from `Docs/MassAndSpaceGames/PLAN.md` §4 at the end.

---

Build the **Space game** for the **<VESSEL>**: a new hull-locked arcade mode that is the vessel's Space card. The
<VESSEL> gets a Space game instead of a Mass game because its kit is built for destruction. The Dolphin is the
model: it has no mass ability worth a game, and **Rampage** is its Space game. The plan this belongs to is
`Docs/MassAndSpaceGames/PLAN.md`. Read it first, especially §1 (what a Space game is) and §6 (the rules).

**What a Space game is.** In this game a Space game is about destroying or reducing mass: the score is mass taken off
the board (`ModeGenre.cs`: `PrismsDestroyed`, `VolumeDestroyed`, `LifeformsKilled`). Look at how the shipped Space
games turn a hull's destruction into a race:
- **Rampage** (Dolphin): one crystal in the arena; skim the thicket to charge, take the crystal to unleash the blast,
  first domain to smash 2000 enemy prisms.
- **Wrecking Ball** (Scarab), **Cleave** (Swordfish), **Salvo** (Sparrow), **Bloomrush** (Manta), **Sirocco** (Tiger
  Moth).
The <VESSEL>'s Space ability and its **omni crystal** (the seed below) are what the game is built around: the crystal
is the moment the destruction peaks, the way Rampage's one crystal fires the Dolphin's blast.

**Before designing anything**
1. Run `/swimlanes` and `git fetch origin`, then
   `python3 Tools/Build/who_works_where.py query Assets/_Scripts/Controller/Vessel/R_VesselActions Assets/_SO_Assets/Games`.
   If the <VESSEL> lane is occupied, stop and tell me who holds it.
2. Load `/arcadegame` and follow it from §0. Load `/vessel` for the hull. If the game destroys creatures or plants,
   load `/ecology` (and `/fauna` or `/flora`) first.
3. Read the vessel's own doc (`Assets/_Scripts/Controller/Vessel/R_VesselActions/<HULL>*.md`, plus
   `Docs/Studios/BRITTLE_STAR.md` for the Brittle Star). Confirm the Space ability and omni crystal in the seed are
   still what ships.
4. Read the destruction-race siblings' docs (`Assets/_Scripts/Controller/Arcade/*.md`): Rampage, Wrecking Ball,
   Cleave, Salvo. Then read the vessel's own Charge game (Jackstraw for the Thresher, Flatline for the Brittle Star) so
   the Space game is clearly a different game: Charge hits pilots, Space breaks mass.

**Design (write the one-page brief first, `Docs/claude/DESIGN_BRIEF.md`)**
- One sentence a player understands: what you break, what makes you break more, how the race is won.
- **The vessel kit stays exactly as it is (LOCKED, Shombith 2026-10-31).** The game is interesting through its track,
  environment (what there is to break, how dense, how shielded, where the crystals sit), intensity ladder I1-I4 and
  AI only. No rationed abilities, no heat or cooldown changes, no added contact effects, no edits to vessel code,
  prefabs or action SOs.
- The score must be mass destroyed, so the card's genre reads Space. Check what `ModeGenre` answers for your metric.
- The <VESSEL> can also make mass (see the seed). Keep the score on destruction, so the game does not quietly
  become a Mass game.
- Prefer emergence over bespoke rules (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`). The Cell, the spawners and the
  prism destruction path already exist; reuse them. Watch the destruction budget (`Docs/claude/SHADERS_AND_PERFORMANCE.md`
  § Prism System Performance): a ring explosion or a windmilling ball at I4 can break thousands of prisms at once.
- Offer me the Vessel Studio (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) in one line in case I want to
  prototype the game there first (the Brittle Star already has a studio page). It is an option, not a step.

**Build (the `/arcadegame` recipe)**
- The `GameModes` id: read `Assets/_Scripts/Data/Enums/GameModes.cs` on bleeding-edge AND the in-flight branches
  (other sessions in this plan are claiming ids at the same time), take the next free one at commit, and run
  `python3 Tools/Build/check_switch_label_collisions.py`.
- The controller, scoring rule and turn monitor, the end-condition target in `EndConditionOverridesSO` (never a
  per-scene field), the four intensity cell configs, the `SO_ArcadeGame` card on `ArcadeGames`, all authored by a
  `Tools/Build/author_<mode>_assets.py` generator with a `--check` that diffs against disk.
- Every word the player reads is an authored field (card text, toasts, HUD), never a C# literal.
- Sounds: exposed, empty `EventReference` fields; say which ones are unwired.
- The AI: drive the hull's existing autopilot through the mode's targets (input only). An ability the AI cannot press
  is inert, so say what the AI can and cannot do. Name the AI plan step it serves (`Docs/AI_SYSTEM/AI_PLAN.md`).
  Froglet Compute is paused, so queue no compute job.
- The card background through `/cardart`.
- A `ModeGenreTests` row pinning the new mode to Space, and edit-mode tests for the scoring rule.

**Verify and land**
- Run every gate in `/arcadegame` §4, the generator's `--check`, `bash Tools/Build/unity_refcompile/run.sh` and
  `bash .claude/skills/ship/gates.sh`. If no Unity editor ran, say so and add an entry to
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- Show it: a still or a short clip of the arena at I2 (`Docs/claude/VIDEOS_AND_PREVIEWS.md`), and say what only the
  editor can judge.
- Write the mode doc (`Assets/_Scripts/Controller/Arcade/<MODE>.md`), update the <VESSEL> row in
  `Docs/MassAndSpaceGames/PLAN.md` §7, and add the mode to `Docs/claude/GAME_MODES_AND_CONTROLLERS.md`.
- Run `python3 Tools/Build/approvals.py check --me <me>` before committing. Queue an `/approve` entry for anyone whose
  work you changed. Commit with conventional commits and push to your branch. Suggest one Froglet Tasks ticket for the
  editor playtest (through `/team-tasks`, once I approve).

**Seed for this vessel** (from `PLAN.md` §4):

<paste here>
