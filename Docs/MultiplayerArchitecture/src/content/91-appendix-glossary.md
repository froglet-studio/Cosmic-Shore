<div class="sec-eyebrow">Appendix B</div>

# Glossary

| Term | Meaning |
|---|---|
| **UGS** | Unity Gaming Services — the cloud suite (Auth, Sessions, Lobby, Relay, Friends, …). |
| **Session (UGS)** | A managed group of players. With Relay it is a *party session*; without, a lobby-only *presence lobby*. |
| **Lobby** | The UGS service backing the presence session — player roster + per-player properties. |
| **Relay** | UGS service that allocates a relay server so NAT-bound clients connect without port-forwarding. |
| **Presence Lobby** | Lobby-only session every signed-in player joins for discovery and invite exchange (no Relay). |
| **Party Session** | Relay-backed session that carries gameplay networking for the people flying together. |
| **Netcode (NGO)** | Unity Netcode for GameObjects — the replication framework. |
| **NetworkManager** | The Netcode singleton driving connection, scene sync, and object spawning. |
| **NetworkBehaviour** | A component that can own `NetworkVariable`s and RPCs (e.g. `Player`, vessel status). |
| **NetworkVariable** | A replicated field with a defined writer (owner or server). |
| **ClientRpc / ServerRpc** | A remote call from server→clients / client→server. |
| **NetworkObject** | A spawned, replicated GameObject identified by `NetworkObjectId`. |
| **Host** | A player that is server **and** client simultaneously. |
| **Eager / "Always-InParty"** | Every player creates their own Relay party session on entering the menu. |
| **Presence (Friends)** | Rich status a friend sees — "In Menu", "In Party", "In Game" + scene/vessel/session. |
| **SOAP** | Scriptable Object Architecture Pattern — `ScriptableVariable` state + `ScriptableEvent` channels. |
| **Single-writer** | A piece of SOAP state has exactly one writer; everyone else reads. |
| **UniTask** | Allocation-light async/await for Unity (`com.cysharp.unitask`). |
| **`.AsMainThread()`** | The boundary helper that re-asserts Unity's main thread after a cross-thread await. |
| **MainThreadDispatcher** | Captures Unity's `SynchronizationContext` and switches onto it reliably. |
| **MPPM** | Multiplayer Play Mode — Unity feature running several virtual players in one editor. |
| **NetDiag** | The diagnostic one-liner appended to party/lobby/session catch blocks. |
| **CSDebug** | Project logger that strips from release builds and is runtime-muteable. |
| **Domain** | Team / affiliation identity (Jade, Ruby, Gold; Blue = neutral / no-team). |
| **Vessel** | A player- or AI-controlled ship; a `NetworkBehaviour` spawned by the server. |
| **DI (Reflex)** | Dependency injection; `AppManager` is the root installer. |
| **Lazy DI singleton** | A service constructed on first injection — possibly before UGS init completes. |
