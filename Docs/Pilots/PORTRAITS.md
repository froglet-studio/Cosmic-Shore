# Pilot portraits: the plan, the audit and the rounds

**Status: round 1 built 2026-11-07 on `cece/sweet-goodall-duyp56`** (Garrett: "i tried to get you to make pilot
artwork but that came out terrible. you have illustrations of pilots for reference. perhaps you could try to build
up a skill that could make art like the ones we have but even better."). The skill is **`/pilot-art`**
(`.claude/skills/pilot-art/`), the kit `Tools/Build/pilot_art/`. Four callsigns show a portrait in the Hangar
(TALON, VOLT, SUNDIAL, BANDIT); fourteen still show "no art". Not yet opened in a Unity editor: the wiring is a
Sprite guid in four pilot assets and the Hangar card's Image has `preserveAspect`, so the hand test is "open
Menu_Main > Hangar > PILOTS and see four portraits" (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## 1. What a portrait is

A pilot is a unique person on a callsign ([`INVENTORY.md`](INVENTORY.md) section 3: a name, human plus two
animals, a faction with a domain, a backstory) and the callsign is a pair of eyes ([`CALLSIGNS.md`](CALLSIGNS.md)).
`PilotSO.portrait` (one per callsign today) shows in the Hangar's pilot card at 44x56, on the Squads page and in
the codex at up to 220x280. The portrait is a full-body painting of the PERSON: the first animal's head and
build, ONE feature lent by the second, the faction's gear in the faction's domain colour (Ruby freedom, Gold
commerce, Jade order), the callsign's mark worn on the body, framed and lit like the painted set.

## 2. Art on hand: the painted set (measured 2026-11-07)

32 portraits in `Assets/_Graphics/Pilots/` (24 named `Portrait_<Name>_Placeholder.png` plus 8 spares
`PilotPortrait_*`), 220x280, full-body anthropomorphic animal pilots, painterly, transparent. They were the
40-pilot roster's art (PILOTS_AND_ARTIFACTS.md, thrown out 2026-10-27) and have been unreferenced by any pilot
since; the legacy `SO_Captain` assets, Menu_Main, a dialogue and the FTUE speaker still reference some guids.
**They stay untouched**: they are the REFERENCE SET the kit measures its envelope from
(`Docs/Pilots/art/style_envelope.json`; the audit sheet with every one's gate verdicts:
`Docs/Pilots/art/sheets/references.png`). Thirty pass the kit's gates; Dozer (crouching) and Echo (winged) fail
`height` and `top`, which is right.

What the set does (`.claude/skills/pilot-art/references/STYLE.md`): head at the top edge, boots on the bottom
edge, the body a hair left of centre, dark (median luminance 0.23) with a real highlight, one saturated accent,
soft fur edges, no backdrop.

## 3. The kit (`Tools/Build/pilot_art/`)

| Piece | What |
|---|---|
| `brief.py` | the art direction composed from the game's data: 48 `AnimalSpecies` lines (head / lent feature / colour), 18 callsign marks, 16 faction gear lines with the domain accent hexes from `colour_domains.json`, the house style, the key colour choice; `--self-test` holds the tables to the enums |
| `specs/<name>.py` | one portrait: the person, callsign, heritage, faction, pose, source, key, exemptions, the pilot it is wired to |
| `matte.py` | the key-colour matte: key estimated from the border ring, the difference key in the edge band, decontamination, specks and pinholes cleaned |
| `finish.py` | the 220x280 sprite framed to the set's stage by measurement (top 0.010, bottom 0.995, anchor 0.49, width <= 0.96), premultiplied Lanczos, edge colour extended, a light unsharp; changes no colour or value |
| `measure.py` | the metrics and the envelope of the painted set |
| `verify.py` | 14 gates calibrated on the set, with planted-defect negative controls and the two reference outliers asserted |
| `sheet.py` | BEFORE at HEAD, AFTER with verdicts, 110x140, the Hangar card at 44x56, the three nearest painted references; `--refs` is the audit |
| `author.py` | spec -> matte -> finish -> verify -> sheet -> write PNG + meta (guid `md5("CosmicShore/PilotArt/<Name>")`) -> `wired.json` -> `author_pilot_assets.py`; `--check` re-finishes every source and is a `/ship` gate |
| `Docs/Pilots/art/sources/` | the captured paintings (1024x1280 over the key colour), the truth `--check` re-finishes from |

The painter is outside the kit (`references/PAINTERS.md`): round 1 used the Figma connector's `generate_image`
(gpt-image-2.5-flare) into the Figma file "Cosmic Shore Pilot Art" (Froglet Studio team), six paintings for four
portraits (Rosh was repainted twice for light), captured at full size through `get_screenshot` because the cloud
environment cannot download from `www.figma.com`.

## 4. Round 1 (2026-11-07): four callsigns get their first person

| Callsign | Person | Heritage | Faction (domain) | File | Gates |
|---|---|---|---|---|---|
| TALON | Wyvendo Waveline (INVENTORY.md's own example) | Human / Lynx / Jackal | Hollowfen Trackers (Hunter, Ruby) | `Portrait_Wyvendo.png` | pass |
| VOLT | Karenth Amberweaver | Human / Axolotl / Koi | Brass Lantern Company (Merchant, Gold) | `Portrait_Karenth.png` | pass |
| SUNDIAL | Delmira Stillwater | Human / Owl / Pangolin | House Verdance (Aristocrat, Jade) | `Portrait_Delmira.png` | pass |
| BANDIT | Rosh Rusthook | Human / Octopus / Raven | Black Tide (Pirate, Ruby) | `Portrait_Rosh.png` | pass (v3; v1 and v2 failed `value`: a dark pilot needs one light garment) |

The sheet: `Docs/Pilots/art/sheets/round.png`. Names are rolled from `PilotGenerator`'s own syllable and family
tables so they read like every other pilot's. What the round taught: `.claude/skills/pilot-art/references/LESSONS.md`.

## 5. What comes next, in order

1. **Garrett looks at `round.png`** and says what is wrong: the animals, the gear, the colours, the light, the
   framing. Each answer goes into `brief.py`'s tables or `STYLE.md`, then the four are repainted if needed.
2. **The other fourteen callsigns** (ENSIGN, MANGER, ANVIL, ZODIAC, OPAL, RAMPART, CITADEL, HORNET, IVY, DOE,
   WINGMAN, NEEDLE, GALLEON, FLUME), one spec each, about fourteen paintings of Figma credit; IVY takes the blue key.
3. **Per-person portraits** (section 6), once the decision is made.
4. **The hand test** in the editor: the Hangar PILOTS column with portraits at 44x56, the Squads page cards.

## 6. Decisions (defaults used; change any)

1. **A callsign's portrait is its first painted person** (Wyvendo is TALON's face for every crew's TALON) until a
   per-person portrait exists. The Hangar already pairs "the person's name and the callsign's portrait", so
   nothing in the UI changes. Alternatives: an archetype per callsign (no person), or per-person art.
2. **Per-person art for unlimited unique pilots** needs a place a PNG per person cannot be: a cloud-rendered
   portrait on recruit (the record's seed names the heritage and faction, so the brief is deterministic), a
   procedural painter (PAINTERS.md section 4), or a heritage "card" (silhouette + colours) drawn at runtime. Open;
   Garrett's call, Shombith's for anything on the backend.
3. **No firearms in the brief.** The painted set has a rifle and a bow; the kit's `NEVER` line bans firearms and
   allows the callsign's instrument. Open for Garrett.
4. **The painter spends Figma AI credits** on the Froglet Studio team's plan; a round says its count up front.
   An artist's painting goes through the same kit (`key="alpha"`).
5. **Shombith's 2026-10-26 "Dont create art we will wait for an artist"** was about the 40-pilot roster's eight
   missing portraits; that roster is gone and this is Garrett's ask for the callsign pilots. The painted set is
   never overwritten; an artist's portrait of any of these pilots replaces the model's through a spec change.
