# Playing Prisma over the internet with Unity Relay

Prisma can now host and join games through **Unity Relay**, the same internet relay service the
Unity build uses. Two people on different networks (home Wi-Fi, mobile hotspot, office) can play
together. Nobody has to open ports on their router. This page explains how to try it on Windows,
using the **Command Prompt**. You do not need Unity for any of it.

> Short version: one person runs `--relay-host`, reads the six-letter **join code** from the
> window title or the console, and sends it to the other person, who runs `--relay-join CODE`.

## 1. Before you start (one time)

1. Install the **.NET 10 SDK** from <https://dotnet.microsoft.com/download> (pick ".NET 10",
   "SDK", "Windows x64"). Then close and reopen the Command Prompt.
2. Check it works. Open **Command Prompt** (press the Windows key, type `cmd`, press Enter) and type:

   ```bat
   dotnet --version
   ```

   You should see a number starting with `10.`.
3. Go to the `Port` folder of your Cosmic Shore checkout. Use your own path in place of this one:

   ```bat
   cd /d C:\path\to\Cosmic-Shore\Port
   ```

4. Build everything once (it takes a minute or two the first time):

   ```bat
   dotnet build
   ```

   It should end with `Build succeeded`.

You need an internet connection. You do **not** need a Unity account, a password, or any secret
key. Prisma signs in to Unity Gaming Services as an *anonymous player* by itself.

## 2. Play: one host, one or more friends

**The host** (the person who starts the game) types:

```bat
dotnet run --project src\CosmicShore.Player -- --relay-host
```

After a few seconds the game shows a **join code**, for example `9BFFDN`:

- in the **window title**: `... HOST · Relay 9BFFDN (us-east1)`;
- in the Command Prompt: `[relay] JOIN CODE: 9BFFDN   (region us-east1, DTLS encrypted, ...)`.

Send that code to your friends (chat, text, anything).

**Each friend** types, using the host's code:

```bat
dotnet run --project src\CosmicShore.Player -- --relay-join 9BFFDN
```

When it works, the friend's console prints `[relay] connected to the host's allocation ...`, and
the window title shows `CLIENT · via Relay`.

Notes:

- Leave the host's game running while friends join. If the host closes the game, the code stops
  working. A new `--relay-host` gives a new code.
- Codes are **not** case sensitive.
- Both players must run builds made from the **same Cosmic Shore branch**. Relay only connects
  players of the same UGS project and environment (see §5).

### Inside a running game

If you started the player with `--relay` (or `--relay-host` / `--relay-join`), these console
commands also work. Send them through the player's control port (the Launcher and the MCP tools use
it), or script them at a frame with `--do FRAME:COMMAND`, for example `--do 600:relay`:

| Command | What it does |
|---|---|
| `relay` | Shows the status: signed-in player, region, join code and the ping to the Relay server |
| `relay host` | Restarts this game as a host behind a new Relay slot, and prints a new code |
| `relay join CODE` | Restarts this game as a client of the host behind `CODE` |

### Party play

With `--relay`, party and session play goes through Relay automatically. The game already asks
for a Relay network when it creates a party (`WithRelayNetwork()`). The host's session then
records its join code, and anyone who joins that session uses the code. Finding a party still uses
the local session folder (`COSMIC_SHORE_NET_DIR`), because there is no internet lobby yet. So
across the internet, use `--relay-host` / `--relay-join CODE` for now.

## 3. Check that Relay works on your machine (two minutes)

This test is **not** run inside Unity. It starts two small programs that act like a host and a
player, connects them through the real Relay service, and checks that the game's networking works
over it: an object spawning, messages (RPCs) both ways, and a synced value (a `NetworkVariable`).

Run it from the `Port` folder:

```bat
set COSMIC_SHORE_RELAY_LIVE=1
dotnet test tests\CosmicShore.Tests --filter "FullyQualifiedName~RelayLiveTests"
set COSMIC_SHORE_RELAY_LIVE=
```

You want to see `Passed!  - Failed: 0, Passed: 2`. It takes about 30 seconds. Without the first
line, the two live tests are **skipped**. That is normal, because the everyday test run never
touches the internet.

To see the numbers (ping, time to connect), run the two programs yourself, in **two** Command
Prompt windows:

```bat
rem Window 1 (host). It prints "JOINCODE ABC123".
dotnet run --project tests\CosmicShore.RelayProbe -- host

rem Window 2 (joiner). Use the code from window 1.
dotnet run --project tests\CosmicShore.RelayProbe -- join ABC123
```

Each prints a `RESULT {...}` line at the end. `"ok":true` means everything arrived. On the host,
`rpcRttP50Ms` is the typical round trip of a message through Relay. Add `--udp` to both windows to
test without encryption.

### The offline tests (no internet needed)

```bat
dotnet test tests\CosmicShore.Tests --filter "FullyQualifiedName~Relay|FullyQualifiedName~ReliableLink|FullyQualifiedName~UgsSession"
```

These check the Relay message format byte for byte, the reliability layer on a simulated bad line
(lost, duplicated and reordered packets), the splitting of big messages, the sign-in cache, and
the host/join wiring, all against a fake Relay server on your own machine.

## 4. Options

| Flag (player) | Same as setting | Default | What it does |
|---|---|---|---|
| `--relay` | `COSMIC_SHORE_NET_TRANSPORT=relay` | off | Use Relay for hosting/joining, without auto-starting anything |
| `--relay-host` | | | `--relay`, then host as soon as the menu is up and print the code |
| `--relay-join CODE` | | | `--relay`, then join the host behind `CODE` |
| `--relay-region R` | `COSMIC_SHORE_RELAY_REGION=R` | automatic (the closest region) | Force a region, e.g. `europe-west4`, `asia-south1` |
| `--relay-udp` | `COSMIC_SHORE_RELAY_DTLS=0` | encrypted (DTLS) | Plain UDP instead of DTLS. Both sides should match |
| | `COSMIC_SHORE_UGS_PROJECT` | the game's UGS project | Which UGS project to use |
| | `COSMIC_SHORE_UGS_ENVIRONMENT` | `development` | Which UGS environment to use |

To set a variable for one Command Prompt window: `set COSMIC_SHORE_RELAY_REGION=asia-south1`.

## 5. What it does behind the scenes

- **Sign-in.** Prisma signs in anonymously to UGS Authentication and saves the session token in
  `ugs-session.json` in the player's data folder (one per `COSMIC_SHORE_PROFILE`). The next run
  reuses the same anonymous player instead of creating a new one each time. The file holds no
  password. Delete it to become a new anonymous player.
- **Region.** It asks UGS for the list of Relay regions, measures each one with Unity's
  documented QoS ping (UDP), and picks the fastest.
- **Allocation.** The host reserves a Relay slot ("allocation") and gets a join code for it.
  Joiners turn the code into their own slot connected to the host's.
- **Data.** Game packets travel inside Relay's `RELAY` messages, encrypted with DTLS 1.2
  (`TLS_PSK_WITH_AES_128_GCM_SHA256`, the one suite Relay accepts) unless you pass `--relay-udp`.
  Each packet is at most 1178 bytes of game data, plus 38 bytes of Relay header and 37 of DTLS,
  well inside Relay's 1394-byte limit. Bigger messages are split and rebuilt automatically.
- **Reliability.** Relay only forwards packets. Prisma's own reliable-UDP layer (the same one
  the LAN UDP transport uses, `UdpTransport`) adds acks, resends, ordering, splitting, keepalive
  pings and timeouts on top. A silent peer is dropped after 10 seconds.
- **Not connected?** If Relay can't be reached when you host a party, the game logs a warning and
  stays on the local network, as before.

Code: `src/CosmicShore.Online/` (UGS client, Relay protocol, DTLS, the Relay link),
`src/CosmicShore.Engine/Networking/Wire/DatagramLink.cs` and `NetRelay.cs` (the engine side, with
no extra libraries). Design and test results: `docs/MULTIPLAYER.md` §6.7.

## 6. Party test harness over Relay (Linux / WSL / Git Bash)

The five-player scenario harness takes the transport from the environment, so this runs the
whole party suite through real Relay. Each pilot has its own profile, so each has its own
anonymous player:

```bash
COSMIC_SHORE_NET_TRANSPORT=relay bash Tools/Build/prisma_party_scenarios/run.sh
```

It needs a full checkout with the game's assets and takes 10-15 minutes. Discovery still uses
the shared session folder, and the game data goes through Relay.

## 7. Troubleshooting

| You see | What it means / what to do |
|---|---|
| `join code not found (code 15009)` | The code is wrong, or the host's game is closed or stuck. The host's slot disappears if the host does not connect to Relay within about 10 seconds. Ask the host for a fresh code |
| `[relay] ... failed: HTTP 401` / `403` | Sign-in was refused. Delete `ugs-session.json` in the player's data folder and try again |
| `no region answered a ping; Relay picks the region` | Your network blocks UDP port 7778 (QoS). Relay still works and picks a region for you, or set `--relay-region` |
| The joiner hangs on `connecting` | UDP to the Relay server is blocked (some office or school networks). Try another network or a phone hotspot |
| `...start with --relay...` when joining a party | That party is hosted through Relay. Start your player with `--relay` too |
