export const meta = {
  name: 'pilot-bust-emblem-sweep',
  description: 'Readers look at painted pilot busts, whole and in enlarged quarters, and list every drawn device (emblem, insignia, symbol, hand or fist shape) and any lettering, with what it resembles',
  whenToUse: 'After a round of busts is painted and before it ships. First: python3 Tools/Build/pilot_art/creator.py tiles <scratch>/tiles [WORD...]. Then pass args {tiles: "<scratch>/tiles", jobs: [{reader: "A1", words: [...about ten busts]}, ...]}. Read every "resembles" that is not none and look at that bust yourself.',
  phases: [
    { title: 'Sweep', detail: 'the readers named in args.jobs, each with its own batch of busts' },
  ],
}

// args.tiles is the scratch folder creator.py tiles wrote (five pictures a bust: <WORD>_0.jpg the whole frame and
// <WORD>_1.jpg to _4.jpg its quarters, enlarged); args.jobs is the batches, about ten busts a reader. The readers
// are told nothing about the pictures and may open only the pictures. For a second, independent pass give the same
// busts in another order and other batches.
const TILES = args.tiles

const SCHEMA = {
  type: 'object',
  properties: {
    images: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          word: { type: 'string', description: 'the painting\'s name exactly as given (the file name before _0.jpg)' },
          devices: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                what: { type: 'string', description: 'what is drawn, in plain words, with its colours' },
                where: { type: 'string', description: 'where in the whole picture (e.g. lower right, on the upper arm)' },
                carrier: { type: 'string', enum: ['band or strip of cloth on an arm or sleeve', 'badge, pin, brooch or patch', 'pendant, buckle or carried object', 'other clothing', 'banner, flag, pennant or sail', 'wall or mural', 'sky, light or other backdrop', 'skin, fur, feathers or scales'] },
                width_fraction: { type: 'number', description: 'its width as a fraction of the whole picture\'s width, roughly (0.05 = one twentieth)' },
                visible_at_256: { type: 'boolean', description: 'would a viewer still notice it if the whole picture were shown 256 px wide' },
                resembles: { type: 'string', description: 'the real-world emblem, flag, logo, or religious, political, military, medical or protected symbol it resembles, or "none"' },
              },
              required: ['what', 'where', 'carrier', 'width_fraction', 'visible_at_256', 'resembles'],
            },
          },
          lettering: { type: 'array', items: { type: 'string' }, description: 'any letters, numerals, runes or pseudo-writing, with where; empty if none' },
          other_flaws: { type: 'string', description: 'any gross painting flaw you happened to notice (an extra limb, a hand with the wrong number of fingers, a duplicated feature), or "none"' },
        },
        required: ['word', 'devices', 'lettering', 'other_flaws'],
      },
    },
  },
  required: ['images'],
}

function prompt(words) {
  const files = words.map(w => `${w}: ` + [0, 1, 2, 3, 4].map(n => `${TILES}/${w}_${n}.jpg`).join('  ')).join('\n')
  return `You are checking ${words.length} finished paintings for ONE kind of flaw: drawn devices. Each painting is a square head-and-shoulders bust of an animal-headed person in clothes, standing in a place.

Each painting is given as five image files. <name>_0.jpg is the whole picture. <name>_1.jpg, _2.jpg, _3.jpg and _4.jpg are its upper left, upper right, lower left and lower right quarters, enlarged so that small things are visible (neighbouring quarters overlap a little).

Open ONLY these image files, with the Read tool. Do not open, list or search anything else, run no commands, and read no source code or documentation. Report only what you see in the pictures.

${files}

Work through the paintings one at a time. For each one, read its five files together in ONE step (five Read calls in the same message), look at the corners and the far background as carefully as at the figure, and settle what you found before you go on. Do not open a file twice.

For EACH painting:
1. List every DRAWN DEVICE you can see anywhere in the frame: on clothing; on a band, strip or knot of cloth on an arm or sleeve; on a badge, pin, brooch, patch, buckle or pendant; on a banner, flag, pennant, sail, wall or mural; or in the sky. A drawn device is any symbol, emblem, insignia, crest, logo, icon, pictogram, hand shape or hand print, fist, cross, crosshair, star, disc or ring with a figure in it, skull, or an animal or object silhouette used as a sign. An object that is shaped as a sign or carries one (a medallion with a figure, a marked lens) is a device too.
   NOT devices: a plain pattern (stripes, checks, colour blocks, a weave, scrollwork with no figure), a plain unmarked band or patch, plain round pins with nothing on them, ordinary buckles, and the animal's own markings. If you list a plain unmarked band or badge anyway, say "plain, nothing on it" in "what".
   For each device give what it is with its colours, where it is in the whole picture, what carries it, its rough width as a fraction of the whole picture, whether a viewer would still notice it with the whole picture shown 256 px wide, and which real-world emblem, flag, logo or religious, political, military, medical or protected symbol it resembles ("none" if it resembles none). Be specific and honest about resemblance: say so when a device on an armband, a hand on a banner, a cross on white, or a star or disc on a flag would remind a viewer of a real one.
2. List any LETTERING: letters, numerals, runes or pseudo-writing, and where.
3. In other_flaws, name any gross painting flaw you happened to notice, or "none". Do not hunt for these.

A painting with no drawn device gets devices: []. Do not guess what the pictures are for and do not name the animals unless it helps to say where something is. Return one entry per painting, with "word" exactly as its name is given above.`
}

phase('Sweep')
const jobs = args.jobs
log(`${jobs.length} readers: ${jobs.map(j => j.reader + ' (' + j.words.length + ')').join(', ')}`)

const results = await parallel(jobs.map(j => () =>
  agent(prompt(j.words), { label: `sweep ${j.reader}`, phase: 'Sweep', schema: SCHEMA, effort: 'medium' })
    .then(r => ({ reader: j.reader, words: j.words, result: r }))))

const out = { readers: [], missing: [] }
for (const r of results) {
  if (!r || !r.result) { out.missing.push(r ? r.reader : 'unknown'); continue }
  out.readers.push({ reader: r.reader, words: r.words, images: r.result.images })
}
if (out.missing.length) log(`readers that returned nothing: ${out.missing.join(', ')}`)
return out
