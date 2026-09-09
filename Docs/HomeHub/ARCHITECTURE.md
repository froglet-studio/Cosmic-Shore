# The home hub — four ways to play

The home screen used to open exactly one thing: **Arcade**. It now opens four, because there is
more than one kind of game mode in the project and only one of them is an arcade card.

| Entry | Modal | State today | What it draws |
|---|---|---|---|
| **Mission** | `ModalWindows.MISSION` | `Unavailable` | nothing yet — the entry exists, the modal does not |
| **Toy Box** | `ModalWindows.TOYBOX` | `Available` | the freestyle toybox, flat |
| **Arena** | `ModalWindows.ARENA` | `Locked` | a full arcade-shaped card grid, behind one flag |
| **Arcade** | `ModalWindows.ARCADE` | `Available` | unchanged |

---

## 1. A hub entry names a modal TYPE, never a window

`MenuHubButton` (`_Scripts/UI/Elements/MenuHubButton.cs`) holds a
`ScreenSwitcher.ModalWindows` value and calls `ScreenSwitcher.OpenModal(type)`. It does **not**
hold a reference to the `ModalWindowManager` it opens.

That matters because the switcher already owns the modal stack, the return-to-modal PlayerPrefs
key, the close-everything sweeps and the "block the screens behind a modal" gate. A button that
reached past it to call `ModalWindowIn` directly would be a second authority on a modal's
lifecycle — the same class of mistake as a panel writing `ArcadeGameConfigSO`.

Adding a fifth hub entry is therefore: one enum member, one `ModalWindowManager` in the
switcher's `Modals` list, one button carrying a `MenuHubButton`.

## 2. Availability is a state, not a missing button

```
Available    → does the entry's job (opens its modal, navigates to its screen, selects its tab)
Locked       → stays pressable, refuses with a Denied sting + a toast saying why
Unavailable  → not interactable, reads as not-built
```

An entry that is simply **not drawn** tells the player the game has three things in it, and the
day it ships they have to re-learn the screen. Both unfinished states stay on screen; they differ
in what they promise. `Locked` says *this exists and you cannot open it yet* — which is true of
Arena, whose modal behind the lock is real and complete. `Unavailable` says *this is not built*,
which is true of Mission, and it does not respond at all.

`MenuHubButton.SetAvailability` is the runtime seam a progression unlock plugs into later, so
opening Arena needs no new plumbing here.

### 2.1 The state is shell-wide, and it is ONE implementation

The hub was not the only surface that could ship before it was finished, so the model does not
live on the hub button any more:

| Piece | Where | What it owns |
|---|---|---|
| `MenuAvailability` | `_Scripts/UI/Elements/MenuAvailability.cs` | the three states, and nothing else |
| `MenuAvailabilityView` | `_Scripts/UI/Elements/MenuAvailabilityView.cs` | **the only place a state becomes pixels and a response** — overlays, label tint, `Selectable.interactable`, the Denied sting, the wording |
| `MenuHubButton` | hub entries | which modal Available opens |
| `ScreenSwitcher` | nav-bar links | which screens are closed (`disabledScreens`), and what Available navigates to |
| `NavLink` | in-screen tab rows | which view Available selects |

**The shared piece is the state and its presentation, never the target.** The three hosts aim at
three different types — `ScreenSwitcher.ModalWindows`, `MenuScreens`, a `View` — and cannot be
unified. What the player actually learns is the *look and the refusal*, and those are now one
implementation, so a second one cannot drift away from it.

`MenuAvailability`'s values are explicit and stable (0/1/2) because they were lifted out of
`MenuHubButton.HubAvailability`, whose serialized fields store these integers.

### 2.2 It has to read as locked with NO authored art

The surfaces that need this most are the ones nobody drew a locked state for. A nav-bar link is
two `Image` children and an `EventTrigger` — no label, no overlay, not even a `Button`. So when no
overlay and no label are wired, `MenuAvailabilityView` falls back to **dimming the host's own
`Graphic`s**: a real visual difference bought with zero authoring, and the fallback switches off
the moment a locked look IS authored, so the two never double up.

It **tints rather than disables**, because an absent graphic does not raycast — switching one off
would silently delete the touch target the entry still needs in order to be pressable enough to
refuse.

One ordering detail that is a bug if you miss it: `NavLink`'s crossfade writes every icon back to
its authored colour on **every group selection**, which wipes the dim. `NavLink` re-asserts the
state at the end of the crossfade (`MenuAvailabilityView.Reapply`), so a locked tab does not
quietly un-dim the first time a sibling is pressed.

### 2.3 The nav bar: `disabledScreens` stays the single source of truth

`ScreenSwitcher.disabledScreens` (`{ARK, PORT}`) already decided which screens are closed. It now
also decides which links *read* as closed: `MarkDisabledNavLinks` stamps `Locked` onto each
disabled screen's link at `Start`, and `NavigateTo` answers a press through that link's view
instead of returning in silence.

Driven at runtime, not authored on the links, for the reason the list exists at all — two authored
copies of the same fact drift. A screen added to `disabledScreens` tomorrow is marked with no scene
edit; a screen removed from it goes back to normal without one either. Only the disabled links get
a view: an Available entry has nothing to present.

The link for a screen index is resolved the same two ways `UpdateNavBar` highlights one — the
explicit `NavActiveImages` list first (each entry's **parent** is its button), then the legacy
container walk.

Two things this pass had to fix before the lock could be true:

- **`NavLink` is not on the nav bar.** It drives the *in-screen tab rows* (Hangar's Vessels /
  Overview / Training, Profile's Squad / Faction / Captains, the ability buttons) — 11 instances in
  `Menu_Main`, none of them a nav-bar link and none of them targeting a `MenuScreens` value. The
  nav-bar links carry a bare `EventTrigger`. It still adopts the shared model (a tab can be locked
  too), but it was never the component standing between the player and ARK/PORT.
- **`ArkLink` called `OnClickHangarNav`.** A copy-paste slip — `OnClickArkNav` was referenced by
  nothing in any scene — and it meant pressing ARK *navigated to the Hangar*, so `NavigateTo` was
  never asked about ARK and the screen could never take the locked state. Fixed by
  `Tools/Build/fix_ark_nav_wiring.py` (`--check`). The lock and that fix ship together or the lock
  is a lie.

## 3. Arena is the arcade, pointed at a different roster

There is **no second card-grid implementation**. `ArcadeExploreView` gained one field:

```csharp
[SerializeField] SO_GameList rosterOverride;   // empty = the injected arcade roster
SO_GameList Roster => rosterOverride ? rosterOverride : GameList;
```

Every consumer resolves through that one accessor, so nothing can read a different roster than
the cards were built from.

A parallel Arena screen would have had to re-derive progression locks, favourites, party picks,
the daily-challenge card and the whole launch modal — and would have drifted from all five. The
Arena modal is a **prefab duplicate** of the arcade one with its explore view pointed at an Arena
`SO_GameList`; the code is shared entirely.

## 4. The Toy Box drives the LIVE toys

`ToyboxModal` (`_Scripts/UI/Modals/ToyboxModal.cs`) is the app-shell face of the freestyle
toybox. Full mechanics live in `Docs/ToySystem/ARCHITECTURE.md` § "The app-shell face"; the short
version:

- Every card is an `IToyShellSurface` **registered by a real toy** standing out by the cell
  membrane. Pressing a row calls the same method the toy's ring calls — "change your domain" in
  the menu is literally `DomainChangerToySet.Apply`. There is no table of toy actions to fall out
  of step with the toys.
- The 2D art is the **encyclopedia's own baked emblem portrait**
  (`Resources/Codex.asset` → `ToyPortraitLibrary`), so a flat card is a picture of the thing the
  player flies at, and re-baking an emblem re-skins the menu with nothing to re-wire.
- Options can **expand** rather than act, because a toy is already a tree in the world (a matrix
  unfolds into stations; the Lifeform Matrix unfolds again into species and elements).
- An option that only means something with the player flying — Wanderway, Arkway, Connect the
  Dots — is marked `RequiresFreestyle`. The modal closes, enters freestyle through
  `MenuCrystalClickHandler`, waits for `OnGameStateTransitionEnd`, and only then applies. It waits
  on that event rather than on `IsInFreestyle` because the flag flips at the *start* of the
  transition, while the vessel's input is still paused and the camera is still blending.

## 4.1 Two windows, and the narrowing that produced them

The Toy Box is a **catalogue plus a detail window**, matching the Arcade's shape:

| Window | Modal type | What it is |
|---|---|---|
| `ToyboxModal` on `ToyboxScreenModal` | `TOYBOX` (13) | The grid. One card per live toy, straight off `ToyShellRegistry`. |
| `ToyConfigureModal` on `ToyboxGameConfigureModal` | `TOYBOX_CONFIGURE` (16) | One toy: title, category, description, a live picture of it, its **variants**, and two verbs — **Navigate** and **Switch**. |

**The detail window has two verbs, and the second one came back on purpose.** The first cut let
the menu drill into a toy's own options in place; the second removed that entirely, leaving
Navigate alone, on the argument that a menu which applies a toy's actions is a second authority on
what a toy does. Half of that argument survives and half of it was wrong.

*What survives:* the menu never gets its own copy of what a toy does. Every row calls the toy's own
`ToyShellOption.Apply` — "change your domain" here is literally `DomainChangerToySet.Apply`, the
call the ring makes. One implementation, two surfaces, which is the whole point of
`IToyShellSurface` and is not a second authority on anything.

*What was wrong:* a player who does not want to fly had no way to change their domain at all, and
"go and fly for it" is a tax rather than a design principle. So the window is an **in-UI toybox**,
and Navigate is still there for everyone who would rather go to the ring — §4.1.1 below, which held
this open as a real decision, is closed in the wire-it-in direction.

**Its own modal TYPE, not a panel inside `TOYBOX`.** Same reason the Maelstrom's launch panel is
its own window: a modal type is what `ScreenSwitcher` unwinds by, so gamepad B out of a toy lands
back on the grid rather than closing the Toy Box.

**The picture is the live toy** (`ToyPreviewCamera`) — a disabled camera stepped by hand onto a
RenderTexture, pointed at the object standing out by the cell membrane. It is ~150 lines against
the arcade preview's satellite-arena machinery because *a mode has to build its world to be
previewed and a toy is already standing in ours*. Three details are borrowed rather than
re-derived, each a bug otherwise: the UI layer is excluded from the culling mask (or it draws this
panel inside its own window), the clip planes are derived from the shot rather than copied from a
template camera, and the camera renders on demand — an enabled one would take a full extra pass
over a live prism ecology every frame.

**Navigate places the vessel BEFORE the transition, and that ordering is the whole of why the
arrival reads smoothly.** Entering freestyle is ONE eased camera blend (`MainMenuCameraController`,
smootherstep over `MenuCrystalClickHandler.TransitionDuration`) whose far endpoint —
`ComputeGameplayPose(target)` — is recomputed every frame from the vessel's live pose. So teleport
first and that single blend simply *arrives at the toy*: the player watches the camera fly there,
and nothing cuts.

It shipped the other way round first — close, toggle, then wait on `OnGameStateTransitionEnd` and
place — reasoning (correctly, as far as it went) that a pose written at the START of the transition
would be overwritten by the tail of the blend. It is not overwritten; the blend *tracks* it. What
the wait actually bought was a hard cut in **the worst possible place**: the camera eased for two
full seconds onto wherever the autopilot happened to leave the ship, handed over to the gameplay
camera, and only then did the world jump. The general shape is worth carrying: **when a transition
eases toward a target that is re-read every frame, move the target before the ease, not after it —
a teleport at t=0 is absorbed, a teleport at t=1 is the only thing the player sees.** Where the
active menu config is vessel-anchored the rig carries the jump itself
(`MainMenuCameraController.HandleAnchorDiscontinuity`, a >100u anchor delta), so relative framing is
preserved in the same frame; where it is the cell-anchored lava lamp the anchor never moved and the
blend is a clean sweep from the cell orbit to the toy.

**The arrival distance carries the COAST, because the ship is already flying.**
`TransitionToFreestyle` drops the autopilot and — with `lockInputDuringEnterTransition` — holds
input paused for the whole blend, so the vessel cruises at its minimum speed for those seconds,
pointed by construction straight at the toy. Navigate therefore stands it off by the intended
distance **plus** `VesselStatus.Speed × TransitionDuration`, and the pilot is handed the stick at
exactly the stand-off: *the coast is the approach*. Without that allowance the ship drifts into the
ring mid-blend and trips the toy before the player has touched a control — `Toy` arms on
`OnGameStateTransitionStart`, at the **top** of the transition, not at its end.

The stand-off sits **outside** the toy's own `SwitchRingRadius` (`arrivalDistanceFactor` > 1) and
faces it, so the player looks at what they chose and flies through the ring to use it; arriving
inside would trip the toy on the first frame, using it without ever seeing it.

**The approach is the toy's INWARD radial — from inside the cell.** The toybox rings its toys
around the membrane facing inward, so the inward radial is both the toy's own front and the side
the world is on: the player arrives looking at the toy with the whole environment behind it, flying
the way they would have flown there themselves. This shipped on the *outward* radial first, on the
reasoning that the membrane must not end up between the player and the toy — geometrically the same
shot, and it reads completely differently, because it parks the player *outside* the membrane
looking at a toy against empty space with the world they are about to enter hidden behind it. The
lesson is small and general: **for a camera placement, "which side has the subject" is not the same
question as "which side has the scene", and only the second one decides what the shot looks like.**
Inward is a *bounded* direction in a way outward was not — run far enough and the arrival is past
the core and out the other side — so the whole lane (stand-off plus coast) is capped at 80% of the
toy-to-centre distance rather than trusting the numbers to stay small.

`ToggleTransition` runs synchronously up to its first `await`, and both `_isInFreestyle = true` and
`OnGameStateTransitionStart` are on that side of it — so `IsInFreestyle` immediately after the call
is the honest answer to *did the toggle take?*. It is checked, because the toggle declines while a
transition is in flight or before the local vessel exists, and a refusal must not leave the ship
teleported across the menu with the autopilot still driving it.

### 4.1.1 Resolved: `BuildShellOptions` had eight producers and no consumer

It now has one. Eight toys implemented it and nothing called it; it was kept as the seam an
in-menu option list would plug back into, and flagged as a real decision rather than an oversight
— *either wire it into `ToyConfigureModal` as an optional list, or remove it from
`IToyShellSurface` and the eight toys.* The first branch was taken. `ToyConfigureModal` draws the
top layer as a scroll list of `ToyVariantCard`s, and the seam earned its keep exactly as written:
**no toy needed a line of code to appear in it.**

#### A row SELECTS; **Switch** COMMITS — except where the toy says otherwise

The two shapes are not a UI preference, they are what these applies *cost*.

| Shape | World form | Flat form | Why |
|---|---|---|---|
| `AppliesOnSelect = true` | a **flip-set** — the option IS a toy you fly through | the row is the act, no second press | instant, and undone by picking another row |
| `AppliesOnSelect = false` (default) | a **matrix** — you fly a station to commit | the row selects, **Switch** commits | a cell swap suctions the world away and grows another behind a veil; a stray tap in a scroll list must not start one |

Only `SwapToySetCoordinator` sets it today, which is the domain changer — so the three domain
cards apply on the press and everything else is select-then-Switch. **The toy declares it, the menu
does not decide it**, for the same reason `ToyDefinitionSO.Category` is declared in code: the cost
of applying is a property of what the option does, and a menu that ruled on it per toy would be a
second opinion about the toy.

Switch is **drawn only for a list that has something for it to commit**, so the domain changer
never shows a button that could never light up — an always-dead control reads as broken rather than
as unnecessary.

#### The picture answers "what IS that"

A list can say *Blob Cell*; only a picture says what that is. `ToyShellOption.BuildPreview` is the
optional seam — the toy builds a model of what the option would give you and the preview window
frames it — and it is optional because most options have nothing to show, in which case the window
keeps photographing the toy, which is still what Navigate would take you to.

Two toys fill it in, both by handing back **the same model their own station shows**, which is what
stops the flat preview and the world station drifting apart: `CellSelectorToy` its cached
`CellMiniatureBuilder` scale model, `VesselChangerToy` its `ToyVesselRoster` live mini hull.

Three details are each a bug if you get them wrong:

- The model is built on a **private stage** at `(0, −90000, 0)` — far outside Menu_Main's 8000 far
  clip, and on a different axis from the arcade's satellite arena at `+X 120000` so the two
  previews cannot photograph each other. Dropped in place it would be a mystery object hanging in
  the lava lamp, which the player is looking straight at whenever they are in freestyle.
- **No bloom-in.** `ToyPreviewCamera` renders one frame the instant it is handed a model and
  *measures* it to frame it; against a model still scaled to zero it would frame nothing, from far
  too close.
- **No idle spin.** The camera already orbits, and the two compose into a tumble.

The camera **measures** rather than being told a size, because each toy builds at whatever radius
its own stations use. And a failed build after a live one goes back to the toy: dropping the old
model and returning early would leave the camera framing a destroyed transform.

#### A branch opens in place, and the way back is a row

The Lifeform Matrix is a tree in the world — kingdom, then species, then element — so it is a tree
here. The way out is a **synthesized back row** at the top of the list rather than a control
somebody has to author, which also means it composes with the one card template the designer drew.
Only the **first** layer is ever rebuilt from the surface: a deeper one came from an option's
`Expand`, a closure belonging to a list the rebuild would replace, and those layers are trees of
authored content rather than live state.

#### Two lifetime traps this window has to answer

**A destroyed toy still passes `!= null`.** Every surface is a MonoBehaviour but the field is typed
as the interface, so the null check is a plain reference comparison and keeps answering true after
a cell swap has destroyed the toy — at which point reading `ShellAvailable` throws rather than
returning false. `LiveSurface` does Unity's own lifetime check against the MonoBehaviour, and
everything reads through it.

**Switching a cell from this window destroys the toy that offered the switch.** That is now the
*ordinary* path, not an edge case: the swap tears the toybox down and builds it again, and a NEW
surface speaks for the same toy. So the window subscribes to `ToyShellRegistry.OnChanged` and
re-binds by **definition**, falling back to display name for the code-built default toybox whose
definitions are `CreateInstance`d per build and match no earlier reference at all — the same
two-step, for the same reason, that `ToyPortraitLibrary` uses to find a toy's codex page. The
definition itself is captured at bind, because it is an *asset* and is the one thing about a
torn-down toy that survives.

(`ToyOptionCard`, the UI component that drew the original rows, was deleted with the reshape and is
not resurrected. `ToyVariantCard` is a smaller thing against a smaller card: fill, rim, name, and an
optional detail line.)

#### Colour: the rim is brighter than the base, in every state

A variants list is usually **one toy's accent repeated down the whole column** — the cell selector
paints every world in the selector's colour — with the domain changer as the exception that
genuinely gives three. So the fill alone cannot say which row is selected, and the border carries
that instead; both are driven from the one accent rather than authored separately.

The rim stays brighter than the base at rest *and* selected. That is the invariant `Docs/PALETTE.md`
§4.0 states for the prism tiers, where it held on nine of twelve tier×domain pairs by accident
rather than by rule and each violation was separately rationalised before being recognised as one
defect. A card is a different surface; the reading is one the player has already learnt.

The rest fill is **muted, not dark** (0.45 of the accent, selected 0.80). The Toy Box grid draws
its cards at the full accent, and a variants list two shades below that reads as a *disabled*
version of the same product rather than as a different part of it. Only RGB is written — each
graphic's authored alpha is captured once and put back, so a translucent plate stays translucent.

#### The selected row also LIGHTS and LIFTS, and both come from the HUD's own style asset

Tint alone is a small signal on a card whose neighbour is 20 units away, so the selected row gains
a **glow behind it** and a **1.04 lift**. The glow is `AbilityLockupStyleSO.bloomSprite` — the same
sprite the ability lockup puts behind an upgraded card and the goal stack puts behind its plate — so
the Toy Box reads as one product with the HUD rather than as a menu that invented its own idea of
"selected". Reading a HUD style from a menu surface is the established pattern here and not a new
coupling: the arcade card's ability preview does the same thing for the same reason
(`Docs/ArcadeLaunch/ARCHITECTURE.md`).

Four details are each a decision:

- The bloom is **built lazily**, only on a card that is actually selected, and a missing style asset
  latches so a list with no asset behind it does not walk `Resources` once per row per redraw.
- It is a **first sibling**, so it sits behind the authored art instead of over it, and its padding
  is **12** rather than the lockup's 26 — the lockup's number is sized for a HUD card standing
  alone, and a grid neighbour is 20 units away.
- Its **hue comes from the option and its alpha from the style**: the glow says which row, the
  product says how bright. On the domain changer that means the light itself says which domain.
- `ApplySelection` is **idempotent** — re-binding the same state re-tints without restarting the
  tweens (the accent can change under a row when a domain is re-picked), so a redraw cannot make a
  settled card flicker. `OnDisable` kills both tweens and snaps to rest, because a pooled card is
  hidden mid-tween every time the layer changes under it.

### 4.1.2 The arrival raises the platform's own objective arrow

`ToyNavigationBeacon` points the standard `ObjectiveIndicator` at the toy for the length of the
trip, reusing `PaintingRunner`'s pattern rather than inventing a second one: ONE indicator, created
at the **canvas root** (the widget stretches to its parent and clamps to that rect's edges, so a
mid-hierarchy container pins it in a corner), driven by a relay so the target can change without
rebuilding the widget.

It is usually invisible on the frame it is raised — Navigate lands the vessel facing the toy, and
the indicator hides itself whenever its target is on screen. It earns its place on the frames after
that, and it **takes itself down** on arrival (inside 3.5 ring radii), on leaving freestyle, or after
90 s. An arrow left up once the player has moved on is noise, not guidance.

## 4.2 A modal closes without being disabled — so the reset rides `OnModalClosed`

`ToyboxModal` holds a layer stack (grid → a toy's options → a nested layer), and that stack has to
come back to the grid whenever the window goes away. The obvious place to put that is `OnEnable`
or `OnDisable`, and **both are wrong**: a `ModalWindowManager` closes by fading its `CanvasGroup`
and stays ACTIVE, so neither message fires on open or close — a reset written there runs once at
scene load and never again, and the player who backed out three layers deep finds them still there
next time.

Nor is the close button enough. `OnCloseModal` is only *that* control's route; the freestyle
handoff, gamepad B and `ScreenSwitcher.CloseAllModals` all go through
`ModalWindowManager.ForceCloseImmediate`, and none of them knows this modal holds a stack. The
subscription is therefore to the modal's OWN `OnModalClosed` event, which every close route raises
— one subscription instead of one rule per caller. That is the same fix, for the same reason, that
`ArcadeGameConfigureModal` makes for its preview window and launch panel.

The one place `OnDisable` still matters is the freestyle handoff, which is deliberately NOT
cancelled there: a handoff closes this window as its first act, and `SetActive(false)` *is* a close
route in this project (`ModalWindowIn` carries an externally-deactivated recovery path for it), so
cancelling on disable could kill the deferred toy action on exactly that route. It is cancelled on
destroy, and superseded when a second handoff starts.

## 5. Scene wiring checklist

The UI itself is hand-designed. What the code needs:

> **Run `FrogletTools ▸ Interface ▸ Home Hub Wiring` first — it does most of this list.** The hub
> screens were authored by duplicating the Arcade's, which is the right way to get the LAYOUT and
> the wrong way to get the WIRING: measured on the authored scene, all four hub buttons called
> `ScreenSwitcher.OnClickArcadeNav`, all four screen modals declared `ModalType = ARCADE`, and none
> of the three new windows was in the switcher's `Modals` list — so every button opened the Arcade
> and `OpenModal(TOYBOX)` had nothing to find. The tool repoints all of it through
> `SerializedObject`/`AddComponent` (never hand-edited YAML) and its read-only twin,
> `python3 Tools/Build/wire_home_hub_scene.py --check`, proves the result from outside the editor.
> Since the slot-filling pass it also **adapts the two duplicated Arcade windows into the Toy
> Box's own** and binds every serialized reference below: it converts one inherited `GameCard` into
> a `ToyCardTemplate`, gives the grid a wrapping `GridLayoutGroup` in place of the arcade's
> row-of-four nesting, creates the empty state, puts `ToyPreviewCamera` on the arcade's own preview
> `RawImage`, re-captions the launch button NAVIGATE, converts the card inside the detail window's
> scroll view into a `ToyVariantTemplate`, finds the SWITCH button, and switches off the arcade
> content a toy has no use for (the vessel picker, the intensity / player-count / domain-count
> steppers, the objective box). **The party roster and friends column are deliberately NOT among
> them** — they
> are kept ON in all four hub windows (§5.2). Arcade **branches** are switched off rather than
> deleted — re-activating a GameObject is a cheaper mistake to undo than re-authoring one — and only
> a component that would actively fight for an object the Toy Box KEEPS is removed:
> `ArcadeExploreView` would drive the very same grid off an `SO_GameList`, and `ModePreviewWindow`
> would stand a satellite arena up behind a toy.
>
> What is left for a human is the LOOK: sizes, sprites, and where each rect sits.

**ScreenSwitcher**
- [ ] Add the Toy Box / Arena / Mission `ModalWindowManager`s to the `Modals` list. The switcher
      finds a modal by its `ModalType`, so that list is the registry — and it is also what
      `CloseAllModals` iterates to `ForceCloseImmediate` every window before a flight, so a modal
      left out of it is one that stays on screen over the ship.

**Home screen**
- [ ] One `MenuHubButton` per entry, each with its `target` set. For Arena/Mission the state and
      its art live on the **`MenuAvailabilityView`** the button ensures on itself (§2.1), not on
      `MenuHubButton` — set `availability` and wire `lockedOverlay` / `unavailableOverlay` there.
      Leaving the overlays empty is legal: the view falls back to dimming the entry's own graphics.
- [ ] Author them on the SCENE's `HomeScreen` object (`Menu_Main`, GameObject `HomeScreen`), not
      on `_Prefabs/UI Elements/Main Menu Screens/HomeScreen.prefab` — that prefab is instanced by
      nothing, and its five persistent `onClick`s still name `OnClickSmash`/`OnClickSoar`/
      `OnClickSport`, an earlier three-way home hub whose methods `HomeScreen.cs` no longer
      declares. It is a snapshot of the screen this feature replaces, not the screen itself.

**Toy Box modal** (`ToyboxModal`, `ModalType = TOYBOX`) — all five filled by the tool
- [ ] `cardGrid` — the grid row the cards are instantiated into
- [ ] `cardPrefab` — a `ToyboxCard`. The tool converts one inherited arcade `GameCard`
      (`ToyCardTemplate`, parked inactive on the modal root) rather than building one from nothing,
      so the Toy Box looks like the rest of the menu with nobody re-authoring a card.
- [ ] `emptyState` — shown when no toy has registered yet
- [ ] `configureModal` — the detail window below
- [ ] `screenSwitcher` — the base `ModalWindowManager` slot. **Note it is the BASE's**: `ToyboxModal`
      deliberately declares no field of that name, because Unity refuses to serialize the same field
      name in a class and its parent and reports it as *"The same field name is serialized multiple
      times"* — a runtime error, not a warning.

**Toy Box detail window** (`ToyConfigureModal`, `ModalType = TOYBOX_CONFIGURE`) — twelve slots
- [ ] `titleText` / `descriptionText` / `categoryText`
- [ ] `preview` — a `ToyPreviewCamera` on the preview `RawImage`
- [ ] `navigateButton` — go and fly it; `backButton` — back to the grid
- [ ] `crystalClickHandler` — the scene's `MenuCrystalClickHandler`. **Required**: without it
      Navigate can only warn, because entering freestyle is that component's job.
- [ ] `screenSwitcher` — the base slot again
- [ ] `variantsRoot` / `variantContent` / `variantCardPrefab` / `switchButton` — the variants list
      (§4.1.1). The content is resolved through the **`ScrollRect`'s own `content`**, never by
      looking for a child called `Content`: the modal's own root is *also* called `Content` and is
      found first, which would bind the whole window as the card parent.
- [ ] The **Switch** button is looked for by name (`Switch Button` / `SwitchButton` / `Switch`),
      then by **caption**, and only then as a spare launch button — in that order, and it says so
      in the log when it guesses. The designer makes this control by duplicating the one beside it,
      so its name is whatever the duplicate inherited and the only thing that reliably says which
      button is which is the word on it. It is resolved **before** the sweep that retires every
      leftover launch button, and spared from it, or the tool would switch off the designer's
      second button the first time it ran after they added it.

      These four are **reported rather than required** by `wire_home_hub_scene.py --check` while
      all four are empty — the scroll view is hand-authored UI and an un-wired feature is an honest
      state on a checkout where it has not landed. The moment **any one** of them is filled the
      group becomes required in full, because a half-wired list draws rows into nothing or draws
      them with no way to commit. It arms itself; nobody has to remember to switch it on.

**Every new script needs its `.meta` committed.** A `.cs` file pushed without one has no stable
GUID: the editor mints a fresh one per machine, so every scene reference the wiring tool wrote
points at a GUID that exists on exactly one computer and reads as *Missing (Mono Script)* for
everybody else — with nothing in the scene diff to say why.
`wire_home_hub_scene.py --check` fails on it by name.

**Arena modal**
- [ ] Duplicate `ArcadeGameConfigureModal.prefab`, set its `ModalType` to `ARENA`
- [ ] Point its `ArcadeExploreView.rosterOverride` at the Arena `SO_GameList`
- [ ] Its `MenuAvailabilityView` starts `Locked` (the `MenuHubButton` reads it)

## 5.2 The party roster and friends column live on every hub window

They are the one part of the duplicated Arcade screen that a Toy Box, an Arena and a Mission all
genuinely want: **who is with you does not change with which thing you are about to play.** So the
wiring tool ensures both are ACTIVE on all four screen modals rather than retiring them with the
rest of the arcade's launch furniture.

They need no syncing of their own, and that is a property of the architecture rather than luck:
`ArcadeLobbyList` and `FriendsListPanel` read `HostConnectionDataSO` and `FriendsDataSO` through
SOAP lists and events, so four copies of the view show one state by construction. Four *sources*
would have to be synced; four *views* of one source cannot disagree.

## 5.3 An inherited persistent `onClick` is a functional defect, not clutter

The Toy Box's Navigate button is the Arcade's launch button repurposed, so it arrives carrying that
modal's persistent `onClick` calls. `UnityEvent.Invoke` builds its call list as **persistent then
runtime** and guards neither, so one throwing entry eats every `AddListener` handler behind it —
which presents as a button that is lit, raycasts correctly, reports `interactable = true`, and does
nothing. The tool strips every persistent listener on that button except `MenuAudio.PlayAudio`,
which is the press sound and the one reviewed entry on the project's persistent-listener
allow-list.

`ToyConfigureModal` binds its two controls from **`Start` as well as `OnEnable`**, idempotently, for
the same class of reason: a modal that is already active at scene load runs `OnEnable` before
anything has bound it, and this window is wired by a tool rather than by hand — *"the button did
nothing" must not be able to come down to which of the two ran first.* The press itself is traced on
the `ToyBox` log channel (FrogletTools ▸ Toolbox ▸ Logging), off by default, so the next time it is
silent the question "did the press even arrive?" is one toggle away.

### 5.3.1 So is an inherited COMPONENT — and that one no listener sweep can see

Measured on the authored scene *after* the designer's own cleanup pass, the Toy Box's Navigate
button still carried two arcade components. Neither shows up as an empty slot, neither is a
persistent listener, and both are live:

| Component | What it does here | What the tool does |
|---|---|---|
| `WeeklyChallengePlayButton` | writes `Button.interactable` from `WeeklyChallengeService` on enable and on every challenge change | **deleted** |
| `ControllerButtonPress` | declared `ARCADE_GAME_CONFIGURE`, so a pad press inside the **Arcade's** modal invoked **this** window's Navigate | **retargeted** to `TOYBOX_CONFIGURE`, and given the CanvasGroup guard it shipped without |

The first *fights `ToyConfigureModal` for the same property* — and when there is no valid weekly
challenge it simply switches Navigate off, with nothing on screen to say why. That is the criterion
this pass already used for `ArcadeExploreView`, so it is deleted by the same rule.

The second is worse and subtler: a modal in this project **closes by fading and stays ACTIVE**, so
the toy window's `Update` runs the whole time the Arcade is open — and `canvasGroup`, the guard that
would have caught exactly this, is left unwired (the component's own source carries a TODO saying
so). A pad press in a window the player is not looking at would teleport their vessel and enter
freestyle. It is **retargeted rather than deleted**, because the pad shortcut is wanted; it was just
aimed at the wrong window.

**The general rule: a duplicated control inherits BEHAVIOUR, not just wiring.** A listener sweep
answers "what does this button call"; it cannot answer "what else is running on it". When a screen
is authored by duplicating another, audit the component list too — and note that `ControllerButtonPress`
is matched by TYPE NAME rather than a compile-time reference, so this tool takes no dependency on a
class it only wants to point somewhere else.

The read-only twin checks both from outside the editor, and it has to parse the list the hard way:
**Unity serializes a `List<SomeEnum>` as a packed little-endian int32 hex blob**, not a YAML
sequence — `ActiveModalWindows: 01000000` is one entry with the value 1, and an empty list is an
empty string. A `- 1` style regex reads every such list as empty, which would make the audit pass on
exactly the scene it exists to catch.

### 5.3.2 The tool CREATES the Switch button rather than reporting it missing

A window with a variants list and no Switch can select and never commit, which is the worst of the
three states — so when no button is found by name, by caption, or as a spare launch button, the tool
duplicates Navigate. Same art, same size, same band, shifted one width left **by anchor** (the
button is anchored to a fraction of its parent, so a pixel offset would drift with the window while
an anchor shift keeps the pair together at every resolution). Where it finally *sits* is a look
decision and stays the designer's; the default only has to not overlap.

The clone is taken **after** the two inherited components above are dealt with, so it never carries
them — and its own `ControllerButtonPress` is removed even so, because a pad binding names one
button and two buttons answering to it would fire both: a teleport and a world swap from a single
press.

### 5.3.3 `categoryText` is optional, and the checker says so

The category (Pilot / World / Creation) bound to the arcade's `Header`, which this window's
authoring deleted. It is **not** demanded back: the Toy Box **grid card already shows it**, and
`ToyConfigureModal` null-guards the label. The tool accepts `Header`, `Category` or `Toy Category`
and binds whichever exists; the checker reports the empty slot as a `~` line rather than failing.
*A gate that argues with the design is a gate that gets ignored.*

## 5.4 Copy: the card gets a line, the window gets a paragraph

`ToyboxCard` shows `ToyPortraitLibrary.Tagline` (the codex tagline, falling back to the toy
definition's own line). `ToyConfigureModal` shows **`ToyPortraitLibrary.Body`** — the codex's
`CodexEntry.Description`, which is body copy the harvester explicitly never writes — falling back to
the same one-liner for a toy the codex has not been scanned for. That adds no second place to
describe a toy: the encyclopedia already owns authored prose per page, and a detail window and a
card simply want different lengths of it.

### 5.4.1 The type scale is the arcade's, and the arcade was labelling a card grid

Three labels ARE this window's left column — the toy's name, its paragraph, and the header over
the variants list — and all three arrived at the size a duplicate of the Arcade's configure modal
gives them, where the title captions a grid of cards and the description is a footnote under a
picture. Measured on the authored scene: name **43.2** fixed, header **36** fixed, description
autosizing **14..36**, and the variant card's own name **27.36** fixed. The tool re-sizes each
(`SizeType`) to `42..58`, `34..44`, `22..44` and `22..34`.

**Every one is a BAND, not a size**, and that is the load-bearing half. A toy's name runs from
"Wanderway" to "Connect the Dots"; its description is authored codex prose of no fixed length; a
variant's name is a domain, a hull, or a cell config's own asset name. A fixed size is a promise
that content cannot keep, and it breaks by CLIPPING — which reads as a broken label rather than as
a long one. A band takes its ceiling when it fits and steps down when it does not. The band also
forces `TextWrappingMode.Normal`: with nowhere to wrap, autosizing answers a long line by shrinking
it to nothing, which is the same failure wearing a different costume.

`SizeType` writes only when the band differs, so re-running the tool on an authored scene reports
nothing and marks nothing dirty.

**The title is resolved inside `GameView` specifically.** The variants header beside it is *also*
called `Game Name` — the designer built the column by duplicating the one next to it — so a search
by name alone answers with whichever is earlier in the hierarchy, which is a fact about sibling
order rather than about the labels. The header is then taken as *the `Game Name` that is not the
title*, and the modal's `titleText`/`descriptionText` are bound from the very references the type
pass sized, so what the tool binds and what it sizes cannot be two different objects.

### 5.4.2 The variant card, and the eleven-unit strip it inherited

The card template is a duplicate of the Arcade's game card, so its `GameTitle` sits in the rect
that card put a title in. On the toy window's `275 x 100` grid cell that resolves to an **11-unit
strip near the top** which a 27pt line overflows downward — a caption that has slid off its own
card, and most of why the first pass read as unfinished. `ShapeVariantCard` gives the name the
cell's upper band (`0.4..1`, inset 18) and adds the **`GameDetail`** line under it (`0..0.4`).

That second line is *created* rather than demanded from the designer, because it is the one thing a
row says that its name cannot — "current", "flying", a painting's progress. `ToyVariantCard`
already reads `ToyShellOption.Detail` into it; unwired, every row is a list of names with no state
in it. The anchors are fractions rather than pixels: the cell size is the designer's to change, and
a layout authored in pixels stops being a layout the moment they do.

`ShapeVariantCard` runs on **every** pass, not only the conversion. The template survives a re-run,
so a layout fix that only ran at conversion time would never reach a scene the tool had already
touched — which is every scene that matters. It also owns the component add and all four slot
binds, so the conversion path and the re-run path cannot produce two different cards.

### 5.4.3 The variants list could not scroll, and a row below the fold was DEAD

The authored `Content` sits at a stretch-x, top anchor with a zero size delta and a
`ContentSizeFitter` whose fits are **both Unconstrained** — so its height is zero however many rows
the grid lays into it, and a `ScrollRect` scrolls a content RECT, not the children inside it. The
list therefore draws every row it is given and can only ever *reach* the ones already inside the
viewport. The rest are clipped by the viewport's `Mask`, which cuts the drawing off and, being an
`ICanvasRaycastFilter`, eats the press too: **invisible and unpressable, from one cause.**

That is the arcade grid's own bug (CLAUDE.md, "Trusting an authored `ScrollRect` Content height"),
reached from the other direction — there the content had a height and the wrong one, here it has
none at all. It matters at the sizes this window actually runs: a `275 x 100` cell with `30`
spacing, three columns and `50` top padding puts four rows at 570 units against a ~478-unit
viewport, and the cell selector alone offers ten worlds.

`ShapeVariantsList` sets the vertical fit to `PreferredSize` and switches **horizontal scrolling
off** with it — the grid has a fixed three-column count in a content that stretches to the
viewport's width, so there is never anything to reach sideways and leaving it on only lets a drag
slide the whole list off its own column.

## 5.1 Known: five dead wirings in the doomed migration prefab

Stripping the arcade's two-screen path removed `OnConfirmConfiguration`,
`OnBackFrom{GameSelectView,VesselSelectionClicked,SquadMateSelectionClicked}` and
`OnPrevious/NextShipClicked` from `ArcadeGameConfigureModal`. Five persistent `onClick`s still name
them, all in `_Prefabs/MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab` — an asset referenced
by nothing outside its own folder, which already carried a dead wiring of exactly this kind
(`OnBackFromGameSelectView`) before this branch. The live `ArcadeGameConfigureModal.prefab` and
`Menu_Main` are clean; verified with `Tools/Build/audit_persistent_listener_injection.py`, which
reports dead wirings and whose set is otherwise unchanged from bleeding-edge. Editing a prefab
named DELETE LATER to repair references into code it is scheduled to outlive was judged scope
creep, not diligence.

## 6. What the arcade strip removed

The scene has run the one-panel launch layout for a while, with the legacy
configure-then-pick-a-vessel path nulled out and inert. It is now gone from the code as well:
Screen 1 / Screen 2 roots and their switching, the vessel picker (next/prev ship, the ship summary
view), the Confirm and Back buttons, the d-pad row highlights, the duplicate
`shipClassTypeVariable` (the same asset `GameDataSO.VesselClassSelectedIndex` already points at),
and `ArcadeConfigSyncManager`'s screen-change RPC, whose only caller was that navigation.

Two things deliberately **stayed**:

- **`UsesLaunchPanels`.** It is no longer a layout choice — a panel is the only place the intensity
  row, the domain tiles and the Start button live — but the scene holds a SECOND copy of
  `ArcadeGameConfigureModal` on the Maelstrom's own window, which carries it purely as a
  `ModalWindowManager` and wires no panels. Both copies subscribe to the sync manager's
  broadcasts, so the gate is what stops the panel-less one "opening" on every client.
- **The player-count and domain-count steppers.** They look legacy (they live under the old
  `ConfigurationDetailView`) and they are not: the scene's `MinigameLaunchPanel` was added onto
  `ConfigurationContent`, the parent of that root, which is still active — the steppers are on
  screen and driving live config.

The matching dead YAML went with it: every `UnityEvent` persistent call to a deleted method (which
logs an error on every press) and every prefab-instance modification naming a deleted field (which
Unity never prunes). The buttons those calls were on still exist and are now inert — delete them
in the editor when the layout is redesigned.

**Left alone, deliberately, as out of scope:** `configChangedEvent` / `RaiseConfigChanged()`. The
channel is raised and nothing in the project subscribes to it, in code or in any scene — a
candidate for removal, but a SOAP integration point rather than part of the two-screen path.

## 7. The plate art is GENERATED, and it is ONE sprite for eight elements

The chamfered frame behind every hub entry is a single sprite — `HomeScreen/Play Button.png` —
authored by `Tools/Build/author_menu_hub_button_sprite.py` (`--check`). Four facts about it are
load-bearing:

**It is drawn SIMPLE (stretched), at eight different rects.** Besides the four hub entries at
312.64×70.30, the same guid is on `Play Button`, `Navigate Button` and `SupportUs`, each
anchor-stretched to its own parent. So the sprite's **aspect is not the hub's to change** — it is
kept at the shipped 272:72 — and the file is replaced **in place**, same guid, same `.meta`, so
nothing re-wires and no consumer moves.

**Resolution was the actual defect.** A 272×72 source stretched into a 312-wide rect is upscaled
on every display: the same trap `Docs/GAME_MODE_TOPBAR.md` records for the goal stack's first cut
("a 112×36 PNG stretched to 312×48 and read exactly that blurry"), at almost the same width. It is
now authored at **4× (1088×288)** from analytic coverage of a convex SDF, so the rim is one pixel
of anti-aliasing at 1080p and still one at 4K. Both dimensions are multiples of 4, so block
compression stays available.

**The design is a plate plus an OFFSET ECHO frame, and the shipped art cropped the echo.** Its
right side and bottom-right corner ran off the canvas — those are the stray lines that appeared to
hang out of the button's edge. Both frames now close inside the canvas, and the tool **asserts**
that nothing reaches the canvas edge. That assertion is not decoration: it caught the outer halo
re-introducing the same crop as a glow, because `exp()` never reaches zero (it left 0.11 alpha on
the edge). The falloff is a raised cosine, which does.

**What it borrows from the rest of the UI.** The rim carries the ability lockup's **graded band** —
solid the whole length of each 45° chamfer, then wrapping around the corners onto the horizontals,
where it grades down. It grades to a **floor**, not to nothing: the lockup's plates are borderless
and this is a closed frame, so a rim that reached zero would open it, and a floor set too low stops
the shape reading as a frame at all and reads as a slab (measured at 0.40 — it did; it ships at
0.62). The rim is lifted toward white above the body per `Docs/PALETTE.md` §4.0, asserted. The
outer bloom buys its glow with dim **area** rather than intensity, per §3.

Two smaller things the generator fixes for free: the body is a vertical falloff with a soft inner
glow instead of a flat 30% wash, held at the shipped art's mean weight so the plate keeps its
presence and the centre band stays the calmest part (the label stretches over the whole rect and is
centred on it); and **fully transparent pixels carry the local ramp colour instead of black** —
Unity filters RGB independently of alpha, so a black transparent pixel darkens whatever edge it is
filtered into, and the shipped art had them.

**The colour ramp is SAMPLED, not re-picked.** The pale-to-cyan gradient is taken from the shipped
art's own body wash (the ~0.3-alpha region, so neither rim nor echo contaminates it) and frozen in
the tool as 33 stops — reproducible with `--dump-ramp`. Frozen rather than re-read because the tool
overwrites the file it would sample, which would make the ramp a function of the last run and let
it drift on every one. So "better" cannot quietly become "a different colour".

> **If this plate is ever redesigned rather than re-rendered, generate it instead of sprighting it.**
> `TrapezoidGraphic` already draws this family of shapes as geometry (`Docs/ABILITY_LOCKUP.md`,
> `Docs/GAME_MODE_TOPBAR.md` §2), which removes the stretch entirely — a chamfer has no 9-slice, so
> a sprited one freezes its slant into the art and is exact only at the size it was exported at.
> This pass stayed a sprite because eight elements draw it at four aspects and a swap is a scene
> change; the resolution and the crop were the reported problem, and both are asset-side.
