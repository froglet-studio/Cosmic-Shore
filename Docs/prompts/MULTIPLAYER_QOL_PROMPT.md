# Prompt — multiplayer quality of life for an invite cohort

Paste everything below into a fresh session.

---

The milestone's whole distribution mechanic is **players inviting their friends** — one to three
each, compounding. That loop runs on the social surface of the game, and the social surface has a
hole in the middle of it:

**There is currently no way for a player to send a friend request.** The facade methods exist and
are called by nothing. A player can receive a request and accept it; they can never initiate one.
In a program whose growth model is *invite your friends*, the friends list can only grow from the
outside.

That is the headline, but it is not alone. This task is about the **friction a player feels in the
multiplayer experience** — not crashes (those are R16) and not features that were deliberately cut.
The things below are all small, all measured, and all sit directly on the invite loop.

Read `Docs/PartySystem/UI.md` first — it is the inventory of this surface — then
`Docs/PartySystem/ARCHITECTURE.md` § "Locked design".

## Measured 10 Sep 2026 — re-verify each before acting

### 1 · A player cannot send a friend request · **severe**

`FriendsServiceFacade` exposes `SendFriendRequestByNameAsync(name)` and
`SendFriendRequestAsync(playerId)`. Grepping all of `Assets/_Scripts` for either, excluding the
facade itself, returns **nothing**. The two UI surfaces that used to call them —
`AddFriendPanel` and `FriendInfoEntry` — no longer exist in the project.

`FriendsListPanel` renders only the Online and Requests sections. Incoming requests still arrive as
`RequestInfoEntry` rows with Accept/Decline, so the receive half works and the send half is absent.

CLAUDE.md already records this as a known gap. It is the single highest-value QoL fix on this list
because every other social feature depends on a friends list existing.

### 2 · An invite you miss is gone forever · **high**

`PartyInviteNotificationPanel` auto-hides after **`autoHideSeconds = 3f`** and is explicitly
**latest-wins** — a second invite replaces the first. There is no inbox, no history, and no badge.

Three seconds is the whole window, and the player is plausibly flying in freestyle, alt-tabbed, or
reading the arcade grid when it fires. Two friends inviting at once means one invite is destroyed
with nobody told.

### 3 · No "recently played with" · **high**

Nothing in the project implements it — no file, no service, no list. So a player who has a good
four-player match with a stranger has **no path to friending them**, which combined with (1) means
the social graph cannot grow from play at all. For a party game this is the natural place friend
requests come from.

### 4 · The join-failure message is best-effort · **medium**

`PartyInviteController`'s toast field is documented as *"Optional. Best-effort toast shown when a
join fails and the client bounces back to its own menu. **May be suppressed during the scene
reload.**"* — and `BounceToSoloMenuAsync` shows the notice *after* recovery, because `ToastService`
is a scene-bound MonoBehaviour.

So the most common failure an invite cohort will hit can present as: you press Accept, the screen
changes, and you are back in your own menu with no explanation. That reads as the game being
broken rather than as a join that did not land.

### 5 · Ready lights are a count, not an identity · **medium**

`ArcadeGameConfigureModal` tracks `_readyCount` and `HandleReadyCountChanged(readyCount, totalExpected)`.
`Docs/ArcadeLaunch/ARCHITECTURE.md` §5.1 states it directly: *"Ready lights are a COUNT, not an
identity."* The sync layer replicates **how many** confirmed, not **which**.

In a four-seat lobby where one person is holding up the launch, nobody can see who. §5.1 also notes
what per-seat identity would cost, so read it before designing — the fix may be a sync-layer change
rather than a UI one.

### 6 · Dropping out of a match · **medium**

Rejoin-in-progress and host migration are **deliberately cut** and stay cut — the checkpoint's
reasoning holds. But the *experience* of dropping is QoL and is in scope: does the player know what
happened, does the rest of the match continue cleanly, and is there a clear route back to their
party? Establish what happens today before proposing anything.

## What to do

Work top-down; (1) is worth more than (2)–(6) combined.

For **(1)**, the question to settle first is *by what*. By exact name is what the retired panel did
and what the facade's by-name method supports; from a recent-players list is better UX but depends
on (3). Adding to `FriendsListPanel` as a third section is the cheap shape and matches how that
panel is already built.

For **(2)**, decide between a longer dwell, a queue, and a persistent badge on the friends panel.
The last is the smallest change that makes an invite impossible to lose. Keep latest-wins for the
*popup* if you like — the defect is that there is nowhere else to look.

For **(3)**, the data already flows through the match: `GameDataSO.Players` carries every pilot and
`RoundStats` carries their identity. This is a local list with a cap, not a backend feature. Check
whether a cloud repository under `Assets/_Scripts/System/CloudData/Repositories/` is the right home
or whether local is enough.

For **(4)**, the honest fix is to make the notice survive the scene change rather than to make the
toast more reliable in place.

## Constraints

- **Single writer.** Only `FriendsServiceFacade` writes `FriendsDataSO`; only `HostConnectionService`
  writes `HostConnectionDataSO`. UI reads SOAP lists and events, and never calls the UGS SDK
  directly. Every mutating facade method calls `SyncAllRelationships()` afterwards — match that.
- **Friend requests and party invites are separate systems** and must stay separate: a friend
  request is a persistent UGS relationship, a party invite is an ephemeral lobby property.
  `Docs/PartySystem/UI.md` § "Friend requests vs. party invites" holds the distinction.
- **Do not touch the locked design** — EAGER per-user Relay stays. No lazy session creation.
- Keep the existing anti-spam cooldown shared by invite/cancel/kick; a new send-request button
  needs the same treatment.
- `.AsMainThread()` on every UGS await (`Docs/THREADING.md`), and no SOAP raise from an
  un-marshalled continuation.
- **Do not build host migration or rejoin-in-progress.**

## Definition of done

1. A player can send a friend request from inside the game, with the same cooldown discipline as
   the other social actions.
2. An invite that is not answered within three seconds is still findable.
3. A decision recorded on recently-played-with — built, or explicitly deferred with a reason.
4. A failed join always tells the player something, including across the scene reload.
5. Ready-light identity is either implemented or costed against §5.1 and deferred on purpose.
6. No change to the locked Relay design, no migration, no rejoin.
7. `Docs/PartySystem/UI.md` updated to match what the surface now does, and
   `Docs/STEAM_RELEASE_TASKS.md` R13 ticked.
