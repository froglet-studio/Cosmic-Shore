# Cosmic Shore — Privacy Policy (DRAFT TEMPLATE)

> ⚠️ **DRAFT for legal review — do NOT publish as-is.** This template is tailored to
> what the game actually collects via Unity Gaming Services (UGS) as of the analytics
> work in PR #546. Fill every `[BRACKETED]` field and have counsel finalize before it
> is hosted and linked from the in-game consent dialog. Hosting + final wording are a
> legal/business responsibility; this just gives counsel an accurate, code-grounded start.

**Effective date:** [DATE]
**Last updated:** [DATE]

## 1. Who we are

Cosmic Shore is developed by **Froglet Inc.** ("Froglet", "we", "us"), a Delaware
C-corporation based in Grand Rapids, Michigan, USA. Contact: **[PRIVACY CONTACT EMAIL]**,
**[POSTAL ADDRESS]**. [If you appoint an EU/UK representative or DPO, name them here.]

This policy covers the Cosmic Shore game on all platforms (Steam/PC, iOS, Android).

## 2. The short version

- We collect **anonymous gameplay data** to understand how the game is played and to
  improve it. It is tied to an **anonymous player ID**, not your real-world identity.
- Analytics is **opt-in**: we collect it only if you accept the in-game consent prompt.
  You can change your mind any time in Settings, and you can request deletion.
- We **do not sell** your personal data.
- We **do not collect** analytics from players under 13 (see §8).

## 3. What we collect

We use **Unity Gaming Services (UGS)** from Unity Technologies as our backend. Depending
on the features you use and the consent you give, we collect:

**a. Account / identity (always, to run the game)**
- An **anonymous UGS player ID** generated at first launch (not your name or email).
- Your chosen **display name** and **avatar selection**.

**b. Gameplay analytics (only with your consent)**
Behavioral events such as: games started/completed (mode, intensity, vessel, player
count, win/loss, duration), session start/end, menu navigation, activation milestones,
in-game currency earned/spent, vessel unlocks, settings changes, social actions (friend
requests, party invites, shares, favorites), progression/quest completion, and a derived
play-style summary. Plus diagnostic events (e.g., failed cloud saves). These carry the
anonymous player ID and gameplay parameters — **no real-world identifiers**.

**c. Saved game data (Cloud Save)**
Your profile (display name, avatar, in-game currency balance, unlocked rewards,
first-seen timestamp), per-mode and per-vessel statistics, mode/intensity progression,
unlocked vessels and preferences, audio/control settings, and (where applicable) daily
challenge, training, squad, and loadout progress.

**d. Social features (if you use them)**
Friend relationships, the display names of players you interact with, and online
presence/status, via UGS Friends. Party/lobby/session data via UGS Multiplayer/Relay/Lobby
when you play with others.

**e. Leaderboards (if you post a score)**
Your score and anonymous player ID for the relevant leaderboard.

**f. Diagnostics & crash data**
Crash and performance diagnostics provided by Unity and the platform (Steam/Apple/Google).

**g. Purchases (if/when in-app purchases are enabled)**
Records of purchases. Payment is handled by the platform (**[Steam / Apple / Google]**);
we do **not** receive or store your full payment-card details.

**h. Advertising (if/when ads are enabled)**
[Describe Unity Ads data, ad identifiers, and whether ads are personalized — REMOVE this
section if ads are not shipped.]

> We do **not** intentionally collect precise geolocation, contacts, microphone/camera
> data, or special-category personal data. [Adjust if any of this changes.]

### 3.x Purchases and entitlements

> **Add this section before selling anything.** It is not needed while the game takes no money.

| Data | Why we hold it | Where it lives |
|---|---|---|
| Platform order id | To credit what you paid for, and to avoid crediting the same order twice | Your Cosmic Shore account (UGS Cloud Save) |
| Episode token balance | So your unspent tokens follow you across devices | Your Cosmic Shore account |
| Episodes you own | So your unlocks are permanent and follow you across devices | Your Cosmic Shore account |
| Lifetime tokens bought and spent | Support and refund questions | Your Cosmic Shore account |

We do **not** receive or store your card number, billing address, or any payment credential. Payment
is handled entirely by the platform you bought through (Steam), and we only ever see an order
identifier confirming that a purchase completed.

Retention: purchase records are kept for `[RETENTION PERIOD - counsel to set; tax and chargeback
windows usually drive this]`.

## 4. Why we use it (purposes)

To operate the game and your account; to save and sync your progress across devices; to
enable multiplayer and social features; to understand and improve gameplay, balance, and
onboarding; to diagnose crashes and bugs; [to process purchases; to show ads]; and to
maintain security and prevent abuse.

## 5. Legal bases (EU/UK GDPR)

- **Consent** — gameplay analytics and [advertising]. You may withdraw it any time.
- **Contractual necessity** — account, cloud save, multiplayer, leaderboards, purchases
  (needed to provide the game you asked for).
- **Legitimate interests** — security, abuse prevention, and crash diagnostics, balanced
  against your rights.

[Counsel to confirm the basis assigned to each processing activity.]

## 6. Who we share it with (processors / sub-processors)

- **Unity Technologies** — Unity Gaming Services: Analytics, Cloud Save, Authentication,
  Friends, Multiplayer/Relay/Lobby, Leaderboards[, Ads]. (Subject to Unity's data
  processing terms / DPA.)
- **[Platform: Valve/Steam, Apple, Google]** — distribution, crash reporting, and
  payment processing for purchases.
- **[Any other vendor — e.g., ad network]**.

We do **not sell** personal data and do **not share** it for cross-context behavioral
advertising. [Adjust if ads change this.]

## 7. International transfers

Data may be processed in the United States and other countries where we or our processors
operate. Where required, transfers rely on appropriate safeguards (e.g., Standard
Contractual Clauses). [Counsel to confirm mechanisms.]

## 8. Children

Cosmic Shore is **not directed to children under 13**, and we do **not knowingly collect
personal information (including analytics identifiers) from them**. The game presents a
neutral age screen at first launch; players under 13 are excluded from analytics
collection entirely. If you believe a child under 13 has provided us data, contact
**[PRIVACY CONTACT EMAIL]** and we will delete it. [If the digital-consent age is higher
in a given EU country, parental consent rules there apply.]

## 9. Data retention

We retain account and saved-game data for as long as your account exists. Analytics data
is retained per Unity's retention schedule [confirm]. You can request deletion at any time
(§10).

## 10. Your rights & choices

- **Withdraw consent / opt out of analytics:** Settings → Privacy → analytics toggle.
- **Delete your data:** Settings → Privacy → "Delete my data" (routes a deletion request
  to UGS), or contact **[PRIVACY CONTACT EMAIL]**.
- Depending on where you live (EU/UK GDPR, California CCPA/CPRA, and others) you may have
  rights to **access, correct, delete, port, or object** to processing, and to **not be
  discriminated against** for exercising them. To exercise these, contact
  **[PRIVACY CONTACT EMAIL]**.

## 11. Security

We use industry-standard measures (including encryption in transit) to protect your data.
No method is 100% secure.

## 12. Changes

We will update this policy as needed and revise the "Last updated" date; material changes
will be surfaced in-game.

## 13. Contact

**[PRIVACY CONTACT EMAIL]** · **[POSTAL ADDRESS]** · [EU/UK representative if applicable].

---

### Implementation cross-reference (for the team, remove before publishing)

The data described above maps to: `Docs/Analytics/DATA_INVENTORY.md` (Cloud Save keys +
event list), `AnalyticsServiceFacade` (the ~28 analytics events + consent gate),
`UGSDataService` (Cloud Save), `FriendsServiceFacade` / `HostConnectionService` (social),
`UGSStatsManager` (leaderboards), `IAPManager` (purchases — stub), `AdsSystem` (ads).
