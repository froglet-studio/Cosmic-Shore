# Unique pilots, unique artifacts and the account inventory

**Status: built 2026-11-07 on `claude/unique-pilots-artifacts-inventory`, not yet opened in a Unity editor.** The pure
rules have 20 edit-mode tests (`AccountInventoryTests`), which also pass under `dotnet test` outside Unity. The runtime
code passes the reference compile (`Tools/Build/unity_refcompile`). Hand tests: `Docs/UNITY_VERIFICATION_CHECKLIST.md`,
"Unique pilots, unique artifacts and the account inventory".

**Garrett's brief (2026-11-07):**

> "each artifact should be unique and only accept one element to make it better in one way. it should accept infinite
> elements but with diminishing returns. we need a system that can create unlimited unique artifacts so no two are the
> same. pilots should have a faction and backstory. pilot callsigns are limited to corresponding vision powers. but each
> pilot is still a unique person that is a part of one player's crew. they have unique backstories, are part of a
> faction, have a fun animal combo (human, species 1, species 2). we need a robust system that can handle the unique
> inventory attached to each account"

This builds on [`CALLSIGNS.md`](CALLSIGNS.md) (a pilot grants vision), [`ARTIFACTS.md`](ARTIFACTS.md) (an artifact
transforms the payload) and [`../Voyages/VOYAGES.md`](../Voyages/VOYAGES.md) §2 and §6.2.1 (one Ark per account, the
crew, captains and factions). It changes none of their rules; it makes the things they describe unique and owned.

## 1. The shape: a template plus a seed

Callsigns and artifact kinds stay authored assets. Every pilot and artifact a player owns is a **unique record rolled on
top of one of them**:

| | Authored template (shared by everyone) | Unique record (one account's) |
|---|---|---|
| **Pilot** | a callsign `PilotSO`: one of the fixed eighteen, each ONE vision power | `PilotRecord`: a person with a name, a human + two-animal heritage, a faction, a homeworld, a backstory and a chronicle |
| **Artifact** | a kind `ArtifactSO`: one payload transformation (Javelin, Aegis, Hydra, Aftershock) with four element tracks | `ArtifactRecord`: attuned to ONE of the kind's tracks, with its own name, ceiling, curve, fixed levels, lore and look |

**The id is the template's id, `#`, and the record's 64-bit seed in hex** (`UniqueId`):
`pilot.callsign.volt#9f3c01d2a4b85e77`, `artifact.javelin#03aa91c0f2e6d418`.

- Anyone holding only the id can still tell the callsign or kind. `PilotArtifactCatalogSO.TryGetPilot` and
  `TryGetArtifact` strip the suffix, so the match code, the payload and the LB sight need no record.
  - The ids reach many places without their records: a squad slot, a network code, an Ark's hold, a wreck, a friend's
    public squad record.
- An artifact's traits can be re-rolled from its seed (`ArtifactGenerator.FromId`), so an artifact id found in a hold
  or a wreck becomes a real artifact again.
- A bare template id with no `#` is a **legacy** id, saved between 2026-10-27 and today. It resolves to the crew's pilot
  of that callsign, or to the hoard's first artifact of that kind (§4.3).

Randomness comes from `SeededRandom` (SplitMix64), not `System.Random` or `UnityEngine.Random`. The same seed must roll
the same pilot or artifact on every runtime and every peer, forever. Each purpose (name, story, traits, look) draws from
its own stream, so adding a roll to one never shifts another.

**Records are snapshots.** Every rolled trait is stored on the record, so retuning a generator's tables never rewrites
something a player owns. `GeneratorVersion` says which tables rolled a record. Never retune a shipped version: add a
version and branch on it.

## 2. Artifacts

### 2.1 One element, one aspect

- **Each artifact accepts exactly one element**: `ArtifactRecord.Element`, which is Mass, Space, Time or Charge. The roll
  picks it from the kind's tracks.
- **It improves exactly one thing with it**: `Aspect`, the kind's track for that element.
  - Mass is how much, Space how far, Time how long, Charge how hard (ARTIFACTS.md §4).
  - A Space Javelin flies farther and nothing else ever changes.
- Omni crystals still feed it, since Omni raises any one track (ARTIFACTS.md §4), but they can only raise its one track.
- **The other three tracks are fixed** at levels rolled in 0-3. That is below the twist at 5, so only the attuned track
  ever gets its level-5 twist.

### 2.2 Unlimited and unique

`ArtifactGenerator` rolls an artifact as a pure function of (kind, seed, version):

| Trait | Range (version 1) |
|---|---|
| attuned element | one of the kind's tracks |
| attuned base level | 0-2 |
| ceiling | 7-10 |
| half-way crystals | 25-60 |
| three fixed tracks | 0-3 each |
| name | 40 epithets x the kind x 60 places ("Weeping Javelin of the Lantern Reach"), plus the serial ("9F3C-01D2") |
| lore | 8 openings x 5 closings, naming its place and its crystal |
| look | hue 0-1, 3-9 facets, one of 64 glyphs (for the art pass; nothing draws them yet) |

- **No two are the same.**
  - The seed is in the id, so two artifacts are different artifacts by construction, out of 2^64 per kind.
  - The inventory refuses a repeated id.
  - Two artifacts even *play* the same almost never: in 5,000 rolled Javelins, no two had the same numbers.
- **Unlimited.** A new artifact is one call (`AccountInventory.MintArtifact`), and nothing is authored per artifact.

### 2.3 The diminishing-returns curve

```
level(c) = base + (ceiling - base) * c / (c + half)
```

Here `c` is every crystal ever fed (`Invested`, a `long` with no limit).

- `half` crystals take the artifact half the way from base to ceiling, `3 x half` three quarters, `9 x half` nine tenths.
- Every crystal adds something, and each adds less than the one before. The gain falls with the square of the pile.
- **Nothing ever reaches the ceiling**, so there is no cap to hit and feeding never stops working.
- **Why this curve and not a logarithm.** `log(1 + c)` never stops growing, so a hoarder's artifact would outgrow the
  0-10 band every transform is authored on. The hyperbola keeps every artifact inside the band (a transform's level-10
  value is still the most it can do), while infinite input stays meaningful.
- **Calibration.** The old track cost 65 crystals from 0 to 10. A typical artifact (half about 42) is past half way at
  65, around 7 at 200, and close to its ceiling at 1,000.
  - Examples: a Space Javelin with base 0.27, ceiling 8.56 and half 47.8 reads 5.05 at 65 crystals, 6.96 at 200 and 8.18
    at 1,000.

`ArtifactCurve` holds the formula, its gain, and its inverse (`CrystalsFor`, for a "crystals to the twist" readout).

### 2.4 Feeding

`PilotLoadoutStore.TryFeed(id, crystals, payWithOmni)` runs in this order:

1. It checks the rule (`Check`): the artifact's own element or Omni, more than 0 crystals, and a balance that covers it.
2. It spends from the profile's balances (`PlayerDataService`, Shombith's section 97, as before).
3. It adds the crystals to `Invested`.

The result says the level before and after, and whether this feed crossed the twist. **Upgrades live on the artifact**
(ARTIFACTS.md §4): a stolen artifact keeps everything fed to it.

## 3. Pilots

### 3.1 A unique person on a fixed callsign

- **Callsigns are a fixed set.** `Callsigns.All` is the eighteen `PilotVision` values, each ONE vision power, with ids
  `pilot.callsign.<word>`.
  - A callsign is shared: two crews can each have a VOLT.
- **A crew holds at most one pilot per callsign** (VOYAGES.md §6.2.1). `AccountInventory.TryAddPilot` refuses a second.
- **Each pilot is a person** (`PilotGenerator`, rolled from the seed and stored):
  - **name**: syllable given names (about 18,000) and compound family names (1,600). A new pilot re-rolls until the
    name is new to the crew.
  - **heritage**: human plus two different animals (`AnimalSpecies`, 48 animals), shown as "Human / Axolotl / Koi" and
    as a blend ("Axoloi").
  - **faction**: one founding faction of a faction type (§3.3).
  - **homeworld and backstory**: five seeded sentences covering where they grew up, their heritage, how they joined
    their faction and what its members do, what their callsign lets them see, and a quirk and a want.
  - **chronicle**: key events appended as they happen ("Joined the crew.", "Promoted to captain; the crew now flies
    for the Black Tide."). This is VOYAGES.md §2's "backstory that grows with key events". The newest 24 are kept.

Example: *Wyvendo Waveline (TALON), Human / Lynx / Jackal, Hollowfen Trackers (Hunter)*. "Wyvendo Waveline grew up
mending nets on Starwake. Part human, part lynx, part jackal, Wyvendo still sleeps like a lynx and eats like a jackal.
Wyvendo ran to the Hollowfen Trackers after a voyage went badly wrong. Its members are hunters who chase what lairs in
the deep cells. Wyvendo always knows where the predators are, which is how TALON stayed alive this long. They cannot
resist a dare. What they want most is to pay back an old debt, with interest."

Text is printable ASCII (the UI font has no other glyphs). Pilots are named, or "they".

### 3.2 Animals

`AnimalSpecies` values are pinned and saved, so never reuse or renumber one. The list leaves out every vessel class
and every callsign word (Squirrel, Manta, Hornet, Doe...), so "a lynx-jackal pilot" never reads as a hull or a sight.

### 3.3 Factions

- `FactionType` now names every type in VOYAGES.md §6.2's table. Values 5-16 are new; 0-4 are unchanged.
- **New pilots are minted only into types whose jobs exist**: `FactionCatalog.Recruitable` is merchant, hunter,
  aristocrat and pirate. Any other type would bring no work to the job board.
- **Factions are shared, pilots are not.** Every type has founding factions (6 for each recruitable type, 3 for the
  rest), with ids like `faction.pirate.black-tide` and a domain per type (Ruby freedom, Gold commerce, Jade order).
- **The captain decides your faction.** `AccountInventory.PromoteToCaptain` sets the primary faction; every other
  pilot's faction is an ally (`AllyFactionIds`).
- The real roster, with an optimal size of 50 and new factions spawned when every faction of a type is full, is the
  backend's (§6.2.1, Shombith). A backend faction id later replaces `FactionId` without touching the pilot.

### 3.4 What a pilot does in play

| Where | What it reads |
|---|---|
| The match: LB sight, the callsign plate, `NetPilot` | the callsign only, unchanged |
| The voyage's job board | the person: their faction type decides which work they bring, and their name fills `{pilot}` |

For the board, `VoyageContent.CrewSource` reads the account's crew. The authored crew in `voyage_slice.json` stays the
stand-in for tests and headless runs.

## 4. The account inventory

### 4.1 What it holds

`AccountInventory` is pure data, with no engine, so every rule runs in an edit-mode test. It holds:

- `Pilots`: the crew.
- `Artifacts`: the Ark's hoard (VOYAGES.md §2: "arks hoard artifacts").
- `CaptainId`.
- `Hangar`: per vessel, the pilot and artifact it flies when no squad is deployed (the Hangar tabs).
- `StarterGranted` and `DeviceImportDone`: one-time steps.

**Invariants**, each enforced by the mutating method and repaired by `Normalize()` after any load:

- Ids are unique and well formed.
- There is at most one pilot per callsign.
- Every artifact is attuned to a track element, with crystals fed never negative and a sane curve.
- The captain and every Hangar pick name something held.
- An artifact rides one vessel at a time.
- The hoard keeps at most 1,000 artifacts. This guards the record's size (about 700 bytes each); it is not a design
  limit.

### 4.2 Save and load

The inventory is one cloud record, `ACCOUNT_INVENTORY` (`UGSKeys.Inventory`, `InventoryCloudData` schema 1, through
`InventoryRepository`). It uses the same pattern as every other player record:

- `CloudDataRepository`'s debounced saves;
- the offline snapshot in `LocalCloudDataCache`;
- failed-load protection: it never uploads a stand-in over the real record;
- `UGSDataService` loading, flushing and resetting it with the rest.

`AccountInventoryStore` is the one live holder.

- Before the account has loaded (an editor scene played directly, no sign-in), it is a stand-in read from this
  machine's snapshot of the same key. When the cloud record arrives, the cloud wins.
- `PilotLoadoutStore` remains the one facade the game calls. Its old PlayerPrefs store is gone.

Every new record is prepared once:

1. **Starter crew and hoard.** One pilot per callsign, and one artifact of each kind not found on voyages (Javelin,
   Aegis). Everything playable before stays playable.
2. **Device import.** This device's old PlayerPrefs are folded in:
   - Hangar seats become the crew's pilot of that callsign.
   - Slots become an artifact of that kind.
   - A recorded voyage find becomes an artifact of that kind.
   - Crystals spent on the old four tracks become crystals fed to that kind's first artifact.
3. **Squad adoption** (`AdoptSquads`). Saved squads that name bare callsign or kind ids now point at the crew's people and
   the hoard's artifacts, with a different artifact for each vessel. A joined squad keeps its owner's ids.

### 4.3 Squads point into it

`SquadSlot.PilotId` and `ArtifactId` now hold person and artifact ids. The squad rules (`FillPilots`, `KeepCardsOwn`)
work on ids, so "each vessel its own pilot and its own artifact" holds unchanged. Resolving is lenient:

- **A pilot id**: the person if held. A legacy callsign id, or another crew's person on a callsign this crew has, reads
  as this crew's pilot on that callsign.
- **An artifact id**: the artifact if held. A legacy kind id reads as the first of that kind not taken.
- **A unique artifact id not held** is gone (lost, or someone else's), so the slot carries nothing.

### 4.4 Voyages

- Docking home grants what was aboard (`PilotLoadoutStore.Grant`).
  - The squad's own artifacts are already held, so nothing changes.
  - A kind id from a vault mints a brand-new unique artifact.
  - A unique id from someone else's wreck is re-rolled from its seed.
- **Not wired yet**:
  - removing the squad's own artifacts from the hoard when its Ark falls (the loss rule of ARTIFACTS.md §5);
  - carrying crystals fed between accounts when an artifact is stolen.

  Both need the backend to move records between accounts. Today a stolen artifact arrives with its rolled traits and
  nothing fed.

## 5. In a match

`VesselPilotLoadout` publishes the slotted artifact whole in the 64-bit `NetArtifacts` (`ArtifactWire`):

| Bits | Holds |
|---|---|
| 8 | the kind index |
| 3 | the attuned element |
| 10 | the attuned level x 100 |
| 4 x 4 | the four fixed levels, in steps of 0.2 |
| 27 | the low bits of the seed, for the look |

Every peer fires the same payload from the code alone: `PayloadCarrier` reads `ArtifactLevels` from the loadout,
where it used to read device-local track levels. `NetPilot` is unchanged (the callsign index), and the person is
local (`VesselPilotLoadout.Person`). Arena matches still load no artifact.

`ArtifactLevels` moved to `CosmicShore.Data`, and its levels are floats. Transforms still read `ArtifactSO.Value` and
`Twisted`, and a track's value is still the lerp from its level-0 to its level-10 number.

## 6. Open questions (defaults used, change any)

1. **Starter crew size.** **(default)** One pilot per callsign, so nothing playable is lost. VOYAGES.md §2 and the
   Notion *Ark Design Document* start an Ark at three bunks, so a smaller starter crew plus recruiting (one pilot from a
   choice of three) is the likely next step. `PilotLoadoutStore.Recruit` exists for it.
2. **Starter hoard.** **(default)** One random artifact each of Javelin and Aegis. Hydra and Aftershock come only from
   voyages, as before.
3. **Curve ranges.** **(default)** Ceiling 7-10, half 25-60, base 0-2, fixed tracks 0-3. Rarity tiers (a wider ceiling
   roll) would slot in as generator version 2.
4. **Batch size on the Ark's FEED button.** It is set on the page, not the rule.
5. **Discontinuing a pilot** (VOYAGES.md §2) exists as `AccountInventory.RemovePilot` with no UI. Whether a discontinued
   person can ever come back is open.

## 7. Where everything is

| What | Where |
|---|---|
| Ids, randomness, callsigns, animals, factions | `Assets/_Scripts/Data/Inventory/` (`UniqueId`, `SeededRandom`, `Callsigns`, `AnimalSpecies`, `FactionCatalog`) |
| Artifacts | `ArtifactRecord`, `ArtifactGenerator`, `ArtifactCurve`, `ArtifactLevels`, `ArtifactArchetype`, `ArtifactWire` (same folder) |
| Pilots | `PilotRecord`, `PilotGenerator` (same folder) |
| The inventory | `AccountInventory` (same folder); `InventoryCloudData`, `InventoryRepository` (`System/CloudData/`); `AccountInventoryStore` (`System/Inventory/`) |
| The facade | `PilotLoadoutStore` (`System/Pilots/`) |
| Match | `VesselPilotLoadout`, `PayloadCarrier` |
| Voyages | `VoyageContent.CrewSource`, `VoyageRun.SeatedPilots` |
| Tests | `Tests/Editor/AccountInventoryTests.cs`, `PilotsAndArtifactsTests.cs` |
