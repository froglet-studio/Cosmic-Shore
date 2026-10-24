# The Flea (VesselClassType.Flea = 24)

The ricochet: it crawls on its own and gets its speed from **bouncing off mass**. A prism contact reflects the hull
instead of costing it, and RT pressed just as it hits is a **perfect bounce** that banks speed and grows a chain. Miss
the press and the bounce is dull (it loses speed). Hold LT to crouch; let go to hop along the nose.

- **Source:** the Vessel Studio artifact's concept fleet, kind `ricochet`
  (`Docs/Studios/VesselStudio/studio-kinds.js` on `Ys-bleeding-edge`, recipe `vessels/flea.json`, design
  `Docs/Studios/CONCEPT_FLEET.md`). The artifact is the source; Unity is its clone (/artifact-to-unity).
- **Decided:** Garrett kept the Flea in the 2026-10-17 review ("bring it into the game, give me a branch"). Next per
  that review: a small racing game for it, then bleeding-edge.
- **Status:** first Unity prototype, branch `claude/flea-vessel`. Flyable in Menu_Main freestyle. Not yet opened in
  Unity (no Unity editor in the session that built it): `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 1. How to fly it (Menu_Main freestyle)

Toy Box ▸ **Vessel Changer** ▸ **Flea**.

| Control | Pad | Keyboard | Mouse + keyboard | What it does |
|---|---|---|---|---|
| Steer | sticks | the usual two-stick keys | mouse | Turns the NOSE. The momentum follows the nose only slowly (Velocity grip 2.2/s): between bounces the Flea is nearly ballistic, so you aim the next bounce with the nose. |
| Bounce | RT | Right Shift | LMB | A press opens the perfect window. Hit a prism within half the window (90 ms) after it: **perfect**, speed x1.35, chain +1. Press up to 90 ms AFTER a dull bounce: saved, it becomes perfect. |
| Crouch / hop | LT | Left Shift | RMB | Held: crouch (speed eases to 15 u/s) and charge (full in 0.6 s). Released: hop along the nose at 60 to 160 u/s by charge. |

A chain is lost 4 s after the last bounce. Speed above the crawl (50 u/s) fades at 0.25/s; the cap is 260 u/s.

## 2. Architecture

| Piece | What it is |
|---|---|
| `FleaMath` (`Controller/Vessel/`) | The page's `step`, `normal` and `onContact`, ported line for line. Pure; `FleaMathTests` holds it to golden numbers produced by running the page's own code in node (`Tools/Build/flea_harness/goldens.cjs`), runnable headless with `bash Tools/Build/flea_harness/run.sh` (`--self-test` plants two defects the tests must catch). |
| `FleaVesselTransformer` | The flight. On the vector flight model with grip and nose thrust switched off (`NoseConvergence` and `ComputeNoseAcceleration` return 0); `ComputeExternalAcceleration` runs one `FleaMath.Step` and returns the change to `course × speed`. Holds the state (speed, course, chain, charge, the press and bounce clocks). A bounce writes `speed` and `VesselStatus.Course`, which the base adopts next frame (SyncExternalWrites). The platform's modifier channels (danger slow, knockback, warp) stay live. |
| `FleaConfigSO` | The numbers. Field names are the page's keys; each tooltip is the artifact's label; defaults are the kind's `defaults`. `samePrismGuard` is Unity-only (one prism, one bounce, even when a trigger reports it twice). |
| `FleaActionSO` (`FleaControl` Crouch = 0, Bounce = 1) | Both controls are HELD; Start / Stop set the transformer's flags. Shared and stateless. |
| `FleaBounceByPrismEffectSO` | The hull's prism contact: calls `FleaVesselTransformer.Bounce(prism)`. |
| `FleaHUDController` | Drives the Stoat's HUD view (stand-in) from the transformer (§4). |
| `Tools/Build/author_flea_assets.py` | The vessel creation tool: clones `Stoat.prefab`, writes the assets, registers the vessel; `--check` in CI, `--self-test` with 20 negative controls. |

## 3. The bounce

- **Surface:** a prism is a long box, so the normal is the hull's offset from the prism's AXIS (`prism.transform.forward`),
  and a hull hitting a ribbon at a shallow angle SKIPS along it keeping its along-track speed. Under 0.5 u off the axis
  the hull's own up is used (`FleaMath.BounceNormal`).
- **Reflection:** the velocity reflects when it is moving into the surface; speed x perfectGain (perfect) or x dullLoss
  (dull), capped at topSpeed. The hull is pushed 2.5 u out along the normal and its nose turned 80% onto the new course.
- **What a contact no longer costs:** `FleaImpactorDataContainer` is the Stoat's container minus
  `VesselDamagePrismEffect`, `VesselResetBoostPrismEffect` and `SquirrelVesselChangeSpeedByPrism` (the hull-contact
  cost a dull bounce replaces, as on the page) and minus the Stoat's omni Overclock (it drives Stoat executors). It keeps
  the haptics and the danger debuff. **Open (designer):** should a perfect bounce mark, shield or break the prism? The
  studio proposes it as Mass 5 "Shatter"; today the prism is untouched.
- Your own fresh trail never bounces you (the platform's `SelfTrailContactConfigSO` guard runs first).

## 4. HUD (stand-in)

The Stoat's HUD variant and view, four gauges in charge, mass, space, time order:
Charge = the chain (full at 6), Mass = speed against the cap, Space = the hop charge, Time = the perfect window after a
press (lit while open). Its icons are still the Stoat's.

## 5. Elements: open

`Assets/Resources/ElementalAbilityMaps/Flea.asset` carries four **open design slots** (Input 0, no level-5 upgrade). The
studio's proposals are written into each slot's description and into `FleaMath.AtLevels` (tested), but **nothing scales
with an element in Unity yet**, because nobody signed them off (vessel skill §3):

| Element | Studio proposal | Scales | Range | L5 proposal |
|---|---|---|---|---|
| Charge | Spring | perfectGain | x1 to x1.15 | Supercharge |
| Mass | Momentum | topSpeed | x1 to x1.2 | Shatter |
| Space | Big Hop | hopMax | x1 to x1.5 | Leap |
| Time | Wide Window | window | x1 to x1.7 | Slow Motion |

## 6. Stand-ins (borrowed from the Stoat until the Flea has its own)

The body (the Stoat model and its lope), the jets and tails, the hit box, the skimmer, the camera, the HUD art and the
class card sprites. The Flea's page body is a round shell with two legs (studio-kinds.js `hull`).

## 7. Not done yet

- No Flea AI: an AIPilot flies the hull but never presses Bounce or Crouch. The page's AI rule (press RT inside 40% of the
  window before a predicted contact; crouch to hop below 70% of crawl with no mass near) is the port target.
- No touch profile (`Tools/Build/author_touch_profiles.py`): on a phone the default trigger cards raise LT / RT.
- No sounds: a bounce, a perfect bounce and a hop each want an exposed `EventReference`, shipped empty.
- No bounce tell (the page's "Bounce in 0.4 s" readout and the predicted contact).
- No game mode: Garrett's next step is a small racing game built around it.
