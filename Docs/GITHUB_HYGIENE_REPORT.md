# GitHub hygiene report (R15)

Executed 2026-10-10 against `Docs/prompts/GITHUB_HYGIENE_PROMPT.md`. Everything here was
measured on this branch; the numbers the prompt carried from 10 Sep 2026 are repeated only where
they were re-verified.

## 1. What landed (no history rewrite, nothing deleted from the FMOD project source)

| Step | Result |
|---|---|
| FMOD per-machine state untracked | `Cosmic Shore/.cache/` (279 files, 74 MB in the working tree) and `Cosmic Shore/.unsaved/` (2 files) removed from the index with `git rm --cached` and ignored in `.gitignore`. The files stay on disk on the machine that ran it. **On every other clone, pulling this change deletes them** (git removes a tracked file that was deleted upstream; `.gitignore` does not stop that): the next bank build re-encodes the cache once, and `.unsaved/` carried one real unsaved edit, recorded in §1.1 so it is not lost. `Cosmic Shore/Metadata/` (218 files, the project source), `Cosmic Shore/Assets/` (106 files, the source audio) and `Cosmic Shore/Build/` (4 files, the banks Unity loads through `FMODStudioSettings` `ImportType: 0`) stay tracked. |
| PR template | `.github/pull_request_template.md`, with the *Verification status* section the `/qa-backlog` scan reads, and a *Tool output* section for the `/ship` §2.5 gate. The scan first FINDS PRs by body search for `untested`, `"verify in editor"` and `"play-test"` (`.claude/skills/qa-backlog/SKILL.md` step 3), so the template's NOT-run line is worded `NOT run, untested; verify in editor: …` to carry two of them. |
| Secret scan | Every tracked text file was scanned for credential-shaped strings (PlayFab `DeveloperSecretKey`, UGS secret keys, AWS and GitHub token prefixes, Slack tokens, private-key blocks, numeric Steam app ids in VDF or env form). **One hit, not a secret:** `Tools/Analytics/export_cloud_save.py` names the environment variable `UGS_SECRET_KEY` in its usage text and reads it from the environment. No value is tracked. `ProjectSettings/ProjectSettings.asset` carries the UGS `cloudProjectId`, which is a public project identifier, not a credential. |

### 1.1 The one unsaved FMOD edit this takes out of git: `Squirel boost` (FMOD owner, before pulling)

When `.unsaved/` was untracked it held one event whose unsaved state differs from the saved
project: **`Squirel boost`** (`event:/SFX/Oneshots/Gameplay sfx/Squirel boost`, event
`{e5eaad0e-b631-4200-8e2f-2b745b9c78e2}`, played by `Squirrel.prefab` and `Thresher.prefab`). The
two copies differ in exactly two track volumes, and the shipped bank matches the UNSAVED copy:

| Track (`EventMixerGroup`) | Saved: `Metadata/Event/{e5eaad0e…}.xml` | Unsaved: `.unsaved/Metadata/Event/{e5eaad0e…}.xml` (no longer tracked) | Shipped: `Build/Desktop/SFX.bank` |
|---|---|---|---|
| Audio 1 `{a6975854-bd62-4aa5-8860-8b455a97fc6c}` | +1 dB | **+9 dB** | +9 dB |
| Audio 3 `{11eade32-7017-4b9e-b4bf-e69e5680d87e}` | −9 dB | **0 dB** (the volume property is absent, which is the default) | 0 dB |

- **The unsaved copy is the newer edit.** Both files were last written by `c13425ba5` (aradia1,
  2026-10-06, "drift changes"), which moved the previous unsaved values (+1 / −9) into `Metadata/`
  and left +9 / 0 in `.unsaved/`.
- **The game plays the unsaved values today.** In `SFX.bank` each mixer group's volume is a float 64
  bytes after the group's little-endian GUID (the offset was checked against a −4.5 dB group in the
  same bank). The bank on this branch reads 9.0 and 0.0; the bank before `c13425ba5` reads 1.0 and
  −9.0.
- **So the next bank build from a clone without the unsaved state silently changes the Squirrel's
  boost**: Audio 1 drops 8 dB and Audio 3 drops 9 dB, for a build made for ANY FMOD change, with
  nothing in that diff to say why.
- The removed file is still readable from history:
  `git show 001f75452^:"Cosmic Shore/.unsaved/Metadata/Event/{e5eaad0e-b631-4200-8e2f-2b745b9c78e2}.xml"`.

**What the FMOD owner (aradia1) does, once, ideally before pulling this change:**

1. Open `Cosmic Shore/Cosmic Shore.fspro` in FMOD Studio 2.02.
2. Open the event **Events ▸ SFX ▸ Oneshots ▸ Gameplay sfx ▸ Squirel boost** and read the volume of
   the tracks **Audio 1** and **Audio 3**.
3. Choose the intended values (the shipped bank has +9 dB / 0 dB, the saved project +1 dB / −9 dB),
   set them, and **File ▸ Save** until the title bar shows no `*`.
4. **File ▸ Build**, then commit only the `Cosmic Shore/Metadata/` and `Cosmic Shore/Build/`
   changes, never a `.cache/` or `.unsaved/` path.

If the pull happened first, FMOD Studio reopens on the saved +1 / −9; set the values from the
table by hand and continue from step 3. If the pull stops with *"Your local changes to the following
files would be overwritten"* or modify/delete conflicts on `Cosmic Shore/.cache/…`, run
`git rm -r --cached "Cosmic Shore/.cache" "Cosmic Shore/.unsaved"`, commit, and pull again (the
files on disk are kept). Never resolve such a conflict with "keep mine": that re-tracks the cache,
and `.gitignore` does not untrack a file once it has been added.

## 2. The decisions this report puts to a human

### 2.1 LFS for audio, models and banks: NOT done, and here is the hazard

The prompt asks for `*.wav`, `*.fbx` and `*.bank` to be LFS-tracked going forward. Two things
in the tree make that a decision rather than a chore:

- **Prisma loads straight from `Assets/`.** The .NET port (`Port/`) reads meshes, scenes and
  the FMOD banks from the repository checkout, and its launcher fetches a branch into its own
  workspace. `Port/tools/fetch_native.py` exists precisely because the FMOD runtime is already
  in LFS and a plain fetch leaves pointer files. Putting every `.fbx` and `.bank` behind LFS
  would turn every Prisma workspace that does not run `git lfs pull` into a silent
  no-models, no-audio build. Prisma's docs and launcher would need an LFS step first.
- **Every CI checkout but two runs with `lfs: false`** (`bleeding-edge-guard.yml` static job,
  `prisma-parity-ci.yml`, `prisma-ios.yml`, `unity-ci.yml` static job). The two Unity compile
  jobs use `lfs: true`. The static jobs are source-only and would be unaffected; the Prisma
  parity job reads scenes and would see pointers.

Also measured: the 47 `.wav` files under `Assets/_Audio/` carry Unity `.meta` guids but the
guid scan found no scene, prefab or SO referencing them (audio reaches the game through FMOD
banks), so the WAVs are FMOD source, duplicated 96-for-47 inside `Cosmic Shore/Assets/`.

**Recommendation:** extend LFS to `*.wav` only (nothing at runtime reads them), after the
Prisma launcher gains an `lfs pull` step; leave `.fbx` and `.bank` alone until then. Either way,
the attribute change only affects files added or modified after it lands; it reclaims nothing.

### 2.2 The history rewrite: the trade, unchanged

`.git` is dominated by the FMOD cache and the duplicated 36-55 MB WAVs stored as full blobs on
every revision. `git filter-repo` (or `git lfs migrate import`) would reclaim most of it and
**rewrite every commit hash**, which breaks every open PR (50 today), every local clone, and
every hash cited in `Docs/` (this repository cites hashes heavily). That is a coordinated
operation with a maintenance window and an announcement. Options:

- **Do it:** reclaim roughly 1 GB; everyone re-clones; cited hashes go stale.
- **Don't:** keep the history; §1 stops the growth; accept the clone cost.
- **Migrate to LFS retroactively:** the same rewrite, plus an LFS storage bill.

Not performed. Nothing on this branch depends on the answer.

### 2.3 Duplicated source audio

`Cosmic Shore/Assets/` (96 WAVs) duplicates `Assets/_Audio/` (47 WAVs). The FMOD project
references its own copy by relative path, so deleting the `Cosmic Shore/Assets/` copies would
break bank builds, and deleting the `Assets/_Audio/` copies is only safe because nothing in
Unity references them (measured above). A human who owns the FMOD project should decide which
copy is canonical; the prompt's constraint ("never remove FMOD project source") points at
keeping `Cosmic Shore/Assets/` and retiring `Assets/_Audio/`, which is a 47-file deletion with
no reference proof needed beyond the guid scan above.

### 2.4 CODEOWNERS: drafted, not added

A `CODEOWNERS` file that names a login without write access shows an error on GitHub, and with
*require review from code owners* enabled a wrong line blocks every merge into a locked system.
The handles below are the ones the commit history establishes; the mapping of system to owner is
a people decision. Add the file once someone confirms it:

```
# Locked systems (CLAUDE.md): a change here gets its owner by construction.
Assets/_Scripts/Controller/Environment/      @gradies
Docs/ECOSYSTEM*.md                            @gradies
Assets/_Scripts/Controller/Party/             @gradies @YsKhan61
Assets/_Scripts/Controller/Arcade/Scoring/    @gradies
Assets/_Graphics/Materials/Graphs/            @gradies
Assets/_Scripts/Utility/PrismCradle.cs        @gradies
.github/                                      @gradies
```

## 3. Branch triage: already in flight on PR #1030

The branch cleanup half of R15 is being done on
[froglet-studio/Cosmic-Shore#1030](https://github.com/froglet-studio/Cosmic-Shore/pull/1030)
(a Prisma BRANCHES page, a branch-cleanup workflow, an archive under `Docs/BranchArchive/` with
an `inactive-363.txt` list and the delete scripts). This report does not duplicate that list.
What it adds, measured today: **516 remote branches**, of which only **6** besides
`bleeding-edge` are ancestors of it (`claude/dreamy-ride-5cbinp`,
`claude/main-menu-online-request-59dqaj`, `grizzly-v2`, `overnight/balance`,
`overnight/hostile`, `prisma/changes`); the rest were squash-merged or never merged, which is
why "merged" cannot be read from `git branch --merged` here. By tip date: 113 touched in
October 2026, 53 in September, 57 in August, and 101 with a tip older than June 2026. **50 PRs
are open**, 31 of them against `development`, `master` or another feature branch with tips from
February to April 2026; a cleanup must exclude every one of those heads. No branch was deleted.

## 4. What is still open on R15

- The `Squirel boost` track volumes (§1.1): the FMOD owner saves the intended values and rebuilds
  the banks.
- The LFS decision (§2.1) and the history decision (§2.2) are the human's.
- `CODEOWNERS` (§2.4) once the handles are confirmed.
- Branch deletions ride #1030.
