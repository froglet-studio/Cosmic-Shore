---
name: pilot-art
description: Make a Cosmic Shore PILOT PORTRAIT - the full-body painted pilot in the Hangar's pilot card, the Squads page and the codex (Assets/_Graphics/Pilots/Portrait_<Name>.png, 220x280) - through the one pilot-art kit (Tools/Build/pilot_art/: the brief composed from the pilot's own record, the painter protocol through the Figma connector, the key-colour matte, the finish that frames to the painted set's measured envelope, the gates, the sheet beside the nearest painted references, the writer that keeps every guid and wires the PilotSO). Use for ANY request to make, replace or improve pilot art, "the pilot art came out terrible", give a callsign its portrait, paint a unique pilot (human + two animals, a faction, a callsign), retire a Portrait_*_Placeholder, hand an artist a brief, or touch Tools/Build/pilot_art/**. Loads the house style measured from the 32 painted portraits, the contract the game binds, the painters and what they cost, and the lessons. The portrait twin of /ability-icon.
---

# Pilot Art Protocol

You are about to make the picture of a PERSON the player recruits, seats and flies with: a unique pilot
(`Docs/Pilots/INVENTORY.md` section 3: a name, human plus two animals, a faction, a backstory) on one of the
eighteen callsigns (`Docs/Pilots/CALLSIGNS.md`). It shows in the Hangar's pilot card at **44x56** and in
the Squads page card's pilot image at about **180x229** (the sprite itself is **220x280**), beside 32 portraits an artist painted
(`Assets/_Graphics/Pilots/`). Garrett, 2026-11-07: "i tried to get you to make pilot artwork but that came out
terrible. you have illustrations of pilots for reference. perhaps you could try to build up a skill that could
make art like the ones we have but even better." The plan, the audit and the round status:
`Docs/Pilots/PORTRAITS.md`.

## 0. The bar, in one paragraph

> "Like the ones we have but even better." Like them: a full-body anthropomorphic animal pilot, standing
> three-quarter, painterly, rugged gear, no backdrop, measured to the set's own envelope
> (`Docs/Pilots/art/style_envelope.json`). Better: it is THIS pilot and not a generic animal in a jacket (the
> head and build of the first animal, ONE feature lent by the second, the faction's gear trimmed in the faction's
> DOMAIN colour, the callsign's mark worn on the body), framed on the same stage as every other portrait, lit to
> read on the dark navy plate at 44 px, matted clean, and judged on a sheet beside its three nearest painted
> references before anything under `Assets/` changes.

A portrait that is "an anthropomorphic animal in gear" is the floor; one where a stranger can read the heritage,
the faction and the callsign is the bar. Test it at 44x56 on the Hangar card: if the animal is not the first thing
named, the painting has failed, whatever it looks like at 1024.

## 1. Read before you touch

- `references/STYLE.md` - the house style: the measured envelope, the language, what "better" means in numbers.
- `references/CONTRACT.md` - what the game binds: size, the `.meta`, the guid that never moves, where it shows.
- `references/PAINTERS.md` - where the pixels come from (the Figma connector's image models, Weave with reference
  images, an artist), what each costs, who approves, and how a painting is captured in a cloud session.
- `references/LESSONS.md` - what each round taught. **Append to it every round.**
- The pilot: `Assets/_Scripts/Data/Inventory/` (`AnimalSpecies`, `FactionCatalog`, `PilotGenerator`) and
  `Tools/Build/author_callsign_assets.py` (the callsigns); `Docs/Pilots/INVENTORY.md` section 3 for the record.
- `git fetch origin && python3 Tools/Build/who_works_where.py query Assets/_Graphics/Pilots Tools/Build/pilot_art`.
- **Do not touch the 32 painted portraits** (`Portrait_<Name>_Placeholder.png`, `PilotPortrait_*`): they are the
  reference set the envelope is measured from, and their guids are still referenced by the legacy `SO_Captain`
  assets, Menu_Main, a dialogue and the FTUE speaker.

## 2. The pipeline (one command per portrait)

```
python3 Tools/Build/pilot_art/brief.py <spec>                  # the brief: the prompt a painter gets, verbatim
python3 Tools/Build/pilot_art/brief.py --callsign TALON --heritage Lynx Jackal --faction Hunter --name "..."
python3 Tools/Build/pilot_art/author.py <spec> --preview       # matte, finish, gate, sheet; nothing under Assets/ changes
python3 Tools/Build/pilot_art/author.py <a> <b> --preview --sheet round2 --dump <dir>   # a round on one sheet, the
                                                               # sprites also saved to <dir> for the stranger test (3.5)
python3 Tools/Build/pilot_art/author.py <spec>                 # ... and write the PNG + .meta (guid kept), wire the PilotSO
python3 Tools/Build/pilot_art/author.py --check                # the ship gate: re-finish every source, compare to disk
python3 Tools/Build/pilot_art/verify.py <png> [--table]        # measure ANY portrait, a hand-painted one included
python3 Tools/Build/pilot_art/sheet.py --refs                  # the audit sheet of the painted set with its verdicts
python3 Tools/Build/pilot_art/measure.py --refs                # re-measure the envelope (only when the set changes)
for m in brief matte finish verify measure author; do python3 Tools/Build/pilot_art/$m.py --self-test; done
```

A portrait is a spec, `Tools/Build/pilot_art/specs/<name>.py`: `PORTRAIT = dict(name, pilot, person, callsign,
heritage=(first, second), faction, faction_name, pose, extra, source, key, crop, exempt, before)`. The SOURCE is
the captured painting under `Docs/Pilots/art/sources/<name>.png` (1024x1280 over the key colour the brief named,
or with its own alpha: `key="alpha"`). The sheet lands in `Docs/Pilots/art/sheets/<name>.png` (`round.png` for
several): BEFORE at HEAD (the callsign's portrait today, or "no art"), AFTER at 220x280 with the gate verdicts,
110x140, the Hangar card at 44x56, and the three nearest painted references. **Read the sheet** (the Read tool on
the PNG) before writing; the gates are a floor, not a judgement.

## 3. The round

0. **One pilot at a time, with the owner.** A round is a few portraits, each a named person with a callsign, a
   heritage and a faction. A callsign's portrait is the first person painted for it until a per-person portrait
   exists (`Docs/Pilots/PORTRAITS.md` decision 1); say which person it is.
1. **The brief**: write the spec (a name from `PilotGenerator`'s tables so it reads like every other pilot; the
   heritage; the faction and its founding name; a pose line that says what the backstory says). Run `brief.py`
   and READ the prompt: does it name the first animal's head, the second's one feature, the gear, the domain
   colour, the callsign mark, the key colour? Fix the TABLES in `brief.py` (a species line, a motif, a gear line),
   never the prompt by hand: the next pilot of that species inherits the fix.
2. **Paint** through a painter in `PAINTERS.md`, the prompt verbatim, 1024x1280, one candidate first. In a cloud
   session the image model is the Figma connector's `generate_image` (the file "Cosmic Shore Pilot Art" in the
   Froglet Studio team; it spends Figma AI credits: say how many paintings before you make them). Capture the
   painting at full size (PAINTERS.md section 1: `get_screenshot` with the inline image, because the asset URL's
   host is not on the cloud environment's allow-list) into `Docs/Pilots/art/sources/<name>.png`.
3. **Preview and look** (`author.py <spec> --preview`): the matte's key and spread print (a spread above 0.03
   means the painter ignored "flat"), then the gates, then the sheet. Judge at 44x56 first, then at 220: the
   animal, the faction colour, the callsign mark, the light. A gate that fails names the fault: `value` is a
   painting that sank (re-brief the light, never lift it in the finish), `alpha_clean` / `no_halo` is the matte
   (a coat near the key colour: change the key in the spec, LESSONS 1 and 3), `height` / `top` is a crouch or a
   crop (the painter added a floor: `crop` in the spec, or repaint).
3.5 **The stranger test** (round 2 made it a step): the bar in section 0 is measured, not felt. Dump the round's
   sprites (`--dump <dir>`), put each on the Deep Sea plate under a NEUTRAL name (p01...) at full size and as the
   real 44x56 thumbnail shown 4x nearest, and have blind readers who see only the pixels (two at the thumbnail, one
   at full size; subagents with the Read tool, told to open nothing else) name the animals most obvious first, the
   one animal they would call the pilot, whether it reads as a person with an animal head, the trim colour and
   the worn items. A grader who knows the spec scores each: pass = the first animal named (or a close relative) at
   full size AND at the thumbnail, the colour right, humanoid; fail = the first animal missed at full size, a
   non-humanoid read, or the wrong colour; weak otherwise. A fail is repainted once with the grader's one brief
   change; a weak one ships with its note in LESSONS. The workflow script that ran it is
   `pilot-art-stranger-test` in the session's workflows folder; its shape is in `references/PAINTERS.md` section 6.
4. **Write** (`author.py <spec>`): the PNG, its `.meta` with the kit's guid, `wired.json`, and the pilot asset
   through `author_pilot_assets.py`. Then `author.py --check` and `author_pilot_assets.py --check` are green.
   The player AVATAR is no longer cut from this portrait when the pilot has a signature bust (`/pilot-creator`,
   `Docs/Pilots/art/busts/`); a pilot without one still re-cuts from its portrait: run
   `author_pilot_avatar_assets.py` (ids append-only, written into `SO_DefaultProfileIcons`) and commit the avatar.
   Never hand-edit a PNG, a `.meta` or a pilot asset's `portrait:` line.
5. **Commit** the spec, the source, the PNG + meta, `wired.json`, the pilot asset, the sheet and the lessons
   together: `feat(pilots): portrait of <Person> (<CALLSIGN>)`, "Affects: Garrett's pilots". No C# changes, so no
   reference compile; say so.
6. **The owner looks** (Garrett reviews the sheet; when he asks for ONE portrait, show the candidates on the sheet
   and pick one, nothing else changes). Fold what he says into `LESSONS.md` and, if it is a rule, into
   `STYLE.md` and `brief.py`'s tables, before the next pilot.

## 4. Rules that keep being relearned

- **The prompt is `brief.py`'s output, verbatim.** A hand-tuned prompt paints one good portrait and teaches nothing;
  a table line paints every pilot of that species, faction or callsign the same way.
- **The first animal is the head; the second lends ONE feature.** Two heads, or a second animal spread over the
  whole body, reads as a monster; one tail, one pair of wings, one shell reads as a heritage.
- **The faction decides the gear and the colour; the callsign decides one mark.** Never the element (elements
  have no colour, LOCKED), never the vessel (a pilot flies any hull).
- **Lit for a dark screen.** Every portrait sits on #222645. A dark animal in dark gear sinks below the painted
  set's highlight floor (Rosh, round 1: lum_p95 0.43, then 0.47 with edge light, against the set's 0.54
  minimum); a highlight needs AREA, so the brief gives one large garment a light value (a tan coat, a cream shirt).
- **The key colour is chosen off the pilot**: green unless the coat is green or the callsign is IVY (then blue);
  never magenta for a Ruby pilot (the matte eats ruby trim). The matte estimates the real key from the border, so
  a slightly-off green is fine; a gradient is not.
- **Nothing in the finish changes a colour or a value.** It frames, resamples and sharpens. A tone fault is the
  painter's and is fixed in the brief, so that the source stays the truth `--check` re-finishes from.
- **Gates are a floor.** Thirty of the thirty-two painted references pass them and the two that fail (Dozer
  crouching, Echo winged) are right to fail. A portrait that passes can still be the wrong pilot.
- **The painted set is not edited.** A new portrait is a new file; a placeholder is retired by a spec with
  `before=` and a wiring change, never by overwriting the artist's PNG.

## 5. Wiring and hand-off

`PilotSO.portrait` is a Sprite reference by guid; the kit writes the PNG and a `.meta` with a deterministic guid
(`md5("CosmicShore/PilotArt/<Name>")`) and records the pair in `Tools/Build/pilot_art/wired.json`;
`author_pilot_assets.py` (the owner of every pilot asset) reads that record and writes the `portrait:` line, so
the two `--check`s agree and a redraw of the same name changes no asset. A portrait with `pilot=None` is written
and sheeted but wired nowhere (a person's own portrait for the lore bible, a candidate). Unique pilots are
unlimited and a shipped PNG per person is not: today a callsign shows one canonical person; a per-person portrait
is made on request and the decision on where it lives is Garrett's (`PORTRAITS.md` section 6).

**Ownership.** The pilots and their art are Garrett's (`who_works_where.py`); the Hangar page is Shombith's UI
(`Docs/UI_STYLE_GUIDE.md`); the ship gates are Shombith's tooling. Shombith's 2026-10-26 call ("Dont create art we
will wait for an artist") was about the 40-pilot roster that was thrown out on 2026-10-27; the painted set stays
the artist's, and an artist's painting goes through the same kit (`key="alpha"`) and gets the same sheet.
