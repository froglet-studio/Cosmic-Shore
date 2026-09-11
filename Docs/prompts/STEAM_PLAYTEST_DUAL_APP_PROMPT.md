# Prompt — make the Steam tooling and runbook match the Playtest model

Paste everything below into a fresh session.

---

The milestone changed shape on 31 July and the Steam half of the repository never followed. Every
runbook and every upload script still describes **Revision 1**: a paid Early Access release, on one
app, promoted through `beta` and `default` branches. The actual milestone is a **Steam Playtest** —
a separate free child app attached to the store page, with a Request Access queue, throttled manual
grant waves, and one-to-three compounding friend invites per tester.

That gap has two costs. Checklist items **A4, A5 and A6 have no procedure behind them** at all, and
**B4's upload script can only target one app** when it now needs two.

Read `Docs/STEAM_RELEASE_TASKS.md` (item R6), `Docs/STEAM_BUSINESS_SETUP.md`, `Tools/Steam/README.md`
and `Docs/BUILD_AND_DELIVERY.md` §B4 first.

## Measured 10 Sep 2026 — re-verify before acting

**Nothing in the repository mentions the Playtest child app.** Grepping `Docs/` and `Tools/Steam/`
for "playtest" returns only Revision-1 usages of the word in its ordinary sense:

| Location | What it actually says |
|---|---|
| `Docs/BUILD_AND_DELIVERY.md:113` | `internal` (team) → `beta` (playtesters, used for E7) → `default` (players) |
| `Docs/BUILD_AND_DELIVERY.md:20` | B7 overlay check "needs a real Steam build on the beta branch" |
| `Tools/Steam/README.md:38` | `beta` — "Invited playtesters, password protected" |
| `Docs/STEAM_BUSINESS_SETUP.md:171-173` | Promote to `beta` for the closed playtest, then mark for review |

Those describe a **password-protected branch on the base app**, which is the Revision-1 closed
playtest. It is not the Steam Playtest feature, it is not a child app, it has no Request Access
queue, and it cannot issue friend invites.

**The upload script takes exactly one app:**

```
Tools/Steam/upload.sh:49   : "${STEAM_APPID:?...}"
Tools/Steam/upload.sh:50   : "${STEAM_DEPOTID:?...}"
Tools/Steam/upload.sh:96   sed -e "s|{{APPID}}|$STEAM_APPID|g" -e "s|{{DEPOTID}}|$STEAM_DEPOTID|g"
```

with `templates/app_build.vdf` and `templates/depot_build.vdf` rendered from that single pair. The
script is deliberately unarmed — it refuses until the env vars are set, and it confirms the app id
interactively before uploading — and that safety is worth preserving exactly as you extend it.

## What to build

**1 · A second target in the uploader.** Revision 2's B4 is "targeting both appIDs: base app
default for the future paid build, Playtest depot as the live invite channel, internal branch for
the team." The clean shape is a **named target** rather than a second pair of env vars:

```
./upload.sh --target playtest   # STEAM_PLAYTEST_APPID / STEAM_PLAYTEST_DEPOTID
./upload.sh --target base       # STEAM_APPID / STEAM_DEPOTID
```

Two properties of the current script must survive: the **refusal** when ids are unset (with a
message naming which checklist item creates them), and the **interactive confirmation** that the
operator typed the right app id. Getting those backwards on a Playtest upload publishes a build to
the wrong audience, which on this milestone means publishing to people who can post reviews.

Build description should keep carrying the version and commit from `build_manifest.txt` — that is
what makes a Steamworks build row traceable to a tag — and should name the target so the two apps'
build lists are not ambiguous.

**2 · The Playtest half of the runbook.** Add to `Docs/STEAM_BUSINESS_SETUP.md` (or a sibling —
your call, but say which and why) the procedure for:

- **A4** — creating and configuring the child app. Flag hard that the **customer-visible Playtest
  name cannot be changed after creation**; that is the one irreversible step in the workstream.
  Request Access signup enabled, friend invites at 1–3 per tester, grant cadence set to **manual
  waves** rather than automatic.
- **A5** — the Coming Soon page with the Playtest signup section visible. Note the ordering that
  actually matters: every week the page is live earns wishlists *and* grows the signup queue while
  grants are still held at zero.
- **A6** — what a signed wave policy has to contain: wave sizes, weekly caps, and pause criteria
  tied to capacity and defect telemetry. Steamworks can pause grants and invites per wave
  immediately; the policy exists so that lever gets pulled on a rule rather than on a feeling.
- The **branch model under two apps** — what `internal` / `beta` / `default` now mean when the
  invite channel is a different app id entirely. This is where the current doc is most misleading.

**3 · Two constraints from Valve that belong in the doc, not in a chat message.**

- **Playtest participants cannot post Steam reviews**, and Playtest metrics never touch the base
  app. This is the entire reason the milestone has the shape it does — Definition of Done #7 is
  satisfied by configuration, not by anything in the build.
- **Valve prohibits charging for Playtest access in any form.** F3 already flags this; make sure
  the business runbook repeats it where someone deciding a wave policy will see it.

## Constraints

- **Do not delete the Revision-1 branch material.** The base app and its `beta`/`default` branches
  are still how the *paid* conversion ships; that content is early, not wrong. Re-frame it as the
  paid path and add the Playtest path beside it.
- **Arm nothing.** No app id, depot id or credential goes in the repository. The script must stay
  refusing-by-default, and the doc should say which checklist item produces each id.
- Keep `upload.sh` POSIX-ish bash with `set -euo pipefail` as it is now; do not rewrite it in
  Python for this.
- If you change the VDF templates, keep them templates — the token substitution is what lets one
  script serve both apps.

## Definition of done

1. `./upload.sh --target playtest` and `--target base` both render correct VDFs, both refuse
   cleanly with unset ids, and both still demand interactive confirmation of the app id.
2. A reader who has never seen the Playtest feature can execute A4, A5 and A6 from the doc alone,
   and knows which step is irreversible.
3. The branch model is stated once, unambiguously, for two apps.
4. Nothing in the repository contains a real app id, depot id, or credential.
5. `Docs/STEAM_RELEASE_TASKS.md` R6 is ticked and its dependants (H3, H4) note that the runbook now
   exists.
