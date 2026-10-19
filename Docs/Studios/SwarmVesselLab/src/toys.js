// ============================================================================================
// toys.js — the toy box. Each toy is ONE fun moment worth testing, expressed as a build
// (species x control x economy x meta) in one arena. `fun` is the hypothesis, `try` is what to do,
// `watch` is the number on the HUD that says whether it happened. Keep / Cut / Merge buttons on each toy
// write to the decision log, so a play session leaves a record the next round can read.
// ============================================================================================
const T = (id, title, species, control, economy, meta, scene, fun, tryIt, watch, extra) => Object.assign({ id, title, species, control, economy, meta, scene, fun, try: tryIt, watch }, extra || {});
const TOYS = [
  // ---- Termite: the shipped anchor ----
  T('termite-mound', 'Mound rising', 'termite', 'focus', 'pheromone', 'deck', 'mass',
    'Somebody else\'s mass walks home on tiny legs and becomes YOUR mound.', 'Tap a ruby patch or a wild flora clump. Play New Mound where you want the city.', 'Mass score: your prisms standing'),
  T('termite-highway', 'Paint a highway', 'termite', 'paint', 'pheromone', 'none', 'mass',
    'You draw a road and the colony starts running it.', 'Drag a line from your mound to a flora clump and keep it fresh.', 'Built prisms'),
  T('termite-commander', 'Cell or queen? (2024)', 'termite', 'split', 'hive', 'none', 'mass',
    'The 2024 order system: half the colony works the mound while the other half guards the queen.', 'Send squad A to a clump, toggle, send squad B home. Feel the trade.', 'Mass score vs petals lost', { notion: 'Termite commander concept (2024): distributing termites between cell and queen' }),
  T('termite-clash', 'Clash commander', 'termite', 'cards', 'pheromone', 'deck', 'charge',
    'You never fly and never point. Every decision is which card, when, and where the swarm happens to be.', 'Play cards 1-4 against three hunting rivals.', 'Charge score'),
  T('termite-castes', 'Workers, soldiers, scouts', 'termite', 'orders', 'hive', 'castes', 'mass',
    'One colony, three jobs: the ratio IS your strategy.', 'Set the caste ratio in Build, then flip between Forage and Defend.', 'Mass score'),

  // ---- Ant ----
  T('ant-trail', 'The trail recruits', 'ant', 'focus', 'foodweb', 'none', 'mass',
    'One scout finds food; within seconds a scent highway forms and the colony streams down it.', 'Just watch the first minute. Then tap a far clump and see whether the trail follows.', 'Built prisms, members'),
  T('ant-starve', 'Starve or thrive', 'ant', 'paint', 'foodweb', 'none', 'space',
    'The colony breathes: it booms on a rich patch and starves back when it runs dry. No timer anywhere.', 'Paint them away from food and watch the population fall; paint them onto a clump and it booms.', 'Members, starved'),
  T('ant-draft', 'Roguelike colony', 'ant', 'focus', 'biomass', 'draft', 'space',
    'Every twenty seconds you pick one of three mutations and the colony becomes something new.', 'Take Jaws + Swift, then try a run of Keen + Clutch.', 'Space score'),

  // ---- Locust ----
  T('locust-snap', 'The phase snap', 'locust', 'mood', 'biomass', 'none', 'space',
    'Alone they dawdle. Crowded, they SNAP gregarious: aligned, fast, eating everything in a marching front.', 'Keep the mood low, then rush them into one clump and watch the colour change.', 'Space score, members'),
  T('locust-plague', 'Herd the plague', 'locust', 'sheepdog', 'biomass', 'none', 'space',
    'You cannot order them at all; you push a living front of mouths with your hull.', 'Fly round behind the swarm and drive it into the fortress.', 'Space score'),
  T('locust-evolve', 'Survival of the swarm', 'locust', 'mood', 'foodweb', 'evolve', 'sandbox',
    'Genes drift toward whatever SURVIVES — no score picks them. Watch speed and sense creep.', 'Run it a minute. Read the gene bars in the HUD.', 'Mean genes'),

  // ---- Pigeon ----
  T('pigeon-wheel', 'The homing wheel', 'pigeon', 'leash', 'crystal', 'none', 'time',
    'The whole flock wheels as one around your ship and peels off couriers to snatch the next gate.', 'Fly toward each gate. Let the flock take the ones you cannot reach.', 'Time score: gates'),
  T('pigeon-relay', 'Message relay', 'pigeon', 'focus', 'crystal', 'none', 'time',
    'You never touch a gate: you point, the flock carries.', 'Tap the next gate (it pulses). The ship just follows along.', 'Time score'),
  T('pigeon-leader', 'Possess the lead bird', 'pigeon', 'leader', 'fixed', 'none', 'time',
    'You ARE one bird; the flock follows you, and its slack is your skill.', 'Hold and drag a path through the gates.', 'Time score'),

  // ---- Rat ----
  T('rat-wall', 'Through the wall', 'rat', 'bait', 'biomass', 'none', 'space',
    'They chew through armour nobody else can eat — the fortress has a back door.', 'Drop bait on the fortress ring and watch the shields peel.', 'Space score'),
  T('rat-piper', 'Pied Piper', 'rat', 'rhythm', 'biomass', 'none', 'space',
    'Keep the beat and the horde follows the tune; miss it and they scatter.', 'Tap Beat (or Space) on the pulse; tap the arena to lead them.', 'Space score, streak'),
  T('rat-boom', 'Breeding explosion', 'rat', 'sheepdog', 'biomass', 'none', 'sandbox',
    'Twelve rats become sixty because you let them eat.', 'Herd them onto the biggest ruby patch and stand back.', 'Members'),

  // ---- Bee / Wasp ----
  T('bee-meadow', 'Claim the meadow', 'bee', 'orders', 'hive', 'none', 'mass',
    'Bees settle on wild flora and it turns yours where it stands. No carrying, no building.', 'Order Forage and tap a flora clump. Then try Defend.', 'Mass score'),
  T('bee-kamikaze', 'Each sting is a life', 'bee', 'orders', 'hive', 'none', 'charge',
    'Order an attack and you SPEND bees: a sting is permanent damage paid for with a body.', 'Hold Attack, then switch back before the hive is gone.', 'Charge score vs deaths'),
  T('wasp-nest', 'The nest boils over', 'wasp', 'mood', 'hive', 'none', 'charge',
    'One dial: calm, they hover; angry, they pour out and sting in waves.', 'Ride the mood slider. Find the setting where they win without dying.', 'Charge score'),
  T('wasp-tiers', 'Tiered fuel deck', 'wasp', 'cards', 'tiered', 'deck', 'charge',
    'Three tanks refilling at different speeds: cheap cards are spam, big cards are a plan.', 'Watch which tank you run dry first.', 'Charge score, plays'),
  T('wasp-merge', 'Three into one', 'wasp', 'focus', 'hive', 'merge', 'charge',
    'Your swarm shrinks into champions: fewer bodies, bigger stings.', 'Watch the elite count. Is fewer-but-bigger better here?', 'Charge score'),

  // ---- Starling / Bat / Firefly ----
  T('starling-decoy', 'Murmuration decoy', 'starling', 'leash', 'fixed', 'none', 'charge',
    'The rival fires into the cloud instead of you, and the cloud reforms.', 'Fly away from the hunters. Count the petals you DON\'T lose.', 'Petals lost'),
  T('starling-shape', 'Shape the cloud', 'starling', 'formation', 'fixed', 'none', 'charge',
    'Ring, line, wedge, blob — the same forty birds, four different shields.', 'Switch shapes while fleeing. Which one eats the most shots?', 'Petals lost, deaths'),
  T('bat-echo', 'Echo reveal', 'bat', 'focus', 'hive', 'none', 'dark',
    'Every ping lights up what was hidden, and the colony dives on what it found.', 'Tap dark areas. The rings are pings.', 'Crystals found'),
  T('firefly-sync', 'Sync flash', 'firefly', 'rhythm', 'fixed', 'none', 'charge',
    'Tap on the beat until the whole swarm blinks together — one flash stuns every rival in range.', 'Tap Beat on the pulse. Watch the sync meter climb.', 'Sync, flashes'),

  // ---- Mosquito / Crow / Piranha ----
  T('mosquito-throw', 'Throw the cloud', 'mosquito', 'cast', 'biomass', 'none', 'charge',
    'Wind up, fling: a cloud lands on a rival and it slows and bleeds petals into your tank.', 'Hold on a rival, release. Throw again when they drift back.', 'Charge score'),
  T('crow-mob', 'Mob and pickpocket', 'crow', 'select', 'crystal', 'none', 'charge',
    'Box three crows, tap a rival: they mob it, mark it, and the whole murder remembers.', 'Drag to select, tap to send. Send a second group to the same rival.', 'Charge score'),
  T('crow-heist', 'Crystal heist', 'crow', 'focus', 'crystal', 'none', 'time',
    'Crows steal every shiny thing, including the gate you are racing for.', 'Point at the next gate.', 'Time score'),
  T('piranha-blood', 'Blood in the water', 'piranha', 'bait', 'foodweb', 'none', 'space',
    'The first bite calls the school; the more they kill the faster they kill.', 'Drop bait on a ruby patch and watch the frenzy spread.', 'Space score'),

  // ---- Slime / Spider / Siphonophore / School / Tadpole / Octopus ----
  T('slime-road', 'Grow a road', 'slime', 'focus', 'foodweb', 'none', 'mass',
    'Explorers lay scent; where it runs thick between meals it hardens into a road of your mass.', 'Let it run. Tap between two clumps to bias the explorers.', 'Mass score'),
  T('spider-lane', 'Web the lane', 'spider', 'focus', 'fixed', 'none', 'charge',
    'Strands go up across the lane; a rival that flies through stops dead.', 'Tap where the hunters will pass. Then play bait with your ship.', 'Snared'),
  T('siphon-wall', 'Living wall', 'siphon', 'leash', 'fixed', 'none', 'charge',
    'A chain of bodies trails you like a tail; swing it between you and a rival and it eats the shots.', 'Circle so the tail sits between you and the hunters.', 'Petals lost'),
  T('school-ring', 'Plasma ring', 'school', 'leash', 'fixed', 'none', 'space',
    'Six fish, six cutting lines: fly THROUGH a patch and the ring scythes it.', 'Fly straight through ruby patches.', 'Space score'),
  T('school-lasso', 'Plasma lasso', 'school', 'mood', 'fixed', 'none', 'charge',
    'Widen the ring around a rival and close it: they burn crossing the lines.', 'Raise the mood to spin the ring faster.', 'Charge score'),
  T('tadpole-frog', 'Metamorphosis', 'tadpole', 'split', 'hive', 'none', 'space',
    'Five tadpoles crowd together and POP into a frog whose tongue snaps crystals from range.', 'Bring both squads to one spot so they crowd.', 'Merges'),
  T('octopus-shuffle', 'Shuffle arms', 'octopus', 'focus', 'pheromone', 'roulette', 'sandbox',
    'Eight arms, eight verbs, and every twelve seconds they all shuffle one seat along.', 'Watch the colours (verbs) rotate. Plan for the next seat.', 'Crystals + built + eaten'),
];
const TOYS_BY_ID = Object.fromEntries(TOYS.map((t) => [t.id, t]));
