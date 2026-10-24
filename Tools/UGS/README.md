# UGS server code (Cloud Code)

Server-side code the game calls through Unity Gaming Services. It runs on Unity's servers, not on a
player's device, so a modified game cannot change what it decides. Part of the fair-play work
(`Docs/AntiCheat/README.md`).

| File | What it does | Threat Board |
|---|---|---|
| `CloudCode/economy.js` | Owns the crystal wallet in the protected Cloud Save key `PLAYER_ECONOMY`: earn, spend and once-only claims, each checked against `RULES` at the top of the file | T3 |
| `CloudCode/economy.test.cjs` | Runs the real script against an in-memory Cloud Save: `node Tools/UGS/CloudCode/economy.test.cjs` | T3 |

## Switching on the server-owned wallet

The game ships with it **off** (`ServerEconomyGate.Enabled = false` in
`Assets/_Scripts/System/CloudData/ServerEconomy.cs`). While it is off, nothing calls Cloud Code and the wallet
works exactly as before. Turn it on in this order, development environment first:

1. **Run the tests:** `node Tools/UGS/CloudCode/economy.test.cjs` prints `11 passed`.
2. **Install and sign in to the UGS CLI** (needs dashboard access to project
   `3030fd69-28ab-433f-b4bd-22b9b93c5118`):
   ```
   npm install -g ugs
   ugs login
   ugs config set project-id 3030fd69-28ab-433f-b4bd-22b9b93c5118
   ugs config set environment-name development
   ```
3. **Deploy the script:** `ugs deploy Tools/UGS/CloudCode/economy.js`. The endpoint name is the file name,
   `economy`. Check it in the dashboard under Cloud Code > Scripts, and run it there with `{"action":"state"}`
   for a test player: expect `ok: true` and that player's balance.
4. **Switch the gate on** in a test build (`ServerEconomyGate.Enabled = true`) that signs into the same
   environment, and run the editor checks in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Server-owned wallet").
5. **Production:** `ugs config set environment-name production`, deploy again, then ship the build with the gate
   on. Debug crystals (the Toolbox crystal field) are refused in production by design.

No Access Control policy is needed for the wallet: players can never write a protected Cloud Save key, so the
game's old client-written copy in `PLAYER_PROFILE` is only a display cache the server ignores.

## What the wallet does and does not protect yet

- **Protected:** the crystal balance, lifetime totals and once-only reward ids (today's toy activity). A forged
  or repeated earn is refused, earning is limited per call, per 30 s and per day, and a spend needs the server
  balance. Each account's first call migrates its old balance once, capped at `RULES.migrationCap`.
- **Not yet:**
  - **Earn amounts are capped, not proven.** The server cannot see a match result until every player reports
    it (Threat Board T4), so a cheater can still claim the top placement reward every match, within the caps.
  - **Vessel and hangar unlock records** stay in client-written keys. The crystals for them are spent on the
    server, but an edited save could still show something as unlocked without paying.
  - **Episode tokens** are unchanged. They are bought with real money and need each store's receipt check,
    which waits on the DLC-vs-tokens decision (`Docs/Legal/DLC_VS_TOKENS_QUESTIONNAIRE.md`).
  - **Offline changes:** if the server cannot be reached, the local change stands until the next server answer,
    which replaces it. An offline match's crystals are lost on reconnect.
