# UI lessons: what Shombith picks and what he rejects, in his words

Mined on 2026-11-07 from `Docs/MENU_REDESIGN_B.md` (126 sections, 43 mockup rounds, Rev 1 to Rev 66) for
`Docs/SKILL_TRAINING_PLAN.md` 3.4: first drafts kept repeating what the UI owner had already rejected, because 43
rounds of picks were a dataset of his taste that no doc distilled. Read this before drawing round 1 of anything.
Every quote is his (or the named owner's) and the number is the section it is in. Nothing here was confirmed as a
rule by him; it is what the record shows he did.

The generated table at the end (`python3 Tools/Build/ui_lessons.py --md`) holds every round's pick, rejection and
editor status; `--screens` prints the rebuild count per screen; `--check` fails on a round older than three days
with no recorded pick.

## 1. What gets picked

- **The interesting option, then simplified.** "the interesting option" (33: A's live preview as the window
  itself with B's step cards on top); then "I like this UI apart from the latest one" (43: the simpler one-column
  configure beat the three-step layout picked a day earlier). The newest option is not automatically the keeper.
- **Calm, slow, opaque.** "keep it a bit slow. Make it 95% opaque" (67: the sea lines); "Good but dont like the
  white bg, also the blue part can have the sea lines" (73); "The old bg was far better" (62). The game world
  barely shows through a window.
- **Glow and motion on the call to action, nowhere else.** "make the borders glow everywhere in the menu" (38);
  "change the selection color to glowing blue with animation / sky blue" (47); "I like C for the hover effects /
  Then we need some new hover animations" (101); "Make everything subtly animated" (89). But "every pixel is
  important" and anything that moves for its own sake reads as noise or "frozen" (61).
- **The real thing, not a stand-in.** "Hangar each vessel should show each powerup animation" (51); the demo must
  be "played by AI and it should be low cost, very low cost" (59); "the colourful card renders" over flat art (82);
  "the Colour sets tab is a window onto the real cell" (126).
- **Modern, one page, the game's own icons.** "I don't like the design, make it modern, take inspiration from
  website" (115, the Fleet Codex); "Make only one page, doesn't need two pages" (114); "Elements use the actual
  element icons and animated. ecology need colors. Vessel need colors" (89).
- **Cards that slide open, not big cards.** "Rather than big cards make smaller cards like arena maybe and they
  slide open when we click them" (117); "Lets go with C and implement it in the real game" (119, the route poster,
  not the recommended option).
- **One shared panel over every window.** The friends drawer "pushes content in the hub windows, overlays Home and
  the Hangar, and never covers a game window" (68); "All of the buttons of the hub should look the same" (69).
- **A unique language tied to the elements.** "I want the UI to be inspired by the elements. As the game is one of
  a kind so should be the UI" (45). And its limit: "you used elemental icon for friends you should never do that"
  (80); "Do not use domain colors" (116).
- **He often picks against the recommendation.** B over the recommended A on the Fleet Codex twice (114, 115),
  C Sheen over A (101), C the route poster over I (118-119), C the deck (94-95). Recommend, but draw the others as
  well as the recommended one.

## 2. What gets rejected

- **Noise.** "Too much noise" (94, Squads); "its too noisy if you cramp stuff up" (79); "This is the part I hate,
  too much text and noise here" (118); "The artifact and the UI is noisy make it look like an application" (104);
  "The game cards are still noisy" (72); "this place is a bit noisy" (83).
- **Empty, black, static.** "too empty, no animation, no prettiness" (13); "Dont like the black screen and the
  sparrow doesnt shoot or anything make this part way coooler" (41); "too black / The UI seems too empty" (57);
  "Black loading screen when changing cell show something interesting" (47); "frozen" (61).
- **Too transparent, too big, overlapping.** "Hangar screen whole bg is too transparent / Vibe tab - I do not even
  notice it because of the transparency" (81); "we can reduce the size of each component it looks too big in the
  game" (84); "the left and right arrows are overlapping UIs / Spectate is getting cut off" (83); "The selection UI
  is overlapping. We do not need a selection icon we can glow the podium below" (86).
- **A whole look built without a pick.** "Scrap the whole lattice design. The old bg was far better." (62, built
  and scrapped the same day); "I didn't much like the mockup ideas" (113, 116).
- **Generic or old.** "I hate the old UI" (99); "the arcade cards again are using the old elelmental icons again"
  (100); "I do not like this UI" (48, a plain port of the old scoreboard in the new skin); "I do not like this
  style at all" (94); "for the ftue I do not like them once bit. Up your game bhai" (100, 102).
- **Slop and text.** "Remove the slop below saying dolphin, pilot 2" (74); "useless text like where does your
  vessel go next" (71); "remove the roman numericals" (78); "Remove this man ASAP (the NOW PLAYING card)" (76).
- **Not working in the editor.** "I apply it but the color of the game does not change at all, I have to restart
  the game" (124); "Missing background / Misaligned texts" (126.1); "the buttons dont work ... there is no live
  bar" (110); "it lags when we get into a game mode" (99); "performance mate its too laggy" (46).

## 3. Rebuilt more than once (`ui_lessons.py --screens`, sections that name the screen)

| Screen | Sections | What finally landed |
|---|---|---|
| Hangar | 15 (11, 31, 35, 37, 41, 51, 59, 63, 66, 76, 81-84, 97) | vessel list on the left, the colourful card renders, the AI-played low-cost demo, abilities as information |
| Configure | 14 (14, 29, 33, 38-40, 43, 74, 78, 81, 83, 86, 96, 108) | G with the preview on the left, no invite button, no hold-to-launch; the preview extends as a tray, never full screen |
| Friends / party | 10 (12, 32, 36, 68, 80, 83, 84, 106, 107, 109) | one drawer, E's open tray with the orb kept, the people glyph (never an element icon) |
| Profile | 8 (15, 56, 75, 122-126) | Rev 54 A, the Showroom: the Colour sets tab as a window onto the real cell |
| Voyage | 8 (69, 71, 78, 79, 113, 116, 117, 119) | smaller cards that slide open; Rev 49 C, the route poster |
| Codex | 6 (84, 87-89, 97, 98) | C Orbit, element icons live, subtle motion, "the silver should ..." |
| Squads | 4 (92-95) | C the deck, after "Too much noise" |
| Window backgrounds | 4 (62, 70, 107, 116) | the sea lines, 95 % opaque; the lattice scrapped; the grid removed everywhere |
| FTUE | 4 (29, 34, 100, 102) | Rev 36 B Comms, after Rev 34 was rejected whole |

## 4. Rules for a first draft (each from the sections above)

1. Draw it on Frogma and get a pick before anything is built in Unity (53, 62).
2. Draw three or four options, make one the recommendation, and draw the others as well as it (101, 114, 118).
3. Count the elements: a screen he rejects as "noisy" has more on it than one he picks; `check_mockup.py`'s noise
   budget is the first filter (79, 94, 104, 118).
4. Nothing black, nothing empty, nothing static: the sea lines behind it, slow, 95 % opaque (47, 57, 62, 67).
5. One glow: the call to action and the selection, in Lime or the sky-blue selection; nothing else moves for its
   own sake (38, 47, 61).
6. The real thing in the picture: the vessel doing its abilities, the real cell behind the colour sets, rendered
   cards, the game's own icons (51, 59, 82, 89, 126).
7. No element icon or domain colour for anything that is not an element (80, 116); never the old elemental icons
   (100).
8. Small cards that slide open beat big cards; one page beats tabs (114, 117).
9. Nothing overlapping, nothing cut off, components smaller than you think (83, 84, 86).
10. No slop text, no Roman numerals, no label the data does not know (71, 74, 78).
11. Modern and application-like for a tool; the website's look is the reference for a codex (104, 115).
12. Record the pick in the round's section the day it is made, so `ui_lessons.py --check` stays green and the
    next draft can read it.

## 5. Regenerate

```
python3 Tools/Build/ui_lessons.py --md        # the table below
python3 Tools/Build/ui_lessons.py --screens   # the rebuild counts
python3 Tools/Build/ui_lessons.py --check     # rounds with no recorded pick
python3 Tools/Build/check_mockup.py <board.html>   # the gate on a mockup before it is shown
```

Measured on 2026-11-07 with `check_mockup.py` on five real boards: the three rejected rounds' boards (Rev 29 Squads
A, Rev 34 FTUE A, Rev 36 B) carry 88, 242 and 399 visual elements; the picked Rev 49 C carries 53 and passes the
noise budget of 60; the picked Rev 54 A (the Showroom with its colour chips) carries 205 and fails it. Every board
predates the tokens of 2026-11-03, so the colour and type gates fire on all five; the noise gate is the one that
tells a rejected round from a picked one in four cases of five.

## 6. The rounds (generated; do not edit between the markers)

<!-- ui-lessons:begin -->
Generated by `python3 Tools/Build/ui_lessons.py --md` from `Docs/MENU_REDESIGN_B.md` (126 sections, 26 mockup rounds). Do not edit between the markers; edit the doc and re-run.

| section | date | screens | picked | his words | rejected words | run in editor |
|---|---|---|---|---|---|---|
| 1 Status | 2026-10-14 |  | Legend: Planned ▸ Mockup ▸ Mockup approved ▸ Built ▸ Approved in editor. |  |  | awaiting |
| 2 Inventory, measured from Menu_Main.unity on 2026-10-13 | 2026-10-13 |  |  |  |  |  |
| 3 Build order | 2026-10-13 |  |  |  |  |  |
| 4 Reusable components (item 0) |  | Kit |  |  |  |  |
| 5 Mockup vs spec, and where the spec wins |  |  |  |  |  |  |
| 6 Decisions | 2026-10-13 |  | Decisions (approved 2026-10-13: all six recommendations) |  |  |  |
| 7 Pre-existing defects found during the inventory (not fixed here) | 2026-10-14 |  |  |  |  |  |
| 8 The component kit as built (item 0) |  | Kit |  |  |  |  |
| 9 Retirement list: what the kit supersedes |  | Kit |  |  |  |  |
| 10 Item 1: top nav | 2026-10-14 | Top nav | pressed and refused). Mockup approved 2026-10-14, with both open points settled as recommended: |  |  | yes |
| 11 Item 2: Hangar | 2026-10-14 | Hangar | HangarNotes lists every change. Mockup approved 2026-10-14 with all four open points as |  |  | yes |
| 12 Item 3: Arcade and the shared party panel | 2026-10-14 | Arcade, Friends / party | ArcadeNoFriends, ArcadePicks and ArcadeNotes. Mockup approved 2026-10-14 with all three |  |  | awaiting |
| 13 Items 4-11: the mockup set | 2026-10-14 |  | 13.2 Decisions (approved 2026-10-14: every recommendation, i.e. the "Drawn as" column) |  | "too empty, no animation, no prettiness" |  |
| 14 Item 4: the game configure windows | 2026-10-15 | Configure | could never be picked. ArenaHullStripTests. Maelstrom: the format block (race target, game cap, points by |  |  | awaiting |
| 15 Item 5: the Profile screen and the profile window | 2026-10-15 | Profile | awaiting |  |  | awaiting |
| 16 Item 7: the Ark screen (Elemental Converter) | 2026-10-15 | Ark | awaiting |  |  | awaiting |
| 17 Item 8: the mode windows | 2026-10-15 | Mode windows | awaiting |  |  | awaiting |
| 18 Item 9: Settings and Credits | 2026-10-15 | Settings, Credits | picked directly, so choosing it snaps back. |  |  | awaiting |
| 19 Item 10: the closed surfaces | 2026-10-15 | Closed surfaces / purchase | awaiting |  |  | awaiting |
| 20 Item 11: menu toasts | 2026-10-15 | Toasts | awaiting |  |  | awaiting |
| 21 Item 6: Home | 2026-10-15 | Home | awaiting |  |  | awaiting |
| 22 Item 12: the in-game UI | 2026-10-15 | In-game HUD | The approved boards are on the canvas's second page, "In-game HUD" (18 boards, approved 2026-10-15 |  |  | awaiting |
| 23 Feature-parity audit against the old UI | 2026-10-15 |  | / HangarVesselGridCard / replaced / HangarVesselTile (item 2, approved in editor). / |  |  |  |
| 24 Performance: the ambient layer sleeps while nobody can see it | 2026-10-15 |  |  |  |  |  |
| 25 The boot world loads before the menu is revealed | 2026-10-15 | Loading | A cover nothing claims (a config with nothing to build) lets go after 20 s. |  |  |  |
| 26 Item 13: the start flow | 2026-10-15 | Start flow / sign-in | Built from the approved Start boards (StartSplash, StartRetry, StartSignIn, StartSignInFailed, |  |  | awaiting |
| 27 Item 14: the Maelstrom game UI | 2026-10-15 | Maelstrom | Built from the approved boards (MaelstromHub, MaelstromStats, MaelstromSummary). Written by |  |  | awaiting |
| 28 Playtest fixes: skip, connecting backdrop and camera, pad navigation, touch devices | 2026-10-15 | Connection |  |  |  |  |
| 29 Tutorial off, Spectate, configure preview and opacity | 2026-10-15 | Configure, FTUE |  |  |  |  |
| 30 Rev 3: Home | 2026-10-16 | Home | Mockups: the Rev 3 page of the canvas (approved 2026-10-16). Built in this order, one commit each: |  |  | awaiting |
| 31 Rev 3: the Hangar | 2026-10-16 | Hangar | awaiting |  |  | awaiting |
| 32 Rev 3: the four windows use the room the friends panel gave back | 2026-10-16 | Friends / party, Mode windows | awaiting |  |  | awaiting |
| 33 Rev 3: the Configure window - cinema preview + step cards | 2026-10-16 | Configure | The owner approved "the interesting option": option A's full live preview under option B's three step cards (canvas Rev 3 |  |  | awaiting |
| 34 Bleeding-edge merged in; the first-login guide on the rev-3 menu | 2026-10-17 | FTUE |  |  |  |  |
| 35 The Hangar's TRY: real ability playback | 2026-10-17 | Hangar | awaiting |  |  | awaiting |
| 36 Home: compact docked cards, clear of the friends tab | 2026-10-17 | Home, Friends / party |  |  | "friends is overlaying vessel hangar, make vessel hangar and weekly challenge smaller tabs" |  |
| 37 Playtest round (2026-10-17): speed, windows, Hangar text, element glyphs, double tap | 2026-10-17 | Hangar |  |  |  |  |
| 38 Glowing borders everywhere; the configure preview framed again | 2026-10-17 | Configure, Kit |  |  |  |  |
| 39 Fly in the frame; full screen is the option; the preview keeps one cell | 2026-10-17 | Configure |  |  |  |  |
| 40 Configure = Rev 3 option B | 2026-10-17 | Configure | The owner picked the second version ("I like the second version of the UI more" - the R3ConfigureB board over the R4 | "I like the second version of the UI more" |  |  |
| 41 Hangar rev 4: a lit bay, the vessel fires, a look-only UPGRADE | 2026-10-17 | Hangar | boards were approved with "Approved, price is fine, make it look-only for now". Built in author_menu_hangar.py: | "Approved, price is fine, make it look-only for now" | "Dont like the black screen and the sparrow doesnt shoot or anything make this part way coooler" |  |
| 42 Every kit label shrinks to fit its panel | 2026-10-17 | Kit |  |  |  |  |
| 43 Configure = Rev 4 simple; Yash's AI seat info in style B (Rev 5) | 2026-10-17 | Configure | Configure (the owner, on the R4Configure board: "I like this UI apart from the latest one"). It replaces option B | "I like this UI apart from the latest one" |  |  |
| 44 Playtest fixes: the Ark under the nav, the fly hint's x, flat - and x | 2026-10-17 | Ark, Top nav |  |  | "Ark screen is messed up" / "i dont like the designs" |  |
| 45 Rev 6: the elemental UI | 2026-10-17 | Palette / elemental UI | Rev 6: the elemental UI (approved 2026-10-17) |  |  |  |
| 46 Playtest fixes, 2026-10-17 (after Rev 6) | 2026-10-17 |  |  |  | "performance mate its too laggy" |  |
| 47 Rev 7: the playtest round | 2026-10-17 |  | Rev 7: the playtest round (approved 2026-10-17) |  |  |  |
| 48 The scoreboard works; Rev 8 redraws it | 2026-10-17 | Scoreboard |  |  | "I do not like this UI" |  |
| 49 The scoreboard is Rev 8 option B, the glass ladder | 2026-10-17 | Scoreboard | The owner picked option B ("I like this one"). Built by author_ingame_hud.py (ScoreboardPanel / Row / Stat and the | "I like this one" |  | awaiting |
| 50 Polish: every line the style-B surfaces write is an authored field | 2026-10-17 |  |  |  |  |  |
| 51 The Hangar shows each vessel's real abilities | 2026-10-18 | Hangar | awaiting |  |  | awaiting |
| 52 Follow-ups this branch leaves | 2026-10-18 |  |  |  |  |  |
| 53 Rev 9 boards: signing in with Steam, Google and Apple | 2026-10-18 | Start flow / sign-in | boards are approved, the UI is built against a provider-availability config so it can ship before the providers work. |  |  |  |
| 54 Rev 9 built: sign-in with Steam, Google and Apple; the Account tab | 2026-10-18 | Start flow / sign-in | The owner approved the §53 boards ("Approved but bring the lattice anim on the bg image"). The UI is built and runs | "Approved but bring the lattice anim on the bg image" |  | awaiting |
| 55 Provider logos must be the originals; Rev 10 boards | 2026-10-18 |  | neutral fill - no recolouring, no monochrome G, no custom icon - with an approved call to action. Its pre-approved |  |  |  |
| 56 Rev 10 built: the pilot record, the Audio Studio, the now-playing card | 2026-10-18 | Profile | The owner approved Rev 10 and then refined it: the Profile screen stays as it was (pilot card, quests, Episodes); |  |  | awaiting |
| 57 R9b built: the providers' own sign-in buttons | 2026-10-18 | Start flow / sign-in | The owner approved R9b and agreed to add the official logos. This replaces §54's stand-in marks and kit-styled provider |  | "too black" / "The UI seems too empty" | awaiting |
| 58 The welcome card on the Bootstrap splash | 2026-10-18 | Start flow / sign-in | awaiting |  |  | awaiting |
| 59 The Hangar preview is the AI's, on the stage, at almost no cost | 2026-10-19 | Hangar | awaiting |  | "the preview of the vessel is still not working in hangar. The preview should only be played by AI and it should be low cost, very low cost. Should not provide a whole environment and everything, just light weight to... | awaiting |
| 60 Song names while the random music plays; Arena hull icons | 2026-10-19 | Arena | song only when the game had picked it (SoundtrackPlayer.Current), and with no per-track FMOD events yet the music is |  |  | awaiting |
| 61 Garrett's palette and attention pass | 2026-10-24 | Palette / elemental UI | awaiting |  |  | awaiting |
| 62 Rev 11 (frosted glass + Amoebius hex lattice) built and scrapped | 2026-10-24 | Window backgrounds | Rev 11 (frosted glass + Amoebius hex lattice) built and scrapped (2026-10-24) |  | "Scrap the whole lattice design. The old bg was far better." |  |
| 63 The Hangar's pad presses belong to the menu | 2026-10-25 | Hangar | awaiting |  |  | awaiting |
| 66 The Hangar's ability rows are information | 2026-10-25 | Hangar | awaiting |  |  | awaiting |
| 67 Sea lines behind every window | 2026-10-25 |  | Shombith picked H, "Sea lines", from the canvas's Rev 12 options board, saw it under the Arcade and Profile windows |  |  | awaiting |
| 68 The friends drawer docks into hub windows and never covers a game window | 2026-10-25 | Friends / party, Mode windows | awaiting |  |  | awaiting |
| 69 Rev 13: four tabs, the profile from the pilot, the Voyage window, a brighter sea | 2026-10-25 | Voyage | Shombith approved the canvas's Rev 13 boards (R13Voyage, R13Profile, R13Home, R13Notes) with four answers: Y opens the |  |  | awaiting |
| 70 The sea everywhere; the lattice retired | 2026-10-25 | Window backgrounds | awaiting |  | "Remove the grid bg from the toy cards also replace with the new background in the authentication and loading screens as well. Any part with grid bg should change to this new sea bg. Hangar should have it too." | awaiting |
| 71 Rev 17 J: episodes apart from voyages, the old quest tracker back | 2026-10-25 | Voyage | awaiting | "Rev17 J implement" | "It does not [need] useless text like where does your vessel go next" | awaiting |
| 72 Rev 18 A: the game and toy cards | 2026-10-25 | Toy Box, Game cards | awaiting | "implement a" | "they are too transparent ... can have a bg like the profile screen - keep this. Just change how they orient the details" / "The game cards are still noisy" | awaiting |
| 73 The hull lit, not plated; the sea in the card bodies | 2026-10-25 | Game cards | awaiting |  | "Good but dont like the white bg, also the blue part can have the sea lines" | awaiting |
| 74 Rev 19 / 20: the arcade configure window, option G | 2026-10-26 | Configure | "Go with G lovely. But remove the invite a friend button and remove the hold to launch just start is enough". Boards: | "Go with G lovely. But remove the invite a friend button and remove the hold to launch just start is enough" | "It is too noisy. Keep the preview on the left and the description below. Move the controls to the right side, keep it at the top. Move the intensity and the team and AI selection below it ... Remove the slop below sa... | awaiting |
| 75 The replay browser, the Arcade's REPLAYS button and My Replays in the Profile | 2026-10-24 | Arcade, Profile, Replays | awaiting |  |  | awaiting |
| 76 Rev 24: the Hangar, option C taken forward; element flowers on the cards | 2026-10-26 | Hangar | value. That same petal can be showed in the arcade game cards updated with the real value the players have. Go ahead an | "In upgrades we do upgrade the elemental petals and they always start with 1 value. That same petal can be showed in the arcade game cards updated with the real value the players have. Go ahead an implement" | "Remove this man ASAP" | awaiting |
| 77 The scoreboard is Rev 23 option D, team versus | 2026-10-26 | Scoreboard | Shombith, 2026-10-26: "For scoreboard I liked D". Board: R23ScoreD ("Rev 23 - Scoreboard", copy in | "For scoreboard I liked D" |  | awaiting |
| 78 Rev 25 boards: the Arcade card in Arena and Voyage; intensity without Roman numerals | 2026-10-26 | Arcade, Configure, Voyage, Arena |  |  |  |  |
| 79 Rev 25 B built: the Arcade card in Arena and Voyage, the signal sign, the open borde... | 2026-10-26 | Arcade, Voyage, Arena | Shombith, 2026-10-26, on the Rev 25 boards (section 78): "Go with B and the signal sign but the add AI button is going out. | "Go with B and the signal sign but the add AI button is going out. Also the open panel that we used in the hangar use it here too" | "You can make the space bigger I guess bro, its too noisy if you cramp stuff up" | awaiting |
| 80 The friends drawer: the people glyph, no maximise | 2026-10-26 | Friends / party |  |  |  |  |
| 81 Round 2 on Rev 24 / 25: the Hangar, the arcade card, the configure card | 2026-10-26 | Hangar, Configure | awaiting |  | "Hangar screen whole bg is too transparent. Make the bg look like the arcade bg ... Left side vessel holders should use the open border card as well ... In the middle remove the hued image behind the vessel ... Vibe t... | awaiting |
| 82 The Hangar list's colourful card art | 2026-10-26 | Hangar | Shombith, 2026-10-26: "Go ahead with the colourful card renders" (section 81 had left the list on the fleet's card icons, | "Go ahead with the colourful card renders" |  | awaiting |
| 83 The configure card's intensity and vessel strip, the friends row's SPECTATE, the Han... | 2026-10-26 | Hangar, Configure, Friends / party | awaiting |  | "1st image (intensity): Decrease the size. 2nd image (the vessel strip): the left and right arrows are overlapping UIs. also claim needs a change button. This will only change in arena and voyage also this place is a... | awaiting |
| 84 Hangar abilities on a press, the Hangar drawn smaller, the list clear of the friends... | 2026-10-26 | Hangar, Codex, Friends / party, Top nav | awaiting |  | "Abilities when I click them then only the vessel should perform. Again I say in the hangar we can reduce the size of each component it looks too big in the game" / "See at bottom left it overlaps the friends icon" | awaiting |
| 85 SORT and FILTER beside ALL GAMES | 2026-10-26 | Arcade | Shombith, 2026-10-26: "In arcade games beside the all games tasks I want to have a sort and filter button"; "Go ahead | "Go ahead with the sort and filter buttons" |  | awaiting |
| 86 The picked pilot's podium glows instead of an outline | 2026-10-26 | Configure | The blue outline round the picked pilot (Pilot/Pick/Editing/Band) is gone. It covered the pilot and their hull and |  | "The selection UI is overlapping. We do not need a selection icon we can glow the podium below more to see it." | awaiting |
| 87 Rev 26 mockups: the Codex | 2026-10-26 | Codex | Nothing is built in Unity until a board is approved. |  |  |  |
| 88 The CODEX screen, built | 2026-10-26 | Codex | Asked (Shombith): "Go ahead implement the codex I like the design" (the Rev 26 boards, section 87). | "Go ahead implement the codex I like the design" |  | awaiting |
| 89 The Codex: Toys, live element icons, colour, and subtle motion | 2026-10-26 | Codex | awaiting |  |  | awaiting |
| 90 Rev 27: the Port opens, with faction messages and the weekly leaderboard | 2026-10-27 | Port | everywhere"; on the chat service, "should we do vivox? We can" and "Lets implement". | "Lets implement" |  | awaiting |
| 91 Rev 32 D: the Port's messages and field, sea-lit | 2026-10-27 | Port | more mockups", then on the four looks, "Go with D implement". | "Go with D implement" | "I dont like the chat bg per person also the input field bg, make them look brigter and better ... more mockups" | awaiting |
| 92 The Ark's tabs: Squads and Decor beside the converter | 2026-10-27 | Ark, Squads | The Squads page below is superseded by section 95 (the deck): the three cards with CHANGE buttons and PLAY |  |  | awaiting |
| 93 Rev 28 mockups: the Ark's Squads and Decor tabs | 2026-10-27 | Ark, Squads | always shown. Rebuilt to these boards once they are approved. Open decision: Arcade, which the first brief named and this |  | "The highlight looks to weird, use the sea lines in bg and also make the highlight 3D light type. the pilots should stand beside the vessel" |  |
| 94 Rev 29 mockups: Squads, six ways | 2026-10-27 | Squads | Picked (Shombith, 2026-10-27): "go with C, the deck". | "go with C, the deck" | "Yes keep it out give me more variations. I do not like this style at all" / "Too much noise" |  |
| 95 The deck: Rev 31 boards and the Squads tab rebuilt to them | 2026-10-27 | Squads | awaiting |  |  | awaiting |
| 96 The squad's domain reaches the launch panel | 2026-10-27 | Configure |  |  |  |  |
| 97 The Codex rebuilt around play, the Arcade's two dropdowns, and the first Hangar fixes | 2026-10-27 | Hangar, Arcade, Codex | Motion (no bobbing). Nothing moves until an entry is picked: |  | "Remove the bobbing animation and use some cool selection animations" / "The dashed outline is the open triangle panel we put in the hangar bg remove that dashed outline and also on the top right we do not like the no... | awaiting |
| 98 The Codex as an Orbit | 2026-10-27 | Codex | Shombith picked board C: "Go with C, implement it". The grid of cards is gone. Generator: Tools/Build/author_menu_codex.py | "Go with C, implement it" |  | awaiting |
| 99 Rev 33: into a game mode, and why it lags | 2026-10-28 | Loading | Status. Shombith picked A and asked for the lag fixes (99.4). The areas they touch: | "Implement a. Also Fix the loading lag" | "Next up is the intro to game modes, I hate the old UI, create mockups maybe look it like when we swap cells, it lags when we get into a game mode, make it look a bit AAA" |  |
| 100 Rev 34 mockups: FTUE, the captain guides from the side | 2026-10-28 | FTUE | Rejected (Shombith, 2026-10-28): "for the ftue I do not like them once bit. Up your game bhai", then "Leave that |  | "for the ftue I do not like them once bit. Up your game bhai" |  |
| 101 Rev 35 mockups: hover, how the menu answers the pointer | 2026-10-28 | Hover | Picked (Shombith, 2026-10-28): "I like C for the hover effects". | "I like C for the hover effects" |  | awaiting |
| 102 Rev 36 mockups: FTUE, redrawn - the captain arrives | 2026-10-28 | FTUE | Picked (Shombith, 2026-10-29): "Lets go with B" (Comms). | "Lets go with B" | "for the ftue I do not like them once bit. Up your game bhai" | awaiting |
| 103 Rev 37: connection states and rejoin | 2026-10-29 | Connection | Approved (Shombith, 2026-10-29): "Go ahead with rev 37 I approve". | "Go ahead with rev 37 I approve" |  | awaiting |
| 104 Rev 38 mockups: Computer artifact, the Compute console as an application | 2026-10-29 | Compute console | "Go with Tailscale and Linear, plus Optuna for searches." He also asked to "Show me the mockup while you do it" and to | "Go with Tailscale and Linear, plus Optuna for searches." | "The artifact and the UI is noisy make it look like an application." |  |
| 106 Rev 39 mockups: the friends tray, a calmer party and rows that never overlap | 2026-10-31 | Friends / party | Rev 39 mockups: the friends tray, a calmer party and rows that never overlap (2026-10-31; superseded by the Rev 40 pick, section 107) |  | "Change the UI of friends just the look and the center of the branch like animation, also the invite and other buttons overlap ... keep the feature where it extends as a tray into game cards." |  |
| 107 Rev 40 mockups: three more friends trays, on the sea lines | 2026-10-31 | Friends / party, Window backgrounds | Picked (Shombith, 2026-10-31): "I dont like this part [D's rail] current friends icon is okay, we can keep it rest | "I dont like this part [D's rail] current friends icon is okay, we can keep it rest we can go with E" | "I dont like this part [D's rail] current friends icon is okay, we can keep it rest we can go with E" | awaiting |
| 108 A mode's preview: FULL SCREEN extends as a tray | 2026-10-31 | Configure | awaiting |  |  | awaiting |
| 109 The party molecule back, and it grows from the host | 2026-10-31 | Friends / party | awaiting |  |  | awaiting |
| 110 Rev 41 mockups: the Compute console, a run you can see start | 2026-10-31 | Compute console | one picked (free, else in use with a start allowed, else the team queue), "This computer · paste a line" last; the |  |  |  |
| 111 Rev 42 mockups: the Compute console, three looks for the Silver Screen | 2026-11-01 | Compute console | Picked (Shombith, 2026-11-01): "Go with A." Built the same hour in Tools/Compute/console/index.html: every view | "Go with A." | "I feel the console app can look better" |  |
| 112 The board becomes the Froglet Design Board, in tabs | 2026-11-02 | Frogma (the board) |  |  |  |  |
| 113 Rev 43 mockups: the Voyage window - the map left, the voyage card in three levels,... | 2026-11-02 | Voyage | six mockups of each UI are presented and picked; the build follows the pick. |  |  |  |
| 114 Rev 45 mockups: the Fleet Codex, a vessel in tabs | 2026-11-02 | Fleet Codex | Picked (Shombith, 2026-11-02): "I like design B and also make the first page in that design and include. Make only | "I like design B and also make the first page in that design and include. Make only one page, doesn't need two pages." |  |  |
| 115 Rev 48 mockups: the Fleet Codex, modern - one page, the kit with its controls, the... | 2026-11-02 | Fleet Codex | Picked (Shombith, 2026-11-02): "Build B". Built the same day into the live codex (Docs/FleetCodex/index.html, | "Build B" | "I don't like the design, make it modern, take inspiration from website, shouldn't separate tabs like this for controls, also omni crystal ability is not shown, use the correct iconography for everything and give me 3... |  |
| 116 Rev 44 mockups: the Voyage window, four more - four tabs on the sea lines | 2026-11-02 | Voyage, Window backgrounds | Rev 44 mockups: the Voyage window, four more - four tabs on the sea lines (2026-11-02; Shombith picked I, section 118) |  | "I made a mistake episode 1 is not the hypersea. Episode saga are seperate things and so is quest tracker. SO 4 tabs and Voyage opens up first obviously with episodes coming soon or locked. And also I didn't much like... |  |
| 117 Rev 46 mockups: small voyage cards that slide open, from the card or the HyperSea map | 2026-11-02 | Voyage | and they slide open when we click them or select them in the map. With this design move ahead." | "Rather than big cards make smaller cards like arena maybe and they slide open when we click them or select them in the map. With this design move ahead." |  |  |
| 118 Rev 49 mockups: the picked I card, four more inviting looks - just the card | 2026-11-02 |  | Asked (Shombith, 2026-11-02): "Lets go I, but make the card background more inviting its a lot of texts and etc, | "Lets go I, but make the card background more inviting its a lot of texts and etc, give me mockups of just the cards maybe 4." / "Lets go I" | "This is the part I hate, too much text and noise here, we have to fix this part." |  |
| 119 Built: the Voyage window with Rev 49 C, the route poster | 2026-11-02 | Voyage | Asked (Shombith, 2026-11-02): "Lets go with C and implement it in the real game." | "Lets go with C and implement it in the real game." |  | no: NOT RUN IN EDITOR |
| 120 Frogma: the design board renamed, with sections and groups | 2026-11-03 | Frogma (the board) |  |  |  |  |
| 121 The Froglet UI style guide on Frogma's first tab; every old area in a section | 2026-11-03 | Frogma (the board), Style guide | Themes: Deep Sea (the game's own, default) and Shore (its light twin, each family re-picked inside itself at 4.5:1). |  |  |  |
| 122 Rev 51 mockups: colour sets in the Profile, and the repaint between them | 2026-11-05 | Profile | Picked (Shombith, 2026-11-05): "go with A, build it in Unity". Built in section 123. | "go with A, build it in Unity" |  |  |
| 123 Built: Rev 51 A, the Profile's Colour sets tab and the repaint | 2026-11-05 | Profile | Shombith: "go with A, build it in Unity". Every set is unlocked (the quests are not planned yet). Compiled against the | "go with A, build it in Unity" |  | no: NOT RUN IN EDITOR |
| 124 Fix: APPLY reaches the live world; LB / RB on the Profile's tabs | 2026-11-05 | Profile |  |  |  | no: NOT RUN IN EDITOR |
| 125 Rev 54 mockups: colour sets, more modern - the set's world leads | 2026-11-05 | Profile | Picked (Shombith, 2026-11-05): "go with A, build it in Unity and open the PR". Built in section 126. | "go with A, build it in Unity and open the PR" | "give me a few better modern mockups, I do not love these" |  |
| 126 Built: Rev 54 A, the Showroom - the Colour sets tab is a window onto the real cell | 2026-11-05 | Profile | Shombith: "go with A, build it in Unity". It replaces section 123's cards and preview panel. The tabs, LB / RB, the | "go with A, build it in Unity" | "Missing background" / "Misaligned texts" | no: NOT RUN IN EDITOR |
| 137 Rev 50 mockups: the Froglet Analytics dashboard, three ways to read it | 2026-11-05 | Analytics console | Asked (Shombith, 2026-11-05): "Lets go with our own dashboard and leave posthog. Pull up /frogma create new tab to | "Lets go with our own dashboard and leave posthog. Pull up /frogma create new tab to show me mockups of the dashboard, follow the style guide." / "go with B, start building the server." |  |  |
| 141 Rev 65 mockups: Froglet Analytics, the full tool - filters, charts, cohorts, eras,... | 2026-11-06 | Analytics console | cohorts, eras and admin queries", under Rev 50 (picked B). Seven screens of one tool, not options: each can be picked, |  | "is not at all good there should different analytical tools like pie charts, ganntt charts etc for business development" |  |
| 142 Rev 66 mockups: Froglet Analytics, more ideas - chart switches, live now, alerts, f... | 2026-11-06 | Analytics console | Alerts: four cards (D1 down to 29% on 0.9.14 Android, rejected events spiking on 0.9.15 WebGL, crystals paid |  |  |  |
<!-- ui-lessons:end -->
