# Meerkat — where it stands (report)

**From:** Yash's sessions and Garrett's kit work, 2026-10-27 to 2026-11-03.

**Branch:** everything below is on `Ys-bleeding-edge`, except the 2026-11-03 work (its own hull, HUD, icons, class cards,
phone controls and round 27), which is on the local branch `meerkat/ready` until Yash merges it. Nothing of the Meerkat
is on `bleeding-edge` until Yash has played it in the editor.

**Status:**
- Designed in the Vessel Studio artifact (rounds 1-26), cloned to Unity, then changed further in Unity by Yash
  (2026-10-29 to 11-01): Soliton Tunnels, the slip-and-slide ride and the plain-wormhole look.
- **Round 27 (2026-11-03) brought those changes back into the artifact**, so the artifact and the game agree again
  (`studio_to_unity.py --check`: 80 mapped, 0 Meerkat drift).
- **2026-11-03, "ready with all its vessel skills" (Yash):** its own hull (`Meerkat.fbx` on the Stoat's rig), its own HUD
  row and five icons (the four abilities and the omni card), its own class cards, and a phone profile (section 5).
- The Unity code compiled clean at the last check (the reference compile, player and editor, 2026-10-28; the 2026-11-03
  HUD files compiled with Unity's Roslyn against the project's built assemblies, not in the editor).
- **None of it has been played in the Unity editor yet.** That is the next step (section 5).

## 1. In one minute

- **The Meerkat is the wormhole vessel.**
  - One thumb flies it.
  - The right stick and LT shape a wormhole on your nose, and RT forms it.
  - Its centreline is one oval arc that bends round its middle.
- **Its width is Garrett's surface area.** The Space level sets one area, so a longer wormhole is thinner and a shorter
  one fatter.
- **It is a Soliton Tunnel** (2026-10-29). The wormhole is an environmental fundamental any vessel or scene can use
  (`Controller/Environment/SolitonTunnel/`). The omnicrystal grows a **Soliton Network** of them, sized by Mass from half
  the cell to the whole cell.
- **You slip and slide through it.** Gravity presses you to the inner wall and you slide along it, bouncing now and then.
  There is no left stick; RT gives a small push, and you leave at the speed you carry, a little boosted (the slingshot).
- **It looks like a plain wormhole** (2026-11-01): bent space, a mouth that lenses the world into an Einstein ring, and a
  stretch inside (`Docs/Shaders/WORMHOLE_SHADERS.md` §16).
- **Inside a tube is another dimension.** Nothing outside touches you, you collect nothing, and you are solid again when
  you come out.
- **It swallows the world round its way in.**
  - Prisms and crystals near the mouth are pulled in.
  - They are carried through stretched and twisted, first in first out, so nothing catches you inside.
  - They are fired out of the far mouth as moving prisms.
- **Garrett's four abilities are in:**
  - Space: the Soliton (RT);
  - Time: Faster Ride (passive);
  - Charge: Sentinel Mode (Y);
  - Mass: Exit Portal (passive).

  Each has its level-5 upgrade. Any omni crystal it takes grows its Soliton Network.

## 2. Try it

- **In Unity** (pull `Ys-bleeding-edge`, open the project, wait for the compile):
  1. Toybox (Lava Lamp), Vessel Changer: the Meerkat. RT a short, fat wormhole. Inside, you drop onto the wall and slide.
     Hold RT for a small push. Fly out of the far mouth a little faster than you went in.
  2. Look at a wormhole from outside: the world round its mouth bends into a ring. Inside, the view streams toward the
     way out.
  3. Form a wormhole next to prisms: they are pulled in, stretched through and fired out. None catches you inside, and
     you lose no petal in the tube. Form one through a wall of prisms: you pass through and the wall stands.
  4. Fly into an omni crystal: a Soliton Network opens round you. Watch an AI Meerkat take one: its network opens too.
  5. Burrow Run (the Meerkat's Time mini-game): the AI forms wormholes, slides them and threads its rings.
  6. Y: Sentinel Mode. A sentinel stands at each new wormhole's way in.
- **In the artifact:** https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa, the Meerkat card. It is round 27: the game's
  slip-and-slide, the plain wormhole and Yash's 14 Unity numbers, as above (`Docs/Studios/MEERKAT.md` round 27).

The full hand tests are the Meerkat entries in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Two of them are marked SUPERSEDED
(the left-stick slide and the tree); skip those.

## 3. What changed in the game (Unity)

The design is in `Docs/Studios/MEERKAT.md` (its "Where it stands now", then the rounds, newest first). The code map is
`Assets/_Scripts/Controller/Vessel/R_VesselActions/MEERKAT.md`. The looks, and how to restore the deleted ones, are in
`Docs/Shaders/WORMHOLE_SHADERS.md`.

| When | What | Where |
|---|---|---|
| Rounds 1-12 | The vessel, the reticle, the ride, the look, the AI, Burrow Run | §1-§6 |
| Rounds 13-21 | The tree and the roots (off in round 26, files deleted 2026-10-29) | §7 (history) |
| Round 22 | The oval arc | `SolitonTunnelShapeMath.Conic` |
| Round 23 | Garrett's surface area; the swallow | §8, `MeerkatSwallow` |
| Round 24 | One flight for a wormhole and the network (a tunnel is a network of one) | `SolitonTunnelMath.Single` / `ITubePath` |
| Round 25 | Phased inside a tube; first in first out; the game's omni crystal grows the network | §9, `MeerkatVesselTransformer.Phased` |
| Round 26 | The network of wormholes, sized by Mass | §7a |
| Kit | Garrett's four abilities | §10, `MeerkatSolitonKit` |
| 2026-10-29 | One calm look; the old shaders deleted | `WORMHOLE_SHADERS.md` §12-§14 |
| 2026-10-29 | Soliton Tunnels, the slip-and-slide, the Environmental Fundamentals window | `WORMHOLE_SHADERS.md` §15, `SolitonTunnelRider` |
| 2026-10-29 | The Soliton Network omni ability; the Meerkat's own impactor container | `MeerkatImpactorDataContainer` |
| 2026-11-01 | The plain wormhole: lensing mouths, Einstein ring, wider mouths | `WORMHOLE_SHADERS.md` §16 |
| 2026-11-03 | Its own hull on the Stoat's rig (StoatAnimation still drives it), its class cards | `Docs/VesselModels/Meerkat.md`, `vessel_model/specs/meerkat.py`, `render_cards.py` |
| 2026-11-03 | Its own HUD row (MeerkatHUDView, MeerkatHUDVariant), four ability icons and the omni card | MEERKAT.md §5, `icon_kit/specs/meerkat.py` |
| 2026-11-03 | A phone profile (ThumbLifts, prototype) | MEERKAT.md §5, `author_touch_profiles.py` |

**Shared code touched (the trail):**
- `OmniCrystalImpactor.CollectedByVessel`: an additive event in Garrett's area.
- `SkimmerImpactor`: the prism skim honours a hull's `IHullContactGate`, a no-op for hulls without one.

## 4. Measured

- **The artifact's headless races** (seed 11, Hard, Warpline I1-I4, Garrett's kit):
  - round 23 (the automatic ride): 67.9 / 66.9 / 87.8 / 68.5 s;
  - round 25 (the left-stick slide, phased): 79.6 / 77.3 / 90.7 / 78.1 s;
  - round 26 (the network): 77.6 / 78.1 / 99.1 / 77.5 s;
  - without wormholes: about 110 / 111 / 120 / 110 s;
  - round 27 (the game's slip-and-slide and numbers, 2026-11-03): 93.9 / 97.2 / 106.8 / 76.6 s, and 139.9 / 132.7 /
    174.6 / 166.0 s without wormholes.

  In round 27 the network costs 10-20 s a race on I1-I3. Nothing steers inside it, so the AI comes out wherever it slid
  and flies back. In a wormhole, holding RT, the AI slides 113-116 u/s on average against the 142.8 u/s it plans with.
- **Round 28, the Rift** (artifact only; the same method: a fresh page, Warpline course seed 7, `runBatch([11])`):

  | Warpline I1 / I2 / I3 / I4 (s) | Hard | Medium | Easy | No wormhole |
  |---|---|---|---|---|
  | the Rift | 70.1 / 72.4 / 76.1 / 76.6 | 75.8 / 74.3 / 85.1 / 75.5 | 77.8 / 79.3 / 86.2 / 83.1 | 97.4 / 97.2 / 101.6 / 91.0 |
  | the network (round 27) | 93.9 / 97.2 / 106.8 / 76.6 | 99.1 / 98.5 / 85.1 / 87.1 | 107.3 / 105.5 / 86.2 / 83.1 | 139.9 / 132.7 / 174.6 / 166.0 |

  - The network's 10-20 s loss is gone: the Rift is 24-31 s faster than the network on Hard I1-I3.
  - Every AI rift ran the full 5 s, 712-716 u.
  - Alone, the AI later rides its own rift in 9 of the 12 races that laid one; in field races 3 of 5 rides are by rivals.
  - The difficulty order now holds on I1-I3. On I4, Medium beats Hard by 1.1 s.

  These are the artifact's races; the Unity build has still not been raced in the editor.
- **Unity, without the editor (2026-10-28):** the maths tests pass in a dotnet harness, and the reference compile is
  clean, player and editor.
- **The studio map** (`studio_to_unity.py --check`): on 2026-11-01, 69 mapped and **14 drift**, all 14 Yash's Unity tuning
  (below). Round 27 (2026-11-03) brought them into the artifact and linked the slide's own numbers: **80 mapped, 0 drift**.
  The 14:
  - the tunnel's throat 20 → 7.1 and mouth 40 → 44;
  - the network's forks 22 → 12, openings 12-56 → 16-50, radius 240-1,080 → 100-531, spread 360 → 273;
  - the network's diameters 40 / 46 → 10.5 / 18, tip 0.6 → 0.309, gnarl 0.3 → 0.779;
  - its top speed 90 → 119;
  - `suckRadius` 90 → 40.

## 5. Open items, and what is asked of you

1. **Play it in the editor** (section 2). Nothing of the Meerkat has been played there yet. When it passes, merge
   `Ys-bleeding-edge` to `bleeding-edge`.
2. **Done (round 27, 2026-11-03): the artifact and the game agree again.** The slip-and-slide, the plain-wormhole look
   and the 14 tuned numbers are in the artifact, which stays the Meerkat's source of truth. The look's numbers sit in a
   nested struct the clone script cannot write, so they are matched by hand (`Docs/Studios/MEERKAT.md` round 27).
3. **Race time with the slip-and-slide** is measured in the artifact (round 27), not yet in the editor. The AI plans its
   tunnels as if it rides at the tube's top speed (`SolitonTunnelConfigSO.MaxSpeed`, through
   `MeerkatSolitonConfigSO.SlideMaxSpeed`). In the artifact, holding RT, it slides at about 80% of that, so some tunnels
   pay less than planned. The network costs 10-20 s a race because nothing steers inside it. Time a Burrow Run once it is
   played.
4. **The Mix's Space L5 Burial** no longer lands, because a rider is phased. Keep it, change it or drop it: Yash's call
   with Garrett, whose design it extends.
5. **Done (2026-11-03): its own hull and icons.** `Meerkat.fbx` (`Docs/VesselModels/Meerkat.md`): a raised sensor head
   with a visor band, ear dishes, digging claws, a plated back and a ringed tail, on the Stoat's rig and at its size, so
   `StoatAnimation`, the colliders and the camera are unchanged. Its own HUD row and five icons (Sentinel Mode, Exit
   Portal, Soliton, Faster Ride, the Soliton Network omni card) and its own class cards. Still for the owner's eye:
   whether it reads as a meerkat in flight, and Garrett's pick on each icon (round 1, `Docs/Icons/sheets/meerkat.png`).
6. **Done as a prototype (2026-11-03): touch.** A ThumbLifts profile: the left thumb flies, the right bends the reticle,
   lift the left to form, lift the right to lengthen, tap the Charge card for Sentinel Mode. It needs a phone test and
   Garrett's sign-off on the scheme; a bent long tunnel is not possible on a phone.
7. **The AI's difficulty order** fails in the artifact's round-27 races: on Warpline I3, Medium and Easy beat Hard, and
   on I4 Easy beats Medium. A finding for the AI plan (`Docs/AI_SYSTEM/AI_PLAN.md`), not tuned here. With round 28's Rift
   it holds on I1-I3; on I4 Medium beats Hard by 1.1 s.
8. **The Rift is in the artifact, not yet in Unity** (round 28, 2026-11-03, `Docs/Studios/MEERKAT.md` round 28).
   - The omnicrystal's ultimate, after Apex's Wraith: into the void, then a soliton laid along the path flown, anyone's to
     ride for 45 s. It replaces the Soliton Network; the network stays behind the `omniMode` selector.
   - Its four keys (`omniMode`, `riftSpeed`, `riftSeconds`, `riftLife`) are artifact-only until the clone.
   - `/artifact-to-unity` next: the Soliton Network omni ability becomes the Rift (a void run in
     `MeerkatSolitonExecutor`, its path laid as a `SolitonTunnel` with an `ITubePath`).
   - Yash's call before then: whether a long rift should stay as thin as Garrett's surface area makes it (a 712 u rift has
     a 1.6 u throat).

## 6. Latest activity (generated)

This block is refreshed by `python3 Tools/Build/team_report.py area meerkat --write`. Any Claude session asked for
this report runs it first (`Docs/TEAM.md` rule 10).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-01 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area meerkat --days 14`._

### Recent changes (25+)
- 2026-11-01 `ed1eabc22` docs(meerkat, vessel-studio): bring this session's docs up to date — on Ys-bleeding-edge
- 2026-11-01 `77fedb3ad` fix(meerkat): re-wire the wormhole's combat-hit channel — on bleeding-edge
- 2026-11-01 `f5561caf8` fix(ci): two more generator checks the sweep had been hiding behind the Angler failure — on another branch
- 2026-11-01 `b4f8acb71` Netcode: tune vessel ClientNetworkTransforms (quaternion sync+compression, half-float, unreliable deltas, no scale) and enable time resync — on Ys-bleeding-edge
- 2026-10-29 `cbd0ae226` Omni-crystal abilities: Soliton Network on the Meerkat (own impactor container), Fulmen replaces Overclock on the Stoat — on Ys-bleeding-edge
- 2026-10-29 `a8c7d6b07` Soliton Tunnels: jelly lens + spindle bridge with domain tint, slip-and-slide ride with small RT thrust, environmental fundamental (SolitonTunnelConfigSO), Trunk/Root/Labyrinth renamed to Soliton Tunnel/Network (GUIDs + FormerlySerializedAs kept), Froglet Tools Environmental Fundamentals window, lab and old shaders removed (restore from 2d1e8726c, see WORMHOLE_SHADERS.md section 15) — on Ys-bleeding-edge
- 2026-10-29 `f1f443a2c` feat(wormhole): one calm, clear jelly look for trunk and root, with comfort settings — on Ys-bleeding-edge, bleeding-edge
- 2026-10-29 `2ad588211` Meerkat wormhole: one look; delete the other wormhole shaders, document them for restore — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `0cc66ca55` Update MeerkatWormholeConfig.asset — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `69ebc5b7c` perf(meerkat): lava lamp quick wins - skins built outside Draw, no menu-autopilot roots, profiler markers — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `b8a373d40` docs(meerkat): where it stands now - the report, the area, a current summary, fixed section links — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `742f4349e` docs(meerkat): round 25 write-up, §7a, the verification entry, clones.json — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `3c3fa3fbe` docs(meerkat): Garrett's kit references point at MEERKAT.md section 10 — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `1835aca94` docs(meerkat): round 25 - phased inside a tube, first in first out, measured — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `12e8363f2` feat(meerkat): inside a tube is another dimension; first in first out; the game's omni crystal grows the roots — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `d4afbae57` docs(meerkat): round 24, the slide - what changed, the two bugs, every kit's races — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `4c09676c3` fix(meerkat): the AI's Chain Dig plans before it lets go of RT (no accidental straight chains while sliding) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `b3b81d05c` wip(meerkat-studio): the network sized by Mass (half the cell at Mass 0, the whole cell at 10); two words, wormhole and network — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `006fcea2c` docs(meerkat): Garrett's kit - MEERKAT.md section 8, FLEET_MAPS approved + shipped, protocol 23, hand tests — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `4f8269efe` feat(meerkat): slide the tunnel as you slide the roots (round 24, artifact + Unity) — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `b61e575a2` wip(meerkat): round 23 clone - the network's channels draw as wormhole tubes in Unity; tree and bark off — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `507062b54` feat(meerkat): wire Garrett's kit onto the Meerkat - prefab, Y binding, config, element map 4/4 — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `659a40a1d` feat(meerkat): Garrett's kit in code - soliton swallow and fire, sentinels, exit portal, the faster ride — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `125406f3f` docs(meerkat, prism-motion): lock the Meerkat's element definition and the shielded-prism bounce for Yash to build on — on Ys-bleeding-edge, bleeding-edge
- 2026-10-28 `e2beadfee` wip(meerkat-studio): round 23 - no tree, no roots: the omnicrystal's wormhole network draws as a formed tunnel (artifact) — on Ys-bleeding-edge, bleeding-edge

### Also in flight on other branches
- `chore/claude-androidbuild-skill` (unattributed, 2026-11-01): Merge remote-tracking branch 'origin/bleeding-edge' into chore/claude-androidbuild-skill

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->
