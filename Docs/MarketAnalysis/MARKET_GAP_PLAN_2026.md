# Cosmic Shore: Market Gap Plan, September 2026

> **What this is.** An answer to one question from the founder: *look over the games market, consider the tagline "a casual game for hardcore gamers", find what our market wants and what we are missing, and devise a plan to close the gap.* It is written against the repository at commit `e800630d` on `bleeding-edge` and against the market as of the first week of September 2026. The evidence behind every claim is in three generated appendices in this folder: `RESEARCH_DIGEST.md` (twelve market-research passes plus a critic), `AUDIT_DIGEST.md` (a seven-dimension audit of what the repository actually ships, each dimension adversarially re-verified) and `SOURCES.md` (602 URLs). Where this plan reverses a decision recorded in `Docs/STEAM_EA_INVESTOR_CHECKPOINT.html` or in the repo's own docs, it says so in the row.
>
> **Verification status.** No Unity session ran while this was written. Every code claim comes from reading source, scenes and assets at `e800630d`, and several claims made by the research and audit agents were corrected against the code before they reached this document (see §11). Nothing here has been played.

---

## 1. Verdict

**The market that will pay $15 for Cosmic Shore is the hardcore restarter**: the Trackmania, Distance and Hyper Demon player who plays alone most nights and drags friends in on a Friday. Every small-team arcade racer in that segment has survived on ghosts, medals and leaderboards at a few hundred concurrent players, and every friends-first multiplayer title in the comparable set settled at 2 to 6 percent of its launch concurrency within months. That is the population Cosmic Shore will have, and the segment that stays Very Positive at that population is the one whose loop does not need anyone else online.

**We have** a flight model with real depth (drift, sword pose, a fleet of four complete hulls), a friends-party spine that works at two players, thirteen reachable modes, and a living reef that no competitor ships.

**We are missing** the one thing the segment requires and the one thing every Steam launch requires:

1. A repeatable solo loop: a fixed target to beat, a restart in seconds, a record that survives the session. Today the closest thing is Skim Race, which reloads a whole scene with no measured budget, seats a per-peer food web, inherits a hidden handicap, and writes personal bests to Cloud Save that nothing reads.
2. A store page. There is no Steam app id and there are zero wishlists, so every conversion number in the research is currently being multiplied by zero.

Behind those two sit four gaps the buyer will feel inside the two-hour refund window: no rebinding or sensitivity and binary keyboard triggers; hidden handicaps and unlabelled bots; an anonymous per-install identity that dies with a reinstall and cannot be found by a Steam friend; and a front door that has never been walked through in a release build.

**The plan** builds **Time Attack** on the four authored Skim Race circuits inside a fauna-free cell as the forever loop; puts a Coming Soon page live by **October 15, 2026** and a demo live by **November 15**; adopts Steam identity so friends can find each other and profiles survive; frees the fleet; fixes controls and competitive integrity; and moves Early Access to **March 16, 2027** so the game gets its one Next Fest (February 22 to March 1, 2027) at the launch that earns most. That last move reverses the recorded plan to reserve Next Fest for 1.0. The reef stays the hook for the trailer and the party cards and is narrated so emergence reads as a story rather than as bugs, but copy never claims it inside Time Attack and it is never the headline.

**The honest expectation is survival, not a hit.** About 7,000 wishlists at launch converts to roughly 800 units in week one and a low five-figure gross in the first quarter of Early Access. The comparables prove that this loop keeps a small team's game Very Positive; they do not prove it pays a team of eight. The founder must confirm runway independent of sales before October 15, and this document carries dated gates at which the plan pivots or stops.

---

## 2. What the market wants

Ranked by how much of the segment's purchase and retention decision each carries. "Both" means the hardcore restarter and the friend they invite.

| # | Want | Who | Evidence | Our status |
|---|---|---|---|---|
| 1 | **A repeatable solo loop**: restart under two seconds, a fixed target, a record that survives the session | Hardcore | Small-team AG racers never passed 452 average CCU yet hold Very Positive on ghosts, medals and boards; Trackmania grew 882 to 1,398 average CCU on four medals per track and a 1.5 s restart | **Absent.** Skim Race replay is a full scene reload with no budget; medals do not exist; per-mode PBs are written to Cloud Save for 4 of 13 modes and read by nothing |
| 2 | **A store page months early** that accumulates wishlists | Both | Wishlist to week-one conversion 0.10 to 0.15x (0.10x above $10); Popular Upcoming needs about 7k; a demo out months early earns about 2.5x at Next Fest; 68 to 88 percent of fest wishlists come from the page | **Absent.** No app id, no page, zero wishlists |
| 3 | **Controls that rebind and tune**, with analog throttle on keyboard | Both (hardcore refunds over it) | Rebinding is the most-cited day-one refund trigger in the sweep; 50.5 percent of Steam hardware is 1080p on 3060-class | **Absent.** No rebinding code anywhere, no sensitivity, keyboard triggers are binary 1/0 so drift depth and sword pose are pad-only, Invert Y is dead on one-thumb hulls |
| 4 | **Fair competition**: no hidden handicap where a number is published, bots labelled, sides equal | Hardcore | Mario Kart World's "RNG lottery" revolt; Splatoon's Tricolor leader punishment; Fall Guys refused unequal team rounds | **Partial.** Comeback has only a per-game switch and five roster cards inherit the unauthored default; the living-heart fauna buff grants element levels in every cell; bot count and difficulty are shown nowhere |
| 5 | **Friends-first play with no friction**: invite from Steam, join a friend, survive the host dropping | Both | Every friends-first comparable settled at 2 to 6 percent of launch CCU; friendslop lives or dies on join friction | **Partial.** Friends party up to 4 with AI backfill to 12, verified at 2 players on real machines; no Steam invites, no host migration, 3 and 4 unproven; every provider sign-in is a stub |
| 6 | **A readable first ten minutes** | Casual | The casual half of the tagline needs the game to explain itself before the first loss | **Dead.** FTUE and the quest chain run in no shipped scene; all 13 cards and every intensity are open from first boot; a frozen 0/6 quest ladder renders in the menu |
| 7 | **Something alive to talk about** | Both, as hook | Rain World holds 94 percent of 30,384 reviews with the ecosystem as antagonist; Ecosystem sits at 76 percent where emergence reads as bugs; artificial-life-as-subject titles cap at 600 to 1,000 reviews | **Live but unnarrated.** Fauna graze and cells flip control with no toast, no name and no Codex reader |
| 8 | **A price that matches the shape** | Both | Median top-50 launch price $15.64; friendslop sells at $5 to $10 | Plan of record is $15. Keep it (§4, §7) |
| 9 | **Visible identity and records**: profile, per-mode stats, a board that is mine | Hardcore | Retention comparables hold players on personal records more than on new content | **Partial.** Stats written for 4 of 13 modes with no UI; the Port leaderboard screen is PlayFab-backed and dead; only the weekly board is live |
| 10 | **An accessibility floor**: text size, colour alternatives, remap | Both | XAG 101 asks for 18 px text at 1080p; the segment reviews for it | **Absent.** `AccessibilitySettings` has five events with zero subscribers |
| 11 | Voice or text in the party | Casual, party nights | Friendslop assumes Discord exists; in-game voice is a convenience, not a gate | Absent. Gated on population (§5, Horizon 3) |
| 12 | Handheld and Deck | Small | Deck is 0.7 percent of users and 2.75 percent of playtime | Absent and deliberately not pursued in EA |

### Positions on the questions the research left open

1. **Beachhead.** Time Attack on the Squirrel is the retention beachhead. Scarab Scramble, already recorded as the accessible party beachhead, is the friends-night door and the trailer's second beat. Two doors, one house; no third. The demo carries both, which is also the cheap parallel test of which door converts (§8).
2. **Bots: lead or hide.** Never hide. Bot count and difficulty appear on the launch card and the scoreboard from Horizon 1. Making bots *good* is a post-EA project (§5, Horizon 3). No bot-influenced result ever reaches a board.
3. **Steam tags.** The first five are racing-shaped (Racing, Arcade, Time Attack, Competitive, Space) because Racing Fest auto-selects by tag weighting. Party and ecology tags follow. Nothing life-sim in the first ten.
4. **Next Fest.** Feasible only if EA releases after March 1, 2027. Verified against Valve's own pages: a game appears in exactly one Next Fest, only unreleased titles qualify (an EA release counts as released), registration closes January 10, 2027 at 11:59 pm PST, final demo build February 8, trailer opt-out February 11, the fest runs February 22 to March 1, and release is allowed only after March 1.
5. **Who the buyer is.** The hardcore restarter first, the friend they invite second. Not the casual solo player and not the artificial-life audience, whose ceiling is 600 to 1,000 reviews.
6. **Mode count at EA.** Six verified cards on the arcade grid, everything else in a labelled Lab tab with a published graduation rule. Thirteen open cards of uneven quality is how a 93-percent-positive game loses 95 percent of its players in six months.
7. **Achievements.** Keep the recorded deferral to 1.0. Steamworks is adopted for identity, Join Game and Steam Input; achievements are the free rider that comes with the SDK later.
8. **Cosmetics.** Keep the recorded cut through EA. Revisit at day 180 only if concurrency holds above 150.
9. **Steam Deck.** No. The build pins DX12 only and the team holds one platform. Do not verify, do not claim.
10. **Elementals on leaderboards.** Element levels that come from the world (the living-heart fauna buff) or from the handicap (comeback) are excluded from every boarded run and asserted zero at submit. Levels earned by collecting crystals laid along the circuit are part of the run and stay. That is skill.
11. **Voice.** None in EA. If 40 percent or more of matches carry three or more humans for 30 days, ship opt-in push-to-talk, default off, per-player mute.
12. **Co-op.** The Co-op Wildlife Blitz scene is not in `EditorBuildSettings`. The co-op offer is the "Party vs the HyperSea" preset (all humans on one domain, bots on the others), built from the existing domain machinery. No bespoke co-op mode.
13. **The reef as pitch.** World hook and trailer beat, never headline. Artificial-life language lives only in the Codex.
14. **Intensity.** Name the four tiers by what the reef does, using the cell's own Calm / Restless / Frenzy vocabulary, and key medals per intensity.
15. **Price arithmetic.** $15 is *below* the $15.64 median top-50 launch price, not above it. Keep $15.
16. **Weekly attempts.** Three per UTC day from Horizon 2, unlimited once Time Attack's restart is measured under three seconds. This reverses the one-attempt rationale in `Docs/WEEKLY_CHALLENGE.md`, which was written for a scarce attempt against a slow restart.

---

## 3. What we are missing

Ranked by what each costs us if it is still true on launch day.

1. **No store presence.** Zero wishlists. Every other gap is invisible while this one stands.
2. **An unverified front door.** 59 of 64 QA backlog items had never been run as of 2026-08-13; no Windows *release* player build has been through QA; the scheduled IL2CPP tier built with the development flag on September 1 with tests skipped. Known P0s: the `Arcade` singleton lives in no scene, so the Hangar's Training modal dereferences null on launch; `GameCanvas-SkimRace.prefab` is a hard copy carrying eight dangling cross-prefab references and is instanced by 12 of 13 shipped scenes; a frozen 0/6 quest ladder renders in Menu_Main. One audit verifier claimed the arcade grid renders eleven of twelve cards locked on a fresh profile. That claim is wrong on the code (`GameModeProgressionService` is instantiated by no shipped scene and `ArcadeExploreView` null-checks it open), but it must still be settled on a fresh machine in week one, because a build nobody has walked through is how such disagreements arise.
3. **No forever loop.** No fast restart, no medals, no PB on the results screen, no ghost. Skim Race is the closest thing and it seats fauna (three configs on a 30 s wave clock, food floor 10), inherits the default comeback rate and reloads the scene with no budget.
4. **Competitive integrity contamination.** Comeback is on in five roster cards; the living-heart fauna buff (`DomainFaunaBuffSystem`) grants element levels to a whole domain from every living heart of its colour, in every cell that has fauna, simulated per peer; the weekly leaderboard time is submitted from the client; ecology-scored modes run fauna client-local so a kill on one machine is a rumour on another.
5. **Controls.** No rebinding, no sensitivity, binary keyboard triggers, and the two-stick hulls have no mouse scheme because `KeyboardMouseInputStrategy` is parked. This is the refund-window gap.
6. **Friends friction.** Identity is an anonymous per-install UGS account; every provider sign-in and link in `AuthenticationServiceFacade` returns `Task.CompletedTask`; UGS has no account merge; no Steam invite, no Join Game, no recovery when the host drops; 3 and 4 player parties unproven.
7. **Onboarding is dead and everything is open.** FTUE and the quest chain run in no shipped scene; 9 of 12 cards lock to one vessel, so a new player's first choice is a vessel picker for a mode that will refuse the pick.
8. **The reef is unnarrated.** Consumption and control flips fire no player-facing event; the Codex has 33 entries and no runtime reader; intensity is a number.
9. **Progression theatre.** A 4,000-crystal vessel lock at 200 per win with the wallet display in no scene, the Scarab unlocked by an asset accident, `ClampVesselToGame` ignoring `IsLocked` (so every vessel-locked mode already hands out its hull free), and a Hangar class list that omits the Scarab and includes two vessels that do not exist.
10. **Presentation debt.** Dolphin, Urchin and Rhino ship on test art across 4 of 13 modes; one music track; boost sound muted by a loop region; engine loops on 3 of 12 hulls; no Windows icon; 1024x768 non-resizable default; build identity 0.2.0 everywhere including analytics.
11. **Build and platform hygiene.** Non-LTS Unity 6000.3.17f1, DX12-only pin, empty shader warmup collection, hand-set `ProtocolVersion 8` with unconditional connection approval, hardcoded `StandaloneWindows64` pipeline.
12. **Content shape.** Urchin has a complete elemental map and no HUD prefab; Manta, Rhino and Serpent have open design slots; the Maelstrom pool draws modes that have not passed QA.
13. **No localisation and no string tables.** Even a Simplified Chinese store page cannot be followed by a Chinese build in EA.

---

## 4. The tagline

**Keep "a casual game for hardcore gamers" as the internal design compass and the investor line. Do not print it on the store page.** It describes exactly the shape that works in this segment (low friction to start, a high ceiling to chase) and it is the shape Trackmania sells. On a capsule it reads as a paradox, and paradox taglines under-perform a concrete promise. Every 2024 to 2026 breakout one-liner in the research names a genre noun plus one twist and never an audience; a self-applied "casual" reads as shallow to the Steam buyer; capsule guidance is zero to three words.

Write the thesis down as a decision rule the team can apply, or it drifts back onto the capsule the first time someone needs copy: **"Does this make a five-minute run more measurable, or a first five minutes more readable?"** If neither, it is not on the plan.

**Player-facing hook.** *Restart until it's perfect. Then bring your friends to the reef.*

**Steam short description, Coming Soon version (October 15), only what is true today:**

> Cosmic Shore is a one-more-run arcade space racer. Drift a ship that lays trails of light around hand-built circuits, race the clock, and chase your own best. Then form a party of up to four and take on party modes in a living reef where the wildlife eats whatever you leave behind. Fully playable solo against bots.

**Launch version (add only when each ships, before the demo goes live):** "chase author medals and climb the weekly boards" replaces "chase your own best" once medals and boards exist. The ghost is not mentioned anywhere until it ships in Horizon 2.

**Two claims are removed because the build cannot prove them.** "The track is the trail you just laid" is false of Skim Race: its track is four hand-authored waypoint circuits laid by `SpawnableWaypointTrack` before the race starts, and the player collects crystals along it. Trail-as-track is real (the Urchin's rail grind, the Squirrel's tube ride) and belongs in B-roll and a later mode, not in the description of the front door. "No queue ever" markets a defect as a feature: public matchmaking is unreachable by construction (every party session is `IsPrivate = true` and the query path sits behind an early return). The EA Q&A states plainly: friends-only parties up to four with AI backfill, no public matchmaking, no voice, no host migration, Windows only, English only.

**Hold one hull number everywhere.** "Four fully realised hulls, four more in Early Access." Four hulls have complete elemental maps (Squirrel, Sparrow, Dolphin, Scarab); the Rhino gets its map in Horizon 3; Manta and Urchin stay visible with honestly LOCKED ability cards; the Serpent is hidden until its map exists.

---

## 5. The plan in three horizons

**Capacity assumptions.** Eight people, three to four of them engineers, several part-time. Engineering capacity is taken as about 130 person-days per 14-week quarter, the middle of the stated 120 to 160. Non-engineering capacity per horizon is taken as producer 50, artist 50, audio 30, designer and writer 40, tester 50. Owners are roles; the founder assigns names in week one.

**Horizon 1 is at capacity with zero slack.** The plan says so rather than pretending. The survivable core, if engineering collapses to half, is 56 engineering days: week-one P0 block 12, Steam identity 14, rebinding and sensitivity 8, competitive integrity 5, records 9, page and demo build support 8. Everything else in Horizon 1 is deferred before any of those six are, in this order: bot presets, controls-block rows, build hygiene beyond the nightly release build, Steam Input and Join Game (slip to Horizon 2), Time Attack medals and boards (slip to Horizon 2; the circuits, the restart budget and the split timer do not slip).

**Every horizon carries a QA fix reserve that is not scheduled against a feature.** The tester runs the backlog; the reserve pays for what it finds.

**Fundamentals tier** follows the order of preference in `CLAUDE.md`: 1 = uses an existing fundamental as-is, 2 = tunes parameters, 3 = extends a fundamental with a small general capability, 5 = bespoke (hygiene, tooling, platform). No initiative in this plan proposes a new fundamental.

### Horizon 1: September 7 to December 15, 2026. Foundation, page, demo.

| # | Initiative | What | Why | Effort (person-days) | Owner | Metric | Tier | Reverses a recorded decision |
|---|---|---|---|---|---|---|---|---|
| 0 | **Week-one verification and P0 block** | Produce a *release* (non-development) IL2CPP Windows build and run `QA-BUILD-WINDOWS-PLAYER` on two machines; run the five P0 items; settle the locked-grid claim on a fresh profile; fix the `Arcade` null dereference by deleting the Training modal launch path; make `GameCanvas-SkimRace` a prefab variant or re-point its eight dangling refs; remove the frozen `QuestTrackView`; decide the Co-op Wildlife Blitz scene (add to build or retire the card) | The front door has never been walked through in a shipped build. Every other initiative is downstream of this one | Eng 12, Tester 8, Producer 2 | Eng lead, Tester | Release build boots to menu and launches all 13 cards on two machines by **September 25**; P0 list empty | 5 | No |
| 1 | **Steam app id and Coming Soon page by October 15** | Steam Direct fee and app id paid by **October 1** (founder's name on it; the 30-day clock and every other Steam task hangs off it); page with two capsule variants and a 15-second clip tested on ten strangers; racing-shaped tag order; truthful copy per §4; EA Q&A written | Zero wishlists is the largest gap; 68 to 88 percent of fest wishlists come from the page | Producer 6, Artist 8 (capsules 6, screenshots 2), Designer 4, Eng 2 | Producer | Page live **October 15**; 500 wishlists by **November 15**; 7 of 10 strangers can say what the game is after the clip | 1 | No. A page was absent, not decided against |
| 2 | **Steam identity** | Steamworks.NET (new dependency, flagged); **Steam-first sign-in to UGS with anonymous fallback** for offline and dev machines; Rich Presence and Join Game feeding `PartyInviteController`'s accept path; Steam Input configuration authored and owned by the IO engineer. Migration stated honestly: UGS has no account merge, so existing anonymous profiles either link on next launch while the device cache is still valid or are lost, and the page says which. Test plan: fresh install, existing anonymous profile, second machine | Friends cannot find a per-install anonymous ID and profiles die with a reinstall; the friends party is the only multiplayer we offer | Eng 24 (identity 14, Join Game 6, Steam Input 4) plus a party regression pass from the reserve | Netcode engineer; Steam Input: IO engineer | Two-machine test: a Steam friend's Join Game lands in the party within 20 s; profile survives reinstall; anonymous fallback still boots offline | 5 | **Yes.** Reverses "no Steamworks SDK at launch". Justified by identity, not achievements, which stay deferred |
| 3 | **Controls** | Rebinding UI for pad and keyboard; sensitivity and dead zone; analog throttle ramp on the Shift triggers as a separately play-tested tuning change with a recorded QA session; revive the parked `KeyboardMouseInputStrategy` as a selectable scheme for two-stick hulls, honouring the one-detector law in `ONE_THUMB_MOUSE_CONTROLS.md` §4.0. Drop the "add keyboard glyph labels" sub-task: the verifier confirmed the labels already cover every live binding, and chips appear when a vessel's map is authored, not when glyphs are | The refund-window gap; the deepest mechanics are unreachable on the device most Steam players use | Eng 12 | IO engineer | Every binding rebindable; zero of the first 100 reviews cite controls as the refund reason | 3 | No |
| 4 | **Competitive integrity** | Author `ComebackRatePerScoreDeficit` explicitly on the five roster cards that inherit the default (Skim Race, Joust, Scurry, Astro League, Brood Rush) with a `--check` build gate; per-lobby "Competitive rules" toggle on the launch card, default on in party cards, forced off in Time Attack and Weekly Shorts, shown on the lobby card and the end screen; **re-applied by the controller after every `SyncFromArcadeGame`**, because `MaelstromController` re-syncs the card each round and would silently re-arm the rate; leaderboard submit asserts comeback 0 **and** fauna-buff contribution 0; docs state the true magnitude (the bonus is divided by ten before it reaches `ResourceSystem`, so a Joust deficit of 3 at rate 1.0 buys at most 3 levels, while the count-metric cards Skim Race and Scurry pin all four elements at 10 from a 10-crystal deficit) | Hidden handicaps are the fastest way to lose the hardcore half | Eng 5 | Gameplay engineer | Build check green; no boarded run with a nonzero handicap or fauna contribution in analytics | 2 | No |
| 5 | **Records** | A reader for the per-mode Cloud Save stats keyed (mode, intensity, bot difficulty); `ReportModeResult` calls for the nine unreported modes; publish `GolfScoring` onto `GameDataSO` so scoring direction stops being a hardcoded three-mode switch; PB and delta on every results screen; wire or delete `GetEvaluatedHighScore` (zero callers); a one-line warning that records live on this device until Steam is linked, removed when initiative 2 lands | Records are written for 4 of 13 modes and read by nothing; PBs are the cheapest retention hook and the cheapest evidence that players care about a personal best at all | Eng 9, Designer 1 | Gameplay engineer | Every results screen shows a PB line | 1 | No |
| 6 | **Free the fleet, delete progression theatre** | `ownedFromStart` on all eight spawnable hulls; fix `SO_Classlist_Classes` (add Scarab, drop Grizzly and Termite); remove crystal earn toasts and hide the wallet until a sink exists; delete the quest chain, `QuestTrackView` and the Training modal | The lock gates nothing a player plays (`ClampVesselToGame` never tests `IsLocked`; the freestyle roster has no ownership filter), the wallet is invisible, and a $15 buyer reads the residue as free-to-play. A counter that goes up for twelve months toward nothing is worse than no counter | Eng 4 | Gameplay engineer | Every complete-map vessel selectable on first boot; no currency visible anywhere | 2 | **Yes.** Reverses the recorded crystal vessel economy |
| 7 | **Time Attack, the forever loop** | A new Squirrel card over the **four authored Skim Race circuits** at four intensities; a new `CellConfigDataSO` derived from `Barren Cell Config` (no flora, no fauna, therefore no living-heart buff and a deterministic world) carrying the waypoint-track spawnables; solo seat, no bots; comeback forced 0; **restart is a scene reload with a measured budget under 3 s on 3060-class hardware**, and the `ConnectingPanel` arena-ready gate (up to 45 s) must not engage; split timer against PB; four medals per circuit (author, gold, silver, bronze) with author times set by the designer from recorded runs, keyed (circuit, intensity); 16 all-time UGS boards created in the dashboard with ascending sort and keep-best verified. **No "seed of the day":** the shipped `SegmentSpawner` is configured with one segment and one candidate spawnable, so a seed varies nothing across days; rotating content is Weekly Shorts (Horizon 2) and designer-authored sets (Horizon 3). **In-place reset stays rejected**: it is a recorded decision, and an in-place reset would leave the crystal-anchor cursor, turn monitor and element state where they were | The segment lives on this loop; a published time needs a deterministic world, and the cheapest legal way to buy one is an authored fauna-free cell, which is "the Cell owns the environment" applied rather than an exception | Eng 30 (cell and card 6, restart budget 8, splits and medals 8, boards 6, polish 2), Designer 6, Tester 4 | Gameplay engineer | Restart under 3 s; 40 percent or more of demo sessions include Time Attack; median 8 restarts per Time Attack session | 1 (Cells, Prisms, Elementals, Vessels used as authored) | No |
| 8 | **Demo** | Demo depot from trunk behind a build flag: Time Attack circuits 1 and 2 at all intensities, Scarab Scramble against bots, the lava lamp; live **November 15**; a named maintenance owner and the rule that the demo is updated within a week of every trunk fix that touches it | Demos out months early earn about 2.5x at Next Fest, and a demo carrying both doors is the cheap parallel beachhead test | Eng 6, Producer 3, Tester 4 | Eng lead | Live November 15; Time Attack vs Scarab Scramble session share reported weekly | 1 | No |
| 9 | **Controls-block rows** | Author rows into each of the 17 `ModeControlsLibrary` mode entries. Fixing `DefaultRows` is not a fix: `RowsFor` falls to it only when a mode has no entry, and every mode has one | The launch card's controls block renders empty on every card | Eng 2, Designer 2 | IO engineer | Every launch card shows its vessel's abilities with correct chips | 2 | No |
| 10 | **Bot transparency and party presets** | Bot count and difficulty on the launch card and scoreboard; "BOTS" labels on the top bar, banner and results; a Rookie / Pilot / Ace / Legend stepper over the existing `AIPilot` skill float with the Low == High prefabs fixed; "Party vs the HyperSea" (humans one domain, bots fill the others) and "Split up" presets; equal sides enforced only when no human made an explicit domain pick, per the recorded law. The stepper is labelled honestly as pace, not skill: abilities still fire on fixed timers and `AIGunner` is an empty stub, so "good bots" is Horizon 3 | Fall Guys refused unequal sides; hidden bots read as deceit | Eng 5, Designer 1 | Gameplay engineer | Every lobby shows human and bot counts per domain | 2 (Domain, Vessels) | No |
| 11 | **Build hygiene** | Nightly *release*-tier build with tests; build identity from the git tag; Windows icon; resizable window; 1080p default; `ProtocolVersion` derived from the build; shader warmup collection populated | A development-flag build is not the product; 0.2.0 stamps analytics too | Eng 5 | Eng lead | Green release build every night for 14 days before the demo | 5 | No |
| 12 | **Audio** | Engine loop on every hull through its own FMOD `EventReference` field; boost unmuted | Engine loops on 3 of 12 hulls | Audio 12 | Audio owner | Every hull has an engine loop and an audible boost | 1 | No |
| 13 | **Art** | Capsules (counted in initiative 1); Dolphin hull art started | Dolphin flies two modes on test art | Artist 10 | Artist | Dolphin hull in review by December 15 | 1 | No |
| 14 | **QA fix reserve** | Unscheduled | The backlog will find things | Eng 20, Tester 20 | Tester | 40 of 64 backlog items run by December 15 | | |

**Horizon 1 totals:** engineering 136 person-days against about 130 available; producer 11, artist 18, audio 12, designer 14, tester 36. **Dated gates:** September 25 P0 list empty; October 1 Steam Direct paid; October 15 page live; November 15 demo live and 500 wishlists; December 15 fauna-narration verdict (Horizon 2, item 4) and Dolphin art in review.

### Horizon 2: December 16, 2026 to March 1, 2027. Ghost, weekly loop, party hardening, Next Fest.

| # | Initiative | What | Why | Effort (person-days) | Owner | Metric | Tier | Reverses a recorded decision |
|---|---|---|---|---|---|---|---|---|
| 1 | **Ghost on Time Attack** | Record poses at 20 Hz through the `RewindSystem` transform buffer; play the PB ghost as a non-networked hull **on its own ghost material path**, never through the vessel vision band's tint alpha (a marker, not an opacity; writing a third state into it redefines the marker for every vessel material); a ghost lays no prisms and carries no collider; blooms in and withers out on the continuity beat; best run local, top runs in Cloud Save. Time Attack only: a Joust ghost is a recording of a different fight | Ghosts are the second pillar of the segment's retention | Eng 14, Artist 2 | Gameplay engineer | PB ghost plays in every Time Attack run; zero prisms attributed to ghosts | 1 (Vessels, photons only) | No |
| 2 | **Weekly Shorts** | Pool shrinks to five time-scored, bot-free, deterministic activities (the four circuits plus one graduated solo activity); attempts 3 per UTC day, unlimited once restart is measured under 3 s; hide the World board until the UTC Monday rollover; wire the unsubscribed `OnAttemptProgress` into an in-run readout; remove the phantom reward tooltip; **verify in the production UGS dashboard** the three settings code cannot enforce (ascending sort, keep best, weekly reset with archive). A five-entry pool repeats a mode two weeks in five; author replacement entries only for modes with a recorded QA pass | Trackmania's Weekly Shorts is the live-ops shape that fits one designer | Eng 8, Designer 2 | Gameplay engineer | Weekly board populated every week; 25 percent of weekly actives submit a time | 2 | **Yes.** Reverses the one-attempt rationale in `Docs/WEEKLY_CHALLENGE.md` |
| 3 | **Party hardening** | 3 and 4 player parties verified in MPPM and on real machines; host-loss auto re-form on a survivor's eager Relay session with turn and checkpoint persistence; **January 31 gate** | Comparables show party sessions die on host drop; the eager per-user Relay design already gives every survivor a session to re-form onto | Eng 16, Tester 8 | Netcode engineer | Two-machine test: host quits mid-match, survivors are back in a match within 30 s with score carried | 1 | **Yes, narrowly.** Reverses the "host migration cut" for re-form only; full state migration stays cut |
| 4 | **Ecology narration** | Two new SOAP events on `CellRuntimeDataSO` (a creature consumed mass; a cell's control flipped) feeding the game toast system; time to first observed graze under 90 s in party cells; recall gate: 7 of 10 testers can name what the fauna did in their match | Emergence read as randomness scores 76 percent; narrated, 94 | Eng 6, Designer 2 | Gameplay engineer | Recall gate passed on the internal build by December 15, re-run in February | 3 (Flora and Fauna, Domain, Cells, Mass) | No |
| 5 | **Codex bestiary panel** | Runtime reader for `Codex.asset`, opened from the menu and from a fauna toast; the only place artificial-life language appears | 33 entries written, none readable | Eng 8, Writer 4 | UI engineer | Codex opens from menu and toast | 1 | No |
| 6 | **Lab tab and graduation rule** | Cards that have not passed QA move to a Lab tab; published rule: QA item passed, two humans across three sessions with no P1, controls rows authored; monthly graduation | Six good cards beat thirteen uneven ones | Eng 4, Producer 2 | Producer | Six cards on the grid at EA; rule published on the page | 5 | No |
| 7 | **Maelstrom as the labelled variety button** | Pool draws graduated cards only; no medals; comeback toggle honoured after each per-round re-sync; auto-ready with one human, skippable cinematic, run-summary sheet; **cold-load and menu-to-first-frame measured per pool mode on the floor machine before it is promoted on Home**, and any round whose overhead exceeds about 20 percent of the round leaves the demo pool | Maelstrom is 3 to 7 rounds over 15 to 35 minutes with a full arena load between each; it is the party night, not the forever loop, and must be sold as what it is | Eng 5, Tester 4 | Gameplay engineer | Pool equals the graduated set; load table published | 1 | No |
| 8 | **First Flight** | Onboarding in the lava lamp through Switch rings: thread a ring to learn skim, boost, trail on and off, ending at a Time Attack station; replaces the dead FTUE | A readable first ten minutes for the casual half, built from an existing fundamental instead of a scripted tutorial; also how a $15 buyer reaches Time Attack inside the two-hour refund window | Eng 8, Designer 3 | Gameplay engineer | 80 percent of new demo players reach Time Attack within 10 minutes | 1 (Switch, Toys, Vessels, Prisms) | No |
| 9 | **Intensity labels** | Name the four tiers by reef state (Calm / Restless / Frenzy vocabulary) and say what changes | A number tells a new player nothing | Designer 2, Eng 1 | Designer | Labels on every card | 2 | No |
| 10 | **Next Fest operations** | Register by **January 10**; final demo build by **February 8**; trailer opt-out decision by February 11; fest **February 22 to March 1** with a livestream slot; tag order confirmed for Racing Fest | The one fest we get | Producer 6, Eng 4, Tester 4 | Producer | 3,000 wishlists before the fest; 2,000 or more gained during it | | **Yes.** Reverses "Next Fest reserved for 1.0". EA is the launch that earns most: median 1.0 revenue is about 40 percent of EA, and only 20 to 21 percent of graduates do better at 1.0 |
| 11 | **Presentation** | Dolphin hull art complete; second music track, menu music, board stings | Two modes fly on test art | Artist 20, Audio 14 | Artist, Audio owner | Dolphin on final art in the fest demo | 1 | No |
| 12 | **Simplified Chinese store page text** | Contract translation of page copy only, about $300; the supported-languages field stays English | Cheap test of the largest non-English Steam market. Wishlist targets in §8 assume English-only reach; if the Chinese page pulls more than 20 percent of wishlists, string tables enter Horizon 3 | Producer 1 | Producer | Page localised | | No. The build stays English only |
| 13 | **UGS cost model** | Cost at 200, 500 and 1,000 CCU for Relay, Lobby and Cloud Save; a plan for the 100-player presence lobby cap (friends-only presence or regional shards) | Unmodelled cost is a shared blind spot | Eng 3, Producer 1 | Netcode engineer | Table published; cap plan chosen | 5 | No |
| 14 | **QA fix reserve** | Unscheduled | | Eng 20, Tester 16 | Tester | 64 of 64 backlog items run by February 8 | | |

**Horizon 2 totals:** engineering 97 person-days over 11 weeks including the holiday period; producer 10, artist 22, audio 14, designer and writer 13, tester 32. **Dated gates:** January 10 registration; January 31 party gate; February 8 fest demo final; March 2 wishlist gate.

### Horizon 3: March 2 to June 30, 2027. Launch and the first 90 days.

| # | Initiative | What | Why | Effort (person-days) | Owner | Metric | Tier | Reverses a recorded decision |
|---|---|---|---|---|---|---|---|---|
| 1 | **Launch** | March 2 wishlist gate (§8); EA release **March 16** at $15 with a 10 percent launch discount and a 2-pack at 15 percent off; weekly patches for eight weeks; review response within 48 hours; **a named community and review-response owner** | Conversion is 0.10x to 0.15x and refunds run 12.4 percent median in EA; the first eight weeks decide the review score | Producer 30, Eng 24 (patch reserve), Tester 20 | Producer | Reviews 85 percent positive at 50; refunds under 12 percent | | No. $15 keeps the recorded price; the 2-pack is a Steamworks bundle |
| 2 | **Racing Fest, April 12 to 19** | Tag order confirmed; a fest-week update carrying one new circuit | Auto-selection is by tags | Producer 3, Designer 4, Eng 4 | Producer | Selected; a measured wishlist and sales bump | 1 | No |
| 3 | **Rhino map approval and wiring** | Approve the Rhino elemental map from `FLEET_MAPS.md`, wire four icons and abilities; hide Serpent from pickers; Manta stays with LOCKED cards | Rhino fronts Astro League and Peel the Cage on an open map | Eng 12, Designer 3, Artist 4 | Gameplay engineer | Rhino audit green | 1 | No. The recorded blocker was design approval, which this asks the founder to give |
| 4 | **Good bots** | `AIPilot` difficulty decoupled from intensity; ability use keyed on state instead of fixed timers, scoped to the four complete hulls; an `AIGunner` for the Sparrow modes | Transparency shipped in Horizon 1; difficulty is the post-EA half | Eng 14 | Gameplay engineer | Testers cannot predict bot ability timing; three labelled tiers | 3 | No |
| 5 | **Time Attack set two** | Four more designer-authored circuits with medals and boards | Content the loop consumes | Designer 8, Eng 4 | Designer | Set two live by June 1 | 1 | No |
| 6 | **Open parties, conditional** | Design only unless concurrency holds at 150 or more for 30 days; if met, public sessions through UGS in the horizon's tail | A queue with no population is worse than no queue | Eng 15 if triggered | Netcode engineer | Match found within 60 s at the gate population | 1 | No |
| 7 | **Presentation** | Rhino hull art; audio polish from review feedback | Rhino flies two modes on test art | Artist 28, Audio 12 | Artist, Audio owner | Rhino on final art by June 30 | 1 | No |
| 8 | **Day 30 and day 90 reviews** | Numeric reviews against §8 with the pivot or stop branch taken in writing | A plan without a stop is a hope | Producer 4 | Founder | Review written within 5 days of each date | | No |
| 9 | **QA fix reserve** | Unscheduled | | Eng 20, Tester 20 | Tester | Zero open P1 for 14 consecutive days | | |

**Horizon 3 totals:** engineering 78 person-days unconditional, 93 if open parties trigger; producer 37, designer 15, artist 32, audio 12, tester 40. Deliberately light on engineering: the first quarter of EA is spent on what reviews say, not on what this document predicts.

### Merge policy for the unplayed backlog

About 160 agent-authored commits have never been played. From week one, a branch merges to trunk only after the tester runs its QA item or the author records "unverified" in the PR body, in which case the feature goes behind the Lab tab. The QA fix reserve pays for what this policy surfaces. Nothing in the demo or the six launch cards may depend on an unverified commit.

---

## 6. What to stop doing

1. **Stop merging unplayed work to trunk.** Apply the merge policy above.
2. **Stop building modes and vessels.** No fourteenth card. Termite, Falcon, Shrike and Grizzly stay planned; the Serpent is hidden. Six cards graduate before anything new is discussed.
3. **Stop the crystal vessel economy and the quest chain.** They are theatre with no reader and they contradict the price.
4. **Stop writing systems without readers** unless the reader ships in the same horizon. Per-mode stats and the Codex both waited for this plan.
5. **Stop describing the game with claims the build cannot prove**: "the track is the trail you just laid", "no queue ever", and any sentence that puts the reef inside Time Attack.
6. **Stop scheduling development-flag IL2CPP builds as the release tier** and stop skipping tests in the scheduled build.
7. **Stop treating attempt scarcity as a retention lever.** The weekly loop is unlimited attempts against a fast restart, not one attempt against a slow one.
8. **Stop platform conversations for twelve months.** No mobile, no Deck verification, no console. The team holds one platform.
9. **Stop letting bot-influenced or handicapped results near any board.**
10. **Stop expanding the launch card.** The presets in Horizon 1 are its last additions before EA.

---

## 7. Decisions the founder must make

| # | Decision | Options | Recommendation |
|---|---|---|---|
| 1 | **EA date and Next Fest** | (a) keep the July plan and release EA before the fest, forfeiting it forever; (b) release EA March 16, 2027 after the fest, leaving the recorded 1.0 slot empty (there is no second fest) | **(b).** EA is the launch; the amplifier belongs to it. Reverses the recorded reservation |
| 2 | **Runway** | (a) confirm cash for twelve months of EA independent of sales before October 15; (b) cannot confirm, in which case Horizon 3 becomes a maintenance plan and the fest becomes a publisher pitch | **(a), in writing.** §10's revenue expectation does not fund the team, and this plan defers revenue by about six months |
| 3 | **Steamworks** | (a) adopt for identity, Join Game and Steam Input, defer achievements; (b) keep anonymous UGS identity and ship without Steam invites; (c) adopt everything | **(a).** Reverses "no Steamworks SDK at launch"; flagged as a new dependency. Pay the Steam Direct fee by October 1 |
| 4 | **Price** | (a) $15 with a 10 percent launch discount and a 2-pack; (b) $9.99 with a public commitment to raise at 1.0; (c) $20 | **(a) as plan of record, decided finally in January.** Be honest about what carries it: after Horizon 2 the solo content is four circuits at four intensities with medals, boards, a ghost and a weekly loop, a 10 to 15 hour mastery loop, not 30. The party cards and the fleet carry the rest of the $15. If the demo's January numbers (median completed runs per session under 4, day-2 demo return under 15 percent) miss, take (b); the raise must land more than 30 days before the 1.0 transition or the 1.0 discount is void |
| 5 | **Free the fleet** | (a) free every spawnable hull and hide the wallet; (b) keep the lock and surface the wallet; (c) a Steam-side supporter unlock | **(a).** Reverses the recorded vessel economy |
| 6 | **Host-loss re-form** | (a) narrow re-form on a survivor's eager Relay session with turn persistence; (b) keep the full cut | **(a)**, gated on the January 31 two-machine test. A narrow reversal |
| 7 | **Weekly attempts** | (a) three per day, unlimited once restart is under 3 s; (b) keep one per day; (c) unlimited now | **(a).** Reverses the recorded one-attempt rationale |
| 8 | **Rhino map** | (a) approve the `FLEET_MAPS.md` proposal in Horizon 3; (b) hide the Rhino and its two modes | **(a)**, or Astro League and Peel the Cage are Lab cards forever |
| 9 | **Hiring** | (a) contract a second tester for Horizons 1 and 2 (the tester carries 68 days across them) and a contract artist for the Rhino; (b) accept that art debt ships at EA | **(a) for the tester, (b) for the artist** unless runway allows both. One tester at one session per three weeks cannot clear 59 items before March |
| 10 | **Simplified Chinese page** | (a) page text only; (b) nothing | **(a).** Does not reverse English-only |
| 11 | **Investor narrative** | Re-baseline to §10 before the page goes live | Do it before October 15 so nobody outside the team holds the July numbers |
| 12 | **Merge policy** | (a) play-gated merges per §5; (b) keep merging unplayed work | **(a)** |

---

## 8. Kill and pivot criteria

**Pre-launch gates.**

| Date | Gate | If it fails |
|---|---|---|
| **September 25, 2026** | P0 list empty; release build boots on two machines | Nothing else in Horizon 1 is scheduled until it passes |
| **November 15, 2026** | Demo live; 500 wishlists with both capsules tested | Re-cut the capsule and clip and re-run the ten-stranger test; a second failure means the demo leads with Scarab Scramble and the page is rewritten around the friends-night card |
| **December 15, 2026** | Ecology recall gate (7 of 10 testers can name what the fauna did) | Fails twice: the reef leaves the trailer's first thirty seconds and becomes a Codex feature only |
| **December 15, 2026** | Performance floor: Time Attack restart under 3 s and 60 fps at 1080p on 3060-class | Fails: Time Attack cannot be the demo's front door; the demo leads with Scarab Scramble until it passes |
| **January 31, 2027** | 3 and 4 player parties and host-loss re-form pass on real machines | The page states two-player parties; the presets are pulled; no code is written past the gate |
| **March 2, 2027** | Wishlists | 7,000 or more: launch March 16. 5,000 to 7,000: launch March 16 with the day-30 unit target halved. 2,500 to 5,000: delay EA to April 26 after Racing Fest and spend April on wishlists. Under 2,500: do not launch EA in Horizon 3, keep the demo live as the product, take the fest results to a publisher |

**Day 30 (April 15, 2027).** Continue if reviews are 85 percent positive at 50 or more, refunds under 12 percent, median session 25 minutes or longer, and Time Attack appears in 40 percent or more of sessions. Freeze features and fix the top three review complaints if positive falls under 70 percent or refunds exceed 18 percent. If Time Attack is under 20 percent of sessions, the loop is wrong and the ghost and medal work is re-examined before any new circuit is authored.

**Day 90 (June 14, 2027).** Continue if units are 3,000 or more and peak weekly concurrency is 100 or more; open parties trigger at 150 sustained for 30 days. Pivot if units are under 1,500 and concurrency under 30: a maintenance cadence with development paused, the demo becoming the product with party modes as the paid tier, or a publisher search with the fest data. The founder picks one in writing within five days.

**Day 180 (September 12, 2027).** Continue toward 1.0 if lifetime units are 10,000 or more and monthly actives 1,000 or more, and hire a second artist. Hold at maintenance if units are 5,000 to 10,000. Stop if lifetime units are under 5,000 and monthly actives under 500: cancel the 1.0 push, keep servers on the UGS free tier, ship a final patch, and say so on the page.

**Measurement caveat.** The repo's analytics are consent-gated (COPPA age gate, then opt-in), so every demo metric is a median over consenters and the consent rate must be recorded beside it. Before the demo ships, verify every event in `UGSKeys` is declared in the UGS Event Manager and derive `demo_session_length` and completed-runs-per-session from `game_completed` on the PostHog dashboard. A criterion against a metric that does not exist is not a criterion.

---

## 9. Risks

1. **Time Attack restart cannot be brought under 3 s through a scene reload.** The Barren-derived cell has no environment build, so the reload should be fast, but the `ConnectingPanel` arena-ready gate can hold up to 45 s. Mitigation: the budget is measured in week one of the initiative and the card is not done until it passes; in-place reset stays rejected because it does not reset run state.
2. **Ghosts, medals and boards prove survival, not revenue.** Every small-team racer in the comparable set stayed under 452 CCU, and Trackmania is a free game with a subscription and twenty years of tracks. Mitigation: §10 states the expectation and decision 2 requires runway confirmation.
3. **Steam-first identity breaks the offline path or dev profiles.** UGS has no account merge. Mitigation: anonymous fallback kept; the migration window and its loss stated on the page; the fresh-install, existing-profile and second-machine test plan in initiative 2.
4. **The locked-grid disagreement is a symptom of a build nobody has walked through.** Mitigation: initiative 0 on a fresh machine before anything else is scheduled.
5. **The reef's variance reads as bugs.** Mitigation: narration toasts, the recall gate, and a Codex page; ecology-scored modes stay off every board until fauna sync is verified in MPPM.
6. **Party hardening consumes more than 16 engineering days.** Mitigation: the January 31 gate; a failed gate means the page states two-player parties and no code is written past it.
7. **Wishlists stall under the 7,000 Popular Upcoming line.** Mitigation: the March 2 gate delays launch rather than launching to nobody.
8. **Non-LTS Unity and a DX12-only pin produce driver crashes on launch day.** Mitigation: the nightly release build across two GPU vendors from Horizon 1; a Vulkan or DX11 fallback is a Horizon 3 patch item if crash reports justify it.
9. **The tester is the single point of failure** for the QA backlog and the graduation rule. Mitigation: decision 9.
10. **Racing Fest tag weighting reads the game as a party game.** Mitigation: racing-shaped first five tags from page live; the fest is a bonus, not a gate.
11. **The refund window.** A $15 game with 2 to 5 minute matches must reach Time Attack inside the first thirty minutes. Mitigation: First Flight leads there; refunds are measured weekly for the first eight weeks.
12. **UGS Relay and Lobby cost grows with the concurrency the plan hopes for.** Mitigation: the Horizon 2 cost table and the presence-lobby cap plan before launch.
13. **The demo becomes a second product to maintain.** Mitigation: it is cut from trunk behind a build flag with a named owner and a one-week update rule; if it falls more than two weeks behind trunk it is pulled rather than left stale.
14. **Horizon 1 is over capacity by about 6 engineering days before the reserve is spent.** Mitigation: the survivable core and the cut order at the top of §5, applied in week four's review rather than discovered in week twelve.

---

## 10. What this plan sacrifices, honestly

- **Revenue.** About 7,000 wishlists at 0.12x is roughly 800 units in week one, near $9,000 gross at the discounted price and about $6,000 to Froglet after Steam's share; a 90-day tail of 2,500 to 3,000 units is $25,000 to $30,000 gross. Twelve months of EA at these rates is a low six-figure gross outcome at best. Moving EA to March defers whatever revenue the July plan would have booked by roughly six months.
- **The 1.0 Next Fest.** Spent on EA; there is no second one.
- **Public matchmaking, voice, full host migration, mobile, console, Deck, build localisation, cosmetics, missions and the Ark arc.** All stay cut through EA, and some may never come back.
- **Seven cards leave the launch grid for a Lab tab**, including modes that represent months of work. Some will not graduate.
- **The Serpent is hidden.** Manta, Rhino and Serpent were open design slots in July and only the Rhino gets a map in this plan.
- **The reef is demoted from headline to hook**, and artificial-life language is confined to the Codex. The platform's north star is unchanged; its marketing weight is not.
- **The crystal economy, the quest chain and the Training modal are deleted** rather than fixed.
- **The founder's July narrative.** Investors holding those numbers need the new ones before October 15.

---

## 11. Method and corrections

**How this was produced.** Three stages, each adversarially checked. (1) Twelve market-research passes (Steam economics, segment definition, friendslop, arcade vehicle games, hardcore versus casual, retention and live-ops, discoverability and taglines, handheld and console, emergent systems, multigenre rosters, the competitor set and its concurrency, three-way teams) followed by a critic pass; the result is `RESEARCH_DIGEST.md`. (2) A seven-dimension audit of the repository (multiplayer and social, onboarding, mastery and competitive integrity, content, progression and economy, presentation, platform and tech), each dimension re-verified by a second agent whose job was to break the first one's claims against the code; the result is `AUDIT_DIGEST.md`. (3) Five strategy plans from five angles (solo mastery, beachhead positioning, cold-eyed operator, co-op versus ecology, living-world differentiator), scored by three judges, then three refuters tried to break the winning plan against the code and the research before this synthesis was written. Every URL cited by any pass is in `SOURCES.md`.

**Corrections applied to the evidence before it reached this plan.** Each of these was asserted by a research or audit agent and was wrong, or wrong enough to change an initiative:

1. **Comeback magnitude.** "Rate 1.0 hands a trailing team level-10 elements from one joust" is wrong by 10x: `ElementalComebackSystem` divides the bonus by ten before it reaches `ResourceSystem`, so at rate 1.0 one point of deficit buys one integer level. Joust (target 3) gets at most +3; the saturating cards are the count-metric ones, Skim Race and Scurry. The initiative is unchanged; its rationale is corrected. Comeback also already has a per-game off switch (`rate <= 0` returns early), so the lobby toggle is a bool and one assignment on a channel that already replicates.
2. **"Seed of the day."** The shipped Skim Race `SegmentSpawner` is configured with one segment and exactly one candidate spawnable, so the server seed makes the track identical across machines and identical across days. The track is four hand-authored waypoint circuits. Time Attack is therefore a campaign of fixed circuits, not a rotating daily; the rotation comes from Weekly Shorts.
3. **"Comeback off makes a run comparable."** It certifies one of two power layers. `DomainFaunaBuffSystem` grants element levels from living hearts in every cell with fauna, simulated per peer. Hence the fauna-free Time Attack cell and the second assertion at submit.
4. **The locked arcade grid.** One verifier claimed eleven of twelve cards render locked on a fresh profile. `GameModeProgressionService.Instance` is set only in `Awake`, its prefab is referenced only from a folder marked for deletion, and `ArcadeExploreView` treats a null service as unlocked. The grid is open; the claim is kept as a week-one check because the disagreement itself is the finding.
5. **"No frame cap or vsync setting."** `GraphicsSettingsApplier` exposes both. Removed from the gaps list.
6. **"No `game_started` event."** `UGSKeys` declares it. Removed.
7. **Keyboard glyphs.** The audit's first pass called for authoring keyboard labels; its adversarial pass found the labels already cover every live binding and chips appear when a vessel's map is authored. The sub-task is dropped from initiative 3.
8. **`DefaultRows`.** Fixing it does nothing because `RowsFor` reaches it only for a mode with no entry, and all 17 modes have entries. Initiative 9 authors rows per entry.
9. **The ghost through the vision band.** The band's tint alpha is a marker, not an opacity, and is the only thing keeping the law off non-vessel materials that share the graph. The ghost gets its own material path.
10. **Restart-in-place.** It was framed as a conserved-mass waiver. Mass is not the binding constraint (`Cell.RequestCellSwap` already suctions a world); the binding constraint is that an in-place reset leaves the crystal-anchor cursor, turn monitor, element state and cell clock where they were, and in-place reset is a recorded rejected decision. The restart is a fast scene reload of a deliberately cheap arena.
11. **Manta hidden.** Hiding the Manta shrinks two of the only three cards where hull choice exists (Maelstrom, Scurry). It stays, with its four LOCKED ability cards drawn honestly by the fleet's lockup. Only the Serpent is hidden.
12. **Steamworks ordering.** "Link Steam after anonymous sign-in" would ship the bug it aims to fix, because the boot chain fires anonymous sign-in first and a second machine is already a new account by then. Sign-in is Steam-first with anonymous fallback.

**Related documents.** `Docs/STEAM_EA_INVESTOR_CHECKPOINT.html` (the July plan this reverses in four places), `Docs/WEEKLY_CHALLENGE.md`, `Docs/QA/QA_BACKLOG.md`, `Docs/PartySystem/ARCHITECTURE.md`, `Docs/ECOSYSTEM.md`, `Docs/ElementalAbilitySystem/FLEET_MAPS.md`, `_Scripts/Controller/Arcade/SKIMRACE.md`.
