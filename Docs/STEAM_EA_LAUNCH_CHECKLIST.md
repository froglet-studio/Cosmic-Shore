# 🚀 Steam Early Access — Remaining-Work Checklist

**Date:** 2026-07-28 · **Supersedes (for launch scoping):** the Notion "2026 Vertical Slice — Canonical Punch List" (May 27)
**Target:** Early Access launch on Steam, bare minimum blocking scope.

> **Read this first.** The May punch list assumed a *paid full release* with an in-build
> monetization rail. We are re-scoping to **Early Access**: a paid app with **no IAP at
> launch**, sold on the strength of what already works. That single decision deletes the
> two largest unfinished engineering projects from the old plan (P8 storefront/entitlement
> and most of P1 onboarding) and converts "what's left" from ~10 projects into
> **6 short workstreams**. Everything below is either a checkbox or explicitly cut.

Task sizes use the punch-list convention: **[S]** ≤ 1 day · **[M]** 2–5 days · **[L]** > 1 week.

---

## ✅ What the punch list wanted that is ALREADY DONE

No action needed — listed so the remaining work reads honestly small.

| Punch-list project | Status | Evidence in repo |
|---|---|---|
| **P2 — Minigame trio** (Hex Race, Crystal Capture, Joust, 4 intensities, 1–4 players) | **Shipped** | `HexRaceController` / `MultiplayerCrystalCaptureController` / `MultiplayerJoustController`, per-intensity laps/tracks, domain-aggregated scoring, AI backfill to 12 players. Bonus modes beyond the slice: Astro League, Brood Rush, Rampage, Skim Race. |
| **P3 — Tournament mode** | **Shipped as "Maelstrom"** | `TournamentController`, HexRace → Joust → Crystal Capture chain, race-to-6 placement scoring, standings, crystal-wallet credit (`Docs/TournamentSystem/`). |
| **P4 — Freestyle hub + Painting Mode** | **Shipped** | Toy system: vessel changer, domain changer, 16-painting "connect the dots" gallery with WebGL share export, Wanderway conveyor (`Docs/ToySystem/`). All vessels flyable; gamepad freestyle input ownership fixed. |
| **P5 — Netcode & sessions** | **Shipped (different stack than planned)** | The punch list assumed Mirror + Steam transport. We shipped **UGS Relay + Netcode for GameObjects**: eager per-user Relay, presence lobby, party invites, friends, kick, disconnect handling (`Docs/PartySystem/`, `Docs/PresenceSystem/`). Works on Steam as-is — no Steam transport required. |
| **P1 — Time-to-Tournament** (structural part) | **Done by design** | Tournament is in `SO_ProgressionConfig.alwaysUnlockedModes` — reachable from first boot. Quest chain (`GameModeQuestList.asset`) is the guided progression. Remaining: one validation pass (Workstream C). |
| **R7 — Vessel purchasable with earned currency** | **Shipped** | Hangar grid + `VesselUnlockSystem`: vessels unlock by spending crystals (`Docs/MENU_PROGRESSION_AND_IAP.md` §2). |
| **Cross-Cutting A — Telemetry** | **Shipped** | PostHog sink, locked event schema, match envelope, FTUE/game funnels, consent-gated (`Docs/Analytics/`). |
| **Settings system** (code layer) | **C# complete** | 4-tab PC settings panel: display/resolution/vsync/FPS cap, quality, colorblind mode, subtitles, consent, invert axes, audio (`Docs/SettingsSystem/`). Unity-side prefab wiring is the remaining piece (Workstream C). |
| **Performance program** | **Ongoing, strong** | Burst spatial index, instanced prisms, adaptive animation, DiagnosticsHUD, benchmark tool + sweep (`Docs/PERFORMANCE_OPTIMIZATION.md`). Remaining: lock the floor + gate (Workstream D). |
| **Legal groundwork** | **Drafted** | Privacy policy template + consent setup (`Docs/Legal/`). Remaining: host it (Workstream F). |

---

## 🔒 Decisions this document locks (answers to the punch list's "Open Decisions")

1. **Launch type: Early Access.** Not a demo, not a full release. The EA framing is honest ("party game growing new vessels and episodes") and removes the monetization rail from the blocking path.
2. **Monetization at launch: the app price only. No IAP.** Episode tokens and the web-checkout flow are **cut from launch** — the current web-checkout has a known unresolved entitlement-verification gap (`Docs/MENU_PROGRESSION_AND_IAP.md` §5) and must not ship taking money. Crystal-earned vessel unlocks (already shipped) stay in as the progression loop.
3. **Netcode stack: UGS Relay + NGO — final.** Already shipped and locked (`Docs/README.md`). No Steamworks networking, no host migration for EA.
4. **Steam SDK integration at launch: none.** A Steam build does **not** require the Steamworks API — the overlay, wishlists, reviews, and forums all work without it. We ship the plain Windows build via SteamPipe. Achievements, Steam Input configs, and Steam identity are post-launch.
5. **Crash reporter: UGS Cloud Diagnostics.** We're already on Unity Gaming Services and the settings panel already has the consent toggle; this is the zero-new-vendor option.
6. **Hardware floor: GTX 1060-class / 8 GB RAM / Win10 64-bit** (per punch list) — confirm with one profiling session, then it's locked (D1).

---

## 📋 The Remaining Work — six workstreams

### Workstream A — Steam business setup *(BIZ; ~1 person-week total, mostly waiting)*

The only items with **hard external lead times**. Start these first; everything else can proceed in parallel.

- [ ] **A1.** Create Steamworks partner account, complete identity + tax + banking **[S + multi-week processing lead time — do this week]**
- [ ] **A2.** Pay the $100 Steam Direct fee, create the app **[S]**
- [ ] **A3.** Complete the content survey / mature-content questionnaire **[S]**
- [ ] **A4.** Write the **Early Access questionnaire** answers (why EA, roughly how long, planned full-version differences, current state, will price change, how the community shapes development) **[S]**
- [ ] **A5.** Put the **Coming Soon page live** as early as assets allow — Steam requires it visible **at least ~2 weeks** before release, and every week live earns wishlists **[S, gated on B-track assets]**
- [ ] **A6.** Set launch price + launch discount **[S]**
- [ ] **A7.** Submit build + store page for Steam review (each pass is ~1–5 business days; budget for one rejection loop) **[S]**

### Workstream B — Windows build & pipeline *(ENG; ~1–2 person-weeks)*

The repo currently has **only a Linux build profile** and no Steam delivery path.

- [ ] **B1.** Add a **Windows x64 build profile**; produce a clean standalone build (IL2CPP, splash, icon, version string) **[M]**
- [ ] **B2.** PC platform sanity pass on that build: Wwise audio init, KB/M + Xbox/PS pads via Input System, alt-tab/focus, quit path from the menu, no mobile-only prompts or NativeShare dead-ends on PC **[M]**
- [ ] **B3.** Offline / UGS-outage behavior: launching with no network reaches the menu with a sane message instead of hanging on auth (guest sign-in path already exists — verify the failure path) **[S]**
- [ ] **B4.** **SteamPipe upload script** (steamcmd + app/depot vdf) + branch convention: `default` (live), `beta` (playtest), `internal` **[S]**
- [ ] **B5.** Repeatable build checklist or CI job that produces the depot from a tagged commit (manual is acceptable for EA; write the steps down) **[S]**
- [ ] **B6.** Enable **UGS Cloud Diagnostics** crash reporting, wired behind the existing consent toggle **[S]**
- [ ] **B7.** Verify Steam overlay renders over the game (no SDK needed; just test on real Steam via a playtest build) **[S]**

### Workstream C — In-game launch blockers *(ENG/DES; ~2 person-weeks)*

- [ ] **C1.** **Wire the settings panel prefab** in Unity per the editor checklist in `Docs/SettingsSystem/ARCHITECTURE.md` — display/resolution/vsync on PC is not optional for a Steam launch **[M]**
- [ ] **C2.** **First-session validation pass**: fresh install → guest sign-in → menu → first Tournament. Confirm the quest chain + controls overlay carries a new player without a human explaining anything; watch 3–5 first-timers, fix only what actually stops them **[M]**
- [ ] **C3.** Verify the funnel telemetry answers "median time-to-Tournament" from real sessions (instrumentation shipped; confirm the dashboard query works) **[S]**
- [ ] **C4.** **De-scope sweep of visible UI**: hide/label anything cut from EA — episode purchase buttons (per Decision 2), locked ARK/PORT screens' presentation, any "coming soon" surfaces get deliberate coming-soon styling instead of looking broken **[M]**
- [ ] **C5.** Confirm vessel-unlock economy numbers for launch (crystal earn rates × vessel prices — one tuning pass, values live in config SOs) **[S]**
- [ ] **C6.** Audio coverage sanity pass: menu bed, freestyle bed, per-minigame beds, UI stings — flag gaps and fill only silent-in-a-trailer holes **[M]**

### Workstream D — Stability gate *(ENG/QA; ~2 person-weeks, calendar-parallel)*

The punch-list quality bar, kept: **zero P0/P1 at submission; ≤ 10 P2 with workarounds.**

- [ ] **D1.** One profiling session on the floor machine (GTX 1060-class): 4-player Tournament at intensity 4. Lock the floor + targets (60 fps target / 30 floor, < 4 GB) **[M]**
- [ ] **D2.** 2-hour soak (freestyle + back-to-back tournaments): memory growth, GC spikes, prism-count ceiling behavior **[M]**
- [ ] **D3.** Multiplayer regression run: the existing S-series (`Docs/PartySystem/TESTS.md`) + P-series (`Docs/PresenceSystem/TESTS.md`) suites, plus 4P × 30-min disconnect/rejoin scenarios **[M]**
- [ ] **D4.** Bug bash week at T-minus-2 from submission; triage to the P0–P3 rubric **[M]**
- [ ] **D5.** Signed-off "bug-free" exit checklist (Garrett) before A7 submission **[S]**

### Workstream E — Marketing minimum *(MKT/ART; ~3 person-weeks, longest pole after A1)*

Scoped to what a converting EA page needs — the full P10 cadence machine is post-launch.

- [ ] **E1.** Positioning one-pager: "Party Game for Pilots," 3 pillars, EA framing **[S]**
- [ ] **E2.** Steam copy pack: short + long description, feature bullets (honest about EA state), tags, EA Q&A text from A4 **[M]**
- [ ] **E3.** **Capsule/key art set** (header 460×215, main 616×353, small 231×87, vertical 374×448, library 600×900) **[L — the single biggest art item left]**
- [ ] **E4.** ≥ 5 screenshots at 1920×1080: one per minigame + Tournament + freestyle/painting **[S]**
- [ ] **E5.** **Trailer v1** (≤ 60 s + a 30 s social cut) — capture from the real build; gameplay-first **[L]**
- [ ] **E6.** Discord live with invite link on the Steam page + a basic landing page **[S]**
- [ ] **E7.** **One closed playtest** via Steam beta branch (10–25 people): validates B4/B7/C2 in one beat, produces quotes + clips for the page **[M]** *(a second, open playtest is a nice-to-have, not blocking)*
- [ ] **E8.** Launch-day comms drafted and scheduled: announcement post, Discord, socials, press email **[S]**
- [ ] **E9.** Wishlist baseline + weekly tracking from the day A5 goes live **[S]**

### Workstream F — Legal & compliance *(BIZ; ~2 person-days)*

- [ ] **F1.** Host the privacy policy (template exists in `Docs/Legal/`) at a public URL; link it in-game (settings panel row exists) and on the Steam page **[S]**
- [ ] **F2.** Support contact (email or Discord) on the store page **[S]**
- [ ] **F3.** EULA decision: Steam's default Subscriber Agreement suffices for EA unless we need custom terms — decide and move on **[S]**
- [ ] **F4.** Confirm analytics/crash consent flow satisfies GDPR/COPPA posture for a PC-only EA launch (consent gating already implemented — this is a review, not a build) **[S]**

---

## ✂️ Explicitly CUT from EA launch (post-launch backlog)

Anything on this list appearing in a launch conversation is scope creep. It is all still on the roadmap — *after* launch.

| Cut | Why it's safe to cut |
|---|---|
| **Episode 1 token / all IAP / web checkout** | EA sells the app itself. The checkout flow has an unresolved server-side entitlement gap and must not take money until a backend verifies orders. |
| **Steamworks SDK integration** (achievements, stats, Steam Input configs, cloud saves, Steam identity) | None of it is required to ship. UGS Cloud Save already roams progress; Input System already handles pads. Add achievements in the first content patch — it's a good update beat. |
| **Rebinding UI** | Invert-Y/throttle shipped; full rebinding is a patch. (Steam Input can cover controller remapping without any work from us.) |
| **Host migration / rejoin-in-progress** | Disconnect handling exists; migration was an L-sized punch-list task with EA-acceptable absence. |
| **Localization** | English-only EA. Keep new strings in tables as we already do. |
| **Second (open) playtest / Next Fest** | Next Fest is for the 1.0 beat, where the wishlist push matters most. |
| **Missions, Arena games, non-Squirrel party games, mobile, cosmetics** | Already out of scope in the punch list; restated so the fence moves with us. |
| **"Minigames coming soon" per-vessel messaging as a bespoke system** | Covered by the C4 de-scope sweep — labeling, not a feature. |

---

## 🏁 Definition of Done (EA launch)

All of the following simultaneously true:

1. Steam page live ≥ 2 weeks with final assets; build passed review (A-track complete).
2. A fresh install on the floor machine reaches a completed 4-player Tournament (humans + bots) with no P0/P1 (B, C, D complete).
3. Crash reporting and funnel telemetry visible in dashboards from playtest builds.
4. One closed playtest run through the actual Steam beta branch.
5. Privacy policy + support contact live (F complete).
6. Garrett signs the exit checklist (D5).

---

## 📅 Suggested critical path (6 weeks)

| Week | Beat |
|---|---|
| **W1** | A1–A4 kicked off (partner account is the long pole). B1–B3 Windows build. E1–E2 copy. |
| **W2** | A5 **Coming Soon page live** (placeholder-quality capsules acceptable to open wishlists). B4–B7 pipeline + crash reporting. C1 settings wiring. E3/E5 art + trailer in flight. |
| **W3** | C2–C6 in-game blockers. D1–D2 perf floor + soak. E9 wishlist tracking. |
| **W4** | **E7 closed playtest on the beta branch.** D3 multiplayer regression. Final E3/E4/E5 assets onto the page. |
| **W5** | D4 bug bash. C4 de-scope sweep finalized. E8 comms drafted. |
| **W6** | D5 sign-off → **A7 submit for review** → launch on approval (+ buffer for one review bounce). |

---

*Maintained in-repo so it versions with the code. When an item completes, check it here in the same PR that completes it.*
