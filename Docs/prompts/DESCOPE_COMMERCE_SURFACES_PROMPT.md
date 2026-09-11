# Prompt — de-scope the commerce surfaces for the invite build

Paste everything below into a fresh session.

---

Give the store, episode and purchase surfaces a deliberate **coming-soon** treatment. Nothing in
this window sells anything — all IAP and web checkout is cut behind the paid-EA gate, and the
checkout flow still carries an unresolved entitlement-verification gap — but the surfaces that
would take money are still live, fully styled, and reachable from the main menu.

This is checklist item **C4** (3.0 person-days). It is doubly load-bearing: the invite build is
the first thing anyone outside the studio sees, and a purchase screen that half-works reads as
broken rather than as unfinished.

Read `Docs/STEAM_RELEASE_TASKS.md` (item R4) and `Docs/MENU_PROGRESSION_AND_IAP.md` first.

## Do not invent a locked look — one already exists and is written to be the only one

`Assets/_Scripts/UI/Elements/MenuAvailability.cs` and `MenuAvailabilityView.cs` are the pattern.
Read the class comment on the view before you touch anything; it states the design directly:

> *The ONE place a `MenuAvailability` becomes pixels and a response. Every menu entry that can
> ship before it is finished … carries one of these rather than growing its own locked look.*

Three states, and the distinction is the whole point:

| State | Means | Behaviour |
|---|---|---|
| `Available` | Does its job | Opens its modal / navigates / selects |
| `Locked` | **This exists and you cannot open it yet** | Stays pressable on purpose — the press is how the player is told, so it refuses with a sting and a reason |
| `Unavailable` | This is not built | Inert |

It already drives the home hub (`MenuHubButton` — Arena is `Locked`, Mission is `Unavailable`),
nav-bar links for screens in `ScreenSwitcher.disabledScreens`, and `NavLink` tabs. It needs **no
authored art**: with no overlay and no label wired it dims the host's own `Graphic`s, and it
**tints rather than disables**, because an absent graphic does not raycast and switching one off
would silently delete the touch target the entry still needs in order to be pressable enough to
refuse. Extending this to the commerce screens is the whole job.

## What is live today (verified 10 Sep 2026 — re-verify, this will drift)

| Surface | File | State |
|---|---|---|
| Store screen | `Assets/_Scripts/UI/Screens/StoreScreen.cs` | Live, reachable — `MenuScreens.STORE` is a `ScreenSwitcher` entry |
| Episode screen | `Assets/_Scripts/UI/Screens/EpisodeScreen.cs` | Live, lazy `LoadView()` on panel toggle |
| Purchase card | `Assets/_Scripts/UI/Elements/Buttons/PurchaseCard.cs` | Live |
| Purchase confirmation | `Assets/_Scripts/UI/Modals/PurchaseConfirmationModal.cs` | Live |
| Hangar captains | `Assets/_Scripts/UI/Views/HangarCaptainsView.cs` | Live, references purchase flow |

**The only coming-soon treatment anywhere in the codebase today** is on quest cards —
`QuestItemCard.cs:112` and `QuestTrackView.cs:438`, both `quest.IsPlaceholder ? "Coming Soon" : …`.
That is the entire inventory. Everything in the table above is unmarked.

## Decisions to make, and the one I would make

**Vessel unlocks are not commerce and must keep working.** Crystals are a soft currency earned
from match placement and spent on vessel unlocks — `Docs/ECONOMY_TABLES.md` records the payout
table as applied in code and assets. That loop is part of the invite build. Only the **real-money**
path (episode tokens, the checkout, anything that would charge) is being de-scoped.

So do not blanket-lock the Hangar. Separate the two economies and lock only the token side.

**Per surface**, my recommendation:

- **Store screen** → `Unavailable`. Nothing in it is purchasable this window, so *this is not built*
  is the honest state, and it takes the nav entry out rather than teasing it.
- **Episode screen** → `Locked`. Episodes exist as content and the entitlement is real; the player
  genuinely cannot buy one *yet*. That is exactly what `Locked` says.
- **Purchase confirmation modal** → must become unreachable rather than restyled. If any path can
  still open it, that path is the bug.
- **Hangar captains** → leave crystal spending alone; lock only the token-purchase affordance.

State your reading in the PR body if you disagree — but do not leave a surface half-treated,
which is the state being fixed.

## Constraints

- **Do not delete the screens or their code.** They come back at the paid-EA conversion. This is a
  presentation state, flipped by one serialized field, per the view's own docstring: *"Flip to
  Available when the thing behind it is ready — nothing else has to change."*
- **Do not add a second locked look.** If `MenuAvailabilityView` cannot express something a
  commerce screen needs, extend that component — do not special-case the screen.
- Anything you cannot do outside the editor, write as a **tool** under `FrogletTools/` per
  `Docs/TOOLING.md`, and record its output with `FrogletToolChangeLedger.Record` plus
  `FrogletToolShipPanel.Draw` — a wirer whose asset output never lands is the failure mode that
  doc exists to stop.
- Check `IAPManager` is not being started for this build, and that no analytics event claims a
  purchase surface was shown.

## Definition of done

1. No path from a cold boot reaches a screen that offers to take money, and no path reaches
   `PurchaseConfirmationModal`.
2. Every de-scoped entry reads as a deliberate state — dimmed, labelled, and refusing with a
   reason — not as a dead button and not as a missing entry with no explanation.
3. Crystal earning and vessel unlocking are untouched and still work.
4. Exactly one implementation of the locked look exists in the project.
5. `Docs/STEAM_RELEASE_TASKS.md` R4 is ticked, and the PR body's *Verification status* section
   says plainly what you could and could not verify in the editor.
