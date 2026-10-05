# Multiplayer / Netcode — Hardening Roadmap & Invariants

**Start here when you ask “what should I work on next in multiplayer?”**

This is the cross-cutting, big-picture layer above the per-system trackers. It captures (1) the
system's strengths as **invariants to preserve** (regression guardrails) and (2) the **real risks**
as a prioritized next-work queue. Granular, already-sequenced items live in the per-layer trackers
(linked at the bottom) — this file does not duplicate them.

Companion to the PDF dossier in `Docs/MultiplayerArchitecture/`
(Part II → “Future improvements & roadmap”).

## How to use this

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

**Measured, not asserted: there are ZERO 🔴 open party or presence bugs.** What there is
instead is a large body of work that is *fixed but never run in the Editor*, and that is
what "the multiplayer systems don't perform well" actually resolves to today. More fixes do
not help until this is drained — a second fix landing on an unverified first one is how a
regression becomes un-bisectable.

| What | State | Where |
|---|---|---|
| **B18–B23** — client can't leave a match; lost scene transition = black screen; a leaver strands the ready gate; a leaver takes their score out; Scoreboard exit/rematch unwired; arcade card lobby doesn't follow the host | all 🟡 *fixed, unverified* | `../PartySystem/BUGS.md` |
| **B5** — 3-4 player sequential/concurrent join | 🟢 but *"wants a multi-machine pass"*, and the pre-fix build passes without an arcade card selected first | `../PartySystem/BUGS.md` |
| **B1 · B4 · B6** — LobbyPatcher spam, second invite not delivered, `WrappedLobbyService` NRE | all 🟡 | `../PresenceSystem/BUGS.md` |
| **Join + Spectate** (the whole no-invite entry path, `partySession` publication, the spectator approval token, `CountHumanClients` subtraction in four systems) | *"Not yet verified in the Editor"* — authored out-of-editor, syntax-gated only | `../PartySystem/SPECTATOR.md` §6 |
| **Offline mode** (loopback host, `ResetPartyLayerAsync`, the reconnect re-boot) | shipped, no editor pass recorded | `../OFFLINE_MODE.md` |
| **The multiplayer SDK upgrade** — NGO 2.5.0→2.13.3, UGS Multiplayer 1.1.8→2.3.3, Transport 2.6.0→2.7.4, MPPM 1.6.1→2.0.2, Friends 1.1.1→1.3.0 | package-only, **zero C# changes against ~180 UGS call sites**, no editor compile | `Packages/manifest.json` |

**Do these in this order, because each one invalidates the next if it fails.**

1. **Open the Editor once and read the console.** The SDK bump is the whole multiplayer
   surface changing underneath the code at once. Two known deprecation surfaces will appear
   as warnings: **29** `[ServerRpc(RequireOwnership = …)]` (NGO 2.7.0 → `RpcInvokePermission`)
   and one `NetworkObject.IsSceneObject` (2.13.0 → `InScenePlaced`). The one thing that
   cannot be checked outside the Editor is that the **engine** module backing MPPM 2.0's
   `CurrentPlayer` exists in this build — MPPM 2.0.2 ships **zero** C# files, so
   `CurrentPlayer.IsMainEditor` / `ReadOnlyTags()` (4 call sites, 2 files, both inside
   `#if UNITY_EDITOR`) now resolve from the engine. If they do not, it is 4 compile errors
   and reverting one manifest line.
2. **Run the 3-VP MPPM smoke** (`../PartySystem/TESTS.md` S-series + `../PresenceSystem/TESTS.md`
   P-series). This is exit criterion 6 and it gates everything below.
3. **Run B5's acceptance with its precondition** — 4-VP concurrent invite **with an arcade
   card selected before the second guest joins**. Without that precondition the pre-fix build
   passes too, so a green run that skipped it proves nothing. **NGO 2.13.2 fixes this class
   upstream** (*"pre-instantiated network prefab instances being marked as in-scene placed"*,
   #4093), which is the root cause behind B16/B5/B11/B14 and what
   `NetworkSceneObjectGuard` plus six producer seams exist to work around. So this run is
   also the test of whether that workaround can eventually be retired — **do not retire it on
   the strength of the changelog alone.**
4. **Run SPECTATOR.md §6's six steps.** It is the newest and least-exercised path, and it is
   the one the roadmap has no bug entries for *because nobody has played it yet* — absence of
   bugs here is absence of testing, not evidence of health.
5. **Only then** take B18–B23 off 🟡, and only the ones a run actually covered.

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

`../PartySystem/BUGS.md` (**B2–B23**) and `../PresenceSystem/BUGS.md` (B1 · B4 · B6).
B5 is also listed above because multi-joiner reliability is a roadmap-level priority, not just a bug.

> **Corrected 2026-10-05.** This line read "(B2 · B3 · B5 · B7)" — the party tracker's own
> original four. It has held **B2–B23** since; B18–B23 landed without this pointer moving. That
> is the same index-vs-entry drift `../PartySystem/BUGS.md` warns about in its own footnote
> ("a status only the index carries is one nobody acts on"), one level up. **When you add a bug
> entry, check who points at the list as well as the list itself.**
