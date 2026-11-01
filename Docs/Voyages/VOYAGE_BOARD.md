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
- **The Voyage window** (Garrett, 2026-11-01: "the map should be visible ... and the voyage cards below that. drilling
  into a card expands its information and causes the map to focus on that path, but the map remains. on the voyage
  card players confirm their squad mission difficulty etc."): ONE galaxy map (`VoyageMapGraphic`, stood by
  `VoyageJobsView` just before the job cards) with the sea around the places, painted from the density field, a corner
  with the whole galaxy, Sol and where this water is. Each job card is closed by default: route and pilot, title and
  the course line `CardCourse` (`{ly}`, `{years}`, `{minutes}`, `{sea}`, `{intensity}`), e.g. "1,500 LY THROUGH WATER -
  LIGHT TAKES 1,500 YEARS". OPEN (or a press anywhere on the card) expands it to its brief (`CardDetails`, or
  `CardHomeDetails` for the way home: the mission, the place, the squad's seats, the intensity, the pay, who brought
  it) with Set sail, and the map frames that job's course cell by cell. One card is open at a time; closing it gives
  the map back every place. Uncharted content draws the old flat chart.
- The lanes' `cells` still set how long a leg sails in the Arkway; the card shows the real distance (§5 decision 8).
- **The Ark escort** sails between charted places too (`ARK_ESCORT.md` §5): `VoyageRules.EscortRoute` picks the
  course (where the Ark lies to the nearest port or the squad's own harbour), the escort card shows it through
  `CardHomeRoute` and `CardCourse`, the map frames it while the escort is out, and the sim's report records it
  (`From`, `To`, `CourseLightYears`). Its worlds are still its own rolled set, not the course's cells (§5 decision 8).

## 4. Keeping the board and the game the same

`node Docs/Voyages/VoyageBoard/golden.js` runs the board's own model from its HTML and prints the numbers
`HyperSeaTests` holds as goldens (hash, noise, density at nine points, positions, sea names and colours, cells, and nine
courses round the reefs with their measures). Change the board's sea, then rerun it and update the test.

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
