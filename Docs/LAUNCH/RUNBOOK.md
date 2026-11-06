# Launch runbook: Invite build v1

**Target: Invite build v1, 2026-12-03 (T-0).** Platforms: Windows (Steam Playtest), iOS (TestFlight external), Android
(Google Play), Web Toybox. Live board: the **Launch Console** (https://claude.ai/artifact/XwF8FQrHhUTvNv1gE8kws2) and
**FrogletTools > Launch > Launch Console** in Unity. What is ready and what is not: `Docs/LAUNCH/READINESS.md`. How the
pipelines run: `Docs/LAUNCH/PIPELINES.md`. What can change without a new build: `Docs/LAUNCH/LIVE_UPDATES.md`.

Every step that reaches players (a store submission, an external TestFlight release, a Steam branch going live, a web
publish to `/play`, a UGS production deploy, a Play track promotion) is pressed by a person. The pipelines stop at a
GitHub Environment with a required reviewer; the reviewer is the person pressing.

Times are US Eastern (the company's registered address is in Michigan); move them to the team's day as needed. "Decides" names the area's owner from
`Docs/TEAM.json`, not an assignee: steps are taken on the console's runbook timeline.

## Go/no-go rules

- **Go** when every platform pill on the console is Go, or the launch lead has written an accepted risk on the
  blocking checklist item (evidence link to the decision).
- **No-go** when any of these holds: a release candidate does not reach the main menu on a platform; the release build
  is not on UGS `production`; the minimum-version gate cannot be deployed; crash reporting has not shown a forced test
  crash; the privacy, deletion and age-rating forms do not match the build.
- A platform that is not ready **ships later on its own**. The invite wave goes to the platforms that are Go.

## T-7 (2026-11-26): freeze and stage

| When | Step | Decides | Done when |
|---|---|---|---|
| 10:00 | Merge freeze on `bleeding-edge` for anything not on the launch checklist. | launch lead | Freeze posted in Discord. |
| 10:30 | Tag the release candidate `rc-1` on `bleeding-edge`. The release workflow drafts notes and starts the player builds. | Shombith | Draft release with notes exists; builds running. |
| 11:00–15:00 | Each build reaches its pre-release channel: Steam Playtest `internal`, TestFlight internal, Play internal, web staging. | area owner per platform | Each `*-prerelease` / `web-staging` record is green on the console with the candidate's commit. |
| 15:00 | Unity smoke per mode on the candidate; EditMode green. | Shombith | `smoke-*` and `unity-compile` green on the console. |
| 16:00 | Deploy launch flags and Cloud Code to UGS `development`; play the candidate against it. | Shombith (Yash for Cloud Code) | `ugs-development` green; one match per platform played. |
| T-7 → T-2 | Play the candidate on every platform: menu, sign in, a match, the invite flow, leave and rejoin. Log every defect on the checklist or the QA backlog. | the team | Checklist items closed or marked accepted risk. |

## T-1 (2026-12-02): decide

| When | Step | Decides | Done when |
|---|---|---|---|
| 10:00 | Go/no-go on the console, platform by platform. Record it on each checklist item. | launch lead | Decision written per platform. |
| 11:00 | Tag the release `v0.3.0` (or `rc-N` promoted) on the Go commit. | Shombith | Release notes drafted. |
| 12:00 | Deploy to UGS `production` (the approved job): Cloud Code, access policy, launch flags with `minimumVersion` = this release. | Shombith | `ugs-production` green; flags shown on the console. |
| 13:00 | Store forms checked against this build: App Store privacy label and age rating, Play Data safety and IARC, Steam content survey. | Garrett for the Steam survey (A3); the store admins for the mobile forms (no doc names them) | Each form's evidence link on its checklist item. |
| 15:00 | Write the Discord launch post and the invite wave list (A6 wave policy). | Garrett | Draft posted for review. |

## T-0 (2026-12-03): launch

| When | Step | Decides | Done when |
|---|---|---|---|
| 09:00 | Last look at the console: nothing red since T-1. | launch lead | Go confirmed. |
| 09:30 | **Windows:** in Steamworks App Admin > Builds, set the release build live on the Playtest app's `default` branch (Valve does not allow this from a script). Grant the first wave. | Shombith | `windows-production` recorded; a tester outside the team launches it. |
| 09:30 | **iOS:** add the build to the external TestFlight group (Beta App Review must already have passed). | Shombith | `ios-production` recorded; public link opens. |
| 09:30 | **Android:** promote internal → closed track (staged, 20 % first). | Shombith / Yash | `android-production` recorded. |
| 09:30 | **Web:** promote the staged Toybox to `/play` (approved job). | Shombith | `web-production` shows the new version and commit. |
| 10:00 | Send the invite wave; post on Discord with UTM-tagged links. | Garrett | Wave sent. |
| 10:00–14:00 | Watch: Cloud Diagnostics crashes, join failures (B35-shape bounces), UGS rate-limit errors, Discord. One person on watch at all times; hand-over in Discord. | whoever takes it | Watch log in the Discord thread. |
| 14:00 | Android: raise the rollout to 100 % if crash-free holds. | Shombith / Yash | Rollout at 100 %. |

## T+1 (2026-12-04) and T+7 (2026-12-10)

- **T+1, 11:00:** read crashes, join failures and D1 by platform. Decide on a hotfix (see Incidents). Close the freeze.
- **T+7, 11:00:** D7 read-out by platform and wave (`invite_wave`); decide the next wave's size; file what the launch
  taught into this runbook.

## Incidents

**First, stop the bleeding with what does not need a new build.** In order of speed:

1. **A broken feature with a server gate** (economy, scores, match reports, trust, entitlements): turn it off with its
   kill switch in the launch flags and deploy to UGS production through the approved job (minutes;
   `Docs/LAUNCH/LIVE_UPDATES.md`). This works only for a gate the build ships ON; all five ship off today, so for the
   invite build this step applies only if one is switched on. A broken **mode** cannot be hidden remotely yet
   (`disabledModes` is read but no menu checks it): it needs a build, or a `message` telling players to avoid it.
2. **A broken build that players must not keep using:** raise `minimumVersion` to the fixed release once it is on every
   store; until then use `recommendedVersion` and the message field.
3. **Roll the platform back:**

| Platform | Roll back by | How fast | Note |
|---|---|---|---|
| Windows / Steam | Steamworks App Admin > Builds: set the previous build live on the branch. | Minutes. | Players get it on next launch. |
| iOS | Pause the phased release in App Store Connect; for TestFlight, expire the bad build and re-add the previous one to the group. Ship a fixed build. | Minutes to pause; a review cycle to fix. | Apple has no binary rollback. |
| Android | Play Console: **Halt rollout** (the promote workflow's `halt`, or its `rollback`, which halts and then prints the steps). Play cannot re-serve an older version code: build the earlier tag again (it gets a higher version code) and promote that. | Minutes to halt; a build to roll back. | Players who updated keep the bad build until the rebuilt one ships (`Docs/LAUNCH/PIPELINES.md`, PR #1751). |
| Web | The publish workflow's **rollback** action points `toybox/latest.json` at the previous build (approved job). | One minute plus cache (`latest.json` is `no-cache`). | Builds are immutable on R2, so the old one is still there. |
| UGS | Re-run `ugs-deploy` to production with the previous tag as `ref` (approved job). | Minutes. | Cloud Save data is not rolled back. |

4. **Multiplayer mismatch** (old and new clients): the protocol version filters matchmaking, so mixed versions do not
   meet; raise `minimumVersion` to force the update.

**Tell players:** a Discord post in `#announcements` within 30 minutes of a player-visible incident: what is broken,
what to do (update, wait), when we next post. No status page exists; Discord is the channel.

**Write it down:** a line on the console's runbook (T+1) and a checklist item for the fix, with evidence links.

## Who to call

| Area | Decides | Where that comes from |
|---|---|---|
| Builds, CI, pipelines, editor tooling, Claude skills | Shombith | CLAUDE.md § Team ("Shombith heads tooling") |
| UGS, backend, Steam code, sign-in, Cloud Save | Shombith | `Docs/TEAM.json` owners; CLAUDE.md ("Anything on UGS or the backend is Shombith's") |
| Cloud Code changes | Yash does the work, through Shombith | CLAUDE.md § Team |
| Multiplayer code | ask Yash and Shombith (no declared owner) | `Docs/TEAM.json` roles (Yash: party / multiplayer hardening; Shombith: multiplayer scenes); `who_works_where.py query Assets/_Scripts/Controller/Multiplayer` |
| Steam business: entity, agreements, content survey, wave policy | Garrett (A1a, A1c, A3; A6 the wave policy) | `Docs/STEAM_BUSINESS_SETUP.md`; `Docs/STEAM_PLAYTEST_RUNBOOK.md` § A6 |
| Steam fee, partner account | Shombith (A1b, A2, A7) | `Docs/STEAM_BUSINESS_SETUP.md` |
| Store page and capsules | Caleb builds, Shombith submits (A5) | `Docs/STEAM_PLAYTEST_RUNBOOK.md` § A5 |
| Playtest child app | Shombith creates, Garrett signs off the name (A4) | `Docs/STEAM_PLAYTEST_RUNBOOK.md` § A4 |
| AI training | Garrett | CLAUDE.md § AI training |

Contacts are in the team's own channels (Discord); this file holds no phone numbers or emails.
