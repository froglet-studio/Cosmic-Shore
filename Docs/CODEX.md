# The Codex — Ethirions & Ecology

The in-game encyclopedia's data layer and the tool that authors it.

- **Ethirion** is the player-facing name for a **crystal**. Charge / Mass / Space / Time / Omni.
- **Ecology** is every **lifeform** — flora and fauna.

One asset, `Assets/Resources/Codex.asset` (`CodexSO`), holds both. The runtime UI reads it with
`CodexSO.Load()`; there is no second data path and nothing to wire per scene, which matters because
the codex is opened from more than one place and a per-scene reference is a per-scene thing to
forget.

**Tool:** FrogletTools ▸ Interface ▸ **Ethirion & Ecology Codex**.

---

## 1. What is one entry

An entry is a **page**: an ethirion, or a **species** of flora or fauna. Its variants live inside
it.

| Kingdom | Entries | Variants inside each |
|---|---|---|
| `Ethirion` | 5 — Charge, Mass, Space, Time, Omni | the 5 heart **levels** (elemental only) |
| `Flora` | 16 species — Arbor, Branching, Cacti, Coral, Frond, Gyroid, Lantern, Nerve, Pine, Quasicrystal, Reed, Rosette, SchwarzP, Spire, Tendril, Wall | the 4 **elements** |
| `Fauna` | 6 species — Brittlestar, Clawfish, QuadFish, Shark, Tadpole, Worm Colony | the 4 **elements** |

That is 27 pages over 88 lifeform config assets plus the crystal set. One entry per config would
have been exhaustive and 1:1 with the project, and would also have rendered as a wall of 88
near-duplicate tiles — the player's question is "what is a Shark", not "what is a Shark Mass".

**A crystal's impactor class is deliberately absent.** Elemental / omni / team decides *who may
collect it*, which is mechanics the palette already communicates in-world; it is not encyclopedia
content and no entry surfaces it.

## 2. The field-ownership contract

This is the whole design. A generator that rebuilds the asset from scratch is useless the moment a
designer writes a paragraph, because the next run eats it. So **Scan & Merge is always safe to
run**, and the rule is per field:

| | Fields | Behaviour on scan |
|---|---|---|
| **Harvester-owned** | `Kingdom`, `SourcePrefab`, all variant wiring, every stat with `Authored == false` | rewritten from the project |
| **Filled only when empty** | `DisplayName`, `Image`, `AccentColor`, `DiscoveryKey` | proposed; a human's value always wins |
| **Never touched** | `Tagline`, `Description`, `UnlockedByDefault`, `SortOrder`, preview pose, `FlatSilhouette`, authored stats | left alone |

Two escape hatches:

- **Detach** a harvested fact (the `AUTO` → `MINE` button) to edit it and keep it.
- **Lock against scan** (`LockAutoHarvest`) freezes an entry whole — for a page with no asset
  behind it. Entries you add by hand are locked automatically.

A codex entry whose source asset has disappeared is reported as an **orphan** and **never deleted
automatically**. A species can go missing because someone is mid-refactor, and a tool that answers
that by deleting an authored page with hand-written body copy is a tool nobody runs twice.

## 3. Species are grouped by PREFAB, not by name

Names lie. The fauna configs include a `WormColonyFaunaConfig` alongside four
`Worm Colony <Element>` assets; a name-prefix grouping files it as a fifth species. All five point
at one prefab — the thing the player actually meets — so the **prefab is the identity**, and the
display name is settled by **majority vote** among the configs sharing it (four "Worm Colony" beat
one "WormColonyFaunaConfig"). Majority rather than "first" for exactly that reason.

## 4. Images

Baked to `Assets/_Graphics/Codex/<id>.png` and imported as Sprites. The entry also keeps its
`SourcePrefab`, so a detail panel can build a live, spinnable model through `ToyModelBuilder` —
the same path the toybox's stations use.

**Nothing is ever instantiated.** Instantiating a crystal or a creature runs its `Awake` —
registries, network objects, spawn coroutines — in the editor, outside a game. Everything below
reads prefab ASSETS.

Three subjects, picked in order:

1. **A flora is asked to draw itself.** Every flora prefab in the project carries exactly **one**
   prism — the seed — because a plant is not a model, it is a growth rule. Harvesting its meshes
   photographs a single box, which is what the first pass shipped. `Flora.TryPreviewGrowth` runs
   that rule in the abstract (no prism, no spindle, no GameObject, no cell) and reports where
   prisms would land; the poses become one mesh through `CellMiniatureBuilder.BuildFromLays`. This
   is the same answer the lava lamp's Lifeform bench already reached — see `FloraIconBuilder` —
   reached through the same two calls rather than a second copy of it.
2. **Fauna are harvested.** Unlike flora they *are* authored in place: a shark's wings, belly and
   danger rods sit at real offsets on the prefab, so its meshes are the creature. Branches named
   `trail` / `vfx` / `pip` / `explosion` / `particle` are skipped — the same filter the bench's
   species stations use, so a codex icon and a station frame the same thing.
3. **A colony's body is its members.** The worm colony's root carries no mesh and no nested
   instance at all; it grows a head, body segments and a tail at runtime. When a prefab yields
   nothing, the baker lays a short chain of its `headPrefab` / `bodyPrefab` / `tailPrefab` at the
   colony's own authored spacing and taper.

Two more things worth knowing:

- **Alpha comes from rendering twice**, once on black and once on white, solving
  `a = 1 − (white − black)` per pixel. Whether a render target carries usable alpha depends on the
  pipeline, the URP asset and the shaders involved, so a bake that trusts the alpha channel works
  on one configuration and silently produces black boxes on another. Two opaque renders and a
  subtraction cannot be wrong.
- **Some gameplay shaders render nothing here, and that is expected.** Prism and crystal graphs
  read global uniforms that only exist inside a running frame. The baker measures coverage and,
  when a render comes back essentially empty, retries as a shaded neutral silhouette and says so in
  the status line. Tick **Flat silhouette** on those entries to make the choice explicit rather
  than relying on the fallback. Grown flora never hit this: they are painted with a lit material in
  the domain's colour, which is the read the lava lamp falls back to when no theme is loaded.

Per-entry **Yaw / Pitch / Padding** re-pose the camera; re-bake to apply.

## 5. Discovery

Every entry ships unlocked. `UnlockedByDefault` and `DiscoveryKey` exist so progression can be
added later without a schema change or a UI rewrite — the same way `ToyboxSO` deferred its unlock
state. **Nothing reads them yet**; do not write code that assumes otherwise.

## 6. Reading it from the UI

```csharp
var codex = CodexSO.Load();

foreach (var entry in codex.EntriesOf(CodexKingdom.Fauna))
{
    icon.sprite  = entry.Image;
    title.text   = entry.DisplayName;
    accent.color = entry.ResolveAccent(fallback);

    foreach (var stat in entry.Stats)
        AddRow(stat.Label, stat.Value);           // already formatted — no per-stat formatter

    var charge = entry.FindVariant(Element.Charge);
    variantIcon.sprite = entry.ImageFor(charge);  // falls back to the entry's image
}
```

Stats are **formatted strings, not typed numbers**, on purpose: a codex row is prose
("Breeds every 40 prisms grown"), and a typed value forces the UI to carry a formatter per stat
kind. The harvester formats once, in the editor, with the source asset in hand.

`AccentColor` uses **alpha 0 as "unset"** so the harvester can tell an authored black from an
unauthored default.

## 7. Shipping the output

The tool's real deliverable is the **asset change**, and that lands in the working tree, not the
branch. This window does **not** push: every file it writes — the codex asset and each PNG — is
recorded on the tool ledger as it is written, so **FrogletTools ▸ Build ▸ Pending Tool Changes**
lists them and they are committed and pushed by hand like any other change.

**Validate** in the toolbar is report-only: ids present and unique, every entry named and
illustrated.

This tool is **permanent**, not a one-off wirer — do not retire it. The codex needs re-scanning
every time a species or crystal is added.

## 8. Known limitations

- **No `Docs/ECOSYSTEM.md` prose is imported.** Taglines and descriptions are blank until someone
  writes them. That is deliberate — the harvester states facts, a writer states character.
- **A grown flora icon is one plant at one seed.** The seed is fixed (12345) so a re-bake is
  reproducible, which also means a species with heavy `wander` always draws the same individual.
- **Level variants exist only for elemental ethirions.** Omni has no level band because it is not
  a lifeform heart.
- **"Found in" is derived from cell configs**, so a species released only by a toy or a mode's own
  spawner reads as "Released by hand — no cell seeds it". That is accurate, not a gap.
- **Fauna speed** is probed by serialized-field name one level deep (through a species' data SO).
  A rename drops the row rather than failing the build — the right trade for an encyclopedia, but
  it means a missing row can mean "renamed" as well as "not authored".
- **Four `Wildlife Cell N Fauna Config Data` assets warn on every scan.** They are empty stubs
  (`FaunaPrefab` unset, `InitialSpawnCount` 0, `SpawnProbability` 0) sitting in the Wildlife Blitz
  cell configs. The warning is correct and there is nothing to harvest; deleting the stubs is the
  only thing that would silence it.
