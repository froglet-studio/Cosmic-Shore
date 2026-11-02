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
                 "{squad} the seats, {intensity} the course's I1-I4, {reward} the crystals it pays, {pilot} and {story} " +
                 "who brought it. <br> breaks a line.")]
        [TextArea] public string CardDetails =
            "{summary}<br><br>{to}: {place}<br><br>SQUAD  {squad}<br>INTENSITY  I{intensity}<br>PAYS  {reward} CRYSTALS<br><br>{pilot}: {story}";
        [Tooltip("The way home's brief (the same tokens; it pays nothing itself).")]
        [TextArea] public string CardHomeDetails = "{summary}<br><br>SQUAD  {squad}<br>INTENSITY  I{intensity}";
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
        public string MapTierCellWhat = "one 20 ly cell and the stars inside it";
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
    }
}
