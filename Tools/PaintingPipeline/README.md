# Painting Pipeline — Camp B (real-reference paintings)

Offline tooling that converts **real reference material** into Connect-the-Dots
paintings, baked as authored `strokes` on a `PaintingDefinitionSO` asset (the
SO's highest-priority stroke source — no runtime code involved).

This exists because first-principles procedural forms read as symbolic/childish
for representational subjects (lions, birds, paintings). Real proportions come
from real references:

| Tool | Input | Output |
|---|---|---|
| `mesh_to_strokes.py` | a 3D scan (OBJ/STL/PLY, via `trimesh`) | flight strokes: **section contours** (the engraving look — true cross-sections) + **feature lines** (sharp-dihedral ridge/valley chains: mane locks, feather edges) |
| `starry_from_image.py` | the public-domain Starry Night scan | strokes traced from the painting's own brush flow (structure-tensor streamlines), palette-quantized to Jade/Ruby/Gold, bent onto an immersive curved canvas with luminance relief |
| `bake_asset.py` | strokes + a `Painting_*.asset` path | rewrites the asset's `strokes:` block in place (validates: y≥0 rebase, flyable segments, 3-domain-only, non-planar) |

Requirements: `pip install numpy pillow trimesh scipy fast-simplification`.

## Shipped bakes

| Painting asset | Reference | License |
|---|---|---|
| `Painting_Lion'sHead.asset` | Temperance Union Lion, Las Vegas NM (Angelo de Tullio, 1896) — Thingiverse thing:985693 via the archive.org mirror | **CC0 1.0** (no attribution required) |
| `Painting_StarryNight.asset` | *The Starry Night* (van Gogh, 1889), Google Art Project scan via Wikimedia Commons — v2 retrace: 11 star/moon ring clusters, double-swirl, 6 cypress flames, curved-canvas relief | public domain |
| `Painting_Phoenix.asset` | *Striding Eagle* sculpture scan — threedscans.com / Saint Louis Art Museum ("without copyright restrictions"; courteous credit: "Striding Eagle — Three D Scans / Saint Louis Art Museum") | no restrictions |
| `Painting_Peacock.asset` | Peafowl photogrammetry scan by YahooJAPAN | **CC BY 4.0 — attribution REQUIRED and shipped in the asset's player-facing description: "3D data by YahooJAPAN (CC-BY 4.0)". Keep it there.** |
| `Painting_AlmightyMountain.asset` | Matterhorn DEM via AWS Terrain Tiles (Mapzen) | open data — **attribution REQUIRED**: "Terrain Tiles: DEM sources include SRTM (NASA), GMTED2010 and ETOPO1 (USGS), EU-DEM (European Environment Agency / Copernicus), and other open datasets; composited by Mapzen, hosted by AWS Open Data." Ship this line in the game's credits screen. |

Full sourcing/licence audit for every fetched candidate (incl. rejected
CC-BY-NC sources): `REFERENCE_MODELS.md`. Meshes themselves are NOT committed
(60–600 MB); re-download via the URLs in that manifest.

## Conventions the bakes must satisfy (validated by `bake_asset.validate`)

- Local space, base plane at y=0, front (broad side) toward +Z.
- Segment lengths flyable: > ~5u, < 0.65·W.
- Domains: Jade(1) / Ruby(2) / Gold(4) only — never Blue(3).
- Stroke order in the bake only chooses the OPENING stroke — at runtime
  `PaintingStrokeToolkit.OrderForFlightContinuity` re-sequences every painting so each stroke
  starts near the previous stroke's end (domain-contiguous, curvier strokes deferred on
  near-ties). Bakes need not hand-order beyond a sensible first stroke.
