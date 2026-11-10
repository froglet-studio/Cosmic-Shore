# Prompt: a Mass game for a vessel

Paste everything below the line into a new session. Replace `<VESSEL>` with the hull's name and paste its seed from
`Docs/MassAndSpaceGames/PLAN.md` §4 at the end.

---

Build the **Mass game** for the **<VESSEL>**: a new hull-locked arcade mode that is the vessel's Mass card, the way
Scurry is the Squirrel's and Scarab Scramble is the Scarab's. The plan this belongs to is
`Docs/MassAndSpaceGames/PLAN.md`. Read it first, especially §1 (what a Mass game is) and §6 (the rules).

**What a Mass game is.** In this game a Mass game is about making or taking mass: the score is mass you put on the
board, or took from a rival, and still hold (`ModeGenre.cs`: `PrismsRemaining`, `PrismsStolen`, `VolumeRemaining`, or
an explicit mode row such as Scurry's and Scarab Scramble's). The shipped Mass games score through the hull's own
mass-making, usually through its **omni crystal**: the Scarab's omni crystal forges a ball, which is "a crystal made
into mass and driven home" (Garrett, 2026-10-27), and scoring in your own net gives a mass burst. Find the
<VESSEL>'s equivalent: its Mass ability and its omni crystal (the seed below) are what the game is built around.

**Before designing anything**
1. Run `/swimlanes` and `git fetch origin`, then
   `python3 Tools/Build/who_works_where.py query Assets/_Scripts/Controller/Vessel/R_VesselActions Assets/_SO_Assets/Games`.
   If the <VESSEL> lane is occupied, stop and tell me who holds it.
2. Load `/arcadegame` and follow it from §0. Load `/vessel` for the hull. If the game touches creatures or plants,
   load `/ecology` (and `/fauna` or `/flora`) first. If the <VESSEL> is a crew vessel, load `/duo-vessel`.
3. Read the vessel's own doc (`Assets/_Scripts/Controller/Vessel/R_VesselActions/<HULL>*.md` and its
   `Docs/Studios/*.md` if there is one). Confirm the Mass ability and omni crystal in the seed are still what ships.
4. Read the two nearest sibling modes' docs (`Assets/_Scripts/Controller/Arcade/*.md`): Scurry and Scarab Scramble,
   plus whichever of Tollway, Hijack or Tapestry is closest in shape.

**Design (write the one-page brief first, `Docs/claude/DESIGN_BRIEF.md`)**
- One sentence a player understands: what you make, how it scores, how a rival takes it from you.
- **The vessel kit stays exactly as it is (LOCKED, Shombith 2026-10-31).** The game is interesting through its track,
  environment (prisms, crystals, flora, hazards), intensity ladder I1-I4 and AI only. No rationed abilities, no press
  vetoes, no added contact effects, no edits to vessel code, prefabs or action SOs. If the kit cannot make mass worth
  scoring, stop and tell me: that goes to `PLAN.md` §5 as a decision, not into the mode.
- The score must be mass the player holds or took, so the card's genre reads Mass. Check what `ModeGenre`
  answers for your metric; add an explicit row only when the metric implies another element (as Scurry's does).
- Prefer emergence over bespoke rules (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`). Reuse the Cell, the spawners
  and the scoring platform; build nothing they already own.
- Offer me the Vessel Studio (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) in one line in case I want to
  prototype the game there first. It is an option, not a step.

**Build (the `/arcadegame` recipe)**
- The `GameModes` id: read `Assets/_Scripts/Data/Enums/GameModes.cs` on bleeding-edge AND the in-flight branches
  (other sessions in this plan are claiming ids at the same time), take the next free one at commit, and run
  `python3 Tools/Build/check_switch_label_collisions.py`.
- The controller, scoring rule and turn monitor, the end-condition target in `EndConditionOverridesSO` (never a
  per-scene field), the four intensity cell configs, the `SO_ArcadeGame` card on `ArcadeGames`, all authored by a
  `Tools/Build/author_<mode>_assets.py` generator with a `--check` that diffs against disk.
- Every word the player reads is an authored field (card text, toasts, HUD), never a C# literal.
- Sounds: exposed, empty `EventReference` fields; say which ones are unwired.
- The AI: drive the hull's existing autopilot through the mode's targets (input only). Name the AI plan step it serves
  (`Docs/AI_SYSTEM/AI_PLAN.md`). Froglet Compute is paused, so queue no compute job.
- The card background through `/cardart`.
- A `ModeGenreTests` row pinning the new mode to Mass, and edit-mode tests for the scoring rule.

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
