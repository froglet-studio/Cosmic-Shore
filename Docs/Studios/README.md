# Studios — web pages for vessel design decisions

A studio is a single HTML page that flies or shows a design **before** it is built in Unity, so a
decision is made by looking and playing rather than by reading numbers. It is a **reader** of the
shipped numbers, never their authority: every constant in it is copied from an asset or a class and
named after it, and when the asset changes the page must follow (or say on screen that it differs).

| Studio | Live page (decision log on) | Repo copy |
|---|---|---|
| **Vessel Studio** (hub: Squirrel Studio v1 + Stoat) | https://claude.ai/artifact/EJYgDToG9R2eLzupaQpLgN | `VesselStudio/` |
| Stoat Flight Studio (round 15) | https://claude.ai/artifact/8Wvnsx3gxJXXMNUCoyyuEt (new artifact, its own decision log; rounds 1–14 and their log: https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc) | `StoatFlightStudio.html` |

**Sim lab results and the plan:** `STOAT_SIM_LAB_PLAN.md`.

**The Vessel Studio** (pick a vessel, its studio opens; web, Windows through Prisma's STUDIOS page, Android; Squirrel and Stoat first): `VesselStudio/` and the plan `VESSEL_STUDIO_PLAN.md`.

**Next: Prisma.** `PRISMA_WORMHOLE_SESSION_PROMPT.md` is the prompt for an engine session that makes the
Stoat and both pair styles flyable, inspectable and swappable in Prisma (`Port/`).

## Stoat Flight Studio (round 15 — the field trajectory)

Live: https://claude.ai/artifact/8Wvnsx3gxJXXMNUCoyyuEt (a new artifact, published 2026-10-09; the round-14 artifact is outside this login's organization, so this
one starts its own decision log).

**Sling tab ▸ Field trajectory** (settings in the new **Field** tab). A second way to fly the pair, beside the dipole sling.
Everything in it is **lab-only**: the game has no such mode, and every `ft*` row is a proposal, not a shipped number.

- **No catch radius and no hold circle**, and neither of their circles is drawn. The pair is only a field equation: the
  black hole's pull GM/(r − r<sub>s</sub>)² (Paczyński–Wiita, as on the prisms) and the white hole's softened push, capped
  at the Dipole tab's force ceiling and fastest bend. It bends the nose and speeds you up or slows you along it; the
  engine pulls that gravity speed back toward cruise at `ftGrip` /s.
- **Press** lays the pair `ftAhead` ahead, the black hole on the trigger's side. The squeeze, read until you let go,
  sets its strength (the Dipole tab's curve). It stands `ftStand` s whatever the trigger does, then closes and
  annihilates. A new press replaces it.
- **The line** is your path on your current inputs (throttle, the steering you hold × `ftSteer`), run with the flight's
  own step from the hull and drawn from `ftNose` ahead of the nose, for `ftLength` u. It is drawn on the HUD after the
  lens, so the black hole does not bend or double it. Colours: cyan while open, and **lime** in two cases:
  - it comes back within `ftMargin` of itself at least `ftMinLoop` u further on;
  - it goes **through the wormhole**: heading into the black hole, the path follows you out of the white hole the way
    the flight carries you (Portal tick on). It is point-reflected, keeps the gravity speed it had before the dive, and
    is drawn with a ring where you go in and a double ring where you come out.

  While the line is lime the engine runs at `ftBoost` × (3). The boost arrives at `ftRise` /s and fades over `ftFade` s
  once the line stops being lime.
- **Scan headings** (Field tab): the share of headings within ±60° of the black hole whose path closes, by squeeze and
  distance, with three controls that must read 0%. **Score the field** races the field against the dipole and against
  no sling, and opens the scorecard window.

**Defaults the lab chose** (each one a row, each one measured below, each one open to the designer):

| Row | Default | Why |
|---|---|---|
| `ftBoostInPath` | 0: the path is flown at the engine speed | At 1 (the literal reading) the boost straightens the loop that earned it within about 2 frames. Lime then flickers: 3.8% of the race lime against 14%, with the most loop onsets per minute (37.8). |
| `ftHole` | 1: a pass through the wormhole is lime | **Your decision applied** (2026-10-09): "heading into an attractor should launch you out the repulsor … boost you through it very quickly with a lime path". Flown straight at the black hole from 140–210 u, the line is lime for the whole approach, and you are through in 0.68 s (full squeeze) to 1.18 s (light squeeze) at a top speed of 242–355 u/s. You come out just outside the white hole's horizon. |
| `ftGrip` | 0.5 /s | At 2 (the dipole's grip) a full squeeze swallows every heading from 300 u (0 loops). At 0 (pure gravity) a pass from far away loops only at the edge of capture. At 0.5 every squeeze has a looping band. |
| `ftSteer` | 1: the held steering is in the path | As written. See **Decision needed**. |

**Heading scan** (final defaults: the share of 121 headings that are lime, the band they fall in, and how many of them
loop or go through the wormhole; 0° = dead at the black hole):

| Squeeze (strength) | from 150 u | from 300 u | from 500 u |
|---|---|---|---|
| 0.25 (S 1.94) | 8% (−5°…4°; 3 loop, 7 through) | 5% (−2°…4°; 3, 3) | 2% (0°…1°; 0, 2) |
| 0.5 (S 4.57) | 31% (−15°…39°; 25, 13) | 17% (−7°…20°; 12, 8) | 8% (−3°…12°; 6, 5) |
| 1 (S 12) | 74% (−38°…51°; 33, 68) | 38% (−19°…26°; 22, 34) | 17% (−4°…15°; 5, 19) |

A path can go through and then loop, so loop + through can exceed the lime count. Controls: no pair 0%, the white hole
alone 0%, the pair beyond the path's reach (920 u) 0%. So the request holds:
- aim into the black hole and you are launched out of the white hole on a lime line;
- aim past it from outside the field and some headings close a loop. The band is a few degrees wide from 500 u and wide
  close in.

**Field scorecard** (Score the field; skilled AI at squeezes 0.5 and 1, rookie = sloppiness 0.8 on three seeds, course
seed 7, intensity 2; 19.3 s headless). The AI only lays a pair on the side of the turn and flies for the ring. It never
hunts for a loop or aims for the black hole.

| | circuit 2 laps | hairpins 2 laps | avg speed | top | time lime | lime onsets / min | rookie |
|---|---|---|---|---|---|---|---|
| No sling | 2:06.7 | 1:22.1 | 54 | 60 | 0% | 0 | — |
| Dipole · Balanced | 1:44.3 | 1:50.7 | 81 | 160 | 0% | 0 | 1:38.4 |
| Field · steering only (no pair) | 1:48.2 | 1:06.8 | 64 | 155 | 8% | 11.6 | — |
| Field · steering only, not in path (control) | 2:06.7 | 1:22.1 | 54 | 60 | 0% | 0 | — |
| **Field · pairs** | **1:28.3** | **1:02.1** | 80 | 303 | 18% | 18.7 | **1:37.8** |
| Field · pairs, steering not in path | 1:39.8 | 1:19.8 | 71 | 347 | 6% | 3.3 | 1:55.7 |
| Field · pairs, path at the boosted speed | 1:41.6 | 1:11.4 | 68 | 321 | 4% | 39.0 | 1:49.4 |

The control row equals No sling to the millisecond, as it must: no pair and no steering in the path, so nothing can
turn lime.

**Decision needed (designer).**

1. **Steering alone boosts.** With the held steering in the path (`ftSteer` 1, as asked), holding a full turn draws its
   own circle, about 180 u round at cruise. That is shorter than the 600 u path, so the line is lime and you fly at 3×.
   Measured: a held full yaw for 5 s was lime 100% of the time at 179 u/s with `ftSteer` 1 or 0.5, and 0% at 60 u/s with
   `ftSteer` 0. The "steering only" row above beats the dipole on the hairpins without ever laying a pair. Options:
   take steering out of the path (`ftSteer` 0; the field rows then lose most of their lime: 4% against 14%), shorten
   the path so only tight loops close (`ftLength`; at cruise a full-stick circle closes below about 180 u), or keep it
   as a skill.
2. **Is the path flown at the boosted speed?** (`ftBoostInPath`; the literal reading flickers, numbers above.)

The wormhole question (`ftHole`) is decided: through the wormhole is lime.

**Checked** (headless Chromium, SwiftShader):

- **Decision log not read this round.** The published artifact belongs to an organization outside this login, so
  `ArtifactData` was refused. Any decisions logged there since round 14 are not applied here.
- `verify_lab.cjs` PASS on `StoatFlightStudio.html` and on the hub copy `VesselStudio/stoat.html`. Its `--self-test` passes all
  six planted defects, including the new reload check (L-GEN-4).
- The page is now on the labmaker contract: `window.__lab` (= `__stoatStudio`) exposes `SHIPPED`, `SPEC`, `reset`,
  `state`, `score`, and a no-argument `runBatch` (L-STU-14).
- Both round-14 layout faults are fixed: the 12 px clip at 1600 × 900 and the header's 103 px sideways scroll on an
  iPhone 13 (L-STU-13).
- **The five-style dipole scorecard is unchanged** apart from one column: every number is byte-identical to round 14
  except "prisms pulled per sling" (see Found).
- **Prediction against flight.** Lay a pair, set the hull on the nearest looping heading, then fly it with no input and
  measure each flown point's distance to the predicted path, over 9 grip × squeeze cases. In 4 cases the distance is
  1.7–3.1 u; in the other 5 it is 11–78 u (measured before the wormhole pass was added; those paths never crossed a
  horizon). The worst case traced (strength 4.6, grip 2) whirled 4.5 u from a 3.1 u
  horizon, where the orbit is chaotic. Running the prediction on the flight's own fixed 4 ms step diverges by the same
  amounts (2.1–82 u), so the divergence is the orbit, not the integrator. Three bugs were found and fixed while getting
  there:
  - the path ignored the engine still spooling up;
  - it was started 8 u ahead instead of drawn from 8 u ahead;
  - it checked the horizon only every 3 u, so a 3 u horizon slipped between two points.
- **Through the wormhole.** For 3 squeezes, aim at the black hole and fly with no input. The line is lime 100% of the
  approach, the flight jumps to the white hole within 0.68–1.18 s, and it comes out 2.2–6.3 u from the white hole's
  centre, against horizons of 2.1–6 u. The drawn line jumps there too.
- All 16 Field-tab sliders have an illustrated tooltip, and none of the 32 previews is blank. There are no console
  errors.

**Found.**

- **The prisms column was never reproducible across page loads**: the prism field was placed with `Math.random`. It is
  now seeded (`mulberry32(20261009)`), so the column moves slightly from round 14 (Balanced 984 → 987) and then stays
  fixed. Every other scorecard column was already deterministic.
- **The studio's white hole lens bends the wrong way for the game.** The lens shader applies the black hole's
  converging bend to the white hole too ("the SAME bending"). The game's `BlackHoleLens.hlsl` now runs the white hole as
  the negated trace, which diverges. So the studio draws a doubled image round the white hole that the game would not.
  Not changed here.

## Vessel Studio in Prisma (2026-10-09, after round 14)

- **Merged:** `vessel-studio` came into this branch: the hub, Squirrel Studio v1, Prisma's STUDIOS page and
  Unity's **FrogletTools ▸ Vessels ▸ Vessel Studio**.
- **Round 14 in the hub:** the hub's Stoat is now round 14 (`VesselStudio/stoat.html`).
- **OPEN IN PRISMA:** the studio as its own app window, reading "Running on Prisma".
- **PLAY IN ENGINE:** the game's own Stoat in Slingshot, one click from the studio.
- **Engine gaps filled:** Prisma could not compile this branch's game code before. The black-hole API gaps
  are now in the engine, and all three Prisma test suites pass.

Test steps: `PRISMA_TEST_STEPS.md`.

## Stoat Flight Studio (round 14 — the course ladder and the editor layout)

**Four intensities that grade the turning** (Course tab; one-click buttons 1–4, each with a description):

1. **Plain circle**: flat, every ring square to the line.
2. **Tilted rings**: about 40° off the line.
3. **Climbs & sharp tilts**: a tighter rolling circle; rings at about 65°, every third one pitched.
4. **Side-on & dives**: rings nearly side-on, plus dive rings.

Measured with the AI, the black hole's share of the turning rises with the level: Balanced's total grip turn
goes from 766° to about 2,300° per race. Comet wins the plain circle by a minute, then fails to finish three
runs in four at intensity 4. The table is in `STOAT_SIM_LAB_PLAN.md` §2.

**A smarter AI**, so the hard levels are flown the way a skilled pilot would:

- it banks until the turn lies off one wing, then lays the pair on that side, so climbs and dives use the
  hole too;
- it aims to pass the black hole on its hold circle until caught;
- it lets go the moment its nose stops closing on the ring.

A rookie (sloppiness 0.5 and up) still steers for the ring before the catch, its classic miss. Flare's
buried boost rose to +3.1 so its peak stays above Comet's, and the slider now reaches 5.

**Editor layout.** The page is one window, like an engine editor; nothing scrolls the page:

- the stage fills the middle;
- the **right dock** has a tab per settings group: Sling, Dipole, Sim lab, Styles, Black hole, White hole,
  Life, Course, Lope, Archive;
- the **bottom dock** holds Runs, Scorecard, Controls, Decisions and About;
- every tab has **⧉ Pop out**, which opens it in a floating mini window you can drag, resize, **⇲ Dock**
  back, or close;
- **the scorecard opens as its own window** when a scoring run finishes (**Show scorecard** reopens it);
- the splitters between the stage and the docks resize them.

Tabs, sizes and open windows are remembered in this browser. Below 900 px wide, the docks stack under the
stage.

**Checked (headless Chromium, 1600 × 900):**

- the stage fills its cell with no page scroll;
- the scorecard window opens after a run;
- a tab pops out, drags and stays after a reload;
- both splitters resize;
- on an emulated iPhone 13 the touch layout and the settings view still work;
- no console errors.

## Stoat Flight Studio (round 13 — fits your device)

The page detects where it is running and shapes its interface to match. Previously a PC saw only a
"Play on phone" button.

**Two questions, answered once at load** (`detectPlatform`, `__stoatStudio.PLATFORM`):

- **Shell: which host runs the studio.** Today it is always `web`. A native host, the coming Prisma light
  studio for every vessel, sets `window.__studioHost = { shell: 'prisma', device: 'pc' | 'phone' }` before
  the page's script runs. The same switch then applies, and the page never sniffs inside a native host.
- **Device: PC or phone.** The page treats the device as a phone when either is true:
  - the browser says it is mobile (`userAgentData.mobile`, a mobile user agent, or iPadOS posing as a Mac);
  - the screen is touch-only (`pointer: coarse` with no fine pointer).

  A touchscreen laptop still has a fine pointer, so it counts as a PC. Tablets get the phone layout.

**What changes with the device:**

| | PC (web) | Phone or tablet (web) |
|---|---|---|
| Stage | gamepad or keyboard; no touch button | opens straight into the touch layout (sticks + LT/RT handles); real fullscreen is requested on the first touch, since browsers need a gesture |
| Start card | full instructions + gamepad status | compact, with Start in the top bar; from the settings view, a "📱 Fly with touch" card |
| Controls card | gamepad / keyboard table | touch table |
| Header | "On this PC …" | "On this phone …" |
| HUD input label | gamepad / keyboard | touch |

**Two ways back from the touch layout.**

- **✕** in the top bar returns to the settings.
- In portrait, the "turn sideways" prompt now has **Settings instead**, so a phone held upright is never
  stuck.

**Override.** The header reads, for example, "Running on **Web · PC (mouse / trackpad)**", with a
**Layout** select: Auto (detected), PC, or Phone. It is remembered in this browser, so you can preview the
phone layout on a PC.

**Checked (headless Chromium):**

- Desktop 1500 × 900 → PC: no touch UI, no phone button, PC text.
- Emulated iPhone 13 (landscape and portrait) and Pixel 7 → phone: touch layout open on load, compact card.
- Portrait shows "Settings instead", which leads back to the page with the Fly card.
- Overriding the layout to Phone on a PC and back to Auto restores the PC layout.
- No console errors; the scorecard numbers are unchanged.

## Stoat Flight Studio (round 12 — the sim lab: an AI flies, you watch)

The full write-up is `STOAT_SIM_LAB_PLAN.md`: results, the game-mode proposal, studio vs Prisma, and the AI
port.

**The Sim lab card.**

- **AI flies the Stoat.** The AI races and restarts on its own, so you can move any slider and watch the next
  lap. It drives only the dual-stick mix and the analog triggers a player has.
- **Cameras.** Chase; Follow (wide and level, like the menu's lava-lamp rig); Free (drag to orbit, wheel to
  zoom).
- **Speed.** 1×, 2× or 4×.
- **AI sliders.**
  - squeeze, release cone and give-up time;
  - when to sling: on a straight (`aiMinRing`) or before a sharp turn (`aiTurn`);
  - steering response;
  - **sloppiness** (`aiNoise`, a rookie).
- **Score all five styles.** Each style at 1 against Balanced and a no-sling baseline, on both courses, with a
  skilled pilot at five squeezes and a rookie on three seeds, all headless in a few seconds. Each style owns
  one column, and the table colours the winner:
  - Comet: average speed;
  - Flare: top speed;
  - Needle: ring error in the hairpins;
  - Anchor: rookie catch rate;
  - Maelstrom: prisms pulled per sling.

**Momentum carry** (Dipole sling card). Part of each release kick is kept as lasting speed:

| Slider | Shipped | Meaning |
|---|---|---|
| `dpCarry` | 0.4 | Share of the kick kept as momentum |
| `dpFlowFade` | 5 s | How long momentum takes to fade |
| `dpFlowCap` | 1.5 × cruise | The momentum ceiling |

The kick scales with the speed you carry, so a chain of slings stacks. A perfect pilot on the shipped tuning
went from 70 to **108 u/s** average. The HUD shows the momentum beside the gravity readout.

**Hairpins course** (Flight card, Course). Eight rings alternate direction 120–190 u apart, then a long straight
runs home. Runs are recorded per course.

**The five styles were retuned against the scorecard.** Each now wins its own column; the new Δ table is in
`STOAT_PLAY_STYLES.md`.

**Found: the tap sling.** The release kick ignores how far round you went, so on a circuit the AI's best play is caught-and-release at 0°. The opt-in `dpKickSweep` slider makes the kick earned. With it on, Balanced slinging loses to not slinging, so the swing has to pay first. Measured and open for a decision in `STOAT_SIM_LAB_PLAN.md` §2.

**Fixed.** Round 11's "Play on phone" button inherited the Stop button's `left`/`bottom` and stretched across
the whole stage on wide screens. The Stop button also sat over the speed readout.

**Verified (headless Chromium, SwiftShader).**

- The scorecard reproduces: 80 races in about 4.5 s, with no console errors.
- Every style wins its own column.
- AI flights for each style were recorded frame by frame through the page's manual clock
  (`__stoatStudio.tick`).

## Stoat Flight Studio (round 11 — play on a phone)

**📱 Play on phone** (top-right of the stage) makes the stage fill the screen. Hold the phone sideways; a
"turn sideways" prompt covers portrait. Where the browser allows it, real fullscreen is requested. Otherwise
the page itself covers the screen.

- **Two thumb sticks** at the bottom, left and right. Each appears where the thumb lands in its half of the
  screen and feeds the same dual-stick mix as a gamepad:
  - both sticks the same way: yaw and pitch;
  - opposite up/down: roll;
  - spread apart or together: throttle.
- **Two trigger handles** at the top corners, for the index fingers. Dragging a handle down sets the
  squeeze; the full travel is a full pull. Lifting the finger releases it. This is the same analog signal
  as LT/RT, so strength, hold time and release behave exactly as on a gamepad.
- **Buttons** along the top: Start (Resume / Again), Stop, Back to the start line, and ✕ to leave.

In play mode the HUD keeps clear of the trigger columns and the button bar, and the HUD's own LT/RT rings
are hidden.

**Checked on an emulated 844 × 390 touch phone:**

- the stage fills the screen;
- the sticks read ±0.71 / +0.54 for a spread-and-push;
- a 60% pull on the RT handle laid a pair on the right at that squeeze, and lifting the finger released it;
- portrait shows the prompt, ✕ restores the page, and there are no console errors.

**Inside the claude.ai viewer,** phones may refuse real fullscreen; the page still fills the frame. For the
cleanest phone test, open `StoatFlightStudio.html` directly in the phone's browser.

## Stoat Flight Studio (round 10 — five types per group)

Each setting group has five one-click **types** above its sliders, plus **Shipped**. A type sets the
whole group's sliders, and the play styles still blend on top of it. Hovering a type opens a card with what
it is, the group's preview as shipped next to the same preview with this type, and every value it sets.
The active type is highlighted, and the decision log records it ("Black hole: the Cinematic type", or
"custom" once a slider has moved).

| Group | The five types |
|---|---|
| Black hole | Pinpoint (tiny, sharp, stretches hard) · Gentle giant (big, soft, far reach) · Cinematic (strong Einstein ring, golden photon ring) · Ravenous (far more pull, prisms from far away) · Minimal (clean disc, little bending, quick settling) |
| White hole | Supernova (blinding solid core) · Ghost (faint, small, weak push) · Twin (the black hole's exact mirror) · Fountain (strong push, throws you clear) · Mirage (all lens, soft core) |
| Birth & annihilation | Snap (pops in and out) · Cinematic (slow, from far beyond the view, majestic wave) · Shockwave (violent wave, shaking view) · Calm (faint, almost no shake) · Ripple pond (slow, wide, long-lasting ripple) |
| Flight & course | Cruiser (slower, gentle, intensity 1) · Racer (faster, intensity 3) · Agile (snappy turns, intensity 4) · Heavy (quick but slow to turn) · Sprint (fast, one lap) |
| Lope | True stoat · Glide · Bounce · Slink · Scamper |

The values are in the studio source (`GROUP_TYPES`). Flight & course types also set the course's intensity
and laps. **Checked offline:**

- every type's values lie within its sliders' ranges;
- each type applies, highlights, and returns to Shipped;
- Sprint switches to one lap at cruise 100;
- none of the 25 type cards renders a blank preview, and there are no console errors.

## Stoat Flight Studio (round 9 — five play styles)

**Play styles.** The rail's **Play styles** card blends the sling and hole settings five ways: Comet
(velocity), Needle (precision), Anchor (forgiving control), Maelstrom (gravity) and Flare (surge). The
sliders elsewhere are now your *base* tuning, and the five weights blend it. A value a style has changed
shows underlined in blue, and its tooltip gives both numbers.

- **Presets** set one style to 1 and the rest to 0.5; **Balanced** returns all five to 0.5.
- **Live** makes a race move the weights:
  - each race starts them at 0.5;
  - element crystals along the course feed one style each: Time → Comet, Space → Anchor, Mass → Maelstrom,
    Charge → Flare;
  - a clean release (out of the grip, nose within 20° of the next ring) earns Needle;
  - unfed weights drift back to 0.5.
- **HUD.** The five weights show as bars on the stage.

The design, the full table of what each style changes, the measured results and the plan for taking it
into the game are in **`STOAT_PLAY_STYLES.md`**.

**Checked offline:**

- the presets measurably differ (table in that doc);
- a Mass crystal moves Maelstrom 0.50 → 0.65, then it drifts back;
- a clean release raises Needle 0.50 → 0.60 and a sloppy one does not;
- all 102 tooltip cards render, with no console errors.

## Stoat Flight Studio (round 8 — birth, annihilation, one pair, Stop)

**A pair's life** (the dipole pair; the **Birth & annihilation** group, 14 settings, each with a tooltip):

- **Birth.** A horizon's radius is proportional to the mass inside it, so the black hole's mass ramps from
  zero over 0.9 s and its horizon, pull, shadow and lens grow with it from a point. A white hole is a black
  hole run backwards, so it arrives the way it will leave, reversed. A light shell converges onto its spot
  from 700 u out over 1.1 s, bending the sky as it passes and washing the view with light where it crosses
  the camera, and its core lights as the shell lands.
- **Annihilation.** When the closing holes' horizons touch, the black hole sinks back to a point over 0.7 s
  and the white hole's light leaves as an outgoing shell. The pair's mass leaves as a **gravitational wave**:
  - a front moving out at 120 u/s, slowed from light speed so it can be watched;
  - a ringdown behind it (period 0.35 s, decay 1 s), weakening as 1/r;
  - strongest sideways to the line the holes closed along and silent along it (sin², as for a head-on
    collision);
  - it is transverse strain, not a pond ripple: the sky is bent, prisms stretch one way and squeeze the
    other, and the whole view stretches and squeezes as the wave passes the camera;
  - the crests and troughs are tinted brighter and darker as a labelled visual aid, which can be set to 0.

**One pair at a time.** A press while a pair exists — held, closing or annihilating — is refused, with an
on-screen notice, in every sling.

**Stop.** The ■ Stop button on the stage (or P) halts the Stoat and pauses the race clock while the world
carries on; ▶ Resume continues. While stopped, LT/RT lays a pair ahead to watch its whole life head-on. In
flight the annihilation usually happens behind you, so this is how to see it.

**Checked offline:**

- In a scripted run, a pair's horizon grows from 0.03 to 3.1 u over 0.7 s, and the white hole lights only
  once its shell lands.
- Presses while a pair is held, closing or dying never make a second pair.
- When the pair meets, its horizon sinks from 3.6 to 0.02 u and both the wave and the outgoing shell appear.
- The Stoat does not move while stopped.
- The sling's grip turns at the same rate as in round 6.
- All 93 tooltip cards render, there are no console errors, and the card fits a 420 px screen.

## Stoat Flight Studio (round 7 — hole settings and illustrated tooltips)

**New setting groups** on the side panel. Each is wired into what the studio flies and draws, so moving a
slider changes the stage as well as the previews.

- **Black hole** — physics: pull per strength (`gmPerStrength`), horizon per strength, the prisms' reach
  (`influenceAccelerationFloor`, `maxInfluenceRadius`), the prisms' pull ceiling and top speed, how
  released prisms settle (`releaseDamping`) and drift home, prism spaghettification (gain and limit), and
  the hull's (`vesselTideScale`). Look: shadow size (× horizon, physical 2.6), lens bend (× Einstein),
  lens reach (`lensRadiusMultiplier`), where the lens starts fading (`lensFadeStart`), and the photon
  ring's glow and width.
- **White hole** — its size and push relative to the black hole, how widely its push is spread, where
  things come out of it (× its horizon), and its look: core brightness (`whiteCoreBrightness`), sky seen
  through the core (`whiteCoreSkyMix`), core size and lens bend.
- **Pair life** — closing speed, closing run-up and the longest hold (moved out of the archived orbit
  group, since the dipole sling uses them).
- The dipole sling gains its two safety ceilings: force (u/s²) and fastest bend (rad/s).

Names in brackets are the game's `BlackHoleConfig` fields. The rest are the studio's own constants made
adjustable. Their shipped values are the numbers the studio used before, so nothing changes until a slider
moves; the sling probe gives the same results as round 6.

**Tooltips.** Every one of the 79 sliders has an ⓘ. Hover the row, focus the slider, or tap the ⓘ (which
pins it; Esc closes it) to open a card with:

- a plain-language description;
- what lowering and raising it does;
- two looping previews of the same moment re-run with only that number changed, at **30%** and **70%** of
  the slider's range;
- the range, the shipped value and the current value.

The previews are small 2D re-enactments built from the studio's own formulas:

| Preview | What it shows |
|---|---|
| sling pass | top-down: press, catch, the grip, release, the pair closing |
| hole view | per-pixel lens: shadow, Einstein ring, photon ring, white core |
| prisms | pulled, spaghettified, through the pair, then released |
| hull stretch | the hull stretching past a hole |
| pair life | the closing gap graph, and the hold cap |
| graphs | the squeeze curve and the key ramp |
| flight | turning circle, roll, speed |
| lope | the bounding body, side view |
| archived | the orbit sling; the crystal pair's life, felt pull, lens and layout |

Where a number only matters in an extreme case (the force ceiling, the reach limit, the ring), the card
says how its preview is staged so the difference is visible. Checked offline: every slider has a card,
none of the 158 previews is blank, there are no console errors, and the card fits a 420 px screen.

## Stoat Flight Studio (round 6 — strength and hold)

**The page.** The dipole sling is the only thing on show. Round 4's orbit sling and charming-cerf's crystal
pair (with the side-by-side compare) moved into a closed **Archive** card; the crystal pair is never the
remembered default. The game still runs the orbit sling; this round is the studio's.

**The sling.** One trigger, two inputs:

- **Squeeze depth = strength.** It is read live and eased over ~80 ms. Strength runs 0.5 → 12 (GM = 20,000 × strength, the game's
  unit) along squeeze^1.5. The horizon grows only 1.5 → 6 u: mostly mass, a little size. The white hole
  mirrors both and pushes with 0.5 × the pull, softened over twice its horizon. **Hold time plays no part in
  the strength.** A keyboard key gives a fixed squeeze (0.5, on the rail).
- **Neither hole moves.** The press lays the black hole 150 u ahead and 45 u to the trigger's side, and the
  white hole the same distance ahead on the other side.
- **The hold radius** is the circle on which the pull exactly bends you round at the engine's speed:
  GM = v²(r − r_s)²/r. **A heavier hole holds you on a WIDER circle**, which is also why it can catch you
  from further out. At 60 u/s this circle is 24 u at a 0.4 squeeze, 48 u at 0.7 and 78 u fully buried.
- **The grip.** Pass within 1.5 × that circle with the hole at least 50° off your nose, or get inside the
  circle, and it catches you. The engine then holds your speed, you settle onto the circle (3 /s), and you
  go round for as long as you hold, up to one full turn (360°, then it lets go on its own). Outside the
  grip the pull only bends you.
- **Release = slingshot.** The pair lets go of you. You leave on the tangent at that angle with a boost of
  speed × (0.25 … 0.9) by the squeeze, over 1.5 s. The pair then falls together and annihilates.
- **The camera** swings out and up on the far side of the circle while you are in the grip, so the hole you
  are going round stays on screen.

**Measured** (scripted pad, full throttle, no steering, RT held):

| squeeze | strength | hold circle | result |
|---|---|---|---|
| 0.15 | 1.2 | 10 u | not caught; bent 27°, release boost +17 u/s |
| 0.4 | 3.4 | 24 u | caught at 28 u; a full turn in ~2.5 s |
| 0.7 | 7.2 | 48 u | caught at 40 u; a full turn in ~4 s; released at 190° → out on the tangent, +38 u/s |
| 1.0 | 12 | 78 u | caught at 76 u; ~8 s a turn; released at 128° → +54 u/s |

The black hole's position was constant in every run.

## Stoat Flight Studio (round 5 — the dipole sling, superseded)

**What changed.** Pair A's sling is now the **dipole sling** (the rail's "A: Dipole sling" switch; "A: Orbit
(round 4)" brings back the orbit sling). It exists only in the studio so far; the game still runs the orbit
sling (`StoatSlingExecutor`).

- **Press** LT/RT: the black hole is laid 240 u ahead and 60 u to the trigger's side, the white hole 240 u
  ahead and 60 u to the other side, both with a 6 u horizon, so the pair's axis starts square across the nose
  and both are in view.
- **Squeeze**: the black hole moves at up to 80 u/s × squeeze^1.5 and its horizon grows at up to 10 u/s
  (to 28 u). How deep you squeeze sets the rate; how long you hold sets how far. While it is still ahead it
  aims for a point beside your line (6 horizons off it, on its own side) so you pass it rather than hit it.
  Once it is abeam it keeps going the way it came. The white hole stays where it was laid, so the axis
  swings diagonal.
- **Physics**: the black hole pulls with the Paczyński–Wiita law, GM sized so a circle at 6 horizons is a
  free orbit at cruise (GM = ((x−1)²/x)·r_s·cruise²). The white hole pushes with 0.5 × its GM, softened over
  twice its horizon. The force across the nose bends it; the force along it is the gravity speed. The
  flight is sub-stepped at 4 ms so a still hole gives back exactly what it gave. Diving through the portal
  restores the speed you had before the dive (the two mouths are one throat); only the white hole's
  bounded push is new.
- **Let go**: the pair falls together at 40 u/s and annihilates; the gravity speed fades over 2 s once you
  are clear of both holes.

**Measured** (scripted pad, full throttle, no steering, RT held ~4.5 s of game time):

| squeeze | turned | speed kept | axis at release | what happened |
|---|---|---|---|---|
| 0.3 | 131° | +91 u/s | 170° | slingshot round the black hole |
| 0.6 | 119° | +27 u/s | 139° | swung round it, climbing out cost most of the gain |
| 1.0 | 88° | +16 u/s | 62° | the black hole arrived fast and large: a dive through the portal |

Control: a black hole that does not move (approach 0, growth 0, no push) gives back what it gave. The
residue left is the depth of the well at the point where the pass ended.

**Why the speed comes from the black hole's motion.** A still well hands back on the way out what it gave on
the way in, so a pass past a still hole gains nothing. The gain is the classic gravity assist: you swing
round a well moving against you. That is why the black hole keeps its momentum after it is abeam, and why a
light, early squeeze (a slow black hole met early) beats a buried one (a big one that swallows you).

**Round 4** (the orbit sling and the vessel portal) is described below; its numbers are still the game's.

## Stoat Flight Studio (round 4)

**Round 4 — your decisions applied.** Pair **A** (the drift pair) is the pick, wearing this branch's
look again (horizon holes traced with the converging lens; the white hole's crossing core glows
white-hot; tides even under time reversal). It now flies the **orbit sling** (`StoatSlingExecutor`,
`StoatSlingMath`): **press** LT/RT and the attractor appears beside you at the orbit radius, the
repulsor mirrored on the other side at the same size and distance; **hold** and you circle the
attractor (squeeze harder → a tighter circle, 150 u at a touch to 40 u buried, horizon = radius ÷ 6,
strength set so the circle at your speed is the free orbit); **let go** and you leave on the tangent
with a boost (0.25–0.9 × speed over 1.5 s) while the two holes fall together (40 u/s after a 0.6 s
ramp) and annihilate when their horizons touch. A pair held for 12 s lets go on its own. **The
portal:** anything that crosses the black hole's horizon (prisms, the Stoat, any vessel) comes out of
the white hole at the point reflection, stretched going in and relaxing coming out — and it leaves in
the closing white hole's frame, so the mouth cannot run it back down. **B** stays for comparison. A
**run** is one race: every ring, every lap, against the clock.

**Found by it (round 4).** A hull carried out of a CLOSING pair between the two holes, flying slower
than they close, was overtaken by its own white hole (the probe showed the distance to the white hole
falling from 23 u to 16 u after the exit). Fixed in the game too: `BlackHoleRegistry.TryGetMouthMotion`.

## Round 3 notes (pair A's numbers here are superseded by round 4)

**What it is.** The plated Stoat (`StoatHullForm`, body-only `StoatLopeMath` lope) flown round a
Slingshot-style course, read through the game's own dual-stick mix (`InputController`: yaw/pitch are
the eased SUM of both sticks, roll/throttle their DIFFERENCE; one stick alone = 29 %, throttle at rest =
half cruise). LT/RT squeeze-and-release slings a wormhole pair with either style:

- **A — the drift pair (this branch):** `BlackHoleRegistry.SpawnPair` geometry from `StoatSlingConfig`
  (strength 2–12 along hold^1.5, horizon 2 u per strength, laid 2 horizons ahead, half gap 4 horizons,
  drift 20 u/s, life 4 s); Paczyński–Wiita pull `GM/(r − r_s)²` with GM = 20,000 × strength, into a
  velocity channel capped at 90 u/s, owner-only; Schwarzschild shadow (2.6 r_s) + Einstein lens
  (θ_E² = 2 r_s / D) and a white-hot repulsor; prisms captured at the horizon re-emerge from the repulsor
  point-reflected through the midpoint.
- **B — the crystal wormhole (`cece/charming-cerf-alf1j1`):** `CrystalWormhole.Curve` (separation
  (1−p)^0.75, spiral 2.5 turns, envelope (1−p)^1.5, anti-phase beat 0.5 × sin 2π·7·p²); Plummer wells
  softened by the throat; the felt law `k·cruise²·R_t·r/(r² + R_t²)^1.5` (k 4, ceiling 1.3 × cruise,
  reach 12 throats); the graded lens `α = A·u·e^(−u²/2)` (A 0.6, w = throat); seamless mouths (soft
  edge 0.65) that show the world carried by Δ and carry the pilot through by a pure translation.

In the game the same choice is one switch, `BlackHoleConfig.crystalPairs` (`Docs/BLACK_HOLE.md` §13): the
Black Hole tool's **Pair style** button (press B), or `blackhole style drift|crystal`.

The throat is mapped to the same size dial as the horizon (2 u per strength), so a full squeeze lays a
24 u throat — close to the crystal cell's shipped ~26 u. B's life is sling-tuned as `BlackHoleConfig`
ships it (0.6 s form, 0.05 s stand, 3.35 s annihilate = A's 4 s); the button "Their cell's 6 s / 9 s" restores the shipped spawnable's.

**Compare** is a split screen: each pair in its own corner of the field, 1,280 u apart, each half
rendered with only its own wells under one orbit. **Runs** records finish times per pair on each
course (seed × intensity × laps). **Decisions** go to the artifact's database collection `decisions`
(`topic`, `choice`, `note`, `style`, `settings` = the pair style, portal switches, course and every
number changed from shipped, `createdAt`, `by`); Claude reads them with `ArtifactData` (`list`,
collection `decisions`). The repo copy has no database: its log lives in that browser, and **Copy log**
exports it as text.

**What it does not model.** The course is a stand-in on the shipped numbers (8 rings on a 600 u
circle, mouths 48/40/32/26 by intensity), not `HeadlongCircuit`; the lenses are screen-space
approximations, not the game's ray march; B's warp field (`WarpFieldRuntime` / vessel scaling) and
A's frame dragging and tides on the hull are not modelled; the vector flight model's grip is replaced
by "velocity = nose × speed + the pull channel".

**Found by it.** The sling slung the LATEST per-frame squeeze; a released trigger sweeps back down
before the release edge, so every gamepad sling was minimum size. The studio's scripted pad releases
the way a thumb does, which is how it showed; `StoatSlingMath.Peak` fixes it (`STOAT.md`).

**Gamepads.** The page uses the browser Gamepad API. If the artifact viewer does not pass the
controller through, open `StoatFlightStudio.html` straight from the repo in Chrome.
