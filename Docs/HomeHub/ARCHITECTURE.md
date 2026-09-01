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
Available    → opens its modal
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

## 5. Scene wiring checklist

The UI itself is hand-designed. What the code needs:

**ScreenSwitcher**
- [ ] Add the Toy Box / Arena / Mission `ModalWindowManager`s to the `Modals` list. The switcher
      finds a modal by its `ModalType`, so that list is the registry.

**Home screen**
- [ ] One `MenuHubButton` per entry, each with its `target` set and, for Arena/Mission, its
      availability + overlay wired.

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
- [ ] Its `MenuHubButton` starts `Locked`

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
