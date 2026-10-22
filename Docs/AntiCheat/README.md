# Fair play: anti-cheat and player conduct (research and design)

**Status:** research and proposed design, 2026-10-22. Nothing here is built. Asked for by Shombith ("a good
anticheat custom made for Cosmic Shore", like Valorant's Vanguard; isolate bad actors instead of banning them).
**Not legal advice.** The legal findings, with their sources, are in [`LEGAL.md`](LEGAL.md); counsel signs off
before any of it ships (the same rule as `Docs/Legal/README.md`).

Who else works here: **Yash** owns the UGS party/presence layer, authentication and the leaderboard service this
design changes (`who_works_where.py`, 2026-10-22: six of his merges touch them in the last 30 days, nothing in
flight). **Shombith** drives the UI and multiplayer scenes. **Garrett** owns product decisions (§8). The flight
recorder this design reuses for bot detection is Yash's (`HumanFlightRecorder`, `Docs/AI_SYSTEM/`).

---

## 1. The answer in short

**What you asked for, and what survives contact with the law and the platforms:**

| You asked for | Verdict | What we do instead |
|---|---|---|
| Don't ban; isolate bad actors in a pool of their own | **Yes.** Industry standard (CS2 Trust Factor, GTA Online "Bad Sport" lobbies, Dota 2 low priority, Call of Duty's mitigations) | **The Undertow**: a separate presence lobby and matchmaking pool, filled out with our AI pilots (§4.6) |
| Players have no idea it exists | **No, not on paper.** Every privacy law we ship under requires the *program* to be disclosed, and EU, UK and California players can demand the data we hold on them, flags included | **Invisible in the moment, disclosed on paper.** The privacy policy and Code of Conduct say the program exists; nothing in the game shows a player which pool they are in or why, and we never publish signals or thresholds (§4.7) |
| A Vanguard-style kernel driver watching the hardware, always on | **Not on iOS or Android at all; on PC possible but the wrong buy for us** | Platform device attestation (Steam account, iOS App Attest + DeviceCheck, Android Play Integrity) remembers the device lawfully; server authority stops the cheats this game can actually suffer (§5) |
| Lock their modes | **Yes, for competitive and ranked play.** Never for anything they paid for | Locks apply to leaderboards, ranked and public play; paid content stays playable solo and with AI (§4.6) |
| After 3 rounds, tell them and let them appeal | **Yes, with two changes** | A human reviews before the mode lock bites (EU GDPR Art. 22), and a restriction made over *what a player wrote* is notified when it is imposed for EU/UK players (DSA Art. 17, UK Online Safety Act) |
| Catch quitting and trolling too | **Yes** | Quitters meet quitters (Dota's low-priority model); trolling comes in through player reports, which Apple and Google require anyway once players can see each other's text |

**The most important finding is not about drivers.** Measured from the code (§2), the cheapest cheats in Cosmic
Shore today need no hacking tool at all: accounts are anonymous and free to re-create, the leaderboard accepts any
score the client sends, currency and tokens live in client-written Cloud Save, and the match host is a player's own
machine. A kernel driver fixes none of those. Server authority fixes all of them, costs little, and is a
launch-quality job either way.

## 2. What we are protecting against (measured from the code, 2026-10-22)

| # | Weakness | Where | What a cheater does with it |
|---|---|---|---|
| T1 | **Accounts are anonymous only.** `SignInWithSteamAsync`, `SignInWithAppleAsync` and `SignInWithGoogleAsync` are stubs that return `Task.CompletedTask` | `AuthenticationController.cs:124-128`, `AuthenticationServiceFacade.SignInAnonymouslyCore` | Any penalty is undone by clearing the session token or reinstalling: a fresh account, a clean record. Bot farms make accounts for free |
| T2 | **The leaderboard trusts the client.** The one check is "not negative, not NaN" | `WeeklyChallengeLeaderboardService.SubmitCompletionAsync` → `LeaderboardsService.AddPlayerScoreAsync` | Post a 0.1 s weekly-challenge time straight to the UGS API with the player's own session token, no game running |
| T3 | **Currency, unlocks and episode tokens are client-written Cloud Save** | `UGSCloudSaveProvider.cs:206`, `EpisodeTokenService.GrantTokens` | Edit the balance or own every episode; the token grant checks the order receipt on the client |
| T4 | **The match host is a player.** UGS Relay, host-authoritative Netcode; scores, crystals and hits are decided on the host's machine | `Docs/MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md` (P2.4 defers dedicated servers "until cheating appears") | A modded host decides every result for all four players |
| T5 | **Vessels move owner-authoritatively** (`ClientNetworkTransform`) | `Assets/_Scripts/Utility/Network/ClientNetworkTransform.cs` | Speed, teleport and boost hacks on any client are believed by everyone else |
| T6 | **The game ships its own bot.** Every client carries the AI pilots, and the AI writes the same input a human does (the input-only rule) | `IVessel.ToggleAIPilot`, `PilotControl` (host test tool, not compiled out), `PilotSwap` | The cheapest "aimbot" for Cosmic Shore is a modded client that turns on the AI pilot for its own hull, or a host that sits in a rival's hull ("score follows the pilot") |
| T7 | **Display names are checked on the client only** | `DisplayNameValidator`, `DisplayNameRegistry.PublishOwnNameAsync` (public Cloud Save write) | A slur as a name, written straight to the API; it shows in every presence list |
| T8 | **No report button.** Block exists (`FriendsServiceFacade.BlockPlayerAsync`); report does not | — | Trolling and abuse have no way in, and Apple/Google require one where players see each other's content ([`LEGAL.md`](LEGAL.md) §P2, §P3) |
| T9 | **Leaving costs nothing.** A guest who leaves mid-match is replaced by AI (B21) | Party layer | Rage-quitting and lobby-dodging are free |

What is **not** a threat today: there is no text or voice chat (the "chat" toasts are game notices), so
"racist and provocative comments" can only travel through display names, and later through any chat we add.
Launch week is 10-40 concurrent players in one 100-player presence lobby (`HARDENING_PLAN_STEAM_LAUNCH.md` §2):
a separate pool will usually hold zero to two real people, which is why the Undertow is filled out with AI.

## 3. The walls: what law and the platforms allow

Full research with sources: [`LEGAL.md`](LEGAL.md). The parts that shape the design:

PLATFORM_AND_LAW_SUMMARY

## 4. The design

```
 client (Steam / iOS / Android)                    server side (UGS)
 ──────────────────────────────                    ─────────────────────────────────────────────
 platform sign-in ─────────────────────────────▶  UGS Auth (Steam / Apple / Google identity)   §4.1
 device attestation token ─────────────────────▶  Cloud Code: verify, remember the device       §4.1
 match summary + 10 Hz flight sample ──────────▶  Cloud Code: rules ▸ statistics ▸ model        §4.4
 score / currency / unlock REQUEST ────────────▶  Cloud Code validates, then writes            §4.2
                                                   │
                                                   ▼
                                        Trust record (Cloud Save, PRIVATE: server-only)      §4.5
                                                   │
               presence lobby + leaderboard ◀──────┘  routes the account: Shore or Undertow  §4.6
```

### 4.1 Identity: one account, remembered devices

A pool of their own means nothing if leaving it costs one reinstall (T1).

- **Online play requires a platform identity**: Steam (session ticket → UGS `SignInWithSteamAsync`), Sign in with
  Apple or Game Center on iOS, Google Play Games on Android, linked to the existing anonymous account so nobody
  loses progress. Offline and solo play stay anonymous.
- **Remember the device the way each platform allows**, never by fingerprinting:
  - **PC:** the Steam account is the identity. A new Steam account costs a game purchase, which is the real
    deterrent. The Steam Web API's ban record (VAC and game bans on other games) and account age feed the trust
    prior, as Valve's own Trust Factor does.
  - **iOS:** **App Attest** proves the request comes from our unmodified app on a real Apple device, and
    **DeviceCheck's two per-device bits**, which Apple stores server-side and which survive a reinstall, mark the
    device itself. That is exactly the "hardware memory" asked for, without reading any hardware.
  - **Android:** **Play Integrity** verdicts (genuine app, genuine device, Play-licensed, risky apps running
    alongside, recent device activity for bot farms) and its device-recall bits.
- Hardware identifiers we read ourselves are **never** sent raw; anything device-derived is a salted hash, kept
  only as long as the restriction it supports (data minimisation, [`LEGAL.md`](LEGAL.md) §L1).

### 4.2 Server authority for anything worth cheating for

The cheapest, strongest layer, and launch work in its own right (T2-T5):

| What | Today | Change |
|---|---|---|
| Weekly-challenge leaderboard | Client posts any time | UGS **Access Control** denies the player's direct leaderboard write; a **Cloud Code** function accepts a run summary, checks it against the course (minimum possible time for the fastest hull at its top speed, ring count, sample continuity) and writes the score |
| Currency, unlocks, progression | Client-written Cloud Save | Same pattern: Cloud Code applies earn/spend rules; the balance lives in a **protected** key the player can read but not write |
| Episode tokens (if tokens win the DLC decision) | Grant checked on the client | Grant only after the store's server API confirms the order (Steam microtransaction API, App Store Server API, Google Play Developer API) |
| Match results | Host decides | **Every participant reports the result.** Cloud Code cross-checks the four reports and the movement samples; a host whose results disagree with its guests' is a signal. Dedicated servers or distributed authority stay deferred (P2.4) until the data says otherwise |
| Movement | Owner-authoritative | The host already sees every vessel: a plausibility check (speed against the hull's configured maximum, boost against charge, teleport distance) reports violations as signals, never as kicks |
| Test tools (`PilotControl`) | Shipped, host-gated | Compile out of release builds (`Docs/CONDITIONAL_COMPILATION.md`) |
| Display names | Client check | The same `DisplayNameValidator` rules run in Cloud Code before the public registry write |

### 4.3 Light client integrity

Enough to make casual memory editing expensive, nothing that needs ring-0:

- IL2CPP, which every target already uses (`ProjectSettings.asset` `scriptingBackend`: Standalone 1, Android 1;
  iOS is always IL2CPP), plus string and symbol obfuscation of the integrity code. IL2CPP is not protection on its
  own: public dumpers recover the class layout from `global-metadata.dat`.
- Obscured values for the handful of numbers worth editing (boost charge, element levels, score) and a speed-hack
  check (wall clock against `Time.realtimeSinceStartup`), as in CodeStage's Anti-Cheat Toolkit.
- Mobile: App Attest assertions and Play Integrity tokens on every Cloud Code call that writes something.
- PC: **Easy Anti-Cheat** is free through Epic Online Services and is the fallback if, after launch, the data shows
  client cheats the server layer cannot see. It is a decision for then, not now (§5).

### 4.4 Detection: the watcher

Three tiers, cheapest and surest first. The "AI" is the third tier, and it only ever adds weight; it never acts
alone.

| Tier | What it is | Confidence | Example |
|---|---|---|---|
| **Rules** | Facts the game's own config makes impossible | Near-certain | A Skim Race finished faster than the course allows at the hull's top speed; 40 crystals in a 3 s window; a boost with no charge; an integrity verdict that says "modified app" |
| **Statistics** | How far a player sits from everyone else in the same mode, hull and intensity | Probable | Win rate, score per minute or ring-split times 5 standard deviations above the population, sustained |
| **Learned models** | A classifier over flight samples | Supporting | "Human or bot?" and "is this the shipped AI flying?" |

**Our unfair advantage for bot detection:** the game already records how humans fly (`HumanFlightRecorder`, 10 Hz:
stick axes, triggers, rates, situation) and already has AI pilots that fly the same hulls through the same input.
That is a labelled dataset of *human* and *our-AI* flights for free, so the classifier learns to tell them apart
before the first bot farm exists. (The recorder is editor and development builds only today, and the team already
decided players get their own consent question before it ships; the release-build anti-cheat sample is a smaller,
separate stream, §4.7.)

What each behaviour looks like to the watcher:

| Behaviour | Signals | Goes to |
|---|---|---|
| **Cheating** | Rules violations; host results that disagree with guests; plausibility reports; integrity verdicts; Steam ban history (prior only) | Undertow |
| **Bot farming** | Input regularity and the human/AI classifier; play around the clock; identical routes across accounts; many accounts per device (DeviceCheck / Play Integrity device activity); emulators | Undertow plus no earnings (farming pays nothing) |
| **Racist or provocative names / chat** | The name filter (server-side); player reports; a text classifier on any future chat | Name reset immediately; Undertow for repeats; EU/UK: told at once (§4.6) |
| **Quitting and AFK** | Leave rate per match; idle input while the vessel is human-held | Quitters' pool: quitters meet quitters, for a number of matches, as Dota does |
| **Trolling and griefing** | Player reports, weighted by the reporter's own trust; team-mode sabotage (scoring for the other team, blocking teammates) | Human review, then Undertow |

### 4.5 The trust record

One record per account, in UGS Cloud Save's **private** access class (server-only: the client can neither read
nor write it):

```
trust: {
  score: 0..1000,            // starts from priors: platform identity, account age, Steam ban history
  stage: shore | watch | undertow | restricted | review,
  reasons: [ { kind: "rules.skimrace.split", at, evidence_ref, confidence } ],
  device_marks: [ "<salted hash>" ],
  undertow_matches: 0,
  appeal: { state, opened_at, decided_by, decided_at },
  expires_at                  // marks decay; clean play earns trust back
}
```

Every reason carries an evidence reference (the match, the samples) so a human reviewer and an appeal can see
exactly what was found.

### 4.6 The ladder: what happens to a flagged account

```
 Shore ──(signals)──▶ Watch ──(confident)──▶ Undertow ──(human confirms)──▶ Restricted
   ▲                    │ silent, no effect     │ automatic, invisible      │ ranked + leaderboards locked
   │                    │                       │                           │
   └──── trust decays back with clean play ─────┴─── after 3 Undertow matches: NOTICE + APPEAL ──┘
```

- **Watch** — silent, no effect on play. Signals only add weight.
- **The Undertow** — an isolated pool, made from what the game already has:
  - a **separate presence lobby** (the same `PRESENCE_LOBBY` machinery, a second lobby), so they see and are seen
    only by other Undertow players;
  - parties filled out by **AI backfill** (`RequestedAIBackfillCount`), so the pool never looks empty and real
    players are never in their matches;
  - a **shadow leaderboard**: their scores go to a separate board, and Cloud Code splices their own entry into the
    public board when *they* read it, so their view looks normal;
  - earnings that count only inside the Undertow.
  Entered automatically only on near-certain rules signals or integrity failures; everything else waits for Watch to
  accumulate, or for a human.
- **Restricted** — ranked play, leaderboards and public matchmaking locked. **A human signs this off** (GDPR Art. 22
  safeguard: no "solely automated" decision with a significant effect). Never touches anything they paid for: paid
  episodes and hulls stay playable solo and with AI.
- **Notice and appeal after 3 Undertow matches**, as you asked. The notice is a statement of reasons: what kind of
  behaviour (cheating, botting, conduct), that automated detection was involved, which rule of the Code of Conduct,
  how to appeal and how long it lasts. It does **not** reveal the detection method. "We detected something in your
  system" should become "our systems detected play that breaks rule 3 of the Code of Conduct": we only claim what we
  can show, and we do not imply we scanned their computer when we did not.
- **The 3-match delay applies only to cheat and bot signals.** Two exceptions:
  - restrictions imposed over **what a player wrote** (a name, a chat line) are notified **when imposed**, because
    DSA Art. 17(2) requires the statement of reasons "at the latest from the date that the restriction is imposed"
    and names shadow-banning (Recital 55). The UK Online Safety Act's complaints duty also covers it. The simplest
    rule is to do this for every player, not only EU and UK ones;
  - **threats to life or safety** in any text go to a human at once, and to the authorities where the law requires
    (DSA Art. 18).
  Whether the delay can stand for EU and UK players flagged for cheating is counsel's question 4. The fallback, if
  it cannot, is a short notice when an account enters the Undertow, while the methods stay hidden.
- **Exit:** an upheld appeal clears the record; otherwise trust decays back with clean play (marks expire), except
  for repeat offenders. A false positive is fixed by a person, recorded, and fed back into the thresholds.
- **Minors are never sent to the Undertow automatically.** The age screen already records a birth year
  (`PrivacyConsentOverlay`), so we know who is under 13 and under 18. For them a signal opens a human review
  instead: EU Recital 71 says solely automated decisions "should not concern a child", the UK Children's Code keeps
  profiling off unless there is a compelling reason, and India bans "behavioural monitoring" of under-18s from about
  May 2027 ([`LEGAL.md`](LEGAL.md) §1.5). Security checks themselves still run. COPPA allows persistent identifiers
  for "security or integrity" without parental consent, but only with notice and only for that purpose. The Undertow
  is chat-free by design, because a pool that mixes children with toxic adults would be a child-safety problem.

### 4.7 What the player sees, and what we publish

| Where | What it says |
|---|---|
| **In the game** | Nothing until the notice. No pool indicator, no "under review" badge, no change to the UI |
| **Privacy policy** (`Docs/Legal/PRIVACY_POLICY_TEMPLATE.md`) | A new "Fair play and security" section: we collect gameplay telemetry, integrity results and platform account standing to keep play fair; we use automated systems with human review; we may place accounts in separate matchmaking; how long we keep it; your rights. Legal basis: legitimate interests (security and fair play), not analytics consent. Section 4 already lists "maintain security and prevent abuse" |
| **Code of Conduct / Terms** | The rules, the possible measures (separate matchmaking, restricted modes, reward withholding), that they can happen without prior notice, and the appeal route |
| **Steam store page** | The anti-cheat disclosure only if we add client-side anti-cheat (EAC); nothing for server-side detection |
| **App Store privacy label / Play Data safety** | "Gameplay content", "Device or other IDs" (attestation), purpose "Fraud prevention, security and compliance" |
| **On request** | A player who asks (GDPR Art. 15, UK GDPR, CCPA) gets their data, including their trust stage and reasons. We may withhold the detection methods (trade secrets), not the fact of the flag |

The release-build telemetry for detection is a **separate stream from analytics**: analytics is opt-in consent and
is off for under-13s; fair-play telemetry rides legitimate interests, is minimal (match summary plus a short, coarse
movement sample), is never used for anything else, and is deleted on a schedule.

## 5. Why not a Vanguard-style kernel driver

KERNEL_SECTION

## 6. Build order

Each phase stands on its own and is worth doing even if the next never happens.

| Phase | What | Touches | Owner to check with |
|---|---|---|---|
| **A. Before launch** | Platform sign-in and account linking (T1); Access Control + Cloud Code for the weekly-challenge board and currency (T2, T3); server-side name check (T7); compile `PilotControl` out of release (T6); a Report button beside Block (T8); the privacy policy section and a Code of Conduct (§4.7) | `AuthenticationServiceFacade`, `WeeklyChallengeLeaderboardService`, `UGSCloudSaveProvider`, `DisplayNameRegistry`, `FriendsServiceFacade`, `Docs/Legal/` | Yash (auth, UGS), Shombith (UI), counsel |
| **B. First patch** | Every participant reports the match result; the rules tier in Cloud Code; the trust record; the Undertow as a second presence lobby plus AI backfill; the shadow leaderboard; the notice-and-appeal screen; a review queue (a simple dashboard or the board) | Party/presence layer, `ServerPlayerVesselInitializerWithAI`, Cloud Code | Yash (party), Shombith (UI) |
| **C. Mobile and bots** | App Attest + DeviceCheck, Play Integrity; the statistics tier; the human/AI flight classifier trained on recorder data | iOS/Android plugins, `HumanFlightRecorder` data | Yash (recorder), Garrett (store builds) |
| **D. Only if the data demands** | Easy Anti-Cheat on PC; dedicated servers or distributed authority for ranked play | Build pipeline, Netcode topology | Garrett |
| **E. If chat is ever added** | Moderation (filter, report, block, review), Apple 1.2 / Play UGC compliance, DSA/OSA notices | New | Garrett, counsel |

## 7. What it costs

| Item | Cost |
|---|---|
| Cloud Code, Access Control, Cloud Save private keys | UGS free tier at launch scale |
| App Attest, DeviceCheck, Play Integrity | Free (Play Integrity has a daily quota; see `LEGAL.md` §P3) |
| Steam sign-in, Steam Web API | Free |
| Easy Anti-Cheat (only if phase D) | Free through Epic Online Services |
| A custom kernel driver | Months of specialist work, an EV certificate, Microsoft attestation signing, permanent security liability (§5) |
| Human review | Minutes a week at launch scale; the real cost is having one named person who does it |

## 8. Decisions for the owner, and questions for counsel

DECISIONS_SECTION

## 9. Sources

Every legal and platform claim is cited in [`LEGAL.md`](LEGAL.md). Code facts cite files above and were read on
2026-10-22.
