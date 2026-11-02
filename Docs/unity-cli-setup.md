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

## Smart merge for scenes, prefabs and assets (per machine, one time)

`.gitattributes` marks `*.unity *.prefab *.asset *.mat *.controller *.anim` with
`merge=unityyamlmerge`. Git only uses that driver once **your local git config** says where Unity's
`UnityYAMLMerge` lives; until then it silently falls back to a plain text merge. Run this once, from
the repo root, with the path for the editor version in `ProjectSettings/ProjectVersion.txt`
(6000.3.17f1 on 2026-10-29):

```powershell
# Windows (PowerShell)
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

```bash
# macOS
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

- Check it: `git config --get merge.unityyamlmerge.driver` prints the line, and
  `git check-attr merge -- Assets/_Scenes/Menu_Main.unity` prints `merge: unityyamlmerge`.
- Use plain `git config` (this repo only), not `--global`. Never commit the path: it is per machine.
- When the editor version changes, re-run the `driver` line with the new folder.
- If the tool cannot merge a file it leaves the conflict for you, the same as before.
