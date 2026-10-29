# Meerkat — where it stands (report)

**From:** Yash's sessions and Garrett's kit work, 2026-10-27 to 2026-10-28.

**Branch:** everything below is on `Ys-bleeding-edge`. bleeding-edge was merged in on 2026-10-28 (583b339948 and
6db26723c6, no conflicts). Nothing of the Meerkat is on `bleeding-edge` until Yash has played it in the editor.

**Status:** designed in the Vessel Studio artifact first, then cloned to Unity. The Unity code compiles (the reference
compile, player and editor), the arithmetic is held to the page's own numbers by edit-mode tests, and the studio map
reads 108 keys with 0 drift. **None of it has been played in the Unity editor yet.** That is the next step (section 5).

## 1. In one minute

- **The Meerkat is the wormhole vessel.**
  - One thumb flies it.
  - The right stick and LT shape a wormhole on your nose, and RT forms it.
  - Its centreline is one oval arc that bends round its middle, more oval as it gets longer.
- **Its width is Garrett's surface area.** The Space level sets one area, so a longer wormhole is thinner and a shorter
  one fatter. Yash's Unity tuning (bend radius 10, throat 20, mouth 40) is the shipped shape.
- **You slide every wormhole.** Inside, you cruise along on your own, RT pushes you faster, the left stick moves you round
  the tube and its wall bounces you. The omnicrystal's network of wormholes flies exactly the same way: one wormhole,
  or a network of them.
- **Inside a tube is another dimension.** Nothing outside touches you, you collect nothing, and you are solid again when
  you come out.
- **It swallows the world round its way in.**
  - Prisms and crystals within 90 u of the mouth are pulled in.
  - They are carried through stretched and twisted, at the slide's speed, first in first out, so nothing catches you
    inside.
  - They are fired out of the far mouth as moving prisms.
- **The network** opens when you take any omni crystal, the game's own included. Mass sizes it, from half the cell at
  Mass 0 to the whole cell at Mass 10.
- **Garrett's four abilities are in:** Space the Soliton (RT), Time Faster Ride (passive), Charge Sentinel Mode (Y) and
  Mass Exit Portal (passive), each with its level-5 upgrade.

## 2. Try it

- **In the artifact:** https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, the Meerkat card. The Kit dropdown (Play Style
  Config) picks Garrett's kit (the default), the Mix, Round 8 or Off.
- **In Unity** (pull `Ys-bleeding-edge`, open the project, wait for the compile):
  1. Toybox (Lava Lamp), Vessel Changer: the Meerkat. Release LT and pull RT for a short, fat wormhole. Inside, push the
     left stick (you bounce off the wall), hold RT (faster), and fly out of the far mouth.
  2. Form a wormhole next to prisms: they are pulled in, stretched through and fired out. None catches you inside, and
     you lose no petal in the tube. Form one through a wall of prisms: you pass through and the wall stands.
  3. Fly into an omni crystal: the network opens round you. Watch an AI Meerkat take one: its network opens too.
  4. Burrow Run (the Meerkat's Time mini-game): the AI forms wormholes, slides them and threads its rings.
  5. Y: Sentinel Mode. A sentinel stands at each new wormhole's way in.

The full hand tests are the Meerkat entries in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 3. What changed in the game (Unity)

The design is in `Docs/Studios/MEERKAT.md` (the rounds, newest first). The code map is
`Assets/_Scripts/Controller/Vessel/R_VesselActions/MEERKAT.md`.

| Round | What | Where |
|---|---|---|
| 1-12 | The vessel, the reticle, the ride, the look, the AI, Burrow Run | §1-§6 |
| 13-21 | The tree and the roots (now shipped off) | §7 |
| 22 | The oval arc | §1, `SolitonTunnelShapeMath.Conic` |
| 23 | Garrett's surface area; the swallow | §8, `MeerkatSwallow` |
| 24 | The slide: one flight for a wormhole and the network | §1, `SolitonTunnelMath.Step` / `ITubePath` |
| 25 | Phased inside a tube; first in first out; the game's omni crystal grows the network | §9, `MeerkatVesselTransformer.Phased` |
| 26 | The network of wormholes, sized by Mass; no tree, no roots | §7a |
| Kit | Garrett's four abilities | §10, `MeerkatSolitonKit` |

**Shared code touched (the trail):**
- `OmniCrystalImpactor.CollectedByVessel`: an additive event in Garrett's area.
- `SkimmerImpactor`: the prism skim honours a hull's `IHullContactGate`, a no-op for hulls without one.
- Protocol version 23.

## 4. Measured

- **The artifact's headless races, seed 11, Hard, Warpline I1-I4, Garrett's kit:**
  - round 23 (the automatic ride): 67.9 / 66.9 / 87.8 / 68.5 s;
  - round 25 (the slide, phased): 79.6 / 77.3 / 90.7 / 78.1 s;
  - round 26 (the network, Yash's tuning): 77.6 / 78.1 / 99.1 / 77.5 s;
  - without wormholes: about 110 / 111 / 120 / 110 s.

  Every kit finishes every race, and mass is conserved: a swallowed prism is moved, never deleted.
- **Unity, without the editor:**
  - the edit-mode maths tests pass in a dotnet harness, 59/59 for the wormhole and the roots (the kit has its own,
    `Tools/Build/meerkat_kit_harness/run.sh`);
  - the reference compile is clean, player and editor;
  - `studio_to_unity.py --check`: 108 keys, 0 drift;
  - `author_meerkat_assets.py --check` passes.

## 5. Open items, and what is asked of you

1. **Play it in the editor** (section 2). Nothing of the Meerkat has been played there yet. When it passes, merge
   `Ys-bleeding-edge` to `bleeding-edge`.
2. **Speed:** the slide is slower than round 23's automatic ride (about 10 s a race at Hard). The lever is
   `rootMaxSpeed`, which a wormhole and the network share. The network costs time on Warpline I3 (Hard 99 s) and I4
   (Medium 103 s).
3. **The Mix's Space L5 Burial** no longer lands, because a rider is phased. Keep it, change it or drop it: Yash's call
   with Garrett, whose design it extends.
4. **The hull and its icons:** the Meerkat still wears the Stoat's model and placeholder icons (`/vessel-model`,
   `/ability-icon`).
5. **Touch:** no per-hull phone profile yet.

## 6. Latest activity (generated)

This block is refreshed by `python3 Tools/Build/team_report.py area meerkat --write`. Any Claude session asked for
this report runs it first (`Docs/TEAM.md` rule 10).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-28 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area meerkat --days 14`._

### Recent changes (25+)
- 2026-10-28 `742f4349e` docs(meerkat): round 25 write-up, §7a, the verification entry, clones.json — on Ys-bleeding-edge
- 2026-10-28 `3c3fa3fbe` docs(meerkat): Garrett's kit references point at MEERKAT.md section 10 — on Ys-bleeding-edge
- 2026-10-28 `1835aca94` docs(meerkat): round 25 - phased inside a tube, first in first out, measured — on Ys-bleeding-edge
- 2026-10-28 `12e8363f2` feat(meerkat): inside a tube is another dimension; first in first out; the game's omni crystal grows the roots — on Ys-bleeding-edge
- 2026-10-28 `ce548fc09` Update MeerkatSolitonConfig.asset — on Ys-bleeding-edge
- 2026-10-28 `d4afbae57` docs(meerkat): round 24, the slide - what changed, the two bugs, every kit's races — on Ys-bleeding-edge
- 2026-10-28 `4c09676c3` fix(meerkat): the AI's Chain Dig plans before it lets go of RT (no accidental straight chains while sliding) — on Ys-bleeding-edge
- 2026-10-28 `b3b81d05c` wip(meerkat-studio): the network sized by Mass (half the cell at Mass 0, the whole cell at 10); two words, wormhole and network — on Ys-bleeding-edge
- 2026-10-28 `006fcea2c` docs(meerkat): Garrett's kit - MEERKAT.md section 8, FLEET_MAPS approved + shipped, protocol 23, hand tests — on Ys-bleeding-edge
- 2026-10-28 `4f8269efe` feat(meerkat): slide the tunnel as you slide the roots (round 24, artifact + Unity) — on Ys-bleeding-edge
- 2026-10-28 `b61e575a2` wip(meerkat): round 23 clone - the network's channels draw as wormhole tubes in Unity; tree and bark off — on Ys-bleeding-edge
- 2026-10-28 `507062b54` feat(meerkat): wire Garrett's kit onto the Meerkat - prefab, Y binding, config, element map 4/4 — on Ys-bleeding-edge
- 2026-10-28 `659a40a1d` feat(meerkat): Garrett's kit in code - soliton swallow and fire, sentinels, exit portal, the faster ride — on Ys-bleeding-edge
- 2026-10-28 `125406f3f` docs(meerkat, prism-motion): lock the Meerkat's element definition and the shielded-prism bounce for Yash to build on — on Ys-bleeding-edge
- 2026-10-28 `e2beadfee` wip(meerkat-studio): round 23 - no tree, no roots: the omnicrystal's wormhole network draws as a formed tunnel (artifact) — on Ys-bleeding-edge
- 2026-10-28 `379809614` feat(meerkat): the kit's pure arithmetic, ported from the artifact's Garrett's kit, with page-golden tests — on Ys-bleeding-edge
- 2026-10-28 `97c267d81` feat(studios): omnicrystal network, three bifurcations from the entrance to every exit — on Ys-bleeding-edge
- 2026-10-28 `f252617ed` feat(meerkat): Garrett's surface area and the soliton's swallow, artifact round 23 cloned to Unity — on Ys-bleeding-edge
- 2026-10-28 `6a78dfa7a` feat(meerkat): the tunnel bends round its middle - one oval arc, more oval with length (artifact + Unity) — on Ys-bleeding-edge
- 2026-10-28 `9edc9abbe` feat(studios): omnicrystal network keeps every path within 20% of the straight line — on Ys-bleeding-edge
- 2026-10-28 `d2d51211e` feat(meerkat): clone the tree and the Omnicrystal's roots from the Vessel Studio artifact, flown on the left stick — on Ys-bleeding-edge
- 2026-10-28 `abeff14da` feat(studios): Omnicrystal Network Lab, the omnicrystal's tunnel network to every exit — on Ys-bleeding-edge
- 2026-10-28 `7e36c049d` fix(meerkat-studio): no black fill inside the roots; a soft inner tube, rough outer bark, a calm ride — on Ys-bleeding-edge
- 2026-10-28 `6d7ff2993` feat(meerkat-studio): round 20 - the trunk is the wormhole, and you fly the roots yourself (artifact only) — on Ys-bleeding-edge
- 2026-10-28 `480b0dba4` feat(vessel-studio): space sound set and a Sound on/off switch on the stage — on Ys-bleeding-edge

### Also in flight on other branches
- None.

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->
