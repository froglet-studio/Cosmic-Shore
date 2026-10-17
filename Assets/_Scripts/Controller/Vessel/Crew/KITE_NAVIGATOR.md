# Kite Navigator: the Kite & Tail crew seat

A crew seat for a second player on one hull. The design is the Crew Vessel Lab's round 3 candidate, which Garrett
signed off on 2026-10-17 (`Docs/Studios/CrewVesselLab.html`, write-up in `Docs/Studios/CREW_VESSEL_LAB.md`).

One player flies the hull. The **navigator** plays a one-thumb 2D game on a **board**, which is the hull's own x-z plane
with the world projected onto it. The navigator flies a **kite**:

- **The head** pops hostile prisms it touches (`Prism.Damage`). It also rams rival hulls.
- **The tail** is a lasso. When the kite crosses its own tail, it closes a loop.
- **A loop round motes of one element** fires that element's effect. The effect grows by `base × growth^(n − 1)` for
  n motes, so with growth 2 each extra mote doubles it:

| Element | Effect | In the game |
|---|---|---|
| Space | a blast at the loop's centre that destroys hostile prisms | `ExplosionHelper.CreateExplosion` with `AOEExplosion.prefab`, sparing our domain; `MaxScale = 60 × 2^(n−1)` (cap 640) |
| Mass | a cluster of our domain's prisms | the hull's own `PrismSpawnChannel` and prism type, sized like the Sparrow's trail prism (2 × 2 × 5); `6 × 2^(n−1)` prisms (cap 240), at most 40 laid per frame |
| Time | boost for the pilot | `1.5 × 2^(n−1)` s of `IsBoosting`, burned by the hull at once (cap 30 s) |
| Charge | kite power | `1 × 2^(n−1)` petals; the next ram on a rival steals that many |

A loop that holds more than one element **fizzles** (`mixedLoops = Fizzle`).

**A ram is a scored Strike.** PvP is petals only (LOCKED). The ram goes through the same gate as every contact weapon:
`ElementalTransfer.IsDecidedHere`, then `CombatHitDrain.TryAdmit(…, CombatHitClass.Strike, stealCooldown,
ElementalDebuffSources.VesselContact)`. It then moves whole petals one at a time from the rival's fullest element
(`ElementalTransfer.ApplyAuthoritative`, the Steal form). Finally it raises `OnCombatHitLanded`, the scoreboard's
channel.

**Motes belong to the navigator.** Only its board draws them, and they are not game crystals.

## Seats: the swap is the arena's

The swap uses the arena's pilot-swap gesture (`PilotSwapGesture`: D-pad left / right, keys 1 / 2), with
`PilotSwap.CooldownSeconds` as its cooldown. It moves the local human between the hull and the kite. An AI squad mate
always flies the empty seat:

- **The human navigates.** The hull's own `AIPilot` flies the hull (`IVessel.ToggleAIPilot(true)`). The human's flight
  input is paused (`InputController.SetPause(true)`), so the pointer and the sticks belong to the board.
- **The human flies.** `KiteNavigatorAI` flies the kite. It moves an aim point at a thumb's speed, with aim noise, and
  presses it. It never writes the kite (the input-only rule).
  - Each think, it rams if charged, else loops the fattest one-element cluster, else pops the nearest hostile prism.
  - The default skill is Hard (`aiSkill`).

## Thumb

On a touch screen, the kite aims `thumbOffsetMillimetres` (21 mm, the lab's 80 CSS px) **above** the thumb, so the thumb
never covers the kite. `Screen.dpi` sets the millimetres; 160 dpi is assumed when the device reports 0.

A press must start on the board, or within `boardTouchMargin` of its rim, to take the kite. The board sits at
`boardScreenCentre` (bottom right by default) at `boardScreenSize` of the screen's short side. Board forward is
screen up.

## Files

| File | What |
|---|---|
| `KiteNavigatorCore.cs` | The kite: move, tail, loop detection, loop resolution, growth, blast radius. Engine-free; ported line for line from the lab's pure functions. |
| `KiteNavigatorAI.cs` | The AI navigator (the lab's `navAI` / `planLoop`). Engine-free. |
| `KiteNavigatorConfigSO.cs` | Every number; each tooltip names its lab key. Asset: `Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset`. |
| `KiteNavigator.cs` | The seat on the hull: board frame, motes, prism and rival shadows, thumb, head contacts, effects, swap. |
| `KiteBoardGraphic.cs` | The board, drawn as one UI mesh on the navigator's own overlay canvas. No prefab or sprite assets. |
| `Assets/_Scripts/Editor/Studios/KiteNavigatorHarnessMenu.cs` | **FrogletTools > Vessels > Kite Navigator > Seat On Local Hull (Play mode)** and **Remove**. |
| `Assets/_Scripts/Tests/Editor/KiteNavigatorCoreTests.cs`, `KiteNavigatorConfigTests.cs` | The core's rules, and the asset's numbers and wiring. |
| `Tools/Build/kite_navigator_lab/parity.cjs` | Compiles the shipped C# core and diffs it against the lab page frame by frame, over 6 scripted thumbs. Also diffs the asset against the page key by key. Planted controls must be caught. |

## Try it

1. Start any match with a vessel (the lab flew the Sparrow).
2. Run **FrogletTools > Vessels > Kite Navigator > Seat On Local Hull (Play mode)**. The board appears bottom right,
   and the AI flies the kite while you fly.
3. Press **2**, or D-pad right, to move to the kite. The hull's AI takes over. Click or touch the board and draw
   loops round a single colour of motes.
4. Press **1**, or D-pad left, to take the hull back.

## Where the game differs from the lab

- **Time is burned at once.** The Sparrow's boost button is unlimited, so the navigator's boost cannot be a reserve the
  pilot spends. The hull asserts `IsBoosting` until the gift runs out, then releases it only if it was the one holding
  it.
- **The Space blast is the real `AOEExplosion`.** Through its `ExplosionImpactor` it can also hit rival hulls, which
  the lab's blast never did.
- **The AI's prism lead uses the hull's velocity.** A prism's drift across the board comes from the hull's velocity and
  ignores the board's own turn. The lab differenced each prism's shadow.
- **The board always shows**, even while the human flies, so the pilot can watch the AI work. It takes the pointer only
  while the human navigates.

## Gates

```
node Tools/Build/kite_navigator_lab/parity.cjs        # core vs page, asset vs page, planted controls
bash Tools/Build/unity_refcompile/run.sh              # the player compile
```

## Follow-ups

- **Networking.** Everything runs on the hull's local human's machine: the board, the motes, the AI and the effects.
  A second human on another machine needs the seat replicated, plus a decision on which machine decides the effects.
- **Arena swap.** In an arena match, the swap gesture also asks the arena to swap hulls while the human flies.
  `InputController` polls it, and is paused only while the human navigates. A crew hull in an arena needs one owner
  for the gesture.
- **Duo vessels.** When the duo-vessel system lands (branch `cece/sharp-tesla-j0kd7z`, skill `/duo-vessel`), register
  the kite as a duo design, rather than keeping this play-mode menu as its only door.
- **Sounds.** All seven `EventReference` slots ship empty: loop closed, fizzle, pop, Mass, Time, Charge, steal.
