# Launch status contract

**One record per pipeline run, on one data branch, read by one console.** Every launch pipeline (a player build, an
upload to a pre-release channel, a promotion, a rollback, a UGS deploy, a gate) ends by publishing a record of what it
just did with `Tools/Launch/launch_status.py publish`. The Launch Console (the FrogletTools editor window and the
claude.ai artifact) reads only the merged `status.json` from the `launch-status` branch, so every number it shows
carries where it came from (`source`, `run_url`) and how old it is (`updated_at`). Nothing on the console is typed by
hand except the launch checklist, which lives in the artifact's own store.

Context: `Docs/LAUNCH/READINESS.md` (the audit and the ranked blockers). Decided 2026-11-05 (Shombith).

## The branch

`launch-status` is an orphan, data-only branch. It is never merged anywhere and holds no code.

```
status.json               every component's latest record, rebuilt on each publish
components/<id>.json      one component's latest record
history/<id>.jsonl        every record that component published, newest last
README.md
```

Read it from anywhere (the repo is public):
`https://raw.githubusercontent.com/froglet-studio/Cosmic-Shore/launch-status/status.json`, or
`python3 Tools/Launch/launch_status.py show` in a checkout.

## A record

| Field | Required | Values |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | the component id (below) |
| `platform` | yes | `windows` `ios` `android` `web` `liveops` `backend` `quality` `release` |
| `kind` | yes | `channel` (what players on a channel run), `build` (an artifact was made), `deploy` (backend config), `gate` (a check) |
| `result` | yes | `success` `failure` `running` `skipped` `cancelled` |
| `updated_at` | yes | ISO-8601 UTC to the second (`2026-11-05T20:00:00Z`), set by the tool to the moment of publishing; never back-dated |
| `source` | yes | the workflow name (defaults to `$GITHUB_WORKFLOW`) |
| `channel` | no | `prerelease` `production` `staging` `development` |
| `version`, `build` | no | the build's version and build number |
| `commit` | no | the commit the build or deploy came from (defaults to `$GITHUB_SHA`) |
| `summary` | no | one plain line, ≤ 280 characters |
| `run_url` | no | the run (filled from `$GITHUB_RUN_ID` in Actions) |
| `link` | no | the store or dashboard page for this component |
| `rollback_to` | no | the version a rollback would return to |
| `details` | no | up to 24 short `lower_snake_case` key → value pairs |

`updated_at` says when the record was written. A record that reports something which happened earlier (a hand
upload, a seed from an audit) says when in `details.event_date` (`YYYY-MM-DD`).

**A record never holds a secret.** Unknown fields are refused; every text value is one short line; every string in
the record, whatever field it is in (a `--build` or `--link` given the wrong variable included), is scanned, and
anything that looks like a credential is refused and the workflow step fails. The rules (`SECRET_PATTERNS` and
`_token_reason` in the tool) catch: a private-key or certificate block, with or without its dashes; a base64 key
body (`MII...`, OpenSSH); GitHub, AWS, Google, Slack and `sk-` API keys; Slack and Discord webhook URLs; a JWT;
credentials in a URL (`user:pass@host`, `token@host`); an `Authorization:` header, a `Bearer`/`Basic` credential and
`password=` / `token:` style assignments; a signed URL or any URL with a query string; a Unity licence serial, an
Apple app-specific password and a 32-character hex key; and by shape, a long unbroken token, a random-looking token
(also one broken up by `/`, `+`, `=` or `.`), and a base64 body split into pieces by spaces or slashes. URLs in
`run_url` and `link` are `https` with no user, port or query string.

This is deliberate: the branch is public, and the 2026-10-12 `USYM_UPLOAD_AUTH_TOKEN` leak came from a log nobody
thought would carry a secret. False negatives cost more than false positives: a refused record costs a re-run with a
plainer summary. The known false positives are ids that look random by construction (a claude.ai artifact link, a
Unity GUID, a sha256 digest), so a record links to the GitHub run or the store page instead. Measured 2026-11-05: the
rules refuse about 1 in 140 lines of the repo's docs and 3 of 1,063 commit subjects; a random base64 token of 20+
characters gets through about 1.4% of the time (16 characters: about 11%), one split into 8-character pieces 0.4%.

## Component ids

| id | platform | kind | written by |
|---|---|---|---|
| `windows-prerelease` | windows | channel | the Steam upload to the Playtest app's `internal` branch |
| `windows-production` | windows | channel | the promotion to the Playtest app's `default` branch |
| `ios-prerelease` | ios | channel | the TestFlight upload |
| `ios-production` | ios | channel | the App Store release (recorded by hand from the promote step until it is automated) |
| `android-prerelease` | android | channel | the upload to the Play internal track |
| `android-production` | android | channel | the promotion to the closed / production track |
| `web-staging` | web | channel | the Toybox publish to the staging root |
| `web-production` | web | channel | the Toybox promotion to `/play` |
| `build-windows`, `build-ios`, `build-android`, `build-web` | that platform | build | the player build job |
| `ugs-development`, `ugs-production` | liveops | deploy | `ugs-deploy.yml` (Cloud Code, access policy, the launch flags) |
| `trunk-static` | quality | gate | the bleeding-edge guard's static checks |
| `unity-compile` | quality | gate | the guard's Unity compile + EditMode run |
| `smoke-<mode>` | quality | gate | the Unity per-mode smoke test |
| `release-<tag>` → use `release` | release | build | the release workflow (tag, release notes) |

A new pipeline adds its id to this table in the same PR.

## Publishing from a workflow

```yaml
permissions:
  contents: write          # the job that publishes needs it; keep every other job read-only
steps:
  - name: Publish launch status
    if: always()
    run: >
      python3 Tools/Launch/launch_status.py publish
      --id web-staging --platform web --kind channel --channel staging
      --result "${{ job.status == 'success' && 'success' || 'failure' }}"
      --version "$VERSION" --summary "Toybox $VERSION on the staging root"
```

Rules:
- Publish with `if: always()` so a failure is recorded too.
- Publish from a job that holds no store or signing secret where you can (a small final job that `needs:` the work).
- `--dry-run` validates and prints the record without pushing; use it in pull-request runs.
- Several workflows may publish at once: `publish` re-fetches and retries the push with backoff (2, 4, 8, 16 s plus
  jitter, five attempts). A failure no retry can fix (no such remote, 403, authentication) stops at once.
- `publish` leaves nothing behind in the repo it runs in: no checkout, no worktree, no local `launch-status` branch.
  It builds the commit with git plumbing (a private temporary index, `hash-object`, `write-tree`, `commit-tree`) on
  the fetched tip and pushes `<commit>:refs/heads/launch-status`; a lost race is a refused non-fast-forward, and the
  next attempt rebuilds on the new tip. It fetches the whole (small) branch, never with `--depth`, so it never turns
  a full clone shallow. git's own error text is scrubbed of credentials before it is printed.

## Proof

`python3 Tools/Launch/launch_status.py self-test` accepts 7 good records (the shapes the first real records take: run
links, a full sha, dates, CamelCase identifiers, words like "Authorization failed") and refuses 40 bad ones, one per
shape above: unknown field, missing result, bad platform, signed URL, long token, GitHub token, private key, JWT, bad
sha, multi-line, bad id, `user:pass@` in a link and in a summary, `token@host`, an `Authorization:` header, `Bearer` and
`Basic` credentials, `password=`, a base64 body split by spaces and by slashes, a stripped PEM body, an OpenSSH body, a
dash-less PEM header, a token followed by a full stop, a token in `build` and in a link path, a Slack webhook, a hex
key, a Unity serial, an AWS key id, a Google key, a query string in a summary, a trailing newline in an id, a URL and
a detail key, a Unicode line separator, a non-UTC or date-only `updated_at`, `schema: true` and a NaN detail. It then
publishes end to end against a throwaway local bare remote: the first record (orphan branch + README), a second
publisher winning the race between fetch and push (the loser retries and both records survive), a refused record
(nothing pushed), a missing remote (fails at once, no retries); and checks that neither caller repo was left with a
local branch, a worktree, a shallow file or a touched working tree.

`python3 Tools/Launch/check_action_pins.py self-test` proves the pin check finds 13 unpinned shapes (a tag, quoted
tags and branches, a short or upper-case sha, a sha with a suffix, no ref, a reusable workflow on a branch, a flow
mapping, a docker tag, a short docker digest, `uses :`, a sha hidden in a trailing comment) and passes pinned ones,
local actions and commented-out lines. `.github/workflows/launch-tools-check.yml` runs every launch tool's self-test
on each change under `Tools/Launch/` or to a `launch-*` / `release*` workflow (`.yml` or `.yaml`), then fails any
launch workflow whose third-party actions are not pinned to a commit sha.
