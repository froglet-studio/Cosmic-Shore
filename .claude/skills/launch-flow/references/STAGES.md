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

**Beta Phase 1** (assumed 2026-11-17 from the diagram, which shows Phase 1 with today's counts: 21 vessels on
Bleeding Edge, 6 on Development and Master; Shombith confirms). Update this line, with the date and who said so,
when a gate passes.

## Not decided yet (Shombith's calls; do not fill them in)

- The **X** and **Y** vessel counts for Phase 2 onward, and the **X%** thresholds of Gates 2 and 3.
- How Gate 1's "co-op repeat retention over weeks" is measured (which event, how many weeks).
- **The Release branch does not exist yet** (the repository has `bleeding-edge`, `development`, `master`, and the
  `build/*` branches). It is first needed in Beta Phase 2. Creating it, and whether `master` stops being "what
  players get" (`GIT_RULES.md`, `Docs/BUILD_AND_DELIVERY.md`) and becomes the Alpha / Pre-release tier the diagram
  shows, is a decision for Shombith, not something a session does.
- Where the platform milestones sit on the stages (`Docs/LAUNCH/READINESS.md` §11: mobile first when activation is
  ready; Steam keys by March 2027, before GDC). Activation data is first read in Beta Phase 2.
