# The Angler: a strategist vessel (proposal)

**Status: in Unity (2026-10-15), unverified in the editor.** The designer kept the Angler and picked the
**Sling painter** in round 2 of the [Angler Command Lab](ANGLER_COMMAND_LAB.md). Following the Vessel Studio rule (the
artifact decides, Unity clones), the lab's numbers and its `CORE` arithmetic were cloned into a new vessel,
`VesselClassType.Angler = 17`. Its design and wiring are in `Assets/_Scripts/Controller/Vessel/R_VesselActions/ANGLER.md`,
its Editor test steps in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. The designer approved the element map on 2026-10-16 (Charge = Erase / Lock-on, Mass = Lure, Tame & Grow,
Space = Paint, Time = Dash to the School; `R_VesselActions/ANGLER.md` §7), so the proposal below is superseded where it
differs. Still not built: the Charge and Time level-5 upgrades, element scaling, the Angler's own hull, and its AI. The Angler gets its page in the one Vessel Studio
(`/vessel-studio` §2) when the team wants it flown there; until then the lab is its source of truth.

## The idea

Every other hull wins with its own trail and its own body. The Angler wins with **other creatures**. It
carries the anglerfish's lure (the *esca*), a light on a stalk ahead of its nose. It lures wild fauna, tames
the ones it holds in the light, and sends particular squads at particular prisms. It is the fleet's
strategist: slow, readable, and as strong as the ecosystem around it.

It only bends what the creatures already do. A commanded creature still eats by the game's own mouthful rule
(a 2 s hold, the target plus its neighbours, any domain outside the nucleus). The command only decides where
it goes. This keeps the Angler inside "favor emergent systems" (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`):
it scripts no kills and exempts nothing.

## Identity

| Field | Proposal | Note |
|---|---|---|
| `VesselClassType` | `Angler = 17` | the next free value (measured 2026-10-15; BrittleStar = 16) |
| Role | Strategist | the fleet's first ecosystem-driven hull |
| Speed | cruise 40 u/s, boost 110, and a crawl (12 u/s) while luring | lab proposals. Squirrel ships 60; 40 is what an L1 Clawfish (32 × 1.25) can follow |
| Controls | both sticks fly (the game's dual-stick mix); **LT** lure, **RT** command, **LB** pick a squad, **B** recall | every command rides one trigger, so both thumbs keep flying |

## The four abilities (one per element: a proposal for the four-icon row)

The vessel contract (`.claude/skills/vessel/references/CONTRACT.md` §3–§5) gives every hull one ability per
element, an elemental scaling on what that ability owns, and a level-5 upgrade. The mapping below is a
**proposal for approval**. The contract says an unapproved mapping must not be authored into
`ElementalAbilityMaps/Angler.asset`.

| Element | Ability (input) | What the element scales | Level-5 upgrade (proposal) |
|---|---|---|---|
| **Space** | **Lure** (LT, hold): the esca light pulls wild fauna within its reach and slows the hull to a crawl | lure reach (70 u in the lab) | the lure also holds creatures already on another player's leash (contested taming) |
| **Time** | **Tame**: a creature held in the light is tamed | time to tame (2.5 s in the lab) | a held creature tames instantly (a level-5 Time "snap") |
| **Charge** | **Command** (RT): the chosen gesture sends the picked squad at prisms | the aggression level commanded creatures run at (L0 / L1 / L2 = speed ×1 / 1.25 / 1.6, mouth reach ×1 / 1.4 / 1.8, the game's own `LightFauna` rows) | Frenzy: commanded creatures run at L2 |
| **Mass** | **Leash** (LB picks a squad, B recalls) | how many creatures you can lead (24 in the lab) | the leash tow: followers keep up at boost |

All four reuse the game's existing primitives (fauna aggression levels, the mouthful, team colour), as the
level-5 ground rules ask (`FLEET_MAPS` §2–§3). None of them needs a shield, a timer or decay.

## What the game needs before the Angler can exist

1. **A persistent goal for a creature.** No code lets anything command a creature. `Fauna.Goal` has a
   setter, but every subclass overwrites it on its next behaviour tick. A port needs a goal provider that a
   creature reads before its own `ResolveGoal` (in the spirit of `SetExternalTargetProvider` for vessel AI),
   plus a "lock" that `LightFauna`'s nearest-edible pick respects.
2. **A leash that is not a team.** `Fauna.SetTeam` and `Cell.SetModeControlOverride` exist, but the
   ecosystem's locked rule says a cell never holds two fauna colours. See the decision below.
3. **The AI.** An Angler AI is input-only like every other (`/vessel-ai`). The lab's commander AI
   (approach, act, break away, recall before each order) is the starting point.

## Decisions needed (designer)

The command gesture and its tuning are in [the lab's write-up](ANGLER_COMMAND_LAB.md#decision-needed-designer).
These belong to the vessel itself:

1. **The name and enum value.** Angler, 17.
2. **Tamed creatures and the one-colour rule** (`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`, locked):
   (a) tamed creatures keep the cell's colour and carry a leash mark (the lab's reading, no rule broken);
   (b) they turn your colour (breaks the locked rule, needs your say-so); or (c) taming only works in a cell
   your domain controls.
3. **The element mapping above**, and whether Space or Charge owns the command reach.
4. **Which modes seat it.** The Angler needs fauna to be anything at all, so it belongs in cells with a living
   ecology (Garland-profile cells carry Clawfish, QuadFish and Tadpoles). In a fauna-free arena it is a slow
   hull with a light.
