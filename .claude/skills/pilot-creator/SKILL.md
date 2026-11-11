---
name: pilot-creator
description: Create a Cosmic Shore PILOT as a signature triple-hybrid bust - a unique person (human + two animals, a faction, a callsign) rolled exactly as the game's PilotGenerator mints them, painted as a head-and-shoulders bust where the first animal owns the face, the second owns the crown and a heritage mark, and the human owns the eyes, hair and a hand, then cut into the player avatar (Assets/_Graphics/Profile/Pilots/Avatar_<NAME>.png). Use on "/pilot-creator", "make a pilot", "give this player a unique pilot", "roll a pilot", "paint a bust / avatar for <pilot>", "the avatar only shows one animal", "repaint <pilot>'s avatar", "add a pilot to the avatars", or any change to Tools/Build/pilot_art/bust.py, creator.py or Docs/Pilots/art/busts/. The full-body Hangar portrait is /pilot-art.
---

# Pilot Creator: the signature triple-hybrid bust

Garrett, 2026-11-10: "can you get us pilot images that really show both clades in the avatar bust when used in that
context. I want to produce a character creator skill that will allow us to give players unique pilots. I want you to
send the quality to its highest levels such that when people see it they know it is cosmic shore because only we have
triple hybrid people with that unique and high quality style." Six samples got "these are definitely improvements";
then all 50 avatar pilots were painted this way (Docs/Pilots/PORTRAITS.md section 7).

## 0. The bar

A stranger who sees the avatar at chip size names THREE heritages: the face's animal, the crown's animal, and a
human (the eyes and the hand). The full-body portraits (/pilot-art) lend the second animal a tail or wings, which an
avatar crop cuts off; a bust is painted AS a bust, so every heritage is inside the square.

**The zones (bust.py, never a hand prompt):**
- **First animal: the FACE** (`FACE[a]`): skull, muzzle or beak, the coat on the face. If its thumbnail tell grows
  from the head (antlers, horns), the face keeps a SMALL version (the moose without antlers read as a horse).
- **Second animal: the CROWN** (`CROWN[b]`: ears, horns, crest, fins, coronet, antennae) **and the HERITAGE MARK**
  (`MARK[b]`): its pattern flowing up from the shoulders over the nape and across ONE cheek like a birthmark.
- **Human: the EYES** (almond, white sclera, natural iris, human brows), **the HAIR** and **ONE HAND** in frame.

**The house look every bust shares:** the #222645 deep-sea plate, a luminous ring halo and rim light in the
faction's DOMAIN colour (Ruby #C800C3, Gold #FFA700, Jade #0EC0B6), a thin glowing domain-colour ring around each
iris (the pilot's sight), drifting crystal motes, a warm key from the upper left, premium painterly finish "like a
collectible character card", three-quarter view, the head in the upper middle with clear space above everything
that grows from it, cropped at mid-chest. No text, no frame, no second character, no firearm.

## 1. Read before you touch

- `Tools/Build/pilot_art/bust.py` (the tables and the brief) and `creator.py` (the roll, the catalog, capture, sheet).
- `.claude/skills/pilot-art/references/LESSONS.md` section "signature busts" and `PAINTERS.md` section 1.
- The pilot record: `Docs/Pilots/INVENTORY.md` section 3; `Assets/_Scripts/Data/Inventory/` (`AnimalSpecies`,
  `FactionCatalog`, `PilotGenerator`, `SeededRandom`).
- `git fetch origin && python3 Tools/Build/who_works_where.py query Tools/Build/pilot_art Assets/_Graphics/Profile`.
  Pilots and their art are Garrett's; the profile window and avatar picker are Shombith's UI.

## 2. Commands

```
python3 Tools/Build/pilot_art/creator.py roll TALON 0x3039     # the person the game mints for that seed
python3 Tools/Build/pilot_art/creator.py roll TALON            # a fresh seed: a new unique pilot
python3 Tools/Build/pilot_art/bust.py --callsign TALON --heritage Mole Newt --faction Pirate --name "Lulolo Dawnwhistle"
python3 Tools/Build/pilot_art/creator.py brief <WORD>          # the brief of a catalog pilot (the 50 avatars)
python3 Tools/Build/pilot_art/creator.py list                  # the catalog and which have a bust
python3 Tools/Build/pilot_art/creator.py capture <WORD> <url>  # save a painting to Docs/Pilots/art/busts/<WORD>.jpg
python3 Tools/Build/pilot_art/creator.py sheet out.png [WORD...]   # bust + octagon avatar sizes, for looking
python3 Tools/Build/author_pilot_avatar_assets.py              # re-cut every avatar (256 px) from its bust
python3 Tools/Build/author_pilot_avatar_assets.py --check      # the drift gate
for m in bust creator; do python3 Tools/Build/pilot_art/$m.py --self-test; done
```

## 3. The round

1. **The person.** For a player's pilot, `roll <CALLSIGN> <seed>` gives the same name, heritage and faction the game
   mints (the C# tables are read from source and `SeededRandom` is ported bit for bit; the self-test holds it). For
   a catalog pilot, `brief <WORD>`. A new legacy-style pilot is a row in `creator.LEGACY` (append only).
2. **The brief.** Read it: does it name the face's animal, the crown, the mark, the human eyes, hair and hand, the
   domain colour? A wrong or weak animal is fixed in `bust.py`'s TABLES (`FACE`, `CROWN`, `MARK`, the `LEGACY_*`
   lines), never in the prompt by hand: every pilot with that animal inherits the fix. Then `--self-test`.
3. **Paint**: Figma `generate_image`, model `gpt-image-2.5-sunburst`, 1024x1024, the brief verbatim, into the file
   "Cosmic Shore Pilot Art" (key `BPjqBNIQgtY3L6RvghdWcg`). Each call spends Figma AI credits: say the count up
   front. Paint in batches of five and capture each batch before the next.
4. **Capture** with `creator.py capture <WORD> <image.url>` (curl; `www.figma.com` is on the Default environment's
   allow-list since 2026-11-10). Never `get_screenshot`: the Collab seat's read limit is about 30 calls a day.
5. **Look** at the 256 px thumbnail the tool returns, then at the avatar sheet. A pilot whose second animal does not
   read (no crown, no mark) is repainted once with the same brief (Flux's orca fin came on the second try); if it
   fails again, strengthen the table line. Note weak ones in LESSONS.
6. **Write** the avatars: `author_pilot_avatar_assets.py` then `--check`. Ids are append-only (1-18 the callsign
   pilots, 19-50 the legacy painted pilots); guids are `md5("CosmicShore/PilotAvatar/<WORD>")` and never move.
7. **Commit** the busts, the avatars, any table change and the lessons together:
   `feat(pilots): signature bust of <Person> (<WORD>)`, "Affects: Garrett's pilots; Shombith's profile UI".
   No C# changes, so no reference compile; say so.

## 4. Rules

- The second animal is never only a tail or wings: it must own something on the HEAD.
- The human is never only implied: white sclera, human brows, five fingers.
- The domain colour comes from the faction (Ruby freedom, Gold commerce, Jade order), never from an element.
- The 32 painted full-body references in `Assets/_Graphics/Pilots/` are never overwritten.
- A bust is painted with its own background (no matte); the avatar is the whole frame less a 4% margin.
- Per-player busts painted on recruit are not built yet: that needs a backend painter and is Garrett's call,
  Shombith's for anything on the backend (PORTRAITS.md section 6, decision 2).
