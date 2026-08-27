# Offline Mode — Short Summary

One-page version. Full detail: **`Docs/OFFLINE_MODE.md`**.

---

## The problem

**The game could not be launched without internet at all.** Not degraded — stuck on the boot
splash forever.

Two root causes:

1. **Nothing in the project ever called `NetworkManager.StartHost()`.** The Netcode host only
   ever came up as a *side effect* of the UGS SDK creating a Relay-backed session. No internet →
   no Relay → no host. And no host means no vessel, because every vessel in the game spawns
   through `Player.OnNetworkSpawn`.

2. **The boot flow waited on that host with no timeout.**
   `AuthenticationSceneController` tried Relay 3 × 15 s, then showed a "tap retry" button and
   waited *forever*. `Menu_Main` is loaded through `NetworkManager.SceneManager`, so without a
   host the menu was literally unreachable.

Underneath that, **no player data survived offline**: all ten cloud repositories only loaded on
a successful sign-in, with no local copy. An offline player got a fresh random `Pilot####`
identity, no unlocked vessels, no episodes, no progression — and lost anything earned at quit.

---

## What we did

**Offline is a local host, not a "no netcode" mode.** When UGS/Relay is unreachable, the game now
starts `NetworkManager` as an ordinary host on `127.0.0.1`. Host == server == client on one
machine, so the whole spawn chain, scene management, RPCs and AI backfill run *identically* to a
solo online session. Every AI-backfilled minigame, freestyle and the toys just work — with zero
offline branches in gameplay code.

| Area | What changed |
|---|---|
| **Boot** | `OfflineModeService` starts the loopback host; `AuthenticationSceneController` falls into it instead of waiting forever (and skips the Relay attempts entirely when the device is plainly unreachable). |
| **Player data** | `LocalCloudDataCache` mirrors every cloud key to disk on load and save. Offline, each repository restores its last-known-good snapshot — **display name, unlocked vessels, unlocked episodes, game/mode progression, settings, loadout**. Cloud always wins when it answers; offline progress saves locally and flushes up on reconnect. |
| **Online plumbing** | `GameDataSO.IsOfflineSession` stands matchmaking (`MultiplayerSetup`) and party/Relay creation (`HostConnectionService`) down, so a late Relay success can't yank the local host out from under a live game. |
| **Online-only UI** | `OfflineUIGate` — one reusable inspector-wired component that hides/disables online-only UI and reveals an offline notice. Backed by hard service-level guards (invites, leaderboard writes, purchases) so an un-wired screen still can't fire a doomed request. |
| **Reconnect** | `ReconnectService` + `ReconnectButton` — one tap in the menu re-runs the boot chain in place (tear down host → clear the flag → re-arm auth → Authentication scene). **No app restart.** A failed retry falls back to offline again rather than stranding the player. |
| **Bug fixed on the way** | `ApplicationStateMachine` never subscribed `OnNetworkFound`, so a Wi-Fi blip parked the app in `Disconnected` permanently. It now resumes the state it interrupted. |

---

## Still open

- Scene wiring: `OfflineUIGate` / `ReconnectButton` are built but must be placed on the
  party, friends, leaderboard and store panels in `Menu_Main`.
- A mid-game transport failure still ends a *solo* game (rare on a live Relay solo session).
- A first-*ever* launch with no internet has no cached identity yet, so it plays on a fresh
  local profile that does not merge into an account created later.

**Not editor-verified** — see `Docs/UNITY_VERIFICATION_CHECKLIST.md` for the play-test steps.
