<div class="sec-eyebrow">Part II · Foundations</div>

# Tech stack & the UGS surface

::: lead
The online stack rests on two pillars — Unity Netcode for GameObjects for replication, and Unity
Gaming Services for identity, discovery, and transport — glued together with UniTask for async and
Reflex for dependency injection.
:::

| Concern | Technology |
|---|---|
| Engine / pipeline | Unity 6 + URP 17.0.4 |
| Replication | Unity **Netcode for GameObjects 2.5.0** |
| Sessions / Lobby / Relay / Friends / Auth | **Unity Gaming Services** (Multiplayer SDK) |
| Async | **UniTask** (`com.cysharp.unitask`) — with the project's `.AsMainThread()` boundary helper |
| Dependency injection | **Reflex 14.1.0** — `AppManager` is the root installer |
| Cross-system state | **SOAP** (Obvious.Soap) — `ScriptableVariable` / `ScriptableEvent` |
| Backend | PlayFab, Firebase, UGS (Analytics, CloudSave, Leaderboards) |

## The UGS services, in depth

**Authentication.** Anonymous sign-in via `AuthenticationService`, producing a stable `PlayerId`.
A single facade (`AuthenticationServiceFacade`) owns it and writes the result into a SOAP
`AuthenticationData` variable. Cached sessions are restored on relaunch. Under Multiplayer Play Mode,
the facade switches to a per-instance tagged profile so each virtual player has a distinct identity.

**Multiplayer Sessions.** The unified `IMultiplayerService` is the factory for *both* session kinds.
A `SessionOptions` without Relay yields the lobby-only presence session; `SessionOptions.WithRelayNetwork()`
yields a Relay-backed party session that automatically configures the Netcode transport.

**Lobby.** Backs the presence session: a roster of players, each with writable per-player properties.
Those properties are the entire invite channel — no custom backend required.

**Relay.** Allocates a relay server and join code so NAT-bound clients can connect. The party session
attaches Relay so the host's `NetworkManager` and joining clients communicate over UTP/DTLS without
port-forwarding.

**Friends.** Persistent relationships (`AddFriendByNameAsync`, `AddFriendAsync`), request management,
and rich presence — a `FriendPresenceActivity` payload carrying scene, vessel class, and party
session id so friends can see exactly what you're doing.

::: insight One SDK, two session shapes
The cleanest mental model: there is one session API, and the *presence of Relay* is what makes a
session a "party" rather than a "lobby". Everything else — properties, player lists, refresh — is
shared machinery. That symmetry is why the same `IMultiplayerService` live-instance pattern and the
same `.AsMainThread()` discipline apply to both layers.
:::
