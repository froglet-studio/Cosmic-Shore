using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The voyage slice's content (Docs/Voyages/SLICE.md §3): the HyperSea map, the crew's people, the jobs, the events
    /// that chain them, the seed news, the worlds each location is, and every line the voyage shows. One shipped asset
    /// under Resources (<see cref="Load"/>), written from <c>Docs/Voyages/voyage_slice.json</c> by
    /// <c>Tools/Build/author_voyage_assets.py</c>; the cell pool is the one hand-authored field (the generator leaves it
    /// as it finds it).
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageContent", menuName = "ScriptableObjects/Voyages/Voyage Content")]
    public class VoyageContentSO : ScriptableObject
    {
        public const string ResourcePath = "Voyages/VoyageContent";

        [Tooltip("The map, crew, jobs, events and seed news. Written by Tools/Build/author_voyage_assets.py from " +
                 "Docs/Voyages/voyage_slice.json; edit the JSON and re-run it, or edit here and port the change back.")]
        [SerializeField] VoyageContent content = new();

        [Header("Worlds")]
        [Tooltip("The worlds a location's Cell index and a lane's open-sea cells draw from (index modulo the count). " +
                 "Empty = the host cell's own worlds, the same ones the Arkway flies.")]
        [SerializeField] List<CellConfigDataSO> cellPool = new();

        [Header("Lines (tokens: {to} {from} {about} {pilot} {count} {seconds})")]
        [SerializeField] VoyageLines lines = new();

        public VoyageContent Content => content;
        public IReadOnlyList<CellConfigDataSO> CellPool => cellPool;
        public VoyageLines Lines => lines;

        static VoyageContentSO _loaded;

        /// <summary>The shipped content, or null when the asset is missing (the voyage then refuses to start, loudly).</summary>
        public static VoyageContentSO Load()
        {
            if (!_loaded) _loaded = Resources.Load<VoyageContentSO>(ResourcePath);
            return _loaded;
        }
    }

    /// <summary>Every line the voyage HUD and its stations show. An empty line shows nothing.</summary>
    [System.Serializable]
    public class VoyageLines
    {
        [Tooltip("The harbour toy's label and the build veil's.")]
        public string VoyageName = "VOYAGE";
        [Tooltip("Banner when the Ark sets sail.")]
        public string Depart = "BOUND FOR {to}";
        [Tooltip("Banner on arrival.")]
        public string Arrived = "ARRIVED AT {to}";
        [Tooltip("Banner at a vault field.")]
        public string Vaults = "VAULTS AHEAD: CRACK ONE, GRAB ITS ARTIFACT, STOW IT AT THE ARK";
        [Tooltip("Banner where a wreck lies.")]
        public string Wreck = "A WRECK: GRAB WHAT FLOATS WITH RB AND STOW IT AT THE ARK";
        [Tooltip("Banner at a core to hold.")]
        public string Hold = "HOLD {to}: STAY WITH THE ARK";
        [Tooltip("The hold's countdown.")]
        public string HoldProgress = "HOLDING {to}: {seconds}";
        [Tooltip("Banner when the job's work is done.")]
        public string Done = "JOB DONE";
        [Tooltip("Banner when a job is left undone.")]
        public string Failed = "JOB LEFT UNDONE";
        [Tooltip("Banner when something is stowed aboard.")]
        public string Stowed = "STOWED ABOARD";
        [Tooltip("Banner on docking at home.")]
        public string Extracted = "DOCKED AT {to}: {count} FOUND ARTIFACTS ARE YOURS";
        [Tooltip("Banner when the Ark falls.")]
        public string ArkLost = "THE ARK IS LOST";
        [Tooltip("The Voyage window's card when no seated pilot has work here.")]
        public string NoWork = "No pilot has work here. Seat other pilots, or sail on.";
        [Tooltip("A friend's banner while the host's board decides the next job.")]
        public string Following = "SAILING WITH YOUR HOST: THEY PICK THE NEXT JOB";
        [Tooltip("Banner after a job's work is done or left undone: where the next voyage is chosen.")]
        public string ChooseNext = "LEAVE FREESTYLE TO CHOOSE YOUR NEXT VOYAGE";

        [Header("The stops the depth pass added (Docs/Voyages/DEPTH.md section 4, section 10)")]
        [Tooltip("Banner at a lair: its creatures wear the other colour.")]
        public string Lair = "A LAIR: THE PREDATORS HERE WEAR ANOTHER COLOUR";
        [Tooltip("Banner at the thieves' hoard.")]
        public string Hoard = "THE THIEVES' HOARD: CRACK A SHELL, GRAB WHAT THEY STOLE, STOW IT";
        [Tooltip("Banner at a nursery.")]
        public string Nursery = "A NURSERY: THE HERDS FEED HERE";
        [Tooltip("Banner at a derelict.")]
        public string Derelict = "A DERELICT: STAND BY IT WITH THE ARK";
        [Tooltip("Banner at the pens.")]
        public string Pen = "THE PENS: RAZE THE MASS THAT HOLDS THE HERDS";
        [Tooltip("Banner at a shrine.")]
        public string Shrine = "A SHRINE: KEEP THE ARK AT THE BEACON";
        [Tooltip("Banner when the Kaiju lairs at the stop.")]
        public string KaijuHere = "THE LEVIATHAN LAIRS HERE";
        [Tooltip("Banner when the stop's world starts under the other colour.")]
        public string HostileWater = "THIS WATER IS HELD AGAINST YOU";
        [Tooltip("Banner when a rival crew stands at the stop: {rival} its name.")]
        public string RivalHere = "{rival} ARE HERE";
        [Tooltip("Banner when a racing rival opens a shell first: {rival}.")]
        public string RivalOpened = "{rival} OPENED A SHELL BEFORE YOU";
        [Tooltip("Banner when arriving put the place on the chart: {to}.")]
        public string Charted = "{to} IS ON YOUR CHART";
        [Tooltip("Banner when a relic comes aboard: {relic} its name.")]
        public string Relic = "{relic} IS ABOARD";
        [Tooltip("Banner when news aboard reached the port: {to}.")]
        public string Delivered = "NEWS DELIVERED AT {to}";
        [Tooltip("Banner on a convoy's arrival rule: {percent} the hull floor.")]
        public string Convoy = "KEEP THE HULL ABOVE {percent} PERCENT";
        [Tooltip("Progress lines per verb: {done} so far, {goal} the goal.")]
        public string ProgressStow = "STOWED {done} OF {goal}";
        public string ProgressHold = "HOLDING: {done} OF {goal} SECONDS";
        public string ProgressGather = "GATHERED {done} OF {goal} CRYSTALS";
        public string ProgressCull = "FELLED {done} OF {goal}";
        public string ProgressLay = "LAID {done} OF {goal} PRISMS";
        public string ProgressRaze = "RAZED {done} OF {goal}";
        public string ProgressObserve = "OBSERVED {done} OF {goal} SECONDS";
        public string ProgressMeet = "THEY ARE HERE: THE WARRANT IS SERVED";
        [Tooltip("A voyage card's eyebrow in the Voyage window: the course, and the pilot who brought the job.")]
        public string CardRoute = "{from} TO {to} - {pilot}";
        [Tooltip("The way home's card eyebrow in the Voyage window.")]
        public string CardHomeRoute = "{from} TO {to}";
        [Tooltip("The Voyage window's card for the job under way.")]
        public string CardUnderway = "UNDER WAY TO {to}";
        [Tooltip("A voyage card's course line on the galaxy map: {ly} light years sailed (and light takes as many years: " +
                 "{years}), {minutes} at a minute a cell, {sea} the thickest water crossed, {intensity} the course's I1-I4.")]
        public string CardCourse = "{ly} LY THROUGH {sea} - LIGHT TAKES {years} YEARS";
        [Tooltip("The sea's names, thinnest to thickest, for {sea}.")]
        public string SeaSpray = "SPRAY";
        public string SeaFroth = "FROTH";
        public string SeaWater = "WATER";
        public string SeaThick = "THICK WATER";
        public string SeaReef = "REEF";
        [Tooltip("A voyage card's chip that opens it to its brief, and closes it again.")]
        public string CardOpen = "Open";
        public string CardClose = "Close";
        [Tooltip("An open voyage card's brief. Tokens: {summary} the job, {to} and {place} the destination and its line, " +
                 "{squad} the seats, {role} the squad role it suits, {standing} how far the pilot's faction trusts the crew, " +
                 "{intensity} the course's I1-I4, {reward} the crystals it pays, {warnings} the world's warnings (CardWarnings), " +
                 "{pilot} and {story} who brought it. <br> breaks a line.")]
        [TextArea] public string CardDetails =
            "{summary}<br><br>{to}: {place}<br><br>SQUAD  {squad}<br>INTENSITY  I{intensity}<br>PAYS  {reward} CRYSTALS<br><br>{pilot}: {story}";
        [Tooltip("The way home's brief (the same tokens; it pays nothing itself).")]
        [TextArea] public string CardHomeDetails = "{summary}<br><br>SQUAD  {squad}<br>INTENSITY  I{intensity}";
        [Tooltip("The warnings block a brief's {warnings} becomes when a job has warnings (empty otherwise): {warnings} the lines joined.")]
        public string CardWarnings = "<br>{warnings}";
        [Tooltip("What stands between two warning lines.")]
        public string CardWarningSeparator = "<br>";
        [Tooltip("One seat of the squad in a brief's {squad}: {vessel} its hull, {pilot} its pilot.")]
        public string CardSquadSeat = "{vessel} ({pilot})";
        [Tooltip("What stands between two seats in {squad}.")]
        public string CardSquadSeparator = ", ";
        [Tooltip("Under the galaxy map when the player taps a place no job sails to: {name} the place, {blurb} its line.")]
        public string MapPlace = "{name} - {blurb}";
        [Tooltip("The caption on the map's edge view (the disk seen edge-on at true scale): {place} the place it is " +
                 "centred on, {height} that place's height (one of the three lines below).")]
        public string EdgeCaption = "EDGE ON, TRUE SCALE - DISK 1,000 LY - {place} {height}";
        [Tooltip("{height} above, below and on the midplane; {ly} the light years.")]
        public string EdgeAbove = "{ly} LY ABOVE THE MIDPLANE";
        public string EdgeBelow = "{ly} LY BELOW THE MIDPLANE";
        public string EdgeMidplane = "ON THE MIDPLANE";

        [Header("Galaxy map deep zoom (Docs/Voyages/VOYAGE_BOARD.md §4b)")]
        [Tooltip("The galaxy map's zoom ladder, widest to narrowest: each tier's name (a press dives to it) and its line in the readout.")]
        public string MapTierGalaxy = "GALAXY";
        public string MapTierArm = "ARM";
        public string MapTierSector = "SECTOR";
        public string MapTierRegion = "REGION";
        public string MapTierNeighbourhood = "NEIGHBOURHOOD";
        public string MapTierCell = "CELL";
        public string MapTierSystem = "SYSTEM";
        public string MapTierGalaxyWhat = "arms, bulge, the halo's globular clusters";
        public string MapTierArmWhat = "giant nurseries, the brightest supergiants";
        public string MapTierSectorWhat = "star clusters and the brightest stars";
        public string MapTierRegionWhat = "clusters, clouds and the stars you see by eye";
        public string MapTierNeighbourhoodWhat = "a slab of nearby stars around one height";
        public string MapTierCellWhat = "one 20 ly cell: membrane, cytoplasm, nucleus, and its stars";
        public string MapTierSystemWhat = "single star systems a few ly apart";
        [Tooltip("The ladder's dives: a press flies from wherever the map is down to Sol's star system, or Tumult's.")]
        public string MapDiveSol = "SOL";
        public string MapDiveTumult = "TUMULT";
        [Tooltip("The map's readout: {tier} and {what} the tier on show, {width} the view's width (MapLy), {slab} MapReadoutSlab from Neighbourhood down, else nothing.")]
        public string MapReadout = "{tier}: {what} - {width} across{slab}";
        [Tooltip("{thick} how far the slice reaches above and below (MapLy), {height} where it is centred (MapAbove, MapBelow or MapMidplane).")]
        public string MapReadoutSlab = " - a slice {thick} either side of {height}";
        [Tooltip("A distance on the map: {ly} the light years.")]
        public string MapLy = "{ly} ly";
        [Tooltip("A height on the map; {ly} is MapLy.")]
        public string MapAbove = "{ly} above the midplane";
        public string MapBelow = "{ly} below the midplane";
        public string MapMidplane = "on the midplane";
        [Tooltip("The card a tapped star, cluster or landmark opens on the map: {kind} what it is, {name}, {body} its line, {source} where it comes from, {where} its height and distances (MapWhere). <br> breaks a line.")]
        [TextArea] public string MapInspect = "<size=80%>{kind}</size><br><b>{name}</b><br>{body} <size=80%>[{source}]</size><br><size=80%>{where}</size>";
        [Tooltip("{height} MapAbove/MapBelow/MapMidplane, {sol} {tumult} {here} distances (MapLy), {time} MapMinutes or MapHours.")]
        public string MapWhere = "{height} - {sol} from Sol - {tumult} from Tumult - {here} from your squad, {time} by Ark in a straight line";
        public string MapMinutes = "{n} min";
        public string MapHours = "{n} h";
        [Tooltip("{kind} for a catalogue star, a model star, one of Tumult's named stars, and the two kinds of cluster (a landmark shows its own kind).")]
        public string MapKindStar = "Star";
        public string MapKindModelStar = "Star, from the galaxy model";
        public string MapKindTumultStar = "Star, as Tumult's crews chart it";
        public string MapKindGlobular = "Globular cluster";
        public string MapKindOpen = "Open cluster";
        [Tooltip("{name} for a catalogue star with no name, and a model star.")]
        public string MapUnnamedStar = "Unnamed star";
        public string MapUnchartedStar = "Uncharted star";
        [Tooltip("A star's {body}: {type} below, {light} MapLight, {mag} its absolute magnitude, {also} MapStarAlso when it has a designation besides its name.")]
        public string MapStarBody = "A {type}, {light} the Sun's light (absolute magnitude {mag}).{also}";
        public string MapStarAlso = " Also {desig}.";
        [Tooltip("How bright, against the Sun: {x} the multiple.")]
        public string MapLight = "{x}x";
        [Tooltip("A star's {type}, by its colour (O/B, B, A, F, G, K, M), and for the brightest red stars.")]
        public string MapTypeOB = "blue-white giant";
        public string MapTypeB = "blue-white star";
        public string MapTypeA = "white star";
        public string MapTypeF = "yellow-white star";
        public string MapTypeG = "yellow star like the Sun";
        public string MapTypeK = "orange star";
        public string MapTypeM = "red star";
        public string MapTypeRedSupergiant = "red supergiant";
        public string MapTypeGiant = "giant";
        [Tooltip("A globular cluster's {body}; {light} MapLight.")]
        public string MapGlobularBody = "A ball of hundreds of thousands of old stars orbiting through the halo, about {light} the Sun's light.";
        [Tooltip("An open cluster's {body}: {members} its stars, {age} MapOpenAge or nothing; MapOpenBodyYoung when its members are not counted.")]
        public string MapOpenBody = "{members} member stars{age}.";
        public string MapOpenBodyYoung = "A young star cluster{age}.";
        [Tooltip("{age} MapAgeBillion or MapAgeMillion; {n} the number.")]
        public string MapOpenAge = ", about {age} years old";
        public string MapAgeBillion = "{n} billion";
        public string MapAgeMillion = "{n} million";
        [Tooltip("{source} on the card: a catalogue's credit ({source}), the model, Tumult's names, a real or a proposed landmark.")]
        public string MapSourceCatalog = "S - {source}";
        public string MapSourceModel = "M - drawn from the galaxy model, not a catalog star";
        public string MapSourceTumult = "A - invented name; the star is drawn from the model";
        public string MapSourceReal = "S - real";
        public string MapSourceProposed = "A - proposed";

        [Header("Galaxy map: the cells")]
        [Tooltip("A cell's name on the map once it is big enough to name: {sea} its sea (SeaSpray...), {q} {r} its coordinates.")]
        public string MapCellLabel = "{sea} {q}, {r}";
        [Tooltip("The {kind} on a cell's card.")]
        public string MapKindCell = "Cell";
        [Tooltip("A cell's {name} on its card: {q} {r} its coordinates.")]
        public string MapCellName = "Cell {q}, {r}";
        [Tooltip("A cell's {body}: {sea} its sea, {density} its perceived density, {danger} 0-3, {berth} MapCellBerth or nothing.")]
        public string MapCellBody = "{sea}, {density} g/cc. Danger {danger} of 3. 20 ly across: an Ark crosses it in a minute.{berth}";
        [Tooltip("{berth} on a cell's card when a place is within its berth (3 cells): {place} its name.")]
        public string MapCellBerth = " Inside the berth of {place}.";
        [Tooltip("{source} on a cell's card.")]
        public string MapSourceCell = "M - the sea model at the cell's centre";

        [Header("Galaxy map: the cells in 3D (Docs/Voyages/VOYAGE_BOARD.md 4c)")]
        [Tooltip("The nucleus's tag inside the cell at the view's centre (once a cell is 200 px wide).")]
        public string MapCellNucleus = "Nucleus";
        [Tooltip("The nucleus's tag in the central cell, whose nucleus is Sgr A*.")]
        public string MapCellNucleusHeart = "Nucleus - Sgr A*, the Heart";
        [Tooltip("Under the nucleus's tag when the slice cuts the nucleus.")]
        public string MapCellNucleusBody = "6.5 ly across - omni regrows here";
        [Tooltip("Under the nucleus's tag when the slice passes under it: {ly} how far (MapLy).")]
        public string MapCellNucleusAbove = "{ly} above this slice - 6.5 ly across";
        [Tooltip("Under the nucleus's tag when the slice passes over it: {ly} how far (MapLy).")]
        public string MapCellNucleusBelow = "{ly} below this slice - 6.5 ly across";
        [Tooltip("The cytoplasm's tag.")]
        public string MapCellCytoplasm = "Cytoplasm";
        [Tooltip("Under the cytoplasm's tag: {sea} the cell's sea, {density} its perceived density.")]
        public string MapCellCytoplasmBody = "{sea}, {density} g/cc - flora and fauna live here";
        [Tooltip("The membrane's tag.")]
        public string MapCellMembrane = "Membrane";
        [Tooltip("The membrane's tag in the central cell.")]
        public string MapCellMembraneHeart = "Membrane - the circumnuclear disk";
        [Tooltip("Under the membrane's tag.")]
        public string MapCellMembraneBody = "14 faces - 20 ly across the square ones";
        [Tooltip("The edge view's caption once the map shows cells: {height} the slice's height (MapAbove, MapBelow or MapMidplane).")]
        public string EdgeCaptionCells = "THE CELLS CUT UPRIGHT ALONG THE MAP'S BOTTOM EDGE - DRAG THE GREEN BAR OR SCROLL HERE TO SLICE THE MAP AT ANOTHER HEIGHT - SLICED {height}";
        [Tooltip("The label on the slice bar's handle in the edge view.")]
        public string EdgeSlice = "SLICE";

        // The open voyage card (Rev 49 C, Docs/MENU_REDESIGN_B.md section 119): three levels, the voyage, its Ark crew, its story.
        [Tooltip("The open card's way back to the voyage cards (B).")]
        public string CardBack = "VOYAGES";
        [Tooltip("The open card's place among the voyages: {n} this one, {count} all of them.")]
        public string CardPager = "VOYAGE {n} OF {count}";
        [Tooltip("The open card's step to the previous voyage (D-pad left).")]
        public string CardPrevious = "<";
        [Tooltip("The open card's step to the next voyage (D-pad right).")]
        public string CardNext = ">";
        [Tooltip("The open card's first level, the voyage.")]
        public string CardLevelVoyage = "1 VOYAGE";
        [Tooltip("The open card's second level, the squad's twelve settings.")]
        public string CardLevelCrew = "2 ARK CREW";
        [Tooltip("The open card's third level, the story.")]
        public string CardLevelStory = "3 STORY";
        [Tooltip("The chip on the open card's course: {ly} light years.")]
        public string CardCourseChip = "YOUR COURSE - {ly} LY";
        [Tooltip("Who brought the voyage: {pilot} the crew member's name.")]
        public string CardFoundBy = "FOUND BY {pilot}";
        [Tooltip("Over the open card's title: {n} its number, {place} where it goes.")]
        public string CardKicker = "VOYAGE {n} - {place}";
        [Tooltip("Under the open card's course number.")]
        public string CardCourseLabel = "COURSE";
        [Tooltip("The open card's course number: {ly} light years.")]
        public string CardCourseValue = "{ly} ly";
        [Tooltip("Under the open card's sailing time.")]
        public string CardSailLabel = "SAIL";
        [Tooltip("The open card's sailing time: {minutes} minutes.")]
        public string CardSailValue = "{minutes} min";
        [Tooltip("Under the open card's intensity.")]
        public string CardIntensityLabel = "INTENSITY";
        [Tooltip("The open card's intensity: {intensity} 1-4.")]
        public string CardIntensityValue = "I{intensity}";
        [Tooltip("Between the course's two ends on the open card: {cells} 20 ly cells, {minutes} minutes.")]
        public string CardCells = "{cells} CELLS - {minutes} MIN";
        [Tooltip("Under the course's first end: where the Ark is.")]
        public string CardYourArk = "YOUR ARK";
        [Tooltip("Under the course's last end.")]
        public string CardDestination = "DESTINATION";
        [Tooltip("Over the squad's name on the open card.")]
        public string CardSquadLabel = "YOUR SQUAD";
        [Tooltip("Over the squads to pick from (the Ark's squads).")]
        public string CardPresetsLabel = "SQUAD PRESETS";
        [Tooltip("Where the squad presets stand when the player has made none.")]
        public string CardNoSquad = "Make a squad in the Ark to sail with it.";
        [Tooltip("Opens the open card's second level, the squad's settings.")]
        public string CardConfigure = "CONFIGURE THE SQUAD";
        [Tooltip("Sails the open voyage.")]
        public string CardSetSail = "SET SAIL";
        [Tooltip("Opens the open card's second level.")]
        public string CardCrew = "ARK CREW";
        [Tooltip("Opens the open card's third level.")]
        public string CardStory = "STORY";
        [Tooltip("Back to the open card's first level.")]
        public string CardVoyage = "VOYAGE";
        [Tooltip("Over the squad's name on the second level.")]
        public string CardCrewHead = "ARK CREW - THREE SEATS, FOUR SETTINGS EACH";
        [Tooltip("Under the squad's name on the second level.")]
        public string CardCrewNote = "Press a setting to change it. Save squad keeps the change.";
        [Tooltip("Under a joined squad's name on the second level: {owner} who made it.")]
        public string CardCrewJoined = "{owner}'s squad - only your own seat changes here.";
        [Tooltip("Saves the squad's settings.")]
        public string CardSaveSquad = "SAVE SQUAD";
        [Tooltip("Save squad's label once the squad is saved.")]
        public string CardSaved = "SAVED";
        [Tooltip("The first seat's name.")]
        public string CardSeatLeader = "LEADER";
        [Tooltip("The second seat's name.")]
        public string CardSeatRogueOne = "ROGUE ONE";
        [Tooltip("The third seat's name.")]
        public string CardSeatRogueTwo = "ROGUE TWO";
        [Tooltip("Who flies the player's own seat.")]
        public string CardSeatYou = "You";
        [Tooltip("Who flies an AI seat.")]
        public string CardSeatAi = "AI wingmate";
        [Tooltip("Who flies an open seat (a friend takes it).")]
        public string CardSeatOpen = "Open seat";
        [Tooltip("A seat's vessel setting.")]
        public string CardSettingVessel = "VESSEL";
        [Tooltip("A seat's pilot setting.")]
        public string CardSettingPilot = "PILOT";
        [Tooltip("A seat's element setting.")]
        public string CardSettingElement = "ELEMENT";
        [Tooltip("A seat's artifact setting.")]
        public string CardSettingArtifact = "ARTIFACT";
        [Tooltip("A seat that carries no artifact.")]
        public string CardNoArtifact = "No artifact";
        [Tooltip("The Charge element's name on a seat.")]
        public string ElementCharge = "Charge";
        [Tooltip("The Mass element's name on a seat.")]
        public string ElementMass = "Mass";
        [Tooltip("The Space element's name on a seat.")]
        public string ElementSpace = "Space";
        [Tooltip("The Time element's name on a seat.")]
        public string ElementTime = "Time";
        [Tooltip("Over the story's beats on the third level.")]
        public string CardStoryHead = "STORY";
        [Tooltip("Beside the story's head: the story can be skipped.")]
        public string CardStoryOptional = "OPTIONAL";
        [Tooltip("The story's beat about the news that brought the voyage.")]
        public string StoryNews = "THE NEWS";
        [Tooltip("The story's beat about where the voyage goes.")]
        public string StoryPlace = "THE PLACE";
        [Tooltip("The story's beat about the crew member who found it.")]
        public string StoryCrew = "WHO BROUGHT IT";
        [Tooltip("Under the crew beat: {pilot} the crew member's name.")]
        public string StoryCrewBy = "{pilot}";
        [Tooltip("The story's beat about the course.")]
        public string StoryWater = "THE WATER";
        [Tooltip("The water beat: {years} light's years, {to} the place, {minutes} the Ark's minutes.")]
        public string StoryWaterLine = "Light takes {years} years to reach {to}. You take {minutes} minutes.";
        [Tooltip("Under the water beat: {sea} the thickest water on the course.")]
        public string StoryWaterBy = "{sea} on the way";
        [Tooltip("The story's beat about the sea today: the job's warnings.")]
        public string StoryWorld = "THE SEA TODAY";
        [Tooltip("Under the sea beat: {warnings} the warnings, one line each.")]
        public string StoryWorldBy = "{warnings}";
        [Tooltip("The story's beat about the crew's own record.")]
        public string StoryLog = "YOUR LOG";
        [Tooltip("The log beat: {voyages} sailed, {extracted} finds brought home, {relics}, {charted} places charted, {arks} Arks lost, {kaiju} felled.")]
        public string StoryLogLine = "{voyages} voyages sailed, {extracted} finds brought home, {relics} relics, {charted} places charted, {arks} Arks lost.";
        [Tooltip("A voyage's row on the Voyages list (Rev 81 B5): its number and where it goes.")]
        public string BoardRowTitle = "{n}  {place}";
        [Tooltip("Under a row's place: the pilot who brought the voyage.")]
        public string BoardRowBy = "Brought by {pilot}";
        [Tooltip("Under a row's place: the voyage's three numbers.")]
        public string BoardRowMeta = "{ly} ly - {minutes} min - Intensity {intensity}";
        [Tooltip("Over the open voyage's distance, in the right panel.")]
        public string BoardDistanceLabel = "DISTANCE";
        [Tooltip("Over the open voyage's sailing time.")]
        public string BoardSailLabel = "SAIL";
        [Tooltip("Over the open voyage's intensity.")]
        public string BoardIntensityLabel = "INTENSITY";
        [Tooltip("The open voyage's intensity.")]
        public string BoardIntensityValue = "{intensity}";
        [Tooltip("Beside the squad chips over the seat cards.")]
        public string BoardSquadLabel = "SQUAD";
        [Tooltip("A squad chip for a squad the player joined.")]
        public string BoardSquadJoined = "{name} - {owner}'s";
        [Tooltip("A seat card's first line: who flies it and the pilot.")]
        public string BoardSeatWho = "{who} - {pilot}";
        [Tooltip("A seat card's second line on a seat the player may change.")]
        public string BoardSeatKit = "{element} - {artifact}";
        [Tooltip("A seat card's second line on a seat the player may not change.")]
        public string BoardSeatNotYours = "{owner}'s seat";
        [Tooltip("Over the seat editor (Rev 83 S2): the seat being changed.")]
        public string BoardEditorTitle = "{role} - {who} - {squad}";
        [Tooltip("Under the seat editor's pilot.")]
        public string BoardPilotLine = "Flies the {vessel} for {squad}";
        [Tooltip("Under a pilot in the editor's list who flies another seat now.")]
        public string BoardPilotSwaps = "Flies the {vessel} - swaps";
        [Tooltip("Under the pilot in the editor's list who flies this seat.")]
        public string BoardPilotFlying = "Flying";
        [Tooltip("Under the seat editor's element.")]
        public string BoardElementLine = "The element this seat flies with";
        [Tooltip("Under the seat editor's artifact when the seat carries none.")]
        public string BoardNoArtifactLine = "This seat carries no artifact";
        [Tooltip("The seat editor's close button.")]
        public string BoardDone = "DONE";
        [Tooltip("The Set sail button while the voyage is out.")]
        public string BoardUnderway = "UNDER WAY";
        [Tooltip("The keycap before Set sail (empty: none).")]
        public string BoardSailKey = "A";
        [Tooltip("The keycap before the seat editor's Done (empty: none).")]
        public string BoardDoneKey = "B";
        [Tooltip("Over the open voyage's name on the Voyages tab: which voyage it is and who brought it.")]
        public string BoardKicker = "Voyage {n} of {count} - brought by {pilot}";
        [Tooltip("Over the open voyage's name when nobody brought it.")]
        public string BoardKickerAlone = "Voyage {n} of {count}";
        [Tooltip("Under the open voyage's name: its three numbers on one line.")]
        public string BoardStats = "{distance}   -   {sail}   -   Intensity {intensity}";
        [Tooltip("A stepper's step back (a seat's vessel, pilot, element or artifact).")]
        public string BoardStepBack = "<";
        [Tooltip("A stepper's step on.")]
        public string BoardStepOn = ">";
        [Tooltip("The voyage screen's zoom-in step over the map.")]
        public string BoardZoomIn = "+";
        [Tooltip("The voyage screen's zoom-out step over the map.")]
        public string BoardZoomOut = "-";
        [Tooltip("The voyage screen's button that opens the squad screen.")]
        public string BoardConfigure = "CONFIGURE SQUAD";
        [Tooltip("The pad key drawn before Configure squad.")]
        public string BoardConfigureKey = "X";
        [Tooltip("The squad screen's way back to the voyage screen.")]
        public string BoardBack = "VOYAGES";
        [Tooltip("The pad key drawn before the squad screen's way back.")]
        public string BoardBackKey = "B";
        [Tooltip("The squad screen's button that keeps the squad as a new squad.")]
        public string BoardSavePreset = "SAVE AS PRESET";
        [Tooltip("The pad key drawn before Save as preset.")]
        public string BoardSavePresetKey = "Y";
        [Tooltip("The squad screen's note at its foot: what changing your own vessel also changes.")]
        public string BoardHangarNote = "A vessel brings the pilot you keep for it in the Hangar.";
        [Tooltip("Who flies a squad, under its name on the voyage screen: {leader}, {one} and {two} are the three seats.")]
        public string BoardPresetWho = "{leader}, {one}, {two}";
        [Tooltip("Under a pilot who flies another seat of the squad: taking them swaps the two; {vessel} is the vessel they leave.")]
        public string BoardSwapsWith = "Swaps with {vessel}";
        [Tooltip("A chip on a table's left (a saga part, a quest): its number and name.")]
        public string SlabChip = "{n}  {name}";
        [Tooltip("Under a saga part's chip.")]
        public string SlabSagaPart = "Part {n} of {count}";
        [Tooltip("Over the picked saga part's name.")]
        public string SlabSagaKicker = "Saga - part {n} of {count}";
        [Tooltip("Over a form's station on the Saga table's front face.")]
        public string SlabSagaForm = "Form {n}";
        [Tooltip("Over the episode's name on the Quests table.")]
        public string SlabQuestsKicker = "Episode 1";
        [Tooltip("The episode's name over the Quests table.")]
        public string SlabQuestsName = "The Hypersea";
        [Tooltip("Under the episode's name: how many quests are claimed.")]
        public string SlabQuestsProgress = "{claimed} of {total} claimed";
        [Tooltip("Over the picked quest in the Quests table's right column.")]
        public string SlabQuestKicker = "Quest {n} of {count}";
        [Tooltip("A quest that is claimed.")]
        public string SlabQuestClaimed = "Claimed";
        [Tooltip("A quest that is done and waits to be claimed.")]
        public string SlabQuestReady = "Done - claim it";
        [Tooltip("The quest to play next.")]
        public string SlabQuestNext = "Next to play";
        [Tooltip("A locked quest: the quest it opens after.")]
        public string SlabQuestLocked = "After {name}";
        [Tooltip("The Quests table's button on a quest that is done.")]
        public string SlabQuestClaim = "CLAIM";
        [Tooltip("The Quests table's button on the quest to play next.")]
        public string SlabQuestPlay = "PLAY QUEST";
        [Tooltip("Over the next episode's name on the Episodes table.")]
        public string SlabEpisodesKicker = "Episodes";
        [Tooltip("The next episode's name.")]
        public string SlabEpisodesName = "Episode 2";
        [Tooltip("Under the next episode's name.")]
        public string SlabEpisodesSoon = "Coming soon";
        [Tooltip("The Episodes table's one line.")]
        public string SlabEpisodesLine = "Voyages and the Saga are open now.";
        [Tooltip("On the Episodes table's front face.")]
        public string SlabEpisodesFace = "New episodes arrive here";
        [Tooltip("The Episodes table's button back to the Voyages tab.")]
        public string SlabEpisodesBack = "BACK TO VOYAGES";
        [Tooltip("The keycap before the Episodes table's button (empty: none).")]
        public string SlabEpisodesBackKey = "LB";
        [Tooltip("Over the saga's name on the Saga table while the saga is locked.")]
        public string SlabSagaLockedKicker = "Saga";
        [Tooltip("The locked Saga table's one line.")]
        public string SlabSagaLockedLine = "Voyages are open now.";
        [Tooltip("On the locked Saga table's front face.")]
        public string SlabSagaLockedFace = "The saga opens here";

        // The Quests window (Docs/MENU_REDESIGN_B.md sections 188 and 200, Rev 107 D).
        [Tooltip("The Quests window's title, top left.")]
        public string QuestsTitle = "Quests";
        [Tooltip("The Quests window's way back to Home.")]
        public string QuestsBack = "BACK";
        [Tooltip("The keycap before the way back (empty: none).")]
        public string QuestsBackKey = "B";
        [Tooltip("A quest whose goals are done and whose reward waits.")]
        public string QuestsReady = "Ready to claim";
        [Tooltip("A quest being played: {done} goals of {total}.")]
        public string QuestsOpen = "{done} of {total} goals";
        [Tooltip("Under a quest's first goal: what reaching it opens.")]
        public string QuestsTier3 = "Opens Intensity 3";
        [Tooltip("Under a quest's second goal: what reaching it opens.")]
        public string QuestsTier4 = "Opens Intensity 4";
        [Tooltip("Under the goal of a quest that opens a feature, not a game ({name} is the quest's).")]
        public string QuestsTierFeature = "Opens the {name}";
        [Tooltip("Beside a goal that is done.")]
        public string QuestsGoalDone = "Done";
        [Tooltip("Beside the goal being played for.")]
        public string QuestsGoalNext = "Next";
        [Tooltip("The last step of every quest.")]
        public string QuestsClaimStep = "Claim";
        [Tooltip("Under the claim step: the colour set it gives.")]
        public string QuestsClaimUnlocks = "Unlocks {reward}";
        [Tooltip("Beside the claim step when it can be pressed.")]
        public string QuestsClaimReady = "Ready";
        [Tooltip("Over the reward while it is not the player's.")]
        public string QuestsRewardKicker = "Reward";
        [Tooltip("Over the reward once the quest is claimed.")]
        public string QuestsRewardKickerYours = "Your colour set";
        [Tooltip("Under the reward's name.")]
        public string QuestsRewardKind = "Colour set";
        [Tooltip("Under the reward once the quest is claimed.")]
        public string QuestsRewardYours = "Yours. Wear it from the Profile.";
        [Tooltip("Under the reward when the quest is ready to claim.")]
        public string QuestsRewardReady = "Claim to unlock it";
        [Tooltip("Under the reward while the quest is still being played or locked.")]
        public string QuestsRewardLater = "Unlocked by this quest";
        [Tooltip("Under the reward's Jade prism.")]
        public string QuestsDomainJade = "Jade";
        [Tooltip("Under the reward's Ruby prism.")]
        public string QuestsDomainRuby = "Ruby";
        [Tooltip("Under the reward's Gold prism.")]
        public string QuestsDomainGold = "Gold";
        [Tooltip("Under the reward's Blue prism.")]
        public string QuestsDomainBlue = "Blue";
        [Tooltip("Over what else claiming the quest opens.")]
        public string QuestsAlsoOpens = "Claim also opens";
        [Tooltip("Over what claiming the quest opened, once claimed.")]
        public string QuestsOpened = "Opened";
        [Tooltip("Over Play quest: where the button takes the player.")]
        public string QuestsPlayNote = "Play {name} in the Arcade";
        [Tooltip("Over the saga's name on its locked card at the foot of the Voyages page (Frogma Rev 103 A).")]
        public string StripSagaKicker = "Saga";
        [Tooltip("The saga's name on its locked card at the foot of the Voyages page.")]
        public string StripSagaName = "Tandava and The Severing";
        [Tooltip("Over the episode's name on its locked card at the foot of the Voyages page.")]
        public string StripEpisodesKicker = "Episodes";
        [Tooltip("The next episode's name on its locked card at the foot of the Voyages page.")]
        public string StripEpisodesName = "Episode 2";
        [Tooltip("Under a locked card's name at the foot of the Voyages page.")]
        public string StripSoon = "Coming soon";
        [Tooltip("Under a locked card's name once it is pressed: it opens nothing.")]
        public string StripNotOpen = "Not open yet. Voyages are open now.";
        [Tooltip("The Voyages page's first step in the header: the HyperSea table and your crew's voyages.")]
        public string StepSea = "01  THE HYPERSEA";
        [Tooltip("The second step in the header: the voyage opened.")]
        public string StepVoyage = "02  THE VOYAGE";
        [Tooltip("The third step in the header: your lobby or a squad, then Set sail.")]
        public string StepArk = "03  YOUR ARK";
        [Tooltip("The header's way back a step (the voyage and Ark pages).")]
        public string PageBack = "BACK";
        [Tooltip("The pad key drawn before Back.")]
        public string PageBackKey = "B";
        [Tooltip("Opens the HyperSea table as a full-screen map.")]
        public string SeaFullScreen = "FULL SCREEN";
        [Tooltip("Takes the full-screen map back to the table.")]
        public string SeaFullScreenBack = "BACK FROM FULL SCREEN";
        [Tooltip("Slides your crew's voyages out beside their portraits.")]
        public string SeaCrew = "CREW";
        [Tooltip("Slides your crew's voyages away again.")]
        public string SeaCrewHide = "HIDE";
        [Tooltip("Beside a zoom tier: how wide the view is there; {ly} is the width.")]
        public string SeaTierWidth = "{ly} ly";
        [Tooltip("The picked voyage's call to action: opens the voyage page.")]
        public string SeaOpenVoyage = "OPEN VOYAGE";
        [Tooltip("Over a voyage's name on the HyperSea page: {kind} is the job, {pilot} who recommends it.")]
        public string SeaOfferKicker = "{kind} - {pilot}";
        [Tooltip("Beside a voyage's difficulty: the crystals it pays; {pay} and {element}.")]
        public string SeaOfferPay = "{pay} {element}";
        [Tooltip("Over a voyage on the crew's sliding cards: who recommends it.")]
        public string SeaCrewRecommends = "{pilot} recommends";
        [Tooltip("Over the voyage's name on the voyage page.")]
        public string BriefKicker = "{kind} - offered by {pilot}";
        [Tooltip("Under the voyage's name: how far, how long, how many stops.")]
        public string BriefStats = "{ly} ly - {minutes} min - {stops} stops";
        [Tooltip("The difficulty picker's label (1 to 9).")]
        public string BriefDifficulty = "DIFFICULTY";
        [Tooltip("Beside the difficulty label: the pilot's own pick.")]
        public string BriefRecommended = "Recommended by {pilot}: {difficulty}";
        [Tooltip("The rewards' label.")]
        public string BriefRewards = "REWARDS";
        [Tooltip("The crystals the voyage pays at the picked difficulty.")]
        public string BriefCrystals = "{pay} {element} crystals";
        [Tooltip("Under the crystals: the difficulty they are paid for.")]
        public string BriefCrystalsBy = "At difficulty {difficulty}";
        [Tooltip("A reward: standing with the house of the pilot who offers it.")]
        public string BriefStanding = "Standing with {faction}";
        [Tooltip("Under the standing reward.")]
        public string BriefStandingBy = "They trust you more";
        [Tooltip("Under the rewards: the squad's role on this voyage.")]
        public string BriefRole = "Your squad sails as {role}";
        [Tooltip("Over the stop the drawing route has reached.")]
        public string BriefStop = "STOP {n} OF {count}";
        [Tooltip("The voyage page's call to action: opens your Ark.")]
        public string BriefReady = "READY YOUR ARK";
        [Tooltip("The Ark page's label when you have a lobby.")]
        public string ArkLobby = "YOUR LOBBY";
        [Tooltip("Under Your lobby: who is in it; {names} are their names.")]
        public string ArkLobbyWith = "{names} sail with you";
        [Tooltip("The Ark page's label when a squad sails.")]
        public string ArkSquadHead = "FLY A SQUAD";
        [Tooltip("Under Fly a squad.")]
        public string ArkSquadLine = "AI wingmates fly the other seats";
        [Tooltip("Under Fly a squad, when you have a lobby.")]
        public string ArkSquadLineLobby = "Your lobby waits; AI wingmates fly the other seats";
        [Tooltip("Your own seat card's name.")]
        public string ArkYou = "You";
        [Tooltip("An AI seat card's name.")]
        public string ArkWingmate = "AI wingmate";
        [Tooltip("A friend's seat card: they pick their own seat on their own screen.")]
        public string ArkInLobby = "IN YOUR LOBBY";
        [Tooltip("Under a friend's seat card.")]
        public string ArkFriendLine = "Picks their own vessel";
        [Tooltip("A seat card's artifact line.")]
        public string ArkArtifact = "Artifact - {artifact}";
        [Tooltip("Leaves your lobby's seats for a squad of your own.")]
        public string ArkFlySquad = "FLY A SQUAD INSTEAD";
        [Tooltip("The pad key drawn before Fly a squad instead.")]
        public string ArkFlySquadKey = "X";
        [Tooltip("From a squad back to your lobby's seats.")]
        public string ArkBackToLobby = "BACK TO YOUR LOBBY";
        [Tooltip("Under the voyage's name on the Ark page, before its difficulty.")]
        public string ArkVoyageLine = "{kind}";
        [Tooltip("The seat picker's title.")]
        public string PickTitle = "Change your seat";
        [Tooltip("The seat picker's title for an AI seat; {seat} is its place in the squad.")]
        public string PickTitleSeat = "Change {seat}";
        [Tooltip("Over the quick kits: a vessel, pilot, element and artifact in one press.")]
        public string PickKits = "QUICK KITS - ALL FOUR IN ONE PRESS";
        [Tooltip("The kit built for the open voyage (its pilot and its crystals' element).")]
        public string PickKitVoyage = "For this voyage";
        [Tooltip("The kit you last set sail with.")]
        public string PickKitLast = "Last flown";
        [Tooltip("Under a kit's name: what it sets.")]
        public string PickKitWho = "{vessel} - {pilot} - {element}";
        [Tooltip("The pilots' filter label: what each pilot sees.")]
        public string PickSees = "SEES";
        [Tooltip("The filter that shows every pilot.")]
        public string PickSeesAll = "All";
        [Tooltip("Pilots who see crystals.")]
        public string PickSeesCrystals = "Crystals";
        [Tooltip("Pilots who see prisms and mass.")]
        public string PickSeesPrisms = "Prisms";
        [Tooltip("Pilots who see plants and fauna.")]
        public string PickSeesLife = "Life";
        [Tooltip("Pilots who see vessels and Arks.")]
        public string PickSeesVessels = "Vessels";
        [Tooltip("Pilots who see gates, switches and solitons.")]
        public string PickSeesPaths = "Paths";
        [Tooltip("Over the choices: how many the filter shows.")]
        public string PickCount = "{shown} of {total}";
        [Tooltip("Over a pilot or vessel suggested for the open voyage.")]
        public string PickForVoyage = "FOR THIS VOYAGE";
        [Tooltip("Under the choices.")]
        public string PickNote = "One press takes it. Your seat behind updates as you pick.";
        [Tooltip("The seat picker's close mark.")]
        public string PickClose = "X";
        [Tooltip("A courier job's name.")]
        public string KindCourier = "Courier";
        [Tooltip("A vault hunt's name.")]
        public string KindHunt = "Vault hunt";
        [Tooltip("A salvage job's name.")]
        public string KindSalvage = "Salvage";
        [Tooltip("A defence job's name.")]
        public string KindDefence = "Defence";
        [Tooltip("A survey job's name.")]
        public string KindSurvey = "Survey";
        [Tooltip("A harvest job's name.")]
        public string KindHarvest = "Harvest";
        [Tooltip("A study job's name.")]
        public string KindStudy = "Study";
        [Tooltip("A rescue job's name.")]
        public string KindRescue = "Rescue";
        [Tooltip("A liberation job's name.")]
        public string KindLiberate = "Liberation";
        [Tooltip("A build job's name.")]
        public string KindBuild = "Build";
        [Tooltip("A thieves' lift's name.")]
        public string KindLift = "Lift";
        [Tooltip("A bounty hunt's name.")]
        public string KindBounty = "Bounty hunt";
        [Tooltip("A rite's name.")]
        public string KindRite = "Rite";
        [Tooltip("A parley's name.")]
        public string KindParley = "Parley";
        [Tooltip("A raid's name.")]
        public string KindRaid = "Piracy";
        [Tooltip("A tending job's name.")]
        public string KindTend = "Tending";
        [Tooltip("An exhibition's name.")]
        public string KindExhibition = "Exhibition";
        [Tooltip("A convoy's name.")]
        public string KindConvoy = "Convoy";
        [Tooltip("A cull's name (predators, or the Kaiju).")]
        public string KindCull = "Hunt";
        [Tooltip("The way home's name.")]
        public string KindHome = "Home";
        [Tooltip("The Ark escort's name.")]
        public string KindEscort = "Ark escort";
    }
}
