---
name: verify-unity
description: Verify a C# change in the real Unity Editor via the Unity CLI before committing — the LOCKED gate from CLAUDE.md ("any C# change must pass /verify-unity before it is committed"). Use before committing any C# change, when asked to verify/compile/test a change in Unity, or when deciding whether editor verification is possible in this session. If the CLI is unavailable (cloud/web session, no open Editor), runs the honest fallback instead of claiming verification.
---

# /verify-unity — editor-verify a C# change before commit

The Unity CLI (`unity` binary + the `com.unity.pipeline` package) drives the **open Unity
Editor on the machine this session runs on**. It is experimental and changes often:
**`unity --help` on the installed version is authoritative** — never assume a subcommand or
flag from memory, from documentation, or from another machine. Setup: `Docs/unity-cli-setup.md`.

## 1. Decide whether the CLI can work HERE

Run `unity --help` (from the **repo root**, never `~` — outside the repo the CLI misidentifies
the project and reports "Project version: unknown").

- **Binary missing, or this is a cloud/web/remote session:** the CLI **cannot** work — there is
  no Unity Editor in the container and nothing to connect to. Do not retry, do not simulate.
  Go to §4 (fallback).
- **Binary present:** continue.

## 2. Confirm the connection to the open Editor

From the repo root, run `unity command`. If it lists commands, you are connected to the open
Editor. If not:

- Ask the human to open Cosmic Shore in the Unity Editor (the CLI drives an OPEN editor).
- `unity doctor` diagnoses environment, credential, and config problems.
- `unity command eval` needs a per-machine security token — point the human at
  `unity command eval --help`. **Never paste, log, or commit that token.**

## 3. Verify the change

Discover what the installed version offers from `unity --help` and the `unity command` listing,
then use it to prove the change is real, minimally:

1. **The Editor compiles and loads the change** — a script refresh/compile with zero new errors
   in the console. This is the bar the LOCKED rule sets; a green edit-mode suite or a clean
   mental compile does not substitute for it.
2. Where the change is behavioral and a repo `[CliCommand]` wrapper or Play-mode check covers
   it, run that too.

**Pass** = the Editor actually compiled and loaded the change with no new errors. Report the
result honestly — never claim a verification that did not run. Then commit.

## 4. Fallback when the CLI is unavailable (the honest path)

Do **all** of the following — this is the documented substitute, not a skipped gate:

1. Run the static gates that exist in every session:
   - `python3 Tools/Build/check_conditional_compilation.py`
   - A real Roslyn compile of the changed C# against stubbed Unity APIs where practical
     (see the `asset-surgery` skill §4 — dotnet-sdk installs in the container).
2. File the change in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (one `### ` section, newest first:
   what landed, concrete verify-in-editor steps, status 🔴). Do not invent a new checklist —
   this file is the one place editor-side risk is recorded.
3. Say explicitly in the commit message and/or PR that editor verification did not run in this
   session and `/verify-unity` should be run on pull.
