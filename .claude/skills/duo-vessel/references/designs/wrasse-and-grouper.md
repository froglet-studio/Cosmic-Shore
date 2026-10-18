# The Wrasse and Grouper: the second crew vessel

**Status:** in the game (`VesselClassType.Wrasse = 19`, the Toybox Vessel Changer's **Wrasse and Grouper** station),
not yet played in the editor. Built on `cece/compassionate-babbage-gc13io` (2026-10-14 to 2026-10-18) from the Crew
Vessel Lab (`Docs/Studios/CrewVesselLab.html`, write-up `Docs/Studios/CREW_VESSEL_LAB.md`), signed off by Garrett on
2026-10-17, then directed by play in rounds 4 to 4f. **The record is
`Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`**; this brief maps it onto the crew-vessel contract
(`SKILL.md`, "One notion of a crew vessel") and holds nothing that file does not.

| Contract item | The Wrasse and Grouper |
|---|---|
| Subvessels | the **Grouper** (the hull: the Sparrow without its guns, missiles and buttons, two thumbs) and the **Wrasse** (the seat: a kite on the hull's x-z plane, flown with one thumb, no click) |
| Card | `SO_Class_Wrasse`: `CrewNames` Wrasse, Grouper → "Wrasse and Grouper"; `Name` Wrasse |
| Seat | `KiteNavigator` (`ICrewSeat`, `SeatName` "Wrasse"), wired to `KiteNavigatorConfig.asset` |
| What one makes for the other | the kite's loops feed the pilot: Space blasts, Mass clusters, saved Time boosts (spent on the pilot's trigger), Charge rams; its head picks hostile prisms off round the hull |
| Swap, wake, hand-over | `CrewSeat` (the arena's gesture; the hull's `AIPilot` flies it while the human navigates; `KiteNavigatorAI` flies the kite otherwise) |
| Views | `CrewPictureInPicture`; the kite camera looks straight down the hull's up axis, orthographic, `viewDepth` 400 u either side of the plane |
| The seat camera's look | `CrewViewRender`: depth from the board's plane; the corridor as a cone from the kite lens down to the board, solid at the plane, the mass above dithered to `ditherCoreAlpha` 0.35 |
| HUD | its own: `WrasseHUDController` (the cards count saved boosts, rams, the last blast's kills, and flash on a cluster) |
| Element meter, L5s | the hull's; each L5 changes a seat effect (KITE_NAVIGATOR.md, "Round 4") |
| Networking | one machine (`T-20261017-b06e`) |

Open items live in KITE_NAVIGATOR.md "Follow-ups" and `Docs/Reports/CREW_VESSELS.md` §4.
