export const meta = {
  name: 'pilot-bust-blind-read',
  description: 'Blind readers name the animals in shuffled pilot busts, at full size and at icon size',
  whenToUse: 'After Tools/Build/pilot_art/blind.py prep <dir>; pass args {dir, count}; save the return value as <dir>/result.json and grade it with blind.py grade <dir>',
  phases: [
    { title: 'Full size', detail: 'one reader for every ten portraits' },
    { title: 'Icon size', detail: 'two readers on the 64 px icon sheets' },
  ],
}

// The readers are told nothing: no brief, no names, no project. They may open only the pictures blind.py prep
// wrote. Keep these prompts as they are between rounds, or the rounds stop being comparable.
const DIR = args.dir
const COUNT = args.count || 50
const pid = i => 'p' + String(i).padStart(2, '0')
const ids = Array.from({ length: COUNT }, (_, i) => pid(i + 1))
const chunks = []
for (let i = 0; i < ids.length; i += 10) chunks.push(ids.slice(i, i + 10))
const sheets = []
for (let i = 0; i < COUNT; i += 25) sheets.push({ n: sheets.length + 1, from: pid(i + 1), to: pid(Math.min(i + 25, COUNT)) })

const FULL_SCHEMA = {
  type: 'object',
  properties: {
    results: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          animals: {
            type: 'array',
            description: 'Every animal you can see in the character, most obvious first, at most 4. Do not list human here.',
            items: {
              type: 'object',
              properties: {
                name: { type: 'string', description: 'the most specific animal name you would say (e.g. "lynx", "heron"); a group ("some bird") if you cannot tell' },
                evidence: { type: 'string', description: 'the visible feature that told you, 12 words at most' },
                confidence: { type: 'string', enum: ['sure', 'likely', 'guess'] },
              },
              required: ['name', 'evidence', 'confidence'],
            },
          },
          head_feature: { type: 'string', description: 'What grows from or sits on the top or sides of the head (ears, horns, crest, antennae, fins...), described plainly' },
          head_feature_reads_as: { type: 'string', description: 'What that head feature makes you think of first, in your own words (an animal, or something generic or mythical such as "unicorn horn", "devil horns", "fairy wings", "a hat")' },
          mythical_or_generic: { type: 'string', description: 'Anything about the character that reads as a mythical creature or a generic fantasy trope rather than a real animal; "none" if nothing' },
          humanoid: { type: 'boolean', description: 'Does it read as a person (upright, clothed) with animal features?' },
          human_features: { type: 'array', items: { type: 'string' }, description: 'Each human feature you see (e.g. "human eyes", "human hair: red bun", "human hand", "clothes")' },
          hand_visible: { type: 'boolean' },
          hand_kind: { type: 'string', enum: ['human', 'animal', 'unclear', 'none'] },
          hand_doing: { type: 'string', description: 'what the hand is doing, or "none"' },
          hair: { type: 'string', description: 'human-style hair if any (colour and style), or "none"' },
          background: { type: 'string', description: 'the background in one sentence: colours, shapes, objects' },
          pose: { type: 'string', description: 'pose and facing in one short sentence' },
          expression: { type: 'string', description: 'three words at most' },
          defects: { type: 'string', description: 'painting defects: extra or fused fingers, broken anatomy, stray text, a second face; "none" if clean' },
        },
        required: ['id', 'animals', 'head_feature', 'head_feature_reads_as', 'mythical_or_generic', 'humanoid', 'human_features', 'hand_visible', 'hand_kind', 'hand_doing', 'hair', 'background', 'pose', 'expression', 'defects'],
      },
    },
  },
  required: ['results'],
}

const THUMB_SCHEMA = {
  type: 'object',
  properties: {
    results: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          animals: { type: 'array', items: { type: 'string' }, description: 'the animals you can make out at this size, most obvious first, at most 3; empty if none' },
          first_impression: { type: 'string', description: 'what you would call this icon in 6 words or fewer' },
          background_colour: { type: 'string', description: 'two words at most' },
          hand_visible: { type: 'boolean' },
        },
        required: ['id', 'animals', 'first_impression', 'background_colour', 'hand_visible'],
      },
    },
  },
  required: ['results'],
}

const fullPrompt = c => `You are a blind reader in an art test. You have never seen these pictures and you know nothing about who or what they are meant to show. Your job is to report ONLY what the pixels show.

Open these ${c.length} image files, one at a time, with the Read tool (they are 640 px character portraits):
${c.map(id => `  ${DIR}/full/${id}.jpg`).join('\n')}

Rules:
- Open ONLY those ${c.length} files. Do not open, list or search any other file or folder, do not read any documentation, and do not use anything you may know about the project the pictures come from. If you know what such characters are "supposed" to be made of, ignore it: report what a stranger would see.
- For each picture, look carefully, then fill in one result entry with its id (the file name without .jpg).
- "animals": name every real animal you can recognise in the character, most obvious first. Be as specific as you honestly can ("lynx" rather than "cat" only if you really see a lynx). If a feature could belong to several animals, say the one a stranger would say first, and mark it "guess". Never invent a second animal just because you expect one: if you see only one animal, list one.
- "head_feature" and "head_feature_reads_as": describe what grows from the head and say honestly what it reads as. A single spiral horn reads as a unicorn horn; say so if that is what you see.
- "mythical_or_generic": if the whole character reads as a known mythical creature (unicorn, jackalope, faun, dragon, harpy, mermaid...) or a stock fantasy race rather than a mix of real animals, name it.
- Human features, the hand, the hair, the background, the pose and the expression: describe them plainly and briefly.
- "defects": look at the hands and the face for painting mistakes.

Return all ${c.length} results.`

const thumbPrompt = r => `You are a blind reader in an icon legibility test. You know nothing about these pictures. Report ONLY what you can make out.

Open these image files with the Read tool, ${r === 1 ? 'in this order' : 'last one first'}:
${sheets.map(s => `  ${DIR}/thumb/sheet${s.n}.png   (icons ${s.from} to ${s.to})`).join('\n')}

Each sheet is a grid of small profile icons, five to a row (64 px icons shown enlarged with hard pixels, so they are blocky on purpose). The label above each icon is its id.

Rules:
- Open ONLY those files. Do not open, list or search anything else, read no documentation, and do not use anything you may know about the project these come from.
- For every icon (all ${COUNT}), report: the animals you can make out at this size, most obvious first (at most 3; if you can only tell "a bird" or "a cat", say that; if you see no animal, leave the list empty); what you would call the icon in 6 words or fewer; the background colour; whether a hand is visible.
- Do not guess a second animal because you expect one. Name only what the blocky icon really lets you see.

Return all ${COUNT} results, in id order.`

const fullRun = parallel(chunks.map(c => () =>
  agent(fullPrompt(c), { label: `full:${c[0]}-${c[c.length - 1]}`, phase: 'Full size', schema: FULL_SCHEMA, effort: 'medium' })))
const thumbRun = parallel([1, 2].map(r => () =>
  agent(thumbPrompt(r), { label: `icon:reader${r}`, phase: 'Icon size', schema: THUMB_SCHEMA, effort: 'medium' })))
const [f, t] = await Promise.all([fullRun, thumbRun])
const full = f.filter(Boolean).flatMap(x => x.results)
const thumb = t.filter(Boolean).map(x => x.results)
log(`full-size reads: ${full.length}/${COUNT}; icon readers: ${thumb.length}/2`)
if (full.length < COUNT) log(`MISSING ${COUNT - full.length} full-size reads: a reader failed, so the totals cover fewer portraits`)
return { full, thumb }
