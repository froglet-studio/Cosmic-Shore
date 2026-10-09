# RelaySpike: Unity Relay from plain .NET (no Unity)

A spike, not a product. It answers one question: can a plain .NET 10 process use Unity Relay for
Prisma-to-Prisma play? (Yes, over UDP and over DTLS. See `/workspace/prisma-relay-spike-report.md`
on the box, or the PR description once there is one.)

The project is standalone on purpose. It is not in `Port/CosmicShore.slnx`, nothing in the
launcher, player or updater references it, and it touches nothing Unity reads.

## Run

```sh
cd Port/spikes/RelaySpike
dotnet run -- selftest                 # two clients in one process, plain UDP
dotnet run -- selftest --dtls          # same over DTLS (encrypted)
dotnet run -- selftest --region asia-south1 --count 300 --rate 30
dotnet run -- host [--dtls] [--seconds 120]          # prints a join code, echoes
dotnet run -- join CODE [--dtls] [--count 300]       # second process or machine
dotnet run -- regions
dotnet run -- dtls-probe               # which PSK cipher suites the Relay server accepts
```

`--project` defaults to the game's `cloudProjectId` (`ProjectSettings/ProjectSettings.asset`).
`--env` defaults to `development` (`ProjectSettings/Packages/com.unity.services.core/Settings.json`).
Each run signs in anonymous players. No secret or service account is needed, and tokens are
never printed or stored. Relay has no delete call, so allocations simply expire.

## Files

| File | What it is | Reusable as |
|---|---|---|
| `UgsClient.cs` | UGS REST: anonymous sign-in, Relay `/allocate`, `/joincode`, `/join`, `/regions` | the start of Prisma's UGS services adapter |
| `RelayProtocol.cs` | Relay message protocol codec (BIND with HMAC, PING, CONNECT_REQUEST, ACCEPTED, RELAY, DISCONNECT, CLOSE, ERROR). Span-based, allocation-free | as is |
| `DatagramChannel.cs` | `IDatagramChannel` and plain UDP | as is |
| `DtlsDatagramChannel.cs` | DTLS 1.2 PSK over UDP (BouncyCastle). PSK = allocation key, identity = allocation id string | as is |
| `RelayConnection.cs` | bind, keep-alive, connect, send and receive for one client | wrap it in an `INetTransport`, with reliability on top |
| `Program.cs` | test harness and RTT measurement | throw away |
