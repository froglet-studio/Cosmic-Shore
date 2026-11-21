# Style-B UI: first full playtest

One pass through the game in the new UI. The bar is **every feature works as it did, in the new
look**. Each step names what to do, what you should see, and where to look if it is wrong.

Branch `claude/new-session-76hnyc` (bleeding-edge merged in). Background:
- `Docs/MENU_REDESIGN_B.md`: the design, section by section.
- Section 23 of that file: the parity audit.
- `Docs/UNITY_VERIFICATION_CHECKLIST.md`: the per-item checklists.

**Before you start**
1. Let Unity import and compile. The console should show no red on import. If it does, copy the
   first error: that is the one to fix.
2. Open `Menu_Main` and press Play. Plug in a pad, and keep the keyboard and mouse handy. Do one pass
   on each device where a step says "pad".

---

## 1. Home

| Do | Expect | If wrong |
|---|---|---|
| Land on Home | The pilot chip (avatar, name, Online, crystals) is top left. The four hubs (Arcade with an A keycap, Arena, Toy Box, Mission) and the Settings hex are in place. The blocks rise in. | `author_menu_home.py`, section 21 |
| Pad: press nothing, then A | Focus starts on Arcade, and A opens the Arcade window | Focus order: section 21 |
| Click the **avatar** | The profile window opens on the Avatar tab (restored, section 23) | `author_menu_home.py` avatar button |
| Click the lamp on the avatar's corner | "Go offline?" with Yes / No. It does NOT also open the profile window. | |
| "Up next" weekly card | It opens the weekly challenge's configure window with its terms pinned | `HomeUpNext` |
| Tap the cell (or X for Settings) | Freestyle starts / Settings opens | |
| Fly freestyle, then exit (the X hex) | You are back on Home, and its blocks rise in again | section 22.8 |

## 2. Top nav and screens

| Do | Expect |
|---|---|
| Visit each nav tab: Hangar, Profile, Ark, Home | Each screen slides in, and its header and blocks rise in **on every visit**, not only the first |
| Hangar | The vessel list rises in order. Locked vessels stay dim. Selecting a vessel shows its hero art, description and abilities. The spend-crystals popup works. |
| Profile | Pilot card, record, quest track. **Change avatar** / **Rename** open the profile window. Pick an avatar and a name; both stick. |
| Ark | The converter reads Ready; CONVERT runs it, the four crystals merge, the Omni pops, and the status reads Converted |

## 3. Arcade -> configure -> a match (the main loop)

| Do | Expect | If wrong |
|---|---|---|
| Open Arcade | Header and body rise in, and the cards pop in. Open it a second time: same again. | section 22.8 |
| Party panel (left) | Your seat, open seats, the online list. Invite a friend if you can. | item 3 |
| Star a card | The favourite toggles and survives a reopen | |
| Open **Skim Race** | The configure tree: vessel node, intensity 1-4, the three domain columns with seats and + AI. An **AI pilots: Easy / Medium / Hard** row sits under the domains. | `author_menu_configure.ai_row` |
| Pick Hard, then open a card with no AI difficulty (Joust, say) | Hard lights when picked (pad: the plates are reachable like any button). On Joust the row is **not shown** and the pad skips it. | `AIDifficultyRules.IsOfferedFor` |
| Pick intensity, a domain, add AI | Closed domains and out-of-range intensities stay **dimmed** after the window animates in (the regression to look for) | section 22.8 |
| START GAME | The connecting panel, then the match | |
| In the match | Score columns pop in; the goal row's bar slides and its count swells; the ability row shows keycaps, and a held key presses in | 12a |
| Ready / countdown | READY rises in; with a party, the waiting plate shows each pilot ticking ready; the countdown counts | 12b |
| Pause (Start / Esc) | The scrim fades, the window rises, Resume / Settings / Leave step in. All three work. | 12c |
| Finish the match | The reveal ("Jade wins", in the winner's colour), then the scoreboard: banner, ranked rows, stats, buttons. **Play again** and **Main menu** both work, and so does **Leave lobby** in a party. A quest-complete toast appears bottom centre if you earned one. | 12d, 22.6 |
| Spectate (die early in a mode that spectates) | The bar drops in with the pilot's name and "Ruby - Sparrow". Switching pilots works. | 12e |

## 4. The other windows

| Do | Expect |
|---|---|
| Maelstrom (Arcade) | Opens its own configure window: game pool, format, seats |
| Weekly challenge -> leaderboard | The podium, rows and inline rewards. Empty podium steps stay **hidden**. Back closes it. |
| Arena | Cards pop in. A card opens the arena configure: hull strip, CLAIM, domains. |
| Toy Box -> a toy | Variant tiles rise in by grid position; preview / variants / about step in; NAVIGATE / SWITCH work |
| Mission | Cards pop in on open (new). A locked mission refuses with its sting. |
| Settings (from Home and from Pause) | Four tabs; every option changes something; music / SFX volume audibly change; Credits opens |
| Party invite (needs a second player) | The popup drops in top right; Join / Decline work on the pad; it auto-hides after 3 s |

## 5. Pad-only pass

Repeat sections 1 and 3 with the mouse untouched. Every button is reachable, B always backs out,
and nothing selects a control on a window that is not open.

---

**Report back** with the step number and what you saw (a screenshot or the first console error is
ideal). Anything not listed that used to work and no longer does is a parity bug: name the old
behaviour and it gets fixed first.
