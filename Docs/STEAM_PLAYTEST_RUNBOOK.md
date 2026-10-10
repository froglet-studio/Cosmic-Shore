# Steam Playtest — Workstream A runbook (the live milestone)

Step-by-step for the **Revision-2** destination: an invite-only **Steam Playtest** — a free child
app attached to the Cosmic Shore store page, with a signup queue, manual grant waves, and one to
three compounding friend invites per tester. Everything here is off-machine except the upload
commands. No code.

This is a **sibling** of [`STEAM_BUSINESS_SETUP.md`](STEAM_BUSINESS_SETUP.md), not a replacement.
That document is the Revision-1 **paid** path and is still how the paid conversion ships; A1–A3 and
the store-page mechanics of A5 are done from there. It is a separate file because the two revisions
reuse the item IDs with different meanings — Rev 1's **A4** is the Early Access questionnaire and
its **A6** is pricing; on the live board **A4 is the Playtest child app** and **A6 is the wave
policy** — and one file carrying both meanings would be exactly the drift
[`STEAM_CHECKPOINT_SERIES.md`](STEAM_CHECKPOINT_SERIES.md) exists to prevent.

**Owners:** Shombith leads setup · Caleb on the store page · Garrett on policy and sign-off
**Board:** [`STEAM_RELEASE_TASKS.md`](STEAM_RELEASE_TASKS.md) (R6, H3, H4, H5, H17)
**Tooling:** [`Tools/Steam/README.md`](../Tools/Steam/README.md), [`BUILD_AND_DELIVERY.md`](BUILD_AND_DELIVERY.md) §4

> **Accuracy note.** Everything stated as a Valve rule below was checked against Valve's Playtest
> documentation on 2026-10-10 (<https://partner.steamgames.com/doc/features/playtest>). Where that
> page does **not** name a control — the grant button, the exact text of the store-page signup
> button — this runbook says so inline rather than guessing. Steamworks changes; read the live page
> before acting on anything time-critical.

---

## Read this first — two constraints from Valve

These two sentences are why the milestone has the shape it does. They belong in front of anyone
deciding a wave, not in a chat message.

1. **Playtest participants cannot post Steam reviews of the game, and Playtest metrics never touch
   the base app.** Valve: *"A customer who has only participated in the Playtest cannot review your
   actual game."* Playtime, wishlists, refunds, achievements and trading cards on the base app are
   all unaffected by Playtest activity. **Definition of Done #7 (zero public review surface) is
   therefore satisfied by configuration** — by shipping to the child app and never to the base
   app's `default` — not by anything in the build.
2. **Charging for Playtest access in any form is prohibited.** Valve: *"No - the design of Steam
   Playtest is for free signups"* and *"it's not OK to charge customers to access your Steam
   Playtest."* No paid tier, no supporter early access, no in-game transaction that unlocks access,
   no selling Playtest keys, no Playtest keys in a paid bundle. F3 already flags this; § A6 repeats
   it where the wave policy is written.

---

## Which checklist item produces which id

Nothing in the repository holds an id or a credential. They live in the build machine's environment,
and `Tools/Steam/upload.sh` refuses until the pair for its target is exported, naming the item below.

| Id | Produced by | Where to read it | Environment variable | Upload target |
|---|---|---|---|---|
| Base app id | **A2** (pay the fee, create the app) | Steamworks → the base app → App Admin | `STEAM_APPID` | `--target base` (the default) |
| Base depot id | **A2** | Base app → App Admin → Depots | `STEAM_DEPOTID` | `--target base` |
| Playtest child app id | **A4** (§ below) | Steamworks → the Playtest child app → App Admin | `STEAM_PLAYTEST_APPID` | `--target playtest` |
| Playtest depot id | **A4** | Playtest child app → App Admin → Depots | `STEAM_PLAYTEST_DEPOTID` | `--target playtest` |

The child app has its own pair. It is never the base app's, and the script refuses if the two pairs
share an id.

---

## Where this sits in the sequence

```
A1  partner account, agreements          ─┐
A2  fee paid, BASE app created, id noted  ├─ paid-path doc, Phase 1  (the 30-day clock starts at A2)
A3  content survey                       ─┘
A4  PLAYTEST child app created + configured      ← this doc
A5  Coming Soon page live, Playtest signup section visible   ← this doc  (queue + wishlists grow)
A6  wave policy signed                           ← this doc
A7  store page + Playtest build submitted for review  (H5)
E7  Wave 0, E10 Wave 1                           (H17)  — grants held at zero until A6 and A7 are done
```

A Playtest hangs off a base app, so A2 must be done first. A4 and A5 are independent of each other
until A5's "enable signups" step, which Valve's setup order places **after** the child app has passed
its simplified review. A6 is paperwork and can start on day one.

---

## A4 · Create and configure the Playtest child app *(Shombith creates; Garrett signs off the name)*

### The one irreversible step: the name

The Playtest has a **customer-visible name** — it is what a tester sees in their library and on the
store-page section. Valve's page says of it: *"you won't be able to change this after it's
released."* Treat it as **fixed at creation.** The gap between creating the child app and its
release is the review itself, there is no renaming lever anywhere later in this workstream, and
nobody should plan on a correction window that Valve does not promise. Everything else in A4 can be
redone; this cannot.

**Decide the name in writing before opening the page.** Valve's own pattern is `<Game> Playtest`,
so the safe default is **Cosmic Shore Playtest**. Anything cleverer is a decision, and an
irreversible one. Localized names are supported and fall under the same rule.

### Steps

1. **Create the child app.** In Steamworks, open the **base app's "Associated Packages & DLC"
   page** and create a Playtest from there (Valve names that page as the starting point; it does not
   quote the button's label — read it off the page). The result is *"a separate 'child' appID that
   is associated with, but separate from your main game"*, with *"access to the same Steamworks
   technical features as your main game."*
2. **Set the customer-visible name** (and any localized names). See above. This is the step to be
   slow on.
3. **Record the ids.** The child app's **app id** → `STEAM_PLAYTEST_APPID`. Create or locate its
   **depot** (child app → App Admin → Depots) → `STEAM_PLAYTEST_DEPOTID`. Valve: *"Upload depots
   and set up builds on the Playtest just like any other game or demo."* Put both in the build
   machine's environment, never in the repository.
4. **Configure the Playtest settings.** Valve names a **"Playtest Settings"** page (under the
   child app's Application tab) and a **"Manage Your Playtest"** area. Set:

   | Setting | Value | Why |
   |---|---|---|
   | **Signup type** | **Limited signup** (Valve's default). Leave it there. | Under Limited signup you *"grant access in batches as you have capacity"* and *"players are selected randomly from the pool of signups"*, optionally filtered by country. **Open signup** admits every requester automatically, and switching Limited → Open *"accepts any pending playtesters, generally within a few minutes"* — the whole queue at once. That switch is a one-way door for the purposes of A6; it is not how a wave is granted. |
   | **Playable status** | **Not Playable** until the build has passed review and A6 is signed. | Valve lists "not playable with signups open" as a standard configuration for building a pool before launch — that is the state A5 wants. *"After deactivation, Steam will not launch the playtest app."* Setting it back to **Visible** restarts it; testers who already had access keep it. |
   | **Friend Invites** | **On**, invites per player set to **1, 2 or 3** — the number is A6's to choose; start at **1**. | Valve: each player gets the configured number of invites, and *invitees can invite more* (compounding). Rules to carry into the policy: *"Users can only invite friends they have been friends with for at least 30 days"*; pausing stops **new** invites only — *"Once an invite is sent, it will remain active, even if you pause friend invites"*; growth can also be limited by restricting invites to players who had access by a given date. Valve calls the feature experimental. |
   | **Grant cadence** | **Manual waves.** | Under Limited signup there is no scheduled auto-grant short of flipping to Open, so the manual batch *is* the grant mechanism. Valve's page does not quote the label of the grant control; it is on the Playtest settings page. Batch sizes are not stated by Valve — they are A6's. |

5. **Assets.** Valve says the child app needs a library capsule and community assets, and that its
   store review checklist *"only consists of capsule images and icons"*. Add the Playtest set to
   Will's E3 list; sizes are the upload sizes in the paid-path doc's capsule table (the library
   capsule, 600 × 900, is the one Valve names).
6. **Build.** Upload with `./Tools/Steam/upload.sh --target playtest --branch internal`, install the
   child app from Steam on a clean machine, run the smoke test and the overlay check (**B7** lives
   here now — the Playtest app is a real Steam build), then set the build live on the child app's
   `default` from App Admin → Builds. See § *Branch model under two apps* for what `default` means on
   this app.
7. **Submit for review** (A7 / H5). The Playtest goes through *"a simplified store page and build
   review"*. The base app's store page review must pass before build review begins (the paid-path
   rule; it still holds).
8. **Keys.** Valve offers Playtest keys (*"You can request keys for your Steam Playtest too"*) as an
   alternative to the signup queue. The plan is the queue plus friend invites; request keys only if
   Wave 0 needs to seat people who cannot sign up through the page, and never sell or bundle them
   (§ *Read this first*, item 2).

### What A4 deliberately does not do

- It does not give the Playtest its own store page: *"your Steam Playtest signup will live right on
  your main game"*. The page work is A5.
- It does not re-answer the content survey. Valve's checklist for the child app names only capsules
  and icons; if App Admin asks for a survey on the child app anyway, answer it as A3 was answered.
- It does not use the base app's `beta` branch for anything. See § *Branch model under two apps*.

---

## A5 · Coming Soon page with the Playtest signup section *(Caleb builds, Shombith submits)*

### The ordering that matters

**Every week the Coming Soon page is live earns wishlists *and* grows the signup queue, while grants
are still held at zero.** The page is therefore put up as early as the assets allow, well before the
build is ready, and the queue is allowed to accumulate. Joining or leaving the Playtest does not
touch a wishlist (Valve: *"A customer's wishlist for your game won't be impacted when they join or
leave your playtest"*), so there is no reason to hold the page back for the Playtest's sake.

### Steps

1. **Build the base app's store page** exactly as the paid-path doc's Phase 3 describes — the five
   capsules at upload sizes, screenshots, copy, tags, links. Nothing there changes.
2. **Submit for review at least 7 days before you want it public**, release status **Coming Soon**.
   Review is 3–5 business days.
3. **Get the Playtest child app through its simplified review** (A4 step 7). Valve's setup order puts
   this *before* enabling signups. Follow that order; do not assume the signup section can appear on
   the page before the child app has passed.
4. **Enable signups.** On the **base app** → Edit Store Page → **"Special Settings"** tab (Valve
   names this tab and this location). The Playtest section then renders on the base game's page; the
   Playtest has no page of its own.
5. **Verify from a non-partner Steam account** that the public page shows the Playtest section and
   its button. The board calls the button "Request Access"; Valve's page shows the control as an
   image rather than quoting its text, so confirm the live label rather than this doc.
6. **Record the go-live date.** It starts the Coming Soon clock (the paid path wants ≥ 2 weeks),
   it starts E9 wishlist tracking, and from that day the signup queue is growing while Playable stays
   **Not Playable** and no batch has been granted.
7. **Hold grants at zero** until A6 is signed and H5 has passed. Do not flip Open signup "to test
   the button" — see A4's settings table for why that is a one-way door.

---

## A6 · The wave policy *(Garrett signs; H4 on the board)*

### Why it is a signed document

Steamworks can stop grants and pause friend invites per wave immediately, and can set the whole
Playtest Not Playable in one click. The policy exists so that lever is pulled **on a rule rather
than on a feeling** — and so that nobody argues, at 11 pm on a bad night, about whether the night
is bad enough.

It sits under the two Valve constraints in § *Read this first*. In particular: **nothing in the
policy may condition access on money.** No paid tier, no supporter wave, no in-game transaction
that grants or accelerates access, no selling or bundling of Playtest keys. The in-game commerce
surfaces are de-scoped (board item R4) and **must stay de-scoped in every Playtest build** — a
Playtest build with a live purchase surface is a policy violation, not a bug.

### What the signed policy must contain

| # | Section | What it has to state |
|---|---|---|
| 1 | **Wave sizes** | Wave 0 (team plus hand-picked testers: how many, admitted how — manual grant or keys). Wave 1 (the first batch from the queue: how many). The growth rule for Wave *n* (for example a ceiling relative to Wave *n-1*). Any country filter. |
| 2 | **The invite multiplier** | The Friend Invites setting (1, 2 or 3 per player) and the arithmetic it implies. A wave of *N* grants with *k* invites each can reach *N × (1 + k)* within days, and keeps compounding because invitees invite too. **Budget participants, not grants**: the number the policy caps is the population on the child app's `default` branch, and the grant batch is whatever keeps the projection under it. |
| 3 | **Weekly caps** | Maximum new grants per week. Maximum total participants. The invite-pause rule — including the date-based form Valve provides (*restrict invites to players who had access by a given date*). |
| 4 | **Pause criteria tied to capacity** | The concurrency the multiplayer stack has been soak-tested to (H9), the floor machine (H8), Discord moderation and support capacity (E6), and the load-time target (R7). Shape: *"if concurrent sessions exceed X % of soak-tested capacity, no new grants and friend invites paused until it is back under Y %."* |
| 5 | **Pause criteria tied to defect telemetry** | Crash-free sessions per `invite_wave` cohort (R9 emits it; the PostHog dashboards that read it are still to be built — the policy should say which chart it reads), open P0/P1 count (R3, H11), join-failure rate (R16, H10). Shape: *"any open P0, or crash-free below N % over the trailing 48 h for the newest wave, or join failures above M %: pause grants and invites, hold until two consecutive days are green."* |
| 6 | **The levers and who pulls them** | Three levers, in escalating order: (a) stop granting — do nothing; (b) pause Friend Invites — stops new invites, sent ones stay active; (c) set **Not Playable** — every tester is locked out, Steam will not launch the app. Name the **role** that may pull each one (not a person), and the restore rule: who decides, on what evidence, that a paused wave resumes. |
| 7 | **The monetisation clause** | Restated verbatim from § *Read this first* item 2, with the R4 de-scope as the build-side guarantee. |
| 8 | **The review and metrics statement** | What DoD #7 rests on: Playtest participants cannot review the base game and Playtest metrics never touch it. Note the corollary: the invite path admits people who will be able to buy and review at the paid release, so quality still matters — there is simply no public review surface *during* the Playtest. |
| 9 | **Signature, date, version** | Who signed, when, where the signed copy is filed, and the rule for amending it (a new version, never an edit in place). |

### Fill-in template

Copy this into the signed document. Values are deliberately blank — **the numbers are H4's to set,
not this runbook's** — except where a Valve rule fixes them.

```
COSMIC SHORE STEAM PLAYTEST — WAVE POLICY  v___   signed ____-__-__ by ____________ (role: ________)

Wave 0   size ____   admitted via  [ ] manual grant  [ ] keys    who: team + ____________
Wave 1   size ____   from the signup queue, random selection, country filter: ____________
Wave n   ceiling = ____ × Wave n-1, never above the weekly cap below

Friend Invites per player  [ ] 1  [ ] 2  [ ] 3     (Valve: friends ≥ 30 days; sent invites survive a pause)
Participant cap (default branch population)  ____     Weekly cap on new grants  ____
Invite-pause date rule: invites restricted to players who had access by ____-__-__ once population ≥ ____

PAUSE — capacity       concurrent sessions > ____ % of soak-tested capacity (H9: ____ sessions)
                       resume when < ____ % for ____ h
PAUSE — telemetry      any open P0;  crash-free < ____ % trailing 48 h for the newest invite_wave;
                       join failures > ____ %;  dashboard read: ____________________
                       resume after ____ consecutive green days, decided by role ________

LEVERS   stop granting: role ________   pause invites: role ________   Not Playable: role ________

MONEY    No access, tier, wave, invite, key or in-game transaction is sold or conditioned on payment.
         Every Playtest build ships with the commerce surfaces de-scoped (R4).
REVIEWS  Participants cannot review the base game; Playtest metrics do not touch it (Valve).
```

Illustrative arithmetic only, to show why item 2 says *participants, not grants*: 25 grants at
1 invite each is 50 people within days if every invite is accepted, and 75 after the invitees use
theirs; at 3 invites each the same 25 grants is 100 people after one round. Pick the invite count
with that in mind, and let the weekly cap be the number that actually binds.

---

## Branch model under two apps

This is the single statement of the model. The README and `BUILD_AND_DELIVERY.md` summarise it;
if they ever disagree with this table, this table wins.

The Revision-1 docs describe `internal` → `beta` → `default` **on one app**, with `beta` as the
password-protected closed-playtest branch. That is still correct for the paid path, and it is the
most misleading thing in the repo for the Playtest, because **the invite channel is now a different
app id entirely.** The same branch name has a different audience on each app:

| Branch | Base app — `--target base`, ids from **A2** | Playtest child app — `--target playtest`, ids from **A4** |
|---|---|---|
| `internal` | Team only, password protected. Every build lands here first. | Team only, password protected. The team installs the exact child-app build from Steam here before it reaches testers. **B7 (overlay check) runs here now**, not on the base app's `beta`. |
| `beta` | Revision-1's closed-playtest branch. Under the Playtest model **nobody outside the team owns the base app**, so this branch has **no audience** until the paid conversion. Keep it defined and password protected; do not point testers at it; do not read "promote to beta for the playtest" in the paid-path doc as a Playtest step. | **Not defined.** The tester population is controlled by signup grants and friend invites, not by a branch password. If a sub-cohort ever needs a different build, that is a decision for the wave policy, not a branch to create quietly. |
| `default` | **Everyone who owns the game.** The paid release and its patches, nothing before. Before the paid release it has no audience in this plan (no Release State Override keys are issued). | **Every tester who has been granted access** — signup grants and friend invites alike. **This is the live invite channel (E7 / E10).** Setting a build live here is a wave-wide ship and is governed by § A6. |

Three consequences:

- **E7 (Wave 0) and E10 (Wave 1) are Playtest-app `default` operations.** Where the older docs say
  "promote to `beta` for the playtest (E7)", read "set live on the Playtest app's `default`".
- **Setting `default` live is an App Admin action on either app.** Valve's SteamPipe documentation
  says the `default` branch cannot be set live from a build script — *"That must be done through
  the App Admin panel"* (<https://partner.steamgames.com/doc/sdk/uploading>). `upload.sh` still
  demands the target's app id typed back before it will pass `--set-live default`; that is the last
  line of defence, not the publishing mechanism.
- **The base app's `default` ships nothing during the Playtest.** Every build that reaches a
  tester goes through `--target playtest`. A build on the base app's `default` would reach owners,
  and owners can post reviews — which is the outcome DoD #7 exists to prevent.

One caveat for accuracy: Valve's Playtest page says builds and depots on the child app work *"just
like any other game or demo"*, and the branch mechanics above follow from that. The child app's
Builds page has not been inspected from this repository; if it differs, update this table first.

---

## Uploading to either app

Full procedure in [`Tools/Steam/README.md`](../Tools/Steam/README.md). Summary:

```bash
export STEAM_USER=<builder-account>                 # builder account, cached steamcmd session

# Playtest child app (ids from A4) — the invite channel
export STEAM_PLAYTEST_APPID=<playtest-appid> STEAM_PLAYTEST_DEPOTID=<playtest-depotid>
./Tools/Steam/upload.sh --target playtest --build-dir Builds/Windows64 --branch internal

# Base app (ids from A2) — the future paid build; --target base is the default
export STEAM_APPID=<appid> STEAM_DEPOTID=<depotid>
./Tools/Steam/upload.sh --target base --build-dir Builds/Windows64 --branch internal
```

The script refuses with the checklist item's name when its target's ids are unset, refuses if the
two pairs share an id, stamps `[playtest]` or `[base]` into the build description beside the version
and commit from `build_manifest.txt`, and demands the target's app id typed back before
`--set-live default`.

---

## Running a wave (E7, E10 — H17 on the board)

Per wave, in this order:

1. **Read the policy's gates** — capacity (item 4) and telemetry (item 5) — and record the readings.
   A wave does not start on a red gate.
2. **Grant the batch** from the child app's Playtest settings page (Limited signup; random selection
   from the queue; country filter if the policy says so). Batch size is the policy's, projected
   against the invite multiplier.
3. **Announce** in Discord (E6) what build is live on the child app's `default` and where to report.
4. **Watch for 48 h** against the telemetry gates. Pause invites first, stop granting second, set
   Not Playable last — in that order, by the roles the policy names.
5. **Log the wave** in the policy's record: date, grants, resulting population, readings, any pause.

Resetting the Playtest (removing every participant) is irreversible and is **not** part of running a
wave; Valve requires Not Playable, Hidden, Limited signup and paused invites before it is even
offered. If it is ever wanted, it is a new decision with its own sign-off.

---

## Master checklist — Playtest path

| # | Task | Owner | Blocks |
|---|---|---|---|
| A1 | Partner account, agreements, banking, tax, signer | Garrett, Shombith, Caleb | A2 |
| A2 | **Pay $100 fee, create the BASE app, record `STEAM_APPID` / `STEAM_DEPOTID`** | Shombith | **30-day clock, A4, B4** |
| A3 | Content survey | Garrett | Store review |
| **A4** | **Name decided in writing → create the Playtest child app → record `STEAM_PLAYTEST_APPID` / `STEAM_PLAYTEST_DEPOTID` → Limited signup, Not Playable, invites 1–3, manual batches → capsules → build on `internal` → simplified review** | Shombith (name: Garrett) | A5 step 4, A7, E7 |
| **A5** | **Coming Soon page live; child app through review; signups enabled under Special Settings; section verified from a non-partner account; date recorded** | Caleb, Shombith | **Coming Soon 2-week clock, E9, the queue** |
| **A6** | **Wave policy signed** (sizes, invite multiplier, weekly caps, capacity and telemetry pause rules, levers and roles, money clause, review statement) | Garrett | **E7, every grant** |
| A7 | Store page + Playtest build submitted for review | Shombith | E7 |
| E7 / E10 | Wave 0, Wave 1 — grants on the child app's `default`, per A6 | H17 | — |

---

## Failure modes worth pre-empting

- **A typo in the Playtest name.** The only irreversible step in the workstream. Decide it in writing,
  have a second person read the field before saving.
- **Flipping Limited → Open signup "to see it work".** Admits the entire pending queue within
  minutes. There is no undo short of a reset, which removes everyone.
- **Reading the paid-path doc's "promote to `beta`" as a Playtest step.** The base app's `beta` has
  no audience under this model. The invite channel is the child app's `default`.
- **Exporting the base app's ids into the Playtest variables** (or the reverse). The script refuses
  when the two pairs share an id, but only if both pairs are exported; when only one pair is, read
  the summary block it prints — it names the target and the variable — before `steamcmd` runs.
- **Granting a wave without reading the invite multiplier.** Three invites each turns a modest batch
  into a crowd within days, and sent invites survive a pause.
- **A Playtest build with a live purchase surface.** Charging in any form is prohibited; the R4
  de-scope is the guarantee and must not be reverted for a Playtest build.
- **Expecting `--set-live default` to publish.** Valve says the default branch is set live from App
  Admin; the script's confirmation is a guard, not the mechanism.

---

## Deliberately not covered here

- **The paid conversion** — pricing, the Early Access questionnaire, the base app's `default` going
  live. That is the paid-path doc, [`STEAM_BUSINESS_SETUP.md`](STEAM_BUSINESS_SETUP.md).
- **Playtest keys as a distribution channel.** Available from Valve; not the plan.
- **Resetting the Playtest.** Irreversible, its own decision.
- **The wave dashboards.** R9 emits `invite_wave`; the PostHog charts that read it are still to be
  built, and the policy should name them when they exist.
- **Any Steamworks SDK integration.** None is in the game, by decision.

---

## Sources

Checked 2026-10-10 against Valve's Steamworks documentation:
[Steam Playtest](https://partner.steamgames.com/doc/features/playtest) ·
[Uploading to Steam (SteamPipe)](https://partner.steamgames.com/doc/sdk/uploading) ·
[Branches (Betas)](https://partner.steamgames.com/doc/store/application/branches)

Quoted phrases above are from those pages. Control labels those pages do not quote are flagged
inline as such. Steam's rules change; re-read the Playtest page in App Admin before creating the
child app or granting a wave.
