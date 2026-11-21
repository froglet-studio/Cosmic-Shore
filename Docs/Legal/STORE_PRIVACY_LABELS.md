# Store privacy declarations for fair play (DRAFT)

> Written 2026-10-24 for the fair-play system (`Docs/AntiCheat/README.md` §4.7). Fill these in
> the store consoles **when the fair-play features ship**, not before: a label must describe
> what the build does. Data an SDK sends counts on both stores (`Docs/AntiCheat/LEGAL.md` §P2,
> §P3). Check each line against the shipping build, then have counsel confirm.

## Apple App Store — App Privacy

| Data type | Collected for | Linked to the user | Used for tracking |
|---|---|---|---|
| Identifiers → **Device ID** (App Attest / DeviceCheck results) | App Functionality | Yes | No |
| Identifiers → **User ID** (UGS player id, linked platform account) | App Functionality | Yes | No |
| Usage Data → **Gameplay Content** (match results, movement sample) | App Functionality | Yes | No |
| User Content → **Other User Content** (display name; reports a player files) | App Functionality | Yes | No |
| Other Data → **Other Data Types** (birth year from the age screen, to protect minors) | App Functionality | Yes | No |

"App Functionality" is Apple's purpose that covers "prevent fraud, implement security measures".
Fingerprinting is not allowed: we use only App Attest and DeviceCheck, never derived device
identifiers.

## Google Play — Data safety

| Data type | Collected | Shared | Purpose | Optional |
|---|---|---|---|---|
| Device or other IDs (Play Integrity verdicts, device recall) | Yes | No | Fraud prevention, security, and compliance | No |
| App activity → Other actions (match results, movement sample, leave rate) | Yes | No | Fraud prevention, security, and compliance | No |
| Personal info → User IDs (UGS player id, linked Google account) | Yes | No | App functionality; Fraud prevention, security, and compliance | No |
| Personal info → Other info (birth year from the age screen, to protect minors) | Yes | No | App functionality; Fraud prevention, security, and compliance | No |
| Messages → Other in-app messages (only if chat is added) | — | — | — | — |

Declare the data as **encrypted in transit** and **deletable on request** (the in-game Delete my
data flow), as the privacy policy already says.

## Steam

No privacy label. The store page's **Anti-Cheat** section is required only for client-side
kernel-level anti-cheat; the server-side checks need none. If Easy Anti-Cheat or similar is ever
added, fill that section in the same release. Steam's privacy-policy URL field links the same
policy as above.

## Gameplay analytics (added 2026-11-17, Shombith: "Go ahead update")

The game sends analytics only with consent, and only for players 13 or older. It goes to UGS
Analytics and to **Froglet's own analytics server** (Oracle Cloud, `Tools/Analytics/server/`); PostHog
left the game on 2026-11-06 (#1761) and its account closes on 23 Nov 2026. The server derives a
two-letter country from the IP address and never stores the IP. Add these rows to the tables above
in the same release as the fair-play rows, or alone if fair play has not shipped:

**Apple App Store — App Privacy**

| Data type | Collected for | Linked to the user | Used for tracking |
|---|---|---|---|
| Identifiers → **User ID** (UGS player id on every event) | Analytics | Yes | No |
| Usage Data → **Product Interaction** (sessions, menus, settings, social actions) | Analytics | Yes | No |
| Usage Data → **Gameplay Content** (modes, results, scores, flight time) | Analytics | Yes | No |
| Location → **Coarse Location** (country only, from the IP; the IP is not stored) | Analytics | Yes | No |
| Diagnostics → **Crash Data** and **Other Diagnostic Data** (`client_exception`, no message text; Unity Cloud Diagnostics) | App Functionality, Analytics | No (crash reports carry no player id) / Yes (`client_exception`) | No |
| Other Data → device model and OS (`device_model`, `os`) | Analytics | Yes | No |

**Google Play — Data safety**

| Data type | Collected | Shared | Purpose | Optional |
|---|---|---|---|---|
| Personal info → User IDs (UGS player id) | Yes | No | Analytics | Yes (consent) |
| App activity → App interactions, Other actions (sessions, matches, menus, settings, social) | Yes | No | Analytics | Yes (consent) |
| Location → Approximate location (country, from the IP) | Yes | No | Analytics | Yes (consent) |
| App info and performance → Crash logs, Diagnostics | Yes | No | Analytics, App functionality | Yes (consent) |
| Device or other IDs → install id | Yes | No | Analytics | Yes (consent) |

"Shared" is No because Unity and Oracle act as our processors. Have counsel confirm both tables,
and check them against the shipping build.
