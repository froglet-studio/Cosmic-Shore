# The Voyage Board: the HyperSea is our Milky Way

**Status: the board is an artifact; its sea is in the game** (Garrett, 2026-11-01: "this is excellent. get this into
unity"). The board: https://claude.ai/artifact/25CvRSxBrjcxEUngEiCzEn, its source `VoyageBoard/voyage-board.html`.
Owner: Garrett (voyages). Nothing here is canon until Garrett settles the open decisions in §5.

> "make us something that invites players on voyages through our unique universe that is just our milky way
> reimagined as an environment that facilitates faster than light travel, shipping, and messaging." (Garrett, 2026-11-01)

## 1. Where things are

- Light years, with the galactic centre at the origin and Sol at (0, 26,000). The board's y runs from the centre toward
  Sol; galactic longitude 90° lies to the left (the galaxy turns clockwise seen from the north). z is height above the
  midplane; Sol sits 65 ly above it. The disk is 1,000 ly thick (500 either side).
- A real object at longitude l, distance d from Sol and latitude b sits at `(-d cos b sin l, 26000 - d cos b cos l)`,
  height `65 + d sin b` (`HyperSea.FromGalactic`, `HyperSea.HeightOf`).
- Cells are 20 ly hexes, a minute of Ark sailing each (canon). A cell can also climb one layer, 0.82 of a cell: a
  course takes its planar steps or its climb's layers, whichever is more.
- The slice's places (proposed, [A] on the board) sit in Tumult's reach, 2,300 ly from the centre toward Sol: Tumult
  (the three harbours ride its rim) at (900, 2100, 30), Tollmoor (1300, 2360, 98), Lantern Reach (560, 1690, -34), the
  Hollow (1520, 1760, -149), Verdigris (430, 2360, -112), Saltmarch (1120, 1520, 145). They are the `galaxy` field of
  each location in `voyage_slice.json`.

## 2. The sea

The sea's thickness is the real gas density, read as a traveller perceives it: one hydrogen atom per cm³ feels like
water (g/cm³). Four log-spiral arms at a 13° pitch plus the Orion Spur, the bar, the centre's ring of reefs, real
molecular clouds (Taurus, Orion, Rho Ophiuchi, the Coalsack...), the Local Bubble and its neighbours, Tumult's bubble
and reefs (proposed), and noise for spurs and channels. Warm gas thins with height (360 ly scale); clouds have their
own heights and thicknesses.

| Sea | g/cm³ | Feels like |
|---|---|---|
| Spray | < 0.1 | fresh snow |
| Froth | < 0.6 | lighter than water |
| Water | < 5 | water, up to iron |
| Thick water | < 100 | lead, gold, osmium and past it |
| Reef | ≥ 100 | no Ark crosses it, except to berth at a place inside it |

Danger is `clamp(0.9 log10(n) + 0.6, 0, 3)`: thicker water is richer and rougher. A course's intensity is
`1 + round(0.6 max + 0.4 average)` of its cells' danger, I1 to I4. Sol's well (one cell) and the Heart's (four) are
closed to every Ark (proposed).

## 3. In the game

- **`HyperSea`** (`Assets/_Scripts/Data/Voyages/HyperSea.cs`): the board's sea model ported line for line in doubles:
  density (through the column, or at a height), arms, sea names, danger, the colour key, cells. **`HyperSeaChart`**: the
  board's Dijkstra over cells (quick cost `1 + 0.002 danger`, safe cost `1 + 0.9 danger²`), reefs passable only at
  berths, and its `measure`. Pure C#, no engine type.
- **`VoyageChart`** (`VoyageChart.cs`): the voyage content's charted locations (each a 3-cell berth) and a job's route
  sailed on the real sea, leg by leg along the lanes `VoyageRules.Route` picks; legs are cached.
- **The Voyage window**, as Garrett sketched it (2026-11-01: "clicking on a voyage expands it over the others. And the
  voyages are ... arranged in a matrix like the different arcade games. Then the episodes and saga are top level above
  the fold modals that contain their own content"; "the map divides them as a fun interactive galaxy explorer deciding
  where you want your voyages to take you"). Laid out by `Tools/Build/author_voyage_window.py`:
  - **Three tabs at the top, VOYAGES | SAGA | EPISODES** (`VoyageWindowTabs`, rebuilt 2026-11-02 to Shombith's
    pick, `Docs/MENU_REDESIGN_B.md` section 119, and again on 2026-11-08, section 151; `VoyageWindowModals` and its
    modals retired). Voyages opens first. Saga holds Tandava and its Severing (`SagaView`, Rev 77 "Saga": the picked part's art on the left, the parts on a coverflow, one Set sail; `Docs/MENU_REDESIGN_B.md` section 157), and Episodes is locked (`EpisodesView`, Rev 77 "Episodes": Episode 2 over the muted galaxy,
    "Coming soon", and a way back to the Saga; section 158). LB / RB step through the tabs and stop at either end; while the window is open it holds the bumpers
    (`HubShoulders`), so they do not slide the hub as well.
  - **The quest tracker is its own window, Quests** (2026-11-08, Rev 77 "Quests", section 151):
    `Assets/Resources/Voyages/QuestsView.prefab`, made by the same generator from this window's chrome. Its tabs are
    Episode 1 and Episode 2 (locked). Episode 1 is the quests on a coverflow of their games' card art
    (`QuestCarouselView`, `Docs/MENU_REDESIGN_B.md` section 156): the picked quest's line ends in Claim, Play quest or
    Open the Hangar. `ScreenSwitcher.EnsureQuestsModal` places it beside this window as
    the `QUESTS` modal (20), and Home's quest card opens it (`OnClickQuests`).
  - **The galaxy map** (`VoyageMapGraphic`), named HyperSea map in the window, fills the left column (`mapColumn`) over
    the edge view. It shows the sea around the places,
    painted from the density field, the galaxy in a corner with Sol and where you are looking. It is an explorer: drag
    to pan, pinch or wheel to zoom (120 to 20,000 ly across), tap a place to open the voyage that sails there, or, if
    none does, to read its line (`MapPlace`). With the open voyage's column on (`openOnGalaxy`) it opens on the whole
    galaxy (`OpenTier` 0, the ladder's own Galaxy tier) and stays there while the voyages are picked.
  - **The edge view** under it (`VoyageEdgeGraphic`): the disk edge-on at the map's own scale both ways, as the board's
    side view (true 1,000 ly thickness, a depth gauge, the places and the course at their heights, `EdgeCaption`).
  - **The voyage column** on the right (Rev 74 C3 "Place", picked 2026-11-08, section 151; `VoyageOpenCard`, built at
    runtime in place of the cards' column). It has four views:
    - **Voyage:** the voyages on a coverflow (`VoyageFlowCard`, poses from `VoyageCardRules.FlowPose`). Each card's
      face is its place's muted close-up (`VoyagePlaceArt`: the map's own density field and colour key, kept a quarter
      of its colour at 0.55 brightness, painted once per place). The Ark escort is the first card. Under the flow are
      the picked voyage's title and numbers (or the escort's difficulty stepper), the squad's presets, three hull
      buttons, the crew line, Ark crew, Story and Set sail.
    - **Ark crew:** the domain chips, then three seats, each with its hull and four settings (vessel, pilot, element,
      artifact). A press opens that setting's strip over the column, and one press picks. A hull picked from the
      voyage view saves at once; the crew view's picks are a draft that Save squad writes back. This replaces stepping
      a setting through its options one press at a time.
    - **Story:** the voyage over its place, one beat at a time (the news, the place, who brought it, the water).
    - **Underway:** a voyage or escort that is out shows its place, the underway line, its course, the squad and a dark
      Set sail ("Set sail - after this voyage").
    Its words are `VoyageLines` (`Card*`, `Story*`, `Element*`). With no column wired, the cards still open in place.
  Uncharted content draws the old flat chart, with no edge view.
- The lanes' `cells` still set how long a leg sails in the Arkway; the card shows the real distance (§5 decision 8).
- **The Ark escort** sails between charted places too (`ARK_ESCORT.md` §5): `VoyageRules.EscortRoute` picks the
  course (where the Ark lies to the nearest port or the squad's own harbour), the escort card shows it through
  `CardHomeRoute` and `CardCourse`, the map frames it while the escort is out, and the sim's report records it
  (`From`, `To`, `CourseLightYears`). Its worlds are still its own rolled set, not the course's cells (§5 decision 8).

## 4. Keeping the board and the game the same

`node Docs/Voyages/VoyageBoard/golden.js` runs the board's own model from its HTML and prints the numbers
`HyperSeaTests` holds as goldens (hash, noise, density at nine points, positions, sea names and colours, cells, and nine
courses round the reefs with their measures). Change the board's sea, then rerun it and update the test.

## 4a. Deep zoom, the real sky and the lanes (2026-11-02)

Garrett, 2026-11-01: "lets start by building up a reasonable region around sol and tumult. give us enough semantic zoom
levels that it feels good to go from the full galaxy into either of those two locations"; 2026-11-02: "the side view
... should be showing a view that makes sense of how vessel paths are using the galactic depth to have few crossings".

- **Seven tiers** (Galaxy, Arm, Sector, Region, Neighbourhood, Cell, System) down to a 4 ly view, on a quadtree of
  140,000 ly whose levels admit stars by brightness (about 900 on screen at any zoom), with a height slab from
  Neighbourhood down. Real objects: 97,296 HYG stars, 157 Harris globulars, 5,647 Hunt & Reffert open clusters; between
  them, procedural stars seeded per tile. The tiers are a ladder on the left; Sol and Tumult dive.
- **Lanes by heading [A]** (proposed, not canon): a course cruises at a height set by its heading against the local
  coreward direction, 8 lanes 20 ly apart, climbing over the first and last 40% so the climb costs no time. Courses that
  cross in plan then pass at different depths; a course heading the same way joins a lane rather than crossing it.
  Short courses cannot always reach their lane, so some crossings stay under a cell apart. `LANES_ON` in `lanes.js`.
  The game does not fly lanes: `golden.js` runs the model with them off, so `HyperSeaTests`' goldens are unchanged.
- **The side view is the course profile**: the selected or sailing course across, its height up, the stars near it,
  every lane it crosses with the depth gap, a minutes axis. With nothing selected it shows the old x-aligned slice.

**Building it.** `voyage-board.html` is generated, never hand-edited: `python3 deepzoom/assemble.py voyage-board.html`
patches `deepzoom/voyage-board.before-deepzoom.html` (the board before deep zoom) with `deepzoom.js` and `lanes.js` and
embeds the catalogue. `deepzoom/build_data.py <downloads> realsky.json` rebuilds the catalogue; without a fresh
`realsky.json` beside it, `assemble.py` reuses the one already embedded in `voyage-board.html`.

**Licences** (read 2026-11-02; not legal advice, check before a paid release):
- HYG v4.1 (David Nash, astronexus): CC BY-SA 4.0. Usable commercially with credit; the board's star table is adapted
  from it, so that table stays CC BY-SA and ships as plain data, never under DRM. Game code is unaffected.
- Harris 1996 (2010 edition, McMaster): free with credit; no fee may be charged for the catalogue itself.
- Hunt & Reffert 2024 (A&A 686, A42, CDS VizieR): VizieR's terms could not be read from this session. The copy used came
  from a mirror whose packaging is All Rights Reserved, so only upstream columns are kept (name, position, distance,
  members, age) and the data must be re-pulled from CDS before shipping.
- Gaia DR3 is CC BY-NC 3.0 IGO, not usable in a paid game without ESA's terms; nothing here uses it.
- NASA Exoplanet Archive (not used yet): acknowledgement only.

## 4b. Deep zoom in the game (2026-11-02)

Garrett, 2026-11-02: "i was expecting to be able to browse the vicinity and preserve all the ability to zoom into the
points of interest etc. but when i zoomed in on bleeding edge it had none of the detail i saw in the artifact." The
Voyage window's map now zooms as the board does: from the whole galaxy (140,000 ly) down to a 4 ly star system.

- **`HyperSeaSky`** (`Assets/_Scripts/Data/Voyages/HyperSeaSky.cs`): `deepzoom.js` ported line for line in doubles: the
  quadtree, the luminosity function, the star column, star colours, the model's stars per tile from the board's own
  seeds (mulberry32, bit for bit), Tumult's invented names, the seven tiers, the view state (`Lcut`, the height slab and
  the height it slices at) and the opacity, size and label rules. **`HyperSeaSkyCatalog`** reads the sky table a slice
  per frame; **`HyperSeaSkyField`** collects what a view draws, making at most 3 new tiles a frame and keeping 600.
- **The sky table** (`Assets/Resources/Voyages/HyperSeaSky.txt`, 4.7 MB, plain tab-separated text): the board's 97,293
  catalogue stars, 5,799 clusters and 48 landmarks with the drawing level the board gives each, the kinds of place and
  the syllables of Tumult's names. Written by `node Docs/Voyages/VoyageBoard/export_sky.js` (`--check` is the drift
  gate), which runs the board's own model and `deepzoom.js`, so the place filter and the famous cluster names are the
  board's. Greek letters are spelled out ("Tau Ori") and the middle dot becomes a dash, for the game's fonts. The table
  is adapted from HYG and so stays CC BY-SA 4.0: it ships as a plain text asset (no DRM on it) and carries the credit in
  its header and in every catalogue star's card.
- **`VoyageMapSky`** (`Assets/_Scripts/UI/Views/VoyageMapSky.cs`), owned by `VoyageMapGraphic`: the stars (glow over
  3.2 levels deep), globular clusters as warm glows, open clusters as dashed rings, the landmarks the content does not
  already draw, up to 48 names placed most important first without overlapping, the ladder (the seven tiers, then SOL
  and TUMULT dives with the board's flight: log-width easing, the centre moving with one over the width on a long
  zoom), the readout (`{tier}: {what} - {width} across`, plus the slice from Neighbourhood down) and the card a tapped
  star, cluster or landmark opens (what it is, its line, its source tag, its height and distances from Sol, Tumult and
  the squad, and the minutes by Ark). A tapped voyage place still opens its voyage. The edge view stands the stars and
  landmarks at their heights.
- **Readable names and the cells** (Garrett, 2026-11-02: "the words are not readable even when zoomed in, but words
  should be readable at all semantic zoom levels. at the deepest levels we should see the hexagons and you should zoom
  into the hexagons to see cells"). The map's text had copied the card eyebrow's 14 px and drawn a share of it; every
  map text now has its own size (`VoyageMapText`: places 22 px, landmarks 20, stars, clusters and cells 18, ladder and
  readout 17, caption 18, card 19, at the 1920x1080 reference) and sits on a dark plate. The 20 ly cells' outlines
  (`HyperSea.CellCentre`, pointy-top) fade in as a cell grows from 16 to 36 px across (the Region tier into the
  Neighbourhood); a cell names itself (`MapCellLabel`, its sea and coordinates) from 150 px, and from 40 px a tap on
  open water opens its card (sea, perceived density, danger, the place whose berth holds it) and outlines it; a second
  tap closes it. The Cell tier puts one cell across the view and the System tier is inside it.
- Every word is authored: the `Map*` lines of `VoyageLines` (`voyage_slice.json` `hud`, written by
  `author_voyage_assets.py`); star and cluster names, landmark lines and kind labels are the table's.
- **Goldens:** `node Docs/Voyages/VoyageBoard/sky_golden.js` prints what `HyperSeaSkyTests` holds (the luminosity
  function, the column, colours, seven tiles star by star, five views).
- Not ported: lanes by heading and the course profile (§5 decision 9, pending), crew's-eye units, pre-cut tiles on R2.

## 4c. Cells in 3D: truncated octahedra, the levels and the ecology regions (2026-11-07)

Garrett, 2026-11-07: "right now we have a 2d honeycomb and a thickness that is lacking any representation of cells.
when we zoom into cells the semantic zoom should see nuclei and membranes and represent the actual cell. cells should
nest in truncated octahedrons ... these things should be visible from both plane and thickness views"; then "include
hypersea ecology regions that are represented in the sea" and "the cellular levels".

- **The lattice** (`deepzoom/cells.js`, module `CELLS`): every cell grows round a nucleus; the nuclei sit on a
  body-centred cubic (BCC) lattice (two cubic sublattices 20 ly apart, offset by half a cell) and each cell is the space
  nearer its nucleus than any other. That Voronoi cell is the truncated octahedron (6 square faces 10 ly from the
  nucleus, 8 hexagonal faces 15 ly out), Kelvin's foam cell. The lattice is anchored so the central cell's nucleus is
  Sgr A*, the Heart [A]. Garrett asked for "the Voronoi of FCC packing"; FCC's Voronoi cell is the rhombic dodecahedron
  and the truncated octahedron is BCC's, so BCC is built and the FCC-or-BCC choice is decision 10 below.
- **The levels**: nucleus (392 u = 3.3 ly in radius, the game's `Nucleus.prefab`: a lit translucent caged sphere,
  halo, great-circle cage rings, fresnel rim), cytoplasm (the radial fill), membrane (the blue walls, deepening at the
  faces). From 200 px a cell tags its three levels; the Heart's read "Nucleus · Sgr A*, the Heart [A]" and "Membrane ·
  the circumnuclear disk [A]".
- **Both views**: the chart is a plane section at one height (octagons and squares on a nucleus layer, diamonds between
  layers); the side view is a true-scale cut upright along the chart's bottom edge at every zoom (Garrett, 2026-11-07:
  "the bottom edge of the plane [is] the slice for the thickness so they always have a nice lock up"; a Lime line marks
  the edge), and shows the course
  profile only while sailing (`lanes.js` draws the cut cells on it). A **green slice bar** in the side view is dragged
  (pointer-relative, the panel's centre frozen during the drag, one redraw a frame), scrolled (2 ly a notch) or stepped
  (Slice ▲▼ 5 ly, Reset) and moves the chart's slice; the slice snaps to the nearest nucleus layer when it is reset.
  The deep tiers' star slab follows the slice (`DZ.viewState().zf = CELLS.sliceZ()`).
- **No moiré**: polygons above 40 px a cell, a per-pixel pass below; membranes fade in from 22 to 44 px a cell, nuclei
  from 4 to 8 px of radius, and below 14 px a cell the smooth sea draws with the ecology-zone edges only.
- **Ecology regions** (the HyperSea Fact Sheet, https://claude.ai/artifact/8bvB2BA3AoUWmkt89wwGB2, its perceived densities): hot bubble = spray (< 0.1), warm medium =
  froth (< 0.6), open water (< 5), cold cloud = thick (< 100), molecular cloud = reef; the named bubbles and clouds;
  the centre's rings (the central cell 10 ly, the circumnuclear disk 30 ly, the Central Molecular Zone 650 ly). The
  hover tip names the region; the key and readout sit on dark plates.
- **Build**: `python3 Docs/Voyages/VoyageBoard/deepzoom/assemble.py Docs/Voyages/VoyageBoard/voyage-board.html` splices
  `cells.js`, `deepzoom.js` and `lanes.js` into the pre-deep-zoom page (the committed board is byte for byte the
  published artifact, version 11). Never hand-edit `voyage-board.html`.
- **Goldens**: `sky_golden.js` loads `cells.js` (the board's canvases and state stubbed) so its five views print the
  page's slab height; `cells_golden.js` prints the cells' own numbers (the nucleus a point is in, the slice's snap,
  the fades, ten sections polygon by polygon).
- **In the game** (2026-11-08; Garrett, after testing bleeding-edge: "i didn't see any of the changes", because the
  first two PRs changed only the board). The Voyage window's galaxy map now draws the same cells:
  - `HyperSeaCells` (`Assets/_Scripts/Data/Voyages/HyperSeaCells.cs`): `cellAt`, `section`, the slice's snap and the
    fades ported line for line in doubles; `HyperSeaCellsTests` holds `cells_golden.js`'s output. `HyperSeaSky.ViewOf`
    now slices the deep tiers at the snapped layer plus the reader's offset (its golden at 900,2100 w 50 is 35, as the
    page prints).
  - `VoyageMapSky` draws the slice's cells under the stars: walls fade in from 36 to 58 px a cell (the board starts at
    22 px; the map is one mesh with a vertex budget, so it starts later), each cell fills with its own sea from 60 px,
    the cytoplasm's gradient and inner walls show from 90 px, nuclei fade in from a 4 px radius, and the cell at the
    view's centre names its levels from 200 px. A tap on open water opens the 3D cell under it (its card reads the sea
    at its nucleus) and outlines its section. A Lime line marks the map's bottom edge.
  - `VoyageCellDraw` draws a cell's fill, cytoplasm and walls, and the nucleus as one quad of a sprite painted once
    into the map's atlas (the caged sphere above 16 px radius, a plain one below so the cage never aliases). The
    sprite does not flatten its rings as the slice moves off the nucleus's middle, as the board's does.
  - `VoyageEdgeGraphic` cuts the sea along the map's bottom edge; with the cells on show it draws them cut upright and
    the slice as a Lime bar: drag it or scroll over the strip to move it, tap its handle to put it back.
  - The level tags, the strip's caption at cell zoom and the bar's handle are authored lines (`MapCellNucleus` ...
    `EdgeSlice` in `voyage_slice.json`, written by `author_voyage_assets.py`). `MapCellLabel` (a hexagon's name) is no
    longer drawn.
  - Still on the hexagons: courses are routed and timed on `HyperSea.CellCentre`, a cell card's name and berth use
    the hexagon under its nucleus, and the ecology regions' names, rings and the board's slice buttons are not ported.

## 5. Open decisions (Garrett)

1. Tumult 2,300 ly from the galactic centre toward Sol (23,900 ly from Sol).
2. The Heart (Sagittarius A*) closed to Arks.
3. Sol closed by the Terminal; the Hyades as the Terminal's beacon harbour for a Sol-side start.
4. Density sets danger and reward.
5. The real 26,000 ly from Sol against Timelines' 15 hours to the edge.
6. Reefs routed round, not crossed.
7. Voyages of 8 to 55 minutes.
8. **New with the port:** the slice's lanes sail 1-2 cells while the real sea puts the same places 27-62 cells apart.
   Keep the short legs for play and show the real distance (as now), or make a leg's length its real cells.
9. **New with the lanes:** courses fly lanes by heading (§4a), a law of the sea or a custom crews keep. It changes
   course heights and so their danger; the game does not fly it yet.
10. **New with the 3D cells (§4c):** truncated octahedra are the Voronoi cells of BCC, not FCC (FCC gives rhombic
    dodecahedra). The board is BCC; confirm, or switch the lattice to FCC and its cell to the rhombic dodecahedron.
11. **New with the 3D cells:** route and time courses on the 3D cells instead of the hexagons (the cells, levels and
    slice bar are in the Voyage window since 2026-11-08, §4c).
