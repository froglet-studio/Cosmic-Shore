<div class="sec-eyebrow">Part II · Foundations</div>

# Authentication & bootstrap

Nothing networked happens until authentication completes. The whole online stack is choreographed off
a single SOAP event — `OnSignedIn` — so the auth flow is the spine the rest hangs from.

::: figure auth-bootstrap
Sign-in fans out: CloudSave profile load, Netcode host start, presence-lobby join + eager party
session, and Friends initialisation — all triggered by the one `OnSignedIn` event.
:::

## Single-writer authentication

`AuthenticationServiceFacade` is the **sole writer** of `AuthenticationData` (a SOAP variable). It owns
UGS initialisation, anonymous sign-in, cached-session restore, event wiring, and sign-out. Every other
system — scene controllers, `PlayerDataService`, `MultiplayerSetup`, `HostConnectionService`,
`FriendsServiceFacade` — *reads* auth state or subscribes to its events. None of them mutate it.

```csharp
// The whole stack keys off one event, raised once, on the main thread:
AuthenticationData.OnSignedIn
   → PlayerDataService.HandleSignedIn()          // CloudSave profile load/merge
   → MultiplayerSetup.EnsureHostStartedAsync()   // nm.StartHost() exactly once
   → HostConnectionService.HandleSignedInEvent() // join presence lobby + EnsurePartySession
   → FriendsInitializer.HandleSignedInEvent()    // Friends init + presence "In Menu"
```

## Bootstrap order

The Bootstrap scene's `AppManager` (`[DefaultExecutionOrder(-100)]`, a Reflex `IInstaller`) registers
every persistent service and SO asset, then starts authentication fire-and-forget. Because lazy DI
singletons are constructed during this registration — **before** `UnityServices.InitializeAsync()`
returns — services must never cache a UGS `*.Instance` at construction time (see the live-instance
pattern in the decisions ledger).

::: pitfall Idempotency across the boot race
`HostConnectionService` lives in Bootstrap (`DontDestroyOnLoad`) but auth completes later, in the
Authentication scene. If auth completes between the service's `Awake` and `Start`, its `Start` calls
`HandleSignedInEvent()` directly — which is **idempotent** (it sees the in-progress/initialised state
and no-ops). Designing the sign-in handler to be safe to call twice is what makes the boot race a
non-event.
:::

The same `OnSignedIn` fan-out runs identically for the host and for a client that later joins a party —
there is no special boot path for "joiner", which keeps the number of distinct startup sequences to
exactly one.
