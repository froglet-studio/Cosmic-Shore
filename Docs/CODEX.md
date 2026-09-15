# The Codex — Ethirions, Ecology & Tools

The in-game encyclopedia's data layer and the tool that authors it.

- **Ethirion** is the player-facing name for a **crystal**. Charge / Mass / Space / Time / Omni.
- **Ecology** is every **lifeform** — flora and fauna.
- **Tool** is the player-facing name for a **Toy** — the freestyle stations you fly into.

One asset, `Assets/Resources/Codex.asset` (`CodexSO`), holds all three. The runtime UI reads it with
`CodexSO.Load()`; there is no second data path and nothing to wire per scene, which matters because
the codex is opened from more than one place and a per-scene reference is a per-scene thing to
forget.

**Tool:** FrogletTools ▸ Interface ▸ **Codex**.

> **Naming hazard.** A **Tool** in this document is a thing in the GAME — the Vessel Changer, the
> Cell Selector. It has nothing to do with **FrogletTools**, which are editor tools. The codebase
> keeps calling the game object a `Toy` (the fundamental) precisely so the two never collide in
> code; only the player-facing surface says "Tool". `ToolCodexHarvester` is the one place the two
> words meet, and it is an editor tool that harvests game tools.

---

## 1. What is one entry

An entry is a **page**: an ethirion, or a **species** of flora or fauna. Its variants live inside
it.

| Kingdom | Entries | Variants inside each |
|---|---|---|
| `Ethirion` | 5 — Charge, Mass, Space, Time, Omni | none — a heart is sized by the **lifeform** carrying it |
| `Flora` | 16 species — Arbor, Branching, Cacti, Coral, Frond, Gyroid, Lantern, Nerve, Pine, Quasicrystal, Reed, Rosette, SchwarzP, Spire, Tendril, Wall | the 4 **elements** |
| `Fauna` | 6 species — Brittlestar, Clawfish, QuadFish, Shark, Tadpole, Worm Colony | the 4 **elements** |
| `Tool` | 6 toys — Vessel Changer, Domain Changer, Cell Selector, Wanderway, Connect the Dots, Lifeform Matrix | the **choices it offers** (hulls, worlds, paintings, kingdoms, domains) |

That is 33 pages over 88 lifeform config assets, the crystal set and 6 toy definitions. One entry per config would
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
| **Harvester-owned** | `Kingdom`, `Group`, `SourcePrefab`, `SourceConfig`, all variant wiring (including each variant's `AccentColor`), every stat with `Authored == false` | rewritten from the project |
| **Filled only when empty** | `DisplayName`, `Tagline`, `Image`, `AccentColor`, `DiscoveryKey` | proposed; a human's value always wins |
| **Never touched** | `Description`, `UnlockedByDefault`, `SortOrder`, preview pose, `FlatSilhouette`, authored stats | left alone |

`Tagline` moved from *never touched* to *filled only when empty* when the Tool kingdom landed, and
the move is safe by that tier's own definition: a blank field has no human value to protect, and a
written one is still untouchable. It buys something real — a toy already authors a player-facing
one-liner on its own definition (*"Fly through to swap your ship"*), written for exactly this slot,
and the alternative was to smuggle prose into the fact table as a stat row.

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

## 3.5 Tools: no prefab, and a category

Two things separate the Tool kingdom from the other two, and both are load-bearing.

**A tool has no prefab.** A crystal and a creature are authored objects the scan can photograph. A
toy is *built at runtime* by `ToyFactory` from its `ToyDefinitionSO` — there is no prefab anywhere
to point at. So a tool entry carries **`SourceConfig`** (the definition) where the others carry
`SourcePrefab`, `CodexEntry.HasSource` is the one question both answer, and the orphan test asks
that rather than the prefab field. Its portrait is **drawn** rather than harvested (§4).

**Every tool declares a category, and the category is a fundamental.** `ToyCategory` divides the
toybox by *what a toy changes*:

| Category | What it changes | Composes with | Today |
|---|---|---|---|
| **Pilot** | YOU — the hull you fly or the colours you wear. The world is exactly where you left it. | Vessel, Domain | Vessel Changer, Domain Changer |
| **World** | WHERE YOU ARE — a world arrives or leaves. The heaviest thing any tool does. | Cells | Cell Selector, Wanderway |
| **Creation** | LEAVES SOMETHING BEHIND that lives on without you. | Prisms/Mass, Flora & Fauna | Connect the Dots, Lifeform Matrix |

These are the **fundamentals a toy composes with**, not a taxonomy invented for a menu. A toy earns
its place by working *through* Vessel / Domain / Cell / Prisms rather than around them, so "which
fundamental does this one reach for?" is the only division that stays true as toys are added — and
a toy that fits none of them is the signal to have the fundamentals conversation (CLAUDE.md,
*Process for curating fundamentals*), not to add a fourth member.

`ToyDefinitionSO.Category` is **abstract and declared in code**, not a serialized field. Two
reasons: a toy's category is a property of what it *does*, and an authored field is a field that
can disagree with the behaviour underneath it; and abstract means a new toy **cannot be added
without saying which fundamental it reaches for**.

The category reaches the codex as **`CodexEntry.Group`** — a harvester-owned sub-heading *within* a
kingdom, which the window draws as a quieter bar inside the kingdom's section. It is general on
purpose: any future kingdom that divides gets the same treatment, and a kingdom that does not
leaves it empty. The stored value carries an ordering prefix (`1 · Pilot`) because the sections
should read **Pilot → World → Creation** — lightest touch to heaviest, the order a player meets
them in — and alphabetical would open on Creation. The window strips the prefix before drawing it.

**Facts are read per TYPE, by pattern match, not by field name.** The opposite trade from the
ecology probes (§8), and the right one here: a toy definition is a handful of assets the editor
assembly can already see, so a field rename is a compile error rather than a silently dropped row.
The switch's default arm **warns**: a toy kind with no case gets a page with only the rows every
tool shares, and adding a toy without teaching the codex what it offers should be noisy.

## 4. Images

Baked to `Assets/_Graphics/Codex/<id>.png` and imported as Sprites. The entry also keeps its
`SourcePrefab`, so a detail panel can build a live, spinnable model through `ToyModelBuilder` —
the same path the toybox's stations use.

**Nothing is ever instantiated.** Instantiating a crystal or a creature runs its `Awake` —
registries, network objects, spawn coroutines — in the editor, outside a game. Everything below
reads prefab ASSETS.

Three subjects are *photographed*, picked in order:

1. **A flora is asked to draw itself.** Every flora prefab in the project carries exactly **one**
   prism — the seed — because a plant is not a model, it is a growth rule. Harvesting its meshes
   photographs a single box, which is what the first pass shipped. `Flora.TryPreviewGrowth` runs
   that rule in the abstract (no prism, no spindle, no GameObject, no cell) and reports where
   prisms would land; the poses become one mesh through `CellMiniatureBuilder.BuildFromLays`. This
   is the same answer the lava lamp's Lifeform bench already reached — see `FloraIconBuilder` —
   reached through the same two calls rather than a second copy of it. Painted **neutral and lit**:
   in this project colour means DOMAIN, i.e. who owns it, and an encyclopedia page is nobody's.
   (The bench paints its icons in the player's domain for the opposite reason — there, you are
   about to release one.)
2. **Fauna are harvested.** Unlike flora they *are* authored in place: a shark's wings, belly and
   danger rods sit at real offsets on the prefab, so its meshes are the creature. Branches named
   `trail` / `vfx` / `pip` / `explosion` / `particle` are skipped — the same filter the bench's
   species stations use, so a codex icon and a station frame the same thing.
3. **A colony's body is its members.** The worm colony's root carries no mesh and no nested
   instance at all; it grows a head, body segments and a tail at runtime. When a prefab yields
   nothing, the baker lays a short chain of its `headPrefab` / `bodyPrefab` / `tailPrefab` at the
   colony's own authored spacing and taper.

And a fourth is **drawn**, because it cannot be photographed:

4. **A tool is drawn from the vocabulary it is built from.** A toy has no prefab (§3.5), so
   `ToolPortraitBuilder` renders its `ToyEmblem`: a **core** (what you are now) ringed by
   **satellites** (what a pass would offer you), inside the **switch ring** — the platform's one
   word for "fly through this and something happens". Every proportion is read from `ToyEmblem`'s
   own published constants, so retuning the emblem retunes the portraits with it and the two can
   never drift into disagreeing about what a toy looks like; the ring radius is *derived* from the
   emblem's outer extent rather than copied from Menu_Main's 42u-over-22u, which lands on the same
   proportion and stays right if either is retuned. The satellite count is the entry's harvested
   variant count, capped at 12 — sixteen paintings read as a smear, and **a picture is a bad place
   to state a number**; the stat row says how many.

   The obvious shortcut — call `ToyFactory`'s own builders — is wrong twice over in an editor pass:
   `AddSphereBody` discards its collider with `Object.Destroy`, which is illegal in edit mode and
   logs once per bake, and `AddRingBody` attaches a live `ToyIdleSpin` and hands out a static mesh
   nobody owns. The rule that nothing wakes up is what makes the geometry here built and owned
   outright.

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
  than relying on the fallback. Grown flora never hit this — they are painted with a material the
  baker owns.
- **The subject fills the frame.** Framing is solved from the bounds' eight corners, not from
  `extents.magnitude` — that half-diagonal is the radius of the sphere the box fits inside, which
  for anything non-spherical is far larger than the box, and it left every icon small inside a wide
  margin (worst for the long thin subjects this codex is mostly made of). `Padding` 1 therefore
  really does mean edge to edge.

Per-entry **Yaw / Pitch / Padding** re-pose the camera; **Reset pose** returns to the defaults.
Re-bake to apply either.

## 4.5 Variants have icons, and most of them are not baked

A variant is drawn as a **card in a grid** under its entry, not as a row in a list, and clicking one
opens its detail below. That is the right shape because sixteen paintings are sixteen different
*pictures* — the whole reason to have them is to see them at once.

The governing question for a variant's art is **"is this variant a distinct object?"**, and for most
of them the answer is no. That is why ~24 icons are baked across the whole codex rather than ~150:

| Variant | Icon | Baked? |
|---|---|---|
| A species' **element** (Charge, Mass, …) | that element's own **ethirion** image | no — resolved at draw time |
| A **domain** (Jade, Ruby, Gold) | its `AccentColor`, drawn as a chip | no — a PNG of a flat colour says nothing |
| A **kingdom** (Fauna / Flora / Vessels) | the entry's own portrait | no — a heading is not a thing |
| A **painting** | its strokes, coloured by domain | **yes** |
| A **hull** | the ship | **yes** |

`CodexSO.VariantImage` is the one resolver — own image, then the ethirion for an element-keyed
variant, then the entry's. It lives on the catalog rather than on `CodexEntry` because that middle
step is a *cross-kingdom* lookup, and resolving it at draw time instead of copying the sprite means
re-baking the Charge ethirion updates every lifeform that drops one, with nothing to re-scan.

Two things the hull path had to get right, both easy to miss:

- **Five of the eight hulls are skinned.** A walk over `MeshFilter`s finds nothing on any of them,
  which would have produced five blank icons and no error worth reading. `CodexImageBaker.HarvestModel`
  covers both vessel families, so a variant icon goes through it rather than through
  `ToyModelBuilder` (mesh filters only).
- **Hulls bake FLAT, always.** A vessel draws with the shared vessel graph — domain-tinted, and
  reading per-frame globals that do not exist outside a running frame — so the authored pass would
  render black, fall back to flat anyway, and cost a second render for the same picture.

A painting is the one place this codex colours by **domain**. Everywhere else a page is neutral
because colour means ownership and an encyclopedia page is nobody's; here the domains *are* the
subject, since a painting is authored as a multi-domain object and the toy recolours your trail
stroke by stroke.

**Variant labels are disambiguated at the source.** A species can carry more than one config per
element (the Wildlife roster is authored per species per cell), so "Charge" is not unique inside an
entry — such labels become `Charge · <config>`. That is not cosmetic: the label is the key the merge
matches variants on, and `ToDictionary` throws on a duplicate. It had never fired only because no
variant had ever carried an image; the first baked one would have taken the next scan down.

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

    // ALWAYS through the catalog: an element variant has no image of its own and resolves to
    // that element's ethirion. entry.ImageFor(variant) knows nothing about other kingdoms and
    // would fall straight back to the species silhouette.
    foreach (var variant in entry.Variants)
        AddIcon(variant.Label, codex.VariantImage(entry, variant),
                variant.ResolveAccent(fallback));   // no image at all → draw the accent
}

// Tools divide inside their kingdom. Group is empty for a kingdom that does not divide, so
// treat empty as "no sub-heading" rather than as a group called nothing.
foreach (var group in codex.EntriesOf(CodexKingdom.Tool).GroupBy(e => e.Group))
{
    AddHeading(group.Key);                        // "1 · Pilot" - strip the ordering prefix
    foreach (var tool in group) AddCard(tool);
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
- **Ethirions have no variants.** A heart's size is a property of the LIFEFORM, authored per
  element in that species' own variant tuning (`Docs/ECOSYSTEM.md` §40.2), so the number belongs
  on the flora and fauna entries where a reader can see whose heart it is. The ethirion entry
  states only the BAND the whole roster spans, measured from the shipped assets. Omni is not
  a lifeform heart.
- **"Found in" is derived from cell configs**, so a species released only by a toy or a mode's own
  spawner reads as "Released by hand — no cell seeds it". That is accurate, not a gap.
- **Fauna speed** is probed by serialized-field name one level deep (through a species' data SO).
  A rename drops the row rather than failing the build — the right trade for an encyclopedia, but
  it means a missing row can mean "renamed" as well as "not authored".
- **A painting icon is a signature, not the painting.** It draws up to 40 strokes at up to 24
  points each, sampled evenly across author order. A monument at full stroke count would be a
  scribble at icon size; the cost is that two paintings built from the same family of curves can
  read similarly in the grid.
- **The Cell Selector harvests no variants, and that is correct.** Its shipped asset authors an
  empty `cells` list on purpose, so the toy reads the containing `Cell`'s own config rotation —
  one source of truth for what a scene's cell can be. That list lives on a scene component, not an
  asset, so the scan cannot reach it and the page says so in words instead of inventing a list.
- **A tool's portrait is a diagram, not a photograph.** It says *core, satellites, switch ring* in
  the toy's own accent; it does not show the mini-cells, hulls or paintings a pass actually blooms.
  Building those would mean running each toy's private `IEmblemSource` — which lives on the runtime
  `Toy` component and needs a live `ToyContext` — i.e. instantiating gameplay, which §4 forbids.
- **Four `Wildlife Cell N Fauna Config Data` assets warn on every scan.** They are empty stubs
  (`FaunaPrefab` unset, `InitialSpawnCount` 0, `SpawnProbability` 0) sitting in the Wildlife Blitz
  cell configs. The warning is correct and there is nothing to harvest; deleting the stubs is the
  only thing that would silence it.
