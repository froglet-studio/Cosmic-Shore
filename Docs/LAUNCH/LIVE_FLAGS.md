# Live flags: remote kill switches and the minimum-version gate

**One flags document, kept in git, uploaded with the Cloud Code scripts, read by every build once after sign-in.** It
turns server features off without a store release, walls builds that are too old, and can carry a short notice. No new
package: it is a Cloud Code script, not Remote Config (Shombith, 2026-11-05, `READINESS.md` §11). It closes launch
blockers **#11** (remote kill switches) and the second half of **#6** (a minimum-version gate with an update screen) in
`Docs/LAUNCH/READINESS.md` §10. The first half of #6, the protocol version in session data, is separate work.

**Owner:** Shombith (UGS and the backend). Yash does the Cloud Code work. Every deploy goes through Shombith first.

| Piece | Where |
|---|---|
| The flags document (the only place a flag is set) | `Tools/UGS/CloudCode/launch-flags.js`, `FLAGS` at the top |
| Its tests, run before every deploy and on every pull request | `Tools/UGS/CloudCode/launch-flags.test.cjs`; `.github/workflows/launch-flags-check.yml` (status id `launch-flags`) |
| The client: parse, version compare, fallback, kill-switch rule | `Assets/_Scripts/System/CloudData/LaunchFlags.cs` |
| The fetch after sign-in (through `UgsRequestPolicy`) | `Assets/_Scripts/System/CloudData/LaunchFlagsService.cs`, registered in `AppManager` |
| The update screens and the notice | `Assets/_Scripts/UI/Screens/UpdateNoticeOverlay.cs` |
| Every word on those screens, and how long a saved answer stays good | `Assets/Resources/LaunchFlagsConfig.asset` (`LaunchFlagsConfigSO`) |
| EditMode tests | `Assets/_Scripts/Tests/Editor/LaunchFlagsTests.cs` |

## 1. What each flag does

| Flag | Values | What the game does |
|---|---|---|
| `schema` | `1` | Anything else and the game ignores the whole document (keeps its saved answer, else the default). |
| `minimumVersion` | semver, e.g. `0.3.0` | A build whose `Application.version` is below it shows the blocking **update required** screen: why (in the danger colour), the version to get, **Update** (opens the platform's link) and, on desktop only, **Quit**. It cannot be closed, keeps the pad on its own buttons and stays across scene loads, so no menu and no matchmaking can be reached behind it. `0.0.0` lets every build in. |
| `recommendedVersion` | semver, at least `minimumVersion` | A build below it (and not below the minimum) shows a dismissible **update available** notice once per run. |
| `updateUrls.windows` / `.ios` / `.android` | an https link (a store's `?id=<package>` is the one query allowed), or `steam://`, `itms-beta://`, `itms-apps://` | Where **Update** goes. Windows and iOS point at the website until the Steam app id and the new TestFlight public link exist. An empty link hides the button. |
| `killSwitches.economy` | `true` / `false` | `true` turns off the server-owned wallet (`ServerEconomyGate`): the game keeps the client-written wallet, as before the gate. The server's wallet is untouched; when the switch goes back to `false` the next server answer replaces the local balance. |
| `killSwitches.scores` | | `true` turns off the server-checked weekly leaderboard (`ServerScoreGate`): times post directly again. **Once `AccessControl/project-policy.json` is applied, direct posts are refused, so killing `scores` then stops weekly times posting at all.** Remove the policy too if times must keep posting. |
| `killSwitches.matchReports` | | `true` stops match start/end reports (`MatchReportGate`): `MatchReporter` is not attached to new matches. |
| `killSwitches.trust` | | `true` stops asking for the trust record (`FairPlayTrustGate`): everyone joins the one presence lobby and posts to the public boards; no fair-play notice is shown. |
| `killSwitches.entitlements` | | `true` turns off server-owned episode tokens (`ServerEntitlementGate`): the local wallet answers. |
| `disabledModes` | `GameModes` names, exact case | **Read, parsed and kept, but nothing uses it yet** (`LaunchFlags.IsModeDisabled`). The arcade grid has no card-availability path to hang it on: `ArcadeExploreView` filters by element and vessel only, and the arena grid, Home's Up Next, the weekly challenge and the Maelstrom pool each reach modes their own way. Hiding a mode needs a decision on every one of those doors (the menu owners), not one filter. |
| `message.title` / `.body` | one line ≤ 80 / text ≤ 400 | A notice shown once per distinct message (the device remembers the last one shown). An empty title shows nothing. The words come from the flags, so they are authored in git like any other player-facing text. |

**A kill switch only ever turns a feature OFF.** Each gate reads `IsActive => LaunchFlags.Gate(Enabled, LaunchKillSwitch.X)`:
the compiled `static readonly bool Enabled` AND not killed. A gate a build shipped off (all five today) stays off
whatever the flags say; turning a feature on is still a build change (`Tools/UGS/README.md`, "Switching on ...").

## 2. When the flags are read, and what happens offline

1. **Before the first scene**, the last answer saved on the device is put in force if it is at most
   `cacheMaxAgeHours` old (72 by default, in `LaunchFlagsConfig.asset`); otherwise the **safe default**: nothing killed,
   every version allowed, no message.
2. **After UGS sign-in**, `LaunchFlagsService` calls `launch-flags` once, through the shared `UgsRequestPolicy`
   (back-off, retry budget). A good answer is put in force at once and saved with its time.
3. A failure (offline, Cloud Code down, a bad answer) changes nothing: the saved answer or the default stays.

**Only this run's server answer can raise the update wall or the notices.** A saved answer applies its kill switches
but never walls: a player who is offline is never locked out of offline play. A flag change reaches a player at their
next sign-in (the next launch, or a reconnect that signs in again); a running session is not re-polled.
The browser Toybox never signs in, so it never reads the flags.

## 3. How to change a flag

1. Edit `FLAGS` in `Tools/UGS/CloudCode/launch-flags.js` on a branch. Run
   `node Tools/UGS/CloudCode/launch-flags.test.cjs`: it must print `14 passed`. It refuses a version that is not semver,
   a recommended version below the minimum, a kill switch the game does not know (or one left out), a mode name
   `GameModes.cs` does not have, an http or query-string link, and any field it does not know.
2. Open a pull request into `bleeding-edge`. **Launch flags check** runs the same tests; get Shombith's review.
3. After merge: GitHub > Actions > **Deploy UGS scripts** > Run workflow on `bleeding-edge`, environment
   **development**. `deploy.sh` tests again before it uploads.
4. Verify on development: Dashboard > Cloud Code > Scripts > `launch-flags` > run it, and read the answer. Then start a
   development build (it signs in to development) and check the effect (the editor checks in
   `Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Live flags").
5. Production: run **Deploy UGS scripts** again with **production**. Once `ugs-deploy` has its `ugs-production`
   Environment with required reviewers (READINESS #7), this step waits for a reviewer's approval.

`deploy.sh` uploads every script together, so **any** UGS deploy (an economy fix too) also uploads the flags exactly as
they are on `bleeding-edge`. Keep `bleeding-edge`'s flags the ones you want live.

A new kill switch: add it to `KNOWN_KILL_SWITCHES` and `FLAGS.killSwitches` in the script, to `LaunchKillSwitch` in
`LaunchFlags.cs`, and give its gate an `IsActive` that reads it, in one pull request (the test checks all three).

## 4. How to roll back

The flags are only ever what `launch-flags.js` says on the branch that was deployed, so a rollback is a revert:

1. `git revert <the flag commit>` on a branch, pull request, merge (or edit the value straight back).
2. Run **Deploy UGS scripts** with development, check, then production.

A wall raised by mistake (a `minimumVersion` too high) is undone the same way; players see the change at their next
sign-in. To clear every flag at once, set the document back to the defaults above (`0.0.0`, every switch `false`, no
modes, empty message) and deploy. Deleting the script in the dashboard also works in an emergency: every build then
keeps its saved answer for up to `cacheMaxAgeHours` and falls back to the safe default after that.

## 5. Proof

- `node Tools/UGS/CloudCode/launch-flags.test.cjs`: 14 passed. Negative controls (the real file broken, the test run,
  the file restored): `minimumVersion: "1.0"` fails with "minimumVersion is not semver"; `econmy: false` fails with
  "unknown kill switch "econmy"" and "killSwitches.economy is missing"; `disabledModes: ["SkimRacer"]` fails with
  ""SkimRacer" is not a GameModes member".
- `bash Tools/Build/unity_refcompile/run.sh`: RESULT OK (player config).
- Not run in the Unity editor; the EditMode tests have not run. The checks are in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Live flags".
