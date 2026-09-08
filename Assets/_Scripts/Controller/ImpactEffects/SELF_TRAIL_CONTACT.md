# Self-Trail Contact Grace

**A pilot does not interact with their own trail while they are making it.** For a short window
after a vessel lays a prism, that vessel's hull and skimmer both ignore it. Every *other* vessel —
opponent and teammate alike — sees the prism as fully live from the frame it appears.

Config: `Assets/_Scripts/ScriptableObjects/SelfTrailContactConfigSO.cs` ·
asset `Assets/Resources/SelfTrailContactConfig.asset`.

---

## 1. Why it exists

A trail prism is laid a fixed `offset` behind the vessel, and the spawner assumes the vessel then
promptly leaves it. Three things break that assumption:

| | |
|---|---|
| **Drift** | The hull slides sideways across the ribbon it is extruding. The prism recedes along the *course* while the vessel's long axis is still lying over it. |
| **MASS scaling** | `VesselPrismController.CreateBlock` cube-roots the MASS volume multiplier into every axis, so an upgraded vessel lays prisms that reach further back than the un-upgraded geometry the clearance delay was sized against. |
| **Skimmer reach** | The trigger sphere is far bigger than the hull (the Squirrel's is 15–30 u). "Outside the ship" and "outside the skimmer" are a long way apart at low speed. |

The consequences were gameplay, not cosmetics:

- A **Squirrel** drifting fed itself skim energy off the ribbon it was extruding
  (`SkimmerBoostPrismEffect` inspects neither owner nor age), and could steal its own prisms.
- A **Dolphin** *rammed* its own fresh trail. `VesselChangeResourceByPrismEffectSO` and
  `VesselChangeBoostByPrismEffectSO` each keep only `retainedFraction` (0.5) of the meter, so a
  self-ram cost **half the banked skim energy and half the charged boost** — plus
  `VesselDamagePrismEffect` shredding the pilot's own mass and an impact SFX to sell it.
The **Rhino** is deliberately NOT in that list. `RhinoSkimmerDamagePrismEffect` carries no domain,
owner or age guard either, so the grace does technically apply to its sword — but the Rhino cannot
come about onto the ribbon it just laid inside a one-second window, so the gate never fires for it
in practice. This matters because cutting your own trail to bank sword energy is a **signed-off
design**, not a bug (`R_VesselActions/RHINO_ENERGY_SWORD.md` § "Friendly fire + self-farming"), and
that doc's guidance — if self-farming ever reads as an exploit, skip the **energy bank**, never the
damage — is unchanged by this branch.

## 2. Why the existing flags could not express it

`Skimmer.affectSelf` compares **domains**, so switching it off also blinds a vessel to its
teammates' trails — and it is evaluated *after* the skimmer effect loop anyway (the
`!skimmer.AffectSelf && prism.Domain == …` line at the tail of `SkimmerImpactor.AcceptImpactee`'s
prism branch — grep the symbol, the line number moves), where it gates only the `_skimStartTimes`
bookkeeping. It changes nothing for effects.

`VesselChangeSpeedByPrismEffectSO` is the one vessel-prism effect with a self-guard, and it is the
same domain compare — a weaker, different rule, and one every sibling effect in the container is
free to forget.

What a pilot must not touch is **their own mass in the instant they are making it**. That is
per-vessel and per-moment, so the gate is:

> `prism.ownerID == vessel.PlayerName` **and** `Time.time - prismProperties.TimeCreated < grace`

`ownerID` (not `PlayerName`) because it records **who laid it** and a steal does not reassign it —
a prism taken off an opponent was never yours to be making, so it stays interactable.
`IsEnvironmentOwned` mass (flora, fauna, cell structure, the SkimRace track) is never anyone's own
trail and is excluded outright.

## 3. Where it is enforced

Written **once**, as two static predicates on the config, so a future dispatch site asks rather
than re-derives:

| Call site | Guard |
|---|---|
| `VesselImpactor.AcceptImpactee` → `case PrismImpactor` | `SuppressesHullContact` |
| `SkimmerImpactor.AcceptImpactee` → `case PrismImpactor` | `SuppressesSkimContact` |

Both sit **above** the shell-ownership guard, so a shielded self-prism (the Squirrel's MASS-5
"Heavy Trail" drift armour) is suppressed on the analytic shell tier as well as the box trigger.
The hull guard also sits above the impact SFX, so a self-ram is silent as well as harmless.

## 4. What deliberately still works

- **Another player's trail — immediately.** A pursuing Squirrel skims a freshly-laid opposing
  ribbon from the frame it appears, keeps gaining energy, and closes to joust range unchanged.
  Nothing about the grace is visible to anyone but the pilot who laid the mass.
- **A teammate's trail — immediately.** The gate is owner-scoped, not domain-scoped.
- **Your own older trail.** Past the grace it is ordinary mass again: rideable, skimmable,
  rammable. A self-laid tube is still a tube; it just cannot be ridden while it is still coming
  out of the ship.

## 5. Known properties

- **The grace is evaluated at contact time, not re-evaluated during a contact.** A self-prism
  suppressed on entry and *still inside* the skimmer when the grace expires is not retroactively
  dispatched — there is no second `OnTriggerEnter` to carry it. In practice a prism crosses a
  30 u sphere in well under the grace at any normal speed; a vessel hovering over its own fresh
  trail is the case that loses the contact, which is the conservative direction. Shielded prisms
  are exempt from this: the shell tier re-dispatches per frame and picks up on its own.
- **Nothing is culled, decayed, delayed into existence, or hidden.** The mass is fully live for
  the whole world from the frame it is laid; one vessel declines to act on it. Conserved mass is
  intact.
- **Fleet-wide, by design.** A prism should read the same whichever hull is next to it, so there
  is no per-vessel override. Set a grace to 0 to disable that half globally.
- **Identity is the player NAME**, because that is the only per-vessel handle a prism carries:
  `Prism.Trail` is null on vessel-laid prisms and there is no `Prism.Vessel`. AI names are indexed
  per match (`profiles[i].Name` / `AI {i+1}`) so they cannot collide; two *humans* with the same
  display name would share the gate. This is the identity model `_skimStartTimes` and `ownerID`
  already use — the gate inherits it rather than introducing it — and the failure mode is mild
  (you do not ram a namesake's freshest prism for one second).

## 6. Companion fix — the clearance delay measured the wrong prism

`VesselPrismController.CreateBlock` sizes `waitTillOutsideSkimmer`'s collider-off delay from
`TrailZScale` (= `BaseScale.z`), which omits **both** `ZScaler` and the MASS volume multiplier
applied a few lines above it. An upgraded vessel therefore had its collider come on while the
prism was still inside the ship. It now measures `scale.z` — the length the prism is *actually*
being laid at — with a speed floor and a ceiling so a stalled vessel cannot divide its way to an
unbounded delay.

Un-upgraded vessels are unchanged: with `ZScaler` 1 and no boost/volume scaling, `scale.z` **is**
`BaseScale.z`. The delay only ever lengthens when the prism is genuinely longer.

This stays a **geometry correction**, not the self-trail lever — it hides the prism from
*everyone*, which is exactly why it cannot be the mechanism for an owner-scoped rule.

Affected (`waitTillOutsideSkimmer: 1`): Sparrow, Rhino, Dolphin, Urchin, Grizzly, Scarab.
Unaffected (`: 0`): Manta, Termite, Squirrel, Falcon, Shrike, Serpent.

## 7. Files

| File | Role |
|---|---|
| `_Scripts/ScriptableObjects/SelfTrailContactConfigSO.cs` | The rule + the two grace windows |
| `Resources/SelfTrailContactConfig.asset` | The fleet's one authored instance |
| `_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs` | Hull guard |
| `_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs` | Skimmer guard |
| `_Scripts/Controller/Vessel/VesselPrismController.cs` | Clearance-delay geometry fix (§6) |

## 8. Tuning knobs

| Knob | Default | Effect |
|---|---|---|
| `hullGraceSeconds` | 1.0 | How long the pilot's hull ignores their own fresh prism. Raise if vessels still clip their own ribbon mid-drift; this is the one that protects the Dolphin's energy bank. |
| `skimGraceSeconds` | 1.0 | How long the pilot's skimmer ignores their own fresh prism. This is what stops a drifting vessel harvesting the ribbon it is extruding. Lower it if self-skim feels too dead coming out of a drift. |

## 9. In-editor verification

1. **Squirrel, freestyle (Menu_Main).** Enter freestyle, drift hard in a tight circle. Watch the
   Charge petal flowers / boost gauge: they must **not** climb from the ribbon you are laying.
   Straighten out, come back across trail laid more than a second ago — skim energy resumes.
2. **Dolphin, Rampage or freestyle.** Bank skim energy on flora, then drift so the hull crosses
   its own fresh ribbon. Energy and charged boost must **not** halve, and there must be **no**
   `VesselImpact` SFX. Repeat against an *older* stretch of your own trail — it should ram, sound,
   and cost you, exactly as before.
3. **MASS upgrade.** Take Mass to 5 on the Squirrel (Heavy Trail: drift prisms arrive shielded)
   and repeat (1). The shielded prisms route through `PrismShellContactManager`, so this is the
   case that proves the guard sits above the shell-ownership check.
4. **Two clients (MPPM).** Fly one vessel behind another. The trailing pilot must skim the leader's
   trail from the moment it appears, gain energy normally, and be able to close to joust range.
   Then put both on the **same domain** and confirm the trailing pilot still skims the leader's
   ribbon — this is what a domain-scoped fix would have broken.
5. **Rhino (regression check, not a new behaviour).** Cutting your own older trail must still bank
   sword energy at the signed-off 0.04/prism — the grace is not expected to reach this vessel at
   all, so any change here is a bug in the grace, not the intended effect.
6. Console clean throughout; no NREs from the new `Resources.Load` path (delete the asset once and
   confirm the defaults still apply and the rule still holds).
