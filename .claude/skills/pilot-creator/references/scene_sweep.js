export const meta = {
  name: 'pilot-scene-sweep',
  description: 'Independent readers, told nothing, look at painted pilot scenes: what kind of image each is, which animals the character is, what they are doing, how many figures there are, what any display shows, and every piece of lettering, broken anatomy, bare human skin or realism break',
  whenToUse: 'After Tools/Build/pilot_art/scene.py sweep-prep <dir>; pass args {dir, ids} as it prints them (per_reader: scenes a reader, default 10); save the return value as <dir>/result.json and grade it with scene.py sweep-grade <dir>',
  phases: [
    { title: 'Sweep', detail: 'one reader for every ten scenes, each scene read once' },
  ],
}

// The readers are told nothing about the pilots: no brief, no names, no project. They may open only the pictures
// scene.py sweep-prep wrote. Keep this prompt as it is between rounds, or the rounds stop being comparable.
// One reader takes ten scenes (four pictures a scene, about 150k tokens a reader: five readers of fifty scenes cost
// 765k on 2026-11-12), so fifty scenes are five readers:
// say that number before running it. A scene that was repainted is swept again on its own (prep a new folder with
// its word).
const DIR = args.dir
const IDS = args.ids
const PER = Math.max(1, Math.min(args.per_reader ?? 10, 12))
const KINDS = ['photograph', 'film still', '3D render', 'digital painting', 'other']

const SCHEMA = {
  type: 'object',
  properties: {
    scenes: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          kind_of_image: { type: 'string', enum: KINDS },
          kind_why: { type: 'string' },
          face_animal: { type: 'string' },
          second_animal: { type: 'string' },
          other_animals: { type: 'array', items: { type: 'string' } },
          doing: { type: 'string' },
          figures_visible: { type: 'integer' },
          face_readable: { type: 'boolean' },
          displays: { type: 'array', items: { type: 'string' } },
          lettering_or_symbols: { type: 'array', items: { type: 'string' } },
          anatomy_problems: { type: 'array', items: { type: 'string' } },
          bare_human_skin: { type: 'array', items: { type: 'string' } },
          permanent_marks: { type: 'array', items: { type: 'string' } },
          realism_breaks: { type: 'array', items: { type: 'string' } },
        },
        required: ['id', 'kind_of_image', 'kind_why', 'face_animal', 'second_animal', 'other_animals', 'doing', 'figures_visible', 'face_readable', 'displays', 'lettering_or_symbols', 'anatomy_problems', 'bare_human_skin', 'permanent_marks', 'realism_breaks'],
      },
    },
  },
  required: ['scenes'],
}

const prompt = (ids) => `You are an independent reader in a picture check. Use ONLY the Read tool, and ONLY on the files named below. Do not open, list or search for any other file, and do not use any other tool: other files hold the answers and reading them would spoil the check. Report what you see, not what you think was intended. If something is unclear at the size you see it, say it is unclear rather than guessing.

The files are in ${DIR}/ . For each id X of ${ids.join(', ')} there are four:
- X.jpg: a wide picture of one character in a place. The character is a person who is part two animals: one animal gives the face, a second animal gives the top and the back of the head and often one more piece of the body.
- X_close.jpg: an enlarged square crop of the character's head.
- X_left.jpg and X_right.jpg: the left and the right half of the wide picture, enlarged, for the small things.

Take the ids in the order given. For each, read the four files and report:
1. kind_of_image: what a stranger would call this image (a photograph, a film still, a 3D render, a digital painting, other), and kind_why in one phrase.
2. face_animal and second_animal: the two animals you see in the character (the face first), as specifically as you can, judged from the close crop first. other_animals: any further animal you would name for a part of the character (an empty list if none).
3. doing: what the character is doing, in one phrase.
4. figures_visible: how many people or figures are visible anywhere in the picture, counting far-off ones, reflections and statues of people.
5. face_readable: in the close crop, is the whole face sharp, lit and easy to read (true), or is it turned away, hidden, dark or soft (false)?
6. displays: every screen, panel, projection or lit chart in the picture, each with what it shows (an empty list if there is none).
7. lettering_or_symbols: every piece of lettering, number, logo, emblem or symbol drawn anywhere in the picture, with where it is (an empty list if none). Plain dots, rings and lines on a chart are not lettering; letters, digits and labels on it are.
8. anatomy_problems: anything anatomically wrong or unfinished: hands (count the fingers where you can see them), limbs, extra or missing parts, parts that melt into each other or into the clothes, a head that looks like a mask or a hat (an empty list if none).
9. bare_human_skin: any bare human skin showing (a human hand, wrist, neck, ankle or face part), with where (an empty list if none). Animal skin, fur, feathers and scales do not count.
10. permanent_marks: any scar, tattoo, eye patch or broken or missing body part on the character (an empty list if none). Dirt, wet, soot and torn cloth do not count.
11. realism_breaks: anything that breaks the sense of a real photograph of a real moment (too clean, too posed, plastic textures, glow, a painted look, anything else). An empty list if none.

Return one entry per id, with the id exactly as given.`

const chunks = []
for (let i = 0; i < IDS.length; i += PER) chunks.push(IDS.slice(i, i + PER))
log(`${IDS.length} scenes, ${chunks.length} reader(s)`)
const readers = await parallel(chunks.map((ids, i) => () =>
  agent(prompt(ids), { label: `sweep:${i + 1}`, phase: 'Sweep', schema: SCHEMA, effort: 'medium' })))
return { readers: readers.filter(Boolean) }
