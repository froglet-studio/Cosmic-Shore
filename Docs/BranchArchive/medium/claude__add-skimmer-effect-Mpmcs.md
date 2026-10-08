# Branch archive: `claude/add-skimmer-effect-Mpmcs`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 4
- **Forked from:** `87efdde2c` (2026-03-04, Merge pull request #355 from froglet-studio/claude/improve-elemental-effects-V)
- **Tip:** `fa4e4ca95`
- **Files touched (5):**
  - `Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/SquirrelSkimmerImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselElementDrainBySkimmerEffect.asset`
  - `Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselElementDrainBySkimmerEffect.asset.meta`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselElementDrainBySkimmerEffectSO.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselElementDrainBySkimmerEffectSO.cs.meta`

### `f3c580c9c` — Add VesselElementDrainBySkimmerEffect to nerf slower player's elements

_Claude, 2026-03-04 04:12:09 +0000_

```text
New skimmer effect that drains all 4 elements by 5 ticks for 4 seconds
when a faster vessel's skimmer collides with a slower vessel. Elements
are restored after the duration expires. Wired into the Squirrel's
SkimmerImpactorDataContainer alongside VesselExplosionBySkimmerEffect.
```

```text
 .../SkimmerContainers/SquirrelSkimmerImpactorDataContainer.asset      |  1 +
 .../Vessel Skimmer Effects/VesselElementDrainBySkimmerEffect.asset    | 17 ++++++
 .../VesselElementDrainBySkimmerEffect.asset.meta                      |  8 +++
 .../Vessel Skimmer Effects/VesselElementDrainBySkimmerEffectSO.cs     | 94 +++++++++++++++++++++++++++++++++
 .../VesselElementDrainBySkimmerEffectSO.cs.meta                       |  2 +
 5 files changed, 122 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

### `fa4e4ca95` — Fix element drain coroutine host and stale state issues

_Claude, 2026-03-04 09:02:11 +0000_

```text
- Run restore coroutine on ResourceSystem (the target MonoBehaviour)
  instead of impactee.Skimmer, so StopCoroutine always cancels the
  correct coroutine regardless of which skimmer triggered the effect
- Switch from static to instance dictionaries to avoid stale state
  across editor play sessions (matches VesselShrinkSkimmerEffectSO)
- Key cooldown by ResourceSystem instead of VesselImpactor for
  consistency with the coroutine host
```

```text
 .../EffectsSO/Vessel Skimmer Effects/VesselElementDrainBySkimmerEffectSO.cs  | 26 ++++++++++++++++----------
 1 file changed, 16 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
