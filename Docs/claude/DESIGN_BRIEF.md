# The design brief: one page before a thing is built

Eight things were built and retired in five weeks (`Docs/SKILL_TRAINING_PLAN.md` 2.3), six of them for a rule the
owner held but nobody had written down. A retired feature costs a build and an undo. This page is written BEFORE a
mechanic, a camera behaviour, a mode rule, a world effect or a screen is built, in the feature's own doc or in the
PR body under **Design brief**, and it is the first step of `/arcadegame`, `/vessel`, `/labmaker`, `/frogma` and
`/world-look`. It takes ten minutes. It is never a ticket and never assigns anyone.

## The template

```
## Design brief: <the thing>, <date>, <branch>

The fundamental it serves      one of Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md's fundamentals, by name
The principles it obeys        ids from Docs/claude/PRINCIPLES.md (python3 Tools/Build/principles.py --area <area>)
The one it comes closest to    the id, and why this design does not break it
What it changes for a player   one sentence: what they see, feel or can do that they could not before
The cheapest prototype         a /labmaker lab, the Vessel Studio page for a vessel, an /ab-test for a choice,
                               a Frogma round for a screen; or "none: built straight in Unity because ..."
Who judges it, and where       the owner of the area (who_works_where.py), in the editor / on a phone / on the sheet
The retire condition           the observation that would mean it was wrong (then the retrospective rule applies)
```

Rules that follow from the plan and the TEAM rules:

- A design that breaks a drafted principle is a proposal to that principle's owner first, not a build.
- A mode never changes the vessel (LOCKED, Shombith, 2026-10-31): a mode's brief lists only track, environment and AI.
- A world fact is shown in the world, in 3D; nothing in the world looks flat (LOCKED, Garrett, 2026-10-18/21).
- The brief names the owner to ASK; it never assigns a ticket or a person.

## The retrospective rule

When a thing is retired or reverted, two lines are added the same day:

1. its brief (or the PR body) gets the line `Retired <date>: <the owner's words>`;
2. `Docs/claude/principles.json` gets the principle that was missing, in the owner's words, with its source line
   (`python3 Tools/Build/principles.py --check` keeps it founded).

That is the only way the ledger grows, and it is how the second violation of a rule becomes impossible.

## A filled example: the camera's drift slide (2026-10-25)

```
## Design brief: the drift slide, 2026-10-25, claude/camera-feel

The fundamental it serves      the camera is a little alive (Yash, 2026-10-25: "not too much, not too fancy")
The principles it obeys        P-camera-05 (skims wiggle, hits shake, drifts and shots are felt, subtle)
The one it comes closest to    none in the ledger on 2026-10-25. The rule it broke was said on 2026-10-27
                               (P-other-04: a drift is felt as a rumble and never moves the hull) and LOCKED on
                               2026-11-02: "disruptive to the players sense of their reference frame, position,
                               speed ... Our game is truly 3d and usually does its best to not have an 'up'"
What it changes for a player   the hull slides off centre during a drift, building in an accelerated way
The cheapest prototype         none: built straight in Unity; a Vessel Studio camera page would have shown the
                               slide to Garrett before the commit
Who judges it, and where       Garrett and Yash, in the editor, flying the Squirrel
The retire condition           a pilot loses their sense of position, speed or orientation during a drift
```

With the ledger as it is today, the same brief would read `The one it comes closest to: P-other-04` and the slide
would have been a proposal to Garrett, not a build. That is the gap the ledger closes, from the second time on.
