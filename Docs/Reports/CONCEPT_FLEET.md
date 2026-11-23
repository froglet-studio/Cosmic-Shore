# Concept fleet and supervessels — PARKED (report for Yash, 2026-11-01)

**Status: parked on Yash's word (2026-11-01): "for now we won't be making any more new vessels."** Every prototype is
kept as it is and playable in the Vessel Studio (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, version 66). A
session brings one back only when Yash asks for it by name. **Branches:** the studio pages are on `Ys-bleeding-edge` and
`bleeding-edge`; the Flea's Unity build is on `claude/flea-vessel` (Garrett's, unmerged); the Gibbon's Tether half is on
`cece/loving-feynman-hlgf18` (unmerged). **The full catalogue:** `Docs/Studios/CONCEPT_FLEET.md` §13 (what each one is,
where it stopped, how to bring it back).

## 1. In one minute

- **What we made.** The concept ten (Gibbon, Flea, Albatross, Remora, Dragonfly, Mole, Marlin, Eel, Octopus, Gecko; Garrett
  kept the first five he named: Gibbon, Flea, Dragonfly, Marlin, Albatross), two supervessels (the Omega crews, then the
  Siphonophore), the calm three (Moon Jelly, Sea Turtle, Seahorse) and play-style choices for Garrett's Orca.
- **What is in Unity.** Only the Flea, on Garrett's unmerged branch (id 24), never opened in an editor. The Omega crews
  were built in Unity (ids 25-27) and removed on 2026-10-27. Nothing else is in the game.
- **What comes next instead** (`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md` §10): each existing vessel's Time minigame,
  made hard and challenging with AI rivals learned from people's races; then the arena games such as Regatta. Yash plays
  the Dolphin in Switchback himself; Claude sessions build the Stoat (Slingshot) and the Meerkat (Warpline) with him.
- **Ids.** 28 Meerkat and 29 Tiger Moth are taken on `bleeding-edge`; **30 is the next free id** (the ledger in
  `CONCEPT_FLEET.md` §0).

## 2. Each prototype, one line

| Prototype | Where it stopped | To bring it back |
|---|---|---|
| Gibbon (web-slinger) | Studio round 2; its AI finishes no course; Tether half unmerged | Yash decides the feel, then the AI, an id, Unity |
| Flea (ricochet) | **Unity** on `claude/flea-vessel` (24), never flown in the editor | Garrett flies it (T-20261024-bd25), then its racing minigame |
| Albatross (soarer) | Round 1 page, weak AI | A cell stage in the studio for thermals (T-20261017-ba54) |
| Dragonfly (hoverer) | Round 1 page | The control swap and the mine / joust kit (T-20261017-3cc7) |
| Marlin (drifter) | Round 1 page; needs a new name (the game keeps "Marlin" for a swordsman) | A pure drifter (T-20261017-1bc9) and Garrett's name call |
| Remora, Mole, Eel, Octopus, Gecko | Round 1 pages, not among Garrett's five | Only on request |
| Omega Crews | Superseded; removed from Unity; page kept as the record | Not planned (the Siphonophore replaced it) |
| Siphonophore | Studio round 2, its ability kit flown one ability at a time; no id | Yash picks the kit; then an id and Unity |
| Moon Jelly, Sea Turtle, Seahorse | Round 1 pages (the calm Level 1 three) | Only on request |
| Orca play styles (Garrett's Orca) | Lab round 9: Tank / Breath / Tide, Shield / Dock, Fly-by-wire / Seeker; none in Unity | Yash and Garrett pick one per ability |

## 3. How the sessions stay out of each other's way (CONCEPT_FLEET.md §0)

The rule still holds for the day a prototype comes back: the id ledger and the enum on every branch before claiming a
number; a section in `Docs/Studios/CONCEPT_FLEET.md` and this report refreshed with
`python3 Tools/Build/team_report.py area concept-fleet --write`; one page in the ONE Vessel Studio artifact; in Unity a
generator that owns the prefab and registrations, a doc beside the code and the hand test in the checklist; hand-offs
only as suggested tickets through `/team-tasks`, never assigned. Garrett's crew plumbing (the Orca) and the Stoat donor
are built on, never changed, without asking Garrett.

## 4. Open tasks and decisions (all waiting; none is being worked while parked)

- Garrett: fly the Flea branch (T-20261024-bd25); the Marlin's name; the open design questions (T-20261017-b999).
- Yash with Garrett: the Orca's play styles (one option per ability, or a choice for the player).
- Yash: the Siphonophore's kit, when he brings it back; Keep / Drop on the concept pages (T-20261017-b227).
- The open tickets in the generated block below stay as they are; none was filed or closed for the parking.

## 5. How to try it

- The studio: open the artifact (link above), pick a bay (the "New vessels · the concept fleet" row, the Siphonophore,
  Omega Crews, Orca or a calm bay), fly it on a gamepad, keys or a phone, and record on its Decisions tab.
- Unity, `claude/flea-vessel`: Menu_Main freestyle, Toy Box, Vessel Changer, Flea (`R_VesselActions/FLEA.md` §6).

## 6. Generated: who touched it, what merged, the open tickets

This block is refreshed by `python3 Tools/Build/team_report.py area concept-fleet --write`. Any Claude session asked for
a report on this area runs that, never answers from memory (`Docs/TEAM.md` rule 10).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-01 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area concept-fleet --days 14`._

### Recent changes (25+)
- 2026-10-29 `a8c7d6b07` Soliton Tunnels: jelly lens + spindle bridge with domain tint, slip-and-slide ride with small RT thrust, environmental fundamental (SolitonTunnelConfigSO), Trunk/Root/Labyrinth renamed to Soliton Tunnel/Network (GUIDs + FormerlySerializedAs kept), Froglet Tools Environmental Fundamentals window, lab and old shaders removed (restore from 2d1e8726c, see WORMHOLE_SHADERS.md section 15) — on Ys-bleeding-edge
- 2026-10-28 `742f4349e` docs(meerkat): round 25 write-up, §7a, the verification entry, clones.json — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `006fcea2c` docs(meerkat): Garrett's kit - MEERKAT.md section 8, FLEET_MAPS approved + shipped, protocol 23, hand tests — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `4f8269efe` feat(meerkat): slide the tunnel as you slide the roots (round 24, artifact + Unity) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `f252617ed` feat(meerkat): Garrett's surface area and the soliton's swallow, artifact round 23 cloned to Unity — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `d2d51211e` feat(meerkat): clone the tree and the Omnicrystal's roots from the Vessel Studio artifact, flown on the left stick — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `43d90d49f` feat(meerkat): round 7 - the umbrella-handle tunnel: LT sets the shaft, the stick the bend — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `7f7fcb37b` feat(vessel-studio): the calm three - Moon Jelly, Sea Turtle and Seahorse as Level 1 studios, compared with the Orca — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `b8d6ef57a` feat(meerkat): the wormhole vessel in Unity - Toybox, AI and its Time race Burrow Run — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `fe90fbf03` docs(concept-fleet): keep Canopy Run as the Gibbon's first-mode idea — on another branch
- 2026-10-27 `ac3ed9f2e` feat(siphonophore): Ram Spool - Helm RT winds the colony up; a red ram opens a wall; round 2 recorded — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `f24f26e59` feat(siphonophore): Pulse Jet - the Bell inhales on RT and jets on release, chaining on the beat — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `9bf4b061d` feat(siphonophore): Tentacle Lash and Trawl, stand-in rivals, and a wall detour for the AI — on Ys-bleeding-edge, bleeding-edge
- 2026-10-27 `1039d5ecd` feat(siphonophore): Slipstream - the Bell leads, the others draft in its wake; B calls the fuse — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `fd798c450` fix(vessel-studio): the Flea's hub preview no longer reads past its path on the first frame — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `74fa8cf81` feat(siphonophore): ability kit for the three zooids and the colony, borrowed from the fleet — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `46db6d58c` feat(siphonophore): the Voltron vessel designed from the ground up, in the Vessel Studio; the three Unity Omega crews removed — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `3871d4117` docs(omega): the design principle - a Voltron vessel is designed from the ground up, never assembled from autonomous vessels — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `63a08b12a` feat(omega): the crews march as one formation with mobility, attack and defence seats; the bodies take their colour — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `b036761b0` docs(concept-fleet): the vessel id ledger and the shared-work rule, the Omega crews and the Flea in the area report and the class list — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `6350dfc87` feat(omega): the Omega crews in Unity - Warden, Bulwark, Tempest (25-27): mega forms that combine into one omega form with three seats — on Ys-bleeding-edge, bleeding-edge
- 2026-10-26 `3a75bb456` feat(vessel-studio): the Omega Crews studio - combiner crew vessels (mega forms, one omega form, three seats) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-24 `eca1568b8` docs(concept-fleet): the Flea's Unity branch is up; Garrett's flight test; the Tether's free id is now 25 — on Ys-bleeding-edge, bleeding-edge
- 2026-10-24 `6332aab74` feat(flea): the Flea (vessel 24) flies in Unity - bounce off mass, RT timed to the contact, LT crouch and hop — on another branch
- 2026-10-24 `9a79de22d` docs(reports): the concept fleet's report and area; the Tether's free id is now 24 — on Ys-bleeding-edge, bleeding-edge

### Also in flight on other branches
- `cece/exciting-thompson-wdi3wm` (unattributed, 2026-10-27): docs(verification): checklist entry for the Time crystal sound fix
- `claude/flea-vessel` (unattributed, 2026-10-24): feat(flea): the Flea (vessel 24) flies in Unity - bounce off mass, RT timed to the contact, LT crouch and hop

### Open tickets (Froglet Tasks)
- T-20261024-bd25 [open] test: Fly the Unity Flea from branch claude/flea-vessel: Menu_Main freestyle, Toy Box > Vessel Changer > Flea; RT (Right Shift / LMB) timed to a prism contact is a perfect bounce, LT (Left Shift / RMB) crouches and hops (who: anyone)
<!-- team_report:end -->
