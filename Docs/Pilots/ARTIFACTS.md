# Artifacts and the payload: what a squad fights over

**Status: THE ARTIFACT CONCEPT** (Garrett, 2026-10-27, rev 2). It is the partner of the pilot concept,
[`CALLSIGNS.md`](CALLSIGNS.md).

**Built so far (`claude/squad-payload-pilots-artifacts`, 2026-10-27, unverified in the editor):** the plain payload
(§9 step 1) in the Wander toy's "With Ark". `ArkHold` (`Assets/_Scripts/Controller/Payload/`) hands out six real hull
prisms per payload (`Ark.TakeHullPrisms`, never the last dozen); `PayloadCarrier`, ensured on every vessel by
`VesselController.Initialize`, takes and releases it on `InputEvents.PayloadAction` (RB / E); the burst runs
`PayloadPrismEffectSO`s from `Resources/PayloadConfig` (written by `Tools/Build/author_payload_assets.py`), and the
plain one, `PayloadCrackPrismEffectSO`, shares `SuperShieldCrack` with the Grizzly sniper. Hand tests:
`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "The plain payload on RB". **Squad = domain** for now: a vessel takes payloads
only from its own domain's Ark. The Ark is the local toy's, so the hold arbitrates on the machine that holds it; a
networked Ark (Voyages, §8.5) makes the hold a server object.

**The fall (§8.4), also built 2026-10-27, unverified in the editor.** The hold carries artifacts by id (the id
`SquadSlot.ArtifactId` names on Shombith's branch; no second artifact type). When the last hull prism goes,
`ArkFall` credits whoever destroyed THAT prism (`Prism.DestroyerName`) and moves everything aboard to that squad's
standing Ark in one step. A creature kill, an unknown force, the Ark's own squad, or a raider with no Ark here earns
nothing, and the artifacts spill as `ArtifactPickup`s (not mass: fauna cannot eat them) that anyone takes with RB,
carries, and stows with RB beside their own Ark. A pickup is a scene root, so uncollected ones die with the instance;
`ArtifactPickup.StrikeAll` is there for a mode whose scene outlives it. Until squad slots load artifacts (§8.2), a
tester puts ids aboard with `PayloadConfig`'s Test Cargo (ships empty). Hand tests: "The Ark's fall" in the checklist.

**Javelin and Aegis (§9 step 2), built 2026-10-27, unverified in the editor.** `ArtifactSO` is now a payload
transformation (`PayloadTransformSO`: `JavelinTransformSO`, `AegisTransformSO`, `Assets/_Scripts/Controller/Payload/Transforms/`)
with four element tracks (`ArtifactTrack`: a value at 0 and at 10, a cost, a twist line at 5) and two empty sounds. No
artifact carries a vessel class (the catalog's `Validate` and `author_pilot_assets.py` both refuse one). A vessel's slotted
artifact (`PilotLoadoutStore`, one slot, read in a match through `VesselPilotLoadout`) loads every payload that player
grabs; RB then releases it through the transformation instead of the plain burst. A refused release (an Aegis away
from home) keeps the parcel on the hull and shows its authored line. The track levels and crystal balances live on the
device for now (`PilotLoadoutStore`, the stand-in for the profile, §10). Numbers: `Docs/Pilots/pilots_and_artifacts.json`.
Hand tests: "Javelin and Aegis" in the checklist.

**Upgrades (§9 step 4), built 2026-10-27, unverified in the editor.** The Ark has an ARTIFACTS tab beside the
Converter and Squads (`ArkArtifactsPage`, written by `author_menu_ark.py`): the player's artifacts, the picked one's four
tracks (value now and next, the twist, dim until level 5), RAISE in the track's own crystals and OMNI to pay in omni
crystals instead (always the player's pick). The crystals are the profile's own balances (`PlayerDataService`'s
elemental crystals and the omni balance, Shombith's section 97), so nothing new goes to the backend; **the track
levels are still on the device** (`PilotLoadoutStore`, PlayerPrefs) until the profile carries them, which is
Shombith's call. Dev: FrogletTools > Pilots > Grant 20 Crystals / Reset Every Artifact Track. Hand tests: "The Ark's
ARTIFACTS tab" in the checklist.

**Shombith's branch merged (Garrett, 2026-10-27: "it should merge and build but throw anything out that conflicts
with what we have decided here").** Kept: the saved squads, `SquadSlot`, the cloud repository, `VesselKeyword` /
`VesselRequirement`, the Hangar PILOTS and ARTIFACTS tabs, `NetPilot` / `NetArtifacts`. Thrown out: the 45 numeric
artifacts and their ranks and stats, the 40 named pilots and the ability each rewrote (the Tomahawk throw), the
default "base kit" pilots and `ElementalBoost`. `Docs/Pilots/PILOTS_AND_ARTIFACTS.md` is superseded.

**Decided since (Garrett, 2026-10-28):** **one Ark per account, deploying one squad at a time** (friends join your
Ark and fly in its squad); the Ark hoards the artifacts and each saved squad assigns one to each vessel; the Ark is
why vessels have no health (all a vessel can lose is elements and the use of its abilities). The artifact economy
waits for later. The plan: [`Docs/Voyages/VOYAGES.md`](../Voyages/VOYAGES.md) §2.

**Decided earlier (Garrett, 2026-10-27):** one Ark per squad of three vessels (now: per account, one squad deployed); an Ark felled by fauna spills its
artifacts for anyone, and uncollected ones are lost when the instance expires; the RB abilities moved to Y, so RB is
free for the payload.

**What changed from rev 1 (same day):**
- Artifacts work with **any vessel**, not one class.
- **Upgrades are paid in elemental crystals**, not earned by extracting.
- **Stealing means taking down the owner's Ark**, which takes every artifact aboard. The rev-1 idea of an artifact
  dropping off a vessel is withdrawn.

**What this replaces:**
- `Docs/PILOTS_AND_ARTIFACTS.md` §3 (2026-10-12): an artifact as a quantitative "fifth knob" on the vessel.
- The 45 quantitative artifacts on Shombith's branch `claude/pensive-dijkstra-qxoxo0` (2026-10-26). Its `ArtifactSO`,
  catalog and the saved squads' artifact id are what this is built on; the roster itself is gone (above).

**Garrett's brief (2026-10-27):**

> "there will be a payload on the ark that can be used to overcome not having a specialist on a team. the rb button
> can be used to grab and activate a payload to destroy supershielded prisms. artifacts could be imbalanced items that
> are part of a vessel's kit which can transform payloads into much more powerful items with more powerful effect
> triggered by the same button. this gives artifacts their own relevant mechanics and reason to steal, protect, hoard,
> and upgrade them, a reason to hunt for them in voyages and a reason to risk taking them (extraction mechanics)."
>
> "lets make artifacts tied to vessels as little as we can, then it is the artifact that is exciting to find because
> you can use it with any vessel. upgrade is done by spending elemental crystals on it. stealing artifacts involves
> taking down that vessel's ark and taking all the artifacts they have."

## 1. The concept

A squad is a **12-card deck** (Garrett, 2026-10-27): three vessels, each with four cards (vessel, element, pilot,
artifact). The four cards of a slot each decide one thing:

| Part | What it decides |
|---|---|
| Kit | what the vessel does |
| Elements | how strongly it does it |
| Pilot | where to look: the sight on LB |
| **Artifact** | **what the payload becomes** |

- **The payload is the generalist's answer.** Super-shielded mass is the wall most of the fleet cannot touch. Today
  the only shipped way through it is the Grizzly sniper's crack (`SniperCrackSuperShieldPrismEffectSO`, whose own
  comment says super shields were designed to fall to "targeted opt-in mechanics"). The payload is the second such
  mechanic, and anyone can use it.
- **An artifact transforms the payload, whoever carries it.** It belongs to the payload, not to the hull, so **any
  vessel can use any artifact**. Finding one is exciting for that reason: it's good in every squad you build.
- **Artifacts ride in the Ark, and the Ark is what you lose.** Nobody can take an artifact off a vessel. To steal
  them you take down the Ark that carries them, and you get **all** of them. A squad decides which artifacts to bring
  on a voyage, knowing the whole collection aboard is at stake.

## 2. The payload

| | |
|---|---|
| **Where it lives** | in the **Ark's hold**. A payload is a parcel of the Ark's own hull mass |
| **Grab** | press **RB** next to your Ark. The parcel leaves the hull, **loaded with the artifact in your squad slot** (or plain if your slot has none), and rides on your hull where everyone can see it |
| **Activate** | press **RB** again. A plain payload cracks every super-shielded (and shielded) prism in a short radius around the release point, then is spent. A loaded payload does what its artifact says (§3) |
| **Cost** | the Ark itself. Every payload thins the hull that protects every artifact aboard |

**Why it is made of the Ark's own mass.**
- *Mass is conserved:* nothing is minted. A payload is existing prisms moved off the Ark, and it spends itself
  (an active force) when it fires. So the payload supply is the Ark's hull, and the Ark does not regrow.
- *The Ark lives or dies by the food web* (`Docs/ECOSYSTEM.md` §41). Here that becomes the central tension: **every
  payload you fire makes your treasure easier to take.**

**The artifact never leaves the Ark.** A loaded payload carries the artifact's *effect*, not the artifact. So a carrier
can never be robbed, and that is what keeps *PvP is petals only* (LOCKED) untouched.

**Silence applies.** A carrier who takes a scored hit is silenced (`Docs/ElementalAbilitySystem/SILENCE.md`). While
silenced, a payload harms no rival prism and no Ark.

**The button.** RB is free on every vessel. The three abilities that were on it (Brittle Star, Grizzly, Shrike) moved
to Y on 2026-10-27 (`CALLSIGNS.md` §5). On the keyboard, E is free for it too.

## 3. Artifacts work with any vessel

- **No vessel class appears in an artifact.** Every artifact transforms the payload object itself: how it is
  delivered, what shape its effect takes, what it does to the mass it reaches. A Sparrow and a Butterfly loaded with
  the same artifact fire the same thing.
- **If an artifact must lean on a vessel, it leans on a shared concept, never a class.** This is rule 1 of the
  compatibility vocabulary (`PILOTS_AND_ARTIFACTS.md` §4): "a trail is *base prism production*". In the list below
  only Wake does, and every vessel in the fleet produces prisms.
- **It is imbalanced on purpose.** A loaded payload should feel like the strongest thing in the match for a few
  seconds. The balance is the trip back to the Ark, the hull it costs, the Ark you are thinning, and silence.
- **It obeys the LOCKED rules.**
  - *PvP is petals only:* an artifact acts on mass, lifeforms and Arks. If it reaches a vessel, it may only move
    petals, and only as a scored hit through `CombatHitDrain.TryAdmit`.
  - *Mass is conserved:* anything it builds comes out of the payload's own parcel.
  - *Super-shielded mass moves for nothing else:* Singularity is the one stated exception (below).

### A first set

Names avoid every vessel, pilot, mode and callsign.

| Artifact | The loaded payload | Leans on |
|---|---|---|
| **Javelin** | thrown, not placed: it flies down your heading, cracks every super-shield along its line, and bursts where it stops | nothing |
| **Hydra** | splits on release into three payloads that fan out and crack where each lands | nothing |
| **Keystone** | the opposite of cracking: it plants as a super-shielded wall of your domain, built from the parcel | nothing |
| **Turncoat** | instead of cracking, it turns rival super-shielded prisms in its radius into **yours**: you take the citadel whole | nothing |
| **Singularity** | implodes: rival prisms in its radius, super-shielded included, are pulled to its centre, and moving prisms destroy what they hit (`PrismMotion`). This is the one thing that moves super-shielded mass, a stated exception to that rule | nothing |
| **Hourglass** | hold RB to wind it: the crack radius grows the longer you hold before you let go | nothing |
| **Aftershock** | cracks twice: again a few seconds later in the same place, catching whatever was rebuilt | nothing |
| **Sentinel** | plants and arms: for as long as it stands, it cracks the first rival super-shield raised within its range | nothing |
| **Aegis** | released at your own Ark, it super-shields the Ark's hull for a while. It protects everything aboard | nothing |
| **Ricochet** | leaps from super-shielded target to super-shielded target, cracking each, up to a count | nothing |
| **Wake** | while you carry it, the prisms you lay become a fuse. Release ignites the whole run, cracking every super-shield it touches | `BasePrismProduction` (the whole fleet) |

## 4. Upgrades: spend elemental crystals

**An artifact is upgraded by spending elemental crystals on it.** Each artifact has **four tracks, one per element**,
and the crystal you spend says which track rises. This uses the game's own element grammar, so an upgrade reads like
an element level:

| Crystal | Raises | Javelin, for example |
|---|---|---|
| **Mass** | how much: more prisms reached, a bigger wall, more pieces | a wider cracking line |
| **Space** | how far: radius, range, reach | flies farther |
| **Time** | how long or how soon: duration, fuse, delay, wind-up | its burst lingers |
| **Charge** | how hard: what it gets through, how many targets | cracks through a second layer |
| **Omni** | any one track, the player's pick | |

- **Tracks run 0 to 10**, like an element's base band. **Level 5 on a track adds one qualitative twist**, the same
  contract every vessel ability has with its elements (`Docs/ElementalAbilitySystem/ARCHITECTURE.md`). For example,
  Javelin at Space 5 pierces and keeps flying after its first target.
- **What each track does is authored per artifact** on its SO: the curve, the cost per level, and the level-5 twist.
  The table above is the grammar, not a formula.
- **Upgrades live on the artifact, not the player.** A stolen artifact keeps every crystal spent on it, so taking a
  well-upgraded artifact steals that investment and losing one loses it. That is why raids and protection matter.
- **Where you spend: the Ark screen**, next to the Elemental Converter. The Ark is where artifacts live, so its screen
  is where you tend them.
- **Per-element crystal balances do not exist yet.** The game holds one crystal currency (`CrystalCurrencyDisplay`),
  and the Converter is still animation only: its own doc says "no such balances exist yet". Artifact upgrades need
  the same per-element balances the Converter needs, so it is one piece of backend work, and it goes through Shombith.
- *Elementals governs all buffing* holds: the only way an artifact gets stronger is elements.

## 5. Stealing: take down the Ark, take everything aboard

- **The only way to take an artifact is to take down the Ark it rides in.** An Ark dies when its last hull prism is
  destroyed (`ECOSYSTEM.md` §41). When it falls, **every artifact aboard goes to the squad whose force destroyed that
  last prism** (destruction is already scored to the force behind it). They go into the raiders' Ark hold, where they
  are now at risk too until extraction.
- **This needs no new PvP.** Attacking an Ark is attacking mass, which is an arena effect. *PvP is petals only*
  (LOCKED) is untouched, and so is the one-to-one rule between scored hits and petal theft.
- **Defence is the game the fleet already plays:**
  - *Protecting an Ark IS controlling the cell:* fauna defend or eat it by the shipped diet rules.
  - Your scored hits **silence raiders**, and a silenced pilot cannot harm an Ark's health prisms (`SILENCE.md`).
  - **Aegis** shields the hull, and **Keystone** walls it.
  - Every payload you fire thins your own hull.
- **If fauna finish an Ark, nobody earned it, so its artifacts spill** (Garrett, 2026-10-27). They become loose
  objects in the world, and anyone can grab one with RB and carry it to their own Ark. **An artifact nobody collects is
  lost when the instance expires.** It is gone for good, not returned to its owner.
- **One Ark per account, one squad deployed at a time** (Garrett, 2026-10-28; it was "one per squad" on 2026-10-27),
  and a squad is three vessels. An Ark's fall takes everything aboard,
  so a squad risks up to three artifacts (one per slot) plus anything it found and brought home on this voyage.

## 6. The loop

1. **Bring.** Building the deck picks the three artifact cards: one per slot, from that player's collection. The
   artifacts brought ride in the Ark that deploys the squad. How much of the Ark's hoard is at stake on a voyage is an
   artifact-economy question, left for later (Garrett, 2026-10-28).
2. **Hunt.** Artifacts are found on voyages, never bought:
   - in **vaults**, small super-shielded shells in the HyperSea that a payload or a specialist opens;
   - in **hoards** (proposed, ecology owner's call), using the thieves of `Docs/BUILDERS_AND_THIEVES.md`;
   - in **rival Arks**.

   A found artifact is grabbed with RB and carried home to your Ark. In transit it is safe from rivals; it is only at
   risk once it's aboard.
3. **Fire.** Payloads come off your Ark loaded with your slot's artifact. Every one thins the hull.
4. **Raid and protect.** Take down rival Arks for everything aboard, and keep yours standing.
5. **Extract.** What is aboard your Ark when the voyage ends is yours: it joins your collection with its upgrades.
6. **Upgrade.** Spend elemental crystals on the Ark screen. The more you spend, the more you have to lose next time
   you bring it.

**A sight for artifacts (proposed).** A pilot callsign that lights every artifact (in vaults, hoards, transit and
Arks) would make artifact hunting a squad role. It needs the fundamentals-curation process first (`CALLSIGNS.md` §7).
Its word needs a free first letter: F, J, K, L, P, Q, U, X or Y. Until then, CITADEL shows vaults, since they are
super-shielded, and GALLEON shows Arks.

## 7. Rules (summary)

1. Payloads come out of the Ark's hull. Nothing is minted.
2. A plain payload cracks super-shielded and shielded prisms in a radius. It is the second sanctioned super-shield
   crack, after the Grizzly sniper's.
3. No artifact names a vessel class. One that must lean on a vessel names a shared concept the fleet has.
4. An artifact never leaves its Ark during a voyage except in a finder's hands on the way home. Payloads carry its
   effect, not the artifact.
5. An Ark's fall takes everything aboard. That is the only theft. One Ark per account, deploying one squad of three. If fauna fell it, its
   artifacts spill for anyone, and any left uncollected are lost when the instance expires.
6. A loaded payload touches vessels only by moving petals as a scored hit. Silence makes a carrier's payload harmless.
7. Upgrades are elemental crystals on four tracks (0 to 10, a twist at 5), and they stay with the artifact.
8. Every artifact name and line is authored text. Every payload and artifact sound is an exposed `EventReference`,
   shipped empty.
9. Ownership is server-authoritative: grabs, an Ark's fall, the transfer and extraction are arbitrated by the server,
   like the arena's lobby hull claim.
10. Each vessel has its own pilot and its own artifact (Garrett, 2026-10-27): no two vessels in a squad share either.
    Every vessel needs a pilot (an unset seat flies the hull's first offered pilot); an empty artifact slot is fine.
11. Arena modes do not support artifacts; voyages support them but do not require one (Garrett, 2026-10-27). A vessel
    in an arena match (`SO_ArcadeGame.ArenaRules`) loads no artifact, so its payloads are plain.

## 8. How to build it

Built on the types from `claude/pensive-dijkstra-qxoxo0` once they land, never beside them.

1. **The payload (new).**
   - `ArkHold` on the `Ark`: hands out a parcel of hull prisms, loads it from the grabber's slot artifact, and holds
     the voyage's artifacts.
   - `PayloadCarrier` on every vessel: RB grab within reach, carried on the hull, RB activate.
   - The crack is a prism impact effect on the same route as `SniperCrackSuperShieldPrismEffectSO`, so super-shield
     removal stays one code path.
2. **The artifact.**
   - Reshape `ArtifactSO` from "one stat and a value" to "one payload transformation": a `PayloadTransformSO`, four
     element tracks (curve, cost, level-5 twist), and authored name, line and sounds.
   - Drop the vessel-class binding. A keyword stays only for the rare artifact that leans on one.
   - `SquadSlot.ArtifactId` already exists.
3. **Upgrades.** Per-element crystal balances plus each artifact's track levels on the player profile. That is the
   backend, so it goes through Shombith. The Ark screen gets an artifact panel beside the Converter.
4. **The fall.** When `Ark` dies, the hold's contents move to the credited squad's Ark, server-side, in one step. With
   no squad to credit (fauna), they spawn as `ArtifactPickup`s that the instance deletes, uncollected, when it ends.
5. **Voyages.** Vaults, hunting and extraction belong to Voyages, not to the Arkway. Garrett (2026-10-28): voyages
   replace missions as the fourth game location after Toybox, Arcade and Arena, and they are the game's culminating
   experience. Squads travel between specific locations on a shared galactic map (the HyperSea) and have unique
   experiences there. "Galactic traffic control" puts squads that meet into managed peer-to-peer instances that make
   sense for both sides' missions. The Arkway (`ECOSYSTEM.md` §41, `Docs/ToySystem/ARKWAY_PLAN.md`) is a toy that also
   uses the Ark: the most voyage-like toy, a taste of a freestyle voyage, not the voyage itself. **The plan is
   [`Docs/Voyages/VOYAGES.md`](../Voyages/VOYAGES.md)**: the map, the locations, the jobs, traffic control, the
   host-owned Ark (§8) and V1's build order (§9.1). Nothing for Voyages is built yet.
6. **`/verify-unity`** before any C# commit (LOCKED).

## 9. Order

The session that builds it starts from `Docs/prompts/PILOTS_ARTIFACTS_PAYLOAD_BUILD_PROMPT.md`.

1. The plain payload (Ark hold, grab, carry, crack) in one arena with an Ark. That proves the generalist answer.
2. Two artifacts on any vessel: Javelin (offence) and Aegis (defence).
3. An Ark's fall transferring its hold.
4. Element tracks and crystal spending, once per-element balances exist.
5. Vaults and extraction in Voyages (the fourth location, §8.5), once Voyages have a plan.

## 10. Open decisions

| Decision | Who decides |
|---|---|
| Singularity moving super-shielded mass, an exception to *Prism movement* | Garrett, through the fundamentals-curation process |
| Artifact track levels on the player profile (on the device until then: `PilotLoadoutStore`; the crystals already are) | Shombith (backend) |
| Thieves hoarding artifacts | the ecology owner (ecology is LOCKED: `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`) |
| An artifact sight among the pilot callsigns | Garrett, through the fundamentals-curation process |
