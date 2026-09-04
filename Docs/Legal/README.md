# Legal documents — what we need, who writes it, where it goes

Answers the three questions: **do we have to create these, where do they live, and can they be
drafted for us.**

> **Not legal advice.** Everything in this folder is a draft for counsel to review. Froglet Inc. is a
> Delaware C-corp; a US lawyer should sign off before anything goes public.

---

## The short answer

| Document | Required? | Who writes it | Where it goes | Status |
|---|---|---|---|---|
| **Privacy policy** | **Yes, unconditionally** | Draft exists → counsel reviews | Public URL + Steam store page + in-game settings | Draft written, **needs a purchases section + hosting** |
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

1. **A public URL you control** — e.g. `https://froglet.games/privacy`. This is the canonical copy.
   A GitHub Pages site or a plain page on the marketing site is fine; it must be reachable without
   logging in and must not require the game.
2. **The Steam store page** — Steamworks has a dedicated privacy-policy URL field in App Admin.
   Paste the same URL.
3. **In-game** — the settings panel already has a Privacy Policy row wired to
   `Application.OpenURL`. Set `GameSettingsPanelController.privacyPolicyUrl` to the same URL. The
   button exists; it just needs the real address.

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
