# Branch protection for bleeding-edge

**Status (2026-11-01): proposed.** Nothing here is switched on until a repository admin follows section 5.
This page is the ruleset, the reasons for it, how agent sessions get their own GitHub account, and the exact clicks.
It changes three files: this doc, `.github/CODEOWNERS`, and `.github/workflows/bleeding-edge-guard.yml` (which now
also runs on pull requests).

## 1. What changes, in one minute

- **Today** the Bleeding-edge guard *reports*. It runs after a commit is already on `bleeding-edge`, opens a
  tracking issue when that commit is broken, and nothing stops the next merge. That is how three generator breaks
  merged on top of an already-red trunk in October without anyone seeing a new failure (`Docs/Reports/CI.md` §1).
- **With the ruleset** the guard *gates*. Its two checks run on the pull request and must pass before the merge
  button works. Paths that can hurt every mode or every machine at once also need one human's approval.
- **The rule is light on purpose.** Nobody reviews pull requests today. Most PRs (gameplay code, assets, docs,
  skills) still need **no review at all**, only green checks. Review is asked for on four kinds of path only.

## 2. The ruleset ("Protect trunks", target `bleeding-edge`)

| Rule | Setting | Why |
|---|---|---|
| Require status checks to pass | **`Static validation`** and **`Unity compile`** (source: GitHub Actions) | The guard's two stages. A break is stopped while it is one PR old, not found fifty commits later. |
| Require branches to be up to date | **Off** | With dozens of agent PRs a day, "up to date" would mean re-running every PR after every merge. The push-side guard still verifies each landed commit. |
| Require a pull request before merging | **On**, required approvals **0** | Every change reaches the trunk through a PR, so the checks above always run. Zero approvals means most PRs merge on green checks alone. |
| Require review from Code Owners | **On** | A PR touching a path in `.github/CODEOWNERS` needs one approval from a listed owner. Section 3 lists the paths. |
| Dismiss stale approvals when new commits are pushed | **On** | An approval covers what the reviewer read, not what was pushed after. It only matters on code-owned PRs. |
| Block force pushes | **On** | Nobody rewrites the trunk's history. |
| Restrict deletions | **On** | Nobody deletes the trunk. |
| Bypass list | **Repository admin**, mode **For pull requests only** | Emergencies: an admin can merge a PR past the rules (GitHub logs every bypass under **Rules > Insights**). Direct pushes stay blocked even for admins. |

**Admins can bypass, and this is deliberate.** If the trunk must be repaired right now (a red check that is wrong, a
reviewer who is away), an admin opens the PR and ticks **Merge without waiting for requirements to be met**. Use it
for emergencies only. A bypass is visible to everyone in the ruleset's Insights tab.

**What "Unity compile" proves today: nothing yet.** No Unity runner has been attached since 2026-10-13, so the job is
skipped, and GitHub counts a skipped required check as passed. Requiring it now costs nothing. The day
`UNITY_RUNNER_LABEL` is set (`Docs/BUILD_PIPELINE_SETUP.md` §5), every PR that touches code gets a real compile with
no change to the ruleset. A PR from a **fork** never reaches the runner, so its "Unity compile" is always a skip.

### What the workflow change does

`bleeding-edge-guard.yml` gained a `pull_request` trigger for `bleeding-edge`:

- **Static validation** and **Unity compile** run on every PR into `bleeding-edge`, `claude/**` heads included,
  with no path filter. A required check whose workflow never starts stays "Expected" forever and blocks the merge.
- On a PR, **Report status** and **Attempt autofix** do not run: no tracking issue, no repair PR. The PR's own red
  check is the report, and its author fixes their branch.
- The Unity scope step reads the PR's base...head range, so a docs-only PR skips the compile the same way a
  docs-only push does.
- Pushes to `bleeding-edge` and `development` behave exactly as before: same jobs, same tracking issue, same autofix.

`unity-ci.yml` is unchanged. It still skips `claude/**` PRs, and its job names (`resolve`, `conditional-compilation`,
`unity`) differ from the guard's, so the required check names cannot be satisfied by the wrong workflow.

## 3. Which paths need a review (`.github/CODEOWNERS`)

| Path | Owners (any one approves) | Why it needs a second person |
|---|---|---|
| `/.github/` | Shombith, Caleb, Garrett, Yash | Workflows can hand repository secrets to whatever they run. Also covers CODEOWNERS itself. |
| `/Port/` | Shombith, Yash, Garrett | Amoebius. Shombith drives the engine, Yash the launcher and studio server, Garrett the parity goldens. |
| `/Assets/_Scenes/` | Shombith, Caleb, Garrett, Yash | Every mode, the menu and boot. A scene override beats its prefab, and a bad one breaks a mode silently. |
| `GameCanvas.prefab` (+ `.meta`) | Shombith, Caleb, Garrett, Yash | The one in-game UI for every mode (`Docs/GAMECANVAS.md`). |
| `ArcadeGameConfigureModal.prefab` (+ `.meta`) | Shombith, Caleb, Garrett, Yash | The modal every card launches through. A nulled reference there once sent every player to the Jade default. |

GitHub logins: Shombith = `@Shombith03`, Caleb = `@FenrysUnchained`, Garrett = `@gradies`, Yash = `@YsKhan61`.

- The last matching line in CODEOWNERS wins. A narrower line *replaces* the owners above it rather than adding to them.
- A PR's author can never approve their own PR. An owner's change needs one of the other owners.
- Four `.unity` files sit outside `Assets/_Scenes/` (QuickScenePro and NiceVibrations demos). They are vendor demos
  and are deliberately not listed.
- To add a path, add a line and say why in a comment. To widen a path's owners, ask the people already listed.

## 4. Agent sessions get their own GitHub account

**The problem.** Agent sessions push, open PRs and merge as `Shombith03`. So a review from `Shombith03` might be a
person or might be a session, and `Shombith03` is a repository admin, so a session holding that token is a bypass
actor. Until agents have their own identity, the code-owner rule only proves a human read the change when the
approval comes from Caleb, Garrett or Yash.

**After the move:**

- An agent's PR is authored by `froglet-agent`. Any of the four owners can approve it, Shombith included, and every
  approval from a human account means a human read it.
- `froglet-agent` has **Write**, not Admin or Maintain. It is not on the bypass list and not in CODEOWNERS. It can open
  PRs and push branches, but it cannot merge past a red check or a missing owner review, and its own approval never
  counts as a code-owner review.
- GitHub attributes each PR, push and review to the right account, so "who did this" is readable again from GitHub alone.
- The guarantee holds only if **no session keeps a human's token**. A session that still runs with `Shombith03`'s
  credentials can still approve as Shombith. Section 4.4 is the check.

Two ways to give agents an identity. **Use the machine user (4.1-4.3)**: it works for Claude Code on the web and on
your own computer today. The GitHub App (4.5) suits workflows and scripts, not interactive sessions.

### 4.1 Create the machine user (anyone with an email inbox, about 10 minutes)

1. Make a shared inbox the team controls, for example `agent@froglet.studio`. Do not use a personal address: whoever
   owns the inbox can reset the account.
2. In a **private or separate browser profile** (so you are not signed in as yourself), go to
   <https://github.com/signup> and create the account `froglet-agent` with that email. GitHub's terms allow one
   machine account per person who sets it up; the person who creates it is responsible for it.
3. Turn on two-factor authentication: **avatar > Settings > Password and authentication > Enable two-factor
   authentication**. Store the recovery codes and the password in the team's password manager, not in the repo.
4. Set its profile name to "Froglet agent (Claude Code sessions)" so PRs and commits read clearly.

### 4.2 Give it write access (a repository admin: Shombith, Garrett, Caleb, Yash or Iggy)

1. Open <https://github.com/froglet-studio/Cosmic-Shore/settings/access>
   (**Settings > Collaborators and teams**).
2. Click **Add people**, type `froglet-agent`, choose it, and pick the role **Write**. Do not pick Maintain or Admin:
   both can change settings, and Admin is a bypass actor.
3. Sign in as `froglet-agent` (the separate browser profile) and accept the invitation from its email or from
   <https://github.com/froglet-studio/Cosmic-Shore/invitations>.
4. If the organisation is on a paid plan, the account takes a seat. On the free plan it does not.

### 4.3 Point Claude Code sessions at it

**Claude Code on the web (claude.ai/code, the cloud sessions most of the team uses).** A cloud session acts on GitHub
as whichever GitHub account is connected to the claude.ai account that started it.

1. Decide whose claude.ai account runs agent sessions. Either one shared claude.ai account for agent work, or each
   person's own account (then each person repeats steps 2-4).
2. Signed in to GitHub as `froglet-agent` (the separate browser profile), open <https://claude.ai/connect-github>
   while signed in to that claude.ai account, and connect GitHub. If it is already connected to a personal GitHub
   account, disconnect that first from the same page.
3. If GitHub asks to install or authorise the Claude GitHub App for `froglet-studio/Cosmic-Shore`, approve it. If
   the app is not installed on the organisation, an organisation owner installs it from
   <https://github.com/apps/claude/installations/select_target>.
4. Start a new session and ask it to open a draft PR. The PR's author should read `froglet-agent`. Close the draft.

Trade-off: while that claude.ai account is connected to `froglet-agent`, everything it does on GitHub is the agent,
including PRs the person meant to open as themselves. People who want to keep both open their own PRs on github.com.

**Claude Code on your own computer (the CLI or the desktop app).** The CLI uses whatever git and `gh` are
signed in as.

1. As `froglet-agent` on github.com: **avatar > Settings > Developer settings > Personal access tokens >
   Fine-grained tokens > Generate new token**.
   - Resource owner: `froglet-studio`. Repository access: **Only select repositories > Cosmic-Shore**.
   - Repository permissions: **Contents: Read and write**, **Pull requests: Read and write**, **Issues: Read and
     write**, **Workflows: Read and write** (needed to push a branch that edits `.github/workflows/`), **Actions:
     Read**, Metadata: Read (automatic). Nothing else.
   - Expiry: 90 days. Put a reminder in the calendar to rotate it.
   - If the organisation requires approval for fine-grained tokens, an organisation owner approves it under
     **Organisation settings > Personal access tokens > Pending requests**.
2. Keep the token in your password manager. Never commit it and never paste it into a chat.
3. In the checkout you run agent sessions from, make this one repository use the agent's identity (your global
   git settings stay yours):

   ```bash
   git config user.name  "froglet-agent"
   git config user.email "<froglet-agent's id>+froglet-agent@users.noreply.github.com"   # GitHub: Settings > Emails shows it
   ```

4. Give the session the token through the environment, not through your own `gh` login. Create
   `.claude/settings.local.json` in that checkout (it is git-ignored):

   ```json
   { "env": { "GH_TOKEN": "<the token>" } }
   ```

   Then run `gh auth setup-git` once in that checkout, so `git push` uses `gh`, which reads `GH_TOKEN`.
5. Check it: `gh api user --jq .login` prints `froglet-agent`.

**GitHub Actions.** The guard's autofix and the other workflows already act as `github-actions[bot]`, a separate
identity. Nothing to change.

### 4.4 Check that no session still acts as a human

- Open <https://github.com/froglet-studio/Cosmic-Shore/pulls?q=is%3Apr+author%3AShombith03> after a week. New
  agent PRs should be authored by `froglet-agent`, not `Shombith03`.
- Shombith revokes any personal token a session was given: **github.com > Settings > Developer settings > Personal
  access tokens**, and **Settings > Applications** for OAuth apps that sessions authorised.
- Then edit the comment at the top of `.github/CODEOWNERS` to say that agents act as `froglet-agent`.

### 4.5 Alternative: a GitHub App instead of a machine user

Better for automation, worse for interactive sessions.

1. Organisation owner: **Organisation settings > Developer settings > GitHub Apps > New GitHub App**. Name
   "Froglet Agent", homepage the repo URL, untick **Webhook > Active**.
2. Repository permissions: Contents, Pull requests, Issues, Workflows: **Read and write**; Metadata: Read.
   **Where can this app be installed: Only on this account.** Create it.
3. **Generate a private key** (a `.pem` download) and store it in the password manager. Note the **App ID**.
4. **Install App > froglet-studio > Only select repositories > Cosmic-Shore**.
5. Anything that runs as the app trades the key for a one-hour installation token (for example with
   `actions/create-github-app-token` in a workflow) and uses that token as `GH_TOKEN`.

Same effect on approvals as the machine user: its PRs show as `froglet-agent[bot]`, it is not a code owner, and it is
not on the bypass list. The catch is that Claude Code on the web cannot run as an app, and a one-hour token does not
suit a local session either. Use the app for workflows that push, the machine user for sessions.

## 5. Applying the ruleset: the exact clicks (a repository admin)

### Before you start

1. **Merge this PR first.** The required checks only appear in GitHub's picker after they have run on a PR at least
   once, and this PR is the first one to run them.
2. **Make sure `bleeding-edge` is green.** The guard has been red since 2026-10-28 (`Docs/Reports/CI.md`). A PR is
   checked as merged into the trunk, so a red trunk makes every PR red and nothing can merge without a bypass. Open
   <https://github.com/froglet-studio/Cosmic-Shore/actions/workflows/bleeding-edge-guard.yml>: the latest
   `bleeding-edge` run must be green. If it is not, fix that first.
3. If a ruleset called "Protect trunks" already exists under Settings > Rules > Rulesets, **edit it** instead of
   making a second one, and check its settings against the list below.

### The clicks

1. Open <https://github.com/froglet-studio/Cosmic-Shore/settings/rules> (**Settings > Rules > Rulesets**).
2. **New ruleset > New branch ruleset**.
3. **Ruleset name:** `Protect trunks`.
4. **Enforcement status:** `Active`.
5. **Bypass list > Add bypass**, tick **Repository admin**, click **Add selected**. In its row, open the dropdown and
   pick **For pull requests only**.
6. **Target branches > Add target > Include by pattern**, type `bleeding-edge`, click **Add inclusion pattern**.
7. Under **Rules**:
   1. Tick **Restrict deletions**.
   2. Tick **Block force pushes**.
   3. Tick **Require a pull request before merging**, then open **Additional settings**:
      - **Required approvals:** `0`
      - Tick **Dismiss stale pull request approvals when new commits are pushed**
      - Tick **Require review from Code Owners**
      - Leave **Require approval of the most recent reviewable push** and **Require conversation resolution before
        merging** unticked
      - **Allowed merge methods:** leave all three
   4. Tick **Require status checks to pass**, then:
      - Leave **Require branches to be up to date before merging** unticked
      - Tick **Do not require status checks on creation**
      - Click **Add checks**, type `Static validation`, and pick the entry whose source is **GitHub Actions**.
        Repeat for `Unity compile`.
   5. Leave every other rule unticked (signed commits, linear history, deployments, code scanning, merge queue).
8. Click **Create** at the bottom.
9. Check it: open any open PR into `bleeding-edge`. The merge box should list **Static validation** and **Unity
   compile** as required, and a PR touching `/.github/` should say **Review required: code owner**.

### Undo

**Settings > Rules > Rulesets > Protect trunks > Enforcement status: Disabled > Save changes.** Everything is back
as it was. The workflow's PR trigger is harmless with the ruleset off: it only adds a check to each PR.

## 6. What this changes for people and for workflows

- **No more direct pushes or local merges into `bleeding-edge`.** "Merge remote-tracking branch ... into
  bleeding-edge" pushed from a computer is refused. Everything goes through a PR, which `/ship` and
  `/to-bleeding-edge` already do. `Ys-bleeding-edge` reaches the trunk through its PR as it does today.
- **perf-daily** pushes its daily status commit straight to `bleeding-edge`. Once the ruleset is on, that push is
  refused and the workflow already falls back to the `perf-status` branch (with a warning). The editor's Project
  Record then reads a status that stops updating on `bleeding-edge`. To keep it, change perf-daily to open a PR,
  or accept the fallback. Do not add GitHub Actions to the bypass list: every workflow would then bypass.
- **The guard's autofix** opens PRs and never pushes to the trunk, so it is unaffected.
- **`development`** is not covered. Add it as a second target pattern later if the team wants the same gate there.
- **Agent sessions** still never approve or merge on their own (`/ship` §5). With the machine user they cannot
  merge a code-owned PR without a human's review anyway.

## 7. Decisions still open

- **Owners in CODEOWNERS** (section 3) were picked from `Docs/TEAM.json` and recent merges. Shombith confirms or
  edits them in this PR.
- **Who connects claude.ai to `froglet-agent`**: one shared agent account, or each person's own (section 4.3).
- **perf-daily**: PR or fallback (section 6).
