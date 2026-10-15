# The Angler — lure, tame, and command fauna (vessel class 17)

The Angler is the fleet's strategist. It does not fight prisms itself. It **lures** wild fauna with the light hanging
ahead of its nose (the *esca*), **tames** the creatures it holds in that light, leads them as a school, and **sends a
picked squad at the prisms it chooses**.

The command scheme is the **Sling painter**, picked in round 2 of the Angler Command Lab:

- The lab page: `Docs/Studios/AnglerCommandLab.html` (artifact https://claude.ai/artifact/CwuwJcgaGkJqBYC5UHfdE6).
- The scorecard and decisions: `Docs/Studios/ANGLER_COMMAND_LAB.md`.
- The vessel proposal: `Docs/Studios/ANGLER.md`.

**The lab is the source of truth; this vessel is its clone.** Every number below comes from the lab's `SHIPPED` block
through `Tools/Build/studio_to_unity/angler.json`. Every formula comes from the page's `CORE` block, through
`AnglerCommandMath` and a parity gate. To change either, change the page first, then re-clone with
`/artifact-to-unity angler`.

## 1. Controls

| Input | Pad | Keyboard | What it does |
|---|---|---|---|
| Right trigger, held | RT (analog) | Right Shift | **Paint.** A beam from the nose marks every enemy prism inside its cone, and the picked squad balls up `slingAhead` ahead of the nose. How far you squeeze sets the brush width: just past halfway paints `brushMin` of the full cone, and a full pull paints all of it. |
| Left trigger, held | LT (analog) | Left Shift | **Erase.** A narrower beam (`eraseCone`) unmarks paint. RT does not paint while LT is held. |
| Right trigger, let go | RT | Right Shift | **Throw.** Each creature in the ball is given its own painted prism. The pick is nearest-first, with each existing claim costing 900 u² so the squad spreads out. Each one is then launched at its prism at `slingSpeed`, homing at `homeTurn`. With nothing painted, it is a plain sling: the creature latches onto the first enemy prism it passes within `slingCatch`. |
| Lure, held | X | Q | The esca lights. Wild herbivores within `lureRadius` turn toward it. A creature held within `tameRadius` tames over `tameTime`, and a tamed one joins the school. While luring, the hull slows to `lureCruise`; boost overrides that. |
| Pick squad | A | Space | Cycles through the whole school, then each kind of creature in the order it was first tamed, then back to the whole school. |
| Recall | B | R | The picked squad drops its order and returns to the school (the paint stays until erased). |
| Boost | RB | E | The vessel's ordinary boost. |

The round-2 write-up first proposed the lure on RB. The game's RB is already throttle and boost, and LB raises no
input event, so the lure is on X (Button3), in the page's pad map and here alike.

## 2. What a leash is (and is not)

`IFaunaLeash` (`Controller/Environment/FloraAndFauna/IFaunaLeash.cs`) is **behaviour only**.

- **What a leash can change:**
  - **Where the creature swims:** `TrySteer` returns a goal, a speed and a turn rate.
  - **Which edible prism its next mouthful faces:** `OrderedMeal`.
- **What it cannot change:**
  - Every ordered prism is re-checked against the creature's own diet. The creature eats it with its own mouthful rule (`LightFauna`/`Boid` `UpdateFeeding`).
  - The starvation clock keeps running.
  - The heart, the body prisms and the colour all stay the creature's own.
- **Who can be leashed:**
  - Only herbivores (`Fauna.CanBeLeashed`). A `Boid` can be leashed only if it is also a forager.
  - A creature can be leashed only by the peer that simulates it (`IsSimAuthority`), and never while it is dying. `Die()` and `OnDestroy()` call `DropLeash()` first.

**Tamed creatures keep the cell's fauna colour.** A cell never holds two fauna colours (LOCKED,
`Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`). So "my squad" is a leash relationship, not a change of team.

The creature-side hooks:

| Class | Hook |
|---|---|
| `Fauna` | `Leash`, `IsLeashed`, `TryLeash`/`Unleash`/`DropLeash`. Virtual `LeashTopSpeed`, `LeashMinSpeed`, `LeashMouthReach`, `TryGetLeashDash`, `LeashLaunch`. |
| `LightFauna` | While leashed and not mid-mouthful, `SteerOnLeash()` slerps the body to the leash goal and eases speed at 2.5/s. `feedCandidate` comes from the leash's ordered meal. |
| `Boid` | The steering target becomes the leash goal; `SteerOnLeash` uses the `tadTurn` turn rate. A dashing forager reports its hunt dash (`huntSpeedMultiplier`, `behaviorUpdateRate`) so its follow cap matches the lab's `tadFollowCap`. |

## 3. Who decides what (multiplayer)

- **Every peer runs the executor**, from the vessel's REPLICATED inputs: `InputStatus.RightTriggerAnalog`, `LeftTriggerAnalog`, the button presses, and the pose. So paint is drawn on every screen, but the marks show only to the local pilot (`DrawMarks`).
- **Consequences happen only on the creature's sim authority:** which creature is leashed, where it swims, and what it eats. They replicate as the creature's own state always has.
- With the server simulating the cell's fauna, a client pilot's commands work through its replicated triggers.

## 4. Files

| File | Role |
|---|---|
| `Controller/Vessel/AnglerCommandMath.cs` | The lab's `CORE` block, line for line: `BrushScale`, `InCone`, `PickTarget`, `FormationSlot`, `FollowSpeed`, `LightFollowCap`, `TadFollowCap`, `GatherSpeed`, `TameStep`, `LurePoint`. Pure. |
| `R_VesselActions/Executors/AnglerCommandExecutor.cs` | Everything that changes: the paint set, the lure, taming, the school, squads, orders, the throw. Implements `IFaunaLeash`. The per-frame order is BrushStroke → UpdateLure → UpdateFlights → UpdateOrders → AssignSlots → DrawMarks. |
| `R_VesselActions/Data Containers/AnglerConfigSO.cs` | The tuning. Each field is named after its lab key, and each tooltip is the lab's label. |
| `R_VesselActions/Data Containers/AnglerActionSO.cs` | `AnglerControl { Lure = 0, CycleSquad = 1, Recall = 2 }`. Lure is a hold; the others act on press only. |
| `R_VesselActions/AnglerWorldMarks.cs` | Camera-facing dots and rings drawn as one mesh: paint, erase, the esca and the hold ring. |
| `Controller/Vessel/AnglerVesselTransformer.cs` | The lure brake: `LureSpeedCap` caps the throttle target unless the hull is boosting. It is not a `ThrottleModifier`, because those raise `IsSlowed`. |
| `UI/View/AnglerHUDView.cs`, `UI/Controller/AnglerHUDController.cs` | The four gauges: command (paint / ball / hunting), leash (school / cap), lure, and tame (the furthest-along taming). The controller polls the executor and is gated to the pilot. |
| `Controller/Environment/FloraAndFauna/IFaunaLeash.cs` | The leash contract (§2). |
| `Tests/Editor/AnglerCommandMathTests.cs` | Three named tests, plus `EveryFunction_MatchesTheLabPage` with 169 golden cases generated from the page. |
| `_Prefabs/Spacevessels/Angler.prefab`, `_Prefabs/UI Elements/VesselHUD/AnglerHUDVariant.prefab` | Cloned from the Stoat by `Tools/Build/author_angler_assets.py`. |
| `_SO_Assets/VesselActions/Angler/` | `AnglerConfig`, plus the `AnglerLure`, `AnglerSquad` and `AnglerRecall` control assets. |
| `_SO_Assets/Classes/SO_Class_Angler.asset`, `_SO_Assets/Camera/AnglerCameraSettingsSO.asset` | The class entry and the camera. |
| `_Graphics/Icons/AbilityIcons/Angler/`, `_Graphics/CardImages/Angler*.png` | Drawn by `Tools/Build/author_angler_icons.py`. |

**Registration** (written by the generator, checked by its `--check`):

- `VesselClassType.Angler = 17`.
- The Vessel Prefab Container (by root Transform) and `DefaultNetworkPrefabs` (by root GameObject, with a fresh `GlobalObjectIdHash`).
- `SO_Classlist_All` / `SO_Classlist_Classes`.
- Four `CrystalHullFusionConfig` entries.
- `ToyVesselRoster.Default`.

## 5. Tuning knobs (lab key → `AnglerConfigSO`)

| Lab label | Lab key | Unity field | Value |
|---|---|---|---|
| Esca distance (u) | `lureAhead` | `lureAhead` | 14 |
| Speed while luring (u/s) | `lureCruise` | `lureCruise` | 12 |
| Lure reach (u) | `lureRadius` | `lureRadius` | 70 |
| Hold radius (u) | `tameRadius` | `tameRadius` | 22 |
| Time to tame (s) | `tameTime` | `tameTime` | 2.5 |
| Taming lost (/s) | `tameDecay` | `tameDecay` | 0.6 |
| Leash capacity | `squadCap` | `squadCap` | 24 |
| Leash tow × | `leashTow` | `leashTow` | 1.6 |
| School ring behind (u) | `formBack` | `formBack` | 6 |
| Tadpole turn (/s) | `tadTurn` | `tadTurn` | 4 |
| Never target my prisms | `spareOwn` | `spareOwn` | 1 (on) |
| Brush reach (u) | `paintRange` | `paintRange` | 160 |
| Brush half-angle (deg) | `paintCone` | `paintCone` | 8 |
| Light-squeeze brush | `brushMin` | `brushMin` | 0.35 |
| Eraser half-angle (deg) | `eraseCone` | `eraseCone` | 4 |
| Ball distance (u) | `slingAhead` | `slingAhead` | 12 |
| Throw speed (u/s) | `slingSpeed` | `slingSpeed` | 150 |
| Homing (/s) | `homeTurn` | `homeTurn` | 12 |
| Throw lasts (s) | `slingTime` | `slingTime` | 3 |
| Catch radius (u) | `slingCatch` | `slingCatch` | 8 |
| Latch cluster (u) | `slingCluster` | `slingCluster` | 24 |
| Cruise (u/s) | `cruise` | `Angler.prefab` `VesselTransformer.DefaultThrottleScaler` | 40 |

**Game only** (the lab draws its own): `maxPaintMarkers` 400, `brushInterval` 0.

**Lab only:**

- `boost` (110) and `turnRate` (100) belong to the lab's own simple hull. The game's Angler flies on the Stoat-derived transformer, with `MaxBoostMultiplier` 2.75.
- `aiErase` belongs to the lab's sim pilot. The game has no Angler autopilot yet.

## 6. Proof

All of this proof was produced offline. **This session had no Unity CLI, so `/verify-unity` did not run.**

- `bash Tools/Build/angler_command_parity/run.sh --self-test`:
  - The C# core and the page's `CORE` agree on 169 cases.
  - 6 out of 6 planted changes are caught: claim penalty, school ring, cone floor, esca height, and others.
- `node Tools/Build/angler_command_parity/goldens.cjs --check`: the test's goldens are the page's.
- `python3 Tools/Build/author_angler_assets.py --check`: the prefab, the assets and the registrations are as generated. `--self-test` fires 12 out of 12.
- `python3 Tools/Build/author_angler_icons.py --check`.
- `python3 Tools/Build/studio_to_unity.py --vessel Angler --check`: 22 mapped, 0 drift.
- `bash Tools/Build/unity_refcompile/run.sh`, player and editor configs: no errors in the Angler or fauna files.
- The C# gates pass: conditional compilation, console logging, using directives, enum member references, switch labels, self-referential locals, network prefab hashes, the vessel prefab container, class icons, and shared channels.

The Editor test steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` under "Angler".

## 7. Design status — what is approved and what is not

- **Approved (the user, 2026-10-15):** keep the Angler, and use the Sling painter, with RT painting and gathering, LT erasing, and releasing RT to throw.
- **NOT approved, so deliberately unauthored:**
  - The element → ability map. `Docs/Studios/ANGLER.md` proposes Charge = Command, Mass = Leash, Space = Lure, Time = Tame.
  - The level-5 upgrades.

  `ElementalAbilityMaps/Angler.asset` does not exist, and the generator's `--check` fails if one appears without this doc changing. Per the `/vessel` design gate.

## 8. Follow-ups

1. **The Angler's own hull.** It wears the Stoat's procedural hull and lope as a stand-in body.
2. **An Angler AI autopilot.** It must be input-only per `/vessel-ai`. The lab's sim pilot (`aiErase`, its squad heuristics) is the starting point.
3. **The element map and level-5 upgrades**, once the designer approves them (§7).
4. **The HUD's squad name.** The HUD shows counts; `SquadName` exists on the executor but has no text slot in the HUD yet.
5. **A squad that is never ordered starves.** A leashed creature with no order follows the school and does not graze, so the game's starvation clock can kill it. That is consistent with "no imposed death" (it is the creature's own clock), but it is a designer call. The alternative is to let a following creature graze in passing.
6. **The Amoebius Port build check** (rule 51; it needs .NET 10, which this session did not have).
7. **The lure on X** (§1), not the RB the round-2 write-up first proposed: confirm it in play.
