# Consent & Privacy Flow — setup guide

What shipped in code (PR #546) and what a human needs to do in the Unity Editor to make
the opt-in analytics consent flow live. Opt-in everywhere (per the launch decision).

## What the code does

`AnalyticsServiceFacade` is now **opt-in and decision-gated**:

- Collection starts only when the player is **age-eligible (13+)** AND has **explicitly
  granted** consent. Undecided / declined / under-13 all keep collection **off**.
- Tri-state, device-local (PlayerPrefs):
  - `AnalyticsAgeEligible` — absent = not asked · 1 = 13+ · 0 = under-13 (never collect)
  - `AnalyticsConsent` — absent = undecided · 1 = granted · 0 = denied
- Public API: `SetConsent(bool)`, `SetAgeEligible(bool)`, `SubmitBirthYear(int)`,
  `RequestDataDeletion()`, and read-only `NeedsPrivacyFlow`, `ConsentGranted`,
  `ConsentDecided`, `AgeEligible`, `AgeChecked`.
- **Fail-safe:** if the panel below is never built/wired, `NeedsPrivacyFlow` stays true,
  nothing is shown, and **nothing is collected**. Silence is the safe default.

Two controllers are provided (`Assets/_Scripts/UI/Privacy/`):

- `PrivacyConsentController` — first-run age gate → consent dialog.
- `AnalyticsPrivacySettingsController` — Settings toggle + "Delete my data" + policy link.

## Editor steps

### 1. First-run consent overlay
1. In an early scene with a Reflex `ContainerScope` — **Authentication** (preferred) or the
   first **Menu_Main** entry — create a full-screen overlay panel (Canvas child) that sits
   on top of everything.
2. Add `PrivacyConsentController` to the panel root and wire:
   - **Root** — the overlay root (defaults to the same GameObject if left empty).
   - **Age gate panel** / **Consent panel** — the two step sub-panels.
   - **Age gate (neutral, preferred):** a `TMP_Dropdown` (birth year) + a submit `Button`.
     The controller populates the dropdown with years and derives 13+ eligibility.
   - **Age gate (simple fallback):** "I'm 13 or older" / "Under 13" buttons (use the neutral
     picker instead where you can — COPPA prefers a neutral age screen).
   - **Consent:** Accept + Decline buttons.
   - **Privacy policy:** a link Button + the hosted **Privacy Policy URL**.
3. Copy: the consent panel must state what's collected (anonymous gameplay data), that it's
   never sold, and link the policy. See `PRIVACY_POLICY_TEMPLATE.md` §2 for wording.
4. (Optional) Subscribe to `OnPrivacyFlowCompleted` if a scene step should wait for the
   answer. Not required — declining still lets the player proceed (no consent wall).

### 2. Settings → Privacy controls
1. On the Settings/Privacy panel, add `AnalyticsPrivacySettingsController` and wire:
   - **Consent toggle** (`Toggle`) — reflects/sets consent; auto-disabled for under-13.
   - **Delete my data** (`Button`) — calls `RequestDataDeletion()` (UGS erasure).
   - **Privacy policy** (`Button`) + the same **Privacy Policy URL**.

### 3. Host the privacy policy
Publish the finalized `PRIVACY_POLICY_TEMPLATE.md` (counsel-reviewed) at a public URL and
paste that URL into both controllers' `privacyPolicyUrl` fields.

## Notes
- **Not a consent wall:** Decline / Under-13 dismiss the panel and let the player play —
  required by GDPR (and Apple 5.1.1). Don't gate gameplay on accepting analytics.
- **Returning players:** the prompt shows once (state persists in PlayerPrefs). It re-shows
  only if the keys are cleared.
- Still required before data flows: declare every event + params in the **UGS dashboard
  Event Manager**, and accept Unity's DPA (see `Docs/Analytics/DATA_INVENTORY.md`).
