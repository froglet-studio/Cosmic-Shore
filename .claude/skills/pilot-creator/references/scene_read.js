export const meta = {
  name: 'pilot-scene-read',
  description: 'Independent readers check painted pilot scenes against the avatars: one finds each scene\'s character among the fifty numbered avatars, told nothing; one compares each scene with its own avatar',
  whenToUse: 'After Tools/Build/pilot_art/scene.py read-prep <dir>; pass args {dir, letters, matchers, comparers}; save the return value as <dir>/result.json and grade it with scene.py read-grade <dir>',
  phases: [
    { title: 'Match', detail: 'blind: which of the fifty avatars is this character?' },
    { title: 'Compare', detail: 'side by side: what carried over, what did not' },
  ],
}

// The readers are told nothing about the pilots: no brief, no names, no project. They may open only the pictures
// scene.py read-prep wrote. Keep these prompts as they are between rounds, or the rounds stop being comparable.
// One reader of each kind is the default (about 110k tokens each for four scenes); two of a kind read the scenes in
// opposite orders. More than two of a kind tells you little more.
const DIR = args.dir
const LETTERS = args.letters || ['A', 'B', 'C', 'D']
const MATCHERS = Math.min(args.matchers ?? 1, 2)     // 0 skips the blind match (a re-read of one repainted scene)
const COMPARERS = Math.min(args.comparers ?? 1, 2)
const ORDERS = [LETTERS, [...LETTERS].reverse()]
const KINDS = ['photograph', 'film still', '3D render', 'digital painting', 'other']

const MATCH_SCHEMA = {
  type: 'object',
  properties: {
    scenes: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          letter: { type: 'string', enum: LETTERS },
          kind_of_image: { type: 'string', enum: KINDS },
          kind_why: { type: 'string' },
          face_animal: { type: 'string' },
          second_animal: { type: 'string' },
          doing: { type: 'string' },
          candidates: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                number: { type: 'integer' },
                confidence: { type: 'string', enum: ['sure', 'likely', 'guess'] },
                why: { type: 'string' },
              },
              required: ['number', 'confidence', 'why'],
            },
          },
          lettering_or_symbols: { type: 'array', items: { type: 'string' } },
          anatomy_problems: { type: 'array', items: { type: 'string' } },
          figures_visible: { type: 'integer' },
        },
        required: ['letter', 'kind_of_image', 'kind_why', 'face_animal', 'second_animal', 'doing', 'candidates', 'lettering_or_symbols', 'anatomy_problems', 'figures_visible'],
      },
    },
  },
  required: ['scenes'],
}

const COMPARE_SCHEMA = {
  type: 'object',
  properties: {
    scenes: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          letter: { type: 'string', enum: LETTERS },
          same_character: { type: 'string', enum: ['yes', 'mostly', 'no'] },
          matches: { type: 'array', items: { type: 'string' } },
          differences: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                what: { type: 'string' },
                severity: { type: 'string', enum: ['a player would notice', 'only side by side', 'trivial'] },
              },
              required: ['what', 'severity'],
            },
          },
          added_permanent: { type: 'array', items: { type: 'string' } },
          trim_colour_present: { type: 'boolean' },
          eye_ring_visible: { type: 'boolean' },
          wear_seen: { type: 'array', items: { type: 'string' } },
          kind_of_image: { type: 'string', enum: KINDS },
          realism_breaks: { type: 'array', items: { type: 'string' } },
          lettering_or_symbols: { type: 'array', items: { type: 'string' } },
          anatomy_problems: { type: 'array', items: { type: 'string' } },
        },
        required: ['letter', 'same_character', 'matches', 'differences', 'added_permanent', 'trim_colour_present', 'eye_ring_visible', 'wear_seen', 'kind_of_image', 'realism_breaks', 'lettering_or_symbols', 'anatomy_problems'],
      },
    },
  },
  required: ['scenes'],
}

const list = (suffix) => LETTERS.map(x => `scene_${x}${suffix}.jpg`).join(', ')
const RULES = `You are an independent reader in a picture check. Use ONLY the Read tool, and ONLY on the files named below. Do not open, list or search for any other file, and do not use any other tool: other files hold the answers and reading them would spoil the check. Report what you see, not what you think was intended. If something is unclear at the size you see it, say it is unclear rather than guessing.`

const matchPrompt = (order) => `${RULES}

The files are in ${DIR}/match/ :
- avatars_1.jpg (portraits numbered 1 to 25) and avatars_2.jpg (portraits numbered 26 to 50). Fifty head-and-shoulders portraits of fifty different characters; the number is in the top left corner of each tile. Every character is a person who is part two animals: one animal gives the face, a second animal gives the top and the back of the head and one more piece of the body.
- ${list('')}: ${LETTERS.length} pictures, each showing one character in a place. ${list('_close')}: an enlarged crop of the figure's head and shoulders in the same picture.

Read both portrait sheets once. Then take the scenes in this order: ${order.join(', ')}. For each scene, read the full picture and its close crop, and report:
1. kind_of_image: what a stranger would call this image (a photograph, a film still, a 3D render, a digital painting, other), and kind_why in one phrase.
2. face_animal and second_animal: the two animals you see in the character (the face first), as specifically as you can.
3. doing: what the character is doing, in one phrase.
4. candidates: find this same individual among the fifty portraits. Judge by the character itself (the animals, the markings and colours, what grows from the head and the back, the clothes and what is worn), NOT by the place behind them. Give up to three portrait numbers, best first, each with sure, likely or guess, and what told you.
5. lettering_or_symbols: every piece of lettering, number, logo, emblem or symbol drawn anywhere in the scene, with where it is (an empty list if none).
6. anatomy_problems: anything anatomically wrong or unfinished: hands (count the fingers where you can see them), limbs, extra or missing parts, parts that melt into each other (an empty list if none).
7. figures_visible: how many people or figures are visible anywhere in the scene, counting far-off ones.

Return one entry per scene.`

const comparePrompt = (order) => `${RULES}

The files are in ${DIR}/compare/ . For each letter X of ${LETTERS.join(', ')} there are three:
- pair_X_avatar.jpg: a character's official portrait, a painted head-and-shoulders bust. The character is a person who is part two animals.
- scene_X.jpg: a new picture that is meant to show the SAME individual, in a place, in a realistic photographic style.
- scene_X_close.jpg: an enlarged crop of the figure's head and shoulders in that scene.

Take the letters in this order: ${order.join(', ')}. For each, read the three files and report:
1. same_character: yes, mostly or no. Would a player who knows the portrait accept the scene as the same individual?
2. matches: the features that carry over (the face animal and its markings, what grows from the head, the coat or skin behind the face, the piece on the back, the eyes, the hair, each garment, the trim colour, each accessory).
3. differences: every feature of the character that differs or is missing in the scene, each with a severity: "a player would notice", "only side by side" or "trivial". Ignore the change of style (painted against photographic), the pose, the light, and things that are simply out of view.
4. added_permanent: anything permanent on the body in the scene that the portrait does not have (a scar, a missing or broken part, a tattoo). Dirt, wet, soot and torn cloth are not permanent. An empty list if none.
5. trim_colour_present: the portrait's clothes are edged with piping in one bright colour; is that colour present on the clothes in the scene? eye_ring_visible: the portrait has a thin coloured ring around each iris; can it be seen in the scene?
6. wear_seen: the signs of wear and use visible in the scene (mud, soot, tears, stains, mends).
7. kind_of_image: what a stranger would call the scene (a photograph, a film still, a 3D render, a digital painting, other). realism_breaks: anything that breaks the sense of a real photograph of a real moment (too clean, too posed, plastic textures, a head like a mask, glow, anything else). An empty list if none.
8. lettering_or_symbols: every piece of lettering, number, logo, emblem or symbol drawn anywhere in the scene, with where it is. anatomy_problems: hands (count the fingers where you can), limbs, extra or missing parts.

Return one entry per letter.`

const jobs = []
for (let i = 0; i < MATCHERS; i++) jobs.push(() => agent(matchPrompt(ORDERS[i]), { label: `match:${i + 1}`, phase: 'Match', schema: MATCH_SCHEMA, effort: 'medium' }))
for (let i = 0; i < COMPARERS; i++) jobs.push(() => agent(comparePrompt(ORDERS[i]), { label: `compare:${i + 1}`, phase: 'Compare', schema: COMPARE_SCHEMA, effort: 'medium' }))
const results = await parallel(jobs)
return { match: results.slice(0, MATCHERS), compare: results.slice(MATCHERS) }
