// ============================================================================================
// species.js — the four axes a swarm vessel is built from. Pure data: the model reads it, the UI lists it.
//   SPECIES   a body: how members move (genes) and the one VERB they do to the world
//   CONTROLS  how the pilot talks to the swarm (it only ever writes focus / order / mood / selection)
//   ECONOMIES what pays for members and cards
//   METAS     the layer above a match: deck, draft, roulette, merge, castes, evolution
//   CARDS     the spells a deck can hold, priced in the economy's pool
// `source` says where an idea came from: "shipped" (in the game today), "notion" (a design doc), "ecology"
// (Tools/Ecology bestiary), or "lab" (proposed here — nothing in the game or the docs backs it yet).
// ============================================================================================
const G = (sep, ali, coh, wander, orbit) => ({ sep, ali, coh, wander, orbit });
const SPECIES = [
  { id: 'termite', name: 'Termite', col: '#f0a043', n: 18, speedK: 1.0, size: 2.6, hp: 1, verb: 'build', alt: 'sting', genes: G(1, 0.2, 0.2, 0.3, 0.3), agility: 6, leash: 170, idleRing: 34, senseK: 1,
    control: 'focus', economy: 'pheromone', meta: 'deck', scene: 'mass', source: 'shipped',
    deck: ['brood', 'rally', 'autothysis', 'mound', 'teamCrystal', 'teleport', 'scout', 'frenzy'],
    fun: 'Point at somebody else\'s mass and watch it walk home and become YOUR mound.', role: 'Builder' },
  { id: 'ant', name: 'Ant', col: '#c9693c', n: 26, speedK: 1.0, size: 2.2, hp: 1, verb: 'trail', alt: 'sting', genes: G(1, 0.1, 0.05, 0.6, 0.1), agility: 7, leash: 999, idleRing: 40, senseK: 1,
    control: 'focus', economy: 'foodweb', meta: 'none', scene: 'mass', source: 'lab',
    fun: 'One ant finds food, lays a scent line home, and seconds later a highway of ants is running it.', role: 'Builder' },
  { id: 'locust', name: 'Locust', col: '#b5c24a', n: 34, speedK: 1.0, size: 2.4, hp: 1, verb: 'march', alt: 'graze', genes: G(1, 0.1, 0.25, 0.7, 0.2), agility: 5, leash: 220, idleRing: 50, senseK: 0.9,
    control: 'mood', economy: 'biomass', meta: 'none', scene: 'space', source: 'ecology',
    fun: 'Crowd them and they SNAP gregarious: aligned, fast, eating everything in a marching front.', role: 'Breaker' },
  { id: 'pigeon', name: 'Pigeon', col: '#9aa9c9', n: 16, speedK: 2.2, size: 2.8, hp: 1, verb: 'fetch', alt: 'graze', genes: G(1, 1.6, 0.4, 0.15, 0.6), agility: 3, leash: 260, idleRing: 60, senseK: 1.4,
    control: 'leash', economy: 'crystal', meta: 'none', scene: 'time', source: 'lab',
    fun: 'The whole flock wheels as one, peels off two couriers, and they come home with the crystal.', role: 'Runner' },
  { id: 'rat', name: 'Rat', col: '#8f8a85', n: 14, speedK: 1.25, size: 2.6, hp: 1.4, verb: 'gnaw', alt: 'graze', genes: G(0.8, 0.2, 0.5, 0.5, 0.2), agility: 8, leash: 190, idleRing: 30, senseK: 1,
    control: 'bait', economy: 'biomass', meta: 'none', scene: 'space', source: 'lab', leavesBody: true,
    fun: 'They chew THROUGH armour nobody else can eat, and every meal is another rat.', role: 'Breaker' },
  { id: 'bee', name: 'Bee', col: '#f2c84b', n: 20, speedK: 1.5, size: 2.2, hp: 1, verb: 'claim', alt: 'sting', genes: G(1, 0.5, 0.3, 0.35, 0.5), agility: 7, leash: 200, idleRing: 40, senseK: 1.2,
    control: 'orders', economy: 'hive', meta: 'none', scene: 'mass', source: 'lab', suicideSting: true,
    fun: 'Bees settle on wild flora and it turns yours where it stands; order an attack and each sting costs a bee.', role: 'Builder' },
  { id: 'wasp', name: 'Wasp', col: '#e0702f', n: 12, speedK: 1.7, size: 2.6, hp: 1.6, verb: 'sting', alt: 'graze', genes: G(1, 0.3, 0.2, 0.4, 0.7), agility: 8, leash: 180, idleRing: 34, senseK: 1.3,
    control: 'mood', economy: 'hive', meta: 'none', scene: 'charge', source: 'lab',
    fun: 'Turn the mood up and the nest boils over: wasps pour out, sting, fall back, and sting again.', role: 'Hunter' },
  { id: 'starling', name: 'Starling', col: '#7f8fa6', n: 40, speedK: 2.0, size: 1.8, hp: 1, verb: 'murmur', alt: 'graze', genes: G(1.2, 1.4, 0.6, 0.1, 0.0), agility: 4, leash: 120, idleRing: 40, senseK: 1,
    control: 'leash', economy: 'fixed', meta: 'none', scene: 'charge', source: 'lab', decoy: true,
    fun: 'A murmuration around your ship: the rival fires into the cloud, and the cloud reforms.', role: 'Support' },
  { id: 'bat', name: 'Bat', col: '#8a6fb5', n: 12, speedK: 2.0, size: 2.4, hp: 1, verb: 'ping', alt: 'fetch', genes: G(1, 0.2, 0.1, 0.5, 0.4), agility: 6, leash: 320, idleRing: 70, senseK: 1.3,
    control: 'focus', economy: 'hive', meta: 'none', scene: 'dark', source: 'lab', decoy: true,
    fun: 'Every ping lights up what was hidden, and the colony dives on what it found.', role: 'Scout' },
  { id: 'firefly', name: 'Firefly', col: '#d7f25a', n: 24, speedK: 1.0, size: 2.0, hp: 1, verb: 'flash', alt: 'graze', genes: G(1, 0.3, 0.4, 0.4, 0.5), agility: 5, leash: 150, idleRing: 40, senseK: 1,
    control: 'rhythm', economy: 'fixed', meta: 'none', scene: 'charge', source: 'lab',
    fun: 'Tap on the beat until the whole swarm blinks together, and that one flash stuns every rival in range.', role: 'Support' },
  { id: 'mosquito', name: 'Mosquito', col: '#c95b5b', n: 22, speedK: 1.8, size: 1.6, hp: 0.6, verb: 'drain', alt: 'graze', genes: G(0.9, 0.2, 0.3, 0.6, 0.3), agility: 9, leash: 260, idleRing: 30, senseK: 1.4,
    control: 'cast', economy: 'biomass', meta: 'none', scene: 'charge', source: 'lab',
    fun: 'Fling a cloud onto a rival: it slows under the weight and leaks petals into your tank.', role: 'Hunter' },
  { id: 'crow', name: 'Crow', col: '#3f4652', n: 10, speedK: 2.0, size: 3.0, hp: 1.6, verb: 'steal', alt: 'fetch', genes: G(1, 0.6, 0.3, 0.3, 0.6), agility: 5, leash: 260, idleRing: 50, senseK: 1.5,
    control: 'select', economy: 'crystal', meta: 'none', scene: 'charge', source: 'lab',
    fun: 'Drag a box round three crows, tap a rival: they mob it and pick its pocket.', role: 'Hunter' },
  { id: 'piranha', name: 'Piranha', col: '#d24a4a', n: 16, speedK: 1.3, size: 2.6, hp: 1.2, verb: 'frenzy', alt: 'graze', genes: G(1, 0.4, 0.5, 0.4, 0.5), agility: 7, leash: 200, idleRing: 34, senseK: 1,
    control: 'bait', economy: 'foodweb', meta: 'none', scene: 'space', source: 'lab',
    fun: 'The first bite puts blood in the water and the school goes into a feeding frenzy.', role: 'Breaker' },
  { id: 'slime', name: 'Slime mold', col: '#e6d36a', n: 30, speedK: 0.7, size: 2.0, hp: 1, verb: 'network', alt: 'graze', genes: G(0.6, 0, 0, 0.5, 0), agility: 4, leash: 999, idleRing: 60, senseK: 1,
    control: 'focus', economy: 'foodweb', meta: 'none', scene: 'mass', source: 'notion',
    fun: 'Explorers lay scent; where it runs thick between two meals it hardens into a road of your mass.', role: 'Builder' },
  { id: 'spider', name: 'Spider', col: '#a3a3a3', n: 6, speedK: 1.2, size: 3.2, hp: 2, verb: 'web', alt: 'sting', genes: G(1, 0.2, 0.2, 0.3, 0.5), agility: 5, leash: 160, idleRing: 40, senseK: 1,
    control: 'focus', economy: 'fixed', meta: 'none', scene: 'charge', source: 'lab',
    fun: 'Strands go up across the lane; a rival that flies through is caught and stops dead.', role: 'Support' },
  { id: 'siphon', name: 'Siphonophore', col: '#6fd4e6', n: 16, speedK: 1.6, size: 3.0, hp: 2, verb: 'link', alt: 'graze', genes: G(0.5, 0, 0, 0, 0), agility: 9, leash: 150, idleRing: 40, senseK: 1,
    control: 'leash', economy: 'fixed', meta: 'none', scene: 'charge', source: 'notion',
    fun: 'A chain of bodies trails you like a tail; whip it between you and a rival and it eats the shots.', role: 'Support' },
  { id: 'school', name: 'Plasma School', col: '#5fe0ff', n: 6, speedK: 2.2, size: 3.4, hp: 3, verb: 'plasma', alt: 'graze', genes: G(0.4, 0, 0, 0, 0), agility: 8, leash: 999, idleRing: 60, senseK: 1,
    control: 'leash', economy: 'fixed', meta: 'none', scene: 'space', source: 'notion',
    fun: 'Six fish hold a ring around you and the lines between them CUT whatever passes through.', role: 'Breaker' },
  { id: 'tadpole', name: 'Tadpole', col: '#5aa86a', n: 30, speedK: 1.1, size: 1.8, hp: 0.6, verb: 'merge', alt: 'graze', genes: G(1, 0.8, 0.5, 0.3, 0.4), agility: 7, leash: 200, idleRing: 34, senseK: 1,
    control: 'split', economy: 'hive', meta: 'none', scene: 'space', source: 'shipped',
    fun: 'Five tadpoles crowd together and pop into a frog whose tongue snaps crystals from range.', role: 'Breaker' },
  { id: 'octopus', name: 'Octopus', col: '#d77fb7', n: 8, speedK: 1.5, size: 3.2, hp: 2, verb: 'graze', verbs: ['graze', 'gnaw', 'fetch', 'sting', 'claim', 'build', 'ping', 'web'], alt: 'graze', genes: G(1, 0.2, 0.4, 0.2, 0.6), agility: 6, leash: 200, idleRing: 40, senseK: 1.1,
    control: 'focus', economy: 'pheromone', meta: 'roulette', scene: 'sandbox', source: 'notion',
    fun: 'Eight arms, eight different verbs, and every few seconds they all shuffle one seat along.', role: 'Flex' },
];
const SPECIES_BY_ID = Object.fromEntries(SPECIES.map((s) => [s.id, s]));

const CONTROLS = [
  { id: 'focus', name: 'Commander point', hull: 'auto', source: 'shipped', how: 'Tap or click the arena: the swarm works around that point. The queen flies herself.' },
  { id: 'paint', name: 'Pheromone brush', hull: 'auto', source: 'notion', how: 'Drag across the arena to paint scent; members climb the painted road.' },
  { id: 'leash', name: 'Escort', hull: 'stick', source: 'lab', how: 'Hold and drag to fly the ship; the swarm stays with you.' },
  { id: 'orders', name: 'D-pad orders', hull: 'auto', source: 'notion', how: 'Four orders: Forage, Gather (to the ship), Attack (nearest rival), Defend (the mound). Tap sets the focus. Keys Q W E R.' },
  { id: 'mood', name: 'Mood dial', hull: 'auto', source: 'notion', how: 'One slider from calm (escort, avoid) to rush (charge the nearest enemy mass). The NCA B5 knob.' },
  { id: 'select', name: 'Select and send', hull: 'auto', source: 'lab', how: 'Drag a box to select members, then tap where they go. "All" reselects everyone.' },
  { id: 'formation', name: 'Formations', hull: 'stick', source: 'lab', how: 'Fly the ship; pick Ring, Line, Wedge or Blob and the swarm holds that shape around you.' },
  { id: 'bait', name: 'Bait', hull: 'auto', source: 'notion', how: 'Tap to drop bait; members (and wild fauna) swarm it for six seconds.' },
  { id: 'sheepdog', name: 'Sheepdog', hull: 'stick', source: 'notion', how: 'You cannot order the swarm at all. Members flee your ship, so you herd them by where you fly.' },
  { id: 'cast', name: 'Throw', hull: 'auto', source: 'lab', how: 'Hold to wind up, release on a point: the swarm is flung there as a volley, then comes back.' },
  { id: 'split', name: 'Two squads', hull: 'auto', source: 'notion', how: 'Members are dealt into squad A and B. Tap toggles which squad your next tap sends.' },
  { id: 'rhythm', name: 'Rhythm', hull: 'auto', source: 'lab', how: 'Tap the Beat button (or Space) in time with the pulse. On-beat taps sync and empower the swarm; off-beat taps cost it.' },
  { id: 'leader', name: 'Possess one', hull: 'auto', source: 'lab', how: 'Hold and drag: one member follows your finger and every other member follows IT.' },
  { id: 'cards', name: 'Cards only', hull: 'auto', source: 'shipped', how: 'The swarm and the ship run themselves; you only play cards (the Termite\'s deck, the Clash Royale model).' },
];
const CONTROLS_BY_ID = Object.fromEntries(CONTROLS.map((c) => [c.id, c]));

const ECONOMIES = [
  { id: 'pheromone', name: 'Pheromone tank', unit: 'pher', source: 'shipped', how: 'A tank that refills on a clock (the Termite: 10 max, 0.357/s). Cards spend it. Members only come from cards.' },
  { id: 'biomass', name: 'Biomass brood', unit: 'food', source: 'ecology', how: 'Every few prisms eaten becomes a new member where it was eaten. The swarm grows on what it destroys.' },
  { id: 'crystal', name: 'Crystal tithe', unit: 'gems', source: 'lab', how: 'Every crystal collected hatches a clutch at the ship and banks a gem to spend on cards.' },
  { id: 'hive', name: 'Hive clock', unit: 'jelly', source: 'lab', how: 'The mound (or the ship, with no mound) lays a member on a clock up to the cap. Time breeds faster.' },
  { id: 'fixed', name: 'Fixed squad', unit: 'focus', source: 'notion', how: 'The squad you start with is the squad you have. No births, every loss is permanent (the School of six).' },
  { id: 'tiered', name: 'Tiered tanks', unit: 'fuel', source: 'notion', how: 'Three tanks (fast, medium, slow) refilling at different rates; each card draws on one (Boldt\'s tiered fuel).' },
  { id: 'foodweb', name: 'Food web', unit: 'food', source: 'ecology', how: 'Members cost upkeep and STARVE when food runs out; eating breeds. Population is bounded by consumption, never a timer.' },
];
const ECONOMIES_BY_ID = Object.fromEntries(ECONOMIES.map((e) => [e.id, e]));

const METAS = [
  { id: 'none', name: 'None', source: 'lab', how: 'Just the swarm.' },
  { id: 'deck', name: 'Card deck', source: 'shipped', how: 'A hand of four drawn from a deck of eight; a played card goes to the back. Keys 1-4.' },
  { id: 'draft', name: 'Draft', source: 'lab', how: 'Every 20 s the match pauses: pick one of three upgrades (roguelike).' },
  { id: 'roulette', name: 'Roulette', source: 'notion', how: 'Every 12 s every member\'s verb shifts one seat along a wheel (the Shuffle octopus).' },
  { id: 'merge', name: 'Merge', source: 'lab', how: 'Every 8 s three ordinary members fuse into one elite: fewer, tougher, harder-hitting.' },
  { id: 'castes', name: 'Castes', source: 'lab', how: 'Newborns are dealt into workers (the species verb), soldiers (sting) and scouts (ping + fetch) by the ratio you set.' },
  { id: 'evolve', name: 'Evolution', source: 'notion', how: 'Each newborn copies a living parent\'s genes, weighted by how long that parent has SURVIVED, plus mutation. Fitness is survival, never a score.' },
];
const METAS_BY_ID = Object.fromEntries(METAS.map((m) => [m.id, m]));

// A card targets the swarm's current focus (or the ship). `tier` is used by the tiered economy.
const CARDS = [
  { id: 'brood', name: 'Brood', cost: 3, tier: 1, src: 'Termite: Queen Drones', desc: '+6 members at the ship.' },
  { id: 'rally', name: 'Rally', cost: 1, tier: 0, src: 'lab', desc: 'Every member to the focus for 4 s.' },
  { id: 'frenzy', name: 'Frenzy', cost: 3, tier: 1, src: 'lab', desc: '6 s of +45% speed and full aggression.' },
  { id: 'autothysis', name: 'Autothysis', cost: 3, tier: 2, src: 'Termite: Autothysis', desc: 'Up to 8 members by the focus burst: hostile mass around each dies, rivals are stunned. The members die too.' },
  { id: 'mound', name: 'New Mound', cost: 5, tier: 2, src: 'Termite: New Mound', desc: 'Found a mound at the focus: builders lay there, the hive breeds there.' },
  { id: 'teamCrystal', name: 'Team Crystal', cost: 2, tier: 1, src: 'Termite: Team Crystal', desc: 'A crystal appears at the focus.' },
  { id: 'teleport', name: 'Teleport', cost: 2, tier: 0, src: 'Termite: Teleport', desc: 'The ship jumps to the focus.' },
  { id: 'scout', name: 'Scout', cost: 2, tier: 0, src: 'lab', desc: 'Reveal everything hidden within 220 of the focus.' },
  { id: 'decoy', name: 'Decoy Crystal', cost: 2, tier: 0, src: 'Notion: Angler fake crystal', desc: 'A fake crystal at the focus: rivals chase it for 8 s.' },
  { id: 'webs', name: 'Web Burst', cost: 3, tier: 1, src: 'lab', desc: 'Six web strands around the focus.' },
  { id: 'molt', name: 'Molt', cost: 2, tier: 1, src: 'lab', desc: 'For 10 s every member uses the species\' second verb.' },
  { id: 'shield', name: 'Shield Wall', cost: 2, tier: 0, src: 'lab', desc: 'For 6 s every member rings the ship and eats shots.' },
];
const CARDS_BY_ID = Object.fromEntries(CARDS.map((c) => [c.id, c]));
const DEFAULT_DECK = ['brood', 'rally', 'frenzy', 'autothysis', 'scout', 'decoy', 'mound', 'shield'];

const SCENES = [
  { id: 'sandbox', name: 'Sandbox', el: '', how: 'A bit of everything: flora, a rival fortress, two rivals, crystals, grazers.' },
  { id: 'time', name: 'Time: race', el: 'time', how: 'Eight gate crystals in order. Score: gates, plus seconds to spare if you finish.' },
  { id: 'space', name: 'Space: destruction', el: 'space', how: 'A rival fortress (part armoured) and fields of hostile mass. Score: hostile prisms destroyed.' },
  { id: 'mass', name: 'Mass: creation', el: 'mass', how: 'Wild flora to work and two rivals grazing your mass. Score: your prisms standing at the end.' },
  { id: 'charge', name: 'Charge: dogfight', el: 'charge', how: 'Two rival ships hunting you. Score: petals taken minus petals lost, plus 3 per rival knocked out.' },
  { id: 'dark', name: 'Dark: hidden crystals', el: 'time', how: 'Ten crystals you cannot see until something reveals them. Score: crystals collected.' },
];
const SCENES_BY_ID = Object.fromEntries(SCENES.map((s) => [s.id, s]));
const GAMES = ['time', 'space', 'mass', 'charge'];
