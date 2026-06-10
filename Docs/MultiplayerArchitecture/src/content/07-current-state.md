<div class="sec-eyebrow">Part I · Overview</div>

# Where it stands

The party system has an explicit definition of "done": a set of **unbreakable exit criteria** that
must hold under adversarial conditions — network drops, client crashes, fast input, and concurrent
invites.

| # | Criterion | Status |
|---|---|---|
| 1 | No fatal failure — no vessel despawn outside an intentional leave, no kicks, no uncaught UGS exception | <span class="badge fixed">🟢 Passing</span> |
| 2 | No stuck UI — party UI always reflects ground truth | <span class="badge fixed">🟢 Passing</span> |
| 3 | No silent state divergence — host and clients agree within one refresh tick | <span class="badge fixed">🟢 Passing</span> |
| 4 | All transitions reversible — a failed accept returns cleanly to solo menu | <span class="badge fixed">🟢 Passing</span> |
| 5 | Idempotent retries — double-tap Accept can't start two transitions | <span class="badge fixed">🟢 Passing</span> |
| 6 | 3-VP accept / decline / leave smoke green every commit | <span class="badge investigating">🟡 Per-commit gate</span> |
| 7 | 3-VP stress (5 accepts, random declines/leaves) green | <span class="badge investigating">🟡 Per-commit gate</span> |
| 8 | 4-VP concurrent invites — all clients join or bounce cleanly | <span class="badge investigating">🟡 Per-commit gate</span> |

Criteria 1–5 hold as of the 17-commit refactor plus the catch-guard fix. Criteria 6–8 are the active
verification gate run on every change via Unity's **Multiplayer Play Mode (MPPM)**, which boots
several virtual players in one editor.

## What's solid, what's open

::: cols
- **Solid:** eager session model; single-writer SOAP; the `.AsMainThread()` threading contract;
  server-authoritative spawning; the validated 7-state party machine; per-catch failure
  classification; clean leave and clean bounce paths (both MPPM-verified).
- **Open (tracked):** a semaphore-dispose race on fast play-mode abort (B2); a late-second-joiner
  failure (B5); two presence-lobby discovery edge cases under simultaneous joins (B4, B6). All are
  documented with reproductions and candidate fixes in Part II.
:::

::: insight Hardening as a discipline, not an event
The system is "hardened toward unbreakable" — an explicit, ongoing posture. Every catch block maps to
a named recovery action, every diagnostic line is classified and strippable from release builds, and
every locked decision has a written rationale so a future change doesn't silently reintroduce a solved
problem. The remaining open bugs are edge cases, not architectural cracks.
:::

The rest of this document is the engineering reference behind all of the above.
