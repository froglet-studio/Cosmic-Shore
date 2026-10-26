# Concept fleet and supervessels — where it stands (report for Yash, 2026-10-26, evening)

**Branches:** the studio pages and the Omega crews are on `Ys-bleeding-edge`; the Flea is on `claude/flea-vessel`
(Garrett's); nothing of it is on `bleeding-edge`. **Artifact:** the Vessel Studio, version 40
(https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa). **Design record and id ledger:** `Docs/Studios/CONCEPT_FLEET.md`
(§0 is the rule every vessel session follows so sessions build on each other instead of colliding). **Status:** two
things are in Unity and neither has been opened in an editor yet.

## 1. In one minute

- **Ten concept vessels** (2026-10-17): Garrett kept the Gibbon (web-slinger), Flea, Dragonfly, Marlin and Albatross.
  Only the Gibbon has had its round-2 session; the other four are round-1 pages.
- **The Flea is in Unity** on `claude/flea-vessel` (vessel 24, 2026-10-24): flyable from the Toybox's Vessel Changer,
  the page's maths ported with golden tests, the Stoat's body as a stand-in. Garrett has not flown it yet.
- **The Omega crews are in Unity** on `Ys-bleeding-edge` (vessels 25-27, 2026-10-26; Yash: "make this prototype in
  Ys-bleeding-edge, I wanna test there"), rebuilt the same day after his first test as a MARCH FORMATION with three
  roles: Warden = Stoat (mobility) / Sparrow (attack) / Rhino (defence), Bulwark = Scarab / Grizzly / Butterfly,
  Tempest = Squirrel / Thresher / Manta. The three fly as one rigid flock; the D-pad moves you (and the camera) between
  them while the AI holds the other two; the attack seat aims and fires, the defence seat shields and fires the ward.
  The lab's combine (mega / omega forms) stays on the page, not in the game yet. `Crew/OMEGA_CREW.md`,
  `Docs/Studios/CONCEPT_FLEET.md` §10.3, the hand test in the checklist.
- **The vessel id ledger** (CONCEPT_FLEET.md §0): 24 Flea, 25-27 the Omega crews, **28 is the next free id**. The
  Tether / Gibbon, Dragonfly, Marlin and Albatross have none yet; the name "Marlin" is kept in the
  game for a swordsman (the Jacks were "Marlin and Jacks"), so the drifter's name still needs Garrett's call.

## 2. Each vessel, one line

| Vessel | Where it is | Next step |
|---|---|---|
| Gibbon | Web-slinger, round 2 done in the studio (§8.3); AI moves but finishes no course | Yash flies it and decides the feel; the Tether prototype (`cece/loving-feynman-hlgf18`) is still unmerged (T-20261017-e64b) |
| Flea | **Unity, `claude/flea-vessel`** (24): Vessel Changer, FleaMath goldens, Stoat body | Garrett flies the branch (T-20261024-bd25), then its small racing game (T-20261017-929b) |
| Warden / Bulwark / Tempest | **Unity, `Ys-bleeding-edge`** (25-27, round 2: the march formation with mobility / attack / defence seats): Vessel Changer, OmegaMath goldens, Stoat body + placeholder member bodies | Yash flies them (the hand test; a ticket for it is filed by `/team-tasks add` on his yes: the session could not reach the Froglet Tasks page's store); Keep / Drop per crew on the studio's Decisions tab; then round 3: member bodies (`/vessel-model`), the combine in the game, multiplayer swapping, the Omega Run mode (`/arcadegame`) |
| Dragonfly | Round 1 page | Control swap, stored boosts, the mine / joust kit (T-20261017-3cc7) |
| Marlin | Round 1 page (drifter) | Pure drifter, never grips (T-20261017-1bc9); its name needs a decision |
| Albatross | Round 1 page (glider, AI weak) | Nucleus-frame up/down, thermals, membrane cost (T-20261017-ba54); needs a cell stage in the studio |

## 3. How the sessions stay out of each other's way (CONCEPT_FLEET.md §0)

Every vessel session, at its start and its end: (1) the id ledger and the enum on every branch before claiming a
number, claimed in one commit with the ledger row; (2) a section in `Docs/Studios/CONCEPT_FLEET.md`, and this report
refreshed with `python3 Tools/Build/team_report.py area concept-fleet --write`; (3) one page in the ONE Vessel Studio
artifact, decisions on its Decisions tab; (4) in Unity, a generator that owns the prefab and the registrations, a doc
beside the code, the hand test in the checklist, `clones.json`, and `Affects:` in the commit naming whose area it
touches; (5) hand-offs only as suggested tickets through `/team-tasks`, one per branch, never assigned. The Omega crews
are built on Garrett's crew plumbing (the seat ring, the Orca's trigger actions and lifecycle) and the Stoat donor
without changing them; a session that changes those asks Garrett first.

## 4. Open tasks and decisions

- Yash: fly the Omega crews (no ticket yet: say yes and a session files it); playtest the concept pages and record Keep / Drop
  (T-20261017-b227); the round-2 sessions (T-20261017-3c5d); review Joseph Zhou's pull request (T-20261017-b900).
- Garrett: fly the Flea (T-20261024-bd25); the open design questions (T-20261017-b999); the Marlin name.
- Designer decision from the Omega lab: the Warden's omega has no reason to split (always combined beats its crew);
  either staying combined costs more, or its members get a reason to be apart (CONCEPT_FLEET.md §10.1).
- Any session: the AI ladders once the five settle (T-20261017-3244); a crew mistake for the Omega levels (Hard is not
  shown fastest yet).

## 5. How to try it

- The studio: open the artifact (link above), the hub's "New vessels · the concept fleet" row or the Omega Crews bay,
  pick a vessel, fly it on a gamepad, keys or a phone, record Keep / Drop on its Decisions tab.
- Unity, `Ys-bleeding-edge`: Menu_Main freestyle, Toy Box, Vessel Changer, Warden; the steps are the Omega crews section
  at the top of `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- Unity, `claude/flea-vessel`: the Flea from the same Vessel Changer (`R_VesselActions/FLEA.md` §6).

## 6. Generated: who touched it, what merged, the open tickets

This block is refreshed by `python3 Tools/Build/team_report.py area concept-fleet --write`. Any Claude session asked for
a report on this area runs that, never answers from memory (`Docs/TEAM.md` rule 10).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-26 from git (last 30 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area concept-fleet --days 30`._

### Recent changes (25+)
- 2026-10-26 `91327c5c6` fix(omega): the crews' class and element-map assets parse as YAML again — on Ys-bleeding-edge
- 2026-10-26 `6350dfc87` feat(omega): the Omega crews in Unity - Warden, Bulwark, Tempest (25-27): mega forms that combine into one omega form with three seats — on Ys-bleeding-edge
- 2026-10-26 `3a75bb456` feat(vessel-studio): the Omega Crews studio - combiner crew vessels (mega forms, one omega form, three seats) — on Ys-bleeding-edge
- 2026-10-24 `eca1568b8` docs(concept-fleet): the Flea's Unity branch is up; Garrett's flight test; the Tether's free id is now 25 — on Ys-bleeding-edge, bleeding-edge
- 2026-10-24 `6332aab74` feat(flea): the Flea (vessel 24) flies in Unity - bounce off mass, RT timed to the contact, LT crouch and hop — on another branch
- 2026-10-24 `9a79de22d` docs(reports): the concept fleet's report and area; the Tether's free id is now 24 — on Ys-bleeding-edge, bleeding-edge
- 2026-10-22 `ebbb963d9` feat(goby): the Goby & Pistol's own kit - dart and perch, dens, undertow, homing rings — on Ys-bleeding-edge, bleeding-edge
- 2026-10-21 `562d5f325` feat(goby): the Pistol's marks go into the world (lab v14) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-21 `f4bc034ba` feat(goby): lab round 13 - the Pistol as a 2.5D character on a tangent plane — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `751959531` feat(studio): Goby lab round 12 - slingshot, a tiny Pistol on a wide view, downhill toward the Goby, the Settled unroll, the real hull — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `ea77f28d5` feat(goby): the vortex ring and flares are lit faceted volumes; ship-pass fixes — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `e570a66ca` chore(goby): the clone record names prototype v10 — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `d4d9259de` feat(goby): the Pistol's view drawn as the game draws it — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `b2bd220bb` docs(tether): the next free vessel id is 18, not 17 (the Angler merged at 17) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `6e8487298` feat(vessel-studio): the Gibbon is a web-slinger - no engine, elastic webs from both hands — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `84c9ba332` feat(vessel-studio): the Gibbon's two modes - time mode on the Tether's auto-tethers, space mode on two tethers — on Ys-bleeding-edge, bleeding-edge
- 2026-10-18 `b3fafd81f` feat(angler): A lures and B dashes (the Sparrow's buttons); fix "the QuadFish don't always listen" — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `671497959` feat(angler): clone round 6 - the lit queue: the school eats every lit prism, newest first — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `71dab4feb` docs(concept-fleet): Garrett's review decides the ten; round 2 order is Given, Flea, Dragonfly, Marlin, Albatross — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `c1f2fd660` feat(angler): round 5 - speed back to 40, a smaller accurate brush, a level camera that rises while luring or dashing — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `422d52e66` docs(concept-fleet): round 2 is one vessel per session; the Gibbon merges the Tether prototype — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `989e690e4` fix(vessel-studio): the Albatross flies as a glider; the concept fleet's four-course scorecard — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `268320eb3` feat(vessel-studio): the concept fleet - ten new movement vessels as recipes on one engine (D40) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-17 `7496683d8` feat(angler): round 4 - one-thumb, 2x speed, camera 4x, the lure stops, crystal seeds, lure growth, Charge spends a creature — on Ys-bleeding-edge, bleeding-edge
- 2026-10-16 `2bcba3eb3` feat(angler): the element map - Space paint 3200 u, Charge lock-on, Mass lure & grow, Time dash; the omni creature — on Ys-bleeding-edge, bleeding-edge

### Also in flight on other branches
- `claude/flea-vessel` (unattributed, 2026-10-24): feat(flea): the Flea (vessel 24) flies in Unity - bounce off mass, RT timed to the contact, LT crouch and hop

### Open tickets (Froglet Tasks)
- T-20261024-bd25 [open] test: Fly the Unity Flea from branch claude/flea-vessel: Menu_Main freestyle, Toy Box > Vessel Changer > Flea; RT (Right Shift / LMB) timed to a prism contact is a perfect bounce, LT (Left Shift / RMB) crouches and hops (who: anyone)
- T-20261017-1bc9 [open] dev: Pure drifter: never gets grip, less drift assist, the kit built around drifting; tune for fun (who: anyone)
- T-20261017-3cc7 [open] dev: Control swap (LT held: look stick becomes roll / elevation, no A/B), long-cooldown stored boosts along the current motion, and a precision kit: grasping joust that steals an element, and geometry mines (triangle / square / kite / pentagon / rhombus) (who: anyone)
- T-20261017-ba54 [open] dev: Up/down from the nearest point on the nucleus (cytoplasm and nucleus), small strong thermals with an explosion, a membrane-crossing consequence that grows faster than linear with time avoided; qualitative before numbers (who: anyone)
- T-20261017-929b [open] dev: Give Garrett a branch with the Flea; if good, bring it into the game as a playable vessel, then a small racing game, then push to bleeding-edge (who: anyone)
- T-20261017-a640 [open] dev: Gibbon (+ the Tether; the notes' "Given" is the Gibbon): two modes from cece/loving-feynman-hlgf18: time mode = the branch's auto-tethers, space mode = two sticks + two triggers (one pair per tether), slicing by what the tethers cut, movement by 0 / 1 / 2 tethers attached; the time <-> space transition first (who: anyone)
- T-20261017-e64b [open] dev: Bring cece/loving-feynman-hlgf18 (the Tether prototype) onto Ys-bleeding-edge: Tether = 14 collides with Thresher = 14; take the next free id (re-check before claiming: 17 went to the Angler and the Kite's branch claims 19, so 18 as of 2026-10-18; 18 then went to the Goby and 19-23 are taken: 24 as of 2026-10-24), rewrite TETHER.md section 12, run check_network_prefab_hashes.py and the edit-mode suite (who: anyone)
- T-20261017-3c5d [open] dev: Round 2 of the concept fleet: one vessel per session in the owner's order (CONCEPT_FLEET.md section 8); the Gibbon's session merges the Tether prototype (section 8.1) (who: anyone)
- T-20261017-b999 [open] decision: Decide the concept fleet's three open design questions before any port: the Albatross's own gravity (a vessel that falls in a HyperSea without gravity), the Remora's compounding slipstream (two drafters ratchet each other to the cap) and whether a tow may take petals, and the Dragonfly / Marlin second input strategy (left stick = translation, RT = throttle). (who: anyone)
- T-20261017-3244 [open] dev: Give the picked concept vessels a clean AI difficulty ladder (Hard fastest on every course, D24): the shared belief mistakes barely cost these fast movers and the lab-only press delay punishes tempo mechanics (the Octopus's Easy DNFs; the Albatross's Easy loses the updraft). One AI round per kept vessel, scorecard before and after. (who: anyone)
- T-20261017-b227 [open] test: Playtest the ten concept vessels in the Vessel Studio artifact (hub row 'New vessels - the concept fleet'): fly each on phone and PC, watch its AI, and Record Keep / Drop with a line of why on each page's Decisions tab; then pick five, then three (Docs/Studios/CONCEPT_FLEET.md section 1). (who: anyone)
<!-- team_report:end -->
