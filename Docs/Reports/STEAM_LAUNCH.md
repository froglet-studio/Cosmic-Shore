# Steam launch — where it stands (report for Shombith and Caleb)

**The same report as a page to share:** [Steam Launch Status](https://claude.ai/artifact/53u11qrp5tgdSm87S1Mj4n)
(private; the owner shares it from the page's Share menu). This file is the one to read.

**From:** a Claude session, 2026-10-14 to 2026-10-25, measured against `bleeding-edge` each time rather than read off
the board. **Branch:** everything described is on `bleeding-edge`; this report doc arrives with PR #1521.
**Status:** the destination is an **invite-only Steam Playtest** — Revision 2's pivot, not the paid Early Access of
Revision 1. The engineering is ahead of the business process. **Nothing has been opened in the Unity editor for this
report, and no Windows player has ever been built.**

## 1. In one minute

- **The clock has not started.** There is no Steamworks partner account and no app id anywhere in the repo — only
  placeholders and the stub ids used in a dry run. Item **H1** (account, $100 Steam Direct fee, tax, banking) is
  unmarked. Every date below counts from the day that fee is paid.
- **The 30-day Steam Direct wait is not the binding constraint.** The chain *capsule art → store page → Valve review →
  the mandatory two-week Coming Soon window* lands around **day 39**, slightly past the fee clock. So commissioning the
  art is as urgent as paying the fee, and starting it after the fee wastes two weeks of a wait already running.
- **Floor about six weeks from the fee; realistically eight to twelve.** The variance is one item: a Windows IL2CPP
  player has never been built or launched.
- **The Steam-side engineering is finished and waiting.** One uploader serving two apps, the Playtest runbook, the
  platform sign-in work, the FMOD credit obligation — all on `bleeding-edge`, all waiting on an app id (§3).
- **Two things went backwards this month**: no CI runner since 2026-10-13, and no QA session since 2026-10-12 (§4).
- **Mobile is not a launch.** iOS is on TestFlight (first build 2026-10-12); Android is on Play **internal testing**
  (build 412826, 2026-10-14, 64-bit only). Neither is a public store release, and the README's badges say otherwise —
  its "Google Play" badge links to itch.io and its "App Store" badge to TestFlight.

## 2. How fast — the critical path

Day 0 is the day the fee is paid. Two chains run in parallel and both land near week six.

| Step | Days | Gate |
|---|---|---|
| Steam Direct wait | 0 → 30 | starts at payment; **not started** |
| Capsule + key art, five sizes | 0 → 13 | one contributor, who also owes the trailer (**H13**) |
| Store page built and submitted | 13 → 20 | **H3**, needs the art |
| Valve page review | 20 → 25 | 3–5 business days (**H5**) |
| Playtest child app review | 20 → 27 | its own simplified review (A4) |
| Coming Soon public | 25 → 39 | **required ≥ 2 weeks before release** |

Earliest grant lands around **day 39**. A review rejection costs another round on top.

Why realistic is longer:

- **No Windows player, ever.** `QA-BUILD-WINDOWS-PLAYER` is still unchecked in the QA backlog. Every defect in that
  chain was invisible in the Editor — the `IL1005` linker failure, the `PauseMenu.Prewarm` crash. It cannot be narrowed
  without building it.
- **No profiling on the hardware floor** (**H8**). Every performance number on record is an Editor number, so the
  system requirements that go on the store page are not yet derived from anything.
- **No load-time target** (**R7**). Publishing it was the deliverable, and it does not exist.

## 3. What is built and waiting on an app id

All on `bleeding-edge`. Most of it carries an editor check that has not been run.

- **One uploader, two apps** (R6, 2026-10-10). `Tools/Steam/upload.sh --target base|playtest`. Each target refuses with
  a message naming the checklist item that creates the missing id; the script refuses outright if the two pairs share an
  id; the build description stamps `[base]`/`[playtest]` beside version and commit so the two apps' build lists cannot
  be confused. Proven against a stub `steamcmd`, never against Steamworks.
- **The Playtest runbook** — `Docs/STEAM_PLAYTEST_RUNBOOK.md`, a sibling of the Revision-1 paid runbook rather than a
  rewrite of it, because Rev 1 and Rev 2 use the same item ids for different things. It covers A4 (the one irreversible
  step is the customer-visible Playtest name), A5 (page live first, grants held at zero), A6 (the nine things a signed
  wave policy must contain, with a template), and Valve's two constraints: Playtest participants cannot review the base
  game, and charging for access in any form is prohibited.
- **Platform sign-in** (2026-10-18) — Steam, Google and Apple behind one abstraction, with the recorded decision that
  Steam is silent, Apple is everywhere and guest is allowed on Steam. **No Steamworks SDK is integrated, by decision**,
  so the Steam path is an interface waiting on one.
- **The FMOD credit obligation is discharged.** A credits manifest carrying both required strings, a credits screen, and
  a release guard that fails any non-development build if the line goes missing. One screen also covers every MIT
  notice the project owes.
- **Commerce de-scoped** (R4) and **PC platform defaults closed** (R5) — 1920×1080, resizable window, the Standalone
  bundle id.
- **Store-page URLs** point at the real domain, and a written report lists eleven places where the privacy policy and
  the code disagree (`Docs/Legal/`).

## 4. What has gone backwards

- **No CI runner since 2026-10-13.** The self-hosted runner was removed under the security rule for a public repo, so
  every Unity tier is skipped and the compile half of the landing guard is dead. **86 pull requests have merged into
  `bleeding-edge` since 2026-10-14 with no compiler verifying any of them.** That risk surfaces all at once in the
  first Windows build — which is also the thing nobody has done.
- **QA stopped on 2026-10-12.** That is the last submitted session. The edit-mode suite was red at that point (2,685
  pass, 35 fail; a later fix branch set a baseline of 23 expected failures). The bug bash (**H11**) is seeded from
  findings that do not exist yet.
- **`claude/*` pull requests get no checks at all.** `unity-ci.yml` skips its resolve job for a `claude/*` head branch.
  Worth knowing when reading a green PR: on those branches, green means nothing ran.

## 5. Open items, and what is asked of you

**Shombith** — the engineering half is yours and it is in good shape. What is left needs a machine, not a session:

1. **Build a Windows player by hand** and launch it: `./Tools/Build/build_windows.sh --version <x.y.z>`, sign in, reach
   `Menu_Main`, open and close the pause menu, read `Player.log` end to end. One afternoon, and it unblocks R3, B7,
   every D item and DoD #3.
2. **Put a CI runner back** on a dedicated isolated machine that satisfies `Docs/BUILD_AND_DELIVERY.md` §10 *Security*.
   One repository variable un-skips the compile tier in all three workflows.
3. **Decide the progression posture.** `ProgressionBackendGate.CloudEnabled` is `false`, so nothing persists between
   sessions, and `DeveloperUnlockGate.DefaultAllUnlocked` is `true`, so nothing is locked. DoD #4 cannot pass in either
   state, and neither has been play-tested.

**Caleb** — the off-machine half is the one holding the date, and none of it leaves a trace in git, so this report
cannot tell whether it has moved:

4. **Has the Steam Direct fee been paid and the app created?** If not, this is the single most valuable hour anyone can
   spend on the launch. Record the app id the moment it exists; it arms `upload.sh` immediately.
5. **Commission the capsule art** (five sizes: 920×430, 462×174, 1232×706, 748×896, 600×900). Thirteen days on one
   contributor, and the chain behind it is what sets day 39.
6. **The content survey, the wave policy and the hosted privacy policy.** The runbook supplies a wave-policy template;
   what remains is choosing the numbers and signing. The privacy policy blocks launch unconditionally.

**Garrett** — nothing here needs a decision from you except whether the Playtest is still the destination, and whether
the paid-EA conversion stays as Revision 1 describes it.

Nothing on the ticket board covers the Steam launch. The one adjacent ticket is **#1515** (`T-20261025-b37f`, Android
build): upload a release App Bundle carrying 32-bit so the two 32-bit testers can install. Open and unclaimed.

## 6. Latest activity (generated)

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-25 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area steam-launch --days 14`._

### Recent changes (23)
- 2026-10-16 `9901474c2` docs(rhino): launch-blocker, speed-tunnel, construction and tail/jet docs — on bleeding-edge, bleeding-edge
- 2026-10-16 `a818a944d` docs(rhino): fleet, system and ability docs to current values (in progress) — on bleeding-edge, bleeding-edge
- 2026-10-14 `9d9e71397` fix(ci): land parts A, C, M of cece/nice-brahmagupta-ojclij on Ys-bleeding-edge — on bleeding-edge, bleeding-edge
- 2026-10-14 `34d902cac` refactor(tooling): reorganise the FrogletTools menu and retire 24 dead tools — on bleeding-edge, bleeding-edge
- 2026-10-14 `bc522bfd5` docs(steam): the internal upload needs --set-live; B5 and B7 run on the Playtest app — on bleeding-edge, bleeding-edge
- 2026-10-14 `34e5d89ff` ci: the AI/replay input-only gate runs in both static jobs — on bleeding-edge, bleeding-edge
- 2026-10-14 `f07076cf6` ci: every asset generator's --check gates both static jobs — on bleeding-edge, bleeding-edge
- 2026-10-14 `01e079bdf` feat(tools): lifeform-crystal twin gains negative-control self-tests and joins both static CI jobs — on bleeding-edge, bleeding-edge
- 2026-10-14 `0edcab974` feat(tools): offline twin of the vessel skimmer audit; DT-002's six hulls classified — on bleeding-edge, bleeding-edge
- 2026-10-14 `58cff71f3` feat(steam): one uploader for two apps; the Playtest runbook (R6) — on bleeding-edge, bleeding-edge
- 2026-10-14 `08e4c7f31` docs(ship): Android ship pass - real-format fixtures technique, harness glob trap, version-stamp row — on bleeding-edge, bleeding-edge
- 2026-10-13 `9178190ec` chore(privacy): turn off Engine Diagnostics; remove the unused notifications package — on bleeding-edge, bleeding-edge
- 2026-10-13 `e0366a488` chore(ios): retire the free-Apple-ID sideload path (ios-unsigned-ipa.yml) — on bleeding-edge, bleeding-edge
- 2026-10-13 `1f7c1df1d` docs(ci): no Unity runner since 2026-10-13; correct the self-hosted runner security rule — on bleeding-edge, bleeding-edge
- 2026-10-13 `dc0613aa2` feat(android): scripted release App Bundle build with a pre-upload Play check — on bleeding-edge, bleeding-edge
- 2026-10-13 `fe17f6d64` chore(playfab): remove the remaining PlayFab leftovers outside Port/ — on bleeding-edge, bleeding-edge
- 2026-10-13 `4e8d7789c` fix(friends): Add Friend reaches a pilot without the #1234 nobody sees — on another branch
- 2026-10-13 `f2b1b343b` fix(party): the bounce notice reaches the menu it lands on — on another branch
- 2026-10-13 `6eec85f81` docs(perf): correct the load-time target's anchors to the shipped assets (R7) — on another branch
- 2026-10-12 `caf4e2dd8` feat(party): an invite you miss is no longer gone (R13 item 2) — on another branch
- 2026-10-12 `613bb9a1a` refactor(amoebius): rename the engine from Prisma to Amoebius everywhere — on bleeding-edge, bleeding-edge
- 2026-10-12 `6b9a0fa65` fix(ios): TestFlight workflow is manual-only — on bleeding-edge, bleeding-edge
- 2026-10-12 `24013fc95` docs(ci): list ios-testflight.yml among the request-file workflows claude/** branches skip — on bleeding-edge, bleeding-edge

### Also in flight on other branches
- `cece/nice-brahmagupta-ojclij` (unattributed, 2026-10-13): docs(plan): what was proven offline, per part, on today's bleeding-edge

### Open tickets (Froglet Tasks)
- None.
<!-- team_report:end -->
