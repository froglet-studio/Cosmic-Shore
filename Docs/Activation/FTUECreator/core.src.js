/* FTUE Creator core: the Quest Graph schema, a Unity YAML reader and writer for
   QuestPhaseGraphSO / QuestSO assets, and validation. Pure functions, no DOM. */
(function (root) {
  'use strict';
  const ENUMS = __ENUMS__;
  ENUMS.QuestNavTarget = [['Home', 0], ['Store', 1], ['Port', 2], ['Hangar', 3], ['Profile', 4], ['ArcadeModal', 5]];
  ENUMS.QuestMicrogameSource = [['GameOfTheWeek', 0], ['SpecificMode', 1]];

  const GUID = {
    QuestPhaseGraphSO: '4262bc93f9a346d184d698a04d485256',
    QuestSO: '5624c2ba33ab44b8af6d26dbf100d099',
  };
  const SPEAKERS = [
    { label: 'Trace (captain)', ref: '{fileID: 11400000, guid: c7150b96b57ea6719cf13977333a9a45, type: 2}' },
    { label: 'None', ref: '{fileID: 0}' },
  ];

  // f: [name, kind, default, extra]. kinds: str, text, lines, bool, float, int, enum, enumlist, ref
  const NODE_TYPES = {
    QuestDialogueNode: { label: 'Dialogue', cat: 'Presentation', guid: 'dd54a9581c9f4ef5a35984a1bf6ea1c7',
      tip: 'The dialogue panel (speaker, typewriter, Next) with the lines authored here. Advances after the last line.',
      fields: [['speakerName', 'str', 'Captain'], ['portrait', 'ref', '{fileID: 0}'], ['lines', 'lines', []]] },
    QuestShowInstructionNode: { label: 'Show Instruction', cat: 'Presentation', guid: '14aa7558bfce4c21bd28232276b6e48e',
      tip: 'Shows instruction text on the overlay, optionally on a hand-built panel (panelKey).',
      fields: [['text', 'text', ''], ['haptic', 'enum', 0, 'HapticType'], ['panelKey', 'str', ''], ['minDisplaySeconds', 'float', 0, { min: 0 }], ['hideOnAdvance', 'bool', 0]] },
    QuestGuideToMicrogameNode: { label: 'Guide to Microgame', cat: 'Gameplay', guid: 'fe3087466933446b89945a6928289b43',
      tip: 'Spotlights Arcade, then the card, then the preview window, and holds until the Lesson ends. The guided path.',
      fields: [['source', 'enum', 0, 'QuestMicrogameSource'], ['mode', 'enum', 33, 'GameModes'],
        ['arcadeCaption', 'text', 'Your first race is waiting. Open the Arcade.'], ['cardCaption', 'text', "This week's game. Open it."],
        ['previewCaption', 'text', 'Tap the window to fly.'], ['speaker', 'speaker', SPEAKERS[0].ref],
        ['arcadeAction', 'str', 'Arcade'], ['cardAction', 'str', 'Open'], ['previewAction', 'str', 'Fly'], ['actionKeycap', 'str', 'A'],
        ['skipLabel', 'str', 'Skip tutorial'], ['skipKeycap', 'str', 'Y'], ['dimAlpha', 'float', 0.7, { min: 0, max: 1 }], ['lostStepGraceSeconds', 'float', 1.5, { min: 0.1 }]] },
    QuestEnterFreestyleNode: { label: 'Enter Freestyle', cat: 'Gameplay', guid: 'fbc0f9b1f3744123b82e188b543e4369', venue: 'Gameplay', legacy: true,
      tip: 'Moves the camera into the player vessel (freestyle). Carries the player: breaks the guided-path rule in new graphs.',
      fields: [['timeoutSeconds', 'float', 8, { min: 1 }]] },
    QuestExitFreestyleNode: { label: 'Exit Freestyle', cat: 'Gameplay', guid: '6fb119a657ae4fc6b3c4239ee5eed551', venue: 'AppShell',
      tip: "Waits for the player to leave freestyle (or forces it). Forcing carries the player.",
      fields: [['forceExit', 'bool', 0], ['timeoutSeconds', 'float', 8, { min: 1 }]] },
    QuestNavigateNode: { label: 'Navigate', cat: 'Gameplay', guid: 'aa42ada9f62240fead8854d23241c687', legacy: true,
      tip: 'Force-navigates the menu. Carries the player: breaks the guided-path rule in new graphs.',
      fields: [['target', 'enum', 5, 'QuestNavTarget']] },
    QuestLockNavigationNode: { label: 'Lock Navigation', cat: 'Gameplay', guid: 'd9b7537175a941d3934f6a72cf33d992', lock: f => !f.unlock,
      tip: 'Locks the nav bar buttons, or restores them.',
      fields: [['unlock', 'bool', 0]] },
    QuestSetArcadeConstraintsNode: { label: 'Set Arcade Constraints', cat: 'Gameplay', guid: '0bab776763f0423199d20750edaec3af', lock: f => !f.clearConstraints,
      tip: 'Narrows the arcade to one card and a forced intensity, player and team count, or clears the funnel.',
      fields: [['clearConstraints', 'bool', 0], ['allowedMode', 'enum', 35, 'GameModes'], ['forcedIntensity', 'int', 1, { min: 0, max: 4 }],
        ['forcedPlayerCount', 'int', 2, { min: 0, max: 12 }], ['forcedDomainCount', 'int', 2, { min: 0, max: 3 }]] },
    QuestSetButtonInteractableNode: { label: 'Set Button Interactable', cat: 'Gameplay', guid: '3014b5d137374f2695269fce5020c23a', lock: f => !f.interactable,
      tip: "Turns one of the runner's Quest Buttons on or off by key.",
      fields: [['buttonKey', 'str', 'episodes'], ['interactable', 'bool', 1]] },
    QuestWaitForInputNode: { label: 'Wait for Input', cat: 'Gate', guid: '374bdf97082c49408dffd573e64cdb29', venue: 'Gameplay',
      tip: 'Waits for one of the accepted vessel inputs (empty = any input).',
      fields: [['acceptedInputs', 'enumlist', [], 'InputEvents'], ['successHaptic', 'enum', 1, 'HapticType']] },
    QuestWaitForSkimNode: { label: 'Wait for Skim', cat: 'Gate', guid: '964a9d6c3be2407aa5af0d1dd1691fab', venue: 'Gameplay',
      tip: 'Counts prisms skimmed and advances at the target, with a live counter.',
      fields: [['targetSkims', 'int', 10, { min: 1 }], ['perSkimHaptic', 'enum', 0, 'HapticType'], ['successHaptic', 'enum', 1, 'HapticType']] },
    QuestWaitForGamePlayedNode: { label: 'Wait for Game Played', cat: 'Gate', guid: '1a60d2f0b34c468d9112e27ffd7c2a6c', venue: 'Gameplay', venueAfter: 'AppShell',
      tip: 'Waits for a finished run, optionally of one mode at a minimum intensity.',
      fields: [['filterByMode', 'bool', 0], ['expectedMode', 'enum', 35, 'GameModes'], ['minIntensity', 'int', 0, { min: 0, max: 4 }]] },
    QuestWaitForIntensityNode: { label: 'Wait for Intensity', cat: 'Gate', guid: '3135c9b91c954d15b031ab7987d17d67',
      tip: "Waits until a mode's unlocked intensity reaches the tier (4 completes the mode's quest).",
      fields: [['mode', 'enum', 35, 'GameModes'], ['intensityTier', 'int', 4, { min: 2, max: 4 }]] },
    QuestWaitForModeUnlockedNode: { label: 'Wait for Mode Unlocked', cat: 'Gate', guid: '3d534964de1e485fb54863c8270c51e6',
      tip: 'Waits until the player claims the mode on the quest track.',
      fields: [['mode', 'enum', 33, 'GameModes']] },
    QuestWaitForUserActionNode: { label: 'Wait for User Action', cat: 'Gate', guid: 'fc71607207fd49ea9b400656e8aff5d3',
      tip: 'Waits for a menu action (view a screen, unlock a vessel, play a game).',
      fields: [['action', 'enum', 320, 'UserActionType']] },
    QuestWaitForLessonNode: { label: 'Wait for Lesson', cat: 'Gate', guid: 'c7d3da56538a481eaf5a560867e83581',
      tip: 'Waits until a microgame Lesson has been completed or skipped.', fields: [] },
    QuestPhaseEndNode: { label: 'Phase End', cat: 'Terminal', guid: '4df76aa225ea4535b52c2e2352dc79e1', terminal: true,
      tip: 'Ends this phase; the runner starts the next one.', fields: [] },
    QuestEndNode: { label: 'Quest End', cat: 'Terminal', guid: 'fe7733c75551462a9e3732fed37e9d98', terminal: true,
      tip: 'Completes the whole quest.', fields: [] },
  };
  const GUID_TO_TYPE = {};
  for (const [k, v] of Object.entries(NODE_TYPES)) GUID_TO_TYPE[v.guid] = k;

  // ---------- ids ----------
  function rndHex(n) {
    const a = new Uint8Array(n / 2);
    (root.crypto || require('crypto').webcrypto).getRandomValues(a);
    return Array.from(a, b => b.toString(16).padStart(2, '0')).join('');
  }
  function rndFileId() {
    // Unity sub-asset local ids: signed 64-bit, never 0 or 11400000.
    const hex = rndHex(16);
    let v = BigInt('0x' + hex) & ((1n << 63n) - 1n);
    if (v < 1000000n) v += 1000000n;
    return (hex[0] > '7' ? '-' : '') + v.toString();
  }

  // ---------- YAML reading (the subset Unity writes for these assets) ----------
  function unDQ(s) {
    // YAML double-quoted, already folded. JSON covers \n \" \\ \uXXXX \t \/.
    s = s.replace(/\\x([0-9a-fA-F]{2})/g, '\\u00$1').replace(/\\ /g, ' ').replace(/\\0/g, '\\u0000')
      .replace(/\\([^"\\\/bfnrtu])/g, '$1');
    return JSON.parse('"' + s.replace(/\t/g, '\\t') + '"');
  }
  function foldDQ(lines) {
    // Join a multi-line double-quoted scalar: line breaks fold to a space, blank lines to \n,
    // and a trailing backslash escapes the break.
    let out = '';
    for (let i = 0; i < lines.length; i++) {
      let l = i === 0 ? lines[i] : lines[i].replace(/^\s+/, '');
      if (i < lines.length - 1) l = l.replace(/\s+$/, '');
      if (i === 0) { out = l; continue; }
      if (l === '') { out += '\\n'; continue; }
      if (out.endsWith('\\')) out = out.slice(0, -1) + l;
      else out += (out.endsWith('\\n') ? '' : ' ') + l;
    }
    return out;
  }
  function scalar(raw) {
    raw = raw.trim();
    if (raw.startsWith('"')) return unDQ(raw.slice(1, raw.lastIndexOf('"')));
    if (raw.startsWith("'")) return raw.slice(1, raw.lastIndexOf("'")).replace(/''/g, "'");
    return raw;
  }
  // Gathers a key's value: the first piece plus every deeper-indented continuation line.
  function readValue(lines, i) {
    const parts = [lines[i].slice(lines[i].indexOf(':') + 1).replace(/^ /, '')];
    let j = i + 1;
    while (j < lines.length) {
      const l = lines[j];
      if (l.trim() === '') { if (j + 1 < lines.length && /^   +\S/.test(lines[j + 1])) { parts.push(''); j++; continue; } break; }
      if (!/^   +\S/.test(l)) break;
      parts.push(l); j++;
    }
    const f = foldDQ(parts).trim();
    let value;
    if (f.startsWith('"')) value = unDQ(f.slice(1, f.lastIndexOf('"')));
    else if (f.startsWith("'")) value = f.slice(1, f.lastIndexOf("'")).replace(/''/g, "'");
    else value = f.replace(/\\n/g, '\n');
    return { value, next: j };
  }
  function parseDoc(text) {
    // One MonoBehaviour document: returns flat map of top-level (indent 2) keys.
    const lines = text.split('\n');
    const out = {};
    let i = 0;
    while (i < lines.length && !/^MonoBehaviour:/.test(lines[i])) i++;
    i++;
    while (i < lines.length) {
      const l = lines[i];
      const m = l.match(/^  ([A-Za-z_][\w]*):(.*)$/);
      if (!m) { i++; continue; }
      const key = m[1];
      const rest = m[2].trim();
      if (rest === '' && i + 1 < lines.length && /^  - /.test(lines[i + 1])) {
        // block list
        const items = [];
        let j = i + 1;
        while (j < lines.length && /^  - /.test(lines[j])) {
          const item = lines[j].slice(4);
          const km = item.match(/^([A-Za-z_]\w*): (.*)$|^([A-Za-z_]\w*):$/);
          if (km && !item.startsWith('{') && !item.startsWith('"')) {
            const obj = {};
            const sub = ['    ' + item];
            j++;
            while (j < lines.length && /^    \S/.test(lines[j])) { sub.push(lines[j]); j++; }
            for (let s = 0; s < sub.length; s++) {
              const mm = sub[s].match(/^    ([A-Za-z_]\w*):(.*)$/);
              if (mm) obj[mm[1]] = scalar(mm[2]);
            }
            items.push(obj);
          } else {
            // scalar list item, possibly multi-line
            const parts = [item];
            j++;
            while (j < lines.length && (/^    \S/.test(lines[j]) || (lines[j].trim() === '' && j + 1 < lines.length && /^    \S/.test(lines[j + 1])))) { parts.push(lines[j]); j++; }
            const f = foldDQ(parts);
            const ft = f.trim();
            if (ft.startsWith('"')) items.push(unDQ(ft.slice(1, ft.lastIndexOf('"'))));
            else if (ft.startsWith("'")) items.push(ft.slice(1, ft.lastIndexOf("'")).replace(/''/g, "'"));
            else items.push(parts.map(p => p.trim()).join(' '));
          }
        }
        out[key] = items;
        i = j;
        continue;
      }
      if (rest === '[]') { out[key] = []; i++; continue; }
      const r = readValue(lines, i);
      out[key] = r.value;
      i = r.next;
    }
    return out;
  }
  function splitDocs(text) {
    const re = /^--- !u!(\d+) &(-?\d+)[^\n]*$/gm;
    const marks = [];
    let m;
    while ((m = re.exec(text))) marks.push({ cls: m[1], id: m[2], at: m.index, end: re.lastIndex });
    return marks.map((mk, k) => ({ cls: mk.cls, id: mk.id, body: text.slice(mk.end, k + 1 < marks.length ? marks[k + 1].at : text.length) }));
  }
  const refFileId = r => { const m = /fileID:\s*(-?\d+)/.exec(r || ''); return m ? m[1] : '0'; };
  const refGuid = r => { const m = /guid:\s*([0-9a-f]{32})/.exec(r || ''); return m ? m[1] : ''; };
  const num = v => { const n = Number(v); return Number.isFinite(n) ? n : 0; };
  function decodeIntList(hex) {
    if (Array.isArray(hex)) return hex.map(num);
    hex = (hex || '').trim();
    const out = [];
    for (let k = 0; k + 8 <= hex.length; k += 8) {
      const b = hex.slice(k, k + 8);
      out.push(parseInt(b.slice(6, 8) + b.slice(4, 6) + b.slice(2, 4) + b.slice(0, 2), 16) | 0);
    }
    return out;
  }
  function encodeIntList(arr) {
    return arr.map(v => { const u = (v >>> 0).toString(16).padStart(8, '0'); return u.slice(6, 8) + u.slice(4, 6) + u.slice(2, 4) + u.slice(0, 2); }).join('');
  }

  function fieldFromYaml(kind, raw, def) {
    if (raw === undefined) return clone(def);
    switch (kind) {
      case 'bool': return num(raw) ? 1 : 0;
      case 'float': case 'int': case 'enum': return num(raw);
      case 'enumlist': return decodeIntList(raw);
      case 'lines': return Array.isArray(raw) ? raw.map(String) : (raw ? [String(raw)] : []);
      case 'ref': case 'speaker': return String(raw).trim() || '{fileID: 0}';
      default: return String(raw);
    }
  }
  const clone = v => (v && typeof v === 'object') ? JSON.parse(JSON.stringify(v)) : v;

  // Parse one QuestPhaseGraphSO .asset into the editor model.
  function parsePhaseAsset(text, guid) {
    const docs = splitDocs(text);
    const nodesById = {};
    let graph = null;
    const unknown = [];
    for (const d of docs) {
      const map = parseDoc(d.body);
      const sg = refGuid(map.m_Script);
      if (sg === GUID.QuestPhaseGraphSO) { graph = { fileId: d.id, map }; continue; }
      const type = GUID_TO_TYPE[sg];
      if (!type) { unknown.push({ fileId: d.id, raw: d.body }); continue; }
      const spec = NODE_TYPES[type];
      const fields = {};
      for (const [name, kind, def] of spec.fields) fields[name] = fieldFromYaml(kind, map[name], def);
      nodesById[d.id] = {
        fileId: d.id, type, nodeId: map.nodeId || rndHex(32), objName: map.m_Name || '',
        displayName: map.displayName !== undefined ? map.displayName : (map.m_Name || ''),
        enabled: map.nodeEnabled === undefined ? 1 : num(map.nodeEnabled),
        pos: parsePos(map.graphPosition),
        outputs: (Array.isArray(map.outputs) ? map.outputs : []).map(o => ({ port: o.portName || 'next', target: o.targetNodeId || '', delay: num(o.delaySeconds) })),
        fields,
      };
    }
    if (!graph) throw new Error('No QuestPhaseGraphSO found. Paste or open a phase .asset from Assets/FTUE/DataContainer/Phases.');
    const g = graph.map;
    const order = (Array.isArray(g.nodes) ? g.nodes : []).map(refFileId);
    const nodes = order.map(id => nodesById[id]).filter(Boolean);
    for (const n of Object.values(nodesById)) if (!nodes.includes(n)) nodes.push(n);
    const entry = nodesById[refFileId(g.entryNode)];
    return {
      guid: guid || rndHex(32), graphFileId: graph.fileId,
      assetName: (g.m_Name || g.graphId || 'QuestPhase'),
      graphId: g.graphId || '', phaseName: g.phaseName || '',
      enabled: g.phaseEnabled === undefined ? 1 : num(g.phaseEnabled),
      underUnlock: num(g.runsUnderDeveloperUnlock) ? 1 : 0,
      notes: g.designerNotes || '',
      entry: entry ? entry.nodeId : (nodes[0] ? nodes[0].nodeId : ''),
      nodes, unknownCount: unknown.length,
      canvasZoom: num(g.canvasZoom) || 1, canvasScroll: parsePos(g.canvasScroll),
    };
  }
  function parsePos(r) {
    const x = /x:\s*(-?[\d.eE+-]+)/.exec(r || ''), y = /y:\s*(-?[\d.eE+-]+)/.exec(r || '');
    return { x: x ? num(x[1]) : 0, y: y ? num(y[1]) : 0 };
  }
  function parseQuestAsset(text) {
    const d = splitDocs(text).find(d => refGuid(parseDoc(d.body).m_Script) === GUID.QuestSO);
    if (!d) throw new Error('No QuestSO found.');
    const m = parseDoc(d.body);
    return {
      assetName: m.m_Name || 'Quest', questId: m.questId || '', enabled: num(m.questEnabled),
      underUnlock: num(m.runsUnderDeveloperUnlock) ? 1 : 0, notes: m.designerNotes || '',
      phaseGuids: (Array.isArray(m.phases) ? m.phases : []).map(refGuid),
    };
  }

  // ---------- YAML writing ----------
  const q = s => JSON.stringify(String(s)).replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029');
  const fnum = v => { const n = Number(v) || 0; return String(n); };
  function header(fileId, scriptGuid, name, cls) {
    return `--- !u!114 &${fileId}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${scriptGuid}, type: 3}\n  m_Name: ${q(name)}\n  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.Core.${cls}\n`;
  }
  function fieldYaml(name, kind, v) {
    switch (kind) {
      case 'bool': return `  ${name}: ${v ? 1 : 0}\n`;
      case 'float': return `  ${name}: ${fnum(v)}\n`;
      case 'int': case 'enum': return `  ${name}: ${Math.round(Number(v) || 0)}\n`;
      case 'enumlist': return v.length ? `  ${name}: ${encodeIntList(v)}\n` : `  ${name}: \n`;
      case 'lines': return v.length ? `  ${name}:\n` + v.map(s => `  - ${q(s)}\n`).join('') : `  ${name}: []\n`;
      case 'ref': case 'speaker': return `  ${name}: ${v || '{fileID: 0}'}\n`;
      default: return `  ${name}: ${q(v)}\n`;
    }
  }
  function writePhaseAsset(ph) {
    let s = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';
    for (const n of ph.nodes) {
      const spec = NODE_TYPES[n.type];
      if (!n.fileId) n.fileId = rndFileId();
      s += header(n.fileId, spec.guid, n.objName || n.displayName || spec.label, n.type);
      s += `  nodeId: ${n.nodeId}\n  displayName: ${q(n.displayName)}\n  nodeEnabled: ${n.enabled ? 1 : 0}\n`;
      s += `  graphPosition: {x: ${fnum(n.pos.x)}, y: ${fnum(n.pos.y)}}\n`;
      const outs = n.outputs.filter(o => o.target);
      s += outs.length ? '  outputs:\n' + outs.map(o => `  - portName: ${o.port}\n    targetNodeId: ${o.target}\n    delaySeconds: ${fnum(o.delay)}\n`).join('') : '  outputs: []\n';
      for (const [name, kind] of spec.fields) s += fieldYaml(name, kind, n.fields[name]);
    }
    const gid = ph.graphFileId || '11400000';
    const entry = ph.nodes.find(n => n.nodeId === ph.entry);
    s += header(gid, GUID.QuestPhaseGraphSO, ph.assetName, 'QuestPhaseGraphSO');
    s += `  graphId: ${q(ph.graphId || ph.assetName)}\n  phaseName: ${q(ph.phaseName)}\n  phaseEnabled: ${ph.enabled ? 1 : 0}\n  runsUnderDeveloperUnlock: ${ph.underUnlock ? 1 : 0}\n`;
    s += `  designerNotes: ${q(ph.notes)}\n  entryNode: {fileID: ${entry ? entry.fileId : 0}}\n`;
    s += ph.nodes.length ? '  nodes:\n' + ph.nodes.map(n => `  - {fileID: ${n.fileId}}\n`).join('') : '  nodes: []\n';
    s += `  canvasScroll: {x: ${fnum(ph.canvasScroll ? ph.canvasScroll.x : 0)}, y: ${fnum(ph.canvasScroll ? ph.canvasScroll.y : 0)}}\n  canvasZoom: ${fnum(ph.canvasZoom || 1)}\n`;
    return s;
  }
  function writeQuestAsset(quest, phases) {
    let s = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';
    s += header('11400000', GUID.QuestSO, quest.assetName, 'QuestSO');
    s += `  questId: ${q(quest.questId || quest.assetName)}\n  questEnabled: ${quest.enabled ? 1 : 0}\n  runsUnderDeveloperUnlock: ${quest.underUnlock ? 1 : 0}\n  designerNotes: ${q(quest.notes)}\n`;
    const seq = phases.filter(p => p.inQuest);
    s += seq.length ? '  phases:\n' + seq.map(p => `  - {fileID: 11400000, guid: ${p.guid}, type: 2}\n`).join('') : '  phases: []\n';
    return s;
  }
  function writeMeta(guid) {
    return `fileFormatVersion: 2\nguid: ${guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
  }

  // ---------- the flow: the chain of "next" edges from the entry ----------
  function chain(ph) {
    const byId = new Map(ph.nodes.map(n => [n.nodeId, n]));
    const seen = new Set(), path = [];
    let cur = byId.get(ph.entry);
    while (cur && !seen.has(cur.nodeId)) {
      seen.add(cur.nodeId); path.push(cur);
      const e = cur.outputs.find(o => o.port === 'next');
      cur = e ? byId.get(e.target) : null;
    }
    return { path, off: ph.nodes.filter(n => !seen.has(n.nodeId)) };
  }
  // Rewrites the next-edges so the given ordered list is the path (keeps each delay).
  function relink(ph, ordered) {
    ordered.forEach((n, k) => {
      const nxt = ordered[k + 1];
      let e = n.outputs.find(o => o.port === 'next');
      const spec = NODE_TYPES[n.type];
      if (!nxt || spec.terminal) { n.outputs = n.outputs.filter(o => o.port !== 'next'); return; }
      if (!e) { e = { port: 'next', target: '', delay: 0 }; n.outputs.push(e); }
      e.target = nxt.nodeId;
    });
    ph.entry = ordered[0] ? ordered[0].nodeId : '';
    const off = ph.nodes.filter(n => !ordered.includes(n));
    ph.nodes = ordered.concat(off);
    layoutRows(ordered);
  }
  // Mirrors QuestGraphLayout's venue rows: a new row at each declared app-shell / gameplay switch.
  function layoutRows(ordered) {
    let row = 0, col = 0, venue = null;
    for (const n of ordered) {
      const s = NODE_TYPES[n.type];
      if (s.venue && venue && s.venue !== venue && col > 0) { row++; col = 0; }
      if (s.venue) venue = s.venue;
      n.pos = { x: 60 + col * 380, y: 40 + row * 220 };
      col++;
      if (s.venueAfter && s.venueAfter !== venue) { venue = s.venueAfter; row++; col = 0; }
    }
  }
  function newNode(type, overrides) {
    const spec = NODE_TYPES[type];
    const fields = {};
    for (const [name, , def] of spec.fields) fields[name] = clone(def);
    Object.assign(fields, (overrides && overrides.fields) || {});
    return { fileId: '', type, nodeId: rndHex(32), displayName: (overrides && overrides.displayName) || spec.label, enabled: 1, pos: { x: 0, y: 0 }, outputs: [], fields };
  }

  // ---------- validation (mirrors the editor's checks plus the LOCKED rules) ----------
  function validate(ph) {
    const out = [];
    const add = (level, msg, nodeId) => out.push({ level, msg, nodeId });
    const ids = new Set(ph.nodes.map(n => n.nodeId));
    const { path, off } = chain(ph);
    if (!ph.nodes.length) add('error', 'This phase has no steps.');
    if (ph.nodes.length && !ids.has(ph.entry)) add('error', 'The phase has no entry step.');
    if (path.length && !NODE_TYPES[path[path.length - 1].type].terminal) add('error', 'The path does not end on Phase End or Quest End, so the phase never completes.');
    for (const n of off) if (n.enabled) add('warn', `"${n.displayName}" is not on the path and never runs.`, n.nodeId);
    for (const n of ph.nodes) {
      const s = NODE_TYPES[n.type], f = n.fields;
      for (const o of n.outputs) if (o.target && !ids.has(o.target)) add('error', `"${n.displayName}" points at a step that does not exist.`, n.nodeId);
      if (n.type === 'QuestDialogueNode' && !(f.lines || []).length) add('error', `"${n.displayName}" has no lines.`, n.nodeId);
      if (n.type === 'QuestShowInstructionNode' && !f.text && !f.panelKey) add('warn', `"${n.displayName}" shows no text.`, n.nodeId);
      if (s.legacy) add('warn', `"${n.displayName}" carries the player (${s.label}). The guided-path rule says spotlight the one control and let them press it; use Guide to Microgame or a Wait step.`, n.nodeId);
      if (n.type === 'QuestExitFreestyleNode' && f.forceExit) add('warn', `"${n.displayName}" forces the exit, which carries the player.`, n.nodeId);
      if (s.lock && s.lock(f) && ph.underUnlock) add('info', `"${n.displayName}" applies a lock and passes straight through while the developer unlock is on.`, n.nodeId);
      if (n.type === 'QuestWaitForGamePlayedNode' && f.filterByMode && !f.expectedMode) add('warn', `"${n.displayName}" filters by mode but the mode is Random.`, n.nodeId);
      for (const line of (f.lines || [])) if (line.length > 140) add('info', `A line in "${n.displayName}" is ${line.length} characters; the panel reads best under 140.`, n.nodeId);
    }
    return out;
  }

  const api = { ENUMS, NODE_TYPES, GUID, SPEAKERS, parsePhaseAsset, parseQuestAsset, writePhaseAsset, writeQuestAsset, writeMeta, chain, relink, layoutRows, newNode, validate, rndHex, rndFileId, decodeIntList, encodeIntList, splitDocs, parseDoc };
  if (typeof module !== 'undefined') module.exports = api; else root.FTUE = api;
})(typeof window !== 'undefined' ? window : globalThis);
