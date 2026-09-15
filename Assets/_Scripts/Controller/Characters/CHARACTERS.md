# CHARACTERS.md — the chimeric character generator (derisking spike)

**Question the spike exists to answer:** *do these read as people you could care about?*
**Deliverable:** a contact sheet of 18 chimeras + 6 pure-human controls baked from one seed, a
blind toggle, and a single-face inspector — `FrogletTools ▸ Characters ▸ Chimera Character Creator`.
**Deletable in one command:** `rm -rf Assets/_Scripts/Controller/Characters Assets/_Scripts/Editor/Characters`.
No existing file was modified.

> **Verification status.** Nothing in this branch has been opened in the Unity editor. The
> runtime core, the editor code and the edit-mode tests were **compiled with Roslyn against
> API stubs** and the shipped test suite was **executed** (27 of 28 pass offline; the 28th reads
> a palette asset through `AssetDatabase` and is editor-only). The two contact sheets beside this
> file are rendered by an **offline rasterizer** over the exact generator output — a simulation
> of the `PreviewRenderUtility` bake with roughly the same lights, not the engine. The first
> in-editor bake is the human gate; §7 says what to look at.

![offline sheet](Docs~/CHARACTERS_offline_sheet.jpg) · blind version: `Docs~/CHARACTERS_offline_sheet_blind.jpg` (the `~` folder is invisible to Unity, so the images carry no `.meta`)

---

## 1. The model, and where the categorical / continuous line fell

Three weighted sources — human, clade A, clade B — summing to 1, each in **[0.20, 0.60]**
(`CharacterWeights`). Every source is always present; none can dominate.

**Continuous traits ride a 20-axis blend space on one shared base head** (`HeadAxis`,
`HeadShape`): cranial height/width/length, forehead bulge (the melon), brow ridge, orbital
spacing/size/depth, cheekbones, muzzle length/width, nose projection/width, lip fullness, mouth
width, jaw width, chin projection, lower-face height, neck thickness/length. The resolver blends
the human's rolled proportions with each clade's authored targets under an effective weight
`w^γ` (γ = 0.72 by default, so a 0.20 clade is still visible).

**Categorical traits swap in as discrete geometry and are never blended** (`FeatureKind`):
beak, mandibles, compound eyes, nose leaf, antennae, crest, blowhole, whiskers, fangs, pinnae,
hair cap — and *removals* (external-ear absence is a claim on the Ears slot with no geometry).

**Where the line fell, and why.** The line is *does interpolation produce a thing that exists?*
A muzzle at 40% is a shorter muzzle — real animals span that range — so it is an axis. A beak at
40% is nothing, so it is a swap. Two cases were argued and moved:

- **Eyes are categorical at the level of the ORGAN, continuous at the level of its size.** The
  vertebrate eye (eyeball + lids) and the compound eye are different objects, so `Eyes` is a slot;
  but eye size, spacing and socket depth are axes, and pupil shape / iris colour are data on the
  winning eye trait. Blending a compound eye toward a vertebrate one gives neither — same reason
  as the beak.
- **The pinna is ONE generator with data**, not three. Human, felid and chiropteran ears differ
  in height, point, cup, tragus and where they attach (`EarSide` vs `EarTop` with a site pitch),
  and every one of those tolerates interpolation *within* the generator. What does not tolerate
  it is *which ear*, so the Ears slot still resolves one winner and the winner's data is used
  whole. This is the pattern for future clades: prefer a parameterised generator + a slot over a
  new generator.
- **Coverings (skin / fur / feathers / scales / chitin / hide) are REGIONAL, not averaged.** A
  clade's covering starts at its anchor (the crown) and reaches toward the face as its weight
  rises (`CoveringRecipe.ReachDegAtMin/Max`, noisy edge). 40% of a feather is not a feather, so
  the texture obeys the same rule the geometry does.

**Weight buys traits.** Each clade's `Traits` list is a ladder, signature first. Rung *i* of *N*
is expressed at weight ≥ `0.20 + 0.40·i/N`; a trait flagged `Signature` is expressed at any
weight. An expressed trait CLAIMS slots; a slot holds one winner — signature > rung > human
default, then weight, then clade A. A trait is placed if it wins its *first* slot; its other
claims only evict competitors (a beak claims Mouth **and** Nose).

**The signature model was too thin, and the spike found it.** With one or two slot-bound
signatures per clade, Coleoptera under a heavy Corvidae lost BOTH its signatures (compound eyes
to the corvid's bead eyes, mandibles to its beak) and vanished from the face — the exact failure
the brief predicted for the two beaked clades. `CladeSignatureTests.ALighterCladeStillShowsASignatureWhenItsFirstOneCollides`
now asserts every clade keeps at least one signature under every heavier partner; it failed, and
the fix was DATA: Coleoptera's antennae (an uncontested slot), Felidae's whiskers (uncontested),
and a slot-free "Plumage" signature for Corvidae. Corvidae and Testudines never collapse into
each other: the corvid beak is long, straight and black; the turtle's is short, hooked and horn-
coloured, and its scutes are a slot-free signature that shows at 0.20.

**Genome** (`CharacterGenome`): seed, two clade keys, three weights, the rolled human base shape,
Age / Fleshiness / HairVolume, palette keys (skin tone/warmth, hair shade/warmth, iris, marking),
domain, and the resolved expression list (written by the resolver, read by nobody). JSON via
`JsonUtility`; content-hashed for the portrait cache. **Hand a genome to someone else and they
get the identical face** — asserted by `CharacterDeterminismTests`.

## 2. Pipeline

```
CharacterGenome ─► CharacterResolver ─► CharacterBlueprint       (shape, placed features, coverings, eye/keratin recipes)
                                     │
        IBaseHead ◄──────────────────┤  config.CreateBaseHead()   ProceduralBaseHead | AuthoredBaseHead
                                     ▼
                   CharacterAssembler ─► CharacterModel            (head part + feature parts in HEAD space, landmarks)
                                     ▼
              CharacterTexturePainter ─► CharacterTextures         (skin+smoothness, normal, eye, keratin, hair — pure canvases)
                                     ▼
                  CharacterBustBuilder ─► GameObject               (URP Lit materials; the ONLY Unity-facing runtime step)
                                     ▼
         CharacterPortraitBaker (editor) ─► PNG in Library/CharacterPortraits   (own PreviewRenderUtility scene, 2-render alpha)
```

Everything above the bust builder is pure C# with no components, no scene and no `UnityEngine.Random`
— the Scarab discipline — which is what let the whole thing be compiled and run outside the editor.

**Canonical space.** Head space: origin at the skull centre, +Y crown, +Z the face, +X the
character's left, head height ≈ 1. Every feature generator emits into a **site frame** in head
units (the mandible author never sees a site radius); seam features emit in *ring units* and the
assembler scales by the site's measured radius. A beetle mandible and a whale melon meet the
head at the same scale by construction.

**The attachment contract** (`AttachmentContract`, settled before the second feature existed):
a head declares named sites (`HeadSiteSpec`: polar angles, ring radius, bilateral flag, an axis
that moves it — orbital spacing moves the eye sites). Two attachment kinds, both asserted:

- **Seam** (the beak): the part's first 24 vertices are its base ring on the unit circle, CCW
  from +x, z = 0; the site samples the surface at 24 matching angles; the assembler snaps ring
  vertex *k* onto surface point *k*. Wrong count, wrong order, off the circle → throws by name.
- **Embedded** (everything rooted — eyes, ears, antennae, whiskers, nose leaf, blowhole rim):
  placed rigidly at the site with the root inset into the surface. A site the head does not
  declare → throws by name.

Bilateral sites resolve a left instance and a mirrored right one (frame reflected, winding
flipped), so every feature is generated once.

## 3. The base head and the swap plan

`IBaseHead` is the seam the spike stands on: topology, `Evaluate(shape)`, a surface sampler, and
sites. `ProceduralBaseHead` is shipped, and since the fidelity pass it is an **anatomical
sculpt expressed as a signed-distance field**, not a union of two eggs: braincase + parietal
fullness + occiput, frontal boss (the melon when `ForeheadBulge` rises), brow ridges and
glabella, orbital dishes, zygomatic mass and arch, mid-face and lower-face masses, buccal
flesh, a mandible of round cones (chin → gonion → condyle) with a chin, a throat, and a
MODELLED nose (dorsum cone, tip, alae, columella, nostril dents), lips (cupid's bow, vermilion
cones, the fissure as a thin cut) and the mentolabial sulcus — every primitive's position and
size a function of the axes, in `AnalyticSurface.BuildAnatomy`. It is sampled along rays from
the skull centre (on the eye line) onto a ring × segment grid; each ray takes the OUTERMOST
crossing (march in, then bisect), which is what keeps a brow overhang or a nose tip from
denting the surface where a single bisection would land on the wrong crossing. A lower-face
transform (`LF`) scales everything below the eye line by `LowerFaceHeight` and pushes it forward
by `MuzzleLength`, so a muzzle carries nose, lips and chin out with it; `NoseProjection` and
`LipFullness` at −1 sink their primitives into the face, which is how a beak's axis overrides
leave a smooth muzzle for its base ring. Integer `HeadDetail` decides topology; `Portrait`
(160 × 192) for baking, `Runtime` (40 × 56) as a decimation target — `HeadTopologyTests`
asserts no shape ever changes a count.

Three features were rebuilt to the same standard. `VertebrateEyeFeature` gives the eyeball a
corneal bulge, the fissure a nasal-biased upper peak and temporal lower trough, the upper lid a
fold, and both lids an orbital skirt that is **sampled off the head surface** so the lids seal
to whatever socket the head sculpted (plus a caruncle). `PinnaFeature` is a sculpted profile —
helix rim, scapha, antihelix, concha bowl, tragus, antitragus, fleshy lobe — on an ear-shaped
outline, still one generator for human / felid / chiropteran ears. `HairCapFeature` is a thin
scalp cap plus a field of two-sided STRAND CARDS combed from a parting and falling with
gravity over scalp, temples and forehead, plus eyebrow cards along the brow ridge (painted in
a darker band of the hair texture).

**If the procedural human still fails the human gate**, the fix is one file:
`CharacterGenerationConfigSO.Head = Authored` + an `AuthoredHeadMesh`
whose blend shapes are named after `HeadAxis` members (`MuzzleLength`, optionally `MuzzleLength-`
for the negative direction), scaled to head space. `AuthoredBaseHead` evaluates it linearly like
every real character creator, ray-casts the blended mesh for the surface sampler, and takes its
sites from `AuthoredSites`. **Nothing downstream — resolver, assembler, painter, baker, tests —
changes.** The one thing an authored head loses is the analytic UV↔direction mapping; it uses a
plain spherical projection instead, which the painter tolerates.

## 4. Textures

Procedural, painted into pure `TextureCanvas`es and uploaded once. The skin canvas is one pass:
`SkinBaseLayer` (tone × warmth + the subsurface regions a face has: warm cheeks/nose/ears, cool
sockets, lighter forehead, age mottling) → each clade's `MarkingsLayer` covering composited by
its coverage, heaviest last → `SurfaceDetailLayer` pores/strands/barbs/scute seams/chitin polish
as colour *and* height (the normal map is derived) → `FaceDetailLayer` painted features keyed on
landmarks: a faint eyebrow underlay (the brows themselves are hair cards), lash line, socket
shading, lip colour and the philtrum shadow over the modelled lips, nostril shadow under the
modelled alae, the blowhole aperture, the domain adornment. Every colour constant is authored in
sRGB — the albedo canvases upload as sRGB textures — so a value here is what you would pick in a
colour picker, not a linear intensity; a beard shadow is rolled per individual off the seed. Alpha carries smoothness per covering.
`IrisTextureLayer` paints the eye (round / slit / bar / all-dark pupils; hexagonal facets for a
compound eye), `KeratinTextureLayer` the beak with its gape line, `HairTextureLayer` the hair.

**Palette.** `Docs/PALETTE.md`'s set is the *domain* palette, and a person should not have teal
skin. Decision, in ONE file — `CharacterPaletteBinding.cs`: skin, scale, feather and chitin keep
ranges of their own (on the human config / each clade), and the **domain enters as an accent** —
the outer iris ring, a tint on the markings, and three painted strokes at each temple — read
through `SO_ColorSet.GetDomainSignalColor`, the accessor every domain-tinted UI uses (falling
back to PALETTE.md §2.4's table when no colour set is available, asserted equal by
`CharacterPaletteBindingTests`). The alternative is kept in the same file:
`Mode = DomainSkin` also blends the signal colour into the skin base at `DomainSkinTint`.

## 5. The critique map — what a person would say, and the ONE file that answers it

| Complaint | File |
|---|---|
| "the jaw reads wrong", "the chin recedes", "the cheeks bulge", "the brow is too heavy", "the eyes are too far apart", "the nose is too small / the nostrils", "the lips don't read", "the skull is an egg" | `ProceduralBaseHead.cs` — every primitive in `AnalyticSurface.BuildAnatomy`, the global form in `Form.Default`, site angles in `DefaultSites` |
| "the eyes look dead", "the eyes bulge", "the lids are wrong", "the eye is too big" | `VertebrateEyeFeature.cs` (+ the human clade asset's `Eye` params: radius, iris fraction, lid open) |
| "the eyes look dead" (iris, pupil, sclera) | `IrisTextureLayer.cs` |
| "the eyes look dead" (lash line, socket shadow) / "the lip colour" / "the nostril shadow" | `FaceDetailLayer.cs` |
| "the mouth corners are wrong" (geometry: the fissure and lip cones) | `ProceduralBaseHead.cs` `BuildAnatomy` — the mouth block |
| "the skin texture is plastic" (colour, blood, mottling, beard shadow) | `SkinBaseLayer.cs` (+ the skin palette on the config asset, in sRGB) |
| "the skin texture is plastic" (pores / fur / barbs / scutes / polish) | `SurfaceDetailLayer.cs` (+ `DetailNormalStrength` on the config asset) |
| "the beak reads wrong" | `BeakFeature.cs` (shape) · the clade asset's `Beak` params (length, hook, colour) |
| "Corvidae doesn't read" — the LADDER (what 0.20 buys) | `CharacterResolver.cs` (`RungThreshold`, slot priority) |
| "Corvidae doesn't read" — the DATA (what its rungs are, signatures) | `Resources/Characters/Clades/Clade Corvidae.asset` (regenerate with `author_character_assets.py`) |
| "20% clade isn't enough to see" | `CharacterGenerationConfigSO.TravelGamma` (continuous travel) — and the clade asset's `ReachDegAtMin` (covering) |
| "the feathers stop in the wrong place", "the markings are wrong", "the stripes" | `MarkingsLayer.cs` |
| "the ears are wrong" (any clade), "the ears are discs" | `PinnaFeature.cs` (the profile) · the clade asset's `Pinna` params + `SitePitchDeg` |
| "the hairline is wrong", "the hair is a helmet", "the hair sticks out", "the eyebrows are wrong" | `HairCapFeature.cs` (+ the human clade asset's `Hair` params: strand count/length/width, parting, fringe, brow cards; `HairTextureLayer.cs` for the strand streaks and the brow band) |
| "the feature is floating / in the wrong place / the wrong size" | `CharacterAssembler.cs` (placement) or the site angles in `ProceduralBaseHead.DefaultSites` |
| "a chimera has two of something / lost something" | `CharacterResolver.cs` (slot resolution) |
| "the domain colour is wrong / the skin should be teal" | `CharacterPaletteBinding.cs` |
| "the portrait is lit wrong / framed wrong" | `CharacterPortraitBaker.cs` |
| "the weights slider feels wrong" | `CharacterWeights.cs` |
| "the humans all look alike" | `GenomeRoller.cs` (what is rolled) · `CharacterGenerationConfigSO.HumanVariationSigma` |
| "add a seventh clade" | a new `.asset` under `Resources/Characters/Clades` — **no code** |
| "add a new kind of discrete feature" | one new `*Feature.cs` + one case in `FeatureCatalog.cs` |
| "the procedural human is hopeless, use a sculpt" | `CharacterGenerationConfigSO` (`Head = Authored`, the mesh) — see §3 |

Every row was checked against the code: none of the named fixes requires editing a second file.
Two honest caveats. Retuning a *site angle* (in `ProceduralBaseHead`) moves both the geometry
and the painted landmark, which is the point — but it is one file only because sites live with
the head. And a clade `.asset` is generated: edit the table in
`Assets/_Scripts/Editor/Characters/author_character_assets.py` and re-run it, or edit the asset
in the inspector and accept that `--check` will then report drift.

## 6. Running it

- **Window:** `FrogletTools ▸ Characters ▸ Chimera Character Creator`. *Contact Sheet* tab: set a
  seed, **Bake sheet**, then the **blind toggle** hides every label so you can try to name the two
  clades before revealing them. *Inspector* tab: pick clades, drag the three weight sliders (each
  drag holds its value and re-distributes the other two inside the region), re-roll the
  individual, bake, save/load a genome JSON, copy the JSON to the clipboard.
- **Bake cost:** the first sheet is ~24 busts × (SDF head at 160 × 192 + ~700 hair cards +
  1024² paint) — expect a few minutes on the first bake, then the cache makes it instant.
- **Cache:** `Library/CharacterPortraits/<hash>_<size>_<version>.png`. Bump
  `CharacterPortraitBaker.BakeVersion` when the generator changes, or use *Clear portrait cache*.
- **Assets:** `python3 Assets/_Scripts/Editor/Characters/author_character_assets.py` writes the
  six clades + the human source + the config + every `.meta` under the two folders; `--check`
  fails on drift; guids are md5 of the path, so re-runs are byte-stable.
- **Tests:** `Assets/_Scripts/Editor/Characters/Tests/` — weights (sum, range, rolls, holds),
  signatures at minimum weight and under a heavier partner, determinism (seed, JSON, textures),
  topology under a shape sweep, NaN sweep (random chimeras, humans, every clade pair at every
  weight corner, every axis at ±1), the attachment contract, the palette binding.
- **Offline harness** (what produced the sheets here): compiles the shipped `.cs` against ~250
  lines of stubs with Roslyn, runs the generator, dumps OBJ + PPM, rasterizes in numpy. It lives
  in the session scratchpad, not the repo; the point of recording it is that the whole runtime
  core is stub-compilable and RUNNABLE outside Unity, so the next person can do the same in
  twenty minutes — see `.claude/skills/asset-surgery` §4 for the recipe.
- **If a portrait bakes magenta or black:** `CharacterPortraitBaker.Capture` calls
  `preview.Render(true, false)` exactly as the codex baker does with the same URP Lit shader; if
  this project's preview path needs the scriptable pipeline, that second argument is the one
  change.

## 7. Verdict (offline, before the human gate)

Two passes: the first proved the architecture on a cartoon head; the second (the fidelity
pass) replaced the head, eyes, ears and hair. The sheets in `Docs~/` are from the second.

**Where it holds.**

- **The architecture holds, and it survived a total replacement of the base head.** The SDF
  sculpt, the sealed lids, the ear profile and the strand hair landed with no change to the
  resolver, the assembler, the painter, the clade data model or the tests — the seams the brief
  asked for are real seams. Genome → face is deterministic and JSON-portable; topology is
  invariant under every shape; every clade pair at every weight corner assembles with no NaN
  and every seam passes the contract; a seventh clade is an asset.
- **The categorical / continuous split holds, and the ladder works.** A 0.20 Corvidae is a
  person with a crow's beak and dark eyes; a 0.50 Corvidae on a Cetacea base is a crow-headed
  thing with no hair and a blowhole. Signatures read at a glance — beak, slit eyes + pointed
  ears + whiskers, compound eyes + mandibles + antennae, blowhole + melon, scutes + hooked horn
  beak, great ears + nose leaf — and Corvidae and Testudines distinguish.
- **The control now reads as people.** Six seeds give six faces with different skulls, noses,
  jaws, brows, skin, hair and beard shadow; eyes sit in sockets under real lids with a corneal
  highlight; lips have volume and a fissure; ears have a helix. They are stylised — clay /
  game-character people, a shade caricatured — but they are faces you can read an age and a
  temperament off, which the first pass's dolls were not. The chimeras read as *those* people
  with something animal done to them.

**Where it still breaks — honest list, in order of what the user will see first.**

- **Expression.** The resting mouth reads as a faint smile on most seeds because the lip cones
  and the fissure are one authored shape; a per-individual mouth-corner axis (or a 21st
  `HeadAxis`) is the fix — an hour, `BuildAnatomy`'s mouth block.
- **Hair is cards, not hair.** It reads as hair at sheet distance and as straw up close; the
  cap shows through where the strands thin, and side strands can splay at the temples. A real
  fix is a hair asset or alpha-tested cards — the pipeline has no alpha today.
- **Coverings are painted, not modelled.** Fur, feathers and scales are texture + normal; a
  cat ear is furry in life. Fix cost: a fur/feather card layer per covering, on the same
  ribbon code the hair uses — a day each.
- **The lips are faceted at portrait distance** (the fissure is a 5–8 ring feature on a
  160-ring head). A denser row distribution over the face, or a lip feature on the Mouth
  slot, fixes it; the mouth still cannot open.
- **One keratin colour per bust.** The Keratin slot takes the heaviest keratin feature's colour.
  Fix cost: per-feature material slots — an afternoon.
- **Not profiled, not baked in-editor.** ~8 s per bust offline at portrait detail (the SDF march
  dominates); the first in-editor run will tell whether `PreviewRenderUtility` needs the
  scriptable-pipeline flag (§6), and whether URP Lit with these textures matches the offline
  rasterizer's read (the harness decodes the sRGB albedo and uses no tonemapper, as the preview
  utility does).

**What the sheet says.** A viewer can point at any cell and say what it is, and at the human
row and say *who* it is. Whether these are people you could care about is the human gate this
branch is being handed over for; the offline answer is "closer than the brief expected from a
procedural head, and every remaining complaint lands in one file".
