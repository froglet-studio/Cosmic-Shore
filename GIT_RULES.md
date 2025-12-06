# Cosmic Shore – Git Rules & Standards

Target level: **AAA / Metacore-style** cleanliness  
Scope: **All Cosmic Shore repos** (game, tools, backends, etc.)

Our goals:

- Keep history **clean, readable, and searchable**
- Make debugging and rollbacks **safe and fast**
- Make collaboration **predictable**, even with many programmers
- Match standards of **top-tier studios**

---

## 1. Branching Model

### 1.1 Main Branches

- `main`
  - Always **stable & releasable**
  - No direct pushes. Changes come **only via Pull Requests**.
- `release/*` (optional)
  - For preparing releases (store submissions, milestone builds, etc.)
  - E.g. `release/2025.12.0`

> If in doubt, target `main` with your PR.

---

### 1.2 Feature / Fix Branches

Short-lived branches for actual work:

- `feature/<area>-<short-description>`
- `bugfix/<area>-<short-description>`
- `hotfix/<area>-<short-description>`
- `chore/<area>-<short-description>`

**Examples (Cosmic Shore–specific):**

- `feature/arcade-swap-tray-powerup`
- `feature/app-apple-signin-flow`
- `bugfix/ads-appodeal-init-conflict`
- `bugfix/game-round2-score-delta`
- `chore/ci-unity-builder-cache`
- `chore/app-localization-cleanup`

> One logical task per branch. If you feel like "this branch is doing 5 different things", split it.

---

### 1.3 Branch Checklist (Before Push)

- [ ] Branch name follows `type/area-short-description`
- [ ] Branch is based on **latest `main`**
- [ ] Only **one logical feature/fix/chore** in this branch  
- [ ] No temporary test scenes / PlayGround scenes that aren’t intended to be kept

---

## 2. Commit Rules

### 2.1 Principles

- Small, **atomic** commits  
  → One commit = one logical change
- Code in a shared branch should **build & run**
- No secrets (API keys, passwords) in commits
- No junk:
  - No `Library/`, `Temp/`, `Logs/`, `.vs/`, etc.
  - No personal `.user` or `.csproj` edits

Unity-specific:

- Save scenes & prefabs before committing.
- Avoid committing huge binary assets “just because”. If needed, use **Git LFS** (if repo is configured).

---

### 2.2 Commit Message Format

```text
<type>(<optional-scope>): <short summary in imperative mood>
```

**Allowed types:**

- `feat` – new feature
- `fix` – bug fix
- `refactor` – internal code changes, no feature/behavior change
- `chore` – maintenance, CI, configs, package updates
- `docs` – documentation only
- `test` – tests only
- `perf` – performance improvements

**Scope examples (optional but encouraged):**

- `feat(arcade)`, `fix(ads)`, `refactor(level-editor)`, `chore(ci)`, `docs(git-rules)`

**Summary line rules:**

- Imperative mood: “add”, “fix”, “update”, NOT “added”, “fixing”
- Keep under ~60–72 chars
- No trailing period
- No emojis

---

### 2.3 Good Commit Examples (Cosmic Shore)

```text
feat(arcade): add swap-tray and move-tray powerups

fix(scoring): compute round2 score using volume delta

refactor(level-editor): extract random placement into service

chore(ci): enable cache for unity 2022.3 builder

docs(git): add cosmic shore git rules
```

**Bad examples:**

```text
"changes"
"fixed stuff"
"update"
"final commit"
"try to fix bug"
```

---

### 2.4 Commit Body (Optional but Recommended)

Use a body when the **“why”** isn’t obvious from the diff.

```text
fix(scoring): compute round2 score using volume delta

Why:
- Score was accumulating total volume instead of per-round delta.
- Round2 scores could explode if player created volume early.

What:
- Track lastVolumeCreated in VolumeCreatedScoring
- Use delta = current - lastVolumeCreated
- Reset state on unsubscribe to avoid cross-round leakage

Notes:
- Verified with 2-player local test in Duel mode
```

---

### 2.5 Commit Checklist (Before `git push`)

- [ ] Commit message follows `type(scope): summary` format
- [ ] Only one logical change per commit
- [ ] Project builds locally in Unity
- [ ] No leftover debug logs like `Debug.Log("test")`
- [ ] No commented-out blocks of old code (use git history if needed)
- [ ] No big accidental asset changes (e.g. unrelated textures, prefabs)

---

## 3. Pull Requests

All changes to `main` must go through **Pull Requests**.

### 3.1 PR Titles

Format:

```text
<type>: short description
```

If you have a task ID or Notion/GitHub issue ID, add it at the front:

```text
CS-123 feat: add swap-tray powerup to arcade mode
```

**Good titles:**

- `feat: add swap-tray powerup to arcade mode`
- `fix: resolve appodeal init conflict with unity ads`
- `chore: clean up legacy duel hud scripts`
- `refactor: split level editor into separate modules`

---

### 3.2 PR Description Template

Copy-paste this in the PR description (GitHub/GitLab template recommended):

```md
## Summary
Short 1–3 line description of what this PR does.

## Motivation / Context
Why is this needed?
- What problem or requirement does it address?
- Any related task/issue/Notion link?

## Changes
- Bullet list of key changes.
- Mention important scenes, prefabs, and scripts.
  - e.g. `Scenes/Duel.unity`
  - e.g. `CosmicShore.Game.UI/DuelCellStatsRoundUIController.cs`

## Testing
- [ ] Unity project opens without compilation errors
- [ ] Manual playtest:
  - [ ] Main menu -> Arcade mode
  - [ ] Duel mode 2 rounds
- [ ] Target platform build (tick what you tested)
  - [ ] Android
  - [ ] iOS
  - [ ] WebGL
- Automated tests:
  - [ ] Not applicable
  - [ ] Added/updated tests:
    - `Tests/Runtime/VolumeCreatedScoringTests.cs`

## Screenshots / Video
If UI or gameplay changed, attach:
- Before / After screenshots
- Or a short clip/gif demonstrating the behavior

## Risks & Rollback
- Risks:
  - e.g. "Touching shared RoundStats logic used by other game modes"
- Rollback plan:
  - e.g. "Revert commit `<hash>` or disable feature flag X"

## Additional Notes
- Known limitations, follow-up tasks, TODOs, etc.
```

---

### 3.3 Author Checklist (Before Requesting Review)

Before assigning reviewers:

- [ ] Branch is up-to-date with `main`  
      (`git fetch` + `git rebase origin/main` preferred)
- [ ] No merge conflicts
- [ ] Project builds locally in Unity
- [ ] Manual testing done (and documented in PR)
- [ ] Removed debug logs / commented-out code
- [ ] PR scope is focused (not “refactor + new feature + random stuff”)
- [ ] PR description is filled and clear

---

### 3.4 Reviewer Checklist

When reviewing someone else’s PR:

**Understanding**

- [ ] Read PR title and summary
- [ ] Understand what problem/feature it addresses
- [ ] Open related ticket/Notion page if linked

**Code quality**

- [ ] Naming is clear and consistent (methods, fields, classes)
- [ ] Logic is understandable without guesswork
- [ ] No unnecessary duplication or obvious refactor candidates
- [ ] Unity patterns are sane (e.g. no heavy logic in `Update` if avoidable)

**Architecture**

- [ ] Responsibilities are in the right place:
  - UI scripts handle UI; game logic stays in managers/services
  - No random cross-module dependencies without reason
- [ ] Changes respect existing patterns (events, ScriptableObjects, managers, etc.)

**Safety & Testing**

- [ ] Edge cases considered (null checks, offline state, missing ads, etc.)
- [ ] Tests make sense or are reasonably omitted (for purely visual/UI-only tweaks)
- [ ] No obvious performance pitfalls for target platforms

**Review decision**

- [ ] Approve if everything is okay
- [ ] Request changes with clear, constructive comments if not
- [ ] Use “Comment” for non-blocking suggestions

---

### 3.5 Comment Style

Be precise and professional:

- ✅ “Can we rename `lastVolume` to `lastVolumeCreated` to match the serialized field name?”
- ✅ “This logic looks similar to `LevelSpawner`. Can we reuse or extract a shared helper?”
- ❌ “This code is bad”
- ❌ “Why did you do this?”

Goal: Make future Cosmic Shore devs feel confident reading this code.

---

## 4. Merge Strategy

- No direct merges to `main` without PR.
- Default merge method: **Squash and merge**.
  - Resulting commit message should be meaningful, e.g.:
    ```text
    feat: add swap-tray powerup to arcade mode
    ```
- Keep history **linear and clean**. Avoid long chains of “Merge branch 'main' into …” when possible:
  - Prefer `git fetch` + `git rebase origin/main` in your feature branch before pushing.

---

## 5. Unity & Cosmic Shore–Specific Rules

- Do not commit:
  - `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`, `.vs/`, `.user`, etc.
- Do commit:
  - `Assets/`, `ProjectSettings/`, `Packages/`, `.editorconfig`, `*.meta` (yes, metas are required).
- Scenes & prefabs:
  - Save changes intentionally; avoid noisy edits in unrelated scenes.
- Platform-specific configs (e.g. iOS/Android settings):
  - Changes should be intentional and mentioned in PR description.

---

## 6. Example: Ideal Flow (Cosmic Shore)

1. Create feature branch:

   ```bash
   git checkout -b feature/arcade-swap-tray-powerup
   ```

2. Do work in small steps with good commits:

   ```text
   feat(arcade): add tray powerup config scriptable
   feat(arcade): implement swap-tray behavior in GridManager
   fix(arcade): prevent swap when tray is locked
   test(arcade): add tests for swap-tray positions
   ```

3. Run Unity, fix errors, playtest Arcade mode with powerups.

4. Push and open PR:

   - Title: `feat: add swap-tray powerup to arcade mode`
   - Description: use the template above.

5. Address review comments, then squash-merge once approved.

6. Delete branch after merge:

   ```bash
   git branch -d feature/arcade-swap-tray-powerup
   ```

---

> **Remember:** Git history is part of the codebase. Treat it with the same care as your C# scripts and scenes.
