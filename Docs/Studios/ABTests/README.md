# A/B tests: one decision, two versions, any audience

An A/B test is a small playable page that puts **one decided thing beside its runner-up** and asks one question: *which
did you like better, A or B, and why?* It is the audience-facing sibling of the Vessel Studio. The studio is where the
team tunes and decides. An A/B page is where people outside the decision say whether the team decided well. The
recipe is the `/ab-test` skill.

| Test | Page (source) | Artifact | A/B |
|---|---|---|---|
| Stoat sling, v1 (2026-10-15) | [`stoat-sling/index.html`](stoat-sling/index.html) | https://claude.ai/artifact/5F3i4firjbsqd6ovfny6oG | **Catch sling** (shipped Time mode, 2026-10-14) vs **orbit sling** (round 4, set aside 2026-10-13) |

## Stoat sling v1

**Why this pair.** `Docs/Studios/STOAT_MECHANICS_PLAN.md` §1 records the orbit sling as "rejected as built" on
2026-10-13 because players "couldn't see the poles, just pulled right or left". That is a complaint about visibility,
not about feel, and the plan's own P3b says to revive it with a visible placement before deciding. The orbit sling is
also the more familiar racing verb: press and you are already turning, hold longer to turn tighter, let go to fling
out (a drift). The catch sling asks you to drop a hole ahead, fly past it, then time a quarter-turn release.

**What is the same in both.** The course, the hull, the camera, the hole and ring drawings, and every number, all
read from the shipped assets:
- `StoatSlingConfig.asset`: squeeze curve 1.5, ramp 3 s, orbit radius 150 to 40, follow 3/s, radial 5/s, boost 0.25 to 0.9 x
  speed over 1.5 s, holes close at 40 u/s, 12 s life.
- `Stoat.prefab`: cruise 60 u/s, yaw 48 deg/s.

While you circle a hole the camera lifts and widens to keep it in frame, in both variants, so the camera is not what
is being tested.

**What differs.** Only where the hole goes and when it grabs you:
- **Catch (A or B):** the pair is laid 150 u ahead and 45 u to the pressed side. It catches you inside the orbit
  radius, or within 1.5x of it once the hole is 50 deg or more off the nose. The release kick pays in full for
  75 to 105 deg of orbit, and 20% outside that window (`StoatSlingMath.SweetSpot` / `TimedKick`). There is no kick if
  it never caught you.
- **Orbit (A or B):** the hole is laid beside the hull at the orbit radius and you are orbiting at once. The kick is
  the squeeze boost, with no timing.

**Blind.** Which mechanic is A is random per viewer (kept in their browser). The vote stores the letter, the
mechanic and the mapping. The names are revealed only after the vote.

**Not modelled.** The course is flat (the game flies in 3D). Several things are missing: the Stoat's Mass mode
(field trajectory), Time's boost ratio (taken as 1), the black holes' pull on prisms, the lens, sound, and other vessels.

**Reading the votes.** One document per voter in the artifact's `votes` collection: `{test, pick, mech, why, assign,
flew: {catch, orbit}: {secs, laps, best, slings, catches, sweet}, device, at}`. From any Claude session:
`ArtifactData list` on the artifact URL, collection `votes`. Rules: everyone who can open it reads, each person writes
only their own vote (`votes/{self}` at Contributor). Viewers who can only view (outside the organization, public link)
cannot save. Their thank-you card offers **Copy my answer**, a one-line text they can paste back to whoever shared it.

**Checked headless (2026-10-15).** No page errors at desktop size or on an emulated phone, and no sideways scroll at
390 px. A plain bot (no slings) finishes the lap in 28.6 s on both. A scripted right sling: the catch sling caught at
0.14 s and released at 94 deg (sweet spot, +54 u/s); the orbit sling swung at once and flung +41 u/s after 2 s. The page
exposes `window.__abTest` (the same `step` it flies) for these checks.

**Finding, not acted on.** `Stoat.prefab` turns at YawScaler **48** deg/s, but the Stoat studio page uses **120**
(`StoatFlightStudio.html` `SHIPPED.pitchYaw`, its comment says "prefab: pitch/yaw 120"). This test uses 48. The studio
needs to be re-read against the prefab (`/vessel-studio` D1).
