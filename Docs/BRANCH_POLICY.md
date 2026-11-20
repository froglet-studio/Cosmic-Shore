# Branch and repo-size policy

Shombith, 2026-11-20 ("Github & Security Protocols"). The repository is public and about 4.2 GB. Most of that is
history: build zips, WAV masters, editor caches and tool output that were committed once and stay in every clone
after they are deleted. There were 690 branches, 201 of them already merged and 387 that never had a pull request.
These rules keep it from growing back.

## Large files

- **Nothing over 10 MB goes into git outside LFS.** The `Large files` check (`.github/workflows/large-files.yml`,
  `Tools/Build/check_large_files.py`) fails a pull request that adds one. Renames and edits of files already in the
  repo are not checked.
- In order of preference, a large file is:
  1. **kept out of git**: build output, captures, caches and tool results go in `.gitignore`;
  2. **stored in LFS**: `git lfs track "<path>"`, commit `.gitattributes`, re-add the file (run `git lfs install`
     once per machine);
  3. **a one-off exception** the repo owner agrees to, listed by exact path in `.github/large-files-allowlist.txt`.
- Player builds and store packages (`*.apk`, `*.aab`, `*.ipa`) never go in git. Use GitHub Releases or Drive.
- `Port/dist/` holds only `README.txt` and `Amoebius-Windows.zip`; anything else there is ignored.
- FMOD Studio's caches (`Cosmic Shore/.cache/`, `Cosmic Shore/.unsaved/`) are ignored; FMOD Studio rebuilds them.
  The banks Unity loads (`Cosmic Shore/Build/`) stay tracked.
- Profiler captures (`*.pdata`, `*.snap`, `ProfileAnalyzer/`) are ignored.

Not changed yet, on purpose: `*.wav` and `*.bank` are not LFS-tracked, because 294 WAVs and 3 banks are already in
git, and an LFS rule would show them all as modified for everyone with LFS installed. Moving them is part of the
history rewrite decision below.

## Branches

- **Names**: `claude/<topic>` for session branches, `<person>/<topic>` for a person's own, `build/<platform>` for
  build branches. Long-lived branches are `bleeding-edge` (default), `Ys-bleeding-edge`, `development`, `master` and
  `build/*`.
- **Merged branches are deleted** automatically when their pull request merges (the repo setting "Automatically
  delete head branches" is on). Delete one by hand if it was merged some other way.
- **An unmerged branch with no pull request and no push for 30 days** is backed up and deleted in the next cleanup.
  The backup is a git bundle kept off GitHub, so the space is really freed; ask for a branch back and it is restored.
  A tag would keep the commits in the repo and free nothing.
- **A branch pushed in the last 7 days is never deleted** in a cleanup (it is an occupied swim lane).
- `master` and `development` have been frozen since August and September. Open pull requests against
  `bleeding-edge`.

## Pull requests

- A pull request with no activity for 30 days gets a comment asking its author whether it is still wanted; one
  nobody answers in 14 more days is closed with a note on how to reopen it. Its branch then falls under the branch
  rule above.

## Cleanups

Every cleanup (branch deletion, closing pull requests, a history rewrite) needs the repo owner's explicit yes on the
exact list first. A history rewrite (removing old build zips, WAV masters and retired folders from history, about
2 to 2.5 GB more) forces everyone to re-clone and redo open pull requests; it is a separate decision with a freeze day.
