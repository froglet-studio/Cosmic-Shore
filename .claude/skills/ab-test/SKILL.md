---
name: ab-test
description: Make a shareable A/B test artifact for one Cosmic Shore design decision - the option the team landed on beside a runner-up - as a tiny playable page anyone can open, fly A, fly B, and answer "which did you like better, and why?". Votes land in the artifact's shared store, blind (letters randomised per viewer), with the names revealed after the vote. Use on "A/B test", "make a shareable test of X vs Y", "get feedback on <mechanic>", "did we drop <thing> too fast", "put this in front of players", or when a studio decision needs an outside audience. Not a studio - the Vessel Studio artifact stays the one place the team tunes (/vessel-studio §0).
---

# A/B test artifacts

One page, one question: **which did you like better, A or B, and why?** The Vessel Studio is where the team tunes and
decides. An A/B page asks people outside the decision whether it was right. The first one is the Stoat sling test
(`Docs/Studios/ABTests/README.md`); copy it.

## 1. Pick the pair (the decision, then its runner-up)

- Read the decision record first: the studio's decision log (`ArtifactData list` of `decisions` on the Vessel Studio
  artifact), `Docs/Studios/*_PLAN.md`, `/vessel-studio` §1, the vessel's `R_VesselActions/*.md`.
- A good runner-up was set aside for a reason that is **not about feel**: it could not be seen, it had no AI, it was
  never measured, it lost to a bug. Say that reason on the reveal card in one sentence, with its date.
- If the runner-up's known flaw has a proposed fix, build the fix in, and say so. Otherwise the test measures the flaw
  again.

## 2. Build the page (the rules that make it a fair test)

1. **One variable.** Everything else is identical in A and B: the course, the hull, the camera rule, the drawings, the
   HUD and the instructions' length. If a helper is added for B's sake (the Stoat's camera lift), give it to A too.
2. **Shipped numbers only** (`/vessel-studio` D1). Every constant names its asset, read from the asset rather than
   the C# default (they differ). Port the math line for line from the game's `*Math.cs` and keep its method names in
   comments. A studio's numbers can drift from the prefab. Check them, and report a drift as a finding.
3. **Blind.** Randomise which variant is A per viewer, keep it in `localStorage` with a test id, and reveal the names only
   after the vote. Do not use a colour or word that hints which one shipped.
4. **Small enough for anyone.** It loads straight into a live attract scene with a card on top: **Fly A**, **Fly B**,
   the controls, **Pick your favourite**. The vote unlocks after about 10 s on each. The course is one short lap (about
   30 s). Phones get touch controls automatically (no button asks, D26). Use a flat course unless the decision is
   about 3D.
5. **The vote:** two big choices plus "Why? One line is plenty". Store one document per voter at `votes/<user id>`:
   `{test, pick, mech, why, assign, flew: {<mech>: stats}, device, at}`. Capabilities:
   `{db: {rules: [{path: "votes", read: "view", write: "owner"}, {path: "votes/{self}", write: "interact"}]}, user: {}}`.
6. **The reveal:** what each one was, where it came from, and the running tally by mechanic name (never by letter).
7. **Outside audiences cannot write.** View-level viewers (outside the organisation, public link) and signed-out
   visitors get `null` or a refused write. The thank-you card then says so and offers **Copy my answer**: one line
   with the pick, the why and the lap times, for them to paste back where the link was shared. Never claim a vote
   saved when it did not.
8. **The Vessel Studio look** (`studio-look.js`, D19: sky, prism field, `marker`), published beside the page through
   `files` (`{"studio-look.js": "Docs/Studios/VesselStudio/studio-look.js"}`). Never paste a copy into the page. Use
   Chakra Petch + Atkinson Hyperlegible and Stoat amber for chrome (D29).
9. **A test hook** (`window.__abTest`) that exposes the same `step` the page flies, so a headless check drives it.

## 3. Check, publish, read

- Headless (Playwright with the pre-installed headless shell, `--use-gl=swiftshader`, three.js served from a local
  copy): no page errors at desktop size or on an emulated phone, no sideways scroll at 390 px, a bot lap that finishes
  on both variants, and one scripted use of each mechanic whose events come out as the math says. Read the
  screenshots.
- Publish as its **own** artifact (it is not a studio page). Then run `ArtifactData list votes` (empty), and an
  `as_level: "interact"` write to someone else's vote, which must be refused.
- Source in `Docs/Studios/ABTests/<test>/index.html`, plus a row in `Docs/Studios/ABTests/README.md`: the URL, the
  pair, why that pair, what is identical, what differs, and what is not modelled.
- Reading results: `ArtifactData list` on `votes`. Report the split by mechanic, the reasons grouped by theme, and the
  lap and sling stats beside them. Record the outcome in the studio's decision log. If it changes a decision, the
  studio changes first, then `/artifact-to-unity`.
- The owner shares the link (Share menu). Contributor access is needed for a vote to save.

## 4. Where this is going (the owner, 2026-10-15)

Today an A/B page is a JavaScript copy built from the shipped numbers (Vessel Studio tier A). The goal is A/B snapshots
served by a scalable Amoebius (the engine running the game's own code), at whatever fidelity the question needs. They
would be made on the fly by the vessel-creation skill and shared with internal and external audiences. Until that
engine runs in a browser, keep the page honest: name it a model of the shipped numbers and list what it does not model.
