# Multiplayer / Netcode — Hardening Roadmap & Invariants

**Start here when you ask “what should I work on next in multiplayer?”**

This is the cross-cutting, big-picture layer above the per-system trackers. It captures (1) the
system's strengths as **invariants to preserve** (regression guardrails) and (2) the **real risks**
as a prioritized next-work queue. Granular, already-sequenced items live in the per-layer trackers
(linked at the bottom) — this file does not duplicate them.

Companion to the PDF dossier in `Docs/MultiplayerArchitecture/`
(Part II → “Future improvements & roadmap”).

Companion **review** (2026-10-06): `REVIEW_INVITE_AND_RESILIENCE.md` — the Invite / Join-direct flow, request
discipline (retries, 429s, duplicate requests) and disconnect resilience for a 4-player party, measured against
industry practice, with the edge-case matrix, a four-phase rollout and the MPPM test plan. Several queue items
below (push-based presence, reconnection, observability) are specified there.

## How to use this

> **For the launch programme, start with
> [`HARDENING_PLAN_STEAM_LAUNCH.md`](HARDENING_PLAN_STEAM_LAUNCH.md)** (2026-10-08) — the ordered
> P0/P1/P2 queue to a Steam launch, the scale ladder (what breaks at 1, 4, 10, 40 and 100 CCU), the
> four-level test strategy, the diagnosis/JSON schema, and the measured refactor list. Its
> executable form is [`../prompts/MULTIPLAYER_HARDENING_PROMPT.md`](../prompts/MULTIPLAYER_HARDENING_PROMPT.md).
> This file stays the live queue; that one is the programme.


- The **invariants** are guardrails — keep them true. A change that breaks one is a regression, not a
  feature. They mirror the locked decisions in `../PartySystem/ARCHITECTURE.md`.
- The **risks** are the prioritized queue. Pick the highest unchecked item. Each has a one-line
  acceptance hint and a doc/code reference.
- Check an item off (`- [x]`) when it lands, with the commit hash.

---

## ✅ Genuinely strong — invariants to preserve (do not regress)

- [ ] **Two-level sessions** — lobby-only Presence vs Relay-backed Party stay separate.
- [ ] **EAGER per-user Relay** (“Always-InParty”) — never reintroduce lazy / on-first-invite creation.
- [ ] **Single-writer SOAP** — exactly one writer per shared container (`HostConnectionDataSO`, `FriendsDataSO`).
- [ ] **`PartyStateMachine` is the only lifecycle authority** — no boolean-flag drift.
- [ ] **`.AsMainThread()` at every UGS / Netcode await** — never `UniTask.SwitchToMainThread()` / `Yield(Update)` for thread marshaling (see `../THREADING.md`).
- [ ] **Server-authoritative, unified spawn pipeline** — one path for menu / AI / gameplay vessels.
- [ ] **Every catch maps to a named recovery** (benign / rate-limit / gone / transient) — no silent state drop.
- [ ] **Session is authoritative over presence** — the lobby is a hint, never the source of truth.

## ⚠️ Real risks — prioritized improvement queue (the “next TODOs”)

### Highest — the verification debt, which is now the binding constraint (added 2026-10-05)

**Measured, not asserted: there were ZERO 🔴 open party or presence bugs** when this block was
written on 2026-10-05. The **first MPPM run, the next day, produced B24** — which is the whole
argument of this block arriving on schedule: the queue was not short of fixes, it was short of
runs, and one run found a latent defect that no amount of further reading had. What there is
instead is a large body of work that is *fixed but never run in the Editor*, and that is
what "the multiplayer systems don't perform well" actually resolves to today. More fixes do
not help until this is drained — a second fix landing on an unverified first one is how a
regression becomes un-bisectable.

| What | State | Where |
|---|---|---|
| **B18–B23** — client can't leave a match; lost scene transition = black screen; a leaver strands the ready gate; a leaver takes their score out; Scoreboard exit/rematch unwired; arcade card lobby doesn't follow the host | all 🟡 *fixed, unverified* | `../PartySystem/BUGS.md` |
| **B24** — a 429 matched neither retry filter: the guest bounced to solo and the host fell back to an OFFLINE session, hiding the online-only party panel. Found by the **first MPPM run**, 2026-10-05 | 🟡 *fixed, unverified* | `../PartySystem/BUGS.md` |
| **B5** — 3-4 player sequential/concurrent join | 🟢 but *"wants a multi-machine pass"*, and the pre-fix build passes without an arcade card selected first | `../PartySystem/BUGS.md` |
| **B1 · B4 · B6** — LobbyPatcher spam, second invite not delivered, `WrappedLobbyService` NRE | all 🟡 | `../PresenceSystem/BUGS.md` |
| **Join + Spectate** (the whole no-invite entry path, `partySession` publication, the spectator approval token, `CountHumanClients` subtraction in four systems) | *"Not yet verified in the Editor"* — authored out-of-editor, syntax-gated only | `../PartySystem/SPECTATOR.md` §6 |
| **Offline mode** (loopback host, `ResetPartyLayerAsync`, the reconnect re-boot) | shipped, no editor pass recorded | `../OFFLINE_MODE.md` |
| **The multiplayer SDK upgrade** — NGO 2.5.0→2.13.3, UGS Multiplayer 1.1.8→2.3.3, Transport 2.6.0→2.7.4, MPPM 1.6.1→2.0.2, Friends 1.1.1→1.3.0 | package-only, zero C# changes — but **API-resolved offline 2026-10-05: 145 files, 0 missing members** (see step 1); no editor compile | `Packages/manifest.json` |

**Do these in this order, because each one invalidates the next if it fails.**

1. **Open the Editor once and read the console.** The SDK bump is the whole multiplayer
   surface changing underneath the code at once.

   **The offline half of this step is now DONE (2026-10-05) and came back clean.** Every
   symbol our code reaches into those packages was resolved against the packages' own
   published API surface — the generated `.api` dumps the UGS packages ship
   (`Unity.Services.Multiplayer.api` alone is 2,025 lines) plus a brace-aware parse of
   NGO's and Transport's sources, which ship none. **145 of our files use the SDK
   namespaces; 0 members are missing.** A "method not found" error is now unlikely rather
   than unknown. Three corrections the measurement produced:

   - **The deprecation count was 29; it is 19.** `grep RequireOwnership` returns 29 lines,
     but **10 are comments** explaining why ownership is required. There are exactly **19**
     `[ServerRpc(RequireOwnership = false)]` attributes, plus the **1**
     `NetworkObject.IsSceneObject` at `AstroLeagueBall.cs:1496` — expect **~20**
     deprecation warnings, not ~30.
   - **Both are warnings and stay warnings.** `ServerRpcAttribute` itself is *not* obsolete
     (only its `RequireOwnership` property is), `IsSceneObject` is `[Obsolete]`-with-warning,
     and the Unity project carries **no `csc.rsp` and no warnings-as-errors** — the only
     `TreatWarningsAsErrors` in the tree is `Port/`, the .NET port, which does not build
     this code.
   - **`IsSceneObject` is behaviourally unchanged — do not "fix" it blind.** It is *not* a
     forward to `InScenePlaced`: it is an independent, **non-serialized** `bool?`
     auto-property, while `InScenePlaced` is `[field: SerializeField] bool`. They are not
     interchangeable. Our one use still works because NGO keeps maintaining the obsolete
     property from `SpawnNetworkObjectLocallyCommon` — which runs on **both** the authority
     and non-authority local-spawn paths — and says so in a comment (*"Obsolete with
     warning means we need the underlying behaviour to keep existing"*). Declaration and
     maintenance are identical in 2.5.0 and 2.13.3, compared side by side. A rename to
     `InScenePlaced` would read a *different, serialized* field: a behaviour change, not a
     cleanup.

   **What remains Editor-only:** whether the **engine** module backing MPPM 2.0's
   `CurrentPlayer` exists in this build. MPPM 2.0.2 ships **zero** C# files — confirmed,
   the tarball is documentation and a `package.json` — so `CurrentPlayer.IsMainEditor` /
   `ReadOnlyTags()` (4 sites: `MultiplayerSetup.cs:271,276` and
   `AuthenticationServiceFacade.cs:400,403`, all inside `#if UNITY_EDITOR`) now resolve
   from the engine. Two things say they will: MPPM 2.0.2's own docs still document
   `Unity.Multiplayer.Playmode.CurrentPlayer.IsMainEditor` and `ReadOnlyTags()` as the 2.0
   API under that same `using`, and the package requires engine **6000.3.0b10+** while we
   run **6000.3.17f1**. If they do not resolve it is 4 compile errors and reverting one
   manifest line. **This is the only compile risk left in the bump.**

   QA item: **QA-NET-SDK-UPGRADE** (`../QA/QA_BACKLOG.md`, P0).

2. **Run the 3-VP MPPM smoke** (`../PartySystem/TESTS.md` S-series + `../PresenceSystem/TESTS.md`
   P-series). This is exit criterion 6 and it gates everything below.
   QA item: **QA-NET-PRESENCE-PARTY**.
3. **Run B5's acceptance with its precondition** — 4-VP concurrent invite **with an arcade
   card selected before the second guest joins**. Without that precondition the pre-fix build
   passes too, so a green run that skipped it proves nothing. **NGO 2.13.2 fixes this class
   upstream** (*"pre-instantiated network prefab instances being marked as in-scene placed"*,
   #4093), which is the root cause behind B16/B5/B11/B14 and what
   `NetworkSceneObjectGuard` plus six producer seams exist to work around. So this run is
   also the test of whether that workaround can eventually be retired — **do not retire it on
   the strength of the changelog alone.**
   QA item: **QA-NET-PARTY-4P-JOIN** — whose steps carry the precondition, so it cannot be
   skipped by accident.
4. **Run SPECTATOR.md §6's six steps.** It is the newest and least-exercised path, and it is
   the one the roadmap has no bug entries for *because nobody has played it yet* — absence of
   bugs here is absence of testing, not evidence of health.
   QA item: **QA-NET-JOIN-SPECTATE**.
5. **Only then** take B18–B23 off 🟡, and only the ones a run actually covered. Each has
   its own QA item so a run maps to exactly one bug: **QA-NET-CLIENT-CAN-LEAVE** (B18+B22),
   **QA-NET-SCENE-TRANSITION-TIMEOUT** (B19), **QA-NET-READY-GATE-LEAVER** (B20),
   **QA-NET-MIDMATCH-LEAVER** (B21), **QA-NET-ARCADE-LOBBY-FOLLOW** (B23).
   ⚠ **B23's item needs three real machines, not MPPM** — virtual players share one process
   and one `GameDataSO`, which is precisely the failure class B23 is about, so an MPPM run
   cannot clear it. The tracker says so; the plan now does too.
   Offline mode has one as well: **QA-NET-OFFLINE-MODE**.

*Acceptance for this whole block:* every 🟡 above is either 🟢-with-a-dated-run or back to 🔴
with a reproduction. A bug that is neither was not tested.

### High
- [~] **Host-loss resilience / migration.** The host *is* a player; a host drop ends the whole party. **Clean-reform half DONE** (`../PartySystem/BUGS.md` B10): a mid-party host disconnect now bounces every remaining member to its OWN working solo menu+host with a "Host disconnected" notice (no dead-session hang), in the lava-lamp menu AND any game scene. **Still open — true migration:** promote a remaining client to host and keep the *same* party alive (Relay re-host + Netcode host-migration + state transfer). *Acceptance for the remaining work:* a mid-party host disconnect leaves the others **together** under a new host, not just cleanly reformed as solos.
- [ ] **Prove 3–4-player party reliability (confirm B5).** **Root-caused 2026-09-16** — the second sequential joiner failed because browsing an arcade card in Menu_Main planted four un-spawned fauna `NetworkObject`s of one prefab, which is B16's scene-object index collision arriving through three producers that skipped `FaunaNetworkSync.ServerSpawn`. Fixed at the producer (the seam now lives in `CellLifeSpawnerBase.SpawnFaunaWithDomain`, the one `Instantiate` every producer reaches) with a `NetworkSceneObjectGuard.Sweep` at connection approval as the backstop. *Acceptance:* 4-VP concurrent-invite MPPM (exit criterion 8) green repeatably, **with an arcade card selected before the second guest joins** — without that precondition the pre-fix build passes too. *Ref:* `../PartySystem/BUGS.md` B5, B16.

### Med–High
- [ ] **Push-based invites / presence.** Replace property *polling* with lobby subscription events to cut invite latency and the SDK stale-index churn that surfaces as B1/B6. *Acceptance:* invite delivery is event-driven; B1/B6 churn drops materially. *Ref:* `../PresenceSystem/BUGS.md` B1, B6.

### Med
- [ ] **Scale & cost story.** Reap idle Relay allocations; shard or query-based discovery beyond the single 100-player `PRESENCE_LOBBY`; add Relay/lobby cost telemetry. *Acceptance:* a documented plan + dashboards for >few-hundred concurrent users.
- [ ] **Production observability.** `NetworkDiagnostics` is dev-only (stripped from release). Add a release-safe party success/failure + join-latency funnel via the analytics managers. *Acceptance:* party reliability is measurable on shipped builds.
- [ ] **CI gate.** No CI today. Run edit-mode + headless play-mode tests (incl. D4 once landed) on every PR so exit criteria are enforced, not manual. *Ref:* `../PartySystem/REFACTOR.md` D4.

### Low–Med
- [ ] **Approval + reconnect hardening.** Validate the joiner against an active invite + capacity in the connection-approval callback (currently unconditional); add reconnect-resume into the same party instead of bounce-to-solo. *Ref:* `../PartySystem/BUGS.md` B5 notes (approval), `PartyState.Reconnecting`.

---

## Already planned (granular backlog — don’t duplicate, link)

- `../PartySystem/REFACTOR.md` — service decomposition (Refactors 1–3, cross-class `leave→reset→join`), deferred D1–D5 (incl. **D4** MPPM play-mode test automation, **D3** `GameDataSO` session split).
- `../PartySystem/TODOS.md` + `../PresenceSystem/TODOS.md` — rate-limit mitigations (~~refresh jitter~~ **done 2026-10-05**, write-coalescing TODO-P2), “Reconnecting…” UI, per-class toasts, invite-freshness timestamp. Note **TODO-1 was re-scoped** (its "one test is the only consumer" premise measured 34 call sites in 10 files) and **TODO-9 closed**.
- `../NetworkDiagnostics/TODOS.md` — `BoostPolling`, active reachability probing, a `NetDiag Report` tool, broader adoption.

## Open bugs (track separately)

`../PartySystem/BUGS.md` (**B2–B24**) and `../PresenceSystem/BUGS.md` (B1 · B4 · B6).
B5 is also listed above because multi-joiner reliability is a roadmap-level priority, not just a bug.

> **Corrected 2026-10-05.** This line read "(B2 · B3 · B5 · B7)" — the party tracker's own
> original four. It has held **B2–B24** since; B18–B23 landed without this pointer moving. That
> is the same index-vs-entry drift `../PartySystem/BUGS.md` warns about in its own footnote
> ("a status only the index carries is one nobody acts on"), one level up. **When you add a bug
> entry, check who points at the list as well as the list itself.**
