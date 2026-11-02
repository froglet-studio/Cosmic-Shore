---
name: androidbuild
description: Build a fresh Android development APK from the latest bleeding-edge (or a named branch), upload it to Google Drive, get a share link, and post that link to the Froglet Games #builds Discord channel. Use on "/androidbuild", "/androidbuild <branch>", "make a new android build", "new drive link for the build", "post a build to discord".
---

# `/androidbuild [branch]` — build, Drive link, Discord post

One command for the whole "new build for the team" loop:

1. Fetch the latest `origin/<branch>` (default `bleeding-edge`).
2. Build an Android **development** APK with Unity batchmode in a separate build worktree.
3. Copy it to the shared Google Drive folder and get a share link.
4. Post the link to the Froglet Games **#builds** Discord channel.
5. Tell the person the link, commit, size and build time.

The argument is the branch to build. With none, build `bleeding-edge`. If the person adds
"diagnostics off", pass `-DiagnosticsOff`.

## Where it runs

The build needs a Windows PC with Unity and the Android modules. It cannot run in a cloud
sandbox. If you are in a cloud or project session, run this on the person's PC through Remote
Control (or a Claude Code session on that PC) and hand it this file. Garrett's PC (**Gbox**) is
already set up with every default below.

## Step 1–3: build and copy (one script)

From the repo root on the PC, in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .claude/skills/androidbuild/androidbuild.ps1 -Branch bleeding-edge
```

- It only **fetches** in the person's clone. Their checkout and files are never touched. The build
  happens in a detached worktree (`C:\CosmicShoreBuild\worktree` by default) that keeps its
  `Library`, so builds after the first are incremental.
- If the build worktree has uncommitted changes (another session sometimes leaves some), it
  **stashes** them with `git stash -u` instead of deleting them, and prints a `NOTE` line. Pass
  that on to the person.
- It reads the Unity version from `ProjectSettings/ProjectVersion.txt` and finds the editor under
  `C:\Unity\<ver>`, `C:\Program Files\Unity\Hub\Editor\<ver>` or `D:\Unity\<ver>`.
- A build takes 10–35 minutes (the IL2CPP compile is the slow part, slower when the PC is busy).
  Run the script in the background and wait for it to exit. Do not send the person "still
  building" messages; if they ask, say which step it is on (`clang++` processes running means
  the IL2CPP compile).
- The last line is the result. Read only that line, not the log:
  - `RESULT <shorthash> | <apk name> | <size>MB | <minutes>min | <commit subject>` — success.
  - `FAIL <reason>` — stop and tell the person the reason and the lines printed above it. Do not
    post anything to Discord.
- The APK lands in Drive as `My Drive → Cosmic Shore Builds → CosmicShore-<branch>-<shorthash>-dev.apk`,
  with its `…-build.log` next to it.

## Step 3: the Drive share link

Google Drive for desktop uploads the copied file in the background; the ~300 MB APK can take a few
minutes to appear online.

- **With the Google Drive connector:** search once by exact name, with content snippets off, e.g.
  `title = '<apk name>'` (pass `excludeContentSnippets: true` if the tool has it; the build.log
  snippets are a big token sink). If it is not there yet, wait a minute or two and retry. Make sure
  it is shared so the team can open it: anyone at `froglet.studio` with the link, the same as
  earlier builds (the Cosmic Shore Builds folder normally already gives this). Use the
  `…/file/d/<id>/view?usp=sharing` link.
- **Without the connector:** ask the person to right-click the file in the Drive folder, choose
  Share → Copy link, and paste it back. Then carry on.

## Step 4: post to Discord #builds

```powershell
powershell -ExecutionPolicy Bypass -File .claude/skills/androidbuild/androidbuild.ps1 -Stage post `
  -Branch bleeding-edge -Link "<drive link>" -ApkName "<apk name>" -SizeMB <size> -Subject "<commit subject>"
```

This posts through the #builds channel webhook in the `CS_DISCORD_BUILDS_WEBHOOK` environment
variable and prints `POSTED`. If the variable is not set but a Discord connector/MCP tool that can
post to the Froglet Games server is available, post the same message there instead. If neither
exists, say so in one line and give the person the message to paste. Never write the webhook URL
into the repo, a commit, a log, or a chat message.

## Step 5: report

Reply with the link as **plain text** (markdown links are not always tappable on mobile), the Drive
path, the short hash and commit subject, size, build time, and that it was posted in #builds. Add
any `NOTE` line the script printed.

## Setting up another PC (one time)

| Need | Why | How |
|---|---|---|
| Windows, Unity `<ProjectVersion>` with **Android Build Support** (OpenJDK + SDK/NDK) | The build | Unity Hub. If Unity is not in a default spot, set `CS_UNITY_EXE`. |
| A clone of this repo with GitHub access | Fetching the branch | The script uses the repo it lives in; override with `CS_REPO_DIR`. |
| ~40 GB free on the build drive | The build worktree + its `Library` | Override the location with `CS_BUILD_WORKTREE` and `CS_BUILD_OUT`. |
| **Google Drive for desktop**, signed in with a `froglet.studio` account that can write to **Cosmic Shore Builds** | Uploading | Default path `G:\My Drive\Cosmic Shore Builds`; override with `CS_BUILD_DRIVE_DIR` (e.g. a shared-drive folder). Turn on "Open Google Drive on system startup": Drive stopping is the most common failure. |
| Google Drive connector in Claude (optional) | Fetching the share link automatically | claude.ai connector settings. Without it the person copies the link by hand. |
| `CS_DISCORD_BUILDS_WEBHOOK` user environment variable | Posting to #builds | A server admin creates it in Discord: #builds → Edit Channel → Integrations → Webhooks → New Webhook → Copy URL. Then `setx CS_DISCORD_BUILDS_WEBHOOK "<url>"` and restart the terminal / Claude. Treat the URL as a secret. |

No signing keystore is needed: development APKs are signed with Unity's debug key. A phone holding
the Play-store install must uninstall it before installing this APK.
