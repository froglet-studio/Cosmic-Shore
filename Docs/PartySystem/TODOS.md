# Party System — TODOs

Parking lot for minor improvements that don't rise to a refactor commit
or a bug. Each entry has enough context that it can be picked up cold.

> **Looking for the big-picture “what should I work on next?”** — the
> cross-cutting roadmap (host-loss resilience, multi-joiner reliability,
> push-vs-poll, scale/cost, production observability, CI) plus the
> strengths-to-preserve invariants live in
> `../MultiplayerArchitecture/ROADMAP.md`. This file is the granular
> party-side parking lot beneath it.

## Code health

### TODO-1. Remove `HostConnectionService.Instance` static accessor — RE-SCOPED 2026-10-05

> **The original premise was wrong by 34x and is kept here as the correction.** It read:
> *"After Refactor 1 (PIC) migrates PIC to `[Inject] IHostConnectionService`, the only
> remaining consumer of the `Instance` static accessor is `PartyInviteSystemTests.cs:1067`
> (reflection). When that test migrates to DI, the static can be deleted."*
>
> Measured on 2026-10-05: **34 call sites across 10 files**, and Refactor 1 has not
> happened — `PartyInviteController` still carries no `IHostConnectionService` inject.

**Where it is actually read** (`grep -rn "HostConnectionService.Instance"`):

| File | Layer |
|---|---|
| `Controller/Party/PartyInviteController.cs` | party (Refactor 1's target) |
| `Controller/Party/HostConnectionService.cs` | its own `Instance` plumbing |
| `Controller/Party/FriendsInitializer.cs` | party |
| `Controller/Party/Interfaces/IPartyStateQuery.cs` | doc reference |
| `Controller/Multiplayer/MultiplayerSetup.cs` | **outside the party layer** |
| `UI/Elements/ArcadeLobbyList.cs` | **UI** |
| `UI/Elements/FriendsListPanel.cs` | **UI** |
| `System/ReconnectService.cs` | **offline/online switch** |
| `System/AuthenticationSceneController.cs` | **boot gate** |
| `System/OfflineModeService.cs` | **offline** |

**So this is not a cleanup behind one test.** Six of those files are production code in
three layers that were written against the static *after* the TODO was filed — the boot
gate and the whole offline/reconnect path among them. Deleting the static is a real DI
migration touching ten files, and it is **sequenced after** Refactor 1 rather than
unlocked by it.

**Risk.** Low per-site (every miss is a compile error), but it is ten files of churn in
the boot and offline paths, which are the two areas with the least editor verification
today. Do it as its own branch, never folded into a behaviour change.

**General lesson (and the reason this entry is kept rather than edited).** A TODO's
scope claim is a measurement taken at the moment somebody stopped looking. Re-measure
before planning around it — see `.claude/skills/refactor`.

### TODO-2. `Docs/PARTY_OPEN_BUGS.md` reference updates — DONE

**Status.** Resolved. The old file is deleted; its 7 bugs were split into
`Docs/PartySystem/BUGS.md` (B2, B3, B5, B7) and
`Docs/PresenceSystem/BUGS.md` (B1, B4, B6). The codebase had 0 inline
references to `PARTY_OPEN_BUGS` (verified by grep), so no code comments
needed updating.

### TODO-3. `Docs/PARTY_SYSTEM_REFACTOR.md` reference updates — DONE

**Status.** Resolved. The old file is deleted; its content was migrated in
full (locked design, investigation Q&A, error-handling matrix, exit
criteria → `ARCHITECTURE.md`; per-commit protocol + deferred items D1-D5 →
`REFACTOR.md`). All 20 inline code comments across 9 files were repointed to
`Docs/PartySystem/ARCHITECTURE.md` (with the specific Q-anchor where the
comment referenced one). Verified 0 remaining `PARTY_SYSTEM_REFACTOR`
references in `Assets/`.

## Diagnostics

### TODO-4. Adopt NetDiag in non-party catch sites → see NetworkDiagnostics

Canonical: `../NetworkDiagnostics/TODOS.md` § "TODO-2. Broader adoption
— non-party UGS catches" (pattern + candidate site list:
`AuthenticationServiceFacade`, `FriendsServiceFacade`, PlayFab, IAP,
leaderboards-write). Not duplicated here.

## UI / UX (deferred — needs design pass)

### TODO-5. Specific toast messages per NetDiag class

**Why.** Today's bounce-to-solo-menu always shows "Couldn't join —
returned to your menu." Once NetDiag log data tells us which classes
fire in practice, we can pick which classes deserve a specific message.

**Candidate matrix (sketch — needs design sign-off):**

| Class | Possible specific toast |
|---|---|
| `Offline` | "Internet connection lost — returned to your menu" |
| `SessionGone` | "Host left the party" |
| `Cancelled` (user-driven) | Suppress toast entirely |
| `RateLimit` | Generic "Couldn't join — please try again in a moment" |
| `AuthRequired` | "Signed out — please log in again" |
| `Transient` | Generic (existing) |
| `Unknown` | Generic (existing) — also a signal to extend `ClassifyException` |

**Touchpoint.** `PartyInviteController.RecoverFromFailedTransitionAsync`
or its successor `TransitionRecoveryService` after Refactor 1.

### TODO-6. Auto-dismiss stale invites on SessionGone

**Why.** When `class=SessionGone` is observed during an Accept, the
invite the user is trying to accept is provably stale. The invite
notification panel could auto-dismiss the matching entry so the user
isn't tempted to retry.

**Touchpoint.** New SOAP event `OnInviteSessionGone(sessionId)` raised
from the bounce path; `PartyInviteNotificationPanel` subscribes and
removes the matching entry.

### TODO-7. Invite freshness window

**Why.** A short timestamp on outgoing invites lets the receiver refuse
to display invites older than N seconds, reducing incidence of
SessionGone-on-accept.

**Touchpoint.** `invite_data` lobby player property already exists;
extending it with a timestamp field is additive.

**Risk.** Clock skew between MPPM VPs and across real devices. Use a
generous window (e.g. 60 s) to avoid false rejections.

## Performance / polish

### TODO-8. Coalesce startup property writes → see PresenceSystem

This is a presence-lobby write-path concern (the `LobbyPropertyWriter`
startup churn that contributes to B1). Tracked canonically in
`../PresenceSystem/TODOS.md` § "TODO-P2. Coalesce startup property
writes" — not duplicated here.

### TODO-9. Document `LobbyRefreshScheduler.Boost()` semantics — DONE

**Status.** Resolved; verified 2026-10-05. `Boost()` carries a full `<summary>` plus a
`<remarks>` list naming its three trigger cases (local player sends an invite, incoming
invite detected, acceptance signal received), and `BOOST_WINDOW_SECONDS` /
`BOOSTED_INTERVAL_SECONDS` each carry the reasoning for their value — including why
0.75 s is the floor (the ~1/s UGS read cap). Nothing left to write.

## Party panel readouts

### TODO-10. Per-slot READY state on the party panel

**Why.** The arcade party panel now carries a synced seating and a per-pilot domain
halo (`Docs/PartySystem/UI.md` § "Seating"), which makes the natural next readout "who
has confirmed". The request that produced the seating work named it explicitly, with no
further spec.

**The blocker is not the UI.** `Docs/ArcadeLaunch/ARCHITECTURE.md` records that ready
lights are deliberately **a COUNT, not an identity** — the sync layer replicates how many
pilots confirmed, never which. A per-slot tick therefore needs the identity put on the
wire first; drawing one off the count is a guess that is wrong for every arrangement but
"the first N seats confirmed".

**Touchpoint.** Whatever publishes the ready count for `ArcadeGameConfigureModal`, then
`FriendInfoSlot` (which already resolves a seat to a UGS player id, so the slot side is a
lookup once the identity exists).

### TODO-11. Intensity vote state on the party panel

**Why.** Named alongside TODO-10 in the same request, and unspecified. Nothing in the
codebase votes on intensity today — `ArcadeGameConfigSO.SelectedIntensity` is a single
host-side value synced to clients by `MultiplayerMiniGameControllerBase`, so this is a
new mechanic (who may vote, how ties resolve, whether the host is bound by it) rather
than a new readout.

**Do not** infer the design from the panel: decide the rule first, then the display.

### TODO-12. Fold the three domain-halo implementations into one component

**Why.** A breathing halo in a pilot's domain colour, drawn behind their avatar, now exists
**three times**: `ConnectingPlayerRoster` (the load screen's pilot chips),
`RematchVoteRoster` (the scoreboard's rematch faces) and `PartySlotDomainGlow` (the arcade
party panel). A player meets all three within minutes of each other, so it is one visual
with three call sites — and three copies drift.

They already differ in ways nobody decided: the two rosters build their halo inside a
per-chip wrapper and tick it from their own loop, the party slot ensures its own sibling and
ticks itself; the rosters' generated fallback sprite uses `Mathf.SmoothStep(0.5f, 1f, d)` —
which is Unity's *interpolate-between-two-values* overload, so it ramps 0.5 → 0 rather than
giving the "solid core, feathered rim" its comment claims — while the party slot uses a real
edge-gated smoothstep with a squared falloff.

**What a shared component owes each caller.** The rosters need a halo that lives inside a
wrapper they build and is ticked from their loop (they already tick per chip for other
reasons); the party slot needs one that finds its own place beside an avatar it does not own.
So the seam is probably the halo's *drawing and tuning*, with placement left to the caller.

**Do not** start by unifying the numbers alone — `PartySlotDomainGlow` already adopted the
rosters' shipped values, so the three read the same today. The debt is the three copies.

> **Two corrections, 2026-10-05.**
>
> **It was one roster, not two.** `ConnectingPlayerRoster` has no generated fallback at all
> — it falls back to the chip template's own sprite (`haloSprite ? haloSprite :
> _templateSprite`). `RematchVoteRoster.GeneratedHalo` was the only site with the
> `Mathf.SmoothStep(0.5f, 1f, d)` defect.
>
> **The sprite-falloff half is FIXED** (commit `02ef66117`), so it is no longer available as
> the evidence that the duplication costs something. Measured by compiling and running the
> shipped maths: centre alpha was **127/255** against an intended 255, and the "solid core"
> was **0 pixels** — so `haloReadyMaxAlpha` 0.95 could never exceed 0.475. It now matches
> `PartySlotDomainGlow`'s gate.
>
> **One real difference remains, and it is the live argument for unifying:**
> `PartySlotDomainGlow` additionally squares the falloff (`a *= a`) "so the visible band sits
> close in around the avatar rather than washing the whole cell"; `RematchVoteRoster` does
> not. That is a LOOK decision nobody has made across both sites — deliberately not imported
> by the bug fix — and it is exactly what a shared component would force someone to decide
> once.

