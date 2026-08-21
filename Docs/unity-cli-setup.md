# Unity CLI — Setup

Experimental, changes often — `unity --help` on your machine is authoritative. Don't copy commands from a teammate.

`com.unity.pipeline` is already in `Packages/manifest.json` — you get it on pull (first pull does one long resolve + reimport; expected). No CLI install needed unless you want the CLI; the package just sits in the project otherwise.

## First-time setup (per machine, Windows PowerShell)

```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
# reopen PowerShell, then:
unity doctor
```

> ⚠️ **Reopen PowerShell after the install** or `unity` won't be found — most common failure.

## Connect

1. Open Cosmic Shore in the Editor.
2. `cd` to the repo root (not your home directory).
3. `unity command` — if it lists commands, you're connected.

`unity command eval` needs a per-machine token: `unity command eval --help`. **Never paste the token into a chat, ticket, or commit.**

## Troubleshooting

- `unity` not found → you didn't reopen the terminal.
- `Project version: unknown` → you ran it outside the repo root; `cd` there first.

## What changes

Claude drives the open Editor directly — no alt-tabbing, no copy-pasting console errors; it can enter Play mode and verify its own work. Editing a `.cs` file still triggers a normal recompile + domain reload; eval only skips the reload for the eval snippet itself.
