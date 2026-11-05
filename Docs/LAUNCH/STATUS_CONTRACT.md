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
| `updated_at` | yes | ISO-8601 UTC, set by the tool |
| `source` | yes | the workflow name (defaults to `$GITHUB_WORKFLOW`) |
| `channel` | no | `prerelease` `production` `staging` `development` |
| `version`, `build` | no | the build's version and build number |
| `commit` | no | the commit the build or deploy came from (defaults to `$GITHUB_SHA`) |
| `summary` | no | one plain line, ≤ 280 characters |
| `run_url` | no | the run (filled from `$GITHUB_RUN_ID` in Actions) |
| `link` | no | the store or dashboard page for this component |
| `rollback_to` | no | the version a rollback would return to |
| `details` | no | up to 24 short `lower_snake_case` key → value pairs |

**A record never holds a secret.** Unknown fields are refused; every text value is one short line; anything that
looks like a credential (a long unbroken token, a GitHub/AWS key, a JWT, a private-key header, a URL with a query
string) is refused, and the workflow step fails. URLs are `https` with no query string. This is deliberate: the branch
is public, and the 2026-10-12 `USYM_UPLOAD_AUTH_TOKEN` leak came from a log nobody thought would carry a secret.

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
- Several workflows may publish at once: `publish` re-fetches and retries the push with backoff.

## Proof

`python3 Tools/Launch/launch_status.py self-test` accepts a good record and refuses eleven bad ones (unknown field,
missing result, bad platform, signed URL, a long token in a summary, a GitHub token in details, a private key header, a
JWT, a bad sha, a multi-line summary, a bad id). `.github/workflows/launch-tools-check.yml` runs it on every change
under `Tools/Launch/`, and fails any launch workflow whose third-party actions are not pinned to a commit.
