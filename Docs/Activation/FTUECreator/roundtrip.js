// node roundtrip.js <core.js> <repo>: every shipped phase must survive parse -> write -> parse
// field for field, then writes out/seed.json (the shipped MainQuest in run order) for the page.
const [, , corePath, repo] = process.argv;
const F = require(corePath), fs = require('fs'), path = require('path');
const base = path.join(repo, 'Assets/FTUE/DataContainer/');
const guidOf = f => /guid: (\w+)/.exec(fs.readFileSync(f + '.meta', 'utf8'))[1];
let bad = 0;
const byGuid = {};
for (const f of fs.readdirSync(base + 'Phases').filter(f => f.endsWith('.asset'))) {
  const t = fs.readFileSync(base + 'Phases/' + f, 'utf8');
  const p = F.parsePhaseAsset(t, guidOf(base + 'Phases/' + f));
  const y = F.writePhaseAsset(JSON.parse(JSON.stringify(p)));
  const A = Object.fromEntries(F.splitDocs(t).map(d => [d.id, F.parseDoc(d.body)]));
  const B = Object.fromEntries(F.splitDocs(y).map(d => [d.id, F.parseDoc(d.body)]));
  let n = 0;
  for (const id in A) for (const k in A[id]) if (k !== 'm_EditorClassIdentifier' && JSON.stringify(A[id][k]) !== JSON.stringify((B[id] || {})[k])) { n++; console.error(`  ${f} ${id} ${k}`); }
  if (p.unknownCount) { n++; console.error(`  ${f}: ${p.unknownCount} node types the creator does not know`); }
  console.log(`${f}: ${p.nodes.length} nodes, ${n ? n + ' differences' : 'round trip ok'}`);
  bad += n; byGuid[p.guid] = p;
}
const qPath = base + 'Quests/MainQuest.asset';
const q = F.parseQuestAsset(fs.readFileSync(qPath, 'utf8')); q.guid = guidOf(qPath);
const phases = q.phaseGuids.map(g => Object.assign(byGuid[g], { inQuest: 1 }));
for (const g in byGuid) if (!q.phaseGuids.includes(g)) phases.push(Object.assign(byGuid[g], { inQuest: 0 }));
delete q.phaseGuids;
fs.writeFileSync(path.join(path.dirname(corePath), 'seed.json'), JSON.stringify({ quest: q, phases }));
process.exit(bad ? 1 : 0);
