# Concept fleet — where it stands (report for Yash, 2026-10-24)

**Branch:** everything is on `Ys-bleeding-edge` only (39 commits ahead of `bleeding-edge`, not merged). **Artifact:** the
Vessel Studio, version 39 (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa), unchanged since 2026-10-18. **Design
record:** `Docs/Studios/CONCEPT_FLEET.md`. **Status:** a browser prototype; nothing of it is in Unity.

## 1. In one minute

- **Ten concept vessels were built and reviewed** (2026-10-17). Garrett kept five, in this order: Gibbon (web /
  tether swinger), Flea (bounce chain), Dragonfly (hover; a miner and a jouster), Marlin (pure 3D drifter), Albatross
  (soaring on the nucleus). The Gecko's trail-run mechanic is kept for a trail runner. The Octopus pulse becomes a mode,
  not a vessel. Remora, Mole and Eel were put down.
- **Only the Gibbon has had its round-2 session** (2026-10-18). It became a web-slinger at Yash's request: no engine,
  elastic webs from both hands, latches on anything, swings and perches with a bounce. The AI cannot race it yet.
- **Nobody has worked on the concept fleet since 2026-10-18**: no commits on its files, no new decisions in the shared
  log, and all eleven of its team tasks are still open.
- **The name "Marlin" is now claimed in the game by someone else.** "Marlin and Jacks" (Swarm Vessel Lab, a fish-herding
  swarm vessel, Garrett's ask on 2026-10-21) was renamed **the Jacks** (vessel 22), and the enum comment says "the name
  Marlin is kept for a swordsman". It is NOT our drifter. Garrett needs to say whether our pure drifter is that
  swordsman or needs another name.

## 2. The five, one line each

| Vessel | Where it is | Next step (task) |
|---|---|---|
| Gibbon | Web-slinger, round 2 done in the studio (§8.3); AI moves but finishes no course | Yash flies it and decides the feel; the Tether prototype (`cece/loving-feynman-hlgf18`) is still unmerged (T-20261017-e64b) |
| Flea | Round 1 page; Garrett had no feedback, "bring it into the game" | Give Garrett a branch, then the Unity vessel and a small racing game (T-20261017-929b) |
| Dragonfly | Round 1 page | Control swap, stored boosts, the mine / joust kit (T-20261017-3cc7) |
| Marlin | Round 1 page (drifter) | Pure drifter, never grips (T-20261017-1bc9); its name now needs a decision (§1) |
| Albatross | Round 1 page (glider, AI weak) | Nucleus-frame up/down, thermals, membrane cost (T-20261017-ba54); needs a cell stage in the studio |

## 3. What the rest of the fleet did this week (on `bleeding-edge`)

Six vessels landed, five of them multi-seat "crew" vessels: **Goby & Pistol (18)**, **Wrasse (19**, the Kite & Tail
navigator on the Sparrow hull), **Orca (20**, one helm and three gunner seats), **Starlings (21**, a passive swarm card
vessel), **Jacks (22)**, **Honeyguide & Ratel (23)**; plus the **Angler (17)** rounds 5-7, the **Brittle Star** ring
explosion as lit faceted volumes, the **Shrike** redesign, and the fleet model pass (the artists' hulls on the Squirrel,
Dolphin, Butterfly, Stoat, Scarab, Shrike, Thresher and Brittle Star). Garrett merged 72 PRs in the window.

## 4. Open tasks and decisions

- Yash: playtest the concept pages and record Keep / Drop (T-20261017-b227); the round-2 sessions (T-20261017-3c5d);
  review Joseph Zhou's pull request (T-20261017-b900).
- Garrett: the open design questions (T-20261017-b999); the Marlin name (new, §1).
- Any session: the AI ladders once the five settle (T-20261017-3244).
- **The Tether's vessel id moved again:** 18 went to the Goby and 19-23 are taken, so the next free id is **24** as of
  2026-10-24 (re-check the in-flight branches before claiming).

## 5. How to try it

Open the Vessel Studio artifact (link above), the hub's "New vessels · the concept fleet" row, pick a vessel. Gamepad,
keys or phone. Record Keep / Drop on each page's Decisions tab.
