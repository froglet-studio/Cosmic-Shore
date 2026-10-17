# The Angler — lure, tame, and command fauna (vessel class 17)

The Angler is the fleet's strategist. It does not fight prisms itself. It **lures** wild fauna with the light hanging
ahead of its nose (the *esca*), **tames** the creatures it holds in that light, leads them as a school, and **sends a
picked squad at the prisms it chooses**.

The command scheme is the **Sling painter**, picked in round 2 of the Angler Command Lab. Round 3 (2026-10-16) set its
reach to 3200 u and gave the four elements their abilities (§7).

Round 4 (2026-10-17):

- The Angler became a **one-thumb** flier (§1h).
- It flies **2x** faster (cruise 80, boost 220).
- Its chase camera sits **4x** further out.
- The lure **stops** it.
- Charge **spends** a creature per lock (§1e).
- An omni crystal brings a **seed** as well as a creature (§1g).

Round 5 (2026-10-17):

- The speed is back to **cruise 40, boost 110**.
- The brush is **smaller**: paint 3°, erase 1.5°, 2 u at the nose.
- The camera flies **level**, so the brush sits on the screen's centre. It **rises** 56 u while you lure or dash (§1h).

Round 6 (2026-10-17): the school keeps eating **every lit prism, newest first**, one creature per prism down the queue (§1j).

Sources:

- The lab page: `Docs/Studios/AnglerCommandLab.html` (artifact https://claude.ai/artifact/CwuwJcgaGkJqBYC5UHfdE6).
- The scorecard and decisions: `Docs/Studios/ANGLER_COMMAND_LAB.md`.
- The vessel proposal: `Docs/Studios/ANGLER.md`.

**The lab is the source of truth; this vessel is its clone.** Every number below comes from the lab's `SHIPPED` block
through `Tools/Build/studio_to_unity/angler.json`. Every formula comes from the page's `CORE` block, through
`AnglerCommandMath` and a parity gate. To change either, change the page first, then re-clone with
`/artifact-to-unity angler`.

## 1. Controls (the approved element map, §7)

| Input | Pad | Keyboard | What it does |
|---|---|---|---|
| Right trigger, held | RT (analog) | Right Shift | **Paint** (Space). A beam from the nose, reaching `paintRange` (3200 u) and never narrower than `brushNear` (6 u) at the nose, so close-up prisms paint too. Every enemy prism inside it lights up, and every creature prefers it (§1c). The **whole** picked squad balls up `slingAhead` ahead of the nose, any creature still in flight from the last throw included. How far you squeeze sets the brush width: just past halfway paints `brushMin` of the full brush, and a full pull paints all of it. **Level 5 (Paint Shields):** shielded prisms paint too, and the squad cracks the shield off when it arrives. |
| Left trigger, held | LT (analog) | Left Shift | **Erase / Lock-on** (Charge). A narrower beam (`eraseCone`, same reach and close-up rule) unmarks paint, and unmarks an already-ordered prism too: it leaves every order, and a creature heading for it picks again. Each time the beam comes onto a rival vessel (another one, or the same one again) it **locks, and spends one creature** on it (§1e). RT does not paint while LT is held. |
| Right trigger, let go | RT | Right Shift | **Throw.** Each creature in the ball (one still in flight is re-thrown) goes for the **most recently painted** prisms first, regardless of distance (`PickRecent`: the fewest claims, then the newest paint). It is launched at its prism at `slingSpeed`, homing at `homeTurn`, and a homing throw never gives up: it reaches its prism at any distance, limited only by speed. With nothing painted, it is a plain sling: the creature latches onto the first enemy prism it passes within `slingCatch`. |
| Lure, held | X | Q | **Lure, Tame & Grow** (Mass). The esca lights and the hull **stops** (`lureCruise` 0; boost overrides it). Wild herbivores within `lureRadius` turn toward it; a creature held within `tameRadius` tames over `tameTime` and joins the school, whose size is the **number on the Mass card**. Plants in the light grow `lureGrowth` (3x) as often. **Letting go plants every seed** an omni crystal left (§1g). **Level 5 (Steal Fauna):** the lure takes other Anglers' creatures too. |
| Pick squad | A | Space | Cycles through the whole school, then each kind of creature in the order it was first tamed, then back to the whole school. |
| Recall | B | R | The picked squad drops its order and returns to the school (lit prisms stay lit until eaten or erased). |
| Dash to the School | Y | F | **Time.** The hull dashes `dashFrac` (75%) of the way to the centroid of every creature it leads, measured when pressed, in `dashTime` (0.35 s) (§1f). Time also **speeds your tamed fauna**: `faunaSpeed` x1 at Time 0 to x2 at Time 10. |
| Boost | RB | E | The vessel's ordinary boost. |
| Omni crystal (pickup) | — | — | **One creature and one plant.** The crystal morphs into a random creature that joins the school, and leaves a **flora seed**, shown on the HUD's omni card and planted the next time the lure is let go (§1g). |

The round-2 write-up first proposed the lure on RB. The game's RB is already throttle and boost, and LB raises no
input event, so the lure is on X (Button3), in the page's pad map and here alike.

### 1a. What paint looks like: the prism's LIT state

A painted prism is drawn **lit**: its own colour lifted 75% toward white (`paintedLift`, the lab's `render()`), through
`Prism.SetColorLift`.

**It stays lit until it is eaten or erased with LT** (the designer, 2026-10-16):

- The throw does not dim it. The thrown-at prisms become the executor's `_ordered` set, the lab's `marked`.
- **Eaten:** the prism is destroyed and its mark goes with it.
- **Erased:** it fades back over 0.12 s (`markFade`, which is also the fade-in when painted).
- A recall or a finished order leaves it lit.

How the lit state is built:

- The lift is ONE clock stamp per change (`PrismRenderService.StampShadeTransition`). Nothing is written per frame.
- It survives a material sync, like `SetColorShade`.
- The next pooled `Initialize` clears it.
- Only the local pilot's machine draws it (the paint is the pilot's instrument). It is a look, never a gameplay state, so nothing reads it and it is not replicated.

The first cut drew a dot at each painted prism's centre. The dot sat inside the prism and was never seen. The second cut
faded the marks out at the throw; the designer asked for them to stay.

### 1b. The keeper: the Angler always leads at least one creature

The designer, 2026-10-16: *"if there are no fauna under the angler's control it should spawn one. there should always be
at least one fauna that doesn't starve"*.

- **An empty school gets a keeper.** When the school is empty, one creature is born behind the hull, already tamed
  (`EnsureKeeper`; the lab's `ensureKeeper`). It is the cell's own first leashable herbivore, or the config's
  `companionFauna` (Clawfish Space) when the cell has none.
- **It is born the way every creature is born:** through the platform's spawn seam, in the cell's controlling colour, and
  blooming in. A replicated species is born only where the ecology is simulated. At most once a second, and never after
  the turn has ended.
- **The school's first creature never starves** (`IFaunaLeash.Sustains`). When the keeper dies, the next creature
  inherits the role.
- **Only starvation is waived.** A keeper that is eaten, shot or jousted dies the ordinary way, and an empty school gets
  a new one.

This is an **authorized exception** to the locked "populations are bounded by consumption + starvation" rule, fenced and
recorded in `Docs/ECOSYSTEM.md` §0 and §61.4 and in `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`.

### 1c. Every creature prefers painted prisms (Space)

`PaintedPrey` holds every prism an Angler has lit (painted, or ordered and not yet eaten or erased). `LightFauna` and
`Boid` rank a painted prism `paintedPreference` (4) times closer when they pick their next meal, wild creatures included.

- **Preference only.** The creature's own diet still decides what is edible, and it only sees prisms in its own sensing radius.
- **Same on every machine.** Each executor writes the registry on every peer, from the replicated trigger, so the ecology's authority holds the same marks the pilot sees.
- **It is cleaned up.** A mark goes when the prism is eaten, erased or reused from the pool, and when the Angler's turn ends.

**Level 5 (Paint Shields).** The brush paints shielded prisms too. No creature eats shielded mass (LOCKED), so a squad
member that reaches a painted shielded prism **cracks its shield off** (`Prism.DeactivateShields`), and the prism is then
ordinary food. That is this build's reading of "paint shielded prisms too": see follow-up 8.

### 1d. Mass: the lure's light grows plants, and its upgrade steals fauna

While the lure is held, **every plant within `lureRadius` of the esca grows `lureGrowth` (3) times as often**
(`Flora.Hasten`).

- **What changes:** the wait between the plant's own grow ticks. Each tick grows exactly what it always did, under the plant's own budgets.
- **It lapses by itself:** the window is short (0.6 s) and refreshed four times a second while the light is on it.
- **The release:** letting go of the lure plants the omni crystals' seeds (§1g).

Round 3 planted a new plant every 2 s instead; the designer chose growth (2026-10-17).

**Level 5 (Steal Fauna).** The lure takes creatures another Angler holds. `Fauna.TryLeash(holder, steal: true)` moves the
leash, and tells the old holder, through `IFaunaLeash.Released`, that it lost the creature.

### 1e. Charge: every lock spends one creature

While LT erases, the rival vessel nearest along the eraser's beam is "in the beam". **Each time the beam comes onto a
vessel** is one LOCK, whether it is another vessel or the same one again after the beam left it or LT was let go. Every
lock **spends one creature** (the designer, 2026-10-17):

- **Which one:** the school's creature nearest that vessel; the keeper only when no other is left.
- **It leaves the school:** your count drops by one, and it no longer takes orders (a recall does not reach it).
- **It hunts that vessel until it dies**, at its own top speed times `leashTow` times Time's `faunaSpeed`.
- **It can starve.** It is no longer the keeper, so the keeper's exception does not cover it.
- **If its vessel leaves the match,** it goes back to the wild.
- **If the spent creature was the last one,** the keeper rule gives the Angler a new one within a second.

Each bite is a scored contact **Strike** that takes petals. PvP is petals only, `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`.
A bite goes through the same gate as every contact reporter:

- It is decided on the machine that owns the Angler (`ElementalTransfer.IsDecidedHere`).
- It is admitted by `CombatHitDrain.TryAdmit`, with `VesselContact`.
- It is taken at the Strike's fleet price (`ApplyPriced`).
- It is raised on `Event_CombatHitStats`.

The per-pair latch makes it at most one bite per `lockBiteCooldown` (1 s) per vessel. Every hunted hull wears a ring on
the pilot's screen. The Charge card fills with the number of hunters out (3 or more = full).

Round 3 sent the whole squad and called it back on a new paint. Round 4 spends one creature per lock and never calls it
back.

### 1f. Time: dash to the school

The press finds the centroid of every creature the Angler leads, then `AnglerCommandMath.DashPoint` takes `dashFrac` of
the way there. One velocity displacement (`VesselTransformer.ModifyVelocity`) carries the hull there over `dashTime`. Its
weight averages exactly 1 over its life, so it covers the whole distance, plus the hull's own cruise. It runs on the
machine that owns the hull, whose transform replicates.

- No school, no dash.
- A second press during a dash is ignored.
- **Time's scaling** (round 4): `faunaSpeed`, an `ElementalFloat` on `AnglerConfigSO` (x1 at Time 0 to x2 at Time 10), multiplies every speed the leash gives a tamed creature: following, gathering, thrown, eating an order, hunting. A creature still being lured is not affected.
- The lab moves its hull on a smoothstep; the endpoint is the same.

**The Y button is new to the game.** `InputEvents.Button4Action` (14):

- the pad's Y (`buttonNorth`) and F on both desktop schemes;
- a chip row in `Resources/ControlGlyphSet` (Y artwork, label F);
- `InputHintBindingMap` entries.

`ControlChipBindingTests` now admits F to the left hand's QWER + Space cluster. No QWER key is free in both desktop
schemes, and F sits directly under R.

### 1g. The omni crystal: one creature and one plant

Collecting an omni crystal (`AnglerTameByCrystalEffectSO`) gives the Angler two things:

- **A creature.** The crystal **morphs** into a random creature at the crystal's collect pose (`AnglerCommandExecutor.OmniMorph`):
  - **Species:** one of `omniFauna` (Clawfish, QuadFish and Tadpole in every element), picked by a hash of a serial.
  - **Birth:** through the fauna spawn seam, in the cell's colour.
  - **School:** it joins at once, up to the leash capacity.
- **A flora seed** (`AddFloraSeed`), one of the cell's own species:
  - It is **shown on the HUD's omni card** (a seed icon with a count; dim with none held).
  - **The next time the lure is let go, it is planted** at the esca, in the pilot's domain (`PlantSeeds`, through `CellLifeSpawnerBase.SpawnFlora`).
  - Several seeds plant on a small golden-angle ring.

### 1h. One-thumb flight, speed and camera (round 4)

- **One thumb.** `AnglerVesselTransformer` now extends `SingleStickVesselTransformer` (the Brittle Star's pattern):
  - The left stick flies, pitch, yaw and roll included, and the right stick is never read.
  - The triggers still read their full analog depth on a pad.
  - On desktop the one-thumb mouse scheme flies it: the mouse buttons and shifts are the triggers, binary.
  - `OneThumbVesselCoverageTests` and `IO/ONE_THUMB_MOUSE_CONTROLS.md` list it.
- **Speed.** Round 4 doubled it (cruise 80, boost 220). Round 5 put it back: cruise 40 u/s, boost 110 (the boost multiplier stays 2.75). The squad's followers top out near 64 u/s (40 u/s fauna times the 1.6 tow), so they keep up with a cruising hull again.
- **The lure stops the hull.** `lureCruise` 0. The brake's "no cap" is now `AnglerVesselTransformer.NoCap` (-1), since 0 is a real cap.
- **Camera 4x, level (round 5).** `AnglerCameraSettingsSO.followOffset` is (0, 0, -160): the lab's `chaseBack` and `chaseUp`. It is a Vector3, so the generator owns it, not `studio_to_unity`.
  - **Level while flying.** With no height, the camera sits on the nose's own line, so the brush's axis is the screen's centre. That is what makes the small brush accurate: what is in the middle of the screen is what gets painted.
  - **Rises while luring or dashing.** `AnglerCommandExecutor.UpdateCameraLift` raises the follow offset's height by `cameraLift` (56 u) over `cameraLiftTime` (0.4 s), smoothstep-eased, while the lure is held or a dash is in flight, and lowers it the same way after. This is the lab's chase camera (`chaseLift`, `chaseLiftTime`).
  - Only the height is written; the distance stays the camera's own (its zoom).
  - Only the local pilot's own player rig moves, and only while it follows this vessel (the SpreadWings pattern), so the death and replay cameras are never raised.
  - Nothing is written while the camera is level. A vessel swap or a disabled executor puts a raised camera straight back.

### 1i. "The fauna didn't always listen" (fixed)

- **Cause:** holding RT gathered only following and attacking creatures, and the throw skipped any creature still in flight.
- **Why it showed in round 3:** a homing throw stopped giving up, and the brush reached 3200 u. A creature thrown at a far target could be in flight for many seconds and deaf to every new paint.
- **Fix:** RT now calls back every creature of the squad, one in flight included, and the throw re-throws it.
- **Proof:** the lab reproduced it headless (24 of 24 stuck on the old paint before; 24 of 24 on the new paint after). The same fix is ported line for line.

### 1j. The lit queue: eat every lit prism, newest first (round 6)

- **Ask (the designer, 2026-10-17):** the fauna keep eating lit prisms in order of how recently each was lit; if one creature is already heading for the newest, the next goes down the queue, and so on.
- **Before:** a throw's creatures picked only from that throw's own paint, and a new throw replaced it, so a prism still lit from an earlier throw was never eaten.
- **Now:** `AnglerCommandExecutor.PickTarget` reads `LitQueue()` for a Sling-painter order: every prism a throw lit that is still lit (`_ordered`), in throw order. `AnglerCommandMath.PickRecent` (fewest claims, then the newest `_paintStamp`) spreads the school over it.
  - The first creature takes the newest prism, the next the next newest, and so on.
  - A creature that eats picks again from the queue and only rejoins the hull when nothing lit is left.
  - Erasing takes a prism out; painting a lit prism again moves it to the front.
  - A latch's cluster (the throw with nothing painted) still eats nearest first from its own set.
- **Proof:** the lab's `litQueue`, checked headless (`ANGLER_COMMAND_LAB.md` round 6: 12 of 12 older lit prisms eaten, against 0 of 12 before).

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
| `Controller/Vessel/AnglerCommandMath.cs` | The lab's `CORE` block, line for line: `BrushScale`, `InBrush`, `PickTarget`, `PickRecent`, `FormationSlot`, `FollowSpeed`, `LightFollowCap`, `TadFollowCap`, `GatherSpeed`, `TameStep`, `LurePoint`, `DashPoint`. Pure. |
| `R_VesselActions/Executors/AnglerCommandExecutor.cs` | Everything that changes: the paint set, the lure, taming, the school, squads, orders, the throw. Implements `IFaunaLeash`. The per-frame order is BrushStroke → UpdateLure → UpdateFlights → UpdateOrders → AssignSlots → DrawMarks. |
| `R_VesselActions/Data Containers/AnglerConfigSO.cs` | The tuning. Each field is named after its lab key, and each tooltip is the lab's label. |
| `R_VesselActions/Data Containers/AnglerActionSO.cs` | `AnglerControl { Lure = 0, CycleSquad = 1, Recall = 2, Dash = 3 }`. Lure is a hold; the others act on press only. |
| `R_VesselActions/AnglerWorldMarks.cs` | Camera-facing dots and rings drawn as one mesh: the brush and eraser rims, the ball, the esca, the hold ring and the taming dots. Painted prisms are lit instead (§1a). |
| `Controller/Vessel/Prism.cs` (`SetColorLift`, `ColorLift`), `ECS/Rendering/PrismRenderService.cs` (`ApplyColorShade` lift, `StampShadeTransition`) | The per-prism lit state (§1a). |
| `ImpactEffects/EffectsSO/Vessel Crystal Effects/AnglerTameByCrystalEffectSO.cs` + `_SO_Assets/Effects/Vessel Crystal Effects/AnglerTameByCrystalEffect.asset` | The omni crystal's mass tame. |
| `_SO_Assets/Effects/Effect Containers/VesselContainers/AnglerImpactorDataContainer.asset` | The Angler's own vessel container: the Stoat's, forked, with the omni list running the mass tame instead of the Stoat's Overclock. |
| `Controller/Vessel/AnglerVesselTransformer.cs` | The lure brake: `LureSpeedCap` caps the throttle target unless the hull is boosting. It is not a `ThrottleModifier`, because those raise `IsSlowed`. |
| `UI/View/AnglerHUDView.cs`, `UI/Controller/AnglerHUDController.cs` | One gauge per element: Charge (the school hunting a locked vessel; ready while erasing), Mass (the taming in the light; ready while luring), Space (the squad out on a painted order; ready while painting), Time (full when the dash is ready). The gauges are element-named and keep their round-2 serialized names (`FormerlySerializedAs`). |
| `Controller/Environment/FloraAndFauna/IFaunaLeash.cs` | The leash contract (§2), including `Sustains`, the keeper's one exception (§1b). |
| `Controller/Environment/FloraAndFauna/PaintedPrey.cs` | The painted-prisms registry every creature's food search reads (§1c). |
| `Assets/Resources/ElementalAbilityMaps/Angler.asset` | The approved element map (§7), owned by the generator. |
| `Data/Enums/InputEvents.cs` (`Button4Action`), the gamepad / keyboard / single-stick-mouse strategies, `UI/Elements/InputHintBindingMap.cs`, `Resources/ControlGlyphSet.asset` | The Y / F button (§1f). |
| `Tests/Editor/AnglerCommandMathTests.cs` | Five named tests, plus `EveryFunction_MatchesTheLabPage` with 189 golden cases generated from the page. |
| `_Prefabs/Spacevessels/Angler.prefab`, `_Prefabs/UI Elements/VesselHUD/AnglerHUDVariant.prefab` | Cloned from the Stoat by `Tools/Build/author_angler_assets.py`. |
| `_SO_Assets/VesselActions/Angler/` | `AnglerConfig`, plus the `AnglerLure`, `AnglerSquad`, `AnglerRecall` and `AnglerDash` control assets. |
| `_SO_Assets/Classes/SO_Class_Angler.asset`, `_SO_Assets/Camera/AnglerCameraSettingsSO.asset` | The class entry and the camera. |
| `_Graphics/Icons/AbilityIcons/Angler/`, `_Graphics/CardImages/Angler*.png` | Drawn by `Tools/Build/author_angler_icons.py`. The icon FILES keep round 1's names (their guids are wired); each draws its slot's approved ability: `Angler_Command` = Charge (lock-on), `Angler_Leash` = Mass (lure and grow), `Angler_Lure` = Space (paint), `Angler_Tame` = Time (dash). |

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
| Speed while luring (u/s) | `lureCruise` | `lureCruise` | 0 (stops) |
| Lure reach (u) | `lureRadius` | `lureRadius` | 70 |
| Hold radius (u) | `tameRadius` | `tameRadius` | 22 |
| Time to tame (s) | `tameTime` | `tameTime` | 2.5 |
| Taming lost (/s) | `tameDecay` | `tameDecay` | 0.6 |
| Leash capacity | `squadCap` | `squadCap` | 24 |
| Leash tow × | `leashTow` | `leashTow` | 1.6 |
| School ring behind (u) | `formBack` | `formBack` | 6 |
| Tadpole turn (/s) | `tadTurn` | `tadTurn` | 4 |
| Never target my prisms | `spareOwn` | `spareOwn` | 1 (on) |
| Brush reach (u) | `paintRange` | `paintRange` | 3200 |
| Brush half-angle (deg) | `paintCone` | `paintCone` | 3 |
| Light-squeeze brush | `brushMin` | `brushMin` | 0.35 |
| Eraser half-angle (deg) | `eraseCone` | `eraseCone` | 1.5 |
| Brush radius at the nose (u) | `brushNear` | `brushNear` | 2 |
| Ball distance (u) | `slingAhead` | `slingAhead` | 12 |
| Throw speed (u/s) | `slingSpeed` | `slingSpeed` | 150 |
| Homing (/s) | `homeTurn` | `homeTurn` | 12 |
| Throw lasts (s) | `slingTime` | `slingTime` | 3 |
| Catch radius (u) | `slingCatch` | `slingCatch` | 8 |
| Latch cluster (u) | `slingCluster` | `slingCluster` | 24 |
| Dash to the school (share) | `dashFrac` | `dashFrac` | 0.75 |
| Dash time (s) | `dashTime` | `dashTime` | 0.35 |
| Camera rise while luring or dashing (u) | `chaseLift` | `cameraLift` | 56 |
| Camera rise time (s) | `chaseLiftTime` | `cameraLiftTime` | 0.4 |
| Cruise (u/s) | `cruise` | `Angler.prefab` `VesselTransformer.DefaultThrottleScaler` | 40 |

**Game only** (the lab models no flora, rival vessels, wild diet, crystals or elements):

- On the config:
  - `paintedPreference` 4 (§1c);
  - `lureGrowth` 3 (§1d);
  - `faunaSpeed` x1 → x2 by Time (§1f);
  - `lockBiteCooldown` 1 s and `lockBiteReach` 6 u (§1e);
  - `omniFauna`, 12 configs (§1g);
  - `companionFauna` (the keeper's fallback species, §1b);
  - `brushInterval` 0.
- On the executor, the paint's look: `paintedLift` 0.75 (the lab's `render()` number) and `markFade` 0.12 s (§1a).

**Lab only:**

- `boost` (110) and `turnRate` (100) belong to the lab's own simple hull. The game's Angler flies on the single-stick transformer, with `MaxBoostMultiplier` 2.75 (110 / 40).
- `chaseBack` (160) and `chaseUp` (0) are held by the generator in `AnglerCameraSettingsSO.followOffset`, not by `studio_to_unity` (the camera field is a Vector3).
- `aiErase` belongs to the lab's sim pilot. The game has no Angler autopilot yet.

## 6. Proof

All of this proof was produced offline. **This session had no Unity CLI, so `/verify-unity` did not run.**

- `bash Tools/Build/angler_command_parity/run.sh --self-test`:
  - The C# core and the page's `CORE` agree on 189 cases.
  - 10 out of 10 planted changes are caught: the near radius, the nose plane, recency reversed, claims ignored by the recency pick, the dash's origin, the claim penalty, and others.
- `node Tools/Build/angler_command_parity/goldens.cjs --check`: the test's goldens are the page's.
- `python3 Tools/Build/author_angler_assets.py --check`: the prefab, the assets and the registrations are as generated. `--self-test` fires 19 out of 19 (the element map, the dash binding, the combat-hit channel, the seed sprite and the camera included).
- `python3 Tools/Build/author_angler_icons.py --check`.
- `python3 Tools/Build/element_ability_table.py Angler`: 4/4 abilities. The Mass and Space level-5 gates are found at their `IsUpgradeActive` call sites. Time scales `faunaSpeed` (1 → 2); Charge, Mass and Space scale nothing yet (follow-up 3).
- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/AnglerCommandLab.html`: PASS. Round 3, headless: the brush catches a prism 3 u ahead and 4 u off-axis and one 2500 u out; one O press adds one creature; the dash lands on the 75% point.
- `python3 Tools/Build/studio_to_unity.py --vessel Angler --check`: 25 mapped, 0 drift.
- `bash Tools/Build/unity_refcompile/run.sh`, player and editor configs: no errors in the Angler or fauna files.
- The C# gates pass: conditional compilation, console logging, using directives, enum member references, switch labels, self-referential locals, network prefab hashes, the vessel prefab container, class icons, and shared channels.

The Editor test steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` under "Angler".

## 7. Design status — what is approved and what is not

- **Approved (the designer, 2026-10-15):** keep the Angler, and use the Sling painter (RT paints and gathers, LT erases, releasing RT throws).
- **Approved (the designer, 2026-10-16): the element map**, now authored in `ElementalAbilityMaps/Angler.asset`:

  | Element | Ability | Input | Level 5 |
  |---|---|---|---|
  | Charge | Erase / Lock-on | LT | open |
  | Mass | Lure, Tame & Grow | X | Steal Fauna |
  | Space | Paint | RT | Paint Shields |
  | Time | Dash to the School (scales: tamed fauna speed) | Y | open |

- **Not yet designed:**
  - the Charge and Time level-5 upgrades (their labels are empty, per the `/vessel` design gate);
  - which number Charge, Mass and Space SCALE (Time scales the tamed fauna's speed, 2026-10-17).

## 8. Follow-ups

1. **The Angler's own hull.** It wears the Stoat's procedural hull and lope as a stand-in body.
2. **An Angler AI autopilot.** It must be input-only per `/vessel-ai`. The lab's sim pilot (`aiErase`, its squad heuristics) is the starting point.
3. **Element scaling for Charge, Mass and Space, and the Charge and Time level-5 upgrades** (§7). Time scales the tamed fauna's speed. The fleet's convention would make Space the paint's reach, Mass the lure's reach or growth, and Charge the bite, but none is approved.
4. **The HUD's squad name.** The HUD shows counts; `SquadName` exists on the executor but has no text slot in the HUD yet.
5. **A squad that is never ordered starves, apart from the keeper.** Whether a following creature should graze in passing is still a designer call.
6. **The Amoebius Port build check** (rule 51; it needs .NET 10, which this session did not have).
7. **A client pilot's school.** Creatures are leashed on the machine that simulates them (the host), so a client pilot's own executor holds no school. Its dash (which needs the school's centroid) and its HUD counts are empty until the school is replicated to the pilot. Solo play and the host are unaffected.
8. **"Paint shielded prisms too"** is read here as "paint them, and the squad cracks the shield off so they can be eaten". The locked rule "shielded mass is never food" is left untouched. If the designer meant the squad should eat them through the shield, that needs a sign-off on that rule.
9. ~~**The squad trails the 2x hull.**~~ Moot since round 5: the cruise is back to 40 u/s, under the followers' 64 u/s.
10. **The omni pool is herbivores only** (Clawfish, QuadFish, Tadpole), because only a herbivore can be leashed. A random shark would need a predator leash.
