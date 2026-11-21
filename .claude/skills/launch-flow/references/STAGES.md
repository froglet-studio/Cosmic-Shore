# Launch stages: the phase plan every launch session works inside

Shombith's launch phase diagram (2026-11-17), copied here stage by stage. The picture is
`Docs/LAUNCH/launch_stages.png`; when the two disagree, the picture wins and this file is fixed to match it.

The launch is six stages, each closed by a **stage gate** (a measured number, not a date). Every stage runs the
same four branches; what changes from stage to stage is how many vessels each branch carries, who gets access to
the Master and Release builds, and which data the gate is read from.

## The key (the diagram's colours)

| Shape | Means |
|---|---|
| Red box | a **branch** |
| Yellow circle | a **build** (Drive builds, an access tier) |
| Green circle | **feedback** |
| Teal circle | **testing** (bug testing) |
| Blue circle | **data** (instrumented, activation, retention, referral) |
| Purple box | a **stage gate** |

## The branch pipeline (the same in every stage)

`Bleeding Edge` -> `Development` -> `Master` -> `Release`. Work flows down only (`GIT_RULES.md`).

- **Bleeding Edge** and **Development** both feed **Drive builds** (the internal builds on Google Drive,
  `/androidbuild`), which feed **Prototype feedback**, **Bug testing** and **Instrumented data**. Bleeding Edge and
  Development also go straight to Prototype feedback and Bug testing (in Phase 1 the Bleeding Edge line is marked
  "in Unity": the team plays it in the editor).
- **Master** feeds an **access tier** (Alpha or Pre-release access), which feeds that stage's outside feedback
  group and **Instrumented data**.
- **Release** (from Phase 2 on) feeds the public tier (Beta access, $15 early access, Free to play), which feeds
  the stage's **activation / retention / referral data**. That data is what the next gate is read from.

## The stages

| Stage | Bleeding Edge | Development | Master | Release | Master's tier -> feedback | Release's tier | Data the gate reads |
|---|---|---|---|---|---|---|---|
| **Beta Phase 1** | 21 vessels | 6 vessels | 6 vessels | (none) | Alpha access -> Friends, Family and Tadpoles feedback | (none) | Instrumented data |
| **Beta Phase 2** | X vessels | Y vessels | Y vessels | Y vessels | Pre-release access -> FFT (Friends, Family and Tadpoles) feedback | Beta access: limited Beta keys, one at a time, handed out from the FFT feedback | Activation data |
| **Beta Phase 3** | X vessels | Y vessels | Y vessels | Y vessels | Alpha access -> Tadpole feedback | Beta access: limited Beta keys, one at a time, handed out from the Tadpole feedback | Activation / retention / referral data |
| **Early access** | X vessels | Y vessels | Y vessels | Y vessels | Alpha access -> Tadpole feedback | **$15 early access** | Activation / retention / referral data |
| **Full release** | X vessels | Y vessels | Y vessels | Y vessels | Alpha access -> Tadpole feedback | **Free to play** | Activation / retention / referral data; Instrumented data leads on to Game 2 |
| **Game 2 and beyond** | | | | | | | Game 2 design; everlasting content releases, bug fixing and balance updates |

## The gates (what moves the launch to the next stage)

| Gate | Closes | Opens | Passes when | Read from |
|---|---|---|---|---|
| **Gate 1** | Beta Phase 1 | Beta Phase 2 | **Co-op repeat retention over weeks with the same experience** | Phase 1 instrumented data |
| **Gate 2** | Beta Phase 2 | Beta Phase 3 | **X% of new users play again after 1 week** | Phase 2 activation data |
| **Gate 3** | Beta Phase 3 | Early access | **X% of users say they would give a 5-star review when surveyed** | Phase 3 activation / retention / referral data (and the survey) |
| **Gate 4** | Early access | Full release | **Feature complete: only content is added from here** | Early access activation / retention / referral data |

Full release has no gate: its instrumented data feeds Game 2 and beyond.

## What a launch session does with this

- **Name the stage** the work is in, and the gate it moves toward. A blocker that does not serve the current stage
  or its gate is ranked below one that does, and is said to be for a later stage.
- **Never pass a gate on a session's say-so.** A gate passes when its number is measured from its data source and
  Shombith calls it. A session reports the measurement (or that nothing measures it yet), nothing more.
- **Feature work stops at Gate 4.** From Early access on, a launch session adds content, fixes bugs and balances;
  a new feature is a decision for Shombith first.
- **Access tiers are outward-facing.** Handing out Alpha, Pre-release or Beta access, a Beta key, or opening $15
  early access or Free to play reaches people outside the team: the session prepares it and the person presses it.

## Current stage

**Beta Phase 1** (Shombith, 2026-11-21: "beta phase 1, the phase we are in right now"). Update this line, with the
date and who said so, when a gate passes.

## A build round (Beta Phase 1)

What Shombith set for this phase (2026-11-17 and 2026-11-21). A launch session runs a round in this order.

**Source:** whatever is on `bleeding-edge`. Every build in a round is made from the same commit.

**Four builds, four destinations:**

| Build | Goes to | How (the way the last builds were made) | Who presses the last button |
|---|---|---|---|
| Windows | **Discord**, as a download link (no Steam yet) | Unity on Shombith's PC, IL2CPP Release player from the build profile (`Docs/BUILD_AND_DELIVERY.md`), zipped and uploaded to Drive | Shombith posts the link in Discord by hand |
| iOS | **TestFlight** | **No Xcode on Shombith's PC**: Unity on his PC exports the Release Xcode project (Development Build off), zipped to a share link; `ios-testflight.yml` (Actions, run by hand) compiles, signs and uploads it on a GitHub-hosted Mac (`Docs/IOS_BUILD.md` §2 Path C; secrets in the `testflight` Environment) | Shombith runs the workflow and approves the Environment |
| Android | **Google Play internal testing** | Unity on Shombith's PC builds the `.aab` (the `/androidbuild` recipe, but an App Bundle for Play); no Play upload automation exists yet, so the `.aab` is uploaded in Play Console | Shombith uploads in Play Console |
| WebGL | **The Toybox page** (www.froglet.games/play) | Unity on Shombith's PC builds WebGL; `toybox-publish.yml` stages it on R2 and promotes it (`Docs/WEBGL_TOYBOX.md`) | Shombith runs the promote step |

All Unity builds run on **Shombith's computer**: a cloud session has no Unity editor and no signing keys. A session
prepares everything up to each button (the version bump, the preflight checks, the exact steps and the copy-paste
prompts) and hands it over.

**One version for the round (cross-play):** players on all four builds play together, so a round carries one
`bundleVersion` and one `NetworkConfig.ProtocolVersion` on all four, and the console's `status/versions` pin holds
them. Before the round:
- **Bump `bundleVersion`** every round. A new feature set bumps the minor (Voyages joined `bleeding-edge`, so the
  2026-11-21 round moves off `0.2.0` to the next minor); a fix-only round bumps the patch.
- **Raise the store build numbers** above the last upload to each store: the iOS build number above the last
  TestFlight upload, `AndroidBundleVersionCode` above the last Play upload. A store refuses an equal or lower number.
- Bump the protocol version only when the network contract changed (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`).
- The bump is one commit on `bleeding-edge`, made before any of the four builds starts.

**Learn from every round:** each build thread writes down what broke or slowed it, and the round ends by folding that
into this file and `SKILL.md`, so the next round needs fewer steps.

## Settled for now (Shombith asked for the recommended option, 2026-11-21)

- **No Release branch in Beta Phase 1.** The diagram gives Phase 1 none, and this phase builds from `bleeding-edge`.
  It is created from `master` on the day Beta Phase 2 starts; on that day `master` becomes the Alpha / Pre-release
  tier and `release` becomes "what players get", and `GIT_RULES.md` and `Docs/BUILD_AND_DELIVERY.md` change in the
  same commit. Until then those two docs stand as they are.
- **The X and Y vessel counts and the X% thresholds stay blank** until Gate 1 is near: nothing in Beta Phase 1 reads
  them. A session never picks them; it asks Shombith when Phase 2 planning starts.
- **Gate 1, proposed measure** (Shombith confirms before anyone reports against it): of the Beta Phase 1 testers, the
  share who play a co-op match (two or more people in one party) in each of three weeks in a row on the same build
  line. It needs the analytics event for a party match; until that exists, a session says Gate 1 is not measured.
- **Milestones on the stages:** the mobile release (when activation is ready) and Steam keys (March 2027, before GDC)
  are read as Beta Phase 2 and 3 work, where Release and Beta keys first appear. Confirm when Phase 2 is planned.
