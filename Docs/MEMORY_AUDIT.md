# Memory Audit — runtime + asset/build footprint

Companion to `PERFORMANCE_OPTIMIZATION.md`, which covers frame cost. This one
covers **what the game holds**, in two halves that fail differently: runtime
memory (never recorded — capture-gated) and the asset/build footprint (measured
2026-09-09 against `78a95259f`).

---

## 1. Runtime memory — never recorded

`DiagnosticsHUD` already exposes a **Memory** section (F7 → F6) and nobody has
written its numbers down. Read it during Captures A–C
(`Docs/PERFORMANCE_CAPTURE_RECIPES.md`) and fill this table in:

| Reading | Capture A (boot world, 1 / 4 / 8 min) | Capture B (benchmark) | Capture C (live mode) |
|---|---|---|---|
| GC per frame | | | |
| Managed heap | | | |
| Unity allocated | | | |
| Reserved vs device RAM | | | |
| Gfx driver | | | |

Add alongside them, since nothing surfaces these today:

- **Pool footprint** — `GenericPoolManager` buffer depths × prefab cost.
- **`PrismSpatialIndex` NativeArray high-water** — the packed arrays scale with
  `_highWaterMark`, which is also what §4 Tier 1 #1's four sync scans iterate.
  One number explains both a memory figure and a frame cost.
- **`PrismRenderService.LiveEntityCount`**.

**Why the boot world is the interesting case:** the Lattice cell reaches
~63,360 prism colliders + 1,080 heart colliders at cap over ~7 minutes
(`Docs/ECOSYSTEM.md:6172`, `:6236`). Reserved memory that climbs and does not
come back down after the population stabilises is the thing to look for.

---

## 2. Asset / build footprint — measured

`Assets/` totals **1,383 MB**. By extension, largest first:

| Type | Size | Verdict |
|---|---|---|
| `.png` | **449 MB** (1,367 files) | Breadth, not a few whales — see §2.2 |
| `.wav` | **220 MB** | **Import settings are the finding** — see §2.1 |
| `.a` | **208 MB** | FMOD platform libs — see §2.3 |
| `.mp4` | **171 MB** (43 files) | Menu previews — see §2.4 |
| `.fbx` | **69 MB** | `SparrowModel4.fbx` alone is 30 MB |
| `.prefab` | **45 MB** | `SpawnedSegments.prefab` alone is 24 MB |
| `.dll` | 41 MB | |
| `.mp3` | 41 MB | |
| `.asset` | 23 MB | |

> **Measure this with a path-safe walk, not `find | xargs du`.** Many of these
> filenames contain spaces (`cosmic shore chill time 3.wav`) and one contains
> a non-ASCII character. An earlier pass used `xargs`, which word-split the
> paths, missed all four music tracks and reported the audio finding as ~5% of
> its real size.

### 2.1 Audio — the four music tracks are the whole finding

| File | Size | `loadType` | `compressionFormat` | `quality` | `loadInBackground` | `3D` |
|---|---|---|---|---|---|---|
| `cosmic shore chill time 3.wav` | 54.8 MB | **0** | 1 | **1** | 0 | **1** |
| `cosmic shore 3 chalres.wav` | 50.5 MB | **0** | 1 | **1** | 0 | **1** |
| `COSMIC SHORE 2 TUNE.wav` | 40.2 MB | **0** | 1 | **1** | 0 | **1** |
| `cosmic shore 3 looped 1.wav` | 36.4 MB | **0** | 1 | **1** | 0 | **1** |

**181.9 MB, and every setting is wrong for music:**

- `loadType: 0` is **DecompressOnLoad** — fully resident PCM.
- `compressionFormat: 1` (Vorbis) at `quality: 1` is near-lossless, so it
  barely compresses.
- `loadInBackground: 0` — decoded on the loading thread.
- `3D: 1` — **on music**, which pays spatialisation for a stereo bed.

All four are referenced through `_SO_Assets/Songs/*.asset`, so they ship.

**Project-wide: 200 of 214 audio clips are DecompressOnLoad**, of which 10 are
over 1 MB (the four above, `FTUE/Sound FX/TypeEffect_LaterToBeRemoved.wav` at
4.4 MB, four NiceVibrations haptic samples, one SFX).

**The fix is an import-settings pass, not code:** Streaming + quality ~0.5–0.7 +
background load + 2D on the music; and a sweep of the remaining
DecompressOnLoad clips, where anything over ~200 KB wants CompressedInMemory.

### 2.2 Textures — 449 MB spread thin

Only **three** files are ≥4 MB, so there is no top-10 list to cut:

- `_Graphics/Texture/bkgrd_normal.png` — **15.2 MB**
- Two more at 7.4 and 7.1 MB, both in
  `_Graphics/Texture/Noise Texture Collection (Angelo)/`

That noise folder holds a cluster of 7 MB files. **Audit it for reference count
first** — a noise texture collection is exactly the kind of asset that ships
whole when two files are used.

Otherwise the work is per-import: max size, compression format, and whether
each file is referenced at all. 1,367 files is a sweep, not a decision.

**Texture streaming is OFF in all five quality tiers**
(`streamingMipmapsActive: 0` on Very Low / Low / Medium / High / Very High)
with `streamingMipmapsMemoryBudget: 512` already configured. Switching it on is
one field per tier and the budget is already authored.

### 2.3 FMOD platform libraries — 244 MB, most of it unshippable anyway

| Platform | Size |
|---|---|
| html5 | 77.7 MB (**two SDK versions present: 2.0.19 and 3.1.8**) |
| visionos | 45.6 MB |
| ios | 45.1 MB |
| tvos | 39.7 MB |
| win | 21.1 MB |
| uwp | 15.1 MB |

Unity excludes non-target platform plugins from a build, so this is mostly
**repo weight rather than build weight** — but it is 244 MB of clone time on
every machine and every CI runner. The html5 duplicate (two full SDK versions)
is the one clear redundancy; confirm which the project targets before removing
either.

### 2.4 Video — 171 MB of menu previews

43 `.mp4` files; `_Graphics/Video/Minigames/RampagePreview.mp4` is 16.5 MB on
its own. **The question is whether they belong in the build at all** — the
arcade card's preview window now stands the mode's real arena
(`Docs/ModePreview/`), and `SO_Game.PreviewClip` is documented as deleted. If
nothing references them, this is the single largest one-line saving available.

### 2.5 Two more, both single-line

- **`SparrowModel4.fbx` is 30 MB** and `SpawnedSegments.prefab` is 24 MB. Worth
  understanding before touching anything smaller.
- **Standalone defaults to quality tier 0 ("Very Low")** —
  `m_PerPlatformDefaultQuality: Standalone: 0`. Flagged in
  `PERFORMANCE_OPTIMIZATION.md` §0.2 and still untouched.

---

## 3. Addressables — a project, not a tweak

**No Addressables package is installed** (zero matches in
`Packages/manifest.json`). Everything above ships in the player or in
`Resources`.

Adopting it would change how audio, video and textures are loaded and is the
structural answer to most of §2 — but it is a project with a migration plan, a
build-pipeline change and a testing burden. **Scope it; do not start it as part
of a perf pass.**

---

## 4. Priority

Ordered by saving per hour of work:

1. **Audio import settings** — 182 MB, one afternoon, no code, no risk.
2. **Video reference audit** — up to 171 MB, and the answer may be "delete the
   folder".
3. **Texture streaming ON** — one field × 5 tiers, budget already authored.
4. **The noise-texture folder's reference count** — bounded, likely large.
5. **FMOD html5 duplicate SDK** — repo weight only; confirm the target first.
6. **Everything else** waits on the runtime numbers in §1, because a build-size
   win and a runtime-memory win are different problems and only §1 tells you
   which one you have.
