# Fair play: anti-cheat and player conduct (research and design)

**Status:** research and proposed design, 2026-10-22. Nothing here is built. Asked for by Shombith ("a good
anticheat custom made for Cosmic Shore", like Valorant's Vanguard; isolate bad actors instead of banning them).
**Not legal advice.** The legal findings, with their sources, are in [`LEGAL.md`](LEGAL.md); counsel signs off
before any of it ships (the same rule as `Docs/Legal/README.md`). The same design, with two diagrams, is the doc
"Cosmic Shore Fair Play" (https://claude.ai/artifact/Q526NwB3r7uY2GoFwMGVn2, private until shared).

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

Every platform lets us isolate cheaters, and none lets us hide that we do. Phones forbid watching the hardware at
all, and each offers a sanctioned way to remember a device instead.

| | Steam (PC) | iOS | Android |
|---|---|---|---|
| Isolate instead of ban | Allowed: Valve's own docs describe "secure" and "insecure" servers that separate cheaters "while still letting them play the game" | Allowed | Allowed |
| Tell players it exists | Valve: "give your customers proper warning that such a system exists" | Privacy label and the law | Data safety and the law |
| Remember a device | The Steam account (a new one costs a game purchase) | DeviceCheck: 2 bits per device, survives reinstall and erase; fingerprinting banned | Play Integrity device recall (beta): 3 bits, survives reinstall and reset; persistent hardware IDs banned |
| Prove the app is genuine | User-mode checks; later, Windows runtime attestation or Easy Anti-Cheat | App Attest | Play Integrity verdicts |
| Kernel-level watching | Possible but costly; breaks on Steam Deck; must be declared on the store page | Impossible | Impossible; `QUERY_ALL_PACKAGES` not allowed for anti-cheat |
| Use other games' bans | No (Valve FAQ) | n/a | n/a |
| Report and block | Expected | Required once players see each other's content (guideline 1.2) | Required (UGC policy) |

The law adds four rules on every store ([`LEGAL.md`](LEGAL.md) §1):

1. **Disclose the program, not the method** (GDPR Art. 13; the FTC's Sony BMG and DesignerWare cases).
2. **Notify at once for anything a player wrote** (DSA Art. 17(2), which names shadow-banning in Recital 55).
3. **A person, not only a model, before a significant penalty**, and never a solely automated one for a child
   (GDPR Art. 22 and Recital 71; UK Arts. 22A-22D).
4. **Never cut off paid content without a way to contest it** (EU CPC principles, Mar 2025; the FTC's Epic order).

Precedent: Call of Duty's published enforcement policy places flagged accounts "in limited matchmaking lobbies
with other accounts in a similar state". Fall Guys ran an undisclosed "Cheater Island" and closed it when its
40-player matches could not fill; AI backfill is our answer to that.

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
    deterrent. We do **not** use other games' bans: Valve's anti-cheat FAQ answers "Can I use bans in other
    games to block users from playing in my game?" with "No", and the Web API terms allow reading a player's
    data only "as requested by the end user" ([`LEGAL.md`](LEGAL.md) §P1).
  - **iOS:** **App Attest** proves the request comes from our unmodified app on a real Apple device (its keys do
    not survive a reinstall), and **DeviceCheck's two per-device bits**, which Apple stores server-side and which
    persist across reinstall, device transfer and even "Erase all contents and settings", mark the device itself.
    That is exactly the "hardware memory" asked for, without reading any hardware. Fingerprinting is banned with
    no fraud exception.
  - **Android:** **Play Integrity** verdicts (genuine app, genuine device, Play-licensed, risky apps running
    alongside, recent device activity for bot farms) and **device recall** (beta, apply by form): 3 bits per
    device that survive reinstall and factory reset, not on emulators, and "not to fingerprint or track".
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
- PC: if, after launch, the data shows client cheats the server layer cannot see, two steps come before any
  driver: Windows' `GetRuntimeAttestationReport` (a user-mode, Secure-Kernel-signed list of loaded drivers, used
  by Riot's Vanguard On-Demand) and **Easy Anti-Cheat** (free through Epic Online Services, Unity via the EOS
  plugin, runs on Steam Deck when enabled per build). A decision for then, not now (§5).

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
separate stream, §4.7. It never replaces or trims the AI training flights, which stay whole under
`Docs/AI_SYSTEM/AI_DATA_IN_UGS.md`; ask Yash or Garrett before the classifier reads them.)

What each behaviour looks like to the watcher:

| Behaviour | Signals | Goes to |
|---|---|---|
| **Cheating** | Rules violations; host results that disagree with guests; plausibility reports; integrity verdicts | Undertow |
| **Bot farming** | Input regularity and the human/AI classifier; play around the clock; identical routes across accounts; many accounts per device (DeviceCheck / Play Integrity device activity); emulators | Undertow plus no earnings (farming pays nothing) |
| **Racist or provocative names / chat** | The name filter (server-side); player reports; a text classifier on any future chat | Name reset immediately; Undertow for repeats; EU/UK: told at once (§4.6) |
| **Quitting and AFK** | Leave rate per match; idle input while the vessel is human-held | Quitters' pool: quitters meet quitters, for a number of matches, as Dota does |
| **Trolling and griefing** | Player reports, weighted by the reporter's own trust; team-mode sabotage (scoring for the other team, blocking teammates) | Human review, then Undertow |

### 4.5 The trust record

One record per account, in UGS Cloud Save's **private** access class (server-only: the client can neither read
nor write it):

```
trust: {
  score: 0..1000,            // starts from priors: platform identity linked, account age in our game
  stage: shore | watch | undertow | restricted | review,
  reasons: [ { kind: "rules.skimrace.split", at, evidence_ref, confidence } ],
  device_marks: [ "<salted hash>" ],
  undertow_matches: 0,
  appeal: { state, opened_at, decided_by, decided_at },
  expires_at                  // marks decay; clean play earns trust back
}
```

Every reason carries an evidence reference (the match, the samples) so a human reviewer and an appeal can see
exactly what was found. (A UK small-claims judge restored an account Activision banned because Activision showed
no evidence of cheating; `LEGAL.md` §P5.)

**Routing reads the record on the server.** The game has no stranger matchmaking (parties form inside the presence
lobby), so a Cloud Code call at sign-in tells the client which presence lobby to join. If UGS Matchmaker is adopted
later, note that its *rules* read Cloud Save's **protected** class (player-readable, server-written), while its
pool *filters* read ticket attributes the client sets and can forge. Route on a rule, and store only an opaque pool
key there, never the stage or reasons.

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
- **What a restriction may cover (R14, enforced since 2026-10-25).** Only play WITH or SEEN BY other people: ranked,
  public matchmaking (the Undertow pool), leaderboards, and rewards earned through the breach. Solo and AI play are
  never restricted in any mode, so a purchase is never cut off and the fair-play code never needs to know what was
  bought. `Tools/Build/check_fairplay_paid_content.py` (in the bleeding-edge guard and `/to-bleeding-edge`'s gates)
  holds it both ways: fair-play code (any `FairPlay*`, `Undertow*`, `TrustRecord*` or `AntiCheat*` file, the
  Moderation and Steam folders, the Cloud Code scripts) may not name the episode-token / store layer, and that layer
  may not name a fair-play type or record. Phase B's mode lock asks only "with people or alone?", never which mode
  or episode.
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
- **Minors are never sent to the Undertow automatically.** The age screen asks a birth year
  (`PrivacyConsentOverlay`); until 2026-10-25 it kept only "13 or older", so nobody could tell a 15-year-old from an
  adult. It now keeps the year (`FairPlayAge`) and sends it with each match report; the server keeps the youngest
  year it has seen (`FAIRPLAY_AGE`, protected) and treats anyone not surely 18, or of unknown age, as a minor
  (built 2026-10-25, Threat Board R13). For them no quit record is kept, and every signal recorded about them says
  `review: "human"`, which phase B must honour. For them a signal opens a human review
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
| **Steam store page** | The Anti-Cheat section is required only for kernel-level anti-cheat and optional otherwise. Valve's anti-cheat guidance still says to "give your customers proper warning that such a system exists" |
| **App Store privacy label** | Gameplay Content and Device ID under "App Functionality", which covers preventing fraud. Data an SDK sends counts |
| **Play Data safety** | Device or other IDs and gameplay data, for "Fraud prevention, security, and compliance". Data an SDK sends counts |
| **On request** | A player who asks (GDPR Art. 15, UK GDPR, CCPA) gets their data, including their trust stage and reasons. We may withhold the detection methods (trade secrets), not the fact of the flag |

The release-build telemetry for detection is a **separate stream from analytics**: analytics is opt-in consent and
is off for under-13s; fair-play telemetry rides legitimate interests, is minimal (match summary plus a short, coarse
movement sample), is never used for anything else, and is deleted on a schedule.

## 5. Why not a Vanguard-style kernel driver

A kernel driver would cost months, cover only the PC build, and stop none of T1-T9. Sources in
[`LEGAL.md`](LEGAL.md) §P1-P4.

- **It cannot exist on two of our three stores.** iOS and Android give apps no kernel access; Easy Anti-Cheat
  itself "does not support … Android, iOS". Android's `QUERY_ALL_PACKAGES` (seeing other installed apps) is not a
  permitted use for anti-cheat.
- **It is expensive to ship.** Windows loads new kernel drivers only when Microsoft signs them. Attestation signing
  is now "for testing purposes only", so a retail driver needs WHQL (HLK-tested) signing plus an EV certificate.
  The vulnerable-driver blocklist and HVCI are on by default on most new Windows 11 machines.
- **It drops players.** Steam's Proton layer (Steam Deck, Linux): "Kernel-space solutions are not currently
  supported and are not recommended." The store page must declare kernel-level anti-cheat (since 30 Oct 2024).
- **It is a permanent security liability.** Genshin Impact's driver (`mhyprot2.sys`) was used by ransomware to
  kill antivirus on PCs that did not even have the game installed (Trend Micro, 2022). Capcom pulled its
  Street Fighter V driver in 2016.
- **It needs consent in the EU** unless "strictly necessary" (ePrivacy Art. 5(3)); a system-wide scan is the
  hardest case to defend ([`LEGAL.md`](LEGAL.md) §L1).
- **It misses our cheats.** A fake leaderboard post, an edited balance or a modded host never touch the kernel.

Microsoft has said it is moving third-party drivers out of the kernel (Nov 2025, May 2026) but has announced no
rule for anti-cheat. If PC client cheating appears after launch, the cheaper steps are in §4.3.

## 6. Build order

Each phase stands on its own and is worth doing even if the next never happens.

| Phase | What | Touches | Owner to check with |
|---|---|---|---|
| **A. Before launch** | Platform sign-in and account linking (T1); Access Control + Cloud Code for the weekly-challenge board and currency (T2, T3); server-side name check (T7); compile `PilotControl` out of release (T6); a Report button beside Block, through the UGS Moderation SDK (T8); the privacy policy section and a Code of Conduct (§4.7) | `AuthenticationServiceFacade`, `WeeklyChallengeLeaderboardService`, `UGSCloudSaveProvider`, `DisplayNameRegistry`, `FriendsServiceFacade`, `Docs/Legal/` | Yash (auth, UGS), Shombith (UI), counsel |
| **B. First patch** | Every participant reports the match result; the rules tier in Cloud Code; the trust record; the Undertow as a second presence lobby plus AI backfill; the shadow leaderboard; the notice-and-appeal screen; a review queue (a simple dashboard or the board) | Party/presence layer, `ServerPlayerVesselInitializerWithAI`, Cloud Code | Yash (party), Shombith (UI) |
| **C. Mobile and bots** | App Attest + DeviceCheck, Play Integrity; the statistics tier; the human/AI flight classifier trained on recorder data | iOS/Android plugins, `HumanFlightRecorder` data | Yash (recorder), Garrett (store builds) |
| **D. Only if the data demands** | Windows runtime attestation or Easy Anti-Cheat on PC; dedicated servers or distributed authority for ranked play (Unity's Multiplay hosting was deprecated on 1 Apr 2026) | Build pipeline, Netcode topology | Garrett |
| **E. If chat is ever added** | Moderation (filter, report, block, review), Apple 1.2 / Play UGC compliance, DSA/OSA notices | New | Garrett, counsel |

## 7. What it costs

| Item | Cost |
|---|---|
| Cloud Code, Access Control, Cloud Save private keys | Usage-priced; launch scale (10-40 concurrent) should sit in UGS's free allowance. Not verified: check Unity's pricing page |
| App Attest, DeviceCheck, Play Integrity | No charge from Apple or Google |
| Steam sign-in | Free |
| Easy Anti-Cheat (only if phase D) | "The anti-cheat service is free" (Epic); paid support is optional |
| A custom kernel driver | Months of specialist work, an EV certificate, WHQL signing for every release, permanent security liability (§5) |
| Human review | Minutes a week at launch scale; the real cost is having one named person who does it |

## 8. Decisions for the owner, and questions for counsel

**For Garrett (product owner):**

1. No custom kernel driver. Server authority first; Windows attestation or Easy Anti-Cheat only if post-launch data
   shows client cheats.
2. Online play requires a platform sign-in (Steam, Apple, Google); offline and solo stay anonymous.
3. Paid content is never locked by a fair-play measure.
4. One named person reviews flagged accounts and appeals.

**For counsel:** the eight questions at the end of [`LEGAL.md`](LEGAL.md) §4. The two that change the design most:
whether the 3-match delay can stand for EU and UK players flagged for cheating (question 4), and whether the
Undertow itself is a "similarly significant" automated decision (question 1).

All of these are on the team board (`Docs/TEAM_TASKS.md`, area "Fair play").

## 8b. Priorities chosen on the Threat Board (Shombith, 2026-10-24)

Set on the Threat Board artifact (https://claude.ai/artifact/PwjCQgUu9JWENw9FyFi4s2, private until shared).
15 of 16 threats ticked; the board's own risk score (likelihood × impact, out of 25) is shown beside each.

| Importance | Threat | Board score | Team task |
|---|---|---|---|
| Critical | R12 Monitoring that isn't disclosed (privacy policy section, Code of Conduct, store labels) | 15 | T-20261024-1a44 **done 2026-10-24** (drafts in `Docs/Legal/`), T-20261022-3075 (counsel) |
| Critical | T3 Currency, unlocks and tokens written by the client | 20 | T-20261022-9bbd. **Crystal wallet built 2026-10-24, gated off until deployed** (`Tools/UGS/README.md`); unlock records and tokens remain |
| Critical | T5 Ship positions trusted from each client (speed / teleport / boost) | 9 | T-20261024-1652. **Monitor built 2026-10-24, observe-only until ceilings are set** |
| High | T1 Anonymous accounts | 20 | T-20261022-6151. **Steam sign-in built 2026-10-25, switched off until the app id and the UGS Steam provider are set** (`Tools/UGS/README.md`): a first launch signs in with Steam, an existing player links Steam. Apple and Google need their plugins (T-20261025-7795); setup T-20261025-6e9a, editor checks T-20261025-885c |
| High | T2 Client-trusted leaderboard | 15 | T-20261022-9bbd. **Server check built 2026-10-24, gated off until deployed with the access policy** |
| High | T8 No Report button | 12 | T-20261022-7708. **Done 2026-10-24**: Report (three reasons, to UGS Moderation) and Block on the profile popup; Moderation package added |
| High | T4 Host decides every result | 6 | T-20261024-af9e. **Match reports built 2026-10-24, gated off**: disagreeing reports flag the host; a host that lies the same to everyone is not caught |
| High | T9 Leaving costs nothing | 8 | T-20261024-9d7c. **Quit counting built 2026-10-24, gated off**; no consequence until the Undertow |
| Later | R11 Memory editing on the device | 8 | **Moved to later 2026-10-25**: build our own (speed-hack check, then obscured values) instead of the $80 kit; the server checks cover launch. See the LATER task on the board |
| Medium | T6 Shipped AI pilot as a bot (`PilotControl`) | 9 | T-20261022-7708. **`PilotControl` out of release builds 2026-10-24. AI-like steering measured on the host 2026-10-25, observe only** until the limits are set from logged play (T-20261025-4fef); a modified client that fakes human-looking steering is not caught (that is bot detection, R10) |
| Medium | R10 Bot farming | 9 | **Two server checks built 2026-10-25, with match reports (gated off), observe only**: `around_the_clock` (match starts in 20 distinct hours of 24) and `repetitive_results` (6 matches of a mode within 2% of the same time). Still phase C: the human-vs-AI flight classifier (needs Yash's and Garrett's flight data, T-20261025-cebb), identical routes across accounts, many accounts per device (needs App Attest / Play Integrity), and farming earning nothing (the Undertow) |
| Medium | R13 Automated penalties on children | 12 | design rule (§4.6). **Built 2026-10-25, with match reports (gated off)**: the birth year is kept and sent; minors and unknown ages get no quit record and every signal about them is marked for a person (§4.6). Counsel decides whether quit counting may run for minors (`RULES.keepConductForMinors`) |
| Medium | R14 Locking paid content | 8 | **Enforced 2026-10-25**: restrictions cover only play with or seen by others (§4.6); a gate in the bleeding-edge guard fails any fair-play code that names the paid layer, or paid code that names a fair-play record |
| Medium | R16 Reading more from the device than needed | 8 | design rule (§5) |
| Unset | T7 Display names checked on the client only | 12 | T-20261022-7708. **Done 2026-10-24 on the receiving side**: every player's game hides offensive names it receives (`Pilot {tag}`); a server check of the sender adds nothing a modified game cannot skip |
| Not ticked | R15 False positives with no evidence or appeal | 12 | — (the ladder in §4.6 still requires evidence and appeal) |

## 9. Sources

Every legal and platform claim is cited in [`LEGAL.md`](LEGAL.md). Code facts cite files above and were read on
2026-10-22.
