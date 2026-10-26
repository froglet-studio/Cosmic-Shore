# Legal documents — what we need, who writes it, where it goes

Answers the three questions: **do we have to create these, where do they live, and can they be
drafted for us.**

> **Not legal advice.** Everything in this folder is a draft for counsel to review. Froglet Inc. is a
> Delaware C-corp; a US lawyer should sign off before anything goes public.

---

## The short answer

| Document | Required? | Who writes it | Where it goes | Status |
|---|---|---|---|---|
| **Privacy policy** | **Yes, unconditionally** | Draft exists → counsel reviews | Public URL + Steam store page + in-game settings | Web version written from the code for `www.froglet.games/privacy` (website repo); in-game URLs pointed at it; **needs counsel, a purchases section, and the gaps below** |
| **EULA** | No — Steam's default covers it | Only if we want custom terms | Steamworks App Admin → EULA field | **Decision: use Steam's default** unless counsel says otherwise |
| **Terms of Sale (virtual items)** | **Only if we sell tokens** | Draft written → counsel reviews | Public URL, linked from store page | Draft written, **on hold pending DLC-vs-tokens** |
| **Refund policy** | Steam's applies to the app | Us, for the spent-token case | Same page as terms of sale | Covered in the terms draft |
| **Support contact** | Yes (Steam requires one) | Garrett/Caleb | Steam store page | Checklist F2 |

**The privacy policy is required whether or not we ever sell anything** — the game already collects
analytics, crash reports, and Cloud Save data. It is checklist item F1 and it blocks launch.

**Everything else depends on the DLC-vs-tokens decision.** If episodes ship as Steam DLC, Valve is
merchant of record: Valve handles payment, refunds, and sales tax, and the Steam Subscriber
Agreement covers the transaction. In that world the terms-of-sale document may be unnecessary
entirely — confirm with counsel (question 2.3 in `DLC_VS_TOKENS_QUESTIONNAIRE.md`).

---

## Where each document actually lives

### Privacy policy → three places

1. **A public URL you control** — **`https://www.froglet.games/privacy`** (the marketing site,
   GitHub Pages, repo `froglet-studio/cosmic-shore-landing-page`). This is the canonical copy. It
   must be reachable without logging in and must not require the game. Its siblings are
   `/support` and `/delete-my-data` (the Google Play account-deletion URL).
2. **The Steam store page** — Steamworks has a dedicated privacy-policy URL field in App Admin.
   Paste the same URL.
3. **In-game** — two places, both pointed at the URLs above (2026-10-14):
   - the settings panel's Privacy Policy / Delete my data / bug-report rows
     (`SettingsModal.prefab` serializes `privacyPolicyUrl`, `deleteDataUrl`, `bugReportUrl`, which
     override the defaults in `GameSettingsPanelController`; change the prefab, not just the code);
   - the first-run consent screen's policy link, `Resources/PrivacyConsentConfig.asset`
     `privacyPolicyUrl` (it was empty, so the consent screen linked nowhere).

> Do not ship the policy as a file inside the game. It has to be updatable without a patch, and
> regulators expect a durable public URL.

### EULA → Steamworks only

If we ever want custom terms, Steamworks App Admin has an EULA field; Steam displays it in the
client at install. Nothing goes in the build. Current plan is Steam's default Subscriber Agreement,
so **no action**.

### Terms of sale → public URL

Same treatment as the privacy policy: a public page, linked from the store page. Only needed if we
sell tokens directly rather than shipping episodes as DLC.

---

## What is in this folder

| File | What it is |
|---|---|
| `PRIVACY_POLICY_TEMPLATE.md` | Full privacy policy draft. Covers analytics, crash reporting, Cloud Save, friends/presence, and children. **Needs a purchases section before we sell anything.** |
| `CONSENT_SETUP.md` | How the in-game consent flow is wired, and the editor steps to finish it. |
| `TERMS_OF_SALE_TEMPLATE.md` | Draft terms for episode tokens. On hold pending the DLC decision. |
| `DLC_VS_TOKENS_QUESTIONNAIRE.md` | The decision questionnaire, including the questions for counsel. |
| `CODE_OF_CONDUCT_TEMPLATE.md` | The six fair-play rules the in-game notice cites, the measures (separate matchmaking, leaderboard and reward holds, name reset, ranked restriction; never paid content), notice and appeal. For `Docs/AntiCheat/`. |
| `STORE_PRIVACY_LABELS.md` | What the App Store privacy label, Play Data safety form and Steam Anti-Cheat section must say once fair-play checks ship. |

---

## Order of operations

1. **Now, regardless of anything else:** fill the bracketed placeholders in
   `PRIVACY_POLICY_TEMPLATE.md`, have counsel review, host it, and paste the URL into the three
   places above. This unblocks checklist **F1** and it blocks launch.
2. **Decide DLC vs tokens** using the questionnaire.
3. **If tokens:** counsel reviews `TERMS_OF_SALE_TEMPLATE.md`, we host it, and add the purchases
   section to the privacy policy.
4. **If DLC:** confirm with counsel that the Steam Subscriber Agreement suffices (question 2.3).
   Most likely nothing further is needed beyond the privacy policy.

---

## What still needs a human

The drafts are structurally complete but every one of them contains bracketed placeholders — legal
entity address, contact email, retention periods, governing law. Those are facts we have and I do
not; fill them before sending to counsel so the review is about substance rather than blanks.

---

## Where the template and the code disagree (audit 2026-10-14, bleeding-edge `04e1ecf`)

The web policy at `/privacy` was written from the CODE, not from `PRIVACY_POLICY_TEMPLATE.md`, because
the two disagree. Each row is evidence for a decision (counsel, owner or a game fix); none of them is
fixed by the URL change that added this section. Re-grep before trusting a line number.

| # | Gap | Evidence | Who decides |
|---|---|---|---|
| 1 | The settings "Delete my data" row deletes nothing; it only opens `deleteDataUrl`. The one controller that calls `RequestDataDeletion` (`AnalyticsPrivacySettingsController`, guid `d891f53eef26405c8822f89d7cc004f1`) is in no scene or prefab. Deletion is a support-handled request until in-app deletion exists (Apple 5.1.1(v), Google Play account deletion). | `GameSettingsPanelController.OpenDeleteDataForm` | Game task |
| 2 | Under-13 players are kept out of analytics and crash reports only. Sign-in, Cloud Save, the public presence lobby and Friends start BEFORE the age question, and under-13s still get a public display name, friends and leaderboards. Template §8 ("we do not knowingly collect personal information… from them") overstates this. | `AppManager.StartAuthentication` runs before `PrivacyConsentOverlay.CreateIfNeeded` | Counsel (COPPA) |
| 3 | "Anonymous" is inaccurate: events are keyed to the UGS player ID; PostHog also receives the display name, an install ID and progress totals as person properties; social events carry OTHER players' IDs. | `PostHogAnalyticsSink`, `AnalyticsServiceFacade` | Copy (done on the web page) |
| 4 | PostHog (EU) is missing from template §6 processors. The consent text already names it. | `Resources/PostHogConfig.asset` | Copy (done on the web page) |
| 5 | Display name and avatar are PUBLIC: presence lobby (`IsPrivate=false`), the public Cloud Save key `display_name_norm`, and the weekly-challenge leaderboard. The template never says so. | `PresenceLobbyService`, `DisplayNameRegistry` | Copy (done on the web page) |
| 6 | Unity hardware statistics are sent at startup with no consent (`submitAnalytics: 1`). Disclosed on the web page; turning it off is cleaner. | `ProjectSettings/ProjectSettings.asset` `submitAnalytics` | Owner |
| 7 | The age check is year-only (`UtcNow.Year - birthYear`), so a 12-year-old whose birthday is later this year passes as 13. Counting the birth year as not yet reached closes it. | `AnalyticsServiceFacade` age-eligibility method | Game task |
| 8 | The same consent turns on crash reports, but the consent text does not mention them. Turning analytics off stops collection but does not request deletion. PostHog IP/GeoIP is not disabled in code (check the PostHog project settings). | `CrashReportingService`, `PrivacyConsentConfig.asset` `consentBody` | Owner |
| 9 | Template §3g/§3h/§4/§6 describe purchases and ads, and §3b a "play-style summary"; none exist in this build (Unity IAP not installed, ads off in `UnityConnectSettings.asset`). | `CommerceAvailability.asset`, `UnityConnectSettings.asset` | Copy |
| 10 | `support@frogletgames.zendesk.com` is in `SendBugReport` / `ShareByEmail`, but `ModalWindows.prefab` and `R_Options_Menu_Panel.prefab` are referenced by no scene; the shipping bug row opens `bugReportUrl`. | prefab guid sweep | Owner (which address to publish) |
| 11 | The legal entity name varies: the template says Froglet Inc.; Unity `companyName` and the site footer say Froglet Games. | `ProjectSettings.asset` `companyName` | Owner |
