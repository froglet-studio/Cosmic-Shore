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

### High
- [~] **Host-loss resilience / migration.** The host *is* a player; a host drop ends the whole party. **Clean-reform half DONE** (`../PartySystem/BUGS.md` B10): a mid-party host disconnect now bounces every remaining member to its OWN working solo menu+host with a "Host disconnected" notice (no dead-session hang), in the lava-lamp menu AND any game scene. **Still open — true migration:** promote a remaining client to host and keep the *same* party alive (Relay re-host + Netcode host-migration + state transfer). *Acceptance for the remaining work:* a mid-party host disconnect leaves the others **together** under a new host, not just cleanly reformed as solos.
- [ ] **Prove 3–4-player party reliability (close B5).** The second sequential joiner fails today, so parties beyond two aren't dependable. *Acceptance:* 4-VP concurrent-invite MPPM (exit criterion 8) green repeatably. *Ref:* `../PartySystem/BUGS.md` B5.

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
- `../PartySystem/TODOS.md` + `../PresenceSystem/TODOS.md` — rate-limit mitigations (refresh jitter, write-coalescing), “Reconnecting…” UI, per-class toasts, invite-freshness timestamp.
- `../NetworkDiagnostics/TODOS.md` — `BoostPolling`, active reachability probing, a `NetDiag Report` tool, broader adoption.

## Open bugs (track separately)

`../PartySystem/BUGS.md` (B2 · B3 · B5 · B7) and `../PresenceSystem/BUGS.md` (B1 · B4 · B6).
B5 is also listed above because multi-joiner reliability is a roadmap-level priority, not just a bug.
