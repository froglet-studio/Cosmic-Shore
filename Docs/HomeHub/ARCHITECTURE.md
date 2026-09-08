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
| `ToyConfigureModal` on `ToyboxGameConfigureModal` | `TOYBOX_CONFIGURE` (16) | One toy: title, category, description, a live picture of it, and **Navigate**. |

**The detail window has one verb, and that is a deliberate narrowing.** The first cut let the menu
drill into a toy's own options in place — a breadcrumb stack over
`IToyShellSurface.BuildShellOptions`, so "change your domain" was *applied from the menu*. That
made the app shell a second authority on what a toy does, which is the failure the single-writer
rule exists to prevent, and it sat awkwardly against the toys being diegetic in the first place.
Navigate replaces it: the player is put in front of the real ring, and from there the toy is the
only thing that acts on the world.

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

**Navigate is a three-step handoff**: close, enter freestyle through `MenuCrystalClickHandler`,
and place the vessel only once `OnGameStateTransitionEnd` has fired. Waiting on `IsInFreestyle`
instead would place it at the START of the transition, while input is still paused and the camera
still blending — the pose is then overwritten by the tail of the blend and the player arrives
somewhere else. The arrival sits **outside** the toy's own `SwitchRingRadius`
(`arrivalDistanceFactor` > 1) and faces it, so the player looks at what they chose and flies
through the ring to use it; arriving inside would trip the toy on the first frame, using it
without ever seeing it. The approach direction is the toy's **outward radial** from the cell
centre, because the toybox rings its toys around the membrane facing inward.

### 4.1.1 Open: `BuildShellOptions` has eight producers and no consumer

Eight toys implement it and nothing calls it any more. It is deliberately **kept**, not deleted:
it is the seam an in-menu option list plugs back into, and the configure window is its obvious
home if a toy is ever given menu-side choices. But it is the mirror of the "authored copy with no
producer" smell this project has burned itself on before, so it is a real decision, not an
oversight — either wire it into `ToyConfigureModal` as an optional list, or remove it from
`IToyShellSurface` and the eight toys. `ShellDefinition` and `ShellAvailable` are load-bearing and
stay regardless. (`ToyOptionCard`, the UI component that drew those rows, had zero references
after the reshape and was deleted.)

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
> What is left for a human is the ART: which rects, which sprites, which text objects go in the
> serialized slots below.

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

**Toy Box modal** (`ToyboxModal`, `ModalType = TOYBOX`)
- [ ] `cardGrid` + `cardPrefab` (a `ToyboxCard`) — the toy grid
- [ ] `gridView` / `optionView` — the two roots it swaps between
- [ ] `optionGrid` + `optionPrefab` (a `ToyOptionCard`), `optionTitle`, `backButton`
- [ ] `crystalClickHandler` — the scene's `MenuCrystalClickHandler`. **Required** for the three
      flight toys; without it those options report that they cannot run rather than half-running.
- [ ] `emptyState` — shown when no toy has registered yet

**Arena modal**
- [ ] Duplicate `ArcadeGameConfigureModal.prefab`, set its `ModalType` to `ARENA`
- [ ] Point its `ArcadeExploreView.rosterOverride` at the Arena `SO_GameList`
- [ ] Its `MenuAvailabilityView` starts `Locked` (the `MenuHubButton` reads it)

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
