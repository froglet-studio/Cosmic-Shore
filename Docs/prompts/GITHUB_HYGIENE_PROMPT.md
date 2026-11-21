# Prompt — clean up the GitHub repository

Paste everything below into a fresh session.

---

The repository has a **1.4 GB `.git`** against a 2.0 GB working tree, and the reason is not the
game. It is an entire FMOD Studio project directory committed whole — including its cache and its
unsaved working state — plus a set of 40–55 MB WAV files stored as raw git blobs because LFS covers
almost nothing.

Every clone, every CI checkout and every new contributor pays that cost, and it grows with each
audio revision.

Read `Docs/BRANCHING_AND_RELEASE.md` first — it holds the branch model this must not break.

## Measured 10 Sep 2026 — re-verify before acting

### The nested FMOD project: 609 tracked files

`Cosmic Shore/` is the FMOD Studio project, committed into the repository root. Tracked
subdirectories include:

```
Cosmic Shore/.cache/fsbcache/Desktop     <- build cache
Cosmic Shore/.unsaved/Metadata/Group     <- FMOD Studio UNSAVED working state
Cosmic Shore/Build/Desktop               <- built .bank files (SFX.bank, 37.7 MB)
Cosmic Shore/Assets                      <- source WAVs, DUPLICATED with Assets/_Audio/Music
Cosmic Shore/Metadata/...                <- the actual FMOD project source
```

Three different things are tangled here and they want three different answers:

- **`.cache/` and `.unsaved/` should never be tracked.** They are per-machine working state; FMOD
  Studio's own `.gitignore` guidance excludes them.
- **`Build/Desktop/*.bank`** is a build artefact — but Unity loads banks at runtime, so it may
  legitimately need to be committed. **Find out how the build consumes them before removing
  anything.** If the banks are built by a human in FMOD Studio and committed, they stay.
- **`Cosmic Shore/Assets/*.wav`** are the same source files as `Assets/_Audio/Music/*.wav`. Measured
  duplicates include `cosmic shore chill time 3.wav` (54.8 MB **twice**), `cosmic shore 3
  chalres.wav` (50.5 MB twice), `COSMIC SHORE 2 TUNE.wav` (40.2 MB twice), `cosmic shore 3 looped
  1.wav` (36.4 MB twice). That is roughly 180 MB of pure duplication, and it is *only* the top of
  the list.

### LFS covers two patterns and neither is audio

`.gitattributes` has exactly two LFS rules — `*.so` and `*.bundle`. Confirmed by measurement:

```
$ git check-attr filter -- "Assets/_Audio/Music/cosmic shore chill time 3.wav"
  filter: unspecified
```

So every large WAV, every FBX (`SparrowModel4.fbx`, 30 MB) and every `.bank` is a full blob in
history, re-stored in full on every revision.

The rest of `.gitattributes` is sound and must be preserved — the FMOD native-library `binary`
rules exist because `* text=auto` was corrupting `.dll`/`.dylib`/`.bundle` payloads on Windows
checkout and triggering Unity's "Repair FMOD Libraries" prompt. Do not simplify that block.

### `.github/` holds only `workflows/`

No pull-request template, no issue templates, no `CODEOWNERS`. CLAUDE.md's own PR instructions tell
an author to look for `.github/pull_request_template.md` and mirror its headings — there is nothing
to mirror, so every PR body is improvised.

## What to do

**1 · Stop the bleeding first.** Before any history work: add the FMOD cache and unsaved state to
`.gitignore`, `git rm --cached` them, and extend `.gitattributes` so `*.wav`, `*.fbx`, `*.bank` and
the other large binary types are LFS-tracked **going forward**. This is a normal commit and is worth
doing even if nothing else on this list happens.

**2 · Decide the history question deliberately, and get a human to agree.** Purging the duplicated
WAVs and the FMOD cache from history with `git filter-repo` would reclaim most of the 1.4 GB — and
it **rewrites every commit hash**, which breaks every open PR, every local clone, and every hash
cited in the documentation (this repository cites commit hashes heavily — `Docs/` and the audit both
do). That is a coordinated operation with a maintenance window, not a background cleanup.

Present the trade honestly and let a human choose:

- **Do it** — reclaim ~1 GB, force every contributor to re-clone, invalidate cited hashes.
- **Don't** — keep the history, stop the growth with step 1, accept the clone cost.
- **Migrate to LFS retroactively** — `git lfs migrate import` is also a rewrite, with the same
  consequences and an added LFS storage bill.

Do not perform a rewrite in this branch on your own initiative.

**3 · Branch and PR hygiene.** The repository is at PR #855. Enumerate remote branches and
categorise: merged-and-stale, abandoned, and live. Propose deletions in a list a human ticks —
never delete branches unilaterally, and never delete a branch that is the head of an open PR.
Check for branch-protection on `bleeding-edge` and the `build/*` branches while you are there.

**4 · The missing `.github` furniture.** Add a pull-request template whose sections match how this
project actually reviews — a *Verification status* section is load-bearing here, because
`Docs/QA/`'s scan reads exactly that to find untested work. Consider `CODEOWNERS` for the locked
systems (ecology, party/presence, threading, scoring, prism animation) so a change to one gets the
right reviewer by construction.

**5 · Secret scan.** Confirm no credential, Steam app id, UGS key or PlayFab secret is tracked.
Report anything found **by location, never by value**.

## Constraints

- **No history rewrite without explicit human sign-off**, and if it happens, it is its own
  operation with a window and an announcement.
- **Do not remove `Cosmic Shore/Metadata/`** — that is the FMOD project *source*, and losing it
  loses the ability to rebuild banks. Only cache, unsaved state and duplicated sources are
  candidates.
- **Determine how banks reach the build before touching `Build/Desktop/`.** `Docs/AudioSystem/FMOD_AUDIT.md`
  and `Docs/BUILD_AND_DELIVERY.md` are the places to look. A missing bank is a silent game with no
  error.
- Preserve the FMOD `binary` rules in `.gitattributes` exactly.
- Deleting a branch is not reversible from the GitHub UI after the ref expires. Propose, do not
  execute.

## Definition of done

1. `.gitignore` and `.gitattributes` updated so the repository stops growing this way, with the
   FMOD binary rules intact and the LFS patterns covering audio, models and banks.
2. The duplicated-audio and cache situation is quantified exactly, with the history-rewrite trade
   written up and put to a human as a decision.
3. A branch cleanup list a human can tick, with open-PR heads excluded.
4. A PR template exists and carries a *Verification status* section.
5. A secret scan result, reported by location only.
6. Nothing destructive done unilaterally — no history rewrite, no branch deletions, no removal of
   FMOD project source.
7. `Docs/STEAM_RELEASE_TASKS.md` R15 is ticked.
