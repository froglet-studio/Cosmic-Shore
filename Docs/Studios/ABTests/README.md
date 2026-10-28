# A/B tests: one decision, two versions, any audience

An A/B test is a small playable page that puts **one decided thing beside its runner-up** and asks one question: *which
did you like better, A or B, and why?* It is the audience-facing sibling of the Vessel Studio. The studio is where the
team tunes and decides. An A/B page is where people outside the decision say whether the team decided well. The
recipe is the `/ab-test` skill.

| Test | Page (source) | Artifact | A/B |
|---|---|---|---|
| Stoat sling, v1 (2026-10-15) | [`stoat-sling/index.html`](stoat-sling/index.html) | https://claude.ai/artifact/5F3i4firjbsqd6ovfny6oG | **Catch sling** (shipped Time mode, 2026-10-14) vs **orbit sling** (round 4, set aside 2026-10-13) |
| Butterfly fold or drop, v1 (2026-10-28) | [`butterfly-drop/index.html`](butterfly-drop/index.html) | https://claude.ai/artifact/7V29goD7bYi3uM23PFrMMJ | **Fold** (shipped Time ability, a teleport along the heading) vs **drop** (new, the tiger moth: fall belly-first, camera rides above) |

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

## Butterfly fold or drop v1

**Why this pair.** Garrett, 2026-10-28: "lets take inspiration from the tiger moth. if this works we will change the name to
tigermoth." A tiger moth escapes a bat by shutting its wings and dropping. B replaces the Fold's teleport with exactly that:
on the press the hull does not fly forward and does not turn, it falls along its own down (the way its Dust-mode dust
rains), and the camera swings up over it so down reads as the new forward. On landing the camera levels out behind it
again. This is not a runner-up the team set aside; it is a new idea put beside the shipped ability.

**What is the same in both.** The trigger (hold, let go), the hull and its turn rates, the camera's follow (offset,
smoothing), the course, the crystals, the dust, the wake and the HUD. Read from the assets:
- `Butterfly.prefab`: cruise 55 u/s, pitch and yaw 45 deg/s, roll 70 deg/s, `restrictedTurnMultiplier` 0.
- `ButterflyFoldAction.asset`: reach 900 u/s up to 1800 u, ghost 1400 u/s, wither 0.22 s, bloom 0.3 s.
- `ButterflyCameraSettingsSO.asset`: follow offset (0, 37.4, -204), follow smooth time 0.2 s, rotation smoothing 5/s.
  The camera step is `CustomCameraController`'s (SmoothDamp position, slerped look, the 50 u teleport snap).
- `ButterflyDustField`: motes fall at 6 u/s and live 1.1 s.

The drop's speed and reach default to the fold's (900 u/s, 1800 u), so a held second takes you as far either way.
The cooldown is 2.5 s in both (the shipped 30 s would allow one use a lap), and that is said on the page.

**What differs.**
- **Fold (A or B):** the shipped behaviour. You stop, wings shut, a ghost glides out along the heading
  (`FoldActionExecutor.ResolveTarget`), the camera frames the ghost (`VesselPlacementView`), and on release the hull
  withers, is written to the ghost and blooms. Roll stays live; pitch and yaw are dead. A crystal the ghost passes is not
  collected (a teleport threads nothing, as in Waystation).
- **Drop (A or B):** you shut your wings and fall along the hull's down at 900 u/s (0.12 s ease in). Nothing rotates. On
  release (or at 1800 u) you brake over 0.25 s while forward speed comes back, then the camera relevels over 0.6 s. The
  camera's follow frame is the hull's rotation pitched nose-down 90 deg, blended in over 0.35 s, so it sits 204 u above
  the hull looking down the fall with the hull's nose at the top of the screen. The drop collects anything it passes.

**Designer settings.** "Designer settings" on the menu (or `#tune`) tunes the drop's proposal numbers: speed, longest
drop, ease in, landing, camera up and back times, camera tilt (0 to 90 deg) and whether the wake lays during the drop.
They stay in that browser and are stored with the vote (`dropTune`), so a vote says what it was cast on.

**Not modelled.** The fold's wormhole pair and its toll; the Mass/Dust mode switch (it flies in Dust mode); the elements
and their level-5 upgrades (Far Fold); the dust's bite on prisms and hearts; the real hull model (a stand-in at the same
span); sound; other vessels; the cell's flora and fauna. The course is a stand-in at cell scale.

**Reading the votes.** One document per voter in `votes`: `{test, pick, mech, why, assign, flew: {fold, drop}: {letter,
secs, laps, best, uses, camPeak}, dropTune, device, at}`. Same rules as the Stoat test (`votes/{self}` at Contributor).

**Checked headless (2026-10-28).** No page errors at 1600x900 or on an emulated iPhone (390 px, no sideways scroll). A bot
pilot that uses inputs only finishes the lap with both: fold 23.7 s (5 uses), drop 25.2 s (5 uses). A scripted 0.8 s drop
from the start fell 779 u straight down (677 u while held, the rest in the landing) with the hull's heading unchanged; by
0.6 s the camera looked straight down (look -0.96 y) with its up on the hull's forward. The relevel blend ends 0.6 s
after landing, and the camera's own rotation smoothing (5/s) brings the view fully behind the hull about 0.5 s later. The drop turns the
camera far more than the fold (peak 193 deg/s against 63 on the bot lap, 1053 deg against 444 in total): the cost of
making down feel like forward, and the first thing to watch in the votes for motion comfort.

**Decision needed (Garrett).** Whether the drop replaces the Fold (and the Butterfly becomes the Tiger Moth); if it does,
whether it keeps the Fold's wormhole pair, whether it lays a wake, and whether it keeps the Fold's 30 s recharge.
