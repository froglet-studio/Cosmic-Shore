<div class="sec-eyebrow">Part II · Cross-cutting</div>

# Resilience & the error matrix

The unbreakable invariant — *the UI must never show "in party" when there is no party* — is enforced
by classifying every failure and mapping it to an explicit recovery. **No catch silently drops
state; no catch leaves the system worse than it entered.**

::: figure error-matrix
Every refresh/session exception is classified into one of four buckets, each with a defined recovery.
The presence-lobby SDK's stale-index noise is recognised as benign and swallowed.
:::

## Recovery actions by failure class

| Failure class | Detection | Recovery |
|---|---|---|
| **Benign** | `SessionException.Error == Unknown`; `LobbyPatcher` stale-index | Swallow silently — known self-correcting SDK churn |
| **Rate-limit** | "Too Many Requests" / HTTP 429 | Back off (2× interval), keep the session ref, retry next tick |
| **Definite (session gone)** | 404 / `SessionNotFound` / `SessionDeleted` / `NotInLobby` | `LeavePartyKeepHost` → fresh solo session; raise `OnHostConnectionLost` + per-member `OnPartyMemberLeft` so UI updates |
| **Transient** | everything else | Log, increment the consecutive-error counter, retry next tick; after a threshold, promote to definite |

## The timing constants that make it robust

| Constant | Value | Why |
|---|---|---|
| Base refresh interval | a few seconds | Under the UGS read rate-limit |
| Boost window | ~2 s for ~15 s | Tightens propagation right after an invite |
| Post-creation grace | 4 s | A fresh session can transiently 404 — don't misclassify it as gone |
| Outgoing invite timeout | 30 s | Stale invites expire automatically |
| Host-conflict retries | 2 | NM still shutting down |
| Rate-limit retries | 3, exp backoff | Cloud throttling |
| Transient retries | 5, exp backoff | SDK NRE / lobby-event collisions |
| Clear-joined-party timeout | 3 s | Bounded — a clean leave must never hang on a flaky property write |

::: insight Classify, don't crash
The key discipline is that recovery policy ("what to do") is decided by a small set of exception
classifiers, and is kept deliberately *separate* from the diagnostic classifier ("what to log", next
section). They can diverge legitimately — a failure can be interesting to log but not worth a retry —
and keeping them apart stops log format from accidentally driving retry behaviour.
:::

The benign bucket deserves a note: the same UGS stale-index defect surfaces three different message
strings across restarts, so the code keys off the **structured** `Error == Unknown` reason rather than
message text — message-matching turned into whack-a-mole, while every *actionable* failure carries a
specific reason that the definite/rate-limit branches catch first.
