# Menu Redesign B - the mockup board, in git

Every board of the **Menu Redesign B** canvas (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2), every round,
copied here so a parallel session can read the designs from the repo.
The live canvas stays the source: new rounds are published there first, and this copy is refreshed by re-reading the
canvas's `project/` files (Artifact read) into this folder.

- **What a board is:** a `.dc.html` file for the claude.ai Design canvas. It renders inside the canvas (it loads the
  canvas runtime `./support.js`), so open the link above to SEE a board; read the file here to see its layout, words,
  sizes and colours. `canvas.json` is the canvas layout: every board's page (round), position and title.
- **Images are not in this copy.** Boards point at `/_blob/<id>` images in the canvas's asset store (vessel renders,
  card art, avatars, icons). A Claude session on the same account reads one with the Artifact tool
  (`action: read`, the canvas url, `path: <id>`).
- **What was decided:** `Docs/MENU_REDESIGN_B.md` records each round, which option was picked and what was built.
  The pick for a round is the board the doc names, not the last one on its page.

<!-- design_board.py readme: generated below, edit the text above -->

Copied on 2026-10-28 from canvas version `1793216666-7774`: Rev 3 to Rev 36, 309 boards on 37 pages. The canvas's launch page is `rev36`. Refresh with `python3 .claude/skills/design-board/design_board.py pull --src <dir the Artifact read saved> --version <v>`.

## Rounds and boards (newest first)

### Rev 36 - FTUE, redrawn: the captain arrives  (page `rev36`)

- [`R36Compare.dc.html`](R36Compare.dc.html) - Rev 36 - the three entrances side by side on the Home step, frame by frame
- [`R36A.dc.html`](R36A.dc.html) - Rev 36 A - Wingman: her Squirrel streaks in on a skim trail that ends at the control, and her comm opens off the hull (recommended) - interactive
- [`R36B.dc.html`](R36B.dc.html) - Rev 36 B - Comms: a tall portrait window docks on the far edge with her voice line and the prompt - interactive
- [`R36C.dc.html`](R36C.dc.html) - Rev 36 C - Cinematic: she leans in large from the edge, name and line set big beside her - interactive
- [`R36Notes.dc.html`](R36Notes.dc.html) - Rev 36 - notes: what was wrong with Rev 34, the entrance timing, the words, the build

### Rev 35 - Hover: how the menu answers the pointer  (page `rev35`)

- [`R35Compare.dc.html`](R35Compare.dc.html) - Rev 35 - the same five controls (button, card, nav tab, hub tile, list row) at rest, hovered and focused, for all four looks
- [`R35A.dc.html`](R35A.dc.html) - Rev 35 A - Lift: the control rises 3 px, its border turns white and a white rule grows under it (recommended) - interactive
- [`R35B.dc.html`](R35B.dc.html) - Rev 35 B - Trace: one white light runs once around the cut border - interactive
- [`R35C.dc.html`](R35C.dc.html) - Rev 35 C - Sheen: one soft band sweeps across the fill, nothing moves - interactive
- [`R35D.dc.html`](R35D.dc.html) - Rev 35 D - Brackets: the corner brackets close in from outside, nothing moves - interactive
- [`R35Notes.dc.html`](R35Notes.dc.html) - Rev 35 - notes: what hover means, how it differs from focus, timings and the build

### Rev 34 - FTUE: the captain guides from the side  (page `rev34`)

- [`R34Compare.dc.html`](R34Compare.dc.html) - Rev 34 - the three captains at the same step, and which side they enter from
- [`R34A.dc.html`](R34A.dc.html) - Rev 34 A - Wingmate: the captain full-length at the screen edge, a speech plate pointing at the one control (recommended) - interactive
- [`R34B.dc.html`](R34B.dc.html) - Rev 34 B - Comm link: a compact incoming-transmission card that docks on the far side - interactive
- [`R34C.dc.html`](R34C.dc.html) - Rev 34 C - Briefing: a lower-third bust and a wide line, letterbox-style - interactive
- [`R34Notes.dc.html`](R34Notes.dc.html) - Rev 34 - notes: the old FTUE (Rev 21), what the captain changes, who speaks, the side rule

### Rev 33 - Into a game mode (loading, intro, countdown) and why it lags  (page `rev33`)

- [`R33A.dc.html`](R33A.dc.html) - Rev 33 A - Cell gate (recommended): the cell-change screen grown into the mode entry; the crystal fills with the build, its hex opens into the intro, then 3-2-1 in the hex - interactive (BOARD buttons pause each phase)
- [`R33B.dc.html`](R33B.dc.html) - Rev 33 B - Lineup: Regatta's six pilots light up as they load, lock in with one impact, then the art pushes into the title and a crystal counts down - interactive
- [`R33C.dc.html`](R33C.dc.html) - Rev 33 C - Briefing: a calm Dog Fight briefing whose arena map develops as the cell builds, then grows into the intro - interactive
- [`R33D.dc.html`](R33D.dc.html) - Rev 33 D - Intro and countdown (shared by whichever loading screen wins): title slam, objective, pilot card, hold to skip, waiting for pilots, 3-2-1 GO - interactive
- [`R33Notes.dc.html`](R33Notes.dc.html) - Rev 33 - notes: what 28 recorded launches measured, why entering a mode lags (code, checked twice), what the new screens change

### Rev 32 - Port: brighter messages and field  (page `rev32`)

- [`R32PortCompare.dc.html`](R32PortCompare.dc.html) - Rev 32 - the four looks side by side: two messages, the field at rest, typing, not sent
- [`R32PortA.dc.html`](R32PortA.dc.html) - Rev 32 A - Lit cards: slate cards with a periwinkle edge, yours on the right
- [`R32PortB.dc.html`](R32PortB.dc.html) - Rev 32 B - Glass bubbles: translucent bubbles over the sea, yours tinted violet
- [`R32PortC.dc.html`](R32PortC.dc.html) - Rev 32 C - Bright ribbons: alternating bright bands, name chips, a light field
- [`R32PortD.dc.html`](R32PortD.dc.html) - Rev 32 D - Sea-lit panels: lit from the corner, gradient borders, a glowing field

### Rev 31 - Squads, the deck  (page `rev31`)

- [`R31DeckMake.dc.html`](R31DeckMake.dc.html) - Rev 31 - Make squad (interactive: pick a card, then its vessel, pilot, element, artifact; Create)
- [`R31DeckMade.dc.html`](R31DeckMade.dc.html) - Rev 31 - Squad made: Take into Mission / Arena - interactive
- [`R31DeckJoined.dc.html`](R31DeckJoined.dc.html) - Rev 31 - A joined squad (Kestrel's) - interactive
- [`R31DeckEmpty.dc.html`](R31DeckEmpty.dc.html) - Rev 31 - No squad yet - interactive
- [`R31DeckJoin.dc.html`](R31DeckJoin.dc.html) - Rev 31 - Join a friend's squad - interactive
- [`R31DeckDecor.dc.html`](R31DeckDecor.dc.html) - Rev 31 - Decor locked
- [`R31DeckNotes.dc.html`](R31DeckNotes.dc.html) - Rev 31 - notes

### Rev 30 - Codex rethought (Vault, Field Guide, Orbit, Lexicon)  (page `rev30`)

- [`R30CodexVault.dc.html`](R30CodexVault.dc.html) - Rev 30 - A Vault: list, living crystal, solid reading panel - interactive (press an entry)
- [`R30CodexGuide.dc.html`](R30CodexGuide.dc.html) - Rev 30 - B Field Guide: the crystal as a plate, its page beside it, every entry on a strip
- [`R30CodexOrbit.dc.html`](R30CodexOrbit.dc.html) - Rev 30 - C Orbit: Omni at the core, the elements orbiting, ecology and lexicon outside
- [`R30CodexLexicon.dc.html`](R30CodexLexicon.dc.html) - Rev 30 - Lexicon: Arcade, Quests, Voyages, Missions, then Vessels, Pilots, Artifacts, Toys - interactive
- [`R30Notes.dc.html`](R30Notes.dc.html) - Rev 30 - notes

### Rev 29 - Squads, six ways (quiet)  (page `rev29`)

- [`R29SquadsA.dc.html`](R29SquadsA.dc.html) - Rev 29 - A Hangar bay - interactive
- [`R29SquadsB.dc.html`](R29SquadsB.dc.html) - Rev 29 - B Formation - interactive
- [`R29SquadsC.dc.html`](R29SquadsC.dc.html) - Rev 29 - C The deck - interactive
- [`R29SquadsD.dc.html`](R29SquadsD.dc.html) - Rev 29 - D The Ark's bays - interactive
- [`R29SquadsE.dc.html`](R29SquadsE.dc.html) - Rev 29 - E Lineup - interactive
- [`R29SquadsF.dc.html`](R29SquadsF.dc.html) - Rev 29 - F Dossier - interactive
- [`R29Notes.dc.html`](R29Notes.dc.html) - Rev 29 - notes

### Rev 28 - Ark tabs (Squads, Decor)  (page `rev28`)

- [`R28SquadsMake.dc.html`](R28SquadsMake.dc.html) - Rev 28 - Make squad (interactive: pick a seat, its vessel, pilot, element, artifact; switch the domain; create)
- [`R28SquadsMade.dc.html`](R28SquadsMade.dc.html) - Rev 28 - Squad made: the podium, then Take into Mission / Arena - interactive
- [`R28SquadsJoined.dc.html`](R28SquadsJoined.dc.html) - Rev 28 - A squad you joined (Kestrel leads) - interactive
- [`R28SquadsEmpty.dc.html`](R28SquadsEmpty.dc.html) - Rev 28 - Squads, no squad yet - interactive
- [`R28SquadsJoin.dc.html`](R28SquadsJoin.dc.html) - Rev 28 - Join a friend's squad - interactive
- [`R28Decor.dc.html`](R28Decor.dc.html) - Rev 28 - Decor tab locked: the press shakes, Not open yet
- [`R28Notes.dc.html`](R28Notes.dc.html) - Rev 28 - notes and decisions

### Rev 27 - Port (faction messages, weekly leaderboard)  (page `rev27`)

- [`R27PortMessages.dc.html`](R27PortMessages.dc.html) - Rev 27 - Port: Faction messages (interactive: type, Enter or Send, 3 s cooldown)
- [`R27PortLeaderboard.dc.html`](R27PortLeaderboard.dc.html) - Rev 27 - Port: the weekly leaderboard as the second tab
- [`R27PortActions.dc.html`](R27PortActions.dc.html) - Rev 27 - Port: message actions - report, mute
- [`R27PortStates.dc.html`](R27PortStates.dc.html) - Rev 27 - Port: empty, loading, offline, not sent
- [`R27Notes.dc.html`](R27Notes.dc.html) - Rev 27 - notes and decisions

### Rev 26 - Codex  (page `rev26`)

- [`R26CodexHome.dc.html`](R26CodexHome.dc.html) - Rev 26 - Codex front page: seven chapters, recently found, closest to complete
- [`R26CodexEthirions.dc.html`](R26CodexEthirions.dc.html) - Rev 26 - Ethirions: the five crystals, Charge open
- [`R26CodexElements.dc.html`](R26CodexElements.dc.html) - Rev 26 - Elements: what each element wakes on every hull, Mass open
- [`R26CodexFauna.dc.html`](R26CodexFauna.dc.html) - Rev 26 - Ecology > Fauna: the food web, Shark open (NEW)
- [`R26CodexFlora.dc.html`](R26CodexFlora.dc.html) - Rev 26 - Ecology > Flora: sixteen plants, Arbor open with its elemental laws
- [`R26CodexPilots.dc.html`](R26CodexPilots.dc.html) - Rev 26 - Pilots: one per element per hull, Shrapnel open
- [`R26CodexVessels.dc.html`](R26CodexVessels.dc.html) - Rev 26 - Vessels: four abilities and their level-5 upgrades, Sparrow open
- [`R26CodexArtifacts.dc.html`](R26CodexArtifacts.dc.html) - Rev 26 - Artifacts (proposed: no artifact data in the game yet)
- [`R26CodexTools.dc.html`](R26CodexTools.dc.html) - Rev 26 - Tools: Pilot / World / Creation, Connect the Dots open
- [`R26CodexDiscovery.dc.html`](R26CodexDiscovery.dc.html) - Rev 26 - Discovery: the in-match toast when you meet something new
- [`R26Notes.dc.html`](R26Notes.dc.html) - Rev 26 - notes

### Rev 25 - Arcade card in Arena and Voyage  (page `rev25`)

- [`R25ArenaA.dc.html`](R25ArenaA.dc.html) - Rev 25 - Arena A (steppers, intensity as a number)
- [`R25ArenaB.dc.html`](R25ArenaB.dc.html) - Rev 25 - Arena B (picked: vessel strip, signal, open border)
- [`R25VoyageA.dc.html`](R25VoyageA.dc.html) - Rev 25 - Voyage A (steppers, number)
- [`R25VoyageB.dc.html`](R25VoyageB.dc.html) - Rev 25 - Voyage B (picked: vessel strip, signal, open border)
- [`R25Notes.dc.html`](R25Notes.dc.html) - Rev 25 - notes

### Rev 24 - Hangar C taken forward  (page `rev24`)

- [`R24HangarUpgrades.dc.html`](R24HangarUpgrades.dc.html) - Rev 24 - Overview > Upgrades: vessel list left, in-game element flowers with N/5, angled open border, vibe bars
- [`R24HangarAbilities.dc.html`](R24HangarAbilities.dc.html) - Rev 24 - Overview > Abilities: the four abilities with their element flowers (info only)
- [`R24HangarGames.dc.html`](R24HangarGames.dc.html) - Rev 24 - Overview > Games: the games that fly the Sparrow
- [`R24HangarPilots.dc.html`](R24HangarPilots.dc.html) - Rev 24 - Pilots: the header names the pilot (Shrapnel); one pilot per element
- [`R24HangarArtifacts.dc.html`](R24HangarArtifacts.dc.html) - Rev 24 - Artifacts: the header names the slot; equipped, carried, undiscovered
- [`R24Borders.dc.html`](R24Borders.dc.html) - Rev 24 - The angled open border, four ways (Cradle, Rail, Split, Echo)
- [`R24Notes.dc.html`](R24Notes.dc.html) - Rev 24 - notes

### Rev 23 - Scoreboard, six ways  (page `rev23`)

- [`R23ScoreA.dc.html`](R23ScoreA.dc.html) - Rev 23 - A Podium: team banner, the top three on steps, reward and actions in the corners
- [`R23ScoreB.dc.html`](R23ScoreB.dc.html) - Rev 23 - B Results table: the glass ladder as a real table plus a 'Your race' card
- [`R23ScoreC.dc.html`](R23ScoreC.dc.html) - Rev 23 - C Your result first: the big place and time, personal best, everyone in one strip
- [`R23ScoreD.dc.html`](R23ScoreD.dc.html) - Rev 23 - D Team versus: two teams face each other with totals and the crystal share
- [`R23ScoreE.dc.html`](R23ScoreE.dc.html) - Rev 23 - E Broadcast: a banner and four pilot cards over the world, esports lower third
- [`R23ScoreF.dc.html`](R23ScoreF.dc.html) - Rev 23 - F Live board: hold Select mid-match for positions, gaps and team totals
- [`R23Notes.dc.html`](R23Notes.dc.html) - Rev 23 - notes: the one match, what every option keeps, what each does

### Rev 22 - Hangar, six ways  (page `rev22`)

- [`R22HangarA.dc.html`](R22HangarA.dc.html) - Rev 22 - A Showroom: big hero, one quiet column (abilities, upgrades with price), hull filmstrip
- [`R22HangarB.dc.html`](R22HangarB.dc.html) - Rev 22 - B Loadout: the four abilities as callouts pinned to the hull, hulls on a left rail
- [`R22HangarC.dc.html`](R22HangarC.dc.html) - Rev 22 - C Dossier: today's Hangar quieter - stage left, one tabbed dossier, gold UPGRADE
- [`R22HangarD.dc.html`](R22HangarD.dc.html) - Rev 22 - D Compass: upgrade-first, the vessel inside a four-point element compass
- [`R22HangarE.dc.html`](R22HangarE.dc.html) - Rev 22 - E Cinematic: the vessel fills the screen; abilities and upgrades are drawers
- [`R22HangarF.dc.html`](R22HangarF.dc.html) - Rev 22 - F Garage rail: neighbours dim in the bay, abilities read along the floor
- [`R22Notes.dc.html`](R22Notes.dc.html) - Rev 22 - notes: what each Hangar option does and what stays

### Rev 21 - FTUE (first login to first finish)  (page `rev21`)

- [`R21F1Home.dc.html`](R21F1Home.dc.html) - Rev 21 - F1 Home: the guide lights ARCADE (Settings stays live)
- [`R21F2Arcade.dc.html`](R21F2Arcade.dc.html) - Rev 21 - F2 Arcade: this week's card, Skim Race
- [`R21F3Configure.dc.html`](R21F3Configure.dc.html) - Rev 21 - F3 Configure (Rev 20 G): fly the preview first; START grey
- [`R21F4Lesson.dc.html`](R21F4Lesson.dc.html) - Rev 21 - F4 Lesson 1 of 4 - LEARN TO FLY, no skip the first time
- [`R21F5Ghost.dc.html`](R21F5Ghost.dc.html) - Rev 21 - F5 Lesson 3 - WATCH FIRST: the autopilot shows drift
- [`R21F6Mentor.dc.html`](R21F6Mentor.dc.html) - Rev 21 - F6 Lesson done: the Mentor's tips, START lime
- [`R21F7Loading.dc.html`](R21F7Loading.dc.html) - Rev 21 - F7 Loading into the first race
- [`R21G1Countdown.dc.html`](R21G1Countdown.dc.html) - Rev 21 - G1 In game: countdown with the goal
- [`R21G2Hint.dc.html`](R21G2Hint.dc.html) - Rev 21 - G2 In game: an idle hint on the ready ability
- [`R21G3Finish.dc.html`](R21G3Finish.dc.html) - Rev 21 - G3 In game: the first finish (placeholder for the scoreboard redesign)
- [`R21Notes.dc.html`](R21Notes.dc.html) - Rev 21 - notes

### Rev 20 - Configuration + START  (page `rev20`)

- [`R20ConfigureE.dc.html`](R20ConfigureE.dc.html) - Rev 20 - E: squad cards; angled START slab with chevrons
- [`R20ConfigureF.dc.html`](R20ConfigureF.dc.html) - Rev 20 - F: team bays you join; START beside the ready pips
- [`R20ConfigureG.dc.html`](R20ConfigureG.dc.html) - Rev 20 - G: pedestals, intensity on LB / RB; LAUNCH held (ring fills, proposed)
- [`R20Notes.dc.html`](R20Notes.dc.html) - Rev 20 - notes
- [`R20ConfigureH.dc.html`](R20ConfigureH.dc.html) - Rev 20 - H: fireteam rail with console prompts; START with a slow sheen
- [`R20ConfigureI.dc.html`](R20ConfigureI.dc.html) - Rev 20 - I: option D finished: lineup + focused seat actions; START inside the panel
- [`R20ConfigureJ.dc.html`](R20ConfigureJ.dc.html) - Rev 20 - J: 2 x 2 seat tiles; hatched START slab
- [`R20ConfigureG2.dc.html`](R20ConfigureG2.dc.html) - Rev 20 - G APPROVED (Shombith 2026-10-26): pedestals, Add AI pilot only, START pressed

### Rev 19 - Configure  (page `rev19`)

- [`R19ConfigureA.dc.html`](R19ConfigureA.dc.html) - Rev 19 - A: team columns - a seat tile per pilot (avatar, name, you / friend / AI), open seats with + AI and Invite
- [`R19ConfigureB.dc.html`](R19ConfigureB.dc.html) - Rev 19 - B: roster - one row per seat, team picker on each, the open row with + AI and Invite friend
- [`R19AddPilots.dc.html`](R19AddPilots.dc.html) - Rev 19 - adding pilots: open seat, + AI, Invite (online friends), invited then joined
- [`R19Notes.dc.html`](R19Notes.dc.html) - Rev 19 - notes
- [`R19ConfigureC.dc.html`](R19ConfigureC.dc.html) - Rev 19 - C: seat cards - team band, big avatar, the open card with + AI and Invite
- [`R19ConfigureD.dc.html`](R19ConfigureD.dc.html) - Rev 19 - D: lineup of avatars; the seat you are on shows its actions under the row

### Rev 18 - Game and toy cards  (page `rev18`)

- [`R18CardsAArcade.dc.html`](R18CardsAArcade.dc.html) - Rev 18 - A, Arcade: art band on top, glass body below - hull on a pale plate, name, petals
- [`R18CardsAArena.dc.html`](R18CardsAArena.dc.html) - Rev 18 - A, Arena and Toy Box in the same treatment
- [`R18Notes.dc.html`](R18Notes.dc.html) - Rev 18 - notes
- [`R18CardsBArcade.dc.html`](R18CardsBArcade.dc.html) - Rev 18 - B, Arcade: art on one side, words on the other
- [`R18CardsBArena.dc.html`](R18CardsBArena.dc.html) - Rev 18 - B, Arena and Toy Box in the same treatment
- [`R18CardsCArcade.dc.html`](R18CardsCArcade.dc.html) - Rev 18 - C, Arcade: the hull lit on a hex plate sitting on the seam
- [`R18CardsCArena.dc.html`](R18CardsCArena.dc.html) - Rev 18 - C, Arena and Toy Box in the same treatment

### Rev 17 - Episodes back, voyages grow  (page `rev17`)

- [`R17VoyageJ.dc.html`](R17VoyageJ.dc.html) - Rev 17 - J, Episodes stacked: Episode 1 open with its quest tracker, Episode 2 a locked bar under it; Voyages a column that scrolls (Tandava, then the next voyages)
- [`R17VoyageK.dc.html`](R17VoyageK.dc.html) - Rev 17 - K, Episode spine: Episode 1 open, Episode 2 a locked spine beside it; Voyages a column that scrolls
- [`R17VoyageL.dc.html`](R17VoyageL.dc.html) - Rev 17 - L, Voyages row on top: Tandava and the next voyages in a row that scrolls sideways; Episodes under it, Episode 2 a locked spine

### Rev 16 - Episodes apart from voyages  (page `rev16`)

- [`R16VoyageG.dc.html`](R16VoyageG.dc.html) - Rev 16 - G, Side by side: Episodes (Episode 1 tab, Episode 2 locked, the smaller quest tracker) in a glass panel; Tandava a full-bleed poster under Voyages
- [`R16VoyageH.dc.html`](R16VoyageH.dc.html) - Rev 16 - H, Voyage banner on top: Tandava full-bleed across the width, the Episodes panel under it with shorter quest cards
- [`R16VoyageI.dc.html`](R16VoyageI.dc.html) - Rev 16 - I, Current quest open: as G, but only the quest in progress opens up and the locked ones fold to art, number and name

### Rev 15 - Voyage, old quest tracker  (page `rev15`)

- [`R15VoyageD.dc.html`](R15VoyageD.dc.html) - Rev 15 - D, Track into the voyages: a slim Episode 1 strip, the old quest cards, and the track carrying on into Tandava and Episode 2
- [`R15VoyageE.dc.html`](R15VoyageE.dc.html) - Rev 15 - E, Voyages at the side: Episode 1 as a chip beside the title, the old full-height quest cards, Tandava and Episode 2 stacked on the right
- [`R15VoyageF.dc.html`](R15VoyageF.dc.html) - Rev 15 - F, Tracker first: the Episode 1 strip, the old quest cards across the width, the voyages as a band underneath

### Rev 14 - Voyage + quest tracker  (page `rev14`)

- [`R14VoyageA.dc.html`](R14VoyageA.dc.html) - Rev 14 - A, Track on top: a slim Episode 1 strip, the old quest tracker (five cards on a track) across the width, Tandava and Episode 2 in a row under it
- [`R14VoyageB.dc.html`](R14VoyageB.dc.html) - Rev 14 - B, Voyages first: Tandava and Episode 2 as one band on top, then the Episode 1 strip and the old quest tracker as the biggest thing
- [`R14VoyageC.dc.html`](R14VoyageC.dc.html) - Rev 14 - C, Episode chip: Episode 1 as a chip beside the title, the old full-height quest tracker on the left, Tandava tall on the right with Episode 2 under it
- [`R14Notes.dc.html`](R14Notes.dc.html) - Rev 14 - what changes from Rev 13

### Rev 13 - Voyage + profile  (page `rev13`)

- [`R13Voyage.dc.html`](R13Voyage.dc.html) - Rev 13 - Voyage replaces Mission: Episode 1 and its quest track on the left (quest 5 leads to Tandava), the voyages on the right with Tandava open (its card art) and the next two locked; brighter gradient sea behind
- [`R13Tandava.dc.html`](R13Tandava.dc.html) - Rev 13 - Voyage 1, Tandava: the four forms it grows through, how you win and lose, your vessel (Squirrel / Sparrow / Rhino), the crew seats, Set sail
- [`R13Profile.dc.html`](R13Profile.dc.html) - Rev 13 - the Profile, opened from the pilot card on Home: Rev 10's style with Episodes and Quests removed, the pilot card full width with Wins / Crystals / Best streak, favourites, fly time by vessel, Audio Studio, now playing
- [`R13Home.dc.html`](R13Home.dc.html) - Rev 13 - Home: four tabs (Hangar, Ark, Home, Port), the pilot card opens the profile, Voyage replaces Mission
- [`R13Notes.dc.html`](R13Notes.dc.html) - Rev 13 - what changes, and the sea now vs brighter with gradients

### Rev 12 - Backgrounds  (page `rev12`)

- [`R12SeaArcade.dc.html`](R12SeaArcade.dc.html) - Rev 12 - H, Sea lines, under the Arcade: an 86 % veil (less see-through), thin wave lines in three depth layers sliding sideways at 30 / 45 / 60 s
- [`R12SeaProfile.dc.html`](R12SeaProfile.dc.html) - Rev 12 - H, Sea lines, under the Profile: the same 86 % veil and moving wave lines
- [`R12World.dc.html`](R12World.dc.html) - Rev 12 - from the game world: 0 the old background, I gyroid flora, J Schwarz P wall, K reef, L quasicrystal, M prism trail, N the cell, O arena echo (Skim Race), P track lines (Skein)
- [`R12Options.dc.html`](R12Options.dc.html) - Rev 12 - first round (none landed): A starfield, B prism shards, C contour map, D depth fog, E scan glass, F constellations, G nebula wash, H sea lines

### Rev 11 - Backgrounds (scrapped)  (page `rev11`)

- [`R11HMotion.dc.html`](R11HMotion.dc.html) - Rev 11 - H chosen (frosted glass + faint hexagons): six ways it could move - H1 still, H2 centre ring, H3 cell shimmer, H4 tide, H5 slow drift, H6 grow on open
- [`R11Lattice.dc.html`](R11Lattice.dc.html) - Rev 11 - G full screen: the Arcade over the game world, a 62 % see-through veil with Amoebius's hexagons and its slow centre ring; the line grid is gone
- [`R11Notes.dc.html`](R11Notes.dc.html) - Rev 11 - what changes, how it is built, and the questions for the owner
- [`R11Glass.dc.html`](R11Glass.dc.html) - Rev 11 - six see-through options over the game world: G clear veil + lattice, H frosted glass, I edge veil, J hex stained glass, K tide glass, L current glass
- [`R11Toybox.dc.html`](R11Toybox.dc.html) - Rev 11 - H full screen: the Toy Box on frosted glass (the world blurred behind, 50 % veil, faint hexagons, no ring)
- [`R11Profile.dc.html`](R11Profile.dc.html) - Rev 11 - I + tide full screen: Profile on the edge veil (clear centre, darker edges, hexagons only at the edges) with the slow tide band
- [`R11Solids.dc.html`](R11Solids.dc.html) - Rev 11 - for comparison only (rejected: too opaque): the solid surfaces A-F

### Rev 10 - Profile + music  (page `rev10`)

- [`R10Profile.dc.html`](R10Profile.dc.html) - Rev 10 - Profile: the pilot card with fly time / games / sessions, favourite vessel (most flown) and favourite pilot (your pick), fly time by vessel, quests, and the now-playing card bottom right
- [`R10AudioStudio.dc.html`](R10AudioStudio.dc.html) - Rev 10 - Profile > Audio Studio, music only: now playing with back / pause / skip, shuffle, stop; every track with Play now and a tick for the rotation
- [`R10NowPlaying.dc.html`](R10NowPlaying.dc.html) - Rev 10 - the now-playing card: slides in on a new track (FC style), resting and focused states, match and music-off behaviour
- [`R10FavPilot.dc.html`](R10FavPilot.dc.html) - Rev 10 - choose your favourite pilot
- [`R10Notes.dc.html`](R10Notes.dc.html) - Rev 10 - what the profile and soundtrack do, what has to be built, questions for the owner

### Rev 9 - Sign in  (page `rev9`)

- [`R9SteamAuto.dc.html`](R9SteamAuto.dc.html) - Rev 9 - sign-in 1: Steam signs you in on its own - your Steam name and avatar, a spinner, Y for other ways
- [`R9Chooser.dc.html`](R9Chooser.dc.html) - Rev 9 - sign-in 2: the chooser on PC - Continue with Steam (focused), Google, Apple, then Play as guest (this device only)
- [`R9Phones.dc.html`](R9Phones.dc.html) - Rev 9 - sign-in 3: phones - iPhone puts Apple first, Android puts Google first, guest last, no keycaps
- [`R9Waiting.dc.html`](R9Waiting.dc.html) - Rev 9 - sign-in 4: Google on PC - finish in your browser (waiting), and the cancelled / timed-out window
- [`R9Account.dc.html`](R9Account.dc.html) - Rev 9 - sign-in 5: Settings > Account for a guest - the guest warning, linked accounts with LINK buttons, Sign out, Delete account
- [`R9AccountLinked.dc.html`](R9AccountLinked.dc.html) - Rev 9 - sign-in 5b: Settings > Account once Steam is linked
- [`R9Conflict.dc.html`](R9Conflict.dc.html) - Rev 9 - sign-in 6: the account you are linking already has a pilot - choose which pilot to fly
- [`R9Notes.dc.html`](R9Notes.dc.html) - Rev 9 - the sign-in flow, guests and linking, what gets built, questions for the owner
- [`R9bChooser.dc.html`](R9bChooser.dc.html) - Rev 9b - the sign-in chooser to Google's and Apple's brand rules: Google's official dark button (unaltered), Apple's white button the same size, no Steam button (Steam signs in silently), focus as a ring around the button

### Rev 8 - Scoreboard  (page `rev8`)

- [`R8ScoreboardA.dc.html`](R8ScoreboardA.dc.html) - Rev 8 - scoreboard, option A: the podium - the winning team on a podium (1st raised), the other team beside it, element stats, the reward strip counting, PLAY AGAIN with rematch faces
- [`R8ScoreboardB.dc.html`](R8ScoreboardB.dc.html) - Rev 8 - scoreboard, option B: the glass ladder - ranked dark-glass rows with a thin team stripe, grouped under team headers (team time once), each pilot's crystal share as a teal line, your row in sky blue
- [`R8ScoreboardC.dc.html`](R8ScoreboardC.dc.html) - Rev 8 - scoreboard, option C: your match first - a big card for you (place, hull, stats against your best), standings in a side rail, the reward strip
- [`R8Notes.dc.html`](R8Notes.dc.html) - Rev 8 - what was wrong with the built board, what every option does, and the questions for the owner

### Rev 7 - Playtest  (page `rev7`)

- [`R7Selection.dc.html`](R7Selection.dc.html) - Rev 7 - selection: one glowing blue everywhere (a bright sweep running round the border, a breathing glow) instead of white - tiles, tabs, the nav and game cards
- [`R7AIDifficulty.dc.html`](R7AIDifficulty.dc.html) - Rev 7 - the AI row on the configure: AI PILOTS - EASY / MEDIUM / HARD under the pilots, the host picks, every AI seat wears the pick (MEDIUM)
- [`R7WindowSlide.dc.html`](R7WindowSlide.dc.html) - Rev 7 - LT / RT inside a window: the hub strip under the nav (Toy Box, Arcade, Arena, Mission) and arrows on both edges; RT slides to the next window, LT back
- [`R7FriendProfile.dc.html`](R7FriendProfile.dc.html) - Rev 7 - an online friend's profile: hover (or focus) their avatar in the party panel and a card pops beside it - avatar, name, FRIEND / ONLINE, where they are (lobby 2 / 4, Skim Race), what they fly, JOIN / INVITE
- [`R7Loading.dc.html`](R7Loading.dc.html) - Rev 7 - changing cell: instead of black, the living lattice, an omni crystal filling as the cell builds, ENTERING GARLAND, the four build steps lighting in their elements, a tip, and the slim bar
- [`R7Notes.dc.html`](R7Notes.dc.html) - Rev 7 - what changes, and the questions for the owner

### Rev 6 - Elemental  (page `rev6`)

- [`R6Language.dc.html`](R6Language.dc.html) - Rev 6 - the elemental design language: a living lattice (energy running along it) instead of the grid, and the four elements as UI roles
- [`R6Arcade.dc.html`](R6Arcade.dc.html) - Rev 6 - the Arcade in the elemental language: lattice backdrop, every card wears the element its mode leans on, PLAY is Charge, the quest track is Time
- [`R6Ark.dc.html`](R6Ark.dc.html) - Rev 6 - the Ark, under the nav where it belongs: lattice in the recipe panel, each element row in its own light, an omni rim on what is about all four
- [`R6FriendsOrb.dc.html`](R6FriendsOrb.dc.html) - Rev 6 friends, option A (closed): the FRIENDS tab is gone - a presence orb bottom-left: you at the centre, your party bonded to you, online friends orbiting
- [`R6FriendsParty.dc.html`](R6FriendsParty.dc.html) - Rev 6 friends, option A (open): the orb blooms into your party as a molecule - bonds to each member in their domain colour, an open socket to invite - then who is online
- [`R6FriendsRail.dc.html`](R6FriendsRail.dc.html) - Rev 6 friends, option B: a bond rail on the left edge - you, your party bonded below you, an invite socket, then online friends; hover a friend for a card, LB opens the full list
- [`R6Notes.dc.html`](R6Notes.dc.html) - Rev 6 - what changes, how it is built in Unity, and the questions for the owner

### Rev 5  (page `rev5`)

- [`R5SeatHover.dc.html`](R5SeatHover.dc.html) - Rev 5 - AI seat hover: the row does not grow; its border lights in the domain colour and the role slot reads the hull (SPARROW); the (i) is a small hex in the row, left of the X
- [`R5SeatInfoCard.dc.html`](R5SeatInfoCard.dc.html) - Rev 5 - the vessel info card: a glass popover beside the seat (a notch points at the row) - art, Ruby AI - Space vessel, SPARROW, the Hangar line, the four abilities with element hexes and controls; X or a tap outside closes it
- [`R5Notes.dc.html`](R5Notes.dc.html) - Rev 5 - Yash's AI seat info in style B: what changes, and what stays

### Rev 4  (page `rev4`)

- [`R4Configure.dc.html`](R4Configure.dc.html) - Rev 4 - Configure, simple: one framed live preview (background + glowing border) and ONE settings column - no step cards, no tree
- [`R4ConfigureFlying.dc.html`](R4ConfigureFlying.dc.html) - Rev 4 - Configure while flying: you fly IN the frame; STOP and FULL SCREEN (the option) above it; settings stay put
- [`R4Notes.dc.html`](R4Notes.dc.html) - Rev 4 - what changes, and the two questions for the owner
- [`R4HangarStage.dc.html`](R4HangarStage.dc.html) - Rev 4 - Hangar stage: a lit hangar bay (holo floor, rings, lights, motes) instead of a black box; RT pressed - the Sparrow fires bolts with a muzzle flash
- [`R4HangarUpgrade.dc.html`](R4HangarUpgrade.dc.html) - Rev 4 - Hangar upgrades: every element has an UPGRADE button with its crystal price; Mass selected
- [`R4HangarUpgradeAnim.dc.html`](R4HangarUpgradeAnim.dc.html) - Rev 4 - Hangar upgrade animation (loops): press UPGRADE - crystals fly to the hull, the next pip fills white then Mass blue, the hull pulses and swells, a ring bursts, LEVEL 4

### Rev 3  (page `rev3`)

- [`R3HangarAbilities.dc.html`](R3HangarAbilities.dc.html) - Rev 3 - Hangar: Abilities tab, RT held - the 3D Sparrow fires its cannons
- [`R3HangarUpgrades.dc.html`](R3HangarUpgrades.dc.html) - Rev 3 - Hangar: Upgrades tab on arrival - no element selected, the default hull
- [`R3HangarUpgradesMass.dc.html`](R3HangarUpgradesMass.dc.html) - Rev 3 - Hangar: Upgrades tab with Mass previewed at level 3 - the hull morphs on the 3D model
- [`R3HangarGames.dc.html`](R3HangarGames.dc.html) - Rev 3 - Hangar: Games tab - the vessel's games as wide art cards
- [`R3HangarPilots.dc.html`](R3HangarPilots.dc.html) - Rev 3 - Hangar: Pilots tab - the old Captains back as pilots, one per element, with level, XP and quote
- [`R3HangarArtifacts.dc.html`](R3HangarArtifacts.dc.html) - Rev 3 - Hangar: Artifacts tab - one slot, a real upgrade, flagged at risk
- [`R3HangarTabSwap.dc.html`](R3HangarTabSwap.dc.html) - Rev 3 - Hangar: the tab swap in motion - loops through all five tabs
- [`R3Home.dc.html`](R3Home.dc.html) - Rev 3 - Home: icons in the nav, white selection, cards docked under the pilot, toasts right, friends tab
- [`R3HomeFriends.dc.html`](R3HomeFriends.dc.html) - Rev 3 - Home: friends drawer open from the left (minimise, maximise, close), weekly card minimised
- [`R3Arcade.dc.html`](R3Arcade.dc.html) - Rev 3 - Arcade with the friends panel gone: three tall featured cards (the quest-view rhythm) and a filtered grid
- [`R3ConfigureA.dc.html`](R3ConfigureA.dc.html) - Rev 3 - Configure option A: Cinematic - the live preview fills the window, settings on a bottom console
- [`R3ConfigureB.dc.html`](R3ConfigureB.dc.html) - Rev 3 - Configure option B: three tall step cards (vessel, intensity, pilots) under a wide live preview
- [`R3ConfigureC.dc.html`](R3ConfigureC.dc.html) - Rev 3 - Configure option C: split console - big preview with Briefing / Controls / Objective tabs, a clean settings list
- [`R3ConfigureNotes.dc.html`](R3ConfigureNotes.dc.html) - Rev 3 - Configure: the three options compared
- [`R3HudSquirrel.dc.html`](R3HudSquirrel.dc.html) - Rev 3 - Squirrel HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudSparrow.dc.html`](R3HudSparrow.dc.html) - Rev 3 - Sparrow HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudDolphin.dc.html`](R3HudDolphin.dc.html) - Rev 3 - Dolphin HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudScarab.dc.html`](R3HudScarab.dc.html) - Rev 3 - Scarab HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudUrchin.dc.html`](R3HudUrchin.dc.html) - Rev 3 - Urchin HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudManta.dc.html`](R3HudManta.dc.html) - Rev 3 - Manta HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudRhino.dc.html`](R3HudRhino.dc.html) - Rev 3 - Rhino HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudSerpent.dc.html`](R3HudSerpent.dc.html) - Rev 3 - Serpent HUD: its own icons, element colours drifting like a lava lamp
- [`R3HudButterfly.dc.html`](R3HudButterfly.dc.html) - Rev 3 - Butterfly HUD: its own icons, element colours drifting like a lava lamp

### Start flow  (page `start`)

- [`StartSplash.dc.html`](StartSplash.dc.html) - Start - Bootstrap splash
- [`StartRetry.dc.html`](StartRetry.dc.html) - Start - Connection lost (retry)
- [`StartSignIn.dc.html`](StartSignIn.dc.html) - Start - Sign in
- [`StartSignInFailed.dc.html`](StartSignInFailed.dc.html) - Start - Sign-in failed / offline
- [`StartName.dc.html`](StartName.dc.html) - Start - Pilot name
- [`StartNameTaken.dc.html`](StartNameTaken.dc.html) - Start - Pilot name taken
- [`StartAgeGate.dc.html`](StartAgeGate.dc.html) - Start - Privacy: age gate
- [`StartConsent.dc.html`](StartConsent.dc.html) - Start - Privacy: consent
- [`StartLoading.dc.html`](StartLoading.dc.html) - Start - Boot world loading (built)
- [`StartNotes.dc.html`](StartNotes.dc.html) - Start - Notes

### In-game HUD  (page `ingame`)

- [`HudMatch.dc.html`](HudMatch.dc.html) - Item 12 - Match HUD - interactive (press the feed for a toast, a score to punch it)
- [`HudCardStates.dc.html`](HudCardStates.dc.html) - Item 12 - Ability card states at 2x
- [`HudNotes.dc.html`](HudNotes.dc.html) - Item 12 - In-game HUD - what changes
- [`HudSquirrel.dc.html`](HudSquirrel.dc.html) - Item 12 - Squirrel HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudSparrow.dc.html`](HudSparrow.dc.html) - Item 12 - Sparrow HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudDolphin.dc.html`](HudDolphin.dc.html) - Item 12 - Dolphin HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudScarab.dc.html`](HudScarab.dc.html) - Item 12 - Scarab HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudUrchin.dc.html`](HudUrchin.dc.html) - Item 12 - Urchin HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudManta.dc.html`](HudManta.dc.html) - Item 12 - Manta HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudRhino.dc.html`](HudRhino.dc.html) - Item 12 - Rhino HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudSerpent.dc.html`](HudSerpent.dc.html) - Item 12 - Serpent HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudButterfly.dc.html`](HudButterfly.dc.html) - Item 12 - Butterfly HUD (its own icons, style-B chrome; 1.5x annotation)
- [`HudReady.dc.html`](HudReady.dc.html) - Item 12 - Ready and countdown - interactive
- [`HudPause.dc.html`](HudPause.dc.html) - Item 12 - Pause
- [`HudConnecting.dc.html`](HudConnecting.dc.html) - Item 12 - Connecting, building the arena
- [`HudEndReveal.dc.html`](HudEndReveal.dc.html) - Item 12 - Match end reveal
- [`HudScoreboard.dc.html`](HudScoreboard.dc.html) - Item 12 - Scoreboard - interactive (vote to play again)
- [`HudSpectator.dc.html`](HudSpectator.dc.html) - Item 12 - Spectating
- [`MaelstromHub.dc.html`](MaelstromHub.dc.html) - Maelstrom - Hub between rounds
- [`MaelstromStats.dc.html`](MaelstromStats.dc.html) - Maelstrom - Stats (tournament decided)
- [`MaelstromSummary.dc.html`](MaelstromSummary.dc.html) - Maelstrom - Summary
- [`MaelstromNotes.dc.html`](MaelstromNotes.dc.html) - Maelstrom - Notes

### Item boards (before the paged rounds; no page)

- [`Main.dc.html`](Main.dc.html) - Item 1 · Top nav · interactive (click tabs, LT / RT)
- [`Hangar.dc.html`](Hangar.dc.html) - Item 2 · Hangar · interactive (pick a vessel, unlock, confirm / cancel)
- [`HangarNotes.dc.html`](HangarNotes.dc.html) - Item 2 · Hangar · what changes
- [`Arcade.dc.html`](Arcade.dc.html) - Item 3 · Arcade · interactive (party: accept, invite, kick, leave; games: star, pick)
- [`ArcadeNotes.dc.html`](ArcadeNotes.dc.html) - Item 3 · Arcade · what changes
- [`Configure.dc.html`](Configure.dc.html) - Item 4 · Configure · interactive, revision 2 (fuller tree, motion: intensity, domain, + AI, fly the preview, Start)
- [`ConfigureNotes.dc.html`](ConfigureNotes.dc.html) - Item 4 · Configure · what changes
- [`WeeklyLeaderboard.dc.html`](WeeklyLeaderboard.dc.html) - Item 4 · Weekly leaderboard · interactive, revision 2 (podium, live countdown, tabs)
- [`Profile.dc.html`](Profile.dc.html) - Item 5 · Profile · interactive, revision 2 (record, quest goals, claim ceremony, Episodes refuses)
- [`ProfileNotes.dc.html`](ProfileNotes.dc.html) - Item 5 · Profile · what changes
- [`Ark.dc.html`](Ark.dc.html) - Item 7 · Ark · interactive, revision 2 (recipe panel, Convert runs the sequence)
- [`ArkNotes.dc.html`](ArkNotes.dc.html) - Item 7 · Ark · what changes
- [`ToyBox.dc.html`](ToyBox.dc.html) - Item 8 · Toy Box · interactive, revision 2 (live activity clock, shuffle rolls, toys)
- [`ModesNotes.dc.html`](ModesNotes.dc.html) - Item 8 · Arena, Toy Box, Mission, Toy configure · what changes
- [`Settings.dc.html`](Settings.dc.html) - Item 9 · Settings · interactive, revision 2 (descriptions, tabs, toggles, dropdowns cycle)
- [`SystemNotes.dc.html`](SystemNotes.dc.html) - Items 9, 10, 11 · what changes
- [`ClosedSurfaces.dc.html`](ClosedSurfaces.dc.html) - Item 10 · Closed surfaces as a player meets them
- [`Home.dc.html`](Home.dc.html) - Item 6 · Home · interactive, revision 2 (Up next cards, hub icons, lamp asks to go offline)
- [`HomeNotes.dc.html`](HomeNotes.dc.html) - Item 6 · Home · what changes
- [`TopNavHover.dc.html`](TopNavHover.dc.html) - Item 1 · Top nav · HANGAR active, pointer over ARK
- [`TopNavLocked.dc.html`](TopNavLocked.dc.html) - Item 1 · Top nav · PORT pressed: locked, refused
- [`MenuKit.dc.html`](MenuKit.dc.html) - Item 0 · Component kit (approved; reference copy)
- [`HangarLocked.dc.html`](HangarLocked.dc.html) - Item 2 · Hangar · locked Manta, gamepad focus on its tile
- [`ArcadeSolo.dc.html`](ArcadeSolo.dc.html) - Item 3 · Arcade · solo, friends online, gamepad focus on a card
- [`ConfigureArena.dc.html`](ConfigureArena.dc.html) - Item 4 · Configure · Arena card, picking a hull (Manta taken)
- [`LeaderboardEmpty.dc.html`](LeaderboardEmpty.dc.html) - Item 4 · Weekly leaderboard · no times yet
- [`ProfileFresh.dc.html`](ProfileFresh.dc.html) - Item 5 · Profile · new pilot, first quest in progress
- [`ArkConverting.dc.html`](ArkConverting.dc.html) - Item 7 · Ark · converting (Mass merging)
- [`ToyBoxClaimed.dc.html`](ToyBoxClaimed.dc.html) - Item 8 · Toy Box · activity claimed, last shuffle, Wander unavailable
- [`SettingsDisplay.dc.html`](SettingsDisplay.dc.html) - Item 9 · Settings · Display
- [`PurchaseConfirm.dc.html`](PurchaseConfirm.dc.html) - Item 10 · Purchase confirmation (blind, unreachable today)
- [`HomeInvite.dc.html`](HomeInvite.dc.html) - Item 6 · Home · party invite arriving
- [`HangarSpend.dc.html`](HangarSpend.dc.html) - Item 2 · Hangar · spend crystals (enough)
- [`ArcadeNoFriends.dc.html`](ArcadeNoFriends.dc.html) - Item 3 · Arcade · solo, no friends online
- [`ConfigureMaelstrom.dc.html`](ConfigureMaelstrom.dc.html) - Item 4 · Configure · Maelstrom: pool and format
- [`ProfileModal.dc.html`](ProfileModal.dc.html) - Item 5 · Profile window · interactive, revision 2 (preview column, pick an avatar, type a name)
- [`ArkDone.dc.html`](ArkDone.dc.html) - Item 7 · Ark · converted
- [`ArenaWindow.dc.html`](ArenaWindow.dc.html) - Item 8 · Arena · gamepad focus on Broadside
- [`SettingsPerformance.dc.html`](SettingsPerformance.dc.html) - Item 9 · Settings · Performance after a hand change (Custom, restart notice)
- [`Toasts.dc.html`](Toasts.dc.html) - Item 11 · Menu toasts · interactive, revision 2 (raise any message; lifetime drains)
- [`HomeFocus.dc.html`](HomeFocus.dc.html) - Item 6 · Home · gamepad focus on Arena
- [`HangarShort.dc.html`](HangarShort.dc.html) - Item 2 · Hangar · spend crystals (not enough)
- [`ArcadePicks.dc.html`](ArcadePicks.dc.html) - Item 3 · Arcade · favourites, party picks, locked look (illustration)
- [`ConfigureWeekly.dc.html`](ConfigureWeekly.dc.html) - Item 4 · Configure · weekly challenge
- [`ProfileName.dc.html`](ProfileName.dc.html) - Item 5 · Profile window · display name, valid
- [`MissionWindow.dc.html`](MissionWindow.dc.html) - Item 8 · Missions (hub entry still Unavailable)
- [`SettingsOther.dc.html`](SettingsOther.dc.html) - Item 9 · Settings · Other
- [`HomeOffline.dc.html`](HomeOffline.dc.html) - Item 6 · Home · offline, asking to go online
- [`HangarNoArt.dc.html`](HangarNoArt.dc.html) - Item 2 · Hangar · vessel with no preview art or abilities (Scarab)
- [`ConfigureWeeklySpent.dc.html`](ConfigureWeeklySpent.dc.html) - Item 4 · Configure · weekly challenge, already played today
- [`ProfileNameError.dc.html`](ProfileNameError.dc.html) - Item 5 · Profile window · display name, invalid
- [`ToyConfigure.dc.html`](ToyConfigure.dc.html) - Item 8 · Toy configure · interactive, revision 2 (Vessel Changer)
- [`Credits.dc.html`](Credits.dc.html) - Item 9 · Credits
- [`ConfigureGuest.dc.html`](ConfigureGuest.dc.html) - Item 4 · Configure · party guest after Ready
- [`ProfileNameTaken.dc.html`](ProfileNameTaken.dc.html) - Item 5 · Profile window · display name taken
- [`ToyConfigurePaint.dc.html`](ToyConfigurePaint.dc.html) - Item 8 · Toy configure · Connect the Dots, paused painting picked
- [`ConfigureLocked.dc.html`](ConfigureLocked.dc.html) - Item 4 · Configure · Joust, Add AI armed, locked intensity pressed
- [`ToyConfigureSpawn.dc.html`](ToyConfigureSpawn.dc.html) - Item 8 · Toy configure · Spawn Matrix, inside Fauna / Shark
- [`ToyConfigureDomain.dc.html`](ToyConfigureDomain.dc.html) - Item 8 · Toy configure · Domain Changer (tap applies, no commit)

<!-- design_board.py readme: end -->
