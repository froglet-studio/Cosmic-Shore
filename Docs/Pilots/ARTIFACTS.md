# Artifacts and the payload: what a squad fights over

**Status: THE ARTIFACT CONCEPT** (Garrett, 2026-10-27: "build artifacts too", from the brief that opened the pilot
work). Design only: no payload or artifact code exists on `bleeding-edge`. It is the partner of the pilot concept,
[`CALLSIGNS.md`](CALLSIGNS.md).

**What this replaces:**
- `Docs/PILOTS_AND_ARTIFACTS.md` §3 (2026-10-12): an artifact as a quantitative "fifth knob" on the vessel.
- The 45 quantitative artifacts on Shombith's in-flight branch `claude/pensive-dijkstra-qxoxo0` (2026-10-26: one
  number each, ranks I-III, three slots). **That branch is not touched.** Its `ArtifactSO`, catalog, ranks and the
  saved squads' artifact id are what this is built on. What becomes of its roster is for Garrett and Shombith (§8).

**Garrett's brief (2026-10-27):**

> "there will be a payload on the ark that can be used to overcome not having a specialist on a team. the rb button
> can be used to grab and activate a payload to destroy supershielded prisms. artifacts could be imbalanced items that
> are part of a vessel's kit which can transform payloads into much more powerful items with more powerful effect
> triggered by the same button. this gives artifacts their own relevant mechanics and reason to steal, protect, hoard,
> and upgrade them, a reason to hunt for them in voyages and a reason to risk taking them (extraction mechanics)."

## 1. The concept

A squad slot has four parts, and each one decides one thing:

| Part | What it decides |
|---|---|
| Kit | what the vessel does |
| Elements | how strongly it does it |
| Pilot | where to look: the sight on LB |
| **Artifact** | **what the payload becomes** |

- **The payload is the generalist's answer.** Super-shielded mass is the wall most of the fleet cannot touch. Today
  the only shipped way through it is the Grizzly sniper's crack (`SniperCrackSuperShieldPrismEffectSO`, whose own
  comment says super shields were designed to fall to "targeted opt-in mechanics"). The payload is the second such
  mechanic, and anyone can use it. **A squad without a specialist can still open a citadel; it just has to go back to
  the Ark for every one.**
- **An artifact is an imbalanced item in a vessel's kit.** It doesn't change the vessel. It changes the payload: when
  this vessel triggers a payload, the artifact turns it into something far stronger, in the vessel's own style. Same
  button.
- **Because artifacts are strong, they are worth fighting over.** You hunt for them on voyages, steal them, protect
  them, hoard them and upgrade them. Taking one into the HyperSea is a risk, because you can lose it before you
  extract.

## 2. The payload

| | |
|---|---|
| **Where it lives** | in the **Ark's hold**. A payload is a parcel of the Ark's own hull mass |
| **Grab** | press **RB** next to your Ark (or a loose payload) and the parcel leaves the hull and rides on yours |
| **Carry** | it rides on your hull where everyone can see it. One at a time |
| **Activate** | press **RB** again. The base payload cracks every super-shielded prism (and shielded prism) in a short radius around the release point, then is spent |
| **Cost** | the Ark itself. Every payload thins the Ark's hull, so a squad that cracks everything has a weaker home |

**Why it is made of the Ark's own mass.**
- *Mass is conserved:* nothing is minted. A payload is existing prisms moved off the Ark, and it spends itself
  (an active force) when it fires. So the payload supply is the Ark's hull, and the Ark does not regrow.
- *The Ark lives or dies by the food web* (`Docs/ECOSYSTEM.md` §41). Spending hull on payloads is a real trade
  against the Ark's survival, so there is no separate payload counter to balance.

**Silence applies.** A carrier who takes a scored hit is silenced (`Docs/ElementalAbilitySystem/SILENCE.md`), and while
silenced a payload, transformed or not, harms no rival prism and no ark. So **protecting a carrier means keeping
petals on them**, and stopping one means hitting them first. That is the rule PvP already runs on, with nothing new.

**The button.** RB is `Throttle` (boost) and raises `FlipAction` in `GamepadInputStrategy` today, so a payload on RB
would also boost and flip on those hulls. That decision is open (§8). The rest of this doc says "RB" for the payload
button.

## 3. Artifacts

- **One artifact slot per vessel.** An artifact belongs to a vessel class, or to every class that shares a keyword in
  the compatibility vocabulary (`PILOTS_AND_ARTIFACTS.md` §4: `Projectile`, `Blade`, `CrystalBlast`, …).
- **It only acts through a payload.** Carrying an artifact without a payload does nothing, so it can't creep into a
  stat buff. *Elementals governs all buffing* stays true: the artifact changes an object, not the vessel's numbers.
- **It is imbalanced on purpose.** A transformed payload should feel like the strongest thing in the match for a few
  seconds. The balance is the trip back to the Ark, the hull it costs, the risk of losing the artifact, and silence.
- **It obeys the LOCKED rules.**
  - *PvP is petals only:* a transformed payload acts on mass, lifeforms and arks. If it reaches a vessel, it may
    only move petals, and only as a scored hit through `CombatHitDrain.TryAdmit`.
  - *Mass is conserved:* anything it builds comes out of the payload's own parcel.

### A first pass, one per vessel (for the vessel owners to reshape)

| Vessel | Artifact | The payload becomes |
|---|---|---|
| Sparrow | **Warhead** | a missile: fired down the nose, it cracks every super-shield along its flight and in its blast |
| Rhino | **Battering Ram** | a ram on the nose: for a few seconds the plough breaks whatever it meets, super-shields included |
| Serpent | **Keystone** | the opposite of cracking: planted, it becomes a super-shielded Seed Wall of your own, built from the parcel |
| Squirrel | **Fuse** | a fuse laid along your drift: the trail behind you burns along its length and cracks the super-shields it touches |
| Dolphin | **Depth Charge** | loaded into the next crystal blast: that cone cracks super-shielded mass. Echo Sight already shows it |
| Manta | **Blight** | dropped while soaring, a cloud that leaves rival super-shields plain for its duration |
| Urchin | **Spike Rail** | carried on the projected rail: the chain spikes cascade through super-shields along it |
| Butterfly | **Fold Charge** | dropped into a wormhole mouth, it goes off at the OTHER mouth |
| Thresher | **Wrecking Core** | loaded into the ball: for a few swings it smashes super-shielded prisms too |
| Scarab | **Sun Core** | a forged ball carrying it raises a super-shielded dais where it threads a switch: a fortress, not a key |
| Stoat | **Event Horizon** | thrown into the dipole, it lets the black hole pull super-shielded mass, which nothing else can move, and annihilate it |
| Angler | **Beacon** | worn as the lure: the school bites through super-shields at the painted point |
| Grizzly | **Mortar** | the specialist's version: the sniper fires it and every super-shield in a wide radius cracks |

The Serpent's Keystone and the Scarab's Sun Core turn the payload into **defence**. That gives a protecting squad its
own reason to carry payloads home.

## 4. The loop: hunt, carry, steal, protect, hoard, upgrade, extract

1. **Hunt.** Artifacts are found on voyages, never bought:
   - in **vaults**, small super-shielded shells in the HyperSea with an artifact inside. A payload or a specialist
     opens one, which is the payload's first job;
   - in **hoards** (proposed, ecology owner's call): the thief species of `Docs/BUILDERS_AND_THIEVES.md` already
     carries mass to a visible hoard, and a thief could carry a loose artifact there too;
   - in a **fallen Ark's** spilled hold;
   - on **rival squads**.
2. **Carry.** An equipped artifact shows on the hull: it is a world fact, shown in the world (LOCKED). Rivals can see
   what you are carrying.
3. **Steal.** PvP is petals only, so nobody can simply take an artifact off a vessel. **Proposed: an artifact is held
   by its carrier's petals.** When scored hits empty the carrier's base band (no petals left above resting level 0),
   the artifact drops as a loose world object, and anyone can grab it with RB. The hits stay ordinary petal hits; the
   drop follows from the victim's own state. This is a reading of a LOCKED rule, so it is Garrett's call (§8).
4. **Protect.** Keep petals on your carrier (that keeps the artifact on the hull and keeps silence off), and keep your
   Ark alive (a fallen Ark spills its hold).
5. **Hoard.** Artifacts you are not carrying ride in the Ark's hold. An Ark is mass: attacking its hull is an arena
   effect, not PvP, so a hoard is safe exactly as long as the squad holds the cell around its Ark. *Protecting an Ark
   IS controlling the cell* (`ECOSYSTEM.md` §41).
6. **Upgrade.** An artifact ranks up **by being extracted with**: rank I, II, III, using the ranks Shombith's
   `ArtifactSO` already carries. Each rank makes the transformed payload stronger (bigger radius, longer duration, a
   second charge). A rank III artifact is the thing you most want to use and most fear to lose, which is the risk the
   brief asks for.
7. **Extract.** A voyage ends by getting out: what you hold when you extract is yours, and what you lost stays in the
   HyperSea for someone else to find.

**A sight for artifacts (proposed).** A pilot callsign that lights every artifact (loose, carried, hoarded, vaulted)
would make artifact hunting a squad role. It needs the fundamentals-curation process first, because artifacts are not
yet a fundamental (`CALLSIGNS.md` §7). Its word would need a free first letter: F, J, K, L, P, Q, U, X or Y, for
example LODESTAR. Until then, CITADEL already shows vaults, since they are super-shielded, and GALLEON shows Arks and
their payloads.

## 5. Rules (summary)

1. Payloads come out of the Ark's hull. Nothing is minted.
2. The base payload cracks super-shielded and shielded prisms in a radius. It is the second sanctioned super-shield
   crack, after the Grizzly sniper's.
3. An artifact acts only through a payload, and only on the vessel classes its keyword names.
4. A transformed payload touches vessels only by moving petals as a scored hit.
5. Silence makes a carrier's payload harmless.
6. Every artifact name and line is authored text. Every payload and artifact sound is an exposed `EventReference`,
   shipped empty.
7. Ownership is server-authoritative: grabs, drops and extractions are arbitrated by the server, like the arena's
   lobby hull claim.

## 6. How to build it

Built on the types from `claude/pensive-dijkstra-qxoxo0` once they land, never beside them.

1. **The payload (new).**
   - `ArkHold` on the `Ark`: hands out a parcel of hull prisms and stows loose artifacts.
   - `PayloadCarrier` on every vessel: RB grab within reach, carried on the hull, RB activate.
   - The crack is a prism impact effect on the same route as `SniperCrackSuperShieldPrismEffectSO`, so super-shield
     removal stays one code path.
   - `PrismMotion` is not involved: a payload cracks, it does not collide.
2. **The artifact.**
   - Reshape `ArtifactSO` from "one stat and a value" to "one payload transformation": a `PayloadTransformSO`, the
     vessel-idiom effect (reusing the vessel's own effect SOs where they exist), plus rank, keyword binding, authored
     name and line, and sounds.
   - `SquadSlot.ArtifactId` already exists.
   - `ArtifactPickup` is the loose world object.
3. **The drop rule** (if §8 accepts it). It lives where petals already settle (`ResourceSystem`), raised when the base
   band empties, so no weapon has to know about artifacts.
4. **Persistence.** What a player holds and each artifact's rank go on the player profile. That is the backend, so it
   goes through Shombith (`CLAUDE.md`: anything on UGS or the backend is his). The device-only `PilotLoadoutStore` is
   the stand-in until then.
5. **The voyage.** Vaults, the Ark's hold and extraction belong to the voyage mode built on the Arkway (`ECOSYSTEM.md`
   §41). That is a mode, so it goes through `/arcadegame`'s recipe.
6. **`/verify-unity`** before any C# commit (LOCKED).

## 7. Order

1. The base payload (Ark hold, grab, carry, crack) in one arena with the Ark. That proves the generalist answer
   without any artifact.
2. Two artifacts: one offensive (Sparrow's Warhead) and one defensive (Serpent's Keystone).
3. The drop rule and loose artifacts.
4. Vaults and extraction in a voyage.
5. Ranks and the profile.

## 8. Open decisions

| Decision | Who decides |
|---|---|
| The payload's button: RB is boost and flip today (§2) | Garrett |
| Theft: an artifact held by its carrier's petals, dropping when the base band empties (§4.3), is a reading of the LOCKED "PvP is petals only" rule | Garrett |
| What becomes of the 45 quantitative artifacts and the Hangar ARTIFACTS tab on `claude/pensive-dijkstra-qxoxo0` | Garrett and Shombith, together |
| Each vessel's artifact (§3 is a first pass) | each vessel's owner |
| Thieves hoarding artifacts | the ecology owner (ecology is LOCKED: `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`) |
| An artifact sight among the pilot callsigns | Garrett, through the fundamentals-curation process |
| Artifacts on the player profile | Shombith (backend) |
