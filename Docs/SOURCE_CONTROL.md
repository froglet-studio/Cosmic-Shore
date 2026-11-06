# Source Control — GitHub Desktop inside Unity, with the /ship checks

**FrogletTools > Build > Source Control** (`FrogletSourceControlWindow`). Built 2026-11-06 for Shombith (tooling) after
Charles's Thresher commit (PR #1766): a collaborator who works only in Unity and GitHub Desktop had no way to run
the checks a Claude session runs before a push, and four of that commit's problems were invisible to Desktop.
GitHub Desktop is still how a person **clones** the project the first time; everything after that can happen here.

## 1. What it does (v1)

| Area | What you can do |
|---|---|
| Top bar | Current branch, ↓behind ↑ahead of GitHub, how many pull requests behind bleeding-edge. **Refresh**, **Fetch** (also automatic every 5 minutes while the window is open; `EditorPrefs` `Froglet.SourceControl.FetchMinutes`, 0 = off), **Pull**, **Push** / **Publish branch**, **Update from bleeding-edge**, **Open pull request**. |
| Changes | Every changed file with a tick box (an asset and its `.meta` are one row), its diff, **Discard…**, and a commit box (summary + description). **Commit** saves open scenes and assets first, runs the push checks on the ticked files, then commits only those. |
| History | The last 100 commits; unpushed ones are marked; click one for its files. |
| Branches | **New branch** from the latest bleeding-edge (name it `yourname/what-it-does`), and **Switch** to any local branch or one that only exists on GitHub. |
| Checks | **Run push checks**, **Run /ship gates** (about 2 minutes), and **Ask Claude to ship this** (copies a `/ship` request for the branch and opens Claude Code). |

**Open pull request** opens GitHub's own pull request page for the branch into bleeding-edge, filled from the repo's
template, in your browser (no GitHub sign-in inside Unity in v1). Merging stays on GitHub.

## 2. The checks, and why each exists

The rules live in python, not in the window, so they are self-tested and a Claude session or a terminal gets the
same answer: `Tools/Build/push_checks.py` (`--self-test`; `--json` is what the window reads). They run before every
commit (on the ticked files) and every push (on the unpushed commits). A **stop** (block) cancels; a **warning** asks
"Go on anyway / Cancel / Show me"; a finding marked discardable gets a **Discard these** button.

| Check | Severity | Why (the push that taught it) |
|---|---|---|
| protected-branch | stop | You are on bleeding-edge / main / development: make a branch. |
| behind-base | warning at 10+ PRs | PR #1766 was 35 merges behind: 563 files showed for 39 real changes. |
| fmod-event | warning | A prefab pointed at an FMOD event that existed only in FMOD's `.unsaved` folder; the next bank build loses it. Also an event the FMOD project does not have at all. |
| junk | warning, discard | `.DS_Store`, `steam_appid.txt` (Steamworks.NET writes `480` on every editor start), FMOD's `.unsaved` / `.cache`, `Library/`, `Temp/`, `Logs/`, `UserSettings/`, merge leftovers. |
| resave-only | warning (info if nothing in flight), discard | A prefab / scene / asset that changed ONLY the way a Unity re-save changes one (re-wrapped lines, re-ordered objects and overrides, recomputed nested-prefab ids, newly written default fields, URP's material version stamp). Names the branches in flight that touch it (`who_works_where.py`): PR #1766's re-saves made two UI branches conflict on 9-22k lines. |
| build-settings | warning, discard | A scene added to or removed from the build (PR #1766 added an archived mode's scene). |
| define-symbols | warning, discard | A scripting define added or removed on a platform (PR #1766 added `STEAMWORKS_NET`). |
| packages | warning | A package added, removed or re-versioned (CLAUDE.md: flag new dependencies first). |
| dynamic-font | warning, discard | TextMeshPro filled a dynamic font while you played (8 KB → 5.4 MB in PR #1766). |
| meta-pair | stop | A new asset without its `.meta` (its GUID changes on every other machine), or a `.meta` without its asset. |
| conflict-markers | stop | `<<<<<<<` / `=======` / `>>>>>>>` left in a file. |
| large-file | stop over 100 MB, warning over 50 MB | GitHub refuses a file over 100 MB. |
| compile (editor) | stop on push | The Console has compile errors. |
| tool-output (editor) | info | An editor tool recorded output: Pending Tool Changes shows which. |

What a re-save check cannot know: whether a newly written field holds its default value (the C# initializer). It
treats a new `key: value` line as a re-save; a new list element, a changed or removed value, or a new object is a
real change. On PR #1766 it found the four true re-saves and correctly kept the three Configure prefabs out: each
also carries one real override (a domain column's Name label `JADE` → `RUBY`) that a by-eye review had missed.

**The /ship gates** are `Tools/Build/ship_gates.py`: the one gate list (`.claude/skills/ship/gates.sh` only calls it,
so Windows without bash runs the same gates). The window passes `--no-refcompile` because the editor compiles the
project itself.

## 3. Safety rules the window keeps

- Pull, Update from bleeding-edge, Switch, Discard and New branch are refused in Play mode, and save open scenes and
  assets first; after them the editor re-imports what changed (`AssetDatabase.Refresh`).
- Pull and Update require no uncommitted edits to tracked files, so a merge never mixes with half-done work.
- A pull or update that conflicts is **undone** (`git merge --abort`) and the files are named: nothing is ever left
  half-merged in the editor. Resolving a prefab or scene conflict is object-by-object work (`/ship` §1,
  `/asset-surgery`): **Ask Claude to ship this**.
- Commits stage exactly the ticked paths (through a file, `--literal-pathspecs`: Unity names with `[ ] *` and lists
  longer than Windows' command line are safe), never `-A`, and leave anything else you staged alone.
- Pull is a merge, never a rebase; nothing force-pushes; the protected branches are refused.
- Every git and python call runs on a worker thread; the editor never freezes on a fetch or a push.

## 4. What it needs

- **git**: on PATH, or the copy GitHub Desktop ships inside itself (`%LOCALAPPDATA%\GitHubDesktop\app-*\resources\app\git\cmd\git.exe`
  on Windows, inside `GitHub Desktop.app` on macOS), or Program Files / Homebrew. `FrogletGit.GitExecutable` finds it.
- **Python 3** for the checks and gates (python3, python or `py -3`, as the QA Session window finds it). Without it
  the window still commits and pushes, and asks before going on unchecked.
- **A GitHub sign-in git can use for pushing.** git runs with no terminal (`GIT_TERMINAL_PROMPT=0`), so it fails fast
  instead of waiting on an invisible password prompt. Git Credential Manager (part of Git for Windows) opens its own
  sign-in window; on macOS the keychain answers. If a push says authentication failed, push once from GitHub Desktop
  or a terminal so the credential is stored, then use the window. **Not yet tried on a collaborator's machine.**

## 5. Files

| Role | Path |
|---|---|
| Window | `Assets/_Scripts/Editor/FrogletTools/FrogletSourceControlWindow.cs` |
| Git operations + parsers | `Assets/_Scripts/Editor/FrogletTools/FrogletSourceControl.cs` |
| Git runner (finds Desktop's git, UTF-8, network timeouts) | `Assets/_Scripts/Editor/FrogletTools/FrogletGit.cs` |
| Python runner | `Assets/_Scripts/Editor/FrogletTools/FrogletPython.cs` |
| Push checks (the rules) | `Tools/Build/push_checks.py` |
| The /ship gate list | `Tools/Build/ship_gates.py` (`gates.sh` calls it) |
| Edit-mode tests | `Assets/_Scripts/Tests/Editor/FrogletSourceControlTests.cs` |

## 6. Next (v2, v3 — not built)

- v2: open the pull request from Unity with a one-time GitHub sign-in (or the credential Desktop already stored),
  the PR body's Affects line from `who_works_where.py`, and CI once.
- v3: merge from Unity, for the core four only, when checks are green.
- Both need a decision on where a GitHub token is stored (`EditorPrefs` is plain text on disk).
